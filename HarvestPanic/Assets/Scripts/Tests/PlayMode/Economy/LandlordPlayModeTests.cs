using System;
using System.Collections;
using HarvestPanic.Core;
using HarvestPanic.Economy;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HarvestPanic.Tests.PlayMode.Economy
{
    /// <summary>Host session: Landlord Sunday walk-in plays and resolves (paid + evicted); HUD shows the weather forecast.</summary>
    public class LandlordPlayModeTests
    {
        sealed class FakeWeather : IWeatherService
        {
            public WeatherKind Current => WeatherKind.Rain;
            public string ForecastText => "Rain: mud is slower, goods get soaked";
            public Vector3 Wind => Vector3.zero;
            public event Action<WeatherKind, WeatherKind> OnWeatherChanged { add { } remove { } }
        }

        GameObject _nmGo, _ground, _barn;
        NetworkManager _nm;
        float _walk;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Services.Clear();
            EconomyDevCvars.DayScale = 0.02f;
            _nmGo = new GameObject("NM");
            var transport = _nmGo.AddComponent<UnityTransport>();
            transport.SetConnectionData("127.0.0.1", (ushort)UnityEngine.Random.Range(20000, 60000));
            _nm = _nmGo.AddComponent<NetworkManager>();
            _nm.NetworkConfig = new NetworkConfig { NetworkTransport = transport, EnableSceneManagement = false };
            _nm.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(Resources.Load<NetworkPrefabsList>("NetworkPrefabs/Economy"));
            _ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            _barn = new GameObject("Barn");
            _barn.AddComponent<LevelMarker>().Configure(MarkerRole.BarnRespawn);
            yield return null;
            Assert.IsTrue(_nm.StartHost());
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (EconomyDirector.Instance != null)
            {
                EconomyDirector.Instance.Tuning.VisitWalkDistance = _walk > 0f ? _walk : EconomyDirector.Instance.Tuning.VisitWalkDistance;
                if (EconomyDirector.Instance.IsSpawned) EconomyDirector.Instance.NetworkObject.Despawn(true);
            }
            if (_nm != null) _nm.Shutdown();
            yield return null;
            foreach (var hud in Object.FindObjectsByType<EconomyHudMarker>(FindObjectsSortMode.None)) Object.Destroy(hud.gameObject);
            if (LandlordVisitor.Current != null) Object.Destroy(LandlordVisitor.Current.gameObject);
            Object.Destroy(_nmGo); Object.Destroy(_ground); Object.Destroy(_barn);
            EconomyDevCvars.DayScale = 1f;
            Services.Clear();
            yield return null;
        }

        static IEnumerator WaitFor(Func<bool> cond, float timeout, string what)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (!cond())
            {
                if (Time.realtimeSinceStartup > end) Assert.Fail("Timed out waiting for " + what);
                yield return null;
            }
        }

        EconomyDirector Spawn()
        {
            var d = Object.Instantiate(EconomyBootstrapper.FindDirectorPrefab()).GetComponent<EconomyDirector>();
            _walk = d.Tuning.VisitWalkDistance;
            d.Tuning.VisitWalkDistance = 3f;
            d.NetworkObject.Spawn(true);
            return d;
        }

        IEnumerator RunToSunday(EconomyDirector d)
        {
            for (int i = 0; i < 80 && d.Phase != DayPhase.Sunday && d.Phase != DayPhase.Evicted; i++) { d.DebugSkipPhase(); yield return null; }
        }

        [UnityTest]
        public IEnumerator SundayVisit_Paid_PlaysAndResolves()
        {
            var d = Spawn();
            yield return WaitFor(() => d.Phase == DayPhase.Morning, 2f, "Morning");
            d.AddWalletTxn(new WalletTxn { Type = WalletTxnType.Sale, Amount = 2000, GoodsId = "Crop.Turnip" });
            yield return RunToSunday(d);
            Assert.AreEqual(DayPhase.Sunday, d.Phase);
            Assert.IsTrue(d.LastReceipt.IsSunday && d.LastReceipt.Paid);
            var v = LandlordVisitor.Current;
            Assert.NotNull(v, "Landlord walked in");
            var hud = Object.FindFirstObjectByType<EconomyHud>();
            if (hud != null) Assert.IsFalse(hud.ReceiptVisible, "receipt waits for the walk-in");
            yield return WaitFor(() => !v.IsPlaying, 20f, "visit done");
            Assert.AreEqual(LandlordVerdict.Paid, v.Script.Verdict);
            Assert.AreEqual("PAID", v.Script.Stamp);
            Assert.AreEqual(LandlordText.SundayPaid, v.Script.Line);
            if (hud != null) { yield return null; yield return null; Assert.IsTrue(hud.ReceiptVisible, "receipt after verdict"); }
            d.NextDay();
            yield return null;
            Assert.IsTrue(LandlordVisitor.Current == null, "he leaves Monday");
            Assert.AreEqual(2, d.Week);
            Assert.AreEqual(1, System.Linq.Enumerable.Count(d.ActiveLandlordRules), "Week 2 draws one rule");
        }

        [UnityTest]
        public IEnumerator SundayVisit_Short_Evicted()
        {
            var d = Spawn();
            yield return WaitFor(() => d.Phase == DayPhase.Morning, 2f, "Morning");
            yield return RunToSunday(d);
            Assert.AreEqual(DayPhase.Evicted, d.Phase);
            var v = LandlordVisitor.Current;
            Assert.NotNull(v);
            yield return WaitFor(() => !v.IsPlaying, 20f, "visit done");
            Assert.AreEqual(LandlordVerdict.Evicted, v.Script.Verdict);
            Assert.AreEqual("EVICTED", v.Script.Stamp);
        }

        [UnityTest]
        public IEnumerator Hud_ShowsForecastOnMorningCard_AndWeatherIcon()
        {
            EconomyDevCvars.DayScale = 1f;
            Services.Register<IWeatherService>(new FakeWeather());
            var d = Spawn();
            yield return WaitFor(() => d.Phase == DayPhase.Morning, 2f, "Morning");
            EconomyHud hud = null;
            yield return WaitFor(() => (hud = Object.FindFirstObjectByType<EconomyHud>()) != null && hud.ForecastShown != null, 3f, "forecast");
            StringAssert.Contains("Rain: mud is slower", hud.ForecastShown);
            Assert.AreEqual(WeatherKind.Rain, hud.WeatherIconShown);
            d.DebugSkipPhase();
            yield return null; yield return null;
            Assert.IsNull(hud.ForecastShown, "forecast only on the morning card");
        }

        [UnityTest]
        public IEnumerator Inspection_FinesWallet_AndBarks()
        {
            var d = Spawn();
            yield return WaitFor(() => d.Phase == DayPhase.Morning, 2f, "Morning");
            d.Tuning.InspectionChecksPicked = 4;
            int before = d.Balance;
            var r = d.RunInspection(new InspectionSnapshot { BrokePlankToday = true, LooseLivestock = 2 });
            d.Tuning.InspectionChecksPicked = 3;
            Assert.AreEqual(50, r.Fine); // W1: $25/check, 2 failed
            Assert.AreEqual(before - 50, d.Balance);
            Assert.IsNotEmpty(d.LastLandlordBark);
        }
    }
}
