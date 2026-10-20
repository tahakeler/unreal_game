using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HarvestPanic.Tests.PlayMode.Livestock
{
    /// <summary>Polish-2 QA: goat vs pig silhouette side-on and 3/4 (set HP_QA_DIR, run with a GPU). Also checks the goat parts exist.</summary>
    public class GoatScreenshots
    {
        [Test]
        public void GoatPrefab_HasHornsBeardAndShins()
        {
#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Critters/Critter_Goat.prefab");
            Assert.That(prefab, Is.Not.Null);
            int horns = 0, beard = 0, shins = 0;
            foreach (var t in prefab.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.StartsWith("Horn")) horns++;
                if (t.name == "Beard") beard++;
                if (t.name == "Shin") shins++;
                if (t.name == "Shin" || t.name == "Beard" || t.name.StartsWith("Horn")) Assert.That(t.GetComponent<Collider>(), Is.Null, "silhouette parts are colliderless");
            }
            Assert.AreEqual(4, horns); Assert.AreEqual(1, beard); Assert.AreEqual(4, shins);
#else
            Assert.Ignore("editor only");
#endif
        }

        [UnityTest]
        public IEnumerator Capture_Goat_Silhouette()
        {
            string dir = Environment.GetEnvironmentVariable("HP_QA_DIR");
            if (string.IsNullOrEmpty(dir) || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("QA screenshot; set HP_QA_DIR and run with a GPU");
#if UNITY_EDITOR
            var goat = Make("Assets/Prefabs/Critters/Critter_Goat.prefab", new Vector3(0f, 0f, 0f), 90f);
            var pig = Make("Assets/Prefabs/Critters/Critter_Pig.prefab", new Vector3(0f, 0f, -1.8f), 90f);
            var sun = new GameObject("Sun").AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity = 1.4f;
            sun.transform.rotation = Quaternion.Euler(40f, -30f, 0f);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane); ground.transform.localScale = Vector3.one * 3f;
            var gm = ground.GetComponent<Renderer>().material; gm.color = new Color(0.35f, 0.5f, 0.25f); gm.SetColor("_BaseColor", gm.color);
            var cam = new GameObject("QACam").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.62f, 0.75f, 0.88f); cam.fieldOfView = 40f;
            yield return null;
            Directory.CreateDirectory(dir);
            Shot(cam, new Vector3(3.6f, 0.9f, -0.9f), new Vector3(0f, 0.4f, -0.9f), Path.Combine(dir, "goat_side.png"));
            Shot(cam, new Vector3(1.4f, 1.0f, 1.6f), new Vector3(0f, 0.45f, 0.1f), Path.Combine(dir, "goat_front34.png"));
            Object.Destroy(goat); Object.Destroy(pig); Object.Destroy(cam.gameObject); Object.Destroy(sun.gameObject); Object.Destroy(ground);
#endif
            yield return null;
        }

#if UNITY_EDITOR
        static GameObject Make(string path, Vector3 at, float yaw)
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var inst = Object.Instantiate(prefab, at, Quaternion.Euler(0f, yaw - 90f, 0f));
            foreach (var b in inst.GetComponentsInChildren<Behaviour>(true)) if (!(b is Animator)) b.enabled = false;
            foreach (var rb in inst.GetComponentsInChildren<Rigidbody>(true)) rb.isKinematic = true;
            return inst;
        }

        static void Shot(Camera cam, Vector3 eye, Vector3 at, string file)
        {
            cam.transform.position = eye; cam.transform.LookAt(at);
            var rt = new RenderTexture(960, 540, 24); cam.targetTexture = rt; cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(960, 540, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); tex.Apply();
            File.WriteAllBytes(file, tex.EncodeToPNG());
            RenderTexture.active = null; cam.targetTexture = null; Object.Destroy(rt); Object.Destroy(tex);
        }
#endif
    }
}
