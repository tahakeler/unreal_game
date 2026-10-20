using System.Collections.Generic;
using HarvestPanic.AI.Night;
using HarvestPanic.Core;
using NUnit.Framework;
using UnityEngine;

namespace HarvestPanic.Tests.EditMode.Night
{
    public class NightEditModeTests
    {
        static readonly NightTuning T = new NightTuning();

        [Test] public void Budget_MatchesGddExample() => Assert.AreEqual(31, NightMath.Budget(3, 3, T));
        [Test] public void Budget_ThreatMultScales() => Assert.AreEqual(62, NightMath.Budget(3, 3, T, 2f));
        [Test] public void Week_FromDayIndex() { Assert.AreEqual(1, NightMath.Week(0, 5)); Assert.AreEqual(1, NightMath.Week(4, 5)); Assert.AreEqual(2, NightMath.Week(5, 5)); Assert.AreEqual(5, NightMath.Week(24, 5)); }

        [Test]
        public void Caps_GrowByWeek()
        {
            Assert.AreEqual(16, NightMath.MaxConcurrentSpend(1, T), "week 1: Stitches + 2 foxes");
            for (int w = 2; w <= 4; w++) Assert.Greater(NightMath.MaxConcurrentSpend(w, T), NightMath.MaxConcurrentSpend(w - 1, T));
        }

        [Test]
        public void DeliverableThreat_KeepsScalingAllRun_NoPlateau()
        {
            // GDD 8.9 flag: under the old concurrency-only model threat capped at 26 by day 3. Pool + weekly caps keeps climbing.
            for (int p = 0; p <= 4; p++)
            {
                for (int d = 1; d < 25; d++)
                    Assert.Greater(NightMath.DeliverableThreat(d, p, T), NightMath.DeliverableThreat(d - 1, p, T), $"day {d} players {p}");
            }
            Assert.Greater(NightMath.DeliverableThreat(24, 4, T), 26 * 4);
        }

        static NightViewer View(Vector3 eye, Vector3 look) => new NightViewer { Eye = eye, Rotation = Quaternion.LookRotation(look), VerticalFov = 70f, Aspect = 16f / 9f };

        static List<Vector3> Pts(Vector3 at) { var l = new List<Vector3>(); NightMath.Samples(new Bounds(at + Vector3.up * 1.4f, new Vector3(1.2f, 2.6f, 1f)), l); return l; }

        [Test]
        public void Frustum_InFrontWithinRange_Seen()
        {
            var v = View(new Vector3(0, 1.6f, 0), Vector3.forward);
            Assert.IsTrue(NightMath.InFrustum(v, new Vector3(0, 1.6f, 10f), 40f));
            Assert.IsFalse(NightMath.InFrustum(v, new Vector3(0, 1.6f, -10f), 40f), "behind");
            Assert.IsFalse(NightMath.InFrustum(v, new Vector3(0, 1.6f, 41f), 40f), "too far");
            Assert.IsFalse(NightMath.InFrustum(v, new Vector3(30f, 1.6f, 5f), 40f), "outside horizontal fov");
        }

        [Test]
        public void Observed_RequiresLineOfSight()
        {
            var views = new[] { View(new Vector3(0, 1.6f, 0), Vector3.forward) };
            var pts = Pts(new Vector3(0, 0, 10f));
            Assert.IsTrue(NightMath.IsObserved(views, pts, 40f, 0.15f, (a, b) => true));
            Assert.IsFalse(NightMath.IsObserved(views, pts, 40f, 0.15f, (a, b) => false), "wall blocks every ray: it moves");
        }

        [Test]
        public void Observed_PartialVisibility_FifteenPercentThreshold()
        {
            var views = new[] { View(new Vector3(0, 1.6f, 0), Vector3.forward) };
            var pts = Pts(new Vector3(0, 0, 10f));
            int n = 0; // only 1 of 9 samples visible (11%) -> unseen; 2 of 9 (22%) -> seen
            Assert.IsFalse(NightMath.IsObserved(views, pts, 40f, 0.15f, (a, b) => n++ < 1));
            n = 0;
            Assert.IsTrue(NightMath.IsObserved(views, pts, 40f, 0.15f, (a, b) => n++ < 2));
        }

        [Test]
        public void Observed_AnyOfFourViewers_FogShortensRange()
        {
            var at = new Vector3(0, 0, 20f);
            var pts = Pts(at);
            var away = new[] { View(Vector3.up * 1.6f, Vector3.back), View(Vector3.up * 1.6f, Vector3.left), View(Vector3.up * 1.6f, Vector3.right), View(new Vector3(0, 1.6f, 40f), Vector3.forward) };
            Assert.IsFalse(NightMath.IsObserved(away, pts, 40f, 0.15f, null));
            var one = new List<NightViewer>(away) { View(Vector3.up * 1.6f, Vector3.forward) };
            Assert.IsTrue(NightMath.IsObserved(one, pts, NightMath.ViewDistance(0f, T), 0.15f, null));
            Assert.IsFalse(NightMath.IsObserved(one, pts, NightMath.ViewDistance(20f, T), 0.15f, null), "fog: 15 m");
        }

        [Test]
        public void NoiseMap_PicksLoudestCell_MandrakeDoubles()
        {
            var m = new NightNoiseMap(20f);
            m.Add(new Vector3(5, 0, 5), 0.8f, NoiseSource.Shout, 0f);
            m.Add(new Vector3(65, 0, 5), 0.5f, NoiseSource.Crop, 0f); // 1.0 > 0.8
            Assert.IsTrue(m.TryLoudest(1f, 20f, out var c));
            Assert.AreEqual(70f, c.x, 1e-3);
            Assert.IsFalse(m.TryLoudest(30f, 20f, out _), "window expired");
        }
    }
}
