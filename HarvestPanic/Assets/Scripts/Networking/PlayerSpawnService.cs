using System;
using System.Collections.Generic;
using HarvestPanic.Core;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HarvestPanic.Networking
{
    /// <summary>
    /// Server-only: spawns the PlayerPrefabRef prefab as each client's player object at the LevelMarker PlayerStart
    /// whose Index equals the player's roster slot (0..3). Players are spawned with destroyWithScene, so every
    /// NGO scene switch (Lobby -> Muddlebury) re-spawns them at the new scene's markers. Late joiners spawn when
    /// their scene synchronisation completes. Skips the menu scene.
    /// </summary>
    [RequireComponent(typeof(NetworkManager))]
    public sealed class PlayerSpawnService : MonoBehaviour
    {
        NetworkManager _nm;
        Func<ulong, int> _slotOf;
        string _menuScene;
        NetworkSceneManager _hookedScenes;

        /// <summary>Server: raised after a player object is spawned (client id, object).</summary>
        public event Action<ulong, NetworkObject> OnPlayerSpawned;

        void Awake() => _nm = GetComponent<NetworkManager>();

        /// <summary>Call right after StartHost succeeds.</summary>
        public void BindServer(Func<ulong, int> slotOf, string menuScene)
        {
            _slotOf = slotOf;
            _menuScene = menuScene;
            if (_hookedScenes == _nm.SceneManager) return;
            _hookedScenes = _nm.SceneManager;
            _nm.SceneManager.OnLoadEventCompleted += (scene, mode, completed, timedOut) =>
            {
                foreach (var id in timedOut) NetLog.Warn("scene-timeout" + id, $"Client {id} timed out loading {scene}.");
                foreach (var id in completed) SpawnFor(id);
            };
            _nm.SceneManager.OnSynchronizeComplete += SpawnFor;
        }

        /// <summary>Spawns every connected client that has no player object yet (used when no scene load happens).</summary>
        public void SpawnMissing()
        {
            if (_nm == null || !_nm.IsServer) return;
            foreach (var id in _nm.ConnectedClientsIds) SpawnFor(id);
        }

        /// <summary>Spawns <paramref name="clientId"/>'s player if it is connected and has none.</summary>
        public void SpawnFor(ulong clientId)
        {
            if (_nm == null || !_nm.IsServer || _nm.ShutdownInProgress) return;
            if (!_nm.ConnectedClients.TryGetValue(clientId, out var client) || client.PlayerObject != null) return;
            if (SceneManager.GetActiveScene().name == _menuScene) return;
            var prefabRef = PlayerPrefabRef.Load();
            if (prefabRef == null || prefabRef.prefab == null)
            {
                NetLog.Error("no-player-prefab", "PlayerPrefabRef is missing or empty (Resources/" + PlayerPrefabRef.ResourcePath + ").");
                return;
            }
            int slot = _slotOf != null ? _slotOf(clientId) : 0;
            GetSpawnPose(slot, out var pos, out var rot);
            var go = Instantiate(prefabRef.prefab, pos, rot);
            var no = go.GetComponent<NetworkObject>();
            if (no == null)
            {
                NetLog.Error("player-no-netobj", $"Player prefab '{prefabRef.prefab.name}' has no NetworkObject.");
                Destroy(go);
                return;
            }
            no.SpawnAsPlayerObject(clientId, true);
            OnPlayerSpawned?.Invoke(clientId, no);
        }

        /// <summary>Marker with Index == slot, else markers[slot % n], else a 2 m spaced row at the origin.</summary>
        public static void GetSpawnPose(int slot, out Vector3 position, out Quaternion rotation)
        {
            slot = Mathf.Max(0, slot);
            var markers = LevelMarker.FindAll(MarkerRole.PlayerStart);
            if (markers.Count > 0)
            {
                LevelMarker pick = null;
                foreach (var m in markers) if (m.Index == slot) { pick = m; break; }
                if (pick == null) pick = markers[slot % markers.Count];
                position = pick.transform.position;
                rotation = Quaternion.Euler(0f, pick.transform.eulerAngles.y, 0f);
                return;
            }
            NetLog.Warn("no-player-start", $"No PlayerStart markers in '{SceneManager.GetActiveScene().name}'; spawning at origin row.");
            position = new Vector3(slot * 2f - 3f, 1f, 0f);
            rotation = Quaternion.identity;
        }
    }
}
