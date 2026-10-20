using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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

namespace HarvestPanic.Tests.PlayMode.Integration
{
    /// <summary>
    /// QA captures of the first-person view in Muddlebury: idle arms + reticle, aiming at a crate, holding a crate
    /// (prompt chip), and a HUD clock crop. Set HP_QA_DIR and run with a GPU (no -nographics); skipped otherwise.
    /// </summary>
    public class FirstPersonScreenshots
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            var nm = NetworkManager.Singleton;
            if (nm != null) { nm.Shutdown(); yield return null; Object.Destroy(nm.gameObject); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator Capture_FirstPerson_Arms_Reticle_Hold_Clock()
        {
            string dir = Environment.GetEnvironmentVariable("HP_QA_DIR");
            if (string.IsNullOrEmpty(dir) || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("QA screenshot; set HP_QA_DIR and run with a GPU");
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Muddlebury.unity", new LoadSceneParameters(LoadSceneMode.Single));
            var go = Object.Instantiate(HarvestPanic.Networking.NetworkingConfig.Load().networkManagerPrefab);
            go.GetComponent<HarvestPanic.Networking.SessionService>().HostInPlaceForEditor();
#endif
            float until = Time.realtimeSinceStartup + 20f;
            while ((PlayerCharacter.Local == null || EconomyDirector.Instance == null) && Time.realtimeSinceStartup < until) yield return null;
            var pc = PlayerCharacter.Local;
            Assert.That(pc, Is.Not.Null);
            yield return new WaitForSeconds(3f);
            Directory.CreateDirectory(dir);
            var cam = pc.ViewCamera.GetComponent<Camera>();

            // 1. idle.
            pc.Motor.SetLook(pc.transform.eulerAngles.y, 8f);
            yield return new WaitForSeconds(0.6f);
            yield return QaCapture.WithHud(cam, Path.Combine(dir, "fp_1_idle_arms_reticle.png"));

            // 2. aim at a crate, 3. hold it.
            Goods crate = null;
            foreach (var g in Goods.All) if (g != null && g.GoodsId == "Product.Crate") { crate = g; break; }
            Assert.That(crate, Is.Not.Null, "no crate in Muddlebury");
            // Bring the crate to open ground in front of the player (server = this process).
            Vector3 fwd = pc.transform.forward; fwd.y = 0f; fwd.Normalize();
            Vector3 spot = pc.transform.position + fwd * 1.35f + Vector3.up * 0.6f;
            var rb = crate.GetComponent<Rigidbody>();
            crate.transform.SetPositionAndRotation(spot, Quaternion.LookRotation(fwd));
            if (rb != null) { rb.position = spot; rb.rotation = Quaternion.LookRotation(fwd); rb.linearVelocity = Vector3.zero; }
            yield return new WaitForSeconds(1.2f);
            var col = crate.GetComponentInChildren<Collider>();
            pc.Motor.LookAt(col != null ? col.bounds.center : crate.transform.position);
            yield return new WaitForSeconds(0.6f);
            var cp2 = pc.CameraPivot;
            bool hit = Physics.Raycast(cp2.position, cp2.forward, out var rh, 5f, ~0, QueryTriggerInteraction.Ignore);
            Debug.Log($"[FPQA] player={pc.transform.position} crate={crate.transform.position} hit={(hit ? rh.collider.name + "@" + rh.distance : "none")}");
            Assert.That(pc.Carry.AimingAtGrabbable, Is.True, "not aiming at the crate");
            yield return QaCapture.WithHud(cam, Path.Combine(dir, "fp_2_aim_crate.png"));

            Assert.That(pc.Carry.TryGrabAimed(), Is.True);
            yield return new WaitForSeconds(0.4f);
            pc.Motor.SetLook(pc.transform.eulerAngles.y, 10f);
            yield return new WaitForSeconds(1.2f);
            Assert.That(pc.Carry.IsHoldingReplicated, Is.True, "crate not held");
            yield return QaCapture.WithHud(cam, Path.Combine(dir, "fp_3_hold_crate.png"));

            // 4. HUD clock (crop of the bottom-left corner, 2x).
            yield return QaCapture.WithHud(cam, Path.Combine(dir, "fp_4_hud_clock.png"), new RectInt(0, 0, 520, 260));
            pc.Carry.RequestRelease();
            yield return null;
        }
    }

    /// <summary>Shared offscreen capture helpers that composite every UI Toolkit document on the HUD panel.</summary>
    public static class QaCapture
    {
        /// <summary>Renders <paramref name="cam"/> plus the HUD panel into a PNG; <paramref name="crop"/> is bottom-left origin pixels.</summary>
        public static IEnumerator WithHud(Camera cam, string path, RectInt? crop = null)
        {
            const int w = 1280, h = 720;
            var docs = new List<UIDocument>();
            PanelSettings original = null;
            var hud = Object.FindFirstObjectByType<EconomyHud>();
            if (hud != null) original = hud.GetComponent<UIDocument>().panelSettings;
            foreach (var d in Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
                if (d.panelSettings != null && (original == null || d.panelSettings == original)) { docs.Add(d); if (original == null) original = d.panelSettings; }

            var sceneRt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            var uiRt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            PanelSettings ps = null;
            var saved = new List<PanelSettings>();
            if (docs.Count > 0)
            {
                ps = Object.Instantiate(original);
                ps.targetTexture = uiRt; ps.clearColor = true; ps.colorClearValue = Color.clear;
                foreach (var d in docs) { saved.Add(d.panelSettings); d.panelSettings = ps; }
                // Documents rebuild on panel change: let them re-run their Update/Build.
                foreach (var d in docs) d.rootVisualElement.MarkDirtyRepaint();
            }
            for (int f = 0; f < 6; f++) yield return null;
            cam.targetTexture = sceneRt; cam.Render(); cam.Render(); cam.targetTexture = null;
            var a = Read(sceneRt);
            if (ps != null)
            {
                var b = Read(uiRt);
                var px = a.GetPixels32(); var ui = b.GetPixels32();
                for (int i = 0; i < px.Length; i++)
                {
                    float al = ui[i].a / 255f;
                    px[i] = new Color32((byte)Mathf.Min(255, ui[i].r + px[i].r * (1 - al)), (byte)Mathf.Min(255, ui[i].g + px[i].g * (1 - al)), (byte)Mathf.Min(255, ui[i].b + px[i].b * (1 - al)), 255);
                }
                a.SetPixels32(px);
                Object.Destroy(b);
                for (int i = 0; i < docs.Count; i++) docs[i].panelSettings = saved[i];
                Object.Destroy(ps);
            }
            Texture2D outTex = a;
            if (crop.HasValue)
            {
                var r = crop.Value;
                outTex = new Texture2D(r.width, r.height, TextureFormat.RGBA32, false);
                outTex.SetPixels(a.GetPixels(r.x, r.y, r.width, r.height));
                outTex.Apply();
            }
            File.WriteAllBytes(path, outTex.EncodeToPNG());
            if (outTex != a) Object.Destroy(outTex);
            Object.Destroy(a);
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
    }
}
