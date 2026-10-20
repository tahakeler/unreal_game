using UnityEngine;

namespace HarvestPanic.Gameplay
{
    /// <summary>
    /// Dev fallback HUD (owner only): centre dot, carry prompt, stamina and KO text via IMGUI.
    /// The real HUD (Economy+HUD team) should subscribe to <see cref="PlayerCarry.OnPromptChanged"/> and read
    /// <see cref="PlayerCarry.Prompt"/>/<see cref="PlayerCharacter.StaminaReplicated"/>; disable this then.
    /// </summary>
    public sealed class PlayerPromptOverlay : MonoBehaviour
    {
        [SerializeField] bool show = true;
        PlayerCharacter _pc;
        PlayerCarry _carry;
        GUIStyle _style;

        void Awake() { _pc = GetComponent<PlayerCharacter>(); _carry = GetComponent<PlayerCarry>(); }

        void OnGUI()
        {
            if (!show || _pc == null || !_pc.IsOwnerSafe || !_pc.IsSpawned) return;
            _style ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 18, fontStyle = FontStyle.Bold };
            float cx = Screen.width * 0.5f, cy = Screen.height * 0.5f;
            GUI.Label(new Rect(cx - 10, cy - 12, 20, 24), "•", _style);
            if (!string.IsNullOrEmpty(_carry.Prompt)) GUI.Label(new Rect(cx - 300, cy + 30, 600, 30), _carry.Prompt, _style);
            float st = _pc.Motor != null && _pc.Motor.Stamina != null ? _pc.Motor.Stamina.Value / _pc.Tuning.StaminaMax : 1f;
            GUI.Box(new Rect(20, Screen.height - 40, 200 * st, 14), GUIContent.none);
        }
    }
}
