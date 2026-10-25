using HarvestPanic.Core;
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
            /// <summary>One-shot hotbar pick, 1-based (1 = Hands ... 5 = Whistle); 0 = none.</summary>
            public int ToolSlot;
            /// <summary>One-shot hotbar step: -1 previous, +1 next, 0 none.</summary>
            public int ToolCycle;
        }

        /// <summary>Hotbar slot actions in order (ToolSlot1 = Hands ... ToolSlot5 = Whistle).</summary>
        public const int ToolSlotCount = 5;

        [SerializeField] InputActionAsset actions;

        /// <summary>When true, input comes from <see cref="Scripted"/> instead of devices.</summary>
        public bool UseScripted;
        /// <summary>Scripted frame; one-shot "Pressed/Released" flags are cleared after each frame.</summary>
        public ScriptedFrame Scripted;

        InputActionAsset _instance;
        InputAction _move, _look, _jump, _sprint, _crouch, _interact, _throw, _use, _holler;
        InputAction _toolNext, _toolPrev, _toolScroll;
        readonly InputAction[] _toolSlots = new InputAction[ToolSlotCount];
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
            // Hotbar (farm-tools rule 1 / GDD 4.8 "Tool slot swap"); optional so older assets still load.
            for (int i = 0; i < ToolSlotCount; i++) _toolSlots[i] = map.FindAction("ToolSlot" + (i + 1));
            _toolNext = map.FindAction("ToolNext"); _toolPrev = map.FindAction("ToolPrev"); _toolScroll = map.FindAction("ToolScroll");
            map.Enable();
        }

        void OnDestroy()
        {
            if (_instance != null) { _instance.Disable(); Destroy(_instance); }
        }

        void Update()
        {
            // Cursor: locked for FPS control, released while a UI modal (wardrobe, menus) blocks input.
            if (!_active || UseScripted || Application.isBatchMode) return;
            bool blocking = InputModal.IsBlocking;
            var want = blocking ? CursorLockMode.None : CursorLockMode.Locked;
            if (Cursor.lockState != want) { Cursor.lockState = want; Cursor.visible = blocking; }
        }

        void OnDisable()
        {
            if (_active && !Application.isBatchMode) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        }

        void LateUpdate()
        {
            Scripted.JumpPressed = Scripted.InteractPressed = Scripted.InteractReleased = false;
            Scripted.ThrowReleased = Scripted.UsePressed = Scripted.UseReleased = Scripted.HollerPressed = false;
            Scripted.ToolSlot = 0; Scripted.ToolCycle = 0;
        }

        /// <summary>Device input is live: active owner, not scripted, and no UI modal (Core InputModal) is open.</summary>
        bool Dev => _active && !UseScripted && _instance != null && !InputModal.IsBlocking;

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

        /// <summary>Hotbar slot picked this frame (0 = Hands ... 4 = Whistle), or -1.</summary>
        public int ToolSlotPressed
        {
            get
            {
                if (UseScripted) return Scripted.ToolSlot > 0 ? Scripted.ToolSlot - 1 : -1;
                if (!Dev) return -1;
                for (int i = 0; i < ToolSlotCount; i++) if (_toolSlots[i] != null && _toolSlots[i].WasPressedThisFrame()) return i;
                return -1;
            }
        }

        /// <summary>Hotbar step this frame: +1 next (RB, wheel down), -1 previous (LB, wheel up), 0 none.</summary>
        public int ToolCycle
        {
            get
            {
                if (UseScripted) return Mathf.Clamp(Scripted.ToolCycle, -1, 1);
                if (!Dev) return 0;
                if (_toolNext != null && _toolNext.WasPressedThisFrame()) return 1;
                if (_toolPrev != null && _toolPrev.WasPressedThisFrame()) return -1;
                if (_toolScroll != null)
                {
                    float y = _toolScroll.ReadValue<float>();
                    if (y > 0.01f) return -1;
                    if (y < -0.01f) return 1;
                }
                return 0;
            }
        }

        /// <summary>Display text of the key bound to hotbar slot <paramref name="slot"/> (0-based), e.g. "1" or "Q".</summary>
        public string ToolSlotKey(int slot)
        {
            var a = slot >= 0 && slot < ToolSlotCount ? _toolSlots[slot] : null;
            if (a == null) return slot == ToolSlotCount - 1 ? "Q" : (slot + 1).ToString();
            string s = a.GetBindingDisplayString(InputBinding.MaskByGroup("KeyboardMouse"));
            return string.IsNullOrEmpty(s) ? (slot + 1).ToString() : s.ToUpperInvariant();
        }
    }
}
