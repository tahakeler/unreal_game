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

    /// <summary>
    /// Every economy/day-loop/ledger tuning knob in one asset (goods-value, day-loop, shared-wallet-economy,
    /// week-quota, blame-ledger, hud-menus §Tuning Knobs). Loaded from Resources/Data/Goods/EconomyTuning;
    /// <see cref="Load"/> falls back to these defaults when the asset is missing.
    /// </summary>
    [CreateAssetMenu(menuName = "HarvestPanic/Economy Tuning", fileName = "EconomyTuning")]
    public sealed class EconomyTuning : ScriptableObject
    {
        public const string ResourcePath = "Data/Goods/EconomyTuning";

        [Header("Bruising (goods-value §4). Impulse is in design units: dv(m/s) x ImpactSpeedToDesignImpulse")]
        public float ImpulseMin = 400f;
        public float ImpulseScale = 40f;
        [Tooltip("Converts contract N*s / mass (= delta-v, m/s) into GDD impulse units (5.4 m/s shoulder drop -> ~1200).")]
        public float ImpactSpeedToDesignImpulse = 222f;
        public float PerHitBruiseCap = 60f;
        public float ImpulseSolverErrorAbove = 20000f;
        public float ScuffedAt = 10f, BruisedAt = 35f, BustedAt = 65f, RuinedAt = 100f;
        public float MultPristine = 1f, MultScuffed = 0.85f, MultBruised = 0.6f, MultBusted = 0.3f;
        public float MultRuinedCrop = 0f, MultRuinedLivestock = 0.25f;
        public int CompostValue = 1;
        public float WaterBpsCrop = 15f, WaterBpsLivestock = 5f;

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
            new OrderableGoods { GoodsId = "Product.EggCrate", BaseQuantity = 2 },
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

        [Header("Blame ledger")]
        public float MergeWindow = 1.5f;
        public int NoiseFloorDollars = 2;
        public float TouchWindow = 3f;
        public float[] TouchShares = { 0.6f, 0.3f, 0.1f };
        public int DisasterSeverity = 60;
        public int ReceiptLinesPerPlayer = 5;
        public int DisasterScoreBonus = 15;
        public int MaxEventsPerDay = 500;

        [Header("Landlord (landlord-events)")]
        public int MaxLandlordRules = 3;

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
