using UnityEngine;

namespace HarvestPanic.AI.Crops
{
    /// <summary>Pure cursed-crop rules (cursed-crops §4). EditMode-tested.</summary>
    public static class CursedCropRules
    {
        /// <summary>Scream loudness = base x SackMult (if sacked) x WagonMult (if in a wagon). Earmuffs only protect the wearer from the stun.</summary>
        public static float ScreamLoudness(float baseLoudness, bool sacked, bool inWagon, float sackMult, float wagonMult) =>
            Mathf.Clamp01(baseLoudness * (sacked ? sackMult : 1f) * (inWagon ? wagonMult : 1f));

        /// <summary>Jostle trigger: carrier acceleration above the threshold, or an impact above the impulse threshold.</summary>
        public static bool IsJostle(float accel, float impactDesign, float accelThreshold, float impactThreshold) =>
            accel > accelThreshold || impactDesign > impactThreshold;

        /// <summary>A new scream may start <paramref name="cooldown"/> s after the last one started.</summary>
        public static bool CanScream(float now, float lastStart, float cooldown) => now - lastStart >= cooldown;

        /// <summary>Spawn rule: planted from <paramref name="unlockDayIndex"/> (0-based) onward.</summary>
        public static bool IsUnlocked(int dayIndex, int unlockDayIndex) => dayIndex >= unlockDayIndex;

        /// <summary>Stun applies to a non-protected player within the radius.</summary>
        public static bool Stuns(float distance, float radius, bool earmuffs) => !earmuffs && distance <= radius;

        /// <summary>
        /// Tumble melon roll heading: downhill on a slope steeper than <paramref name="slopeDeg"/>; back toward its field
        /// when outside the leash; otherwise away from the nearest player (if any), else <paramref name="fallback"/>.
        /// Always flat and normalised.
        /// </summary>
        public static Vector3 RollDirection(Vector3 groundNormal, float slopeDeg, Vector3 pos, Vector3 home, float leash,
                                            Vector3? nearestPlayer, Vector3 fallback)
        {
            Vector3 to = home - pos; to.y = 0f;
            if (to.magnitude > leash) return to.normalized;
            if (groundNormal.sqrMagnitude > 1e-6f && Vector3.Angle(groundNormal, Vector3.up) > slopeDeg)
            {
                Vector3 down = Vector3.ProjectOnPlane(Vector3.down, groundNormal); down.y = 0f;
                if (down.sqrMagnitude > 1e-6f) return down.normalized;
            }
            if (nearestPlayer.HasValue)
            {
                Vector3 away = pos - nearestPlayer.Value; away.y = 0f;
                if (away.sqrMagnitude > 1e-6f) return away.normalized;
            }
            fallback.y = 0f;
            return fallback.sqrMagnitude > 1e-6f ? fallback.normalized : Vector3.forward;
        }

        /// <summary>How many crops to plant this morning to top the field back up to <paramref name="target"/>.</summary>
        public static int TopUp(int target, int alive) => Mathf.Max(0, target - alive);
    }
}
