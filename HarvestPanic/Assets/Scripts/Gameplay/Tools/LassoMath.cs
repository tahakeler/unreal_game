using HarvestPanic.Core;
using UnityEngine;

namespace HarvestPanic.Gameplay
{
    /// <summary>What a lasso loop landed on.</summary>
    public enum LassoTargetKind : byte { None, Critter, Goods, Player }

    /// <summary>Outcome of a lasso throw against one candidate (farm-tools rule 3 and section 5).</summary>
    public enum LassoVerdict : byte { Hit, Nothing, OutOfRange, TooSmall, AwakePlayer, Self, Held }

    /// <summary>Ground under the person holding the rope (farm-tools section 4, ground_mult).</summary>
    public enum RopeGround : byte { Grass, Mud, Plank }

    /// <summary>Lasso state machine (farm-tools section 3 "States (lasso)"); Reeling is Tethered + Use held.</summary>
    public enum LassoPhase : byte { Idle, Aiming, Flying, Tethered, Retracting }

    /// <summary>
    /// Pure lasso rules (design/gdd/farm-tools.md rule 3, section 4 "Lasso tug-of-war", section 5 edge cases;
    /// physics-carry rule 7). No scene access so EditMode tests pin the GDD numbers.
    /// </summary>
    public static class LassoMath
    {
        /// <summary>
        /// Target rule: critters must be at least <paramref name="minSize"/> (Medium: pigs, goats, cows; chickens are net
        /// targets), goods are always allowed (tow rope), teammates only while knocked out (anti-grief, GDD 5.11 rule 6),
        /// never yourself, never something another player is holding, and nothing beyond <paramref name="range"/>.
        /// </summary>
        public static LassoVerdict Evaluate(LassoTargetKind kind, SizeClass size, bool playerKnockedOut, bool isSelf,
            float distance, float range, SizeClass minSize = SizeClass.Medium, bool heldByOther = false)
        {
            if (kind == LassoTargetKind.None) return LassoVerdict.Nothing;
            if (isSelf) return LassoVerdict.Self;
            if (distance > range) return LassoVerdict.OutOfRange;
            if (heldByOther) return LassoVerdict.Held;
            switch (kind)
            {
                case LassoTargetKind.Critter: return size >= minSize ? LassoVerdict.Hit : LassoVerdict.TooSmall;
                case LassoTargetKind.Player: return playerKnockedOut ? LassoVerdict.Hit : LassoVerdict.AwakePlayer;
                default: return LassoVerdict.Hit;
            }
        }

        /// <summary>ground_mult: grass 1.0, mud 0.6, plank 0.8.</summary>
        public static float GroundMult(RopeGround g) => g == RopeGround.Mud ? 0.6f : g == RopeGround.Plank ? 0.8f : 1f;

        /// <summary>
        /// Ground class for the rope holder: bridge planks, else mud when the movement zones slow you like mud does
        /// (mud 0.6, 0.8 with Rubber Boots), else grass. Rain/storm's global ground multiplier (0.9-0.92) is not mud.
        /// </summary>
        public static RopeGround GroundFor(bool onBridge, float movementMult) =>
            onBridge ? RopeGround.Plank : movementMult < MudBelow ? RopeGround.Mud : RopeGround.Grass;

        /// <summary>Movement multiplier below which the ground counts as mud.</summary>
        public const float MudBelow = 0.75f;

        /// <summary>
        /// <c>player_anchor = base * (stamina &gt; 0 ? 1 : 0.4) * (helpers + 1) * ground_mult</c> in newtons.
        /// </summary>
        public static float PlayerAnchor(float baseN, float stamina, int helpersOnRope, RopeGround ground, float exhaustedMult = 0.4f) =>
            baseN * (stamina > 0f ? 1f : exhaustedMult) * (Mathf.Max(0, helpersOnRope) + 1) * GroundMult(ground);

        /// <summary><c>dragged = critter_pull &gt; player_anchor</c>.</summary>
        public static bool IsDragged(float critterPullN, float anchorN) => critterPullN > anchorN;

        /// <summary>Rope length after reeling for <paramref name="dt"/> s (never below <paramref name="minLength"/>).</summary>
        public static float Reel(float length, float reelSpeed, float dt, float minLength) =>
            Mathf.Max(minLength, length - Mathf.Max(0f, reelSpeed) * Mathf.Max(0f, dt));

        /// <summary>Reeling state exit: at or under the auto-grab distance the player grabs the catch.</summary>
        public static bool ShouldAutoGrab(float distance, float autoGrabDistance) => distance <= autoGrabDistance;

        /// <summary>Seconds the loop takes to reach <paramref name="distance"/>.</summary>
        public static float FlightTime(float distance, float loopSpeed) => loopSpeed <= 0f ? 0f : Mathf.Max(0f, distance) / loopSpeed;

        /// <summary>Rope snaps when stretched far beyond its length (stuck geometry, teleports) or above the break force.</summary>
        public static bool RopeSnaps(float distance, float length, float snapSlack, float tensionN, float breakForceN) =>
            distance > length + snapSlack || tensionN > breakForceN;

        /// <summary>
        /// Where a rope of <paramref name="length"/> from <paramref name="anchor"/> lets <paramref name="target"/> be,
        /// measured on the ground plane (height is the target's own). Inside the length the target is free.
        /// </summary>
        public static Vector3 ClampToRopeFlat(Vector3 anchor, Vector3 target, float length)
        {
            Vector3 d = target - anchor; d.y = 0f;
            float m = d.magnitude;
            if (m <= length || m < 1e-4f) return target;
            Vector3 p = anchor + d / m * Mathf.Max(0f, length);
            p.y = target.y;
            return p;
        }

        /// <summary>Horizontal rope distance (the rope is long enough to cover height differences on a farm).</summary>
        public static float FlatDistance(Vector3 a, Vector3 b) { a.y = 0f; b.y = 0f; return Vector3.Distance(a, b); }

        /// <summary>Visual sag of a rope (m): slack hangs, a taut rope still dips a little.</summary>
        public static float Sag(float length, float distance, float slackFactor = 0.45f, float minSag = 0.06f) =>
            Mathf.Max(minSag, (length - distance) * slackFactor);

        /// <summary>Flying loop: a parabola from <paramref name="from"/> to <paramref name="to"/> peaking at +<paramref name="height"/>.</summary>
        public static Vector3 LoopArc(Vector3 from, Vector3 to, float t, float height)
        {
            t = Mathf.Clamp01(t);
            return Vector3.Lerp(from, to, t) + Vector3.up * (4f * height * t * (1f - t));
        }
    }
}
