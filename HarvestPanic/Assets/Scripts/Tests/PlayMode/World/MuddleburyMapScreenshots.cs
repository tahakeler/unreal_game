using System;
using System.Collections;
using System.IO;
using HarvestPanic.Core;
using HarvestPanic.Weather;
using HarvestPanic.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HarvestPanic.Tests.PlayMode.World
{
    /// <summary>
    /// Map QA captures (docs/qa/map-progress.md) from the scene's QA_Cameras in play mode: day set (overview, farmyard,
    /// fields, bridge, market, barn interior, ...) and a dusk set with WeatherLook's Dusk phase applied the way
    /// WeatherPresenter does and the windows lit. Set HP_QA_DIR and run WITHOUT -nographics; skipped otherwise.
    /// </summary>
    public class MuddleburyMapScreenshots
    {
        const string ScenePath = "Assets/Scenes/Muddlebury.unity";

        [TearDown] public void Reset() { NightWindows.ForceLit = null; _applier?.Restore(); _applier = null; }

        [UnityTest]
        public IEnumerator Capture_Map_Day_And_Dusk()
        {
            string dir = Environment.GetEnvironmentVariable("HP_QA_DIR");
            if (string.IsNullOrEmpty(dir) || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("QA screenshot; set HP_QA_DIR and run with a GPU");
#if UNITY_EDITOR
            var op = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            while (!op.isDone) yield return null;
#endif
            for (int i = 0; i < 10; i++) yield return null; // signs bind their lit material and the font atlas fills
            dir = Path.Combine(dir, "map");
            Directory.CreateDirectory(dir);
            var root = GameObject.Find("QA_Cameras");
            Assert.IsNotNull(root, "QA_Cameras");
            var cams = root.GetComponentsInChildren<Camera>(true);
            int n = 0;
            // The live WeatherPresenter (if a director spawned) re-applies its own look every LateUpdate: park it, and
            // apply the QA look (same WeatherLookApplier the presenter uses) right before each capture.
            foreach (var wp in Object.FindObjectsByType<WeatherPresenter>(FindObjectsSortMode.None)) wp.enabled = false;
            var day = WeatherLook.ForPhase(DayClockPhase.None, 0f);
            foreach (var cam in cams) { ApplyLook(day); Capture(cam, Path.Combine(dir, $"map_day_{cam.name}.png")); n++; }

            NightWindows.ForceLit = true;
            foreach (var nw in Object.FindObjectsByType<NightWindows>(FindObjectsSortMode.None)) nw.Refresh();
            var dusk = WeatherLook.ForPhase(DayClockPhase.Dusk, 0.12f);
            var night = WeatherLook.ForPhase(DayClockPhase.Dusk, 0.9f);
            foreach (var cam in cams)
                if (cam.name.StartsWith("01") || cam.name.StartsWith("02") || cam.name.StartsWith("04") || cam.name.StartsWith("08") || cam.name.StartsWith("12") || cam.name.StartsWith("13"))
                { ApplyLook(dusk); Capture(cam, Path.Combine(dir, $"map_dusk_{cam.name}.png")); n++; }
            foreach (var cam in cams)
                if (cam.name.StartsWith("04") || cam.name.StartsWith("09") || cam.name.StartsWith("13"))
                { ApplyLook(night); Capture(cam, Path.Combine(dir, $"map_night_{cam.name}.png")); n++; }
            yield return null;
            Debug.Log($"[MapQA] wrote {n} captures to {dir}");
            Assert.That(n, Is.GreaterThanOrEqualTo(20));
        }

        static WeatherLookApplier _applier;

        /// <summary>Same mapping as the live WeatherPresenter (shared WeatherLookApplier).</summary>
        static void ApplyLook(WeatherLook l)
        {
            if (_applier == null) { _applier = new WeatherLookApplier(); _applier.Capture(null); }
            _applier.Apply(l);
            Debug.Log($"[MapQA] look sun={l.SunIntensity:0.00}@{l.SunPitch:0} fog={l.FogStart:0}-{l.FogEnd:0} exp={l.PostExposure:0.00}");
        }

        static void Capture(Camera cam, string path)
        {
            const int w = 1280, h = 720;
            cam.enabled = true;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32); rt.Create();
            var req = new RenderPipeline.StandardRequest { destination = rt };
            for (int i = 0; i < 3; i++)
            {
                if (RenderPipeline.SupportsRenderRequest(cam, req)) RenderPipeline.SubmitRenderRequest(cam, req);
                else { cam.targetTexture = rt; cam.Render(); cam.targetTexture = null; }
            }
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.Destroy(tex); rt.Release(); Object.Destroy(rt);
            cam.enabled = false;
        }
    }
}
