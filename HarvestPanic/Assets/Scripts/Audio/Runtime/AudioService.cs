using System;
using System.Collections.Generic;
using HarvestPanic.Core;
using Unity.Netcode;
using UnityEngine;

namespace HarvestPanic.Audio
{
    /// <summary>
    /// Client-local audio router (design/gdd/audio-feedback.md, design/audio/sound-bible.md). Created automatically
    /// after the first scene loads and registered as <see cref="IAudioService"/> + <see cref="IAudioSettingsSink"/>.
    /// Buses are software gains (Master x bus x duck) applied to pooled AudioSources each frame, so no mixer asset is
    /// needed and tests can read the bus of every voice. Listens to <see cref="INoiseService.OnNoise"/> (server) and
    /// economy events (replicated by Economy), and relays noise cues to clients via <see cref="AudioNetRelay"/>.
    /// </summary>
    [DefaultExecutionOrder(-400)]
    public sealed class AudioService : MonoBehaviour, IAudioService
    {
        public const int PoolSize = 40;
        public const float VoiceDuckRadius = 15f, ShoutDuckHold = 0.6f;
        const string PrefsKey = "HarvestPanic.Audio.Bus.";

        /// <summary>Record of one started voice (tests, debug overlay).</summary>
        public struct PlayRecord { public string Event; public AudioBus Bus; public MixGroup Group; public Vector3 Position; public bool Spatial; public float Time; }

        sealed class Voice
        {
            public AudioSource Src;
            public bool Busy;
            public AudioEventDef Def;
            public float BaseLinear;
            public float Distance;
            public float StartTime;
        }

        readonly Voice[] _voices = new Voice[PoolSize];
        readonly float[] _busVol = new float[5];
        readonly List<PlayRecord> _history = new List<PlayRecord>(64);
        readonly List<(float at, string evt, float scale)> _scheduled = new List<(float, string, float)>();
        readonly float[] _tmpDist = new float[16], _tmpGain = new float[16];
        readonly int[] _tmpIdx = new int[16];
        readonly DisasterStingerRules _disasterRules = new DisasterStingerRules();

        public readonly AudioClipLibrary Library = new AudioClipLibrary();
        public readonly DuckingState Ducking = new DuckingState();
        public MusicDirector Music { get; private set; }

        bool _voiceReported;
        float _shoutUntil;
        Transform _listener;
        float _nextListenerLookup;
        INoiseService _noise;
        IEconomyService _economy;
        readonly Queue<float> _recentDisasters = new Queue<float>();
        readonly Queue<float> _recentPanics = new Queue<float>();

        /// <summary>Last 64 started voices, oldest first.</summary>
        public IReadOnlyList<PlayRecord> History => _history;
        /// <summary>Voices currently playing (one-shots only).</summary>
        public int ActiveVoiceCount { get { int n = 0; foreach (var v in _voices) if (v.Busy) n++; return n; } }
        /// <summary>Overrides the listener (tests); otherwise Camera.main.</summary>
        public Transform ListenerOverride;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (Services.TryGet(out IAudioService _)) return;
            var go = new GameObject("[AudioService]");
            DontDestroyOnLoad(go);
            go.AddComponent<AudioService>();
        }

        void Awake()
        {
            for (int b = 0; b < _busVol.Length; b++)
                _busVol[b] = PlayerPrefs.GetFloat(PrefsKey + (AudioBus)b, b == (int)AudioBus.Music ? 0.8f : 1f);
            for (int i = 0; i < PoolSize; i++)
            {
                var child = new GameObject("Voice" + i);
                child.transform.SetParent(transform, false);
                var src = child.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.dopplerLevel = 0f;
                src.rolloffMode = AudioRolloffMode.Logarithmic;
                _voices[i] = new Voice { Src = src };
            }
            Music = new MusicDirector(this, gameObject);
        }

        void OnEnable()
        {
            Services.Register<IAudioService>(this);
            Services.Register<IAudioSettingsSink>(this);
        }

        void OnDisable()
        {
            Services.Unregister<IAudioService>(this);
            Services.Unregister<IAudioSettingsSink>(this);
            Bind(ref _noise, null);
            BindEconomy(null);
        }

        // ------------------------------------------------------------------ settings

        public float GetBusVolume(AudioBus bus) => _busVol[(int)bus];

        public void SetBusVolume(AudioBus bus, float linear01)
        {
            float v = float.IsNaN(linear01) ? 1f : Mathf.Clamp01(linear01);
            if (Mathf.Approximately(_busVol[(int)bus], v)) return;
            _busVol[(int)bus] = v;
            PlayerPrefs.SetFloat(PrefsKey + bus, v);
        }

        /// <summary>Master x bus gain (linear).</summary>
        public float BusGain(AudioBus bus) => bus == AudioBus.Master ? _busVol[0] : _busVol[0] * _busVol[(int)bus];

        /// <summary>Full software gain for a mix group: bus x duck.</summary>
        public float GroupGain(MixGroup g) => BusGain(AudioEvents.BusOf(g)) * AudioMath.DbToLinear(Ducking.CurrentDb(g));

        // ------------------------------------------------------------------ playback

        public void ReportVoiceActivity(bool anyNearbySpeaking) => _voiceReported = anyNearbySpeaking;

        public bool PlayAt(string eventName, Vector3 position, float volumeScale = 1f) => Play(eventName, position, true, volumeScale, 0f);

        public bool Play2D(string eventName, float volumeScale = 1f) => Play(eventName, Vector3.zero, false, volumeScale, 0f);

        /// <summary>Plays a routed cue locally (3D unless its preset is 2D).</summary>
        public bool PlayCue(in SoundCue cue) => Play(cue.Event, cue.Position, true, 1f, cue.GainDb);

        /// <summary>Plays <paramref name="evt"/> after <paramref name="delay"/> seconds (2D).</summary>
        public void Schedule2D(string evt, float delay, float scale = 1f) => _scheduled.Add((Time.unscaledTime + delay, evt, scale));

        Vector3 ListenerPos(out bool has)
        {
            if (ListenerOverride != null) { has = true; return ListenerOverride.position; }
            if (_listener == null && Time.unscaledTime >= _nextListenerLookup)
            {
                _nextListenerLookup = Time.unscaledTime + 0.5f;
                var cam = Camera.main;
                _listener = cam != null ? cam.transform : null;
            }
            has = _listener != null;
            return has ? _listener.position : Vector3.zero;
        }

        static float RolloffEstimate(float d, float min) => d <= min ? 1f : min / d;

        bool Play(string evt, Vector3 pos, bool spatialRequested, float scale, float gainDb)
        {
            if (!AudioEvents.TryGet(evt, out var def)) return false;
            bool spatial = spatialRequested && def.Attenuation != AttenuationPreset.TwoD;
            AudioEvents.Range(def.Attenuation, out float min, out float max);
            float dist = 0f;
            if (spatial)
            {
                var lp = ListenerPos(out bool has);
                if (has) { dist = Vector3.Distance(lp, pos); if (dist > max) return false; }
            }
            var clip = Library.Pick(evt);
            if (clip == null) return false;
            float baseLin = AudioMath.DbToLinear(def.BaseDb + gainDb) * Mathf.Max(0f, scale);

            Voice slot = null;
            int cap = AudioEvents.CapOf(def.Limit);
            if (cap > 0)
            {
                int n = 0;
                for (int i = 0; i < PoolSize && n < _tmpIdx.Length; i++)
                {
                    var v = _voices[i];
                    if (!v.Busy || v.Def.Limit != def.Limit) continue;
                    _tmpIdx[n] = i; _tmpDist[n] = v.Distance; _tmpGain[n] = v.BaseLinear * RolloffEstimate(v.Distance, min);
                    n++;
                }
                if (n >= cap)
                {
                    int victim = VoiceStealing.PickVictim(def.Limit, _tmpDist, _tmpGain, n, dist, baseLin * RolloffEstimate(dist, min));
                    if (victim < 0) return false;
                    slot = _voices[_tmpIdx[victim]];
                    slot.Src.Stop();
                }
            }
            if (slot == null) slot = FreeVoice();
            if (slot == null) return false;

            var s = slot.Src;
            s.clip = clip;
            s.loop = false;
            s.spatialBlend = spatial ? 1f : 0f;
            s.minDistance = min; s.maxDistance = max;
            s.transform.position = pos;
            s.pitch = def.PitchJitterSemis > 0f ? AudioMath.SemitonesToPitch(Library.RandomRange(-def.PitchJitterSemis, def.PitchJitterSemis)) : 1f;
            slot.Busy = true; slot.Def = def; slot.BaseLinear = baseLin; slot.Distance = dist; slot.StartTime = Time.unscaledTime;
            s.volume = baseLin * GroupGain(def.Group);
            s.Play();

            if (_history.Count >= 64) _history.RemoveAt(0);
            _history.Add(new PlayRecord { Event = evt, Bus = AudioEvents.BusOf(def.Group), Group = def.Group, Position = pos, Spatial = spatial, Time = Time.unscaledTime });
            return true;
        }

        Voice FreeVoice()
        {
            Voice oldest = null;
            foreach (var v in _voices)
            {
                if (!v.Busy) return v;
                if (v.Def.Limit != VoiceLimit.Stingers && (oldest == null || v.StartTime < oldest.StartTime)) oldest = v;
            }
            if (oldest != null) oldest.Src.Stop();
            return oldest;
        }

        /// <summary>True while any stinger voice is playing.</summary>
        public bool StingerPlaying
        {
            get { foreach (var v in _voices) if (v.Busy && v.Def.Limit == VoiceLimit.Stingers) return true; return false; }
        }

        // ------------------------------------------------------------------ frame

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            float now = Time.unscaledTime;

            if (Services.TryGet(out INoiseService n)) { if (!ReferenceEquals(n, _noise)) Bind(ref _noise, n); }
            else if (_noise != null) Bind(ref _noise, null);
            Services.TryGet(out IEconomyService e);
            if (!ReferenceEquals(e, _economy)) BindEconomy(e);

            for (int i = _scheduled.Count - 1; i >= 0; i--)
                if (now >= _scheduled[i].at) { var s = _scheduled[i]; _scheduled.RemoveAt(i); Play2D(s.evt, s.scale); }

            Ducking.VoiceActive = _voiceReported || now < _shoutUntil;
            Ducking.StingerActive = StingerPlaying;
            Music.Tick(dt, now, ChaosNow(now));
            Ducking.MusicCut = Music.IsCut;
            Ducking.Tick(dt);

            foreach (var v in _voices)
            {
                if (!v.Busy) continue;
                if (!v.Src.isPlaying) { v.Busy = false; continue; }
                v.Src.volume = v.BaseLinear * GroupGain(v.Def.Group);
            }
        }

        float ChaosNow(float now)
        {
            while (_recentDisasters.Count > 0 && now - _recentDisasters.Peek() > 60f) _recentDisasters.Dequeue();
            while (_recentPanics.Count > 0 && now - _recentPanics.Peek() > 10f) _recentPanics.Dequeue();
            // No fleeing-critter count is exposed through Core, so recent squeals (10 s) stand in for FleeingCritters.
            return AudioMath.ChaosLevel(Mathf.Min(8, _recentPanics.Count), 0, _recentDisasters.Count);
        }

        // ------------------------------------------------------------------ noise

        void Bind(ref INoiseService field, INoiseService next)
        {
            if (field != null) field.OnNoise -= HandleNoise;
            field = next;
            if (field != null) field.OnNoise += HandleNoise;
        }

        /// <summary>Server side: route a noise, play it here and relay it to clients.</summary>
        public void HandleNoise(NoiseEvent e)
        {
            AudioNetRelay.BroadcastNoise(e); // polish-2: clients route + caption themselves (also unrouted noises)
            RouteAndPlay(e);
        }

        /// <summary>Client side (polish-2): a noise relayed by the server; route and play it here only.</summary>
        public void HandleRemoteNoise(NoiseEvent e) => RouteAndPlay(e);

        void RouteAndPlay(in NoiseEvent e)
        {
            var lp = ListenerPos(out bool has);
            if ((e.Source == NoiseSource.Shout || e.Source == NoiseSource.Voice) && (!has || Vector3.Distance(lp, e.Location) <= VoiceDuckRadius))
                _shoutUntil = Time.unscaledTime + ShoutDuckHold;
            string critter = e.Source == NoiseSource.Critter || (e.Tags & NoiseTag.Squeal) != 0 ? CritterKey(e.InstigatorId) : null;
            if (!NoiseRouting.TryRoute(e, critter, out var cue)) return;
            OnCueHeard(cue);
            PlayCue(cue);
        }

        /// <summary>Bookkeeping shared by locally routed and relayed cues (chaos estimate).</summary>
        public void OnCueHeard(in SoundCue cue)
        {
            if (cue.Event == AudioEvents.PigSqueal || cue.Event == AudioEvents.ChickenPanic) _recentPanics.Enqueue(Time.unscaledTime);
        }

        static string CritterKey(ulong networkObjectId)
        {
            if (networkObjectId == 0) return null;
            var nm = NetworkManager.Singleton;
            if (nm == null || nm.SpawnManager == null) return null;
            if (!nm.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out var no) || no == null) return null;
            var goods = no.GetComponentInChildren<IGoods>();
            string id = goods != null ? goods.GoodsId : null;
            return string.IsNullOrEmpty(id) ? no.name : id;
        }

        // ------------------------------------------------------------------ economy

        void BindEconomy(IEconomyService next)
        {
            if (_economy != null) { _economy.OnWalletTxn -= HandleWalletTxn; _economy.OnDisaster -= HandleDisaster; }
            _economy = next;
            if (_economy != null) { _economy.OnWalletTxn += HandleWalletTxn; _economy.OnDisaster += HandleDisaster; }
        }

        /// <summary>Wallet ka-ching / wah + coin ticks (raised on host and, via Economy's RPC, on clients).</summary>
        public void HandleWalletTxn(WalletTxn t)
        {
            var cue = WalletCues.CueFor(t.Amount, out int ticks);
            if (cue == null) return;
            Play2D(cue);
            for (int i = 0; i < ticks; i++) Schedule2D(AudioEvents.CoinTick, WalletCues.TickSpacing * (i + 1));
        }

        /// <summary>Disaster: music hard-cut + stinger (+ record scratch), or sweetener only within 3 s.</summary>
        public void HandleDisaster(BlameEvent e)
        {
            float now = Time.unscaledTime;
            _recentDisasters.Enqueue(now);
            _disasterRules.Decide(e.Verb, e.Severity, now, out bool full, out string stinger, out bool scratch);
            if (!full) { Play2D(stinger); return; }
            Music.TriggerDisasterCut();
            if (scratch) Play2D(AudioEvents.StgRecordScratch);
            if (!Play2D(stinger)) Play2D(AudioEvents.StgSweetener);
        }
    }
}
