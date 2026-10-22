using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarvestPanic.Economy;
using HarvestPanic.Gameplay;
using HarvestPanic.Tests.PlayMode.Integration;
using HarvestPanic.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HarvestPanic.Tests.PlayMode.Tools
{
    /// <summary>
    /// QA captures for feature/wagon-lasso-whistle: first-person lasso aim (twirl + arc) and reel, a roped pig and the
    /// loaded / bridge / tipped wagon from a chase camera, wagon push and whistle arm poses, and the HUD hotbar.
    /// Set HP_QA_DIR and run with a GPU (no -nographics); skipped otherwise.
    /// </summary>
    public class ToolsScreenshots
    {
        [UnitySetUp] public IEnumerator SetUp()
        {
            string dir = Environment.GetEnvironmentVariable("HP_QA_DIR");
            if (string.IsNullOrEmpty(dir) || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("QA screenshot; set HP_QA_DIR and run with a GPU");
            yield return Farm.Load();
        }

        [UnityTearDown] public IEnumerator TearDown()
        {
            if (PlayerCharacter.Local != null) PlayerCharacter.Local.Input.UseScripted = false;
            yield return Farm.Shutdown();
        }

        static string Dir { get { string d = Environment.GetEnvironmentVariable("HP_QA_DIR"); Directory.CreateDirectory(d); return d; } }

        /// <summary>The owner normally sees only the shadow of its own body; show it for third-person shots.</summary>
        static List<(Renderer, ShadowCastingMode)> ShowOwnBody(PlayerCharacter pc)
        {
            var saved = new List<(Renderer, ShadowCastingMode)>();
            var root = pc.BodyRoot != null ? pc.BodyRoot : pc.transform;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                if (r.shadowCastingMode == ShadowCastingMode.ShadowsOnly) { saved.Add((r, r.shadowCastingMode)); r.shadowCastingMode = ShadowCastingMode.On; }
            return saved;
        }

        static void Restore(List<(Renderer, ShadowCastingMode)> saved) { foreach (var (r, m) in saved) if (r != null) r.shadowCastingMode = m; }

        static IEnumerator Chase(Vector3 eye, Vector3 look, string file)
        {
            var go = new GameObject("QA Chase Cam");
            var cam = go.AddComponent<Camera>();
            go.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
            cam.fieldOfView = 55f; cam.nearClipPlane = 0.05f; cam.farClipPlane = 500f;
            go.transform.position = eye;
            go.transform.LookAt(look);
            yield return null;
            const int w = 1280, h = 720;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt; cam.Render(); cam.Render(); cam.targetTexture = null;
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(Path.Combine(Dir, file), tex.EncodeToPNG());
            Object.Destroy(tex); rt.Release(); Object.Destroy(go);
        }

        static Camera View(PlayerCharacter pc) => pc.ViewCamera.GetComponent<Camera>();

        [UnityTest]
        public IEnumerator Capture_Lasso_AimRopeReel()
        {
            var pc = PlayerCharacter.Local;
            var belt = pc.GetComponent<PlayerToolbelt>();
            Vector3 spot = Farm.Ground(new Vector3(2f, 0f, -10f));
            yield return Farm.Teleport(pc, spot, 90f);
            yield return Farm.Equip(pc, ToolSlot.Lasso);
            var lasso = belt.PocketTool<LassoTool>(ToolSlot.Lasso);
            var pig = Farm.Critters("Critter.Pig").First();
            Farm.Warp(pig, spot + Vector3.right * 7f);
            yield return new WaitForFixedUpdate();
            pc.Motor.LookAt(pig.transform.position + Vector3.up * 0.4f);
            yield return Farm.Press(pc, true);
            yield return new WaitForSeconds(0.5f);
            pc.Motor.LookAt(pig.transform.position + Vector3.up * 0.4f);
            yield return new WaitForSeconds(0.25f);
            yield return QaCapture.WithHud(View(pc), Path.Combine(Dir, "tools_1_fp_lasso_aim.png"));
            yield return Farm.Press(pc, false);
            float until = Time.time + 2f;
            while (lasso.Phase == LassoPhase.Flying && Time.time < until) yield return null;
            Assert.That(lasso.IsTethered, Is.True, $"no catch for the screenshot ({lasso.LastThrowInfo})");
            yield return new WaitForSeconds(1.5f);
            pc.Motor.LookAt(pig.transform.position + Vector3.up * 0.3f);
            yield return new WaitForSeconds(0.3f);
            yield return QaCapture.WithHud(View(pc), Path.Combine(Dir, "tools_2_fp_lasso_tethered.png"));

            var saved = ShowOwnBody(pc);
            Vector3 mid = (pc.transform.position + pig.transform.position) * 0.5f;
            Vector3 side = Vector3.Cross(Vector3.up, (pig.transform.position - pc.transform.position).normalized);
            yield return Chase(mid + side * 6.5f + Vector3.up * 2.6f, mid + Vector3.up * 0.6f, "tools_3_tp_lasso_pig.png");
            Restore(saved);

            yield return Farm.Press(pc, true); // reel
            yield return new WaitForSeconds(0.6f);
            pc.Motor.LookAt(pig.transform.position + Vector3.up * 0.3f);
            yield return null;
            yield return QaCapture.WithHud(View(pc), Path.Combine(Dir, "tools_4_fp_lasso_reel.png"));
            yield return Farm.Press(pc, false);
        }

        [UnityTest]
        public IEnumerator Capture_Wagon_Loaded_Push_Bridge_Tipped_Whistle_Hotbar()
        {
            var pc = PlayerCharacter.Local;
            Action<PlayerState, PlayerState> log = (a, b) => Debug.Log($"[ToolsQA] farmer {a} -> {b} at {pc.transform.position} t={Time.time:0.0}");
            pc.OnStateChanged += log;
            var wagon = WagonCart.All[0];
            var planks = Object.FindObjectsByType<BridgePlank>(FindObjectsSortMode.None);
            float west = planks.Min(p => p.transform.position.x) - 1f;
            wagon.BumpSpillChanceOverride = 0f;
            Vector3 start = Farm.Ground(new Vector3(west - 6f, 0f, 0f)) + Vector3.up * 0.1f;
            wagon.ServerPlace(start, Quaternion.Euler(0f, 90f, 0f));
            yield return new WaitForSeconds(1f);
            // Two crates dropped in behind the (later) pumpkin slots.
            var pumpkin = Farm.Goods("Crop.PrizePumpkin");
            for (int i = 0; i < 2; i++)
            {
                var crate = Farm.Goods("Product.Crate", i);
                Farm.Place(crate, wagon.Bed.TransformPoint(new Vector3(i == 0 ? -0.3f : 0.3f, 0.8f, -0.65f)));
                yield return new WaitForSeconds(0.8f);
            }
            Debug.Log($"[ToolsQA] screenshot wagon cargo {wagon.CargoCount} ({wagon.CargoKg:0} kg)");

            // Push onto the bridge (one farmer cannot heave 300 kg over the abutment lip; crates only), then the pumpkin.
            yield return Farm.Teleport(pc, Farm.Ground(wagon.transform.TransformPoint(new Vector3(0f, 0f, -2.3f))), 90f);
            Vector3 bar0 = (wagon.Handles[2].position + wagon.Handles[3].position) * 0.5f;
            Assert.That(pc.Carry.ServerGrab(wagon.NetworkObject, wagon.transform.InverseTransformPoint(bar0), false), Is.True, "push bar");
            yield return new WaitForSeconds(0.3f);
            pc.Motor.SetLook(90f, 8f);
            pc.Input.Scripted.Move = new Vector2(0f, 1f);
            float until0 = Time.time + 12f;
            while (wagon.transform.position.x < west + 12f && Time.time < until0) yield return null;
            pc.Input.Scripted.Move = Vector2.zero;
            yield return new WaitForSeconds(0.8f);
            pc.Carry.RequestRelease();
            yield return new WaitForSeconds(0.4f);
            if (pumpkin != null)
            {
                Farm.Place(pumpkin, wagon.Bed.TransformPoint(new Vector3(0f, 0.9f, 0.35f)));
                yield return new WaitForSeconds(0.1f);
                Debug.Log($"[ToolsQA] pumpkin loaded on the bridge: {wagon.ServerTryLoad(pumpkin.NetworkObject, false)}");
            }

            // Third-person: the jackpot load on the bridge, farmer on the push bar.
            yield return Farm.Teleport(pc, Farm.Ground(wagon.transform.TransformPoint(new Vector3(0f, 0f, -2.3f))), 90f);
            // Tall cargo can sit in the line of sight of the push bar (the prompt tells players which one they aim at);
            // for the capture take the bar directly.
            Vector3 bar = (wagon.Handles[2].position + wagon.Handles[3].position) * 0.5f;
            Assert.That(pc.Carry.ServerGrab(wagon.NetworkObject, wagon.transform.InverseTransformPoint(bar), false), Is.True, "push bar");
            yield return new WaitForSeconds(0.4f);
            pc.Motor.SetLook(90f, 8f);
            var saved = ShowOwnBody(pc);
            yield return Chase(wagon.transform.TransformPoint(new Vector3(4.2f, 2.4f, 2.5f)), wagon.transform.position + Vector3.up * 0.6f, "tools_5_tp_wagon_loaded.png");
            Restore(saved);
            yield return QaCapture.WithHud(View(pc), Path.Combine(Dir, "tools_6_fp_wagon_push.png"));

            saved = ShowOwnBody(pc);
            yield return Chase(wagon.transform.position + new Vector3(-3.5f, 3.2f, 5.5f), wagon.transform.position + Vector3.up * 0.5f, "tools_7_tp_wagon_bridge.png");
            Restore(saved);

            // Open road: tip it over.
            pc.Carry.RequestRelease();
            yield return new WaitForSeconds(0.3f);
            Vector3 bank = Farm.Ground(new Vector3(30f, 0f, 0f)) + Vector3.up * 0.1f;
            wagon.ServerPlace(bank, Quaternion.Euler(0f, 90f, 0f));
            yield return new WaitForSeconds(0.6f);
            for (int i = 0; i < 3; i++)
            {
                var crate = Farm.Goods("Product.Crate", i);
                Farm.Place(crate, wagon.Bed.TransformPoint(new Vector3(i % 2 == 0 ? -0.3f : 0.3f, 0.8f, 0.6f - i * 0.6f)));
                yield return new WaitForSeconds(0.6f);
            }
            Debug.Log($"[ToolsQA] tip shot: cargo {wagon.CargoCount}, farmer {pc.State} holding {pc.Carry.HeldId}");
            var rb = wagon.GetComponent<Rigidbody>();
            rb.position = wagon.transform.position + Vector3.up * 0.5f;
            rb.rotation = Quaternion.Euler(0f, 90f, 75f);
            rb.angularVelocity = Vector3.zero;
            yield return new WaitForSeconds(1.6f);
            yield return Chase(wagon.transform.position + new Vector3(-5.5f, 3.4f, -5f), wagon.transform.position + new Vector3(0f, 0.2f, -0.8f), "tools_8_tp_wagon_tipped.png");

            // Whistle at the lips + hotbar.
            float t0 = Time.time;
            while (!pc.HasControl && Time.time - t0 < 8f) yield return null;
            Debug.Log($"[ToolsQA] whistle shot: farmer {pc.State} control {pc.HasControl} holding {pc.Carry.HeldId}");
            if (pc.Carry.IsHoldingReplicated) { pc.Carry.RequestRelease(); yield return new WaitForSeconds(0.3f); }
            yield return Farm.Teleport(pc, Farm.Ground(new Vector3(20f, 0f, 8f)), 0f);
            yield return Farm.Equip(pc, ToolSlot.Whistle);
            pc.Motor.SetLook(0f, 4f);
            yield return Farm.Press(pc, true);
            yield return new WaitForSeconds(0.35f);
            yield return QaCapture.WithHud(View(pc), Path.Combine(Dir, "tools_9_fp_whistle.png"));
            yield return QaCapture.WithHud(View(pc), Path.Combine(Dir, "tools_10_hud_hotbar.png"), new RectInt(320, 0, 640, 120));
            yield return Farm.Press(pc, false);
            pc.OnStateChanged -= log;
        }
    }
}
