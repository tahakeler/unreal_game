using System;
using System.Collections.Generic;
using HarvestPanic.Core;
using Unity.Netcode;
using UnityEngine;

namespace HarvestPanic.Economy
{
    /// <summary>
    /// Goods Item component (goods-value.md "Contract: Goods Item"). Server-authoritative bruise points, freshness,
    /// tags and last handler, replicated with NetworkVariables. Implements <see cref="IGoods"/> and
    /// <see cref="ICarryable"/> so the Player carry system and AI can use any goods prop without referencing Economy.
    /// Critter prefabs can add this component too (prefab composition needs no assembly reference).
    /// Offline (not spawned, no listening NetworkManager) the local instance is authoritative so tests and
    /// dev scenes work without a session.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class Goods : NetworkBehaviour, IGoods, ICarryable, IGoodsSoakable
    {
        [SerializeField] GoodsDefinition definition;
        [SerializeField, Tooltip("Used when Definition is empty (resolved through Content.Goods).")] string goodsId;
        [SerializeField, Tooltip("Tools/light props attach rigidly and never bruise.")] bool kinematicCarry;

        readonly NetworkVariable<float> _nvBruise = new NetworkVariable<float>();
        readonly NetworkVariable<float> _nvFresh = new NetworkVariable<float>(1f);
        readonly NetworkVariable<int> _nvTags = new NetworkVariable<int>();
        readonly NetworkVariable<ulong> _nvLastHandler = new NetworkVariable<ulong>(PlayerIds.None);

        float _bruise;
        float _freshElapsed = -1f;
        float _fresh = 1f;
        GoodsTag _tags;
        ulong _lastHandler = PlayerIds.None;
        TouchHistory _touches;
        readonly HashSet<ulong> _holders = new HashSet<ulong>();
        float _releasedAt = -100f, _thrownAt = -100f;
        ulong _thrower = PlayerIds.None;
        float _waterAccumBp, _waterAccumTimer;
        Rigidbody _rb;
        EconomyTuning _t;
        static readonly ulong[] Scratch = new ulong[3];

        /// <summary>Raised after each bruise hit (server and offline).</summary>
        public event Action<BruiseReport> OnBruised;
        /// <summary>Raised when the bruise tier changes (old, new).</summary>
        public event Action<BruiseState, BruiseState> OnStateChanged;

        /// <summary>All live goods (server-side lookups, HUD value floats).</summary>
        public static readonly List<Goods> All = new List<Goods>();

        // ---------------- IGoods ----------------
        public Transform GoodsTransform => transform;
        public GoodsDefinition Definition
        {
            get
            {
                if (definition == null && !string.IsNullOrEmpty(goodsId)) GoodsResolver.TryResolve(goodsId, out definition);
                return definition;
            }
        }
        public string GoodsId => Definition != null ? Definition.GoodsId : goodsId;
        public float BruisePoints => ReadRemote ? _nvBruise.Value : _bruise;
        public BruiseState BruiseState => EconomyMath.StateFor(BruisePoints, Tuning);
        public float Freshness => ReadRemote ? _nvFresh.Value : _fresh;
        public GoodsTag Tags => ReadRemote ? (GoodsTag)_nvTags.Value : _tags;
        public ulong LastHandlerId => ReadRemote ? _nvLastHandler.Value : _lastHandler;
        public int CurrentValue => EconomyDirector.Instance != null ? EconomyDirector.Instance.QuoteValue(this, false) : BaseQuote();

        // ---------------- ICarryable ----------------
        public Transform CarryTransform => transform;
        public int HandsRequired => Definition != null ? Definition.CarryHands : 1;
        public float MassKg => _rb != null ? _rb.mass : (Definition != null ? Definition.MassKg : 1f);
        public bool IsKinematicCarry => kinematicCarry;

        /// <summary>True while at least one player holds it.</summary>
        public bool IsHeld => _holders.Count > 0;
        /// <summary>Holders (server).</summary>
        public IReadOnlyCollection<ulong> Holders => _holders;

        EconomyTuning Tuning => _t != null ? _t : (_t = EconomyTuning.Load());
        bool ReadRemote => IsSpawned && !IsServer;
        /// <summary>True where this instance may mutate state (server, or offline).</summary>
        public bool IsEconomyAuthority => IsSpawned ? IsServer : !NetUtil.IsListening;

        /// <summary>Editor/test setup.</summary>
        public void Configure(GoodsDefinition def) { definition = def; goodsId = def != null ? def.GoodsId : goodsId; ApplyDefinition(); }

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _touches = new TouchHistory(Tuning.TouchWindow, 3);
            ApplyDefinition();
        }

        void ApplyDefinition()
        {
            var def = Definition;
            if (def == null || _rb == null) return;
            _rb.mass = def.MassKg;
            _tags |= def.DefaultTags;
            if (def.Category == GoodsCategory.Product) _freshElapsed = 0f; // laid/produced at spawn
        }

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        public override void OnNetworkSpawn()
        {
            if (IsServer) PushAll();
        }

        void PushAll()
        {
            if (!IsSpawned || !IsServer) return;
            _nvBruise.Value = _bruise;
            _nvFresh.Value = _fresh;
            _nvTags.Value = (int)_tags;
            _nvLastHandler.Value = _lastHandler;
        }

        void Update()
        {
            if (!IsEconomyAuthority || Definition == null) return;
            float dt = Time.deltaTime;
            var dir = EconomyDirector.Instance;
            bool decaying = dir == null || dir.FreshnessRunning;
            if (_freshElapsed >= 0f && decaying && Definition.GoodsId != "Special.GoldenEgg")
            {
                float mult = dir != null ? dir.ResolveParam(DayModifierStack.Params.FreshnessDecayMult, 1f) : 1f;
                _freshElapsed += dt * mult;
                float f = EconomyMath.FreshnessFor(Definition.Category, _freshElapsed, Tuning);
                if (Mathf.Abs(f - _fresh) > 0.001f) { _fresh = f; if (IsSpawned) _nvFresh.Value = f; }
            }
            if (_waterAccumBp > 0f)
            {
                _waterAccumTimer += dt;
                if (_waterAccumTimer >= 1f) FlushWater();
            }
        }

        // ---------------- touches / carry ----------------

        public void RecordTouch(ulong clientId)
        {
            if (!IsEconomyAuthority || !PlayerIds.IsPlayer(clientId)) return;
            _touches.Record(clientId, Time.time);
            _lastHandler = clientId;
            if (IsSpawned) _nvLastHandler.Value = clientId;
        }

        public void OnGrabbed(ulong clientId)
        {
            if (!IsEconomyAuthority) return;
            _holders.Add(clientId);
            RecordTouch(clientId);
            if (Definition != null && Definition.Category == GoodsCategory.Crop && _freshElapsed < 0f) _freshElapsed = 0f; // harvested
        }

        public void OnReleased(ulong clientId)
        {
            if (!IsEconomyAuthority) return;
            _holders.Remove(clientId);
            RecordTouch(clientId);
            _releasedAt = Time.time;
        }

        public void ApplyThrow(Vector3 velocity, ulong thrower)
        {
            if (!IsEconomyAuthority) return;
            _holders.Clear();
            RecordTouch(thrower);
            _thrower = thrower;
            _thrownAt = _releasedAt = Time.time;
            if (_rb != null && !_rb.isKinematic) _rb.linearVelocity = velocity;
        }

        /// <summary>Adds or removes dynamic tags (server).</summary>
        public void SetTag(GoodsTag tag, bool on)
        {
            if (!IsEconomyAuthority) return;
            _tags = on ? _tags | tag : _tags & ~tag;
            if (IsSpawned) _nvTags.Value = (int)_tags;
        }

        // ---------------- bruising ----------------

        void OnCollisionEnter(Collision c)
        {
            if (!IsEconomyAuthority || kinematicCarry) return;
            float impulse = c.impulse.magnitude;
            if (impulse <= 0f) return;
            float now = Time.time;
            // Player carry reports bruises itself while held and for CarryReportWindow after release (no double count).
            if (IsHeld || now - _releasedAt < Tuning.CarryReportWindow) return;
            foreach (var h in _holders) _touches.Record(h, now);
            BlameVerb verb = BlameVerb.Bruised;
            if (!IsHeld && now - _thrownAt <= Tuning.TouchWindow) verb = BlameVerb.Threw;
            else if (!IsHeld && now - _releasedAt <= Tuning.TouchWindow) verb = BlameVerb.Dropped;
            ApplyBruise(ImpactUnits.ToDesign(impulse), _touches.LastToucher(now), verb);
        }

        /// <summary>Server only. <paramref name="impulse"/> in DESIGN units (already <c>ImpactUnits.ToDesign</c>'d), per the IGoods contract.</summary>
        public void ApplyBruise(float impulse, ulong instigator, BlameVerb verb)
        {
            if (!IsEconomyAuthority || Definition == null || kinematicCarry) return;
            var dir = EconomyDirector.Instance;
            float bruiseMult = dir != null ? dir.ResolveParam(DayModifierStack.Params.GoodsBruiseMult, 1f) : 1f;
            float design = impulse;
            float dbp = EconomyMath.BruiseGain(design, Definition.Fragility, bruiseMult, Tuning);
            if (dbp <= 0f) return;
            LastBruiseKind = BruiseKind.Impact;
            AddBruisePoints(dbp, instigator, verb, null);
        }

        /// <summary>Server only. Adds raw bruise points (water, creatures) and reports/blames the loss.</summary>
        public void AddBruisePoints(float dbp, ulong instigator, BlameVerb verb, string causeTag)
        {
            if (!IsEconomyAuthority || Definition == null || dbp <= 0f) return;
            var t = Tuning;
            var oldState = EconomyMath.StateFor(_bruise, t);
            float bOld = EconomyMath.BruiseMult(oldState, Definition.Category, t);
            _bruise = Mathf.Min(100f, _bruise + dbp);
            if (IsSpawned) _nvBruise.Value = _bruise;
            var newState = EconomyMath.StateFor(_bruise, t);
            float bNew = EconomyMath.BruiseMult(newState, Definition.Category, t);
            var dir = EconomyDirector.Instance;
            float demand = dir != null ? dir.ExpectedDemand(GoodsId) : 1f;
            int dollars = EconomyMath.BruiseDollarLoss(Definition.BaseValue, demand, bOld, bNew);
            if (newState != oldState)
            {
                if (newState == BruiseState.Ruined && Definition.Category == GoodsCategory.Livestock) SetTag(GoodsTag.Grumpy, true);
                OnStateChanged?.Invoke(oldState, newState);
            }
            var report = new BruiseReport { Item = this, DeltaPoints = dbp, DeltaDollars = dollars, InstigatorId = instigator, Verb = verb };
            OnBruised?.Invoke(report);
            BruiseFeed.Raise(report); // polish-2: server presentation feed (Audio crunch/register)
            if (dollars >= 1) EmitLoss(-dollars, instigator, verb, causeTag);
        }

        void EmitLoss(int dollars, ulong instigator, BlameVerb verb, string causeTag)
        {
            if (!Services.TryGet(out IEconomyService eco)) return;
            int n = _touches.Snapshot(Time.time, Scratch);
            var ids = new List<ulong>(4);
            if (PlayerIds.IsPlayer(instigator)) ids.Add(instigator);
            for (int i = 0; i < n; i++) if (!ids.Contains(Scratch[i])) ids.Add(Scratch[i]);
            eco.EmitBlame(new BlameEvent
            {
                Instigators = ids.ToArray(), Verb = verb, ItemId = GoodsId, ItemTag = "Goods." + GoodsId,
                DollarDelta = dollars, Location = transform.position, CauseTag = ids.Count == 0 ? (causeTag ?? "Nobody") : causeTag
            });
        }

        /// <summary>Despawn/out-of-bounds: full value lost to the last handler within the window.</summary>
        public void ReportLost(BlameVerb verb, string causeTag)
        {
            if (!IsEconomyAuthority) return;
            EmitLoss(-CurrentValue, _touches.LastToucher(Time.time), verb, causeTag);
        }

        // ---------------- water ----------------

        /// <summary>Kind of the most recent bruise (Impact or Soaked).</summary>
        public BruiseKind LastBruiseKind { get; private set; }

        /// <summary>
        /// IGoodsSoakable: one-shot soak (river dunk, rain) = <see cref="EconomyTuning.SoakSeconds"/> of submersion
        /// at the goods-value water rate (crops 15 BP/s, livestock 5 BP/s, ducks 0).
        /// </summary>
        public void ApplySoak(ulong instigator)
        {
            if (!IsEconomyAuthority || Definition == null || Definition.GoodsId == "Livestock.Duck") return;
            float rate = Definition.Category == GoodsCategory.Livestock ? Tuning.WaterBpsLivestock : Tuning.WaterBpsCrop;
            if (!PlayerIds.IsPlayer(instigator)) instigator = _touches.LastToucher(Time.time);
            LastBruiseKind = BruiseKind.Soaked;
            AddBruisePoints(rate * Tuning.SoakSeconds, instigator, BlameVerb.DroppedInRiver, "Hazard.River");
        }

        void OnTriggerStay(Collider other)
        {
            if (!IsEconomyAuthority || Definition == null || other.gameObject.layer != LayerMask.NameToLayer(GameLayers.Water)) return;
            if (Definition.GoodsId == "Livestock.Duck") return;
            float rate = Definition.Category == GoodsCategory.Livestock ? Tuning.WaterBpsLivestock : Tuning.WaterBpsCrop;
            _waterAccumBp += rate * Time.fixedDeltaTime;
        }

        void FlushWater()
        {
            float bp = _waterAccumBp; _waterAccumBp = 0f; _waterAccumTimer = 0f;
            LastBruiseKind = BruiseKind.Soaked;
            AddBruisePoints(bp, _touches.LastToucher(Time.time), BlameVerb.DroppedInRiver, "Hazard.River");
        }

        // ---------------- value ----------------

        int BaseQuote()
        {
            var d = Definition;
            if (d == null) return 0;
            return EconomyMath.SaleValue(d.BaseValue, d.Category, BruiseState, Freshness, 1f, 1f, 1f, Tuning);
        }

        /// <summary>Server: removes the item after a sale or day-end cleanup.</summary>
        public void Remove()
        {
            if (IsSpawned) { if (IsServer) NetworkObject.Despawn(true); }
            else Destroy(gameObject);
        }
    }
}
