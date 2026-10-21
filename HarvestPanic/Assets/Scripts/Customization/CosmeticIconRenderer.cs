using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace HarvestPanic.Customization
{
    /// <summary>
    /// Card thumbnails for the creator: a hidden puppet farmer is dressed in "your loadout + this item" and photographed
    /// (hats on your head, back items from behind, outfits full body), so every card previews the choice on you.
    /// Items with an authored <see cref="CosmeticItem.icon"/> skip this. Renders a few per frame from a queue into one
    /// MSAA target and resolves into a small per-icon RenderTexture (cached by look; LRU-trimmed). No-op without a GPU.
    /// </summary>
    public sealed class CosmeticIconRenderer : MonoBehaviour
    {
        /// <summary>Rig position (away from the turntable's view, above the level).</summary>
        public static readonly Vector3 Origin = new Vector3(60f, 520f, 0f);
        /// <summary>Thumbnail size in pixels.</summary>
        public const int Size = 176;
        const int MaxCached = 96;

        struct Job { public string key; public FarmerLoadout look; public PreviewFocus framing; public RenderTexture rt; }

        CosmeticCatalog _catalog;
        FarmerVisual _puppet;
        Transform _stage, _turn;
        Camera _cam;
        RenderTexture _msaa;
        readonly List<Light> _lights = new List<Light>();
        readonly Dictionary<string, RenderTexture> _cache = new Dictionary<string, RenderTexture>();
        readonly LinkedList<string> _order = new LinkedList<string>();
        readonly Queue<Job> _queue = new Queue<Job>();
        readonly HashSet<string> _queued = new HashSet<string>();
        readonly List<Light> _off = new List<Light>();

        /// <summary>Raised after one or more thumbnails were rendered this frame.</summary>
        public event Action Rendered;

        /// <summary>True when a GPU is available (thumbnails are blank otherwise).</summary>
        public bool CanRender => _msaa != null;

        /// <summary>Thumbnails waiting to render.</summary>
        public int Pending => _queue.Count;

        /// <summary>Creates the rig (inactive until it renders).</summary>
        public static CosmeticIconRenderer Create(CosmeticCatalog catalog)
        {
            var go = new GameObject("CosmeticIconRenderer");
            var r = go.AddComponent<CosmeticIconRenderer>();
            r._catalog = catalog;
            var rig = new GameObject("IconRig");
            rig.transform.SetParent(go.transform, false);
            rig.transform.position = Origin;
            r._stage = rig.transform;
            r._turn = new GameObject("Turn").transform;
            r._turn.SetParent(rig.transform, false);
            if (catalog != null && catalog.farmerPrefab != null)
            {
                var f = Instantiate(catalog.farmerPrefab, r._turn, false);
                f.name = "IconPuppet";
                WardrobePreview.StripNetworking(f);
                r._puppet = f.GetComponent<FarmerVisual>();
                r._puppet.IsPreview = true;
                r._puppet.OfflineIsLocal = false;
                if (r._puppet.BodyAnimator != null) r._puppet.BodyAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
            var camGo = new GameObject("IconCamera");
            camGo.transform.SetParent(rig.transform, false);
            r._cam = camGo.AddComponent<Camera>();
            r._cam.enabled = false;
            r._cam.clearFlags = CameraClearFlags.SolidColor;
            r._cam.backgroundColor = new Color(0.94f, 0.89f, 0.76f, 1f);
            r._cam.fieldOfView = 24f;
            r._cam.nearClipPlane = 0.05f;
            r._cam.farClipPlane = 12f;
            r._cam.allowHDR = false;
            var data = camGo.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = false;
            data.renderShadows = false;
            r._lights.Add(Light(rig.transform, LightType.Directional, Quaternion.Euler(30f, 150f, 0f), Vector3.zero, new Color(1f, 0.94f, 0.82f), 1.3f));
            r._lights.Add(Light(rig.transform, LightType.Point, Quaternion.identity, new Vector3(1.6f, 1.6f, 2f), new Color(0.78f, 0.86f, 1f), 1.8f));
            r._lights.Add(Light(rig.transform, LightType.Point, Quaternion.identity, new Vector3(-0.8f, 2.4f, -1.4f), new Color(1f, 0.92f, 0.8f), 3f));
            foreach (var l in r._lights) l.enabled = false;
            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
            {
                r._msaa = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32) { name = "HP_IconMSAA", antiAliasing = 4 };
                r._msaa.Create();
            }
            rig.SetActive(false);
            return r;
        }

        static Light Light(Transform parent, LightType type, Quaternion rot, Vector3 pos, Color c, float intensity)
        {
            var go = new GameObject("IconLight");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos; go.transform.localRotation = rot;
            var l = go.AddComponent<Light>();
            l.type = type; l.color = c; l.intensity = intensity; l.shadows = LightShadows.None;
            if (type != LightType.Directional) l.range = 7f;
            return l;
        }

        /// <summary>
        /// Thumbnail for <paramref name="look"/> photographed with <paramref name="framing"/>. Returns the (possibly not yet
        /// rendered) texture; it fills in within a few frames. Null without a GPU.
        /// </summary>
        public RenderTexture Get(FarmerLoadout look, PreviewFocus framing)
        {
            if (_msaa == null || _puppet == null) return null;
            string key = framing + ":" + look.GetHashCode().ToString("X8") + ":" + look.Hat + "/" + look.Hair + "/" + look.Outfit + "/" + look.Head;
            if (_cache.TryGetValue(key, out var rt) && rt != null) return rt;
            rt = new RenderTexture(Size, Size, 0, RenderTextureFormat.ARGB32) { name = "HP_Icon" };
            rt.Create();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            GL.Clear(true, true, _cam.backgroundColor);
            RenderTexture.active = prev;
            _cache[key] = rt;
            _order.AddLast(key);
            Trim();
            if (_queued.Add(key)) _queue.Enqueue(new Job { key = key, look = look, framing = framing, rt = rt });
            return rt;
        }

        void Trim()
        {
            while (_order.Count > MaxCached)
            {
                string k = _order.First.Value;
                _order.RemoveFirst();
                if (_queued.Contains(k)) { _order.AddLast(k); break; }
                if (_cache.TryGetValue(k, out var rt) && rt != null) { rt.Release(); Destroy(rt); }
                _cache.Remove(k);
            }
        }

        /// <summary>Renders up to <paramref name="max"/> queued thumbnails now.</summary>
        public int Pump(int max)
        {
            if (_queue.Count == 0 || _msaa == null || _puppet == null) return 0;
            int done = 0;
            _stage.gameObject.SetActive(true);
            Isolate(true);
            try
            {
                while (done < max && _queue.Count > 0)
                {
                    var job = _queue.Dequeue();
                    _queued.Remove(job.key);
                    if (job.rt == null) continue;
                    RenderOne(job);
                    done++;
                }
            }
            finally
            {
                Isolate(false);
                _stage.gameObject.SetActive(false);
            }
            if (done > 0) Rendered?.Invoke();
            return done;
        }

        void RenderOne(Job job)
        {
            _puppet.Apply(job.look);
            var anim = _puppet.BodyAnimator;
            if (anim != null && anim.isActiveAndEnabled) { anim.Rebind(); anim.Update(0.35f); }
            _puppet.ApplyHeadBoneScale();
            Frame(job.framing);
            _cam.targetTexture = _msaa;
            _cam.enabled = true;
            try
            {
                var req = new RenderPipeline.StandardRequest { destination = _msaa };
                if (RenderPipeline.SupportsRenderRequest(_cam, req)) RenderPipeline.SubmitRenderRequest(_cam, req);
                else _cam.Render();
            }
            finally { _cam.enabled = false; }
            Graphics.Blit(_msaa, job.rt);
        }

        void Frame(PreviewFocus f)
        {
            float headY = _puppet.HeadAttachPoint != null ? _puppet.HeadAttachPoint.position.y - Origin.y : 1.75f;
            float yaw; Vector3 target; float dist;
            switch (f)
            {
                case PreviewFocus.Head: yaw = 205f; target = new Vector3(0f, headY + 0.08f, 0f); dist = 2.3f; break;
                case PreviewFocus.Face: yaw = 195f; target = new Vector3(0f, headY - 0.12f, 0.04f); dist = 1.25f; break;
                case PreviewFocus.Back: yaw = 35f; target = new Vector3(0f, headY * 0.72f, 0f); dist = 2.4f; break;
                case PreviewFocus.Outfit: yaw = 200f; target = new Vector3(0f, headY * 0.55f, 0f); dist = 3.0f; break;
                default: yaw = 200f; target = new Vector3(0f, headY * 0.52f, 0f); dist = 4.6f; break;
            }
            _turn.localRotation = Quaternion.Euler(0f, yaw - 180f, 0f);
            var rot = Quaternion.Euler(4f, 180f, 0f);
            _cam.transform.position = Origin + target - rot * Vector3.forward * dist;
            _cam.transform.rotation = rot;
        }

        void Isolate(bool on)
        {
            if (on)
            {
                _off.Clear();
                foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (l != null && l.enabled && l.type == LightType.Directional && !_lights.Contains(l)) { l.enabled = false; _off.Add(l); }
                foreach (var l in _lights) l.enabled = true;
            }
            else
            {
                foreach (var l in _lights) if (l != null) l.enabled = false;
                foreach (var l in _off) if (l != null) l.enabled = true;
                _off.Clear();
            }
        }

        void OnDestroy()
        {
            Isolate(false);
            foreach (var rt in _cache.Values) if (rt != null) { rt.Release(); Destroy(rt); }
            _cache.Clear();
            if (_msaa != null) { _msaa.Release(); Destroy(_msaa); }
        }
    }
}
