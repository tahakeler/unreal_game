using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using HarvestPanic.Networking;
using HarvestPanic.UI;
using NUnit.Framework;
using UnityEngine;

namespace HarvestPanic.Tests.EditMode.UI
{
    /// <summary>String table (parse, fallback, every key used by UXML and code exists, no hard-coded UXML text).</summary>
    public class StringTableTests
    {
        static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        [TearDown] public void TearDown() => UiText.UseTable(null);

        [Test]
        public void Parse_CommentsEscapesAndDuplicates()
        {
            var d = UiText.Parse("# comment\nkey.a = Hello\n  key.b=Line one\\nLine two \nbad line\nkey.a = Again\n= novalue\n");
            Assert.AreEqual("Again", d["key.a"], "later duplicate wins");
            Assert.AreEqual("Line one\nLine two", d["key.b"]);
            Assert.AreEqual(2, d.Count);
        }

        [Test]
        public void Get_FallsBackToKey_AndFormatsInvariant()
        {
            UiText.UseTable("greet = Hi {0}, {1:0.0}x\n");
            Assert.AreEqual("Hi Dave, 1.5x", UiText.Format("greet", "Dave", 1.5f));
            Assert.AreEqual("Hi Dave, 1.5x", UiText.Format("@greet", "Dave", 1.5f), "leading @ is ignored");
            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, new Regex("Missing string 'nope.key'"));
            Assert.AreEqual("nope.key", UiText.Get("nope.key"));
        }

        [Test]
        public void EnglishTable_HasTipsAndCoreKeys()
        {
            UiText.UseTable(null);
            Assert.GreaterOrEqual(UiText.KeysWithPrefix("tip.").Count, 10, "loading screens and the title note need tips");
            foreach (var k in new[] { "menu.play", "menu.character", "menu.settings", "menu.credits", "menu.quit", "host.go", "join.go", "settings.title", "pause.resume", "lobby.crew", "loading.farm.title", "error.full.title", "error.version.title" })
                Assert.IsTrue(UiText.Has(k), k);
            Assert.AreEqual("A co-op farm. Everything has legs.", UiText.Get("title.tagline"));
        }

        /// <summary>Every "@key" in frontend UXML and every literal key passed to UiText/UiKit in UI code exists in English.</summary>
        [Test]
        public void EveryReferencedKey_Exists()
        {
            UiText.UseTable(null);
            var missing = new List<string>();
            foreach (var f in Directory.GetFiles(Path.Combine(Root, "Assets/Resources/Frontend"), "*.uxml"))
                foreach (Match m in Regex.Matches(File.ReadAllText(f), "(?:text|label)=\"@([a-z0-9_.]+)\""))
                    if (!UiText.Has(m.Groups[1].Value)) missing.Add(Path.GetFileName(f) + ": " + m.Groups[1].Value);
            var code = new Regex("(?:UiText\\.(?:Get|Format|Has)\\(\\s*\"|\"@)([a-z][a-z0-9_]*(?:\\.[a-z0-9_]+)+)\"");
            foreach (var f in Directory.GetFiles(Path.Combine(Root, "Assets/Scripts/UI"), "*.cs", SearchOption.AllDirectories))
                foreach (Match m in code.Matches(File.ReadAllText(f)))
                    if (!UiText.Has(m.Groups[1].Value)) missing.Add(Path.GetFileName(f) + ": " + m.Groups[1].Value);
            // settings rows build "settings.row.<id>" (label) and "settings.desc.<id>" (side card) from their id
            string panel = File.ReadAllText(Path.Combine(Root, "Assets/Scripts/UI/Menu/SettingsPanel.cs"));
            foreach (Match m in Regex.Matches(panel, "Add(?:Slider|Toggle|Choice|Text|ButtonRow)\\(\"([a-z]+)\""))
                foreach (var k in new[] { "settings.row." + m.Groups[1].Value, "settings.desc." + m.Groups[1].Value })
                    if (!UiText.Has(k)) missing.Add("SettingsPanel.cs: " + k);
            foreach (var id in SettingsPanel.TabIds)
                foreach (var k in new[] { "settings.tab." + id, "settings.tabinfo." + id })
                    if (!UiText.Has(k)) missing.Add("SettingsPanel tabs: " + k);
            CollectionAssert.IsEmpty(missing, "missing string-table keys:\n" + string.Join("\n", missing));
        }

        /// <summary>Localization scan (hud-menus AC 4): frontend UXML text is a key, never literal words.</summary>
        [Test]
        public void Uxml_HasNoHardcodedText()
        {
            var bad = new List<string>();
            foreach (var f in Directory.GetFiles(Path.Combine(Root, "Assets/Resources/Frontend"), "*.uxml"))
                foreach (Match m in Regex.Matches(File.ReadAllText(f), "\\b(?:text|label)=\"([^\"]*)\""))
                {
                    string v = m.Groups[1].Value;
                    if (v.Length == 0 || v.StartsWith("@") || !Regex.IsMatch(v, "[A-Za-z]{2,}")) continue;
                    bad.Add(Path.GetFileName(f) + ": " + v);
                }
            CollectionAssert.IsEmpty(bad, "hard-coded UXML text:\n" + string.Join("\n", bad));
        }
    }

    /// <summary>Menu screen-stack navigation (Back always returns to the previous screen; the title is the root).</summary>
    public class MenuNavigationTests
    {
        [Test]
        public void PlayHostJoin_BackChain()
        {
            var st = new ScreenStack<MenuScreen>();
            st.ClearTo(MenuScreen.Title);
            st.Push(MenuScreen.Play); st.Push(MenuScreen.Host);
            Assert.IsTrue(st.Pop()); Assert.AreEqual(MenuScreen.Play, st.Current);
            st.Push(MenuScreen.Join);
            Assert.IsTrue(st.Contains(MenuScreen.Play));
            Assert.IsTrue(st.Pop()); Assert.IsTrue(st.Pop());
            Assert.AreEqual(MenuScreen.Title, st.Current);
            Assert.IsFalse(st.Pop(), "Back on the title does not leave it");
            Assert.AreEqual(1, st.Depth);
        }

        [Test]
        public void SettingsAndCredits_ReturnToWhereTheyOpened()
        {
            var st = new ScreenStack<MenuScreen>();
            st.ClearTo(MenuScreen.Title); st.Push(MenuScreen.Settings);
            st.Pop(); Assert.AreEqual(MenuScreen.Title, st.Current);
            st.Push(MenuScreen.Credits); st.Pop(); Assert.AreEqual(MenuScreen.Title, st.Current);
        }
    }

    /// <summary>Video/settings persistence: new fields default, clamp and survive a save/load round trip; old saves migrate.</summary>
    public class VideoSettingsTests
    {
        [SetUp] public void SetUp() => SettingsService.UseStore(new MemorySettingsStore());
        [TearDown] public void TearDown() => SettingsService.UseStore(null);

        [Test]
        public void Defaults_AreSafe()
        {
            var s = new GameSettings();
            Assert.AreEqual(-1, s.QualityLevel); Assert.IsTrue(s.VSync); Assert.AreEqual(0, s.FrameRateCap);
            Assert.AreEqual(0, s.ResolutionWidth); Assert.AreEqual(0, s.ResolutionHeight);
        }

        [Test]
        public void Clamp_SnapsCapsAndDropsBadResolutions()
        {
            var s = new GameSettings { FrameRateCap = 59, QualityLevel = 99, ResolutionWidth = 100, ResolutionHeight = 50 }.Clamp();
            Assert.AreEqual(60, s.FrameRateCap); Assert.AreEqual(GameSettings.MaxQualityLevel, s.QualityLevel);
            Assert.AreEqual(0, s.ResolutionWidth); Assert.AreEqual(0, s.ResolutionHeight);
            Assert.AreEqual(0, new GameSettings { FrameRateCap = -5 }.Clamp().FrameRateCap);
            Assert.AreEqual(144, GameSettings.NearestCap(150));
            var ok = new GameSettings { ResolutionWidth = 2560, ResolutionHeight = 1440 }.Clamp();
            Assert.AreEqual(2560, ok.ResolutionWidth);
        }

        [Test]
        public void VideoSettings_PersistAndReload()
        {
            var store = new MemorySettingsStore(); SettingsService.UseStore(store);
            var s = SettingsService.Current.Copy();
            s.QualityLevel = 2; s.VSync = false; s.FrameRateCap = 120; s.ResolutionWidth = 1920; s.ResolutionHeight = 1080;
            s.WindowMode = WindowModeSetting.Borderless; s.Colorblind = ColorblindMode.Tritanopia; s.UiScale = 1.25f;
            SettingsService.Apply(s);
            SettingsService.UseStore(store);
            var r = SettingsService.Current;
            Assert.AreEqual(2, r.QualityLevel); Assert.IsFalse(r.VSync); Assert.AreEqual(120, r.FrameRateCap);
            Assert.AreEqual(1920, r.ResolutionWidth); Assert.AreEqual(1080, r.ResolutionHeight);
            Assert.AreEqual(WindowModeSetting.Borderless, r.WindowMode); Assert.AreEqual(ColorblindMode.Tritanopia, r.Colorblind);
            Assert.AreEqual(1.25f, r.UiScale, 1e-4f);
        }

        [Test]
        public void OldSave_WithoutVideoFields_GetsDefaults()
        {
            SettingsService.UseStore(new MemorySettingsStore { Json = "{\"Fov\":100.0,\"MasterVolume\":0.5}" });
            var r = SettingsService.Current;
            Assert.AreEqual(100f, r.Fov); Assert.AreEqual(0.5f, r.MasterVolume);
            Assert.IsTrue(r.VSync, "missing VSync keeps its default"); Assert.AreEqual(-1, r.QualityLevel);
        }

        [Test]
        public void ResolutionList_DistinctSortedAndIndexed()
        {
            var raw = new[] { new Vector2Int(1280, 720), new Vector2Int(1920, 1080), new Vector2Int(1920, 1080), new Vector2Int(640, 480), new Vector2Int(2560, 1440) };
            var list = VideoOptions.Distinct(raw);
            CollectionAssert.AreEqual(new[] { new Vector2Int(2560, 1440), new Vector2Int(1920, 1080), new Vector2Int(1280, 720) }, list);
            Assert.AreEqual(0, VideoOptions.IndexOf(list, 0, 0), "0 x 0 = native");
            Assert.AreEqual(2, VideoOptions.IndexOf(list, 1920, 1080));
            Assert.AreEqual(0, VideoOptions.IndexOf(list, 1000, 700), "unknown size falls back to native");
            Assert.AreEqual(0, VideoOptions.QualityChoice(-1, 6)); Assert.AreEqual(3, VideoOptions.QualityChoice(2, 6)); Assert.AreEqual(0, VideoOptions.QualityChoice(9, 6));
            var win = VideoOptions.NativeFor(FullScreenMode.Windowed, new Vector2Int(2560, 1600));
            Assert.Less(win.x, 2560); Assert.AreEqual(16f / 9f, win.x / (float)win.y, 0.02f);
            Assert.AreEqual(new Vector2Int(2560, 1600), VideoOptions.NativeFor(FullScreenMode.FullScreenWindow, new Vector2Int(2560, 1600)));
        }
    }

    /// <summary>Networking's real reason strings become the right dialogs (fails if a reason is reworded).</summary>
    public class SessionErrorTests
    {
        [Test]
        public void NetworkingReasons_AreClassified()
        {
            Assert.AreEqual(SessionErrorKind.FarmFull, SessionErrors.KindOf(ConnectionApproval.FullReason(4)));
            Assert.AreEqual(SessionErrorKind.VersionMismatch, SessionErrors.KindOf(ConnectionApproval.VersionReason("1.2.0", 3, "1.1.0", 2)));
            Assert.AreEqual(SessionErrorKind.Locked, SessionErrors.KindOf(ConnectionApproval.LockedReason));
            Assert.AreEqual(SessionErrorKind.Busy, SessionErrors.KindOf(ConnectionApproval.BusyReason));
            Assert.AreEqual(SessionErrorKind.UpdateNeeded, SessionErrors.KindOf(ConnectionApproval.MalformedReason));
            Assert.AreEqual(SessionErrorKind.Kicked, SessionErrors.KindOf(SessionService.KickReason));
            Assert.AreEqual(SessionErrorKind.HostLeft, SessionErrors.KindOf(SessionService.HostLeftReason));
            Assert.AreEqual(SessionErrorKind.ConnectionLost, SessionErrors.KindOf(SessionService.ConnectionLostReason));
            Assert.AreEqual(SessionErrorKind.BadAddress, SessionErrors.KindOf("\"x y\" isn't a valid address. Use an IP like 192.168.1.20 or 192.168.1.20:7777."));
            Assert.AreEqual(SessionErrorKind.SteamUnavailable, SessionErrors.KindOf("Steam isn't running. Start Steam, or join with a LAN address."));
            Assert.AreEqual(SessionErrorKind.HostFailed, SessionErrors.KindOf("Couldn't start hosting on port 7777. Is another game using it?"));
            Assert.AreEqual(SessionErrorKind.Unreachable, SessionErrors.KindOf("Couldn't reach the farm at 10.0.0.2:7777."));
            Assert.AreEqual(SessionErrorKind.Generic, SessionErrors.KindOf("Something odd"));
        }

        [Test]
        public void Dialogs_UseTableTitles_AndKeepNumbers()
        {
            UiText.UseTable(null);
            var full = SessionErrors.Classify(ConnectionApproval.FullReason(3));
            Assert.AreEqual(UiText.Get("error.full.title"), full.Title);
            StringAssert.Contains("3 of 3", full.Body);
            Assert.IsTrue(full.CanRetry);
            var ver = SessionErrors.Classify(ConnectionApproval.VersionReason("1.2.0", 3, "1.1.0", 2));
            StringAssert.Contains("v1.2.0", ver.Body); StringAssert.Contains("v1.1.0", ver.Body);
            Assert.IsFalse(ver.CanRetry, "a version mismatch can't be fixed by retrying");
            var odd = SessionErrors.Classify("Gremlins in the barn.");
            Assert.AreEqual("Gremlins in the barn.", odd.Body, "unknown reasons keep their text");
        }
    }

    public class InputGlyphTests
    {
        [Test]
        public void Glyphs_DifferPerDevice()
        {
            Assert.AreEqual("A", InputGlyphs.Glyph(UiPrompt.Select, true));
            Assert.AreEqual("B", InputGlyphs.Glyph(UiPrompt.Back, true));
            Assert.AreEqual(UiText.Get("glyph.key.esc"), InputGlyphs.Glyph(UiPrompt.Back, false));
            Assert.AreEqual("R", InputGlyphs.Glyph(UiPrompt.Ready, false));
            Assert.AreEqual("loading.farm.title", LoadingScreen.TitleKey("Muddlebury"));
            Assert.AreEqual("loading.lobby.title", LoadingScreen.TitleKey(SceneLoader.Lobby));
        }
    }
}
