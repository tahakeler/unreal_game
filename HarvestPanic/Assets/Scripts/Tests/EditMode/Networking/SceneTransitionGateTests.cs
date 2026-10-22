using System.Collections.Generic;
using HarvestPanic.Networking;
using NUnit.Framework;

namespace HarvestPanic.Tests.EditMode.Networking
{
    /// <summary>Pure ordering rules for joins vs NGO scene switches (join-during-load regression).</summary>
    public class SceneTransitionGateTests
    {
        readonly List<ulong> _release = new List<ulong>();
        readonly List<ulong> _refuse = new List<ulong>();

        static SceneTransitionGate Gate() => new SceneTransitionGate { MaxHoldSeconds = 15f, MaxJoinerWaitSeconds = 20f, MaxSceneLoadSeconds = 130f };

        [Test]
        public void Idle_JoinsNotHeld_SwitchStartsNow()
        {
            var g = Gate();
            Assert.IsFalse(g.ShouldHoldJoins);
            Assert.IsTrue(g.RequestSwitch("Lobby", 0f));
            Assert.IsNull(g.Tick(1f, _release, _refuse));
            CollectionAssert.IsEmpty(_release);
            CollectionAssert.IsEmpty(_refuse);
        }

        [Test]
        public void JoinDuringServerLoad_HeldUntilServerLoadFinished()
        {
            var g = Gate();
            g.ServerLoadStarted(0f);
            Assert.IsTrue(g.ShouldHoldJoins, "joins must wait while the server loads");
            g.Hold(7, 0.5f);
            g.Tick(2f, _release, _refuse);
            CollectionAssert.IsEmpty(_release, "still loading: keep holding");
            CollectionAssert.IsEmpty(_refuse);

            g.ServerLoadFinished();
            g.Tick(2.1f, _release, _refuse);
            CollectionAssert.AreEqual(new ulong[] { 7 }, _release);
            Assert.AreEqual(0, g.HeldCount);
            Assert.IsFalse(g.IsHeld(7));
        }

        [Test]
        public void HeldTooLong_RefusedNotReleased()
        {
            var g = Gate();
            g.ServerLoadStarted(0f);
            g.Hold(3, 1f);
            g.Tick(15.9f, _release, _refuse);
            CollectionAssert.IsEmpty(_refuse, "14.9 s held: below the limit");
            g.Tick(16f, _release, _refuse);
            CollectionAssert.AreEqual(new ulong[] { 3 }, _refuse);
            CollectionAssert.IsEmpty(_release);
            Assert.AreEqual(0, g.HeldCount);
        }

        [Test]
        public void SwitchWhileJoinerSynchronising_QueuedUntilSynchronized()
        {
            var g = Gate();
            g.Approved(5, 0f);
            Assert.IsFalse(g.RequestSwitch("Muddlebury", 1f), "joiner still synchronising: queue the switch");
            Assert.AreEqual("Muddlebury", g.QueuedScene);
            Assert.IsTrue(g.ShouldHoldJoins, "a queued switch also holds new joins");
            Assert.IsNull(g.Tick(2f, _release, _refuse));

            g.Synchronized(5);
            Assert.AreEqual("Muddlebury", g.Tick(2.1f, _release, _refuse));
            Assert.IsNull(g.QueuedScene);
        }

        [Test]
        public void HeldJoin_NotReleasedOnTheTickTheQueuedSwitchStarts()
        {
            var g = Gate();
            g.Approved(5, 0f);
            g.RequestSwitch("Muddlebury", 0f);
            g.Hold(6, 0.5f);
            g.Synchronized(5);
            Assert.AreEqual("Muddlebury", g.Tick(1f, _release, _refuse));
            CollectionAssert.IsEmpty(_release, "the switch starts now; the held join waits for it");
            g.ServerLoadStarted(1f);
            g.Tick(2f, _release, _refuse);
            CollectionAssert.IsEmpty(_release);
            g.ServerLoadFinished();
            g.Tick(3f, _release, _refuse);
            CollectionAssert.AreEqual(new ulong[] { 6 }, _release);
        }

        [Test]
        public void SwitchRequestedDuringServerLoad_StartsAfterIt()
        {
            var g = Gate();
            g.ServerLoadStarted(0f);
            Assert.IsFalse(g.RequestSwitch("Muddlebury", 0.1f));
            Assert.IsNull(g.Tick(1f, _release, _refuse));
            g.ServerLoadFinished();
            Assert.AreEqual("Muddlebury", g.Tick(1.1f, _release, _refuse));
        }

        [Test]
        public void StuckJoiner_StopsBlockingAfterWaitLimit()
        {
            var g = Gate();
            g.Approved(9, 0f);
            Assert.IsFalse(g.RequestSwitch("Muddlebury", 1f));
            Assert.IsNull(g.Tick(19.9f, _release, _refuse));
            Assert.AreEqual("Muddlebury", g.Tick(20f, _release, _refuse), "a stuck joiner must not freeze the host");
            Assert.IsTrue(g.RequestSwitch("Lobby", 21f), "stale joiner no longer blocks later switches");
        }

        [Test]
        public void DisconnectWhileHeldOrJoining_Forgotten()
        {
            var g = Gate();
            g.Approved(1, 0f);
            g.ServerLoadStarted(0f);
            g.Hold(2, 0f);
            g.Disconnected(1);
            g.Disconnected(2);
            Assert.AreEqual(0, g.HeldCount);
            Assert.AreEqual(0, g.JoiningCount);
            g.ServerLoadFinished();
            Assert.IsTrue(g.RequestSwitch("Lobby", 1f));
        }

        [Test]
        public void ServerLoadNeverReported_StopsBlockingAfterSafetyLimit()
        {
            var g = Gate();
            g.ServerLoadStarted(0f);
            g.Hold(4, 0f);
            g.MaxHoldSeconds = 1000f;
            g.Tick(130f, _release, _refuse);
            CollectionAssert.IsEmpty(_release);
            g.Tick(130.5f, _release, _refuse);
            CollectionAssert.AreEqual(new ulong[] { 4 }, _release);
            Assert.IsFalse(g.ServerLoading);
        }

        [Test]
        public void LatestQueuedSwitchWins_AndResetClearsAll()
        {
            var g = Gate();
            g.Approved(1, 0f);
            g.RequestSwitch("Lobby", 0f);
            g.RequestSwitch("Muddlebury", 0.5f);
            Assert.AreEqual("Muddlebury", g.QueuedScene);
            g.Hold(2, 0f);
            g.ServerLoadStarted(0f);
            g.Reset();
            Assert.IsNull(g.QueuedScene);
            Assert.AreEqual(0, g.HeldCount);
            Assert.AreEqual(0, g.JoiningCount);
            Assert.IsFalse(g.ShouldHoldJoins);
        }

        [Test]
        public void BusyReason_IsPlayerReadable()
        {
            StringAssert.Contains("Try joining again", ConnectionApproval.BusyReason);
        }
    }
}
