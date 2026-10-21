using System;
using UnityEngine;

namespace HarvestPanic.Economy
{
    /// <summary>Per-goods order tuning (day-loop "BaseQ_species"; not in the GDD table, designer-owned).</summary>
    [Serializable]
    public struct OrderableGoods
    {
        public string GoodsId;
        [Min(1)] public int BaseQuantity;
    }

    /// <summary>Extra loose goods spawned each Morning per player beyond the first (beta-bugs-1 balance, tuned 2026-10-21).</summary>
    [Serializable]
    public struct ExtraDailyGoods
    {
        public string GoodsId;
        [Min(0)] public int PerExtraPlayer;
    }

    /// <summary>
    /// Every economy/day-loop/ledger tuning knob in one asset (goods-value, day-loop, shared-wallet-economy,
    /// week-quota, blame-ledger, hud-menus §Tuning Knobs). Loaded from Resources/Data/Goods/EconomyTuning;
    /// <see cref="Load"/> falls back to these defaults when the asset is missing.
    /// </summary>
    [CreateAssetMenu(menuName = "HarvestPanic/Economy Tuning", fileName = "EconomyTuning")]
    public sealed class EconomyTuning : ScriptableObject
    {
        public const string ResourcePath = "Data/Goods/EconomyTuning";

        [Header("Bruising (goods-value §4). Impulse in design units (Core ImpactUnits)")]
        public float ImpulseMin = 400f;
        public float ImpulseScale = 40f;
        [Tooltip("Seconds after release during which the Player carry system reports bruises (we skip own collisions).")]
        public float CarryReportWindow = 2f;
        public float PerHitBruiseCap = 60f;
        public float ImpulseSolverErrorAbove = 20000f;
        public float ScuffedAt = 10f, BruisedAt = 35f, BustedAt = 65f, RuinedAt = 100f;
        public float MultPristine = 1f, MultScuffed = 0.85f, MultBruised = 0.6f, MultBusted = 0.3f;
        public float MultRuinedCrop = 0f, MultRuinedLivestock = 0.25f;
        public int CompostValue = 1;
        public float WaterBpsCrop = 15f, WaterBpsLivestock = 5f;
        [Tooltip("IGoodsSoakable.ApplySoak = this many seconds of submersion.")] public float SoakSeconds = 1f;
        [Tooltip("Seconds after a goods item spawns during which impacts don't bruise it while nobody has touched it yet " +
                 "(spawn settle, fix/beta-bugs-1). The first grab/throw/touch ends the window early.")]
        public float SpawnGraceSeconds = 3f;

        [Header("Freshness (goods-value §4)")]
        public float CropGrace = 120f, CropDecay = 480f;
        public float ProductGrace = 180f, ProductDecay = 600f;
        public float FreshnessFloor = 0.5f;

        [Header("Day loop (day-loop §3, §7)")]
        public float MorningSeconds = 30f;
        public float WorkdaySeconds = 540f;
        public float DuskSeconds = 180f;
        public float SundownWarningSeconds = 30f;
        public float SundownGraceSeconds = 2f;
        [Tooltip("Dusk vote is not built yet; when true every day has a dusk phase.")]
        public bool DuskEnabled = true;
        public float DuskBonus = 1.25f;
        public float MarketAcceptSeconds = 2f;
        public float OrderMultiplier = 1.5f;
        public int OrderLines = 3;
        public int OrderCompleteBonusPerWeek = 60;
        public float OrderWeeklyGrowth = 0.15f;
        [Tooltip("0 = Payout waits for the host's Next Day button.")]
        public float PayoutAutoAdvanceSeconds = 0f;
        public int DaysPerWeek = 5;
        public float WeatherChancePerDay = 0.5f;
        public OrderableGoods[] Orderable =
        {
            new OrderableGoods { GoodsId = "Livestock.Pig", BaseQuantity = 2 },
            new OrderableGoods { GoodsId = "Livestock.Chicken", BaseQuantity = 3 },
            new OrderableGoods { GoodsId = "Product.Egg", BaseQuantity = 6 },
            new OrderableGoods { GoodsId = "Product.Crate", BaseQuantity = 2 },
            new OrderableGoods { GoodsId = "Crop.PrizePumpkin", BaseQuantity = 1 },
            new OrderableGoods { GoodsId = "Crop.Turnip", BaseQuantity = 5 },
            new OrderableGoods { GoodsId = "Crop.Carrot", BaseQuantity = 5 },
        };

        [Header("Wallet (shared-wallet-economy)")]
        public int StartingBalance = 100;
        public int OverdraftFloor = -200;
        public int BalanceCap = 2_000_000_000;
        public float RespawnFeeBase = 25f;
        public float RespawnFeeWeeklyGrowth = 0.25f;
        public float DemandDayMin = 0.8f, DemandDayMax = 1.2f;
        public int SaturationUnits = 6;
        public float SaturationDecay = 0.95f;
        public float DemandMin = 0.6f, DemandMax = 1.5f;

        [Header("Quota (week-quota)")]
        public float QuotaBase = 1200f;
        public float QuotaGrowth = 1.35f;
        public float RegionMult = 1f;
        public float[] PlayerScale = { 0.45f, 0.65f, 0.85f, 1f };

        [Header("Daily loose goods by crew size (GDD 9.3 / 9.11, tuned 2026-10-21)")]
        [Tooltip("Extra goods spawned around the matching Goods markers each Morning, per player beyond the first " +
                 "(the level's markers are the solo supply). docs/economy/week1-income-model.md has the math.")]
        public ExtraDailyGoods[] ExtraDailyGoodsPerExtraPlayer =
        {
            new ExtraDailyGoods { GoodsId = "Product.Crate", PerExtraPlayer = 2 },
        };
        [Tooltip("Minimum gap between spawned goods footprints, m (Morning spawn layout).")] public float SpawnSpacing = 0.25f;

        [Header("Blame ledger")]
        public float MergeWindow = 1.5f;
        public int NoiseFloorDollars = 2;
        public float TouchWindow = 3f;
        public float[] TouchShares = { 0.6f, 0.3f, 0.1f };
        public int DisasterSeverity = 60;
        public int ReceiptLinesPerPlayer = 5;
        public int DisasterScoreBonus = 15;
        public int MaxEventsPerDay = 500;

        [Header("Shop / upgrades (farm-upgrades §7, week-quota §7)")]
        [Range(0.25f, 0.75f)] public float SellBackRate = 0.5f;
        [Range(4, 8)] public int OffersPerShop = 6;
        [Tooltip("Sunday Collateral window when short (s).")] public float CollateralSeconds = 60f;
        [Tooltip("0 = shop waits for the host's Done button.")] public float ShopAutoAdvanceSeconds = 0f;
        [Tooltip("Seconds a crew proposal waits for the host before expiring.")] public float ProposalTimeoutSeconds = 20f;

        [Header("Landlord (landlord-events)")]
        public int MaxLandlordRules = 3;
        [Tooltip("Mid-week visits in W2-3 / W4+ (0 in W1).")] public int VisitsPerWeekEarly = 1, VisitsPerWeekLate = 2;
        public int InspectionFinePerCheck = 25;
        [Tooltip("+20%/week from W2.")] public float InspectionFineWeeklyGrowth = 0.2f;
        [Tooltip("Max inspection fines per week.")] public int InspectionCap = 150;
        public int InspectionChecksPicked = 3;
        [Tooltip("Workday fraction at which the inspector checks the farm.")] [Range(0.1f, 0.9f)] public float InspectionAtWorkday01 = 0.5f;
        [Tooltip("Livestock farther than this from the barn respawn marker counts as loose (m).")] public float LooseLivestockRadius = 18f;
        public int FarmWalkFinePerHit = 50, FarmWalkMaxHits = 3;
        [Tooltip("Visit deferred past Friday by the chaos cap.")] public int MissedAppointmentFee = 50;

        [Header("Landlord Sunday walk-in (presentation)")]
        public float VisitWalkSpeed = 2.4f;
        [Tooltip("Spawn distance from the farmhouse door (m).")] public float VisitWalkDistance = 9f;
        public float VisitGreetSeconds = 2.2f, VisitCountSeconds = 2.4f, VisitVerdictSeconds = 3.5f;

        [Header("Respawn fee player scaling (GDD ch.9 open issue 5)")]
        [Tooltip("Fee × S_p^exp; 0.5 → solo 0.67×, 2p 0.81×, 3p 0.92×, 4p 1×. 0 disables.")] [Range(0f, 1f)] public float RespawnFeePlayerExponent = 0.5f;

        [Header("Awards")]
        [Range(0, 5)] public int DailyAwardsMax = 3;

        static EconomyTuning _cached;

        /// <summary>Asset from Resources, or a defaults instance.</summary>
        public static EconomyTuning Load()
        {
            if (_cached != null) return _cached;
            _cached = Resources.Load<EconomyTuning>(ResourcePath);
            if (_cached == null) _cached = CreateInstance<EconomyTuning>();
            return _cached;
        }

        /// <summary>Player scale S_p for 1..4 players (clamped).</summary>
        public float PlayerScaleFor(int players) =>
            PlayerScale[Mathf.Clamp(players, 1, PlayerScale.Length) - 1];
    }
}
