using HarvestPanic.AI;
using HarvestPanic.AI.Crops;
using HarvestPanic.Core;
using NUnit.Framework;
using UnityEngine;

namespace HarvestPanic.Tests.EditMode.CritterAI
{
    /// <summary>livestock-roster §4 "Sheep follow" rules (pure).</summary>
    public class FlockRulesTests
    {
        [Test]
        public void Follow_OnlyBeyondMaxDistance_IgnoresHeight()
        {
            var leader = new Vector3(0f, 0f, 0f);
            Assert.IsFalse(FlockRules.NeedsToFollow(leader, new Vector3(3.9f, 0f, 0f), 4f));
            Assert.IsFalse(FlockRules.NeedsToFollow(leader, new Vector3(3f, 5f, 0f), 4f), "flat distance");
            Assert.IsTrue(FlockRules.NeedsToFollow(leader, new Vector3(4.1f, 0f, 0f), 4f));
        }

        [Test]
        public void FollowPoint_StaysInTwoToFourMetreBand([Values(0f, 0.5f, 1f)] float t, [Values(-60f, 0f, 60f)] float spread)
        {
            var leader = new Vector3(10f, 0f, 10f);
            var p = FlockRules.FollowPoint(leader, new Vector3(30f, 0f, 10f), 2f, 4f, t, spread);
            float d = Vector3.Distance(new Vector3(p.x, 0f, p.z), new Vector3(leader.x, 0f, leader.z));
            Assert.That(d, Is.InRange(2f - 1e-4f, 4f + 1e-4f));
            Assert.AreEqual(Mathf.Lerp(2f, 4f, t), d, 1e-4f);
            // Coincident follower still gets a valid point.
            Assert.AreEqual(2f, Vector3.Distance(FlockRules.FollowPoint(leader, leader, 2f, 4f, 0f, 0f), leader), 1e-4f);
        }

        [Test]
        public void FollowSpeed_SeventyPercentOfSprintWhileLeaderCarried()
        {
            Assert.AreEqual(2.6f, FlockRules.FollowSpeed(2.6f, 4.8f, false, 0.7f), 1e-5f);
            Assert.AreEqual(4.8f * 0.7f, FlockRules.FollowSpeed(2.6f, 4.8f, true, 0.7f), 1e-5f);
        }

        [Test]
        public void Leader_OldestMember_ReelectedAfterThreeSeconds()
        {
            Assert.AreEqual(-1, FlockRules.LeaderIndex(0, 5f, float.NegativeInfinity, 3f), "empty flock");
            Assert.AreEqual(0, FlockRules.LeaderIndex(5, 5f, float.NegativeInfinity, 3f), "never lost: oldest leads");
            Assert.AreEqual(-1, FlockRules.LeaderIndex(4, 12f, 10f, 3f), "2 s after the leader was delivered: nobody leads");
            Assert.AreEqual(0, FlockRules.LeaderIndex(4, 13f, 10f, 3f), "3 s later the new oldest takes over");
        }

        [Test]
        public void Stampede_SharedHeadingWinsWhileFresh()
        {
            var shared = new Vector3(1f, 0f, 0f);
            var own = new Vector3(0f, 0.5f, -3f);
            Assert.AreEqual(shared, FlockRules.StampedeHeading(own, shared, true));
            var h = FlockRules.StampedeHeading(own, shared, false);
            Assert.AreEqual(0f, h.y, 1e-6f);
            Assert.AreEqual(1f, h.magnitude, 1e-5f);
            Assert.AreEqual(1f, Vector3.Dot(h, Vector3.back), 1e-5f, "stale: own (flattened) direction");
            Assert.AreEqual(Vector3.forward, FlockRules.StampedeHeading(Vector3.zero, Vector3.zero, true), "degenerate input");
        }

        [Test]
        public void ShareWithin_RosterAc4()
        {
            var leader = Vector3.zero;
            var f = new[] { new Vector3(3, 0, 0), new Vector3(9, 0, 0), new Vector3(0, 0, 9.9f), new Vector3(12, 0, 0), new Vector3(2, 0, 2) };
            Assert.AreEqual(0.8f, FlockRules.ShareWithin(leader, f, 10f), 1e-5f);
            Assert.AreEqual(1f, FlockRules.ShareWithin(leader, new Vector3[0], 10f));
        }

        [Test]
        public void MandrakeSpook_AlwaysClearsFleeThreshold()
        {
            foreach (float flee in new[] { 0.45f, 0.5f, 0.55f, 0.85f })
                Assert.That(CritterMath.MandrakeSpook(flee), Is.GreaterThan(flee).Or.EqualTo(1f));
            Assert.AreEqual(1f, CritterMath.MandrakeSpook(0.95f));
        }
    }

    /// <summary>cursed-crops §4 rules (pure).</summary>
    public class CursedCropRulesTests
    {
        [Test]
        public void ScreamLoudness_SackAndWagonDamp()
        {
            Assert.AreEqual(0.9f, CursedCropRules.ScreamLoudness(0.9f, false, false, 0.3f, 0.8f), 1e-5f);
            Assert.AreEqual(0.27f, CursedCropRules.ScreamLoudness(0.9f, true, false, 0.3f, 0.8f), 1e-5f, "sack -70%");
            Assert.AreEqual(0.72f, CursedCropRules.ScreamLoudness(0.9f, false, true, 0.3f, 0.8f), 1e-5f, "wagon -20%");
            Assert.AreEqual(0.216f, CursedCropRules.ScreamLoudness(0.9f, true, true, 0.3f, 0.8f), 1e-5f);
        }

        [Test]
        public void Jostle_AccelOrImpactAboveThreshold()
        {
            Assert.IsFalse(CursedCropRules.IsJostle(7.9f, 199f, 8f, 200f), "gentle carry (no sprint)");
            Assert.IsTrue(CursedCropRules.IsJostle(8.1f, 0f, 8f, 200f), "> 800 cm/s^2");
            Assert.IsTrue(CursedCropRules.IsJostle(0f, 201f, 8f, 200f), "> 200 impact");
        }

        [Test]
        public void Cooldown_EightSecondsBetweenScreamStarts()
        {
            Assert.IsTrue(CursedCropRules.CanScream(0f, float.NegativeInfinity, 8f));
            Assert.IsFalse(CursedCropRules.CanScream(7.9f, 0f, 8f));
            Assert.IsTrue(CursedCropRules.CanScream(8f, 0f, 8f));
        }

        [Test]
        public void Stun_WithinThreeMetresUnlessEarmuffs()
        {
            Assert.IsTrue(CursedCropRules.Stuns(2.9f, 3f, false));
            Assert.IsFalse(CursedCropRules.Stuns(3.1f, 3f, false));
            Assert.IsFalse(CursedCropRules.Stuns(0.5f, 3f, true), "earmuffs protect the wearer");
        }

        [Test]
        public void SpawnRules_UnlockDayAndTopUp()
        {
            var t = ScriptableObject.CreateInstance<CursedCropTuning>();
            Assert.AreEqual(2, t.UnlockDay("Crop.Mandrake"));
            Assert.AreEqual(3, t.UnlockDay("crop.tumblemelon"), "case-insensitive");
            Assert.AreEqual(int.MaxValue, t.UnlockDay("Crop.Carrot"));
            Assert.IsFalse(CursedCropRules.IsUnlocked(1, 2));
            Assert.IsTrue(CursedCropRules.IsUnlocked(2, 2));
            Assert.AreEqual(4, CursedCropRules.TopUp(6, 2));
            Assert.AreEqual(0, CursedCropRules.TopUp(6, 7));
            Object.DestroyImmediate(t);
        }

        [Test]
        public void MelonRoll_DownhillThenLeashThenAwayFromPlayer()
        {
            var flat = Vector3.up;
            var slope = Quaternion.Euler(0f, 0f, 10f) * Vector3.up; // normal leans toward -x: the ground falls away toward -x
            var fb = Vector3.left;
            var down = CursedCropRules.RollDirection(slope, 4f, Vector3.zero, Vector3.zero, 10f, null, fb);
            Assert.Less(down.x, -0.99f, "downhill on a 10 deg slope");
            var back = CursedCropRules.RollDirection(slope, 4f, new Vector3(0, 0, 12f), Vector3.zero, 10f, null, fb);
            Assert.Less(back.z, -0.99f, "outside the leash it rolls home even on a slope");
            var juke = CursedCropRules.RollDirection(flat, 4f, Vector3.zero, Vector3.zero, 10f, new Vector3(0f, 0f, -2f), fb);
            Assert.Greater(juke.z, 0.99f, "away from the player");
            Assert.AreEqual(fb, CursedCropRules.RollDirection(flat, 4f, Vector3.zero, Vector3.zero, 10f, null, fb));
        }

        [Test]
        public void BruiseDelta_GoodsValueFormula()
        {
            Assert.AreEqual(0f, CursedCropGoods.BruiseDelta(400f, 1.2f));
            Assert.AreEqual(12f, CursedCropGoods.BruiseDelta(800f, 1.2f), 1e-4f);
            Assert.AreEqual(60f, CursedCropGoods.BruiseDelta(9000f, 1.4f), "per-hit cap");
            Assert.AreEqual(0f, CursedCropGoods.BruiseMult(BruiseState.Ruined));
        }
    }

    /// <summary>Authored data: sheep personality + prefab, the two cursed crops' goods data and prefabs.</summary>
    public class SheepAndCropDataTests
    {
        [Test]
        public void SheepPersonality_MatchesRoster()
        {
            Content.Critters.Invalidate();
            Assert.IsTrue(Content.Critters.TryGet("Critter.Sheep", out var p), "Critter_Sheep.asset");
            Assert.AreEqual(HerdMode.Follow, p.HerdMode);
            Assert.AreEqual("Livestock.Sheep", p.GoodsId);
            Assert.AreEqual(2.6f, p.MoveSpeed, 1e-4f); Assert.AreEqual(4.8f, p.SprintSpeed, 1e-4f);
            Assert.AreEqual(12f, p.AlertRadius, 1e-4f); Assert.AreEqual(1.2f, p.NoiseSensitivity, 1e-4f);
            Assert.AreEqual(CorneredResponse.Freeze, p.CorneredResponse);
            Assert.AreEqual(1, p.SecureCarriers);
            Assert.IsTrue(p.TryGetNoiseResponse(NoiseTag.WhistleLong, out var r) && r.Reaction == NoiseReaction.Rally, "long whistle rallies");
            Assert.IsTrue(p.TryGetNoiseResponse(NoiseTag.Whistle, out var s) && s.Reaction == NoiseReaction.Flee, "short whistle scares");
            Assert.NotNull(p.Prefab, "prefab linked");
            var goods = p.Prefab.GetComponent<CritterGoods>();
            Assert.NotNull(goods);
            Assert.AreEqual(80, goods.CurrentValue, "$80 pristine");
            Assert.AreEqual(2, goods.Definition.CarryHands, "2 hands (1 carrier)");
            Assert.NotNull(p.Prefab.GetComponent<CritterCarryable>());
            Assert.NotNull(p.Prefab.GetComponent<SheepBehaviour>());
            var bell = p.Prefab.transform.Find("Bell");
            Assert.NotNull(bell, "bell-wether bell");
            Assert.IsFalse(bell.gameObject.activeSelf, "bell hidden until leader");
            foreach (var t in p.Prefab.GetComponentsInChildren<Transform>(true))
                Assert.IsFalse(t.name.StartsWith("GoatParts"), "goat silhouette parts stay goat-only");
        }

        [Test]
        public void CursedCropGoods_PremiumCursedCrops()
        {
            Content.Goods.Invalidate();
            Assert.IsTrue(Content.Goods.TryGet("Crop.Mandrake", out var m));
            Assert.IsTrue(Content.Goods.TryGet("Crop.TumbleMelon", out var t));
            Assert.IsTrue(Content.Goods.TryGet("Crop.Turnip", out var turnip));
            foreach (var d in new[] { m, t })
            {
                Assert.AreEqual(GoodsCategory.Crop, d.Category);
                Assert.IsTrue((d.DefaultTags & GoodsTag.Cursed) != 0, d.GoodsId + " cursed");
                Assert.Greater(d.BaseValue, turnip.BaseValue * 4, d.GoodsId + " premium");
                Assert.NotNull(d.WorldPrefab, d.GoodsId + " prefab");
                var g = d.WorldPrefab.GetComponent<CursedCropGoods>();
                Assert.NotNull(g); Assert.NotNull(d.WorldPrefab.GetComponent<Unity.Netcode.NetworkObject>());
                Assert.AreEqual(d.GoodsId, g.GoodsId);
            }
            Assert.AreEqual(45, m.BaseValue); Assert.AreEqual(1, m.CarryHands); Assert.AreEqual(1.2f, m.Fragility, 1e-4f);
            Assert.AreEqual(80, t.BaseValue); Assert.AreEqual(4, t.CarryHands); Assert.AreEqual(1.4f, t.Fragility, 1e-4f);
            Assert.NotNull(m.WorldPrefab.GetComponent<MandrakeScream>());
            Assert.NotNull(t.WorldPrefab.GetComponent<TumbleMelon>());
        }

        [Test]
        public void CropAndSheepPrefabs_InCritterNetworkList()
        {
            var list = Resources.Load<Unity.Netcode.NetworkPrefabsList>("NetworkPrefabs/Critters");
            Assert.NotNull(list);
            int found = 0;
            foreach (var e in list.PrefabList)
                if (e.Prefab != null && (e.Prefab.name == "Critter_Sheep" || e.Prefab.name == "Crop_Mandrake" || e.Prefab.name == "Crop_TumbleMelon")) found++;
            Assert.AreEqual(3, found);
        }

        [Test]
        public void TuningAssets_Authored()
        {
            Assert.NotNull(Resources.Load<CursedCropTuning>(CursedCropTuning.ResourcePath));
            var ct = CritterTuning.Default;
            Assert.AreEqual(2f, ct.FollowMinDistance); Assert.AreEqual(4f, ct.FollowMaxDistance);
            Assert.AreEqual(0.7f, ct.FollowCarriedSpeedMult, 1e-5f); Assert.AreEqual(3f, ct.LeaderReelectSeconds);
            Assert.AreEqual(20f, ct.MandrakeSpookRadius);
        }
    }
}
