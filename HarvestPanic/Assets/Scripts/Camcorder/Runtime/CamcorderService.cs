using System;
using System.Collections.Generic;
using HarvestPanic.Core;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace HarvestPanic.Camcorder
{
    /// <summary>
    /// Grandpa's Camcorder recorder (design/gdd/camcorder-replay.md, GDD 12.7). Client-local: every peer samples the
    /// transforms of players, critters, carryables (NGO spawned objects classified by layer) and bridge planks (Bridge layer)
    /// into a preallocated <see cref="ReplayRingBuffer"/> at <c>tickHz</c>, records disasters (IEconomyService.OnDisaster,
    /// raised on every client) and server noise cues, and cuts tapes per <see cref="CaptureRules"/>.
    /// Auto-created after the first scene load; registered as <see cref="ICamcorderService"/>.
    /// Steady-state sampling allocates nothing; scene scans (every <c>rescanSeconds</c>) allocate only for new actors.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public sealed class CamcorderService : MonoBehaviour, ICamcorderService
    {
        sealed class Tracked
        {
            public int Id, Key;
            public Transform T;
            public TrackKind Kind;
            public ulong Owner = PlayerIds.None;
            public Renderer[] Renderers;
            public bool Explicit;
        }

        sealed class PendingTape
        {
            public long Id;
            public bool Manual;
            public byte Severity;
            public int DollarDelta, InstigatorCount, Extra;
            public BlameVerb Verb;
            public ulong Primary = PlayerIds.None;
            public Vector3 Focus;
            public string Title = "";
            public float TriggerClock;
            public Texture2D Thumb;
        }

        public static CamcorderService Instance { get; private set; }

        public CamcorderTuning Tuning { get; private set; }
        public ReplayRingBuffer Ring { get; private set; }
        public ReplayEventRing EventRing { get; private set; }
        public CaptureRules Rules { get; private set; }
        public MomentShelf Shelf { get; } = new MomentShelf();
        public ReplayViewer Viewer { get; private set; }
        public CamcorderHud Hud { get; private set; }
        /// <summary>Recording clock (scaled game time while recording; stops while paused solo or viewing).</summary>
        public float Clock { get; private set; }
        public int TrackedCount => _tracked.Count;
        public int DayIndex { get; private set; }

        /// <summary>Raised when a tape is cut (new or re-cut after an extension).</summary>
        public event Action<Moment> MomentSaved;
        /// <summary>Raised for short player-facing messages (HUD toast).</summary>
        public event Action<string> Toast;
        /// <summary>Raised when the day reaches payout (HUD shows "Moment of the day").</summary>
        public event Action PayoutReached;

        public int TapeCount => Shelf.Count;
        public bool IsViewing => (Viewer != null && Viewer.IsOpen) || (Hud != null && (Hud.ShelfOpen || Hud.ClosedThisFrame));

        readonly List<Tracked> _tracked = new List<Tracked>(128);
        readonly Dictionary<int, Tracked> _byKey = new Dictionary<int, Tracked>(256);
        readonly Dictionary<int, GhostVisual> _visuals = new Dictionary<int, GhostVisual>(256);
        readonly Dictionary<int, TrackInfo> _infos = new Dictionary<int, TrackInfo>(256);
        readonly Vector3[] _playerPos = new Vector3[16];
        readonly HashSet<int> _idScratch = new HashSet<int>();
        int _nextId = 1;
        long _nextTapeId = 1;
        float _accum, _rescan, _plankRescan, _servicePoll;
        PendingTape _pending;
        float _thumbAt = -1f;
        IEconomyService _economy;
        INoiseService _noise;
        IDayClockService _clock;
        DayClockPhase _lastPhase = DayClockPhase.None;
        InputAction _saveAction, _shelfAction;

        const string SaveKeyPref = "HarvestPanic.Camcorder.SaveKey";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!Application.isPlaying || Instance != null) return;
            if (!CamcorderTuning.Load().enabled) return;
            EnsureExists();
        }

        /// <summary>Creates the persistent camcorder object if missing (tests call this directly).</summary>
        public static CamcorderService EnsureExists()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("HP_Camcorder");
            DontDestroyOnLoad(go);
            return go.AddComponent<CamcorderService>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            Tuning = CamcorderTuning.Load();
            Ring = new ReplayRingBuffer(Tuning.RingFrames, Tuning.maxActorsPerFrame);
            EventRing = new ReplayEventRing(256);
            Rules = CaptureRules.From(Tuning);
            Shelf.MaxTapes = Tuning.maxTapesPerDay;
            Shelf.BudgetBytes = (long)(Tuning.tapeMemoryBudgetMB * 1024 * 1024);
            Viewer = gameObject.AddComponent<ReplayViewer>();
            Hud = gameObject.AddComponent<CamcorderHud>();
            BuildInput();
        }

        void OnEnable()
        {
            Services.Register<ICamcorderService>(this);
            SceneManager.sceneLoaded += OnSceneLoaded;
            _saveAction?.Enable(); _shelfAction?.Enable();
        }

        void OnDisable()
        {
            Services.Unregister<ICamcorderService>(this);
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Unhook();
            _saveAction?.Disable(); _shelfAction?.Disable();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            _saveAction?.Dispose(); _shelfAction?.Dispose();
            if (_pending?.Thumb != null) Destroy(_pending.Thumb);
            Shelf.Clear();
        }

        void OnSceneLoaded(Scene s, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single) return;
            // New level: old actors are gone; keep tapes (they own their ghost recipes through _visuals).
            for (int i = _tracked.Count - 1; i >= 0; i--) if (!_tracked[i].Explicit || _tracked[i].T == null) RemoveAt(i);
            _rescan = 0f; _plankRescan = 0f;
        }

        // ---------------------------------------------------------------- input

        void BuildInput()
        {
            _saveAction = new InputAction("CamcorderSave", InputActionType.Button);
            _saveAction.AddBinding(PlayerPrefs.GetString(SaveKeyPref, Tuning.saveKeyPath));
            _saveAction.AddCompositeBinding("OneModifier").With("Modifier", "<Gamepad>/select").With("Binding", "<Gamepad>/dpad/down");
            _shelfAction = new InputAction("CamcorderShelf", InputActionType.Button, Tuning.shelfKeyPath);
        }

        /// <summary>Rebinds the keyboard save key (Input System path, e.g. "&lt;Keyboard&gt;/f7") and persists it.</summary>
        public void RebindSaveKey(string path)
        {
            if (string.IsNullOrEmpty(path)) path = Tuning.saveKeyPath;
            PlayerPrefs.SetString(SaveKeyPref, path);
            _saveAction.ApplyBindingOverride(0, path);
        }

        public string SaveKeyDisplay => _saveAction != null ? _saveAction.GetBindingDisplayString(0) : "F8";

        // ---------------------------------------------------------------- tracking API

        /// <summary>Tracks <paramref name="t"/> explicitly (tests, or systems that want to be filmed). Returns the track id.</summary>
        public int Track(Transform t, TrackKind kind, ulong owner = PlayerIds.None, string name = null)
        {
            if (t == null) return -1;
            int key = t.GetInstanceID();
            if (_byKey.TryGetValue(key, out var existing)) return existing.Id;
            var e = Add(t, kind, owner, name);
            e.Explicit = true;
            return e.Id;
        }

        public void Untrack(Transform t)
        {
            if (t == null) return;
            if (!_byKey.TryGetValue(t.GetInstanceID(), out var e)) return;
            RemoveAt(_tracked.IndexOf(e));
        }

        public GhostVisual VisualFor(int id) => _visuals.TryGetValue(id, out var v) ? v : null;

        /// <summary>Live renderers of tracked actors (the viewer hides them while ghosts play).</summary>
        public void CollectLiveRenderers(List<Renderer> into)
        {
            foreach (var e in _tracked)
                if (e.T != null && e.Renderers != null)
                    foreach (var r in e.Renderers) if (r != null) into.Add(r);
        }

        Tracked Add(Transform t, TrackKind kind, ulong owner, string name)
        {
            var e = new Tracked { Id = _nextId++, Key = t.GetInstanceID(), T = t, Kind = kind, Owner = owner, Renderers = t.GetComponentsInChildren<Renderer>(true) };
            _tracked.Add(e);
            _byKey[e.Key] = e;
            _visuals[e.Id] = GhostVisual.Capture(t, kind);
            _infos[e.Id] = new TrackInfo { Id = e.Id, Kind = kind, OwnerClientId = owner, Name = name ?? t.name };
            return e;
        }

        void RemoveAt(int i)
        {
            if (i < 0) return;
            _byKey.Remove(_tracked[i].Key);
            _tracked.RemoveAt(i);
        }

        static TrackKind Classify(int layer)
        {
            if (layer == LayerMask.NameToLayer(GameLayers.Player)) return TrackKind.Player;
            if (layer == LayerMask.NameToLayer(GameLayers.Critter)) return TrackKind.Critter;
            if (layer == LayerMask.NameToLayer(GameLayers.Carryable)) return TrackKind.Carryable;
            if (layer == LayerMask.NameToLayer(GameLayers.Bridge)) return TrackKind.Plank;
            return TrackKind.Other;
        }

        void Rescan()
        {
            for (int i = _tracked.Count - 1; i >= 0; i--) if (_tracked[i].T == null) RemoveAt(i);
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.SpawnManager != null && nm.IsListening)
            {
                foreach (var no in nm.SpawnManager.SpawnedObjectsList)
                {
                    if (no == null) continue;
                    var t = no.transform;
                    if (_byKey.ContainsKey(t.GetInstanceID())) continue;
                    var kind = Classify(no.gameObject.layer);
                    if (kind == TrackKind.Other) continue;
                    ulong owner = kind == TrackKind.Player ? no.OwnerClientId : PlayerIds.None;
                    Add(t, kind, owner, kind == TrackKind.Player ? PlayerName(owner) : StripClone(no.name));
                }
            }
            _plankRescan -= Tuning.rescanSeconds;
            if (_plankRescan <= 0f)
            {
                _plankRescan = 10f;
                int bridge = LayerMask.NameToLayer(GameLayers.Bridge);
                if (bridge >= 0)
                    foreach (var c in FindObjectsByType<Collider>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                    {
                        if (c.gameObject.layer != bridge) continue;
                        var t = c.attachedRigidbody != null ? c.attachedRigidbody.transform : c.transform;
                        if (_tracked.Count >= Tuning.maxActorsPerFrame || _byKey.ContainsKey(t.GetInstanceID())) continue;
                        Add(t, TrackKind.Plank, PlayerIds.None, t.name);
                    }
            }
        }

        static string StripClone(string n) => n.EndsWith("(Clone)", StringComparison.Ordinal) ? n.Substring(0, n.Length - 7) : n;

        public static string PlayerName(ulong clientId)
        {
            if (clientId == PlayerIds.None || clientId == PlayerIds.Nature) return "Nature";
            if (Services.TryGet(out ISessionService s) && s.Players != null)
                foreach (var p in s.Players) if (p.ClientId == clientId && !string.IsNullOrEmpty(p.Name)) return p.Name;
            return "Farmer " + (clientId + 1);
        }

        // ---------------------------------------------------------------- loop

        void Update()
        {
            PollServices();
            PollPhase();
            if (Viewer != null && Viewer.IsOpen) return; // no recording while watching
            if (!IsViewing)
            {
                if (_saveAction != null && _saveAction.WasPressedThisFrame()) SaveManualMoment();
                if (_shelfAction != null && _shelfAction.WasPressedThisFrame() && !InputModal.IsBlocking) OpenShelf();
            }
            Step(Time.deltaTime);
        }

        /// <summary>Advances the recording clock by <paramref name="dt"/>, samples at tickHz and cuts finished tapes.
        /// Called from Update; tests disable the component and drive it directly for deterministic timing.</summary>
        public void Step(float dt)
        {
            if (!Tuning.enabled || dt <= 0f) return;
            Clock += dt;
            _rescan -= dt;
            if (_rescan <= 0f) { _rescan = Tuning.rescanSeconds; Rescan(); }

            float step = 1f / Mathf.Max(1f, Tuning.tickHz);
            _accum += dt;
            if (_accum >= step) { _accum = Mathf.Min(_accum - step, step); SampleNow(); }

            if (_thumbAt >= 0f && Clock >= _thumbAt) { _thumbAt = -1f; CaptureThumbnail(); }
            if (Rules.Tick(Clock)) CutTape();
        }

        /// <summary>Writes one frame of every relevant tracked actor into the ring (no allocations).</summary>
        public void SampleNow()
        {
            int players = 0;
            for (int i = 0; i < _tracked.Count; i++)
            {
                var e = _tracked[i];
                if (e.Kind == TrackKind.Player && e.T != null && players < _playerPos.Length) _playerPos[players++] = e.T.position;
            }
            float r2 = Tuning.relevanceRadius * Tuning.relevanceRadius;
            Ring.BeginFrame(Clock);
            for (int i = 0; i < _tracked.Count; i++)
            {
                var e = _tracked[i];
                if (e.T == null) continue;
                var pos = e.T.position;
                if (e.Kind != TrackKind.Player && players > 0 && !e.Explicit)
                {
                    bool near = false;
                    for (int p = 0; p < players && !near; p++) near = (pos - _playerPos[p]).sqrMagnitude <= r2;
                    if (!near) continue;
                }
                var flags = SampleFlags.None;
                if (e.T.gameObject.activeInHierarchy && AnyRendererOn(e.Renderers)) flags |= SampleFlags.Visible;
                Ring.Add(new ReplaySample(e.Id, e.Kind, flags, pos, e.T.rotation));
            }
            Ring.EndFrame();
        }

        static bool AnyRendererOn(Renderer[] rs)
        {
            if (rs == null || rs.Length == 0) return true;
            for (int i = 0; i < rs.Length; i++) { var r = rs[i]; if (r != null && r.enabled && r.gameObject.activeInHierarchy) return true; }
            return false;
        }

        // ---------------------------------------------------------------- services

        void PollServices()
        {
            _servicePoll -= Time.unscaledDeltaTime;
            if (_servicePoll > 0f) return;
            _servicePoll = 0.5f;
            Services.TryGet(out IEconomyService eco);
            if (!ReferenceEquals(eco, _economy))
            {
                if (_economy != null) _economy.OnDisaster -= OnDisaster;
                _economy = eco;
                if (_economy != null) _economy.OnDisaster += OnDisaster;
            }
            Services.TryGet(out INoiseService noise);
            if (!ReferenceEquals(noise, _noise))
            {
                if (_noise != null) _noise.OnNoise -= OnNoise;
                _noise = noise;
                if (_noise != null) _noise.OnNoise += OnNoise;
            }
            Services.TryGet(out _clock);
        }

        void Unhook()
        {
            if (_economy != null) _economy.OnDisaster -= OnDisaster;
            if (_noise != null) _noise.OnNoise -= OnNoise;
            _economy = null; _noise = null;
        }

        void PollPhase()
        {
            if (_clock == null) return;
            DayClockPhase ph;
            try { ph = _clock.Phase; } catch (Exception) { return; }
            if (ph == _lastPhase) return;
            var prev = _lastPhase;
            _lastPhase = ph;
            if (ph == DayClockPhase.Morning && prev != DayClockPhase.None) NewDay();
            if (ph == DayClockPhase.Payout || ph == DayClockPhase.Evicted)
            {
                if (Rules.IsCapturing) { Rules.Tick(float.PositiveInfinity); CutTape(); }
                Shelf.PruneForPayout(Tuning.tapesKeptAtPayout);
                PayoutReached?.Invoke();
            }
        }

        /// <summary>Day rollover: tapes that were not exported are discarded, manual budget refills.</summary>
        public void NewDay()
        {
            DayIndex++;
            Rules.NewDay();
            Shelf.Clear();
            _pending = null;
        }

        void OnNoise(NoiseEvent n)
        {
            if (n.Loudness < 0.5f) return;
            EventRing.Add(new ReplayEvent { Time = Clock, Type = ReplayEventType.Noise, Position = n.Location, Value = n.Loudness, Code = (int)n.Tags, PlayerId = n.BlamedPlayerId });
        }

        /// <summary>Disaster hook (IEconomyService.OnDisaster); public so tests and dev tools can drive it.</summary>
        public void OnDisaster(BlameEvent e)
        {
            if (Viewer != null && Viewer.IsOpen) return;
            EventRing.Add(new ReplayEvent { Time = Clock, Type = ReplayEventType.Disaster, Position = e.Location, Value = e.Severity, Code = (int)e.Verb, PlayerId = e.PrimaryInstigator });
            var d = Rules.OnDisaster(Clock, e.Severity, Ring.Count > 0 ? Ring.OldestTime : Clock);
            if (d == CaptureDecision.Started)
            {
                _pending = new PendingTape { Id = _nextTapeId++ };
                ApplyDisaster(_pending, e, true);
                _thumbAt = Clock + 0.15f;
            }
            else if ((d == CaptureDecision.Extended || d == CaptureDecision.Reopened) && _pending != null)
            {
                bool bigger = e.Severity > _pending.Severity || _pending.Manual;
                ApplyDisaster(_pending, e, bigger);
                if (bigger) _thumbAt = Clock + 0.15f;
            }
            if (e.DayIndex > DayIndex) DayIndex = e.DayIndex;
        }

        static void ApplyDisaster(PendingTape p, in BlameEvent e, bool headline)
        {
            p.DollarDelta += e.DollarDelta;
            if (!headline) { p.Extra++; p.Title = BaseTitle(p) + $" (+{p.Extra} MORE)"; return; }
            if (p.Severity > 0 || p.Manual) p.Extra++;
            p.Manual = false;
            p.Severity = (byte)Mathf.Max(p.Severity, e.Severity);
            p.Verb = e.Verb;
            p.Primary = e.PrimaryInstigator;
            p.InstigatorCount = Mathf.Max(p.InstigatorCount, e.Instigators?.Length ?? 0);
            p.Focus = e.Location;
            p.Title = TapeTitles.ForDisaster(PlayerName(e.PrimaryInstigator), e.Verb, p.DollarDelta) + (p.Extra > 0 ? $" (+{p.Extra} MORE)" : "");
            p.TriggerClock = Instance != null ? Instance.Clock : 0f;
        }

        static string BaseTitle(PendingTape p)
        {
            int i = p.Title.IndexOf(" (+", StringComparison.Ordinal);
            return i >= 0 ? p.Title.Substring(0, i) : p.Title;
        }

        // ---------------------------------------------------------------- ICamcorderService

        public void OpenShelf() { if (Hud != null) Hud.OpenShelf(); }

        public bool SaveManualMoment()
        {
            if (!Tuning.enabled) { Toast?.Invoke("Camcorder is off."); return false; }
            var d = Rules.OnManual(Clock, Ring.Count > 0 ? Ring.OldestTime : Clock);
            if (d == CaptureDecision.DeniedManualCap) { Toast?.Invoke("Grandpa's tape is full. No more manual saves today."); return false; }
            if (d == CaptureDecision.DeniedOff) return false;
            if (d == CaptureDecision.Started || _pending == null)
            {
                ulong me = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening ? NetworkManager.Singleton.LocalClientId : 0UL;
                _pending = new PendingTape
                {
                    Id = _nextTapeId++, Manual = true, Primary = me, TriggerClock = Clock,
                    Title = TapeTitles.ForManual(PlayerName(me), DayIndex),
                    Focus = Camera.main != null ? Camera.main.transform.position + Camera.main.transform.forward * 4f : Vector3.zero
                };
                _thumbAt = Clock + 0.05f;
            }
            EventRing.Add(new ReplayEvent { Time = Clock, Type = ReplayEventType.Manual, Position = _pending.Focus, PlayerId = _pending.Primary });
            Toast?.Invoke($"REC ● Saving the last {Mathf.RoundToInt(Tuning.preRollSeconds)} s... ({Rules.ManualLeft} manual left today)");
            return true;
        }

        /// <summary>Cuts the open tape immediately (tests / payout).</summary>
        public Moment FinalizeNow()
        {
            if (!Rules.IsCapturing) return null;
            Rules.Tick(float.PositiveInfinity);
            return CutTape();
        }

        Moment CutTape()
        {
            if (_pending == null) return null;
            if (_thumbAt >= 0f) { _thumbAt = -1f; CaptureThumbnail(); }
            Ring.Measure(Rules.TapeStart, Rules.TapeEnd, out int first, out int frames, out int samples);
            if (frames < 2) return null;
            float t0 = Ring.TimeAt(first);
            var m = new Moment
            {
                Id = _pending.Id, Title = _pending.Title, Manual = _pending.Manual, DayIndex = DayIndex, Severity = _pending.Severity,
                DollarDelta = _pending.DollarDelta, InstigatorCount = _pending.InstigatorCount, Verb = _pending.Verb,
                PrimaryInstigator = _pending.Primary, Focus = _pending.Focus, SourceStart = t0,
                TriggerTime = Mathf.Max(0f, _pending.TriggerClock - t0),
                FrameTimes = new float[frames], FrameStarts = new int[frames], Samples = new ReplaySample[samples]
            };
            Ring.CopyFrames(first, frames, t0, m.FrameTimes, m.FrameStarts, m.Samples);
            _idScratch.Clear();
            foreach (var s in m.Samples)
                if (_idScratch.Add(s.Id) && _infos.TryGetValue(s.Id, out var info)) m.Tracks.Add(info);
            var evs = new List<ReplayEvent>();
            float t1 = t0 + m.Duration;
            for (int i = 0; i < EventRing.Count; i++)
            {
                var e = EventRing[i];
                if (e.Time < t0 || e.Time > t1) continue;
                e.Time -= t0;
                evs.Add(e);
            }
            m.Events = evs.ToArray();
            if (_pending.Thumb != null)
            {
                // Each cut owns its own copy so re-cuts and shelf eviction never destroy a texture still in use.
                m.Thumbnail = Instantiate(_pending.Thumb);
            }
            Shelf.Add(m);
            MomentSaved?.Invoke(m);
            Toast?.Invoke(m.Manual ? "Clip saved! " + m.Title : "● TAPE SAVED: " + m.Title);
            return m;
        }

        void CaptureThumbnail()
        {
            if (_pending == null) return;
            var cam = Camera.main;
            if (cam == null || !cam.isActiveAndEnabled) return;
            var tex = _pending.Manual || _pending.Focus == Vector3.zero
                ? ClipRender.RenderToTexture(cam, Tuning.thumbnailWidth, Tuning.thumbnailHeight)
                : ClipRender.RenderWide(cam, _pending.Focus, Tuning.thumbnailWidth, Tuning.thumbnailHeight);
            if (tex == null) return;
            if (_pending.Thumb != null) Destroy(_pending.Thumb);
            _pending.Thumb = tex;
        }

        /// <summary>Opens <paramref name="m"/> in the replay viewer.</summary>
        public void Watch(Moment m)
        {
            if (m == null || Viewer == null) return;
            if (Rules.IsCapturing) FinalizeNow();
            Viewer.Open(m, this);
            Hud.ShowViewer(m);
        }

        /// <summary>Budget numbers for docs/tests.</summary>
        public long RingMemoryBytes => Ring.MemoryBytes;
    }
}
