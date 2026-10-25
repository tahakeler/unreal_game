using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarvestPanic.Customization;
using HarvestPanic.Customization.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

namespace HarvestPanic.Tests.EditMode.Customization
{
    /// <summary>
    /// The hero Tripo farmer: recolour mask (coverage, protected face), recolour maths (identity on the painted colour),
    /// home swatches = starter loadout, skin weights, humanoid avatar + retarget fidelity (the A-pose regression),
    /// body controller wiring and the player prefab using all of it.
    /// </summary>
    public class HeroFarmerEditModeTests
    {
        const string PrefabPath = "Assets/Prefabs/Farmer/FarmerBody.prefab";
        const string QuaterniusFbx = "Assets/ThirdParty/Characters/QuaterniusFarmer/Farmer.fbx";

        [Serializable] class Deform { public float mean_mm, p99_mm, max_mm; public int max_influences; }
        [Serializable] class DeformSet { public Deform collapse_weights, transferred_weights; }
        [Serializable] class Face { public bool found; public float eye_radius, protected_pct; }
        [Serializable] class Stats { public Face face; public DeformSet deform; public int output_tris, source_tris; }

        static Stats ReadStatsFile() => JsonUtility.FromJson<Stats>(File.ReadAllText(HeroFarmerBuilder.StatsPath));

        static Color32[] Pixels(string assetPath, out int w)
        {
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            Assert.That(t.LoadImage(File.ReadAllBytes(assetPath)), assetPath);
            w = t.width;
            var px = t.GetPixels32();
            Object.DestroyImmediate(t);
            return px;
        }

        [Test]
        public void Mask_ZoneCoverage_MatchesTheStatsAndIsPlausible()
        {
            var (_, coverage) = HeroFarmerBuilder.ReadStats();
            var mask = Pixels(HeroFarmerBuilder.MaskPath, out _);
            float n = mask.Length;
            float[] measured =
            {
                mask.Count(p => p.r > 127) / n * 100f, mask.Count(p => p.g > 127) / n * 100f,
                mask.Count(p => p.b > 127) / n * 100f, mask.Count(p => p.a > 127) / n * 100f,
            };
            string[] names = { "overalls", "shirt", "boots/gloves", "skin" };
            float[] lo = { 10f, 3f, 12f, 10f }, hi = { 35f, 20f, 40f, 35f };
            for (int i = 0; i < 4; i++)
            {
                Assert.That(measured[i], Is.EqualTo(coverage[i]).Within(1.5f), names[i] + " coverage drifted from the stats");
                Assert.That(measured[i], Is.InRange(lo[i], hi[i]), names[i] + " coverage implausible");
            }
        }

        [Test]
        public void Mask_ProtectsTheEyes_AndFindsTheFace()
        {
            var s = ReadStatsFile();
            Assert.That(s.face.found, "eyes located from the sclera");
            Assert.That(s.face.protected_pct, Is.InRange(0.3f, 4f), "face features protected (eyes, brows, mouth), not the whole face");
            var basePx = Pixels(HeroFarmerBuilder.BaseColorPath, out int w);
            var mask = Pixels(HeroFarmerBuilder.MaskPath, out int mw);
            var reg = Pixels(HeroFarmerBuilder.RegionsPath, out int rw);
            Assert.That(mw, Is.EqualTo(w));
            int sclera = 0, tinted = 0;
            for (int i = 0; i < basePx.Length; i++)
            {
                var c = basePx[i];
                int mx = Mathf.Max(c.r, Mathf.Max(c.g, c.b)), mn = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
                bool white = mx > 215 && (mx - mn) < 0.18f * mx;
                var m = mask[i];
                if (!white || m.g > 64) continue; // the shirt is white too (and recoloured on purpose)
                int x = i % w, y = i / w;
                var r = reg[(y * rw / w) * rw + x * rw / w];
                if (r.r > 127) continue; // straw highlights on the hat
                sclera++;
                if (m.r > 64 || m.b > 64 || m.a > 64 || r.b > 64 || r.g > 64) tinted++;
            }
            Assert.That(sclera, Is.GreaterThan(500), "eye whites present in the atlas");
            Assert.That(tinted / (float)sclera, Is.LessThan(0.03f), "eye whites must never take a skin/hair/clothes tint");
        }

        [Test]
        public void RecolourMath_IdentityOnThePaintedColour_TintAtTheMeanAndMaskZeroUntouched()
        {
            var (refs, _) = HeroFarmerBuilder.ReadStats();
            var identity = refs.Select(r => (Vector3)r).ToArray();
            var rng = new System.Random(7);
            for (int k = 0; k < 200; k++)
            {
                var texel = new Vector3((float)rng.NextDouble(), (float)rng.NextDouble(), (float)rng.NextDouble());
                var m = new Vector4((float)rng.NextDouble(), (float)rng.NextDouble(), (float)rng.NextDouble(), (float)rng.NextDouble());
                var g = new Vector4(0f, (float)rng.NextDouble(), (float)rng.NextDouble(), 0f);
                var outC = FarmerRecolourMath.Apply(texel, m, g, refs, identity);
                Assert.That(Vector3.Distance(outC, texel), Is.LessThan(1e-5f), "tint == ref must reproduce the texture");
                var tints = Enumerable.Range(0, 6).Select(_ => new Vector3((float)rng.NextDouble(), (float)rng.NextDouble(), (float)rng.NextDouble())).ToArray();
                Assert.That(Vector3.Distance(FarmerRecolourMath.Apply(texel, Vector4.zero, Vector4.zero, refs, tints), texel), Is.LessThan(1e-6f), "mask 0 keeps the paint");
            }
            // a texel at the zone mean becomes the swatch exactly; folds keep their relative darkness
            var tintsA = new Vector3[6]; for (int i = 0; i < 6; i++) tintsA[i] = identity[i];
            tintsA[FarmerVisual.ZonePrimary] = new Vector3(0.8f, 0.1f, 0.1f);
            var mean = (Vector3)refs[FarmerVisual.ZonePrimary];
            // (the stats round the mean luminance to 4 decimals, so "at the mean" is within a few thousandths)
            Assert.That(Vector3.Distance(FarmerRecolourMath.Apply(mean, new Vector4(1, 0, 0, 0), Vector4.zero, refs, tintsA), tintsA[0]), Is.LessThan(3e-3f));
            var fold = FarmerRecolourMath.Apply(mean * 0.5f, new Vector4(1, 0, 0, 0), Vector4.zero, refs, tintsA);
            Assert.That(FarmerRecolourMath.Luminance(fold), Is.EqualTo(FarmerRecolourMath.Luminance(tintsA[0]) * 0.5f).Within(0.01f));
            // the shader carries the same formula
            var shader = File.ReadAllText("Assets/Art/Cosmetics/Shaders/FarmerRecolour.shader");
            StringAssert.Contains("c + saturate(m) * shade * (tint - refColour.rgb)", shader);
            StringAssert.Contains("clamp(lum / max(refColour.a, 0.02), 0.0, 1.6)", shader);
        }

        [Test]
        public void HomeSwatches_AreTheStarterLoadout_AndThePrefabShowsThePaintedColours()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CosmeticCatalog>(FarmerAssetBuilder.CatalogPath);
            Assert.That(catalog, Is.Not.Null);
            var (refs, _) = HeroFarmerBuilder.ReadStats();
            var homes = HeroFarmerBuilder.HomeSwatches(catalog, refs);
            var d = FarmerLoadout.Default;
            Assert.That(homes, Is.EqualTo(new[] { (int)d.Primary, d.Secondary, d.Accent, d.Skin, d.HairColor, d.NameTag }),
                "FarmerLoadout.Default must use the hero's home swatches so the starter farmer is the painted model");
            var v = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).GetComponent<FarmerVisual>();
            Assert.That(v.HomeSwatches, Is.EqualTo(homes));
            var tints = v.TintsFor(d);
            Color[] shown = { tints.Primary, tints.Secondary, tints.Accent, tints.Skin, tints.Hair, tints.NameTag };
            for (int i = 0; i < 6; i++)
            {
                var lin = shown[i].linear;
                Assert.That(new Vector3(lin.r, lin.g, lin.b), Is.EqualTo((Vector3)refs[i]).Using(new Vector3EqualityComparer(0.002f)), "zone " + i);
            }
            // any other swatch is the palette colour
            var l = d; l.Primary = (byte)((d.Primary + 1) % catalog.palette.Count);
            Assert.That(v.TintsFor(l).Primary, Is.EqualTo(catalog.PaletteColor(l.Primary)));
        }

        sealed class Vector3EqualityComparer : IEqualityComparer<Vector3>
        {
            readonly float _tol;
            public Vector3EqualityComparer(float tol) { _tol = tol; }
            public bool Equals(Vector3 a, Vector3 b) => Vector3.Distance(a, b) <= _tol;
            public int GetHashCode(Vector3 v) => 0;
        }

        [Test]
        public void Mesh_Decimated_WeightsTransferred_FourInfluences()
        {
            var s = ReadStatsFile();
            Assert.That(s.output_tris, Is.InRange(9000, 11000));
            Assert.That(s.deform.transferred_weights.max_influences, Is.LessThanOrEqualTo(4));
            Assert.That(s.deform.transferred_weights.mean_mm, Is.LessThan(2f), "decimated mesh deforms like the source in the stress pose");
            Assert.That(s.deform.transferred_weights.p99_mm, Is.LessThan(10f));
            var mesh = AssetDatabase.LoadAllAssetsAtPath(HeroFarmerBuilder.HeroFbx).OfType<Mesh>().First();
            var perVertex = mesh.GetBonesPerVertex();
            Assert.That(perVertex.Length, Is.EqualTo(mesh.vertexCount));
            Assert.That(perVertex.ToArray().Max(), Is.LessThanOrEqualTo(4));
            var uv1 = new List<Vector2>(); mesh.GetUVs(1, uv1);
            Assert.That(uv1.Count, Is.EqualTo(mesh.vertexCount), "PatternUV (outfit patterns) present");
        }

        [Test]
        public void Avatars_AreValidHumanoids()
        {
            foreach (var path in new[] { HeroFarmerBuilder.HeroFbx, QuaterniusFbx })
            {
                var avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
                Assert.That(avatar, Is.Not.Null, path);
                Assert.That(avatar.isValid && avatar.isHuman, path);
                var imp = (ModelImporter)AssetImporter.GetAtPath(path);
                Assert.That(imp.animationType, Is.EqualTo(ModelImporterAnimationType.Human), path);
                var human = imp.humanDescription.human.Select(h => h.humanName).ToList();
                foreach (var b in new[] { "Hips", "Spine", "Chest", "Neck", "Head", "LeftShoulder", "LeftUpperArm", "LeftLowerArm", "LeftHand",
                             "RightShoulder", "RightUpperArm", "RightLowerArm", "RightHand", "LeftUpperLeg", "LeftLowerLeg", "LeftFoot", "RightUpperLeg", "RightLowerLeg", "RightFoot" })
                    Assert.That(human, Does.Contain(b), path + " maps " + b);
            }
            // the hero's limbs are mapped to the limb bones, never to the twist helpers
            var heroMap = ((ModelImporter)AssetImporter.GetAtPath(HeroFarmerBuilder.HeroFbx)).humanDescription.human.ToDictionary(h => h.humanName, h => h.boneName);
            Assert.That(heroMap["LeftUpperArm"], Is.EqualTo("upperarm_l")); Assert.That(heroMap["RightLowerArm"], Is.EqualTo("lowerarm_r"));
            Assert.That(heroMap["LeftShoulder"], Is.EqualTo("clavicle_l")); Assert.That(heroMap.Values.Any(n => n.Contains("twist")), Is.False);
        }

        static readonly (HumanBodyBones a, HumanBodyBones b)[] Segments =
        {
            (HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm), (HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand),
            (HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm), (HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand),
            (HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg), (HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg),
            (HumanBodyBones.Hips, HumanBodyBones.Neck), (HumanBodyBones.Neck, HumanBodyBones.Head),
        };

        static Vector3[] SampleDirections(GameObject model, AnimationClip clip, float t)
        {
            var go = (GameObject)Object.Instantiate(model);
            try
            {
                var anim = go.GetComponentInChildren<Animator>();
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var g = PlayableGraph.Create("RetargetTest");
                var o = AnimationPlayableOutput.Create(g, "o", anim);
                var p = AnimationClipPlayable.Create(g, clip);
                p.SetApplyFootIK(false);
                o.SetSourcePlayable(p); p.SetTime(t); g.Evaluate(0f);
                var frame = HeroFarmerBuilder.CharacterFrame(anim);
                var inv = Quaternion.Inverse(frame);
                var dirs = Segments.Select(s => inv * (anim.GetBoneTransform(s.b).position - anim.GetBoneTransform(s.a).position).normalized).ToArray();
                g.Destroy();
                return dirs;
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Retarget_HeroLimbsFollowTheSourceClips()
        {
            // Regression for the A-pose avatar reference (arms 25-60 deg off, bent backwards / into the belly): every
            // retargeted Quaternius clip must put the hero's arms, thighs and spine where the source avatar puts its own.
            var clips = AssetDatabase.LoadAllAssetsAtPath(QuaterniusFbx).OfType<AnimationClip>().ToDictionary(c => c.name);
            var hero = AssetDatabase.LoadAssetAtPath<GameObject>(HeroFarmerBuilder.HeroFbx);
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(QuaterniusFbx);
            foreach (var (name, frac) in new[] { ("Idle", 0f), ("Walk", 0.25f), ("Run", 0.25f), ("Wave", 0.5f), ("Interact", 0.5f), ("Point", 0.5f) })
            {
                Assert.That(clips.ContainsKey(name), name);
                var clip = clips[name];
                var a = SampleDirections(source, clip, clip.length * frac);
                var b = SampleDirections(hero, clip, clip.length * frac);
                for (int i = 0; i < Segments.Length; i++)
                    Assert.That(Vector3.Angle(a[i], b[i]), Is.LessThan(12f), $"{name}: {Segments[i].a}->{Segments[i].b}");
            }
        }

        [Test]
        public void Controller_HasTheGameplayParams_UpperBodyLayer_AndHumanoidClips()
        {
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(HeroFarmerBuilder.ControllerPath);
            Assert.That(ctrl, Is.Not.Null);
            var p = ctrl.parameters.ToDictionary(x => x.name, x => x.type);
            var want = new Dictionary<string, AnimatorControllerParameterType>
            {
                { "Speed", AnimatorControllerParameterType.Float }, { "Grounded", AnimatorControllerParameterType.Bool }, { "Jump", AnimatorControllerParameterType.Trigger },
                { "Fainted", AnimatorControllerParameterType.Bool }, { "CarryHands", AnimatorControllerParameterType.Int }, { "Tool", AnimatorControllerParameterType.Bool },
                { "ThrowCharge", AnimatorControllerParameterType.Float }, { "Toss", AnimatorControllerParameterType.Trigger }, { "Down", AnimatorControllerParameterType.Bool },
                { "GetUp", AnimatorControllerParameterType.Trigger },
            };
            foreach (var kv in want)
            {
                Assert.That(p.ContainsKey(kv.Key), "param " + kv.Key);
                Assert.That(p[kv.Key], Is.EqualTo(kv.Value), "param type " + kv.Key);
            }
            Assert.That(ctrl.layers.Length, Is.GreaterThanOrEqualTo(2));
            var upper = ctrl.layers[FarmerVisual.UpperBodyLayer];
            Assert.That(upper.avatarMask, Is.Not.Null, "upper-body layer is masked");
            Assert.That(upper.avatarMask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm) && upper.avatarMask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm));
            Assert.That(upper.avatarMask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftLeg), Is.False, "legs keep walking under a carry");
            var upperStates = upper.stateMachine.states.Select(s => s.state.name).ToList();
            foreach (var s in new[] { "Carry1", "Carry2", "Tool", "Windup", "Toss" }) Assert.That(upperStates, Does.Contain(s));
            var baseStates = ctrl.layers[0].stateMachine.states.Select(s => s.state.name).ToList();
            foreach (var s in new[] { "Locomotion", "Faint", "Down", "GetUp", "Wave", "Dance" }) Assert.That(baseStates, Does.Contain(s));
            foreach (var c in ctrl.animationClips) Assert.That(c.humanMotion, c.name + " is a humanoid clip (retargets to future characters)");
        }

        [Test]
        public void PlayerBodyPrefab_UsesTheHeroModel_RecolourShader_AndHeroController()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var v = prefab.GetComponent<FarmerVisual>();
            Assert.That(v.RecolourRenderers.Count, Is.EqualTo(1));
            var smr = (SkinnedMeshRenderer)v.RecolourRenderers[0];
            Assert.That(AssetDatabase.GetAssetPath(smr.sharedMesh), Is.EqualTo(HeroFarmerBuilder.HeroFbx));
            Assert.That(smr.sharedMaterial.shader.name, Is.EqualTo(HeroFarmerBuilder.ShaderName));
            foreach (var prop in HeroFarmerBuilder.RefProperties) Assert.That(smr.sharedMaterial.GetVector(prop).w, Is.GreaterThan(0f), prop);
            var anim = v.BodyAnimator;
            Assert.That(anim.avatar != null && anim.avatar.isValid && anim.avatar.isHuman);
            Assert.That(AssetDatabase.GetAssetPath(anim.runtimeAnimatorController), Is.EqualTo(HeroFarmerBuilder.ControllerPath));
            Assert.That(v.BakedHatKey, Is.EqualTo("hat.straw"));
        }
    }
}
