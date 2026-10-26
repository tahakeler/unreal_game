using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.UIElements;

namespace HarvestPanic.UI
{
    public enum UiPrompt { Select, Back, Ready, Crew, Wardrobe, Bell, Menu, Tab, Refresh, Navigate }

    /// <summary>
    /// Which device the player last pressed a button on (keyboard/mouse or gamepad) and the matching prompt glyph text,
    /// so footers and the lobby prompt bar show "Enter" or "A" (unity-ui-specialist: show correct prompts per device).
    /// Gamepad glyphs use the position names of the Xbox layout and are drawn as neutral ink keycaps (colour-blind safe).
    /// </summary>
    public static class InputGlyphs
    {
        static bool _gamepad;
        static IDisposable _listener;

        public static event Action Changed;

        /// <summary>True when the last button press came from a gamepad.</summary>
        public static bool UsingGamepad
        {
            get { Hook(); return _gamepad; }
            set { if (_gamepad == value) return; _gamepad = value; Changed?.Invoke(); }
        }

        static void Hook()
        {
            if (_listener != null) return;
            try
            {
                _listener = InputSystem.onAnyButtonPress.Call(ctrl =>
                {
                    if (ctrl?.device == null) return;
                    UsingGamepad = ctrl.device is Gamepad;
                });
            }
            catch (Exception) { /* no input system (headless edge cases): stay on keyboard */ }
        }

        /// <summary>Keycap text for <paramref name="p"/> on the current device.</summary>
        public static string Glyph(UiPrompt p) => Glyph(p, UsingGamepad);

        public static string Glyph(UiPrompt p, bool gamepad)
        {
            if (gamepad)
                return p switch
                {
                    UiPrompt.Select => "A", UiPrompt.Back => "B", UiPrompt.Ready => "Y", UiPrompt.Crew => UiText.Get("glyph.pad.view"),
                    UiPrompt.Wardrobe => "X", UiPrompt.Bell => "LB", UiPrompt.Menu => UiText.Get("glyph.pad.menu"), UiPrompt.Tab => "LB RB",
                    UiPrompt.Refresh => "Y", _ => UiText.Get("glyph.pad.dpad"),
                };
            return p switch
            {
                UiPrompt.Select => UiText.Get("glyph.key.enter"), UiPrompt.Back => UiText.Get("glyph.key.esc"), UiPrompt.Ready => "R",
                UiPrompt.Crew => UiText.Get("glyph.key.tab"), UiPrompt.Wardrobe => "C", UiPrompt.Bell => "B", UiPrompt.Menu => UiText.Get("glyph.key.esc"),
                UiPrompt.Tab => "Q E", UiPrompt.Refresh => "F5", _ => UiText.Get("glyph.key.arrows"),
            };
        }

        /// <summary>Builds a prompt chip: keycap + label (label is a string-table key).</summary>
        public static VisualElement Prompt(UiPrompt p, string labelKey, out Label keycap)
        {
            var chip = new VisualElement(); chip.AddToClassList("prompt");
            keycap = new Label(Glyph(p)); keycap.AddToClassList("keycap"); keycap.AddToClassList(UsingGamepad ? "keycap--pad" : "keycap--key");
            var text = new Label(UiText.Get(labelKey)); text.AddToClassList("prompt-text");
            chip.Add(keycap); chip.Add(text);
            return chip;
        }

        /// <summary>Refreshes keycaps built by <see cref="Prompt"/> (call from a <see cref="Changed"/> handler).</summary>
        public static void Refresh(Label keycap, UiPrompt p)
        {
            if (keycap == null) return;
            keycap.text = Glyph(p);
            keycap.EnableInClassList("keycap--pad", UsingGamepad);
            keycap.EnableInClassList("keycap--key", !UsingGamepad);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() { _listener?.Dispose(); _listener = null; _gamepad = false; Changed = null; }
    }
}
