using System.IO;
using HarvestPanic.Core;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace HarvestPanic.UI.EditorTools
{
    /// <summary>
    /// Builds the Frontend scenes (MainMenu with the live farm backdrop, Lobby) and the empty Frontend NetworkPrefabsList from code so they are reproducible:
    /// Unity -batchmode -quit -projectPath HarvestPanic -executeMethod HarvestPanic.UI.EditorTools.UiSceneBuilder.BuildAll
    /// </summary>
    public static class UiSceneBuilder
    {
        const string MatDir = "Assets/Art/UI/Materials";
        static readonly Color Grass = new Color(0.50f, 0.64f, 0.29f), Dirt = new Color(0.66f, 0.54f, 0.35f), Brick = new Color(0.61f, 0.29f, 0.23f),
            Wood = new Color(0.54f, 0.38f, 0.25f), Cream = new Color(0.96f, 0.92f, 0.83f), Hay = new Color(0.91f, 0.73f, 0.29f),
            Leaf = new Color(0.36f, 0.55f, 0.28f), Slate = new Color(0.33f, 0.30f, 0.34f), Sky = new Color(0.62f, 0.82f, 0.93f);

        [MenuItem("HarvestPanic/UI/Build Frontend Scenes")]
        public static void BuildAll()
        {
            Directory.CreateDirectory(MatDir);
            BuildNetworkList();
            BuildMainMenu();
            BuildLobby();
            AssetDatabase.SaveAssets();
            Debug.Log("[UiSceneBuilder] Built MainMenu + Lobby + Frontend.asset");
        }

        /// <summary>Rebuilds only the MainMenu scene (farm backdrop + menu UI): -executeMethod HarvestPanic.UI.EditorTools.UiSceneBuilder.BuildMainMenuOnly</summary>
        [MenuItem("HarvestPanic/UI/Build Main Menu Scene")]
        public static void BuildMainMenuOnly()
        {
            try
            {
                Directory.CreateDirectory(MatDir);
                BuildMainMenu();
                AssetDatabase.SaveAssets();
                Debug.Log("[UiSceneBuilder] Built MainMenu");
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        static void BuildNetworkList()
        {
            const string path = "Assets/Resources/NetworkPrefabs/Frontend.asset";
            Directory.CreateDirectory("Assets/Resources/NetworkPrefabs");
            if (AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(path) != null) return;
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<NetworkPrefabsList>(), path); // Frontend has no networked prefabs
        }

        // ------------------------------------------------------------------ MainMenu
        /// <summary>
        /// MainMenu = the real farm as a live backdrop: Muddlebury's visual groups are moved into the menu scene (prefab links
        /// kept, the source scene is never saved), every gameplay script, rigidbody, NetworkObject and audio source is stripped
        /// (only cosmetic World components stay: instanced forest, windmill spinner, water bobbers, sign text), Muddlebury's
        /// lighting is copied, a few critter models wander (MenuCritter) and MenuBackdrop eases the camera between one framing
        /// per menu screen. Re-run after map changes.
        /// </summary>
        static void BuildMainMenu()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var farm = MenuBackdropContent.ImportFarm(scene);
            var cam = MakeCamera("Menu Camera", MenuBackdropContent.Shots[0].position, Quaternion.Euler(MenuBackdropContent.Shots[0].euler), MenuBackdropContent.Shots[0].fov);
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.farClipPlane = 900f;
            var camData = cam.GetComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            if (!farm) // Muddlebury missing: fall back to the old primitive diorama so the menu still has a backdrop.
            {
                SetupLighting(new Color(0.62f, 0.78f, 0.9f));
                MakeSun(Quaternion.Euler(38f, -50f, 0f), 1.5f);
                var root = new GameObject("Backdrop").transform;
                Block(root, PrimitiveType.Plane, "Ground", new Vector3(0, 0, 0), new Vector3(12, 1, 12), Grass);
                Barn(root, new Vector3(14f, 0, 14f), 22f);
            }
            var rig = new GameObject("Menu Backdrop");
            rig.AddComponent<MenuBackdrop>().Configure(cam, MenuBackdropContent.Shots);
            if (farm) MenuBackdropContent.AddCritters(rig.transform);

            var ui = new GameObject("MainMenu UI");
            ui.AddComponent<MainMenuController>();
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/MainMenu.unity");
        }

        // ------------------------------------------------------------------ Lobby
        static void BuildLobby()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SetupLighting(new Color(0.62f, 0.78f, 0.9f));
            MakeSun(Quaternion.Euler(48f, -35f, 0f), 1.6f);
            var root = new GameObject("LobbyYard").transform;

            var ground = Block(root, PrimitiveType.Plane, "Ground", Vector3.zero, new Vector3(4f, 1, 4f), Grass);
            Block(root, PrimitiveType.Cube, "DirtPath", new Vector3(0, 0.01f, 4f), new Vector3(5f, 0.04f, 22f), Dirt);
            // Farmhouse + porch (porch is the ready zone)
            Block(root, PrimitiveType.Cube, "Farmhouse", new Vector3(0, 2.2f, 17f), new Vector3(12f, 4.4f, 6f), Cream);
            Roof(root, new Vector3(0, 4.4f, 17f), 13.5f, 7f, Slate);
            Block(root, PrimitiveType.Cube, "Door", new Vector3(0, 1.2f, 13.95f), new Vector3(1.6f, 2.4f, 0.15f), Wood);
            var porch = Block(root, PrimitiveType.Cube, "PorchDeck", new Vector3(0, 0.15f, 11.6f), new Vector3(8f, 0.3f, 4.2f), Wood);
            foreach (float x in new[] { -3.7f, 3.7f })
                Block(root, PrimitiveType.Cube, "PorchPost", new Vector3(x, 1.6f, 9.7f), new Vector3(0.25f, 3.2f, 0.25f), Wood);
            Block(root, PrimitiveType.Cube, "PorchRoof", new Vector3(0, 3.3f, 11.9f), new Vector3(8.6f, 0.2f, 4.9f), Slate);
            var zone = new GameObject("PorchZone") { layer = LayerMask.NameToLayer(GameLayers.Interactable) };
            zone.transform.SetParent(root, false);
            zone.transform.position = new Vector3(0, 0.8f, 11.6f);
            var box = zone.AddComponent<BoxCollider>(); box.isTrigger = true; box.size = new Vector3(7.4f, 1.6f, 3.8f);
            var porchZone = zone.AddComponent<PorchZone>();
            var lamp = Block(root, PrimitiveType.Sphere, "PorchLight", new Vector3(0, 2.9f, 10.2f), Vector3.one * 0.5f, Cream);
            lamp.GetComponent<Collider>().enabled = false;
            Wire(porchZone, "lightRenderer", lamp.GetComponent<Renderer>());
            var sign = Block(root, PrimitiveType.Cube, "ReadySign", new Vector3(0, 2.3f, 9.75f), new Vector3(2.4f, 0.6f, 0.08f), Hay);
            sign.GetComponent<Collider>().enabled = false;

            // Barn on the left
            Barn(root, new Vector3(-18f, 0, 8f), -70f);
            // Yard props
            for (int i = 0; i < 6; i++)
            {
                var bale = Block(root, PrimitiveType.Cube, "HayBale" + i, new Vector3(6f + (i % 3) * 1.3f, 0.45f + (i / 3) * 0.9f, 2f), new Vector3(1.2f, 0.9f, 0.9f), Hay);
                bale.AddComponent<Rigidbody>().mass = 12f;
            }
            var scale = Block(root, PrimitiveType.Cylinder, "FriendScale", new Vector3(-6f, 0.1f, 3f), new Vector3(3f, 0.1f, 3f), Slate);
            Block(root, PrimitiveType.Cube, "ScaleDial", new Vector3(-6f, 1.4f, 4.7f), new Vector3(1.6f, 1.2f, 0.2f), Cream);
            Block(root, PrimitiveType.Cube, "Mirror", new Vector3(-9.5f, 1.5f, 10f), new Vector3(1.4f, 2.6f, 0.1f), Sky);
            var wardrobe = Block(root, PrimitiveType.Cube, "Wardrobe", new Vector3(9.5f, 1.3f, 9.5f), new Vector3(1.8f, 2.6f, 0.9f), Wood);
            wardrobe.AddComponent<LevelMarker>().Configure(MarkerRole.Wardrobe);

            var post = Block(root, PrimitiveType.Cube, "BellPost", new Vector3(4.6f, 1.2f, 8.2f), new Vector3(0.25f, 2.4f, 0.25f), Wood);
            var bell = Block(root, PrimitiveType.Sphere, "DinnerBell", new Vector3(4.6f, 2.3f, 8.2f), new Vector3(0.7f, 0.7f, 0.7f), Hay);
            bell.GetComponent<Collider>().enabled = false;
            var bellPivot = new GameObject("BellPivot").transform;
            bellPivot.SetParent(post.transform.parent, false); bellPivot.position = new Vector3(4.6f, 2.6f, 8.2f);
            bell.transform.SetParent(bellPivot, true);
            var db = post.AddComponent<DinnerBell>(); Wire(db, "bell", bellPivot);

            Fence(root, new Vector3(-19f, 0, -19f), 20, new Vector3(2f, 0, 0));
            Fence(root, new Vector3(-19f, 0, 19f), 7, new Vector3(2f, 0, 0));
            for (int i = 0; i < 8; i++) Tree(root, new Vector3(-23f + i * 6.5f, 0, 24f));

            for (int i = 0; i < 4; i++)
            {
                var start = new GameObject("PlayerStart" + i);
                start.transform.SetParent(root, false);
                start.transform.position = new Vector3(-3f + i * 2f, 0.1f, -8f);
                start.transform.rotation = Quaternion.LookRotation(Vector3.forward);
                start.AddComponent<LevelMarker>().Configure(MarkerRole.PlayerStart, null, 1, i);
            }

            // Overview camera + fallback walker
            var cam = MakeCamera("Lobby Camera", new Vector3(0f, 2.6f, -9f), Quaternion.Euler(10f, 0f, 0f), 70f);
            var walker = new GameObject("FallbackWalker") { layer = 0 };
            walker.transform.position = new Vector3(0f, 0.1f, -8f);
            var cc = walker.AddComponent<CharacterController>(); cc.height = 1.8f; cc.radius = 0.35f; cc.center = new Vector3(0, 0.9f, 0);
            var head = new GameObject("Head"); head.transform.SetParent(walker.transform, false); head.transform.localPosition = new Vector3(0, 1.65f, 0);
            var hc = head.AddComponent<Camera>(); hc.fieldOfView = 90f; hc.nearClipPlane = 0.05f; head.tag = "MainCamera";
            head.AddComponent<AudioListener>(); head.AddComponent<UniversalAdditionalCameraData>();
            var w = walker.AddComponent<LobbyFallbackWalker>(); Wire(w, "head", head.transform);
            walker.SetActive(false);

            var ui = new GameObject("Lobby UI");
            var lc = ui.AddComponent<LobbyController>();
            Wire(lc, "fallbackWalker", w); Wire(lc, "lobbyCamera", cam);
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/Lobby.unity");
        }

        // ------------------------------------------------------------------ helpers
        static void SetupLighting(Color sky)
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.76f, 0.92f);
            RenderSettings.ambientEquatorColor = new Color(0.58f, 0.60f, 0.50f);
            RenderSettings.ambientGroundColor = new Color(0.34f, 0.28f, 0.22f);
            RenderSettings.fog = false;
        }

        static Camera MakeCamera(string name, Vector3 pos, Quaternion rot, float fov)
        {
            var go = new GameObject(name) { tag = "MainCamera" };
            go.transform.SetPositionAndRotation(pos, rot);
            var c = go.AddComponent<Camera>();
            c.fieldOfView = fov; c.clearFlags = CameraClearFlags.SolidColor; c.backgroundColor = Sky; c.nearClipPlane = 0.1f; c.farClipPlane = 300f;
            go.AddComponent<AudioListener>();
            go.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = false;
            return c;
        }

        static void MakeSun(Quaternion rot, float intensity)
        {
            var l = new GameObject("Sun").AddComponent<Light>();
            l.type = LightType.Directional; l.intensity = intensity; l.color = new Color(1f, 0.94f, 0.82f); l.shadows = LightShadows.Soft;
            l.transform.rotation = rot;
        }

        static GameObject Block(Transform parent, PrimitiveType type, string name, Vector3 pos, Vector3 scale, Color color)
        {
            var g = GameObject.CreatePrimitive(type);
            g.name = name; g.transform.SetParent(parent, false);
            g.transform.position = pos; g.transform.localScale = scale;
            g.GetComponent<Renderer>().sharedMaterial = Mat(color);
            return g;
        }

        static Material Mat(Color c)
        {
            string name = "HP_UI_" + ColorUtility.ToHtmlStringRGB(c);
            string path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", 0.1f);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static void Barn(Transform parent, Vector3 pos, float yaw)
        {
            var b = new GameObject("Barn").transform; b.SetParent(parent, false); b.position = pos; b.rotation = Quaternion.Euler(0, yaw, 0);
            Block(b, PrimitiveType.Cube, "BarnBody", pos + b.up * 3f, new Vector3(10f, 6f, 8f), Brick).transform.rotation = b.rotation;
            var roof = Block(b, PrimitiveType.Cube, "BarnRoof", pos + b.up * 6.9f, new Vector3(10.6f, 0.4f, 10f), Slate);
            roof.transform.rotation = b.rotation;
            var door = Block(b, PrimitiveType.Cube, "BarnDoor", pos + b.up * 2f + b.forward * -4.05f, new Vector3(3.6f, 4f, 0.15f), Cream);
            door.transform.rotation = b.rotation;
        }

        static void Roof(Transform parent, Vector3 pos, float w, float d, Color c)
        {
            var l = Block(parent, PrimitiveType.Cube, "RoofL", pos + new Vector3(-w * 0.2f, 0.7f, 0), new Vector3(w * 0.6f, 0.3f, d), c);
            l.transform.rotation = Quaternion.Euler(0, 0, 18f);
            var r = Block(parent, PrimitiveType.Cube, "RoofR", pos + new Vector3(w * 0.2f, 0.7f, 0), new Vector3(w * 0.6f, 0.3f, d), c);
            r.transform.rotation = Quaternion.Euler(0, 0, -18f);
        }

        static void Fence(Transform parent, Vector3 start, int count, Vector3 step)
        {
            for (int i = 0; i < count; i++)
            {
                var p = start + step * i;
                Block(parent, PrimitiveType.Cube, "Post", p + Vector3.up * 0.6f, new Vector3(0.18f, 1.2f, 0.18f), Wood);
                if (i < count - 1)
                {
                    var rail = Block(parent, PrimitiveType.Cube, "Rail", p + step * 0.5f + Vector3.up * 0.8f, new Vector3(Mathf.Max(step.magnitude, 0.1f), 0.12f, 0.08f), Wood);
                    rail.transform.rotation = Quaternion.LookRotation(Vector3.Cross(step.normalized, Vector3.up)) ;
                    rail.transform.rotation = Quaternion.FromToRotation(Vector3.right, step.normalized);
                }
            }
        }

        static void Tree(Transform parent, Vector3 pos)
        {
            Block(parent, PrimitiveType.Cylinder, "Trunk", pos + Vector3.up * 1.4f, new Vector3(0.6f, 1.4f, 0.6f), Wood);
            Block(parent, PrimitiveType.Sphere, "Crown", pos + Vector3.up * 4f, new Vector3(3.6f, 3.2f, 3.6f), Leaf);
        }

        static void Wire(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
