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
    /// Robustness options (tools/net/two-process-test.sh):
    ///   host:   -netMaxPlayers N, -netFinalPlayers N (players required for host ok, default ExpectPlayers),
    ///           -netQuitFile /abs/path (stay after ok until the file exists, then leave like a host quitting)
    ///   client: -netWaitHost (after ok, stay until the host leaves; logs "hostLeft reason=..."),
    ///           -netRejoin (after ok, leave and rejoin once; logs "rejoin ok=1"),
    ///           -netExpectReject text (pass when the join is refused with a reason containing text),
    ///           -netFakeVersion V (dev builds: send build version V)
    ///   host load stress (dev builds): -netHostLoadDelay S holds the activation of every host NGO scene load
    ///           (Lobby, Muddlebury) for S seconds, so joins land mid-load (logs "hostLoadHeld"/"hostLoadReleased")
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
        bool _hostLoadedAll, _chatSent;
        int _chatReceived;
        int _final, _lastRoster = -1;
        string _quitFile, _expectReject, _joinAddr;
        bool _waitHost, _rejoin, _rejoining, _rejoined;
        float _hostLoadDelay, _heldLoadUntil;
        AsyncOperation _heldLoad;

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
            _final = int.TryParse(SessionService.Arg("-netFinalPlayers"), out var f) ? f : _expect;
            _quitFile = SessionService.Arg("-netQuitFile");
            _expectReject = SessionService.Arg("-netExpectReject");
            _waitHost = SessionService.HasArg("-netWaitHost");
            _rejoin = SessionService.HasArg("-netRejoin");
            _joinAddr = join;
            int maxPlayers = int.TryParse(SessionService.Arg("-netMaxPlayers"), out var m) ? m : GameInfo.MaxPlayers;
            var cfg = _session.Config;
            cfg.lobbyScene = SessionService.Arg("-netLobbyScene") ?? cfg.lobbyScene;
            cfg.gameScene = SessionService.Arg("-netGameScene") ?? cfg.gameScene;
            _session.OnError += msg =>
            {
                _lastError = msg;
                Write("error=" + msg);
                if (_expectReject != null && !_done && msg.IndexOf(_expectReject, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _done = true;
                    Write("ok=1 rejected reason=" + msg.Replace(' ', '_'));
                    ScheduleQuit(0, 0.5f);
                }
                if (_done && _waitHost && !_host) { Write("hostLeft reason=" + msg.Replace(' ', '_')); ScheduleQuit(0, 0.5f); }
            };
            _session.OnSessionChanged += () =>
            {
                if (_session.Players.Count == _lastRoster) return;
                _lastRoster = _session.Players.Count;
                Write($"roster n={_lastRoster}");
            };
            _session.OnServerSceneLoaded += (scene, completed) =>
            {
                if (scene == cfg.gameScene && completed.Count >= _expect) _hostLoadedAll = true;
                Write($"serverSceneLoaded={scene} completed={completed.Count}");
            };
            _session.OnChat += (from, text) => { _chatReceived++; Write($"chat from={from} text={text.Replace(' ', '_')}"); };
            Write($"role={(_host ? "host" : "client")} pid={System.Diagnostics.Process.GetCurrentProcess().Id}");
            if (_host && Debug.isDebugBuild && float.TryParse(SessionService.Arg("-netHostLoadDelay"), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var hold) && hold > 0f)
            {
                _hostLoadDelay = hold;
                var nm = _session.Manager;
                nm.OnServerStarted += () => nm.SceneManager.OnSceneEvent += HoldHostLoad; // before the Lobby load starts
            }
            if (_host) _session.HostLan("AutoTest", maxPlayers);
            else _session.JoinLan(join);
        }

        /// <summary>Load stress: keeps the host's own NGO scene load from activating for -netHostLoadDelay seconds.</summary>
        void HoldHostLoad(SceneEvent e)
        {
            var nm = _session.Manager;
            if (_heldLoad != null || e.SceneEventType != SceneEventType.Load || e.ClientId != nm.LocalClientId || e.AsyncOperation == null) return;
            _heldLoad = e.AsyncOperation;
            _heldLoad.allowSceneActivation = false;
            _heldLoadUntil = Time.realtimeSinceStartup + _hostLoadDelay;
            Write($"hostLoadHeld scene={e.SceneName} s={_hostLoadDelay:F1}");
        }

        void Update()
        {
            if (!_active) return;
            if (_heldLoad != null && Time.realtimeSinceStartup >= _heldLoadUntil)
            {
                _heldLoad.allowSceneActivation = true;
                _heldLoad = null;
                Write($"hostLoadReleased held={_session.SceneGate.HeldCount}");
            }
            if (_quitAt > 0f)
            {
                if (Time.realtimeSinceStartup >= _quitAt) Quit(_quitCode);
                return;
            }
            var nm = _session.Manager;
            string scene = SceneManager.GetActiveScene().name;
            int replicatedPlayers = CountPlayerObjects(nm);

            if (!_host && !_chatSent && _session.IsConnected && _session.Players.Count >= _expect)
            {
                _chatSent = true;
                _session.SendChat("  hello <b>farm</b>\u200B  ");
            }

            if (_host && !_started && _session.Players.Count >= _expect && scene == _session.Config.lobbyScene && replicatedPlayers >= _expect)
            {
                _started = true;
                Write($"lobbyReady players={_session.Players.Count} replicated={replicatedPlayers}");
                _session.StartGame();
            }

            int need = _host ? _final : _expect;
            bool inGame = scene == _session.Config.gameScene && _session.Players.Count >= need && replicatedPlayers >= need;
            if (!_done && _expectReject == null && inGame && (!_host || _hostLoadedAll))
            {
                _done = true;
                Write($"ok=1 scene={scene} players={_session.Players.Count} replicated={replicatedPlayers} playerStartMarkers={LevelMarker.FindAll(MarkerRole.PlayerStart).Count} chatReceived={_chatReceived} names={Names()}");
                if (_rejoin && !_host)
                {
                    _rejoining = true;
                    Write("rejoin leaving");
                    _session.Leave();
                }
                // Host lingers so the client can observe its final state; client exits when the host leaves or after 3 s.
                else if (!(_host && _quitFile != null) && !(_waitHost && !_host)) ScheduleQuit(0, _host ? 4f : 3f);
            }

            if (_rejoining)
            {
                if (!_rejoined && !_session.IsConnected && !_session.IsConnecting && !nm.IsListening && !nm.ShutdownInProgress
                    && scene == _session.Config.menuScene)
                {
                    _rejoined = true;
                    Write("rejoin joining");
                    _session.JoinLan(_joinAddr);
                }
                else if (_rejoined && inGame)
                {
                    _rejoining = false;
                    Write($"rejoin ok=1 players={_session.Players.Count} replicated={replicatedPlayers}");
                    if (!_waitHost) ScheduleQuit(0, 3f);
                }
                if (Time.realtimeSinceStartup > _deadline) { Write("rejoin ok=0 reason=timeout"); ScheduleQuit(1, 0.1f); }
                return;
            }

            if (_done && _host && _quitFile != null && File.Exists(_quitFile)) { Write("hostQuitting"); ScheduleQuit(0, 0.1f); }
            if (_done && !_host && !_session.IsConnected && !_session.IsConnecting) ScheduleQuit(0, _waitHost ? 0.5f : 0.1f);
            if (!_done && _lastError != null && !_session.IsConnected && !_session.IsConnecting)
            {
                Write("ok=0 reason=" + _lastError);
                ScheduleQuit(1, 0.5f);
            }
            if (Time.realtimeSinceStartup > _deadline && (!_done || _quitFile != null || _waitHost))
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
