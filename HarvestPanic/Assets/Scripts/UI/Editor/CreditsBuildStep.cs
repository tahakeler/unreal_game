using System.IO;
using HarvestPanic.UI;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace HarvestPanic.UI.EditorTools
{
    /// <summary>
    /// Build hook for every player build (BuildScript batchmode methods and the editor Build menu alike):
    /// pre-build bakes the credits/licence text from THIRD_PARTY_LICENSES.md into a Resources TextAsset (gitignored
    /// folder, regenerated each build); post-build writes the same text as LICENSES.txt beside the player
    /// (Windows: next to HarvestPanic.exe; macOS: next to HarvestPanic.app) so the MIT and OFL notices ship with the binaries.
    /// </summary>
    public sealed class CreditsBuildStep : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        public int callbackOrder => 0;

        static string ProjectPath => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        public void OnPreprocessBuild(BuildReport report) => Bake();

        public void OnPostprocessBuild(BuildReport report)
        {
            string dir = PlayerDirectory(report.summary.outputPath);
            if (dir == null) { Debug.LogWarning("[Credits] No output folder for " + report.summary.outputPath); return; }
            string path = WriteLicensesFile(dir);
            Debug.Log("[Credits] Wrote " + path);
        }

        /// <summary>Writes the baked TextAsset; returns its text.</summary>
        [MenuItem("Harvest Panic/Release/Bake Credits Text")]
        public static string Bake()
        {
            string text = CreditsText.ComposeFromDisk(ProjectPath);
            string full = Path.Combine(ProjectPath, CreditsText.ResourceAssetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            if (!File.Exists(full) || File.ReadAllText(full) != text)
            {
                File.WriteAllText(full, text);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
            Debug.Log($"[Credits] Baked {text.Length} chars -> {CreditsText.ResourceAssetPath}");
            return text;
        }

        /// <summary>Writes LICENSES.txt into <paramref name="dir"/>; returns the file path.</summary>
        public static string WriteLicensesFile(string dir)
        {
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, CreditsText.LicensesFileName);
            File.WriteAllText(path, CreditsText.ComposeFromDisk(ProjectPath));
            return path;
        }

        /// <summary>Folder that contains the player (the .app bundle or the .exe), or the output folder itself.</summary>
        public static string PlayerDirectory(string outputPath)
        {
            if (string.IsNullOrEmpty(outputPath)) return null;
            string full = Path.GetFullPath(outputPath).TrimEnd('/', '\\');
            if (full.EndsWith(".app") || File.Exists(full) || Path.HasExtension(full)) return Path.GetDirectoryName(full);
            return full;
        }
    }
}
