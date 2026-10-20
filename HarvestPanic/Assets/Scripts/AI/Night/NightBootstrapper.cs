using HarvestPanic.Core;
using Unity.Netcode;
using UnityEngine;

namespace HarvestPanic.AI.Night
{
    /// <summary>Spawns the NightDirector on the server once a farm scene (MarketPen marker) is loaded (offline: local instance).</summary>
    public sealed class NightBootstrapper : MonoBehaviour
    {
        const string ListPath = "NetworkPrefabs/Night";
        float _next, _offlineSince = -1f;
        public static bool Enabled = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            Enabled = true;
            var go = new GameObject("[NightBootstrapper]") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(go);
            go.AddComponent<NightBootstrapper>();
        }

        void Update()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 0.5f;
            if (!Enabled || NightDirector.Instance != null) { _offlineSince = -1f; return; }
            if (LevelMarker.FindAll(MarkerRole.MarketPen).Count == 0) return;
            var nm = NetworkManager.Singleton;
            bool listening = nm != null && nm.IsListening;
            if (listening && !nm.IsServer) return;
            if (!listening)
            {
                if (_offlineSince < 0f) { _offlineSince = Time.unscaledTime; return; }
                if (Time.unscaledTime - _offlineSince < 1.5f) return;
            }
            var prefab = FindDirectorPrefab();
            if (prefab == null) { _next += 10f; return; }
            var inst = Instantiate(prefab); inst.name = prefab.name;
            if (listening) inst.GetComponent<NetworkObject>().Spawn(true);
        }

        public static GameObject FindDirectorPrefab()
        {
            var list = Resources.Load<NetworkPrefabsList>(ListPath);
            if (list == null) return null;
            foreach (var p in list.PrefabList) if (p.Prefab != null && p.Prefab.GetComponent<NightDirector>() != null) return p.Prefab;
            return null;
        }
    }
}
