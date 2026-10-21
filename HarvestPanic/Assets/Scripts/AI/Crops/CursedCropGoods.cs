using System;
using System.Collections.Generic;
using HarvestPanic.Core;
using Unity.Netcode;
using UnityEngine;

namespace HarvestPanic.AI.Crops
{
    /// <summary>
    /// <see cref="IGoods"/> + <see cref="ICarryable"/> for a cursed crop (the crop's single goods/carry component, like
    /// <see cref="CritterGoods"/> for critters, so AI never references Economy). Uses Economy's
    /// <see cref="GoodsDefinition"/> (<c>Crop.Mandrake</c>, <c>Crop.TumbleMelon</c>) and falls back to a runtime stub.
    /// Starts <see cref="Planted"/> (kinematic, in the soil); the first grab pulls it out. Bruise = goods-value §4
    /// (ΔBP = max(0, I - 400)/40 x Fragility, cap 60/hit). Cursed crops do not wilt (Freshness 1). Server-authoritative.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CursedCropGoods : NetworkBehaviour, IGoods, ICarryable
    {
        [SerializeField] string goodsId = "Crop.Mandrake";
        [Header("Fallback definition (used only when the GoodsDefinition asset is missing)")]
        [SerializeField, Min(0)] int fallbackBaseValue = 45;
        [SerializeField, Range(0.25f, 2f)] float fallbackFragility = 1.2f;
        [SerializeField, Range(1, 8)] int fallbackCarryHands = 1;
        [SerializeField, Min(0.01f)] float fallbackMassKg = 3f;
        [SerializeField, Tooltip("Starts planted (kinematic) until first grabbed")] bool startPlanted = true;

        const float ImpulseMin = 400f, ImpulseScale = 40f, PerHitCap = 60f;

        readonly NetworkVariable<float> _netBruise = new NetworkVariable<float>();
        readonly NetworkVariable<bool> _netPlanted = new NetworkVariable<bool>(true);
        readonly TouchHistory _touches = new TouchHistory(3f, 4);
        readonly List<ulong> _holders = new List<ulong>(4);
        GoodsDefinition _def;
        float _bruise;
        bool _planted;
        ulong _lastHandler = PlayerIds.None, _thrower = PlayerIds.None;
        Rigidbody _rb;

        /// <summary>Raised after each bruise hit.</summary>
        public event Action<BruiseReport> OnBruised;
        /// <summary>Server: a holder grabbed it (client id, true when it is the first holder).</summary>
        public event Action<ulong, bool> Grabbed;
        /// <summary>Server: a holder let go (client id, true when nobody holds it any more).</summary>
        public event Action<ulong, bool> Released;
        /// <summary>Server: thrown (velocity, thrower).</summary>
        public event Action<Vector3, ulong> Thrown;
        /// <summary>Server: pulled out of the soil (first grab of a planted crop).</summary>
        public event Action<ulong> Pulled;
        /// <summary>Server: impact while loose (design-unit impulse).</summary>
        public event Action<float> Impact;

        /// <summary>Holders (server).</summary>
        public IReadOnlyList<ulong> Holders => _holders;
        /// <summary>True while anybody holds it.</summary>
        public bool IsHeld => _holders.Count > 0;
        /// <summary>True while still in the ground.</summary>
        public bool Planted => IsSpawned && !IsServer ? _netPlanted.Value : _planted;
        /// <summary>True on the server or offline.</summary>
        public bool Authority => !IsSpawned || IsServer;
        /// <summary>True when the definition came from Economy's data.</summary>
        public bool UsesEconomyDefinition { get; private set; }

        public Transform GoodsTransform => transform;
        public GoodsDefinition Definition => _def != null ? _def : (_def = Resolve());
        public string GoodsId => Definition != null ? Definition.GoodsId : goodsId;
        public float BruisePoints => IsSpawned && !IsServer ? _netBruise.Value : _bruise;
        public BruiseState BruiseState => CritterGoods.StateFor(BruisePoints);
        public float Freshness => 1f;
        public int CurrentValue => Mathf.RoundToInt((Definition != null ? Definition.BaseValue : fallbackBaseValue) * BruiseMult(BruiseState));
        public ulong LastHandlerId => _touches.LastToucher(Time.time) is var id && id != PlayerIds.None ? id : _lastHandler;
        public GoodsTag Tags => (Definition != null ? Definition.DefaultTags : GoodsTag.None) | GoodsTag.Cursed;

        public Transform CarryTransform => transform;
        public int HandsRequired => Definition != null ? Definition.CarryHands : fallbackCarryHands;
        public float MassKg => Definition != null ? Definition.MassKg : fallbackMassKg;
        public bool IsKinematicCarry => false;

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _def = Resolve();
            if (_rb != null && _def != null) _rb.mass = _def.MassKg;
            SetPlanted(startPlanted);
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer) { _netPlanted.Value = _planted; _netBruise.Value = _bruise; }
            else if (_rb != null) _rb.isKinematic = true; // NetworkTransform drives clients
        }

        GoodsDefinition Resolve()
        {
            if (Content.Goods.TryGet(goodsId, out var d)) { UsesEconomyDefinition = true; return d; }
            UsesEconomyDefinition = false;
            var s = ScriptableObject.CreateInstance<GoodsDefinition>();
            s.hideFlags = HideFlags.DontSave;
            s.name = goodsId + " (stub)";
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            JsonUtility.FromJsonOverwrite("{" +
                $"\"<GoodsId>k__BackingField\":\"{goodsId}\",\"<Category>k__BackingField\":{(int)GoodsCategory.Crop}," +
                $"\"<BaseValue>k__BackingField\":{fallbackBaseValue},\"<Fragility>k__BackingField\":{fallbackFragility.ToString(inv)}," +
                $"\"<CarryHands>k__BackingField\":{fallbackCarryHands},\"<MassKg>k__BackingField\":{fallbackMassKg.ToString(inv)}," +
                $"\"<DefaultTags>k__BackingField\":{(int)GoodsTag.Cursed}" + "}", s);
            return s;
        }

        /// <summary>Editor builder: id + fallback values.</summary>
        public void Configure(string id, int baseValue, float fragility, int hands, float massKg, bool planted)
        {
            goodsId = id; fallbackBaseValue = baseValue; fallbackFragility = fragility; fallbackCarryHands = hands; fallbackMassKg = massKg;
            startPlanted = planted; _def = null;
        }

        /// <summary>Server: plants (kinematic, no gravity) or frees (dynamic) the crop.</summary>
        public void SetPlanted(bool planted)
        {
            _planted = planted;
            if (IsSpawned && IsServer) _netPlanted.Value = planted;
            if (_rb == null || (IsSpawned && !IsServer)) return;
            _rb.isKinematic = planted;
            _rb.useGravity = !planted;
            _rb.interpolation = planted ? RigidbodyInterpolation.None : RigidbodyInterpolation.Interpolate;
        }

        // ------------------------------------------------------------ ICarryable

        public void OnGrabbed(ulong clientId)
        {
            if (!Authority || _holders.Contains(clientId)) return;
            bool first = _holders.Count == 0;
            _holders.Add(clientId);
            RecordTouch(clientId);
            if (_planted) { SetPlanted(false); Pulled?.Invoke(clientId); }
            Grabbed?.Invoke(clientId, first);
        }

        public void OnReleased(ulong clientId)
        {
            if (!Authority || !_holders.Remove(clientId)) return;
            RecordTouch(clientId);
            Released?.Invoke(clientId, _holders.Count == 0);
        }

        public void ApplyThrow(Vector3 velocity, ulong thrower)
        {
            if (!Authority) return;
            _holders.Clear();
            RecordTouch(thrower);
            _thrower = thrower;
            if (_planted) SetPlanted(false);
            if (_rb != null && !_rb.isKinematic) _rb.linearVelocity = velocity;
            Thrown?.Invoke(velocity, thrower);
            Released?.Invoke(thrower, true);
        }

        void OnCollisionEnter(Collision c)
        {
            if (!Authority || _planted) return;
            float impulse = ImpactUnits.ToDesign(c.impulse.magnitude);
            Impact?.Invoke(impulse);
            if (IsHeld) return;
            ulong who = PlayerIds.IsPlayer(_thrower) ? _thrower : LastHandlerId;
            ApplyBruise(impulse, who, PlayerIds.IsPlayer(_thrower) ? BlameVerb.Threw : BlameVerb.Dropped);
            _thrower = PlayerIds.None;
        }

        // ------------------------------------------------------------ IGoods

        public void ApplyBruise(float impulse, ulong instigator, BlameVerb verb)
        {
            if (!Authority) return;
            var def = Definition;
            float delta = BruiseDelta(impulse, def != null ? def.Fragility : fallbackFragility);
            if (delta <= 0f) return;
            int before = CurrentValue;
            _bruise = Mathf.Min(100f, _bruise + delta);
            if (IsSpawned) _netBruise.Value = _bruise;
            int lost = Mathf.Max(0, before - CurrentValue);
            OnBruised?.Invoke(new BruiseReport { Item = this, DeltaPoints = delta, DeltaDollars = lost, InstigatorId = instigator, Verb = verb });
            if (lost >= 1 && Services.TryGet(out IEconomyService eco))
            {
                bool player = PlayerIds.IsPlayer(instigator);
                eco.EmitBlame(new BlameEvent
                {
                    Timestamp = Time.time,
                    Instigators = player ? new[] { instigator } : Array.Empty<ulong>(),
                    BlameShares = player ? new[] { 1f } : Array.Empty<float>(),
                    PrimaryInstigator = player ? instigator : PlayerIds.None,
                    Verb = verb, ItemId = GoodsId, ItemTag = "Goods.Crop.Cursed", DollarDelta = -lost,
                    Location = transform.position, CauseTag = GoodsId
                });
            }
        }

        public void RecordTouch(ulong clientId)
        {
            if (!PlayerIds.IsPlayer(clientId)) return;
            _touches.Record(clientId, Time.time);
            _lastHandler = clientId;
        }

        /// <summary>goods-value §4 bruise points for one hit (design units).</summary>
        public static float BruiseDelta(float impulse, float fragility) => Mathf.Min(PerHitCap, Mathf.Max(0f, impulse - ImpulseMin) / ImpulseScale * fragility);

        /// <summary>Crop value multiplier per tier (ruined crops are compost: 0).</summary>
        public static float BruiseMult(BruiseState s) => s switch
        {
            BruiseState.Pristine => 1f, BruiseState.Scuffed => 0.85f, BruiseState.Bruised => 0.6f, BruiseState.Busted => 0.3f, _ => 0f
        };

        /// <summary>Server: removes the crop (sold, cleanup).</summary>
        public void Remove()
        {
            if (IsSpawned) { if (IsServer) NetworkObject.Despawn(true); }
            else Destroy(gameObject);
        }
    }
}
