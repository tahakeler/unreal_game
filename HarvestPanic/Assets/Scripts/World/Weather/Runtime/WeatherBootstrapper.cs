using HarvestPanic.Core;
using Unity.Netcode;
using UnityEngine;

namespace HarvestPanic.Weather
{
    /// <summary>
    /// Spawns the WeatherDirector once a farm scene (MarketPen or River marker) is loaded: on the server it is a
    /// networked spawn from Resources/NetworkPrefabs/Weather; with no session it is a local instance (dev/tests).
    /// </summary>
    public sealed class WeatherBootstrapper : MonoBehaviour
    {
        const string ListPath = "NetworkPrefabs/Weather";
        float _next, _offlineSince = -1f;

        /// <summary>Disable auto-spawn (tests that build their own director).</summary>
        public static bool Enabled = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            Enabled = true;
            var go = new GameObject("[WeatherBootstrapper]") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(go);
            go.AddComponent<WeatherBootstrapper>();
        }

        void Update()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 0.5f;
            if (!Enabled || WeatherDirector.Instance != null) { _offlineSince = -1f; return; }
            if (LevelMarker.FindAll(MarkerRole.MarketPen).Count == 0 && LevelMarker.FindAll(MarkerRole.River).Count == 0) return;
            var nm = NetworkManager.Singleton;
            bool listening = nm != null && nm.IsListening;
            if (listening && !nm.IsServer) return;
            if (!listening)
            {
                // give a session a moment to start (scene loaded before StartHost)
                if (_offlineSince < 0f) { _offlineSince = Time.unscaledTime; return; }
                if (Time.unscaledTime - _offlineSince < 1.5f) return;
            }
            var prefab = FindDirectorPrefab();
            if (prefab == null) { Debug.LogWarning("[Weather] No WeatherDirector prefab in Resources/" + ListPath); _next += 10f; return; }
            var inst = Instantiate(prefab);
            inst.name = prefab.name;
            if (listening) inst.GetComponent<NetworkObject>().Spawn(true);
        }

        /// <summary>The director prefab from the Weather network prefab list.</summary>
        public static GameObject FindDirectorPrefab()
        {
            var list = Resources.Load<NetworkPrefabsList>(ListPath);
            if (list == null) return null;
            foreach (var p in list.PrefabList)
                if (p.Prefab != null && p.Prefab.GetComponent<WeatherDirector>() != null) return p.Prefab;
            return null;
        }
    }
}
