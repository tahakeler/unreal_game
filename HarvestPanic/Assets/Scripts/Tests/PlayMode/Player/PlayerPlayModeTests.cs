using System;
using System.Collections;
using System.IO;
using HarvestPanic.Core;
using HarvestPanic.Gameplay;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HarvestPanic.Tests.PlayMode.Player
{
    /// <summary>
    /// End-to-end player slice in Scenes/Dev/PlayerTest: host spawns the networked player, which grabs the
    /// 2-hand test crate, carries it, throws it; plus holler noise, ragdoll/KO and barn respawn.
    /// Set env HP_QA_DIR to also write the first-person and third-person screenshots (run without -nographics).
    /// </summary>
    public class PlayerPlayModeTests
    {
        const string ScenePath = "Assets/Scenes/Dev/PlayerTest.unity";

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            var nm = NetworkManager.Singleton;
            if (nm != null)
            {
                nm.Shutdown();
                yield return null;
                Object.Destroy(nm.gameObject);
            }
            yield return null;
        }

        IEnumerator LoadAndSpawn()
        {
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            Assert.Ignore("Editor-only test");
#endif
            float until = Time.realtimeSinceStartup + 15f;
            while (PlayerCharacter.Local == null && Time.realtimeSinceStartup < until) yield return null;
            Assert.That(PlayerCharacter.Local, Is.Not.Null, "host player did not spawn");
            Assert.That(NetworkManager.Singleton.IsHost, Is.True);
            while (FindProp(2) == null && Time.realtimeSinceStartup < until) yield return null;
            yield return new WaitForSeconds(0.5f); // props settle
        }

        static CarryableProp FindProp(int hands)
        {
            foreach (var p in Object.FindObjectsByType<CarryableProp>(FindObjectsSortMode.None))
                if (p.HandsRequired == hands && p.GetType() == typeof(CarryableProp)) return p;
            return null;
        }

        [UnityTest]
        public IEnumerator HostPlayer_GrabsCarriesAndThrowsCrate()
        {
            yield return LoadAndSpawn();
            var pc = PlayerCharacter.Local;
            var carry = pc.Carry;
            var crate = FindProp(2);
            Assert.That(crate, Is.Not.Null);

            // Aim and grab.
            pc.Motor.LookAt(crate.transform.position + Vector3.up * 0.25f);
            yield return null;
            Assert.That(carry.TryAim(out var aimed, out _, out _), Is.True, "crate not under the centre ray");
            Assert.That(ReferenceEquals(aimed, crate), Is.True);
            Assert.That(carry.TryGrabAimed(), Is.True);
            float until = Time.time + 2f;
            while (carry.HeldId != crate.NetworkObjectId && Time.time < until) yield return null;
            Assert.That(carry.HeldId, Is.EqualTo(crate.NetworkObjectId), "grab not confirmed");
            Assert.That(carry.HandsUsed, Is.EqualTo(2));

            pc.Motor.SetLook(pc.transform.eulerAngles.y, 8f);
            yield return new WaitForSeconds(1.0f);
            Assert.That(crate.transform.position.y, Is.GreaterThan(0.55f), "2-hand crate should be lifted by one player");
            Assert.That(carry.SpeedMultiplier, Is.EqualTo(CarryMath.SpeedMultiplier(12f, 2, 2)).Within(0.01f));

            // Carry it forward.
            Vector3 start = pc.transform.position;
            pc.Input.UseScripted = true;
            pc.Input.Scripted.Move = new Vector2(0f, 1f);
            for (int i = 0; i < 6; i++)
            {
                yield return new WaitForSeconds(0.25f);
                var cc = pc.GetComponent<CharacterController>();
                Debug.Log($"[PlayerQA] pos={pc.transform.position} ccEnabled={cc.enabled} ccVel={cc.velocity} grounded={cc.isGrounded} control={pc.HasControl} move={pc.Input.Move} mult={carry.SpeedMultiplier} crate={crate.transform.position} state={pc.State}");
            }
            pc.Input.Scripted.Move = Vector2.zero;
            yield return new WaitForSeconds(0.6f);
            Assert.That(Vector3.Distance(start, pc.transform.position), Is.GreaterThan(2f), "player did not walk");
            Assert.That(Vector3.Distance(crate.transform.position, pc.HeadPivot.position), Is.LessThan(2.2f), "crate did not follow");

            yield return Screenshots(pc);

            // Throw at full charge.
            Vector3 throwFrom = crate.transform.position;
            var rb = crate.GetComponent<Rigidbody>();
            carry.RequestThrow(1f);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.That(carry.HeldId, Is.EqualTo(PlayerCarry.NoObject));
            Assert.That(rb.linearVelocity.magnitude, Is.GreaterThan(8f), "throw speed");
            yield return new WaitForSeconds(1.2f);
            Assert.That(Vector3.Distance(throwFrom, crate.transform.position), Is.GreaterThan(4f), "crate should fly");
        }

        [UnityTest]
        public IEnumerator Holler_Ragdoll_Knockout_AndBarnRespawn()
        {
            yield return LoadAndSpawn();
            var pc = PlayerCharacter.Local;

            // Holler -> Shout noise 0.8.
            Assert.That(Services.TryGet(out INoiseService noise), Is.True, "NoiseService not registered");
            NoiseEvent? heard = null;
            Action<NoiseEvent> h = e => { if (e.Source == NoiseSource.Shout) heard = e; };
            noise.OnNoise += h;
            pc.Holler();
            float until = Time.time + 1f;
            while (heard == null && Time.time < until) yield return null;
            noise.OnNoise -= h;
            Assert.That(heard.HasValue, Is.True, "no shout noise");
            Assert.That(heard.Value.Loudness, Is.EqualTo(0.8f).Within(0.05f));
            Assert.That(heard.Value.BlamedPlayerId, Is.EqualTo(pc.OwnerClientId));

            // Below threshold: nothing. Above: ragdoll. 2x: knockout.
            pc.ApplyImpulse(Vector3.forward * 500f, PlayerIds.Nature, BlameVerb.Crashed);
            yield return null;
            Assert.That(pc.State, Is.EqualTo(PlayerState.Normal));
            pc.ApplyImpulse(Vector3.forward * 1000f, PlayerIds.Nature, BlameVerb.Crashed);
            yield return null; yield return null;
            Assert.That(pc.State, Is.EqualTo(PlayerState.Ragdoll));
            Assert.That(pc.IsDown, Is.True);
            yield return new WaitForSeconds(0.5f);
            pc.ApplyImpulse(Vector3.right * 1900f, PlayerIds.Nature, BlameVerb.Crashed);
            yield return null; yield return null;
            Assert.That(pc.State, Is.EqualTo(PlayerState.KnockedOut));

            // Sent to the barn: Respawning for RespawnDelay, then Normal at the BarnRespawn marker.
            ((IKnockable)pc).SendToBarn(BlameVerb.KnockedOut, true);
            yield return null;
            Assert.That(pc.State, Is.EqualTo(PlayerState.Respawning));
            yield return new WaitForSeconds(pc.Tuning.RespawnDelay + 0.6f);
            Assert.That(pc.State, Is.EqualTo(PlayerState.Normal));
            var barn = LevelMarker.FindAll(MarkerRole.BarnRespawn)[0].transform.position;
            Assert.That(Vector3.Distance(barn, pc.transform.position), Is.LessThan(1.5f), "not at barn");
        }

        // ---- QA screenshots ------------------------------------------------------------------------------

        static IEnumerator Screenshots(PlayerCharacter pc)
        {
            string dir = Environment.GetEnvironmentVariable("HP_QA_DIR");
            if (string.IsNullOrEmpty(dir) || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) yield break;
            yield return null; yield return null;
            var fpCam = pc.ViewCamera.GetComponent<Camera>();
            Capture(fpCam, Path.Combine(dir, "player_fp_holding_crate.png"));

            var view = pc.GetComponent<PlayerView>();
            view.ForceRemoteLook = true;
            view.ApplyViewMode();
            yield return null;
            var qa = new GameObject("QA Camera").AddComponent<Camera>();
            qa.fieldOfView = 55f;
            Vector3 chest = pc.transform.position + Vector3.up * 1.2f;
            qa.transform.position = chest + pc.transform.rotation * new Vector3(2.4f, 0.6f, 2.6f);
            qa.transform.LookAt(chest + pc.transform.forward * 0.6f);
            yield return null; yield return null;
            Capture(qa, Path.Combine(dir, "player_third_person_body.png"));
            Object.Destroy(qa.gameObject);
            view.ForceRemoteLook = false;
            view.ApplyViewMode();
        }

        static void Capture(Camera cam, string path)
        {
            const int w = 1280, h = 720;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            rt.Create();
            var req = new RenderPipeline.StandardRequest { destination = rt };
            for (int i = 0; i < 2; i++)
            {
                if (RenderPipeline.SupportsRenderRequest(cam, req)) RenderPipeline.SubmitRenderRequest(cam, req);
                else { cam.targetTexture = rt; cam.Render(); cam.targetTexture = null; }
            }
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.Destroy(tex);
            rt.Release();
            Object.Destroy(rt);
            Debug.Log("[PlayerQA] wrote " + path);
        }
    }
}
