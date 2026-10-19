using System.Collections.Generic;
using HarvestPanic.Core;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HarvestPanic.Networking
{
    /// <summary>
    /// Root of the persistent NetworkManager prefab (Prefabs/Networking/NetworkManager.prefab, placed in Bootstrap).
    /// Keeps exactly one instance alive (DontDestroyOnLoad), registers every Resources/NetworkPrefabs/*.asset list
    /// plus the PlayerPrefabRef prefab, and spawns itself when a scene is played without Bootstrap.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    [RequireComponent(typeof(NetworkManager))]
    public sealed class NetworkBootstrap : MonoBehaviour
    {
        /// <summary>The live instance (null before boot / after destroy).</summary>
        public static NetworkBootstrap Instance { get; private set; }

        /// <summary>Folder under Resources holding every team's NetworkPrefabsList.</summary>
        public const string PrefabListsFolder = "NetworkPrefabs";

        NetworkManager _nm;
        readonly List<NetworkPrefabsList> _registered = new List<NetworkPrefabsList>();

        public NetworkManager Manager => _nm;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                // Bootstrap reloaded (e.g. back-to-menu fallback): keep the original.
                gameObject.SetActive(false);
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            _nm = GetComponent<NetworkManager>();
            RegisterPrefabLists();
        }

        void Start()
        {
            if (Instance != this) return;
            // Boot flow: Bootstrap -> MainMenu (unless automation or a Steam invite drives the session).
            if (SceneManager.GetActiveScene().name != GameInfo.BootstrapScene) return;
            if (SessionService.HasArg("-netHost") || SessionService.HasArg("-netJoin") || SessionService.HasArg("+connect_lobby")) return;
            string menu = NetworkingConfig.Load().menuScene;
            if (_nm.IsListening || !SessionService.IsSceneInBuild(menu)) return;
            SceneManager.LoadScene(menu, LoadSceneMode.Single);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Adds each Resources/NetworkPrefabs list to NetworkConfig.Prefabs (idempotent) and makes sure the player
        /// prefab is registered. Call before StartHost/StartClient; NGO re-initialises prefabs on start.
        /// </summary>
        public void RegisterPrefabLists()
        {
            if (_nm == null) _nm = GetComponent<NetworkManager>();
            var prefabs = _nm.NetworkConfig.Prefabs;
            var lists = Resources.LoadAll<NetworkPrefabsList>(PrefabListsFolder);
            System.Array.Sort(lists, (a, b) => string.CompareOrdinal(a.name, b.name)); // deterministic on every peer
            foreach (var list in lists)
            {
                if (list == null || prefabs.NetworkPrefabsLists.Contains(list)) continue;
                prefabs.NetworkPrefabsLists.Add(list);
                _registered.Add(list);
            }

            var playerRef = PlayerPrefabRef.Load();
            if (playerRef != null && playerRef.prefab != null && !IsRegistered(playerRef.prefab))
            {
                if (_nm.IsListening) NetLog.Warn("player-prefab", "Player prefab is not in any NetworkPrefabs list and the session is running.");
                else _nm.AddNetworkPrefab(playerRef.prefab);
            }
        }

        bool IsRegistered(GameObject prefab)
        {
            foreach (var list in _nm.NetworkConfig.Prefabs.NetworkPrefabsLists)
                if (list != null && list.Contains(prefab)) return true;
            return _nm.NetworkConfig.Prefabs.Contains(prefab);
        }

        /// <summary>Lists added from Resources (for diagnostics/tests).</summary>
        public IReadOnlyList<NetworkPrefabsList> RegisteredLists => _registered;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoSpawn()
        {
            if (Instance != null || NetworkManager.Singleton != null) return;
            string scene = SceneManager.GetActiveScene().name;
            if (scene.StartsWith("InitTestScene")) return; // test runner: tests create their own managers
            var cfg = NetworkingConfig.Load();
            if (!cfg.autoSpawnNetworkManager || cfg.networkManagerPrefab == null) return;
            var go = Instantiate(cfg.networkManagerPrefab);
            go.name = cfg.networkManagerPrefab.name;
#if UNITY_EDITOR
            if (cfg.editorAutoHostInGameplayScenes && scene != GameInfo.BootstrapScene && scene != cfg.menuScene && scene != cfg.lobbyScene)
            {
                var session = go.GetComponent<SessionService>();
                if (session != null) session.HostInPlaceForEditor();
            }
#endif
        }
    }
}
