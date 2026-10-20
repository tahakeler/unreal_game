using HarvestPanic.Core;
using UnityEngine;

namespace HarvestPanic.Audio
{
    /// <summary>
    /// Phase-driven music (sound-bible §2-3). Reads <see cref="IDayPhaseInfo"/> each frame, crossfades between loops
    /// (1.5 s), adds the "_lead" stem at MusicLayer >= 2, ticks the pocket watch in Crunch, rings the sundown bell when
    /// Workday ends, runs the farm-day ambience bed, and performs the disaster cut (0.6 s silence, resume one layer lower
    /// for 8 s). Not bar-quantized yet (placeholder loops; no Quartz equivalent wired) — transitions are crossfades.
    /// </summary>
    public sealed class MusicDirector
    {
        public const float Crossfade = 1.5f, PostDisasterPenaltySeconds = 8f, LayerFade = 2f;

        readonly AudioService _svc;
        readonly AudioSource _a, _b, _lead, _amb;
        AudioSource _active;
        float _activeFade, _otherFade, _leadFade, _ambFade;
        string _cue;
        string _phase = "None";
        float _cutUntil = -1f, _penaltyUntil = -1f, _nextTick;
        bool _throatCleared;

        /// <summary>Current loop event (null = silence).</summary>
        public string CurrentCue => _cue;
        /// <summary>Current phase name read from <see cref="IDayPhaseInfo"/>.</summary>
        public string Phase => _phase;
        /// <summary>True during the disaster silence.</summary>
        public bool IsCut => Time.unscaledTime < _cutUntil;
        /// <summary>Effective music layer 1..3 after the post-disaster penalty.</summary>
        public int Layer { get; private set; } = 1;
        /// <summary>Phase override for tests/tools (null = use the service).</summary>
        public IDayPhaseInfo PhaseSource;

        public MusicDirector(AudioService svc, GameObject host)
        {
            _svc = svc;
            _a = MakeLoop(host, "MusicA"); _b = MakeLoop(host, "MusicB"); _lead = MakeLoop(host, "MusicLead"); _amb = MakeLoop(host, "Ambience");
            _active = _a;
        }

        static AudioSource MakeLoop(GameObject host, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(host.transform, false);
            var s = go.AddComponent<AudioSource>();
            s.loop = true; s.playOnAwake = false; s.spatialBlend = 0f; s.volume = 0f; s.priority = 0;
            return s;
        }

        /// <summary>Starts the disaster cut now.</summary>
        public void TriggerDisasterCut()
        {
            float now = Time.unscaledTime;
            _cutUntil = now + DisasterStingerRules.SilenceSeconds;
            _penaltyUntil = _cutUntil + PostDisasterPenaltySeconds;
            if (!_throatCleared) { _throatCleared = true; _svc.Schedule2D(AudioEvents.StgThroatClear, DisasterStingerRules.SilenceSeconds + 1.2f); }
        }

        public void Tick(float dt, float now, float chaos)
        {
            IDayPhaseInfo info = PhaseSource;
            if (info == null) Services.TryGet(out info);
            string phase = info != null ? info.PhaseName ?? "None" : "None";
            float remaining = info != null ? info.PhaseRemaining : 0f;

            if (phase != _phase)
            {
                if (_phase == "Workday" && (phase == "Dusk" || phase == "Payout")) _svc.Play2D(AudioEvents.SundownBell);
                if (phase == "Morning") _throatCleared = false;
                _phase = phase;
            }

            string cue = MusicRules.CueFor(phase, remaining, out bool watch);
            if (cue != _cue) SwitchTo(cue);

            if (watch && now >= _nextTick) { _nextTick = now + MusicRules.PocketWatchInterval; _svc.Play2D(AudioEvents.PocketWatchTick); }

            int layer = AudioMath.MusicLayer(chaos);
            if (now < _penaltyUntil) layer = Mathf.Max(1, layer - 1);
            Layer = layer;

            float step = dt / Crossfade;
            _activeFade = Mathf.MoveTowards(_activeFade, _cue != null ? 1f : 0f, step);
            _otherFade = Mathf.MoveTowards(_otherFade, 0f, step);
            _leadFade = Mathf.MoveTowards(_leadFade, _cue != null && layer >= 2 && _lead.clip != null ? 1f : 0f, dt / LayerFade);
            bool ambient = phase == "Morning" || phase == "Workday" || phase == "Payout";
            _ambFade = Mathf.MoveTowards(_ambFade, ambient ? 1f : 0f, dt / 3f);

            float musicGain = _svc.GroupGain(MixGroup.Music) * AudioMath.DbToLinear(-14f) * 3f; // loops are mastered hot (-14 dB target + 9.5 dB headroom)
            var other = _active == _a ? _b : _a;
            _active.volume = musicGain * _activeFade;
            other.volume = musicGain * _otherFade;
            _lead.volume = musicGain * _leadFade * _activeFade;
            if (other.isPlaying && _otherFade <= 0f) other.Stop();
            EnsurePlaying(_amb, AudioEvents.AmbFarmDay, _ambFade > 0f);
            _amb.volume = _svc.GroupGain(MixGroup.Ambience) * AudioMath.DbToLinear(-18f) * 3f * _ambFade;
        }

        void EnsurePlaying(AudioSource s, string evt, bool on)
        {
            if (on && !s.isPlaying)
            {
                if (s.clip == null) s.clip = _svc.Library.Pick(evt);
                if (s.clip != null) s.Play();
            }
            else if (!on && s.isPlaying) s.Stop();
        }

        void SwitchTo(string cue)
        {
            _cue = cue;
            var next = _active == _a ? _b : _a;
            _otherFade = _activeFade; // old becomes the fading one
            _active = next;
            _activeFade = 0f;
            _lead.Stop(); _lead.clip = null; _leadFade = 0f;
            if (cue == null) { next.Stop(); return; }
            var baseClip = _svc.Library.Stem(cue, "_base") ?? _svc.Library.Pick(cue);
            next.clip = baseClip;
            if (baseClip != null) next.Play();
            var lead = _svc.Library.Stem(cue, "_lead");
            if (lead != null && baseClip != null)
            {
                _lead.clip = lead;
                _lead.PlayScheduled(AudioSettings.dspTime + 0.05);
                next.Stop(); next.PlayScheduled(AudioSettings.dspTime + 0.05); // stems start on the same DSP tick
            }
        }
    }
}
