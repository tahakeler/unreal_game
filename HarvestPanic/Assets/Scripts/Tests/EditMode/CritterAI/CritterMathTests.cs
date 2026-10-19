using System.Collections.Generic;
using HarvestPanic.AI;
using HarvestPanic.Core;
using NUnit.Framework;
using UnityEngine;

namespace HarvestPanic.Tests.EditMode.CritterAI
{
    public class CritterMathTests
    {
        CritterTuning _t;

        [SetUp] public void SetUp() => _t = ScriptableObject.CreateInstance<CritterTuning>();
        [TearDown] public void TearDown() => Object.DestroyImmediate(_t);

        [Test]
        public void SourceThreat_GddPigExample_Is065()
        {
            // critter-ai §4: player 4 m away, sprinting, in view, AlertRadius 10 -> 0.3 + 0.2 + 0.15.
            Assert.That(CritterMath.SourceThreat(4f, 10f, true, true, 0f, 0f, _t), Is.EqualTo(0.65f).Within(1e-4f));
        }

        [Test]
        public void SourceThreat_OutsideRadius_IgnoresProximityAndSprint()
        {
            Assert.That(CritterMath.SourceThreat(12f, 10f, true, false, 0f, 0f, _t), Is.EqualTo(0f));
            Assert.That(CritterMath.SourceThreat(12f, 10f, true, false, 0.9f, 0.6f, _t), Is.EqualTo(1f), "clamped");
        }

        [Test]
        public void TotalThreat_IsMaxPlusQuarterOfOthers()
        {
            Assert.That(CritterMath.TotalThreat(new List<float> { 0.4f, 0.2f, 0.2f }), Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(CritterMath.TotalThreat(new List<float>()), Is.EqualTo(0f));
            Assert.That(CritterMath.TotalThreat(new List<float> { 0.9f, 0.9f }), Is.EqualTo(1f));
        }

        [Test]
        public void PerceivedNoise_UsesLoudnessSensitivitySourceMultAndFalloff()
        {
            var e = new NoiseEvent { Loudness = 0.8f, Radius = 20f, Source = NoiseSource.Shout };
            Assert.That(CritterMath.PerceivedNoise(e, 5f, 1.4f, 1.5f), Is.EqualTo(0.8f * 1.4f * 1.5f * 0.75f).Within(1e-5f));
            Assert.That(CritterMath.PerceivedNoise(e, 25f, 1f, 1f), Is.EqualTo(0f));
        }

        [Test]
        public void PerceivedNoise_ShoutSendsFleeButQuietFootstepDoesNot()
        {
            // §8.2: shout >= 0.8 within 10 m flees; footstep 0.10 does not.
            var shout = new NoiseEvent { Loudness = 0.8f, Radius = 25f, Source = NoiseSource.Shout };
            var step = new NoiseEvent { Loudness = 0.1f, Radius = 6f, Source = NoiseSource.Footstep };
            Assert.That(CritterMath.PerceivedNoise(shout, 5f, 1f, 1f), Is.GreaterThanOrEqualTo(0.55f));
            Assert.That(CritterMath.PerceivedNoise(step, 2f, 1f, 1f), Is.LessThan(0.3f));
        }

        [Test]
        public void LureWins_AgainstLowThreat_LosesAgainstHighThreat()
        {
            // pig affinity 0.9, bucket 8 m away: 0.9 * (1 - 8/15) = 0.42
            Assert.That(CritterMath.LureWins(0.9f, 8f, 0.0f), Is.True);
            Assert.That(CritterMath.LureWins(0.9f, 8f, 0.5f), Is.True, "0.42 > 0.40");
            Assert.That(CritterMath.LureWins(0.9f, 8f, 0.6f), Is.False, "0.42 < 0.48");
            Assert.That(CritterMath.LureWins(0.9f, 16f, 0f), Is.False, "out of range");
        }

        [Test]
        public void BreakFree_SecureCarriersNeverBreak_SingleHolderMeanThreeToFiveSeconds()
        {
            Assert.That(CritterMath.BreakFreeChance(2, 2, _t), Is.EqualTo(0f));
            Assert.That(CritterMath.BreakFreeChance(2, 3, _t), Is.EqualTo(0f));
            Assert.That(CritterMath.BreakFreeChance(1, 1, _t), Is.EqualTo(0f), "chicken held by 1");
            float p = CritterMath.BreakFreeChance(2, 1, _t);
            Assert.That(p, Is.EqualTo(0.25f).Within(1e-5f));
            float mean = CritterMath.ExpectedBreakFreeSeconds(p, 0.5f, 1.5f);
            Assert.That(mean, Is.InRange(3f, 5f));

            // Monte Carlo with the real interval distribution.
            var rng = new System.Random(1234);
            double total = 0; const int runs = 4000;
            for (int r = 0; r < runs; r++)
            {
                double time = 0;
                while (true) { time += 0.5 + rng.NextDouble(); if (rng.NextDouble() < p) break; }
                total += time;
            }
            Assert.That(total / runs, Is.InRange(3.0, 5.0));
        }

        [Test]
        public void FleeDirection_SingleThreat_PointsAway()
        {
            var dir = CritterMath.FleeDirection(Vector3.zero, new List<Vector3> { new Vector3(3f, 0f, 0f) }, new List<float> { 0.7f }, Vector3.forward);
            Assert.That(Vector3.Dot(dir, Vector3.left), Is.GreaterThan(0.99f));
        }

        [Test]
        public void FleeDirection_PincerFromBothSides_IsPerpendicular()
        {
            var dir = CritterMath.FleeDirection(Vector3.zero,
                new List<Vector3> { new Vector3(4f, 0f, 0f), new Vector3(-4f, 0f, 0f) }, new List<float> { 0.6f, 0.6f }, Vector3.forward);
            Assert.That(Mathf.Abs(dir.x), Is.LessThan(0.01f));
            Assert.That(dir.z, Is.GreaterThan(0.99f), "keeps current heading side");
        }

        [Test]
        public void FleeDirection_WeightsStrongerThreat()
        {
            var dir = CritterMath.FleeDirection(Vector3.zero,
                new List<Vector3> { new Vector3(4f, 0f, 0f), new Vector3(0f, 0f, 4f) }, new List<float> { 0.9f, 0.1f }, Vector3.forward);
            Assert.That(dir.x, Is.LessThan(-0.9f));
        }

        [Test]
        public void WidestGap_TwoThreatsAhead_JukesBackward()
        {
            var dir = CritterMath.WidestGapDirection(Vector3.zero, new List<Vector3> { new Vector3(1f, 0f, 3f), new Vector3(-1f, 0f, 3f) }, Vector3.forward);
            Assert.That(dir.z, Is.LessThan(-0.9f));
        }

        [Test]
        public void SightCone_220Degrees_SeesSidesNotBehind()
        {
            Assert.That(CritterMath.InSightCone(Vector3.forward, Vector3.right, 220f), Is.True);
            Assert.That(CritterMath.InSightCone(Vector3.forward, Vector3.back, 220f), Is.False);
        }

        [Test]
        public void ThreatTracker_NoiseDecaysAndMergesWithPlayer()
        {
            var tr = new ThreatTracker(_t);
            tr.AddNoise(3, new Vector3(2f, 0f, 0f), 0.6f);
            tr.Recompute(0f);
            Assert.That(tr.Total, Is.EqualTo(0.6f).Within(1e-5f));
            tr.Recompute(1f);
            Assert.That(tr.Total, Is.EqualTo(0.35f).Within(1e-5f), "decays 0.25/s");
            tr.AddNoise(3, Vector3.zero, 0.2f);
            tr.Recompute(0f);
            Assert.That(tr.Sources.Count, Is.EqualTo(1), "same player merges");
            tr.Recompute(5f);
            Assert.That(tr.Sources.Count, Is.EqualTo(0));
        }

        [Test]
        public void ThreatTracker_SprintDetectedFromObservedSpeed()
        {
            var tr = new ThreatTracker(_t);
            tr.BeginTick(); tr.Observe(1, PlayerIds.None, Vector3.zero, 0f, 0.6f, true);
            tr.BeginTick(); float v = tr.Observe(1, PlayerIds.None, new Vector3(0.65f, 0f, 0f), 0.1f, 0.6f, true);
            tr.Recompute(0.1f);
            Assert.That(v, Is.EqualTo(6.5f).Within(0.01f));
            Assert.That(tr.Total, Is.EqualTo(0.65f).Within(1e-4f));
        }

        [Test]
        public void Personalities_PigAndChicken_LoadFromCatalog()
        {
            Content.Critters.Invalidate();
            Assert.That(Content.Critters.TryGet("Critter.Pig", out var pig), Is.True);
            Assert.That(Content.Critters.TryGet("Critter.Chicken", out var chicken), Is.True);
            Assert.That(pig.SecureCarriers, Is.EqualTo(2));
            Assert.That(pig.Prefab, Is.Not.Null);
            Assert.That(chicken.HerdMode, Is.EqualTo(HerdMode.Flock));
            Assert.That(pig.TryGetNoiseResponse(NoiseTag.Food, out var r) && r.Reaction == NoiseReaction.Lure, Is.True);
        }
    }
}
