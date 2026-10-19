using System;
using System.Threading.Tasks;
using Steamworks;
using Steamworks.Data;
using UnityEngine;

namespace HarvestPanic.Networking
{
    /// <summary>
    /// Thin wrapper around Facepunch.Steamworks: lazy init with LAN fallback, lobbies, invites, join-on-invite.
    /// Steam callbacks run on the main thread (Init with asyncCallbacks=false; <see cref="Tick"/> pumps them while
    /// FacepunchTransport is not the active transport). Note FacepunchTransport.Shutdown() calls SteamClient.Shutdown(),
    /// so <see cref="EnsureInit"/> re-initialises after each Steam session.
    /// </summary>
    public sealed class SteamPlatform
    {
        readonly uint _appId;
        readonly bool _allowed;
        float _nextRetry;
        bool _subscribed;
        Lobby? _lobby;

        /// <summary>Raised when the user accepts an invite / clicks Join on a friend (lobby id).</summary>
        public event Action<ulong> OnJoinRequested;

        public SteamPlatform(uint appId, bool allowed)
        {
            _appId = appId;
            _allowed = allowed;
        }

        public bool IsAvailable => _allowed && SafeIsValid();
        public ulong CurrentLobbyId => _lobby?.Id.Value ?? 0;
        public string PersonaName => IsAvailable ? SteamClient.Name : null;

        static bool SafeIsValid()
        {
            try { return SteamClient.IsValid; } catch (Exception) { return false; }
        }

        /// <summary>Initialises Steam if allowed and not already valid. Retries at most every 10 s. Never throws.</summary>
        public bool EnsureInit(bool force = false)
        {
            if (!_allowed) return false;
            if (SafeIsValid()) { Subscribe(); return true; }
            if (!force && Time.realtimeSinceStartup < _nextRetry) return false;
            _nextRetry = Time.realtimeSinceStartup + 10f;
            try
            {
                SteamClient.Init(_appId, false);
                _subscribed = false;
                Subscribe();
                NetLog.Info($"Steam initialised as {SteamClient.Name} ({SteamClient.SteamId}).");
                return true;
            }
            catch (Exception e)
            {
                NetLog.Warn("steam-init", "Steam unavailable, using LAN: " + e.Message);
                return false;
            }
        }

        void Subscribe()
        {
            if (_subscribed) return;
            _subscribed = true;
            SteamFriends.OnGameLobbyJoinRequested -= HandleLobbyJoinRequested;
            SteamFriends.OnGameLobbyJoinRequested += HandleLobbyJoinRequested;
            SteamFriends.OnGameRichPresenceJoinRequested -= HandleRichPresenceJoin;
            SteamFriends.OnGameRichPresenceJoinRequested += HandleRichPresenceJoin;
        }

        void HandleLobbyJoinRequested(Lobby lobby, SteamId friend) => OnJoinRequested?.Invoke(lobby.Id.Value);

        void HandleRichPresenceJoin(Friend friend, string connect)
        {
            if (TryParseConnectLobby(connect, out ulong id)) OnJoinRequested?.Invoke(id);
        }

        /// <summary>Pumps Steam callbacks when the transport is not doing it.</summary>
        public void Tick(bool transportPumps)
        {
            if (transportPumps || !SafeIsValid()) return;
            try { SteamClient.RunCallbacks(); } catch (Exception e) { NetLog.Warn("steam-cb", "Steam callbacks: " + e.Message); }
        }

        /// <summary>Creates a lobby that advertises this host. Returns null on failure.</summary>
        public async Task<ulong?> CreateLobbyAsync(string name, int maxPlayers, bool friendsOnly, string version, int protocol)
        {
            if (!IsAvailable) return null;
            var created = await SteamMatchmaking.CreateLobbyAsync(maxPlayers);
            if (!created.HasValue) return null;
            var lobby = created.Value;
            if (friendsOnly) lobby.SetFriendsOnly(); else lobby.SetPublic();
            lobby.SetJoinable(true);
            lobby.SetData("hp_name", name ?? "");
            lobby.SetData("hp_version", version ?? "");
            lobby.SetData("hp_protocol", protocol.ToString());
            lobby.SetData("hp_host", SteamClient.SteamId.Value.ToString());
            _lobby = lobby;
            SteamFriends.SetRichPresence("connect", "+connect_lobby " + lobby.Id.Value);
            return lobby.Id.Value;
        }

        /// <summary>Result of <see cref="JoinLobbyAsync"/>.</summary>
        public struct JoinResult { public bool Ok; public ulong HostSteamId; public string Error; public string Version; public int Protocol; }

        /// <summary>Enters a Steam lobby and returns the owner's Steam id to connect the transport to.</summary>
        public async Task<JoinResult> JoinLobbyAsync(ulong lobbyId)
        {
            if (!IsAvailable) return new JoinResult { Error = "Steam isn't running." };
            var lobby = new Lobby(lobbyId);
            var enter = await lobby.Join();
            if (enter != RoomEnter.Success) return new JoinResult { Error = "Couldn't join that farm (" + enter + ")." };
            _lobby = lobby;
            ulong.TryParse(lobby.GetData("hp_host"), out ulong host);
            if (host == 0) host = lobby.Owner.Id.Value;
            int.TryParse(lobby.GetData("hp_protocol"), out int proto);
            return new JoinResult { Ok = true, HostSteamId = host, Version = lobby.GetData("hp_version"), Protocol = proto };
        }

        public void LeaveLobby()
        {
            if (!_lobby.HasValue) return;
            try { _lobby.Value.Leave(); SteamFriends.ClearRichPresence(); } catch (Exception) { }
            _lobby = null;
        }

        public bool OpenInviteOverlay()
        {
            if (!IsAvailable || !_lobby.HasValue) return false;
            SteamFriends.OpenGameInviteOverlay(_lobby.Value.Id);
            return true;
        }

        /// <summary>Steam account type from the id's high bits: 1 = individual (a host), 7 = chat (a lobby).</summary>
        public static bool IsLobbyId(ulong id) => ((id >> 52) & 0xF) == 7;
        public static bool IsUserId(ulong id) => ((id >> 52) & 0xF) == 1;

        /// <summary>Parses "+connect_lobby 1234" (Steam launch/invite argument).</summary>
        public static bool TryParseConnectLobby(string s, out ulong id)
        {
            id = 0;
            if (string.IsNullOrEmpty(s)) return false;
            int i = s.IndexOf("+connect_lobby", StringComparison.Ordinal);
            if (i < 0) return false;
            string rest = s.Substring(i + "+connect_lobby".Length).Trim();
            int sp = rest.IndexOf(' ');
            if (sp >= 0) rest = rest.Substring(0, sp);
            return ulong.TryParse(rest, out id) && id != 0;
        }

        /// <summary>Looks for "+connect_lobby &lt;id&gt;" in the process arguments (game launched from an invite).</summary>
        public static bool TryGetLaunchLobby(out ulong id)
        {
            id = 0;
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "+connect_lobby" && ulong.TryParse(args[i + 1], out id)) return true;
            return false;
        }
    }
}
