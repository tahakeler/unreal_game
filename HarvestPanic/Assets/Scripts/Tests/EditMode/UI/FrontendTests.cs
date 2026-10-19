using System.Collections.Generic;
using HarvestPanic.Core;
using HarvestPanic.UI;
using NUnit.Framework;

namespace HarvestPanic.Tests.EditMode.UI
{
    public class SettingsTests
    {
        [SetUp] public void SetUp() => SettingsService.UseStore(new MemorySettingsStore());
        [TearDown] public void TearDown() => SettingsService.UseStore(null);

        [Test]
        public void Clamp_ForcesEveryRange()
        {
            var s = new GameSettings { Fov = 500, Sensitivity = -3, MasterVolume = 7, MusicVolume = -1, SfxVolume = float.NaN, VoiceVolume = 2, ResolutionScale = 0.1f, UiScale = 9, TextSize = 9 }.Clamp();
            Assert.AreEqual(110f, s.Fov); Assert.AreEqual(GameSettings.MinSensitivity, s.Sensitivity);
            Assert.AreEqual(1f, s.MasterVolume); Assert.AreEqual(0f, s.MusicVolume); Assert.AreEqual(1f, s.SfxVolume); Assert.AreEqual(1f, s.VoiceVolume);
            Assert.AreEqual(0.5f, s.ResolutionScale); Assert.AreEqual(1.5f, s.UiScale); Assert.AreEqual(2, s.TextSize);
        }

        [Test]
        public void FovRange_Is70To110()
        {
            Assert.AreEqual(70f, new GameSettings { Fov = 10 }.Clamp().Fov);
            Assert.AreEqual(90f, new GameSettings().Fov);
        }

        [Test]
        public void Apply_PersistsAndBroadcasts()
        {
            var store = new MemorySettingsStore(); SettingsService.UseStore(store);
            GameSettings seen = null; int calls = 0;
            SettingsService.Changed += OnChanged; void OnChanged(GameSettings g) { seen = g; calls++; }
            try
            {
                var next = SettingsService.Current.Copy(); next.Fov = 100; next.InvertY = true; next.PlayerName = "  Dave  ";
                calls = 0;
                SettingsService.Apply(next);
                Assert.AreEqual(1, calls); Assert.AreEqual(100f, seen.Fov); Assert.AreEqual("Dave", seen.PlayerName);
                SettingsService.UseStore(store); // reload from the persisted json
                Assert.AreEqual(100f, SettingsService.Current.Fov); Assert.IsTrue(SettingsService.Current.InvertY);
            }
            finally { SettingsService.Changed -= OnChanged; }
        }

        [Test]
        public void Apply_ClampsBadValues_AndLiveEditsDoNotPersistUntilPersist()
        {
            var store = new MemorySettingsStore(); SettingsService.UseStore(store);
            var s = new GameSettings { Fov = 1000f };
            SettingsService.Apply(s, persist: false);
            Assert.AreEqual(110f, SettingsService.Current.Fov); Assert.IsNull(store.Json);
            SettingsService.Persist();
            Assert.IsNotNull(store.Json);
        }

        [Test]
        public void CorruptJson_FallsBackToDefaults()
        {
            SettingsService.UseStore(new MemorySettingsStore { Json = "{not json" });
            Assert.AreEqual(90f, SettingsService.Current.Fov);
        }
    }

    public class ScreenStackTests
    {
        [Test]
        public void Flow_TitlePlayHostBackSettingsClear()
        {
            var st = new ScreenStack<MenuScreen>();
            var log = new List<string>();
            st.Changed += (a, b) => log.Add((a?.ToString() ?? "") + ">" + (b?.ToString() ?? ""));
            st.ClearTo(MenuScreen.Title); st.Push(MenuScreen.Play); st.Push(MenuScreen.Host);
            Assert.AreEqual(MenuScreen.Host, st.Current); Assert.AreEqual(3, st.Depth);
            Assert.IsTrue(st.Pop()); Assert.AreEqual(MenuScreen.Play, st.Current);
            st.Replace(MenuScreen.Join); Assert.AreEqual(MenuScreen.Join, st.Current); Assert.AreEqual(2, st.Depth);
            st.ClearTo(MenuScreen.Title);
            Assert.AreEqual(1, st.Depth); Assert.IsFalse(st.Pop(), "root cannot be popped");
            CollectionAssert.AreEqual(new[] { ">Title", "Title>Play", "Play>Host", "Host>Play", "Play>Join", "Join>Title" }, log);
        }
    }

    public class StartSequenceTests
    {
        static List<PlayerInfo> Crew(params bool[] ready)
        {
            var l = new List<PlayerInfo>();
            for (int i = 0; i < ready.Length; i++) l.Add(new PlayerInfo { ClientId = (ulong)i, Name = "P" + i, IsReady = ready[i], IsHost = i == 0 });
            return l;
        }

        [Test] public void NonHost_CannotRing() => Assert.AreEqual(BellResult.NotHost, new StartSequence().RingBell(false, Crew(true)));

        [Test]
        public void AllReady_StartsCountdown_AndCompletesAfterThreeSeconds()
        {
            var s = new StartSequence();
            Assert.AreEqual(BellResult.CountdownStarted, s.RingBell(true, Crew(true, true)));
            Assert.IsFalse(s.Tick(2.9f)); Assert.IsTrue(s.Tick(0.2f)); Assert.IsFalse(s.IsCounting);
        }

        [Test]
        public void NotAllReady_NeedsSecondRingWithinWindow()
        {
            var s = new StartSequence(); var crew = Crew(true, false);
            Assert.AreEqual(BellResult.NeedsConfirm, s.RingBell(true, crew));
            Assert.AreEqual(BellResult.CountdownStarted, s.RingBell(true, crew));
        }

        [Test]
        public void ConfirmWindowExpires()
        {
            var s = new StartSequence(); var crew = Crew(true, false);
            s.RingBell(true, crew); s.Tick(3.5f);
            Assert.AreEqual(BellResult.NeedsConfirm, s.RingBell(true, crew));
        }

        [Test]
        public void LeavingPorchDuringReadyCountdown_Cancels_ButStartAnywayDoesNot()
        {
            var s = new StartSequence();
            s.RingBell(true, Crew(true, true));
            Assert.IsTrue(s.OnPlayersChanged(Crew(true, false))); Assert.IsFalse(s.IsCounting);
            var a = new StartSequence(); var crew = Crew(true, false);
            a.RingBell(true, crew); a.RingBell(true, crew);
            Assert.IsFalse(a.OnPlayersChanged(crew)); Assert.IsTrue(a.IsCounting);
        }

        [Test] public void SoloHostCanStart() => Assert.AreEqual(BellResult.NeedsConfirm, new StartSequence().RingBell(true, Crew(false)));
    }

    public class SessionAccessTests
    {
        [Test]
        public void Fake_ExposesOptionalFeatures()
        {
            var f = new FakeSessionService();
            Assert.AreEqual("LAN", SessionAccess.TransportLabel(f));
            Assert.AreEqual(3, SessionAccess.GetHosts(f).Count);
            Assert.IsTrue(SessionAccess.SupportsChat(f)); Assert.IsTrue(SessionAccess.SupportsKick(f));
            f.StartHost("x", 4); f.AddGuest("Bob");
            Assert.AreEqual(2, f.Players.Count);
            Assert.IsTrue(SessionAccess.Kick(f, 11)); Assert.AreEqual(1, f.Players.Count);
        }

        [Test] public void PlainSession_HasNoOptionalFeatures() => Assert.IsFalse(SessionAccess.SupportsChat(new object()));
    }
}
