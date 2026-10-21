using System.Collections;
using System.Collections.Generic;
using System.Text;
using HarvestPanic.Core;
using HarvestPanic.Economy;
using HarvestPanic.Gameplay;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HarvestPanic.Tests.PlayMode.Integration
{
    /// <summary>
    /// Regression tests for the bot full-week playtest findings (docs/qa/playtest/bot-week-report.md, fix/beta-bugs-1):
    /// every daily goods type sells when dropped in the Sale Pen; Morning goods rest on the ground without "Nobody"
    /// bruises (solo and 4p supply); the Landlord speaks in week 1 and when a rule is drawn; a continued run plans its
    /// visits. Muddlebury host at day scale 0.05.
    /// </summary>
    public class BetaBugsPlayTests
    {
        float _prevScale;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _prevScale = EconomyDevCvars.DayScale;
            EconomyDevCvars.DayScale = 0.05f;
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

        static IEnumerator Boot()
        {
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Muddlebury.unity", new LoadSceneParameters(LoadSceneMode.Single));
            var go = Object.Instantiate(HarvestPanic.Networking.NetworkingConfig.Load().networkManagerPrefab);
            go.GetComponent<HarvestPanic.Networking.SessionService>().HostInPlaceForEditor();
#else
            Assert.Ignore("Editor-only");
            yield break;
#endif
            yield return WaitFor(() => PlayerCharacter.Local != null && EconomyDirector.Instance != null, 30f, "player + economy");
        }

        static List<Goods> DailyGoods()
        {
            var l = new List<Goods>();
            foreach (var g in Goods.All) if (g != null && g.IsSpawned && g.Definition != null) l.Add(g);
            return l;
        }

        static void Place(Goods g, Vector3 pos)
        {
            g.transform.position = pos;
            var rb = g.GetComponent<Rigidbody>();
            if (rb != null) { rb.position = pos; rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
            Physics.SyncTransforms();
        }

        /// <summary>Bug 2 (S3): every daily goods type (incl. the 8-hand prize pumpkin and every crate) sells in the pen.</summary>
        [UnityTest, Timeout(180000)]
        public IEnumerator EveryDailyGood_DroppedInSalePen_Sells()
        {
            yield return Boot();
            var dir = EconomyDirector.Instance;
            dir.Restart();
            yield return null;
            yield return WaitFor(() => dir.Phase == DayPhase.Workday, 15f, "Workday");
            var pen = LevelMarker.FindAll(MarkerRole.MarketPen)[0].GetComponent<MarketPen>();
            Assert.That(pen, Is.Not.Null);
            var b = pen.Trigger.bounds;
            var drop = new Vector3(b.center.x, b.min.y + 0.6f, b.center.z);

            var sold = new List<string>();
            dir.OnWalletTxn += t => { if (t.Type == WalletTxnType.Sale && t.Amount > 0) sold.Add(t.GoodsId); };
            var goods = DailyGoods();
            Assert.That(goods.Count, Is.GreaterThanOrEqualTo(5), "Muddlebury spawns 5 daily goods");
            var report = new StringBuilder();
            int idx = 0;
            foreach (var g in goods)
            {
                string id = g.GoodsId; int before = sold.Count;
                int quote = g.CurrentValue; var state = g.BruiseState;
                // Bot-style drop: teleported with zero velocity (no carry, no throw), each to its own spot.
                Place(g, drop + new Vector3((idx % 2) * 2f - 1f, 0f, (idx / 2) * 1.5f - 1.5f));
                idx++;
                float end = Time.time + 6f;
                while (Time.time < end && sold.Count == before) yield return null;
                report.AppendLine($"{id}: sold={(sold.Count > before)} quote={quote} state={state} bp={(g != null ? g.BruisePoints : -1):0.0} penCount={pen.Count} pos={(g != null ? g.transform.position.ToString() : "gone")}");
            }
            Debug.Log("[BetaBugs] pen drops:\n" + report);
            Assert.AreEqual(goods.Count, sold.Count, "every daily good sells for > $0 in the pen\n" + report);
            Assert.IsTrue(dir.IsMarketOpen);
        }

        /// <summary>
        /// Checks the Morning spawn: every daily good rests on the ground next to its marker (not on top of the cart that
        /// overhangs two crate markers), no two overlap, none is bruised once the settle grace has expired, and the ledger
        /// has no "Nobody" event. Waits 5 s, past <see cref="EconomyTuning.SpawnGraceSeconds"/>.
        /// </summary>
        static IEnumerator AssertMorningSpawnClean(EconomyDirector dir, int expectedGoods, string label)
        {
            float end = Time.time + 5f;
            while (Time.time < end) yield return null;
            var goods = DailyGoods();
            var markers = LevelMarker.FindAll(MarkerRole.Goods);
            var report = new StringBuilder();
            foreach (var g in goods)
            {
                var col = g.GetComponent<Collider>();
                LevelMarker home = null; float best = float.MaxValue;
                foreach (var m in markers)
                {
                    if (!GoodsResolver.TryResolve(m.Key, out var def) || def.GoodsId != g.GoodsId) continue;
                    var d = m.transform.position - g.transform.position; d.y = 0f;
                    if (d.magnitude < best) { best = d.magnitude; home = m; }
                }
                report.AppendLine($"{g.GoodsId}: bp={g.BruisePoints:0.0} bottom={col.bounds.min.y:0.00} marker={(home != null ? home.name : "none")} dXZ={best:0.00} v={g.GetComponent<Rigidbody>().linearVelocity.magnitude:0.00}");
            }
            Debug.Log($"[BetaBugs] settle ({label}):\n" + report);
            Assert.AreEqual(expectedGoods, goods.Count, "daily goods spawned\n" + report);
            for (int i = 0; i < goods.Count; i++)
            {
                var g = goods[i];
                var b = g.GetComponent<Collider>().bounds;
                Assert.AreEqual(0f, g.BruisePoints, 0.001f, "spawn/settle bruised " + g.GoodsId + "\n" + report);
                Assert.Less(g.GetComponent<Rigidbody>().linearVelocity.magnitude, 0.2f, g.GoodsId + " still moving after 5 s\n" + report);
                Assert.Less(b.min.y, 0.5f, g.GoodsId + " is resting on a prop, not the ground\n" + report);
                for (int j = i + 1; j < goods.Count; j++)
                {
                    var o = goods[j].GetComponent<Collider>().bounds; o.Expand(-0.02f);
                    Assert.IsFalse(b.Intersects(o), $"{g.GoodsId} overlaps {goods[j].GoodsId}\n" + report);
                }
            }
            foreach (var g in goods)
            {
                float near = float.MaxValue;
                foreach (var m in markers)
                {
                    if (!GoodsResolver.TryResolve(m.Key, out var def) || def.GoodsId != g.GoodsId) continue;
                    var d = m.transform.position - g.transform.position; d.y = 0f; near = Mathf.Min(near, d.magnitude);
                }
                Assert.Less(near, 3f, g.GoodsId + " spawned far from its markers\n" + report);
            }
            foreach (var e in dir.Ledger.Day)
                Assert.AreNotEqual("Nobody", e.Event.CauseTag, "Nobody blame at spawn: " + e.Event.Verb + " " + e.Event.ItemId);
        }

        /// <summary>Bug 3 (S3): goods spawned at Morning rest on the ground and are not bruised by "Nobody" (solo: 4 crates + pumpkin).</summary>
        [UnityTest, Timeout(120000)]
        public IEnumerator SpawnedGoods_SettleWithoutBruises()
        {
            yield return Boot();
            var dir = EconomyDirector.Instance;
            dir.Restart();
            yield return null;
            Assert.AreEqual(DayPhase.Morning, dir.Phase);
            yield return AssertMorningSpawnClean(dir, 5, "solo");
        }

        /// <summary>Bug 4 (S2) supply: a 4-player week starts with 4 + 6 crates and the pumpkin, laid out without overlap.</summary>
        [UnityTest, Timeout(120000)]
        public IEnumerator CrewOfFour_MorningSpawnsTenCrates_OnTheGround()
        {
            yield return Boot();
            var dir = EconomyDirector.Instance;
            dir.ApplySave(new RunSaveData { Resume = RunResumePoint.WeekStart, Week = 1, DayOfWeek = 1, DayIndex = 0, Balance = 100, Players = 4 });
            yield return null;
            Assert.AreEqual(DayPhase.Morning, dir.Phase);
            Assert.AreEqual(1200, dir.Quota);
            int crates = 0;
            foreach (var g in DailyGoods()) if (g.GoodsId == "Product.Crate") crates++;
            Assert.AreEqual(10, crates, "4 crate markers + 2 per extra player");
            yield return AssertMorningSpawnClean(dir, 11, "4p");
        }

        /// <summary>Bug 3 (S3, Landlord): a continued run plans its week's visits (it used to resume with none, so no visit barks).</summary>
        [UnityTest, Timeout(120000)]
        public IEnumerator ContinuedRun_PlansTheWeeksLandlordVisit()
        {
            yield return Boot();
            var dir = EconomyDirector.Instance;
            dir.ApplySave(new RunSaveData { Resume = RunResumePoint.WeekStart, Week = 2, DayOfWeek = 1, DayIndex = 5, Balance = 300, Players = 1 });
            yield return null;
            Assert.AreEqual(2, dir.Week);
            Assert.AreEqual(1, dir.VisitPlan.Count, "week 2 has one Landlord visit (Tue-Thu)");
            Assert.That(dir.VisitPlan[0].dayOfWeek, Is.InRange(2, 4));
        }
        /// <summary>Bug 2 (S3) variant: all daily goods dumped on one spot at once (pile-up, still settling) all sell.</summary>
        [UnityTest, Timeout(180000)]
        public IEnumerator AllDailyGoods_DumpedTogether_AllSell()
        {
            yield return Boot();
            var dir = EconomyDirector.Instance;
            dir.Restart();
            yield return null;
            yield return WaitFor(() => dir.Phase == DayPhase.Workday, 15f, "Workday");
            var b = LevelMarker.FindAll(MarkerRole.MarketPen)[0].GetComponent<MarketPen>().Trigger.bounds;
            int sold = 0;
            dir.OnWalletTxn += t => { if (t.Type == WalletTxnType.Sale && t.Amount > 0) sold++; };
            var goods = DailyGoods();
            for (int i = 0; i < goods.Count; i++) Place(goods[i], new Vector3(b.center.x, b.min.y + 0.4f + i * 1.7f, b.center.z));
            yield return WaitFor(() => sold >= goods.Count, 15f, $"all {goods.Count} goods sold (sold {sold})");
        }

        /// <summary>Bug 4 (S3): the Landlord speaks in week 1 (run start) and when a weekly rule is drawn.</summary>
        [UnityTest, Timeout(240000)]
        public IEnumerator LandlordBarks_Week1Welcome_And_Week2Rule()
        {
            yield return Boot();
            var dir = EconomyDirector.Instance;
            var barks = new List<string>();
            dir.OnLandlordBark += l => barks.Add($"W{dir.Week}D{dir.DayOfWeek} {dir.Phase}: {l}");
            dir.Restart();
            yield return null;
            Assert.That(barks.Exists(l => l.Contains("Welcome")), "no week-1 bark: " + string.Join(" | ", barks));
            float deadline = Time.realtimeSinceStartup + 180f;
            while (dir.Week < 2 || dir.Phase != DayPhase.Morning)
            {
                Assert.Less(Time.realtimeSinceStartup, deadline, "timed out advancing to week 2 at " + dir.Phase);
                switch (dir.Phase)
                {
                    case DayPhase.Shop:
                        if (dir.DayOfWeek >= dir.Tuning.DaysPerWeek && dir.Balance < dir.Quota + dir.Iou)
                            dir.AddWalletTxn(new WalletTxn { Type = WalletTxnType.Refund, Amount = dir.Quota + dir.Iou - dir.Balance + 10, PlayerId = PlayerIds.None });
                        dir.NextDay(); break;
                    case DayPhase.Payout: case DayPhase.Sunday: dir.NextDay(); break;
                    case DayPhase.Collateral: case DayPhase.Evicted: Assert.Fail("rent not paid: " + dir.Phase); break;
                    default: dir.DebugSkipPhase(); break;
                }
                yield return null;
            }
            Assert.That(barks.Exists(l => l.Contains("New house rule")), "no rule bark in week 2: " + string.Join(" | ", barks));
            Assert.That(dir.LastLandlordBark, Is.Not.Null.And.Not.Empty);
            Debug.Log("[BetaBugs] barks:\n" + string.Join("\n", barks));
        }
    }
}
