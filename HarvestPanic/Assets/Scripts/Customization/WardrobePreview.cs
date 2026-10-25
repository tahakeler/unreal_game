using System.Collections.Generic;
using HarvestPanic.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HarvestPanic.Customization
{
    /// <summary>What the turntable camera frames.</summary>
    public enum PreviewFocus { FullBody, Head, Face, Back, Outfit }

    /// <summary>
    /// The creator's 3D turntable: a non-networked FarmerBody copy on a little wooden stage in front of a studio backdrop,
    /// far above the level, filmed by its own camera into a RenderTexture shown by the creator UI.
    /// Drag spins the farmer (turntable), the wheel zooms (GDD: 1.2-3.5 m around the default 2.5 m), and each tab eases the
    /// camera to what it edits (head for hats/hair, back for back items). It idles with the Idle clip and a slow spin
    /// (off with reduced motion) and previews emotes. Its own key/fill/rim lights are enabled only while it renders and the
    /// scene's directional lights are switched off for that render, so it looks the same in the menu, lobby and barn.
    /// </summary>
    public sealed class WardrobePreview : MonoBehaviour
    {
        /// <summary>Where preview rigs are placed (above everything so level geometry never shadows them).</summary>
        public static readonly Vector3 Origin = new Vector3(0f, 500f, 0f);

        Camera _cam;
        FarmerVisual _farmer;
        CosmeticCatalog _catalog;
        Transform _stage;
        readonly List<Light> _ownLights = new List<Light>();
        readonly List<Light> _sceneLights = new List<Light>();
        readonly List<Light> _disabled = new List<Light>();
        float _sceneLightsAge = 999f;

        float _yaw = 200f, _yawTarget = 200f, _spinVelocity;
        float _pitch = 6f, _zoom = 1f, _zoomTarget = 1f;
        Vector3 _target, _targetGoal;
        float _distance, _distanceGoal;
        float _idle;
        PreviewFocus _focus = PreviewFocus.FullBody;

        /// <summary>Texture the preview renders into (null without a GPU).</summary>
        public RenderTexture Texture { get; private set; }

        /// <summary>The preview farmer.</summary>
        public FarmerVisual Farmer => _farmer;

        /// <summary>Current focus.</summary>
        public PreviewFocus Focus => _focus;

        /// <summary>Slow idle spin after a few seconds without input (off with reduced motion).</summary>
        public bool AutoSpin { get; set; } = true;

        /// <summary>Current turntable yaw in degrees (180 = facing the camera).</summary>
        public float Yaw => _yaw;

        /// <summary>Camera distance from the focus target (metres).</summary>
        public float Distance => _distance;

        /// <summary>Creates a preview rig. Returns null when the catalog has no farmer prefab.</summary>
        public static WardrobePreview Create(CosmeticCatalog catalog, int width = 960, int height = 1104)
        {
            if (catalog == null || catalog.farmerPrefab == null) return null;
            var holder = new GameObject("WardrobePreview");
            holder.SetActive(false);
            holder.transform.position = Origin;
            var p = holder.AddComponent<WardrobePreview>();
            p._catalog = catalog;

            p._stage = new GameObject("Turntable").transform;
            p._stage.SetParent(holder.transform, false);
            var farmerGo = Instantiate(catalog.farmerPrefab, p._stage, false);
            farmerGo.name = "PreviewFarmer";
            StripNetworking(farmerGo);
            p._farmer = farmerGo.GetComponent<FarmerVisual>();
            p._farmer.IsPreview = true;
            p._farmer.OfflineIsLocal = false;
            if (p._farmer.BodyAnimator != null) p._farmer.BodyAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            BuildSet(p, holder.transform, catalog);

            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null) // headless servers/tests have no GPU
            {
                p.Texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { name = "WardrobePreviewRT", antiAliasing = 4 };
                p.Texture.Create();
            }
            var camGo = new GameObject("PreviewCamera");
            camGo.transform.SetParent(holder.transform, false);
            p._cam = camGo.AddComponent<Camera>();
            p._cam.targetTexture = p.Texture;
            p._cam.enabled = p.Texture != null;
            p._cam.clearFlags = CameraClearFlags.SolidColor;
            p._cam.backgroundColor = new Color(0.96f, 0.9f, 0.77f, 1f);
            p._cam.nearClipPlane = 0.05f;
            p._cam.farClipPlane = 14f;
            p._cam.fieldOfView = 28f;
            p._cam.allowHDR = false;
            p._cam.depth = -50;
            var data = camGo.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = false;
            data.antialiasing = AntialiasingMode.None;
            data.renderShadows = true;

            p.SetFocus(PreviewFocus.FullBody, snap: true);
            holder.SetActive(true);
            p.UpdateCamera();
            return p;
        }

        /// <summary>Removes NetworkBehaviours (reverse order: FarmerEmotes requires FarmerLoadoutSync).</summary>
        public static void StripNetworking(GameObject go)
        {
            var nbs = go.GetComponentsInChildren<Unity.Netcode.NetworkBehaviour>(true);
            for (int i = nbs.Length - 1; i >= 0; i--) DestroyImmediate(nbs[i]);
        }

        static void BuildSet(WardrobePreview p, Transform root, CosmeticCatalog cat)
        {
            Color wood = new Color(0.55f, 0.37f, 0.22f), woodDark = new Color(0.38f, 0.25f, 0.15f), straw = new Color(0.9f, 0.77f, 0.45f);
            void Part(Transform parent, PartShape s, Vector3 pos, Vector3 size, Color c, Vector3 euler = default) =>
                AccessoryBuilder.BuildPart(new CosmeticPart { shape = s, position = pos, size = size, color = c, euler = euler }, parent, cat.baseMaterial, default);
            // Stage: a barrel-top disc that spins with the farmer, plus a few loose straws so the spin reads.
            Part(p._stage, PartShape.Cylinder, new Vector3(0f, -0.05f, 0f), new Vector3(1.25f, 0.1f, 1.25f), wood);
            Part(p._stage, PartShape.Cylinder, new Vector3(0f, -0.055f, 0f), new Vector3(1.32f, 0.08f, 1.32f), woodDark);
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f + 15f;
                var d = Quaternion.Euler(0f, a, 0f) * Vector3.forward * 0.5f;
                Part(p._stage, PartShape.Cube, d + Vector3.up * 0.003f, new Vector3(0.012f, 0.008f, 0.16f), straw, new Vector3(0f, a + 70f, 0f));
            }
            // Ground below the stage and the studio backdrop (unlit gradient).
            Part(root, PartShape.Cylinder, new Vector3(0f, -0.11f, 0f), new Vector3(7f, 0.02f, 7f), new Color(0.84f, 0.68f, 0.42f));
            var back = new GameObject("Backdrop");
            back.transform.SetParent(root, false);
            back.transform.localPosition = new Vector3(0f, 1.6f, -3.2f);
            back.AddComponent<MeshFilter>().sharedMesh = Quad(9f, 7f);
            var mr = back.AddComponent<MeshRenderer>();
            var mat = cat.unlitMaterial != null ? new Material(cat.unlitMaterial) : new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            mat.name = "HP_PreviewBackdrop";
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", CreatorTheme.Backdrop);
            AccessoryBuilder.SetColor(mat, Color.white);
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;

            // Lights: warm key (directional, shadows), cool fill, warm rim from behind. Only on while the preview renders.
            p._ownLights.Add(MakeLight(root, LightType.Directional, Quaternion.Euler(38f, 155f, 0f), Vector3.zero, new Color(1f, 0.93f, 0.8f), 1.35f, 0f, LightShadows.Soft));
            p._ownLights.Add(MakeLight(root, LightType.Point, Quaternion.identity, new Vector3(2.2f, 1.5f, 2.4f), new Color(0.75f, 0.85f, 1f), 2.2f, 8f, LightShadows.None));
            p._ownLights.Add(MakeLight(root, LightType.Point, Quaternion.identity, new Vector3(1.1f, 2.4f, -1.4f), new Color(1f, 0.92f, 0.78f), 4.5f, 6f, LightShadows.None));
            foreach (var l in p._ownLights) l.enabled = false;
        }

        static Light MakeLight(Transform parent, LightType type, Quaternion rot, Vector3 pos, Color c, float intensity, float range, LightShadows shadows)
        {
            var go = new GameObject("PreviewLight_" + type);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            var l = go.AddComponent<Light>();
            l.type = type; l.color = c; l.intensity = intensity; l.shadows = shadows;
            if (type != LightType.Directional) l.range = range;
            if (shadows != LightShadows.None) { l.shadowStrength = 0.55f; l.shadowBias = 0.02f; l.shadowNormalBias = 0.3f; }
            return l;
        }

        /// <summary>Quad facing +Z (towards the preview camera), UV 0..1.</summary>
        static Mesh Quad(float w, float h)
        {
            var m = new Mesh { name = "HP_PreviewQuad" };
            m.vertices = new[] { new Vector3(-w / 2, -h / 2, 0), new Vector3(w / 2, -h / 2, 0), new Vector3(-w / 2, h / 2, 0), new Vector3(w / 2, h / 2, 0) };
            m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            m.normals = new[] { Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward };
            m.triangles = new[] { 0, 1, 2, 1, 3, 2 }; // clockwise as seen from +Z
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Shows <paramref name="loadout"/> on the preview farmer.</summary>
        public void Apply(FarmerLoadout loadout) { if (_farmer != null) _farmer.Apply(loadout); }

        /// <summary>Eases the camera to <paramref name="focus"/> (and turns the farmer around for back items).</summary>
        public void SetFocus(PreviewFocus focus, bool snap = false)
        {
            _focus = focus;
            float headY = HeadHeight();
            float hk = CosmeticIconRenderer.HeadScale(_farmer);
            switch (focus)
            {
                case PreviewFocus.Head: _targetGoal = new Vector3(0f, headY + 0.04f * hk, 0f); _distanceGoal = Mathf.Max(2.35f, 1.6f * hk); break;
                case PreviewFocus.Face: _targetGoal = new Vector3(0f, headY - 0.1f * hk, 0f); _distanceGoal = Mathf.Max(1.7f, 1.2f * hk); break;
                case PreviewFocus.Back: _targetGoal = new Vector3(0f, headY * 0.7f, 0f); _distanceGoal = 2.9f; break;
                case PreviewFocus.Outfit: _targetGoal = new Vector3(0f, headY * 0.55f, 0f); _distanceGoal = 3.4f; break;
                default: _targetGoal = new Vector3(0f, headY * 0.52f, 0f); _distanceGoal = 4.1f; break;
            }
            _yawTarget = focus == PreviewFocus.Back ? 20f : focus == PreviewFocus.Face ? 180f : 200f;
            _yawTarget = _yaw + Mathf.DeltaAngle(_yaw, _yawTarget);
            _zoomTarget = 1f;
            _idle = 0f;
            if (snap) { _target = _targetGoal; _distance = _distanceGoal; _yaw = _yawTarget; _zoom = 1f; }
        }

        float HeadHeight()
        {
            if (_farmer == null || _farmer.HeadAttachPoint == null) return 1.75f;
            return Mathf.Clamp(_farmer.HeadAttachPoint.position.y - Origin.y - 0.08f, 1.0f, 2.2f);
        }

        /// <summary>Spins the turntable by <paramref name="deltaYaw"/> degrees and tilts the camera by <paramref name="deltaPitch"/>.</summary>
        public void Orbit(float deltaYaw, float deltaPitch)
        {
            _yaw += deltaYaw;
            _yawTarget = _yaw;
            _spinVelocity = Mathf.Lerp(_spinVelocity, deltaYaw / Mathf.Max(Time.unscaledDeltaTime, 0.016f), 0.5f);
            _pitch = Mathf.Clamp(_pitch + deltaPitch, -8f, 35f);
            _idle = 0f;
            UpdateCamera();
        }

        /// <summary>Lets go after a drag: the turntable keeps a little momentum.</summary>
        public void Release() => _idle = 0f;

        /// <summary>Zooms by <paramref name="delta"/> metres (positive = out) within the catalog's 1.2-3.5 m feel range.</summary>
        public void Zoom(float delta)
        {
            float min = _catalog != null ? _catalog.previewMinDistance : 1.2f, max = _catalog != null ? _catalog.previewMaxDistance : 3.5f;
            // Map the GDD range onto a zoom factor around the current focus distance.
            float baseD = Mathf.Max(0.5f, _distanceGoal);
            _zoomTarget = Mathf.Clamp(_zoomTarget + delta / baseD, Mathf.Min(1f, min / 2.5f) * 0.7f, Mathf.Max(1f, max / 2.5f) * 1.1f);
            _idle = 0f;
        }

        /// <summary>Plays an emote on the preview farmer ("wave", "dance", ...).</summary>
        public void PlayEmote(string key)
        {
            if (_farmer == null) return;
            _farmer.PlayEmote(key);
            _idle = -2f;
        }

        /// <summary>A happy hop with a puff of hay (Randomize feedback).</summary>
        public void Hop()
        {
            var anim = _farmer != null ? _farmer.BodyAnimator : null;
            if (anim != null && anim.isActiveAndEnabled) anim.SetTrigger(FarmerVisual.JumpParam);
            if (!UiAccessibility.ReducedMotion && _catalog != null)
            {
                var poof = HayPoof.Spawn(Origin + Vector3.up * 0.9f, 0.35f, _catalog.baseMaterial);
                poof.transform.SetParent(transform, true);
            }
            _idle = -1f;
        }

        void LateUpdate()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            _idle += dt;
            bool spin = AutoSpin && !UiAccessibility.ReducedMotion && _idle > 5f;
            if (spin) { _yaw += 9f * dt; _yawTarget = _yaw; }
            else if (Mathf.Abs(_spinVelocity) > 1f && _idle < 1.5f && !UiAccessibility.ReducedMotion)
            {
                _yaw += _spinVelocity * dt; _yawTarget = _yaw;
                _spinVelocity *= Mathf.Exp(-4f * dt);
            }
            else
            {
                _spinVelocity = 0f;
                _yaw = Mathf.Lerp(_yaw, _yawTarget, 1f - Mathf.Exp(-8f * dt));
            }
            float k = UiAccessibility.ReducedMotion ? 1f : 1f - Mathf.Exp(-6f * dt);
            _target = Vector3.Lerp(_target, _targetGoal, k);
            _distance = Mathf.Lerp(_distance, _distanceGoal, k);
            _zoom = Mathf.Lerp(_zoom, _zoomTarget, k);
            UpdateCamera();
        }

        void UpdateCamera()
        {
            if (_cam == null) return;
            if (_stage != null) _stage.localRotation = Quaternion.Euler(0f, _yaw - 180f, 0f);
            Vector3 target = Origin + _target;
            Quaternion rot = Quaternion.Euler(_pitch, 180f, 0f);
            _cam.transform.position = target - rot * Vector3.forward * (_distance * _zoom);
            _cam.transform.rotation = rot;
        }

        void OnEnable() => RenderPipelineManager.beginCameraRendering += OnBegin;

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBegin;
            RenderPipelineManager.endCameraRendering -= OnEnd;
            Restore();
        }

        void OnBegin(ScriptableRenderContext ctx, Camera cam)
        {
            if (cam != _cam) return;
            Isolate();
            RenderPipelineManager.endCameraRendering -= OnEnd;
            RenderPipelineManager.endCameraRendering += OnEnd;
        }

        void OnEnd(ScriptableRenderContext ctx, Camera cam)
        {
            if (cam != _cam) return;
            RenderPipelineManager.endCameraRendering -= OnEnd;
            Restore();
        }

        /// <summary>Own lights on, the scene's directional lights off (for this camera only).</summary>
        void Isolate()
        {
            _sceneLightsAge += Time.unscaledDeltaTime;
            if (_sceneLightsAge > 2f)
            {
                _sceneLightsAge = 0f;
                _sceneLights.Clear();
                foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (l != null && l.type == LightType.Directional && !_ownLights.Contains(l)) _sceneLights.Add(l);
            }
            _disabled.Clear();
            foreach (var l in _sceneLights) if (l != null && l.enabled) { l.enabled = false; _disabled.Add(l); }
            foreach (var l in _ownLights) if (l != null) l.enabled = true;
        }

        void Restore()
        {
            foreach (var l in _ownLights) if (l != null) l.enabled = false;
            foreach (var l in _disabled) if (l != null) l.enabled = true;
            _disabled.Clear();
        }

        /// <summary>Renders immediately (used by offscreen captures).</summary>
        public void RenderNow()
        {
            if (Texture == null || _cam == null) return;
            UpdateCamera();
            Isolate();
            try
            {
                var req = new RenderPipeline.StandardRequest { destination = Texture };
                if (RenderPipeline.SupportsRenderRequest(_cam, req)) RenderPipeline.SubmitRenderRequest(_cam, req);
                else _cam.Render();
            }
            finally { Restore(); }
        }

        void OnDestroy()
        {
            Restore();
            if (Texture != null) { Texture.Release(); Destroy(Texture); }
        }
    }
}
