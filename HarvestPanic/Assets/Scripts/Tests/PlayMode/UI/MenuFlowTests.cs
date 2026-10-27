using System.Collections;
using System.Collections.Generic;
using HarvestPanic.Core;
using HarvestPanic.Networking;
using HarvestPanic.UI;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static HarvestPanic.Tests.PlayMode.UI.MenuTestKit;
using Direction = UnityEngine.UIElements.NavigationMoveEvent.Direction;

namespace HarvestPanic.Tests.PlayMode.UI
{
    /// <summary>Menu flows on the dev session (FakeSessionService): join error dialogs, pause -> settings -> back, focus order, lobby crew.</summary>
    public class MenuFlowTests
    {
        FakeSessionService _fake;
        FakeAudio _audio;

        [SetUp]
        public void SetUp()
        {
            SettingsService.UseStore(new MemorySettingsStore());
            Services.Clear();
            _fake = new FakeSessionService { ConnectDelay = 0f, LocalName = "Tester" };
            Services.Register<ISessionService>(_fake);
            _audio = new FakeAudio();
            Services.Register<IAudioService>(_audio);
            Services.Register<IRunSaveService>(new FakeRunSave());
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            if (UiRuntime.Instance != null) UiRuntime.Instance.GetComponent<PauseOverlay>()?.Close();
            LoadingScreen.Cancel();
            SettingsService.UseStore(null);
        }

        static MainMenuController Menu() => Object.FindFirstObjectByType<MainMenuController>();

        [UnityTest]
        public IEnumerator JoinFlow_ErrorDialogs_ClassifyRetryAndCancel()
        {
            yield return FrontendFlowTests.LoadScene(SceneLoader.MainMenu);
            var menu = Menu();
            menu.OpenPlay(); menu.OpenJoin();
            yield return null;

            menu.JoinAddress("   ");
            Assert.IsTrue(menu.Dialog.IsOpen);
            Assert.AreEqual(UiText.Get("join.empty.title"), menu.Dialog.TitleText);
            menu.GoBack();
            Assert.IsFalse(menu.Dialog.IsOpen, "Esc / B closes the dialog");

            menu.JoinAddress("fail");
            yield return null;
            Assert.AreEqual(SessionErrorKind.Unreachable, menu.Dialog.ErrorKind);
            Assert.AreEqual(UiText.Get("common.retry"), menu.Dialog.Primary.text, "unreachable farms offer Try again");
            Assert.Contains(UiSounds.ErrorEvent, _audio.Played, "error sound through IAudioService");
            Submit(menu.Dialog.Primary);
            yield return null;
            Assert.AreEqual("fail", _fake.LastJoinAddress, "retry repeats the same join");
            Assert.IsTrue(menu.Dialog.IsOpen && menu.Dialog.ErrorKind == SessionErrorKind.Unreachable);
            menu.Dialog.Close();

            _fake.ConnectDelay = 5f;
            menu.JoinAddress("192.168.1.20");
            Assert.IsTrue(menu.IsConnecting);
            Assert.AreEqual(DialogTone.Busy, menu.Dialog.Tone);
            _fake.RaiseError("The farm is full (4/4).");
            Assert.IsFalse(menu.IsConnecting);
            Assert.AreEqual(SessionErrorKind.FarmFull, menu.Dialog.ErrorKind);
            StringAssert.Contains("4 of 4", menu.Dialog.BodyText);
            menu.Dialog.Close();

            menu.JoinAddress("192.168.1.20");
            _fake.RaiseError("Your farm is a different version (v0.9 p1 vs v1.0 p1).");
            Assert.AreEqual(SessionErrorKind.VersionMismatch, menu.Dialog.ErrorKind);
            Assert.IsFalse(menu.Dialog.Secondary.style.display == DisplayStyle.Flex, "no retry for a version mismatch");
            menu.Dialog.Close();

            menu.JoinAddress("192.168.1.20");
            Assert.IsTrue(menu.IsConnecting);
            menu.GoBack();
            Assert.IsFalse(menu.IsConnecting, "Esc cancels connecting");
            Assert.IsFalse(menu.Dialog.IsOpen);
            Assert.AreEqual(MenuScreen.Join, menu.Stack.Current);

            // a full farm in the Nearby list is refused before connecting
            menu.SelectJoinTab(JoinTab.Nearby);
            yield return null;
            var fullRow = menu.Panel.Root.Q<Button>("lan-row-2");
            Assert.NotNull(fullRow, "three LAN rows from the dev session");
            Assert.IsTrue(fullRow.ClassListContains("lan-row--blocked"));
            Submit(fullRow);
            Assert.IsFalse(menu.IsConnecting);
            Assert.AreEqual(SessionErrorKind.FarmFull, menu.Dialog.ErrorKind);
        }

        [UnityTest]
        public IEnumerator Host_PrivacyCrewAndContinue_FakeSession()
        {
            yield return FrontendFlowTests.LoadScene(SceneLoader.MainMenu);
            var menu = Menu();
            menu.OpenPlay();
            Assert.IsTrue(menu.RefreshContinue(), "save -> Continue card enabled");
            menu.OpenHost(true);
            Assert.IsTrue(menu.HostContinue);
            Assert.AreEqual(UiText.Get("host.go.continue"), menu.Panel.Root.Q<Button>("btn-host-go").text);
            menu.SetPrivacy(HostPrivacy.Public);
            Assert.AreEqual(HostPrivacy.Lan, menu.Privacy, "no Steam in tests: privacy falls back to LAN");
            menu.SetMaxPlayers(2);
            Submit(menu.Panel.Root.Q<Button>("btn-host-go"));
            Assert.AreEqual(2, _fake.LastMaxPlayers);
            Assert.IsTrue(Services.Get<IRunSaveService>().ContinueRequested, "Continue Farm asks Economy to load the save");
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == SceneLoader.Lobby, 6f);
            Assert.AreEqual(SceneLoader.Lobby, SceneManager.GetActiveScene().name);
            Assert.Contains(SceneLoader.Lobby, LoadingScreen.Instance.History, "loading card covered the lobby load");
        }

        [UnityTest]
        public IEnumerator Pause_Settings_Back_ReturnsAndPersists()
        {
            yield return FrontendFlowTests.LoadScene(SceneLoader.Lobby);
            var pause = UiRuntime.Instance.GetComponent<PauseOverlay>();
            pause.Open();
            yield return null;
            Assert.IsTrue(pause.IsOpen);
            var settings = pause.OpenSettings();
            Assert.AreEqual(PauseOverlay.Layer.Settings, pause.Current);
            settings.SelectTab(3);
            Assert.AreEqual(3, settings.CurrentTab);
            var s = SettingsService.Current.Copy(); s.VSync = false; SettingsService.Apply(s, persist: false);
            pause.Back();
            Assert.AreEqual(PauseOverlay.Layer.Main, pause.Current, "Back from settings returns to the pause menu");
            Assert.IsTrue(pause.IsOpen);
            pause.Back();
            Assert.IsFalse(pause.IsOpen, "Back from the pause menu resumes");
            Assert.AreEqual(1f, Time.timeScale);
        }

        [UnityTest]
        public IEnumerator Settings_Persist_OnClose()
        {
            var store = new MemorySettingsStore();
            SettingsService.UseStore(store);
            yield return FrontendFlowTests.LoadScene(SceneLoader.MainMenu);
            var menu = Menu();
            var panel = menu.OpenSettings();
            panel.SelectTab(3);
            var s = SettingsService.Current.Copy(); s.FrameRateCap = 60; s.VSync = false; SettingsService.Apply(s, persist: false);
            Assert.IsNull(store.Json, "live edits are not written until the panel closes");
            menu.GoBack();
            Assert.AreEqual(MenuScreen.Title, menu.Stack.Current);
            Assert.IsNotNull(store.Json);
            SettingsService.UseStore(store);
            Assert.AreEqual(60, SettingsService.Current.FrameRateCap);
            Assert.IsFalse(SettingsService.Current.VSync);
        }

        [UnityTest]
        public IEnumerator GamepadFocusOrder_TitleHostDialogSettings()
        {
            yield return FrontendFlowTests.LoadScene(SceneLoader.MainMenu);
            var menu = Menu();
            var root = menu.Panel.Root;
            yield return Seconds(0.3f);
            Assert.AreEqual("btn-play", Focused(root)?.name, "title opens focused on Play");
            var order = new List<string>();
            for (int i = 0; i < 5; i++) { Nav(root, Direction.Down); order.Add(Focused(root)?.name); }
            CollectionAssert.AreEqual(new[] { "btn-character", "btn-settings", "btn-credits", "btn-quit", "btn-play" }, order, "Down walks the title menu and wraps");
            Nav(root, Direction.Up);
            Assert.AreEqual("btn-quit", Focused(root)?.name);
            Nav(root, Direction.Right);
            Assert.AreEqual("profile-chip", Focused(root)?.name);
            Nav(root, Direction.Left);
            Assert.AreEqual("btn-play", Focused(root)?.name);
            yield return Seconds(0.2f);
            Assert.Contains(UiSounds.HoverEvent, _audio.Played, "focus moves tick through IAudioService");

            Submit(root.Q<Button>("btn-play"));
            Assert.AreEqual(MenuScreen.Play, menu.Stack.Current, "Submit on Play opens the play cards");
            Assert.Contains(UiSounds.ConfirmEvent, _audio.Played);
            menu.OpenHost(false);
            yield return Seconds(0.2f);
            Assert.AreEqual("btn-host-go", Focused(root)?.name, "host form focuses Open the Gate");
            Nav(root, Direction.Up); Assert.AreEqual("privacy-lan", Focused(root)?.name);
            Nav(root, Direction.Up); Assert.AreEqual("crew-4", Focused(root)?.name);
            Nav(root, Direction.Left); Assert.AreEqual("crew-3", Focused(root)?.name);
            Nav(root, Direction.Down); Assert.AreEqual("privacy-lan", Focused(root)?.name);
            Nav(root, Direction.Down); Assert.AreEqual("btn-host-go", Focused(root)?.name);
            Nav(root, Direction.Left); Assert.AreEqual("btn-host-back", Focused(root)?.name);

            menu.Dialog.Show("t", "b", DialogTone.Info, "!", "@common.ok", null, "@common.cancel", null);
            yield return Seconds(0.2f);
            Assert.AreEqual("btn-dialog-primary", Focused(root)?.name, "dialogs take focus");
            Nav(root, Direction.Up); Nav(root, Direction.Left);
            Assert.AreEqual("btn-dialog-primary", Focused(root)?.name, "focus is trapped inside the dialog");
            Nav(root, Direction.Right); Assert.AreEqual("btn-dialog-secondary", Focused(root)?.name);
            menu.GoBack();
            Assert.IsFalse(menu.Dialog.IsOpen);
            Assert.Contains(UiSounds.BackEvent, _audio.Played);

            menu.Stack.ClearTo(MenuScreen.Title);
            var settings = menu.OpenSettings();
            yield return Seconds(0.2f);
            Assert.AreEqual("tab-gameplay", Focused(root)?.name);
            Nav(root, Direction.Right); Assert.AreEqual("tab-controls", Focused(root)?.name);
            Nav(root, Direction.Left);
            Nav(root, Direction.Down);
            Assert.AreSame(settings.Focusables[0], FocusOwner(root, settings.Focusables[0]), "Down from the tabs enters the first row");
            Nav(root, Direction.Down); Assert.AreSame(settings.Focusables[1], Focused(root));
            Nav(root, Direction.Up); Nav(root, Direction.Up);
            Assert.AreEqual("tab-gameplay", Focused(root)?.name, "Up from the first row returns to the tabs");
        }

        /// <summary>The focusable that owns focus (text fields delegate focus to an inner element).</summary>
        static VisualElement FocusOwner(VisualElement root, VisualElement expected)
        {
            var f = Focused(root);
            return f != null && (f == expected || expected.Contains(f)) ? expected : f;
        }

        [UnityTest]
        public IEnumerator Lobby_CrewLedger_ReadyMuteRulesStart()
        {
            var voice = new FakeVoice();
            Services.Register<IVoiceChat>(voice);
            yield return FrontendFlowTests.LoadScene(SceneLoader.Lobby);
            ulong dave = _fake.AddGuest("Dave", true);
            voice.SetSpeaking(dave, true);
            var lobby = Object.FindFirstObjectByType<LobbyController>();
            yield return null;
            var root = lobby.Panel.Root;
            Assert.AreEqual(GameInfo.MaxPlayers, root.Q("crew-strip").childCount, "strip shows every seat");
            Assert.AreEqual(GameInfo.MaxPlayers, root.Q("crew-list").childCount);
            Assert.NotNull(root.Q("crew-list").Q(className: "tag--talking"), "talking indicator");
            var mute = root.Q<Button>("mute-" + dave);
            Assert.NotNull(mute, "mute per remote player");
            lobby.SetCrewOpen(true);
            Submit(mute);
            Assert.IsTrue(voice.IsMuted(dave));
            lobby.SetCountdown(5f);
            Assert.AreEqual(5f, lobby.Sequence.CountdownSeconds);
            lobby.ToggleReady();
            Assert.IsTrue(lobby.IsLocalReady());
            Submit(root.Q<Button>("btn-start-day"));
            Assert.IsTrue(lobby.Sequence.IsCounting, "host's Ring the bell starts the countdown when all are ready");
            Assert.AreEqual(5f, lobby.Sequence.SecondsLeft, 0.1f);
            lobby.Sequence.Cancel();
            lobby.SetCrewOpen(false);
            Services.Unregister<IVoiceChat>(voice);
        }
    }

    /// <summary>
    /// The real thing on LAN: title -> Host -> NGO Lobby -> ready + dinner bell -> countdown -> NGO Muddlebury,
    /// with the loading card covering both loads (real SessionService on a random port; no Steam in batchmode).
    /// </summary>
    public class MenuRealSessionTests
    {
        ushort _savedPort;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            LogAssert.ignoreFailingMessages = true; // farm-scene systems may log warnings with one player
            SettingsService.UseStore(new MemorySettingsStore());
            foreach (var nm in Object.FindObjectsByType<NetworkManager>(FindObjectsSortMode.None)) { if (nm.IsListening) nm.Shutdown(); Object.Destroy(nm.gameObject); }
            yield return null;
            Services.Clear();
            var cfg = NetworkingConfig.Load();
            _savedPort = cfg.lanPort;
            cfg.lanPort = (ushort)Random.Range(20000, 40000);
            yield return SceneManager.LoadSceneAsync(GameInfo.BootstrapScene, LoadSceneMode.Single);
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == cfg.menuScene, 10f);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var nm in Object.FindObjectsByType<NetworkManager>(FindObjectsSortMode.None)) if (nm != null && nm.IsListening) nm.Shutdown();
            yield return null; yield return null;
            foreach (var nm in Object.FindObjectsByType<NetworkManager>(FindObjectsSortMode.None)) if (nm != null) Object.Destroy(nm.gameObject);
            yield return null;
            // Leave an empty scene behind so later tests don't inherit the farm (cameras, players, directors).
            var empty = SceneManager.CreateScene("MenuRealSessionTests_Empty");
            SceneManager.SetActiveScene(empty);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s != empty && s.isLoaded) yield return SceneManager.UnloadSceneAsync(s);
            }
            NetworkingConfig.Load().lanPort = _savedPort;
            Services.Clear();
            SettingsService.UseStore(null);
            LogAssert.ignoreFailingMessages = false;
        }

        [UnityTest]
        public IEnumerator Title_Host_Lobby_Start_ReachesMuddlebury()
        {
            Assert.AreEqual("MainMenu", SceneManager.GetActiveScene().name);
            var menu = Object.FindFirstObjectByType<MainMenuController>();
            Assert.NotNull(menu);
            Assert.IsTrue(Services.TryGet(out ISessionService session) && session is SessionService, "real session service");
            yield return Seconds(0.3f);
            menu.OpenPlay();
            menu.OpenHost(false);
            Assert.AreEqual(HostPrivacy.Lan, menu.Privacy);
            menu.SetMaxPlayers(2);
            int before = LoadingScreen.Instance.History.Count;
            menu.HostGame();
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == "Lobby", 20f);
            Assert.AreEqual("Lobby", SceneManager.GetActiveScene().name, "hosting loads the Lobby through NGO");
            Assert.IsTrue(session.IsHost);
            Assert.Contains("Lobby", LoadingScreen.Instance.History.GetRange(before, LoadingScreen.Instance.History.Count - before), "loading card covered the lobby load");

            LobbyController lobby = null;
            yield return WaitUntil(() => (lobby = Object.FindFirstObjectByType<LobbyController>()) != null, 5f);
            Assert.NotNull(lobby);
            yield return Seconds(0.5f);
            lobby.ToggleReady();
            yield return WaitUntil(() => lobby.IsLocalReady(), 3f);
            Assert.IsTrue(lobby.IsLocalReady());
            lobby.RingBell();
            Assert.IsTrue(lobby.Sequence.IsCounting, "dinner bell starts the countdown");
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == "Muddlebury", 60f);
            Assert.AreEqual("Muddlebury", SceneManager.GetActiveScene().name, "the countdown starts the day for everyone");
            Assert.Contains("Muddlebury", LoadingScreen.Instance.History);
            yield return WaitUntil(() => !LoadingScreen.Instance.IsVisible, 15f);
            Assert.IsFalse(LoadingScreen.Instance.IsVisible, "loading card hides once the farm is up");
        }
    }
}
