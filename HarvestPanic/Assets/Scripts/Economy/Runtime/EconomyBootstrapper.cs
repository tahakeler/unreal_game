using HarvestPanic.Core;
using Unity.Netcode;
using UnityEngine;

namespace HarvestPanic.Economy
{
    /// <summary>
    /// Spawns the EconomyDirector on the server once a farm scene (any MarketPen or Goods marker) is loaded,
    /// so no other team has to place it. The director prefab is found in Resources/NetworkPrefabs/Economy
    /// (the list Networking registers at startup). Hidden, DontDestroyOnLoad, polls twice per second.
    /// </summary>
    public sealed class EconomyBootstrapper : MonoBehaviour
    {
        const string ListPath = "NetworkPrefabs/Economy";
        float _next;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var go = new GameObject("[EconomyBootstrapper]") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(go);
            go.AddComponent<EconomyBootstrapper>();
        }

        void Update()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 0.5f;
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer || EconomyDirector.Instance != null) return;
            if (LevelMarker.FindAll(MarkerRole.MarketPen).Count == 0 && LevelMarker.FindAll(MarkerRole.Goods).Count == 0) return;
            var prefab = FindDirectorPrefab();
            if (prefab == null) { Debug.LogWarning("[Economy] No EconomyDirector prefab in Resources/" + ListPath); _next += 10f; return; }
            var inst = Instantiate(prefab);
            inst.GetComponent<NetworkObject>().Spawn(true);
        }

        /// <summary>The director prefab from the Economy network prefab list.</summary>
        public static GameObject FindDirectorPrefab()
        {
            var list = Resources.Load<NetworkPrefabsList>(ListPath);
            if (list == null) return null;
            foreach (var p in list.PrefabList)
                if (p.Prefab != null && p.Prefab.GetComponent<EconomyDirector>() != null) return p.Prefab;
            return null;
        }
    }
}
