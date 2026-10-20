using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace HarvestPanic.Camcorder
{
    /// <summary>
    /// Exports a tape without a video encoder (beta scope): a numbered PNG frame sequence, a 4x4 contact sheet, a "best shot"
    /// thumbnail at the impact, the binary tape (<c>moment.hpm</c>) and a README with the ffmpeg command, into
    /// <c>Application.persistentDataPath/Clips/&lt;stamp&gt;_&lt;title&gt;/</c>. Frames are rendered one per Unity frame through
    /// the viewer camera; PNG encoding and disk writes run on the thread pool (<c>ImageConversion.EncodeArrayToPNG</c>),
    /// with two pooled pixel buffers per in-flight frame so the main thread never stalls on compression.
    /// </summary>
    public static class ClipExporter
    {
        public const int SheetCols = 4, SheetRows = 4;

        public static string ClipsRoot => Path.Combine(Application.persistentDataPath, "Clips");

        public static string FolderName(Moment m, DateTime now)
        {
            var slug = new System.Text.StringBuilder();
            foreach (char c in (m.Title ?? "tape").ToLowerInvariant())
            {
                if (c >= 'a' && c <= 'z' || c >= '0' && c <= '9') slug.Append(c);
                else if (slug.Length > 0 && slug[slug.Length - 1] != '_') slug.Append('_');
                if (slug.Length >= 40) break;
            }
            return now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + "_" + slug.ToString().Trim('_');
        }

        /// <summary>Number of export frames for a tape of <paramref name="duration"/> s.</summary>
        public static int FrameCount(float duration, int fps, int maxFrames) =>
            Mathf.Clamp(Mathf.FloorToInt(duration * Mathf.Max(1, fps)) + 1, 1, Mathf.Max(1, maxFrames));

        public static string FfmpegHint(int fps) =>
            $"ffmpeg -framerate {fps} -i frames/frame_%04d.png -vf \"scale=trunc(iw/2)*2:trunc(ih/2)*2\" -c:v libx264 -pix_fmt yuv420p -movflags +faststart clip.mp4";

        /// <summary>Coroutine: renders and writes the whole export; calls <paramref name="done"/> with the folder or null and an error.</summary>
        public static IEnumerator Export(ReplayViewer viewer, CamcorderTuning tuning, Action<string, string> done, string rootOverride = null)
        {
            var m = viewer.Current;
            if (m == null || viewer.Cam == null) { done?.Invoke(null, "Nothing to export."); yield break; }
            string folder;
            try
            {
                folder = Path.Combine(rootOverride ?? ClipsRoot, FolderName(m, DateTime.Now));
                Directory.CreateDirectory(Path.Combine(folder, "frames"));
            }
            catch (Exception e) { done?.Invoke(null, "Could not create the clip folder: " + e.Message); yield break; }

            bool wasPlaying = viewer.Playing; float wasTime = viewer.Time;
            viewer.Playing = false; viewer.Exporting = true;
            int w = tuning.exportWidth, h = tuning.exportHeight, fps = Mathf.Max(1, tuning.exportFps);
            int frames = FrameCount(m.Duration, fps, tuning.maxExportFrames);
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32); rt.Create();
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var pending = new List<Task>();
            var pool = new Stack<byte[]>();
            string error = null;
            try
            {
                for (int f = 0; f < frames && error == null; f++)
                {
                    float t = Mathf.Min(m.Duration, f / (float)fps);
                    viewer.SetTime(t);
                    viewer.UpdateCamera(1f / fps);
                    ClipRender.RenderInto(viewer.Cam, rt);
                    ClipRender.ReadInto(rt, tex);
                    var raw = tex.GetRawTextureData<byte>();
                    byte[] buf;
                    lock (pool) buf = pool.Count > 0 ? pool.Pop() : new byte[raw.Length];
                    raw.CopyTo(buf);
                    string path = Path.Combine(folder, "frames", $"frame_{f + 1:0000}.png");
                    pending.Add(Task.Run(() =>
                    {
                        var png = ImageConversion.EncodeArrayToPNG(buf, GraphicsFormat.R8G8B8A8_UNorm, (uint)w, (uint)h);
                        File.WriteAllBytes(path, png);
                        lock (pool) pool.Push(buf);
                    }));
                    // Keep at most 4 frames in flight (bounded memory: 4 x w x h x 4 bytes).
                    while (pending.Count >= 4) { error = Drain(pending, 1); if (error != null) break; yield return null; }
                    if (f % 2 == 1) yield return null;
                }
                while (pending.Count > 0 && error == null) { error = Drain(pending, 0); if (pending.Count > 0) yield return null; }
                if (error == null)
                {
                    WriteSheet(viewer, m, folder);
                    WriteThumb(viewer, m, folder, tuning);
                    File.WriteAllBytes(Path.Combine(folder, "moment.hpm"), MomentSerializer.ToBytes(m));
                    File.WriteAllText(Path.Combine(folder, "README.txt"),
                        $"HARVEST PANIC - Grandpa's Camcorder\n{m.Title}\n\n{frames} frames at {fps} fps ({w}x{h}).\n" +
                        "Make an MP4 (run inside this folder):\n  " + FfmpegHint(fps) + "\n" +
                        "Make a GIF:\n  ffmpeg -framerate " + fps + " -i frames/frame_%04d.png -vf \"fps=" + fps + ",scale=480:-1:flags=lanczos\" clip.gif\n");
                }
            }
            finally
            {
                UnityEngine.Object.Destroy(tex); rt.Release(); UnityEngine.Object.Destroy(rt);
                if (viewer.IsOpen)
                {
                    viewer.Exporting = false;
                    viewer.SetTime(wasTime); viewer.Playing = wasPlaying;
                }
            }
            if (error != null) { done?.Invoke(null, error); yield break; }
            m.ExportFolder = folder;
            done?.Invoke(folder, null);
        }

        static string Drain(List<Task> pending, int keep)
        {
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                var t = pending[i];
                if (!t.IsCompleted) continue;
                pending.RemoveAt(i);
                if (t.IsFaulted) return "Could not write the clip (disk full?): " + t.Exception?.GetBaseException().Message;
            }
            return null;
        }

        static void WriteSheet(ReplayViewer viewer, Moment m, string folder)
        {
            const int tw = 320, th = 180;
            var sheet = new Texture2D(tw * SheetCols, th * SheetRows, TextureFormat.RGB24, false);
            var rt = RenderTexture.GetTemporary(tw, th, 24, RenderTextureFormat.ARGB32);
            int n = SheetCols * SheetRows;
            for (int i = 0; i < n; i++)
            {
                viewer.SetTime(m.Duration * i / (n - 1));
                viewer.UpdateCamera(0.1f);
                ClipRender.RenderInto(viewer.Cam, rt);
                int col = i % SheetCols, row = SheetRows - 1 - i / SheetCols; // first tile top-left
                ClipRender.ReadInto(rt, sheet, col * tw, row * th);
            }
            sheet.Apply(false);
            File.WriteAllBytes(Path.Combine(folder, "contact_sheet.png"), sheet.EncodeToPNG());
            RenderTexture.ReleaseTemporary(rt);
            UnityEngine.Object.Destroy(sheet);
        }

        static void WriteThumb(ReplayViewer viewer, Moment m, string folder, CamcorderTuning t)
        {
            viewer.SetTime(m.TriggerTime);
            viewer.UpdateCamera(0.1f);
            var tex = ClipRender.RenderToTexture(viewer.Cam, t.thumbnailWidth, t.thumbnailHeight);
            if (tex == null) return;
            File.WriteAllBytes(Path.Combine(folder, "thumb.png"), tex.EncodeToPNG());
            if (m.Thumbnail != null) File.WriteAllBytes(Path.Combine(folder, "thumb_live.png"), m.Thumbnail.EncodeToPNG());
            UnityEngine.Object.Destroy(tex);
        }
    }
}
