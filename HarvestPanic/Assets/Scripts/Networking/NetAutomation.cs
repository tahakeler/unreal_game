using System;
using System.IO;
using System.Text;
using HarvestPanic.Core;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HarvestPanic.Networking
{
    /// <summary>
    /// Command-line driven session automation for headless multiplayer tests (inactive without -netHost/-netJoin).
    /// Host:   -netHost [-netExpectPlayers 2] [-netListen 127.0.0.1] [-netNoDiscovery]
    /// Client: -netJoin 127.0.0.1[:port]
    /// Common: -netName N, -netPort P, -netLobbyScene S, -netGameScene S, -netResult /abs/file.txt, -netTimeout 90
    /// Host starts the game once ExpectPlayers are in the roster; both write "ok=1" lines once the game scene is
    /// loaded and ExpectPlayers player objects are replicated, then quit with exit code 0 (1 on timeout/failure).
    /// </summary>
    [RequireComponent(typeof(SessionService))]
    public sealed class NetAutomation : MonoBehaviour
    {
        SessionService _session;
        bool _active, _host, _started, _done;
        int _expect;
        float _deadline, _quitAt = -1f;
        int _quitCode;
        string _resultPath;
        string _lastError;
        bool _hostLoadedAll;

        void Start()
        {
            _session = GetComponent<SessionService>();
            if (NetworkBootstrap.Instance != GetComponent<NetworkBootstrap>()) return;
            _host = SessionService.HasArg("-netHost");
            string join = SessionService.Arg("-netJoin");
            _active = _host || join != null;
            if (!_active) return;

            if (Application.isBatchMode) Application.targetFrameRate = 60;
            _expect = int.TryParse(SessionService.Arg("-netExpectPlayers"), out var e) ? e : 2;
            _deadline = Time.realtimeSinceStartup + (float.TryParse(SessionService.Arg("-netTimeout"), out var t) ? t : 90f);
            _resultPath = SessionService.Arg("-netResult");
            var cfg = _session.Config;
            cfg.lobbyScene = SessionService.Arg("-netLobbyScene") ?? cfg.lobbyScene;
            cfg.gameScene = SessionService.Arg("-netGameScene") ?? cfg.gameScene;
            _session.OnError += msg => { _lastError = msg; Write("error=" + msg); };
            _session.OnServerSceneLoaded += (scene, completed) =>
            {
                if (scene == cfg.gameScene && completed.Count >= _expect) _hostLoadedAll = true;
                Write($"serverSceneLoaded={scene} completed={completed.Count}");
            };
            Write($"role={(_host ? "host" : "client")} pid={System.Diagnostics.Process.GetCurrentProcess().Id}");
            if (_host) _session.HostLan("AutoTest", GameInfo.MaxPlayers);
            else _session.JoinLan(join);
        }

        void Update()
        {
            if (!_active) return;
            if (_quitAt > 0f)
            {
                if (Time.realtimeSinceStartup >= _quitAt) Quit(_quitCode);
                return;
            }
            var nm = _session.Manager;
            string scene = SceneManager.GetActiveScene().name;
            int replicatedPlayers = CountPlayerObjects(nm);

            if (_host && !_started && _session.Players.Count >= _expect && scene == _session.Config.lobbyScene && replicatedPlayers >= _expect)
            {
                _started = true;
                Write($"lobbyReady players={_session.Players.Count} replicated={replicatedPlayers}");
                _session.StartGame();
            }

            if (!_done && scene == _session.Config.gameScene && _session.Players.Count >= _expect && replicatedPlayers >= _expect && (!_host || _hostLoadedAll))
            {
                _done = true;
                Write($"ok=1 scene={scene} players={_session.Players.Count} replicated={replicatedPlayers} names={Names()}");
                // Host lingers so the client can observe its final state; client exits when the host leaves or after 3 s.
                ScheduleQuit(0, _host ? 4f : 3f);
            }

            if (_done && !_host && !_session.IsConnected) ScheduleQuit(0, 0.1f);
            if (!_done && _lastError != null && !_session.IsConnected && !_session.IsConnecting)
            {
                Write("ok=0 reason=" + _lastError);
                ScheduleQuit(1, 0.5f);
            }
            if (!_done && Time.realtimeSinceStartup > _deadline)
            {
                Write($"ok=0 reason=timeout scene={scene} players={_session.Players.Count} replicated={replicatedPlayers}");
                ScheduleQuit(1, 0.1f);
            }
        }

        string Names()
        {
            var sb = new StringBuilder();
            foreach (var p in _session.Players) sb.Append(sb.Length > 0 ? "," : "").Append(p.Name.Replace(' ', '_'));
            return sb.ToString();
        }

        /// <summary>Spawned player objects visible on this peer.</summary>
        public static int CountPlayerObjects(NetworkManager nm)
        {
            if (nm == null || nm.SpawnManager == null) return 0;
            int n = 0;
            foreach (var no in nm.SpawnManager.SpawnedObjectsList) if (no != null && no.IsPlayerObject) n++;
            return n;
        }

        void ScheduleQuit(int code, float delay)
        {
            if (_quitAt > 0f) return;
            _quitCode = code;
            _quitAt = Time.realtimeSinceStartup + delay;
        }

        void Quit(int code)
        {
            Write("exit=" + code);
            _session.Leave();
            Application.Quit(code);
        }

        void Write(string line)
        {
            string l = $"[{Time.realtimeSinceStartup:F2}] {line}";
            Debug.Log("[NetAutomation] " + l);
            if (string.IsNullOrEmpty(_resultPath)) return;
            try { File.AppendAllText(_resultPath, l + "\n"); } catch (Exception ex) { Debug.LogWarning("[NetAutomation] " + ex.Message); }
        }
    }
}
