using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace HarvestPanic.Editor
{
    /// <summary>
    /// CI / batchmode player builds. Output defaults to Builds/&lt;platform&gt;/ (override with -buildOutput &lt;path&gt;).
    /// Unity -batchmode -nographics -projectPath HarvestPanic -executeMethod HarvestPanic.Editor.BuildScript.BuildMac -quit -logFile build.log
    /// Exits non-zero on failure when run in batchmode.
    /// </summary>
    public static class BuildScript
    {
        // Release = no Development flag, no NetTest scenes. Dev = Development build + NetTest scenes + steam_appid.txt
        // next to the player so Steam works when launched outside the Steam client. Mono backend (IL2CPP not required).

        /// <summary>macOS release player: Builds/macOS/HarvestPanic.app.</summary>
        [MenuItem("Harvest Panic/Build/macOS (Release)")]
        public static void BuildMacRelease() => Build(BuildTarget.StandaloneOSX, "Builds/macOS/HarvestPanic.app", false);

        /// <summary>Alias kept for older docs/CI (release unless -development is passed).</summary>
        public static void BuildMac() => Build(BuildTarget.StandaloneOSX, "Builds/macOS/HarvestPanic.app", null);

        /// <summary>Development macOS player (includes the NetTest scenes) used by the multiplayer tests.</summary>
        [MenuItem("Harvest Panic/Build/macOS (Development)")]
        public static void BuildMacDev() => Build(BuildTarget.StandaloneOSX, "Builds/macOS-dev/HarvestPanic.app", true);

        /// <summary>Windows x64 development player: Builds/Windows-dev/HarvestPanic.exe.</summary>
        [MenuItem("Harvest Panic/Build/Windows (Development)")]
        public static void BuildWindowsDev() => Build(BuildTarget.StandaloneWindows64, "Builds/Windows-dev/HarvestPanic.exe", true);

        /// <summary>Windows x64 release player: Builds/Windows/HarvestPanic.exe.</summary>
        [MenuItem("Harvest Panic/Build/Windows (Release)")]
        public static void BuildWindowsRelease() => Build(BuildTarget.StandaloneWindows64, "Builds/Windows/HarvestPanic.exe", false);

        /// <summary>Alias kept for older docs/CI (release unless -development is passed).</summary>
        public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64, "Builds/Windows/HarvestPanic.exe", null);
        public static void BuildWindows64() => BuildWindows();

        static void Build(BuildTarget target, string defaultPath, bool? development)
        {
            NetworkingSetup.SyncBuildScenes();
            bool dev = development ?? Environment.GetCommandLineArgs().Contains("-development");
            if (EditorUserBuildSettings.activeBuildTarget != target)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, target);
            var options = new BuildPlayerOptions
            {
                // NGO identifies scenes by path hash, so peers only need the same paths, not the same indices.
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled && (dev || !s.path.StartsWith(NetworkingSetup.TestSceneDir))).Select(s => s.path).ToArray(),
                locationPathName = Arg("-buildOutput") ?? defaultPath,
                target = target,
                targetGroup = BuildTargetGroup.Standalone,
                options = dev ? BuildOptions.Development : BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(options);
            var s = report.summary;
            Debug.Log($"[BuildScript] {target}: {s.result} size={s.totalSize} errors={s.totalErrors} time={s.totalTime} -> {options.locationPathName}");
            if (s.result == BuildResult.Succeeded && dev) WriteSteamAppId(options.locationPathName, target);
            if (Application.isBatchMode && s.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }

        /// <summary>
        /// Dev builds only: steam_appid.txt beside the executable (macOS: beside the .app) so SteamAPI_Init works without
        /// launching through Steam. Release builds must NOT ship it; Steam supplies the App ID when it launches the game.
        /// </summary>
        static void WriteSteamAppId(string playerPath, BuildTarget target)
        {
            try
            {
                var cfg = AssetDatabase.LoadAssetAtPath<HarvestPanic.Networking.NetworkingConfig>("Assets/Resources/" + HarvestPanic.Networking.NetworkingConfig.ResourcePath + ".asset");
                uint id = cfg != null && cfg.steamAppId != 0 ? cfg.steamAppId : 480;
                string dir = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(playerPath));
                System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "steam_appid.txt"), id.ToString());
            }
            catch (Exception e) { Debug.LogWarning("[BuildScript] steam_appid.txt: " + e.Message); }
        }

        internal static string Arg(string name)
        {
            var a = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(a, name);
            return i >= 0 && i + 1 < a.Length ? a[i + 1] : null;
        }
    }
}
