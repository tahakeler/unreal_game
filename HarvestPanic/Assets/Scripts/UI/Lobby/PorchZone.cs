using UnityEngine;

namespace HarvestPanic.UI
{
    /// <summary>
    /// Ready zone on Grandpa's porch. The trigger collider lives on the Interactable layer; "is the local player on it" is decided
    /// from the local camera position so it works with whatever player prefab the Player team spawns, and only ever for this client.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class PorchZone : MonoBehaviour
    {
        public static PorchZone Instance { get; private set; }

        [SerializeField, Tooltip("Extra vertical reach above the zone (m) so a jumping farmer still counts.")] float headroom = 2.5f;
        [SerializeField] Renderer lightRenderer;
        BoxCollider _box;
        MaterialPropertyBlock _mpb;

        /// <summary>Test hook: when set, overrides the camera-based check.</summary>
        public bool? ForcedInside;

        public bool LocalInside
        {
            get
            {
                if (ForcedInside.HasValue) return ForcedInside.Value;
                var cam = Camera.main;
                if (cam == null) return false;
                var local = transform.InverseTransformPoint(cam.transform.position) - _box.center;
                var half = _box.size * 0.5f;
                return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.z) <= half.z && local.y > -half.y - 1f && local.y < half.y + headroom;
            }
        }

        void Awake() { _box = GetComponent<BoxCollider>(); _box.isTrigger = true; }
        void OnEnable() => Instance = this;
        void OnDisable() { if (Instance == this) Instance = null; }

        /// <summary>Porch light shows the local ready state.</summary>
        public void SetLight(bool on)
        {
            if (lightRenderer == null) return;
            _mpb ??= new MaterialPropertyBlock();
            lightRenderer.GetPropertyBlock(_mpb);
            _mpb.SetColor("_BaseColor", on ? new Color(1f, 0.96f, 0.78f) : new Color(0.35f, 0.3f, 0.22f));
            lightRenderer.SetPropertyBlock(_mpb);
        }
    }
}
