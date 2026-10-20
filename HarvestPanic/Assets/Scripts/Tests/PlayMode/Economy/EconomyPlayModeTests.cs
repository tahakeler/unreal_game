using System.Collections;
using System.IO;
using HarvestPanic.Core;
using HarvestPanic.Economy;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace HarvestPanic.Tests.PlayMode.Economy
{
    /// <summary>Host session: a crate rests in the market pen, sells, the day ends, payout totals match.</summary>
    public class EconomyPlayModeTests
    {
        GameObject _nmGo, _penGo, _ground;
        NetworkManager _nm;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Services.Clear();
            EconomyDevCvars.DayScale = 0.02f; // Morning 0.6 s, Workday 10.8 s
            _nmGo = new GameObject("NM");
            var transport = _nmGo.AddComponent<UnityTransport>();
            transport.SetConnectionData("127.0.0.1", (ushort)Random.Range(20000, 60000));
            _nm = _nmGo.AddComponent<NetworkManager>();
            _nm.NetworkConfig = new NetworkConfig { NetworkTransport = transport, EnableSceneManagement = false };
            var list = Resources.Load<NetworkPrefabsList>("NetworkPrefabs/Economy");
            Assert.NotNull(list, "Economy network prefab list");
            _nm.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(list);

            _ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            _ground.transform.localScale = Vector3.one * 3f;
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
            Object.Destroy(_nmGo); Object.Destroy(_penGo); Object.Destroy(_ground);
            EconomyDevCvars.DayScale = 1f;
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
        public IEnumerator SellCrateInPen_EndDay_PayoutTotalsCorrect()
        {
            var director = Object.Instantiate(EconomyBootstrapper.FindDirectorPrefab()).GetComponent<EconomyDirector>();
            director.NetworkObject.Spawn(true);
            Assert.IsTrue(Services.TryGet(out IEconomyService eco));
            Assert.AreSame(director, eco);
            Assert.IsTrue(Services.TryGet(out IDayModifierService _));
            yield return WaitFor(() => director.Phase == DayPhase.Morning, 2f, "Morning");
            Assert.AreEqual(director.Tuning.StartingBalance, director.Balance);
            Assert.IsFalse(director.IsMarketOpen);

            Assert.IsTrue(Content.Goods.TryGet("Product.Crate", out var def));
            var crate = Object.Instantiate(def.WorldPrefab, new Vector3(0f, 0.4f, 0f), Quaternion.identity).GetComponent<Goods>();
            crate.NetworkObject.Spawn(true);
            crate.RecordTouch(NetworkManager.ServerClientId);

            int sold = 0; WalletTxn saleTxn = default;
            director.OnWalletTxn += t => { if (t.Type == WalletTxnType.Sale) { sold++; saleTxn = t; } };

            yield return WaitFor(() => director.Phase == DayPhase.Workday, 5f, "Workday");
            int expected = director.QuoteValue(crate, true);
            Assert.Greater(expected, 0);
            yield return new WaitForSeconds(1.0f);
            Assert.AreEqual(0, sold, "not sold before resting 2 s");
            yield return WaitFor(() => sold > 0, 5f, "sale");
            Assert.AreEqual(1, sold);
            Assert.AreEqual(expected, saleTxn.Amount);
            Assert.AreEqual(NetworkManager.ServerClientId, saleTxn.PlayerId);
            Assert.IsTrue(crate == null || !crate.IsSpawned, "sold crate despawned");

            ReceiptData receipt = null;
            director.OnReceipt += r => receipt = r;
            while (director.Phase != DayPhase.Payout) { director.DebugSkipPhase(); yield return null; }
            yield return WaitFor(() => receipt != null, 2f, "receipt");

            Assert.AreEqual(expected, receipt.Sales);
            Assert.AreEqual(1, receipt.UnitsSold);
            Assert.AreEqual(receipt.Sales + receipt.OrderBonus + receipt.Costs, receipt.Net);
            Assert.AreEqual(director.Tuning.StartingBalance + receipt.Net, receipt.Balance);
            Assert.AreEqual(director.Balance, receipt.Balance);
            Assert.AreEqual(EconomyMath.Quota(1, 1, director.Tuning), receipt.Quota);
            int sum = 0; foreach (var t in director.WalletState.History) sum += t.Amount;
            Assert.AreEqual(director.Balance - director.Tuning.StartingBalance, sum, "wallet audit");

            director.NextDay();
            yield return null;
            Assert.AreEqual(DayPhase.Shop, director.Phase, "Payout -> Shop");
            director.NextDay();
            yield return null;
            Assert.AreEqual(DayPhase.Morning, director.Phase);
            Assert.AreEqual(1, director.DayIndex);
        }

        [UnityTest]
        public IEnumerator RespawnFee_FloorAndBlame()
        {
            var director = Object.Instantiate(EconomyBootstrapper.FindDirectorPrefab()).GetComponent<EconomyDirector>();
            director.NetworkObject.Spawn(true);
            yield return WaitFor(() => director.Phase == DayPhase.Morning, 2f, "Morning");
            director.AddWalletTxn(new WalletTxn { Type = WalletTxnType.Fine, Amount = -280 });
            Assert.AreEqual(-180, director.Balance);
            int charged = director.ChargeRespawnFee(0, BlameVerb.Drowned);
            Assert.AreEqual(25, charged);
            Assert.AreEqual(-200, director.Balance);
            Assert.AreEqual(5, director.Iou);
            Assert.AreEqual(1, director.Ledger.Day.Count);
            Assert.AreEqual(BlameVerb.Drowned, director.Ledger.Day[0].Event.Verb);
        }
    }
}
