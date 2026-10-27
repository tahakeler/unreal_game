using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using HarvestPanic.Core;
using HarvestPanic.UI;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace HarvestPanic.Tests.PlayMode.UI
{
    /// <summary>Shared fakes and helpers for the menu PlayMode tests and QA captures.</summary>
    public static class MenuTestKit
    {
        public sealed class FakeRunSave : IRunSaveService
        {
            public bool HasSave { get; set; } = true;
            public string Summary { get; set; } = "Week 2 · WED · $340";
            public bool ContinueRequested { get; set; }
            public void DeleteSave() => HasSave = false;
        }

        public sealed class FakeVoice : IVoiceChat
        {
            public readonly HashSet<ulong> Speaking = new HashSet<ulong>(), Muted = new HashSet<ulong>();
            public VoiceTransmitMode Mode { get; set; } = VoiceTransmitMode.PushToTalk;
            public float OpenMicThreshold { get; set; } = 0.2f;
            public bool MicMuted { get; set; }
            public bool IsTransmitting { get; set; }
            public float LocalLoudness { get; set; } = 0.42f;
            public string BackendName => "Fake";
            public bool IsSpeaking(ulong clientId) => Speaking.Contains(clientId);
            public bool IsMuted(ulong clientId) => Muted.Contains(clientId);
            public void SetMuted(ulong clientId, bool muted) { if (muted) Muted.Add(clientId); else Muted.Remove(clientId); }
            public event Action<ulong, bool> OnSpeakingChanged;
            public void SetSpeaking(ulong id, bool on) { if (on) Speaking.Add(id); else Speaking.Remove(id); OnSpeakingChanged?.Invoke(id, on); }
        }

        /// <summary>Records UI_* requests (the real AudioService is not used in tests).</summary>
        public sealed class FakeAudio : IAudioService
        {
            public readonly List<string> Played = new List<string>();
            public float GetBusVolume(AudioBus bus) => 1f;
            public void SetBusVolume(AudioBus bus, float linear01) { }
            public bool PlayAt(string eventName, Vector3 position, float volumeScale = 1f) { Played.Add(eventName); return true; }
            public bool Play2D(string eventName, float volumeScale = 1f) { Played.Add(eventName); return true; }
            public void ReportVoiceActivity(bool anyNearbySpeaking) { }
        }

        public static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }

        public static IEnumerator Seconds(float s)
        {
            float end = Time.realtimeSinceStartup + s;
            while (Time.realtimeSinceStartup < end) yield return null;
        }

        public static IEnumerator WaitUntil(Func<bool> cond, float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!cond() && Time.realtimeSinceStartup < end) yield return null;
        }

        public static VisualElement Focused(VisualElement anyInPanel) => anyInPanel?.panel?.focusController?.focusedElement as VisualElement;

        /// <summary>Sends a gamepad/arrow navigation move to the focused element (as the input module would).</summary>
        public static void Nav(VisualElement anyInPanel, NavigationMoveEvent.Direction dir)
        {
            var target = Focused(anyInPanel) ?? anyInPanel;
            using (var e = NavigationMoveEvent.GetPooled(dir)) { e.target = target; target.SendEvent(e); }
        }

        public static void Submit(VisualElement target)
        {
            using (var e = NavigationSubmitEvent.GetPooled()) { e.target = target; target.SendEvent(e); }
        }

        public static bool QaEnabled(out string dir)
        {
            dir = Environment.GetEnvironmentVariable("HP_QA_DIR");
            if (string.IsNullOrEmpty(dir) || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return false;
            dir = Path.Combine(dir, "menu-ui");
            Directory.CreateDirectory(dir);
            return true;
        }

        /// <summary>
        /// 1920x1080 capture: the active game camera plus every visible UI Toolkit panel composited in sorting order
        /// (panels render into one texture; the lowest one clears it).
        /// </summary>
        public static IEnumerator Capture(string path)
        {
            const int W = 1920, H = 1080;
            var uiRt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32); uiRt.Create();
            var docs = new List<UIDocument>();
            foreach (var d in Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
                if (d.panelSettings != null && d.isActiveAndEnabled) docs.Add(d);
            docs.Sort((a, b) => a.panelSettings.sortingOrder.CompareTo(b.panelSettings.sortingOrder));
            var saved = new List<(PanelSettings ps, RenderTexture rt, bool clear, Color cc)>();
            var seen = new HashSet<PanelSettings>();
            bool first = true;
            foreach (var d in docs)
            {
                var ps = d.panelSettings;
                if (!seen.Add(ps)) continue;
                saved.Add((ps, ps.targetTexture, ps.clearColor, ps.colorClearValue));
                ps.targetTexture = uiRt; ps.clearColor = first; ps.colorClearValue = Color.clear;
                first = false;
            }
            yield return Frames(4);
            var cam = PickCamera();
            var sceneRt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32); sceneRt.Create();
            if (cam != null)
            {
                var req = new RenderPipeline.StandardRequest { destination = sceneRt };
                for (int i = 0; i < 2; i++)
                {
                    if (RenderPipeline.SupportsRenderRequest(cam, req)) RenderPipeline.SubmitRenderRequest(cam, req);
                    else { cam.targetTexture = sceneRt; cam.Render(); cam.targetTexture = null; }
                }
            }
            var a = Read(sceneRt); var b = Read(uiRt);
            var px = a.GetPixels32(); var ui = b.GetPixels32();
            for (int i = 0; i < px.Length; i++)
            {
                float al = ui[i].a / 255f; // UI Toolkit output is premultiplied
                px[i] = new Color32((byte)Mathf.Min(255, ui[i].r + px[i].r * (1 - al)), (byte)Mathf.Min(255, ui[i].g + px[i].g * (1 - al)), (byte)Mathf.Min(255, ui[i].b + px[i].b * (1 - al)), 255);
            }
            a.SetPixels32(px);
            File.WriteAllBytes(path, a.EncodeToPNG());
            foreach (var (ps, rt, clear, cc) in saved) { if (ps == null) continue; ps.targetTexture = rt; ps.clearColor = clear; ps.colorClearValue = cc; }
            Object.Destroy(a); Object.Destroy(b);
            uiRt.Release(); sceneRt.Release();
            yield return null;
        }

        static Camera PickCamera()
        {
            if (MenuBackdrop.Instance != null && MenuBackdrop.Instance.Camera != null && MenuBackdrop.Instance.Camera.isActiveAndEnabled) return MenuBackdrop.Instance.Camera;
            if (Camera.main != null && Camera.main.isActiveAndEnabled) return Camera.main;
            Camera best = null;
            foreach (var c in Camera.allCameras) if (c.targetTexture == null && (best == null || c.depth > best.depth)) best = c;
            return best;
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
