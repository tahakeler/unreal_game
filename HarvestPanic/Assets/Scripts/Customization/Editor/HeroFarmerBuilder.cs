using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarvestPanic.Core;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

namespace HarvestPanic.Customization.Editor
{
    /// <summary>
    /// The player farmer body built from the owner's rigged Tripo hero (Assets/ThirdParty/Tripo/SK_Farmer_Hero, produced by
    /// tools/tripo/farmer_hero.py): Humanoid avatar with an explicit bone map, CC0 Quaternius clips retargeted through
    /// Mecanim (idle, walk, run, wave, point, faint, interact), and in-house humanoid clips recorded with
    /// <see cref="HumanPoseHandler"/> from poses expressed on <see cref="HumanBodyBones"/> (so they retarget to future
    /// characters): jump, fall, dance, shrug, salute, chicken dance, carry one-/two-handed, tool, throw wind-up, toss,
    /// down (limp) and get-up. Controller: base layer (locomotion, air, emotes, faint, down/get-up) + upper-body layer
    /// (carry/throw, weight driven by FarmerVisual from the Player's replicated state).
    /// </summary>
    public static class HeroFarmerBuilder
    {
        public const string HeroDir = "Assets/ThirdParty/Tripo/SK_Farmer_Hero";
        public const string HeroFbx = HeroDir + "/SK_Farmer_Hero.fbx";
        public const string BaseColorPath = HeroDir + "/SK_Farmer_Hero_BaseColor.png";
        public const string MaskPath = HeroDir + "/SK_Farmer_Hero_Mask.png";
        public const string RegionsPath = HeroDir + "/SK_Farmer_Hero_Regions.png";
        public const string StatsPath = HeroDir + "/SK_Farmer_Hero_stats.json";
        public const string ShaderName = "HarvestPanic/Characters/FarmerRecolour";
        public const string MaterialPath = FarmerAssetBuilder.MatDir + "/M_FarmerHero.mat";
        public const string ControllerPath = FarmerAssetBuilder.AnimDir + "/FarmerHero.controller";
        public const string MaskAssetPath = FarmerAssetBuilder.AnimDir + "/FarmerUpperBody.mask";
        public const string ClipDir = FarmerAssetBuilder.AnimDir + "/Humanoid";

        /// <summary>Hero (UE-mannequin naming) -> humanoid bones.</summary>
        public static readonly (HumanBodyBones bone, string name)[] HeroMap =
        {
            (HumanBodyBones.Hips, "pelvis"), (HumanBodyBones.Spine, "spine_01"), (HumanBodyBones.Chest, "spine_02"), (HumanBodyBones.UpperChest, "spine_03"),
            (HumanBodyBones.Neck, "neck_01"), (HumanBodyBones.Head, "head"),
            (HumanBodyBones.LeftShoulder, "clavicle_l"), (HumanBodyBones.LeftUpperArm, "upperarm_l"), (HumanBodyBones.LeftLowerArm, "lowerarm_l"), (HumanBodyBones.LeftHand, "hand_l"),
            (HumanBodyBones.RightShoulder, "clavicle_r"), (HumanBodyBones.RightUpperArm, "upperarm_r"), (HumanBodyBones.RightLowerArm, "lowerarm_r"), (HumanBodyBones.RightHand, "hand_r"),
            (HumanBodyBones.LeftUpperLeg, "thigh_l"), (HumanBodyBones.LeftLowerLeg, "calf_l"), (HumanBodyBones.LeftFoot, "foot_l"), (HumanBodyBones.LeftToes, "ball_l"),
            (HumanBodyBones.RightUpperLeg, "thigh_r"), (HumanBodyBones.RightLowerLeg, "calf_r"), (HumanBodyBones.RightFoot, "foot_r"), (HumanBodyBones.RightToes, "ball_r"),
            (HumanBodyBones.LeftThumbProximal, "thumb_01_l"), (HumanBodyBones.LeftThumbIntermediate, "thumb_02_l"), (HumanBodyBones.LeftThumbDistal, "thumb_03_l"),
            (HumanBodyBones.LeftIndexProximal, "index_01_l"), (HumanBodyBones.LeftIndexIntermediate, "index_02_l"), (HumanBodyBones.LeftIndexDistal, "index_03_l"),
            (HumanBodyBones.LeftMiddleProximal, "middle_01_l"), (HumanBodyBones.LeftMiddleIntermediate, "middle_02_l"), (HumanBodyBones.LeftMiddleDistal, "middle_03_l"),
            (HumanBodyBones.LeftRingProximal, "ring_01_l"), (HumanBodyBones.LeftRingIntermediate, "ring_02_l"), (HumanBodyBones.LeftRingDistal, "ring_03_l"),
            (HumanBodyBones.LeftLittleProximal, "pinky_01_l"), (HumanBodyBones.LeftLittleIntermediate, "pinky_02_l"), (HumanBodyBones.LeftLittleDistal, "pinky_03_l"),
            (HumanBodyBones.RightThumbProximal, "thumb_01_r"), (HumanBodyBones.RightThumbIntermediate, "thumb_02_r"), (HumanBodyBones.RightThumbDistal, "thumb_03_r"),
            (HumanBodyBones.RightIndexProximal, "index_01_r"), (HumanBodyBones.RightIndexIntermediate, "index_02_r"), (HumanBodyBones.RightIndexDistal, "index_03_r"),
            (HumanBodyBones.RightMiddleProximal, "middle_01_r"), (HumanBodyBones.RightMiddleIntermediate, "middle_02_r"), (HumanBodyBones.RightMiddleDistal, "middle_03_r"),
            (HumanBodyBones.RightRingProximal, "ring_01_r"), (HumanBodyBones.RightRingIntermediate, "ring_02_r"), (HumanBodyBones.RightRingDistal, "ring_03_r"),
            (HumanBodyBones.RightLittleProximal, "pinky_01_r"), (HumanBodyBones.RightLittleIntermediate, "pinky_02_r"), (HumanBodyBones.RightLittleDistal, "pinky_03_r"),
        };

        /// <summary>Quaternius CC0 farmer (animation source) -> humanoid bones (fingers left unmapped: relaxed hands).</summary>
        public static readonly (HumanBodyBones bone, string name)[] QuaterniusMap =
        {
            // The legs hang off "Body" (Hips is a spine bone in this rig), so Body is the humanoid Hips.
            (HumanBodyBones.Hips, "Body"), (HumanBodyBones.Spine, "Abdomen"), (HumanBodyBones.Chest, "Torso"), (HumanBodyBones.UpperChest, "Chest"),
            (HumanBodyBones.Neck, "Neck"), (HumanBodyBones.Head, "Head"),
            (HumanBodyBones.LeftShoulder, "Shoulder.L"), (HumanBodyBones.LeftUpperArm, "UpperArm.L"), (HumanBodyBones.LeftLowerArm, "LowerArm.L"), (HumanBodyBones.LeftHand, "Wrist.L"),
            (HumanBodyBones.RightShoulder, "Shoulder.R"), (HumanBodyBones.RightUpperArm, "UpperArm.R"), (HumanBodyBones.RightLowerArm, "LowerArm.R"), (HumanBodyBones.RightHand, "Wrist.R"),
            // Foot.L/R are IK targets parented to Root (not to the shins), so the shin end bones stand in for the feet
            // (ankle rotation is not retargeted; the hero's feet stay neutral in these clips).
            (HumanBodyBones.LeftUpperLeg, "UpperLeg.L"), (HumanBodyBones.LeftLowerLeg, "LowerLeg.L"), (HumanBodyBones.LeftFoot, "LowerLeg.L_end"),
            (HumanBodyBones.RightUpperLeg, "UpperLeg.R"), (HumanBodyBones.RightLowerLeg, "LowerLeg.R"), (HumanBodyBones.RightFoot, "LowerLeg.R_end"),
        };

        /// <summary>Retargeted Quaternius takes: clip name, FBX take, loop.</summary>
        public static readonly (string name, string take, bool loop)[] SourceClips =
        {
            ("Idle", "Idle", true), ("Walk", "Walk", true), ("Run", "Run", true), ("Wave", "Wave", false), ("Faint", "Death", false),
            ("Point", "Idle_Gun_Pointing", true), ("Interact", "Interact", false), ("HitReact", "HitRecieve", false),
        };

        // ---- Import -------------------------------------------------------------------------------------------------

        /// <summary>Imports <paramref name="path"/> as Humanoid with an explicit bone map; returns the avatar (null on failure).</summary>
        public static Avatar ConfigureHumanoid(string path, (HumanBodyBones bone, string name)[] map, (string name, string take, bool loop)[] clips)
        {
            var imp = (ModelImporter)AssetImporter.GetAtPath(path);
            if (imp == null) throw new FileNotFoundException(path);
            // Pass 1: final geometry settings, so the skeleton read below matches what the avatar will be built from.
            imp.importBlendShapes = false; imp.importCameras = false; imp.importLights = false;
            imp.materialImportMode = ModelImporterMaterialImportMode.None;
            imp.bakeAxisConversion = true;
            imp.animationType = ModelImporterAnimationType.Generic;
            imp.SaveAndReimport();
            // Pass 2: humanoid with an explicit bone map and a T-pose reference skeleton. Mecanim treats the avatar's
            // skeleton pose as the T-pose its muscle space is measured from; the Tripo export is an A-pose (arms ~50 deg
            // down), which retargeted every clip 25-60 deg off on every limb (arms into the belly, bent backwards).
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var skeleton = TPoseSkeleton(model, map);
            var desc = new HumanDescription
            {
                human = map.Select(m => new HumanBone { humanName = HumanTrait.BoneName[(int)m.bone], boneName = m.name, limit = new HumanLimit { useDefaultValues = true } }).ToArray(),
                skeleton = skeleton,
                upperArmTwist = 0.5f, lowerArmTwist = 0.5f, upperLegTwist = 0.5f, lowerLegTwist = 0.5f,
                armStretch = 0.05f, legStretch = 0.05f, feetSpacing = 0f, hasTranslationDoF = false,
            };
            imp.animationType = ModelImporterAnimationType.Human;
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.humanDescription = desc;
            imp.importAnimation = clips != null && clips.Length > 0;
            if (imp.importAnimation)
            {
                var takes = imp.defaultClipAnimations;
                var list = new List<ModelImporterClipAnimation>();
                foreach (var (name, take, loop) in clips)
                {
                    var src = takes.FirstOrDefault(t => t.takeName == take || t.takeName.EndsWith("|" + take));
                    if (src == null) { Debug.LogWarning("[HeroFarmerBuilder] missing take " + take); continue; }
                    src.name = name;
                    src.loopTime = loop; src.loopPose = loop;
                    src.lockRootRotation = src.lockRootHeightY = src.lockRootPositionXZ = true;
                    src.keepOriginalOrientation = src.keepOriginalPositionY = src.keepOriginalPositionXZ = true;
                    src.heightFromFeet = false;
                    list.Add(src);
                }
                imp.clipAnimations = list.ToArray();
            }
            imp.SaveAndReimport();
            var avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
            Debug.Log($"[HeroFarmerBuilder] {path}: avatar {(avatar != null ? avatar.name : "none")} valid={avatar != null && avatar.isValid} human={avatar != null && avatar.isHuman}");
            return avatar;
        }

        /// <summary>Reference T-pose: (bone, child, direction in the character frame: x right, y up, z forward).</summary>
        static readonly (HumanBodyBones bone, HumanBodyBones child, Vector3 dir)[] TPoseChain =
        {
            (HumanBodyBones.Hips, HumanBodyBones.Spine, Vector3.up), (HumanBodyBones.Spine, HumanBodyBones.Chest, Vector3.up),
            (HumanBodyBones.Chest, HumanBodyBones.UpperChest, Vector3.up), (HumanBodyBones.UpperChest, HumanBodyBones.Neck, Vector3.up),
            (HumanBodyBones.Neck, HumanBodyBones.Head, Vector3.up),
            (HumanBodyBones.LeftShoulder, HumanBodyBones.LeftUpperArm, Vector3.left), (HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, Vector3.left),
            (HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, Vector3.left),
            (HumanBodyBones.RightShoulder, HumanBodyBones.RightUpperArm, Vector3.right), (HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, Vector3.right),
            (HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, Vector3.right),
            (HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, Vector3.down), (HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, Vector3.down),
            (HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, Vector3.down), (HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, Vector3.down),
        };

        /// <summary>
        /// "Enforce T-Pose": the model's transforms with every mapped chain segment swung (parent first, minimal rotation)
        /// onto <see cref="TPoseChain"/>, as <see cref="SkeletonBone"/>s for the avatar. The mesh bind pose is untouched.
        /// </summary>
        public static SkeletonBone[] TPoseSkeleton(GameObject model, (HumanBodyBones bone, string name)[] map)
        {
            var go = (GameObject)Object.Instantiate(model);
            try
            {
                go.name = model.name;
                go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var all = go.GetComponentsInChildren<Transform>(true);
                Transform T(HumanBodyBones b)
                {
                    foreach (var m in map) if (m.bone == b) return all.FirstOrDefault(t => t.name == m.name);
                    return null;
                }
                Transform l = T(HumanBodyBones.LeftUpperArm), r = T(HumanBodyBones.RightUpperArm);
                Vector3 right = Vector3.ProjectOnPlane(r.position - l.position, Vector3.up).normalized;
                var frame = Quaternion.LookRotation(Vector3.Cross(right, Vector3.up), Vector3.up);
                foreach (var (bone, child, dir) in TPoseChain)
                {
                    Transform a = T(bone), c = T(child);
                    if (a == null || c == null) continue;
                    Vector3 now = c.position - a.position;
                    if (now.sqrMagnitude < 1e-8f) continue;
                    a.rotation = Quaternion.FromToRotation(now, frame * dir) * a.rotation;
                }
                return all.Select(t => t == go.transform
                    ? new SkeletonBone { name = model.name, position = model.transform.localPosition, rotation = model.transform.localRotation, scale = model.transform.localScale }
                    : new SkeletonBone { name = t.name, position = t.localPosition, rotation = t.localRotation, scale = t.localScale }).ToArray();
            }
            finally { Object.DestroyImmediate(go); }
        }

        static void ConfigureTexture(string path, bool srgb, int max)
        {
            if (!(AssetImporter.GetAtPath(path) is TextureImporter ti)) throw new FileNotFoundException(path);
            bool d = false;
            if (ti.sRGBTexture != srgb) { ti.sRGBTexture = srgb; d = true; }
            if (ti.maxTextureSize != max) { ti.maxTextureSize = max; d = true; }
            if (ti.textureCompression != TextureImporterCompression.CompressedHQ) { ti.textureCompression = TextureImporterCompression.CompressedHQ; d = true; }
            if (!ti.mipmapEnabled) { ti.mipmapEnabled = true; d = true; }
            if (!srgb && ti.alphaSource != TextureImporterAlphaSource.FromInput) { ti.alphaSource = TextureImporterAlphaSource.FromInput; d = true; }
            if (ti.alphaIsTransparency) { ti.alphaIsTransparency = false; d = true; }
            if (d) ti.SaveAndReimport();
        }

        [Serializable] class ZoneStat { public float coverage_pct; public float mean_linear_lum; }
        [Serializable] class Zones { public ZoneStat overalls_R, shirt_G, bootsgloves_B, skin_A, hat, band, hair; }
        [Serializable] class Stats { public Zones zones; public int output_tris; public float height_m; }

        /// <summary>Recolour stats written by tools/tripo/farmer_hero.py (zone coverage + reference luminance).</summary>
        public static (Vector4 maskRef, Vector4 regionRef, float[] coverage) ReadStats()
        {
            var s = JsonUtility.FromJson<Stats>(File.ReadAllText(StatsPath));
            var z = s.zones;
            return (new Vector4(z.overalls_R.mean_linear_lum, z.shirt_G.mean_linear_lum, z.bootsgloves_B.mean_linear_lum, z.skin_A.mean_linear_lum),
                new Vector4(z.hat.mean_linear_lum, z.band.mean_linear_lum, z.hair.mean_linear_lum, 0f),
                new[] { z.overalls_R.coverage_pct, z.shirt_G.coverage_pct, z.bootsgloves_B.coverage_pct, z.skin_A.coverage_pct });
        }

        public static Material BuildMaterial()
        {
            ConfigureTexture(BaseColorPath, true, 2048);
            ConfigureTexture(MaskPath, false, 2048);
            ConfigureTexture(RegionsPath, false, 1024);
            var shader = Shader.Find(ShaderName);
            if (shader == null) throw new InvalidOperationException("Missing shader " + ShaderName);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, MaterialPath); }
            mat.shader = shader;
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(BaseColorPath));
            mat.SetTexture("_MaskMap", AssetDatabase.LoadAssetAtPath<Texture2D>(MaskPath));
            mat.SetTexture("_RegionMap", AssetDatabase.LoadAssetAtPath<Texture2D>(RegionsPath));
            var (maskRef, regionRef, _) = ReadStats();
            mat.SetVector("_MaskRef", maskRef);
            mat.SetVector("_RegionRef", regionRef);
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ---- Procedural humanoid clips ------------------------------------------------------------------------------

        /// <summary>Per-bone world-space (character frame: +Z forward, +X the farmer's right) rotation and position offsets for one sample.</summary>
        public delegate void HumanPoseFn(float t, Dictionary<HumanBodyBones, Quaternion> rot, Dictionary<HumanBodyBones, Vector3> pos);

        static readonly HumanBodyBones[] ParentFirst =
        {
            HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest, HumanBodyBones.Neck, HumanBodyBones.Head,
            HumanBodyBones.LeftShoulder, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
            HumanBodyBones.RightShoulder, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,
            HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot,
        };

        static Quaternion X(float deg) => Quaternion.AngleAxis(deg, Vector3.right);
        static Quaternion Y(float deg) => Quaternion.AngleAxis(deg, Vector3.up);
        static Quaternion Z(float deg) => Quaternion.AngleAxis(deg, Vector3.forward);
        static float Env(float t, float rise, float hold, float end) =>
            t < rise ? Mathf.SmoothStep(0f, 1f, t / rise) : t < hold ? 1f : t < end ? Mathf.SmoothStep(1f, 0f, (t - hold) / (end - hold)) : 0f;

        // Left arm hangs at -X: Z(-a) swings it out sideways; X(-a) brings any hanging limb forward, X(+a) back.
        static void Dance(float t, Dictionary<HumanBodyBones, Quaternion> r, Dictionary<HumanBodyBones, Vector3> p)
        {
            float ph = t * Mathf.PI * 2f, flap = 25f + 40f * (0.5f + 0.5f * Mathf.Sin(2f * ph));
            p[HumanBodyBones.Hips] = new Vector3(0f, -0.04f * (0.5f - 0.5f * Mathf.Cos(2f * ph)), 0f);
            r[HumanBodyBones.Hips] = Z(7f * Mathf.Sin(ph));
            r[HumanBodyBones.Chest] = Y(16f * Mathf.Sin(ph));
            r[HumanBodyBones.Head] = X(12f * Mathf.Sin(2f * ph)) * Z(-9f * Mathf.Sin(ph));
            r[HumanBodyBones.LeftUpperArm] = Z(-flap); r[HumanBodyBones.RightUpperArm] = Z(flap);
            r[HumanBodyBones.LeftLowerArm] = Z(80f); r[HumanBodyBones.RightLowerArm] = Z(-80f);
        }

        static void Jump(float t, Dictionary<HumanBodyBones, Quaternion> r, Dictionary<HumanBodyBones, Vector3> p)
        {
            float crouch = t < 0.12f ? t / 0.12f : t < 0.22f ? 1f - (t - 0.12f) / 0.1f : t > 0.55f ? Mathf.Sin((t - 0.55f) / 0.15f * Mathf.PI) * 0.6f : 0f;
            float air = t < 0.12f ? 0f : t < 0.25f ? (t - 0.12f) / 0.13f : t < 0.45f ? 1f : t < 0.6f ? 1f - (t - 0.45f) / 0.15f : 0f;
            p[HumanBodyBones.Hips] = new Vector3(0f, -0.1f * crouch, 0f);
            r[HumanBodyBones.Chest] = X(12f * crouch - 6f * air);
            r[HumanBodyBones.LeftUpperArm] = Z(-140f * air - 20f * crouch); r[HumanBodyBones.RightUpperArm] = Z(140f * air + 20f * crouch);
            r[HumanBodyBones.LeftUpperLeg] = X(-35f * air - 25f * crouch); r[HumanBodyBones.RightUpperLeg] = X(-20f * air - 25f * crouch);
            r[HumanBodyBones.LeftLowerLeg] = X(55f * air + 45f * crouch); r[HumanBodyBones.RightLowerLeg] = X(40f * air + 45f * crouch);
        }

        static void Fall(float t, Dictionary<HumanBodyBones, Quaternion> r, Dictionary<HumanBodyBones, Vector3> p)
        {
            float ph = t * Mathf.PI * 2f;
            r[HumanBodyBones.LeftUpperArm] = Z(-120f - 20f * Mathf.Sin(ph * 2f)); r[HumanBodyBones.RightUpperArm] = Z(120f + 20f * Mathf.Sin(ph * 2f + 1f));
            r[HumanBodyBones.LeftLowerArm] = Z(-25f * Mathf.Sin(ph * 2f)); r[HumanBodyBones.RightLowerArm] = Z(25f * Mathf.Sin(ph * 2f + 1f));
            r[HumanBodyBones.LeftUpperLeg] = X(-30f - 15f * Mathf.Sin(ph)); r[HumanBodyBones.RightUpperLeg] = X(-10f + 15f * Mathf.Sin(ph));
            r[HumanBodyBones.LeftLowerLeg] = X(50f); r[HumanBodyBones.RightLowerLeg] = X(35f);
            r[HumanBodyBones.Head] = X(-10f);
        }

        static void Shrug(float t, Dictionary<HumanBodyBones, Quaternion> r, Dictionary<HumanBodyBones, Vector3> p)
        {
            float e = Env(t, 0.25f, 1.0f, 1.35f);
            r[HumanBodyBones.LeftShoulder] = Z(-14f * e); r[HumanBodyBones.RightShoulder] = Z(14f * e);
            // Elbows out and forearms forward-and-out, palms up: clear of a big belly.
            r[HumanBodyBones.LeftUpperArm] = Z(-42f * e) * X(-15f * e); r[HumanBodyBones.RightUpperArm] = Z(42f * e) * X(-15f * e);
            r[HumanBodyBones.LeftLowerArm] = Z(-40f * e) * X(-70f * e); r[HumanBodyBones.RightLowerArm] = Z(40f * e) * X(-70f * e);
            r[HumanBodyBones.LeftHand] = Z(-30f * e); r[HumanBodyBones.RightHand] = Z(30f * e);
            r[HumanBodyBones.Head] = Z(12f * e) * X(-6f * e);
            r[HumanBodyBones.Chest] = X(-4f * e);
        }

        static void Salute(float t, Dictionary<HumanBodyBones, Quaternion> r, Dictionary<HumanBodyBones, Vector3> p)
        {
            float e = Env(t, 0.3f, 1.2f, 1.55f);
            r[HumanBodyBones.RightUpperArm] = Y(-22f * e) * Z(88f * e);
            r[HumanBodyBones.RightLowerArm] = Z(138f * e);
            r[HumanBodyBones.Head] = X(-8f * e);
            r[HumanBodyBones.Chest] = X(-3f * e);
        }

        static void Chicken(float t, Dictionary<HumanBodyBones, Quaternion> r, Dictionary<HumanBodyBones, Vector3> p)
        {
            float ph = t * Mathf.PI * 2f, flap = 0.5f + 0.5f * Mathf.Sin(2f * ph);
            p[HumanBodyBones.Hips] = new Vector3(0f, -0.04f * (0.5f - 0.5f * Mathf.Cos(2f * ph)), 0f);
            r[HumanBodyBones.Chest] = Y(8f * Mathf.Sin(ph)) * X(6f);
            r[HumanBodyBones.LeftUpperArm] = Z(-(18f + 38f * flap)); r[HumanBodyBones.RightUpperArm] = Z(18f + 38f * flap);
            r[HumanBodyBones.LeftLowerArm] = Z(140f); r[HumanBodyBones.RightLowerArm] = Z(-140f);
            r[HumanBodyBones.Head] = X(14f * Mathf.Sin(2f * ph));
            float l = Mathf.Max(0f, Mathf.Sin(ph)), rr = Mathf.Max(0f, -Mathf.Sin(ph));
            r[HumanBodyBones.LeftUpperLeg] = X(-28f * l); r[HumanBodyBones.LeftLowerLeg] = X(40f * l);
            r[HumanBodyBones.RightUpperLeg] = X(-28f * rr); r[HumanBodyBones.RightLowerLeg] = X(40f * rr);
        }

        static void HoldBoth(Dictionary<HumanBodyBones, Quaternion> r, float lift, float breathe)
        {
            // Both arms forward around a big load (pig, crate): upper arms forward + in, forearms up a little.
            r[HumanBodyBones.LeftUpperArm] = Y(14f) * X(-72f - lift - breathe);
            r[HumanBodyBones.RightUpperArm] = Y(-14f) * X(-72f - lift - breathe);
            r[HumanBodyBones.LeftLowerArm] = Y(22f) * X(-22f);
            r[HumanBodyBones.RightLowerArm] = Y(-22f) * X(-22f);
            r[HumanBodyBones.Chest] = X(-7f);
        }

        static void Carry2(float t, Dictionary<HumanBodyBones, Quaternion> r, Dictionary<HumanBodyBones, Vector3> p)
        {
            HoldBoth(r, 0f, 2f * Mathf.Sin(t * Mathf.PI * 2f));
        }

        static void Carry1(float t, Dictionary<HumanBodyBones, Quaternion> r, Dictionary<HumanBodyBones, Vector3> p)
        {
            float b = 2f * Mathf.Sin(t * Mathf.PI * 2f);
            r[HumanBodyBones.RightUpperArm] = Y(-10f) * X(-38f - b);
            r[HumanBodyBones.RightLowerArm] = Y(-30f) * X(-62f);
            r[HumanBodyBones.Chest] = Z(3f);
        }

        static void Tool(float t, Dictionary<HumanBodyBones, Quaternion> r, Dictionary<HumanBodyBones, Vector3> p)
        {
            float b = 2f * Mathf.Sin(t * Mathf.PI * 2f);
            r[HumanBodyBones.RightUpperArm] = Y(-8f) * X(-55f - b);
            r[HumanBodyBones.RightLowerArm] = X(-22f);
            r[HumanBodyBones.LeftUpperArm] = Y(20f) * X(-40f - b);
            r[HumanBodyBones.LeftLowerArm] = Y(25f) * X(-30f);
        }

        static void Windup(float t, Dictionary<HumanBodyBones, Quaternion> r, Dictionary<HumanBodyBones, Vector3> p)
        {
            // Load hoisted over the right shoulder, body coiled back.
            r[HumanBodyBones.LeftUpperArm] = Y(10f) * X(150f) * Z(-10f);
            r[HumanBodyBones.RightUpperArm] = Y(-10f) * X(150f) * Z(10f);
            r[HumanBodyBones.LeftLowerArm] = X(40f); r[HumanBodyBones.RightLowerArm] = X(40f);
            r[HumanBodyBones.Chest] = Y(-14f) * X(-12f);
            r[HumanBodyBones.Spine] = X(-6f);
            r[HumanBodyBones.LeftUpperLeg] = X(-12f); r[HumanBodyBones.LeftLowerLeg] = X(18f);
        }

        static void Toss(float t, Dictionary<HumanBodyBones, Quaternion> r, Dictionary<HumanBodyBones, Vector3> p)
        {
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.22f)), back = 1f - Mathf.Clamp01((t - 0.3f) / 0.15f);
            float arm = Mathf.Lerp(150f, -80f, k);
            r[HumanBodyBones.LeftUpperArm] = X(arm * back); r[HumanBodyBones.RightUpperArm] = X(arm * back);
            r[HumanBodyBones.Chest] = Y(Mathf.Lerp(-14f, 10f, k) * back) * X(Mathf.Lerp(-12f, 14f, k) * back);
        }

        static void LimpPose(Dictionary<HumanBodyBones, Quaternion> r, Dictionary<HumanBodyBones, Vector3> p, float w, float breathe)
        {
            r[HumanBodyBones.LeftUpperArm] = Z(-70f * w) * X(-10f * w); r[HumanBodyBones.RightUpperArm] = Z(75f * w) * X(-5f * w);
            r[HumanBodyBones.LeftLowerArm] = Z(25f * w); r[HumanBodyBones.RightLowerArm] = Z(-35f * w);
            r[HumanBodyBones.Head] = Z(22f * w) * X((18f + breathe) * w);
            r[HumanBodyBones.Chest] = X((14f + breathe) * w);
            r[HumanBodyBones.LeftUpperLeg] = X(-35f * w) * Z(-8f * w); r[HumanBodyBones.RightUpperLeg] = X(-15f * w) * Z(6f * w);
            r[HumanBodyBones.LeftLowerLeg] = X(55f * w); r[HumanBodyBones.RightLowerLeg] = X(30f * w);
        }

        static void Down(float t, Dictionary<HumanBodyBones, Quaternion> r, Dictionary<HumanBodyBones, Vector3> p) =>
            LimpPose(r, p, 1f, 3f * Mathf.Sin(t * Mathf.PI * 2f));

        static void GetUp(float t, Dictionary<HumanBodyBones, Quaternion> r, Dictionary<HumanBodyBones, Vector3> p)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / 1.1f);
            LimpPose(r, p, 1f - k, 0f);
            float push = Mathf.Sin(Mathf.Clamp01(t / 1.1f) * Mathf.PI); // hands on knees half way, then a stretch
            r[HumanBodyBones.Chest] = X(30f * push) * (r.TryGetValue(HumanBodyBones.Chest, out var c) ? c : Quaternion.identity);
            r[HumanBodyBones.LeftUpperArm] = X(-35f * push) * (r.TryGetValue(HumanBodyBones.LeftUpperArm, out var l) ? l : Quaternion.identity);
            r[HumanBodyBones.RightUpperArm] = X(-35f * push) * (r.TryGetValue(HumanBodyBones.RightUpperArm, out var rr) ? rr : Quaternion.identity);
            p[HumanBodyBones.Hips] = new Vector3(0f, -0.12f * push, 0f);
            r[HumanBodyBones.LeftUpperLeg] = X(-40f * push); r[HumanBodyBones.RightUpperLeg] = X(-40f * push);
            r[HumanBodyBones.LeftLowerLeg] = X(60f * push); r[HumanBodyBones.RightLowerLeg] = X(60f * push);
        }

        /// <summary>Clip name -> (length s, loop, pose).</summary>
        public static readonly (string name, float length, bool loop, HumanPoseFn pose)[] ProceduralClips =
        {
            ("Jump", 0.7f, false, Jump), ("Fall", 0.8f, true, Fall), ("Dance", 1.0f, true, Dance), ("Shrug", 1.4f, false, Shrug),
            ("Salute", 1.6f, false, Salute), ("ChickenDance", 1.0f, true, Chicken), ("Carry2", 1.6f, true, Carry2), ("Carry1", 1.6f, true, Carry1),
            ("Tool", 1.6f, true, Tool), ("Windup", 0.5f, true, Windup), ("Toss", 0.45f, false, Toss), ("Down", 1.6f, true, Down), ("GetUp", 1.2f, false, GetUp),
        };

        /// <summary>
        /// Records each procedural pose as a humanoid (muscle) clip on <paramref name="hero"/>: base pose = <paramref name="idle"/>
        /// frame 0, deltas applied parent-first in character space, read back through <see cref="HumanPoseHandler"/>.
        /// </summary>
        public static Dictionary<string, AnimationClip> BuildProceduralClips(GameObject heroModel, Avatar avatar, AnimationClip idle)
        {
            FarmerAssetBuilder.EnsureFolderPublic(ClipDir);
            var result = new Dictionary<string, AnimationClip>();
            var go = (GameObject)Object.Instantiate(heroModel);
            try
            {
                // Record unrotated: HumanPoseHandler.GetHumanPose reports the body rotation in world space but SetHumanPose
                // applies it on top of the root, so a rotated root turns the body around between the two calls. The pose
                // functions are authored in a character frame (+Z forward, +X right) that is mapped onto the model below.
                go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var anim = go.GetComponent<Animator>() ?? go.AddComponent<Animator>();
                anim.avatar = avatar;
                anim.applyRootMotion = false;
                // Nothing renders this instance in batchmode, so a culling animator would skip the sample and leave the
                // bind (A) pose as the base; the arm deltas below assume idle arms hanging down.
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var graph = PlayableGraph.Create("HeroIdleSample");
                var output = AnimationPlayableOutput.Create(graph, "out", anim);
                var playable = AnimationClipPlayable.Create(graph, idle);
                output.SetSourcePlayable(playable);
                playable.SetTime(0.0); graph.Evaluate(0f);
                var handler = new HumanPoseHandler(avatar, go.transform);
                var basePose = new HumanPose();
                handler.GetHumanPose(ref basePose);
                graph.Destroy();

                var bones = ParentFirst.Select(b => (b, t: anim.GetBoneTransform(b))).Where(x => x.t != null).ToList();
                var bp0 = basePose;
                handler.SetHumanPose(ref bp0);
                var frame = CharacterFrame(anim);
                var frameInv = Quaternion.Inverse(frame);
                var rot = new Dictionary<HumanBodyBones, Quaternion>();
                var pos = new Dictionary<HumanBodyBones, Vector3>();
                var muscleBindings = Enumerable.Range(0, HumanTrait.MuscleCount).Select(i => MuscleProperty(HumanTrait.MuscleName[i])).ToArray();
                foreach (var (name, length, loop, fn) in ProceduralClips)
                {
                    const float fps = 30f;
                    int frames = Mathf.Max(2, Mathf.RoundToInt(length * fps));
                    var curves = new AnimationCurve[HumanTrait.MuscleCount];
                    for (int i = 0; i < curves.Length; i++) curves[i] = new AnimationCurve();
                    var rootT = new[] { new AnimationCurve(), new AnimationCurve(), new AnimationCurve() };
                    var rootQ = new[] { new AnimationCurve(), new AnimationCurve(), new AnimationCurve(), new AnimationCurve() };
                    var pose = new HumanPose();
                    for (int f = 0; f <= frames; f++)
                    {
                        float t = f / fps;
                        var bp = basePose;
                        handler.SetHumanPose(ref bp);
                        rot.Clear(); pos.Clear();
                        fn(loop ? t / length : t, rot, pos); // loops take a 0..1 phase, one-shots take seconds
                        foreach (var (b, tr) in bones)
                        {
                            if (rot.TryGetValue(b, out var q)) tr.rotation = frame * q * frameInv * tr.rotation;
                            if (pos.TryGetValue(b, out var d)) tr.position += frame * d;
                        }
                        handler.GetHumanPose(ref pose);
                        for (int i = 0; i < curves.Length; i++) curves[i].AddKey(t, pose.muscles[i]);
                        rootT[0].AddKey(t, pose.bodyPosition.x); rootT[1].AddKey(t, pose.bodyPosition.y); rootT[2].AddKey(t, pose.bodyPosition.z);
                        rootQ[0].AddKey(t, pose.bodyRotation.x); rootQ[1].AddKey(t, pose.bodyRotation.y); rootQ[2].AddKey(t, pose.bodyRotation.z); rootQ[3].AddKey(t, pose.bodyRotation.w);
                    }
                    string path = ClipDir + "/Farmer_" + name + ".anim";
                    var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                    if (clip == null) { clip = new AnimationClip { name = "Farmer_" + name }; AssetDatabase.CreateAsset(clip, path); }
                    clip.ClearCurves();
                    clip.frameRate = fps;
                    for (int i = 0; i < curves.Length; i++)
                        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), muscleBindings[i]), curves[i]);
                    string[] tn = { "RootT.x", "RootT.y", "RootT.z" }, qn = { "RootQ.x", "RootQ.y", "RootQ.z", "RootQ.w" };
                    for (int i = 0; i < 3; i++) AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), tn[i]), rootT[i]);
                    for (int i = 0; i < 4; i++) AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), qn[i]), rootQ[i]);
                    var settings = AnimationUtility.GetAnimationClipSettings(clip);
                    settings.loopTime = loop;
                    settings.loopBlend = loop;
                    settings.loopBlendOrientation = settings.loopBlendPositionY = settings.loopBlendPositionXZ = true;
                    settings.keepOriginalOrientation = settings.keepOriginalPositionY = settings.keepOriginalPositionXZ = true;
                    AnimationUtility.SetAnimationClipSettings(clip, settings);
                    EditorUtility.SetDirty(clip);
                    result[name] = clip;
                }
                handler.Dispose();
            }
            finally { Object.DestroyImmediate(go); }
            return result;
        }

        /// <summary>
        /// World rotation of the character frame (+Z forward, +X right, +Y up) of a posed humanoid, from its shoulders:
        /// the Tripo export faces -Z, the Quaternius farmer +Z, and the pose functions must not care.
        /// </summary>
        public static Quaternion CharacterFrame(Animator anim)
        {
            var l = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            var r = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
            Vector3 right = Vector3.ProjectOnPlane(r.position - l.position, Vector3.up);
            if (right.sqrMagnitude < 1e-6f) return anim.transform.rotation;
            return Quaternion.LookRotation(Vector3.Cross(right.normalized, Vector3.up), Vector3.up);
        }

        /// <summary>Animator curve property for a muscle (finger muscles use "LeftHand.Index.1 Stretched" style names).</summary>
        public static string MuscleProperty(string muscle)
        {
            foreach (var side in new[] { "Left", "Right" })
                foreach (var finger in new[] { "Thumb", "Index", "Middle", "Ring", "Little" })
                {
                    string prefix = side + " " + finger + " ";
                    if (muscle.StartsWith(prefix)) return side + "Hand." + finger + "." + muscle.Substring(prefix.Length);
                }
            return muscle;
        }

        // ---- Controller ---------------------------------------------------------------------------------------------

        public static AnimatorController BuildController(Dictionary<string, AnimationClip> clips)
        {
            AssetDatabase.DeleteAsset(ControllerPath);
            var ac = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            ac.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ac.AddParameter(new AnimatorControllerParameter { name = "Grounded", type = AnimatorControllerParameterType.Bool, defaultBool = true });
            ac.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
            ac.AddParameter("Fainted", AnimatorControllerParameterType.Bool);
            ac.AddParameter("CarryHands", AnimatorControllerParameterType.Int);
            ac.AddParameter("Tool", AnimatorControllerParameterType.Bool);
            ac.AddParameter("ThrowCharge", AnimatorControllerParameterType.Float);
            ac.AddParameter("Toss", AnimatorControllerParameterType.Trigger);
            ac.AddParameter("Down", AnimatorControllerParameterType.Bool);
            ac.AddParameter("GetUp", AnimatorControllerParameterType.Trigger);

            var sm = ac.layers[0].stateMachine;
            var loco = ac.CreateBlendTreeInController("Locomotion", out var tree, 0);
            tree.blendType = BlendTreeType.Simple1D; tree.blendParameter = "Speed"; tree.useAutomaticThresholds = false;
            tree.AddChild(clips["Idle"], 0f); tree.AddChild(clips["Walk"], 1.6f); tree.AddChild(clips["Run"], 4.5f);
            sm.defaultState = loco;
            AnimatorState S(string n, Vector3 p) { var s = sm.AddState(n, p); s.motion = clips[n]; return s; }
            var jump = S("Jump", new Vector3(300, -160)); var fall = S("Fall", new Vector3(560, -160));
            var wave = S("Wave", new Vector3(300, 0)); var point = S("Point", new Vector3(300, 60)); var dance = S("Dance", new Vector3(300, 120));
            var faint = S("Faint", new Vector3(300, 180)); var shrug = S("Shrug", new Vector3(560, 0)); var salute = S("Salute", new Vector3(560, 60));
            var chicken = S("ChickenDance", new Vector3(560, 120)); var down = S("Down", new Vector3(300, 260)); var getUp = S("GetUp", new Vector3(560, 260));
            var interact = S("Interact", new Vector3(820, 0)); var hit = S("HitReact", new Vector3(820, 60));

            var anyJump = sm.AddAnyStateTransition(jump);
            anyJump.AddCondition(AnimatorConditionMode.If, 0, "Jump"); anyJump.AddCondition(AnimatorConditionMode.IfNot, 0, "Fainted"); anyJump.AddCondition(AnimatorConditionMode.IfNot, 0, "Down");
            anyJump.canTransitionToSelf = false; anyJump.duration = 0.08f;
            var jl = jump.AddTransition(loco); jl.hasExitTime = true; jl.exitTime = 0.9f; jl.duration = 0.15f; jl.AddCondition(AnimatorConditionMode.If, 0, "Grounded");
            var jf = jump.AddTransition(fall); jf.hasExitTime = true; jf.exitTime = 0.9f; jf.duration = 0.2f; jf.AddCondition(AnimatorConditionMode.IfNot, 0, "Grounded");
            var lf = loco.AddTransition(fall); lf.hasExitTime = false; lf.duration = 0.25f; lf.AddCondition(AnimatorConditionMode.IfNot, 0, "Grounded");
            var fl = fall.AddTransition(loco); fl.hasExitTime = false; fl.duration = 0.12f; fl.AddCondition(AnimatorConditionMode.If, 0, "Grounded");
            Exit(wave, loco, 0.95f); Exit(point, loco, 0.72f); Exit(shrug, loco, 0.95f); Exit(salute, loco, 0.95f); Exit(interact, loco, 0.9f); Exit(hit, loco, 0.9f);
            foreach (var loopy in new[] { dance, chicken })
            {
                var o = loopy.AddTransition(loco); o.hasExitTime = false; o.duration = 0.2f; o.AddCondition(AnimatorConditionMode.Greater, 0.6f, "Speed");
            }
            var anyFaint = sm.AddAnyStateTransition(faint);
            anyFaint.AddCondition(AnimatorConditionMode.If, 0, "Fainted"); anyFaint.canTransitionToSelf = false; anyFaint.duration = 0.1f;
            var faintOut = faint.AddTransition(loco); faintOut.hasExitTime = false; faintOut.duration = 0.35f; faintOut.AddCondition(AnimatorConditionMode.IfNot, 0, "Fainted");
            var anyDown = sm.AddAnyStateTransition(down);
            anyDown.AddCondition(AnimatorConditionMode.If, 0, "Down"); anyDown.canTransitionToSelf = false; anyDown.duration = 0.12f;
            var up = down.AddTransition(getUp); up.hasExitTime = false; up.duration = 0.1f; up.AddCondition(AnimatorConditionMode.IfNot, 0, "Down");
            Exit(getUp, loco, 0.92f);

            // Upper body: carry / throw (layer weight driven by FarmerVisual).
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(MaskAssetPath);
            if (mask == null) { mask = new AvatarMask(); AssetDatabase.CreateAsset(mask, MaskAssetPath); }
            foreach (AvatarMaskBodyPart part in Enum.GetValues(typeof(AvatarMaskBodyPart)))
            {
                if (part == AvatarMaskBodyPart.LastBodyPart) continue;
                bool on = part == AvatarMaskBodyPart.Body || part == AvatarMaskBodyPart.LeftArm || part == AvatarMaskBodyPart.RightArm
                          || part == AvatarMaskBodyPart.LeftFingers || part == AvatarMaskBodyPart.RightFingers;
                mask.SetHumanoidBodyPartActive(part, on);
            }
            EditorUtility.SetDirty(mask);
            ac.AddLayer("UpperBody");
            var layers = ac.layers;
            layers[1].avatarMask = mask; layers[1].defaultWeight = 0f; layers[1].blendingMode = AnimatorLayerBlendingMode.Override;
            ac.layers = layers;
            var usm = ac.layers[1].stateMachine;
            AnimatorState U(string n, Vector3 p) { var s = usm.AddState(n, p); s.motion = clips[n]; return s; }
            var c2 = U("Carry2", new Vector3(300, 0)); var c1 = U("Carry1", new Vector3(300, 70)); var tool = U("Tool", new Vector3(300, 140));
            var wind = U("Windup", new Vector3(560, 0)); var toss = U("Toss", new Vector3(560, 70));
            usm.defaultState = c2;
            AnimatorStateTransition A(AnimatorState to, float dur)
            {
                var t = usm.AddAnyStateTransition(to); t.canTransitionToSelf = false; t.duration = dur; t.hasExitTime = false; return t;
            }
            var tt = A(tool, 0.15f); tt.AddCondition(AnimatorConditionMode.If, 0, "Tool"); tt.AddCondition(AnimatorConditionMode.Less, 0.05f, "ThrowCharge");
            var t1 = A(c1, 0.15f); t1.AddCondition(AnimatorConditionMode.Equals, 1, "CarryHands"); t1.AddCondition(AnimatorConditionMode.IfNot, 0, "Tool"); t1.AddCondition(AnimatorConditionMode.Less, 0.05f, "ThrowCharge");
            var t2 = A(c2, 0.15f); t2.AddCondition(AnimatorConditionMode.Greater, 1, "CarryHands"); t2.AddCondition(AnimatorConditionMode.IfNot, 0, "Tool"); t2.AddCondition(AnimatorConditionMode.Less, 0.05f, "ThrowCharge");
            var tw = A(wind, 0.25f); tw.AddCondition(AnimatorConditionMode.Greater, 0.05f, "ThrowCharge");
            var ts = A(toss, 0.05f); ts.AddCondition(AnimatorConditionMode.If, 0, "Toss");
            var back = toss.AddTransition(c2); back.hasExitTime = true; back.exitTime = 0.95f; back.duration = 0.1f;
            EditorUtility.SetDirty(ac);
            return ac;
        }

        static void Exit(AnimatorState from, AnimatorState to, float exitTime)
        {
            var t = from.AddTransition(to); t.hasExitTime = true; t.exitTime = exitTime; t.duration = 0.2f;
        }

        // ---- Prefab -------------------------------------------------------------------------------------------------

        /// <summary>Vertex classes read from the mask/region textures (bind pose, world space).</summary>
        public struct Measure
        {
            public float headWidth, eyeY, faceZ, crownY, headX, headZ, chestY, backZ, frontZ, topY;
        }

        static Texture2D ReadPng(string path)
        {
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            t.LoadImage(File.ReadAllBytes(path));
            return t;
        }

        /// <summary>Head and torso landmarks from mask-classified vertices (skin, hair, fixed eye texels, shirt/overalls).</summary>
        public static Measure MeasureBody(SkinnedMeshRenderer smr, Transform chest, Transform neck)
        {
            var mesh = new Mesh();
            smr.BakeMesh(mesh, false);
            var verts = mesh.vertices.Select(v => smr.transform.position + smr.transform.rotation * v).ToArray();
            var uvs = smr.sharedMesh.uv;
            var mask = ReadPng(MaskPath); var reg = ReadPng(RegionsPath);
            Object.DestroyImmediate(mesh);
            var skin = new List<Vector3>(); var eyes = new List<Vector3>(); var cloth = new List<Vector3>();
            float neckY = neck.position.y, top = float.MinValue;
            for (int i = 0; i < verts.Length && i < uvs.Length; i++)
            {
                var p = verts[i]; top = Mathf.Max(top, p.y);
                Color m = mask.GetPixelBilinear(uvs[i].x, uvs[i].y), r = reg.GetPixelBilinear(uvs[i].x, uvs[i].y);
                if (p.y > neckY && (m.a > 0.5f || r.b > 0.5f)) skin.Add(p);
                else if (p.y > neckY && m.r + m.g + m.b + m.a < 0.2f && r.r + r.g + r.b < 0.2f) eyes.Add(p);
                if (m.r > 0.5f || m.g > 0.5f) cloth.Add(p);
            }
            Object.DestroyImmediate(mask); Object.DestroyImmediate(reg);
            var me = new Measure();
            me.headWidth = skin.Count > 0 ? skin.Max(v => v.x) - skin.Min(v => v.x) : 0.4f;
            me.headX = skin.Count > 0 ? (skin.Max(v => v.x) + skin.Min(v => v.x)) * 0.5f : 0f;
            me.headZ = skin.Count > 0 ? (skin.Max(v => v.z) + skin.Min(v => v.z)) * 0.5f : 0f;
            float skinMidY = skin.Count > 0 ? skin.Average(v => v.y) : neckY + 0.2f;
            var eyeCandidates = eyes.Where(v => v.y > skinMidY && v.z > me.headZ).ToList();
            me.eyeY = eyeCandidates.Count > 0 ? eyeCandidates.Average(v => v.y) : skinMidY + 0.05f;
            me.faceZ = skin.Where(v => Mathf.Abs(v.y - me.eyeY) < 0.06f && Mathf.Abs(v.x - me.headX) < 0.08f).DefaultIfEmpty(new Vector3(0, 0, me.headZ + 0.15f)).Max(v => v.z);
            me.crownY = me.eyeY + 0.146f * (me.headWidth / 0.22f) * 0.85f;
            me.chestY = chest.position.y;
            var band = cloth.Where(v => Mathf.Abs(v.y - me.chestY) < 0.08f && Mathf.Abs(v.x) < 0.15f).ToList();
            me.backZ = band.Count > 0 ? band.Min(v => v.z) : -0.15f;
            me.frontZ = band.Count > 0 ? band.Max(v => v.z) : 0.15f;
            me.topY = top;
            Debug.Log($"[HeroFarmerBuilder] head width {me.headWidth:F3} centre ({me.headX:F3},{me.headZ:F3}) eyeY {me.eyeY:F3} faceZ {me.faceZ:F3} crownY {me.crownY:F3} chest {me.chestY:F3} back {me.backZ:F3} front {me.frontZ:F3} top {me.topY:F3} (skin {skin.Count} eyes {eyes.Count})");
            return me;
        }
    }
}
