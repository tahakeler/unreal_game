using System.Collections;
using System.IO;
using HarvestPanic.Core;
using HarvestPanic.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static HarvestPanic.Tests.PlayMode.UI.MenuTestKit;
using Direction = UnityEngine.UIElements.NavigationMoveEvent.Direction;

namespace HarvestPanic.Tests.PlayMode.UI
{
    /// <summary>
    /// QA captures of every frontend screen over the live farm backdrop, plus focus and colour-blind states
    /// (set HP_QA_DIR, run without -nographics; skipped otherwise). Output: $HP_QA_DIR/menu-ui/*.png.
    /// </summary>
    public class MenuScreenshots
    {
        FakeSessionService _fake;
        FakeVoice _voice;

        [SetUp]
        public void SetUp()
        {
            SettingsService.UseStore(new MemorySettingsStore());
            Services.Clear();
            _fake = new FakeSessionService { ConnectDelay = 0f, LocalName = "Taha" };
            Services.Register<ISessionService>(_fake);
            Services.Register<IRunSaveService>(new FakeRunSave());
            _voice = new FakeVoice();
        }

        [TearDown] public void TearDown() { Time.timeScale = 1f; SettingsService.UseStore(null); }

        static void SetColorblind(ColorblindMode m) { var s = SettingsService.Current.Copy(); s.Colorblind = m; SettingsService.Apply(s); }

        [UnityTest]
        public IEnumerator Capture_EveryMenuScreen()
        {
            if (!QaEnabled(out var dir)) { Assert.Ignore("QA capture: set HP_QA_DIR and run with a GPU"); yield break; }
            yield return FrontendFlowTests.LoadScene(SceneLoader.MainMenu);
            var menu = Object.FindFirstObjectByType<MainMenuController>();
            var root = menu.Panel.Root;
            yield return Seconds(1.2f);
            yield return Capture(Path.Combine(dir, "01_title.png"));
            Nav(root, Direction.Down); Nav(root, Direction.Down);
            yield return Seconds(0.3f);
            Assert.AreEqual("btn-settings", Focused(root)?.name);
            yield return Capture(Path.Combine(dir, "02_title_focus_settings.png"));

            menu.OpenPlay(); yield return Seconds(2.8f);
            yield return Capture(Path.Combine(dir, "03_play.png"));
            menu.OpenHost(false); yield return Seconds(2.8f);
            yield return Capture(Path.Combine(dir, "04_host.png"));
            menu.Stack.Pop(); menu.OpenHost(true); yield return Seconds(0.6f);
            yield return Capture(Path.Combine(dir, "05_host_continue.png"));
            menu.Stack.Pop(); menu.OpenJoin(); yield return Seconds(2.8f);
            yield return Capture(Path.Combine(dir, "06_join_nearby.png"));
            menu.SelectJoinTab(JoinTab.Friends); yield return Seconds(0.4f);
            yield return Capture(Path.Combine(dir, "07_join_friends.png"));
            menu.SelectJoinTab(JoinTab.Address); yield return Seconds(0.4f);
            yield return Capture(Path.Combine(dir, "08_join_address.png"));

            menu.Dialog.ShowProgress(UiText.Get("connect.join.title"), UiText.Get("connect.knocking") + "...\n192.168.1.20", null);
            yield return Seconds(0.5f);
            yield return Capture(Path.Combine(dir, "09_connecting.png"));
            menu.Dialog.ShowError(SessionErrors.Classify("Your farm is a different version (v1.2.0 p3 vs v1.1.0 p2)."), null);
            yield return Seconds(0.5f);
            yield return Capture(Path.Combine(dir, "10_error_version.png"));
            menu.Dialog.ShowError(SessionErrors.Classify("The farm is full (4/4)."), () => { });
            yield return Seconds(0.5f);
            yield return Capture(Path.Combine(dir, "11_error_full.png"));
            menu.Dialog.ShowError(SessionErrors.Classify("The host went to bed. The run was saved at the start of the day."), null);
            yield return Seconds(0.5f);
            yield return Capture(Path.Combine(dir, "12_error_hostleft.png"));
            menu.Dialog.Close();
            menu.Stack.ClearTo(MenuScreen.Title);
            yield return Seconds(0.4f);

            var settings = menu.OpenSettings(); yield return Seconds(2.6f);
            string[] tabs = { "gameplay", "controls", "audio", "video", "access" };
            for (int i = 0; i < tabs.Length; i++)
            {
                settings.SelectTab(i);
                yield return Seconds(0.3f);
                if (settings.Focusables.Count > 1) settings.Focusables[1].Focus();
                yield return Seconds(0.3f);
                yield return Capture(Path.Combine(dir, $"{13 + i:00}_settings_{tabs[i]}.png"));
            }
            SetColorblind(ColorblindMode.Deuteranopia);
            settings.SelectTab(4); yield return Seconds(0.4f);
            yield return Capture(Path.Combine(dir, "18_settings_access_cb_deutan.png"));
            SetColorblind(ColorblindMode.Off);
            settings.Close(); yield return Seconds(0.4f);

            menu.OpenCredits(); yield return Seconds(3.2f);
            yield return Capture(Path.Combine(dir, "19_credits.png"));
            menu.GoBack(); yield return Seconds(2.6f);
            menu.ConfirmQuit(); yield return Seconds(0.5f);
            yield return Capture(Path.Combine(dir, "20_quit_confirm.png"));
            menu.Dialog.Back();

            LoadingScreen.Show("Muddlebury"); yield return Seconds(0.6f);
            yield return Capture(Path.Combine(dir, "21_loading_farm.png"));
            LoadingScreen.Cancel();

            // ---- lobby
            Services.Register<IVoiceChat>(_voice);
            yield return FrontendFlowTests.LoadScene(SceneLoader.Lobby);
            ulong dave = _fake.AddGuest("Dave", true);
            _fake.AddGuest("Mabel");
            _voice.SetSpeaking(dave, true);
            var lobby = Object.FindFirstObjectByType<LobbyController>();
            yield return Seconds(2.2f);
            yield return Capture(Path.Combine(dir, "22_lobby_hud.png"));
            lobby.SetCrewOpen(true); yield return Seconds(0.6f);
            yield return Capture(Path.Combine(dir, "23_lobby_crew.png"));
            lobby.RingBell(); yield return Seconds(0.4f);
            yield return Capture(Path.Combine(dir, "24_lobby_start_anyway.png"));
            SetColorblind(ColorblindMode.Deuteranopia); yield return Seconds(0.3f);
            yield return Capture(Path.Combine(dir, "25_lobby_crew_cb_deutan.png"));
            SetColorblind(ColorblindMode.Off);
            lobby.SetCrewOpen(false);
            lobby.SetCountdown(10f);
            lobby.ToggleReady();
            foreach (var p in new System.Collections.Generic.List<PlayerInfo>(_fake.Players)) if (p.ClientId != _fake.LocalClientId) _fake.SetGuestReady(p.ClientId, true);
            lobby.RingBell(); yield return Seconds(1.2f);
            yield return Capture(Path.Combine(dir, "26_lobby_countdown.png"));
            lobby.Sequence.Cancel();

            var pause = UiRuntime.Instance.GetComponent<PauseOverlay>();
            pause.Open(); yield return Seconds(0.6f);
            yield return Capture(Path.Combine(dir, "27_pause.png"));
            pause.ShowLayer(PauseOverlay.Layer.Confirm); yield return Seconds(0.5f);
            yield return Capture(Path.Combine(dir, "28_pause_leave_confirm.png"));
            pause.ShowLayer(PauseOverlay.Layer.Main);
            pause.OpenHelp(); yield return Seconds(0.5f);
            yield return Capture(Path.Combine(dir, "29_pause_help.png"));
            pause.Close();
            Services.Unregister<IVoiceChat>(_voice);
        }
    }
}
