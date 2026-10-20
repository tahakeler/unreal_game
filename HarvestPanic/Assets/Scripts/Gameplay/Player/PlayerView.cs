using HarvestPanic.Core;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;

namespace HarvestPanic.Gameplay
{
    /// <summary>
    /// Presentation of the player (design/gdd/player-character.md "Camera and view" 5a-5e):
    /// owner sees a Cinemachine first-person camera with head bob, sprint FOV kick and impact shake, plus
    /// procedurally posed first-person arms (<see cref="FirstPersonArmsRig"/>); the owner's own body only casts shadows (owner-no-see) until it
    /// ragdolls. Everyone else sees the full third-person body, whose arms reach forward while carrying.
    /// No gameplay state lives here.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public sealed class PlayerView : MonoBehaviour
    {
        [Header("First-person")]
        [SerializeField] CinemachineCamera vcam;
        [Header("Third-person body pivots")]
        [SerializeField] Transform bodyArmL, bodyArmR, bodyLegL, bodyLegR;

        [Tooltip("QA: render this player as remote players see it (body visible, no arms) even when local.")]
        public bool ForceRemoteLook;

        PlayerCharacter _pc;
        PlayerCarry _carry;
        PlayerMotor _motor;
        readonly List<Renderer> _bodyRenderers = new List<Renderer>();
        float _bobPhase, _trauma, _fov, _walkPhase, _rescanAt;

        void Awake()
        {
            _pc = GetComponent<PlayerCharacter>();
            _carry = GetComponent<PlayerCarry>();
            _motor = GetComponent<PlayerMotor>();
            _fov = _pc.Tuning.BaseFov;
        }

        /// <summary>Adds camera trauma (0..1 adds up, clamped).</summary>
        public void AddTrauma(float amount) => _trauma = Mathf.Clamp01(_trauma + Mathf.Max(0f, amount));

        bool LocalView => _pc.IsOwnerSafe && !ForceRemoteLook;
        bool BodyDown => PlayerStateMachine.IsPhysicsDriven(_pc.State) || _pc.State == PlayerState.Respawning;

        /// <summary>Re-applies camera/arms/body visibility for the current owner and state.</summary>
        public void ApplyViewMode()
        {
            bool local = LocalView;
            bool down = BodyDown;
            if (_pc.ViewCamera != null) _pc.ViewCamera.SetActive(_pc.IsOwnerSafe);
            if (vcam != null) vcam.gameObject.SetActive(_pc.IsOwnerSafe);
            if (_pc.FirstPersonArms != null) _pc.FirstPersonArms.gameObject.SetActive(local && !down);
            RescanBody();
            var mode = local && !down ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
            foreach (var r in _bodyRenderers) if (r != null) r.shadowCastingMode = mode;
        }

        void RescanBody()
        {
            _bodyRenderers.Clear();
            if (_pc.BodyRoot != null) _pc.BodyRoot.GetComponentsInChildren(true, _bodyRenderers);
        }

        void Update()
        {
            // Customization may swap the body at runtime: keep owner-no-see in force.
            if (Time.time >= _rescanAt) { _rescanAt = Time.time + 1f; ApplyViewMode(); }
            if (_pc.IsOwnerSafe) UpdateCameraFeel(Time.deltaTime);
        }

        void LateUpdate()
        {
            PoseBody(Time.deltaTime);
        }

        void UpdateCameraFeel(float dt)
        {
            var t = _pc.Tuning;
            var pivot = _pc.CameraPivot;
            float speedRatio = _motor != null ? Mathf.Clamp(_motor.PlanarSpeed / Mathf.Max(0.01f, t.WalkSpeed), 0f, 1.8f) : 0f;
            bool grounded = _motor == null || _motor.Grounded;
            bool down = BodyDown;

            // Head bob scaled by ground speed (5d).
            Vector3 bob = Vector3.zero;
            if (!down && grounded && speedRatio > 0.05f)
            {
                _bobPhase += dt * t.HeadBobFrequency * Mathf.PI * 2f * Mathf.Max(0.6f, speedRatio);
                float amp = t.HeadBobAmplitude * speedRatio * CameraComfort.BobScale;
                bob = new Vector3(Mathf.Cos(_bobPhase) * amp * 0.5f, Mathf.Sin(_bobPhase * 2f) * amp, 0f);
            }

            // Impact shake: trauma^2 * max degrees, decaying (5d).
            _trauma = Mathf.Max(0f, _trauma - t.ShakeDecay * dt);
            float shake = t.ShakeMaxDegrees * _trauma * _trauma * CameraComfort.ShakeScale;
            float n = Time.time * 25f;
            Quaternion shakeRot = Quaternion.Euler(
                shake * (Mathf.PerlinNoise(n, 0.1f) * 2f - 1f),
                shake * (Mathf.PerlinNoise(0.2f, n) * 2f - 1f),
                shake * (Mathf.PerlinNoise(n, n) * 2f - 1f));

            pivot.localPosition = Vector3.Lerp(pivot.localPosition, bob, 1f - Mathf.Exp(-18f * dt));
            pivot.localRotation = shakeRot;

            // Sprint FOV kick.
            float baseFov = CameraComfort.BaseFov > 0f ? CameraComfort.BaseFov : t.BaseFov;
            float targetFov = baseFov + (_motor != null && _motor.IsSprinting ? t.SprintFovKick * CameraComfort.FovKickScale : 0f);
            _fov = Mathf.Lerp(_fov, targetFov, 1f - Mathf.Exp(-t.FovLerpSpeed * dt));
            if (vcam != null)
            {
                var lens = vcam.Lens;
                lens.FieldOfView = _fov;
                vcam.Lens = lens;
            }
        }

        /// <summary>Poses the first-person arms immediately (previews, QA captures).</summary>
        public void PoseArmsNow()
        {
            if (_pc.FirstPersonArms != null && _pc.FirstPersonArms.TryGetComponent<FirstPersonArmsRig>(out var rig)) rig.PoseNow();
        }

        void PoseBody(float dt)
        {
            if (bodyLegL == null) return;
            Vector3 v = _pc.Velocity; v.y = 0f;
            float speed = v.magnitude;
            bool down = BodyDown;
            float swing = down ? 0f : Mathf.Clamp01(speed / _pc.Tuning.WalkSpeed) * 35f;
            _walkPhase += dt * speed * 3.2f;
            float s = Mathf.Sin(_walkPhase) * swing;
            float k = 1f - Mathf.Exp(-12f * dt);

            bodyLegL.localRotation = Quaternion.Slerp(bodyLegL.localRotation, Quaternion.Euler(s, 0f, 0f), k);
            bodyLegR.localRotation = Quaternion.Slerp(bodyLegR.localRotation, Quaternion.Euler(-s, 0f, 0f), k);

            bool holding = _carry != null && _carry.IsHoldingReplicated;
            Quaternion al, ar;
            if (down) { al = Quaternion.Euler(0f, 0f, -70f); ar = Quaternion.Euler(0f, 0f, 70f); }
            else if (holding) { al = Quaternion.Euler(-80f, 0f, 8f); ar = Quaternion.Euler(-80f, 0f, -8f); }
            else { al = Quaternion.Euler(-s * 0.8f, 0f, 0f); ar = Quaternion.Euler(s * 0.8f, 0f, 0f); }
            bodyArmL.localRotation = Quaternion.Slerp(bodyArmL.localRotation, al, k);
            bodyArmR.localRotation = Quaternion.Slerp(bodyArmR.localRotation, ar, k);
        }
    }
}
