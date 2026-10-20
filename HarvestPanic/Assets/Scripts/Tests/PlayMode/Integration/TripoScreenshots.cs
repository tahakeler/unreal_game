using System;
using System.Collections;
using System.IO;
using System.Linq;
using HarvestPanic.AI;
using HarvestPanic.Economy;
using HarvestPanic.Gameplay;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HarvestPanic.Tests.PlayMode.Integration
{
    /// <summary>
    /// QA captures of the Tripo art pass in Muddlebury (set HP_QA_DIR; run without -nographics). Skipped otherwise.
    /// Also asserts the rig-free critters carry the procedural animator and no Animator.
    /// </summary>
    public class TripoScreenshots
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            var nm = NetworkManager.Singleton;
            if (nm != null) { nm.Shutdown(); yield return null; Object.Destroy(nm.gameObject); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator Capture_TripoCritters_Market_Bucket_Pumpkin()
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
            yield return new WaitForSeconds(2f);
            Directory.CreateDirectory(dir);

            // 1. Pig + chickens close-up in the pig pen (chickens warped next to Hamlet).
            var pig = CritterRegistry.All.FirstOrDefault(b => b.Personality != null && b.Personality.CritterId == "Critter.Pig");
            Assert.That(pig, Is.Not.Null, "no pig spawned");
            Assert.That(pig.GetComponent<CritterProceduralAnim>(), Is.Not.Null, "pig should use the procedural animator");
            Assert.That(pig.GetComponentInChildren<Animator>(), Is.Null, "Tripo pig has no rig/Animator");
            var chickens = CritterRegistry.All.Where(b => b.Personality != null && b.Personality.CritterId == "Critter.Chicken").Take(3).ToList();
            Vector3 pp = pig.transform.position;
            for (int i = 0; i < chickens.Count; i++)
            {
                var ag = chickens[i].GetComponent<NavMeshAgent>();
                Vector3 p = pp + Quaternion.Euler(0, 40f + i * 50f, 0) * new Vector3(0f, 0f, 1.6f);
                if (ag != null && ag.enabled) ag.Warp(p); else chickens[i].transform.position = p;
            }
            pc.ServerTeleport(pp + new Vector3(-12f, 0.2f, -12f), Quaternion.identity);   // keep the farmer out of the shot
            yield return new WaitForSeconds(1.2f);
            var cam = new GameObject("QA Camera").AddComponent<Camera>();
            cam.fieldOfView = 45f;
            cam.transform.position = pig.transform.position + new Vector3(2.2f, 1.3f, -2.6f);
            cam.transform.LookAt(pig.transform.position + new Vector3(0.3f, 0.35f, 0.6f));
            yield return null;
            Capture(cam, Path.Combine(dir, "tripo_1_pig_chickens_pen.png"));

            // 2. Market square with the three stalls.
            cam.fieldOfView = 62f;
            cam.transform.position = new Vector3(117f, 4.2f, 2f);
            cam.transform.LookAt(new Vector3(140f, 1.2f, 0f));
            yield return null;
            Capture(cam, Path.Combine(dir, "tripo_2_market_stalls.png"));

            // 3. First-person holding the feed bucket.
            var bucket = Object.FindObjectsByType<FeedBucketTool>(FindObjectsSortMode.None).FirstOrDefault();
#if UNITY_EDITOR
            if (bucket == null)
            {
                var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Tools/FeedBucket.prefab");
                var b = Object.Instantiate(prefab, pc.transform.position + pc.transform.forward * 1.2f, Quaternion.identity);
                b.GetComponent<NetworkObject>().Spawn();
                bucket = b.GetComponent<FeedBucketTool>();
            }
#endif
            Assert.That(bucket, Is.Not.Null);
            Vector3 open = new Vector3(20f, 0f, 14f);
            pc.ServerTeleport(open, Quaternion.LookRotation(Vector3.forward));
            yield return new WaitForSeconds(0.5f);
            var brb = bucket.GetComponent<Rigidbody>(); if (brb != null) { brb.linearVelocity = Vector3.zero; }
            bucket.transform.position = pc.transform.position + pc.transform.forward * 1.1f + Vector3.up * 0.1f;
            yield return new WaitForSeconds(0.6f);
            yield return Grab(pc, bucket.transform.position + Vector3.up * 0.15f);
            Assert.That(pc.Carry.HeldObject, Is.EqualTo(bucket.transform), "bucket not grabbed");
            pc.Motor.SetLook(pc.transform.eulerAngles.y, 12f);
            yield return new WaitForSeconds(0.6f);
            Capture(pc.ViewCamera.GetComponent<Camera>(), Path.Combine(dir, "tripo_3_firstperson_feed_bucket.png"));
            pc.Carry.RequestRelease();
            yield return new WaitForSeconds(0.5f);

            // 4. A carried prize pumpkin (third-person look).
            var pumpkin = Object.FindObjectsByType<Goods>(FindObjectsSortMode.None).FirstOrDefault(g => g.GoodsId == "Crop.PrizePumpkin");
            Assert.That(pumpkin, Is.Not.Null, "no prize pumpkin in Muddlebury");
            Vector3 pk = pumpkin.transform.position;
            pc.ServerTeleport(pk + new Vector3(0f, 0.2f, -2.1f), Quaternion.LookRotation(Vector3.forward));
            yield return new WaitForSeconds(0.6f);
            yield return Grab(pc, pumpkin.transform.position + Vector3.up * 0.8f);
            var view = pc.GetComponent<PlayerView>();
            view.ForceRemoteLook = true; view.ApplyViewMode();
            yield return new WaitForSeconds(1.0f);
            cam.fieldOfView = 55f;
            cam.transform.position = pc.transform.position + pc.transform.rotation * new Vector3(3.2f, 2.2f, -2.8f);
            cam.transform.LookAt(Vector3.Lerp(pc.transform.position, pumpkin.transform.position, 0.5f) + Vector3.up * 0.8f);
            yield return null;
            Capture(cam, Path.Combine(dir, "tripo_4_carried_pumpkin.png"));
            Debug.Log($"[TripoQA] pig={pp} chickens={chickens.Count} bucketHeld={pc.Carry.HoldingTool} pumpkinHeld={pc.Carry.HeldObject == pumpkin.transform} hands={pc.Carry.HandsUsed}");
            Object.Destroy(cam.gameObject);
        }

        static IEnumerator Grab(PlayerCharacter pc, Vector3 aimAt)
        {
            pc.Motor.LookAt(aimAt);
            yield return null;
            pc.Carry.TryGrabAimed();
            float until = Time.time + 2f;
            while (!pc.Carry.IsHoldingReplicated && Time.time < until) yield return null;
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
