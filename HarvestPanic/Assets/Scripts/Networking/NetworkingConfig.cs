using UnityEngine;

namespace HarvestPanic.Networking
{
    /// <summary>
    /// All network tuning lives here, not in code. Asset: Resources/Data/Networking/NetworkingConfig.asset.
    /// Sized for 2-4 players, &lt;= 150 ms RTT, budget 96-128 kbps host->client (session-networking GDD).
    /// </summary>
    [CreateAssetMenu(menuName = "Harvest Panic/Networking/Networking Config", fileName = "NetworkingConfig")]
    public sealed class NetworkingConfig : ScriptableObject
    {
        public const string ResourcePath = "Data/Networking/NetworkingConfig";

        [Header("Prefabs")]
        public GameObject networkManagerPrefab;
        public PlayerPrefabRef playerPrefab;

        [Header("Versioning")]
        [Tooltip("Bump on any wire/gameplay-replication change. Mismatch blocks join with a readable reason.")]
        public int protocolVersion = 1;

        [Header("Simulation")]
        [Tooltip("NGO tick rate (Hz). GDD near-body rate is 30 Hz.")]
        public uint tickRate = 30;

        [Header("LAN (Unity Transport)")]
        public ushort lanPort = 7777;
        public string lanListenAddress = "0.0.0.0";
        [Tooltip("ms per attempt; total connect timeout = connectTimeoutMs * maxConnectAttempts.")]
        public int connectTimeoutMs = 1000;
        public int maxConnectAttempts = 10;
        [Tooltip("Seconds without traffic before a peer is dropped.")]
        public int disconnectTimeoutMs = 10000;

        [Header("LAN discovery (UDP broadcast)")]
        public ushort discoveryPort = 47777;
        public float discoveryInterval = 1.5f;
        [Tooltip("Hosts not heard from for this long drop off the list.")]
        public float discoveryExpiry = 5f;

        [Header("Steam (Facepunch)")]
        // TODO(owner): replace 480 (Valve's Spacewar test app) with HARVEST PANIC's real Steam App ID once the
        // Steamworks app exists. Overridable without a rebuild via steam_appid.txt or "-steamAppId N" (see SteamAppId).
        [Tooltip("Steam App ID. 480 = Spacewar test app (TODO: owner's real App ID). steam_appid.txt / -steamAppId override.")]
        public uint steamAppId = 480;
        public bool preferSteam = true;
        [Tooltip("Try Steam in -batchmode (CI normally has no Steam client).")]
        public bool steamInBatchmode = false;

        [Header("Scenes (names in Build Settings)")]
        public string menuScene = "MainMenu";
        public string lobbyScene = "Lobby";
        public string gameScene = "Muddlebury";

        [Header("Dev convenience")]
        [Tooltip("Instantiate the NetworkManager prefab when a scene is played without Bootstrap.")]
        public bool autoSpawnNetworkManager = true;
        [Tooltip("Editor only: playing a gameplay scene directly starts a solo LAN host in place.")]
        public bool editorAutoHostInGameplayScenes = true;

        static NetworkingConfig _cached;

        /// <summary>Loads the shared config (or a default instance when the asset is missing).</summary>
        public static NetworkingConfig Load()
        {
            if (_cached != null) return _cached;
            _cached = Resources.Load<NetworkingConfig>(ResourcePath);
            if (_cached == null) _cached = CreateInstance<NetworkingConfig>();
            return _cached;
        }
    }
}
