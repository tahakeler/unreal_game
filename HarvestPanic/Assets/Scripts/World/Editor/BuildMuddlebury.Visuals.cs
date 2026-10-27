using HarvestPanic.Core;
using HarvestPanic.Weather;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HarvestPanic.World.Editor
{
    /// <summary>
    /// Visual overhaul (feature/visual-overhaul): the scene's static look = WeatherLook.ForPhase(None) (the no-clock golden
    /// afternoon), so edit mode, QA captures without a WeatherDirector and the live WeatherPresenter all agree and the
    /// presenter takes over without a pop. Used by Build() and, for an existing scene, by ApplyVisualBaseline (no rebuild):
    /// Unity -batchmode -quit -projectPath HarvestPanic -executeMethod HarvestPanic.World.Editor.BuildMuddlebury.ApplyVisualBaseline
    /// It switches the terrains to HarvestPanic/Stylized Terrain, the sky to HarvestPanic/Stylized Sky, regenerates the
    /// terrain layer textures at 512 px, and writes sun / ambient / fog / VP_Muddlebury from the look.
    /// </summary>
    public static partial class BuildMuddlebury
    {
        const string TerrainMatPath = WorldAssets.MatDir + "/M_World_Terrain.mat";
        const string SkyMatPath = WorldAssets.MatDir + "/M_World_Sky.mat";

        /// <summary>Trilight base colours; WeatherLook.Ambient multiplies them at runtime.</summary>
        static readonly Color AmbientSky = WorldAssets.Hex("A9C4DC"), AmbientEquator = WorldAssets.Hex("D8C3A0"), AmbientGround = WorldAssets.Hex("6E5A44");

        static WeatherLook BaselineLook => WeatherLook.ForPhase(DayClockPhase.None, 0f);

        /// <summary>Fixed aerial-perspective share of the horizon hills (was 0.5: washed-out dunes against the new sky).</summary>
        const float HorizonFogMix = 0.2f;

        [MenuItem("Harvest Panic/World/Apply Visual Baseline (stylized)")]
        public static void ApplyVisualBaseline()
        {
            try
            {
                var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                _layers = CreateLayers(); // rewrites the TerrainLayer assets + textures in place (GUIDs kept)
                var terrainMat = TerrainMaterial();
                foreach (var t in Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None)) { t.materialTemplate = terrainMat; EditorUtility.SetDirty(t); }
                ApplySceneLook(RenderSettings.sun, BaselineLook);
                var hills = AssetDatabase.LoadAssetAtPath<Material>(WorldAssets.MatDir + "/M_World_HorizonHills.mat");
                if (hills != null) { hills.SetFloat("_FogMix", HorizonFogMix); EditorUtility.SetDirty(hills); }
                var vol = Object.FindObjectsByType<Volume>(FindObjectsSortMode.None);
                foreach (var v in vol) if (v.sharedProfile != null && v.sharedProfile.name == "VP_Muddlebury") ApplyVolumeBaseline(v.sharedProfile, BaselineLook);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene, ScenePath);
                AssetDatabase.SaveAssets();
                Debug.Log("[BuildMuddlebury] visual baseline applied (stylized terrain + sky, 512 px ground, afternoon look)");
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        /// <summary>Stylized terrain material (falls back to URP Terrain Lit if the shader is missing).</summary>
        static Material TerrainMaterial()
        {
            var sh = Shader.Find("HarvestPanic/Stylized Terrain");
            if (sh == null) return AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/TerrainLit.mat");
            var m = AssetDatabase.LoadAssetAtPath<Material>(TerrainMatPath);
            if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, TerrainMatPath); }
            m.shader = sh;
            m.SetFloat("_EnableInstancedPerPixelNormal", 1f);
            m.EnableKeyword("_TERRAIN_INSTANCED_PERPIXEL_NORMAL");
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>The scene skybox: HarvestPanic/Stylized Sky with the look's gradient, clouds and sun disc.</summary>
        static Material SkyMaterial(in WeatherLook l)
        {
            var sky = AssetDatabase.LoadAssetAtPath<Material>(SkyMatPath);
            var sh = Shader.Find("HarvestPanic/Stylized Sky") ?? Shader.Find("Skybox/Procedural");
            if (sky == null) { sky = new Material(sh); AssetDatabase.CreateAsset(sky, SkyMatPath); }
            if (sky.shader != sh) sky.shader = sh;
            // Static cloud shape (the look only drives cover/colours): crisp painted edges, mid-size puffs, slow drift.
            sky.SetFloat("_CloudSoftness", 0.022f);
            sky.SetFloat("_CloudScale", 2.2f);
            sky.SetVector("_CloudSpeed", new Vector4(0.006f, 0.002f, 0f, 0f));
            sky.SetFloat("_CloudOpacity", 0.96f);
            sky.SetFloat("_HorizonExponent", 0.5f);
            WeatherLookApplier.ApplySky(sky, l);
            EditorUtility.SetDirty(sky);
            return sky;
        }

        /// <summary>Sun, sky, trilight ambient and linear fog from a look (what WeatherLookApplier does at runtime).</summary>
        static void ApplySceneLook(Light sun, in WeatherLook l)
        {
            if (sun != null)
            {
                float yaw = sun.transform.rotation.eulerAngles.y;
                sun.transform.rotation = Quaternion.Euler(l.SunPitch, yaw, 0f);
                sun.color = l.SunColor; sun.intensity = l.SunIntensity;
                sun.shadows = LightShadows.Soft;
                sun.shadowStrength = WeatherLookApplier.SunShadowStrength;
                EditorUtility.SetDirty(sun);
                EditorUtility.SetDirty(sun.transform);
            }
            RenderSettings.skybox = SkyMaterial(l);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = AmbientSky * l.Ambient;
            RenderSettings.ambientEquatorColor = AmbientEquator * l.Ambient;
            RenderSettings.ambientGroundColor = AmbientGround * l.Ambient;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = l.FogColor;
            RenderSettings.fogStartDistance = l.FogStart;
            RenderSettings.fogEndDistance = l.FogEnd;
        }

        /// <summary>
        /// VP_Muddlebury = the static grade of the baseline look (the runtime Weather Volume, priority 20, overrides the
        /// same parameters per phase). Neutral tonemapping keeps the palette's hues (ACES pushes yellows to orange).
        /// </summary>
        static void ApplyVolumeBaseline(VolumeProfile profile, in WeatherLook l)
        {
            T Get<T>() where T : VolumeComponent
            {
                if (!profile.TryGet<T>(out var c)) { c = profile.Add<T>(false); AssetDatabase.AddObjectToAsset(c, profile); }
                return c;
            }
            Get<Tonemapping>().mode.Override(TonemappingMode.Neutral);
            var ca = Get<ColorAdjustments>();
            ca.saturation.Override(l.Saturation); ca.contrast.Override(l.Contrast); ca.postExposure.Override(l.PostExposure);
            ca.colorFilter.Override(Color.white);
            var wb = Get<WhiteBalance>(); wb.temperature.Override(l.Temperature); wb.tint.Override(0f);
            var bloom = Get<Bloom>(); bloom.intensity.Override(l.BloomIntensity); bloom.threshold.Override(l.BloomThreshold); bloom.scatter.Override(0.62f);
            var smh = Get<ShadowsMidtonesHighlights>();
            smh.shadows.Override(new Vector4(0.97f, 0.96f, 1.04f, 0f)); // a breath of violet in the darkest values (art bible 5)
            smh.highlights.Override(new Vector4(1.02f, 1.0f, 0.97f, 0f));
            var lgg = Get<LiftGammaGain>(); lgg.lift.Override(new Vector4(l.Lift.r, l.Lift.g, l.Lift.b, l.Lift.a));
            var vig = Get<Vignette>(); vig.intensity.Override(l.Vignette); vig.smoothness.Override(0.45f);
            var mb = Get<MotionBlur>(); mb.active = false;
            EditorUtility.SetDirty(profile);
        }
    }
}
