using System;
using UnityEngine;

namespace HarvestPanic.AI.Crops
{
    /// <summary>
    /// Knobs for the cursed crops (cursed-crops §3, §7). Authored at <c>Resources/Data/Critters/CursedCropTuning.asset</c>
    /// (AI-owned data). Values (price, hands, fragility, mass) live in the crops' <c>GoodsDefinition</c> assets.
    /// </summary>
    [CreateAssetMenu(menuName = "HarvestPanic/Cursed Crop Tuning", fileName = "CursedCropTuning")]
    public sealed class CursedCropTuning : ScriptableObject
    {
        public const string ResourcePath = "Data/Critters/CursedCropTuning";

        /// <summary>Spawn rule per crop: first day (0-based run day) it is planted in the field.</summary>
        [Serializable] public struct SpawnRule { public string GoodsId; [Min(0)] public int UnlockDayIndex; }

        [Header("Spawn rules (day-loop: crops are topped up each morning once unlocked)")]
        public SpawnRule[] Rules =
        {
            new SpawnRule { GoodsId = "Crop.Mandrake", UnlockDayIndex = 2 },    // week 1, day 3
            new SpawnRule { GoodsId = "Crop.TumbleMelon", UnlockDayIndex = 3 }, // week 1, day 4
        };

        [Header("Screaming Mandrake")]
        [Tooltip("Scream loudness (cursed-crops 0.9).")] [Range(0, 1)] public float ScreamLoudness = 0.9f;
        [Tooltip("Scream length (s).")] [Min(0.1f)] public float ScreamSeconds = 3f;
        [Tooltip("Cooldown between scream starts (s).")] [Min(0)] public float ScreamCooldown = 8f;
        [Tooltip("Jostle trigger: carrier acceleration (m/s^2; GDD 800 cm/s^2).")] [Min(0)] public float JostleAccel = 8f;
        [Tooltip("Jostle trigger: impact impulse in design units (GDD 200).")] [Min(0)] public float JostleImpact = 200f;
        [Tooltip("Acceleration low-pass time constant (s).")] [Min(0.01f)] public float AccelSmoothing = 0.08f;
        [Tooltip("Players within this radius are stunned (m).")] [Min(0)] public float StunRadius = 3f;
        [Tooltip("Stagger impulse (design units, below the 900 ragdoll threshold).")] [Min(0)] public float StunImpulse = 450f;
        [Tooltip("Burlap Sack loudness multiplier (-70%).")] [Range(0, 1)] public float SackMult = 0.3f;
        [Tooltip("Wagon damping multiplier (-20%).")] [Range(0, 1)] public float WagonMult = 0.8f;
        [Tooltip("How deep a planted mandrake sits (m).")] [Min(0)] public float PlantDepth = 0.22f;

        [Header("Tumble Melon")]
        [Tooltip("Seconds resting after being set down before it rolls off.")] [Min(0)] public float SettleSeconds = 0.6f;
        [Tooltip("Readable wobble before each roll (s).")] [Min(0)] public float WobbleSeconds = 0.6f;
        [Tooltip("Roll launch speed (m/s).")] [Min(0)] public float RollSpeed = 3.2f;
        [Tooltip("A player this close to a resting melon makes it juke away (m).")] [Min(0)] public float SpookRadius = 3f;
        [Tooltip("Cooldown between spook rolls (s).")] [Min(0)] public float SpookCooldown = 5f;
        [Tooltip("Slope (deg) above which it rolls downhill instead of away from players.")] [Range(0, 45)] public float DownhillSlopeDeg = 4f;
        [Tooltip("Beyond this distance from its field it rolls back toward it (m).")] [Min(1)] public float LeashRadius = 10f;
        [Tooltip("Players further than this are ignored when picking a roll direction (m).")] [Min(0)] public float PlayerAwareness = 7f;
        [Tooltip("Speed below which it counts as resting (m/s).")] [Min(0)] public float RestSpeed = 0.25f;

        static CursedCropTuning _default;
        public static CursedCropTuning Default
        {
            get
            {
                if (_default != null) return _default;
                _default = Resources.Load<CursedCropTuning>(ResourcePath);
                if (_default == null) { _default = CreateInstance<CursedCropTuning>(); _default.hideFlags = HideFlags.DontSave; }
                return _default;
            }
        }

        /// <summary>Unlock day for <paramref name="goodsId"/> (int.MaxValue when it has no rule).</summary>
        public int UnlockDay(string goodsId)
        {
            if (Rules != null) foreach (var r in Rules) if (string.Equals(r.GoodsId, goodsId, StringComparison.OrdinalIgnoreCase)) return r.UnlockDayIndex;
            return int.MaxValue;
        }
    }
}
