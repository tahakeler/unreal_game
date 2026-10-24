using System.Collections;
using System.Collections.Generic;
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
    /// Regression for "Server Scene Handle (...) already exist! Happened during scene load of Lobby": a client approved
    /// while the host was still loading a scene got a Synchronize that already listed that scene AND the scene's Load
    /// event when the host finished, so it loaded the scene twice and never reached Muddlebury.
    /// In-process host (real SessionService, LAN) + a bare client NetworkManager on 127.0.0.1. The client records every
    /// scene NGO asks it to load (VerifySceneBeforeLoading) and declines, so it never replaces the host's scenes; each
    /// scene must be requested exactly once. The host's scene load is held open deterministically with
    /// AsyncOperation.allowSceneActivation = false.
    /// </summary>
    public class JoinDuringSceneLoadTests
    {
        SessionService _session;
        readonly List<string> _ngoErrors = new List<string>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            LogAssert.ignoreFailingMessages = true; // the declining client logs scene-validation warnings by design
            _ngoErrors.Clear();
            Application.logMessageReceived += CaptureErrors;
            yield return SceneManager.LoadSceneAsync(GameInfo.BootstrapScene, LoadSceneMode.Single);
            var cfg = NetworkingConfig.Load();
            for (int i = 0; i < 120 && SceneManager.GetActiveScene().name != cfg.menuScene; i++) yield return null;
            Assert.That(NetworkBootstrap.Instance, Is.Not.Null);
            _session = NetworkBootstrap.Instance.GetComponent<SessionService>();
            Assume.That(SessionService.IsSceneInBuild(_session.Config.lobbyScene), "Lobby scene must be in Build Settings");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Application.logMessageReceived -= CaptureErrors;
            foreach (var nm in Object.FindObjectsByType<NetworkManager>(FindObjectsSortMode.None))
                if (nm != null && nm.IsListening) nm.Shutdown();
            yield return null; yield return null;
            foreach (var nm in Object.FindObjectsByType<NetworkManager>(FindObjectsSortMode.None))
                if (nm != null) Object.Destroy(nm.gameObject);
            yield return null;
            LogAssert.ignoreFailingMessages = false;
        }

        void CaptureErrors(string message, string stack, LogType type)
        {
            if (message == null) return;
            if (message.Contains("already exist") || message.Contains("Failed to find") || type == LogType.Exception)
                _ngoErrors.Add(type + ": " + message);
        }

        /// <summary>Bare client that records (scene:mode) for every NGO load request and declines it.</summary>
        NetworkManager StartClient(string name, List<string> asked)
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
                BuildVersion = Application.version,
                PlayerName = name,
            }.ToBytes();
            nm.NetworkConfig = cfg;
            go.SetActive(true);
            Object.DontDestroyOnLoad(go);
            ushort port = host.NetworkConfig.NetworkTransport is UnityTransport t ? t.ConnectionData.Port : NetworkingConfig.Load().lanPort;
            utp.SetConnectionData("127.0.0.1", port);
            Assert.IsTrue(nm.StartClient(), "client " + name + " failed to start");
            nm.SceneManager.VerifySceneBeforeLoading = (_, scene, mode) => { asked.Add(scene + ":" + mode); return false; };
            return nm;
        }

        static IEnumerator WaitFor(System.Func<bool> cond, float seconds = 10f)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!cond() && Time.realtimeSinceStartup < end) yield return null;
        }

        static IEnumerator Wait(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end) yield return null;
        }

        static int Count(List<string> asked, string scene)
        {
            int n = 0;
            foreach (var a in asked) if (a.StartsWith(scene + ":")) n++;
            return n;
        }

        bool HasPlayerObject(ulong clientId) =>
            _session.Manager.ConnectedClients.TryGetValue(clientId, out var c) && c.PlayerObject != null;

        [UnityTest]
        public IEnumerator JoinWhileHostLoadsLobby_ClientSyncsLobbyOnce()
        {
            var host = _session.Manager;
            string lobby = _session.Config.lobbyScene;
            AsyncOperation pendingLoad = null;
            void HoldLobby(SceneEvent e)
            {
                if (pendingLoad != null || e.SceneEventType != SceneEventType.Load || e.ClientId != host.LocalClientId) return;
                if (e.SceneName != lobby || e.AsyncOperation == null) return;
                pendingLoad = e.AsyncOperation;
                pendingLoad.allowSceneActivation = false; // host stays "loading Lobby" until released below
            }
            void Hook() => host.SceneManager.OnSceneEvent += HoldLobby;
            host.OnServerStarted += Hook;
            _session.HostLan("JoinRace", GameInfo.MaxPlayers);
            host.OnServerStarted -= Hook;
            Assert.IsTrue(_session.IsHost, "host did not start");
            Assert.IsNotNull(pendingLoad, "host should have started loading " + lobby);

            int requestsWhileLoading = 0;
            var approve = host.ConnectionApprovalCallback;
            host.ConnectionApprovalCallback = (req, resp) =>
            {
                if (!pendingLoad.isDone) requestsWhileLoading++;
                approve(req, resp);
            };

            var asked = new List<string>();
            var client = StartClient("Cora", asked);
            yield return WaitFor(() => requestsWhileLoading > 0, 10f);
            Assert.AreEqual(1, requestsWhileLoading, "the join must arrive while the host is still loading " + lobby);
            yield return Wait(1f); // let the (unfixed) host approve + synchronise mid-load
            Assert.IsFalse(pendingLoad.isDone, "load must still be pending at this point");

            pendingLoad.allowSceneActivation = true;
            yield return WaitFor(() => SceneManager.GetActiveScene().name == lobby && client.IsConnectedClient
                && _session.Players.Count == 2 && HasPlayerObject(client.LocalClientId), 20f);
            yield return Wait(1f); // any duplicate Load event would arrive here

            Assert.AreEqual(lobby, SceneManager.GetActiveScene().name);
            Assert.AreEqual(1, Count(asked, lobby), $"client must be asked to load {lobby} exactly once; got [{string.Join(", ", asked)}]");
            Assert.IsTrue(client.IsConnectedClient, "client should finish connecting");
            Assert.AreEqual(2, _session.Players.Count, "host roster should contain the joiner");
            Assert.IsTrue(HasPlayerObject(client.LocalClientId), "joiner should be spawned in the lobby");
            Assert.AreEqual(0, _session.SceneGate.HeldCount, "no join left held");
            CollectionAssert.IsEmpty(_ngoErrors, "no NGO scene errors expected");
        }

        [UnityTest]
        public IEnumerator StartGameWhileJoinerSynchronises_SwitchWaitsForJoiner()
        {
            Assume.That(SessionService.IsSceneInBuild(_session.Config.gameScene), "game scene must be in Build Settings");
            var host = _session.Manager;
            string lobby = _session.Config.lobbyScene, game = _session.Config.gameScene;
            bool lobbyDone = false;
            var order = new List<string>();
            void Track(SceneEvent e)
            {
                if (e.SceneEventType == SceneEventType.LoadEventCompleted && e.SceneName == lobby) lobbyDone = true;
                if (e.SceneEventType == SceneEventType.SynchronizeComplete) order.Add("syncComplete:" + e.ClientId);
                if (e.SceneEventType == SceneEventType.Load && e.ClientId == host.LocalClientId) order.Add("load:" + e.SceneName);
            }
            void Hook() => host.SceneManager.OnSceneEvent += Track;
            host.OnServerStarted += Hook;
            _session.HostLan("SwitchRace", GameInfo.MaxPlayers);
            host.OnServerStarted -= Hook;
            yield return WaitFor(() => lobbyDone && SceneManager.GetActiveScene().name == lobby, 20f);
            Assert.AreEqual(lobby, SceneManager.GetActiveScene().name, "host should be in the lobby");

            // The host presses Start the instant the joiner's synchronisation is sent (joiner not yet synchronised).
            bool started = false;
            host.SceneManager.OnSynchronize += _ => { if (started) return; started = true; _session.StartGame(); };
            var asked = new List<string>();
            var client = StartClient("Lou", asked);
            yield return WaitFor(() => started, 10f);
            Assert.IsTrue(started, "joiner was never synchronised");
            yield return WaitFor(() => SceneManager.GetActiveScene().name == game, 60f);
            yield return Wait(1f);

            Assert.AreEqual(game, SceneManager.GetActiveScene().name, "the deferred switch must still happen");
            int sync = order.IndexOf("syncComplete:" + client.LocalClientId);
            int load = order.IndexOf("load:" + game);
            Assert.That(sync, Is.GreaterThanOrEqualTo(0), "joiner should finish synchronising; order [" + string.Join(", ", order) + "]");
            Assert.That(load, Is.GreaterThan(sync), "switch must start after the joiner synchronised; order [" + string.Join(", ", order) + "]");
            Assert.AreEqual(1, Count(asked, game), $"client must be asked to load {game} exactly once; got [{string.Join(", ", asked)}]");
            CollectionAssert.IsEmpty(_ngoErrors, "no NGO scene errors expected");
        }
    }
}
