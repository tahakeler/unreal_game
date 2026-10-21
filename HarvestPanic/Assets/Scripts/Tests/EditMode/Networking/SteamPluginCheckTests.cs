using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarvestPanic.Editor;
using NUnit.Framework;
using UnityEditor;

namespace HarvestPanic.Tests.EditMode.Networking
{
    /// <summary>
    /// Steam plugin guard: project import settings (real AssetDatabase), the shipped native binaries, Mach-O/PE header
    /// parsing, and the post-build player checks against synthetic macOS/Windows player layouts in a temp folder.
    /// </summary>
    public class SteamPluginCheckTests
    {
        // Mach-O cputype/cpusubtype values (mach/machine.h).
        const int CpuI386 = 7, CpuX86_64 = 0x01000007, CpuArm64 = 0x0100000C;
        const int SubI386All = 3, SubX86_64All = 3, SubArm64All = 0;
        const string Exe = "HARVEST PANIC";

        string _root;

        [SetUp] public void SetUp() => _root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "hp-steamcheck-" + Guid.NewGuid().ToString("N"))).FullName;
        [TearDown] public void TearDown() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }

        // ---------------------------------------------------------------- fixtures

        static byte[] Fat(params (int cpu, int sub)[] archs)
        {
            var b = new byte[8 + archs.Length * 20];
            PutBE(b, 0, 0xCAFEBABE); PutBE(b, 4, (uint)archs.Length);
            for (int i = 0; i < archs.Length; i++) { PutBE(b, 8 + i * 20, (uint)archs[i].cpu); PutBE(b, 12 + i * 20, (uint)archs[i].sub); }
            return b;
        }

        static byte[] Thin64(int cpu, int sub)
        {
            var b = new byte[32];
            PutLE(b, 0, 0xFEEDFACF); PutLE(b, 4, (uint)cpu); PutLE(b, 8, (uint)sub);
            return b;
        }

        static byte[] Pe(ushort machine)
        {
            var b = new byte[0x100];
            b[0] = (byte)'M'; b[1] = (byte)'Z'; PutLE(b, 0x3C, 0x80);
            b[0x80] = (byte)'P'; b[0x81] = (byte)'E'; b[0x84] = (byte)(machine & 0xFF); b[0x85] = (byte)(machine >> 8);
            return b;
        }

        static byte[] Universal => Fat((CpuX86_64, SubX86_64All), (CpuArm64, SubArm64All));

        static void PutBE(byte[] b, int o, uint v) { b[o] = (byte)(v >> 24); b[o + 1] = (byte)(v >> 16); b[o + 2] = (byte)(v >> 8); b[o + 3] = (byte)v; }
        static void PutLE(byte[] b, int o, uint v) { b[o] = (byte)v; b[o + 1] = (byte)(v >> 8); b[o + 2] = (byte)(v >> 16); b[o + 3] = (byte)(v >> 24); }

        string Write(string relative, byte[] bytes)
        {
            string p = Path.Combine(_root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(p));
            File.WriteAllBytes(p, bytes);
            return p;
        }

        /// <summary>A macOS .app with a universal executable and the given PlugIns / Managed contents.</summary>
        string MacApp(Dictionary<string, byte[]> plugIns, params string[] managed)
        {
            Write($"HarvestPanic.app/Contents/MacOS/{Exe}", Universal);
            Write("HarvestPanic.app/Contents/PlugIns/lib_burst_generated.bundle", Universal);
            foreach (var kv in plugIns) Write("HarvestPanic.app/Contents/PlugIns/" + kv.Key, kv.Value);
            Directory.CreateDirectory(Path.Combine(_root, "HarvestPanic.app/Contents/Resources/Data/Managed"));
            foreach (var m in managed) Write("HarvestPanic.app/Contents/Resources/Data/Managed/" + m, new byte[] { 0x4D, 0x5A });
            return Path.Combine(_root, "HarvestPanic.app");
        }

        string WinPlayer(byte[] nativeBytes, params string[] managed)
        {
            string exe = Write("HarvestPanic.exe", Pe(0x8664));
            Directory.CreateDirectory(Path.Combine(_root, "HarvestPanic_Data/Managed"));
            if (nativeBytes != null) Write("HarvestPanic_Data/Plugins/x86_64/steam_api64.dll", nativeBytes);
            foreach (var m in managed) Write("HarvestPanic_Data/Managed/" + m, new byte[] { 0x4D, 0x5A });
            return exe;
        }

        static List<string> CheckMac(string app) => SteamPluginCheck.CheckMacPlayer(app, SteamPluginCheck.ParseMachOArchs);

        static void AssertMentions(List<string> errors, string text) =>
            Assert.IsTrue(errors.Any(e => e.Contains(text)), $"expected an error mentioning '{text}', got: {string.Join(" | ", errors)}");

        // ---------------------------------------------------------------- project import settings

        [Test]
        public void ProjectPlugins_MacOS_OnlyPosixAndUniversalDylibEnabled() =>
            CollectionAssert.IsEmpty(SteamPluginCheck.CheckProjectPlugins(BuildTarget.StandaloneOSX));

        [Test]
        public void ProjectPlugins_Windows64_OnlyWin64AndSteamApi64Enabled() =>
            CollectionAssert.IsEmpty(SteamPluginCheck.CheckProjectPlugins(BuildTarget.StandaloneWindows64));

        [Test]
        public void ProjectPlugins_PackageLinuxCopyEnabledForMac_Fails()
        {
            // The original bug: the transport package's Linux managed DLL was enabled for OSXUniversal.
            const string linux = "Packages/com.community.netcode.transport.facepunch/Runtime/Facepunch/Facepunch.Steamworks.Linux.dll";
            var all = new[] { SteamPluginCheck.MacManagedAsset, SteamPluginCheck.MacNativeAsset, linux };

            var errors = SteamPluginCheck.EvaluateProjectPlugins(BuildTarget.StandaloneOSX, all, all);

            AssertMentions(errors, "outside " + SteamPluginCheck.PluginRoot);
            AssertMentions(errors, linux + " must not be enabled");
        }

        [Test]
        public void ProjectPlugins_NativeNotEnabledForMac_Fails()
        {
            var all = new[] { SteamPluginCheck.MacManagedAsset, SteamPluginCheck.MacNativeAsset };

            var errors = SteamPluginCheck.EvaluateProjectPlugins(BuildTarget.StandaloneOSX, all, new[] { SteamPluginCheck.MacManagedAsset });

            AssertMentions(errors, SteamPluginCheck.MacNativeAsset + " is missing or not enabled");
        }

        [TestCase("Assets/Plugins/Steamworks/Facepunch.Steamworks.Posix.dll", true)]
        [TestCase("Packages/x/Facepunch.Steamworks.Linux.dll", true)]
        [TestCase("Assets/Plugins/Steamworks/macOS/libsteam_api.dylib", true)]
        [TestCase("Packages/x/osx/libsteam_api.bundle/", true)]
        [TestCase("Assets/Plugins/x86_64/steam_api64.dll", true)]
        [TestCase("Assets/Plugins/steam_api.dll", true)]
        [TestCase("Assets/Plugins/Steamworks/LICENSE.txt", false)]
        [TestCase("Assets/Plugins/lib_burst_generated.bundle", false)]
        [TestCase(null, false)]
        public void IsSteamPluginFile_ClassifiesPaths(string path, bool expected) =>
            Assert.AreEqual(expected, SteamPluginCheck.IsSteamPluginFile(path));

        // ---------------------------------------------------------------- shipped binaries

        [Test]
        public void ShippedMacNative_ContainsArm64AndX86_64()
        {
            string dylib = Path.GetFullPath(SteamPluginCheck.MacNativeAsset);

            var parsed = SteamPluginCheck.ParseMachOArchs(dylib);
            var lipo = SteamPluginCheck.LipoArchs(dylib); // null when lipo isn't installed

            CollectionAssert.IsSupersetOf(parsed, new[] { "arm64", "x86_64" });
            if (lipo != null) CollectionAssert.AreEquivalent(lipo, parsed, "lipo -archs and the header parser disagree");
        }

        [Test]
        public void ShippedWindowsNative_IsX64() =>
            Assert.AreEqual(0x8664, SteamPluginCheck.ReadPeMachine(Path.GetFullPath(SteamPluginCheck.WinNativeAsset)));

        // ---------------------------------------------------------------- Mach-O parsing

        [Test]
        public void ParseMachO_FatUniversal_ReturnsBothArchs()
        {
            var b = Universal;
            CollectionAssert.AreEqual(new[] { "x86_64", "arm64" }, SteamPluginCheck.ParseMachOArchs(b, b.Length));
        }

        [Test]
        public void ParseMachO_FatLegacy_HasNoArm64()
        {
            var b = Fat((CpuI386, SubI386All), (CpuX86_64, SubX86_64All));
            CollectionAssert.AreEqual(new[] { "i386", "x86_64" }, SteamPluginCheck.ParseMachOArchs(b, b.Length));
        }

        [Test]
        public void ParseMachO_Thin_ReturnsArch()
        {
            var b = Thin64(CpuArm64, SubArm64All);
            CollectionAssert.AreEqual(new[] { "arm64" }, SteamPluginCheck.ParseMachOArchs(b, b.Length));
        }

        [Test]
        public void ParseMachO_NotMachO_ReturnsEmpty()
        {
            var b = Pe(0x8664);
            CollectionAssert.IsEmpty(SteamPluginCheck.ParseMachOArchs(b, b.Length));
        }

        // ---------------------------------------------------------------- macOS player

        [Test]
        public void MacPlayer_UniversalDylibAndPosix_Passes()
        {
            string app = MacApp(new Dictionary<string, byte[]> { ["libsteam_api.dylib"] = Universal }, SteamPluginCheck.MacManagedFile);

            CollectionAssert.IsEmpty(CheckMac(app));
        }

        [Test]
        public void MacPlayer_BaselineLayout_FailsOnMissingNativeAndLinuxVariant()
        {
            // Regression: the pre-fix macOS-dev build had only lib_burst_generated.bundle in PlugIns and shipped
            // Facepunch.Steamworks.Linux.dll as the managed assembly.
            string app = MacApp(new Dictionary<string, byte[]>(), "Facepunch.Steamworks.Linux.dll");

            var errors = CheckMac(app);

            AssertMentions(errors, "has no libsteam_api.dylib");
            AssertMentions(errors, "lacks the managed Facepunch.Steamworks.Posix.dll");
            AssertMentions(errors, "wrong Facepunch.Steamworks variant: Facepunch.Steamworks.Linux.dll");
        }

        [Test]
        public void MacPlayer_LegacyBundleWithoutArm64_Fails()
        {
            // The transport package's libsteam_api.bundle (Steamworks SDK 1.48) is a flat i386/x86_64 Mach-O.
            string app = MacApp(new Dictionary<string, byte[]> { ["libsteam_api.bundle"] = Fat((CpuI386, SubI386All), (CpuX86_64, SubX86_64All)) },
                SteamPluginCheck.MacManagedFile);

            AssertMentions(CheckMac(app), "lacks arm64");
        }

        [Test]
        public void MacPlayer_NativeMissingAnExecutableArch_Fails()
        {
            string app = MacApp(new Dictionary<string, byte[]> { ["libsteam_api.dylib"] = Thin64(CpuArm64, SubArm64All) }, SteamPluginCheck.MacManagedFile);

            AssertMentions(CheckMac(app), "lacks x86_64");
        }

        [Test]
        public void MacPlayer_WindowsNativeInPlugIns_Fails()
        {
            string app = MacApp(new Dictionary<string, byte[]> { ["libsteam_api.dylib"] = Universal, ["steam_api64.dll"] = Pe(0x8664) },
                SteamPluginCheck.MacManagedFile);

            AssertMentions(CheckMac(app), "non-macOS Steam library: steam_api64.dll");
        }

        [Test]
        public void MacPlayer_MissingApp_Fails() => AssertMentions(CheckMac(Path.Combine(_root, "Nope.app")), "not found");

        // ---------------------------------------------------------------- Windows player

        [Test]
        public void WindowsPlayer_SteamApi64AndWin64_Passes() =>
            CollectionAssert.IsEmpty(SteamPluginCheck.CheckWindowsPlayer(WinPlayer(Pe(0x8664), SteamPluginCheck.WinManagedFile)));

        [Test]
        public void WindowsPlayer_MissingNative_Fails() =>
            AssertMentions(SteamPluginCheck.CheckWindowsPlayer(WinPlayer(null, SteamPluginCheck.WinManagedFile)), "steam_api64.dll not found");

        [Test]
        public void WindowsPlayer_X86Native_Fails() =>
            AssertMentions(SteamPluginCheck.CheckWindowsPlayer(WinPlayer(Pe(0x014C), SteamPluginCheck.WinManagedFile)), "not an x64 image");

        [Test]
        public void WindowsPlayer_PosixVariant_Fails() =>
            AssertMentions(SteamPluginCheck.CheckWindowsPlayer(WinPlayer(Pe(0x8664), SteamPluginCheck.WinManagedFile, SteamPluginCheck.MacManagedFile)),
                "wrong Facepunch.Steamworks variant: Facepunch.Steamworks.Posix.dll");
    }
}
