using System;
using System.Linq;
using HarvestPanic.AI.Crops;
using HarvestPanic.Core;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEngine;

namespace HarvestPanic.AI.Editor
{
    /// <summary>
    /// Idempotent generator for the two cursed crops (cursed-crops §3): Screaming Mandrake and Tumble Melon.
    /// Builds the AI-owned tuning (<c>Resources/Data/Critters/CursedCropTuning.asset</c>), creepy primitive prefabs
    /// (<c>Prefabs/Critters/Crops</c>, listed in <c>NetworkPrefabs/Critters.asset</c>) and the two NEW goods data assets
    /// (<c>Resources/Data/Goods/Goods_Mandrake.asset</c>, <c>Goods_TumbleMelon.asset</c>; data only, no Economy code).
    /// <c>-executeMethod HarvestPanic.AI.Editor.CursedCropAssetBuilder.Build</c>
    /// </summary>
    public static class CursedCropAssetBuilder
    {
        public const string CropPrefabDir = CritterAssetBuilder.PrefabDir + "/Crops";
        public const string GoodsDir = "Assets/Resources/Data/Goods";
        public const string TuningPath = CritterAssetBuilder.DataDir + "/CursedCropTuning.asset";
        public const string MandrakePrefab = CropPrefabDir + "/Crop_Mandrake.prefab";
        public const string MelonPrefab = CropPrefabDir + "/Crop_TumbleMelon.prefab";

        /// <summary>cursed-crops §3 values: id, base $, fragility, hands, mass (kg).</summary>
        public static readonly (string id, string asset, int value, float fragility, int hands, float mass)[] Goods =
        {
            ("Crop.Mandrake", "Goods_Mandrake", 45, 1.2f, 1, 3f),
            ("Crop.TumbleMelon", "Goods_TumbleMelon", 80, 1.4f, 4, 14f),
        };

        /// <summary>Whole feature/sheep-cursed-crops content pass: sheep, both crops, Muddlebury markers + fields.</summary>
        public static void BuildSheepAndCrops()
        {
            CritterAssetBuilder.BuildSheep();
            Build();
            LivestockSceneHook.AddMuddleburyMarkers();
        }

        public static void Build()
        {
            try
            {
                foreach (var d in new[] { CritterAssetBuilder.PrefabDir, CropPrefabDir, CritterAssetBuilder.PrefabDir + "/Materials", GoodsDir })
                    if (!AssetDatabase.IsValidFolder(d)) System.IO.Directory.CreateDirectory(d);
                AssetDatabase.Refresh();
                var tuning = AssetDatabase.LoadAssetAtPath<CursedCropTuning>(TuningPath);
                if (tuning == null) { tuning = ScriptableObject.CreateInstance<CursedCropTuning>(); AssetDatabase.CreateAsset(tuning, TuningPath); }

                var mandrake = BuildMandrake(tuning);
                var melon = BuildMelon(tuning);
                BuildGoods(Goods[0], mandrake);
                BuildGoods(Goods[1], melon);

                var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(CritterAssetBuilder.NetListPath);
                if (list == null) { list = ScriptableObject.CreateInstance<NetworkPrefabsList>(); AssetDatabase.CreateAsset(list, CritterAssetBuilder.NetListPath); }
                foreach (var go in new[] { mandrake, melon })
                    if (!list.PrefabList.Any(x => x.Prefab == go)) list.Add(new NetworkPrefab { Prefab = go });
                EditorUtility.SetDirty(list);
                AssetDatabase.SaveAssets();
                Content.Goods.Invalidate();
                Debug.Log("[CursedCropAssetBuilder] Done.");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        static void BuildGoods((string id, string asset, int value, float fragility, int hands, float mass) g, GameObject prefab)
        {
            string path = $"{GoodsDir}/{g.asset}.asset";
            var def = AssetDatabase.LoadAssetAtPath<GoodsDefinition>(path);
            if (def == null) { def = ScriptableObject.CreateInstance<GoodsDefinition>(); AssetDatabase.CreateAsset(def, path); }
            var so = new SerializedObject(def);
            void P(string n, Action<SerializedProperty> set) => set(so.FindProperty($"<{n}>k__BackingField") ?? throw new InvalidOperationException(n));
            P("GoodsId", p => p.stringValue = g.id);
            P("Category", p => p.intValue = (int)GoodsCategory.Crop);
            P("BaseValue", p => p.intValue = g.value);
            P("Fragility", p => p.floatValue = g.fragility);
            P("CarryHands", p => p.intValue = g.hands);
            P("MassKg", p => p.floatValue = g.mass);
            P("IsAlive", p => p.boolValue = false);
            P("DefaultTags", p => p.intValue = (int)GoodsTag.Cursed);
            P("WorldPrefab", p => p.objectReferenceValue = prefab);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(def);
        }

        // ------------------------------------------------------------------ prefabs

        static Material Mat(string name, Color c, float smooth = 0.15f, Color? emission = null)
        {
            string path = $"{CritterAssetBuilder.PrefabDir}/Materials/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", smooth);
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        static Transform Part(Transform parent, string name, PrimitiveType type, Vector3 pos, Vector3 euler, Vector3 scale, Material m)
        {
            var g = GameObject.CreatePrimitive(type);
            g.name = name;
            UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos; g.transform.localEulerAngles = euler; g.transform.localScale = scale;
            g.GetComponent<Renderer>().sharedMaterial = m;
            return g.transform;
        }

        static GameObject Root(string name, float mass, out Transform visual)
        {
            var root = new GameObject(name);
            var rb = root.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.isKinematic = true; // planted; CursedCropGoods frees it on the first grab
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            root.AddComponent<NetworkObject>();
            var nt = root.AddComponent<NetworkTransform>();
            nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false;
            visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            return root;
        }

        static GameObject Save(GameObject root, string path)
        {
            int layer = LayerMask.NameToLayer(GameLayers.Carryable);
            if (layer >= 0) foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
            CritterAssetBuilder.AddLod(root);
            try { return PrefabUtility.SaveAsPrefabAsset(root, path); }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        /// <summary>
        /// Wrinkled pink-brown root body with a screaming face (hollow eyes + open mouth), root arms and legs and a tuft of
        /// dark leaves. Planted 0.25 m deep, only the leaves and the staring eyes show.
        /// </summary>
        static GameObject BuildMandrake(CursedCropTuning tuning)
        {
            var skin = Mat("M_Crop_Mandrake_Skin", new Color(0.62f, 0.45f, 0.36f), 0.05f);
            var wrinkle = Mat("M_Crop_Mandrake_Wrinkle", new Color(0.42f, 0.29f, 0.22f), 0.05f);
            var voidM = Mat("M_Crop_Void", new Color(0.03f, 0.02f, 0.02f), 0f);
            var leaf = Mat("M_Crop_Mandrake_Leaf", new Color(0.18f, 0.32f, 0.12f), 0.2f);
            var glint = Mat("M_Crop_Mandrake_Eye", new Color(0.9f, 0.85f, 0.4f), 0.6f, new Color(0.6f, 0.5f, 0.1f));

            var root = Root("Crop_Mandrake", 3f, out var v);
            var col = root.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 0.24f, 0f); col.radius = 0.13f; col.height = 0.5f;

            Part(v, "Body", PrimitiveType.Capsule, new Vector3(0f, 0.24f, 0f), Vector3.zero, new Vector3(0.24f, 0.2f, 0.22f), skin);
            Part(v, "Brow", PrimitiveType.Capsule, new Vector3(0f, 0.37f, 0.085f), new Vector3(0f, 0f, 90f), new Vector3(0.05f, 0.09f, 0.04f), wrinkle);
            Part(v, "Wrinkle1", PrimitiveType.Capsule, new Vector3(0f, 0.17f, 0.1f), new Vector3(0f, 0f, 80f), new Vector3(0.025f, 0.08f, 0.02f), wrinkle);
            Part(v, "Wrinkle2", PrimitiveType.Capsule, new Vector3(0.02f, 0.12f, 0.095f), new Vector3(0f, 0f, 100f), new Vector3(0.02f, 0.07f, 0.02f), wrinkle);
            for (int s = -1; s <= 1; s += 2)
            {
                Part(v, "EyeHole" + s, PrimitiveType.Sphere, new Vector3(0.045f * s, 0.32f, 0.095f), Vector3.zero, new Vector3(0.05f, 0.06f, 0.03f), voidM);
                Part(v, "EyeGlint" + s, PrimitiveType.Sphere, new Vector3(0.045f * s, 0.325f, 0.108f), Vector3.zero, new Vector3(0.012f, 0.012f, 0.01f), glint);
                Part(v, "Arm" + s, PrimitiveType.Capsule, new Vector3(0.13f * s, 0.22f, 0.02f), new Vector3(0f, 0f, -35f * s), new Vector3(0.035f, 0.09f, 0.035f), skin);
                Part(v, "Hand" + s, PrimitiveType.Capsule, new Vector3(0.19f * s, 0.29f, 0.03f), new Vector3(0f, 0f, -70f * s), new Vector3(0.02f, 0.05f, 0.02f), wrinkle);
                Part(v, "Leg" + s, PrimitiveType.Capsule, new Vector3(0.05f * s, 0.05f, 0f), new Vector3(0f, 0f, 15f * s), new Vector3(0.04f, 0.07f, 0.04f), skin);
            }
            Part(v, "Taproot", PrimitiveType.Capsule, new Vector3(0f, -0.04f, -0.02f), new Vector3(10f, 0f, 0f), new Vector3(0.02f, 0.07f, 0.02f), wrinkle);
            var mouth = Part(v, "Mouth", PrimitiveType.Sphere, new Vector3(0f, 0.235f, 0.1f), Vector3.zero, new Vector3(0.055f, 0.04f, 0.03f), voidM);
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f + (i % 2) * 17f;
                Part(v, "Leaf" + i, PrimitiveType.Sphere, Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0.5f, 0.06f), new Vector3(-40f, a, 0f),
                     new Vector3(0.07f, 0.2f, 0.025f), leaf);
            }

            var goods = root.AddComponent<CursedCropGoods>();
            goods.Configure(Goods[0].id, Goods[0].value, Goods[0].fragility, Goods[0].hands, Goods[0].mass, true);
            root.AddComponent<MandrakeScream>().Configure(tuning, v, mouth);
            return Save(root, MandrakePrefab);
        }

        /// <summary>Bloated bruise-green melon with sickly stripes, crooked stitched grin, slit eyes and a curled black stem.</summary>
        static GameObject BuildMelon(CursedCropTuning tuning)
        {
            var rind = Mat("M_Crop_Melon_Rind", new Color(0.2f, 0.33f, 0.16f), 0.45f);
            var stripe = Mat("M_Crop_Melon_Stripe", new Color(0.55f, 0.62f, 0.3f), 0.45f);
            var stitch = Mat("M_Crop_Melon_Stitch", new Color(0.08f, 0.05f, 0.08f), 0.1f);
            var eye = Mat("M_Crop_Melon_Eye", new Color(0.85f, 0.25f, 0.15f), 0.6f, new Color(0.7f, 0.12f, 0.05f));
            var stem = Mat("M_Crop_Melon_Stem", new Color(0.12f, 0.09f, 0.06f), 0.1f);

            var root = Root("Crop_TumbleMelon", 14f, out var v);
            var col = root.AddComponent<SphereCollider>();
            col.center = new Vector3(0f, 0.34f, 0f); col.radius = 0.34f;
            var pm = new PhysicsMaterial("PM_TumbleMelon") { bounciness = 0.15f, dynamicFriction = 0.35f, staticFriction = 0.5f, frictionCombine = PhysicsMaterialCombine.Average };
            string pmPath = CropPrefabDir + "/PM_TumbleMelon.physicMaterial";
            var existing = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(pmPath);
            if (existing == null) { AssetDatabase.CreateAsset(pm, pmPath); existing = pm; }
            col.sharedMaterial = existing;

            Part(v, "Rind", PrimitiveType.Sphere, new Vector3(0f, 0.34f, 0f), Vector3.zero, new Vector3(0.7f, 0.66f, 0.72f), rind);
            for (int i = 0; i < 4; i++)
                Part(v, "Stripe" + i, PrimitiveType.Sphere, new Vector3(0f, 0.34f, 0f), new Vector3(0f, i * 45f + 22f, 0f), new Vector3(0.712f, 0.672f, 0.06f), stripe);
            for (int s = -1; s <= 1; s += 2)
            {
                Part(v, "Eye" + s, PrimitiveType.Cube, new Vector3(0.11f * s, 0.46f, 0.315f), new Vector3(-20f, 0f, 25f * s), new Vector3(0.1f, 0.025f, 0.03f), eye);
                Part(v, "EyeSocket" + s, PrimitiveType.Sphere, new Vector3(0.11f * s, 0.46f, 0.305f), Vector3.zero, new Vector3(0.13f, 0.07f, 0.04f), stitch);
            }
            // Crooked grin: a dark seam with cross-stitches along an arc.
            for (int i = -3; i <= 3; i++)
            {
                float x = i * 0.05f;
                float y = 0.25f + 0.0045f * i * i + (i == 2 ? 0.015f : 0f);
                float z = Mathf.Sqrt(Mathf.Max(0f, 0.34f * 0.34f - x * x)) * 0.97f;
                Part(v, "Seam" + i, PrimitiveType.Cube, new Vector3(x, y, z), new Vector3(0f, -x * 150f, i * 7f), new Vector3(0.055f, 0.012f, 0.02f), stitch);
                Part(v, "Stitch" + i, PrimitiveType.Cube, new Vector3(x, y, z + 0.004f), new Vector3(0f, -x * 150f, 70f), new Vector3(0.045f, 0.008f, 0.02f), stitch);
            }
            Part(v, "Stem", PrimitiveType.Capsule, new Vector3(0.02f, 0.7f, 0f), new Vector3(0f, 0f, -25f), new Vector3(0.035f, 0.07f, 0.035f), stem);
            Part(v, "StemCurl", PrimitiveType.Capsule, new Vector3(0.08f, 0.77f, 0f), new Vector3(0f, 0f, -95f), new Vector3(0.025f, 0.05f, 0.025f), stem);

            var goods = root.AddComponent<CursedCropGoods>();
            goods.Configure(Goods[1].id, Goods[1].value, Goods[1].fragility, Goods[1].hands, Goods[1].mass, true);
            root.AddComponent<TumbleMelon>().Configure(tuning, v);
            return Save(root, MelonPrefab);
        }
    }
}
