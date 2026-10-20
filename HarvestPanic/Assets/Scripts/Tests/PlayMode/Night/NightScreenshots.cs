using System;
using System.Collections;
using System.IO;
using HarvestPanic.AI.Night;
using HarvestPanic.Economy;
using HarvestPanic.Gameplay;
using HarvestPanic.Weather;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HarvestPanic.Tests.PlayMode.Night
{
    /// <summary>QA capture of Mr. Stitches at night in Muddlebury (set HP_QA_DIR, run without -nographics).</summary>
    public class NightScreenshots
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            var nm = NetworkManager.Singleton;
            if (nm != null) { nm.Shutdown(); yield return null; Object.Destroy(nm.gameObject); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator Capture_MrStitches_AtNight()
        {
            string dir = Environment.GetEnvironmentVariable("HP_QA_DIR");
            if (string.IsNullOrEmpty(dir) || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("QA screenshot; set HP_QA_DIR and run with a GPU");
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Muddlebury.unity", new LoadSceneParameters(LoadSceneMode.Single));
            var go = Object.Instantiate(HarvestPanic.Networking.NetworkingConfig.Load().networkManagerPrefab);
            go.GetComponent<HarvestPanic.Networking.SessionService>().HostInPlaceForEditor();
#endif
            float until = Time.realtimeSinceStartup + 25f;
            while ((PlayerCharacter.Local == null || NightDirector.Instance == null || EconomyDirector.Instance == null || WeatherDirector.Instance == null) && Time.realtimeSinceStartup < until) yield return null;
            var pc = PlayerCharacter.Local; var night = NightDirector.Instance; var eco = EconomyDirector.Instance;
            Assert.That(pc, Is.Not.Null); Assert.That(night, Is.Not.Null, "night director auto-spawned");
            WeatherDirector.Instance.ForceWeather(HarvestPanic.Core.WeatherKind.Clear);
            EconomyDevCvars.DayScale = 0.06f; // Dusk ~11 s so the shot lands deep in the night blend
            while (eco.Phase != DayPhase.Dusk) { eco.DebugSkipPhase(); yield return null; }
            float t0 = Time.time; while (night.Stitches.Count == 0 && Time.time - t0 < 5f) yield return null;
            Assert.That(night.Stitches.Count, Is.GreaterThan(0));
            var s = night.Stitches[0];
            s.DebugTarget = null;
            Vector3 eye = new Vector3(-44f, 0.3f, -36f);
            Vector3 at = NightDirector.Ground(new Vector3(-50f, 0f, -40f));
            s.GetComponent<Unity.Netcode.Components.NetworkTransform>().Teleport(at, Quaternion.LookRotation(eye - at), Vector3.one);
            pc.ServerTeleport(eye, Quaternion.LookRotation(at - eye));
            // let the dusk lighting settle deep into night
            var p = WeatherDirector.Instance.GetComponent<WeatherPresenter>();
            var clock = HarvestPanic.Core.Services.Get<HarvestPanic.Core.IDayClockService>();
            t0 = Time.time; while (clock.PhaseProgress01 < 0.9f && clock.Phase == HarvestPanic.Core.DayClockPhase.Dusk && Time.time - t0 < 20f) { s.DebugTarget = s.transform.position; yield return null; }
            EconomyDevCvars.DayScale = 1f;
            p.Snap(); yield return null;
            Directory.CreateDirectory(dir);
            var cam = pc.ViewCamera.GetComponent<Camera>();
            cam.transform.parent.LookAt(s.transform.position + Vector3.up * 1.6f);
            yield return null;
            Capture(cam, Path.Combine(dir, "night_stitches.png"));
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
