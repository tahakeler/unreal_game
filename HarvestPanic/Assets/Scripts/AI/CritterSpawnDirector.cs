using System.Collections.Generic;
using HarvestPanic.Core;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace HarvestPanic.AI
{
    /// <summary>
    /// Spawns critters for every <see cref="LevelMarker"/> with role <see cref="MarkerRole.CritterSpawner"/>
    /// (key = critter id such as <c>Critter.Pig</c> or <c>Pig</c>, count = how many). Self-bootstrapping, so no scene edits
    /// are needed: on the server it spawns NetworkObjects once the server is listening; with no NetworkManager it spawns
    /// plain instances (offline sandbox, tests). Clients never spawn; NGO replicates.
    /// </summary>
    public sealed class CritterSpawnDirector : MonoBehaviour
    {
        /// <summary>Set false to stop automatic spawning (tests that place critters themselves).</summary>
        public static bool AutoSpawn = true;

        static CritterSpawnDirector _instance;
        readonly HashSet<int> _done = new HashSet<int>();
        bool _hookedServer;

        /// <summary>Critters spawned by the director (server/offline).</summary>
        public static readonly List<CritterBrain> Spawned = new List<CritterBrain>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _instance = null; AutoSpawn = true; Spawned.Clear(); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("[CritterSpawnDirector]") { hideFlags = HideFlags.HideInHierarchy };
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<CritterSpawnDirector>();
            SceneManager.sceneLoaded += (_, __) => { if (_instance != null) _instance.TrySpawnAll(); };
            _instance.TrySpawnAll();
        }

        void TrySpawnAll()
        {
            if (!AutoSpawn) return;
            var nm = NetworkManager.Singleton;
            if (nm != null && !nm.IsListening)
            {
                if (!_hookedServer) { _hookedServer = true; nm.OnServerStarted += () => TrySpawnAll(); }
                return;
            }
            if (nm != null && !nm.IsServer) return;
            // Objects spawned while the server is still loading the scene for clients are not delivered to them:
            // wait for the NGO scene event to complete (a solo host never loads a networked scene and spawns now).
            HookSceneEvents(nm);
            if (nm != null && _sceneLoading) return;
            SpawnForMarkers(LevelMarker.FindAll(MarkerRole.CritterSpawner), nm != null && nm.IsListening);
        }

        bool _sceneLoading;
        NetworkSceneManager _hookedScenes;

        void HookSceneEvents(NetworkManager nm)
        {
            if (nm == null || nm.SceneManager == null || _hookedScenes == nm.SceneManager) return;
            _hookedScenes = nm.SceneManager;
            nm.SceneManager.OnLoad += (id, scene, mode, op) => { if (id == nm.LocalClientId) _sceneLoading = true; };
            nm.SceneManager.OnLoadEventCompleted += (scene, mode, done, timedOut) => { _sceneLoading = false; TrySpawnAll(); };
        }

        void SpawnForMarkers(List<LevelMarker> markers, bool networked)
        {
            foreach (var m in markers)
            {
                if (m == null || !_done.Add(m.GetInstanceID())) continue;
                if (!TryResolve(m.Key, out var p) || p.Prefab == null)
                {
                    Debug.LogWarning($"[CritterSpawnDirector] No critter personality/prefab for key '{m.Key}' ({m.name})", m);
                    continue;
                }
                for (int i = 0; i < m.Count; i++) Spawn(p, SpawnPoint(m.transform.position, i), networked);
            }
        }

        /// <summary>Finds a personality by id, accepting <c>Critter.Pig</c>, <c>Pig</c> or <c>pig</c>.</summary>
        public static bool TryResolve(string key, out CritterPersonality p)
        {
            p = null;
            if (string.IsNullOrEmpty(key)) return false;
            return Content.Critters.TryGet(key, out p) || Content.Critters.TryGet("Critter." + key, out p);
        }

        /// <summary>Spawns one critter at <paramref name="position"/> (server/offline only).</summary>
        public static CritterBrain Spawn(CritterPersonality p, Vector3 position, bool networked)
        {
            var rot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            var go = Instantiate(p.Prefab, position, rot);
            var brain = go.GetComponent<CritterBrain>();
            if (brain != null) { brain.Home = position; if (brain.Personality == null) brain.SetPersonality(p); }
            if (networked && go.TryGetComponent(out NetworkObject no)) no.Spawn(true);
            if (brain != null) Spawned.Add(brain);
            return brain;
        }

        static Vector3 SpawnPoint(Vector3 centre, int i)
        {
            if (i == 0 && NavMesh.SamplePosition(centre, out var h0, 2f, NavMesh.AllAreas)) return h0.position;
            float angle = i * 137.5f * Mathf.Deg2Rad; // golden-angle spiral
            float r = 0.8f + 0.45f * Mathf.Sqrt(i);
            Vector3 p = centre + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * r;
            return NavMesh.SamplePosition(p, out var hit, 2f, NavMesh.AllAreas) ? hit.position : p;
        }
    }
}
