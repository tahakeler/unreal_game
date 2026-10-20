using System;
using System.Collections.Generic;
using HarvestPanic.Core;

namespace HarvestPanic.UI
{
    /// <summary>
    /// In-memory <see cref="ISessionService"/> for UI development and tests. Registered by <see cref="UiBootstrap"/> only when no
    /// real provider exists. Also exposes the optional duck-typed members the UI looks for (see <see cref="SessionAccess"/>):
    /// TransportName, DiscoveredHosts, RefreshDiscovery, SendChat/OnChat, Kick.
    /// </summary>
    public sealed class FakeSessionService : ISessionService, ISessionSocial
    {
        readonly List<PlayerInfo> _players = new List<PlayerInfo>();
        readonly List<LanHost> _hosts = new List<LanHost>
        {
            new LanHost { Name = "Grandpa's Farm", Address = "192.168.1.20", Players = 2, MaxPlayers = 4 },
            new LanHost { Name = "Dave's Pig Sty", Address = "192.168.1.34", Players = 1, MaxPlayers = 4 },
            new LanHost { Name = "Full House", Address = "192.168.1.51", Players = 4, MaxPlayers = 4 },
        };
        float _pendingDelay = -1f;
        Action _pending;

        public string TransportName { get; set; } = "LAN";
        /// <summary>Seconds before a Host/Join "connects"; 0 = synchronous (tests).</summary>
        public float ConnectDelay { get; set; } = 0f;
        public string LocalName { get; set; } = "You";

        public int StartGameCalls { get; private set; }
        public int LeaveCalls { get; private set; }
        public string LastJoinAddress { get; private set; }
        public string LastLobbyName { get; private set; }
        public int LastMaxPlayers { get; private set; }
        public List<ulong> Kicked { get; } = new List<ulong>();
        public List<string> SentChat { get; } = new List<string>();

        public bool IsConnected { get; private set; }
        public bool IsHost { get; private set; }
        public ulong LocalClientId { get; private set; }
        public IReadOnlyList<PlayerInfo> Players => _players;
        public IReadOnlyList<LanHost> DiscoveredHosts => _hosts;

        public event Action OnSessionChanged;
        public event Action<string> OnError;
        public event Action<ulong, string> OnChat;

        public void StartHost(string lobbyName, int maxPlayers)
        {
            LastLobbyName = lobbyName; LastMaxPlayers = maxPlayers;
            Defer(() =>
            {
                IsConnected = true; IsHost = true; LocalClientId = 0;
                _players.Clear();
                _players.Add(new PlayerInfo { ClientId = 0, Name = LocalName, IsHost = true });
                OnSessionChanged?.Invoke();
            });
        }

        public void Join(string addressOrCode)
        {
            LastJoinAddress = addressOrCode;
            if (string.IsNullOrWhiteSpace(addressOrCode) || addressOrCode.Trim().ToLowerInvariant() == "fail")
            {
                Defer(() => OnError?.Invoke("Could not reach that farm. Check the address and try again."));
                return;
            }
            Defer(() =>
            {
                IsConnected = true; IsHost = false; LocalClientId = 1;
                _players.Clear();
                _players.Add(new PlayerInfo { ClientId = 0, Name = "Grandpa", IsHost = true, IsReady = true });
                _players.Add(new PlayerInfo { ClientId = 1, Name = LocalName });
                OnSessionChanged?.Invoke();
            });
        }

        public void Leave()
        {
            LeaveCalls++;
            _pending = null; _pendingDelay = -1f;
            IsConnected = false; IsHost = false; _players.Clear();
            OnSessionChanged?.Invoke();
        }

        public void SetReady(bool ready)
        {
            for (int i = 0; i < _players.Count; i++)
            {
                if (_players[i].ClientId != LocalClientId) continue;
                var p = _players[i]; p.IsReady = ready; _players[i] = p;
            }
            OnSessionChanged?.Invoke();
        }

        public void StartGame() => StartGameCalls++;

        // ---- dev helpers / optional duck-typed API -------------------------------------------------------
        public void RefreshDiscovery() { }

        public void SendChat(string text)
        {
            SentChat.Add(text);
            OnChat?.Invoke(LocalClientId, text);
        }

        public void Kick(ulong clientId)
        {
            Kicked.Add(clientId);
            _players.RemoveAll(p => p.ClientId == clientId);
            OnSessionChanged?.Invoke();
        }

        public void RaiseError(string message) => OnError?.Invoke(message);

        public ulong AddGuest(string name, bool ready = false)
        {
            ulong id = (ulong)(_players.Count + 10);
            _players.Add(new PlayerInfo { ClientId = id, Name = name, IsReady = ready });
            OnSessionChanged?.Invoke();
            return id;
        }

        public void SetGuestReady(ulong id, bool ready)
        {
            for (int i = 0; i < _players.Count; i++)
                if (_players[i].ClientId == id) { var p = _players[i]; p.IsReady = ready; _players[i] = p; }
            OnSessionChanged?.Invoke();
        }

        public void Tick(float dt)
        {
            if (_pending == null) return;
            _pendingDelay -= dt;
            if (_pendingDelay > 0f) return;
            var a = _pending; _pending = null; a();
        }

        void Defer(Action a)
        {
            if (ConnectDelay <= 0f) { a(); return; }
            _pending = a; _pendingDelay = ConnectDelay;
        }
    }
}
