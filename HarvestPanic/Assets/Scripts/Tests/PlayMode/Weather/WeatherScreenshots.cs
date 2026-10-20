using System;
using System.Collections;
using System.IO;
using HarvestPanic.Economy;
using HarvestPanic.Gameplay;
using HarvestPanic.Weather;
using HarvestPanic.Core;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HarvestPanic.Tests.PlayMode.Weather
{
    /// <summary>QA captures of every weather + day phase in Muddlebury (set HP_QA_DIR, run without -nographics, pass -netPort to avoid 7777).</summary>
    public class WeatherScreenshots
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            var nm = NetworkManager.Singleton;
            if (nm != null) { nm.Shutdown(); yield return null; Object.Destroy(nm.gameObject); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator Capture_EachWeather_AndPhases()
        {
            string dir = Environment.GetEnvironmentVariable("HP_QA_DIR");
            if (string.IsNullOrEmpty(dir) || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("QA screenshot; set HP_QA_DIR and run with a GPU");
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Muddlebury.unity", new LoadSceneParameters(LoadSceneMode.Single));
            var go = Object.Instantiate(HarvestPanic.Networking.NetworkingConfig.Load().networkManagerPrefab);
            go.GetComponent<HarvestPanic.Networking.SessionService>().HostInPlaceForEditor();
#endif
            float until = Time.realtimeSinceStartup + 25f;
            while ((PlayerCharacter.Local == null || WeatherDirector.Instance == null || EconomyDirector.Instance == null) && Time.realtimeSinceStartup < until) yield return null;
            var pc = PlayerCharacter.Local;
            var w = WeatherDirector.Instance;
            Assert.That(pc, Is.Not.Null); Assert.That(w, Is.Not.Null, "weather director spawned");
            yield return new WaitForSeconds(2f);
            Directory.CreateDirectory(dir);
            var cam = pc.ViewCamera.GetComponent<Camera>();
            // Look out over the farm toward the bridge.
            pc.ServerTeleport(new Vector3(30f, 0.3f, -6f), Quaternion.LookRotation(new Vector3(1f, 0f, 0.15f)));
            yield return new WaitForSeconds(1f);
            var presenter = w.GetComponent<WeatherPresenter>();
            Assert.That(presenter, Is.Not.Null);
            var eco = EconomyDirector.Instance;
            while (eco.Phase != DayPhase.Workday) { eco.DebugSkipPhase(); yield return null; }
            for (int i = 0; i < WeatherDeck.KindCount; i++)
            {
                var k = (WeatherKind)i;
                w.ForceWeather(k);
                yield return new WaitForSeconds(0.5f);
                presenter.Snap();
                yield return new WaitForSeconds(0.6f);
                Capture(cam, Path.Combine(dir, $"weather_{i}_{k}.png"));
            }
            w.ForceWeather(WeatherKind.Clear);
            eco.Restart();
            yield return null; yield return null;
            presenter.Snap(); yield return null; yield return null;
            Capture(cam, Path.Combine(dir, "phase_1_morning.png"));
            eco.DebugSkipPhase();
            yield return null; presenter.Snap(); yield return null; yield return null;
            Capture(cam, Path.Combine(dir, "phase_2_workday.png"));
            eco.DebugSkipPhase();
            yield return null; presenter.Snap(); yield return null; yield return null;
            Capture(cam, Path.Combine(dir, $"phase_3_{eco.Phase}.png".ToLowerInvariant()));
        }

        static void Capture(Camera cam, string path)
        {
            const int w = 1280, h = 720;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32); rt.Create();
            var req = new RenderPipeline.StandardRequest { destination = rt };
            for (int i = 0; i < 2; i++)
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
        }
    }
}
