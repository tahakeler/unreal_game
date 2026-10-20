using System;
using System.Collections.Generic;
using HarvestPanic.Core;
using UnityEngine;

namespace HarvestPanic.AI.Night
{
    /// <summary>All night tunables (night-creatures §7). Serialized on the NightDirector prefab.</summary>
    [Serializable]
    public sealed class NightTuning
    {
        [Header("Budget B = Base + PerDay*DayIndex + PerPlayer*PlayersOutside (pool; pays re-spawns)")]
        public int BudgetBase = 10, BudgetPerDay = 4, BudgetPerPlayer = 3;
        public int DaysPerWeek = 5;
        public int StitchesCost = 8, FoxCost = 4;
        [Tooltip("Concurrent foxes in week 1; +FoxCapPerWeek each week up to FoxCapMax")] public int FoxCapBase = 2, FoxCapPerWeek = 1, FoxCapMax = 5;
        [Tooltip("A second Mr. Stitches from this week on (0 = never)")] public int SecondStitchesWeek = 4;
        [Tooltip("Seconds before a repelled/finished creature slot is re-bought from the pool")] public float RespawnCooldown = 20f;
        [Tooltip("Nominal night length for the ceiling estimate, s")] public float NightSeconds = 180f;
        [Header("Mr. Stitches")]
        public float StitchesSpeed = 7f, StitchesDragSpeed = 2.5f, ViewDistance = 40f, FogViewDistance = 15f;
        [Range(0f, 1f)] public float MinVisibleFraction = 0.15f;
        public float GrabRange = 1.4f, GrabImpulse = 2400f, GrabCooldown = 6f, TeleportDistance = 30f, AfkSeconds = 60f;
        public float DefaultVerticalFov = 70f, DefaultAspect = 16f / 9f;
        [Header("Fox")]
        public float FoxSpeed = 6f, FoxCarrySpeed = 4.5f, FoxFleeSpeed = 8f, FoxMaxMassKg = 6f, FoxScareLoudness = 0.6f, FoxScareRadius = 12f, FoxScarePlayerRadius = 4f;
        [Header("Safe zones, m")]
        public float BarnSafeRadius = 10f, MarketSafeRadius = 14f, BridgeSafeRadius = 3.5f, LanternRadius = 6f;
        [Header("Steering")]
        public float SteerInterval = 20f, CellSize = 20f;
        [Header("Muddlebury layout (world XZ)")]
        public Vector3[] FieldPosts = { new Vector3(-56, 0, -44), new Vector3(-48, 0, -30), new Vector3(-64, 0, -56), new Vector3(-30, 0, -28), new Vector3(0, 0, -22), new Vector3(0, 0, 22), new Vector3(30, 0, -14) };
        public Vector3[] FoxDens = { new Vector3(40, 0, -72), new Vector3(-80, 0, 60), new Vector3(-90, 0, -80) };
        public Vector3[] LanternPostPositions = { new Vector3(-14, 0, 3.2f), new Vector3(2, 0, -3.2f), new Vector3(18, 0, 3.2f), new Vector3(34, 0, -3.2f), new Vector3(50, 0, 3.2f), new Vector3(100, 0, 6f) };
    }

    /// <summary>A view to test the weeping-angel rule against (a player's replicated head, or a test bot).</summary>
    public struct NightViewer
    {
        public Vector3 Eye; public Quaternion Rotation; public float VerticalFov; public float Aspect;
    }

    /// <summary>Pure night formulas (EditMode-tested).</summary>
    public static class NightMath
    {
        public static int Week(int dayIndex, int daysPerWeek) => Mathf.Max(0, dayIndex) / Mathf.Max(1, daysPerWeek) + 1;

        /// <summary>GDD §3.2 budget: B = 10 + 4·DayIndex + 3·PlayersOutside, times Night.ThreatMult.</summary>
        public static int Budget(int dayIndex, int playersOutside, NightTuning t, float threatMult = 1f)
            => Mathf.Max(0, Mathf.RoundToInt((t.BudgetBase + t.BudgetPerDay * Mathf.Max(0, dayIndex) + t.BudgetPerPlayer * Mathf.Clamp(playersOutside, 0, 4)) * Mathf.Max(0f, threatMult)));

        public static int FoxCap(int week, NightTuning t) => Mathf.Min(t.FoxCapMax, t.FoxCapBase + (Mathf.Max(1, week) - 1) * t.FoxCapPerWeek);
        public static int StitchesCap(int week, NightTuning t) => t.SecondStitchesWeek > 0 && week >= t.SecondStitchesWeek ? 2 : 1;
        public static int MaxConcurrentSpend(int week, NightTuning t) => StitchesCap(week, t) * t.StitchesCost + FoxCap(week, t) * t.FoxCost;

        /// <summary>
        /// Plateau fix (GDD 8.9 flag): B is a per-night POOL that also pays re-spawns, and caps grow by week, so the
        /// threat a night can deliver is min(B, concurrent spend x refill cycles). Ceiling > B through a 5-week run.
        /// </summary>
        public static int DeliverableThreat(int dayIndex, int playersOutside, NightTuning t)
        {
            int week = Week(dayIndex, t.DaysPerWeek);
            int cycles = 1 + Mathf.FloorToInt(t.NightSeconds / Mathf.Max(1f, t.RespawnCooldown));
            return Mathf.Min(Budget(dayIndex, playersOutside, t), MaxConcurrentSpend(week, t) * cycles);
        }

        /// <summary>View distance: 40 m, 15 m when fog (Weather.FogDistance &gt; 0).</summary>
        public static float ViewDistance(float fogDistance, NightTuning t) => fogDistance > 0f ? t.FogViewDistance : t.ViewDistance;

        /// <summary>True when <paramref name="p"/> is inside the viewer's frustum and within <paramref name="maxDist"/>.</summary>
        public static bool InFrustum(in NightViewer v, Vector3 p, float maxDist)
        {
            Vector3 local = Quaternion.Inverse(v.Rotation) * (p - v.Eye);
            if (local.z <= 0.05f || local.sqrMagnitude > maxDist * maxDist) return false;
            float tanV = Mathf.Tan(v.VerticalFov * 0.5f * Mathf.Deg2Rad), tanH = tanV * v.Aspect;
            return Mathf.Abs(local.x) <= local.z * tanH && Mathf.Abs(local.y) <= local.z * tanV;
        }

        /// <summary>Centre + 8 corners (shrunk 80%) of <paramref name="b"/>.</summary>
        public static void Samples(Bounds b, List<Vector3> into)
        {
            into.Clear(); into.Add(b.center);
            Vector3 e = b.extents * 0.8f;
            for (int i = 0; i < 8; i++)
                into.Add(b.center + new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z));
        }

        /// <summary>Fraction of sample points in frustum with line of sight (<paramref name="hasLos"/>(eye, point)).</summary>
        public static float VisibleFraction(in NightViewer v, IReadOnlyList<Vector3> pts, float maxDist, Func<Vector3, Vector3, bool> hasLos)
        {
            if (pts.Count == 0) return 0f;
            int seen = 0;
            for (int i = 0; i < pts.Count; i++)
                if (InFrustum(v, pts[i], maxDist) && (hasLos == null || hasLos(v.Eye, pts[i]))) seen++;
            return seen / (float)pts.Count;
        }

        /// <summary>Weeping-angel rule: observed if ANY viewer sees ≥ minFraction of the bounds.</summary>
        public static bool IsObserved(IReadOnlyList<NightViewer> viewers, IReadOnlyList<Vector3> pts, float maxDist, float minFraction, Func<Vector3, Vector3, bool> hasLos)
        {
            for (int i = 0; i < viewers.Count; i++)
                if (VisibleFraction(viewers[i], pts, maxDist, hasLos) >= minFraction - 1e-4f) return true;
            return false;
        }

        /// <summary>Attraction multiplier per noise source (§4).</summary>
        public static float SourceMult(NoiseSource s)
        {
            switch (s)
            {
                case NoiseSource.Crop: return 2f;
                case NoiseSource.Voice: case NoiseSource.Shout: return 1f;
                case NoiseSource.Impact: case NoiseSource.Hazard: return 0.7f;
                default: return 0.5f;
            }
        }
    }

    /// <summary>Loudest-cell tracker (sum of Loudness × SourceMult per cell over a sliding window).</summary>
    public sealed class NightNoiseMap
    {
        struct Entry { public Vector2Int Cell; public float W; public float T; }
        readonly List<Entry> _e = new List<Entry>();
        readonly float _cell;
        public NightNoiseMap(float cellSize) { _cell = Mathf.Max(1f, cellSize); }
        public Vector2Int CellOf(Vector3 p) => new Vector2Int(Mathf.FloorToInt(p.x / _cell), Mathf.FloorToInt(p.z / _cell));
        public void Add(Vector3 p, float loudness, NoiseSource s, float time) => _e.Add(new Entry { Cell = CellOf(p), W = loudness * NightMath.SourceMult(s), T = time });

        public bool TryLoudest(float now, float window, out Vector3 centre)
        {
            _e.RemoveAll(x => now - x.T > window);
            var sums = new Dictionary<Vector2Int, float>();
            Vector2Int best = default; float bestW = 0f;
            foreach (var x in _e)
            {
                sums.TryGetValue(x.Cell, out float w); w += x.W; sums[x.Cell] = w;
                if (w > bestW) { bestW = w; best = x.Cell; }
            }
            centre = new Vector3((best.x + 0.5f) * _cell, 0f, (best.y + 0.5f) * _cell);
            return bestW > 0f;
        }
    }
}
