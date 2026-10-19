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
        [MenuItem("Harvest Panic/Build/macOS")]
        public static void BuildMac() => Build(BuildTarget.StandaloneOSX, "Builds/macOS/HarvestPanic.app");

        /// <summary>Development macOS player (includes the NetTest scenes) used by the multiplayer tests.</summary>
        [MenuItem("Harvest Panic/Build/macOS (Development)")]
        public static void BuildMacDev() => Build(BuildTarget.StandaloneOSX, "Builds/macOS-dev/HarvestPanic.app", true);

        [MenuItem("Harvest Panic/Build/Windows")]
        public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64, "Builds/Windows/HarvestPanic.exe");

        static void Build(BuildTarget target, string defaultPath, bool forceDevelopment = false)
        {
            NetworkingSetup.SyncBuildScenes();
            bool dev = forceDevelopment || Environment.GetCommandLineArgs().Contains("-development");
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
            if (Application.isBatchMode && s.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }

        internal static string Arg(string name)
        {
            var a = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(a, name);
            return i >= 0 && i + 1 < a.Length ? a[i + 1] : null;
        }
    }
}
