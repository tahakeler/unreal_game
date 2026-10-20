using HarvestPanic.AI;
using HarvestPanic.Core;
using NUnit.Framework;
using UnityEngine;

namespace HarvestPanic.Tests.EditMode.CritterAI
{
    public class LivestockRulesTests
    {
        LivestockTuning T => LivestockTuning.Default;

        [Test]
        public void Headbutt_RequiresFrontConeRangeAndCooldown()
        {
            Assert.IsTrue(LivestockRules.CanStartHeadbutt(CritterState.Idle, Vector3.forward, new Vector3(0, 0, 1.2f), false, 0f, T));
            Assert.IsFalse(LivestockRules.CanStartHeadbutt(CritterState.Idle, Vector3.forward, new Vector3(0, 0, -1.2f), false, 0f, T), "behind");
            Assert.IsFalse(LivestockRules.CanStartHeadbutt(CritterState.Idle, Vector3.forward, new Vector3(0, 0, 5f), false, 0f, T), "far");
            Assert.IsFalse(LivestockRules.CanStartHeadbutt(CritterState.Idle, Vector3.forward, new Vector3(0, 0, 1f), true, 0f, T), "already down");
            Assert.IsFalse(LivestockRules.CanStartHeadbutt(CritterState.Idle, Vector3.forward, new Vector3(0, 0, 1f), false, 2f, T), "cooldown");
        }

        [TestCase(CritterState.Flee, false)]
        [TestCase(CritterState.Carried, false)]
        [TestCase(CritterState.Stunned, false)]
        [TestCase(CritterState.Cornered, true)]
        [TestCase(CritterState.Alert, true)]
        public void Headbutt_StateGate(CritterState s, bool expected) => Assert.AreEqual(expected, LivestockRules.StateAllowsHeadbutt(s));

        [Test]
        public void Headbutt_ImpulseKnocksDownButNeverKnocksOut()
        {
            var imp = LivestockRules.HeadbuttImpulse(new Vector3(1, 0, 0), Vector3.forward, T);
            Assert.That(imp.magnitude, Is.GreaterThanOrEqualTo(900f), "player ragdoll threshold");
            Assert.That(imp.magnitude, Is.LessThan(1800f), "player KO threshold");
            Assert.That(imp.x, Is.GreaterThan(0f)); Assert.That(imp.y, Is.GreaterThan(0f));
            Assert.IsFalse(LivestockRules.HeadbuttConnects(new Vector3(0, 0, 3f), false, T), "sidestepped out of reach");
        }

        [Test]
        public void Climb_HeightBandAndPerchExit()
        {
            Assert.IsTrue(LivestockRules.IsClimbable(1.2f, 0f, T));
            Assert.IsFalse(LivestockRules.IsClimbable(0.2f, 0f, T));
            Assert.IsFalse(LivestockRules.IsClimbable(4f, 0f, T));
            Assert.IsTrue(LivestockRules.ShouldLeavePerch(45f, 0f, 0.55f, T), "bored after 45 s");
            Assert.IsFalse(LivestockRules.ShouldLeavePerch(1f, 0.9f, 0.55f, T), "min perch");
            Assert.IsTrue(LivestockRules.ShouldLeavePerch(5f, 0.9f, 0.55f, T));
        }

        [Test]
        public void Moo_IntervalAndLift()
        {
            Assert.AreEqual(20f + 4f, LivestockRules.NextMoo(0f, 1f, T), 1e-4f);
            Assert.AreEqual(16f, LivestockRules.NextMoo(0f, 0f, T), 1e-4f);
            Assert.IsFalse(LivestockRules.CanLift(1, 4)); Assert.IsTrue(LivestockRules.CanLift(2, 4));
            Assert.AreEqual(2, LivestockRules.CarriersToLift(4));
        }

        [Test]
        public void Personalities_CowAndGoatData()
        {
            Content.Critters.Invalidate();
            Assert.IsTrue(Content.Critters.TryGet("Critter.Cow", out var cow));
            Assert.IsTrue(Content.Critters.TryGet("Critter.Goat", out var goat));
            Assert.AreEqual(1.5f, cow.MoveSpeed, 1e-3f); Assert.AreEqual(CorneredResponse.Freeze, cow.CorneredResponse);
            Assert.AreEqual(2, cow.SecureCarriers); Assert.That(cow.FleeThreshold, Is.GreaterThan(0.8f));
            Assert.AreEqual("Livestock.Cow", cow.GoodsId); Assert.IsNotNull(cow.Prefab);
            Assert.AreEqual(CorneredResponse.Charge, goat.CorneredResponse); Assert.AreEqual(0.6f, goat.Curiosity, 1e-3f);
            Assert.IsTrue((goat.Traits & CritterTrait.Climber) != 0); Assert.IsTrue((goat.ShortcutAccess & NavShortcut.Climb) != 0);
            Assert.AreEqual("Livestock.Goat", goat.GoodsId); Assert.IsNotNull(goat.Prefab);
            Assert.IsNotNull(goat.Prefab.GetComponent<GoatBehaviour>()); Assert.IsNotNull(cow.Prefab.GetComponent<CowBehaviour>());
        }
    }
}
