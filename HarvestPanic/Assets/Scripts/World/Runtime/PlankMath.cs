using UnityEngine;

namespace HarvestPanic.World
{
    /// <summary>
    /// Pure bridge-plank formulas from design/gdd/bridge-hazards.md section 4. No Unity state, so EditMode tests
    /// cover them directly.
    /// </summary>
    public static class PlankMath
    {
        /// <summary>Default safe load per plank (kg).</summary>
        public const float DefaultSafeLoadKg = 250f;
        /// <summary>Damage per kg over the safe load per second.</summary>
        public const float DamageRate = 0.2f;
        /// <summary>Integrity at or below which a plank is cracked.</summary>
        public const float CrackThreshold = 50f;
        /// <summary>Full integrity.</summary>
        public const float MaxIntegrity = 100f;
        /// <summary>Impacts above this impulse (N*s) deal instant damage.</summary>
        public const float ImpactThreshold = 800f;
        /// <summary>Impulse divisor for instant impact damage.</summary>
        public const float ImpactDivisor = 40f;
        /// <summary>Player body mass counted on a plank when the player has no Rigidbody (kg).</summary>
        public const float PlayerMassKg = 70f;

        /// <summary>
        /// <c>max(0, load - SafeLoad * safeLoadMult) * 0.2 * (1 + 0.5 * cracked) * breakMult</c>.
        /// </summary>
        public static float DamagePerSecond(float loadKg, bool cracked, float safeLoadKg = DefaultSafeLoadKg,
            float safeLoadMult = 1f, float breakMult = 1f)
        {
            float over = Mathf.Max(0f, loadKg - safeLoadKg * safeLoadMult);
            return over * DamageRate * (cracked ? 1.5f : 1f) * breakMult;
        }

        /// <summary>Instant damage of a single impact (0 below 800 N*s).</summary>
        public static float ImpactDamage(float impulse) => impulse > ImpactThreshold ? impulse / ImpactDivisor : 0f;

        /// <summary>True once integrity is at or below the crack threshold.</summary>
        public static bool IsCracked(float integrity) => integrity <= CrackThreshold;

        /// <summary>Seconds a constant load needs to break a fresh plank (infinity when it never does).</summary>
        public static float SecondsToBreak(float loadKg, float safeLoadKg = DefaultSafeLoadKg)
        {
            float a = DamagePerSecond(loadKg, false, safeLoadKg);
            if (a <= 0f) return float.PositiveInfinity;
            return (MaxIntegrity - CrackThreshold) / a + CrackThreshold / DamagePerSecond(loadKg, true, safeLoadKg);
        }

        /// <summary>Vertical sag of a rope bridge at <paramref name="t"/> in [0,1] along its length (parabola, 0 at the ends).</summary>
        public static float Sag(float t, float maxSag) => -maxSag * (1f - (2f * t - 1f) * (2f * t - 1f));
    }
}
