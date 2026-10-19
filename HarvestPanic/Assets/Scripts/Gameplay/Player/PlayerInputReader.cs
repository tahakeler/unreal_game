using UnityEngine;
using UnityEngine.InputSystem;

namespace HarvestPanic.Gameplay
{
    /// <summary>
    /// Owner-only input facade over <c>Prefabs/Player/Input/PlayerControls.inputactions</c> (rebindable through the
    /// Input System). Gameplay scripts read these properties instead of devices, so tests and bots can drive a
    /// player by setting <see cref="Scripted"/> and <see cref="UseScripted"/>.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class PlayerInputReader : MonoBehaviour
    {
        /// <summary>Scripted input used when <see cref="UseScripted"/> is set (tests, bots).</summary>
        public struct ScriptedFrame
        {
            public Vector2 Move, Look;
            public bool Sprint, Crouch, InteractHeld, ThrowHeld, UseHeld;
            public bool JumpPressed, InteractPressed, InteractReleased, ThrowReleased, UsePressed, UseReleased, HollerPressed;
        }

        [SerializeField] InputActionAsset actions;

        /// <summary>When true, input comes from <see cref="Scripted"/> instead of devices.</summary>
        public bool UseScripted;
        /// <summary>Scripted frame; one-shot "Pressed/Released" flags are cleared after each frame.</summary>
        public ScriptedFrame Scripted;

        InputActionAsset _instance;
        InputAction _move, _look, _jump, _sprint, _crouch, _interact, _throw, _use, _holler;
        bool _active;

        /// <summary>Activates device input (called for the owning player only).</summary>
        public void Activate()
        {
            if (_active || actions == null) { _active = true; return; }
            _active = true;
            _instance = Instantiate(actions);
            var map = _instance.FindActionMap("Player", true);
            _move = map.FindAction("Move"); _look = map.FindAction("Look"); _jump = map.FindAction("Jump");
            _sprint = map.FindAction("Sprint"); _crouch = map.FindAction("Crouch"); _interact = map.FindAction("Interact");
            _throw = map.FindAction("Throw"); _use = map.FindAction("Use"); _holler = map.FindAction("Holler");
            map.Enable();
        }

        void OnDestroy()
        {
            if (_instance != null) { _instance.Disable(); Destroy(_instance); }
        }

        void LateUpdate()
        {
            Scripted.JumpPressed = Scripted.InteractPressed = Scripted.InteractReleased = false;
            Scripted.ThrowReleased = Scripted.UsePressed = Scripted.UseReleased = Scripted.HollerPressed = false;
        }

        bool Dev => _active && !UseScripted && _instance != null;

        public Vector2 Move => UseScripted ? Scripted.Move : Dev ? _move.ReadValue<Vector2>() : Vector2.zero;
        /// <summary>Look delta: mouse pixels or stick (-1..1); <see cref="LookIsStick"/> tells which.</summary>
        public Vector2 Look => UseScripted ? Scripted.Look : Dev ? _look.ReadValue<Vector2>() : Vector2.zero;
        public bool LookIsStick => Dev && _look.activeControl != null && _look.activeControl.device is Gamepad;
        public bool JumpPressed => UseScripted ? Scripted.JumpPressed : Dev && _jump.WasPressedThisFrame();
        public bool SprintHeld => UseScripted ? Scripted.Sprint : Dev && _sprint.IsPressed();
        public bool CrouchHeld => UseScripted ? Scripted.Crouch : Dev && _crouch.IsPressed();
        public bool InteractPressed => UseScripted ? Scripted.InteractPressed : Dev && _interact.WasPressedThisFrame();
        public bool InteractHeld => UseScripted ? Scripted.InteractHeld : Dev && _interact.IsPressed();
        public bool InteractReleased => UseScripted ? Scripted.InteractReleased : Dev && _interact.WasReleasedThisFrame();
        public bool ThrowHeld => UseScripted ? Scripted.ThrowHeld : Dev && _throw.IsPressed();
        public bool ThrowReleased => UseScripted ? Scripted.ThrowReleased : Dev && _throw.WasReleasedThisFrame();
        public bool UsePressed => UseScripted ? Scripted.UsePressed : Dev && _use.WasPressedThisFrame();
        public bool UseReleased => UseScripted ? Scripted.UseReleased : Dev && _use.WasReleasedThisFrame();
        public bool HollerPressed => UseScripted ? Scripted.HollerPressed : Dev && _holler.WasPressedThisFrame();
    }
}
