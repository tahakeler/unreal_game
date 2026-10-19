using System.Collections.Generic;
using System.IO;
using HarvestPanic.Core;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace HarvestPanic.Economy.Editor
{
    /// <summary>
    /// Idempotent generator for every Economy+HUD asset: goods definitions (goods-value table), GoodsProp prefabs
    /// from the CC0 meshes, day modifiers, tuning, director + HUD prefabs, the Economy network prefab list and the
    /// EconomyTest dev scene. Batchmode:
    /// <c>Unity -batchmode -nographics -quit -projectPath HarvestPanic -executeMethod HarvestPanic.Economy.Editor.EconomyAssetBuilder.BuildAll</c>
    /// </summary>
    public static class EconomyAssetBuilder
    {
        const string GoodsData = "Assets/Resources/Data/Goods";
        const string ModData = "Assets/Resources/Data/DayModifiers";
        const string GoodsPrefabs = "Assets/Prefabs/Goods";
        const string HudPrefabs = "Assets/Prefabs/HUD";
        const string Meshes = "Assets/ThirdParty/Props/Quaternius";
        const string NetList = "Assets/Resources/NetworkPrefabs/Economy.asset";
        public const string TestScene = "Assets/Scenes/Dev/EconomyTest.unity";

        struct GoodsSpec
        {
            public string Id, Asset, Mesh; public GoodsCategory Cat; public int Base; public float Frag; public int Hands; public float Mass; public bool Alive;
        }

        static readonly GoodsSpec[] Specs =
        {
            new GoodsSpec { Id = "Livestock.Pig", Asset = "Pig", Mesh = "SM_Pig", Cat = GoodsCategory.Livestock, Base = 60, Frag = 0.8f, Hands = 2, Mass = 40f, Alive = true },
            new GoodsSpec { Id = "Livestock.Chicken", Asset = "Chicken", Mesh = "SM_Chicken", Cat = GoodsCategory.Livestock, Base = 15, Frag = 0.6f, Hands = 1, Mass = 5f, Alive = true },
            new GoodsSpec { Id = "Product.Egg", Asset = "Egg", Mesh = "SM_Egg", Cat = GoodsCategory.Product, Base = 5, Frag = 2f, Hands = 1, Mass = 0.06f },
            new GoodsSpec { Id = "Product.EggCrate", Asset = "EggCrate", Mesh = "SM_Crate", Cat = GoodsCategory.Product, Base = 30, Frag = 1.5f, Hands = 2, Mass = 4f },
            new GoodsSpec { Id = "Crop.PrizePumpkin", Asset = "PrizePumpkin", Mesh = "SM_PrizePumpkin", Cat = GoodsCategory.Crop, Base = 300, Frag = 1.5f, Hands = 8, Mass = 180f },
            new GoodsSpec { Id = "Crop.Turnip", Asset = "Turnip", Mesh = "SM_Turnip", Cat = GoodsCategory.Crop, Base = 10, Frag = 1f, Hands = 1, Mass = 2f },
            new GoodsSpec { Id = "Crop.Carrot", Asset = "Carrot", Mesh = "SM_Carrot", Cat = GoodsCategory.Crop, Base = 8, Frag = 0.8f, Hands = 1, Mass = 0.3f },
        };

        [MenuItem("HarvestPanic/Economy/Build All Assets")]
        public static void BuildAll()
        {
            foreach (var d in new[] { GoodsData, ModData, GoodsPrefabs, HudPrefabs, "Assets/Resources/NetworkPrefabs", "Assets/Scenes/Dev" }) Directory.CreateDirectory(d);
            ConfigureMeshImports();
            var tuning = EnsureTuning();
            var prefabs = new List<GameObject>();
            foreach (var s in Specs) prefabs.Add(BuildGoods(s));
            BuildModifiers();
            var hud = BuildHud();
            var director = BuildDirector(tuning, hud);
            prefabs.Insert(0, director);
            BuildNetList(prefabs);
            AssetDatabase.SaveAssets();
            Content.Goods.Invalidate();
            Content.DayModifiers.Invalidate();
            BuildTestScene(director);
            AssetDatabase.SaveAssets();
            Debug.Log("[EconomyAssetBuilder] done");
        }

        // ---------------- helpers ----------------

        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a != null) return a;
            a = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(a, path);
            return a;
        }

        static SerializedProperty P(SerializedObject so, string auto) =>
            so.FindProperty("<" + auto + ">k__BackingField") ?? so.FindProperty(auto);

        static void ConfigureMeshImports()
        {
            foreach (var s in Specs)
            {
                var imp = AssetImporter.GetAtPath($"{Meshes}/{s.Mesh}.fbx") as ModelImporter;
                if (imp == null) { Debug.LogError("Missing mesh " + s.Mesh); continue; }
                bool dirty = false;
                if (imp.animationType != ModelImporterAnimationType.None) { imp.animationType = ModelImporterAnimationType.None; dirty = true; }
                if (imp.importAnimation) { imp.importAnimation = false; dirty = true; }
                if (imp.materialImportMode != ModelImporterMaterialImportMode.ImportStandard) { imp.materialImportMode = ModelImporterMaterialImportMode.ImportStandard; dirty = true; }
                if (dirty) imp.SaveAndReimport();
            }
        }

        static EconomyTuning EnsureTuning() => LoadOrCreate<EconomyTuning>(GoodsData + "/EconomyTuning.asset");

        static GameObject BuildGoods(GoodsSpec s)
        {
            var def = LoadOrCreate<GoodsDefinition>($"{GoodsData}/Goods_{s.Asset}.asset");
            string prefabPath = $"{GoodsPrefabs}/GoodsProp_{s.Asset}.prefab";

            var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{Meshes}/{s.Mesh}.fbx");
            var root = new GameObject("GoodsProp_" + s.Asset);
            int layer = LayerMask.NameToLayer(GameLayers.Carryable);
            if (model != null)
            {
                var vis = (GameObject)PrefabUtility.InstantiatePrefab(model);
                vis.name = "Model";
                vis.transform.SetParent(root.transform, false);
            }
            var bounds = new Bounds(Vector3.up * 0.1f, Vector3.one * 0.2f);
            bool first = true;
            foreach (var r in root.GetComponentsInChildren<Renderer>())
            {
                if (first) { bounds = r.bounds; first = false; } else bounds.Encapsulate(r.bounds);
            }
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (layer >= 0) t.gameObject.layer = layer;

            var box = root.AddComponent<BoxCollider>();
            box.center = bounds.center; box.size = Vector3.Max(bounds.size, Vector3.one * 0.05f);
            var rb = root.AddComponent<Rigidbody>();
            rb.mass = s.Mass; rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = s.Mass < 1f ? CollisionDetectionMode.ContinuousDynamic : CollisionDetectionMode.Discrete;
            root.AddComponent<NetworkObject>();
            root.AddComponent<NetworkTransform>();
            root.AddComponent<NetworkRigidbody>();
            var goods = root.AddComponent<Goods>();
            var gso = new SerializedObject(goods);
            gso.FindProperty("definition").objectReferenceValue = def;
            gso.FindProperty("goodsId").stringValue = s.Id;
            gso.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);

            var so = new SerializedObject(def);
            P(so, "GoodsId").stringValue = s.Id;
            P(so, "Category").enumValueIndex = (int)s.Cat;
            P(so, "BaseValue").intValue = s.Base;
            P(so, "Fragility").floatValue = s.Frag;
            P(so, "CarryHands").intValue = s.Hands;
            P(so, "MassKg").floatValue = s.Mass;
            P(so, "IsAlive").boolValue = s.Alive;
            P(so, "WorldPrefab").objectReferenceValue = prefab;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(def);
            return prefab;
        }

        struct ModSpec
        {
            public string Id, Asset, Group, Card; public DayModifierCategory Cat; public DayModifierWeight W; public DayModifierStackRule Rule;
            public DayModifierDuration Dur; public string[] Grants; public DayParamEffect[] Fx;
        }

        static DayParamEffect Fx(string p, DayParamOp op, float v) => new DayParamEffect { ParamName = p, Op = op, Value = v };

        static void BuildModifiers()
        {
            var specs = new[]
            {
                new ModSpec { Id = "Mod.Weather.Fog", Asset = "Weather_Fog", Group = "Group.Weather", Cat = DayModifierCategory.Weather, W = DayModifierWeight.Minor, Rule = DayModifierStackRule.ReplaceGroup, Dur = DayModifierDuration.Day,
                    Card = "Fog today. Grandpa can't see the pigs. Neither can you.", Fx = new[] { Fx(DayParams.NoiseEnvMult, DayParamOp.Mul, 0.8f) }, Grants = new[] { "World.Fog" } },
                new ModSpec { Id = "Mod.Weather.Storm", Asset = "Weather_Storm", Group = "Group.Weather", Cat = DayModifierCategory.Weather, W = DayModifierWeight.Major, Rule = DayModifierStackRule.ReplaceGroup, Dur = DayModifierDuration.Day,
                    Card = "Storm today. Bridge will leave. Do not follow bridge.", Fx = new[] { Fx(DayParams.NoiseEnvMult, DayParamOp.Mul, 0.6f), Fx("Goods.BruiseMult", DayParamOp.Mul, 1.25f) }, Grants = new[] { "World.Storm" } },
                new ModSpec { Id = "Mod.Weather.Heatwave", Asset = "Weather_Heatwave", Group = "Group.Weather", Cat = DayModifierCategory.Weather, W = DayModifierWeight.Minor, Rule = DayModifierStackRule.ReplaceGroup, Dur = DayModifierDuration.Day,
                    Card = "Hot one. The turnips are sweating. Sell them fast.", Fx = new[] { Fx("Goods.FreshnessDecayMult", DayParamOp.Mul, 1.5f), Fx("Market.DemandMult", DayParamOp.Mul, 1.1f) }, Grants = new string[0] },
                new ModSpec { Id = "Mod.Landlord.LandlordsCut", Asset = "Landlord_LandlordsCut", Cat = DayModifierCategory.Landlord, W = DayModifierWeight.Minor, Rule = DayModifierStackRule.Unique, Dur = DayModifierDuration.Run,
                    Card = "Landlord's Cut: 5% of every sale. \"Administrative.\"", Fx = new[] { Fx("Economy.IncomeTaxRate", DayParamOp.Add, 0.05f) }, Grants = new string[0] },
                new ModSpec { Id = "Mod.Landlord.EarlyCurfew", Asset = "Landlord_EarlyCurfew", Cat = DayModifierCategory.Landlord, W = DayModifierWeight.Minor, Rule = DayModifierStackRule.Unique, Dur = DayModifierDuration.Run,
                    Card = "Early Curfew: market closes 45 s early.", Fx = new[] { Fx("Day.WorkdaySeconds", DayParamOp.Add, -45f) }, Grants = new string[0] },
                new ModSpec { Id = "Mod.Landlord.RespawnPaperwork", Asset = "Landlord_RespawnPaperwork", Cat = DayModifierCategory.Landlord, W = DayModifierWeight.Minor, Rule = DayModifierStackRule.Unique, Dur = DayModifierDuration.Run,
                    Card = "Respawn Paperwork: dying now costs 20% more.", Fx = new[] { Fx(DayParams.EconomyRespawnFeeMult, DayParamOp.Mul, 1.2f) }, Grants = new string[0] },
                new ModSpec { Id = "Mod.Landlord.QuietHours", Asset = "Landlord_QuietHours", Cat = DayModifierCategory.Landlord, W = DayModifierWeight.Minor, Rule = DayModifierStackRule.Unique, Dur = DayModifierDuration.Run,
                    Card = "Quiet Hours: voices carry further. Whisper.", Fx = new[] { Fx(DayParams.NoiseVoiceRadiusMult, DayParamOp.Mul, 1.25f) }, Grants = new string[0] },
                new ModSpec { Id = "Mod.Landlord.NoRunningOnBridge", Asset = "Landlord_NoRunningOnBridge", Cat = DayModifierCategory.Landlord, W = DayModifierWeight.Minor, Rule = DayModifierStackRule.Unique, Dur = DayModifierDuration.Run,
                    Card = "No Running on the Bridge: planks break 30% easier.", Fx = new[] { Fx("Bridge.PlankBreakMult", DayParamOp.Mul, 1.3f) }, Grants = new string[0] },
            };
            foreach (var s in specs)
            {
                var m = LoadOrCreate<DayModifier>($"{ModData}/Mod_{s.Asset}.asset");
                var so = new SerializedObject(m);
                P(so, "ModifierId").stringValue = s.Id;
                P(so, "Category").enumValueIndex = (int)s.Cat;
                P(so, "Weight").enumValueIndex = (int)s.W;
                P(so, "StackRule").enumValueIndex = (int)s.Rule;
                P(so, "GroupTag").stringValue = s.Group ?? "";
                P(so, "Duration").enumValueIndex = (int)s.Dur;
                P(so, "CardText").stringValue = s.Card;
                var fx = P(so, "Effects"); fx.arraySize = s.Fx.Length;
                for (int i = 0; i < s.Fx.Length; i++)
                {
                    var el = fx.GetArrayElementAtIndex(i);
                    el.FindPropertyRelative("ParamName").stringValue = s.Fx[i].ParamName;
                    el.FindPropertyRelative("Op").enumValueIndex = (int)s.Fx[i].Op;
                    el.FindPropertyRelative("Value").floatValue = s.Fx[i].Value;
                }
                var g = P(so, "GrantTags"); g.arraySize = s.Grants.Length;
                for (int i = 0; i < s.Grants.Length; i++) g.GetArrayElementAtIndex(i).stringValue = s.Grants[i];
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(m);
            }
        }

        public const string PanelPath = HudPrefabs + "/EconomyHudPanel.asset";

        static GameObject BuildHud()
        {
            var panel = LoadOrCreate<PanelSettings>(PanelPath);
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1920, 1080);
            panel.match = 0.5f;
            panel.sortingOrder = 10;
            EditorUtility.SetDirty(panel);

            var root = new GameObject("EconomyHUD");
            var doc = root.AddComponent<UIDocument>();
            doc.panelSettings = panel;
            root.AddComponent<EconomyHudMarker>();
            var hud = root.AddComponent<EconomyHud>();
            var so = new SerializedObject(hud);
            so.FindProperty("styleSheet").objectReferenceValue = AssetDatabase.LoadAssetAtPath<StyleSheet>(HudPrefabs + "/EconomyHud.uss");
            so.FindProperty("headlineFont").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Font>("Assets/ThirdParty/Fonts/LilitaOne/LilitaOne-Regular.ttf");
            so.FindProperty("bodyFont").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Font>("Assets/ThirdParty/Fonts/Nunito/Nunito-Variable.ttf");
            so.ApplyModifiedPropertiesWithoutUndo();
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, HudPrefabs + "/EconomyHUD.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        static GameObject BuildDirector(EconomyTuning tuning, GameObject hud)
        {
            var root = new GameObject("EconomyDirector");
            root.AddComponent<NetworkObject>();
            var d = root.AddComponent<EconomyDirector>();
            var so = new SerializedObject(d);
            so.FindProperty("tuning").objectReferenceValue = tuning;
            so.FindProperty("hudPrefab").objectReferenceValue = hud;
            so.ApplyModifiedPropertiesWithoutUndo();
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, GoodsPrefabs + "/EconomyDirector.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        static void BuildNetList(List<GameObject> prefabs)
        {
            var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(NetList);
            if (list != null) AssetDatabase.DeleteAsset(NetList);
            list = ScriptableObject.CreateInstance<NetworkPrefabsList>();
            foreach (var p in prefabs) list.Add(new NetworkPrefab { Prefab = p });
            AssetDatabase.CreateAsset(list, NetList);
        }

        // ---------------- dev scene ----------------

        static void BuildTestScene(GameObject directorPrefab)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/HP_DefaultVolumeProfile.asset");
            if (profile != null)
            {
                var vol = new GameObject("Global Volume").AddComponent<Volume>();
                vol.isGlobal = true; vol.sharedProfile = profile;
            }
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.5f; sun.shadows = LightShadows.Soft;
            sun.color = new Color(1f, 0.93f, 0.8f);
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.72f, 0.85f);
            RenderSettings.ambientEquatorColor = new Color(0.55f, 0.55f, 0.45f);
            RenderSettings.ambientGroundColor = new Color(0.3f, 0.26f, 0.2f);

            var lit = Shader.Find("Universal Render Pipeline/Lit");
            Material Mat(string name, Color c)
            {
                string path = $"{GoodsPrefabs}/Dev_{name}.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null) { m = new Material(lit); AssetDatabase.CreateAsset(m, path); }
                m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", 0.15f);
                EditorUtility.SetDirty(m);
                return m;
            }
            var grass = Mat("Grass", new Color(0.47f, 0.6f, 0.33f));
            var wood = Mat("Wood", new Color(0.55f, 0.4f, 0.26f));
            var dirt = Mat("Dirt", new Color(0.62f, 0.5f, 0.36f));

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground"; ground.transform.localScale = new Vector3(4f, 1f, 4f);
            ground.GetComponent<Renderer>().sharedMaterial = grass;

            // Market pen: marker + visible fence (World team owns the real one).
            var penGo = new GameObject("MarketPen");
            penGo.transform.position = new Vector3(4f, 0f, 3f);
            var pm = penGo.AddComponent<LevelMarker>(); pm.Configure(MarkerRole.MarketPen);
            penGo.tag = MarkerTag.MarketPen;
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "PenFloor"; Object.DestroyImmediate(floor.GetComponent<Collider>());
            floor.transform.SetParent(penGo.transform, false);
            floor.transform.localPosition = new Vector3(0, 0.01f, 0); floor.transform.localScale = new Vector3(6f, 0.02f, 6f);
            floor.GetComponent<Renderer>().sharedMaterial = dirt;
            for (int side = 0; side < 4; side++)
            {
                if (side == 3) continue; // open side facing the camera
                var rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rail.name = "Fence" + side;
                rail.transform.SetParent(penGo.transform, false);
                bool x = side % 2 == 0;
                float off = side < 2 ? 3f : -3f;
                rail.transform.localPosition = x ? new Vector3(0, 0.45f, off) : new Vector3(off, 0.45f, 0);
                rail.transform.localScale = x ? new Vector3(6f, 0.9f, 0.12f) : new Vector3(0.12f, 0.9f, 6f);
                rail.GetComponent<Renderer>().sharedMaterial = wood;
            }

            void GoodsMarker(string key, Vector3 pos, int count)
            {
                var g = new GameObject("Goods_" + key);
                g.transform.position = pos;
                var lm = g.AddComponent<LevelMarker>(); lm.Configure(MarkerRole.Goods, key, count);
                g.tag = MarkerTag.Goods;
            }
            GoodsMarker("Crop.PrizePumpkin", new Vector3(-3.5f, 0f, 2.5f), 1);
            GoodsMarker("Product.EggCrate", new Vector3(-1.2f, 0f, 0.5f), 2);
            GoodsMarker("Livestock.Pig", new Vector3(0.6f, 0f, 2.8f), 1);
            GoodsMarker("Livestock.Chicken", new Vector3(-1.6f, 0f, 3.2f), 1);
            GoodsMarker("Crop.Turnip", new Vector3(-4.2f, 0f, -0.2f), 3);
            GoodsMarker("Crop.Carrot", new Vector3(-2.6f, 0f, -1.0f), 3);
            GoodsMarker("Product.Egg", new Vector3(0.6f, 0f, -0.6f), 3);

            var cam = new GameObject("Main Camera").AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.transform.position = new Vector3(0f, 4.2f, -7.5f);
            cam.transform.rotation = Quaternion.Euler(24f, 0f, 0f);
            cam.fieldOfView = 55f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.72f, 0.84f, 0.95f);
            cam.gameObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;

            PrefabUtility.InstantiatePrefab(directorPrefab);
            EditorSceneManager.SaveScene(scene, TestScene);
        }
    }
}
