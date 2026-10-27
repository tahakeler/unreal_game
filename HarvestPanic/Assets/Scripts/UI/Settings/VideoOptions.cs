using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HarvestPanic.UI
{
    /// <summary>
    /// Video tab data and its application to the engine: quality preset (QualitySettings level), resolution, window mode,
    /// vsync, frame-rate cap and render scale. List building is pure (EditMode tested); <see cref="Apply"/> only runs in a
    /// player (never in the editor or batchmode, so test runs never touch ProjectSettings or the URP assets).
    /// </summary>
    public static class VideoOptions
    {
        public const int MinWidth = 800, MinHeight = 600;
        static (int w, int h, FullScreenMode m)? _lastWindow;

        /// <summary>Distinct resolutions, largest first, at least 800 x 600.</summary>
        public static List<Vector2Int> Distinct(IEnumerable<Vector2Int> raw)
        {
            var seen = new HashSet<Vector2Int>();
            var list = new List<Vector2Int>();
            foreach (var r in raw)
            {
                if (r.x < MinWidth || r.y < MinHeight || !seen.Add(r)) continue;
                list.Add(r);
            }
            list.Sort((a, b) => b.x != a.x ? b.x.CompareTo(a.x) : b.y.CompareTo(a.y));
            return list;
        }

        /// <summary>The machine's resolutions (player) or common 16:9 sizes (editor).</summary>
        public static List<Vector2Int> Available()
        {
            var raw = new List<Vector2Int>();
            foreach (var r in Screen.resolutions) raw.Add(new Vector2Int(r.width, r.height));
            if (raw.Count == 0 || Application.isEditor)
                foreach (var v in new[] { new Vector2Int(3840, 2160), new Vector2Int(2560, 1440), new Vector2Int(1920, 1080), new Vector2Int(1600, 900), new Vector2Int(1280, 720) })
                    raw.Add(v);
            return Distinct(raw);
        }

        /// <summary>Choice index for the saved size: 0 = native, i + 1 = list[i]; unknown sizes fall back to native.</summary>
        public static int IndexOf(IList<Vector2Int> list, int w, int h)
        {
            if (w <= 0 || h <= 0) return 0;
            for (int i = 0; i < list.Count; i++) if (list[i].x == w && list[i].y == h) return i + 1;
            return 0;
        }

        /// <summary>Quality level names (index = QualitySettings level).</summary>
        public static string[] QualityNames => QualitySettings.names;

        /// <summary>Choice index for the saved quality: 0 = "Default" (-1), i + 1 = level i.</summary>
        public static int QualityChoice(int level, int count) => level < 0 || level >= count ? 0 : level + 1;

        public static FullScreenMode ModeFor(WindowModeSetting m) => m switch
        {
            WindowModeSetting.Fullscreen => FullScreenMode.ExclusiveFullScreen,
            WindowModeSetting.Borderless => FullScreenMode.FullScreenWindow,
            _ => FullScreenMode.Windowed,
        };

        /// <summary>The size to use for "native" in <paramref name="mode"/> given the desktop size.</summary>
        public static Vector2Int NativeFor(FullScreenMode mode, Vector2Int desktop)
        {
            if (mode != FullScreenMode.Windowed) return desktop;
            int w = Mathf.RoundToInt(desktop.x * 0.8f), h = Mathf.RoundToInt(w * 9f / 16f);
            if (h > desktop.y * 0.85f) { h = Mathf.RoundToInt(desktop.y * 0.85f); w = Mathf.RoundToInt(h * 16f / 9f); }
            return new Vector2Int(Mathf.Max(MinWidth, w), Mathf.Max(MinHeight, h));
        }

        /// <summary>Applies the video settings (player only).</summary>
        public static void Apply(GameSettings s)
        {
            if (Application.isEditor || Application.isBatchMode) return;
            int levels = QualitySettings.names.Length;
            if (s.QualityLevel >= 0 && s.QualityLevel < levels && s.QualityLevel != QualitySettings.GetQualityLevel())
                QualitySettings.SetQualityLevel(s.QualityLevel, true);
            QualitySettings.vSyncCount = s.VSync ? 1 : 0;
            Application.targetFrameRate = s.FrameRateCap > 0 ? s.FrameRateCap : -1;

            var mode = ModeFor(s.WindowMode);
            var desktop = new Vector2Int(Screen.currentResolution.width, Screen.currentResolution.height);
            var size = s.ResolutionWidth > 0 && s.ResolutionHeight > 0 ? new Vector2Int(s.ResolutionWidth, s.ResolutionHeight) : NativeFor(mode, desktop);
            var want = (size.x, size.y, mode);
            if (_lastWindow != want)
            {
                _lastWindow = want;
                Screen.SetResolution(size.x, size.y, mode);
            }
            // The quality level may swap the URP asset, so render scale goes last.
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp) urp.renderScale = s.ResolutionScale;
        }
    }
}
