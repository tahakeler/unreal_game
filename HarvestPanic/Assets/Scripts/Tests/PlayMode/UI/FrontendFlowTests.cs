using System;
using System.Collections;
using System.IO;
using HarvestPanic.Core;
using HarvestPanic.UI;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace HarvestPanic.Tests.PlayMode.UI
{
    public class FrontendFlowTests
    {
        FakeSessionService _fake;

        [SetUp]
        public void SetUp()
        {
            SettingsService.UseStore(new MemorySettingsStore());
            Services.Clear();
            _fake = new FakeSessionService { ConnectDelay = 0f, LocalName = "Tester" };
            Services.Register<ISessionService>(_fake);
        }

        [TearDown] public void TearDown() { Time.timeScale = 1f; SettingsService.UseStore(null); }

        public static IEnumerator LoadScene(string name)
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode($"Assets/Scenes/{name}.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null; yield return null;
        }

        [UnityTest]
        public IEnumerator Title_HostLan_Lobby_Ready_Countdown_CallsStartGame()
        {
            yield return LoadScene(SceneLoader.MainMenu);
            var menu = UnityEngine.Object.FindFirstObjectByType<MainMenuController>();
            Assert.NotNull(menu, "MainMenuController in MainMenu scene");
            Assert.AreEqual(MenuScreen.Title, menu.Stack.Current);
            Assert.AreEqual("A co-op farm. Everything has legs.", menu.Panel.Root.Q<Label>("tagline").text);
            Assert.AreEqual("LAN mode", menu.Panel.Root.Q<Label>("footer-mode").text);

            menu.OpenPlay();
            Assert.AreEqual(MenuScreen.Play, menu.Stack.Current);
            menu.Stack.Push(MenuScreen.Host);
            menu.HostGame();

            float t = 0;
            while (SceneManager.GetActiveScene().name != SceneLoader.Lobby && t < 6f) { t += Time.unscaledDeltaTime; yield return null; }
            Assert.AreEqual(SceneLoader.Lobby, SceneManager.GetActiveScene().name);
            Assert.IsTrue(_fake.IsConnected && _fake.IsHost);
            yield return null; yield return null;

            var lobby = UnityEngine.Object.FindFirstObjectByType<LobbyController>();
            Assert.NotNull(lobby);
            lobby.ToggleReady();
            Assert.IsTrue(lobby.IsLocalReady());
            lobby.RingBell();
            Assert.IsTrue(lobby.Sequence.IsCounting);
            Assert.AreEqual(0, _fake.StartGameCalls);
            t = 0;
            while (_fake.StartGameCalls == 0 && t < 5f) { t += Time.unscaledDeltaTime; yield return null; }
            Assert.AreEqual(1, _fake.StartGameCalls);
            Assert.GreaterOrEqual(t, 2.5f, "countdown ran ~3 s");
        }

        [UnityTest]
        public IEnumerator Join_Failure_ShowsErrorAndStaysOnJoin()
        {
            yield return LoadScene(SceneLoader.MainMenu);
            var menu = UnityEngine.Object.FindFirstObjectByType<MainMenuController>();
            menu.OpenPlay(); menu.Stack.Push(MenuScreen.Join);
            menu.JoinAddress("fail");
            yield return null;
            Assert.IsFalse(menu.IsConnecting);
            Assert.IsTrue(menu.Panel.Root.Q("popup-message").ClassListContains("is-active"));
            Assert.AreEqual(MenuScreen.Join, menu.Stack.Current);
        }

        [UnityTest]
        public IEnumerator Lobby_NotAllReady_NeedsConfirm_ThenStarts()
        {
            yield return LoadScene(SceneLoader.Lobby);
            _fake.AddGuest("Dave");
            yield return null;
            var lobby = UnityEngine.Object.FindFirstObjectByType<LobbyController>();
            lobby.RingBell();
            Assert.IsFalse(lobby.Sequence.IsCounting); Assert.IsTrue(lobby.Sequence.IsAwaitingConfirm);
            lobby.RingBell();
            Assert.IsTrue(lobby.Sequence.IsCounting);
        }

        [UnityTest]
        public IEnumerator Settings_Changes_AreBroadcast()
        {
            yield return LoadScene(SceneLoader.MainMenu);
            GameSettings seen = null;
            Action<GameSettings> h = g => seen = g; SettingsService.Changed += h;
            var s = SettingsService.Current.Copy(); s.Fov = 105; SettingsService.Apply(s);
            SettingsService.Changed -= h;
            Assert.AreEqual(105f, seen.Fov);
        }

        // ---------------------------------------------------------------- screenshots (run with HP_SHOTS=/abs/dir, needs a GPU: no -nographics)
        [UnityTest]
        public IEnumerator Screenshots()
        {
            string dir = Environment.GetEnvironmentVariable("HP_SHOTS");
            if (string.IsNullOrEmpty(dir)) { Assert.Ignore("Set HP_SHOTS to capture screenshots."); yield break; }
            Directory.CreateDirectory(dir);

            yield return LoadScene(SceneLoader.MainMenu);
            var menu = UnityEngine.Object.FindFirstObjectByType<MainMenuController>();
            yield return Settle();
            yield return Shot(menu.Panel, dir, "title");
            menu.OpenPlay(); yield return Settle(); yield return Shot(menu.Panel, dir, "play");
            menu.Stack.Push(MenuScreen.Join); yield return Settle(); yield return Shot(menu.Panel, dir, "join");
            menu.GoBack(); menu.Stack.Push(MenuScreen.Settings);
            var host = menu.Panel.Root.Q("settings-host"); var sp = new SettingsPanel(host);
            yield return Settle(); yield return Shot(menu.Panel, dir, "settings");

            yield return LoadScene(SceneLoader.Lobby);
            _fake.AddGuest("Dave", true); _fake.AddGuest("Mabel");
            var lobby = UnityEngine.Object.FindFirstObjectByType<LobbyController>();
            lobby.ToggleReady();
            yield return Settle(); yield return Shot(lobby.Panel, dir, "lobby");
            lobby.SetCrewOpen(true); lobby.RingBell();
            yield return Settle(); yield return Shot(lobby.Panel, dir, "lobby_crew");
        }

        static IEnumerator Settle() { for (int i = 0; i < 20; i++) yield return null; yield return new WaitForSecondsRealtime(0.5f); }

        static IEnumerator Shot(UiPanel panel, string dir, string name)
        {
            const int W = 1920, H = 1080;
            var uiRt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32); uiRt.Create();
            var camRt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32); camRt.Create();
            panel.Panel.targetTexture = uiRt; panel.Panel.clearColor = true; panel.Panel.colorClearValue = new Color(0, 0, 0, 0);
            Camera cam = null;
            foreach (var c in Camera.allCameras) if (c.isActiveAndEnabled) { cam = c; break; }
            if (cam != null) { cam.targetTexture = camRt; }
            for (int i = 0; i < 4; i++) yield return null;
            yield return new WaitForEndOfFrame();
            var prev = RenderTexture.active;
            var bg = new Texture2D(W, H, TextureFormat.RGBA32, false); var fg = new Texture2D(W, H, TextureFormat.RGBA32, false);
            RenderTexture.active = camRt; bg.ReadPixels(new Rect(0, 0, W, H), 0, 0); bg.Apply();
            RenderTexture.active = uiRt; fg.ReadPixels(new Rect(0, 0, W, H), 0, 0); fg.Apply();
            RenderTexture.active = prev;
            var bp = bg.GetPixels32(); var fp = fg.GetPixels32();
            for (int i = 0; i < bp.Length; i++)
            {
                float a = fp[i].a / 255f; // UI Toolkit output is premultiplied
                bp[i] = new Color32((byte)Mathf.Min(255, fp[i].r + bp[i].r * (1 - a)), (byte)Mathf.Min(255, fp[i].g + bp[i].g * (1 - a)), (byte)Mathf.Min(255, fp[i].b + bp[i].b * (1 - a)), 255);
            }
            bg.SetPixels32(bp);
            File.WriteAllBytes(Path.Combine(dir, name + ".png"), bg.EncodeToPNG());
            panel.Panel.targetTexture = null;
            if (cam != null) cam.targetTexture = null;
            uiRt.Release(); camRt.Release();
        }
    }
}
