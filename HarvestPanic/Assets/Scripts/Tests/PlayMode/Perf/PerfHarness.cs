using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using HarvestPanic.Core;
using HarvestPanic.Economy;
using HarvestPanic.Gameplay;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace HarvestPanic.Tests.PlayMode.Perf
{
    /// <summary>Shared scene boot (Muddlebury + in-place LAN host on a random port) and teardown.</summary>
    static class PerfHarness
    {
        public static string RepoRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));

        public static IEnumerator BootMuddlebury()
        {
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Muddlebury.unity", new LoadSceneParameters(LoadSceneMode.Single));
            var go = Object.Instantiate(HarvestPanic.Networking.NetworkingConfig.Load().networkManagerPrefab);
            go.GetComponent<HarvestPanic.Networking.SessionService>().HostInPlaceForEditor();
            yield return WaitFor(() => PlayerCharacter.Local != null && EconomyDirector.Instance != null, 30f, "player + economy");
#else
            Assert.Ignore("Editor-only");
            yield break;
#endif
        }

        public static IEnumerator Shutdown()
        {
            var nm = NetworkManager.Singleton;
            if (nm != null) { nm.Shutdown(); yield return null; Object.Destroy(nm.gameObject); }
            yield return null;
        }

        public static IEnumerator WaitFor(Func<bool> cond, float timeout, string what)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (!cond())
            {
                if (Time.realtimeSinceStartup > end) Assert.Fail("Timed out waiting for " + what);
                yield return null;
            }
        }

        public static Vector3 PenDropPoint()
        {
            var pens = LevelMarker.FindAll(MarkerRole.MarketPen);
            Assert.That(pens.Count, Is.GreaterThan(0), "no Sale Pen marker");
            var pen = pens[0].GetComponent<MarketPen>();
            Assert.That(pen, Is.Not.Null, "pen not set up by the director");
            var b = pen.Trigger.bounds;
            return new Vector3(b.center.x, b.min.y, b.center.z);
        }

        public static List<BotFarmer> SpawnBots(int count, int seed)
        {
            var prefab = HarvestPanic.Networking.PlayerPrefabRef.Load()?.prefab;
            Assert.That(prefab, Is.Not.Null, "no player prefab");
            var starts = LevelMarker.FindAll(MarkerRole.PlayerStart);
            var origin = PlayerCharacter.Local.transform.position;
            var bots = new List<BotFarmer>();
            BotFarmer.Claimed.Clear();
            for (int i = 0; i < count; i++)
            {
                Vector3 p = starts.Count > i + 1 ? starts[i + 1].transform.position : origin + new Vector3(2f * (i + 1), 0f, 1f);
                bots.Add(new BotFarmer(prefab, p + Vector3.up * 0.2f, i + 1, seed + i));
            }
            return bots;
        }

        public static int SpawnedNetworkObjects => NetworkManager.Singleton != null && NetworkManager.Singleton.SpawnManager != null
            ? NetworkManager.Singleton.SpawnManager.SpawnedObjectsList.Count : 0;
    }

    /// <summary>ProfilerRecorder bundle + per-frame sample store.</summary>
    sealed class PerfRecorder : IDisposable
    {
        readonly Dictionary<string, ProfilerRecorder> _rec = new Dictionary<string, ProfilerRecorder>();
        public readonly Dictionary<string, List<double>> Samples = new Dictionary<string, List<double>>();
        public readonly List<double> FrameMs = new List<double>();

        public PerfRecorder()
        {
            Add("MainThreadMs", ProfilerCategory.Internal, "Main Thread", 1e-6);
            Add("RenderThreadMs", ProfilerCategory.Internal, "Render Thread", 1e-6);
            Add("DrawCalls", ProfilerCategory.Render, "Draw Calls Count", 1);
            Add("Batches", ProfilerCategory.Render, "Batches Count", 1);
            Add("SetPass", ProfilerCategory.Render, "SetPass Calls Count", 1);
            Add("Triangles", ProfilerCategory.Render, "Triangles Count", 1);
            Add("ShadowCasters", ProfilerCategory.Render, "Shadow Casters Count", 1);
            Add("GcAllocKB", ProfilerCategory.Memory, "GC Allocated In Frame", 1.0 / 1024);
            Add("GcAllocCount", ProfilerCategory.Memory, "GC Allocation In Frame Count", 1);
            Add("TotalUsedMB", ProfilerCategory.Memory, "Total Used Memory", 1.0 / (1024 * 1024));
            Add("SystemUsedMB", ProfilerCategory.Memory, "System Used Memory", 1.0 / (1024 * 1024));
            Add("GcUsedMB", ProfilerCategory.Memory, "GC Used Memory", 1.0 / (1024 * 1024));
            Add("TextureMB", ProfilerCategory.Memory, "Texture Memory", 1.0 / (1024 * 1024));
            Add("MeshMB", ProfilerCategory.Memory, "Mesh Memory", 1.0 / (1024 * 1024));
            Add("PhysicsMs", ProfilerCategory.Physics, "Physics.Simulate", 1e-6);
            Add("ScriptsUpdateMs", ProfilerCategory.Scripts, "Update.ScriptRunBehaviourUpdate", 1e-6);
            Add("ScriptsLateUpdateMs", ProfilerCategory.Scripts, "PreLateUpdate.ScriptRunBehaviourLateUpdate", 1e-6);
            Add("FixedUpdateMs", ProfilerCategory.Scripts, "FixedUpdate.ScriptRunBehaviourFixedUpdate", 1e-6);
            Add("ActiveBodies", ProfilerCategory.Physics, "Active Dynamic Bodies", 1);
        }

        readonly Dictionary<string, double> _scale = new Dictionary<string, double>();
        void Add(string key, ProfilerCategory cat, string stat, double scale)
        {
            _rec[key] = ProfilerRecorder.StartNew(cat, stat, 1);
            _scale[key] = scale;
            Samples[key] = new List<double>(8192);
        }

        public void Sample(double frameMs)
        {
            FrameMs.Add(frameMs);
            foreach (var kv in _rec)
                if (kv.Value.Valid) Samples[kv.Key].Add(kv.Value.LastValue * _scale[kv.Key]);
        }

        public void Dispose() { foreach (var r in _rec.Values) r.Dispose(); }

        public static double Pct(List<double> v, double p)
        {
            if (v.Count == 0) return double.NaN;
            var s = new List<double>(v); s.Sort();
            return s[Math.Min(s.Count - 1, (int)Math.Ceiling(p * s.Count) - 1)];
        }
        public static double Avg(List<double> v) { if (v.Count == 0) return double.NaN; double t = 0; foreach (var x in v) t += x; return t / v.Count; }
        public static double Max(List<double> v) { double m = double.NaN; foreach (var x in v) if (double.IsNaN(m) || x > m) m = x; return m; }

        public string Table()
        {
            var sb = new StringBuilder();
            sb.AppendLine("| Metric | avg | p50 | p95 | p99 | max | samples |");
            sb.AppendLine("|---|---|---|---|---|---|---|");
            Row(sb, "FrameMs (wall)", FrameMs);
            foreach (var kv in Samples) Row(sb, kv.Key, kv.Value);
            return sb.ToString();
        }

        static void Row(StringBuilder sb, string name, List<double> v)
        {
            if (v.Count == 0) { sb.AppendLine($"| {name} | n/a | | | | | 0 |"); return; }
            sb.AppendLine($"| {name} | {Avg(v):0.##} | {Pct(v, .5):0.##} | {Pct(v, .95):0.##} | {Pct(v, .99):0.##} | {Max(v):0.##} | {v.Count} |");
        }
    }
}
