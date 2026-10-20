using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using HarvestPanic.AI.Night;
using HarvestPanic.Core;
using HarvestPanic.Economy;
using HarvestPanic.Weather;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HarvestPanic.Tests.PlayMode.Perf
{
    /// <summary>
    /// Same scenario as <see cref="PerfCaptureTests"/>, but records ~300 frames with the editor profiler and dumps the
    /// top markers by self time and by GC alloc (merged by name) to docs/qa/perf/raw/hotspots-&lt;label&gt;.md.
    /// Editor-only (uses ProfilerDriver / HierarchyFrameDataView).
    /// </summary>
    [Category("Perf")]
    public class PerfHotspotTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (NightDirector.Instance != null) NightDirector.Instance.ForceNight(null);
            if (WeatherDirector.Instance != null) WeatherDirector.Instance.ClearForce();
            yield return PerfHarness.Shutdown();
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator Muddlebury_Hotspots()
        {
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1;
            yield return PerfHarness.BootMuddlebury();
            var dir = EconomyDirector.Instance;
            dir.Restart();
            yield return PerfHarness.WaitFor(() => dir.Phase == DayPhase.Workday, 60f, "Workday");
            WeatherDirector.Instance.ForceWeather(WeatherKind.Rain);
            NightDirector.Instance.ForceNight(true);
            var bots = PerfHarness.SpawnBots(3, 1234);
            var pen = PerfHarness.PenDropPoint();
            var cam = HarvestPanic.Gameplay.PlayerCharacter.Local.ViewCamera.GetComponent<Camera>();
            var rt = new RenderTexture(1920, 1080, 24); rt.Create(); cam.targetTexture = rt;
            float warm = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < warm) { Tick(bots, pen, dir); yield return null; }

            UnityEditorInternal.ProfilerDriver.ClearAllFrames();
            UnityEditorInternal.ProfilerDriver.profileEditor = false;
            UnityEngine.Profiling.Profiler.enabled = true;
            UnityEditorInternal.ProfilerDriver.enabled = true;
            for (int i = 0; i < 320; i++) { Tick(bots, pen, dir); yield return null; }
            UnityEditorInternal.ProfilerDriver.enabled = false;

            int first = UnityEditorInternal.ProfilerDriver.firstFrameIndex, last = UnityEditorInternal.ProfilerDriver.lastFrameIndex;
            var self = new Dictionary<string, double>(); var gc = new Dictionary<string, double>(); var calls = new Dictionary<string, double>();
            int frames = 0;
            for (int f = Math.Max(first, last - 299); f <= last; f++)
            {
                using (var view = UnityEditorInternal.ProfilerDriver.GetHierarchyFrameDataView(f, 0, UnityEditor.Profiling.HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName,
                           UnityEditor.Profiling.HierarchyFrameDataView.columnSelfTime, false))
                {
                    if (view == null || !view.valid) continue;
                    frames++;
                    var stack = new Stack<int>(); stack.Push(view.GetRootItemID());
                    var kids = new List<int>();
                    while (stack.Count > 0)
                    {
                        int id = stack.Pop();
                        if (id != view.GetRootItemID())
                        {
                            string name = view.GetItemName(id);
                            Acc(self, name, view.GetItemColumnDataAsDouble(id, UnityEditor.Profiling.HierarchyFrameDataView.columnSelfTime));
                            Acc(gc, name, view.GetItemColumnDataAsDouble(id, UnityEditor.Profiling.HierarchyFrameDataView.columnGcMemory));
                            Acc(calls, name, view.GetItemColumnDataAsDouble(id, UnityEditor.Profiling.HierarchyFrameDataView.columnCalls));
                        }
                        view.GetItemChildren(id, kids);
                        foreach (var k in kids) stack.Push(k);
                    }
                }
            }
            string label = Arg("-perfLabel", "run");
            var sb = new StringBuilder();
            sb.AppendLine($"# Hotspots `{label}` -- {frames} profiled frames (main thread, merged by marker name, per-frame averages)");
            sb.AppendLine();
            sb.AppendLine("## Top 30 by self time"); sb.AppendLine("| Marker | self ms/frame | calls/frame | GC B/frame |"); sb.AppendLine("|---|---|---|---|");
            foreach (var kv in self.OrderByDescending(k => k.Value).Take(30))
                sb.AppendLine($"| `{kv.Key}` | {kv.Value / frames:0.000} | {Get(calls, kv.Key) / frames:0.#} | {Get(gc, kv.Key) / frames:0} |");
            sb.AppendLine(); sb.AppendLine("## Top 60 by GC alloc (inclusive)"); sb.AppendLine("| Marker | GC B/frame | self ms/frame | calls/frame |"); sb.AppendLine("|---|---|---|---|");
            foreach (var kv in gc.Where(k => k.Value > 0).OrderByDescending(k => k.Value).Take(60))
                sb.AppendLine($"| `{kv.Key}` | {kv.Value / frames:0} | {Get(self, kv.Key) / frames:0.000} | {Get(calls, kv.Key) / frames:0.#} |");
            sb.AppendLine(); sb.Append(RendererAudit());
            var outDir = Path.Combine(PerfHarness.RepoRoot, "docs", "qa", "perf", "raw"); Directory.CreateDirectory(outDir);
            File.WriteAllText(Path.Combine(outDir, $"hotspots-{label}.md"), sb.ToString());
            Debug.Log(sb.ToString());
            cam.targetTexture = null; rt.Release();
            foreach (var b in bots) b.Destroy();
            Assert.That(frames, Is.GreaterThan(10), "profiler captured no frames");
#else
            Assert.Ignore("Editor-only"); yield break;
#endif
        }

        /// <summary>Scene renderer census: who casts shadows, who shares meshes, instancing / static batching state.</summary>
        static string RendererAudit()
        {
            var all = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            int enabled = 0, shadow = 0, staticBatched = 0, skinned = 0, instancingMats = 0, totalMats = 0;
            var groups = new Dictionary<string, int[]>(); // key -> count, shadow casters, tris
            var mats = new HashSet<Material>();
            foreach (var r in all)
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                enabled++;
                if (r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off) shadow++;
                if (r.isPartOfStaticBatch) staticBatched++;
                Mesh m = null;
                if (r is SkinnedMeshRenderer smr) { skinned++; m = smr.sharedMesh; }
                else { var mf = r.GetComponent<MeshFilter>(); if (mf != null) m = mf.sharedMesh; }
                foreach (var mat in r.sharedMaterials) if (mat != null) mats.Add(mat);
                string root = r.transform.root.name;
                string key = (m != null ? m.name : r.GetType().Name) + " @ " + root;
                if (!groups.TryGetValue(key, out var g)) groups[key] = g = new int[3];
                g[0]++; if (r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off) g[1]++;
                if (m != null && m.isReadable) g[2] += (int)(m.GetIndexCount(0) / 3); else if (m != null) for (int s = 0; s < m.subMeshCount; s++) g[2] += (int)(m.GetIndexCount(s) / 3);
            }
            foreach (var mat in mats) { totalMats++; if (mat.enableInstancing) instancingMats++; }
            var sb = new StringBuilder();
            sb.AppendLine($"## Renderer audit");
            sb.AppendLine($"- active renderers {enabled} (skinned {skinned}), shadow casting {shadow}, static-batched {staticBatched}, distinct materials {totalMats} (instancing on {instancingMats})");
            sb.AppendLine($"- terrains {Terrain.activeTerrains.Length}" + (Terrain.activeTerrain != null ? $", detailDist {Terrain.activeTerrain.detailObjectDistance}, treeDist {Terrain.activeTerrain.treeDistance}, pixelError {Terrain.activeTerrain.heightmapPixelError}, castShadows {Terrain.activeTerrain.shadowCastingMode}" : ""));
            sb.AppendLine($"- lights: " + string.Join(", ", UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Where(l => l.isActiveAndEnabled).Select(l => $"{l.name}({l.type},{l.shadows})")));
            sb.AppendLine(); sb.AppendLine("| mesh @ root | renderers | shadow casters | tris (sum) |"); sb.AppendLine("|---|---|---|---|");
            foreach (var kv in groups.OrderByDescending(k => k.Value[0]).Take(25)) sb.AppendLine($"| `{kv.Key}` | {kv.Value[0]} | {kv.Value[1]} | {kv.Value[2]} |");
            sb.AppendLine(); sb.AppendLine("Materials without GPU instancing (top 20 by name):");
            foreach (var mat in mats.Where(m => !m.enableInstancing).OrderBy(m => m.name).Take(20)) sb.AppendLine($"- `{mat.name}` ({mat.shader.name})");
            return sb.ToString();
        }

        static void Acc(Dictionary<string, double> d, string k, double v) { d.TryGetValue(k, out var o); d[k] = o + v; }
        static double Get(Dictionary<string, double> d, string k) => d.TryGetValue(k, out var v) ? v : 0;
        static void Tick(List<BotFarmer> bots, Vector3 pen, EconomyDirector dir) { foreach (var b in bots) b.Tick(Time.deltaTime, pen, dir.IsMarketOpen); }
        static string Arg(string name, string def)
        {
            var a = Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
            return def;
        }
    }
}
