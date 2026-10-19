using System;
using System.Collections.Generic;

namespace HarvestPanic.Core
{
    /// <summary>One connected player as shown in lobby and HUD.</summary>
    [Serializable]
    public struct PlayerInfo
    {
        /// <summary>NGO client id.</summary>
        public ulong ClientId;
        /// <summary>Display name.</summary>
        public string Name;
        /// <summary>Ready flag in the lobby.</summary>
        public bool IsReady;
        /// <summary>True for the host.</summary>
        public bool IsHost;
    }

    /// <summary>
    /// Session control for the Frontend UI. Provider: Networking team. v0: Networking may add members
    /// (add-only) but must not change these; UI codes only against this interface.
    /// </summary>
    public interface ISessionService
    {
        /// <summary>True while connected as host or client.</summary>
        bool IsConnected { get; }

        /// <summary>True when this machine is the host/server.</summary>
        bool IsHost { get; }

        /// <summary>This machine's client id (valid when connected).</summary>
        ulong LocalClientId { get; }

        /// <summary>Connected players, host first.</summary>
        IReadOnlyList<PlayerInfo> Players { get; }

        /// <summary>Raised when connection state or the player list changes.</summary>
        event Action OnSessionChanged;

        /// <summary>Raised with a player-readable message when a start/join/leave fails.</summary>
        event Action<string> OnError;

        /// <summary>Starts hosting (LAN or Steam depending on build); <paramref name="maxPlayers"/> is 1..4.</summary>
        void StartHost(string lobbyName, int maxPlayers);

        /// <summary>Joins a host by LAN address, Steam id or lobby code.</summary>
        void Join(string addressOrCode);

        /// <summary>Leaves the session and returns to the frontend.</summary>
        void Leave();

        /// <summary>Sets the local player's ready flag.</summary>
        void SetReady(bool ready);

        /// <summary>Host only: loads the farm scene for everyone.</summary>
        void StartGame();
    }
}
