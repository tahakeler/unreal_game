using System;
using HarvestPanic.Core;
using Unity.Netcode;
using UnityEngine;

namespace HarvestPanic.AI
{
    /// <summary>
    /// <see cref="IGoods"/> for a living critter. Uses Economy's <see cref="GoodsDefinition"/> when one exists
    /// (looked up by the personality's GoodsId, then by <see cref="fallbackShortId"/>); until Economy's data merges it
    /// builds a runtime stub from the fallback fields below. Bruise math follows goods-value §4
    /// (ΔBP = max(0, I - 400)/40 × Fragility, cap 60/hit); Economy may replace this with its own component later.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CritterGoods : NetworkBehaviour, IGoods
    {
        [Header("Lookup")]
        [SerializeField, Tooltip("Second lookup key, e.g. 'pig'")] string fallbackShortId = "pig";
        [Header("Fallback definition (used only when Economy's GoodsDefinition is missing)")]
        [SerializeField] string fallbackGoodsId = "Livestock.Pig";
        [SerializeField, Min(0)] int fallbackBaseValue = 60;
        [SerializeField, Range(0.25f, 2f)] float fallbackFragility = 0.8f;
        [SerializeField, Range(1, 8)] int fallbackCarryHands = 2;
        [SerializeField, Min(0.01f)] float fallbackMassKg = 30f;

        const float ImpulseMin = 400f, ImpulseScale = 40f, PerHitCap = 60f, LivestockRuinedMult = 0.25f;

        readonly NetworkVariable<float> _netBruise = new NetworkVariable<float>();
        readonly TouchHistory _touches = new TouchHistory(3f, 4);
        GoodsDefinition _def;
        float _bruise;
        GoodsTag _extraTags;
        CritterBrain _brain;

        /// <inheritdoc/>
        public event Action<BruiseReport> OnBruised;

        /// <inheritdoc/>
        public Transform GoodsTransform => transform;
        /// <inheritdoc/>
        public GoodsDefinition Definition => _def != null ? _def : (_def = Resolve());
        /// <inheritdoc/>
        public string GoodsId => Definition != null ? Definition.GoodsId : fallbackGoodsId;
        /// <inheritdoc/>
        public float BruisePoints => IsSpawned && !IsServer ? _netBruise.Value : _bruise;
        /// <inheritdoc/>
        public BruiseState BruiseState => StateFor(BruisePoints);
        /// <inheritdoc/>
        public float Freshness => 1f;
        /// <inheritdoc/>
        public int CurrentValue => Mathf.RoundToInt((Definition != null ? Definition.BaseValue : fallbackBaseValue) * BruiseMult(BruiseState));
        /// <inheritdoc/>
        public ulong LastHandlerId => _touches.LastToucher(Time.time) is var id && id != PlayerIds.None ? id : _lastHandler;
        /// <inheritdoc/>
        public GoodsTag Tags => (Definition != null ? Definition.DefaultTags : GoodsTag.None) | _extraTags | (BruiseState == BruiseState.Ruined ? GoodsTag.Grumpy : GoodsTag.None);
        /// <summary>True when the definition came from Economy's data (false = runtime stub).</summary>
        public bool UsesEconomyDefinition { get; private set; }

        ulong _lastHandler = PlayerIds.None;

        void Awake()
        {
            _brain = GetComponent<CritterBrain>();
            _def = Resolve();
            var rb = GetComponent<Rigidbody>();
            if (rb != null && _def != null) rb.mass = _def.MassKg;
        }

        GoodsDefinition Resolve()
        {
            string id = _brain != null && _brain.Personality != null && !string.IsNullOrEmpty(_brain.Personality.GoodsId)
                ? _brain.Personality.GoodsId : fallbackGoodsId;
            if (Content.Goods.TryGet(id, out var d) || Content.Goods.TryGet(fallbackShortId, out d)) { UsesEconomyDefinition = true; return d; }
            UsesEconomyDefinition = false;
            return BuildStub(id);
        }

        GoodsDefinition BuildStub(string id)
        {
            var d = ScriptableObject.CreateInstance<GoodsDefinition>();
            d.hideFlags = HideFlags.DontSave;
            d.name = id + " (stub)";
            string json = "{" +
                $"\"<GoodsId>k__BackingField\":\"{id}\"," +
                $"\"<Category>k__BackingField\":{(int)GoodsCategory.Livestock}," +
                $"\"<BaseValue>k__BackingField\":{fallbackBaseValue}," +
                $"\"<Fragility>k__BackingField\":{fallbackFragility.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                $"\"<CarryHands>k__BackingField\":{fallbackCarryHands}," +
                $"\"<MassKg>k__BackingField\":{fallbackMassKg.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                "\"<IsAlive>k__BackingField\":true}";
            JsonUtility.FromJsonOverwrite(json, d);
            return d;
        }

        /// <summary>Configures the fallback stub (editor builder).</summary>
        public void ConfigureFallback(string goodsId, string shortId, int baseValue, float fragility, int carryHands, float massKg)
        {
            fallbackGoodsId = goodsId; fallbackShortId = shortId; fallbackBaseValue = baseValue;
            fallbackFragility = fragility; fallbackCarryHands = carryHands; fallbackMassKg = massKg;
            _def = null;
        }

        /// <inheritdoc/>
        public void ApplyBruise(float impulse, ulong instigator, BlameVerb verb)
        {
            if (IsSpawned && !IsServer) return;
            var def = Definition;
            float frag = def != null ? def.Fragility : fallbackFragility;
            float delta = Mathf.Min(PerHitCap, Mathf.Max(0f, impulse - ImpulseMin) / ImpulseScale * frag);
            if (delta <= 0f) return;
            int before = CurrentValue;
            var oldState = BruiseState;
            _bruise = Mathf.Min(100f, _bruise + delta);
            if (IsSpawned) _netBruise.Value = _bruise;
            int lost = Mathf.Max(0, before - CurrentValue);
            var report = new BruiseReport { Item = this, DeltaPoints = delta, DeltaDollars = lost, InstigatorId = instigator, Verb = verb };
            OnBruised?.Invoke(report);
            if (BruiseState == BruiseState.Ruined && oldState != BruiseState.Ruined && _brain != null && _brain.Personality != null)
                _brain.SpeedMultiplier = _brain.Personality.GrumpyMods.FleeSpeedMult;
            if (lost >= 1 && Services.TryGet(out IEconomyService eco))
            {
                bool player = PlayerIds.IsPlayer(instigator);
                eco.EmitBlame(new BlameEvent
                {
                    Timestamp = Time.time,
                    Instigators = player ? new[] { instigator } : Array.Empty<ulong>(),
                    BlameShares = player ? new[] { 1f } : Array.Empty<float>(),
                    PrimaryInstigator = player ? instigator : PlayerIds.None,
                    Verb = verb,
                    ItemId = GoodsId,
                    ItemTag = "Goods.Livestock",
                    DollarDelta = -lost,
                    Location = transform.position,
                    CauseTag = _brain != null && _brain.Personality != null ? _brain.Personality.CritterId : ""
                });
            }
        }

        /// <inheritdoc/>
        public void RecordTouch(ulong clientId)
        {
            if (!PlayerIds.IsPlayer(clientId)) return;
            _touches.Record(clientId, Time.time);
            _lastHandler = clientId;
        }

        /// <summary>Bruise tier from points (goods-value: &lt;10, 10-34, 35-64, 65-99, 100).</summary>
        public static BruiseState StateFor(float bp) =>
            bp >= 100f ? BruiseState.Ruined : bp >= 65f ? BruiseState.Busted : bp >= 35f ? BruiseState.Bruised : bp >= 10f ? BruiseState.Scuffed : BruiseState.Pristine;

        /// <summary>Livestock value multiplier per tier.</summary>
        public static float BruiseMult(BruiseState s) => s switch
        {
            BruiseState.Pristine => 1f, BruiseState.Scuffed => 0.85f, BruiseState.Bruised => 0.6f, BruiseState.Busted => 0.3f, _ => LivestockRuinedMult
        };
    }
}
