using System.Collections;
using System.Reflection;
using HarvestPanic.Core;
using HarvestPanic.Networking;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HarvestPanic.Tests.PlayMode
{
    /// <summary>
    /// In-process host (real SessionService, LAN, random port) + bare client NetworkManagers on 127.0.0.1.
    /// Clients refuse NGO scene loads (VerifySceneBeforeLoading) so they don't replace the host's scene.
    /// Covers: max players enforced with a reason, version mismatch rejected with a reason, disconnect + rejoin.
    /// </summary>
    public class SessionRobustnessTests
    {
        SessionService _session;
        ushort _savedPort;
        ushort _port;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            LogAssert.ignoreFailingMessages = true; // client-side player/scene warnings are irrelevant here
            var cfg = NetworkingConfig.Load();
            _savedPort = cfg.lanPort;
            _port = (ushort)Random.Range(20000, 40000);
            cfg.lanPort = _port;
            yield return SceneManager.LoadSceneAsync(GameInfo.BootstrapScene, LoadSceneMode.Single);
            for (int i = 0; i < 120 && SceneManager.GetActiveScene().name != cfg.menuScene; i++) yield return null;
            Assert.That(NetworkBootstrap.Instance, Is.Not.Null);
            _session = NetworkBootstrap.Instance.GetComponent<SessionService>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var nm in Object.FindObjectsByType<NetworkManager>(FindObjectsSortMode.None))
                if (nm != null && nm.IsListening) nm.Shutdown();
            yield return null; yield return null;
            foreach (var nm in Object.FindObjectsByType<NetworkManager>(FindObjectsSortMode.None))
                if (nm != null) Object.Destroy(nm.gameObject);
            yield return null;
            NetworkingConfig.Load().lanPort = _savedPort;
            LogAssert.ignoreFailingMessages = false;
        }

        NetworkManager StartClient(string name, string build = null)
        {
            var host = _session.Manager;
            var go = new GameObject("TestClient " + name);
            go.SetActive(false);
            var utp = go.AddComponent<UnityTransport>();
            var nm = go.AddComponent<NetworkManager>();
            var cfg = new NetworkConfig();
            foreach (var f in typeof(NetworkConfig).GetFields(BindingFlags.Public | BindingFlags.Instance))
                f.SetValue(cfg, f.GetValue(host.NetworkConfig));
            cfg.NetworkTransport = utp;
            cfg.ConnectionData = new ConnectionPayload
            {
                Protocol = NetworkingConfig.Load().protocolVersion,
                BuildVersion = build ?? Application.version,
                PlayerName = name,
            }.ToBytes();
            nm.NetworkConfig = cfg;
            go.SetActive(true);
            Object.DontDestroyOnLoad(go);
            utp.SetConnectionData("127.0.0.1", HostPort());
            Assert.IsTrue(nm.StartClient(), "client " + name + " failed to start");
            nm.SceneManager.VerifySceneBeforeLoading = (_, __, ___) => false;
            return nm;
        }

        /// <summary>
        /// Port the host really listens on. SessionService prefers a <c>-netPort</c> command-line argument over
        /// <c>NetworkingConfig.lanPort</c>, and parallel batchmode test runs pass one, so read it from the host transport.
        /// </summary>
        ushort HostPort() => _session.Manager.NetworkConfig.NetworkTransport is UnityTransport t ? t.ConnectionData.Port : _port;

        static IEnumerator WaitFor(System.Func<bool> cond, float seconds = 10f)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!cond() && Time.realtimeSinceStartup < end) yield return null;
        }

        IEnumerator Host(int maxPlayers)
        {
            _session.HostInPlaceForEditor(maxPlayers);
            yield return WaitFor(() => _session.IsHost);
            Assert.IsTrue(_session.IsHost, "host did not start on port " + _port);
        }

        [UnityTest]
        public IEnumerator MaxPlayers_ExtraClientRejectedWithReason()
        {
            yield return Host(2);
            var a = StartClient("Cora");
            yield return WaitFor(() => a.IsConnectedClient && _session.Players.Count == 2);
            Assert.AreEqual(2, _session.Players.Count, "first client should be in the roster");

            var b = StartClient("Lou");
            yield return WaitFor(() => !b.IsListening || !string.IsNullOrEmpty(b.DisconnectReason));
            yield return WaitFor(() => !b.IsListening);
            Assert.IsFalse(b.IsConnectedClient);
            Assert.AreEqual(ConnectionApproval.FullReason(2), b.DisconnectReason);
            Assert.AreEqual(2, _session.Players.Count);
        }

        [UnityTest]
        public IEnumerator FourPlayerCap_FifthRejected()
        {
            yield return Host(GameInfo.MaxPlayers);
            var c = new NetworkManager[3];
            for (int i = 0; i < 3; i++) c[i] = StartClient("P" + i);
            yield return WaitFor(() => _session.Players.Count == 4, 15f);
            Assert.AreEqual(4, _session.Players.Count);
            var e = StartClient("Fifth");
            yield return WaitFor(() => !e.IsListening, 10f);
            Assert.AreEqual(ConnectionApproval.FullReason(4), e.DisconnectReason);
        }

        [UnityTest]
        public IEnumerator VersionMismatch_RejectedWithReadableReason()
        {
            yield return Host(GameInfo.MaxPlayers);
            var c = StartClient("Old", "0.0.1-old");
            yield return WaitFor(() => !c.IsListening);
            StringAssert.Contains("different version", c.DisconnectReason);
            Assert.AreEqual(1, _session.Players.Count);
        }

        [UnityTest]
        public IEnumerator Disconnect_ThenRejoin_RosterRecovers()
        {
            yield return Host(2);
            var a = StartClient("Cora");
            yield return WaitFor(() => _session.Players.Count == 2);
            Assert.AreEqual(2, _session.Players.Count);

            a.Shutdown();
            yield return WaitFor(() => _session.Players.Count == 1);
            Assert.AreEqual(1, _session.Players.Count, "host roster should drop the leaver");
            yield return null;
            Object.Destroy(a.gameObject);
            yield return null;

            var again = StartClient("Cora");
            yield return WaitFor(() => again.IsConnectedClient && _session.Players.Count == 2);
            Assert.AreEqual(2, _session.Players.Count, "rejoin should be accepted into the freed slot");
            bool hasCora = false;
            foreach (var p in _session.Players) if (p.Name == "Cora") hasCora = true;
            Assert.IsTrue(hasCora, "rejoiner keeps their name (no stale duplicate)");
        }
    }
}
