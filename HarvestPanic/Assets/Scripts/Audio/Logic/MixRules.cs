using HarvestPanic.Core;

namespace HarvestPanic.Audio
{
    /// <summary>
    /// Ducking matrix (audio-feedback Core Rule 1, sound-bible §4.3) as pure state. Feed inputs each frame,
    /// read the smoothed per-group duck in dB. Attack 80 ms / release 600 ms for voice; stinger 10 ms / 300 ms.
    /// </summary>
    public sealed class DuckingState
    {
        public const float VoiceDuckMusicDb = -6f, VoiceDuckCrittersDb = -3f, VoiceDuckAmbienceDb = -3f;
        public const float StingerDuckMusicDb = -9f, CutDb = -80f;
        public const float VoiceAttack = 0.08f, VoiceRelease = 0.6f, StingerAttack = 0.01f, StingerRelease = 0.3f;

        float _musicVoice, _crittersVoice, _ambVoice, _musicStinger;

        /// <summary>True while a nearby player is speaking.</summary>
        public bool VoiceActive;
        /// <summary>True while a stinger plays.</summary>
        public bool StingerActive;
        /// <summary>True during the 0.6 s disaster silence (hard cut, no smoothing).</summary>
        public bool MusicCut;

        /// <summary>Target dB of each duck for the current inputs (no smoothing).</summary>
        public static float TargetDb(MixGroup g, bool voice, bool stinger, bool cut)
        {
            switch (g)
            {
                case MixGroup.Music:
                    if (cut) return CutDb;
                    return (voice ? VoiceDuckMusicDb : 0f) + (stinger ? StingerDuckMusicDb : 0f);
                case MixGroup.Critters: return voice ? VoiceDuckCrittersDb : 0f;
                case MixGroup.Ambience: return voice ? VoiceDuckAmbienceDb : 0f;
                default: return 0f; // Voice is never ducked; impacts/player/UI untouched.
            }
        }

        static float Step(float cur, float target, float dt, float attack, float release)
        {
            // Going more negative = attack.
            float tau = target < cur ? attack : release;
            return cur + (target - cur) * AudioMath.SmoothAlpha(dt, tau);
        }

        /// <summary>Advances the smoothers.</summary>
        public void Tick(float dt)
        {
            _musicVoice = Step(_musicVoice, VoiceActive ? VoiceDuckMusicDb : 0f, dt, VoiceAttack, VoiceRelease);
            _crittersVoice = Step(_crittersVoice, VoiceActive ? VoiceDuckCrittersDb : 0f, dt, VoiceAttack, VoiceRelease);
            _ambVoice = Step(_ambVoice, VoiceActive ? VoiceDuckAmbienceDb : 0f, dt, VoiceAttack, VoiceRelease);
            _musicStinger = Step(_musicStinger, StingerActive ? StingerDuckMusicDb : 0f, dt, StingerAttack, StingerRelease);
        }

        /// <summary>Current smoothed duck in dB for <paramref name="g"/>.</summary>
        public float CurrentDb(MixGroup g) => g switch
        {
            MixGroup.Music => MusicCut ? CutDb : _musicVoice + _musicStinger,
            MixGroup.Critters => _crittersVoice,
            MixGroup.Ambience => _ambVoice,
            _ => 0f,
        };
    }

    /// <summary>Disaster stinger rules (audio-feedback Core Rule 6 + Edge Case "multiple disasters within 3 s").</summary>
    public sealed class DisasterStingerRules
    {
        public const float SilenceSeconds = 0.6f, RepeatWindow = 3f;
        public const int RecordScratchSeverity = 80;
        double _lastAt = double.NegativeInfinity;

        /// <summary>Stinger event for a verb.</summary>
        public static string StingerFor(BlameVerb v) => v switch
        {
            BlameVerb.DroppedInRiver or BlameVerb.Drowned or BlameVerb.LassoedIntoRiver => AudioEvents.StgRiver,
            BlameVerb.BrokePlank => AudioEvents.StgPlank,
            BlameVerb.TippedWagon or BlameVerb.Crashed or BlameVerb.SpilledCargo => AudioEvents.StgWagon,
            _ => AudioEvents.StgGeneric,
        };

        /// <summary>
        /// Decides what a disaster at <paramref name="now"/> plays. <paramref name="full"/> = true: music cut + stinger
        /// (+ record scratch when <paramref name="scratch"/>); false: sweetener only.
        /// </summary>
        public void Decide(BlameVerb verb, int severity, double now, out bool full, out string stinger, out bool scratch)
        {
            full = now - _lastAt > RepeatWindow;
            _lastAt = now;
            stinger = full ? StingerFor(verb) : AudioEvents.StgSweetener;
            scratch = full && severity >= RecordScratchSeverity;
        }
    }

    /// <summary>Wallet cue rules (audio-feedback Core Rules 3, 8).</summary>
    public static class WalletCues
    {
        public const float TickSpacing = 0.06f;

        /// <summary>Main cue for a transaction (null for zero), and the coin-tick count for losses.</summary>
        public static string CueFor(int amount, out int ticks)
        {
            ticks = 0;
            if (amount > 0) return AudioEvents.KaChing;
            if (amount < 0) { ticks = AudioMath.CoinTicks(-amount); return AudioEvents.WalletWah; }
            return null;
        }
    }

    /// <summary>Phase -> music cue (sound-bible §2).</summary>
    public static class MusicRules
    {
        public const float CrunchSeconds = 60f;

        /// <summary>Loop for a day phase; Workday becomes Crunch in the last 60 s. Null = silence.</summary>
        public static string CueFor(string phase, float remaining, out bool pocketWatch)
        {
            pocketWatch = false;
            switch (phase)
            {
                case "Morning": return AudioEvents.MusMorning;
                case "Workday":
                    if (remaining > 0f && remaining <= CrunchSeconds) { pocketWatch = true; return AudioEvents.MusCrunch; }
                    return AudioEvents.MusRoundup;
                case "Dusk": return AudioEvents.MusDusk;
                case "Payout": return AudioEvents.MusPayout;
                case "Sunday": return AudioEvents.MusSunday;
                case "Evicted": return AudioEvents.MusSunday;
                default: return null;
            }
        }

        /// <summary>Crunch pocket-watch tick interval: eighth notes at 152 BPM would be 0.197 s; we tick quarters (0.395 s).</summary>
        public const float PocketWatchInterval = 60f / 152f;
    }

    /// <summary>Voice stealing (sound-bible §4.4): footsteps steal farthest, impacts steal quietest.</summary>
    public static class VoiceStealing
    {
        /// <summary>
        /// Index of the voice to steal among <paramref name="count"/> candidates, or -1 to reject the new sound
        /// (new one is farther/quieter than every playing voice). Distances in m, gains linear.
        /// </summary>
        public static int PickVictim(VoiceLimit limit, float[] distances, float[] gains, int count, float newDistance, float newGain)
        {
            int best = -1;
            if (limit == VoiceLimit.Footsteps)
            {
                float far = newDistance;
                for (int i = 0; i < count; i++) if (distances[i] > far) { far = distances[i]; best = i; }
            }
            else if (limit == VoiceLimit.Impacts)
            {
                float quiet = newGain;
                for (int i = 0; i < count; i++) if (gains[i] < quiet) { quiet = gains[i]; best = i; }
            }
            return best; // Stingers: never steal (new one is downgraded by the caller).
        }
    }
}
