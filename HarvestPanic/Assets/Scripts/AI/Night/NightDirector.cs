using System;
using System.Collections.Generic;
using HarvestPanic.Core;
using Unity.Netcode;
using UnityEngine;

namespace HarvestPanic.AI.Night
{
    /// <summary>
    /// Night Director (night-creatures §3.2). Server: while the day clock is in Dusk ("night") it spends a per-night
    /// threat POOL on Mr. Stitches and foxes, re-buying slots after RespawnCooldown while budget remains; caps grow by
    /// week. Steers toward the loudest 20 m cell. Replicates IsNight / budget / lantern posts to every peer.
    /// </summary>
    public sealed class NightDirector : NetworkBehaviour
    {
        [SerializeField] NightTuning tuning = new NightTuning();
        [SerializeField] GameObject stitchesPrefab;
        [SerializeField] GameObject foxPrefab;

        public static NightDirector Instance { get; private set; }
        public static event Action<bool> OnNightChanged;

        readonly NetworkVariable<bool> _nvNight = new NetworkVariable<bool>();
        readonly NetworkVariable<int> _nvBudget = new NetworkVariable<int>();
        readonly NetworkVariable<int> _nvSpent = new NetworkVariable<int>();
        readonly NetworkVariable<byte> _nvLanterns = new NetworkVariable<byte>();

        bool _night; int _budget, _spent, _lanterns, _week = 1;
        bool? _forced;
        float _nextSpawnCheck, _nextSteer, _slotFreeAt;
        readonly List<MrStitches> _stitches = new List<MrStitches>();
        readonly List<NightFox> _foxes = new List<NightFox>();
        readonly List<(Vector3 c, float r)> _safe = new List<(Vector3, float)>();
        NightNoiseMap _noise;
        INoiseService _noiseSvc;

        public NightTuning Tuning => tuning;
        bool ReadRemote => IsSpawned && !IsServer;
        public bool IsAuthority => IsSpawned ? IsServer : !(NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening);
        public bool IsNight => ReadRemote ? _nvNight.Value : _night;
        public int Budget => ReadRemote ? _nvBudget.Value : _budget;
        public int Spent => ReadRemote ? _nvSpent.Value : _spent;
        public int LanternPosts => ReadRemote ? _nvLanterns.Value : _lanterns;
        public int Week => _week;
        public IReadOnlyList<MrStitches> Stitches => _stitches;
        public IReadOnlyList<NightFox> Foxes => _foxes;
        /// <summary>Server: loudest cell centre from the last steer tick (null = none).</summary>
        public Vector3? HotCell { get; private set; }
        /// <summary>Dev/test: forces the creature spawns off (only lighting/state).</summary>
        public bool SpawnCreatures = true;

        void Awake() { Instance = this; }
        public override void OnDestroy() { if (Instance == this) Instance = null; Unhook(); base.OnDestroy(); }

        public override void OnNetworkSpawn() { _nvNight.OnValueChanged += (a, b) => OnNightChanged?.Invoke(b); }

        /// <summary>Dev/test: null follows the day clock; true/false forces night.</summary>
        public void ForceNight(bool? on) => _forced = on;

        void Update()
        {
            if (!IsAuthority) return;
            DayClockPhase phase = Services.TryGet(out IDayClockService clock) ? clock.Phase : DayClockPhase.None;
            bool want = _forced ?? phase == DayClockPhase.Dusk;
            if (want && !_night) BeginNight();
            else if (!want && _night) EndNight();
            if (_night) TickNight();
        }

        void BeginNight()
        {
            _night = true;
            int day = Services.TryGet(out IDayModifierService mods) ? mods.DayIndex : 0;
            float threat = mods != null ? mods.Resolve(DayParams.NightThreatMult, 1f) : 1f;
            _lanterns = mods != null ? Mathf.Clamp(Mathf.RoundToInt(mods.Resolve(DayParams.NightLanternPosts, 0f)), 0, tuning.LanternPostPositions.Length) : 0;
            BuildSafeZones();
            _week = NightMath.Week(day, tuning.DaysPerWeek);
            _budget = NightMath.Budget(day, CountPlayersOutside(), tuning, threat);
            _spent = 0; _slotFreeAt = 0f; _nextSpawnCheck = 0f; _nextSteer = Time.time + tuning.SteerInterval;
            _noise = new NightNoiseMap(tuning.CellSize);
            if (Services.TryGet(out _noiseSvc)) _noiseSvc.OnNoise += HandleNoise;
            Publish();
            Debug.Log($"[Night] begins: day {day} week {_week} budget {_budget} lanterns {_lanterns}");
            if (!IsSpawned) OnNightChanged?.Invoke(true);
        }

        void EndNight()
        {
            _night = false;
            Unhook();
            foreach (var s in _stitches) if (s != null) Despawn(s.gameObject);
            foreach (var f in _foxes) if (f != null) { f.DropCarried(); Despawn(f.gameObject); }
            _stitches.Clear(); _foxes.Clear(); HotCell = null;
            Publish();
            if (!IsSpawned) OnNightChanged?.Invoke(false);
        }

        void Unhook() { if (_noiseSvc != null) _noiseSvc.OnNoise -= HandleNoise; _noiseSvc = null; }

        void HandleNoise(NoiseEvent e)
        {
            _noise?.Add(e.Location, e.Loudness, e.Source, Time.time);
            foreach (var f in _foxes) if (f != null) f.HearNoise(e);
        }

        void Publish()
        {
            if (!IsSpawned) return;
            _nvNight.Value = _night; _nvBudget.Value = _budget; _nvSpent.Value = _spent; _nvLanterns.Value = (byte)_lanterns;
        }

        void TickNight()
        {
            _stitches.RemoveAll(s => s == null); _foxes.RemoveAll(f => f == null || f.Finished && Despawn(f.gameObject));
            if (Time.time >= _nextSteer)
            {
                _nextSteer = Time.time + tuning.SteerInterval;
                HotCell = _noise != null && _noise.TryLoudest(Time.time, tuning.SteerInterval, out var c) ? c : (Vector3?)null;
            }
            if (!SpawnCreatures || Time.time < _nextSpawnCheck) return;
            _nextSpawnCheck = Time.time + 0.5f;
            if (Time.time < _slotFreeAt) return;
            int left = _budget - _spent;
            if (stitchesPrefab != null && _stitches.Count < NightMath.StitchesCap(_week, tuning) && left >= tuning.StitchesCost)
            {
                var pos = tuning.FieldPosts.Length > 0 ? tuning.FieldPosts[_stitches.Count % tuning.FieldPosts.Length] : Vector3.zero;
                var s = Spawn(stitchesPrefab, Ground(pos)).GetComponent<MrStitches>();
                s.Init(this); _stitches.Add(s); _spent += tuning.StitchesCost;
            }
            else if (foxPrefab != null && _foxes.Count < NightMath.FoxCap(_week, tuning) && left >= tuning.FoxCost && tuning.FoxDens.Length > 0)
            {
                var den = tuning.FoxDens[(_spent / tuning.FoxCost) % tuning.FoxDens.Length];
                var f = Spawn(foxPrefab, Ground(den)).GetComponent<NightFox>();
                f.Init(this, den); _foxes.Add(f); _spent += tuning.FoxCost;
            }
            else return;
            // stagger arrivals; once full, every finished slot waits RespawnCooldown before being re-bought
            _slotFreeAt = Time.time + (_spent <= NightMath.MaxConcurrentSpend(_week, tuning) ? 1.5f : tuning.RespawnCooldown);
            Publish();
        }

        GameObject Spawn(GameObject prefab, Vector3 pos)
        {
            var go = Instantiate(prefab, pos, Quaternion.identity);
            if (IsSpawned) go.GetComponent<NetworkObject>().Spawn(true);
            return go;
        }

        bool Despawn(GameObject go)
        {
            var no = go.GetComponent<NetworkObject>();
            if (no != null && no.IsSpawned) no.Despawn(true); else Destroy(go);
            return true;
        }

        int CountPlayersOutside()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer) return 0;
            int n = 0;
            var barns = LevelMarker.FindAll(MarkerRole.BarnRespawn);
            foreach (var c in nm.ConnectedClientsList)
            {
                if (c.PlayerObject == null) continue;
                bool inside = false;
                foreach (var b in barns) if (b != null && Flat(b.transform.position - c.PlayerObject.transform.position) < tuning.BarnSafeRadius) inside = true;
                if (!inside) n++;
            }
            return n;
        }

        void BuildSafeZones()
        {
            _safe.Clear();
            foreach (var m in LevelMarker.FindAll(MarkerRole.BarnRespawn)) _safe.Add((m.transform.position, tuning.BarnSafeRadius));
            foreach (var m in LevelMarker.FindAll(MarkerRole.MarketPen)) _safe.Add((m.transform.position, tuning.MarketSafeRadius));
            foreach (var m in LevelMarker.FindAll(MarkerRole.BridgePlank)) _safe.Add((m.transform.position, tuning.BridgeSafeRadius));
            for (int i = 0; i < _lanterns; i++) _safe.Add((tuning.LanternPostPositions[i], tuning.LanternRadius));
        }

        /// <summary>Test hook: adds a safe circle.</summary>
        public void AddSafeZone(Vector3 c, float r) => _safe.Add((c, r));

        /// <summary>True inside barn, market, bridge or a lit lantern post.</summary>
        public bool InSafeZone(Vector3 p)
        {
            foreach (var s in _safe) if (Flat(s.c - p) < s.r) return true;
            return false;
        }

        public static float Flat(Vector3 d) => new Vector2(d.x, d.z).magnitude;

        static readonly int GroundMask = ~0 & ~(1 << 3) & ~(1 << 4) & ~(1 << 6) & ~(1 << 7) & ~(1 << 9) & ~(1 << 10);
        public static int LosMask => GroundMask;

        public static Vector3 Ground(Vector3 p)
        {
            if (Physics.Raycast(new Vector3(p.x, p.y + 30f, p.z), Vector3.down, out var hit, 80f, GroundMask, QueryTriggerInteraction.Ignore))
                return hit.point;
            return new Vector3(p.x, p.y, p.z);
        }

        /// <summary>Field post nearest <paramref name="p"/>.</summary>
        public Vector3 NearestPost(Vector3 p)
        {
            Vector3 best = p; float bd = float.MaxValue;
            foreach (var f in tuning.FieldPosts) { float d = Flat(f - p); if (d < bd) { bd = d; best = f; } }
            return best;
        }
    }
}
