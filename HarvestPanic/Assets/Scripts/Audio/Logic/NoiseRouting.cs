using HarvestPanic.Core;

namespace HarvestPanic.Audio
{
    /// <summary>A routed, playable sound: event, world position and extra gain in dB.</summary>
    public struct SoundCue
    {
        public string Event;
        public UnityEngine.Vector3 Position;
        public float GainDb;
    }

    /// <summary>NoiseEvent -> sound event table (pure; tested in EditMode).</summary>
    public static class NoiseRouting
    {
        /// <summary>
        /// Maps a server noise to a sound cue. <paramref name="instigatorIsChicken"/> is resolved by the caller
        /// (critter personality lookup). Returns false for noises with no sound (voice: players hear the real voice).
        /// </summary>
        public static bool TryRoute(in NoiseEvent e, bool instigatorIsChicken, out SoundCue cue) =>
            TryRoute(e, instigatorIsChicken ? "chicken" : null, out cue);

        /// <summary>Species-aware routing: <paramref name="critterKey"/> is the instigator's goods/critter id (any case), or null.</summary>
        public static bool TryRoute(in NoiseEvent e, string critterKey, out SoundCue cue)
        {
            cue = new SoundCue { Position = e.Location, GainDb = 0f };
            var t = e.Tags;
            if ((t & NoiseTag.PlankBreak) != 0) cue.Event = AudioEvents.PlankBreak;
            else if ((t & NoiseTag.Creak) != 0) cue.Event = AudioEvents.PlankCreak;
            else if ((t & NoiseTag.Splash) != 0) cue.Event = e.Loudness >= 0.6f ? AudioEvents.SplashBig : AudioEvents.SplashSmall;
            else if (e.Source == NoiseSource.Footstep) { cue.Event = AudioEvents.FootstepDirt; cue.GainDb = LoudnessDb(e.Loudness, 0.5f); }
            else if (e.Source == NoiseSource.Shout) cue.Event = AudioEvents.PlayerShout;
            else if (e.Source == NoiseSource.Critter || (t & NoiseTag.Squeal) != 0)
                cue.Event = CritterEvent(critterKey);
            else if ((t & NoiseTag.Metal) != 0) cue.Event = AudioEvents.ImpactMetal;
            else if (e.Source == NoiseSource.Tool) cue.Event = AudioEvents.ToolMetal;
            else if (e.Source == NoiseSource.Impact || e.Source == NoiseSource.Hazard || e.Source == NoiseSource.Vehicle)
            { cue.Event = AudioEvents.ImpactCrate; cue.GainDb = LoudnessDb(e.Loudness, 0.5f); }
            else return false;
            return true;
        }

        /// <summary>Critter vocal event for a goods/critter id (pig squeal is the default).</summary>
        public static string CritterEvent(string critterKey)
        {
            if (string.IsNullOrEmpty(critterKey)) return AudioEvents.PigSqueal;
            string k = critterKey.ToLowerInvariant();
            if (k.Contains("chicken")) return AudioEvents.ChickenPanic;
            if (k.Contains("cow")) return AudioEvents.CowMoo;
            if (k.Contains("goat")) return AudioEvents.GoatBleat;
            return AudioEvents.PigSqueal;
        }

        /// <summary>
        /// Loudness 0..1 to a relative gain: <paramref name="reference"/> loudness = 0 dB, every halving ~ -6 dB,
        /// clamped to [-18, +6] dB so quiet impacts stay audible and loud ones never clip the bus.
        /// </summary>
        public static float LoudnessDb(float loudness, float reference)
        {
            if (loudness <= 0f) return -18f;
            float db = AudioMath.LinearToDb(loudness / reference);
            return db < -18f ? -18f : db > 6f ? 6f : db;
        }
    }
}
