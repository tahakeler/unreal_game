using System.IO;
using HarvestPanic.Core;
using Unity.Cinemachine;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HarvestPanic.Gameplay.Editor
{
    /// <summary>
    /// Idempotent generator for every Player-team asset: tuning assets, placeholder materials, the networked
    /// Player prefab (first-person rig + third-person placeholder farmer), tool prefabs (feed bucket, net),
    /// test props, <c>Resources/NetworkPrefabs/Player.asset</c>, the Networking PlayerPrefabRef pointer and the
    /// dev scene <c>Scenes/Dev/PlayerTest.unity</c>.
    /// Batchmode: Unity -batchmode -nographics -quit -projectPath HarvestPanic -executeMethod HarvestPanic.Gameplay.Editor.PlayerAssetBuilder.BuildAll
    /// </summary>
    public static class PlayerAssetBuilder
    {
        const string PlayerDir = "Assets/Prefabs/Player";
        const string ToolsDir = "Assets/Prefabs/Tools";
        const string TuningDir = PlayerDir + "/Tuning";
        const string MatDir = PlayerDir + "/Materials";
        const string TestDir = PlayerDir + "/Test";
        public const string PlayerPrefabPath = PlayerDir + "/Player.prefab";
        public const string ScenePath = "Assets/Scenes/Dev/PlayerTest.unity";
        const string ListPath = "Assets/Resources/NetworkPrefabs/Player.asset";
        const string PrefabRefPath = "Assets/Resources/Data/Networking/PlayerPrefabRef.asset";
        public const ushort TestPort = 7791;

        [MenuItem("Harvest Panic/Player/Build Player Assets")]
        public static void BuildAll()
        {
            foreach (var d in new[] { PlayerDir, ToolsDir, TuningDir, MatDir, TestDir, PlayerDir + "/Resources/HarvestPanic", "Assets/Scenes/Dev", "Assets/Resources/NetworkPrefabs" })
                EnsureFolder(d);

            var pt = LoadOrCreate<PlayerTuning>(TuningDir + "/PlayerTuning.asset");
            var ct = LoadOrCreate<CarryTuning>(TuningDir + "/CarryTuning.asset");
            LoadOrCreate<NoiseTuning>(PlayerDir + "/Resources/HarvestPanic/NoiseTuning.asset");
            var tt = LoadOrCreate<ToolTuning>(ToolsDir + "/ToolTuning.asset");

            var player = BuildPlayer(pt, ct);
            var bucket = BuildFeedBucket(tt);
            var net = BuildNet(tt);
            var crate = BuildCrate();
            var turnip = BuildTurnip();

            var list = LoadOrCreate<NetworkPrefabsList>(ListPath);
            var so = new SerializedObject(list);
            var arr = so.FindProperty("List");
            arr.ClearArray();
            so.ApplyModifiedPropertiesWithoutUndo();
            foreach (var p in new[] { player, bucket, net, crate, turnip }) list.Add(new NetworkPrefab { Prefab = p });
            EditorUtility.SetDirty(list);

            var prefabRef = AssetDatabase.LoadAssetAtPath<ScriptableObject>(PrefabRefPath);
            if (prefabRef != null)
            {
                var rso = new SerializedObject(prefabRef);
                rso.FindProperty("prefab").objectReferenceValue = player;
                rso.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(prefabRef);
            }

            AssetDatabase.SaveAssets();
            BuildScene(player, new[] { crate, turnip, bucket, net });
            AssetDatabase.SaveAssets();
            Debug.Log("[PlayerAssetBuilder] Done.");
        }

        /// <summary>Rebuilds only the feed bucket + net prefabs (same paths/GUIDs; Player prefab and scene untouched).</summary>
        [MenuItem("Harvest Panic/Player/Rebuild Tool Prefabs")]
        public static void BuildToolPrefabs()
        {
            EnsureFolder(ToolsDir); EnsureFolder(MatDir);
            var tt = LoadOrCreate<ToolTuning>(ToolsDir + "/ToolTuning.asset");
            BuildFeedBucket(tt);
            BuildNet(tt);
            AssetDatabase.SaveAssets();
            Debug.Log("[PlayerAssetBuilder] Tool prefabs rebuilt.");
        }

        /// <summary>Owner's Tripo3D mesh (real size; bucket base-centre pivot, net grip-end pivot along +Z), or null.</summary>
        static GameObject TripoModel(string name, Transform parent, Vector3 localPos)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/ThirdParty/Tripo/{name}/{name}.fbx");
            if (src == null) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(src);
            go.name = "Tripo_" + name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = parent.gameObject.layer;
            return go;
        }

        static Bounds LocalBounds(Transform space, GameObject go)
        {
            bool any = false; var b = new Bounds();
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
                foreach (var v in mf.sharedMesh.vertices)
                {
                    var p = space.InverseTransformPoint(mf.transform.TransformPoint(v));
                    if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p);
                }
            return b;
        }

        // ---- Player ------------------------------------------------------------------------------------------

        static GameObject BuildPlayer(PlayerTuning pt, CarryTuning ct)
        {
            var root = new GameObject("Player");
            root.layer = LayerMask.NameToLayer(GameLayers.Player);
            var cc = root.AddComponent<CharacterController>();
            cc.height = pt.StandHeight; cc.radius = pt.Radius; cc.center = new Vector3(0f, pt.StandHeight * 0.5f, 0f);
            cc.stepOffset = 0.35f; cc.slopeLimit = 50f; cc.skinWidth = 0.04f; cc.minMoveDistance = 0f; // high frame rates move < 1 mm per frame
            var rag = root.AddComponent<CapsuleCollider>();
            rag.height = pt.StandHeight; rag.radius = pt.Radius; rag.center = cc.center; rag.enabled = false;
            var rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true; rb.mass = pt.MassKg; rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            root.AddComponent<NetworkObject>();
            var nt = root.AddComponent<NetworkTransform>();
            nt.AuthorityMode = NetworkTransform.AuthorityModes.Owner;
            nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false;
            nt.Interpolate = true;

            var input = root.AddComponent<PlayerInputReader>();
            Set(input, "actions", AssetDatabase.LoadAssetAtPath<InputActionAsset>(PlayerDir + "/Input/PlayerControls.inputactions"));
            root.AddComponent<PlayerMotor>();
            root.AddComponent<PlayerCarry>();

            // Head / camera rig.
            var head = Child(root.transform, "HeadPivot", new Vector3(0f, pt.StandHeight * 0.5f + pt.EyeAboveCentre, 0f));
            var camPivot = Child(head, "CameraPivot", Vector3.zero);
            var fpCam = Child(camPivot, "FirstPersonCamera", Vector3.zero);
            var vcam = fpCam.gameObject.AddComponent<CinemachineCamera>();
            var lens = vcam.Lens; lens.FieldOfView = pt.BaseFov; lens.NearClipPlane = 0.03f; lens.FarClipPlane = 600f; vcam.Lens = lens;

            var viewCam = Child(root.transform, "ViewCamera", head.localPosition);
            viewCam.tag = "MainCamera";
            var cam = viewCam.gameObject.AddComponent<Camera>();
            cam.nearClipPlane = 0.03f; cam.fieldOfView = pt.BaseFov; cam.depth = 10;
            viewCam.gameObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
            viewCam.gameObject.AddComponent<CinemachineBrain>();
            viewCam.gameObject.AddComponent<AudioListener>();
            viewCam.gameObject.SetActive(false);

            // First-person arms (owner only): procedural rig, see FirstPersonArmsRig.
            var arms = Child(camPivot, "FirstPersonArms", Vector3.zero);
            var sleeve = Mat("Sleeve", new Color(0.72f, 0.2f, 0.16f));
            var skin = Mat("Skin", new Color(0.93f, 0.72f, 0.56f));
            var glove = Mat("Glove", new Color(0.55f, 0.38f, 0.2f));

            // Customization's FarmerBody (no NetworkObject of its own) is the third-person body when present.
            var farmerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Farmer/FarmerBody.prefab");
            Transform farmer = null;
            if (farmerPrefab != null)
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(farmerPrefab, root.transform);
                inst.name = "FarmerBody";
                inst.transform.localPosition = Vector3.zero;
                inst.transform.localRotation = Quaternion.identity;
                farmer = inst.transform;
            }

            // Placeholder farmer: fallback body when FarmerBody is missing (others see it; owner sees only its shadow).
            var body = Child(root.transform, "Body", Vector3.zero);
            var animator = body.gameObject.AddComponent<Animator>();
            var overalls = Mat("Overalls", new Color(0.22f, 0.36f, 0.62f));
            var denim = Mat("Denim", new Color(0.17f, 0.24f, 0.4f));
            var straw = Mat("Straw", new Color(0.9f, 0.78f, 0.42f));
            var boot = Mat("Boot", new Color(0.3f, 0.2f, 0.12f));
            var dark = Mat("Eye", new Color(0.05f, 0.05f, 0.05f));
            var torso = Prim(PrimitiveType.Capsule, "Torso", body, new Vector3(0f, 1.15f, 0f), new Vector3(0.56f, 0.45f, 0.38f), overalls);
            Prim(PrimitiveType.Cube, "Bib", body, new Vector3(0f, 1.3f, 0.17f), new Vector3(0.32f, 0.3f, 0.06f), overalls);
            Prim(PrimitiveType.Sphere, "Head", body, new Vector3(0f, 1.72f, 0f), Vector3.one * 0.34f, skin);
            Prim(PrimitiveType.Sphere, "Nose", body, new Vector3(0f, 1.71f, 0.17f), Vector3.one * 0.07f, skin);
            Prim(PrimitiveType.Sphere, "EyeL", body, new Vector3(-0.07f, 1.77f, 0.15f), Vector3.one * 0.045f, dark);
            Prim(PrimitiveType.Sphere, "EyeR", body, new Vector3(0.07f, 1.77f, 0.15f), Vector3.one * 0.045f, dark);
            Prim(PrimitiveType.Cylinder, "HatBrim", body, new Vector3(0f, 1.86f, 0f), new Vector3(0.56f, 0.012f, 0.56f), straw);
            Prim(PrimitiveType.Cylinder, "HatCrown", body, new Vector3(0f, 1.93f, 0f), new Vector3(0.3f, 0.07f, 0.3f), straw);
            var headAttach = Child(body, "HeadAttach", new Vector3(0f, 1.88f, 0f));
            Transform Limb(string n, Vector3 pivot, Vector3 scale, Material m, float drop)
            {
                var p = Child(body, n, pivot);
                Prim(PrimitiveType.Capsule, n + "Mesh", p, new Vector3(0f, -drop, 0f), scale, m);
                return p;
            }
            var armL = Limb("ArmL", new Vector3(-0.34f, 1.48f, 0f), new Vector3(0.13f, 0.3f, 0.13f), sleeve, 0.28f);
            var armR = Limb("ArmR", new Vector3(0.34f, 1.48f, 0f), new Vector3(0.13f, 0.3f, 0.13f), sleeve, 0.28f);
            Prim(PrimitiveType.Sphere, "HandMeshL", armL, new Vector3(0f, -0.6f, 0f), Vector3.one * 0.12f, glove);
            Prim(PrimitiveType.Sphere, "HandMeshR", armR, new Vector3(0f, -0.6f, 0f), Vector3.one * 0.12f, glove);
            var legL = Limb("LegL", new Vector3(-0.13f, 0.78f, 0f), new Vector3(0.18f, 0.38f, 0.18f), denim, 0.38f);
            var legR = Limb("LegR", new Vector3(0.13f, 0.78f, 0f), new Vector3(0.18f, 0.38f, 0.18f), denim, 0.38f);
            Prim(PrimitiveType.Cube, "BootL", legL, new Vector3(0f, -0.72f, 0.05f), new Vector3(0.17f, 0.12f, 0.28f), boot);
            Prim(PrimitiveType.Cube, "BootR", legR, new Vector3(0f, -0.72f, 0.05f), new Vector3(0.17f, 0.12f, 0.28f), boot);

            if (farmer != null) Object.DestroyImmediate(body.gameObject);
            var pc = root.AddComponent<PlayerCharacter>();
            Set(pc, "tuning", pt); Set(pc, "carryTuning", ct);
            Set(pc, "headPivot", head); Set(pc, "cameraPivot", camPivot);
            Set(pc, "viewCamera", viewCam.gameObject); Set(pc, "firstPersonCamera", fpCam.gameObject);
            Set(pc, "firstPersonArms", arms); Set(pc, "ragdollCollider", rag);
            if (farmer != null) Set(pc, "bodyRoot", farmer);
            else
            {
                Set(pc, "bodyRoot", body);
                Set(pc, "bodyRenderer", torso.GetComponent<Renderer>()); Set(pc, "bodyAnimator", animator);
                Set(pc, "headAttachPoint", headAttach);
            }

            var view = root.AddComponent<PlayerView>();
            Set(view, "vcam", vcam);
            if (farmer == null) { Set(view, "bodyArmL", armL); Set(view, "bodyArmR", armR); Set(view, "bodyLegL", legL); Set(view, "bodyLegR", legR); }
            root.AddComponent<PlayerPromptOverlay>();
            ConfigureFirstPersonPresentation(root);

            return Save(root, PlayerPrefabPath);
        }

        /// <summary>
        /// Upgrades only Player.prefab in place (FP arms rig + UI Toolkit centre HUD), leaving tools/props untouched.
        /// Batchmode: -executeMethod HarvestPanic.Gameplay.Editor.PlayerAssetBuilder.UpgradePlayerPrefab
        /// </summary>
        [MenuItem("Harvest Panic/Player/Upgrade Player Prefab (arms + centre HUD)")]
        public static void UpgradePlayerPrefab()
        {
            var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                ConfigureFirstPersonPresentation(root);
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("[PlayerAssetBuilder] Player prefab upgraded.");
        }

        static void ConfigureFirstPersonPresentation(GameObject root)
        {
            var arms = root.GetComponent<PlayerCharacter>() != null ? (Transform)new SerializedObject(root.GetComponent<PlayerCharacter>()).FindProperty("firstPersonArms").objectReferenceValue : null;
            if (arms == null) { Debug.LogError("[PlayerAssetBuilder] Player has no FirstPersonArms"); return; }
            for (int i = arms.childCount - 1; i >= 0; i--) Object.DestroyImmediate(arms.GetChild(i).gameObject);
            var rig = arms.GetComponent<FirstPersonArmsRig>();
            if (rig == null) rig = arms.gameObject.AddComponent<FirstPersonArmsRig>();
            Set(rig, "tuning", LoadOrCreate<ArmsTuning>(TuningDir + "/ArmsTuning.asset"));
            Set(rig, "baseMaterial", Mat("Sleeve", new Color(0.72f, 0.2f, 0.16f)));

            var overlay = root.GetComponent<PlayerPromptOverlay>();
            if (overlay == null) overlay = root.AddComponent<PlayerPromptOverlay>();
            Set(overlay, "panelSettings", AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.PanelSettings>("Assets/Prefabs/HUD/EconomyHudPanel.asset"));
            Set(overlay, "styleSheet", AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.StyleSheet>(PlayerDir + "/UI/PlayerCentreHud.uss"));
            Set(overlay, "headlineFont", AssetDatabase.LoadAssetAtPath<Font>("Assets/ThirdParty/Fonts/LilitaOne/LilitaOne-Regular.ttf"));
            Set(overlay, "bodyFont", AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/UI/Fonts/Nunito-ExtraBold.ttf"));
        }

        // ---- Tools and props ----------------------------------------------------------------------------------

        static GameObject NetProp(string name, out Transform visual)
        {
            var root = new GameObject(name);
            root.layer = LayerMask.NameToLayer(GameLayers.Carryable);
            var rb = root.AddComponent<Rigidbody>();
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            root.AddComponent<NetworkObject>();
            var nt = root.AddComponent<NetworkTransform>();
            nt.AuthorityMode = NetworkTransform.AuthorityModes.Server;
            nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false;
            root.AddComponent<NetworkRigidbody>();
            visual = Child(root.transform, "Visual", Vector3.zero);
            return root;
        }

        static GameObject BuildFeedBucket(ToolTuning tt)
        {
            var root = NetProp("FeedBucket", out var v);
            var box = root.AddComponent<BoxCollider>(); box.size = new Vector3(0.32f, 0.36f, 0.32f); box.center = new Vector3(0f, 0.18f, 0f);
            var tripo = TripoModel("FeedBucket", v, Vector3.zero);
            if (tripo != null)
            {
                var b = LocalBounds(root.transform, tripo);
                box.size = b.size; box.center = b.center;
                var tool0 = root.AddComponent<FeedBucketTool>();
                tool0.Configure(1, 3f, true);
                Set(tool0, "tuning", tt);
                return Save(root, ToolsDir + "/FeedBucket.prefab");
            }
            var metal = Mat("BucketMetal", new Color(0.62f, 0.66f, 0.7f), 0.6f, 0.5f);
            var feed = Mat("Feed", new Color(0.93f, 0.76f, 0.25f));
            Prim(PrimitiveType.Cylinder, "Pail", v, new Vector3(0f, 0.16f, 0f), new Vector3(0.3f, 0.16f, 0.3f), metal);
            Prim(PrimitiveType.Cylinder, "Feed", v, new Vector3(0f, 0.32f, 0f), new Vector3(0.27f, 0.01f, 0.27f), feed);
            Prim(PrimitiveType.Cube, "HandleTop", v, new Vector3(0f, 0.46f, 0f), new Vector3(0.3f, 0.015f, 0.015f), metal);
            Prim(PrimitiveType.Cube, "HandleL", v, new Vector3(-0.15f, 0.39f, 0f), new Vector3(0.015f, 0.14f, 0.015f), metal);
            Prim(PrimitiveType.Cube, "HandleR", v, new Vector3(0.15f, 0.39f, 0f), new Vector3(0.015f, 0.14f, 0.015f), metal);
            var tool = root.AddComponent<FeedBucketTool>();
            tool.Configure(1, 3f, true);
            Set(tool, "tuning", tt);
            return Save(root, ToolsDir + "/FeedBucket.prefab");
        }

        static GameObject BuildNet(ToolTuning tt)
        {
            var root = NetProp("Net", out var v);
            var box = root.AddComponent<BoxCollider>(); box.size = new Vector3(0.5f, 0.5f, 1.5f); box.center = new Vector3(0f, 0f, 0.45f);
            // Grip end sits 0.25 m behind the hold point (same reach as the placeholder pole), hoop forward on +Z.
            var tripo = TripoModel("Net", v, new Vector3(0f, 0f, -0.25f));
            if (tripo != null)
            {
                var b = LocalBounds(root.transform, tripo);
                box.size = b.size; box.center = b.center;
                // Bag point: hoop/pocket centre (front 30% of the length, mid-height of the pocket).
                var bp = Child(root.transform, "BagPoint", new Vector3(b.center.x, b.center.y, b.max.z - b.size.z * 0.2f));
                var tool0 = root.AddComponent<NetTool>();
                tool0.Configure(1, 2f, true);
                Set(tool0, "tuning", tt); Set(tool0, "bagPoint", bp);
                return Save(root, ToolsDir + "/Net.prefab");
            }
            var wood = Mat("NetPole", new Color(0.55f, 0.4f, 0.24f));
            var rope = Mat("NetRope", new Color(0.92f, 0.9f, 0.82f));
            var pole = Prim(PrimitiveType.Cylinder, "Pole", v, new Vector3(0f, 0f, 0.25f), new Vector3(0.04f, 0.55f, 0.04f), wood);
            pole.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            for (int i = 0; i < 14; i++)
            {
                float a = i / 14f * Mathf.PI * 2f;
                var seg = Prim(PrimitiveType.Cube, "Hoop" + i, v, new Vector3(Mathf.Cos(a) * 0.22f, Mathf.Sin(a) * 0.22f, 0.85f), new Vector3(0.03f, 0.1f, 0.03f), wood);
                seg.transform.localRotation = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg);
            }
            var bag = Prim(PrimitiveType.Sphere, "Bag", v, new Vector3(0f, -0.05f, 1.05f), new Vector3(0.4f, 0.4f, 0.45f), rope);
            var bagPoint = Child(root.transform, "BagPoint", new Vector3(0f, -0.05f, 1.05f));
            var tool = root.AddComponent<NetTool>();
            tool.Configure(1, 2f, true);
            Set(tool, "tuning", tt); Set(tool, "bagPoint", bagPoint);
            return Save(root, ToolsDir + "/Net.prefab");
        }

        static GameObject BuildCrate()
        {
            var root = NetProp("TestCrate", out var v);
            var box = root.AddComponent<BoxCollider>(); box.size = Vector3.one * 0.6f;
            var wood = Mat("CrateWood", new Color(0.78f, 0.56f, 0.32f));
            var slat = Mat("CrateSlat", new Color(0.55f, 0.36f, 0.18f));
            Prim(PrimitiveType.Cube, "Box", v, Vector3.zero, Vector3.one * 0.6f, wood);
            foreach (float z in new[] { -0.305f, 0.305f })
            {
                Prim(PrimitiveType.Cube, "SlatH", v, new Vector3(0f, 0f, z), new Vector3(0.6f, 0.08f, 0.02f), slat);
                var d = Prim(PrimitiveType.Cube, "SlatD", v, new Vector3(0f, 0f, z), new Vector3(0.8f, 0.07f, 0.02f), slat);
                d.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            }
            foreach (float x in new[] { -0.305f, 0.305f })
                Prim(PrimitiveType.Cube, "SlatSide", v, new Vector3(x, 0f, 0f), new Vector3(0.02f, 0.08f, 0.6f), slat);
            root.AddComponent<CarryableProp>().Configure(2, 12f, false);
            return Save(root, TestDir + "/TestCrate.prefab");
        }

        static GameObject BuildTurnip()
        {
            var root = NetProp("TestTurnip", out var v);
            var sph = root.AddComponent<SphereCollider>(); sph.radius = 0.13f;
            Prim(PrimitiveType.Sphere, "Bulb", v, Vector3.zero, Vector3.one * 0.26f, Mat("Turnip", new Color(0.72f, 0.45f, 0.78f)));
            Prim(PrimitiveType.Capsule, "Leaves", v, new Vector3(0f, 0.18f, 0f), new Vector3(0.06f, 0.08f, 0.06f), Mat("Leaf", new Color(0.3f, 0.65f, 0.25f)));
            root.AddComponent<CarryableProp>().Configure(1, 2f, false);
            return Save(root, TestDir + "/TestTurnip.prefab");
        }

        // ---- Scene ---------------------------------------------------------------------------------------------

        static void BuildScene(GameObject player, GameObject[] props)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/HP_DefaultVolumeProfile.asset");
            if (profile != null) { var vol = new GameObject("GlobalVolume").AddComponent<Volume>(); vol.isGlobal = true; vol.sharedProfile = profile; }

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.6f; sun.shadows = LightShadows.Soft;
            sun.color = new Color(1f, 0.95f, 0.85f);
            sun.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.7f, 0.9f);
            RenderSettings.ambientEquatorColor = new Color(0.5f, 0.55f, 0.45f);
            RenderSettings.ambientGroundColor = new Color(0.3f, 0.25f, 0.2f);

            var env = new GameObject("Environment").transform;
            Prim(PrimitiveType.Plane, "Ground", env, Vector3.zero, new Vector3(8f, 1f, 8f), Mat("Grass", new Color(0.36f, 0.6f, 0.26f)), true);
            var ramp = Prim(PrimitiveType.Cube, "Ramp30", env, new Vector3(8f, 1.0f, 6f), new Vector3(4f, 0.3f, 8f), Mat("Dirt", new Color(0.5f, 0.38f, 0.25f)), true);
            ramp.transform.rotation = Quaternion.Euler(-30f, 0f, 0f);
            Prim(PrimitiveType.Cube, "Fence", env, new Vector3(-4f, 0.5f, 10f), new Vector3(6f, 1f, 0.15f), Mat("FenceWood", new Color(0.6f, 0.45f, 0.3f)), true);
            Prim(PrimitiveType.Cube, "Barn", env, new Vector3(-10f, 2f, -8f), new Vector3(6f, 4f, 5f), Mat("BarnRed", new Color(0.7f, 0.15f, 0.12f)), true);

            var river = Prim(PrimitiveType.Cube, "River", env, new Vector3(0f, -0.4f, 24f), new Vector3(40f, 1f, 6f), Mat("Water", new Color(0.2f, 0.45f, 0.75f)), true);
            river.layer = LayerMask.NameToLayer(GameLayers.Water);
            river.GetComponent<Collider>().isTrigger = true;
            river.tag = MarkerTag.River;

            var barnMarker = new GameObject("BarnRespawn");
            barnMarker.transform.SetPositionAndRotation(new Vector3(-6f, 0.05f, -4f), Quaternion.Euler(0f, 45f, 0f));
            barnMarker.AddComponent<LevelMarker>().Configure(MarkerRole.BarnRespawn);
            barnMarker.tag = MarkerTag.BarnRespawn;

            var nmGo = new GameObject("NetworkManager");
            var utp = nmGo.AddComponent<UnityTransport>();
            utp.SetConnectionData("127.0.0.1", TestPort, "0.0.0.0");
            var nm = nmGo.AddComponent<NetworkManager>();
            nm.NetworkConfig = new NetworkConfig { NetworkTransport = utp, PlayerPrefab = player };
            nm.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(ListPath));

            var boot = new GameObject("PlayerTestBootstrap").AddComponent<PlayerTestBootstrap>();
            var so = new SerializedObject(boot);
            var arr = so.FindProperty("props");
            Vector3[] pos = { new Vector3(0f, 0.31f, 1.5f), new Vector3(1.3f, 0.14f, 1.6f), new Vector3(-1.4f, 0.01f, 1.5f), new Vector3(2.6f, 0.3f, 1.2f) };
            arr.arraySize = props.Length;
            for (int i = 0; i < props.Length; i++)
            {
                var e = arr.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("Prefab").objectReferenceValue = props[i];
                e.FindPropertyRelative("Position").vector3Value = pos[i];
                e.FindPropertyRelative("Yaw").floatValue = i * 20f;
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        // ---- Helpers ---------------------------------------------------------------------------------------

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a != null) return a;
            a = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(a, path);
            return a;
        }

        static Material Mat(string name, Color c, float smooth = 0.25f, float metal = 0f)
        {
            string path = MatDir + "/M_" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", metal);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Transform Child(Transform parent, string name, Vector3 localPos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            t.gameObject.layer = parent.gameObject.layer;
            return t;
        }

        static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 scale, Material m, bool keepCollider = false)
        {
            var g = GameObject.CreatePrimitive(type);
            g.name = name;
            if (!keepCollider) Object.DestroyImmediate(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.localPosition = localPos;
            g.transform.localScale = scale;
            g.layer = parent.gameObject.layer;
            g.GetComponent<Renderer>().sharedMaterial = m;
            return g;
        }

        static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null) { Debug.LogError($"[PlayerAssetBuilder] {target.GetType().Name}.{field} not found"); return; }
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static GameObject Save(GameObject root, string path)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            // Persist the NetworkObject GlobalObjectIdHash (NGO only computes it in OnValidate).
            var no = asset.GetComponent<NetworkObject>();
            if (no != null)
            {
                typeof(NetworkObject).GetMethod("OnValidate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)?.Invoke(no, null);
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssetIfDirty(asset);
            }
            return asset;
        }
    }
}
