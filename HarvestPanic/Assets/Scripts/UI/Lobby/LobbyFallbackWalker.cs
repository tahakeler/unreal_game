using UnityEngine;
using UnityEngine.InputSystem;

namespace HarvestPanic.UI
{
    /// <summary>
    /// Stand-in first-person walker so the Lobby is walkable before (or without) the Player team's prefab.
    /// <see cref="LobbyController"/> enables it only when no real player shows up.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class LobbyFallbackWalker : MonoBehaviour
    {
        [SerializeField] Transform head;
        [SerializeField] float speed = 4.5f;
        CharacterController _cc;
        float _pitch, _vy;

        void Awake() => _cc = GetComponent<CharacterController>();

        void Update()
        {
            if (UiFocus.ModalCount > 0) return;
            var kb = Keyboard.current; var mouse = Mouse.current; var pad = Gamepad.current;
            Vector2 move = Vector2.zero, look = Vector2.zero;
            if (kb != null)
            {
                move.x = (kb.dKey.isPressed ? 1 : 0) - (kb.aKey.isPressed ? 1 : 0);
                move.y = (kb.wKey.isPressed ? 1 : 0) - (kb.sKey.isPressed ? 1 : 0);
            }
            if (pad != null) { move += pad.leftStick.ReadValue(); look += pad.rightStick.ReadValue() * 120f * Time.deltaTime; }
            if (mouse != null) look += mouse.delta.ReadValue() * 0.1f * SettingsService.Current.Sensitivity * (SettingsService.Current.InvertY ? 1f : 1f);
            transform.Rotate(0f, look.x, 0f);
            float inv = SettingsService.Current.InvertY ? 1f : -1f;
            _pitch = Mathf.Clamp(_pitch + look.y * inv, -85f, 85f);
            if (head != null) head.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
            var cam = head != null ? head.GetComponent<Camera>() : null;
            if (cam != null) cam.fieldOfView = SettingsService.Current.Fov;

            _vy = _cc.isGrounded ? -1f : _vy - 20f * Time.deltaTime;
            var dir = transform.right * move.x + transform.forward * move.y;
            _cc.Move((Vector3.ClampMagnitude(dir, 1f) * speed + Vector3.up * _vy) * Time.deltaTime);
        }
    }
}
