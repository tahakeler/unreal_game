using System.Collections.Generic;
using HarvestPanic.Core;
using UnityEngine;

namespace HarvestPanic.AI
{
    /// <summary>
    /// Pure critter-ai formulas (§4). No scene access, so every rule is EditMode-testable.
    /// </summary>
    public static class CritterMath
    {
        /// <summary>
        /// Threat from one source: <c>clamp(P*wProx + S*wSprint + V*wSight + N + D, 0, 1)</c>
        /// where <c>P = 1 - d/AlertRadius</c> (0 beyond), <c>S</c>/<c>V</c> are 0 or 1.
        /// </summary>
        /// <example><code>SourceThreat(4, 10, true, true, 0, 0, t) // pig example = 0.65</code></example>
        public static float SourceThreat(float distance, float alertRadius, bool sprinting, bool inSight, float noise, float damage, CritterTuning t)
        {
            float p = Proximity(distance, alertRadius);
            float s = sprinting && p > 0f ? 1f : 0f;
            float v = inSight ? 1f : 0f;
            return SourceThreat(p, s, v, noise, damage, t);
        }

        /// <summary>Threat from already-computed components (P, S, V in 0..1; N, D additive).</summary>
        public static float SourceThreat(float p, float s, float v, float noise, float damage, CritterTuning t) =>
            Mathf.Clamp01(p * t.WeightProximity + s * t.WeightSprint + v * t.WeightSight + Mathf.Max(0f, noise) + Mathf.Max(0f, damage));

        /// <summary><c>P = 1 - d/AlertRadius</c>, 0 outside the radius.</summary>
        public static float Proximity(float distance, float alertRadius) =>
            alertRadius <= 0f || distance >= alertRadius ? 0f : 1f - Mathf.Max(0f, distance) / alertRadius;

        /// <summary>Total threat <c>T = max(T_src) + w * sum(other T_src)</c>, clamped 0..1.</summary>
        public static float TotalThreat(IReadOnlyList<float> sources, float otherWeight = 0.25f)
        {
            if (sources == null || sources.Count == 0) return 0f;
            float max = 0f, sum = 0f;
            for (int i = 0; i < sources.Count; i++) { float v = Mathf.Max(0f, sources[i]); sum += v; if (v > max) max = v; }
            return Mathf.Clamp01(max + otherWeight * (sum - max));
        }

        /// <summary>
        /// Perceived noise threat <c>N = Loudness * NoiseSensitivity * NoiseSourceMult * (1 - d/Radius)</c>, floored at 0.
        /// </summary>
        public static float PerceivedNoise(in NoiseEvent e, float distance, float sensitivity, float sourceMult, float globalMult = 1f) =>
            Mathf.Max(0f, e.PerceivedStrength(distance) * sensitivity * sourceMult * globalMult);

        /// <summary>Personality-aware overload.</summary>
        public static float PerceivedNoise(in NoiseEvent e, float distance, CritterPersonality p, float globalMult = 1f) =>
            PerceivedNoise(e, distance, p.NoiseSensitivity, p.GetNoiseSourceMult(e.Source), globalMult);

        /// <summary>Lure attraction <c>affinity * (1 - d/range)</c>, 0 outside range.</summary>
        public static float LureAttraction(float affinity, float lureDistance, float lureRange = 15f) =>
            lureRange <= 0f || lureDistance >= lureRange ? 0f : Mathf.Clamp01(affinity) * (1f - Mathf.Max(0f, lureDistance) / lureRange);

        /// <summary>Lure wins if <c>LureAffinity * (1 - d/15) &gt; T * 0.8</c>.</summary>
        public static bool LureWins(float affinity, float lureDistance, float threat, float lureRange = 15f, float threatFactor = 0.8f)
        {
            float a = LureAttraction(affinity, lureDistance, lureRange);
            return a > 0f && a > threat * threatFactor;
        }

        /// <summary>Maps a noise tag to the lure category it represents (Food = FeedBucket, Whistle = Whistle).</summary>
        public static LureTag LureTagFor(NoiseTag tags)
        {
            if ((tags & NoiseTag.Food) != 0) return LureTag.FeedBucket;
            if ((tags & (NoiseTag.Whistle | NoiseTag.WhistleLong)) != 0) return LureTag.Whistle;
            if ((tags & NoiseTag.Metal) != 0) return LureTag.Shiny;
            return LureTag.None;
        }

        /// <summary>Affinity for <paramref name="lure"/> from the personality, or <paramref name="fallback"/> when not listed.</summary>
        public static float LureAffinityFor(CritterPersonality p, LureTag lure, float fallback)
        {
            if (p != null && lure != LureTag.None)
                foreach (var e in p.LureAffinity) if ((e.Lure & lure) != 0) return e.Affinity;
            return fallback;
        }

        /// <summary>
        /// Break-free chance per struggle interval. Resolves the GDD conflict (§4 formula gave a 1 s mean and a
        /// nonzero chance at full grip, while §8.5 wants 3-5 s for one holder and never for SecureCarriers):
        /// 0 when <c>carriers &gt;= secure</c>, else <c>clamp(missing * perMissing + base, 0, max)</c>.
        /// Defaults (0.2, 0.05) give a pig (secure 2) held by 1 player 0.25 per ~1 s interval = 4 s mean.
        /// </summary>
        public static float BreakFreeChance(int secureCarriers, int carriers, float perMissing = 0.2f, float baseChance = 0.05f, float max = 0.9f)
        {
            int missing = secureCarriers - Mathf.Max(0, carriers);
            if (missing <= 0) return 0f;
            return Mathf.Clamp(missing * perMissing + baseChance, 0f, max);
        }

        /// <summary>Tuning overload of <see cref="BreakFreeChance(int,int,float,float,float)"/>.</summary>
        public static float BreakFreeChance(int secureCarriers, int carriers, CritterTuning t) =>
            BreakFreeChance(secureCarriers, carriers, t.BreakFreePerMissingCarrier, t.BreakFreeBase, t.BreakFreeMax);

        /// <summary>Expected seconds to break free: mean interval / p (geometric), infinity when p = 0.</summary>
        public static float ExpectedBreakFreeSeconds(float chancePerInterval, float intervalMin, float intervalMax) =>
            chancePerInterval <= 0f ? float.PositiveInfinity : 0.5f * (intervalMin + intervalMax) / chancePerInterval;

        /// <summary>True when <paramref name="toTarget"/> lies inside a horizontal cone of <paramref name="coneDegrees"/> around <paramref name="forward"/>.</summary>
        public static bool InSightCone(Vector3 forward, Vector3 toTarget, float coneDegrees)
        {
            if (coneDegrees >= 360f) return true;
            forward.y = 0f; toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 1e-6f) return true;
            if (forward.sqrMagnitude < 1e-6f) return false;
            return Vector3.Angle(forward, toTarget) <= coneDegrees * 0.5f;
        }

        /// <summary>Threat-weighted centroid of <paramref name="positions"/> (flattened to the critter's height).</summary>
        public static Vector3 ThreatCentroid(IReadOnlyList<Vector3> positions, IReadOnlyList<float> weights, Vector3 fallback)
        {
            Vector3 sum = Vector3.zero; float w = 0f;
            for (int i = 0; i < positions.Count; i++) { float wi = Mathf.Max(0f, weights[i]); sum += positions[i] * wi; w += wi; }
            return w > 1e-5f ? sum / w : fallback;
        }

        /// <summary>
        /// Horizontal flee direction: threat-weighted sum of "away from each source". When threats cancel out
        /// (pincer), flees perpendicular to the axis between the two strongest sources, on the side of
        /// <paramref name="preferred"/> (current heading). Returns a unit vector (or <paramref name="preferred"/> when no threats).
        /// </summary>
        public static Vector3 FleeDirection(Vector3 self, IReadOnlyList<Vector3> positions, IReadOnlyList<float> weights, Vector3 preferred)
        {
            preferred.y = 0f;
            if (preferred.sqrMagnitude < 1e-6f) preferred = Vector3.forward;
            preferred.Normalize();
            Vector3 away = Vector3.zero; float total = 0f;
            int a = -1, b = -1;
            for (int i = 0; i < positions.Count; i++)
            {
                float w = Mathf.Max(0f, weights[i]);
                if (w <= 0f) continue;
                Vector3 d = self - positions[i]; d.y = 0f;
                if (d.sqrMagnitude < 1e-6f) d = -preferred;
                away += d.normalized * w; total += w;
                if (a < 0 || w > weights[a]) { b = a; a = i; }
                else if (b < 0 || w > weights[b]) b = i;
            }
            if (total <= 0f) return preferred;
            if (away.magnitude >= 0.3f * total || b < 0) return away.normalized;
            Vector3 axis = positions[a] - positions[b]; axis.y = 0f;
            if (axis.sqrMagnitude < 1e-6f) return away.sqrMagnitude > 1e-6f ? away.normalized : preferred;
            Vector3 perp = Vector3.Cross(Vector3.up, axis.normalized);
            return Vector3.Dot(perp, preferred) >= 0f ? perp : -perp;
        }

        /// <summary>
        /// Bisector of the widest angular gap between threat bearings (Juke target). With one threat, the
        /// direction straight through-and-past it is not chosen; the side-step 90 degrees from it is.
        /// </summary>
        public static Vector3 WidestGapDirection(Vector3 self, IReadOnlyList<Vector3> positions, Vector3 preferred)
        {
            preferred.y = 0f;
            if (preferred.sqrMagnitude < 1e-6f) preferred = Vector3.forward;
            if (positions.Count == 0) return preferred.normalized;
            var angles = new List<float>(positions.Count);
            foreach (var p in positions)
            {
                Vector3 d = p - self; d.y = 0f;
                if (d.sqrMagnitude < 1e-6f) continue;
                angles.Add(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg);
            }
            if (angles.Count == 0) return preferred.normalized;
            if (angles.Count == 1) return Quaternion.Euler(0f, angles[0] + 90f, 0f) * Vector3.forward;
            angles.Sort();
            float bestGap = -1f, bestMid = 0f;
            for (int i = 0; i < angles.Count; i++)
            {
                float from = angles[i], to = i + 1 < angles.Count ? angles[i + 1] : angles[0] + 360f;
                float gap = to - from;
                if (gap > bestGap) { bestGap = gap; bestMid = from + gap * 0.5f; }
            }
            return Quaternion.Euler(0f, bestMid, 0f) * Vector3.forward;
        }
    }
}
