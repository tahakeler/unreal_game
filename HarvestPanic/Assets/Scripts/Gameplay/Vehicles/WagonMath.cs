using UnityEngine;

namespace HarvestPanic.Gameplay
{
    /// <summary>Surface under the wagon wheels (wagon-vehicles section 4, rolling_k).</summary>
    public enum WagonGround : byte { Dirt, Mud, Plank }

    /// <summary>
    /// Pure wagon formulas (design/gdd/wagon-vehicles.md sections 3-4). No scene access so EditMode tests pin the GDD
    /// examples: 2 players, 220 kg, dirt = 5.45 m/s; mud = 2.7 m/s.
    /// </summary>
    public static class WagonMath
    {
        /// <summary>rolling_k: dirt 1.5, mud 3.0, plank 1.2.</summary>
        public static float RollingK(WagonGround g, float dirt = 1.5f, float mud = 3f, float plank = 1.2f) =>
            g == WagonGround.Mud ? mud : g == WagonGround.Plank ? plank : dirt;

        /// <summary>Ground under the wheels: bridge planks, else mud when the movement zones slow things, else dirt.</summary>
        public static WagonGround GroundFor(bool onBridge, float movementMult) =>
            onBridge ? WagonGround.Plank : movementMult < LassoMath.MudBelow ? WagonGround.Mud : WagonGround.Dirt;

        /// <summary><c>v_max = clamp(sum_pull_force / (total_mass_kg * rolling_k), 0, cap)</c> m/s.</summary>
        public static float VMax(float sumPullForceN, float totalMassKg, float rollingK, float cap = 6f)
        {
            if (totalMassKg <= 0f || rollingK <= 0f) return 0f;
            return Mathf.Clamp(sumPullForceN / (totalMassKg * rollingK), 0f, cap);
        }

        /// <summary>
        /// Rolling resistance (N) that makes <see cref="VMax"/> the terminal speed under a constant push:
        /// <c>F = total_mass * rolling_k * speed</c>.
        /// </summary>
        public static float RollingDrag(float totalMassKg, float rollingK, float speed) => Mathf.Max(0f, totalMassKg) * Mathf.Max(0f, rollingK) * speed;

        /// <summary>Roll/pitch angle of the bed away from upright (degrees).</summary>
        public static float TiltDegrees(Vector3 up) => Vector3.Angle(up, Vector3.up);

        /// <summary>Tipping rule: tilt beyond <paramref name="tipAngle"/> (50 degrees) tips the wagon.</summary>
        public static bool IsTipped(Vector3 up, float tipAngle) => TiltDegrees(up) > tipAngle;

        /// <summary>Slots an item takes: one per 2 carry hands (a 6-hand cow takes 3, the 8-hand prize pumpkin 4).</summary>
        public static int SlotCost(int carryHands) => Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(1, carryHands) / 2f));

        /// <summary>
        /// Cargo capacity: the item must fit in the free slots and the cargo must still be under the soft cap
        /// (soft: the item that crosses 400 kg still goes in, nothing after it).
        /// </summary>
        public static bool CanLoad(int usedSlots, int totalSlots, float cargoKg, int itemHands, float softCapKg) =>
            usedSlots + SlotCost(itemHands) <= totalSlots && cargoKg < softCapKg;

        /// <summary>Vertical bump acceleration above which cargo bounces out: <c>9.8 * (1 + wall/0.4 - 0.3)</c>.</summary>
        public static float BounceThreshold(float wallHeight) => 9.8f * (1f + wallHeight / 0.4f - 0.3f);

        /// <summary>Players needed to right a tipped wagon: 2, or everyone present when fewer are connected.</summary>
        public static int RightingPlayers(int connectedPlayers, int required = 2) => Mathf.Clamp(Mathf.Min(required, connectedPlayers), 1, Mathf.Max(1, required));

        /// <summary>Movement multiplier for a player on the handle so they walk at the wagon's pace.</summary>
        public static float CarrierSpeedMult(float vMax, float walkSpeed, float floor = 0.35f) =>
            walkSpeed <= 0f ? 1f : Mathf.Clamp(vMax / walkSpeed, floor, 1f);

        /// <summary>Bruise impulse (design units) passed to cargo when the wagon body is hit; the bed cushions it.</summary>
        public static float CargoBruise(float designImpulse, float cushion) => Mathf.Max(0f, designImpulse) * Mathf.Clamp01(cushion);

        /// <summary>
        /// Signed distance of the player outside a handle bar along its <paramref name="outward"/> axis (behind a rear
        /// push bar, ahead of a front pull bar); negative once they have walked through it into the cart.
        /// </summary>
        public static float HandleGap(Vector3 handle, Vector3 player, Vector3 outward)
        {
            Vector3 d = player - handle; d.y = 0f; outward.y = 0f;
            return Vector3.Dot(d, outward.sqrMagnitude > 1e-6f ? outward.normalized : Vector3.back);
        }

        /// <summary>
        /// Handle "tow bar": horizontal force (N) on the handle that keeps the player <paramref name="rest"/> metres
        /// outside the bar. Too close pushes the handle away (pushing the cart), too far pulls it toward the player
        /// (pulling). A player who ends up on the wrong side of the bar only ever pushes along the cart's axis, so the
        /// bar never yanks the cart back onto them.
        /// </summary>
        public static Vector3 HandleForce(Vector3 handle, Vector3 player, Vector3 relVelocity, Vector3 outward, float rest, float stiffness, float damping, float maxForce)
        {
            outward.y = 0f;
            outward = outward.sqrMagnitude > 1e-6f ? outward.normalized : Vector3.back;
            Vector3 d = player - handle; d.y = 0f; // from the handle to the player
            float along = Vector3.Dot(d, outward);
            bool outside = along > 0.2f && d.sqrMagnitude > 1e-6f;
            Vector3 dir = outside ? d.normalized : outward;
            float dist = outside ? d.magnitude : along;
            relVelocity.y = 0f;
            float f = stiffness * (dist - rest) - damping * Vector3.Dot(relVelocity, dir);
            return dir * Mathf.Clamp(f, -maxForce, maxForce);
        }
    }

    /// <summary>
    /// Cargo slots in the bed (wagon-vehicles rule 1 "capacity is physical"; build: slots + soft cap). Slots are numbered
    /// row-major from the front-left; an item that needs several slots takes consecutive free ones.
    /// </summary>
    public sealed class WagonSlots
    {
        readonly bool[] _used;

        /// <summary>Creates <paramref name="count"/> free slots.</summary>
        public WagonSlots(int count) { _used = new bool[Mathf.Max(1, count)]; }

        /// <summary>Total slots.</summary>
        public int Count => _used.Length;

        /// <summary>Occupied slots.</summary>
        public int Used { get { int n = 0; foreach (var u in _used) if (u) n++; return n; } }

        /// <summary>True when slot <paramref name="i"/> is taken.</summary>
        public bool IsUsed(int i) => i >= 0 && i < _used.Length && _used[i];

        /// <summary>Takes <paramref name="cost"/> consecutive free slots; false (nothing taken) when they do not fit.</summary>
        public bool TryTake(int cost, out int first)
        {
            first = -1;
            cost = Mathf.Max(1, cost);
            for (int s = 0; s + cost <= _used.Length; s++)
            {
                bool free = true;
                for (int k = 0; k < cost && free; k++) free = !_used[s + k];
                if (!free) continue;
                for (int k = 0; k < cost; k++) _used[s + k] = true;
                first = s;
                return true;
            }
            return false;
        }

        /// <summary>Frees <paramref name="cost"/> slots starting at <paramref name="first"/>.</summary>
        public void Free(int first, int cost)
        {
            for (int k = 0; k < Mathf.Max(1, cost); k++) if (first + k >= 0 && first + k < _used.Length) _used[first + k] = false;
        }

        /// <summary>Frees everything.</summary>
        public void Clear() { for (int i = 0; i < _used.Length; i++) _used[i] = false; }

        /// <summary>
        /// Bed-local centre (x across, z along, metres from the bed centre) of <paramref name="cost"/> slots from
        /// <paramref name="first"/> in a <paramref name="columns"/>-wide grid on a <paramref name="width"/> x
        /// <paramref name="length"/> bed (row 0 is the front).
        /// </summary>
        public static Vector2 Centre(int first, int cost, int count, int columns, float width, float length)
        {
            columns = Mathf.Max(1, columns);
            int rows = Mathf.Max(1, Mathf.CeilToInt(count / (float)columns));
            Vector2 sum = Vector2.zero;
            cost = Mathf.Max(1, cost);
            for (int k = 0; k < cost; k++)
            {
                int i = first + k, r = i / columns, c = i % columns;
                float x = (c + 0.5f) / columns * width - width * 0.5f;
                float z = length * 0.5f - (r + 0.5f) / rows * length;
                sum += new Vector2(x, z);
            }
            return sum / cost;
        }
    }
}
