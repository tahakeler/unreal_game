using System.Collections.Generic;
using HarvestPanic.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HarvestPanic.Camcorder
{
    /// <summary>Viewer camera modes (GDD 12.7; Item Cam is a later addition).</summary>
    public enum ViewerCam { Grandpa = 0, Victim = 1, Free = 2 }

    /// <summary>
    /// Plays a <see cref="Moment"/> on visual-only ghost copies (no physics, scripts or networking) with its own camera.
    /// Live tracked renderers are hidden with <c>forceRenderingOff</c> and every other camera is disabled while open;
    /// both are restored on close. Playback uses unscaled time so it works while the solo game is paused.
    /// Grandpa Cam = slow auto-orbit around the disaster with 0.5x slow-mo around the impact (off with reduced motion).
    /// </summary>
    public sealed class ReplayViewer : MonoBehaviour
    {
        public Moment Current { get; private set; }
        public bool IsOpen => Current != null;
        public float Time { get; private set; }
        public float Speed { get; set; } = 1f;
        public bool Playing { get; set; }
        public bool Loop { get; set; } = true;
        public ViewerCam Mode { get; set; } = ViewerCam.Grandpa;
        public bool AutoSlowMo { get; set; } = true;
        public Camera Cam { get; private set; }
        /// <summary>True while an export drives the clock (no input, no auto-advance).</summary>
        public bool Exporting { get; internal set; }

        CamcorderService _svc;
        Transform _root;
        readonly Dictionary<int, GameObject> _ghosts = new Dictionary<int, GameObject>();
        readonly List<Renderer> _hidden = new List<Renderer>();
        readonly List<Camera> _disabledCams = new List<Camera>();
        float _yaw = 35f, _pitch = 28f, _dist = 14f;
        Vector3 _freePos;
        float _freeYaw, _freePitch;
        float _lastUserInput = -99f;

        public float EffectiveSpeed
        {
            get
            {
                float s = Speed;
                if (Mode == ViewerCam.Grandpa && AutoSlowMo && CameraComfort.FovKickScale > 0f && Current != null && !Current.Manual)
                {
                    float w = _svc != null ? _svc.Tuning.slowMoWindow : 1.5f;
                    if (Mathf.Abs(Time - Current.TriggerTime) <= w * 0.5f) s *= _svc != null ? _svc.Tuning.slowMoFactor : 0.5f;
                }
                return s;
            }
        }

        public void Open(Moment m, CamcorderService svc)
        {
            if (IsOpen) Close();
            _svc = svc;
            Current = m;
            Time = 0f; Playing = true; Speed = 1f;
            _root = new GameObject("CamcorderReplay").transform;
            foreach (var t in m.Tracks)
            {
                var vis = (svc != null ? svc.VisualFor(t.Id) : null) ?? GhostVisual.Capture(null, t.Kind);
                var g = vis.Build(_root, $"Ghost_{t.Kind}_{t.Id}");
                _ghosts[t.Id] = g;
            }
            // Hide the live world actors and the gameplay cameras.
            _hidden.Clear();
            svc?.CollectLiveRenderers(_hidden);
            for (int i = _hidden.Count - 1; i >= 0; i--) { if (_hidden[i].forceRenderingOff) _hidden.RemoveAt(i); else _hidden[i].forceRenderingOff = true; }
            _disabledCams.Clear();
            foreach (var c in Camera.allCameras) if (c.enabled) { c.enabled = false; _disabledCams.Add(c); }
            var camGo = new GameObject("CamcorderCam");
            camGo.transform.SetParent(_root, false);
            Cam = camGo.AddComponent<Camera>();
            Cam.fieldOfView = 55f; Cam.nearClipPlane = 0.1f; Cam.farClipPlane = 600f; Cam.depth = 50;
            var focus = FocusPoint();
            _freePos = focus + new Vector3(-8f, 6f, -8f);
            var look = Quaternion.LookRotation(focus - _freePos).eulerAngles;
            _freeYaw = look.y; _freePitch = look.x;
            InputModal.Push(this);
            Apply();
            UpdateCamera(0f);
        }

        public void Close()
        {
            if (!IsOpen) return;
            foreach (var r in _hidden) if (r != null) r.forceRenderingOff = false;
            _hidden.Clear();
            foreach (var c in _disabledCams) if (c != null) c.enabled = true;
            _disabledCams.Clear();
            if (_root != null) Destroy(_root.gameObject);
            _ghosts.Clear();
            Cam = null; Current = null; Exporting = false;
            InputModal.Pop(this);
        }

        void OnDestroy() => Close();

        public Transform GhostFor(int id) => _ghosts.TryGetValue(id, out var g) && g != null ? g.transform : null;

        public void SetTime(float t)
        {
            if (!IsOpen) return;
            Time = Mathf.Clamp(t, 0f, Current.Duration);
            Apply();
        }

        public void CycleCamera() => Mode = (ViewerCam)(((int)Mode + 1) % 3);

        /// <summary>Moves every ghost to its recorded pose at <see cref="Time"/>.</summary>
        public void Apply()
        {
            if (!IsOpen) return;
            foreach (var kv in _ghosts)
            {
                if (kv.Value == null) continue;
                if (Current.TryEvaluate(kv.Key, Time, out var p, out var r, out var flags))
                {
                    kv.Value.transform.SetPositionAndRotation(p, r);
                    bool vis = (flags & SampleFlags.Visible) != 0;
                    if (kv.Value.activeSelf != vis) kv.Value.SetActive(vis);
                }
                else if (kv.Value.activeSelf) kv.Value.SetActive(false);
            }
        }

        void Update()
        {
            if (!IsOpen || Exporting) return;
            float dt = UnityEngine.Time.unscaledDeltaTime;
            if (Playing)
            {
                Time += dt * EffectiveSpeed;
                if (Time >= Current.Duration) { if (Loop) Time = 0f; else { Time = Current.Duration; Playing = false; } }
            }
            HandleKeys();
            Apply();
            UpdateCamera(dt);
        }

        void HandleKeys()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.spaceKey.wasPressedThisFrame) Playing = !Playing;
            if (kb.cKey.wasPressedThisFrame) CycleCamera();
            if (kb.digit1Key.wasPressedThisFrame) Speed = 0.25f;
            if (kb.digit2Key.wasPressedThisFrame) Speed = 0.5f;
            if (kb.digit3Key.wasPressedThisFrame) Speed = 1f;
            if (kb.leftArrowKey.wasPressedThisFrame) SetTime(Time - 2f);
            if (kb.rightArrowKey.wasPressedThisFrame) SetTime(Time + 2f);
        }

        Vector3 FocusPoint()
        {
            if (Current == null) return Vector3.zero;
            if (Current.Focus != Vector3.zero) return Current.Focus;
            int id = Current.PlayerTrackFor(Current.PrimaryInstigator);
            var g = GhostFor(id);
            return g != null ? g.position : Vector3.zero;
        }

        /// <summary>Positions the viewer camera for the current mode (also used by export at fixed steps).</summary>
        public void UpdateCamera(float dt)
        {
            if (Cam == null) return;
            var mouse = Mouse.current;
            var kb = Keyboard.current;
            Vector2 look = Vector2.zero;
            bool rmb = mouse != null && mouse.rightButton.isPressed && !Exporting;
            if (rmb) { look = mouse.delta.ReadValue() * 0.15f; _lastUserInput = UnityEngine.Time.unscaledTime; }
            if (mouse != null && !Exporting) { float sc = mouse.scroll.ReadValue().y; if (Mathf.Abs(sc) > 0.01f) _dist = Mathf.Clamp(_dist - Mathf.Sign(sc), 3f, 40f); }

            switch (Mode)
            {
                case ViewerCam.Grandpa:
                {
                    if (UnityEngine.Time.unscaledTime - _lastUserInput > 2f) _yaw += dt * 9f; // slow wide drift
                    _yaw += look.x; _pitch = Mathf.Clamp(_pitch - look.y, 5f, 80f);
                    var focus = FocusPoint();
                    var rot = Quaternion.Euler(_pitch, _yaw, 0f);
                    Cam.transform.SetPositionAndRotation(focus + rot * new Vector3(0f, 0f, -_dist), rot);
                    break;
                }
                case ViewerCam.Victim:
                {
                    var g = GhostFor(Current.PlayerTrackFor(Current.PrimaryInstigator));
                    if (g == null) { Mode = ViewerCam.Grandpa; break; }
                    var fwd = Vector3.ProjectOnPlane(g.forward, Vector3.up);
                    if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
                    var target = g.position + Vector3.up * 1.4f;
                    var want = target - fwd.normalized * 4.5f + Vector3.up * 1.6f;
                    float k = dt <= 0f ? 1f : 1f - Mathf.Exp(-6f * dt);
                    Cam.transform.position = Vector3.Lerp(Cam.transform.position, want, k);
                    Cam.transform.rotation = Quaternion.LookRotation(target - Cam.transform.position);
                    break;
                }
                case ViewerCam.Free:
                {
                    _freeYaw += look.x; _freePitch = Mathf.Clamp(_freePitch - look.y, -85f, 85f);
                    var rot = Quaternion.Euler(_freePitch, _freeYaw, 0f);
                    if (kb != null && !Exporting)
                    {
                        var v = Vector3.zero;
                        if (kb.wKey.isPressed) v += Vector3.forward; if (kb.sKey.isPressed) v += Vector3.back;
                        if (kb.aKey.isPressed) v += Vector3.left; if (kb.dKey.isPressed) v += Vector3.right;
                        if (kb.eKey.isPressed) v += Vector3.up; if (kb.qKey.isPressed) v += Vector3.down;
                        float spd = kb.leftShiftKey.isPressed ? 20f : 8f;
                        _freePos += rot * v * spd * dt;
                    }
                    Cam.transform.SetPositionAndRotation(_freePos, rot);
                    break;
                }
            }
        }
    }
}
