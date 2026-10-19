using UnityEngine;
using UnityEngine.Rendering;

namespace HarvestPanic.Customization
{
    /// <summary>
    /// Live 3D wardrobe preview: a non-networked FarmerBody copy far above the level, filmed by its own camera into a
    /// RenderTexture shown by the wardrobe UI (GDD: camera 2.5 m in front, mouse orbit, zoom 1.2-3.5 m).
    /// </summary>
    public sealed class WardrobePreview : MonoBehaviour
    {
        /// <summary>Where preview rigs are placed (above everything so level geometry never shadows them).</summary>
        public static readonly Vector3 Origin = new Vector3(0f, 500f, 0f);

        Camera _cam;
        FarmerVisual _farmer;
        CosmeticCatalog _catalog;
        float _yaw = 180f, _pitch = 8f, _distance;

        /// <summary>Texture the preview renders into.</summary>
        public RenderTexture Texture { get; private set; }

        /// <summary>The preview farmer.</summary>
        public FarmerVisual Farmer => _farmer;

        /// <summary>Creates a preview rig. Returns null when the catalog has no farmer prefab.</summary>
        public static WardrobePreview Create(CosmeticCatalog catalog, int width = 720, int height = 960)
        {
            if (catalog == null || catalog.farmerPrefab == null) return null;
            var holder = new GameObject("WardrobePreview");
            holder.SetActive(false);
            holder.transform.position = Origin;
            var p = holder.AddComponent<WardrobePreview>();
            p._catalog = catalog;
            p._distance = catalog.previewDistance;

            var farmerGo = Instantiate(catalog.farmerPrefab, holder.transform, false);
            farmerGo.name = "PreviewFarmer";
            var nbs = farmerGo.GetComponentsInChildren<Unity.Netcode.NetworkBehaviour>(true);
            for (int i = nbs.Length - 1; i >= 0; i--) DestroyImmediate(nbs[i]); // reverse: FarmerEmotes requires FarmerLoadoutSync
            p._farmer = farmerGo.GetComponent<FarmerVisual>();
            p._farmer.IsPreview = true;
            p._farmer.OfflineIsLocal = false;

            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null) // headless servers/tests have no GPU
            {
                p.Texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { name = "WardrobePreviewRT", antiAliasing = 1 };
                p.Texture.Create();
            }
            var camGo = new GameObject("PreviewCamera");
            camGo.transform.SetParent(holder.transform, false);
            p._cam = camGo.AddComponent<Camera>();
            p._cam.targetTexture = p.Texture;
            p._cam.enabled = p.Texture != null;
            p._cam.clearFlags = CameraClearFlags.SolidColor;
            p._cam.backgroundColor = new Color(0.42f, 0.3f, 0.22f, 1f);
            p._cam.nearClipPlane = 0.05f;
            p._cam.farClipPlane = 12f;
            p._cam.fieldOfView = 50f;

            AddLight(holder.transform, new Vector3(1.2f, 2.4f, 2.0f), new Color(1f, 0.93f, 0.8f), 6f, 6f);
            AddLight(holder.transform, new Vector3(-1.6f, 1.6f, -1.2f), new Color(0.75f, 0.85f, 1f), 3f, 6f);
            holder.SetActive(true);
            p.UpdateCamera();
            return p;
        }

        static void AddLight(Transform parent, Vector3 pos, Color c, float intensity, float range)
        {
            var go = new GameObject("PreviewLight");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point; l.color = c; l.intensity = intensity; l.range = range; l.shadows = LightShadows.None;
        }

        /// <summary>Shows <paramref name="loadout"/> on the preview farmer.</summary>
        public void Apply(FarmerLoadout loadout) { if (_farmer != null) _farmer.Apply(loadout); }

        /// <summary>Orbits by <paramref name="deltaYaw"/>/<paramref name="deltaPitch"/> degrees.</summary>
        public void Orbit(float deltaYaw, float deltaPitch)
        {
            _yaw += deltaYaw;
            _pitch = Mathf.Clamp(_pitch + deltaPitch, -10f, 45f);
            UpdateCamera();
        }

        /// <summary>Zooms by <paramref name="delta"/> metres within the catalog range.</summary>
        public void Zoom(float delta)
        {
            _distance = Mathf.Clamp(_distance + delta, _catalog.previewMinDistance, _catalog.previewMaxDistance);
            UpdateCamera();
        }

        /// <summary>Renders immediately (used by offscreen captures).</summary>
        public void RenderNow()
        {
            if (Texture == null) return;
            var req = new RenderPipeline.StandardRequest { destination = Texture };
            if (RenderPipeline.SupportsRenderRequest(_cam, req)) RenderPipeline.SubmitRenderRequest(_cam, req);
            else _cam.Render();
        }

        void UpdateCamera()
        {
            if (_cam == null) return;
            Vector3 target = Origin + Vector3.up * 1.2f;
            Quaternion rot = Quaternion.Euler(_pitch, _yaw, 0f);
            _cam.transform.position = target - rot * Vector3.forward * _distance;
            _cam.transform.rotation = rot;
        }

        void OnDestroy()
        {
            if (Texture != null) { Texture.Release(); Destroy(Texture); }
        }
    }
}
