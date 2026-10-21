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
    /// every daily goods type sells when dropped in the Sale Pen, and spawned goods are not bruised by "Nobody"
    /// while they settle. Muddlebury solo host at day scale 0.05.
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

        /// <summary>Bug 3 (S3): goods spawned at Morning settle without bruises or "Nobody" blame events.</summary>
        [UnityTest, Timeout(120000)]
        public IEnumerator SpawnedGoods_SettleWithoutBruises()
        {
            yield return Boot();
            var dir = EconomyDirector.Instance;
            var blames = new List<BlameEvent>();
            dir.Restart();
            yield return null;
            Assert.AreEqual(DayPhase.Morning, dir.Phase);
            float end = Time.time + 3f;
            while (Time.time < end) yield return null;
            var report = new StringBuilder();
            foreach (var g in DailyGoods())
            {
                report.AppendLine($"{g.GoodsId}: bp={g.BruisePoints:0.0} y={g.transform.position.y:0.00} v={g.GetComponent<Rigidbody>().linearVelocity.magnitude:0.00}");
                Assert.AreEqual(0f, g.BruisePoints, 0.001f, "spawn/settle bruised " + g.GoodsId + "\n" + report);
                Assert.Less(g.GetComponent<Rigidbody>().linearVelocity.magnitude, 0.2f, g.GoodsId + " still moving after 3 s");
            }
            Debug.Log("[BetaBugs] settle:\n" + report);
            foreach (var e in dir.Ledger.Day)
                Assert.AreNotEqual("Nobody", e.Event.CauseTag, "Nobody blame at spawn: " + e.Event.Verb + " " + e.Event.ItemId);
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
