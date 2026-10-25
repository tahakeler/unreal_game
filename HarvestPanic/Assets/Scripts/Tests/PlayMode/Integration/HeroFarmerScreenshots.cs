using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using HarvestPanic.Customization;
using HarvestPanic.Economy;
using HarvestPanic.Gameplay;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HarvestPanic.Tests.PlayMode.Integration
{
    /// <summary>
    /// QA captures of the hero Tripo farmer in Muddlebury (set HP_QA_DIR; run with a GPU, skipped otherwise):
    /// first-person arms idle and holding a crate, the same player seen third-person carrying it (the animator is driven by
    /// the real PlayerCarry state through IPlayerAnimationSource), knocked down and getting up, a crew of four and six
    /// colourways of the recolour shader under the level's lighting.
    /// </summary>
    public class HeroFarmerScreenshots
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            var nm = NetworkManager.Singleton;
            if (nm != null) { nm.Shutdown(); yield return null; Object.Destroy(nm.gameObject); }
            yield return null;
        }

        static void ShowBody(FarmerVisual v)
        {
            foreach (var r in v.GetComponentsInChildren<Renderer>(true))
                if (v.NameTag == null || !r.transform.IsChildOf(v.NameTag.transform)) r.shadowCastingMode = ShadowCastingMode.On;
        }

        static Camera Cam(Vector3 pos, Vector3 lookAt, float fov = 40f)
        {
            var cam = new GameObject("QA Cam").AddComponent<Camera>();
            cam.fieldOfView = fov; cam.nearClipPlane = 0.05f; cam.farClipPlane = 400f;
            cam.transform.position = pos; cam.transform.LookAt(lookAt);
            return cam;
        }

        static readonly FarmerLoadout[] Colourways =
        {
            new FarmerLoadout { Body = 0, Primary = 0, Secondary = 14, Accent = 17, Skin = 4, NameTag = 0, HairColor = 17, Hat = 1, Emote0 = 1, Emote1 = 2, Emote2 = 3, Emote3 = 4 },
            new FarmerLoadout { Body = 0, Primary = 1, Secondary = 15, Accent = 13, Skin = 10, NameTag = 1, HairColor = 13, Hat = 1, Emote0 = 1, Emote1 = 2, Emote2 = 3, Emote3 = 4 },
            new FarmerLoadout { Body = 0, Primary = 2, Secondary = 20, Accent = 12, Skin = 0, NameTag = 2, HairColor = 11, Hat = 1, Emote0 = 1, Emote1 = 2, Emote2 = 3, Emote3 = 4 },
            new FarmerLoadout { Body = 0, Primary = 3, Secondary = 19, Accent = 9, Skin = 7, NameTag = 3, HairColor = 20, Hat = 1, Emote0 = 1, Emote1 = 2, Emote2 = 3, Emote3 = 4 },
            new FarmerLoadout { Body = 0, Primary = 13, Secondary = 23, Accent = 15, Skin = 2, NameTag = 4, HairColor = 15, Hat = 1, Emote0 = 1, Emote1 = 2, Emote2 = 3, Emote3 = 4 },
            new FarmerLoadout { Body = 0, Primary = 15, Secondary = 12, Accent = 4, Skin = 11, NameTag = 5, HairColor = 13, Hat = 1, Emote0 = 1, Emote1 = 2, Emote2 = 3, Emote3 = 4 },
        };

        [UnityTest]
        public IEnumerator Capture_HeroFarmer_FirstPerson_Carry_Down_Crew_Colourways()
        {
            string dir = Environment.GetEnvironmentVariable("HP_QA_DIR");
            if (string.IsNullOrEmpty(dir) || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("QA screenshot; set HP_QA_DIR and run with a GPU");
            Directory.CreateDirectory(dir);
            LogAssert.ignoreFailingMessages = true;
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Muddlebury.unity", new LoadSceneParameters(LoadSceneMode.Single));
            var go = Object.Instantiate(HarvestPanic.Networking.NetworkingConfig.Load().networkManagerPrefab);
            go.GetComponent<HarvestPanic.Networking.SessionService>().HostInPlaceForEditor();
#endif
            float until = Time.realtimeSinceStartup + 20f;
            while ((PlayerCharacter.Local == null || EconomyDirector.Instance == null) && Time.realtimeSinceStartup < until) yield return null;
            var pc = PlayerCharacter.Local;
            Assert.That(pc, Is.Not.Null);
            var body = pc.GetComponentInChildren<FarmerVisual>();
            Assert.That(body, Is.Not.Null, "player nests the farmer body");
            Assert.That(body.RecolourRenderers.Count, Is.GreaterThan(0), "player uses the recoloured hero farmer");
            var sync = body.GetComponent<FarmerLoadoutSync>();
            sync.RequestLoadout(Colourways[2]);
            yield return new WaitForSeconds(3f);
            var fpCam = pc.ViewCamera.GetComponent<Camera>();

            // 1. first person, idle arms (sleeves = shirt colour, gloves = accent).
            pc.Motor.SetLook(pc.transform.eulerAngles.y, 8f);
            yield return new WaitForSeconds(0.6f);
            yield return QaCapture.WithHud(fpCam, Path.Combine(dir, "hero_fp_1_idle.png"));

            // 2. hold a crate (first person) and 3. the same farmer seen from outside.
            Goods crate = null;
            foreach (var g in Goods.All) if (g != null && g.GoodsId == "Product.Crate") { crate = g; break; }
            Assert.That(crate, Is.Not.Null, "no crate in Muddlebury");
            Vector3 fwd = pc.transform.forward; fwd.y = 0f; fwd.Normalize();
            Vector3 spot = pc.transform.position + fwd * 1.35f + Vector3.up * 0.6f;
            var rb = crate.GetComponent<Rigidbody>();
            crate.transform.SetPositionAndRotation(spot, Quaternion.LookRotation(fwd));
            if (rb != null) { rb.position = spot; rb.rotation = Quaternion.LookRotation(fwd); rb.linearVelocity = Vector3.zero; }
            yield return new WaitForSeconds(1.2f);
            var col = crate.GetComponentInChildren<Collider>();
            pc.Motor.LookAt(col != null ? col.bounds.center : crate.transform.position);
            yield return new WaitForSeconds(0.6f);
            Assert.That(pc.Carry.TryGrabAimed(), Is.True, "grab the crate");
            yield return new WaitForSeconds(0.4f);
            pc.Motor.SetLook(pc.transform.eulerAngles.y, 10f);
            yield return new WaitForSeconds(1.2f);
            Assert.That(pc.Carry.IsHoldingReplicated, Is.True, "crate not held");
            yield return QaCapture.WithHud(fpCam, Path.Combine(dir, "hero_fp_2_hold_crate.png"));

            var anim = body.BodyAnimator;
            Assert.That(anim.GetInteger(FarmerVisual.CarryHandsParam), Is.GreaterThan(0), "carry state reaches the body animator");
            Assert.That(anim.GetLayerWeight(FarmerVisual.UpperBodyLayer), Is.GreaterThan(0.5f), "upper-body carry layer is on");
            ShowBody(body);
            Vector3 p = pc.transform.position, right = pc.transform.right;
            var cam = Cam(p + fwd * 3.2f + right * 2.2f + Vector3.up * 1.7f, p + Vector3.up * 1.0f);
            yield return new WaitForSeconds(0.3f);
            Save(cam, Path.Combine(dir, "hero_tp_1_carry_crate.png"));
            pc.Carry.RequestRelease();
            yield return new WaitForSeconds(0.15f);
            Save(cam, Path.Combine(dir, "hero_tp_2_toss.png"));
            yield return new WaitForSeconds(0.8f);

            // 4. knocked down (limp), 5. getting up.
            pc.Faint(true);
            yield return new WaitForSeconds(1.0f);
            ShowBody(body);
            Assert.That(anim.GetBool(FarmerVisual.DownParam), Is.True, "down state reaches the body animator");
            Save(cam, Path.Combine(dir, "hero_tp_3_down.png"));
            pc.Faint(false);
            yield return new WaitForSeconds(0.45f);
            ShowBody(body);
            Save(cam, Path.Combine(dir, "hero_tp_4_getting_up.png"));
            yield return new WaitForSeconds(1.5f);

            // 6. a crew of four and 7. six colourways, standing in the level's light near the player start.
            var prefab = body.Catalog.farmerPrefab;
            Vector3 origin = p + fwd * 6f;
            var spawned = new List<GameObject>();
            FarmerVisual Spawn(Vector3 at, FarmerLoadout l, string name)
            {
                var f = Object.Instantiate(prefab, at, Quaternion.LookRotation(-fwd));
                WardrobePreview.StripNetworking(f);
                var v = f.GetComponent<FarmerVisual>();
                v.Apply(l);
                if (v.NameTag != null) v.NameTag.SetName(name);
                spawned.Add(f);
                return v;
            }
            var crew = new[] { WardrobeTestLineup.SetA[0], WardrobeTestLineup.SetB[2], WardrobeTestLineup.SetC[1], WardrobeTestLineup.SetA[3] };
            string[] names = { "Hank", "Cora", "Lou", "Fay" };
            for (int i = 0; i < 4; i++) Spawn(origin + right * ((i - 1.5f) * 1.9f), crew[i], names[i]);
            yield return new WaitForSeconds(0.8f);
            var crewCam = Cam(origin - fwd * 6.5f + Vector3.up * 2.2f, origin + Vector3.up * 1.1f, 45f);
            Save(crewCam, Path.Combine(dir, "hero_crew_4_muddlebury.png"));
            foreach (var s in spawned) Object.Destroy(s);
            spawned.Clear();
            yield return null;
            for (int i = 0; i < Colourways.Length; i++) Spawn(origin + right * ((i - 2.5f) * 1.35f), Colourways[i], "");
            yield return new WaitForSeconds(0.8f);
            var cwCam = Cam(origin - fwd * 6.8f + Vector3.up * 1.9f, origin + Vector3.up * 1.0f, 45f);
            Save(cwCam, Path.Combine(dir, "hero_colourways_6.png"));
            foreach (var s in spawned) Object.Destroy(s);
            LogAssert.ignoreFailingMessages = false;
        }

        static void Save(Camera cam, string path)
        {
            var rt = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32);
            var req = new RenderPipeline.StandardRequest { destination = rt };
            for (int i = 0; i < 2; i++)
            {
                if (RenderPipeline.SupportsRenderRequest(cam, req)) RenderPipeline.SubmitRenderRequest(cam, req);
                else { cam.targetTexture = rt; cam.Render(); cam.targetTexture = null; }
            }
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            rt.Release();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Debug.Log("[HeroQA] wrote " + path);
        }
    }
}
