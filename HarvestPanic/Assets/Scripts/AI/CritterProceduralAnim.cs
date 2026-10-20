using HarvestPanic.Core;
using UnityEngine;

namespace HarvestPanic.AI
{
    /// <summary>
    /// Rig-free cartoon animation for single-mesh critters (Tripo models). Runs locally on every peer from replicated
    /// data only: the brain's networked <see cref="CritterState"/> and the root's motion (NetworkTransform-interpolated
    /// on clients), so it needs no extra sync. Drives one pivot transform (<see cref="body"/>, feet at local y = 0):
    /// waddle roll + bob at walk, hops with squash-and-stretch landings at run, Alert jitter, Carried wriggle, Stunned flop,
    /// PlayDead roll, idle breathing and look-arounds; the chicken adds flap shudders and pecks.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CritterProceduralAnim : MonoBehaviour
    {
        /// <summary>Species flavour (gait frequencies, hop height, flaps).</summary>
        public enum Style { Pig, Chicken }

        [SerializeField, Tooltip("Pivot child holding the mesh; its feet must be at local y = 0")] Transform body;
        [SerializeField] Style style = Style.Pig;
        [SerializeField, Tooltip("Height of the roll/tilt pivot above the feet (m)")] float pivotHeight = 0.35f;
        [SerializeField, Tooltip("Optional renderer for visibility culling")] Renderer cullRenderer;

        CritterBrain _brain;
        Vector3 _basePos, _baseScale;
        Quaternion _baseRot;
        Vector3 _lastRootPos;
        float _speed, _phase, _prevHop, _squash, _squashVel, _lookYaw, _lookTarget, _nextLook, _peck, _nextPeck;
        float _roll, _pitch, _yaw, _lift, _stateStart;
        CritterState _shown;
        float _seed;

        /// <summary>Configures references (editor builder).</summary>
        public void Configure(Transform bodyPivot, Style s, float pivot, Renderer r) { body = bodyPivot; style = s; pivotHeight = pivot; cullRenderer = r; }

        /// <summary>Current smoothed horizontal speed (m/s), for tests and debug.</summary>
        public float Speed => _speed;

        void Awake()
        {
            _brain = GetComponent<CritterBrain>();
            if (body != null) { _basePos = body.localPosition; _baseRot = body.localRotation; _baseScale = body.localScale; }
            _lastRootPos = transform.position;
            _seed = (GetInstanceID() & 1023) * 0.618f;
            _nextLook = Time.time + 2f + Hash(1) * 3f;
            _nextPeck = Time.time + 1f + Hash(2) * 2f;
        }

        float Hash(float k) => Mathf.Repeat(Mathf.Sin((_seed + k) * 12.9898f) * 43758.547f, 1f);

        void LateUpdate()
        {
            if (body == null) return;
            float dt = Mathf.Clamp(Time.deltaTime, 1e-4f, 0.1f);
            var s = _brain != null ? _brain.State : CritterState.Idle;
            if (s != _shown) { _shown = s; _stateStart = Time.time; }
            float st = Time.time - _stateStart;

            Vector3 d = transform.position - _lastRootPos; d.y = 0f;
            _lastRootPos = transform.position;
            float raw = d.magnitude / dt;
            if (raw > 30f) raw = 0f;                                // teleport / respawn
            _speed = Mathf.Lerp(_speed, raw, 1f - Mathf.Exp(-dt * 10f));
            if (cullRenderer != null && !cullRenderer.isVisible) return;

            bool chicken = style == Style.Chicken;
            float walkRef = _brain != null && _brain.Personality != null ? Mathf.Max(0.5f, _brain.Personality.MoveSpeed * 0.5f) : 1.2f;
            float sprint = _brain != null && _brain.Personality != null ? _brain.Personality.SprintSpeed : 5f;
            bool held = s == CritterState.Carried || s == CritterState.Stunned;
            float move = held ? 0f : Mathf.Clamp01(_speed / walkRef);                          // 0 idle .. 1 walking
            float run = held ? 0f : Mathf.Clamp01((_speed - walkRef * 1.3f) / Mathf.Max(0.1f, sprint * 0.6f - walkRef * 1.3f));

            // Gait phase: steps per second rise with speed.
            float hz = chicken ? Mathf.Lerp(3.2f, 6.5f, run) : Mathf.Lerp(2.2f, 4.2f, run);
            if (move > 0.02f) _phase += dt * hz * Mathf.Lerp(0.6f, 1f, move);
            float w = _phase * Mathf.PI * 2f;

            float roll = 0f, pitch = 0f, yaw = 0f, lift = 0f, jx = 0f, jz = 0f, breathe = 0f;

            // Walk: side-to-side waddle roll + double bob; Run: hops with forward lean.
            float waddle = (chicken ? 7f : 9f) * move * (1f - run);
            roll += Mathf.Sin(w) * waddle;
            yaw += Mathf.Sin(w) * waddle * 0.4f;
            lift += Mathf.Abs(Mathf.Sin(w)) * (chicken ? 0.025f : 0.03f) * move * (1f - run);
            float hopH = chicken ? 0.12f : 0.14f;
            float hop = Mathf.Max(0f, Mathf.Sin(w)) * hopH * run;
            lift += hop;
            pitch += (chicken ? 14f : 8f) * run - Mathf.Cos(w) * 5f * run;
            if (_prevHop > 0.01f && hop <= 0.001f && run > 0.3f) _squashVel -= 3.5f * run;    // landing thump
            _prevHop = hop;
            if (chicken && run > 0.2f) roll += Mathf.Sin(Time.time * 41f + _seed) * 6f * run; // panicked flap shudder

            // Idle: breathing, look-arounds, chicken pecks.
            float idle = held ? 0f : 1f - move;
            breathe = Mathf.Sin(Time.time * (chicken ? 3.1f : 2.1f) + _seed) * 0.018f * idle;
            if (Time.time > _nextLook)
            {
                _lookTarget = Mathf.Abs(_lookTarget) > 1f ? 0f : (Hash(Time.time) - 0.5f) * 70f;
                _nextLook = Time.time + (_lookTarget == 0f ? 2.5f + Hash(Time.time + 3f) * 3f : 0.9f + Hash(Time.time + 5f) * 0.8f);
            }
            _lookYaw = Mathf.Lerp(_lookYaw, _lookTarget * idle, 1f - Mathf.Exp(-dt * 6f));
            yaw += _lookYaw;
            if (chicken)
            {
                if (Time.time > _nextPeck && idle > 0.8f) { _peck = 1f; _nextPeck = Time.time + 1.2f + Hash(Time.time + 7f) * 3f; }
                _peck = Mathf.Max(0f, _peck - dt * 3.5f);
                pitch += Mathf.Sin(_peck * Mathf.PI) * 28f * idle;
            }

            float tell = _brain != null ? _brain.Tuning.AlertTellSeconds : 0.4f;
            switch (s)
            {
                case CritterState.Alert:
                    // startled hop, then nervous jitter while it decides
                    if (st < tell) { lift += Mathf.Sin(st / tell * Mathf.PI) * (chicken ? 0.16f : 0.12f); if (st < dt * 1.5f) _squashVel += 3f; }
                    jx += (Mathf.PerlinNoise(Time.time * 18f, _seed) - 0.5f) * 0.03f;
                    jz += (Mathf.PerlinNoise(_seed, Time.time * 18f) - 0.5f) * 0.03f;
                    roll += Mathf.Sin(Time.time * 37f) * 3f;
                    break;
                case CritterState.Lured when move < 0.3f:
                    pitch += (chicken ? 30f : 14f) * (0.6f + 0.4f * Mathf.Sin(Time.time * (chicken ? 9f : 5f)));   // eating nods
                    break;
                case CritterState.Carried:
                    // struggling wriggle; chickens flap (fast roll + stretch pulses)
                    roll += Mathf.Sin(Time.time * 23f) * 16f + (chicken ? Mathf.Sin(Time.time * 47f) * 10f : 0f);
                    pitch += Mathf.Sin(Time.time * 17f) * 12f;
                    yaw += Mathf.Sin(Time.time * 29f) * 20f;
                    breathe += Mathf.Sin(Time.time * (chicken ? 30f : 14f)) * (chicken ? 0.06f : 0.04f);
                    break;
                case CritterState.Stunned:
                    // flop onto the side with a bounce, then dazed wobble
                    float k = Mathf.Clamp01(st * 5f);
                    float flop = 80f * (k + Mathf.Sin(k * Mathf.PI) * 0.15f);
                    roll += flop + Mathf.Sin(Time.time * 7f) * 6f * k;
                    yaw += Mathf.Sin(Time.time * 3f) * 10f * k;
                    lift -= pivotHeight * 0.35f * k;
                    if (st < dt * 1.5f) _squashVel -= 4f;
                    break;
                case CritterState.Cornered when _brain != null && _brain.Personality != null && _brain.Personality.CorneredResponse == CorneredResponse.PlayDead:
                    roll += Mathf.Min(1f, st * 4f) * 90f;
                    lift -= pivotHeight * 0.35f * Mathf.Min(1f, st * 4f);
                    break;
                case CritterState.Cornered:
                    jx += Mathf.Sin(Time.time * 31f) * 0.02f;
                    break;
            }

            // Squash-and-stretch spring (volume preserving).
            _squashVel += (-_squash * 180f - _squashVel * 14f) * dt;
            _squash = Mathf.Clamp(_squash + _squashVel * dt, -0.35f, 0.35f);
            if (run > 0.2f) breathe += hop / hopH * 0.08f * run;                                   // stretch in the air

            // Smooth the big pose channels to avoid pops at state changes (jitter stays crisp).
            float a = 1f - Mathf.Exp(-dt * 14f);
            _roll = Mathf.Lerp(_roll, roll, a); _pitch = Mathf.Lerp(_pitch, pitch, a); _yaw = Mathf.Lerp(_yaw, yaw, a);
            _lift = Mathf.Lerp(_lift, lift, 1f - Mathf.Exp(-dt * 25f));

            float sy = 1f + _squash + breathe;
            float sxz = 1f / Mathf.Sqrt(Mathf.Max(0.3f, sy));
            var rot = Quaternion.Euler(_pitch, _yaw, _roll);
            Vector3 pivot = Vector3.up * pivotHeight;
            body.localRotation = _baseRot * rot;
            body.localPosition = _basePos + new Vector3(jx, _lift, jz) + (pivot - rot * pivot);
            body.localScale = Vector3.Scale(_baseScale, new Vector3(sxz, sy, sxz));
        }
    }
}
