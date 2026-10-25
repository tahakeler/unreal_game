using UnityEngine;

namespace HarvestPanic.Gameplay
{
    /// <summary>
    /// First-person arm pose chosen from the carry state (player-character 5b). The poses after
    /// <see cref="ThrowSwing"/> are asked for by the held tool or vehicle (<see cref="IToolArmPose"/>, farm-tools / wagon).
    /// </summary>
    public enum ArmPose : byte { Idle, Reach, GripOneHand, GripTwoHands, Tool, ThrowSwing, LassoTwirl, LassoHold, LassoReel, WhistleBlow, WagonPush }

    /// <summary>Pure arm math (no scene access) so it is EditMode-testable.</summary>
    public static class ArmMath
    {
        /// <summary>Picks the pose. A throw follow-through overrides everything for its duration.</summary>
        public static ArmPose Select(bool holding, bool tool, int handsRequired, bool aiming, bool swinging) =>
            Select(holding, tool, handsRequired, aiming, swinging, ArmPose.Idle);

        /// <summary>
        /// Picks the pose; <paramref name="heldOverride"/> is the held tool/vehicle's own pose (lasso twirl, whistle,
        /// wagon push) and wins over the generic grip while holding. Anything below <see cref="ArmPose.LassoTwirl"/> is
        /// "no override".
        /// </summary>
        public static ArmPose Select(bool holding, bool tool, int handsRequired, bool aiming, bool swinging, ArmPose heldOverride)
        {
            if (swinging) return ArmPose.ThrowSwing;
            if (holding && IsToolPose(heldOverride)) return heldOverride;
            if (holding) return tool ? ArmPose.Tool : handsRequired >= 2 ? ArmPose.GripTwoHands : ArmPose.GripOneHand;
            return aiming ? ArmPose.Reach : ArmPose.Idle;
        }

        /// <summary>True for the tool/vehicle-specific poses.</summary>
        public static bool IsToolPose(ArmPose p) => p >= ArmPose.LassoTwirl;

        /// <summary>
        /// Hand-over-hand reel: how far (m, along the rope toward the body) each hand has pulled at
        /// <paramref name="phase"/> (cycles). The hands alternate: one pulls back while the other reaches forward.
        /// </summary>
        public static void ReelHands(float phase, float stroke, out float left, out float right)
        {
            float u = phase - Mathf.Floor(phase);
            float tri = u < 0.5f ? u * 2f : 2f - u * 2f; // 0..1..0
            left = tri * stroke;
            right = (1f - tri) * stroke;
        }

        /// <summary>Finger curl 0 (open) .. 1 (fist) for a pose.</summary>
        public static float Curl(ArmPose pose)
        {
            switch (pose)
            {
                case ArmPose.Reach: return 0.1f;
                case ArmPose.GripOneHand: return 0.55f;
                case ArmPose.GripTwoHands: return 0.45f;
                case ArmPose.Tool: return 0.92f;
                case ArmPose.ThrowSwing: return 0.05f;
                case ArmPose.LassoTwirl: return 0.88f;
                case ArmPose.LassoHold:
                case ArmPose.LassoReel: return 0.95f;
                case ArmPose.WhistleBlow: return 0.72f;
                case ArmPose.WagonPush: return 0.9f;
                default: return 0.62f;
            }
        }

        /// <summary>
        /// Two-bone IK. Returns the elbow; <paramref name="wrist"/> is the reached target (the target itself when
        /// reachable). When the target is beyond reach, both bones stretch up to <paramref name="maxStretch"/>;
        /// <paramref name="stretch"/> reports the factor used (1 = none).
        /// </summary>
        public static Vector3 SolveTwoBone(Vector3 shoulder, Vector3 target, float upper, float fore, Vector3 pole, float maxStretch,
            out Vector3 wrist, out float stretch)
        {
            Vector3 d = target - shoulder;
            float dist = d.magnitude;
            Vector3 n = dist > 1e-5f ? d / dist : Vector3.forward;
            float reach = upper + fore;
            stretch = 1f;
            if (dist > reach * 0.999f)
            {
                stretch = Mathf.Clamp(dist / (reach * 0.999f), 1f, Mathf.Max(1f, maxStretch));
                upper *= stretch; fore *= stretch; reach *= stretch;
            }
            dist = Mathf.Clamp(dist, Mathf.Abs(upper - fore) + 1e-3f, reach * 0.999f);
            wrist = shoulder + n * dist;
            float cosA = Mathf.Clamp((upper * upper + dist * dist - fore * fore) / (2f * upper * dist), -1f, 1f);
            float sinA = Mathf.Sqrt(Mathf.Max(0f, 1f - cosA * cosA));
            Vector3 perp = pole - n * Vector3.Dot(pole, n);
            if (perp.sqrMagnitude < 1e-8f) perp = Vector3.Cross(n, Vector3.right);
            if (perp.sqrMagnitude < 1e-8f) perp = Vector3.Cross(n, Vector3.up);
            perp.Normalize();
            return shoulder + n * (upper * cosA) + perp * (upper * sinA);
        }

        /// <summary>
        /// Palm targets on the left/right sides of a held box (camera space centre + half extents), gripping
        /// <paramref name="low"/> of the way toward its bottom, palms <paramref name="inset"/> outside the faces.
        /// </summary>
        public static void SideGrip(Vector3 center, Vector3 halfExtents, float low, float inset, float maxHalfSpread, out Vector3 left, out Vector3 right)
        {
            float hx = Mathf.Min(halfExtents.x + inset, maxHalfSpread);
            float y = center.y - halfExtents.y * low;
            float z = center.z - halfExtents.z * 0.6f; // near edge of the side faces (closest to the camera)
            left = new Vector3(center.x - hx, y, z);
            right = new Vector3(center.x + hx, y, z);
        }

        /// <summary>0..1..0 bell for a follow-through of <paramref name="duration"/> at time <paramref name="t"/>.</summary>
        public static float SwingCurve(float t, float duration)
        {
            if (duration <= 0f || t <= 0f || t >= duration) return 0f;
            float u = t / duration;
            return u < 0.35f ? Mathf.SmoothStep(0f, 1f, u / 0.35f) : Mathf.SmoothStep(1f, 0f, (u - 0.35f) / 0.65f);
        }
    }
}
