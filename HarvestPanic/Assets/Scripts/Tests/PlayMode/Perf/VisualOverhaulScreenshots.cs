using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using HarvestPanic.AI.Night;
using HarvestPanic.Core;
using HarvestPanic.Economy;
using HarvestPanic.Gameplay;
using HarvestPanic.Weather;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HarvestPanic.Tests.PlayMode.Perf
{
    /// <summary>
    /// Visual-overhaul QA captures in the live game (host + 3 bot farmers, the real WeatherPresenter and NightPresenter):
    /// the 4-farmer crew in the farmyard by day, the same crew at night (hand lantern + lantern posts), and the
    /// first-person night view. Set HP_QA_DIR and run WITHOUT -nographics; skipped otherwise.
    /// -testFilter VisualOverhaulScreenshots
    /// </summary>
    [Category("Perf")]
    public class VisualOverhaulScreenshots
    {
        List<BotFarmer> _bots;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_bots != null) foreach (var b in _bots) b.Destroy();
            _bots = null;
            if (NightDirector.Instance != null) NightDirector.Instance.ForceNight(null);
            if (WeatherDirector.Instance != null) WeatherDirector.Instance.ClearForce();
            yield return PerfHarness.Shutdown();
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator Capture_Crew_Day_And_Night()
        {
            string dir = Environment.GetEnvironmentVariable("HP_QA_DIR");
            if (string.IsNullOrEmpty(dir) || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) { Assert.Ignore("QA screenshot; set HP_QA_DIR and run with a GPU"); yield break; }
            dir = Path.Combine(dir, "visuals");
            Directory.CreateDirectory(dir);
            LogAssert.ignoreFailingMessages = true;

            yield return PerfHarness.BootMuddlebury();
            yield return PerfHarness.WaitFor(() => WeatherDirector.Instance != null && NightDirector.Instance != null, 15f, "weather + night directors");
            var eco = EconomyDirector.Instance;
            eco.Restart();
            yield return PerfHarness.WaitFor(() => eco.Phase == DayPhase.Workday, 60f, "Workday");
            WeatherDirector.Instance.ForceWeather(WeatherKind.Clear);
            var presenter = WeatherDirector.Instance.GetComponent<WeatherPresenter>();
            Assert.That(presenter, Is.Not.Null);

            // Crew line-up in the farmyard between the barn and the pens (barn + farmhouse behind them).
            var pc = PlayerCharacter.Local;
            var view = pc.GetComponent<PlayerView>();
            view.ForceRemoteLook = true; view.ApplyViewMode();
            Vector3 centre = new Vector3(-4f, 0f, 9f);
            Vector3 face = new Vector3(1f, 0f, 0.35f).normalized;
            // Bots first (they spawn at the starts / next to the host), parked in the line, then the host walks in: a bot
            // body appearing on top of the host would knock it into ragdoll.
            _bots = PerfHarness.SpawnBots(3, 77);
            var offsets = new[] { new Vector3(0.9f, 0f, 1.4f), new Vector3(-0.7f, 0f, 3.2f), new Vector3(0.4f, 0f, 5.0f) };
            for (int i = 0; i < _bots.Count; i++)
            {
                foreach (var rb in _bots[i].Body.GetComponentsInChildren<Rigidbody>()) rb.isKinematic = true;
                _bots[i].Body.transform.SetPositionAndRotation(Ground(centre + offsets[i]), Quaternion.LookRotation(face + new Vector3(0f, 0f, 0.15f * (i - 1))));
            }
            yield return new WaitForSeconds(0.5f);
            pc.ServerTeleport(Ground(centre + new Vector3(0f, 0f, -1.8f)) + Vector3.up * 0.1f, Quaternion.LookRotation(face));
            yield return new WaitForSeconds(2.5f);
            presenter.Snap();
            yield return null;

            var cam = new GameObject("QA Crew Camera").AddComponent<Camera>();
            cam.fieldOfView = 55f;
            cam.transform.position = centre + new Vector3(7.5f, 2.1f, 4.5f);
            cam.transform.LookAt(centre + new Vector3(0f, 1.0f, 1.8f));
            yield return null;
            Capture(cam, Path.Combine(dir, "crew_day.png"));

            // Night: real NightPresenter lanterns, the presenter's own Dusk->night look at full night.
            NightDirector.Instance.ForceNight(true);
            EconomyDevCvars.DayScale = 0.05f;
            while (eco.Phase != DayPhase.Dusk) { eco.DebugSkipPhase(); yield return null; }
            var clock = Services.Get<IDayClockService>();
            float t0 = Time.realtimeSinceStartup;
            while (clock.PhaseProgress01 < 0.7f && clock.Phase == DayClockPhase.Dusk && Time.realtimeSinceStartup - t0 < 30f) yield return null;
            EconomyDevCvars.DayScale = 1f;
            presenter.Snap();
            yield return new WaitForSeconds(0.5f);
            Capture(cam, Path.Combine(dir, "crew_night.png"));
            view.ForceRemoteLook = false; view.ApplyViewMode();
            var fp = pc.ViewCamera.GetComponent<Camera>();
            fp.transform.parent.LookAt(new Vector3(-26f, 3f, 0f)); // toward the barn and its lit windows
            yield return null;
            Capture(fp, Path.Combine(dir, "night_firstperson_lantern.png"));
            Object.Destroy(cam.gameObject);
            Debug.Log($"[VisualQA] crew captures in {dir}; night look exp={presenter.CurrentLook.PostExposure:0.00} moon={presenter.CurrentLook.SunIntensity:0.00}");
        }

        static Vector3 Ground(Vector3 p)
        {
            if (Physics.Raycast(p + Vector3.up * 30f, Vector3.down, out var hit, 60f, ~0, QueryTriggerInteraction.Ignore)) return hit.point + Vector3.up * 0.05f;
            return p;
        }

        static void Capture(Camera cam, string path)
        {
            const int w = 1280, h = 720;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 }; rt.Create();
            var prevTarget = cam.targetTexture;
            var req = new RenderPipeline.StandardRequest { destination = rt };
            for (int i = 0; i < 3; i++)
            {
                if (RenderPipeline.SupportsRenderRequest(cam, req)) RenderPipeline.SubmitRenderRequest(cam, req);
                else { cam.targetTexture = rt; cam.Render(); cam.targetTexture = prevTarget; }
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
