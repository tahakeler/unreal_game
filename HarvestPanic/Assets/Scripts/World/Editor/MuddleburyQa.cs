using System;
using System.IO;
using HarvestPanic.Core;
using HarvestPanic.Weather;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HarvestPanic.World.Editor
{
    /// <summary>
    /// Windowless look-dev capture of every camera under "QA_Cameras" in Muddlebury. Needs a GPU, so run batchmode
    /// WITHOUT -nographics (no window opens):
    /// Unity -batchmode -projectPath HarvestPanic -executeMethod HarvestPanic.World.Editor.MuddleburyQa.CaptureAll -qaOutDir /abs/dir -quit -logFile /abs/qa.log
    /// Options: -qaCams 01,04 (name prefixes), -qaLook Dusk|Night (WeatherLook phase applied like WeatherPresenter, windows lit).
    /// </summary>
    public static class MuddleburyQa
    {
        public static void CaptureAll()
        {
            string outDir = Arg("-qaOutDir") ?? Path.GetFullPath("../Logs/muddlebury_qa");
            int w = int.TryParse(Arg("-qaWidth"), out var pw) ? pw : 1280;
            int h = int.TryParse(Arg("-qaHeight"), out var ph) ? ph : 720;
            string[] only = Arg("-qaCams")?.Split(',');
            string look = Arg("-qaLook");
            Directory.CreateDirectory(outDir);
            EditorSceneManager.OpenScene(BuildMuddlebury.ScenePath, OpenSceneMode.Single);
            var root = GameObject.Find("QA_Cameras");
            if (root == null || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Debug.LogError("[MuddleburyQa] no QA_Cameras or null graphics device (-nographics?)");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }
            LitText.ApplyAll();
            string suffix = "";
            if (look == "Dusk" || look == "Night")
            {
                ApplyLook(WeatherLook.ForPhase(DayClockPhase.Dusk, look == "Dusk" ? 0.12f : 0.8f));
                NightWindows.ForceLit = true;
                foreach (var nw in UnityEngine.Object.FindObjectsByType<NightWindows>(FindObjectsSortMode.None)) nw.Refresh();
                suffix = "_" + look;
            }
            int n = 0;
            foreach (var cam in root.GetComponentsInChildren<Camera>(true))
            {
                if (only != null && Array.FindIndex(only, p => cam.name.StartsWith(p)) < 0) continue;
                cam.enabled = true;
                Render(cam, w, h, Path.Combine(outDir, cam.name + suffix + ".png"));
                cam.enabled = false;
                n++;
            }
            NightWindows.ForceLit = null;
            Debug.Log($"[MuddleburyQa] wrote {n} captures to {outDir}");
        }

        /// <summary>Mirror of WeatherPresenter.Apply for an editor capture (the scene is never saved).</summary>
        static void ApplyLook(WeatherLook l)
        {
            var sun = RenderSettings.sun;
            if (sun != null)
            {
                var e = sun.transform.rotation.eulerAngles;
                sun.transform.rotation = Quaternion.Euler(l.SunPitch, e.y, 0f);
                sun.color = l.SunColor; sun.intensity = l.SunIntensity;
            }
            RenderSettings.ambientSkyColor *= l.Ambient; RenderSettings.ambientEquatorColor *= l.Ambient; RenderSettings.ambientGroundColor *= l.Ambient;
            RenderSettings.fogColor = l.FogColor; RenderSettings.fogStartDistance = l.FogStart; RenderSettings.fogEndDistance = l.FogEnd;
            if (RenderSettings.skybox != null)
            {
                var sky = new Material(RenderSettings.skybox);
                sky.SetColor("_SkyTint", l.SkyTint); sky.SetFloat("_Exposure", l.SkyExposure);
                sky.SetColor("_GroundColor", Color.Lerp(l.FogColor, l.SkyTint, 0.3f));
                RenderSettings.skybox = sky;
            }
            var vgo = new GameObject("QA Weather Volume");
            var vol = vgo.AddComponent<Volume>(); vol.isGlobal = true; vol.priority = 20;
            var prof = ScriptableObject.CreateInstance<VolumeProfile>();
            var ca = prof.Add<ColorAdjustments>(true);
            ca.saturation.Override(l.Saturation - 4f); ca.postExposure.Override(l.PostExposure - 0.1f);
            vol.profile = prof;
        }

        static void Render(Camera cam, int width, int height, string path)
        {
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
            rt.Create();
            try
            {
                var req = new RenderPipeline.StandardRequest { destination = rt };
                for (int pass = 0; pass < 3; pass++)
                {
                    if (RenderPipeline.SupportsRenderRequest(cam, req)) RenderPipeline.SubmitRenderRequest(cam, req);
                    else { cam.targetTexture = rt; cam.Render(); cam.targetTexture = null; }
                }
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                File.WriteAllBytes(path, tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
            }
            finally { rt.Release(); UnityEngine.Object.DestroyImmediate(rt); }
        }

        static string Arg(string name)
        {
            var a = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(a, name);
            return i >= 0 && i + 1 < a.Length ? a[i + 1] : null;
        }
    }
}
