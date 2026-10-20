using System.Collections;
using HarvestPanic.AI.Night;
using HarvestPanic.Core;
using HarvestPanic.Economy;
using HarvestPanic.Weather;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.TestTools;

namespace HarvestPanic.Tests.PlayMode.Night
{
    /// <summary>Night starts after Workday (Dusk), Mr. Stitches obeys the weeping-angel rule, night state replicates.</summary>
    public class NightPlayModeTests
    {
        sealed class Bot : INightViewerSource
        {
            public NightViewer View; public bool On = true;
            public bool TryGetView(out NightViewer v) { v = View; return On; }
        }

        NetworkManager _host, _client;
        ushort _port;
        GameObject _ground;

        static NetworkManager MakeNm(string name, ushort port)
        {
            var go = new GameObject(name);
            var t = go.AddComponent<UnityTransport>();
            t.SetConnectionData("127.0.0.1", port);
            var nm = go.AddComponent<NetworkManager>();
            nm.NetworkConfig = new NetworkConfig { NetworkTransport = t, EnableSceneManagement = false };
            // every team list (other bootstrappers, e.g. audio relay, may spawn while the host runs)
            foreach (var l in Resources.LoadAll<NetworkPrefabsList>("NetworkPrefabs")) nm.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(l);
            return nm;
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Services.Clear();
            WeatherBootstrapper.Enabled = false;
            NightBootstrapper.Enabled = false;
            NightObservers.ClearExtra();
            EconomyDevCvars.DayScale = 0.02f;
            _ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            _ground.transform.localScale = Vector3.one * 30f;
            _port = (ushort)Random.Range(21000, 59000);
            _host = MakeNm("NM Host", _port);
            yield return null;
            Assert.IsTrue(_host.StartHost(), "StartHost");
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            NightObservers.ClearExtra();
            if (_client != null) { _client.Shutdown(); yield return null; Object.Destroy(_client.gameObject); }
            if (NightDirector.Instance != null && NightDirector.Instance.IsSpawned) { NightDirector.Instance.ForceNight(false); yield return null; NightDirector.Instance.NetworkObject.Despawn(true); }
            if (EconomyDirector.Instance != null && EconomyDirector.Instance.IsSpawned) EconomyDirector.Instance.NetworkObject.Despawn(true);
            if (_host != null) _host.Shutdown();
            yield return null;
            foreach (var hud in Object.FindObjectsByType<EconomyHudMarker>(FindObjectsSortMode.None)) Object.Destroy(hud.gameObject);
            foreach (var d in Object.FindObjectsByType<NightDirector>(FindObjectsSortMode.None)) Object.Destroy(d.gameObject);
            foreach (var s in Object.FindObjectsByType<MrStitches>(FindObjectsSortMode.None)) Object.Destroy(s.gameObject);
            foreach (var f in Object.FindObjectsByType<NightFox>(FindObjectsSortMode.None)) Object.Destroy(f.gameObject);
            if (_host != null) Object.Destroy(_host.gameObject);
            Object.Destroy(_ground);
            EconomyDevCvars.DayScale = 1f;
            WeatherBootstrapper.Enabled = true;
            NightBootstrapper.Enabled = true;
            yield return null;
        }

        static IEnumerator WaitFor(System.Func<bool> cond, float timeout, string what)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (!cond()) { if (Time.realtimeSinceStartup > end) Assert.Fail("Timed out waiting for " + what); yield return null; }
        }

        static NightDirector SpawnNight()
        {
            var d = Object.Instantiate(NightBootstrapper.FindDirectorPrefab()).GetComponent<NightDirector>();
            d.NetworkObject.Spawn(true);
            return d;
        }

        [UnityTest]
        public IEnumerator Night_StartsAfterWorkday_AndEndsAtPayout()
        {
            var eco = Object.Instantiate(EconomyBootstrapper.FindDirectorPrefab()).GetComponent<EconomyDirector>();
            eco.NetworkObject.Spawn(true);
            var night = SpawnNight();
            yield return WaitFor(() => eco.Phase == DayPhase.Workday, 5f, "Workday");
            yield return null;
            Assert.IsFalse(night.IsNight, "no night during Workday");
            eco.DebugSkipPhase(); // -> Dusk
            yield return WaitFor(() => eco.Phase == DayPhase.Dusk, 3f, "Dusk");
            yield return WaitFor(() => night.IsNight, 2f, "night begins");
            Assert.AreEqual(10, night.Budget, "day 0, nobody outside -> B = 10");
            yield return WaitFor(() => night.Stitches.Count == 1, 3f, "Mr. Stitches spawned");
            Assert.LessOrEqual(night.Spent, night.Budget);
            while (eco.Phase == DayPhase.Dusk) { eco.DebugSkipPhase(); yield return null; }
            yield return null; yield return null;
            Assert.IsFalse(night.IsNight);
            Assert.AreEqual(0, night.Stitches.Count, "creatures leave at dawn");
        }

        IEnumerator StitchesAt(Vector3 p, System.Action<NightDirector, MrStitches> got)
        {
            var night = SpawnNight();
            night.ForceNight(true);
            yield return WaitFor(() => night.Stitches.Count == 1, 3f, "Stitches");
            var s = night.Stitches[0];
            s.GetComponent<Unity.Netcode.Components.NetworkTransform>().Teleport(p, Quaternion.identity, Vector3.one);
            yield return null;
            got(night, s);
        }

        [UnityTest]
        public IEnumerator Stitches_FrozenWhileObserved_MovesWhenUnobserved()
        {
            NightDirector night = null; MrStitches s = null;
            yield return StitchesAt(new Vector3(0, 0, 10f), (d, x) => { night = d; s = x; });
            var bot = new Bot { View = new NightViewer { Eye = new Vector3(0, 1.6f, -5f), Rotation = Quaternion.identity, VerticalFov = 70f, Aspect = 16f / 9f } };
            NightObservers.Register(bot);
            s.DebugTarget = new Vector3(0, 0, 30f);
            yield return null;
            Vector3 start = s.transform.position;
            float t0 = Time.time; while (Time.time - t0 < 1f) yield return null;
            Assert.IsTrue(s.ObservedNow);
            Assert.AreEqual(MrStitches.Mode.Watched, s.CurrentMode);
            Assert.Less((s.transform.position - start).magnitude, 1e-3f, "never moves while observed");

            // four bots all looking elsewhere -> he moves
            bot.View.Rotation = Quaternion.LookRotation(Vector3.back);
            for (int i = 0; i < 3; i++) NightObservers.Register(new Bot { View = new NightViewer { Eye = new Vector3(i * 5f, 1.6f, -5f), Rotation = Quaternion.LookRotation(Vector3.left), VerticalFov = 70f, Aspect = 1.7f } });
            t0 = Time.time; while (Time.time - t0 < 0.5f) yield return null;
            Assert.IsFalse(s.ObservedNow);
            Assert.Greater(s.transform.position.z, start.z + 1f, "moves toward the target when unseen");
        }

        [UnityTest]
        public IEnumerator Stitches_MovesWhenViewBlockedByWall_AndStopsAtSafeZone()
        {
            NightDirector night = null; MrStitches s = null;
            yield return StitchesAt(new Vector3(0, 0, 10f), (d, x) => { night = d; s = x; });
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0, 2f, 2f); wall.transform.localScale = new Vector3(20f, 6f, 0.5f);
            NightObservers.Register(new Bot { View = new NightViewer { Eye = new Vector3(0, 1.6f, -5f), Rotation = Quaternion.identity, VerticalFov = 70f, Aspect = 1.7f } });
            night.AddSafeZone(new Vector3(0, 0, 25f), 5f);
            s.DebugTarget = new Vector3(0, 0, 25f);
            float t0 = Time.time; while (Time.time - t0 < 3f) yield return null;
            Object.Destroy(wall);
            Assert.IsFalse(s.ObservedNow, "no line of sight through the wall");
            Assert.Greater(s.transform.position.z, 15f, "moved");
            Assert.Less(s.transform.position.z, 20.1f, "stopped at the lantern/safe zone edge");
        }

        [UnityTest]
        public IEnumerator Night_ReplicatesToClient()
        {
            var night = SpawnNight();
            night.ForceNight(true);
            yield return WaitFor(() => night.Stitches.Count == 1, 3f, "Stitches");
            var s = night.Stitches[0];
            _client = MakeNm("NM Client", _port);
            yield return null;
            Assert.IsTrue(_client.StartClient());
            yield return WaitFor(() => _client.IsConnectedClient, 10f, "client connect");
            MrStitches remote = null; NightDirector remoteDir = null;
            yield return WaitFor(() =>
            {
                foreach (var x in Object.FindObjectsByType<MrStitches>(FindObjectsSortMode.None)) if (x != s && x.IsSpawned && !x.IsServer) remote = x;
                foreach (var x in Object.FindObjectsByType<NightDirector>(FindObjectsSortMode.None)) if (x != night && x.IsSpawned && !x.IsServer) remoteDir = x;
                return remote != null && remoteDir != null;
            }, 10f, "client copies");
            yield return WaitFor(() => remoteDir.IsNight && remoteDir.Budget == night.Budget, 3f, "night state replicated");
            Vector3 goal = s.transform.position + new Vector3(8f, 0f, 0f);
            s.DebugTarget = goal; // unobserved (no viewers): walks 8 m and stops
            yield return WaitFor(() => (s.transform.position - goal).magnitude < 0.2f, 5f, "server reached goal");
            yield return WaitFor(() => (remote.transform.position - s.transform.position).magnitude < 0.3f, 5f, "position replicated");
            Assert.IsNotNull(remote.transform.Find("StitchesVisual"), "visual built on client");
        }
    }
}
