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
    
        /// <summary>
        /// "Grandpa angle" picture: a temporary camera copied from <paramref name="template"/>, placed up and back from
        /// <paramref name="focus"/> (facing the same way as the player), so the best shot shows the disaster, not first-person hands.
        /// </summary>
        public static Texture2D RenderWide(Camera template, Vector3 focus, int w, int h, float distance = 9f, float height = 5f)
        {
            if (template == null) return null;
            var fwd = Vector3.ProjectOnPlane(template.transform.forward, Vector3.up);
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
            fwd.Normalize();
            var go = new GameObject("CamcorderThumbCam");
            try
            {
                var cam = go.AddComponent<Camera>();
                cam.CopyFrom(template);
                cam.enabled = false;
                cam.fieldOfView = 50f;
                var pos = focus - fwd * distance + Vector3.up * height;
                cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(focus + Vector3.up * 0.8f - pos));
                return RenderToTexture(cam, w, h);
            }
            finally { Object.Destroy(go); }
        }
    }
}
