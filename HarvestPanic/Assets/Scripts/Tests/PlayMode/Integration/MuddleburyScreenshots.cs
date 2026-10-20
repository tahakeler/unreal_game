using System;
using System.Collections;
using System.IO;
using HarvestPanic.AI;
using HarvestPanic.Core;
using HarvestPanic.Gameplay;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HarvestPanic.Tests.PlayMode.Integration
{
    /// <summary>QA captures of Muddlebury (set HP_QA_DIR; run without -nographics). Skipped otherwise.</summary>
    public class MuddleburyScreenshots
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            var nm = NetworkManager.Singleton;
            if (nm != null) { nm.Shutdown(); yield return null; Object.Destroy(nm.gameObject); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator Capture_FirstPerson_PenAnimals_Bridge()
        {
            string dir = Environment.GetEnvironmentVariable("HP_QA_DIR");
            if (string.IsNullOrEmpty(dir) || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("QA screenshot; set HP_QA_DIR and run with a GPU");
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Muddlebury.unity", new LoadSceneParameters(LoadSceneMode.Single));
            var go = Object.Instantiate(HarvestPanic.Networking.NetworkingConfig.Load().networkManagerPrefab);
            go.GetComponent<HarvestPanic.Networking.SessionService>().HostInPlaceForEditor();
#endif
            float until = Time.realtimeSinceStartup + 20f;
            while (PlayerCharacter.Local == null && Time.realtimeSinceStartup < until) yield return null;
            var pc = PlayerCharacter.Local;
            Assert.That(pc, Is.Not.Null);
            yield return new WaitForSeconds(3f);

            // 1. first-person at spawn, with the real screen (HUD) composited.
            Directory.CreateDirectory(dir);
            yield return CaptureWithHud(pc.ViewCamera.GetComponent<Camera>(), Path.Combine(dir, "muddlebury_1_firstperson_hud.png"));

            // 2. farmer + pigs/chickens near the pen (third-person look).
            var view = pc.GetComponent<PlayerView>();
            view.ForceRemoteLook = true; view.ApplyViewMode();
            Vector3 focus = Vector3.zero; int n = 0;
            foreach (var b in CritterRegistry.All) { focus += b.transform.position; n++; }
            Vector3 pig = CritterRegistry.All[0].transform.position;
            pc.ServerTeleport(pig + new Vector3(-3f, 0.2f, -3f), Quaternion.LookRotation(new Vector3(1, 0, 1)));
            yield return new WaitForSeconds(1f);
            var cam = new GameObject("QA Camera").AddComponent<Camera>();
            cam.fieldOfView = 60f;
            cam.transform.position = pc.transform.position + pc.transform.rotation * new Vector3(2.5f, 2.0f, -4.5f);
            cam.transform.LookAt(pc.transform.position + pc.transform.forward * 3f + Vector3.up * 0.8f);
            yield return null; yield return null;
            Capture(cam, Path.Combine(dir, "muddlebury_2_farmer_animals_pen.png"));

            // 3. on the bridge.
            var planks = LevelMarker.FindAll(MarkerRole.BridgePlank);
            var mid = planks[planks.Count / 2].transform.position;
            pc.ServerTeleport(mid + Vector3.up * 0.4f, Quaternion.LookRotation(Vector3.right));
            yield return new WaitForSeconds(1f);
            cam.transform.position = pc.transform.position + new Vector3(-5f, 2.8f, -5f);
            cam.transform.LookAt(pc.transform.position + Vector3.up * 0.9f);
            yield return null; yield return null;
            Capture(cam, Path.Combine(dir, "muddlebury_3_bridge.png"));
            Object.Destroy(cam.gameObject);
            Debug.Log($"[MuddleburyQA] critters={n} bridge={mid} player={pc.transform.position}");
        }

        static IEnumerator CaptureWithHud(Camera cam, string path)
        {
            var hud = Object.FindFirstObjectByType<HarvestPanic.Economy.EconomyHud>();
            var doc = hud != null ? hud.GetComponent<UIDocument>() : null;
            if (doc == null) { Capture(cam, path); yield break; }
            const int w = 1280, h = 720;
            var sceneRt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            var uiRt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            var original = doc.panelSettings;
            var ps = Object.Instantiate(original);
            ps.targetTexture = uiRt; ps.clearColor = true; ps.colorClearValue = Color.clear;
            doc.panelSettings = ps;
            for (int f = 0; f < 4; f++) yield return null;
            cam.targetTexture = sceneRt; cam.Render(); cam.Render(); cam.targetTexture = null;
            var a = Read(sceneRt); var b = Read(uiRt);
            var px = a.GetPixels32(); var ui = b.GetPixels32();
            for (int i = 0; i < px.Length; i++)
            {
                float al = ui[i].a / 255f;
                px[i] = new Color32((byte)Mathf.Min(255, ui[i].r + px[i].r * (1 - al)), (byte)Mathf.Min(255, ui[i].g + px[i].g * (1 - al)), (byte)Mathf.Min(255, ui[i].b + px[i].b * (1 - al)), 255);
            }
            a.SetPixels32(px);
            File.WriteAllBytes(path, a.EncodeToPNG());
            doc.panelSettings = original;
            Object.Destroy(ps); Object.Destroy(a); Object.Destroy(b);
            sceneRt.Release(); uiRt.Release();
        }

        static Texture2D Read(RenderTexture rt)
        {
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var t = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            t.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); t.Apply();
            RenderTexture.active = prev;
            return t;
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
