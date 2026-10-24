using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using HarvestPanic.AI;
using HarvestPanic.AI.Crops;
using HarvestPanic.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HarvestPanic.Tests.PlayMode.Livestock
{
    /// <summary>
    /// QA captures for feature/sheep-cursed-crops (set HP_QA_DIR; run without -nographics, skipped otherwise):
    /// the sheep flock and both cursed-crop fields in Muddlebury (offline load: spawners and fields self-run), plus a
    /// studio line-up (sheep vs goat silhouette, mandrake planted/pulled, tumble melon).
    /// </summary>
    public class SheepCropScreenshots
    {
        static string Dir()
        {
            string dir = Environment.GetEnvironmentVariable("HP_QA_DIR");
            if (string.IsNullOrEmpty(dir) || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("QA screenshot; set HP_QA_DIR and run with a GPU");
            Directory.CreateDirectory(dir);
            return dir;
        }

        [UnityTest]
        public IEnumerator Capture_Muddlebury_FlockAndCursedFields()
        {
            string dir = Dir();
            LogAssert.ignoreFailingMessages = true; // offline load of the full level: other systems may complain without a session
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Muddlebury.unity", new LoadSceneParameters(LoadSceneMode.Single));
#endif
            yield return new WaitForSeconds(2.5f);
            var sheep = new List<CritterBrain>();
            foreach (var b in CritterRegistry.All) if (b != null && b.Personality != null && b.Personality.CritterId == "Critter.Sheep") sheep.Add(b);
            Assert.AreEqual(5, sheep.Count, "sheep flock spawned from the Muddlebury marker");
            Assert.AreEqual(2, CursedCropField.All.Count, "two cursed-crop fields in Muddlebury");
            foreach (var f in CursedCropField.All) Assert.AreEqual(f.Count, f.PlantedCount, f.GoodsId + " planted (no day services offline)");

            var cam = new GameObject("QA Cam").AddComponent<Camera>();
            cam.fieldOfView = 50f; cam.farClipPlane = 600f;

            // 1. flock grazing around its bell-wether.
            Vector3 c = Vector3.zero; foreach (var s in sheep) c += s.transform.position; c /= sheep.Count;
            Shot(cam, c + new Vector3(4.5f, 2.4f, -5.5f), c + Vector3.up * 0.4f, Path.Combine(dir, "sheep_1_flock_muddlebury.png"));

            // 2. flock stampede: shout next to it (noise dispatched straight to the brains).
            CritterRegistry.Dispatch(new NoiseEvent { Source = NoiseSource.Shout, Loudness = 1f, Radius = 30f, Location = c + new Vector3(-2.5f, 0f, -2.5f), Tags = NoiseTag.Scream });
            yield return new WaitForSeconds(1.6f);
            Vector3 c2 = Vector3.zero; foreach (var s in sheep) c2 += s.transform.position; c2 /= sheep.Count;
            Shot(cam, c2 + new Vector3(-6f, 3.5f, -6f), c2 + Vector3.up * 0.3f, Path.Combine(dir, "sheep_2_flock_stampede.png"));

            // 3. mandrake field: one pulled and screaming in the air, the rest staring out of the soil.
            CursedCropField mf = null, melons = null;
            foreach (var f in CursedCropField.All) { if (f.GoodsId == "Crop.Mandrake") mf = f; else melons = f; }
            var pulled = mf.Crops[0];
            pulled.OnGrabbed(1);
            var rb = pulled.GetComponent<Rigidbody>(); rb.isKinematic = true;
            Vector3 fc = mf.transform.position;
            pulled.transform.position = fc + new Vector3(0.2f, 0.9f, -2.6f);
            pulled.transform.rotation = Quaternion.Euler(0f, 160f, -12f);
            yield return null;
            Shot(cam, fc + new Vector3(1.0f, 1.6f, -5.0f), fc + new Vector3(0f, 0.5f, -1.2f), Path.Combine(dir, "crops_1_mandrake_field.png"));

            // 4. tumble melons by the pumpkin patch.
            Vector3 mc = melons.transform.position;
            Shot(cam, mc + new Vector3(1.5f, 1.6f, -3.6f), mc + Vector3.up * 0.3f, Path.Combine(dir, "crops_2_tumble_melons.png"));
            Object.Destroy(cam.gameObject);
            LogAssert.ignoreFailingMessages = false;
        }

        [UnityTest]
        public IEnumerator Capture_Studio_SheepGoatAndCrops()
        {
            string dir = Dir();
#if UNITY_EDITOR
            CritterSpawnDirector.AutoSpawn = false;
            var scene = SceneManager.CreateScene("QA Studio " + Time.frameCount);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
                if (SceneManager.GetSceneAt(i) != scene) yield return SceneManager.UnloadSceneAsync(SceneManager.GetSceneAt(i));
            SceneManager.SetActiveScene(scene);
            var root = new GameObject("Studio");
            var sun = new GameObject("Sun").AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity = 1.4f;
            sun.transform.SetParent(root.transform); sun.transform.rotation = Quaternion.Euler(40f, -30f, 0f);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane); ground.transform.SetParent(root.transform); ground.transform.localScale = Vector3.one * 3f;
            var gm = ground.GetComponent<Renderer>().material; gm.SetColor("_BaseColor", new Color(0.35f, 0.5f, 0.25f));
            Make("Assets/Prefabs/Critters/Critter_Sheep.prefab", new Vector3(0f, 0f, 0f), 90f, root, showBell: true);
            Make("Assets/Prefabs/Critters/Critter_Goat.prefab", new Vector3(0f, 0f, -1.6f), 90f, root);
            var m1 = Make("Assets/Prefabs/Critters/Crops/Crop_Mandrake.prefab", new Vector3(-1.5f, 0f, 0.5f), 100f, root);
            var m2 = Make("Assets/Prefabs/Critters/Crops/Crop_Mandrake.prefab", new Vector3(-1.0f, -0.25f, 1.0f), 75f, root);
            var melon = Make("Assets/Prefabs/Critters/Crops/Crop_TumbleMelon.prefab", new Vector3(-2.3f, 0f, -0.5f), 110f, root);
            var cam = new GameObject("QACam").AddComponent<Camera>(); cam.transform.SetParent(root.transform);
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.62f, 0.75f, 0.88f); cam.fieldOfView = 40f;
            yield return null;
            Shot(cam, new Vector3(3.8f, 0.9f, -0.8f), new Vector3(0f, 0.45f, -0.8f), Path.Combine(dir, "sheep_3_vs_goat_side.png"));
            Shot(cam, new Vector3(-1.5f, 1.0f, 3.3f), new Vector3(-1.6f, 0.3f, 0.2f), Path.Combine(dir, "crops_3_studio.png"));
            Object.Destroy(root);
            CritterSpawnDirector.AutoSpawn = true;
#endif
            yield return null;
        }

#if UNITY_EDITOR
        static GameObject Make(string path, Vector3 at, float yaw, GameObject parent, bool showBell = false)
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.NotNull(prefab, path);
            var inst = Object.Instantiate(prefab, at, Quaternion.Euler(0f, yaw - 90f, 0f), parent.transform);
            foreach (var b in inst.GetComponentsInChildren<Behaviour>(true)) if (!(b is Animator)) b.enabled = false;
            foreach (var rb in inst.GetComponentsInChildren<Rigidbody>(true)) rb.isKinematic = true;
            if (showBell) { var bell = inst.transform.Find("Bell"); if (bell != null) bell.gameObject.SetActive(true); }
            return inst;
        }
#endif

        static void Shot(Camera cam, Vector3 eye, Vector3 at, string file)
        {
            cam.transform.position = eye; cam.transform.LookAt(at);
            var rt = new RenderTexture(1280, 720, 24); cam.targetTexture = rt; cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply();
            File.WriteAllBytes(file, tex.EncodeToPNG());
            RenderTexture.active = null; cam.targetTexture = null; Object.Destroy(rt); Object.Destroy(tex);
        }
    }
}
