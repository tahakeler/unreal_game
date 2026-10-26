using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace HarvestPanic.Editor
{
    /// <summary>
    /// Guards the Facepunch.Steamworks plugin set (managed DLL + Valve native library), so a player can't ship without
    /// Steam and silently fall back to LAN. Two stages:
    /// <list type="bullet">
    /// <item><see cref="CheckProjectPlugins"/>: before the build. Every Steam plugin must live in <see cref="PluginRoot"/>
    /// (catches a package re-adding its own Facepunch copies), and exactly the expected set must be enabled for the target.</item>
    /// <item><see cref="CheckMacPlayer"/> / <see cref="CheckWindowsPlayer"/>: after the build, against the real player files.
    /// macOS needs libsteam_api with arm64 and every architecture of the player executable (<c>lipo -archs</c>), the Posix
    /// managed assembly, and no Linux/Windows variant.</item>
    /// </list>
    /// All checks return error strings (empty = OK) and never throw, so they are EditMode-testable.
    /// </summary>
    public static class SteamPluginCheck
    {
        /// <summary>The only folder allowed to hold Steam plugins.</summary>
        public const string PluginRoot = "Assets/Plugins/Steamworks/";
        /// <summary>Managed Facepunch.Steamworks for macOS (and the macOS editor).</summary>
        public const string MacManagedFile = "Facepunch.Steamworks.Posix.dll";
        /// <summary>Managed Facepunch.Steamworks for Windows x64 (and the Windows editor).</summary>
        public const string WinManagedFile = "Facepunch.Steamworks.Win64.dll";
        /// <summary>Valve native library for macOS (universal: arm64 + x86_64).</summary>
        public const string MacNativeFile = "libsteam_api.dylib";
        /// <summary>Valve native library for Windows x64.</summary>
        public const string WinNativeFile = "steam_api64.dll";

        public const string MacManagedAsset = PluginRoot + MacManagedFile;
        public const string WinManagedAsset = PluginRoot + WinManagedFile;
        public const string MacNativeAsset = PluginRoot + "macOS/" + MacNativeFile;
        public const string WinNativeAsset = PluginRoot + "Windows/" + WinNativeFile;

        const string PatchDoc = "Packages/com.community.netcode.transport.facepunch/HARVESTPANIC-PATCH.md";

        /// <summary>Architectures every macOS Steam library must contain, whatever the player architecture.</summary>
        public static readonly string[] RequiredMacArchs = { "arm64" };

        // ------------------------------------------------------------------ project (preflight)

        /// <summary>True for a Facepunch.Steamworks managed DLL or any Valve steam_api native library.</summary>
        public static bool IsSteamPluginFile(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            string name = Path.GetFileName(path.TrimEnd('/', '\\'));
            if (name.StartsWith("Facepunch.Steamworks.", StringComparison.OrdinalIgnoreCase)
                && name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) return true;
            string stem = Path.GetFileNameWithoutExtension(name);
            return stem.Equals("steam_api", StringComparison.OrdinalIgnoreCase)
                || stem.Equals("steam_api64", StringComparison.OrdinalIgnoreCase)
                || stem.Equals("libsteam_api", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>The exact Steam plugin assets that must be enabled for <paramref name="target"/>, or null if Steam is not
        /// shipped on that target (no check).</summary>
        public static string[] ExpectedPlugins(BuildTarget target)
        {
            switch (target)
            {
                case BuildTarget.StandaloneOSX: return new[] { MacManagedAsset, MacNativeAsset };
                case BuildTarget.StandaloneWindows64: return new[] { WinManagedAsset, WinNativeAsset };
                default: return null;
            }
        }

        /// <summary>Pure core of <see cref="CheckProjectPlugins"/>: <paramref name="allSteamPlugins"/> are every Steam plugin
        /// asset path in the project, <paramref name="enabledForTarget"/> those enabled for the target.</summary>
        public static List<string> EvaluateProjectPlugins(BuildTarget target, IEnumerable<string> allSteamPlugins, IEnumerable<string> enabledForTarget)
        {
            var errors = new List<string>();
            foreach (var p in allSteamPlugins.Where(IsSteamPluginFile).Distinct())
                if (!p.StartsWith(PluginRoot, StringComparison.Ordinal))
                    errors.Add($"Steam plugin outside {PluginRoot}: {p}. A package re-added its own Facepunch.Steamworks copy; remove it (see {PatchDoc}).");
            var expected = ExpectedPlugins(target);
            if (expected == null) return errors;
            var enabled = new HashSet<string>(enabledForTarget.Where(IsSteamPluginFile));
            foreach (var e in expected)
                if (!enabled.Contains(e)) errors.Add($"{e} is missing or not enabled for {target}.");
            foreach (var e in enabled.OrderBy(x => x, StringComparer.Ordinal))
                if (!expected.Contains(e)) errors.Add($"{e} must not be enabled for {target}.");
            return errors;
        }

        /// <summary>Preflight: plugin location and per-target import settings, read from the AssetDatabase.</summary>
        public static List<string> CheckProjectPlugins(BuildTarget target)
        {
            try
            {
                var all = PluginImporter.GetAllImporters().Select(i => i.assetPath).Where(IsSteamPluginFile).ToList();
                var enabled = PluginImporter.GetImporters(target).Select(i => i.assetPath).Where(IsSteamPluginFile).ToList();
                return EvaluateProjectPlugins(target, all, enabled);
            }
            catch (Exception e) { return new List<string> { "Steam plugin preflight failed: " + e.Message }; }
        }

        // ------------------------------------------------------------------ built player (post-process)

        /// <summary>Dispatches to the player check for <paramref name="target"/>; other targets return no errors.</summary>
        public static List<string> CheckPlayer(BuildTarget target, string playerPath)
        {
            switch (target)
            {
                case BuildTarget.StandaloneOSX: return CheckMacPlayer(playerPath, ReadArchs);
                case BuildTarget.StandaloneWindows64: return CheckWindowsPlayer(playerPath);
                default: return new List<string>();
            }
        }

        /// <summary>
        /// Checks a built macOS .app. Requires: Contents/PlugIns/libsteam_api.dylib (or .bundle) whose architectures include
        /// <see cref="RequiredMacArchs"/> and every architecture of the executables in Contents/MacOS; exactly
        /// <see cref="MacManagedFile"/> among the Facepunch managed assemblies (Mono); no Windows/Linux Steam natives.
        /// </summary>
        /// <param name="appPath">Path of the .app bundle.</param>
        /// <param name="readArchs">Returns the Mach-O architectures of a file (null/empty if unreadable).</param>
        public static List<string> CheckMacPlayer(string appPath, Func<string, IReadOnlyCollection<string>> readArchs)
        {
            var errors = new List<string>();
            string contents = Path.Combine(appPath ?? "", "Contents");
            if (!Directory.Exists(contents)) { errors.Add($"macOS player not found: {appPath}"); return errors; }

            string plugIns = Path.Combine(contents, "PlugIns");
            string native = FindMacNative(plugIns);
            if (native == null)
                errors.Add($"Contents/PlugIns has no {MacNativeFile} (or libsteam_api.bundle): Steam can't load and the game falls back to LAN.");
            else
            {
                var archs = readArchs(native) ?? Array.Empty<string>();
                var required = new SortedSet<string>(RequiredMacArchs, StringComparer.Ordinal);
                string exeDir = Path.Combine(contents, "MacOS");
                if (Directory.Exists(exeDir))
                    foreach (var exe in Directory.GetFiles(exeDir))
                        foreach (var a in readArchs(exe) ?? Array.Empty<string>()) required.Add(a);
                var missing = required.Where(a => !archs.Contains(a)).ToList();
                if (archs.Count == 0) errors.Add($"Couldn't read the architectures of {native}.");
                else if (missing.Count > 0)
                    errors.Add($"{Path.GetFileName(native)} lacks {string.Join(", ", missing)} (has: {string.Join(" ", archs)}); Steam can't load on that CPU.");
            }
            if (Directory.Exists(plugIns))
                foreach (var f in Directory.GetFileSystemEntries(plugIns).Select(Path.GetFileName))
                    if (IsSteamPluginFile(f) && f != MacNativeFile && f != "libsteam_api.bundle")
                        errors.Add($"Contents/PlugIns contains a non-macOS Steam library: {f}.");

            // Mono players carry managed assemblies; IL2CPP players have none (compiled into GameAssembly).
            string managed = Path.Combine(contents, "Resources", "Data", "Managed");
            if (Directory.Exists(managed)) errors.AddRange(CheckManagedSet(managed, MacManagedFile, "macOS"));
            return errors;
        }

        /// <summary>
        /// Checks a built Windows x64 player: &lt;Product&gt;_Data/Plugins/x86_64/steam_api64.dll is an x64 PE image, and
        /// exactly <see cref="WinManagedFile"/> is among the Facepunch managed assemblies.
        /// </summary>
        public static List<string> CheckWindowsPlayer(string exePath)
        {
            var errors = new List<string>();
            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath)) { errors.Add($"Windows player not found: {exePath}"); return errors; }
            string data = Path.Combine(Path.GetDirectoryName(exePath), Path.GetFileNameWithoutExtension(exePath) + "_Data");
            string native = new[] { Path.Combine(data, "Plugins", "x86_64", WinNativeFile), Path.Combine(data, "Plugins", WinNativeFile) }
                .FirstOrDefault(File.Exists);
            if (native == null) errors.Add($"{WinNativeFile} not found under {Path.GetFileName(data)}/Plugins: Steam can't load and the game falls back to LAN.");
            else if (ReadPeMachine(native) != 0x8664) errors.Add($"{native} is not an x64 image.");
            string managed = Path.Combine(data, "Managed");
            if (Directory.Exists(managed)) errors.AddRange(CheckManagedSet(managed, WinManagedFile, "Windows"));
            return errors;
        }

        static IEnumerable<string> CheckManagedSet(string managedDir, string expected, string platform)
        {
            var present = Directory.GetFiles(managedDir, "Facepunch.Steamworks.*.dll").Select(Path.GetFileName).ToList();
            if (!present.Contains(expected)) yield return $"{platform} player lacks the managed {expected}.";
            foreach (var p in present.Where(p => p != expected).OrderBy(p => p, StringComparer.Ordinal))
                yield return $"{platform} player includes the wrong Facepunch.Steamworks variant: {p}.";
        }

        static string FindMacNative(string plugIns)
        {
            if (!Directory.Exists(plugIns)) return null;
            string dylib = Path.Combine(plugIns, MacNativeFile);
            if (File.Exists(dylib)) return dylib;
            string bundle = Path.Combine(plugIns, "libsteam_api.bundle");
            if (File.Exists(bundle)) return bundle; // flat Mach-O named .bundle
            string inner = Path.Combine(bundle, "Contents", "MacOS", "libsteam_api");
            return File.Exists(inner) ? inner : null;
        }

        // ------------------------------------------------------------------ architectures

        /// <summary>Mach-O architectures via <c>lipo -archs</c>; falls back to <see cref="ParseMachOArchs"/> when lipo is
        /// unavailable (no Xcode command-line tools, non-macOS host) or fails.</summary>
        public static IReadOnlyCollection<string> ReadArchs(string path) => LipoArchs(path) ?? ParseMachOArchs(path);

        /// <summary>Runs <c>/usr/bin/lipo -archs</c>. Null if lipo is missing, fails or prints nothing.</summary>
        public static IReadOnlyCollection<string> LipoArchs(string path)
        {
            const string lipo = "/usr/bin/lipo";
            if (!File.Exists(lipo) || !File.Exists(path)) return null;
            try
            {
                var psi = new ProcessStartInfo(lipo, "-archs \"" + path.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"")
                {
                    UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true,
                };
                using var p = Process.Start(psi);
                string output = p.StandardOutput.ReadToEnd();
                p.StandardError.ReadToEnd();
                if (!p.WaitForExit(15000) || p.ExitCode != 0) return null;
                var archs = output.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                return archs.Length > 0 ? archs : null;
            }
            catch (Exception) { return null; }
        }

        /// <summary>Architectures from a Mach-O file header (fat or thin). Empty if the file isn't Mach-O.</summary>
        public static IReadOnlyCollection<string> ParseMachOArchs(string path)
        {
            try
            {
                using var fs = File.OpenRead(path);
                var head = new byte[4096];
                int n = fs.Read(head, 0, head.Length);
                return ParseMachOArchs(head, n);
            }
            catch (Exception) { return Array.Empty<string>(); }
        }

        /// <summary>Pure Mach-O header parse (first <paramref name="length"/> bytes of a file).</summary>
        public static IReadOnlyCollection<string> ParseMachOArchs(byte[] head, int length)
        {
            var result = new List<string>();
            if (head == null || length < 8) return result;
            uint magicBE = BE(head, 0);
            if (magicBE == 0xCAFEBABE || magicBE == 0xCAFEBABF) // FAT_MAGIC / FAT_MAGIC_64 (big-endian)
            {
                uint count = BE(head, 4);
                int stride = magicBE == 0xCAFEBABF ? 32 : 20;
                if (count == 0 || count > 32) return result;
                for (int i = 0; i < count; i++)
                {
                    int o = 8 + i * stride;
                    if (o + 8 > length) break;
                    result.Add(ArchName(unchecked((int)BE(head, o)), unchecked((int)BE(head, o + 4))));
                }
                return result;
            }
            uint magicLE = LE(head, 0);
            if (magicLE == 0xFEEDFACF || magicLE == 0xFEEDFACE) // MH_MAGIC_64 / MH_MAGIC (little-endian)
                result.Add(ArchName(unchecked((int)LE(head, 4)), unchecked((int)LE(head, 8))));
            return result;
        }

        /// <summary>lipo-style name for a Mach-O cputype/cpusubtype.</summary>
        public static string ArchName(int cpuType, int cpuSubtype)
        {
            int sub = cpuSubtype & 0x00FFFFFF;
            switch (cpuType)
            {
                case 7: return "i386";
                case 0x01000007: return sub == 8 ? "x86_64h" : "x86_64";
                case 12: return "arm";
                case 0x0100000C: return sub == 2 ? "arm64e" : "arm64";
                case 18: return "ppc";
                case 0x01000012: return "ppc64";
                default: return "cputype" + cpuType;
            }
        }

        static uint BE(byte[] b, int o) => (uint)(b[o] << 24 | b[o + 1] << 16 | b[o + 2] << 8 | b[o + 3]);
        static uint LE(byte[] b, int o) => (uint)(b[o] | b[o + 1] << 8 | b[o + 2] << 16 | b[o + 3] << 24);

        /// <summary>PE machine field (0x8664 = x64), or 0 if the file isn't a PE image.</summary>
        public static int ReadPeMachine(string path)
        {
            try
            {
                using var fs = File.OpenRead(path);
                var b = new byte[4096];
                int n = fs.Read(b, 0, b.Length);
                if (n < 0x40 || b[0] != 'M' || b[1] != 'Z') return 0;
                int pe = (int)LE(b, 0x3C);
                if (pe < 0 || pe + 6 > n || b[pe] != 'P' || b[pe + 1] != 'E' || b[pe + 2] != 0 || b[pe + 3] != 0) return 0;
                return b[pe + 4] | b[pe + 5] << 8;
            }
            catch (Exception) { return 0; }
        }

        // ------------------------------------------------------------------ reporting / CLI

        /// <summary>Logs <paramref name="errors"/> under <paramref name="stage"/>. Returns true when there are none.</summary>
        public static bool Report(string stage, IReadOnlyCollection<string> errors)
        {
            if (errors.Count == 0) { Debug.Log($"[SteamPluginCheck] {stage}: OK"); return true; }
            foreach (var e in errors) Debug.LogError($"[SteamPluginCheck] {stage} FAILED: {e}");
            return false;
        }

        /// <summary>
        /// Batchmode: verify an existing player without rebuilding.
        /// <c>-executeMethod HarvestPanic.Editor.SteamPluginCheck.VerifyPlayerCli -checkTarget mac|win -checkPlayer &lt;.app|.exe&gt;</c>.
        /// Exits 0 when the player passes, 1 otherwise.
        /// </summary>
        public static void VerifyPlayerCli()
        {
            string t = BuildScript.Arg("-checkTarget") ?? "mac";
            var target = t.StartsWith("win", StringComparison.OrdinalIgnoreCase) ? BuildTarget.StandaloneWindows64 : BuildTarget.StandaloneOSX;
            bool ok = Report("player " + BuildScript.Arg("-checkPlayer"), CheckPlayer(target, BuildScript.Arg("-checkPlayer")));
            EditorApplication.Exit(ok ? 0 : 1);
        }
    }
}
