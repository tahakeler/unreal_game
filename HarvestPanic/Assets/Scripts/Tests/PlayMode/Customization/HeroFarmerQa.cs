using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarvestPanic.Customization;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HarvestPanic.Tests.PlayMode.Customization
{
    /// <summary>
    /// Owner-facing QA sheets of the hero farmer (set HP_QA_DIR; run with a GPU, skipped otherwise), in the wardrobe test
    /// scene's neutral light:
    /// hero_original_vs_ingame.png (the Tripo model on URP/Lit with its own texture next to the in-game body on the default
    /// loadout, full body + face, plus a difference image), hero_colourways_8.png (default + 7, bodies and faces) and
    /// hero_pose_sheet.png (every clip of the body controller, front + side).
    /// </summary>
    public class HeroFarmerQa
    {
        const string ScenePath = "Assets/Scenes/Dev/WardrobeTest.unity";
        const int TileW = 300, TileH = 400;

        static readonly FarmerLoadout[] Ways =
        {
            FarmerLoadout.Default,
            new FarmerLoadout { Primary = 0, Secondary = 14, Accent = 17, Skin = 2, NameTag = 0, HairColor = 17, Hat = 1 },
            new FarmerLoadout { Primary = 1, Secondary = 15, Accent = 13, Skin = 10, NameTag = 1, HairColor = 13, Hat = 1 },
            new FarmerLoadout { Primary = 2, Secondary = 20, Accent = 12, Skin = 0, NameTag = 2, HairColor = 11, Hat = 1 },
            new FarmerLoadout { Primary = 3, Secondary = 19, Accent = 9, Skin = 7, NameTag = 3, HairColor = 20, Hat = 1 },
            new FarmerLoadout { Primary = 13, Secondary = 23, Accent = 15, Skin = 4, NameTag = 4, HairColor = 15, Hat = 1 },
            new FarmerLoadout { Primary = 15, Secondary = 12, Accent = 4, Skin = 11, NameTag = 5, HairColor = 13, Hat = 1 },
            new FarmerLoadout { Primary = 8, Secondary = 7, Accent = 21, Skin = 8, NameTag = 6, HairColor = 18, Hat = 1 },
        };

        string _dir;

        IEnumerator Setup()
        {
            _dir = Environment.GetEnvironmentVariable("HP_QA_DIR");
            if (string.IsNullOrEmpty(_dir) || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("QA sheet; set HP_QA_DIR and run with a GPU");
            Directory.CreateDirectory(_dir);
            LogAssert.ignoreFailingMessages = true;
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#endif
            yield return null;
            var lineup = Object.FindAnyObjectByType<WardrobeTestLineup>();
            if (lineup != null) lineup.gameObject.SetActive(false);
            foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) c.enabled = false;
        }

        static FarmerVisual SpawnBody(FarmerLoadout l)
        {
#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Farmer/FarmerBody.prefab");
            var go = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity);
            WardrobePreview.StripNetworking(go);
            var v = go.GetComponent<FarmerVisual>();
            v.Apply(l);
            if (v.NameTag != null) v.NameTag.gameObject.SetActive(false);
            v.enabled = false; // posed by the sheet, not by locomotion
            return v;
#else
            return null;
#endif
        }

        /// <summary>Freezes <paramref name="anim"/> on <paramref name="clip"/> at <paramref name="time"/> (controller off, no foot IK).</summary>
        static void Pose(Animator anim, AnimationClip clip, float time)
        {
            anim.enabled = true;
            anim.runtimeAnimatorController = null;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var g = PlayableGraph.Create("QaPose");
            g.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var o = AnimationPlayableOutput.Create(g, "o", anim);
            var p = AnimationClipPlayable.Create(g, clip);
            p.SetApplyFootIK(false);
            o.SetSourcePlayable(p);
            p.SetTime(time);
            g.Evaluate(0f);
            anim.enabled = false; // keep the sampled pose
            g.Destroy();
        }

        static Camera Cam()
        {
            var cam = new GameObject("QA Cam").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.86f, 0.83f, 0.76f);
            cam.nearClipPlane = 0.05f; cam.farClipPlane = 60f;
            return cam;
        }

        static void Aim(Camera cam, Vector3 target, float yawDeg, float dist, float fov)
        {
            cam.fieldOfView = fov;
            var dir = Quaternion.Euler(0f, yawDeg, 0f) * Vector3.forward;
            cam.transform.position = target + dir * dist + Vector3.up * 0.08f * dist;
            cam.transform.LookAt(target);
        }

        static Texture2D Grab(Camera cam, int w, int h)
        {
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 4;
            var req = new RenderPipeline.StandardRequest { destination = rt };
            for (int i = 0; i < 2; i++)
            {
                if (RenderPipeline.SupportsRenderRequest(cam, req)) RenderPipeline.SubmitRenderRequest(cam, req);
                else { cam.targetTexture = rt; cam.Render(); cam.targetTexture = null; }
            }
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            return tex;
        }

        sealed class Sheet
        {
            readonly Texture2D _t;
            readonly int _cols, _w, _h;
            public Sheet(int cols, int rows, int w, int h)
            {
                _cols = cols; _w = w; _h = h;
                _t = new Texture2D(cols * w, rows * h, TextureFormat.RGBA32, false);
                var fill = Enumerable.Repeat(new Color32(240, 236, 228, 255), cols * w * rows * h).ToArray();
                _t.SetPixels32(fill);
            }
            public void Put(int index, Texture2D tile)
            {
                int cx = index % _cols, cy = index / _cols, rows = _t.height / _h;
                _t.SetPixels32(cx * _w, (rows - 1 - cy) * _h, _w, _h, tile.GetPixels32());
                Object.Destroy(tile);
            }
            public void Save(string path) { _t.Apply(); File.WriteAllBytes(path, _t.EncodeToPNG()); Debug.Log("[HeroQA] wrote " + path); }
        }

        static Vector3 HeadPoint(FarmerVisual v)
        {
            var anim = v.BodyAnimator;
            var head = anim != null && anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.Head) : null;
            return head != null ? head.position + Vector3.up * 0.2f : v.transform.position + Vector3.up * 1.4f;
        }

        [UnityTest]
        public IEnumerator Capture_OriginalModel_vs_InGame_DefaultLoadout()
        {
            yield return Setup();
#if UNITY_EDITOR
            var body = SpawnBody(FarmerLoadout.Default);
            yield return null;
            var idle = body.BodyAnimator.runtimeAnimatorController.animationClips.First(c => c.name == "Idle");
            Pose(body.BodyAnimator, idle, 0f);

            // The owner's model as delivered: same mesh and texture on URP/Lit, no recolour.
            var model = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ThirdParty/Tripo/SK_Farmer_Hero/SK_Farmer_Hero.fbx");
            var orig = Object.Instantiate(model, Vector3.zero, Quaternion.Euler(0f, 180f, 0f)); // the export faces -Z
            var lit = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            lit.SetTexture("_BaseMap", UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ThirdParty/Tripo/SK_Farmer_Hero/SK_Farmer_Hero_BaseColor.png"));
            lit.SetColor("_BaseColor", Color.white);
            lit.SetFloat("_Smoothness", 0.12f);
            foreach (var r in orig.GetComponentsInChildren<Renderer>()) r.sharedMaterial = lit;
            Pose(orig.GetComponentInChildren<Animator>(), idle, 0f);
            var cam = Cam();
            var shots = new List<Texture2D>();
            Vector3 headAt = HeadPoint(body);
            foreach (var show in new[] { orig, body.gameObject })
            {
                orig.SetActive(show == orig); body.gameObject.SetActive(show == body.gameObject);
                yield return null;
                Aim(cam, new Vector3(0f, 0.9f, 0f), 0f, 4.6f, 30f); shots.Add(Grab(cam, TileW, TileH));
                Aim(cam, headAt - Vector3.up * 0.12f, 0f, 1.5f, 30f); shots.Add(Grab(cam, TileW, TileH));
            }
            // difference images (x4) of the full body and the face
            var diffs = new List<Texture2D>();
            float worst = 0f, meanSum = 0f; int n = 0;
            for (int k = 0; k < 2; k++)
            {
                var a = shots[k].GetPixels32(); var b = shots[2 + k].GetPixels32();
                var d = new Color32[a.Length];
                for (int i = 0; i < a.Length; i++)
                {
                    int dr = Mathf.Abs(a[i].r - b[i].r), dg = Mathf.Abs(a[i].g - b[i].g), db = Mathf.Abs(a[i].b - b[i].b);
                    int m = Mathf.Max(dr, Mathf.Max(dg, db));
                    worst = Mathf.Max(worst, m); meanSum += m; n++;
                    byte v = (byte)Mathf.Min(255, m * 4);
                    d[i] = new Color32(v, v, v, 255);
                }
                var t = new Texture2D(TileW, TileH, TextureFormat.RGBA32, false); t.SetPixels32(d); t.Apply();
                diffs.Add(t);
            }
            var sheet = new Sheet(3, 2, TileW, TileH);
            sheet.Put(0, shots[0]); sheet.Put(1, shots[2]); sheet.Put(2, diffs[0]);
            sheet.Put(3, shots[1]); sheet.Put(4, shots[3]); sheet.Put(5, diffs[1]);
            sheet.Save(Path.Combine(_dir, "hero_original_vs_ingame.png"));
            Debug.Log($"[HeroQA] original vs in-game: max channel diff {worst}/255, mean {meanSum / n:F2}/255");
            Assert.That(meanSum / n, Is.LessThan(3f), "default loadout should render like the painted model");
#endif
            yield return null;
        }

        [UnityTest]
        public IEnumerator Capture_EightColourways_BodiesAndFaces()
        {
            yield return Setup();
#if UNITY_EDITOR
            var cam = Cam();
            var sheet = new Sheet(Ways.Length, 2, 240, 320);
            for (int i = 0; i < Ways.Length; i++)
            {
                var v = SpawnBody(Ways[i]);
                yield return null;
                Pose(v.BodyAnimator, v.BodyAnimator.runtimeAnimatorController.animationClips.First(c => c.name == "Idle"), 0f);
                Aim(cam, new Vector3(0f, 0.9f, 0f), 12f, 4.8f, 30f); sheet.Put(i, Grab(cam, 240, 320));
                Aim(cam, HeadPoint(v) - Vector3.up * 0.12f, 8f, 1.3f, 30f); sheet.Put(Ways.Length + i, Grab(cam, 240, 320));
                Object.Destroy(v.gameObject);
                yield return null;
            }
            sheet.Save(Path.Combine(_dir, "hero_colourways_8.png"));
#endif
            yield return null;
        }

        [UnityTest]
        public IEnumerator Capture_FaceItemsAndHats_OnTheHero()
        {
            yield return Setup();
#if UNITY_EDITOR
            var cam = Cam();
            var probe = SpawnBody(FarmerLoadout.Default);
            var cat = probe.Catalog;
            Object.Destroy(probe.gameObject);
            var looks = new List<FarmerLoadout>();
            foreach (var f in cat.Items(CosmeticSlot.Face)) { var l = FarmerLoadout.Default; l.Face = f.id; looks.Add(l); }
            foreach (var h in cat.Items(CosmeticSlot.Hat)) { var l = FarmerLoadout.Default; l.Hat = h.id; looks.Add(l); }
            const int cols = 6;
            var sheet = new Sheet(cols, Mathf.CeilToInt(looks.Count / (float)cols), 240, 260);
            for (int i = 0; i < looks.Count; i++)
            {
                var v = SpawnBody(looks[i]);
                yield return null;
                Pose(v.BodyAnimator, v.BodyAnimator.runtimeAnimatorController.animationClips.First(c => c.name == "Idle"), 0f);
                Aim(cam, HeadPoint(v), 25f, 2.4f, 30f);
                sheet.Put(i, Grab(cam, 240, 260));
                Object.Destroy(v.gameObject);
                yield return null;
            }
            sheet.Save(Path.Combine(_dir, "hero_face_items_and_hats.png"));
#endif
            yield return null;
        }

        [UnityTest]
        public IEnumerator Capture_PoseSheet_EveryClip_FrontAndSide()
        {
            yield return Setup();
#if UNITY_EDITOR
            var v = SpawnBody(FarmerLoadout.Default);
            yield return null;
            var clips = v.BodyAnimator.runtimeAnimatorController.animationClips.Distinct().OrderBy(c => c.name).ToList();
            var anim = v.BodyAnimator;
            var cam = Cam();
            var label = new GameObject("Label").AddComponent<TextMesh>();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.font = font; label.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            label.fontSize = 64; label.characterSize = 0.03f; label.anchor = TextAnchor.LowerCenter; label.color = new Color(0.2f, 0.15f, 0.1f);
            const int cols = 6;
            int rows = Mathf.CeilToInt(clips.Count * 2f / cols);
            var sheet = new Sheet(cols, rows, 220, 300);
            int tile = 0;
            foreach (var clip in clips)
            {
                bool oneShot = !clip.isLooping;
                float t = clip.name.Contains("Faint") || clip.name == "Down" ? clip.length * 0.95f : clip.length * (oneShot ? 0.55f : 0.3f);
                Pose(anim, clip, t);
                yield return null;
                label.text = clip.name.Replace("Farmer_", "");
                foreach (float yaw in new[] { 0f, 90f })
                {
                    var dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                    label.transform.position = new Vector3(0f, 0.02f, 0f) + dir * 0.9f;
                    label.transform.rotation = Quaternion.LookRotation(-dir);
                    Aim(cam, new Vector3(0f, 0.85f, 0f), yaw, 5.2f, 30f);
                    sheet.Put(tile++, Grab(cam, 220, 300));
                }
            }
            sheet.Save(Path.Combine(_dir, "hero_pose_sheet.png"));
#endif
            yield return null;
        }
    }
}
