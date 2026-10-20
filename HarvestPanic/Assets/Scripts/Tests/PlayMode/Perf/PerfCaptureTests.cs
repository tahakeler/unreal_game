using System;
using System.Collections;
using System.IO;
using System.Text;
using HarvestPanic.AI;
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
    /// GDD 17.7 perf capture: Muddlebury, host + 3 bot farmers (4 bodies), all critters, forced Rain + Night,
    /// rendered offscreen at 1920x1080 through the URP camera every frame, ~60 s of samples.
    /// Writes docs/qa/perf/raw/perf-&lt;label&gt;.md (label from -perfLabel, default "run").
    /// Run with: -runTests -testPlatform PlayMode -testFilter PerfCaptureTests (graphics on, NOT -nographics).
    /// </summary>
    [Category("Perf")]
    public class PerfCaptureTests
    {
        float _prevScale;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _prevScale = EconomyDevCvars.DayScale;
            EconomyDevCvars.DayScale = 1f; // keep the clock in Workday for the whole capture
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (NightDirector.Instance != null) NightDirector.Instance.ForceNight(null);
            if (WeatherDirector.Instance != null) WeatherDirector.Instance.ClearForce();
            yield return PerfHarness.Shutdown();
            EconomyDevCvars.DayScale = _prevScale;
        }

        static string Arg(string name, string def)
        {
            var a = Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
            return def;
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator Muddlebury_4Farmers_RainNight_60s()
        {
            LogAssert.ignoreFailingMessages = true; // perf capture records errors, it doesn't judge them
            int errors = 0;
            Application.LogCallback onLog = (c, s, t) => { if (t == LogType.Error || t == LogType.Exception) errors++; };
            Application.logMessageReceived += onLog;

            yield return PerfHarness.BootMuddlebury();
            var dir = EconomyDirector.Instance;
            dir.Restart();
            yield return PerfHarness.WaitFor(() => dir.Phase == DayPhase.Workday, 60f, "Workday");

            yield return PerfHarness.WaitFor(() => WeatherDirector.Instance != null && NightDirector.Instance != null, 10f, "weather + night directors");
            WeatherDirector.Instance.ForceWeather(WeatherKind.Rain);
            NightDirector.Instance.ForceNight(true);

            float seconds = float.Parse(Arg("-perfSeconds", "60"), System.Globalization.CultureInfo.InvariantCulture);
            string label = Arg("-perfLabel", "run");
            var bots = PerfHarness.SpawnBots(3, 1234);
            var pen = PerfHarness.PenDropPoint();

            // Offscreen 1080p render target for the local first-person camera.
            var cam = PlayerCharacter.Local.ViewCamera.GetComponent<Camera>();
            var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32); rt.Create();
            cam.targetTexture = rt;

            // warm-up (shader compiles, pool fills)
            float warmEnd = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < warmEnd) { TickBots(bots, pen, dir); yield return null; }

            int critters = CritterSpawnDirector.Spawned.Count;
            int netObjs = PerfHarness.SpawnedNetworkObjects;
            long frames = 0;
            using (var rec = new PerfRecorder())
            {
                // detect whether the editor player loop renders the camera on its own in batchmode
                bool manual = false;
                for (int i = 0; i < 5; i++) { TickBots(bots, pen, dir); yield return null; }
                rec.Sample(0);
                var batches = rec.Samples["Batches"];
                if (batches.Count == 0 || batches[batches.Count - 1] <= 0) manual = true;
                rec.FrameMs.Clear(); foreach (var l in rec.Samples.Values) l.Clear();

                double last = Time.realtimeSinceStartupAsDouble;
                float end = Time.realtimeSinceStartup + seconds;
                while (Time.realtimeSinceStartup < end)
                {
                    TickBots(bots, pen, dir);
                    if (manual) Render(cam, rt);
                    yield return null;
                    double now = Time.realtimeSinceStartupAsDouble;
                    rec.Sample((now - last) * 1000.0);
                    last = now;
                    frames++;
                }

                var sb = new StringBuilder();
                sb.AppendLine($"# Perf capture `{label}` -- {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC");
                sb.AppendLine();
                sb.AppendLine($"- Unity {Application.unityVersion}, {SystemInfo.graphicsDeviceType} on {SystemInfo.graphicsDeviceName}, {SystemInfo.processorType}, {SystemInfo.systemMemorySize} MB RAM");
                sb.AppendLine($"- Editor batchmode PlayMode, offscreen 1920x1080 RT, render mode: {(manual ? "manual Camera.Render per frame" : "player loop")}");
                sb.AppendLine($"- Scene Muddlebury, host + {bots.Count} bot farmers, critters spawned {critters}, weather {WeatherDirector.Instance.Current}, night {NightDirector.Instance.IsNight}, stitches {NightDirector.Instance.Stitches.Count}, foxes {NightDirector.Instance.Foxes.Count}");
                sb.AppendLine($"- Duration {seconds:0} s, frames {frames}, spawned NetworkObjects {netObjs}, bot deliveries {Sum(bots, b => b.Delivered)}, bot shouts {Sum(bots, b => b.Shouts)}, errors logged {errors}");
                sb.AppendLine($"- Quality level {QualitySettings.names[QualitySettings.GetQualityLevel()]}, shadow distance {QualitySettings.shadowDistance}, URP asset {(GraphicsSettings.currentRenderPipeline != null ? GraphicsSettings.currentRenderPipeline.name : "none")}");
                sb.AppendLine();
                sb.Append(rec.Table());
                var outDir = Path.Combine(PerfHarness.RepoRoot, "docs", "qa", "perf", "raw");
                Directory.CreateDirectory(outDir);
                File.WriteAllText(Path.Combine(outDir, $"perf-{label}.md"), sb.ToString());
                Debug.Log("[Perf] wrote perf-" + label + "\n" + sb);
                Assert.That(rec.FrameMs.Count, Is.GreaterThan(30), "too few frames sampled");
            }

            cam.targetTexture = null;
            rt.Release();
            foreach (var b in bots) b.Destroy();
            Application.logMessageReceived -= onLog;
        }

        static int Sum(System.Collections.Generic.List<BotFarmer> bots, Func<BotFarmer, int> f) { int s = 0; foreach (var b in bots) s += f(b); return s; }

        static void TickBots(System.Collections.Generic.List<BotFarmer> bots, Vector3 pen, EconomyDirector dir)
        {
            float dt = Time.deltaTime;
            foreach (var b in bots) b.Tick(dt, pen, dir.IsMarketOpen);
        }

        static void Render(Camera cam, RenderTexture rt)
        {
            var req = new RenderPipeline.StandardRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(cam, req)) RenderPipeline.SubmitRenderRequest(cam, req);
            else cam.Render();
        }
    }
}
