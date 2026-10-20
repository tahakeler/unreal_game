using System.Collections;
using System.Collections.Generic;
using System.IO;
using HarvestPanic.Core;
using HarvestPanic.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace HarvestPanic.Tests.PlayMode.UI
{
    public class OnboardingPlayTests
    {
        sealed class FakeClock : IDayClockService
        {
            public DayClockPhase Phase { get; set; } = DayClockPhase.Morning;
            public float PhaseProgress01 => 0f;
        }

        MemoryTipStore _tips;
        Camera _cam;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            SettingsService.UseStore(new MemorySettingsStore());
            InputBindings.UseStore(new MemorySettingsStore());
            Services.Clear(); InputModal.Clear();
            Services.Register<ISessionService>(new FakeSessionService { ConnectDelay = 0f, LocalName = "Tester" });
            _tips = new MemoryTipStore();
            OnboardingOverlay.StoreOverride = _tips;
            var scene = SceneManager.CreateScene("OnboardingFarm_" + Random.Range(0, 99999));
            SceneManager.SetActiveScene(scene);
            var camGo = new GameObject("Cam") { tag = "MainCamera" };
            _cam = camGo.AddComponent<Camera>();
            _cam.clearFlags = CameraClearFlags.SolidColor; _cam.backgroundColor = new Color(0.55f, 0.72f, 0.45f);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (UiRuntime.Instance != null) Object.Destroy(UiRuntime.Instance.gameObject);
            OnboardingOverlay.StoreOverride = null;
            Time.timeScale = 1f; InputModal.Clear();
            SettingsService.UseStore(null); InputBindings.UseStore(null);
            yield return null;
        }

        static OnboardingOverlay Overlay() => UiRuntime.Ensure().GetComponent<OnboardingOverlay>();

        IEnumerator WaitForTip(OnboardingOverlay o, float seconds)
        {
            float t = 0;
            while (o.Showing == null && t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
        }

        [UnityTest]
        public IEnumerator FirstTip_AppearsOnDayOne_NotOnSecondRun()
        {
            var o = Overlay();
            yield return null;
            Assert.IsNull(o.Showing, "no tape before the day starts");
            Services.Register<IDayClockService>(new FakeClock());
            yield return WaitForTip(o, 2f);
            Assert.NotNull(o.Showing, "move tape shows when Morning starts");
            Assert.AreEqual("move", o.Showing.Id);
            var tape = o.Root.Q("tape");
            Assert.AreEqual(DisplayStyle.Flex, tape.resolvedStyle.display);
            StringAssert.Contains("[W/A/S/D]", o.Root.Q<Label>("tape-body").text);

            // "Second run": new runtime, same profile store.
            Object.Destroy(UiRuntime.Instance.gameObject);
            yield return null;
            var o2 = Overlay();
            yield return WaitForTip(o2, 1f);
            Assert.IsNull(o2.Showing, "already seen on this profile");
        }

        [UnityTest]
        public IEnumerator TipsDisabled_InSettings_ShowNothing()
        {
            var s = SettingsService.Current.Copy(); s.TipsEnabled = false; SettingsService.Apply(s);
            var o = Overlay();
            Services.Register<IDayClockService>(new FakeClock());
            yield return WaitForTip(o, 1f);
            Assert.IsNull(o.Showing);
        }

        [UnityTest]
        public IEnumerator PauseMenu_BlocksInput_TimeAndTips_HelpAndLeaveConfirm()
        {
            var o = Overlay();
            var pause = UiRuntime.Instance.GetComponent<PauseOverlay>();
            pause.Open();
            yield return null;
            Assert.IsTrue(InputModal.IsBlocking, "player input gated through InputModal");
            Assert.AreEqual(0f, Time.timeScale, "solo pause freezes time");
            Services.Register<IDayClockService>(new FakeClock());
            yield return null; yield return null;
            Assert.IsNull(o.Showing, "no tape while paused");

            pause.OpenHelp();
            yield return null;
            Assert.AreEqual(PauseOverlay.Layer.Help, pause.Current);
            var root = UiRuntime.Instance.GetComponentsInChildren<UiPanel>(true);
            pause.Back();
            Assert.AreEqual(PauseOverlay.Layer.Main, pause.Current);
            pause.ShowLayer(PauseOverlay.Layer.Confirm);
            pause.Back();
            Assert.AreEqual(PauseOverlay.Layer.Main, pause.Current, "Stay/back returns without leaving");
            Assert.IsTrue(pause.IsOpen);
            pause.Close();
            Assert.IsFalse(InputModal.IsBlocking);
            Assert.AreEqual(1f, Time.timeScale);
            yield return WaitForTip(o, 2f);
            Assert.NotNull(o.Showing, "tape appears once the menu closes");
        }

        [UnityTest]
        public IEnumerator Captions_FromNoise_ShowWithDirection()
        {
            var o = Overlay();
            yield return null;
            o.OnNoise(new NoiseEvent { Source = NoiseSource.Critter, Tags = NoiseTag.Squeal, Loudness = 0.6f, Radius = 30f, Location = new Vector3(10, 0, 0) });
            yield return null;
            var caps = o.Root.Q("captions");
            Assert.AreEqual(1, caps.childCount);
            Assert.AreEqual("[Pig squeals] >", ((Label)caps[0]).text);
        }

        // ---------------------------------------------------------------- QA screenshots (HP_QA_DIR; needs graphics)
        [UnityTest]
        public IEnumerator Screenshots_Tip_Settings_Pause_Help()
        {
            string dir = System.Environment.GetEnvironmentVariable("HP_QA_DIR");
            if (string.IsNullOrEmpty(dir) || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) { Assert.Ignore("HP_QA_DIR not set or no graphics"); yield break; }
            Directory.CreateDirectory(dir);
            var o = Overlay();
            Services.Register<IDayClockService>(new FakeClock());
            yield return WaitForTip(o, 2f);
            o.OnNoise(new NoiseEvent { Source = NoiseSource.Impact, Tags = NoiseTag.PlankBreak, Loudness = 1f, Radius = 30f, Location = new Vector3(-8, 0, 3) });
            o.OnNoise(new NoiseEvent { Source = NoiseSource.Critter, Tags = NoiseTag.Squeal, Loudness = 0.6f, Radius = 30f, Location = new Vector3(10, 0, 0) });
            yield return Capture(Path.Combine(dir, "onboarding-tip.png"));

            var pause = UiRuntime.Instance.GetComponent<PauseOverlay>();
            pause.Open();
            yield return Capture(Path.Combine(dir, "onboarding-pause.png"));
            pause.OpenHelp();
            yield return Capture(Path.Combine(dir, "onboarding-help.png"));
            pause.OpenSettings();
            yield return null;
            var settingsRoot = FindPanelRoot("UI_Pause");
            Click(settingsRoot.Q<Button>("tab-access"));
            yield return Capture(Path.Combine(dir, "onboarding-settings-access.png"));
            Click(settingsRoot.Q<Button>("tab-controls"));
            yield return Capture(Path.Combine(dir, "onboarding-settings-controls.png"));
            pause.Close();
        }

        static void Click(Button b)
        {
            using (var e = NavigationSubmitEvent.GetPooled()) { e.target = b; b.SendEvent(e); }
        }

        static VisualElement FindPanelRoot(string uxml)
        {
            foreach (var p in UiRuntime.Instance.GetComponentsInChildren<UiPanel>(true))
                if (p.name == "UI_" + uxml) return p.Root;
            return null;
        }

        const int W = 1920, H = 1080;

        IEnumerator Capture(string path)
        {
            var sceneRt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            var uiRt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            var docs = Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None);
            var originals = new List<(UIDocument, PanelSettings)>();
            var prev = RenderTexture.active; RenderTexture.active = uiRt; GL.Clear(true, true, Color.clear); RenderTexture.active = prev;
            foreach (var d in docs)
            {
                if (d.panelSettings == null) continue;
                originals.Add((d, d.panelSettings));
                var ps = Object.Instantiate(d.panelSettings);
                ps.targetTexture = uiRt; ps.clearColor = false;
                d.panelSettings = ps;
            }
            for (int f = 0; f < 6; f++) yield return null;
            _cam.targetTexture = sceneRt; _cam.Render(); _cam.targetTexture = null;
            var a = Read(sceneRt); var b = Read(uiRt);
            var px = a.GetPixels32(); var ui = b.GetPixels32();
            for (int i = 0; i < px.Length; i++)
            {
                float al = ui[i].a / 255f;
                px[i] = new Color32((byte)Mathf.Min(255, ui[i].r + px[i].r * (1 - al)), (byte)Mathf.Min(255, ui[i].g + px[i].g * (1 - al)), (byte)Mathf.Min(255, ui[i].b + px[i].b * (1 - al)), 255);
            }
            a.SetPixels32(px);
            File.WriteAllBytes(path, a.EncodeToPNG());
            foreach (var (d, ps) in originals) { var tmp = d.panelSettings; d.panelSettings = ps; Object.Destroy(tmp); }
            Object.Destroy(a); Object.Destroy(b); sceneRt.Release(); uiRt.Release();
            yield return null;
        }

        static Texture2D Read(RenderTexture rt)
        {
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var t = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            t.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); t.Apply();
            RenderTexture.active = prev;
            return t;
        }
    }
}
