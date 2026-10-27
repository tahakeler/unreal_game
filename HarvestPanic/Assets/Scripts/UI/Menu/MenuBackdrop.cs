using System;
using UnityEngine;

namespace HarvestPanic.UI
{
    /// <summary>
    /// Live 3D farm behind the main menu: the camera eases between one framing per menu screen (title = farmyard, play =
    /// barn, host = porch, join = road...) and drifts slowly like a hand-held postcard. Reduced motion: no drift, hard cuts.
    /// Pure presentation, client-local, no gameplay. Scene content is built by UiSceneBuilder from Muddlebury's visuals.
    /// </summary>
    public sealed class MenuBackdrop : MonoBehaviour
    {
        [Serializable]
        public struct Shot
        {
            public MenuScreen screen;
            public Vector3 position;
            public Vector3 euler;
            public float fov;
        }

        public static MenuBackdrop Instance { get; private set; }

        [SerializeField] Camera cam;
        [SerializeField] Shot[] shots = new Shot[0];
        [SerializeField, Tooltip("Seconds to ease from one framing to the next.")] float blendSeconds = 2.4f;
        [SerializeField, Tooltip("Hand-held drift radius (m).")] float drift = 0.35f;
        [SerializeField, Tooltip("Hand-held sway (degrees).")] float sway = 0.8f;
        [SerializeField] float driftPeriod = 26f;

        Shot _from, _to;
        float _blend = 1f;

        public MenuScreen CurrentShot => _to.screen;
        public Camera Camera => cam;
        public float BlendProgress => _blend;

        /// <summary>Build-time setup (UiSceneBuilder).</summary>
        public void Configure(Camera camera, Shot[] framings) { cam = camera; shots = framings; }

        void OnEnable()
        {
            Instance = this;
            if (cam == null) cam = GetComponentInChildren<Camera>();
            _to = _from = Find(MenuScreen.Title);
            _blend = 1f;
            Apply(_to, 0f);
        }

        void OnDisable() { if (Instance == this) Instance = null; }

        /// <summary>Starts easing to the framing for <paramref name="screen"/> (the title framing when it has none).</summary>
        public void SetShot(MenuScreen screen)
        {
            var next = Find(screen);
            if (next.screen == _to.screen && next.position == _to.position) return;
            _from = Current();
            _to = next;
            _blend = UiKit.ReducedMotion ? 1f : 0f;
        }

        Shot Find(MenuScreen s)
        {
            foreach (var sh in shots) if (sh.screen == s) return sh;
            foreach (var sh in shots) if (sh.screen == MenuScreen.Title) return sh;
            return shots.Length > 0 ? shots[0] : new Shot { position = new Vector3(0, 2, -10), fov = 50f };
        }

        Shot Current()
        {
            float k = Ease(_blend);
            return new Shot
            {
                screen = _to.screen,
                position = Vector3.Lerp(_from.position, _to.position, k),
                euler = Quaternion.Slerp(Quaternion.Euler(_from.euler), Quaternion.Euler(_to.euler), k).eulerAngles,
                fov = Mathf.Lerp(_from.fov, _to.fov, k),
            };
        }

        static float Ease(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

        void LateUpdate()
        {
            if (cam == null) return;
            float dt = Time.unscaledDeltaTime;
            if (_blend < 1f) _blend = Mathf.Min(1f, _blend + dt / Mathf.Max(0.05f, blendSeconds));
            Apply(Current(), UiKit.ReducedMotion ? 0f : 1f);
        }

        void Apply(Shot s, float motion)
        {
            if (cam == null) return;
            float t = Time.unscaledTime * Mathf.PI * 2f / Mathf.Max(1f, driftPeriod);
            var off = new Vector3(Mathf.Sin(t) * drift, Mathf.Sin(t * 1.7f + 1.3f) * drift * 0.35f, Mathf.Cos(t * 0.8f) * drift * 0.5f) * motion;
            var rot = Quaternion.Euler(s.euler) * Quaternion.Euler(Mathf.Sin(t * 1.3f) * sway * 0.4f * motion, Mathf.Sin(t * 0.9f + 0.5f) * sway * motion, 0f);
            cam.transform.SetPositionAndRotation(s.position + off, rot);
            if (s.fov > 1f) cam.fieldOfView = s.fov;
        }
    }
}
