using System;
using Unity.Netcode;
using UnityEngine;

namespace HarvestPanic.Gameplay
{
    /// <summary>
    /// Dev-scene helper for <c>Scenes/Dev/PlayerTest.unity</c>: starts a host on play and spawns test props
    /// (crates, tools) on the server. Not used in the real game flow (Networking's bootstrap owns that).
    /// </summary>
    public sealed class PlayerTestBootstrap : MonoBehaviour
    {
        [Serializable]
        public struct PropSpawn
        {
            public GameObject Prefab;
            public Vector3 Position;
            public float Yaw;
        }

        [SerializeField] bool autoStartHost = true;
        [SerializeField] PropSpawn[] props = Array.Empty<PropSpawn>();

        void Start()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return;
            nm.OnServerStarted += SpawnProps;
            if (autoStartHost && !nm.IsListening) nm.StartHost();
        }

        void OnDestroy()
        {
            if (NetworkManager.Singleton != null) NetworkManager.Singleton.OnServerStarted -= SpawnProps;
        }

        void SpawnProps()
        {
            var nm = NetworkManager.Singleton;
            foreach (var p in props)
                if (p.Prefab != null)
                    NetworkObject.InstantiateAndSpawn(p.Prefab, nm, position: p.Position, rotation: Quaternion.Euler(0f, p.Yaw, 0f));
        }
    }
}
