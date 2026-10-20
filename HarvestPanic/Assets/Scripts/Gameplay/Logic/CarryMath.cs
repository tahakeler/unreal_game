using UnityEngine;

namespace HarvestPanic.Gameplay
{
    /// <summary>
    /// Pure carry/throw/ragdoll formulas (design/gdd/physics-carry.md section 4, player-character.md section 4).
    /// No engine state; every input is a parameter so EditMode tests can pin the GDD examples.
    /// </summary>
    public static class CarryMath
    {
        /// <summary>
        /// Per-player <c>load_ratio = mass / (hands_attached * KgPerHand)</c> clamped 0..1 (0 when nothing is held).
        /// </summary>
        public static float LoadRatio(float massKg, int handsAttached, float kgPerHand = 25f)
        {
            if (handsAttached <= 0 || massKg <= 0f || kgPerHand <= 0f) return 0f;
            return Mathf.Clamp01(massKg / (handsAttached * kgPerHand));
        }

        /// <summary>
        /// GDD carry speed: <c>clamp(1 - 0.5 * mass / (hands * KgPerHand), floor, 1)</c>.
        /// 1-hand 5 kg chicken = 0.9; 8 hands on a 300 kg cow = 0.35.
        /// </summary>
        public static float CarrySpeed(float massKg, int handsAttached, float kgPerHand = 25f, float floor = 0.35f)
        {
            if (handsAttached <= 0 || kgPerHand <= 0f) return floor;
            return Mathf.Clamp(1f - 0.5f * (massKg / (handsAttached * kgPerHand)), floor, 1f);
        }

        /// <summary>
        /// Group factor for under-crewed loads (lead brief): <c>clamp(hands / required, floor, 1)</c>.
        /// Equals 1 once enough hands are attached; below that the body is dragged at this fraction.
        /// </summary>
        public static float GroupFactor(int handsAttached, int handsRequired, float floor = 0.35f)
        {
            if (handsRequired <= 0) return 1f;
            return Mathf.Clamp((float)handsAttached / handsRequired, floor, 1f);
        }

        /// <summary>
        /// Final movement multiplier applied to every carrier: <c>clamp(CarrySpeed * GroupFactor, floor, 1)</c>.
        /// </summary>
        public static float SpeedMultiplier(float massKg, int handsAttached, int handsRequired, float kgPerHand = 25f, float floor = 0.35f)
        {
            if (handsAttached <= 0) return 1f;
            return Mathf.Clamp(CarrySpeed(massKg, handsAttached, kgPerHand, floor) * GroupFactor(handsAttached, handsRequired, floor), floor, 1f);
        }

        /// <summary>
        /// Throw speed: <c>lerp(min, max, charge) * clamp(massRef / mass, massFloor, 1)</c> m/s.
        /// 2 kg turnip at full charge = 14; 40 kg pig = 4.2.
        /// </summary>
        public static float ThrowSpeed(float charge01, float massKg, float min = 4f, float max = 14f, float massRef = 10f, float massFloor = 0.3f)
        {
            float massMult = massKg <= 0f ? 1f : Mathf.Clamp(massRef / massKg, massFloor, 1f);
            return Mathf.Lerp(min, max, Mathf.Clamp01(charge01)) * massMult;
        }

        /// <summary>Charge 0..1 after holding Throw for <paramref name="heldSeconds"/>.</summary>
        public static float ThrowCharge(float heldSeconds, float chargeTime = 1f) =>
            chargeTime <= 0f ? 1f : Mathf.Clamp01(heldSeconds / chargeTime);

        /// <summary>
        /// Whether a single remaining holder may throw: they must be the only holder, have lifted it
        /// (hands &gt;= required) and, for 2-hand items, the mass must be at most <paramref name="heaveMaxMass"/>.
        /// Items needing more than 2 hands are never thrown (they are just released).
        /// </summary>
        public static bool CanThrow(int holderCount, int handsAttached, int handsRequired, float massKg, float heaveMaxMass = 40f)
        {
            if (holderCount != 1 || handsAttached < handsRequired) return false;
            if (handsRequired <= 1) return true;
            if (handsRequired == 2) return massKg <= heaveMaxMass;
            return false;
        }

        /// <summary>Hands one player contributes to an item: min(2, max(1, remaining needed)).</summary>
        public static int HandsContributed(int handsRequired, int handsAlreadyAttached)
        {
            int remaining = handsRequired - handsAlreadyAttached;
            return Mathf.Clamp(remaining, 1, 2);
        }

        /// <summary>
        /// Ragdoll threshold: <c>RagdollImpulse * (1 - f * load) * (stamina &lt; low ? lowMult : 1) * mud</c>.
        /// Range with defaults 302..900 design N*s.
        /// </summary>
        public static float RagdollThreshold(float ragdollImpulse, float loadRatio, float stamina, float mudMult = 1f,
            float loadFactor = 0.4f, float lowStamina = 20f, float lowStaminaMult = 0.7f)
        {
            float t = ragdollImpulse * (1f - loadFactor * Mathf.Clamp01(loadRatio));
            if (stamina < lowStamina) t *= lowStaminaMult;
            return t * mudMult;
        }

        /// <summary>Knockout impulse: at least 2x the base RagdollImpulse knob (bull charge 1800 = KO).</summary>
        public static bool IsKnockoutImpulse(float impulse, float ragdollImpulse) => impulse >= 2f * ragdollImpulse;

        /// <summary>Wobble noise amplitude: <c>amp * (1 + 2 * speed_ratio) * (1 + struggle)</c>.</summary>
        public static float WobbleAmplitude(float baseAmp, float speedRatio, float struggle) =>
            baseAmp * (1f + 2f * Mathf.Clamp01(speedRatio)) * (1f + Mathf.Max(0f, struggle));

        /// <summary>Net catch chance <c>clamp(0.95 - 0.1 * agility, 0.4, 0.95)</c> (farm-tools section 4).</summary>
        public static float NetCatchChance(float agility) => Mathf.Clamp(0.95f - 0.1f * agility, 0.4f, 0.95f);

        /// <summary>Spring-damper force for one hand, magnitude capped at <paramref name="maxForce"/>.</summary>
        public static Vector3 HandForce(Vector3 toTarget, Vector3 relativeVelocity, float stiffness, float damping, float maxForce)
        {
            Vector3 f = stiffness * toTarget - damping * relativeVelocity;
            return Vector3.ClampMagnitude(f, maxForce);
        }
    }
}
