using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarvestPanic.Core;
using HarvestPanic.Networking;
using Netcode.Transports.Facepunch;
using Unity.Netcode;
using Unity.Netcode.Editor.Configuration;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HarvestPanic.Editor
{
    /// <summary>
    /// Idempotent generator for every Networking-owned asset: NetworkManager + placeholder player prefabs,
    /// NetworkingConfig / PlayerPrefabRef data, the Networking NetworkPrefabsList, NetTest scenes, the
    /// Bootstrap scene's NetworkManager instance and the Build Settings scene list.
    /// Unity -batchmode -nographics -projectPath HarvestPanic -executeMethod HarvestPanic.Editor.NetworkingSetup.Configure -quit -logFile /abs/log
    /// </summary>
    public static class NetworkingSetup
    {
        public const string PrefabDir = "Assets/Prefabs/Networking";
        public const string NetworkManagerPrefabPath = PrefabDir + "/NetworkManager.prefab";
        public const string PlaceholderPlayerPath = PrefabDir + "/PlaceholderPlayer.prefab";
        public const string DataDir = "Assets/Resources/Data/Networking";
        public const string ConfigPath = DataDir + "/NetworkingConfig.asset";
        public const string PlayerRefPath = DataDir + "/PlayerPrefabRef.asset";
        public const string PrefabListPath = "Assets/Resources/NetworkPrefabs/Networking.asset";
        public const string TestSceneDir = "Assets/Scenes/Networking";
        public const string TestLobbyScene = "NetTest_Lobby";
        public const string TestFarmScene = "NetTest_Farm";

        /// <summary>Scenes in Build Settings order; missing ones (other teams' not merged yet) are skipped.</summary>
        static readonly string[] BuildScenes =
        {
            ProjectSetup.BootstrapPath, "Assets/Scenes/MainMenu.unity", "Assets/Scenes/Lobby.unity", "Assets/Scenes/Muddlebury.unity",
            TestSceneDir + "/" + TestLobbyScene + ".unity", TestSceneDir + "/" + TestFarmScene + ".unity",
        };

        [MenuItem("Harvest Panic/Setup/Configure Networking")]
        public static void Configure()
        {
            Directory.CreateDirectory(PrefabDir);
            Directory.CreateDirectory(DataDir);
            Directory.CreateDirectory(Path.GetDirectoryName(PrefabListPath));
            Directory.CreateDirectory(TestSceneDir);
            AssetDatabase.Refresh();

            DisableDefaultPrefabGeneration();
            var player = CreatePlaceholderPlayer();
            var list = CreatePrefabList(player);
            var playerRef = CreatePlayerRef(player);
            var nmPrefab = CreateNetworkManagerPrefab();
            CreateConfig(nmPrefab, playerRef);
            CreateTestScene(TestLobbyScene, new Color(0.45f, 0.65f, 0.35f));
            CreateTestScene(TestFarmScene, new Color(0.55f, 0.45f, 0.3f));
            PlaceInBootstrap(nmPrefab);
            SyncBuildScenes();
            AssetDatabase.SaveAssets();
            list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(PrefabListPath);
            Debug.Log($"[NetworkingSetup] Done. prefabs in Networking list={list.PrefabList.Count}");
        }

        static void DisableDefaultPrefabGeneration()
        {
            var s = NetcodeForGameObjectsProjectSettings.instance;
            if (!s.GenerateDefaultNetworkPrefabs) return;
            s.GenerateDefaultNetworkPrefabs = false;
            var save = typeof(NetcodeForGameObjectsProjectSettings).GetMethod("SaveSettings", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            save?.Invoke(s, null);
        }

        static GameObject CreatePlaceholderPlayer()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PlaceholderPlayerPath);
            if (existing != null) return existing;
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "PlaceholderPlayer";
            go.layer = GameLayers.IndexOf(GameLayers.Player);
            go.AddComponent<NetworkObject>();
            var nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(nose.GetComponent<Collider>());
            nose.name = "Facing";
            nose.transform.SetParent(go.transform, false);
            nose.transform.localPosition = new Vector3(0f, 0.5f, 0.45f);
            nose.transform.localScale = new Vector3(0.3f, 0.15f, 0.2f);
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, PlaceholderPlayerPath);
            Object.DestroyImmediate(go);
            return prefab;
        }

        static NetworkPrefabsList CreatePrefabList(GameObject player)
        {
            var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(PrefabListPath);
            if (list == null)
            {
                list = ScriptableObject.CreateInstance<NetworkPrefabsList>();
                AssetDatabase.CreateAsset(list, PrefabListPath);
            }
            if (!list.Contains(player)) list.Add(new NetworkPrefab { Prefab = player });
            EditorUtility.SetDirty(list);
            return list;
        }

        static PlayerPrefabRef CreatePlayerRef(GameObject player)
        {
            var r = AssetDatabase.LoadAssetAtPath<PlayerPrefabRef>(PlayerRefPath);
            if (r == null)
            {
                r = ScriptableObject.CreateInstance<PlayerPrefabRef>();
                r.prefab = player;
                AssetDatabase.CreateAsset(r, PlayerRefPath);
            }
            else if (r.prefab == null) { r.prefab = player; EditorUtility.SetDirty(r); }
            return r;
        }

        static void CreateConfig(GameObject nmPrefab, PlayerPrefabRef playerRef)
        {
            var c = AssetDatabase.LoadAssetAtPath<NetworkingConfig>(ConfigPath);
            if (c == null)
            {
                c = ScriptableObject.CreateInstance<NetworkingConfig>();
                AssetDatabase.CreateAsset(c, ConfigPath);
            }
            c.networkManagerPrefab = nmPrefab;
            c.playerPrefab = playerRef;
            EditorUtility.SetDirty(c);
        }

        static GameObject CreateNetworkManagerPrefab()
        {
            var go = new GameObject("NetworkManager");
            var utp = go.AddComponent<UnityTransport>();
            utp.SetConnectionData("127.0.0.1", 7777, "0.0.0.0");
            var fp = go.AddComponent<FacepunchTransport>();
            var nm = go.AddComponent<NetworkManager>();
            nm.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = utp, TickRate = 30, ConnectionApproval = true, EnableSceneManagement = true,
                ClientConnectionBufferTimeout = 10, ForceSamePrefabs = true,
            };
            nm.NetworkConfig.Prefabs.NetworkPrefabsLists.Clear(); // lists are added at runtime from Resources/NetworkPrefabs
            go.AddComponent<NetworkBootstrap>();
            go.AddComponent<PlayerSpawnService>();
            go.AddComponent<SessionService>();
            go.AddComponent<NetAutomation>();
            var so = new SerializedObject(fp);
            var appId = so.FindProperty("steamAppId");
            if (appId != null) { appId.longValue = 480; so.ApplyModifiedPropertiesWithoutUndo(); }
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, NetworkManagerPrefabPath);
            Object.DestroyImmediate(go);
            return prefab;
        }

        static void CreateTestScene(string name, Color groundColor)
        {
            string path = TestSceneDir + "/" + name + ".unity";
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = Vector3.one * 2f;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader != null)
            {
                var mat = new Material(shader);
                mat.SetColor("_BaseColor", groundColor);
                ground.GetComponent<Renderer>().sharedMaterial = mat;
                string matPath = TestSceneDir + "/" + name + "_Ground.mat";
                AssetDatabase.DeleteAsset(matPath);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            for (int i = 0; i < GameInfo.MaxPlayers; i++)
            {
                var m = new GameObject("PlayerStart_" + i);
                m.transform.position = new Vector3(-3f + i * 2f, 1f, -2f);
                m.AddComponent<LevelMarker>().Configure(MarkerRole.PlayerStart, null, 1, i);
                m.tag = MarkerTag.PlayerStart;
            }
            EditorSceneManager.SaveScene(scene, path);
        }

        static void PlaceInBootstrap(GameObject nmPrefab)
        {
            if (!File.Exists(ProjectSetup.BootstrapPath)) return;
            var scene = EditorSceneManager.OpenScene(ProjectSetup.BootstrapPath, OpenSceneMode.Single);
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.GetComponent<NetworkManager>() == null) continue;
                if (PrefabUtility.GetCorrespondingObjectFromSource(root) == nmPrefab) { EditorSceneManager.SaveScene(scene); return; }
                Object.DestroyImmediate(root);
            }
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(nmPrefab, scene);
            inst.name = "NetworkManager";
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        /// <summary>Puts Bootstrap first, then menu / lobby / farm / net-test scenes that exist; keeps any other scenes after.</summary>
        [MenuItem("Harvest Panic/Setup/Sync Build Scenes")]
        public static void SyncBuildScenes()
        {
            var result = new List<EditorBuildSettingsScene>();
            foreach (var p in BuildScenes)
                if (File.Exists(p)) result.Add(new EditorBuildSettingsScene(p, true));
            foreach (var s in EditorBuildSettings.scenes)
                if (result.All(r => r.path != s.path) && File.Exists(s.path)) result.Add(s);
            EditorBuildSettings.scenes = result.ToArray();
        }
    }
}
