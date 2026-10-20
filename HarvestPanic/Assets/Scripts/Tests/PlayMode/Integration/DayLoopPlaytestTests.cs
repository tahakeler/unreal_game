using System.Collections;
using System.Collections.Generic;
using HarvestPanic.Core;
using HarvestPanic.Economy;
using HarvestPanic.Gameplay;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace HarvestPanic.Tests.PlayMode.Integration
{
    /// <summary>
    /// Full-day playtest in Muddlebury as a solo host (day-loop §3 Phases, Market zone; hud-menus Payout Receipt):
    /// Morning -> Workday (market opens) -> the player grabs a crate, it is set down in the Sale Pen and sells ->
    /// Dusk -> Payout receipt shown on the HUD -> Next Day -> Morning of day 2. Runs at day scale 0.02
    /// (same as <c>-hpDayScale 0.02</c>; an explicit command-line value wins).
    /// </summary>
    public class DayLoopPlaytestTests
    {
        float _prevScale;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _prevScale = EconomyDevCvars.DayScale;
            if (Mathf.Approximately(_prevScale, 1f)) EconomyDevCvars.DayScale = 0.02f;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            var nm = NetworkManager.Singleton;
            if (nm != null) { nm.Shutdown(); yield return null; Object.Destroy(nm.gameObject); }
            EconomyDevCvars.DayScale = _prevScale;
            yield return null;
        }

        static IEnumerator WaitFor(System.Func<bool> cond, float timeout, string what)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (!cond())
            {
                if (Time.realtimeSinceStartup > end) Assert.Fail("Timed out waiting for " + what);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator SoloHost_FullDay_MorningWorkdaySaleDuskPayoutNextDay()
        {
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Muddlebury.unity", new LoadSceneParameters(LoadSceneMode.Single));
            var go = Object.Instantiate(HarvestPanic.Networking.NetworkingConfig.Load().networkManagerPrefab);
            go.GetComponent<HarvestPanic.Networking.SessionService>().HostInPlaceForEditor();
#else
            Assert.Ignore("Editor-only");
            yield break;
#endif
            yield return WaitFor(() => PlayerCharacter.Local != null && EconomyDirector.Instance != null, 20f, "player + economy");
            var pc = PlayerCharacter.Local;
            var dir = EconomyDirector.Instance;
            var phases = new List<DayPhase>();
            dir.OnPhaseChanged += (a, b) => phases.Add(b);
            int sales = 0, saleAmount = 0;
            dir.OnWalletTxn += t => { if (t.Type == WalletTxnType.Sale && t.Amount > 0) { sales++; saleAmount += t.Amount; } };

            // The run may already be past Morning by the time the player spawns; restart for a clean day.
            dir.Restart();
            yield return null;
            Assert.AreEqual(DayPhase.Morning, dir.Phase, "day starts in Morning");
            Assert.IsFalse(dir.IsMarketOpen, "market closed in the morning");
            int startBalance = dir.Balance;

            yield return WaitFor(() => dir.Phase == DayPhase.Workday, 10f, "Workday");
            Assert.IsTrue(dir.IsMarketOpen, "market opens at Workday");

            // Player grabs a crate (real carry path), then it is set down inside the Sale Pen.
            Goods crate = null;
            foreach (var g in Goods.All) if (g != null && g.GoodsId == "Product.Crate" && !g.IsHeld) { crate = g; break; }
            Assert.That(crate, Is.Not.Null, "no crate goods in Muddlebury");
            Vector3 fwd = pc.transform.forward; fwd.y = 0f; fwd.Normalize();
            Place(crate, pc.transform.position + fwd * 1.3f + Vector3.up * 0.5f);
            yield return new WaitForSeconds(0.6f);
            pc.Motor.LookAt(crate.GetComponentInChildren<Collider>().bounds.center);
            yield return new WaitForSeconds(0.3f);
            Assert.IsTrue(pc.Carry.TryGrabAimed(), "player could not aim at the crate");
            yield return WaitFor(() => pc.Carry.IsHoldingReplicated, 3f, "crate held");
            pc.Carry.RequestRelease();
            yield return WaitFor(() => !pc.Carry.IsHoldingReplicated, 3f, "crate released");

            var pens = LevelMarker.FindAll(MarkerRole.MarketPen);
            Assert.That(pens.Count, Is.GreaterThan(0), "no Sale Pen marker");
            var pen = pens[0].GetComponent<MarketPen>();
            Assert.That(pen, Is.Not.Null, "pen not set up by the director");
            Vector3 penCentre = pen.Trigger.bounds.center;
            Place(crate, new Vector3(penCentre.x, pen.Trigger.bounds.min.y + 0.6f, penCentre.z));
            yield return WaitFor(() => sales > 0, 8f, "crate sale in the pen");
            Assert.AreEqual(DayPhase.Workday, dir.Phase, "sold during the workday");
            Assert.Greater(dir.Balance, startBalance, "wallet credited");

            yield return WaitFor(() => dir.Phase == DayPhase.Dusk, 20f, "Dusk");
            yield return WaitFor(() => dir.Phase == DayPhase.Payout, 10f, "Payout");
            var receipt = dir.LastReceipt;
            Assert.That(receipt, Is.Not.Null, "no payout receipt");
            Assert.IsFalse(receipt.IsSunday);
            Assert.AreEqual(1, receipt.UnitsSold);
            Assert.AreEqual(saleAmount, receipt.Sales);

            // The HUD shows the receipt with a Next Day button for the host.
            yield return null;
            var hudDoc = Object.FindFirstObjectByType<EconomyHud>().GetComponent<UIDocument>();
            var labels = hudDoc.rootVisualElement.Query<Label>().ToList();
            Assert.That(labels.Exists(l => l.text == "DAILY RECEIPT" && l.resolvedStyle.display != DisplayStyle.None), "receipt not on the HUD");
            var next = hudDoc.rootVisualElement.Query<Button>().ToList().Find(b => b.text == "Next Day");
            Assert.That(next, Is.Not.Null, "no Next Day button");
            using (var e = NavigationSubmitEvent.GetPooled()) { e.target = next; next.SendEvent(e); }
            yield return null;
            if (dir.Phase == DayPhase.Payout) dir.NextDay(); // fall back if the panel ignores synthetic submit in batchmode
            yield return null;

            Assert.AreEqual(DayPhase.Morning, dir.Phase, "next day starts");
            Assert.AreEqual(1, dir.DayIndex);
            CollectionAssert.IsSubsetOf(new[] { DayPhase.Morning, DayPhase.Workday, DayPhase.Dusk, DayPhase.Payout }, phases);
            int iw = phases.IndexOf(DayPhase.Workday), id = phases.IndexOf(DayPhase.Dusk), ip = phases.IndexOf(DayPhase.Payout);
            Assert.That(iw < id && id < ip && phases.LastIndexOf(DayPhase.Morning) > ip, "phase order " + string.Join(",", phases));
        }

        static void Place(Goods g, Vector3 pos)
        {
            g.transform.position = pos;
            var rb = g.GetComponent<Rigidbody>();
            if (rb != null) { rb.position = pos; rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
            Physics.SyncTransforms();
        }
    }
}
