using UnityEngine;
using UnityEngine.Rendering;

namespace HarvestPanic.Camcorder
{
    /// <summary>Off-screen camera renders for thumbnails and export frames (URP render request, legacy fallback).</summary>
    public static class ClipRender
    {
        /// <summary>Renders <paramref name="cam"/> into <paramref name="rt"/> without touching the screen.</summary>
        public static void RenderInto(Camera cam, RenderTexture rt)
        {
            var req = new RenderPipeline.StandardRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(cam, req)) RenderPipeline.SubmitRenderRequest(cam, req);
            else
            {
                var prev = cam.targetTexture;
                cam.targetTexture = rt; cam.Render(); cam.targetTexture = prev;
            }
        }

        /// <summary>Reads <paramref name="rt"/> into <paramref name="dst"/> at (x, y).</summary>
        public static void ReadInto(RenderTexture rt, Texture2D dst, int x = 0, int y = 0)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            dst.ReadPixels(new Rect(0, 0, rt.width, rt.height), x, y, false);
            RenderTexture.active = prev;
        }

        /// <summary>One-shot picture of <paramref name="cam"/> (the caller owns the returned texture). Null without a GPU.</summary>
        public static Texture2D RenderToTexture(Camera cam, int w, int h)
        {
            if (cam == null || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return null;
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
            try
            {
                RenderInto(cam, rt);
                var tex = new Texture2D(w, h, TextureFormat.RGB24, false) { name = "CamcorderThumb", wrapMode = TextureWrapMode.Clamp };
                ReadInto(rt, tex);
                tex.Apply(false, false);
                return tex;
            }
            finally { RenderTexture.ReleaseTemporary(rt); }
        }
    }
}
