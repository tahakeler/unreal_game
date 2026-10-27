using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarvestPanic.Core;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace HarvestPanic.Customization.Editor
{
    /// <summary>
    /// Builds every Customization asset from the imported CC0 farmer (idempotent):
    /// FBX import settings (Generic rig + clips), authored Dance/Jump clips, FarmerBody.controller, materials,
    /// CosmeticCatalog.asset, wardrobe PanelSettings, Prefabs/Farmer/FarmerBody.prefab, Resources/NetworkPrefabs/Customization.asset
    /// and Scenes/Dev/WardrobeTest.unity.
    /// Batch: Unity -batchmode -nographics -quit -projectPath HarvestPanic -executeMethod HarvestPanic.Customization.Editor.FarmerAssetBuilder.BuildAll
    /// </summary>
    public static class FarmerAssetBuilder
    {
        public const string FbxPath = "Assets/ThirdParty/Characters/QuaterniusFarmer/Farmer.fbx";
        public const string Root = "Assets/Art/Cosmetics";
        public const string AnimDir = Root + "/Anim";
        public const string MatDir = Root + "/Materials";
        public const string UiDir = Root + "/UI";
        public const string CatalogPath = Root + "/CosmeticCatalog.asset";
        public const string LegacyControllerPath = AnimDir + "/FarmerBody.controller"; // pre-hero generic controller (removed by BuildAll)
        public const string PanelPath = UiDir + "/WardrobePanel.asset";
        public const string ThemePath = UiDir + "/WardrobeTheme.tss";
        public const string PrefabPath = "Assets/Prefabs/Farmer/FarmerBody.prefab";
        public const string NetListPath = "Assets/Resources/NetworkPrefabs/Customization.asset";
        public const string ScenePath = "Assets/Scenes/Dev/WardrobeTest.unity";
        public const string CreatorStylePath = UiDir + "/CharacterCreator.uss";
        public const string LinkPath = "Assets/Resources/Customization/CustomizationLink.asset";
        public const string NetTestPrefabPath = "Assets/Prefabs/Farmer/Test/FarmerNetTest.prefab";

        [MenuItem("HarvestPanic/Customization/Build All Farmer Assets")]
        public static void BuildAll()
        {
            try
            {
                foreach (var d in new[] { AnimDir, MatDir, UiDir, "Assets/Prefabs/Farmer", "Assets/Prefabs/Farmer/Test", "Assets/Resources/NetworkPrefabs", "Assets/Resources/Customization", "Assets/Scenes/Dev" }) EnsureFolder(d);
                var heroAvatar = HeroFarmerBuilder.ConfigureHumanoid(HeroFarmerBuilder.HeroFbx, HeroFarmerBuilder.HeroMap, null);
                var animAvatar = HeroFarmerBuilder.ConfigureHumanoid(FbxPath, HeroFarmerBuilder.QuaterniusMap, HeroFarmerBuilder.SourceClips);
                if (heroAvatar == null || !heroAvatar.isValid || !heroAvatar.isHuman) throw new InvalidOperationException("Hero farmer avatar is not a valid humanoid");
                if (animAvatar == null || !animAvatar.isValid || !animAvatar.isHuman) throw new InvalidOperationException("Animation source avatar is not a valid humanoid");
                var (baseMat, unlitMat) = BuildMaterials();
                var heroMat = HeroFarmerBuilder.BuildMaterial();
                var clips = new Dictionary<string, AnimationClip>();
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(FbxPath))
                    if (o is AnimationClip c && !c.name.StartsWith("__preview__")) clips[c.name] = c;
                Debug.Log("[FarmerAssetBuilder] retargeted clips: " + string.Join(", ", clips.Keys));
                var heroModel = AssetDatabase.LoadAssetAtPath<GameObject>(HeroFarmerBuilder.HeroFbx);
                foreach (var kv in HeroFarmerBuilder.BuildProceduralClips(heroModel, heroAvatar, clips["Idle"])) clips[kv.Key] = kv.Value;
                var controller = HeroFarmerBuilder.BuildController(clips);
                var panel = BuildPanel();
                var catalog = BuildCatalog(baseMat, unlitMat, panel);
                var prefab = BuildPrefab(catalog, controller, heroMat, heroAvatar);
                catalog.farmerPrefab = prefab;
                EditorUtility.SetDirty(catalog);
                BuildLink(catalog);
                BuildNetTestPrefab(prefab);
                BuildNetworkList();
                RemoveObsolete();
                AssetDatabase.SaveAssets();
                BuildScene(prefab);
                Debug.Log("[FarmerAssetBuilder] Done");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        /// <summary>Assets of the previous Quaternius body (generic rig clips, UV'd mesh copies) that nothing uses any more.</summary>
        static void RemoveObsolete()
        {
            foreach (var p in new[] { LegacyControllerPath, AnimDir + "/Farmer_Dance.anim", AnimDir + "/Farmer_Jump.anim", AnimDir + "/Farmer_Shrug.anim",
                         AnimDir + "/Farmer_Salute.anim", AnimDir + "/Farmer_ChickenDance.anim", Root + "/Meshes" })
                if (AssetDatabase.LoadMainAssetAtPath(p) != null || AssetDatabase.IsValidFolder(p)) AssetDatabase.DeleteAsset(p);
        }

        /// <summary>Creates an asset folder (and parents) if missing.</summary>
        public static void EnsureFolderPublic(string path) => EnsureFolder(path);

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        // ---- Materials, panel, catalog ------------------------------------------------------------------------------

        static (Material, Material) BuildMaterials()
        {
            Material Make(string path, string shader, Action<Material> init)
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null) { m = new Material(Shader.Find(shader)); AssetDatabase.CreateAsset(m, path); }
                m.shader = Shader.Find(shader);
                init(m);
                EditorUtility.SetDirty(m);
                return m;
            }
            var lit = Make(MatDir + "/M_HP_Cosmetic.mat", "Universal Render Pipeline/Lit", m =>
            {
                m.SetColor("_BaseColor", Color.white); m.SetFloat("_Smoothness", 0.18f); m.SetFloat("_Metallic", 0f);
                m.SetFloat("_EnvironmentReflections", 0f); m.enableInstancing = true;
            });
            var unlit = Make(MatDir + "/M_HP_CosmeticUnlit.mat", "Universal Render Pipeline/Unlit", m => m.SetColor("_BaseColor", Color.white));
            return (lit, unlit);
        }

        static PanelSettings BuildPanel()
        {
            if (!File.Exists(ThemePath)) { File.WriteAllText(ThemePath, "@import url(\"unity-theme://default\");\n"); AssetDatabase.ImportAsset(ThemePath); }
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath);
            if (panel == null) { panel = ScriptableObject.CreateInstance<PanelSettings>(); AssetDatabase.CreateAsset(panel, PanelPath); }
            panel.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1920, 1080);
            panel.match = 0.5f;
            panel.sortingOrder = 100;
            EditorUtility.SetDirty(panel);
            return panel;
        }

        static CosmeticCatalog BuildCatalog(Material lit, Material unlit, PanelSettings panel)
        {
            var cat = AssetDatabase.LoadAssetAtPath<CosmeticCatalog>(CatalogPath);
            if (cat == null)
            {
                cat = ScriptableObject.CreateInstance<CosmeticCatalog>();
                cat.ResetToDefaults();
                AssetDatabase.CreateAsset(cat, CatalogPath);
            }
            else
            {
                int added = cat.EnsureDefaults();
                if (added > 0) Debug.Log("[FarmerAssetBuilder] Catalog: merged " + added + " new default items");
            }
            cat.entries.RemoveAll(e => e == null);
            cat.baseMaterial = lit; cat.unlitMaterial = unlit; cat.wardrobePanel = panel;
            cat.creatorStyle = AssetDatabase.LoadAssetAtPath<StyleSheet>(CreatorStylePath);
            if (cat.creatorStyle == null) Debug.LogWarning("[FarmerAssetBuilder] Missing " + CreatorStylePath);
            EditorUtility.SetDirty(cat);
            return cat;
        }

        [MenuItem("HarvestPanic/Customization/Reset Catalog To Defaults")]
        public static void ResetCatalog()
        {
            var cat = AssetDatabase.LoadAssetAtPath<CosmeticCatalog>(CatalogPath);
            if (cat == null) { BuildAll(); return; }
            cat.ResetToDefaults();
            EditorUtility.SetDirty(cat);
            AssetDatabase.SaveAssets();
        }

        // ---- Prefab -------------------------------------------------------------------------------------------------

        static GameObject BuildPrefab(CosmeticCatalog catalog, AnimatorController controller, Material heroMat, Avatar avatar)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(HeroFarmerBuilder.HeroFbx);
            var root = new GameObject("FarmerBody");
            try
            {
                var visual = root.AddComponent<FarmerVisual>();
                root.AddComponent<FarmerLoadoutSync>();
                root.AddComponent<FarmerEmotes>();

                var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
                inst.name = "Model";
                inst.transform.SetParent(root.transform, false);
                inst.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // the FBX faces -Z; the game's characters face +Z
                var anim = inst.GetComponent<Animator>() ?? inst.AddComponent<Animator>();
                anim.avatar = avatar;
                anim.runtimeAnimatorController = controller;
                anim.applyRootMotion = false;
                anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                var smr = inst.GetComponentInChildren<SkinnedMeshRenderer>(true);
                smr.sharedMaterial = heroMat;
                smr.updateWhenOffscreen = false;

                var head = anim.GetBoneTransform(HumanBodyBones.Head);
                var neck = anim.GetBoneTransform(HumanBodyBones.Neck);
                var chest = anim.GetBoneTransform(HumanBodyBones.UpperChest) ?? anim.GetBoneTransform(HumanBodyBones.Chest);
                var hips = anim.GetBoneTransform(HumanBodyBones.Hips);
                var m = HeroFarmerBuilder.MeasureBody(smr, chest, neck);
                // Accessory recipes were authored for a 22 cm wide head and a 22 cm deep chest: scale the attach spaces to this body.
                float headK = Mathf.Clamp(m.headWidth / 0.22f, 0.8f, 3f);
                // Back/outfit parts: shoulder span (upper-arm roots) vs the 0.30 m of the farmer the recipes were made on;
                // the belly depth would blow them up.
                var lu = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm); var ru = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
                float widthK = Vector3.Distance(lu.position, ru.position) / 0.30f;
                float heightK = (neck.position.y - hips.position.y) / 0.613f;          // neck-to-hips of the original farmer
                float bodyK = Mathf.Clamp(Mathf.Sqrt(widthK * heightK), 0.8f, 2f);     // chibi: wide but short torso

                Transform Attach(string n, Transform parent, Vector3 worldPos, float k, float yFactor = 1f)
                {
                    var t = new GameObject(n).transform;
                    t.SetParent(parent, true);
                    t.SetPositionAndRotation(worldPos, Quaternion.identity);
                    var ls = parent.lossyScale;
                    t.localScale = new Vector3(k / ls.x, k * yFactor / ls.y, k / ls.z);
                    return t;
                }
                // Hats fit the hero's wide head but keep a hat-like height (a toque at full head scale towered ~1 m).
                var hatAttach = Attach("HatAttach", head, new Vector3(m.headX, m.crownY, m.headZ), headK, 0.72f);
                var faceAttach = Attach("FaceAttach", head, new Vector3(m.headX, m.eyeY, m.faceZ), headK);
                var backAttach = Attach("BackAttach", chest, new Vector3(0f, m.chestY, m.backZ), bodyK);
                var chestAttach = Attach("ChestAttach", chest, new Vector3(0f, m.chestY, (m.frontZ + m.backZ) * 0.5f), bodyK);
                var hipsAttach = Attach("HipsAttach", hips, hips.position, bodyK);
                Debug.Log($"[FarmerAssetBuilder] hero attach scale head {headK:F2} body {bodyK:F2}");

                var tag = BuildNameTag(root.transform, m.topY + 0.75f, catalog); // clear of tall hats (cone, toque)
                visual.EditorSetup(catalog, anim, inst.transform, head, hatAttach, faceAttach, backAttach, smr, new List<ZoneBinding>(), tag);
                visual.EditorSetupOutfitAttach(chestAttach, hipsAttach);
                var (refs, _) = HeroFarmerBuilder.ReadStats();
                var homes = HeroFarmerBuilder.HomeSwatches(catalog, refs);
                Debug.Log("[HeroFarmerBuilder] home swatches (overalls, shirt, boots/gloves, skin, hair, band): " + string.Join(", ", homes));
                visual.EditorSetupRecolour(new List<Renderer> { smr }, "hat.straw", 1.18f, new Vector3(1.12f, 0.96f, 1.12f),
                    new Vector3(0.27f, 0.17f, 0.3f), new Vector3(0f, -0.06f, -0.01f),
                    homes, refs.Select(r => new Color(r.x, r.y, r.z).gamma).ToArray());
                return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { Object.DestroyImmediate(root); }
        }

        static void BuildLink(CosmeticCatalog catalog)
        {
            var link = AssetDatabase.LoadAssetAtPath<CustomizationLink>(LinkPath);
            if (link == null) { link = ScriptableObject.CreateInstance<CustomizationLink>(); AssetDatabase.CreateAsset(link, LinkPath); }
            link.catalog = catalog;
            EditorUtility.SetDirty(link);
        }

        /// <summary>Test-only network prefab (NetworkObject root + FarmerBody): in-process host/client replication tests. Not in any list.</summary>
        static void BuildNetTestPrefab(GameObject farmerPrefab)
        {
            var root = new GameObject("FarmerNetTest");
            try
            {
                root.AddComponent<NetworkObject>();
                var body = (GameObject)PrefabUtility.InstantiatePrefab(farmerPrefab);
                body.name = "FarmerBody";
                body.transform.SetParent(root.transform, false);
                PrefabUtility.SaveAsPrefabAsset(root, NetTestPrefabPath);
            }
            finally { Object.DestroyImmediate(root); }
        }

        static NameTag BuildNameTag(Transform parent, float height, CosmeticCatalog catalog)
        {
            var root = new GameObject("NameTag");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(0f, height, 0f);
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            TextMesh Label(string n, Vector3 pos, Color c)
            {
                var go = new GameObject(n);
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = pos;
                var tm = go.AddComponent<TextMesh>();
                tm.font = font; tm.fontSize = 72; tm.characterSize = 0.012f; tm.anchor = TextAnchor.MiddleCenter;
                tm.alignment = TextAlignment.Center; tm.fontStyle = FontStyle.Bold; tm.color = c; tm.text = "Farmer";
                var mr = go.GetComponent<MeshRenderer>();
                mr.sharedMaterial = font.material; mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = false;
                return tm;
            }
            var shadow = Label("Shadow", new Vector3(-0.008f, -0.008f, 0.01f), new Color(0.1f, 0.06f, 0.03f));
            var text = Label("Text", Vector3.zero, Color.white);
            var iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(root.transform, false);
            iconGo.transform.localPosition = new Vector3(0f, 0.17f, 0f);
            iconGo.transform.localScale = Vector3.one * 0.16f;
            var mf = iconGo.AddComponent<MeshFilter>();
            mf.sharedMesh = null;
            var imr = iconGo.AddComponent<MeshRenderer>();
            imr.sharedMaterial = catalog.unlitMaterial; imr.shadowCastingMode = ShadowCastingMode.Off; imr.receiveShadows = false;
            var tag = root.AddComponent<NameTag>();
            tag.EditorSetup(text, shadow, mf, imr);
            return tag;
        }

        static void BuildNetworkList()
        {
            var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(NetListPath);
            if (list != null) return;
            list = ScriptableObject.CreateInstance<NetworkPrefabsList>();
            AssetDatabase.CreateAsset(list, NetListPath); // intentionally empty: FarmerBody nests under the Player NetworkObject, wardrobes are local props
        }

        // ---- Test scene ---------------------------------------------------------------------------------------------

        static void BuildScene(GameObject prefab)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.72f, 0.9f);
            RenderSettings.ambientEquatorColor = new Color(0.6f, 0.58f, 0.48f);
            RenderSettings.ambientGroundColor = new Color(0.32f, 0.27f, 0.2f);

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.5f; sun.shadows = LightShadows.Soft;
            sun.color = new Color(1f, 0.93f, 0.8f);
            sun.transform.rotation = Quaternion.Euler(42f, 150f, 0f);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(4f, 1f, 4f);
            var gm = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            gm.SetColor("_BaseColor", new Color(0.48f, 0.64f, 0.3f)); gm.SetFloat("_Smoothness", 0.05f);
            AssetDatabase.CreateAsset(gm, MatDir + "/M_WardrobeTestGround.mat");
            ground.GetComponent<Renderer>().sharedMaterial = gm;

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.62f, 0.8f, 0.96f);
            cam.fieldOfView = 34f;
            camGo.transform.position = new Vector3(0f, 1.55f, 6.4f);
            camGo.transform.LookAt(new Vector3(0f, 1.15f, 0f));
            camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();

            var lineup = new GameObject("Lineup").AddComponent<WardrobeTestLineup>();
            var farmers = new List<FarmerVisual>();
            for (int i = 0; i < 4; i++)
            {
                var f = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                f.name = "Farmer_" + (i + 1);
                f.transform.SetParent(lineup.transform, false);
                f.transform.localPosition = new Vector3(-2.4f + i * 1.6f, 0f, 0f);
                f.transform.localRotation = Quaternion.Euler(0f, (i - 1.5f) * -8f, 0f);
                farmers.Add(f.GetComponent<FarmerVisual>());
            }
            lineup.EditorSetup(farmers);

            var marker = new GameObject("WardrobeMarker");
            marker.transform.position = new Vector3(3.7f, 0f, -2.4f);
            marker.transform.rotation = Quaternion.Euler(0f, -20f, 0f);
            marker.AddComponent<LevelMarker>().Configure(MarkerRole.Wardrobe);
            marker.tag = MarkerTag.Wardrobe;

            EditorSceneManager.SaveScene(scene, ScenePath);
        }
    }
}
