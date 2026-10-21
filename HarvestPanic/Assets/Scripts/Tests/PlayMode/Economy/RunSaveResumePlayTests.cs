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
    /// GDD 18.7 DoD item 5 on a real host session: play two Shop phases, "quit" (despawn the director and shut the
    /// NetworkManager down, reset the in-memory save service as a fresh process would), start a new host and Continue.
    /// Clean quit resumes the latest Shop; a save torn by a kill mid-write resumes the previous Shop from .bak.
    /// </summary>
    public class RunSaveResumePlayTests
    {
        GameObject _nmGo, _penGo;
        NetworkManager _nm;
        string _saveDir;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Services.Clear();
            _saveDir = Path.Combine(Path.GetTempPath(), "hp_save_resume_" + System.Guid.NewGuid().ToString("N"));
            RunSaveStore.DirectoryOverride = _saveDir;
            RunSaveService.Instance.ContinueRequested = false;
            EconomyDevCvars.DayScale = 0.02f;
            _nmGo = new GameObject("NM");
            var transport = _nmGo.AddComponent<UnityTransport>();
            _nm = _nmGo.AddComponent<NetworkManager>();
            _nm.NetworkConfig = new NetworkConfig { NetworkTransport = transport, EnableSceneManagement = false };
            _nm.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(Resources.Load<NetworkPrefabsList>("NetworkPrefabs/Economy"));
            _penGo = new GameObject("Pen");
            _penGo.AddComponent<LevelMarker>().Configure(MarkerRole.MarketPen);
            yield return null;
            yield return StartHost();
        }

        IEnumerator StartHost()
        {
            ((UnityTransport)_nm.NetworkConfig.NetworkTransport).SetConnectionData("127.0.0.1", (ushort)Random.Range(20000, 60000));
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

        /// <summary>Plays to the Shop of day 0 (buys an upgrade), then to the Shop of day 1. Returns (day0 balance, day1 balance).</summary>
        IEnumerator PlayTwoShops(int[] balances)
        {
            var d = SpawnDirector();
            yield return WaitFor(() => d.Phase == DayPhase.Morning, 2f, "Morning");
            yield return SkipTo(d, DayPhase.Shop);
            Assert.AreEqual(0, d.DayIndex);
            d.AddWalletTxn(new WalletTxn { Type = WalletTxnType.Sale, Amount = 1000, GoodsId = "Test" });
            d.ProposeBuy("Upgrade.BridgeReinforcement1");
            CollectionAssert.Contains(d.OwnedUpgrades, "Upgrade.BridgeReinforcement1");
            balances[0] = d.Balance;
            d.NextDay();
            yield return null;
            yield return SkipTo(d, DayPhase.Shop);
            Assert.AreEqual(1, d.DayIndex);
            balances[1] = d.Balance;
            Assert.IsTrue(RunSaveStore.Exists(), "saved at the second Shop");
        }

        /// <summary>Ends the session the way quitting to desktop does, then boots a fresh host and presses Continue.</summary>
        IEnumerator QuitAndContinue()
        {
            EconomyDirector.Instance.NetworkObject.Despawn(true);
            _nm.Shutdown();
            yield return WaitFor(() => !_nm.ShutdownInProgress && !_nm.IsListening, 5f, "shutdown");
            yield return null;
            Services.Clear();
            RunSaveService.Instance.ContinueRequested = false; // statics of a fresh process
            RunSaveService.Install();
            yield return StartHost();
            Assert.IsTrue(Services.TryGet(out IRunSaveService svc) && svc.HasSave, "menu shows Continue Farm");
            svc.ContinueRequested = true;
        }

        [UnityTest]
        public IEnumerator QuitToDesktop_Continue_ResumesLatestShop()
        {
            var bal = new int[2];
            yield return PlayTwoShops(bal);
            yield return QuitAndContinue();
            StringAssert.Contains("Week 1", RunSaveService.Instance.Summary);
            var d2 = SpawnDirector();
            yield return null;
            Assert.AreEqual(DayPhase.Shop, d2.Phase);
            Assert.AreEqual(1, d2.DayIndex, "latest Shop");
            Assert.AreEqual(bal[1], d2.Balance);
            CollectionAssert.Contains(d2.OwnedUpgrades, "Upgrade.BridgeReinforcement1");
            Assert.AreEqual(0.7f, d2.Resolve(DayParams.BridgePlankBreakMult, 1f), 1e-4, "upgrade effect restored");
            d2.NextDay();
            yield return null;
            Assert.AreEqual(DayPhase.Morning, d2.Phase);
            Assert.AreEqual(2, d2.DayIndex, "run continues");
        }

        [UnityTest]
        public IEnumerator KilledMidSave_TornMainFile_ResumesPreviousShopFromBak()
        {
            var bal = new int[2];
            yield return PlayTwoShops(bal);
            string main = RunSaveStore.PathFor();
            Assert.IsTrue(File.Exists(main + ".bak"));
            byte[] b = File.ReadAllBytes(main);
            var torn = new byte[b.Length / 2];
            System.Array.Copy(b, torn, torn.Length);
            File.WriteAllBytes(main, torn); // process killed while the save was being written
            yield return QuitAndContinue();
            var d2 = SpawnDirector();
            yield return null;
            Assert.AreEqual(DayPhase.Shop, d2.Phase);
            Assert.AreEqual(0, d2.DayIndex, "one Shop phase lost, no more");
            Assert.AreEqual(bal[0], d2.Balance);
            CollectionAssert.Contains(d2.OwnedUpgrades, "Upgrade.BridgeReinforcement1", "purchase made in that Shop survives");
            d2.NextDay();
            yield return null;
            Assert.AreEqual(1, d2.DayIndex);
            Assert.IsNotNull(RunSaveStore.Load(out _), "save healthy again after the next boundary");
        }
    }
}
