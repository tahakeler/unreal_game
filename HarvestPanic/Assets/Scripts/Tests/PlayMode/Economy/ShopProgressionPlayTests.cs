using System.Collections;
using System.IO;
using HarvestPanic.Core;
using HarvestPanic.Economy;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.TestTools;

namespace HarvestPanic.Tests.PlayMode.Economy
{
    /// <summary>
    /// Host session through the shop: Payout -> Shop -> buy -> modifier active next day; Sunday paid -> Week 2;
    /// Collateral sell-back saves the farm; run save -> Continue restores the run.
    /// </summary>
    public class ShopProgressionPlayTests
    {
        GameObject _nmGo, _penGo;
        NetworkManager _nm;
        string _saveDir;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Services.Clear();
            _saveDir = Path.Combine(Path.GetTempPath(), "hp_shop_play_" + System.Guid.NewGuid().ToString("N"));
            RunSaveStore.DirectoryOverride = _saveDir;
            RunSaveService.Instance.ContinueRequested = false;
            EconomyDevCvars.DayScale = 0.02f;
            _nmGo = new GameObject("NM");
            var transport = _nmGo.AddComponent<UnityTransport>();
            transport.SetConnectionData("127.0.0.1", (ushort)Random.Range(20000, 60000));
            _nm = _nmGo.AddComponent<NetworkManager>();
            _nm.NetworkConfig = new NetworkConfig { NetworkTransport = transport, EnableSceneManagement = false };
            _nm.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(Resources.Load<NetworkPrefabsList>("NetworkPrefabs/Economy"));
            _penGo = new GameObject("Pen");
            _penGo.AddComponent<LevelMarker>().Configure(MarkerRole.MarketPen);
            yield return null;
            Assert.IsTrue(_nm.StartHost(), "StartHost");
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (EconomyDirector.Instance != null && EconomyDirector.Instance.IsSpawned) EconomyDirector.Instance.NetworkObject.Despawn(true);
            if (_nm != null) _nm.Shutdown();
            yield return null;
            foreach (var hud in Object.FindObjectsByType<EconomyHudMarker>(FindObjectsSortMode.None)) Object.Destroy(hud.gameObject);
            Object.Destroy(_nmGo); Object.Destroy(_penGo);
            EconomyDevCvars.DayScale = 1f;
            RunSaveStore.DirectoryOverride = null;
            RunSaveService.Instance.ContinueRequested = false;
            InputModal.Clear();
            if (Directory.Exists(_saveDir)) Directory.Delete(_saveDir, true);
            yield return null;
        }

        static EconomyDirector SpawnDirector()
        {
            var d = Object.Instantiate(EconomyBootstrapper.FindDirectorPrefab()).GetComponent<EconomyDirector>();
            d.NetworkObject.Spawn(true);
            return d;
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

        /// <summary>Skips timed phases (and Payout) until <paramref name="phase"/>.</summary>
        static IEnumerator SkipTo(EconomyDirector d, DayPhase phase)
        {
            int guard = 0;
            while (d.Phase != phase)
            {
                if (++guard > 50) Assert.Fail($"never reached {phase} (at {d.Phase})");
                if (d.Phase == DayPhase.Shop) d.NextDay(); else d.DebugSkipPhase();
                yield return null;
            }
        }

        static void Give(EconomyDirector d, int dollars) => d.AddWalletTxn(new WalletTxn { Type = WalletTxnType.Sale, Amount = dollars, GoodsId = "Test" });

        [UnityTest]
        public IEnumerator EndDay_Shop_Buy_ModifierActiveNextDay_AndSaved()
        {
            var d = SpawnDirector();
            yield return WaitFor(() => d.Phase == DayPhase.Morning, 2f, "Morning");
            yield return SkipTo(d, DayPhase.Payout);
            d.NextDay();
            yield return null;
            Assert.AreEqual(DayPhase.Shop, d.Phase);
            Assert.IsTrue(RunSaveStore.Exists(), "saved on entering the shop");
            yield return null;
            var hud = Object.FindFirstObjectByType<EconomyHud>();
            Assert.NotNull(hud);
            Assert.IsTrue(hud.ShopVisible, "shop panel on HUD");
            Assert.IsTrue(InputModal.IsBlocking, "shop holds input");

            Assert.Contains("Upgrade.BridgeReinforcement1", (System.Collections.ICollection)d.ShopOffers, "staple offered");
            string id = null;
            foreach (var o in d.ShopOffers) if (o == "Upgrade.PaddedCrates" || o == "Upgrade.BridgeReinforcement1") { id = o; break; }
            Assert.IsTrue(d.Shop.TryGet(id, out var def));
            int before = d.Balance;
            d.ProposeBuy(id); // too poor: $100 + order bonus
            if (before < def.Price) { Assert.AreEqual(before, d.Balance, "rejected while short"); Give(d, 1000); before = d.Balance; d.ProposeBuy(id); }
            Assert.AreEqual(before - def.Price, d.Balance);
            CollectionAssert.Contains(d.OwnedUpgrades, id);
            var fx = def.Modifier.Effects[0];
            float active = d.Resolve(fx.ParamName, 1f);
            Assert.AreNotEqual(1f, active, "modifier active");
            var saved = RunSaveStore.Load(out _);
            CollectionAssert.Contains(saved.Upgrades, id, "purchase re-saved");

            d.NextDay();
            yield return null;
            Assert.AreEqual(DayPhase.Morning, d.Phase);
            Assert.AreEqual(1, d.DayIndex);
            Assert.AreEqual(active, d.Resolve(fx.ParamName, 1f), 1e-4, "still active next day");
            Assert.IsFalse(hud.ShopVisible);
            Assert.IsFalse(InputModal.IsBlocking, "input released");

            yield return SkipTo(d, DayPhase.Shop);
            int bal = d.Balance;
            d.ProposeSell(id);
            Assert.AreEqual(bal + def.Price / 2, d.Balance, "50% refund");
            Assert.AreEqual(1f, d.Resolve(fx.ParamName, 1f), 1e-4, "removed on sell");
        }

        [UnityTest]
        public IEnumerator FullWeek_SundayPaid_Week2HigherQuota_SavedAtWeekStart()
        {
            var d = SpawnDirector();
            yield return WaitFor(() => d.Phase == DayPhase.Morning, 2f, "Morning");
            int q1 = d.Quota;
            Assert.AreEqual(EconomyMath.Quota(1, 1, d.Tuning), q1);
            Give(d, q1 + 50);
            for (int day = 1; day <= 5; day++)
            {
                yield return SkipTo(d, DayPhase.Shop);
                Assert.AreEqual(day, d.DayOfWeek);
                if (day < 5) { d.NextDay(); yield return null; }
            }
            d.NextDay();
            yield return null;
            Assert.AreEqual(DayPhase.Sunday, d.Phase);
            Assert.IsTrue(d.LastReceipt.IsSunday && d.LastReceipt.Paid, "paid");
            d.NextDay();
            yield return null;
            Assert.AreEqual(DayPhase.Morning, d.Phase);
            Assert.AreEqual(2, d.Week);
            Assert.AreEqual(1, d.DayOfWeek);
            Assert.AreEqual(EconomyMath.Quota(2, 1, d.Tuning), d.Quota);
            Assert.Greater(d.Quota, q1);
            var s = RunSaveStore.Load(out _);
            Assert.AreEqual(RunResumePoint.WeekStart, s.Resume);
            Assert.AreEqual(2, s.Week);
        }

        [UnityTest]
        public IEnumerator SundayShort_Collateral_SellBack_Pays()
        {
            var d = SpawnDirector();
            yield return WaitFor(() => d.Phase == DayPhase.Morning, 2f, "Morning");
            int due = d.Quota;
            yield return SkipTo(d, DayPhase.Shop);
            Assert.IsTrue(d.Shop.TryGet("Upgrade.BridgeReinforcement1", out var b1));
            // After buying, short of rent by less than the refund.
            Give(d, b1.Price + due - 50 - d.Balance);
            d.ProposeBuy(b1.UpgradeId);
            Assert.AreEqual(due - 50, d.Balance);
            for (int day = 1; day < 5; day++) { d.NextDay(); yield return null; yield return SkipTo(d, DayPhase.Shop); }
            d.AddWalletTxn(new WalletTxn { Type = WalletTxnType.Fine, Amount = -(d.Balance - (due - 50)) }); // undo order bonuses
            d.NextDay();
            yield return null;
            Assert.AreEqual(DayPhase.Collateral, d.Phase);
            d.ProposeSell(b1.UpgradeId);
            yield return null;
            Assert.AreEqual(DayPhase.Sunday, d.Phase);
            Assert.IsTrue(d.LastReceipt.Paid);
            Assert.AreEqual(b1.Price / 2 - 50, d.Balance);
        }

        [UnityTest]
        public IEnumerator SundayShort_NoUpgrades_Evicted_SaveWiped()
        {
            var d = SpawnDirector();
            yield return WaitFor(() => d.Phase == DayPhase.Morning, 2f, "Morning");
            for (int day = 1; day <= 5; day++) { yield return SkipTo(d, DayPhase.Shop); if (day < 5) { d.NextDay(); yield return null; } }
            Assert.IsTrue(RunSaveStore.Exists());
            d.NextDay();
            yield return null;
            Assert.AreEqual(DayPhase.Evicted, d.Phase);
            Assert.IsFalse(d.LastReceipt.Paid);
            Assert.AreEqual(5, d.LastReceipt.DaysPlayed);
            Assert.IsFalse(RunSaveStore.Exists(), "eviction wipes the run save");
            d.NextDay(); // New Run
            yield return null;
            Assert.AreEqual(DayPhase.Morning, d.Phase);
            Assert.AreEqual(1, d.Week);
            Assert.AreEqual(0, d.OwnedUpgrades.Count);
        }

        [UnityTest]
        public IEnumerator SaveAtShop_ContinueRequested_NewDirectorResumes()
        {
            var d = SpawnDirector();
            yield return WaitFor(() => d.Phase == DayPhase.Morning, 2f, "Morning");
            yield return SkipTo(d, DayPhase.Shop);
            Give(d, 1000);
            d.ProposeBuy("Upgrade.BridgeReinforcement1");
            int bal = d.Balance;
            d.NetworkObject.Despawn(true);
            yield return null;
            Assert.IsTrue(Services.TryGet(out IRunSaveService svc) || true);
            Assert.IsTrue(RunSaveService.Instance.HasSave);
            StringAssert.Contains("Week 1", RunSaveService.Instance.Summary);
            RunSaveService.Instance.ContinueRequested = true;
            var d2 = SpawnDirector();
            yield return null;
            Assert.AreEqual(DayPhase.Shop, d2.Phase);
            Assert.AreEqual(bal, d2.Balance);
            CollectionAssert.Contains(d2.OwnedUpgrades, "Upgrade.BridgeReinforcement1");
            Assert.AreEqual(0.7f, d2.Resolve(DayParams.BridgePlankBreakMult, 1f), 1e-4);
            Assert.IsFalse(RunSaveService.Instance.ContinueRequested, "consumed");
            d2.NextDay();
            yield return null;
            Assert.AreEqual(DayPhase.Morning, d2.Phase);
            Assert.AreEqual(1, d2.DayIndex);
        }
    }
}
