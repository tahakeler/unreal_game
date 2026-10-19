using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HarvestPanic.Editor
{
    /// <summary>
    /// Windowless QA capture: renders a camera into a RenderTexture and writes a PNG.
    /// Needs a GPU device, so run batchmode WITHOUT -nographics (still no window):
    /// Unity -batchmode -projectPath HarvestPanic -executeMethod HarvestPanic.Editor.QaScreenshot.CaptureTestScene -qaOut /abs/out.png -quit -logFile qa.log
    /// Optional: -qaScene Assets/Scenes/X.unity (renders its Camera.main instead of the built-in test scene), -qaWidth/-qaHeight.
    /// </summary>
    public static class QaScreenshot
    {
        public static void CaptureTestScene()
        {
            string outPath = BuildScript.Arg("-qaOut") ?? Path.GetFullPath("Logs/qa_screenshot.png");
            int w = int.TryParse(BuildScript.Arg("-qaWidth"), out var pw) ? pw : 1280;
            int h = int.TryParse(BuildScript.Arg("-qaHeight"), out var ph) ? ph : 720;
            string scenePath = BuildScript.Arg("-qaScene");

            Camera cam;
            if (scenePath != null)
            {
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                cam = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
            }
            else cam = BuildTestScene();

            if (cam == null) { Fail("No camera found"); return; }
            bool ok = Capture(cam, w, h, outPath);
            if (!ok) Fail("Capture failed");
            else Debug.Log($"[QaScreenshot] Wrote {outPath} ({w}x{h}) device={SystemInfo.graphicsDeviceType}");
        }

        /// <summary>Renders <paramref name="cam"/> off-screen and saves a PNG. Returns false on a null graphics device.</summary>
        /// <example><code>QaScreenshot.Capture(Camera.main, 1920, 1080, "/tmp/shot.png");</code></example>
        public static bool Capture(Camera cam, int width, int height, string path)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Debug.LogError("[QaScreenshot] Null graphics device (-nographics?). Run without -nographics.");
                return false;
            }
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
            rt.Create();
            var prevTarget = cam.targetTexture;
            try
            {
                var req = new RenderPipeline.StandardRequest { destination = rt };
                // Two passes: the first warms up per-material GPU data (SRP Batcher) which a
                // single cold render in batchmode does not upload, giving every object one colour.
                for (int pass = 0; pass < 2; pass++)
                {
                    if (RenderPipeline.SupportsRenderRequest(cam, req)) RenderPipeline.SubmitRenderRequest(cam, req);
                    else { cam.targetTexture = rt; cam.Render(); }
                }

                var prevActive = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();
                RenderTexture.active = prevActive;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                return true;
            }
            finally
            {
                cam.targetTexture = prevTarget;
                rt.Release();
                Object.DestroyImmediate(rt);
            }
        }

        static Camera BuildTestScene()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProjectSetup.VolumePath);
            if (profile != null) ProjectSetup.AddGlobalVolume(profile);

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.6f; sun.shadows = LightShadows.Soft;
            sun.color = new Color(1f, 0.95f, 0.85f);
            sun.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.7f, 0.9f);
            RenderSettings.ambientEquatorColor = new Color(0.5f, 0.55f, 0.45f);
            RenderSettings.ambientGroundColor = new Color(0.3f, 0.25f, 0.2f);

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            Material Mat(Color c) { var m = new Material(shader); m.SetColor("_BaseColor", c); return m; }
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.transform.localScale = Vector3.one * 3f;
            ground.GetComponent<Renderer>().sharedMaterial = Mat(new Color(0.35f, 0.6f, 0.25f));
            Color[] cols = { new Color(0.9f, 0.3f, 0.2f), new Color(0.95f, 0.75f, 0.2f), new Color(0.3f, 0.5f, 0.9f), new Color(0.6f, 0.35f, 0.2f) };
            PrimitiveType[] types = { PrimitiveType.Cube, PrimitiveType.Sphere, PrimitiveType.Capsule, PrimitiveType.Cylinder };
            for (int i = 0; i < 4; i++)
            {
                var g = GameObject.CreatePrimitive(types[i]);
                g.transform.position = new Vector3(-3f + i * 2f, 0.5f + (i == 2 ? 0.5f : 0f), 0f);
                g.GetComponent<Renderer>().sharedMaterial = Mat(cols[i]);
            }

            var cam = new GameObject("QA Camera").AddComponent<Camera>();
            cam.transform.position = new Vector3(0f, 3f, -7f);
            cam.transform.LookAt(new Vector3(0f, 0.5f, 0f));
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.55f, 0.75f, 0.95f);
            cam.gameObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
            return cam;
        }

        static void Fail(string msg)
        {
            Debug.LogError("[QaScreenshot] " + msg);
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }
}
