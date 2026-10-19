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
        public const string ControllerPath = AnimDir + "/FarmerBody.controller";
        public const string PanelPath = UiDir + "/WardrobePanel.asset";
        public const string ThemePath = UiDir + "/WardrobeTheme.tss";
        public const string PrefabPath = "Assets/Prefabs/Farmer/FarmerBody.prefab";
        public const string NetListPath = "Assets/Resources/NetworkPrefabs/Customization.asset";
        public const string ScenePath = "Assets/Scenes/Dev/WardrobeTest.unity";

        /// <summary>Clip name -> FBX take suffix, loop.</summary>
        static readonly (string name, string take, bool loop)[] Clips =
        {
            ("Idle", "Idle", true), ("Walk", "Walk", true), ("Run", "Run", true), ("Wave", "Wave", false),
            ("Faint", "Death", false), ("Point", "Idle_Gun_Pointing", true), ("Interact", "Interact", false),
        };

        [MenuItem("HarvestPanic/Customization/Build All Farmer Assets")]
        public static void BuildAll()
        {
            try
            {
                foreach (var d in new[] { AnimDir, MatDir, UiDir, "Assets/Prefabs/Farmer", "Assets/Resources/NetworkPrefabs", "Assets/Scenes/Dev" }) EnsureFolder(d);
                ConfigureFbx();
                var (baseMat, unlitMat) = BuildMaterials();
                var clips = LoadClips();
                var dance = BuildProceduralClip("Farmer_Dance", 1.0f, true, DancePose);
                var jump = BuildProceduralClip("Farmer_Jump", 0.7f, false, JumpPose);
                var controller = BuildController(clips, dance, jump);
                var panel = BuildPanel();
                var catalog = BuildCatalog(baseMat, unlitMat, panel);
                var prefab = BuildPrefab(catalog, controller);
                catalog.farmerPrefab = prefab;
                EditorUtility.SetDirty(catalog);
                BuildNetworkList();
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

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        // ---- FBX ----------------------------------------------------------------------------------------------------

        static void ConfigureFbx()
        {
            var imp = (ModelImporter)AssetImporter.GetAtPath(FbxPath);
            if (imp == null) throw new FileNotFoundException(FbxPath);
            imp.animationType = ModelImporterAnimationType.Generic;
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.importAnimation = true;
            imp.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            imp.importBlendShapes = false;
            imp.importCameras = false; imp.importLights = false;
            var takes = imp.defaultClipAnimations;
            Debug.Log("[FarmerAssetBuilder] FBX takes: " + string.Join(", ", takes.Select(t => t.takeName)));
            var list = new List<ModelImporterClipAnimation>();
            foreach (var (name, take, loop) in Clips)
            {
                var src = takes.FirstOrDefault(t => t.takeName == take || t.takeName.EndsWith("|" + take));
                if (src == null) { Debug.LogWarning("[FarmerAssetBuilder] Missing take " + take); continue; }
                src.name = name;
                src.loopTime = loop;
                src.loopPose = false;
                src.lockRootRotation = src.lockRootHeightY = src.lockRootPositionXZ = true;
                src.keepOriginalOrientation = src.keepOriginalPositionY = src.keepOriginalPositionXZ = true;
                list.Add(src);
            }
            imp.clipAnimations = list.ToArray();
            imp.SaveAndReimport();
        }

        static Dictionary<string, AnimationClip> LoadClips()
        {
            var d = new Dictionary<string, AnimationClip>();
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(FbxPath))
                if (o is AnimationClip c && !c.name.StartsWith("__preview__")) d[c.name] = c;
            Debug.Log("[FarmerAssetBuilder] Clips: " + string.Join(", ", d.Keys));
            return d;
        }

        // ---- Procedural clips ---------------------------------------------------------------------------------------

        /// <summary>Pose offsets for one key: per-bone world-space rotation (character frame) and position offsets.</summary>
        delegate void PoseFn(float t, Dictionary<string, Quaternion> rot, Dictionary<string, Vector3> pos);

        static void DancePose(float t, Dictionary<string, Quaternion> rot, Dictionary<string, Vector3> pos)
        {
            float ph = t * Mathf.PI * 2f;
            pos["Body"] = new Vector3(0f, -0.035f * (0.5f - 0.5f * Mathf.Cos(2f * ph)), 0f);
            rot["Body"] = Quaternion.AngleAxis(7f * Mathf.Sin(ph), Vector3.forward);
            rot["Torso"] = Quaternion.AngleAxis(16f * Mathf.Sin(ph), Vector3.up);
            rot["Head"] = Quaternion.AngleAxis(12f * Mathf.Sin(2f * ph), Vector3.right) * Quaternion.AngleAxis(-9f * Mathf.Sin(ph), Vector3.forward);
            float flap = 25f + 40f * (0.5f + 0.5f * Mathf.Sin(2f * ph));
            rot["UpperArm.L"] = Quaternion.AngleAxis(-flap, Vector3.forward);
            rot["UpperArm.R"] = Quaternion.AngleAxis(flap, Vector3.forward);
            rot["LowerArm.L"] = Quaternion.AngleAxis(80f, Vector3.forward);
            rot["LowerArm.R"] = Quaternion.AngleAxis(-80f, Vector3.forward);
        }

        static void JumpPose(float t, Dictionary<string, Quaternion> rot, Dictionary<string, Vector3> pos)
        {
            // 0-0.12 crouch, 0.12-0.5 tuck in the air (arms up), 0.5-0.7 land
            float crouch = t < 0.12f ? t / 0.12f : t < 0.22f ? 1f - (t - 0.12f) / 0.1f : t > 0.55f ? Mathf.Sin((t - 0.55f) / 0.15f * Mathf.PI) * 0.6f : 0f;
            float air = t < 0.12f ? 0f : t < 0.25f ? (t - 0.12f) / 0.13f : t < 0.45f ? 1f : t < 0.6f ? 1f - (t - 0.45f) / 0.15f : 0f;
            pos["Body"] = new Vector3(0f, -0.09f * crouch, 0f);
            rot["Torso"] = Quaternion.AngleAxis(12f * crouch - 6f * air, Vector3.right);
            rot["UpperArm.L"] = Quaternion.AngleAxis(-140f * air - 20f * crouch, Vector3.forward);
            rot["UpperArm.R"] = Quaternion.AngleAxis(140f * air + 20f * crouch, Vector3.forward);
            rot["UpperLeg.L"] = Quaternion.AngleAxis(-35f * air, Vector3.right);
            rot["UpperLeg.R"] = Quaternion.AngleAxis(-20f * air, Vector3.right);
            rot["LowerLeg.L"] = Quaternion.AngleAxis(55f * air, Vector3.right);
            rot["LowerLeg.R"] = Quaternion.AngleAxis(40f * air, Vector3.right);
            pos["Foot.L"] = new Vector3(0f, 0.14f * air, 0.05f * air);
            pos["Foot.R"] = new Vector3(0f, 0.1f * air, 0.02f * air);
        }

        /// <summary>
        /// Authors a Generic clip keyed on every bone: base pose = Idle frame 0, plus character-space offsets from
        /// <paramref name="pose"/> applied parent-first so children inherit their parent's motion.
        /// </summary>
        static AnimationClip BuildProceduralClip(string name, float length, bool loop, PoseFn pose)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            var idle = AssetDatabase.LoadAllAssetsAtPath(FbxPath).OfType<AnimationClip>().First(c => c.name == "Idle");
            var go = (GameObject)Object.Instantiate(model);
            try
            {
                go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                go.transform.localScale = Vector3.one;
                idle.SampleAnimation(go, 0f);
                var bones = go.GetComponentsInChildren<Transform>(true).Where(t => t != go.transform && t.GetComponent<Renderer>() == null).ToList();
                var baseRot = bones.ToDictionary(b => b, b => b.localRotation);
                var basePos = bones.ToDictionary(b => b, b => b.localPosition);
                var byName = new Dictionary<string, Transform>();
                foreach (var b in bones) if (!byName.ContainsKey(b.name)) byName[b.name] = b;

                const float fps = 24f;
                int frames = Mathf.RoundToInt(length * fps);
                var curves = new Dictionary<(Transform, string), AnimationCurve>();
                AnimationCurve Curve(Transform b, string prop)
                {
                    if (!curves.TryGetValue((b, prop), out var c)) curves[(b, prop)] = c = new AnimationCurve();
                    return c;
                }
                var rot = new Dictionary<string, Quaternion>();
                var pos = new Dictionary<string, Vector3>();
                for (int f = 0; f <= frames; f++)
                {
                    float t = f / fps;
                    foreach (var b in bones) { b.localRotation = baseRot[b]; b.localPosition = basePos[b]; }
                    rot.Clear(); pos.Clear();
                    pose(loop ? t / length : t, rot, pos); // Dance takes a 0..1 phase, Jump takes seconds
                    foreach (var b in bones) // GetComponentsInChildren is depth-first parent-first
                    {
                        if (rot.TryGetValue(b.name, out var q)) b.rotation = q * b.rotation;
                        if (pos.TryGetValue(b.name, out var p)) b.position += p;
                    }
                    foreach (var b in bones)
                    {
                        var lr = b.localRotation; var lp = b.localPosition;
                        Curve(b, "m_LocalRotation.x").AddKey(t, lr.x); Curve(b, "m_LocalRotation.y").AddKey(t, lr.y);
                        Curve(b, "m_LocalRotation.z").AddKey(t, lr.z); Curve(b, "m_LocalRotation.w").AddKey(t, lr.w);
                        Curve(b, "m_LocalPosition.x").AddKey(t, lp.x); Curve(b, "m_LocalPosition.y").AddKey(t, lp.y);
                        Curve(b, "m_LocalPosition.z").AddKey(t, lp.z);
                    }
                }

                string path = AnimDir + "/" + name + ".anim";
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip == null) { clip = new AnimationClip { name = name }; AssetDatabase.CreateAsset(clip, path); }
                clip.ClearCurves();
                clip.frameRate = fps;
                foreach (var kv in curves)
                {
                    string rel = AnimationUtility.CalculateTransformPath(kv.Key.Item1, go.transform);
                    clip.SetCurve(rel, typeof(Transform), kv.Key.Item2, kv.Value);
                }
                clip.EnsureQuaternionContinuity();
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = loop;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                EditorUtility.SetDirty(clip);
                return clip;
            }
            finally { Object.DestroyImmediate(go); }
        }

        // ---- Controller ---------------------------------------------------------------------------------------------

        static AnimatorController BuildController(Dictionary<string, AnimationClip> clips, AnimationClip dance, AnimationClip jump)
        {
            AssetDatabase.DeleteAsset(ControllerPath);
            var ac = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            ac.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ac.AddParameter(new AnimatorControllerParameter { name = "Grounded", type = AnimatorControllerParameterType.Bool, defaultBool = true });
            ac.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
            ac.AddParameter("Fainted", AnimatorControllerParameterType.Bool);
            var sm = ac.layers[0].stateMachine;

            var loco = ac.CreateBlendTreeInController("Locomotion", out var tree, 0);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            tree.AddChild(clips["Idle"], 0f);
            tree.AddChild(clips["Walk"], 1.6f);
            tree.AddChild(clips["Run"], 4.5f);
            sm.defaultState = loco;

            AnimatorState State(string n, Motion m, Vector3 p) { var s = sm.AddState(n, p); s.motion = m; return s; }
            var jumpS = State("Jump", jump, new Vector3(300, -120));
            var wave = State("Wave", clips["Wave"], new Vector3(300, 0));
            var point = State("Point", clips["Point"], new Vector3(300, 60));
            var danceS = State("Dance", dance, new Vector3(300, 120));
            var faint = State("Faint", clips["Faint"], new Vector3(300, 180));

            var anyJump = sm.AddAnyStateTransition(jumpS);
            anyJump.AddCondition(AnimatorConditionMode.If, 0, "Jump");
            anyJump.AddCondition(AnimatorConditionMode.IfNot, 0, "Fainted");
            anyJump.canTransitionToSelf = false; anyJump.duration = 0.08f;
            Exit(jumpS, loco, 0.92f);
            Exit(wave, loco, 0.95f);
            Exit(point, loco, 0.72f);
            var danceOut = danceS.AddTransition(loco);
            danceOut.hasExitTime = false; danceOut.duration = 0.2f;
            danceOut.AddCondition(AnimatorConditionMode.Greater, 0.6f, "Speed");
            var anyFaint = sm.AddAnyStateTransition(faint);
            anyFaint.AddCondition(AnimatorConditionMode.If, 0, "Fainted");
            anyFaint.canTransitionToSelf = false; anyFaint.duration = 0.1f;
            var faintOut = faint.AddTransition(loco);
            faintOut.hasExitTime = false; faintOut.duration = 0.35f;
            faintOut.AddCondition(AnimatorConditionMode.IfNot, 0, "Fainted");
            EditorUtility.SetDirty(ac);
            return ac;
        }

        static void Exit(AnimatorState from, AnimatorState to, float exitTime)
        {
            var t = from.AddTransition(to);
            t.hasExitTime = true; t.exitTime = exitTime; t.duration = 0.2f;
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
            cat.baseMaterial = lit; cat.unlitMaterial = unlit; cat.wardrobePanel = panel;
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

        /// <summary>Material name on the imported FBX -> zone (+ fixed colour).</summary>
        static (BodyZone, Color) ZoneFor(string renderer, string material)
        {
            string m = material.Split(' ')[0];
            Color C(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }
            switch (m)
            {
                case "LightBlue": return (BodyZone.Primary, default);
                case "Skin": return (BodyZone.Skin, default);
                case "Brown": return renderer.Contains("Feet") ? (BodyZone.Accent, default) : (BodyZone.Secondary, default);
                case "Brown2": return (BodyZone.Fixed, C("#3A2A1E"));
                case "Beige": return (BodyZone.Fixed, C("#C9B48A"));
                case "Eyebrows": return (BodyZone.Fixed, C("#3A2618"));
                case "Eye": return (BodyZone.Fixed, C("#16100B"));
                default: return (BodyZone.Fixed, C("#808080"));
            }
        }

        static GameObject BuildPrefab(CosmeticCatalog catalog, AnimatorController controller)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            var root = new GameObject("FarmerBody");
            try
            {
                var visual = root.AddComponent<FarmerVisual>();
                root.AddComponent<FarmerLoadoutSync>();
                root.AddComponent<FarmerEmotes>();

                var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
                inst.name = "Model";
                inst.transform.SetParent(root.transform, false);
                var anim = inst.GetComponent<Animator>() ?? inst.AddComponent<Animator>();
                anim.runtimeAnimatorController = controller;
                anim.applyRootMotion = false;
                anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

                var all = inst.GetComponentsInChildren<Transform>(true);
                Transform Find(string n) => all.First(t => t.name == n);
                var head = Find("Head");
                var chest = Find("Chest");
                var smrs = inst.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                var headSmr = smrs.First(s => s.name.Contains("Head"));
                var bodySmr = smrs.First(s => s.name.Contains("Body"));

                // Measure the bind pose to place attach points.
                var headVerts = BakedWorldVerts(headSmr, out var headSub);
                float crown = headVerts.Max(v => v.y);
                var skinIdx = MaterialIndex(headSmr, "Skin");
                var eyeIdx = MaterialIndex(headSmr, "Eye");
                var eyeVerts = eyeIdx >= 0 ? headSub[eyeIdx] : headVerts;
                float eyeY = eyeVerts.Average(v => v.y);
                float faceZ = headVerts.Where(v => Mathf.Abs(v.y - eyeY) < 0.04f && Mathf.Abs(v.x) < 0.05f).DefaultIfEmpty(Vector3.forward * 0.1f).Max(v => v.z);
                float headX = (headVerts.Min(v => v.x) + headVerts.Max(v => v.x)) * 0.5f;
                float headZ = (headVerts.Min(v => v.z) + headVerts.Max(v => v.z)) * 0.5f;
                var bodyVerts = BakedWorldVerts(bodySmr, out _);
                float chestY = chest.position.y;
                float backZ = bodyVerts.Where(v => Mathf.Abs(v.y - chestY) < 0.08f && Mathf.Abs(v.x) < 0.12f).DefaultIfEmpty(new Vector3(0, 0, -0.12f)).Min(v => v.z);
                Debug.Log($"[FarmerAssetBuilder] crown={crown:F3} eyeY={eyeY:F3} faceZ={faceZ:F3} chestY={chestY:F3} backZ={backZ:F3} headCentre=({headX:F3},{headZ:F3})");

                Transform Attach(string n, Transform parent, Vector3 worldPos)
                {
                    var t = new GameObject(n).transform;
                    t.SetParent(parent, true);
                    t.SetPositionAndRotation(worldPos, Quaternion.identity);
                    t.localScale = Vector3.one;
                    return t;
                }
                var hatAttach = Attach("HatAttach", head, new Vector3(headX, crown - 0.015f, headZ));
                var faceAttach = Attach("FaceAttach", head, new Vector3(headX, eyeY, faceZ));
                var backAttach = Attach("BackAttach", chest, new Vector3(0f, chestY, backZ));

                var zones = new List<ZoneBinding>();
                foreach (var smr in smrs)
                {
                    var mats = smr.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        var (zone, col) = ZoneFor(smr.name, mats[i] != null ? mats[i].name : "");
                        zones.Add(new ZoneBinding { renderer = smr, materialIndex = i, zone = zone, fixedColor = col });
                    }
                    smr.updateWhenOffscreen = false;
                }

                var tag = BuildNameTag(root.transform, crown + 0.55f, catalog);
                visual.EditorSetup(catalog, anim, inst.transform, head, hatAttach, faceAttach, backAttach, bodySmr, zones, tag);

                var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                return prefab;
            }
            finally { Object.DestroyImmediate(root); }
        }

        static int MaterialIndex(Renderer r, string name)
        {
            var m = r.sharedMaterials;
            for (int i = 0; i < m.Length; i++) if (m[i] != null && m[i].name.Split(' ')[0] == name) return i;
            return -1;
        }

        static List<Vector3> BakedWorldVerts(SkinnedMeshRenderer smr, out List<List<Vector3>> perSubmesh)
        {
            var mesh = new Mesh();
            smr.BakeMesh(mesh, true); // vertices in renderer space with scale already applied
            var world = mesh.vertices.Select(v => smr.transform.position + smr.transform.rotation * v).ToList();
            perSubmesh = new List<List<Vector3>>();
            for (int s = 0; s < mesh.subMeshCount; s++)
                perSubmesh.Add(mesh.GetIndices(s).Distinct().Select(i => world[i]).ToList());
            Object.DestroyImmediate(mesh);
            return world;
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
                go.transform.localRotation = Quaternion.Euler(0, 180, 0);
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
                f.transform.localRotation = Quaternion.Euler(0f, 180f + (i - 1.5f) * -8f, 0f);
                farmers.Add(f.GetComponent<FarmerVisual>());
            }
            lineup.EditorSetup(farmers);

            var marker = new GameObject("WardrobeMarker");
            marker.transform.position = new Vector3(4.6f, 0f, -1.8f);
            marker.transform.rotation = Quaternion.Euler(0f, 200f, 0f);
            marker.AddComponent<LevelMarker>().Configure(MarkerRole.Wardrobe);
            marker.tag = MarkerTag.Wardrobe;

            EditorSceneManager.SaveScene(scene, ScenePath);
        }
    }
}
