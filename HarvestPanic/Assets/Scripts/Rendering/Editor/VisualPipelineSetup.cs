using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HarvestPanic.Rendering.Editor
{
    /// <summary>
    /// Deliberate URP settings for the stylized look (feature/visual-overhaul). Idempotent; the values live here so a
    /// build run that re-serializes HP_URP.asset (shader prefilter flags) never silently changes the look.
    /// Batch: -executeMethod HarvestPanic.Rendering.Editor.VisualPipelineSetup.ConfigurePipeline
    ///
    /// Choices (art bible 5, Mac High tier; one URP asset ships today):
    ///  * Shadows: 110 m, 4 cascades (6.6 / 20 / 46 / 110 m), 4096 atlas (2048 per cascade), soft shadows (medium PCF).
    ///    The open farm is read from 60-200 m (spawn road, aerial, bridge) and the old 60 m / 1 cascade left every tree
    ///    past the yard shadowless and the near shadows blurry.
    ///  * SSAO: renderer feature, depth-normals, half resolution, blue noise, radius 0.4 m, intensity 1.1, direct 0.15.
    ///    Grounds props, fences, critters and buildings (contact shadows the ramp shading cannot give).
    ///  * AA: MSAA 4x, not TAA. Apple-silicon GPUs resolve MSAA in tile memory (cheap), the look is flat colour with hard
    ///    geometric edges (exactly what MSAA fixes), and TAA would smear fast ragdolls/critters and rain streaks in the
    ///    15-second comedy clips the game is built around (art bible 1, 5: crisp, no motion blur). URP TAA also cannot
    ///    combine with MSAA.
    ///  * HDR on (bloom on lanterns, sun disc, emissive windows), depth texture on (water depth tint + shore foam).
    /// </summary>
    public static class VisualPipelineSetup
    {
        public const string PipelinePath = "Assets/Settings/HP_URP.asset";
        public const string RendererPath = "Assets/Settings/HP_URP_Renderer.asset";

        public const float ShadowDistance = 110f;
        public const int ShadowCascades = 4;
        public static readonly Vector3 Cascade4Split = new Vector3(0.06f, 0.18f, 0.42f);
        public const int ShadowResolution = 4096;
        public const int Msaa = 4;

        [MenuItem("Harvest Panic/Rendering/Configure URP for Stylized Look")]
        public static void ConfigurePipeline()
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (pipeline == null || renderer == null)
            {
                Debug.LogError("[VisualSetup] HP_URP / HP_URP_Renderer missing");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }
            var so = new SerializedObject(pipeline);
            Set(so, "m_SupportsHDR", true);
            Set(so, "m_MSAA", Msaa);
            Set(so, "m_RequireDepthTexture", true);
            Set(so, "m_RequireOpaqueTexture", false);
            Set(so, "m_MainLightShadowsSupported", true);
            Set(so, "m_MainLightShadowmapResolution", ShadowResolution);
            Set(so, "m_ShadowDistance", ShadowDistance);
            Set(so, "m_ShadowCascadeCount", ShadowCascades);
            so.FindProperty("m_Cascade4Split").vector3Value = Cascade4Split;
            Set(so, "m_CascadeBorder", 0.15f);
            Set(so, "m_ShadowDepthBias", 1.0f);
            Set(so, "m_ShadowNormalBias", 0.8f);
            Set(so, "m_SoftShadowsSupported", true);
            Set(so, "m_SoftShadowQuality", 2); // Medium
            Set(so, "m_AdditionalLightShadowsSupported", false); // lanterns: unshadowed (art bible 10: <= 4 shadowed lights)
            Set(so, "m_ColorGradingLutSize", 32);
            Set(so, "m_UseSRPBatcher", true);
            Set(so, "m_GPUResidentDrawerMode", 1);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipeline);

            ConfigureSsao(renderer);
            AssetDatabase.SaveAssets();
            Debug.Log($"[VisualSetup] URP: shadows {ShadowDistance} m x{ShadowCascades} @{ShadowResolution}, soft, MSAA {Msaa}x, HDR, depth texture; SSAO feature on {renderer.name}");
        }

        /// <summary>
        /// Whole visual overhaul in one batch call: URP settings + SSAO, material conversion, texture audit, then the
        /// World team's Muddlebury baseline (called by reflection: Rendering.Editor references no team assembly).
        /// -executeMethod HarvestPanic.Rendering.Editor.VisualPipelineSetup.ApplyAll
        /// </summary>
        [MenuItem("Harvest Panic/Rendering/Apply Whole Visual Overhaul")]
        public static void ApplyAll()
        {
            ConfigurePipeline();
            StylizedMaterialConverter.ConvertAll();
            TextureImportAudit.Run();
            var t = System.Type.GetType("HarvestPanic.World.Editor.BuildMuddlebury, HarvestPanic.World.Editor");
            var m = t?.GetMethod("ApplyVisualBaseline", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (m != null) m.Invoke(null, null); else Debug.LogWarning("[VisualSetup] BuildMuddlebury.ApplyVisualBaseline not found");
            AssetDatabase.SaveAssets();
            Debug.Log("[VisualSetup] whole visual overhaul applied");
        }

        static void Set(SerializedObject so, string prop, bool v) { var p = so.FindProperty(prop); if (p != null) p.boolValue = v; else Debug.LogWarning("[VisualSetup] missing " + prop); }
        static void Set(SerializedObject so, string prop, int v) { var p = so.FindProperty(prop); if (p != null) p.intValue = v; else Debug.LogWarning("[VisualSetup] missing " + prop); }
        static void Set(SerializedObject so, string prop, float v) { var p = so.FindProperty(prop); if (p != null) p.floatValue = v; else Debug.LogWarning("[VisualSetup] missing " + prop); }

        /// <summary>Adds (or re-tunes) the URP SSAO renderer feature on the forward renderer.</summary>
        public static ScreenSpaceAmbientOcclusion ConfigureSsao(UniversalRendererData renderer)
        {
            var ssao = renderer.rendererFeatures.OfType<ScreenSpaceAmbientOcclusion>().FirstOrDefault();
            if (ssao == null)
            {
                ssao = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();
                ssao.name = "SSAO";
                AssetDatabase.AddObjectToAsset(ssao, renderer);
                var rso = new SerializedObject(renderer);
                var features = rso.FindProperty("m_RendererFeatures");
                var map = rso.FindProperty("m_RendererFeatureMap");
                features.arraySize++;
                features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = ssao;
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(ssao, out _, out long localId);
                map.arraySize++;
                map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                rso.ApplyModifiedPropertiesWithoutUndo();
            }
            var s = new SerializedObject(ssao);
            var st = s.FindProperty("m_Settings");
            st.FindPropertyRelative("AOMethod").enumValueIndex = 0;          // BlueNoise
            st.FindPropertyRelative("Downsample").boolValue = true;           // half resolution
            st.FindPropertyRelative("AfterOpaque").boolValue = false;         // feed the lit shaders (ambient only + a little direct)
            st.FindPropertyRelative("Source").enumValueIndex = 1;             // DepthNormals
            st.FindPropertyRelative("NormalSamples").enumValueIndex = 1;      // Medium
            st.FindPropertyRelative("Intensity").floatValue = 1.1f;
            st.FindPropertyRelative("DirectLightingStrength").floatValue = 0.15f;
            st.FindPropertyRelative("Radius").floatValue = 0.4f;
            st.FindPropertyRelative("Samples").enumValueIndex = 1;            // Medium
            st.FindPropertyRelative("BlurQuality").enumValueIndex = 0;        // High (bilateral: no halos at half res)
            st.FindPropertyRelative("Falloff").floatValue = 120f;
            s.ApplyModifiedPropertiesWithoutUndo();
            ssao.SetActive(true);
            EditorUtility.SetDirty(ssao);
            EditorUtility.SetDirty(renderer);
            return ssao;
        }
    }
}
