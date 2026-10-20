using System.Collections;
using HarvestPanic.Core;
using HarvestPanic.Economy;
using HarvestPanic.Weather;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.TestTools;

namespace HarvestPanic.Tests.PlayMode.Weather
{
    /// <summary>
    /// weather-twists: forced Rain makes its Day Modifier live (soak, mud, slick ground, river); the director rolls
    /// at Morning; weather replicates host -> client (second in-process NetworkManager, own random UDP port).
    /// </summary>
    public class WeatherPlayModeTests
    {
        NetworkManager _host, _client;
        ushort _port;

        static NetworkManager MakeNm(string name, ushort port)
        {
            var go = new GameObject(name);
            var t = go.AddComponent<UnityTransport>();
            t.SetConnectionData("127.0.0.1", port);
            var nm = go.AddComponent<NetworkManager>();
            nm.NetworkConfig = new NetworkConfig { NetworkTransport = t, EnableSceneManagement = false };
            nm.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(Resources.Load<NetworkPrefabsList>("NetworkPrefabs/Economy"));
            nm.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(Resources.Load<NetworkPrefabsList>("NetworkPrefabs/Weather"));
            return nm;
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Services.Clear();
            WeatherBootstrapper.Enabled = false;
            EconomyDevCvars.DayScale = 0.02f;
            _port = (ushort)Random.Range(21000, 59000);
            _host = MakeNm("NM Host", _port);
            yield return null;
            Assert.IsTrue(_host.StartHost(), "StartHost");
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_client != null) { _client.Shutdown(); yield return null; Object.Destroy(_client.gameObject); }
            if (WeatherDirector.Instance != null && WeatherDirector.Instance.IsSpawned) WeatherDirector.Instance.NetworkObject.Despawn(true);
            if (EconomyDirector.Instance != null && EconomyDirector.Instance.IsSpawned) EconomyDirector.Instance.NetworkObject.Despawn(true);
            if (_host != null) _host.Shutdown();
            yield return null;
            foreach (var hud in Object.FindObjectsByType<EconomyHudMarker>(FindObjectsSortMode.None)) Object.Destroy(hud.gameObject);
            foreach (var w in Object.FindObjectsByType<WeatherDirector>(FindObjectsSortMode.None)) Object.Destroy(w.gameObject);
            if (_host != null) Object.Destroy(_host.gameObject);
            EconomyDevCvars.DayScale = 1f;
            WeatherBootstrapper.Enabled = true;
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

        static WeatherDirector SpawnWeather()
        {
            var w = Object.Instantiate(WeatherBootstrapper.FindDirectorPrefab()).GetComponent<WeatherDirector>();
            w.NetworkObject.Spawn(true);
            return w;
        }

        static EconomyDirector SpawnEconomy()
        {
            var e = Object.Instantiate(EconomyBootstrapper.FindDirectorPrefab()).GetComponent<EconomyDirector>();
            e.NetworkObject.Spawn(true);
            return e;
        }

        [UnityTest]
        public IEnumerator ForcedRain_ModifiersActive()
        {
            var eco = SpawnEconomy();
            var w = SpawnWeather();
            yield return WaitFor(() => eco.Phase == DayPhase.Morning, 3f, "Morning");
            yield return null;
            Assert.IsTrue(Services.TryGet(out IWeatherService svc));
            Assert.AreSame(w, svc);
            w.ForceWeather(WeatherKind.Rain);
            yield return null;
            var day = Services.Get<IDayModifierService>();
            Assert.AreEqual(WeatherKind.Rain, svc.Current);
            Assert.IsTrue(day.HasWorldTag("World.Rain"));
            Assert.AreEqual(45f, day.Resolve(DayParams.WeatherSoakSeconds, 0f), 1e-4);
            Assert.AreEqual(0.6f * 0.85f, day.Resolve(DayParams.MudSpeedMult, 0.6f), 1e-4);
            Assert.AreEqual(1.4f, day.Resolve(DayParams.RiverCurrentMult, 1f), 1e-4);
            Assert.Less(MovementModifiers.Evaluate(new Vector3(500f, 0f, 500f)), 1f, "slick ground slows players");
            StringAssert.Contains("Rain", svc.ForecastText);

            // Only one weather card is ever active (ReplaceGroup) and the old Economy roll stays out.
            int weather = 0;
            foreach (var m in eco.ActiveModifiers) if (m.Category == DayModifierCategory.Weather) weather++;
            Assert.AreEqual(1, weather);

            // Forced weather persists through the next morning.
            w.ForceWeather(WeatherKind.Rain);
            yield return WaitFor(() => eco.Phase == DayPhase.Workday, 5f, "Workday");
            eco.DebugSkipPhase(); // -> Dusk
            yield return WaitFor(() => eco.Phase == DayPhase.Dusk || eco.Phase == DayPhase.Payout, 5f, "Dusk/Payout");
            while (eco.Phase != DayPhase.Payout) { eco.DebugSkipPhase(); yield return null; }
            eco.NextDay();
            yield return null; yield return null;
            Assert.AreEqual(DayPhase.Morning, eco.Phase);
            Assert.AreEqual(1, eco.DayIndex);
            Assert.IsTrue(day.HasWorldTag(WeatherDeck.WorldTag(WeatherKind.Rain)), "rain re-applied on day 2");
        }

        [UnityTest]
        public IEnumerator SeededRoll_AppliedAtMorning_AndRestartReapplies()
        {
            var eco = SpawnEconomy();
            var w = SpawnWeather();
            yield return WaitFor(() => eco.Phase == DayPhase.Morning, 3f, "Morning");
            yield return null; yield return null;
            var expected = WeatherDeck.Roll(w.Seed, 0, w.Tuning.Weights);
            Assert.AreEqual(expected, w.Current);
            var day = Services.Get<IDayModifierService>();
            Assert.IsTrue(day.HasWorldTag(WeatherDeck.WorldTag(expected)));
            eco.Restart();
            yield return null; yield return null;
            Assert.IsTrue(day.HasWorldTag(WeatherDeck.WorldTag(w.Current)), "weather survives a run restart");
        }

        [UnityTest]
        public IEnumerator Weather_ReplicatesHostToClient()
        {
            var w = SpawnWeather();
            yield return null;
            w.ForceWeather(WeatherKind.Storm);
            _client = MakeNm("NM Client", _port);
            yield return null;
            Assert.IsTrue(_client.StartClient(), "StartClient");
            WeatherDirector remote = null;
            yield return WaitFor(() =>
            {
                foreach (var d in Object.FindObjectsByType<WeatherDirector>(FindObjectsSortMode.None))
                    if (d.IsSpawned && !d.IsServer) remote = d;
                return remote != null;
            }, 15f, "client copy of the director");
            Debug.Log($"[WeatherTest] remote={remote.Current} hostNM={remote.NetworkManager == _host} clientNM={remote.NetworkManager == _client} hostCur={w.Current}");
            float t0 = Time.realtimeSinceStartup;
            yield return WaitFor(() => remote.Current == WeatherKind.Storm, 5f, "Storm on client");
            Debug.Log($"[WeatherTest] storm after {Time.realtimeSinceStartup - t0:F2}s");
            Assert.AreEqual(w.Seed, remote.Seed);
            WeatherKind seen = remote.Current;
            remote.OnWeatherChanged += (a, b) => seen = b;
            w.ForceWeather(WeatherKind.Fog);
            yield return WaitFor(() => remote.Current == WeatherKind.Fog, 5f, "Fog on client");
            Assert.AreEqual(WeatherKind.Fog, seen, "client event fired");
        }
    }
}
