using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using HarvestPanic.Camcorder;
using HarvestPanic.Core;
using HarvestPanic.Economy;
using HarvestPanic.Gameplay;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace HarvestPanic.Tests.PlayMode.Camcorder
{
    /// <summary>QA captures: replay viewer and payout "Moment of the day" in Muddlebury, plus a real export
    /// (set HP_QA_DIR, run without -nographics, pass -netPort to avoid 7777).</summary>
    public class CamcorderScreenshots
    {
        const int W = 1280, H = 720;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (CamcorderService.Instance != null) CamcorderService.Instance.Viewer.Close();
            var nm = NetworkManager.Singleton;
            if (nm != null) { nm.Shutdown(); yield return null; Object.Destroy(nm.gameObject); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator Capture_Viewer_And_MomentOfTheDay()
        {
            string dir = Environment.GetEnvironmentVariable("HP_QA_DIR");
            if (string.IsNullOrEmpty(dir) || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("QA screenshot; set HP_QA_DIR and run with a GPU");
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Muddlebury.unity", new LoadSceneParameters(LoadSceneMode.Single));
            var go = Object.Instantiate(HarvestPanic.Networking.NetworkingConfig.Load().networkManagerPrefab);
            go.GetComponent<HarvestPanic.Networking.SessionService>().HostInPlaceForEditor();
#endif
            float until = Time.realtimeSinceStartup + 25f;
            while ((PlayerCharacter.Local == null || EconomyDirector.Instance == null) && Time.realtimeSinceStartup < until) yield return null;
            var pc = PlayerCharacter.Local;
            Assert.That(pc, Is.Not.Null);
            var svc = CamcorderService.EnsureExists();
            Directory.CreateDirectory(dir);
            var eco = EconomyDirector.Instance;
            while (eco.Phase != DayPhase.Workday) { eco.DebugSkipPhase(); yield return null; }
            // Walk the farmer toward the bridge while the camcorder rolls.
            pc.ServerTeleport(new Vector3(24f, 0.3f, -6f), Quaternion.LookRotation(new Vector3(1f, 0f, 0.15f)));
            for (int i = 0; i < 100; i++)
            {
                pc.ServerTeleport(new Vector3(24f + i * 0.075f, 0.3f, -6f + Mathf.Sin(i * 0.3f)), Quaternion.LookRotation(new Vector3(1f, 0f, 0.15f)));
                yield return new WaitForSeconds(0.1f);
            }
            Assert.That(svc.TrackedCount, Is.GreaterThan(1), "players/critters/planks tracked");
            svc.OnDisaster(new BlameEvent
            {
                Severity = 88, Verb = BlameVerb.DroppedInRiver, DollarDelta = -240, PrimaryInstigator = NetworkManager.Singleton.LocalClientId,
                Instigators = new[] { NetworkManager.Singleton.LocalClientId }, Location = pc.transform.position + Vector3.right * 2f
            });
            for (int i = 0; i < 40; i++)
            {
                pc.ServerTeleport(new Vector3(31.5f + i * 0.2f, 0.3f, -6f), Quaternion.LookRotation(Vector3.right));
                yield return new WaitForSeconds(0.1f);
            }
            Assert.That(svc.Shelf.Count, Is.GreaterThanOrEqualTo(1), "disaster tape saved");
            var m = svc.Shelf.Items[svc.Shelf.Count - 1]; // the farmer's own tape (the scene may produce its own disasters too)
            Debug.Log($"[CamcorderQA] tape '{m.Title}' {m.Duration:0.0}s frames={m.FrameCount} tracks={m.Tracks.Count} samples={m.Samples.Length} bytes={m.MemoryBytes} ring={svc.RingMemoryBytes}");

            svc.Watch(m);
            svc.Viewer.Playing = false;
            svc.Viewer.SetTime(m.TriggerTime);
            for (int i = 0; i < 5; i++) { svc.Viewer.UpdateCamera(0.2f); yield return null; }
            yield return Capture(svc.Viewer.Cam, Path.Combine(dir, "camcorder_viewer_grandpa.png"));
            svc.Viewer.Mode = ViewerCam.Victim;
            for (int i = 0; i < 30; i++) { svc.Viewer.UpdateCamera(0.1f); yield return null; }
            yield return Capture(svc.Viewer.Cam, Path.Combine(dir, "camcorder_viewer_victim.png"));
            svc.Viewer.Mode = ViewerCam.Grandpa;

            string folder = null, err = null;
            yield return ClipExporter.Export(svc.Viewer, svc.Tuning, (f, e) => { folder = f; err = e; }, Path.Combine(dir, "clips"));
            Assert.That(err, Is.Null, err);
            Debug.Log("[CamcorderQA] exported to " + folder);
            svc.Viewer.Close();

            while (eco.Phase != DayPhase.Payout) { eco.DebugSkipPhase(); yield return null; }
            yield return new WaitForSeconds(1f);
            if (!svc.Hud.CardVisible) svc.Hud.ShowCard();
            Assert.That(svc.Hud.CardVisible, Is.True);
            yield return Capture(Camera.main, Path.Combine(dir, "camcorder_receipt_moment.png"));
        }

        /// <summary>Scene camera + every active UIDocument (composited by sorting order).</summary>
        static IEnumerator Capture(Camera cam, string path)
        {
            var docs = new List<UIDocument>(Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None));
            docs.RemoveAll(d => d.panelSettings == null || !d.isActiveAndEnabled);
            docs.Sort((a, b) => a.panelSettings.sortingOrder.CompareTo(b.panelSettings.sortingOrder));
            var originals = new List<PanelSettings>(); var rts = new List<RenderTexture>(); var clones = new List<PanelSettings>();
            foreach (var d in docs)
            {
                var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
                var ps = Object.Instantiate(d.panelSettings);
                ps.targetTexture = rt; ps.clearColor = true; ps.colorClearValue = Color.clear;
                originals.Add(d.panelSettings); clones.Add(ps); rts.Add(rt);
                d.panelSettings = ps;
            }
            for (int f = 0; f < 4; f++) yield return null;
            var sceneRt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            ClipRender.RenderInto(cam, sceneRt); ClipRender.RenderInto(cam, sceneRt);
            var a = Read(sceneRt);
            var px = a.GetPixels32();
            for (int k = 0; k < docs.Count; k++)
            {
                var b = Read(rts[k]); var ui = b.GetPixels32();
                for (int i = 0; i < px.Length; i++)
                {
                    float al = ui[i].a / 255f;
                    px[i] = new Color32((byte)Mathf.Min(255, ui[i].r + px[i].r * (1 - al)), (byte)Mathf.Min(255, ui[i].g + px[i].g * (1 - al)), (byte)Mathf.Min(255, ui[i].b + px[i].b * (1 - al)), 255);
                }
                Object.Destroy(b);
                docs[k].panelSettings = originals[k];
                Object.Destroy(clones[k]); rts[k].Release();
            }
            a.SetPixels32(px);
            File.WriteAllBytes(path, a.EncodeToPNG());
            Object.Destroy(a); sceneRt.Release();
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
