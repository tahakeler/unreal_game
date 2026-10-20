using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarvestPanic.Core;
using Unity.AI.Navigation;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering.Universal;

namespace HarvestPanic.AI.Editor
{
    /// <summary>
    /// Idempotent generator for everything the Critter AI team owns as assets:
    /// personalities (<c>Resources/Data/Critters</c>), tuning, animator controllers + prefabs (<c>Prefabs/Critters</c>),
    /// the <c>Resources/NetworkPrefabs/Critters.asset</c> list and the <c>Scenes/Dev/CritterTest.unity</c> scene (baked NavMesh).
    /// Batchmode: <c>Unity -batchmode -nographics -quit -projectPath HarvestPanic -executeMethod HarvestPanic.AI.Editor.CritterAssetBuilder.BuildAll</c>
    /// </summary>
    public static class CritterAssetBuilder
    {
        public const string DataDir = "Assets/Resources/Data/Critters";
        public const string PrefabDir = "Assets/Prefabs/Critters";
        public const string AnimDir = PrefabDir + "/Animation";
        public const string NetListPath = "Assets/Resources/NetworkPrefabs/Critters.asset";
        public const string ScenePath = "Assets/Scenes/Dev/CritterTest.unity";
        public const string SceneAssetsDir = "Assets/Scenes/Dev/CritterTest";
        public const string AnimalsDir = "Assets/ThirdParty/Animals";

        /// <summary>Fit rule for the model scale.</summary>
        public enum Fit { Length, Height }

        /// <summary>Per-species build spec (art + carry fallback); behaviour values live in the personality.</summary>
        public sealed class Spec
        {
            public string Name, Fbx, HeadBone, CritterId, GoodsId, ShortId;
            public Fit Fit; public float TargetM;
            public int BaseValue, CarryHands; public float Fragility, MassKg;
            public Color Tint = Color.white;
            /// <summary>Preferred rig-free Tripo mesh (<c>Assets/ThirdParty/Tripo/&lt;name&gt;</c>); the Quaternius FBX is the fallback.</summary>
            public string Tripo; public float TripoTargetM; public CritterProceduralAnim.Style AnimStyle;
        }

        public const string TripoDir = "Assets/ThirdParty/Tripo";
        public static string TripoFbx(Spec s) => string.IsNullOrEmpty(s.Tripo) ? null : $"{TripoDir}/{s.Tripo}/{s.Tripo}.fbx";

        public static readonly Spec[] Specs =
        {
            new Spec { Name = "Pig", Fbx = AnimalsDir + "/Pig/Pig.fbx", HeadBone = "Head", CritterId = "Critter.Pig", GoodsId = "Livestock.Pig", ShortId = "pig",
                       Fit = Fit.Length, TargetM = 1.10f, BaseValue = 60, CarryHands = 2, Fragility = 0.8f, MassKg = 30f, Tint = new Color(0.949f, 0.639f, 0.627f),
                       Tripo = "Pig_Hamlet", TripoTargetM = 1.10f, AnimStyle = CritterProceduralAnim.Style.Pig },
            new Spec { Name = "Chicken", Fbx = AnimalsDir + "/Chicken/Chicken.fbx", HeadBone = "Body_end", CritterId = "Critter.Chicken", GoodsId = "Livestock.Chicken", ShortId = "chicken",
                       Fit = Fit.Height, TargetM = 0.33f, BaseValue = 15, CarryHands = 1, Fragility = 0.6f, MassKg = 2.5f, Tint = new Color(0.97f, 0.95f, 0.88f),
                       Tripo = "Chicken", TripoTargetM = 0.40f, AnimStyle = CritterProceduralAnim.Style.Chicken },
            // Cow: goods-value $150 / fragility 0.7. Hands 4 (2 carriers) per the cow-goat feature brief (roster says 6/3: see docs/qa/cow-goat-progress.md).
            new Spec { Name = "Cow", Fbx = AnimalsDir + "/Cow/Cow.fbx", HeadBone = "Head", CritterId = "Critter.Cow", GoodsId = "Livestock.Cow", ShortId = "cow",
                       Fit = Fit.Length, TargetM = 2.10f, BaseValue = 150, CarryHands = 4, Fragility = 0.7f, MassKg = 300f, Tint = new Color(0.93f, 0.91f, 0.86f) },
            // Goat: $70 / 0.5 / 2 hands. No goat model in the CC0 packs yet: Quaternius Sheep rig, scaled down and tinted goat-brown.
            new Spec { Name = "Goat", Fbx = AnimalsDir + "/Sheep/Sheep.fbx", HeadBone = "Head", CritterId = "Critter.Goat", GoodsId = "Livestock.Goat", ShortId = "goat",
                       Fit = Fit.Length, TargetM = 1.05f, BaseValue = 70, CarryHands = 2, Fragility = 0.5f, MassKg = 35f, Tint = new Color(0.62f, 0.5f, 0.38f) },
        };

        public const string LivestockTuningPath = DataDir + "/LivestockTuning.asset";

        static LivestockTuning BuildLivestockTuning()
        {
            var t = AssetDatabase.LoadAssetAtPath<LivestockTuning>(LivestockTuningPath);
            if (t == null) { t = ScriptableObject.CreateInstance<LivestockTuning>(); AssetDatabase.CreateAsset(t, LivestockTuningPath); }
            return t;
        }

        /// <summary>Species signature components + a LODGroup (LOD0 = model, culled below 1.5% screen height).</summary>
        static void AddSignatureAndLod(GameObject root, Spec s, Transform model)
        {
            var lt = BuildLivestockTuning();
            if (s.Name == "Cow") root.AddComponent<CowBehaviour>().Configure(lt);
            if (s.Name == "Goat") root.AddComponent<GoatBehaviour>().Configure(lt, model);
            AddLod(root);
        }

        /// <summary>Critter LODs: LOD0 full mesh with shadows; LOD1 same mesh (shared) to 4%; culled below 1.5% of screen height.</summary>
        public static void AddLod(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;
            var lod = root.TryGetComponent<LODGroup>(out var existing) ? existing : root.AddComponent<LODGroup>();
            lod.fadeMode = LODFadeMode.None;
            lod.SetLODs(new[] { new LOD(0.04f, renderers), new LOD(0.015f, renderers) });
            lod.RecalculateBounds();
        }

        [MenuItem("Harvest Panic/Critters/Build Critter Assets")]
        public static void BuildAll()
        {
            try
            {
                EnsureDirs();
                var tuning = BuildTuning();
                var prefabs = new List<GameObject>();
                foreach (var s in Specs)
                {
                    var p = BuildPersonality(s);
                    var prefab = BuildPrefab(s, p, tuning);
                    SetProp(p, "Prefab", prefab);
                    prefabs.Add(prefab);
                }
                BuildNetworkList(prefabs);
                AssetDatabase.SaveAssets();
                Content.Critters.Invalidate();
                BuildTestScene();
                AssetDatabase.SaveAssets();
                Debug.Log("[CritterAssetBuilder] Done.");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        static void EnsureDirs()
        {
            foreach (var d in new[] { DataDir, PrefabDir, AnimDir, "Assets/Resources/NetworkPrefabs", SceneAssetsDir })
                if (!AssetDatabase.IsValidFolder(d)) Directory.CreateDirectory(d);
            AssetDatabase.Refresh();
        }

        // ------------------------------------------------------------------ data

        static CritterTuning BuildTuning()
        {
            string path = DataDir + "/CritterTuning.asset";
            var t = AssetDatabase.LoadAssetAtPath<CritterTuning>(path);
            if (t == null) { t = ScriptableObject.CreateInstance<CritterTuning>(); AssetDatabase.CreateAsset(t, path); }
            return t;
        }

        static CritterPersonality BuildPersonality(Spec s)
        {
            string path = $"{DataDir}/Critter_{s.Name}.asset";
            var p = AssetDatabase.LoadAssetAtPath<CritterPersonality>(path);
            if (p == null) { p = ScriptableObject.CreateInstance<CritterPersonality>(); AssetDatabase.CreateAsset(p, path); }
            var so = new SerializedObject(p);
            Set(so, "CritterId", s.CritterId);
            Set(so, "GoodsId", s.GoodsId);
            if (s.Name == "Pig")
            {
                // livestock-roster §3.1/3.2: Hamlet - greedy, sneaky, dramatic; 280/560 cm/s; feed bucket 0.9; SecureCarriers 2.
                Set(so, "Traits", (int)(CritterTrait.Greedy | CritterTrait.Sneaky | CritterTrait.Dramatic));
                Set(so, "MoveSpeed", 2.8f); Set(so, "SprintSpeed", 5.6f); Set(so, "Acceleration", 15f);
                Set(so, "AlertRadius", 10f); Set(so, "NoiseSensitivity", 1.0f); Set(so, "SightCone", 220f);
                Set(so, "CorneredResponse", (int)CorneredResponse.Juke); Set(so, "SizeClass", (int)SizeClass.Medium);
                Set(so, "Agility", 3f); Set(so, "CritterPullN", 500f); Set(so, "SecureCarriers", 2); Set(so, "StruggleImpulse", 150f);
                Set(so, "HerdMode", (int)HerdMode.None); Set(so, "ShortcutAccess", (int)NavShortcut.UnderFence);
                Set(so, "ComedyFlags", (int)ComedyFlag.Screams); Set(so, "Curiosity", 0.2f);
                SetLures(so, (LureTag.FeedBucket, 0.9f));
                SetNoise(so, (NoiseTag.Food, NoiseReaction.Lure, 0.9f), (NoiseTag.Whistle, NoiseReaction.Flee, 0.8f), (NoiseTag.WhistleLong, NoiseReaction.Ignore, 0f));
            }
            else if (s.Name == "Cow")
            {
                // Big Martha - lazy, stubborn: 150/300 cm/s, AlertRadius 6, NoiseSens 0.5, Freeze, feed bucket 0.95, SecureCarriers 2 (brief), barely flees.
                Set(so, "Traits", (int)(CritterTrait.Loud));
                Set(so, "MoveSpeed", 1.5f); Set(so, "SprintSpeed", 3.0f); Set(so, "Acceleration", 4f);
                Set(so, "AlertRadius", 6f); Set(so, "NoiseSensitivity", 0.5f); Set(so, "SightCone", 200f);
                Set(so, "AlertThreshold", 0.45f); Set(so, "FleeThreshold", 0.85f); Set(so, "CalmThreshold", 0.3f); Set(so, "CalmTime", 2f);
                Set(so, "CorneredResponse", (int)CorneredResponse.Freeze); Set(so, "SizeClass", (int)SizeClass.Large);
                Set(so, "Agility", 0f); Set(so, "CritterPullN", 900f); Set(so, "SecureCarriers", 2); Set(so, "StruggleImpulse", 200f);
                Set(so, "StruggleIntervalMin", 1.5f); Set(so, "StruggleIntervalMax", 3f);
                Set(so, "HerdMode", (int)HerdMode.None); Set(so, "ShortcutAccess", (int)NavShortcut.None);
                Set(so, "ComedyFlags", (int)ComedyFlag.None); Set(so, "Curiosity", 0.05f); Set(so, "PenEscapeChance", 0.3f);
                SetLures(so, (LureTag.FeedBucket, 0.95f));
                SetNoise(so, (NoiseTag.Food, NoiseReaction.Lure, 0.95f), (NoiseTag.Whistle, NoiseReaction.Ignore, 0f), (NoiseTag.WhistleLong, NoiseReaction.Ignore, 0f));
            }
            else if (s.Name == "Goat")
            {
                // Gary - curious, climber, chaotic: 300/520 cm/s, AlertRadius 9, NoiseSens 0.7, Charge, feed 0.4 / shiny 0.5, Curiosity 0.6.
                Set(so, "Traits", (int)(CritterTrait.Climber | CritterTrait.Aggressive | CritterTrait.Thief));
                Set(so, "MoveSpeed", 3.0f); Set(so, "SprintSpeed", 5.2f); Set(so, "Acceleration", 18f);
                Set(so, "AlertRadius", 9f); Set(so, "NoiseSensitivity", 0.7f); Set(so, "SightCone", 240f);
                Set(so, "CorneredResponse", (int)CorneredResponse.Charge); Set(so, "ChargeImpulse", 1100f); Set(so, "SizeClass", (int)SizeClass.Medium);
                Set(so, "Agility", 4f); Set(so, "CritterPullN", 400f); Set(so, "SecureCarriers", 1); Set(so, "StruggleImpulse", 120f);
                Set(so, "HerdMode", (int)HerdMode.None); Set(so, "ShortcutAccess", (int)NavShortcut.Climb);
                Set(so, "ComedyFlags", (int)ComedyFlag.None); Set(so, "Curiosity", 0.6f); Set(so, "Aggression", 0.5f);
                SetLures(so, (LureTag.FeedBucket, 0.4f), (LureTag.Shiny, 0.5f));
                SetNoise(so, (NoiseTag.Food, NoiseReaction.Lure, 0.4f), (NoiseTag.Whistle, NoiseReaction.Ignore, 0f), (NoiseTag.WhistleLong, NoiseReaction.Ignore, 0f));
            }
            else
            {
                // Chicken: skittish flock; 250/620 cm/s; AlertRadius 8; NoiseSens 1.4; whistle 0.6 / feed 0.5.
                Set(so, "Traits", (int)(CritterTrait.Skittish | CritterTrait.Herd));
                Set(so, "MoveSpeed", 2.5f); Set(so, "SprintSpeed", 6.2f); Set(so, "Acceleration", 25f);
                Set(so, "AlertRadius", 8f); Set(so, "NoiseSensitivity", 1.4f); Set(so, "SightCone", 300f);
                Set(so, "AlertThreshold", 0.25f); Set(so, "FleeThreshold", 0.45f);
                Set(so, "CorneredResponse", (int)CorneredResponse.Juke); Set(so, "SizeClass", (int)SizeClass.Small);
                Set(so, "Agility", 4f); Set(so, "CritterPullN", 50f); Set(so, "SecureCarriers", 1); Set(so, "StruggleImpulse", 40f);
                Set(so, "HerdMode", (int)HerdMode.Flock); Set(so, "ShortcutAccess", (int)NavShortcut.None);
                Set(so, "ComedyFlags", (int)ComedyFlag.None); Set(so, "CalmTime", 3f);
                SetLures(so, (LureTag.Whistle, 0.6f), (LureTag.FeedBucket, 0.5f));
                SetNoise(so, (NoiseTag.Food, NoiseReaction.Lure, 0.5f), (NoiseTag.Whistle, NoiseReaction.Flee, 0.8f), (NoiseTag.WhistleLong, NoiseReaction.Ignore, 0f));
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(p);
            return p;
        }

        static void SetLures(SerializedObject so, params (LureTag tag, float aff)[] items)
        {
            var arr = so.FindProperty(Backing("LureAffinity"));
            arr.arraySize = items.Length;
            for (int i = 0; i < items.Length; i++)
            {
                var e = arr.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("Lure").intValue = (int)items[i].tag;
                e.FindPropertyRelative("Affinity").floatValue = items[i].aff;
            }
        }

        static void SetNoise(SerializedObject so, params (NoiseTag tag, NoiseReaction r, float s)[] items)
        {
            var arr = so.FindProperty(Backing("NoiseTagResponses"));
            arr.arraySize = items.Length;
            for (int i = 0; i < items.Length; i++)
            {
                var e = arr.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("Tag").intValue = (int)items[i].tag;
                e.FindPropertyRelative("Reaction").intValue = (int)items[i].r;
                e.FindPropertyRelative("Strength").floatValue = items[i].s;
            }
        }

        static string Backing(string prop) => $"<{prop}>k__BackingField";

        static void Set(SerializedObject so, string prop, object value)
        {
            var sp = so.FindProperty(Backing(prop)) ?? so.FindProperty(prop);
            if (sp == null) throw new InvalidOperationException($"Property {prop} not found on {so.targetObject.GetType().Name}");
            switch (value)
            {
                case string s: sp.stringValue = s; break;
                case float f: sp.floatValue = f; break;
                case int i: sp.intValue = i; break;
                case bool b: sp.boolValue = b; break;
                case UnityEngine.Object o: sp.objectReferenceValue = o; break;
                default: throw new ArgumentException(prop);
            }
        }

        static void SetProp(UnityEngine.Object target, string prop, object value)
        {
            var so = new SerializedObject(target);
            Set(so, prop, value);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        // ------------------------------------------------------------------ animation

        /// <summary>Clip whose name ends with one of <paramref name="suffixes"/> after the take separator.</summary>
        public static AnimationClip FindClip(IEnumerable<AnimationClip> clips, params string[] suffixes)
        {
            foreach (var suf in suffixes)
                foreach (var c in clips)
                {
                    string n = c.name;
                    int bar = n.LastIndexOf('|');
                    string tail = bar >= 0 ? n.Substring(bar + 1) : n;
                    if (string.Equals(tail, suf, StringComparison.OrdinalIgnoreCase)) return c;
                }
            return null;
        }

        static AnimatorController BuildController(Spec s, CritterPersonality p, out AnimationClip idle)
        {
            var clips = AssetDatabase.LoadAllAssetsAtPath(s.Fbx).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToList();
            idle = FindClip(clips, "Idle");
            var walk = FindClip(clips, "Walk");
            var run = FindClip(clips, "Run", "Gallop");
            if (idle == null) throw new InvalidOperationException($"{s.Name}: no Idle clip in {string.Join(", ", clips.Select(c => c.name))}");
            float walkScale = 1f, runScale = 1f;
            if (walk == null && run != null) { walk = run; walkScale = 0.5f; }    // chicken has no walk clip
            if (run == null && walk != null) { run = walk; runScale = 2f; }       // pigeon has no run clip

            string path = $"{AnimDir}/AC_{s.Name}.controller";
            AssetDatabase.DeleteAsset(path);
            var ac = AnimatorController.CreateAnimatorControllerAtPath(path);
            ac.AddParameter(CritterPresentation.SpeedParam, AnimatorControllerParameterType.Float);
            var state = ac.CreateBlendTreeInController("Locomotion", out var tree, 0);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = CritterPresentation.SpeedParam;
            tree.useAutomaticThresholds = false;
            float walkSpeed = p.MoveSpeed * CritterTuning.Default.WanderSpeedFraction;
            tree.children = new[]
            {
                new ChildMotion { motion = idle, threshold = 0f, timeScale = 1f },
                new ChildMotion { motion = walk ?? idle, threshold = walkSpeed, timeScale = walkScale },
                new ChildMotion { motion = run ?? idle, threshold = p.SprintSpeed, timeScale = runScale },
            };
            ac.layers[0].stateMachine.defaultState = state;
            EditorUtility.SetDirty(ac);
            EditorUtility.SetDirty(tree);
            return ac;
        }

        // ------------------------------------------------------------------ prefab

        static GameObject BuildPrefab(Spec s, CritterPersonality p, CritterTuning tuning)
        {
            var tripo = TripoFbx(s) != null ? AssetDatabase.LoadAssetAtPath<GameObject>(TripoFbx(s)) : null;
            if (tripo != null) return BuildTripoPrefab(s, p, tuning, tripo);
            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(s.Fbx) ?? throw new InvalidOperationException("Missing " + s.Fbx);
            var ac = BuildController(s, p, out _);

            var root = new GameObject("Critter_" + s.Name);
            int layer = LayerMask.NameToLayer(GameLayers.Critter);
            try
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
                model.name = "Model";
                model.transform.SetParent(root.transform, false);
                var bounds = FitModel(model.transform, s);

                foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
                var mat = SpeciesMaterial(s);
                foreach (var r in model.GetComponentsInChildren<Renderer>(true))
                    r.sharedMaterials = Enumerable.Repeat(mat, r.sharedMaterials.Length).ToArray();
                var anim = model.GetComponent<Animator>() ?? model.AddComponent<Animator>();
                anim.runtimeAnimatorController = ac;
                anim.applyRootMotion = false;
                anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

                var box = root.AddComponent<BoxCollider>();
                box.center = bounds.center;
                box.size = bounds.size;

                var rb = root.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.mass = s.MassKg;
                rb.interpolation = RigidbodyInterpolation.None; // agent drives the transform; brain enables interpolation while thrown
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

                var agent = root.AddComponent<NavMeshAgent>();
                agent.radius = Mathf.Clamp(Mathf.Min(bounds.size.x, bounds.size.z) * 0.5f, 0.1f, 1f);
                agent.height = Mathf.Max(0.2f, bounds.size.y);
                agent.speed = p.MoveSpeed; agent.acceleration = p.Acceleration; agent.angularSpeed = 720f;
                agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;

                var eye = new GameObject("Eye").transform;
                eye.SetParent(root.transform, false);
                eye.localPosition = new Vector3(0f, bounds.size.y * 0.8f, bounds.max.z * 0.8f);

                root.AddComponent<NetworkObject>();
                var nt = root.AddComponent<NetworkTransform>();
                nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false;
                var na = root.AddComponent<NetworkAnimator>();
                na.Animator = anim;

                var brain = root.AddComponent<CritterBrain>();
                var bso = new SerializedObject(brain);
                bso.FindProperty("personality").objectReferenceValue = p;
                bso.FindProperty("tuning").objectReferenceValue = tuning;
                bso.FindProperty("eye").objectReferenceValue = eye;
                bso.ApplyModifiedPropertiesWithoutUndo();

                root.AddComponent<CritterGoods>().ConfigureFallback(s.GoodsId, s.ShortId, s.BaseValue, s.Fragility, s.CarryHands, s.MassKg);
                root.AddComponent<CritterCarryable>();
                root.AddComponent<CritterPresentation>().Configure(model.transform, anim);
                AddSignatureAndLod(root, s, model.transform);

                string path = $"{PrefabDir}/Critter_{s.Name}.prefab";
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"[CritterAssetBuilder] {s.Name}: bounds size {bounds.size} (m), scale {model.transform.localScale.x:0.####}");
                return prefab;
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        /// <summary>
        /// Rig-free variant: Tripo mesh under a "Model" pivot animated by <see cref="CritterProceduralAnim"/>
        /// (no Animator / NetworkAnimator; animation is derived locally from replicated state + motion).
        /// </summary>
        static GameObject BuildTripoPrefab(Spec s, CritterPersonality p, CritterTuning tuning, GameObject tripo)
        {
            var root = new GameObject("Critter_" + s.Name);
            int layer = LayerMask.NameToLayer(GameLayers.Critter);
            try
            {
                var pivot = new GameObject("Model").transform;
                pivot.SetParent(root.transform, false);
                var mesh = (GameObject)PrefabUtility.InstantiatePrefab(tripo);
                mesh.name = "Mesh";
                mesh.transform.SetParent(pivot, false);
                // Tripo export is already real-size, +Z forward, feet at 0; re-fit defensively to the spec size.
                var fit = new Spec { Fit = s.Fit, TargetM = s.TripoTargetM > 0f ? s.TripoTargetM : s.TargetM };
                var bounds = FitModel(mesh.transform, fit);
                foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
                var renderer = mesh.GetComponentInChildren<Renderer>();
                foreach (var r in mesh.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

                var box = root.AddComponent<BoxCollider>();
                box.center = bounds.center;
                box.size = bounds.size;

                var rb = root.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.mass = s.MassKg;
                rb.interpolation = RigidbodyInterpolation.None;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

                var agent = root.AddComponent<NavMeshAgent>();
                agent.radius = Mathf.Clamp(Mathf.Min(bounds.size.x, bounds.size.z) * 0.5f, 0.1f, 1f);
                agent.height = Mathf.Max(0.2f, bounds.size.y);
                agent.speed = p.MoveSpeed; agent.acceleration = p.Acceleration; agent.angularSpeed = 720f;
                agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;

                var eye = new GameObject("Eye").transform;
                eye.SetParent(root.transform, false);
                eye.localPosition = new Vector3(0f, bounds.size.y * 0.8f, bounds.max.z * 0.8f);

                root.AddComponent<NetworkObject>();
                var nt = root.AddComponent<NetworkTransform>();
                nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false;

                var brain = root.AddComponent<CritterBrain>();
                var bso = new SerializedObject(brain);
                bso.FindProperty("personality").objectReferenceValue = p;
                bso.FindProperty("tuning").objectReferenceValue = tuning;
                bso.FindProperty("eye").objectReferenceValue = eye;
                bso.ApplyModifiedPropertiesWithoutUndo();

                root.AddComponent<CritterGoods>().ConfigureFallback(s.GoodsId, s.ShortId, s.BaseValue, s.Fragility, s.CarryHands, s.MassKg);
                root.AddComponent<CritterCarryable>();
                root.AddComponent<CritterPresentation>().Configure(null, null);   // procedural anim owns the pivot
                root.AddComponent<CritterProceduralAnim>().Configure(pivot, s.AnimStyle, bounds.size.y * 0.45f, renderer);
                AddSignatureAndLod(root, s, pivot);

                string path = $"{PrefabDir}/Critter_{s.Name}.prefab";
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"[CritterAssetBuilder] {s.Name} (Tripo): bounds size {bounds.size} (m), scale {mesh.transform.localScale.x:0.####}");
                return prefab;
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        /// <summary>
        /// Rotates the model so its head points +Z, scales it to the spec (length or height), and puts the feet at y = 0.
        /// Returns the final local bounds relative to the prefab root.
        /// </summary>
        static Bounds FitModel(Transform model, Spec s)
        {
            model.localPosition = Vector3.zero; model.localRotation = Quaternion.identity; model.localScale = Vector3.one;
            var b = MeasureLocal(model.parent, model);
            var head = string.IsNullOrEmpty(s.HeadBone) ? null : model.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == s.HeadBone);
            if (head != null)
            {
                Vector3 toHead = model.parent.InverseTransformPoint(head.position) - b.center; toHead.y = 0f;
                if (toHead.sqrMagnitude > 1e-8f)
                {
                    float yaw = Mathf.Atan2(toHead.x, toHead.z) * Mathf.Rad2Deg;
                    yaw = Mathf.Round(yaw / 90f) * 90f; // snap: FBX axes are orthogonal
                    model.localRotation = Quaternion.Euler(0f, -yaw, 0f);
                }
            }
            b = MeasureLocal(model.parent, model);
            float current = s.Fit == Fit.Height ? b.size.y : Mathf.Max(b.size.x, b.size.z);
            if (current > 1e-5f) model.localScale = Vector3.one * (s.TargetM / current);
            b = MeasureLocal(model.parent, model);
            model.localPosition = new Vector3(-b.center.x, -b.min.y, -b.center.z);
            return MeasureLocal(model.parent, model);
        }

        /// <summary>Axis-aligned bounds of all (baked) renderers under <paramref name="model"/> in <paramref name="space"/> local coordinates.</summary>
        public static Bounds MeasureLocal(Transform space, Transform model)
        {
            bool any = false; var b = new Bounds();
            void Add(Vector3 world)
            {
                var p = space.InverseTransformPoint(world);
                if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p);
            }
            foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr.sharedMesh == null) continue;
                var l2w = smr.transform.localToWorldMatrix;
                foreach (var v in smr.sharedMesh.vertices) Add(l2w.MultiplyPoint3x4(v));
            }
            foreach (var mf in model.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh != null) foreach (var v in mf.sharedMesh.vertices) Add(mf.transform.TransformPoint(v));
            return b;
        }

        /// <summary>URP Lit material per species (art-bible tint; the FBX flat colours do not survive URP import).</summary>
        static Material SpeciesMaterial(Spec s)
        {
            string path = $"{PrefabDir}/Materials/M_Critter_{s.Name}.mat";
            if (!AssetDatabase.IsValidFolder(PrefabDir + "/Materials")) AssetDatabase.CreateFolder(PrefabDir, "Materials");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.SetColor("_BaseColor", s.Tint);
            m.SetFloat("_Smoothness", 0.15f);
            EditorUtility.SetDirty(m);
            return m;
        }

        static void BuildNetworkList(List<GameObject> prefabs)
        {
            var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(NetListPath);
            if (list == null) { list = ScriptableObject.CreateInstance<NetworkPrefabsList>(); AssetDatabase.CreateAsset(list, NetListPath); }
            foreach (var go in prefabs)
                if (!list.PrefabList.Any(x => x.Prefab == go)) list.Add(new NetworkPrefab { Prefab = go });
            EditorUtility.SetDirty(list);
        }

        // ------------------------------------------------------------------ test scene

        static Material Mat(string name, Color c)
        {
            string path = $"{SceneAssetsDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", 0.1f);
            EditorUtility.SetDirty(m);
            return m;
        }

        static void BuildTestScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.5f; sun.shadows = LightShadows.Soft;
            sun.color = new Color(1f, 0.95f, 0.85f);
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.6f, 0.72f, 0.9f);
            RenderSettings.ambientEquatorColor = new Color(0.5f, 0.55f, 0.45f);
            RenderSettings.ambientGroundColor = new Color(0.3f, 0.25f, 0.2f);

            var env = new GameObject("Environment");
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground (60x60 m)";
            ground.transform.SetParent(env.transform);
            ground.transform.localScale = new Vector3(6f, 1f, 6f);
            ground.GetComponent<Renderer>().sharedMaterial = Mat("M_Grass", new Color(0.36f, 0.6f, 0.26f));
            var wood = Mat("M_Wood", new Color(0.55f, 0.38f, 0.22f));
            void Box(string n, Vector3 pos, Vector3 size)
            {
                var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
                g.name = n; g.transform.SetParent(env.transform);
                g.transform.position = pos + Vector3.up * size.y * 0.5f; g.transform.localScale = size;
                g.GetComponent<Renderer>().sharedMaterial = wood;
            }
            // Pen corner (for Cornered tests) and a hay bale for line-of-sight.
            Box("Fence_N", new Vector3(-14f, 0f, 14f), new Vector3(12f, 1.1f, 0.2f));
            Box("Fence_W", new Vector3(-20f, 0f, 8f), new Vector3(0.2f, 1.1f, 12f));
            Box("HayBale", new Vector3(8f, 0f, -6f), new Vector3(2f, 1.2f, 1.2f));

            var surface = env.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();
            string navPath = $"{SceneAssetsDir}/NavMesh_CritterTest.asset";
            AssetDatabase.DeleteAsset(navPath);
            AssetDatabase.CreateAsset(surface.navMeshData, navPath);
            surface.navMeshData = AssetDatabase.LoadAssetAtPath<NavMeshData>(navPath);

            void Marker(string n, string key, int count, Vector3 pos)
            {
                var g = new GameObject(n);
                g.transform.position = pos;
                g.AddComponent<LevelMarker>().Configure(MarkerRole.CritterSpawner, key, count);
                try { g.tag = MarkerTag.CritterSpawner; } catch (UnityException) { Debug.LogWarning("Tag HP_CritterSpawner missing"); }
            }
            Marker("CritterSpawner_Pig", "Critter.Pig", 1, new Vector3(0f, 0f, 0f));
            Marker("CritterSpawner_Chicken", "Critter.Chicken", 6, new Vector3(5f, 0f, 4f));
            Marker("CritterSpawner_Cow", "Critter.Cow", 1, new Vector3(-6f, 0f, 6f));
            Marker("CritterSpawner_Goat", "Critter.Goat", 1, new Vector3(6f, 0f, -4f));

            var cam = new GameObject("Main Camera").AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.transform.position = new Vector3(1.5f, 3.2f, -6.5f);
            cam.transform.LookAt(new Vector3(2.5f, 0.3f, 2f));
            cam.fieldOfView = 50f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.55f, 0.75f, 0.95f);
            cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            cam.gameObject.AddComponent<AudioListener>();

            EditorSceneManager.SaveScene(scene, ScenePath);
        }
    }
}
