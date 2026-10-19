using System.Linq;
using HarvestPanic.Core;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HarvestPanic.Editor
{
    /// <summary>
    /// Idempotent one-shot project configuration. Safe to re-run.
    /// Batchmode: Unity -batchmode -nographics -projectPath HarvestPanic -executeMethod HarvestPanic.Editor.ProjectSetup.Configure -quit -logFile log.txt
    /// </summary>
    public static class ProjectSetup
    {
        public const string SettingsDir = "Assets/Settings";
        public const string PipelinePath = SettingsDir + "/HP_URP.asset";
        public const string RendererPath = SettingsDir + "/HP_URP_Renderer.asset";
        public const string VolumePath = SettingsDir + "/HP_DefaultVolumeProfile.asset";
        public const string BootstrapPath = "Assets/Scenes/Bootstrap.unity";

        [MenuItem("Harvest Panic/Setup/Configure Project")]
        public static void Configure()
        {
            ConfigurePlayer();
            var pipeline = ConfigureUrp();
            var profile = ConfigureVolumeProfile();
            CreateBootstrapScene(profile);
            AssetDatabase.SaveAssets();
            Debug.Log($"[ProjectSetup] Done. Pipeline={pipeline.name}");
        }

        static void ConfigurePlayer()
        {
            EditorSettings.serializationMode = SerializationMode.ForceText;
            VersionControlSettings.mode = "Visible Meta Files";
            PlayerSettings.companyName = "Harvest Panic Team";
            PlayerSettings.productName = GameInfo.ProductName;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            UnityEditor.OSXStandalone.UserBuildSettings.architecture = UnityEditor.Build.OSArchitecture.ARM64;

            // Active Input Handling = Input System Package (new) only. No public API; set serialized field.
            var ps = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset").FirstOrDefault();
            if (ps != null)
            {
                var so = new SerializedObject(ps);
                var prop = so.FindProperty("activeInputHandler");
                if (prop != null) { prop.intValue = 1; so.ApplyModifiedPropertiesWithoutUndo(); }
                else Debug.LogWarning("[ProjectSetup] activeInputHandler property not found");
            }
        }

        static UniversalRenderPipelineAsset ConfigureUrp()
        {
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, RendererPath);
            }
            renderer.renderingMode = RenderingMode.ForwardPlus;
            if (renderer.postProcessData == null)
            {
                var guid = AssetDatabase.FindAssets("t:PostProcessData", new[] { "Packages/com.unity.render-pipelines.universal" }).FirstOrDefault();
                if (guid != null) renderer.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>(AssetDatabase.GUIDToAssetPath(guid));
            }
            EditorUtility.SetDirty(renderer);

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }
            pipeline.supportsHDR = true;
            pipeline.msaaSampleCount = 4;
            pipeline.shadowDistance = 60f;
            EditorUtility.SetDirty(pipeline);

            GraphicsSettings.defaultRenderPipeline = pipeline;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
            }
            QualitySettings.SetQualityLevel(current, false);
            return pipeline;
        }

        /// <summary>Stylized, readable look: neutral-ish tonemap, soft bloom, punchy colour. No motion blur, no film grain.</summary>
        static VolumeProfile ConfigureVolumeProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, VolumePath);
            }
            T Get<T>() where T : VolumeComponent
            {
                if (!profile.TryGet<T>(out var c)) { c = profile.Add<T>(true); AssetDatabase.AddObjectToAsset(c, profile); }
                return c;
            }
            var tone = Get<Tonemapping>(); tone.mode.Override(TonemappingMode.ACES);
            var bloom = Get<Bloom>(); bloom.intensity.Override(0.35f); bloom.threshold.Override(1.0f); bloom.scatter.Override(0.6f);
            var color = Get<ColorAdjustments>(); color.saturation.Override(12f); color.contrast.Override(8f); color.postExposure.Override(0.1f);
            var vig = Get<Vignette>(); vig.intensity.Override(0.18f); vig.smoothness.Override(0.4f);
            // Explicitly off: comfort + stylized clarity in first-person co-op.
            var mb = Get<MotionBlur>(); mb.active = false; mb.intensity.Override(0f);
            var grain = Get<FilmGrain>(); grain.active = false; grain.intensity.Override(0f);
            var ca = Get<ChromaticAberration>(); ca.active = false; ca.intensity.Override(0f);
            EditorUtility.SetDirty(profile);
            return profile;
        }

        static void CreateBootstrapScene(VolumeProfile profile)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            AddGlobalVolume(profile);

            // NetworkManager: the Networking prefab when it exists (NetworkingSetup.Configure creates it).
            var nmPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(NetworkingSetup.NetworkManagerPrefabPath);
            if (nmPrefab != null) PrefabUtility.InstantiatePrefab(nmPrefab, scene);
            else
            {
                var net = new GameObject("NetworkManager");
                var utp = net.AddComponent<UnityTransport>();
                utp.SetConnectionData("127.0.0.1", 7777, "0.0.0.0");
                var nm = net.AddComponent<NetworkManager>();
                nm.NetworkConfig = new NetworkConfig { NetworkTransport = utp };
            }

            EditorSceneManager.SaveScene(scene, BootstrapPath);
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != BootstrapPath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(BootstrapPath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        public static Volume AddGlobalVolume(VolumeProfile profile)
        {
            var go = new GameObject("GlobalVolume");
            var v = go.AddComponent<Volume>();
            v.isGlobal = true;
            v.sharedProfile = profile;
            return v;
        }
    }
}
