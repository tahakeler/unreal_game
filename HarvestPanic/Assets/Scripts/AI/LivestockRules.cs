using UnityEngine;

namespace HarvestPanic.AI
{
    /// <summary>Pure decision rules for the livestock signature tasks (EditMode tested).</summary>
    public static class LivestockRules
    {
        /// <summary>States from which a goat may start a headbutt (not while fleeing, carried or stunned).</summary>
        public static bool StateAllowsHeadbutt(CritterState s) =>
            s == CritterState.Idle || s == CritterState.Wander || s == CritterState.Alert || s == CritterState.Calm || s == CritterState.Cornered;

        /// <summary>True when a target at <paramref name="toTarget"/> (flat) is a valid headbutt wind-up target.</summary>
        public static bool CanStartHeadbutt(CritterState state, Vector3 forward, Vector3 toTarget, bool targetDown, float cooldownLeft, LivestockTuning t)
        {
            if (!StateAllowsHeadbutt(state) || targetDown || cooldownLeft > 0f) return false;
            toTarget.y = 0f; forward.y = 0f;
            float d = toTarget.magnitude;
            if (d > t.HeadbuttRange || d < 1e-3f) return false;
            return Vector3.Angle(forward, toTarget) <= t.HeadbuttConeDeg;
        }

        /// <summary>The hit lands only if the target is still in reach at the end of the telegraph (dodgeable).</summary>
        public static bool HeadbuttConnects(Vector3 toTarget, bool targetDown, LivestockTuning t)
        {
            toTarget.y = 0f;
            return !targetDown && toTarget.magnitude <= t.HeadbuttHitReach;
        }

        /// <summary>Design-unit impulse vector: flat direction plus lift, magnitude = HeadbuttImpulse.</summary>
        public static Vector3 HeadbuttImpulse(Vector3 toTarget, Vector3 fallbackForward, LivestockTuning t)
        {
            toTarget.y = 0f;
            Vector3 dir = toTarget.sqrMagnitude > 1e-4f ? toTarget.normalized : fallbackForward.normalized;
            return (dir + Vector3.up * t.HeadbuttLift).normalized * t.HeadbuttImpulse;
        }

        /// <summary>A surface top is climbable when it is within the height band above the goat's feet.</summary>
        public static bool IsClimbable(float topY, float feetY, LivestockTuning t)
        {
            float h = topY - feetY;
            return h >= t.ClimbMinHeight && h <= t.ClimbMaxHeight;
        }

        /// <summary>Perched goat jumps down when bored, or when threatened after a minimum perch.</summary>
        public static bool ShouldLeavePerch(float perchedSeconds, float threat, float fleeThreshold, LivestockTuning t) =>
            perchedSeconds >= t.PerchBoredSeconds || (perchedSeconds >= t.PerchMinSeconds && threat >= fleeThreshold);

        /// <summary>Next moo time from now with symmetric jitter (roll 0..1).</summary>
        public static float NextMoo(float now, float roll, LivestockTuning t) =>
            now + Mathf.Max(1f, t.MooInterval + (roll * 2f - 1f) * t.MooJitter);

        /// <summary>Lift check matching physics-carry: carriers each add up to 2 hands.</summary>
        public static bool CanLift(int carriers, int handsRequired) => Mathf.Min(carriers * 2, 8) >= Mathf.Max(1, handsRequired);

        /// <summary>Minimum carriers to lift.</summary>
        public static int CarriersToLift(int handsRequired) => Mathf.Max(1, (handsRequired + 1) / 2);
    }
}
