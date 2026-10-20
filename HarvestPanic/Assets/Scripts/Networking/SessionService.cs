using System;
using System.Collections.Generic;
using System.Net;
using HarvestPanic.Core;
using Netcode.Transports.Facepunch;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HarvestPanic.Networking
{
    /// <summary>
    /// Implements <see cref="ISessionService"/> + <see cref="ISessionPlatform"/> on the persistent NetworkManager.
    /// Listen-server topology (host + up to 3 clients). LAN = Unity Transport, Steam = FacepunchTransport (relay),
    /// automatic LAN fallback when Steam is not running. Roster (names/ready) syncs via named messages:
    /// "HP.Roster" server->clients (full list on change), "HP.Ready" client->server.
    /// Scene flow through NGO SceneManager: host start -> Lobby, StartGame -> Muddlebury, end -> menu (local load).
    /// </summary>
    [DefaultExecutionOrder(-900)]
    [RequireComponent(typeof(NetworkManager), typeof(NetworkBootstrap), typeof(PlayerSpawnService))]
    public sealed class SessionService : MonoBehaviour, ISessionService, ISessionPlatform, ISessionSocial
    {
        public const string RosterMessage = "HP.Roster";
        public const string ReadyMessage = "HP.Ready";
        public const string ChatInMessage = "HP.ChatIn";   // client -> server: [byte ver][string text]
        public const string ChatOutMessage = "HP.ChatOut"; // server -> clients: [byte ver][u64 sender][string text]
        public const string KickReason = "Sent home by the host";
        public const string HostLeftReason = "The host went to bed. The run was saved at the start of the day.";
        public const string ConnectionLostReason = "Lost connection to the host.";
        public const string NamePrefsKey = "hp.playerName";

        NetworkManager _nm;
        UnityTransport _utp;
        FacepunchTransport _fp;
        PlayerSpawnService _spawner;
        NetworkBootstrap _bootstrap;
        NetworkingConfig _cfg;
        readonly SessionRoster _roster = new SessionRoster();
        readonly ChatRules _chat = new ChatRules();
        readonly Dictionary<ulong, string> _pendingNames = new Dictionary<ulong, string>();
        LanDiscovery _discovery;
        SteamPlatform _steam;

        SessionTransport _transport;
        bool _connecting, _wasConnected, _leaving, _pendingMenu, _steamReinit, _stayInScene;
        int _maxPlayers = GameInfo.MaxPlayers;
        string _lobbyName = "Farm";
        string _joinTarget;
        string _capturedReason;
        string _localNameOverride;

        // ---------------- ISessionService ----------------
        public bool IsConnected => _nm != null && _nm.IsListening && (_nm.IsServer || _nm.IsConnectedClient);
        public bool IsHost => _nm != null && _nm.IsHost;
        public ulong LocalClientId => _nm != null ? _nm.LocalClientId : PlayerIds.None;
        public IReadOnlyList<PlayerInfo> Players => _roster.Players;
        public event Action OnSessionChanged;
        public event Action<string> OnError;

        // ---------------- ISessionPlatform ----------------
        public SessionTransport ActiveTransport => _transport;
        public bool IsConnecting => _connecting;
        public bool IsSteamAvailable => _steam != null && _steam.IsAvailable;
        public string LastEndReason { get; private set; }
        public IReadOnlyList<DiscoveredSession> LanSessions => _discovery != null ? _discovery.Sessions : Array.Empty<DiscoveredSession>();
        public event Action OnLanSessionsChanged;

        // ---------------- Networking-only API (tests, automation) ----------------
        /// <summary>Runtime copy of the config (safe to modify, e.g. scene names in tests).</summary>
        public NetworkingConfig Config => _cfg;
        /// <summary>Server: refuse joins while true (e.g. payout cutscene).</summary>
        public bool JoinLocked { get; set; }
        /// <summary>Server: raised when an NGO scene load finished (scene, clients that completed).</summary>
        public event Action<string, IReadOnlyList<ulong>> OnServerSceneLoaded;
        /// <summary>Raised on every peer when the active scene changes during a session.</summary>
        public event Action<string> OnLocalSceneLoaded;
        public NetworkManager Manager => _nm;
        public PlayerSpawnService Spawner => _spawner;
        /// <summary>Steam lobby id while hosting/joined through Steam (0 otherwise).</summary>
        public ulong SteamLobbyId => _steam?.CurrentLobbyId ?? 0;

        void Awake()
        {
            _nm = GetComponent<NetworkManager>();
            _utp = GetComponent<UnityTransport>();
            _fp = GetComponent<FacepunchTransport>();
            _spawner = GetComponent<PlayerSpawnService>();
            _bootstrap = GetComponent<NetworkBootstrap>();
            _cfg = Instantiate(NetworkingConfig.Load());
            _localNameOverride = Arg("-netName");

            _discovery = new LanDiscovery(_cfg.discoveryPort) { Interval = _cfg.discoveryInterval, Expiry = _cfg.discoveryExpiry };
            _discovery.OnChanged += () => OnLanSessionsChanged?.Invoke();

            bool steamAllowed = _fp != null && !HasArg("-nosteam") && (!Application.isBatchMode || _cfg.steamInBatchmode || HasArg("-steam"));
            _steam = new SteamPlatform(_cfg.steamAppId, steamAllowed);
            _steam.OnJoinRequested += OnSteamJoinRequested;

            _nm.ConnectionApprovalCallback = Approve;
            _nm.OnConnectionEvent += HandleConnectionEvent;
            _nm.OnClientDisconnectCallback += HandleClientDisconnect;
            _nm.OnClientStopped += HandleClientStopped;
            _nm.OnServerStopped += HandleServerStopped;
            _nm.OnTransportFailure += () => NetLog.Error("transport-fail", "Transport failure; session will end.");
            SceneManager.activeSceneChanged += (_, s) => { if (_nm.IsListening) OnLocalSceneLoaded?.Invoke(s.name); };
        }

        void OnEnable()
        {
            if (NetworkBootstrap.Instance != null && NetworkBootstrap.Instance != _bootstrap) return; // duplicate being destroyed
            Services.Register<ISessionService>(this);
            Services.Register<ISessionPlatform>(this);
            Services.Register<ISessionSocial>(this);
        }

        void OnDisable()
        {
            Services.Unregister<ISessionService>(this);
            Services.Unregister<ISessionPlatform>(this);
            Services.Unregister<ISessionSocial>(this);
        }

        void Start()
        {
            if (NetworkBootstrap.Instance != _bootstrap) return;
            if (_steam.EnsureInit(true) && SteamPlatform.TryGetLaunchLobby(out ulong lobby))
                JoinSteam(lobby); // launched from a Steam invite: skip the title screen
        }

        void OnDestroy()
        {
            _discovery?.Dispose();
            _steam?.LeaveLobby();
        }

        void Update()
        {
            _discovery.Tick(Time.realtimeSinceStartup);
            _steam.Tick(_nm.IsListening && _transport == SessionTransport.Steam);
            if (_nm.IsListening || _nm.ShutdownInProgress) return;
            if (_steamReinit) { _steamReinit = false; _steam.EnsureInit(true); }
            if (_pendingMenu) { _pendingMenu = false; GoToMenu(); }
        }

        // ---------------- Host / Join ----------------
        public void StartHost(string lobbyName, int maxPlayers)
        {
            if (_cfg.preferSteam && _steam.EnsureInit()) HostSteam(lobbyName, maxPlayers, true);
            else HostLan(lobbyName, maxPlayers);
        }

        public void Join(string addressOrCode)
        {
            string s = (addressOrCode ?? "").Trim();
            if (s.Length >= 15 && ulong.TryParse(s, out ulong steamId)) JoinSteam(steamId);
            else JoinLan(s);
        }

        public void HostLan(string lobbyName, int maxPlayers)
        {
            if (!CanStart()) return;
            PrepareStart(lobbyName, maxPlayers);
            _nm.NetworkConfig.NetworkTransport = _utp;
            string listen = Arg("-netListen") ?? _cfg.lanListenAddress;
            _utp.SetConnectionData("127.0.0.1", Port(), listen);
            if (!_nm.StartHost()) { Fail($"Couldn't start hosting on port {Port()}. Is another game using it?"); return; }
            _transport = SessionTransport.Lan;
            AfterHostStarted();
            if (!HasArg("-netNoDiscovery")) _discovery.StartResponding(BuildDiscoveryReply);
        }

        public void JoinLan(string address)
        {
            if (!CanStart()) return;
            if (!TryParseEndpoint(address, Port(), out string ip, out ushort port))
            {
                Fail($"\"{address}\" isn't a valid address. Use an IP like 192.168.1.20 or 192.168.1.20:7777.");
                return;
            }
            PrepareStart(null, GameInfo.MaxPlayers);
            _nm.NetworkConfig.NetworkTransport = _utp;
            _utp.SetConnectionData(ip, port);
            _joinTarget = $"{ip}:{port}";
            if (!_nm.StartClient()) { Fail($"Couldn't start connecting to {_joinTarget}."); return; }
            _transport = SessionTransport.Lan;
            AfterClientStarted();
        }

        public async void HostSteam(string lobbyName, int maxPlayers, bool friendsOnly)
        {
            if (!_steam.EnsureInit(true)) { NetLog.Info("Steam not running: hosting on LAN instead."); HostLan(lobbyName, maxPlayers); return; }
            if (!CanStart()) return;
            PrepareStart(lobbyName, maxPlayers);
            _nm.NetworkConfig.NetworkTransport = _fp;
            if (!_nm.StartHost()) { Fail("Couldn't start hosting through Steam."); return; }
            _transport = SessionTransport.Steam;
            AfterHostStarted();
            // No LAN discovery for Steam hosts: LAN peers cannot reach the Steam relay socket.
            ulong? lobby = null;
            try { lobby = await _steam.CreateLobbyAsync(_lobbyName, _maxPlayers, friendsOnly, Application.version, _cfg.protocolVersion); }
            catch (Exception e) { NetLog.Warn("steam-lobby", "CreateLobby threw: " + e.Message); }
            if (lobby == null && _nm.IsListening) RaiseError("Couldn't create the Steam lobby. You are hosting, but friends can't see you on Steam.");
            OnSessionChanged?.Invoke();
        }

        public async void JoinSteam(ulong lobbyOrHostSteamId)
        {
            if (_nm.IsListening)
            {
                // Invite accepted while already in a session: the frontend should confirm first; we leave and join.
                Leave();
                _pendingMenu = false;
                while (_nm.IsListening || _nm.ShutdownInProgress) await System.Threading.Tasks.Task.Yield();
            }
            if (!_steam.EnsureInit(true)) { Fail("Steam isn't running. Start Steam, or join with a LAN address."); return; }
            if (!CanStart()) return;
            ulong host = 0;
            _connecting = true;
            OnSessionChanged?.Invoke();
            if (SteamPlatform.IsLobbyId(lobbyOrHostSteamId))
            {
                var r = await _steam.JoinLobbyAsync(lobbyOrHostSteamId);
                if (!r.Ok) { _connecting = false; Fail(r.Error); return; }
                if (r.Protocol != 0 && (r.Protocol != _cfg.protocolVersion || r.Version != Application.version))
                {
                    _steam.LeaveLobby();
                    _connecting = false;
                    Fail(ConnectionApproval.VersionReason(Application.version, _cfg.protocolVersion, r.Version, r.Protocol));
                    return;
                }
                host = r.HostSteamId;
            }
            else if (SteamPlatform.IsUserId(lobbyOrHostSteamId)) host = lobbyOrHostSteamId;
            else { _connecting = false; Fail("That doesn't look like a Steam lobby or player id."); return; }

            PrepareStart(null, GameInfo.MaxPlayers);
            _nm.NetworkConfig.NetworkTransport = _fp;
            _fp.targetSteamId = host;
            _joinTarget = "Steam " + host;
            if (!_nm.StartClient()) { _steam.LeaveLobby(); Fail("Couldn't connect through Steam."); return; }
            _transport = SessionTransport.Steam;
            AfterClientStarted();
        }

        public bool OpenInviteOverlay() => _transport == SessionTransport.Steam && _steam.OpenInviteOverlay();

        void OnSteamJoinRequested(ulong lobbyId)
        {
            if (lobbyId == _steam.CurrentLobbyId) return;
            JoinSteam(lobbyId);
        }

#if UNITY_EDITOR
        /// <summary>Editor: playing a gameplay scene directly hosts a solo LAN session in that scene.</summary>
        public void HostInPlaceForEditor()
        {
            _stayInScene = true;
            HostLan("Editor", GameInfo.MaxPlayers);
            _stayInScene = false;
        }
#endif

        bool CanStart()
        {
            if (_nm.IsListening || _nm.ShutdownInProgress || _connecting && _nm.IsListening)
            {
                RaiseError("Already in a session. Leave it first.");
                return false;
            }
            return true;
        }

        void PrepareStart(string lobbyName, int maxPlayers)
        {
            _bootstrap.RegisterPrefabLists();
            _roster.Clear();
            _pendingNames.Clear();
            _capturedReason = null;
            _wasConnected = false;
            _leaving = false;
            _pendingMenu = false;
            _maxPlayers = Mathf.Clamp(maxPlayers, GameInfo.MinPlayers, GameInfo.MaxPlayers);
            _lobbyName = PlayerNameSanitizer.Sanitize(string.IsNullOrWhiteSpace(lobbyName) ? LocalPlayerName + "'s farm" : lobbyName, 32);
            var c = _nm.NetworkConfig;
            c.TickRate = _cfg.tickRate;
            c.ConnectionApproval = true;
            c.EnableSceneManagement = true;
            c.ConnectionData = new ConnectionPayload { Protocol = _cfg.protocolVersion, BuildVersion = Application.version, PlayerName = LocalPlayerName }.ToBytes();
            _utp.ConnectTimeoutMS = _cfg.connectTimeoutMs;
            _utp.MaxConnectAttempts = _cfg.maxConnectAttempts;
            _utp.DisconnectTimeoutMS = _cfg.disconnectTimeoutMs;
        }

        void AfterHostStarted()
        {
            _connecting = false;
            _wasConnected = true;
            RegisterMessages();
            HookScenes();
            _spawner.BindServer(_roster.SlotOf, _cfg.menuScene);
            if (!_roster.Contains(_nm.LocalClientId)) _roster.Add(_nm.LocalClientId, TakePending(_nm.LocalClientId) ?? PlayerNameSanitizer.Sanitize(LocalPlayerName), true);
            NetLog.Info($"Hosting '{_lobbyName}' ({_transport}) max {_maxPlayers}.");
            if (!_stayInScene && IsSceneInBuild(_cfg.lobbyScene) && SceneManager.GetActiveScene().name != _cfg.lobbyScene)
                LoadNetworkScene(_cfg.lobbyScene);
            else
            {
                if (!_stayInScene) NetLog.Warn("no-lobby", $"Lobby scene '{_cfg.lobbyScene}' is not in Build Settings; staying in the current scene.");
                _spawner.SpawnMissing();
            }
            OnSessionChanged?.Invoke();
        }

        void AfterClientStarted()
        {
            _connecting = true;
            RegisterMessages();
            HookScenes();
            NetLog.Info($"Joining {_joinTarget} via {_transport}.");
            OnSessionChanged?.Invoke();
        }

        NetworkSceneManager _hookedScenes;

        void HookScenes()
        {
            if (_hookedScenes == _nm.SceneManager) return;
            _hookedScenes = _nm.SceneManager;
            _nm.SceneManager.OnLoadEventCompleted += (scene, mode, completed, timedOut) =>
            {
                if (_nm.IsServer) OnServerSceneLoaded?.Invoke(scene, completed);
                if (_nm.IsServer) BroadcastRoster(); // discovery "in progress" + late UI refresh
            };
        }

        void LoadNetworkScene(string scene)
        {
            var status = _nm.SceneManager.LoadScene(scene, LoadSceneMode.Single);
            if (status != SceneEventProgressStatus.Started)
                NetLog.Warn("scene-load" + scene, $"NGO refused to load '{scene}': {status}.");
        }

        // ---------------- Lobby actions ----------------
        public void SetReady(bool ready)
        {
            if (!IsConnected) return;
            if (_nm.IsServer)
            {
                if (_roster.SetReady(_nm.LocalClientId, ready)) { BroadcastRoster(); OnSessionChanged?.Invoke(); }
                return;
            }
            using var w = new FastBufferWriter(8, Allocator.Temp);
            w.WriteValueSafe((byte)1);
            w.WriteValueSafe(ready);
            _nm.CustomMessagingManager.SendNamedMessage(ReadyMessage, NetworkManager.ServerClientId, w, NetworkDelivery.Reliable);
        }

        public void StartGame()
        {
            if (!IsHost) { RaiseError("Only the host can start the day."); return; }
            if (!IsSceneInBuild(_cfg.gameScene)) { RaiseError($"Farm scene '{_cfg.gameScene}' is not in Build Settings."); return; }
            if (_nm.SceneManager == null) return;
            LoadNetworkScene(_cfg.gameScene);
        }

        public void Leave()
        {
            if (!_nm.IsListening && !_nm.ShutdownInProgress) { _connecting = false; GoToMenu(); return; }
            EndSession(null);
        }

        /// <summary>Ends the session locally; host first tells every client why. Menu loads once NGO has shut down.</summary>
        void EndSession(string reason)
        {
            _leaving = true;
            if (_nm.IsServer)
            {
                var ids = new List<ulong>(_nm.ConnectedClientsIds);
                foreach (var id in ids) if (id != NetworkManager.ServerClientId) _nm.DisconnectClient(id, HostLeftReason);
            }
            if (_transport == SessionTransport.Steam) _steamReinit = true;
            _steam.LeaveLobby();
            _discovery.StopResponding();
            if (_nm.IsListening) _nm.Shutdown();
            ResetState();
            if (reason != null) { LastEndReason = reason; RaiseError(reason); }
            _pendingMenu = true;
            OnSessionChanged?.Invoke();
        }

        void ResetState()
        {
            _roster.Clear();
            _pendingNames.Clear();
            _chat.Clear();
            _transport = SessionTransport.None;
            _connecting = false;
            _wasConnected = false;
        }

        void GoToMenu()
        {
            _leaving = false;
            string target = IsSceneInBuild(_cfg.menuScene) ? _cfg.menuScene : GameInfo.BootstrapScene;
            if (SceneManager.GetActiveScene().name == target) return;
            if (!IsSceneInBuild(target)) return;
            SceneManager.LoadScene(target, LoadSceneMode.Single);
        }

        // ---------------- NGO callbacks ----------------
        void Approve(NetworkManager.ConnectionApprovalRequest req, NetworkManager.ConnectionApprovalResponse resp)
        {
            bool self = req.ClientNetworkId == NetworkManager.ServerClientId;
            var taken = new List<string>(_roster.Names);
            taken.AddRange(_pendingNames.Values);
            var d = ConnectionApproval.Evaluate(req.Payload, self, _roster.Count + _pendingNames.Count, _maxPlayers,
                _cfg.protocolVersion, Application.version, JoinLocked, taken);
            resp.Approved = d.Approved;
            resp.Reason = d.Reason;
            resp.CreatePlayerObject = false; // PlayerSpawnService spawns at markers after scene sync
            resp.Pending = false;
            if (d.Approved) _pendingNames[req.ClientNetworkId] = d.PlayerName;
            else NetLog.Warn("reject", $"Rejected client {req.ClientNetworkId}: {d.Reason}");
        }

        void HandleConnectionEvent(NetworkManager nm, ConnectionEventData e)
        {
            if (e.EventType == ConnectionEvent.ClientConnected)
            {
                if (nm.IsServer)
                {
                    if (!_roster.Contains(e.ClientId))
                        _roster.Add(e.ClientId, TakePending(e.ClientId) ?? PlayerNameSanitizer.Sanitize(null), e.ClientId == nm.LocalClientId);
                    BroadcastRoster();
                    OnSessionChanged?.Invoke();
                }
                else if (e.ClientId == nm.LocalClientId)
                {
                    _connecting = false;
                    _wasConnected = true;
                    NetLog.Info($"Connected to {_joinTarget} as client {e.ClientId}.");
                    OnSessionChanged?.Invoke();
                }
            }
            else if (e.EventType == ConnectionEvent.ClientDisconnected && nm.IsServer && e.ClientId != nm.LocalClientId)
            {
                _pendingNames.Remove(e.ClientId);
                _chat.Forget(e.ClientId);
                if (_roster.Remove(e.ClientId)) { BroadcastRoster(); OnSessionChanged?.Invoke(); }
            }
        }

        void HandleClientDisconnect(ulong id)
        {
            if (_nm.IsServer) { _pendingNames.Remove(id); return; }
            if (!string.IsNullOrEmpty(_nm.DisconnectReason)) _capturedReason = _nm.DisconnectReason;
        }

        void HandleClientStopped(bool wasHost)
        {
            if (wasHost || _leaving) return;
            string reason = !string.IsNullOrEmpty(_nm.DisconnectReason) ? _nm.DisconnectReason : _capturedReason;
            if (string.IsNullOrEmpty(reason))
                reason = _wasConnected ? HostLeftReason : $"Couldn't reach the farm at {_joinTarget}.";
            NetLog.Warn("client-stopped", "Session ended: " + reason);
            if (_transport == SessionTransport.Steam) _steamReinit = true;
            _steam.LeaveLobby();
            ResetState();
            LastEndReason = reason;
            RaiseError(reason);
            _pendingMenu = true;
            OnSessionChanged?.Invoke();
        }

        void HandleServerStopped(bool wasHost)
        {
            if (_leaving) return;
            const string reason = "Hosting stopped unexpectedly (network error).";
            if (_transport == SessionTransport.Steam) _steamReinit = true;
            _steam.LeaveLobby();
            _discovery.StopResponding();
            ResetState();
            LastEndReason = reason;
            RaiseError(reason);
            _pendingMenu = true;
            OnSessionChanged?.Invoke();
        }

        string TakePending(ulong id)
        {
            if (!_pendingNames.TryGetValue(id, out var n)) return null;
            _pendingNames.Remove(id);
            return n;
        }

        // ---------------- Roster messages ----------------
        void RegisterMessages()
        {
            var cmm = _nm.CustomMessagingManager;
            cmm.RegisterNamedMessageHandler(RosterMessage, OnRosterMessage);
            cmm.RegisterNamedMessageHandler(ReadyMessage, OnReadyMessage);
            cmm.RegisterNamedMessageHandler(ChatInMessage, OnChatIn);
            cmm.RegisterNamedMessageHandler(ChatOutMessage, OnChatOut);
        }

        void BroadcastRoster()
        {
            if (!_nm.IsServer || _nm.CustomMessagingManager == null) return;
            var targets = new List<ulong>();
            foreach (var id in _nm.ConnectedClientsIds) if (id != NetworkManager.ServerClientId) targets.Add(id);
            if (targets.Count == 0) return;
            byte[] bytes = _roster.Serialize();
            using var w = new FastBufferWriter(bytes.Length + 8, Allocator.Temp);
            w.WriteValueSafe(bytes.Length);
            w.WriteBytesSafe(bytes);
            _nm.CustomMessagingManager.SendNamedMessage(RosterMessage, targets, w, NetworkDelivery.ReliableSequenced);
        }

        void OnRosterMessage(ulong sender, FastBufferReader reader)
        {
            if (_nm.IsServer || sender != NetworkManager.ServerClientId) return;
            reader.ReadValueSafe(out int len);
            if (len <= 0 || len > 4096) { NetLog.Warn("roster-bad", "Bad roster message length " + len); return; }
            var bytes = new byte[len];
            reader.ReadBytesSafe(ref bytes, len);
            if (_roster.TryDeserialize(bytes)) OnSessionChanged?.Invoke();
            else NetLog.Warn("roster-parse", "Could not parse roster (newer host version?).");
        }

        void OnReadyMessage(ulong sender, FastBufferReader reader)
        {
            if (!_nm.IsServer) return;
            reader.ReadValueSafe(out byte ver);
            if (ver < 1) return;
            reader.ReadValueSafe(out bool ready);
            if (_roster.SetReady(sender, ready)) { BroadcastRoster(); OnSessionChanged?.Invoke(); }
        }

        // ---------------- ISessionSocial ----------------
        public event Action<ulong, string> OnChat;

        /// <summary>Host only: disconnects <paramref name="clientId"/> with <see cref="KickReason"/>.</summary>
        public void Kick(ulong clientId)
        {
            if (!IsHost) { RaiseError("Only the host can send farmhands home."); return; }
            if (clientId == NetworkManager.ServerClientId || !_nm.ConnectedClients.ContainsKey(clientId)) return;
            NetLog.Info($"Kicking client {clientId}.");
            _nm.DisconnectClient(clientId, KickReason);
        }

        /// <summary>Sends a chat line; the server cleans, caps (120) and rate-limits it, then relays to every peer.</summary>
        public void SendChat(string text)
        {
            if (!IsConnected || string.IsNullOrWhiteSpace(text)) return;
            if (text.Length > ChatRules.MaxLength * 2) text = text.Substring(0, ChatRules.MaxLength * 2);
            if (_nm.IsServer) { ServerRelayChat(_nm.LocalClientId, text); return; }
            using var w = new FastBufferWriter(16 + text.Length * 4, Allocator.Temp);
            w.WriteValueSafe((byte)1);
            w.WriteValueSafe(text);
            _nm.CustomMessagingManager.SendNamedMessage(ChatInMessage, NetworkManager.ServerClientId, w, NetworkDelivery.ReliableSequenced);
        }

        void OnChatIn(ulong sender, FastBufferReader reader)
        {
            if (!_nm.IsServer) return;
            reader.ReadValueSafe(out byte ver);
            if (ver < 1) return;
            reader.ReadValueSafe(out string text);
            ServerRelayChat(sender, text);
        }

        void ServerRelayChat(ulong sender, string raw)
        {
            string text = ChatRules.Clean(raw);
            if (text == null) return;
            if (!_chat.Allow(sender, Time.realtimeSinceStartup)) { NetLog.Warn("chat-rate" + sender, $"Chat from {sender} rate-limited."); return; }
            var targets = new List<ulong>();
            foreach (var id in _nm.ConnectedClientsIds) if (id != NetworkManager.ServerClientId) targets.Add(id);
            if (targets.Count > 0)
            {
                using var w = new FastBufferWriter(24 + text.Length * 4, Allocator.Temp);
                w.WriteValueSafe((byte)1);
                w.WriteValueSafe(sender);
                w.WriteValueSafe(text);
                _nm.CustomMessagingManager.SendNamedMessage(ChatOutMessage, targets, w, NetworkDelivery.ReliableSequenced);
            }
            OnChat?.Invoke(sender, text);
        }

        void OnChatOut(ulong from, FastBufferReader reader)
        {
            if (_nm.IsServer || from != NetworkManager.ServerClientId) return;
            reader.ReadValueSafe(out byte ver);
            if (ver < 1) return;
            reader.ReadValueSafe(out ulong sender);
            reader.ReadValueSafe(out string text);
            text = ChatRules.Clean(text);
            if (text != null) OnChat?.Invoke(sender, text);
        }

        // ---------------- Names, discovery ----------------
        public string LocalPlayerName
        {
            get
            {
                if (!string.IsNullOrEmpty(_localNameOverride)) return PlayerNameSanitizer.Sanitize(_localNameOverride);
                string saved = PlayerPrefs.GetString(NamePrefsKey, "");
                if (!string.IsNullOrWhiteSpace(saved)) return PlayerNameSanitizer.Sanitize(saved);
                string steam = _steam?.PersonaName;
                return PlayerNameSanitizer.Sanitize(steam);
            }
        }

        public void SetLocalPlayerName(string name)
        {
            PlayerPrefs.SetString(NamePrefsKey, PlayerNameSanitizer.Sanitize(name));
            PlayerPrefs.Save();
            OnSessionChanged?.Invoke();
        }

        public void ClearLastEndReason() => LastEndReason = null;

        public void StartLanSearch() => _discovery.StartSearching();
        public void StopLanSearch() => _discovery.StopSearching();

        LanDiscovery.Reply BuildDiscoveryReply() => new LanDiscovery.Reply
        {
            host = LocalPlayerName, lobby = _lobbyName, players = _roster.Count, max = _maxPlayers,
            port = Port(), version = Application.version,
            inProgress = SceneManager.GetActiveScene().name == _cfg.gameScene,
        };

        // ---------------- helpers ----------------
        ushort Port() => ushort.TryParse(Arg("-netPort"), out var p) ? p : _cfg.lanPort;

        void Fail(string message)
        {
            if (_nm.IsListening) _nm.Shutdown();
            ResetState();
            LastEndReason = message;
            RaiseError(message);
            OnSessionChanged?.Invoke();
        }

        void RaiseError(string message)
        {
            NetLog.Warn("err:" + message, message);
            OnError?.Invoke(message);
        }

        /// <summary>True when a scene with this name (or path) is enabled in Build Settings.</summary>
        public static bool IsSceneInBuild(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return false;
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                string path = UnityEngine.SceneManagement.SceneUtility.GetScenePathByBuildIndex(i);
                if (path == sceneName || System.IO.Path.GetFileNameWithoutExtension(path) == sceneName) return true;
            }
            return false;
        }

        /// <summary>Parses "host", "host:port" or "[v6]:port"; resolves host names to IPv4.</summary>
        public static bool TryParseEndpoint(string text, ushort defaultPort, out string ip, out ushort port)
        {
            ip = null; port = defaultPort;
            if (string.IsNullOrWhiteSpace(text)) return false;
            text = text.Trim();
            string host = text;
            int colon = text.LastIndexOf(':');
            if (colon > 0 && text.IndexOf(':') == colon)
            {
                if (!ushort.TryParse(text.Substring(colon + 1), out port) || port == 0) return false;
                host = text.Substring(0, colon);
            }
            if (IPAddress.TryParse(host, out var addr)) { ip = addr.ToString(); return true; }
            if (host.Length > 253 || host.IndexOf(' ') >= 0) return false;
            try
            {
                foreach (var a in Dns.GetHostAddresses(host))
                    if (a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) { ip = a.ToString(); return true; }
            }
            catch (Exception) { }
            return false;
        }

        public static bool HasArg(string name) => Array.IndexOf(Environment.GetCommandLineArgs(), name) >= 0;

        public static string Arg(string name)
        {
            var a = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(a, name);
            return i >= 0 && i + 1 < a.Length ? a[i + 1] : null;
        }
    }
}
