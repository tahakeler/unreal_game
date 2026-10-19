using UnityEngine;

namespace HarvestPanic.Gameplay
{
    /// <summary>
    /// Owner-side first-person movement (design/gdd/player-character.md "Movement" and "Camera" 5a).
    /// Runs only on the owning client; the owner-authoritative NetworkTransform replicates the result.
    /// Speeds: walk/sprint/crouch from <see cref="PlayerTuning"/>, scaled by the server-computed carry multiplier.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMotor : MonoBehaviour
    {
        PlayerCharacter _pc;
        CharacterController _cc;
        PlayerInputReader _input;
        StaminaModel _stamina;
        Vector3 _velocity;
        float _yaw, _pitch;
        bool _crouched;

        /// <summary>Stamina model (owner only).</summary>
        public StaminaModel Stamina => _stamina;
        /// <summary>Current planar speed (m/s).</summary>
        public float PlanarSpeed { get; private set; }
        /// <summary>True while sprinting this frame.</summary>
        public bool IsSprinting { get; private set; }
        /// <summary>True while crouched.</summary>
        public bool IsCrouched => _crouched;
        /// <summary>Grounded this frame.</summary>
        public bool Grounded => _cc != null && _cc.isGrounded;
        /// <summary>Camera pitch in degrees (positive looks down).</summary>
        public float Pitch => _pitch;

        void Awake()
        {
            _pc = GetComponent<PlayerCharacter>();
            _cc = GetComponent<CharacterController>();
            _input = GetComponent<PlayerInputReader>();
        }

        /// <summary>Owner start-up.</summary>
        public void InitOwner()
        {
            _stamina = new StaminaModel(_pc.Tuning);
            _yaw = transform.eulerAngles.y;
        }

        /// <summary>Points the view (yaw/pitch degrees). Used by tests, spawns and teleports.</summary>
        public void SetLook(float yaw, float pitch)
        {
            _yaw = yaw;
            _pitch = Mathf.Clamp(pitch, -_pc.Tuning.PitchLimit, _pc.Tuning.PitchLimit);
            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            _pc.ApplyPitch(_pitch);
        }

        /// <summary>Turns the view toward a world point.</summary>
        public void LookAt(Vector3 worldPoint)
        {
            Vector3 d = worldPoint - _pc.HeadPivot.position;
            float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
            SetLook(yaw, pitch);
        }

        /// <summary>Clears momentum (teleport / get-up).</summary>
        public void ResetMotion() { _velocity = Vector3.zero; _yaw = transform.eulerAngles.y; }

        void Update()
        {
            if (_pc == null || !_pc.IsOwnerSafe || _stamina == null) return;
            var t = _pc.Tuning;
            float dt = Time.deltaTime;
            bool control = _pc.HasControl;
            float load = _pc.Carry != null ? _pc.Carry.LoadRatio : 0f;

            // Look (also while ragdolled the camera rides the head; no look input then).
            if (control)
            {
                Vector2 look = _input.Look;
                float scale = _input.LookIsStick ? t.GamepadLookSpeed * dt : t.MouseSensitivity;
                _yaw += look.x * scale;
                _pitch = Mathf.Clamp(_pitch - look.y * scale, -t.PitchLimit, t.PitchLimit);
                transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
                _pc.ApplyPitch(_pitch);
            }

            if (!control || !_cc.enabled)
            {
                IsSprinting = false;
                PlanarSpeed = 0f;
                _stamina.Tick(dt, false, 0f);
                _pc.PublishOwnerState(false, _crouched, _stamina.Value);
                return;
            }

            // Crouch (quiet-walk) with headroom check on stand-up.
            bool wantCrouch = _input.CrouchHeld;
            if (wantCrouch != _crouched && (wantCrouch || HasHeadroom())) SetCrouch(wantCrouch);

            Vector2 mv = Vector2.ClampMagnitude(_input.Move, 1f);
            bool sprint = _input.SprintHeld && !_crouched && mv.y > 0.1f && _stamina.CanSprint;
            float speed = _crouched ? t.CrouchSpeed : sprint ? t.SprintSpeed : t.WalkSpeed;
            speed *= _pc.Carry != null ? _pc.Carry.SpeedMultiplier : 1f;
            _stamina.Tick(dt, sprint, load);

            Vector3 wish = (transform.forward * mv.y + transform.right * mv.x) * speed;
            bool grounded = _cc.isGrounded;
            float accel = grounded ? t.Acceleration : t.AirAcceleration;
            Vector3 planar = new Vector3(_velocity.x, 0f, _velocity.z);
            planar = Vector3.MoveTowards(planar, wish, accel * dt);
            _velocity.x = planar.x; _velocity.z = planar.z;

            if (grounded && _velocity.y < 0f) _velocity.y = -2f;
            if (grounded && _input.JumpPressed && !_crouched) _velocity.y = Mathf.Sqrt(2f * t.Gravity * t.JumpApex);
            _velocity.y -= t.Gravity * dt;

            _cc.Move(_velocity * dt);
            PlanarSpeed = new Vector2(_cc.velocity.x, _cc.velocity.z).magnitude;
            IsSprinting = sprint && PlanarSpeed > t.WalkSpeed * 0.5f;
            _pc.PublishOwnerState(IsSprinting, _crouched, _stamina.Value);

            // Loaded sprint on a steep slope trips the player (rule 6); the owner predicts, the server confirms.
            if (IsSprinting && grounded && load >= t.TripLoadRatio && GroundSlope() > t.TripSlopeDegrees)
                _pc.OwnerTrip();
        }

        void SetCrouch(bool crouch)
        {
            var t = _pc.Tuning;
            _crouched = crouch;
            float h = crouch ? t.CrouchHeight : t.StandHeight;
            _cc.height = h;
            _cc.center = new Vector3(0f, h * 0.5f, 0f);
            _pc.SetEyeHeight(h * 0.5f + t.EyeAboveCentre * (h / t.StandHeight));
        }

        bool HasHeadroom()
        {
            var t = _pc.Tuning;
            Vector3 bottom = transform.position + Vector3.up * (t.Radius + 0.05f);
            return !Physics.SphereCast(bottom, t.Radius * 0.9f, Vector3.up, out _, t.StandHeight - t.Radius,
                ~(1 << gameObject.layer), QueryTriggerInteraction.Ignore);
        }

        float GroundSlope()
        {
            if (Physics.Raycast(transform.position + Vector3.up * 0.3f, Vector3.down, out var hit, 0.8f, ~(1 << gameObject.layer), QueryTriggerInteraction.Ignore))
                return Vector3.Angle(hit.normal, Vector3.up);
            return 0f;
        }
    }
}
