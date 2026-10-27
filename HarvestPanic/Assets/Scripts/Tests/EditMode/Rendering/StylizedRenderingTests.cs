using System.Linq;
using HarvestPanic.Core;
using HarvestPanic.Rendering.Editor;
using HarvestPanic.Weather;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HarvestPanic.Tests.EditMode.Rendering
{
    /// <summary>
    /// Visual-overhaul contracts: the stylized shaders compile and keep SRP Batcher / GPU Resident Drawer support, no Lit
    /// material sneaks back into the converted folders (asset builders recreate Lit materials - re-run the converter), the
    /// recolour property names stay URP Lit's, and the deliberate URP settings are what the art direction chose.
    /// </summary>
    public class StylizedRenderingTests
    {
        static readonly string[] Shaders =
        {
            "HarvestPanic/Stylized Lit", "HarvestPanic/Stylized Lit (Outlined)", "HarvestPanic/Stylized Terrain",
            "Hidden/HarvestPanic/Stylized Terrain (Add Pass)", "Hidden/HarvestPanic/Stylized Terrain (Base Pass)",
            "HarvestPanic/Stylized Sky", "HarvestPanic/StylizedWater",
        };

        [Test]
        public void StylizedShaders_ExistAndCompile()
        {
            foreach (var name in Shaders)
            {
                var sh = Shader.Find(name);
                Assert.That(sh, Is.Not.Null, name);
                Assert.That(ShaderUtil.ShaderHasError(sh), Is.False, name + " has compile errors");
            }
        }

        [Test]
        public void StylizedLit_SupportsDotsInstancing_ForGpuResidentDrawer()
        {
            foreach (var name in new[] { "HarvestPanic/Stylized Lit", "HarvestPanic/Stylized Lit (Outlined)" })
            {
                var sh = Shader.Find(name);
                Assert.That(sh.keywordSpace.keywordNames, Does.Contain("DOTS_INSTANCING_ON"), name);
                Assert.That(sh.keywordSpace.keywordNames, Does.Contain("_CLUSTER_LIGHT_LOOP"), name + " (Forward+)");
                Assert.That(sh.keywordSpace.keywordNames, Does.Contain("_SCREEN_SPACE_OCCLUSION"), name + " (SSAO)");
            }
        }

        [Test]
        public void StylizedLit_KeepsUrpLitRecolourPropertyNames()
        {
            var m = new Material(Shader.Find("HarvestPanic/Stylized Lit"));
            try
            {
                foreach (var p in new[] { "_BaseColor", "_BaseMap", "_Color", "_Smoothness", "_EmissionColor", "_Cutoff", "_Surface" })
                    Assert.That(m.HasProperty(p), p);
            }
            finally { Object.DestroyImmediate(m); }
        }

        [Test]
        public void NoUnconvertedLitMaterials_InStylizedFolders()
        {
            var left = StylizedMaterialConverter.FindUnconverted();
            Assert.That(left, Is.Empty, "Run Harvest Panic > Rendering > Convert Materials to Stylized:\n" + string.Join("\n", left));
        }

        [Test]
        public void ConverterProfiles_OutlineCrittersAndHeroProps_NotWorld()
        {
            Assert.That(StylizedMaterialConverter.ProfileFor("Assets/Prefabs/Critters/Materials/M_Critter_Pig.mat").Value.Outline, Is.True);
            Assert.That(StylizedMaterialConverter.ProfileFor("Assets/Art/Tripo/Materials/M_Tripo_PrizePumpkin.mat").Value.Outline, Is.True);
            Assert.That(StylizedMaterialConverter.ProfileFor("Assets/Art/World/Materials/M_World_BarnBoards.mat").Value.Outline, Is.False);
            Assert.That(StylizedMaterialConverter.ProfileFor("Assets/Art/Cosmetics/Materials/M_Anything.mat"), Is.Null, "character branch owns the farmer");
        }

        [Test]
        public void UrpAsset_HasTheChosenShadowsAaAndSsao()
        {
            var urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(VisualPipelineSetup.PipelinePath);
            Assert.That(urp, Is.Not.Null);
            Assert.That(urp.shadowDistance, Is.EqualTo(VisualPipelineSetup.ShadowDistance).Within(0.01f));
            Assert.That(urp.shadowCascadeCount, Is.EqualTo(VisualPipelineSetup.ShadowCascades));
            Assert.That(urp.mainLightShadowmapResolution, Is.EqualTo(VisualPipelineSetup.ShadowResolution));
            Assert.That(urp.supportsSoftShadows, Is.True);
            Assert.That(urp.msaaSampleCount, Is.EqualTo(VisualPipelineSetup.Msaa));
            Assert.That(urp.supportsHDR, Is.True);
            Assert.That(urp.supportsCameraDepthTexture, Is.True, "water depth tint + shore foam");
            Assert.That(urp.useSRPBatcher, Is.True);
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(VisualPipelineSetup.RendererPath);
            var ssao = renderer.rendererFeatures.OfType<ScreenSpaceAmbientOcclusion>().FirstOrDefault();
            Assert.That(ssao, Is.Not.Null, "SSAO renderer feature");
            Assert.That(ssao.isActive, Is.True);
        }

        [Test]
        public void MuddleburyAssets_UseStylizedSkyAndTerrain()
        {
            var sky = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/World/Materials/M_World_Sky.mat");
            Assert.That(sky != null && sky.shader.name == "HarvestPanic/Stylized Sky", "M_World_Sky");
            var terrain = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/World/Materials/M_World_Terrain.mat");
            Assert.That(terrain != null && terrain.shader.name == "HarvestPanic/Stylized Terrain", "M_World_Terrain");
        }

        [Test]
        public void LookApplier_SetsAndClearsStylizedGlobals()
        {
            var go = new GameObject("sun"); var sun = go.AddComponent<Light>(); sun.type = LightType.Directional;
            var prevSun = RenderSettings.sun; RenderSettings.sun = sun;
            var applier = new WeatherLookApplier();
            try
            {
                applier.Capture(null);
                var night = WeatherLook.ForPhase(DayClockPhase.Dusk, 1f);
                applier.Apply(night);
                Assert.That(Shader.GetGlobalVector(WeatherLookApplier.LookParamsId).z, Is.EqualTo(night.AmbientFloor).Within(1e-4f));
                Assert.That(sun.intensity, Is.EqualTo(night.SunIntensity).Within(1e-4f));
                Assert.That(applier.Volume, Is.Not.Null);
                Assert.That(applier.Volume.priority, Is.EqualTo(20f));
                applier.Restore();
                Assert.That(Shader.GetGlobalVector(WeatherLookApplier.LookParamsId), Is.EqualTo(Vector4.zero));
                Assert.That(Shader.GetGlobalVector(WeatherLookApplier.ShadowShiftId), Is.EqualTo(Vector4.zero));
            }
            finally { applier.Restore(); RenderSettings.sun = prevSun; Object.DestroyImmediate(go); }
        }
    }
}
