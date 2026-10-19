using System;
using System.Collections.Generic;

namespace HarvestPanic.Core
{
    /// <summary>Which transport the current session runs on.</summary>
    public enum SessionTransport { None, Lan, Steam }

    /// <summary>A host found by LAN discovery (UDP broadcast).</summary>
    [Serializable]
    public struct DiscoveredSession
    {
        /// <summary>IP address to pass to <see cref="ISessionPlatform.JoinLan"/>.</summary>
        public string Address;
        /// <summary>Game port on the host.</summary>
        public ushort Port;
        /// <summary>Host player's display name.</summary>
        public string HostName;
        /// <summary>Lobby / farm name.</summary>
        public string LobbyName;
        /// <summary>Connected players.</summary>
        public int Players;
        /// <summary>Player cap.</summary>
        public int MaxPlayers;
        /// <summary>Host build version (Application.version).</summary>
        public string Version;
        /// <summary>True when the host is past the lobby (join-in-progress).</summary>
        public bool InProgress;
        /// <summary>Time.realtimeSinceStartup of the last reply.</summary>
        public float LastSeen;
    }

    /// <summary>
    /// Add-only extension of <see cref="ISessionService"/> for the frontend: explicit LAN / Steam hosting and joining,
    /// LAN browser, Steam invites and the local player name. Provider: Networking (same object as ISessionService).
    /// </summary>
    public interface ISessionPlatform
    {
        /// <summary>Transport in use (None when offline).</summary>
        SessionTransport ActiveTransport { get; }
        /// <summary>True while a host/join is in progress but not yet connected.</summary>
        bool IsConnecting { get; }
        /// <summary>True when the Steam client is running and initialised.</summary>
        bool IsSteamAvailable { get; }
        /// <summary>Name sent to the host (Steam persona, else saved setting, else "Farmer").</summary>
        string LocalPlayerName { get; }
        /// <summary>Sets and persists the local player name (sanitised).</summary>
        void SetLocalPlayerName(string name);
        /// <summary>Player-readable reason the last session ended or failed, or null. Read it when the menu opens.</summary>
        string LastEndReason { get; }
        /// <summary>Clears <see cref="LastEndReason"/> once shown.</summary>
        void ClearLastEndReason();

        /// <summary>Hosts over Unity Transport on the LAN port.</summary>
        void HostLan(string lobbyName, int maxPlayers);
        /// <summary>Joins a LAN host by IP or "ip:port".</summary>
        void JoinLan(string address);
        /// <summary>Hosts through a Steam lobby; falls back to LAN when Steam is not running.</summary>
        void HostSteam(string lobbyName, int maxPlayers, bool friendsOnly);
        /// <summary>Joins a Steam lobby id or a host Steam id.</summary>
        void JoinSteam(ulong lobbyOrHostSteamId);
        /// <summary>Opens the Steam invite overlay for the current lobby. False when unavailable.</summary>
        bool OpenInviteOverlay();

        /// <summary>Hosts currently answering LAN discovery.</summary>
        IReadOnlyList<DiscoveredSession> LanSessions { get; }
        /// <summary>Raised when <see cref="LanSessions"/> changes.</summary>
        event Action OnLanSessionsChanged;
        /// <summary>Starts broadcasting discovery probes (call when the Join/LAN tab opens).</summary>
        void StartLanSearch();
        /// <summary>Stops probing.</summary>
        void StopLanSearch();
    }
}
