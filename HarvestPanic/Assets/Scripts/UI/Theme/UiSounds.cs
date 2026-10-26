using System.Collections.Generic;
using HarvestPanic.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace HarvestPanic.UI
{
    public enum UiSound { Hover, Confirm, Back, Error }

    /// <summary>
    /// Menu sounds through the Audio team's <see cref="IAudioService"/> (UI_* events on the UI stinger group, volume from the
    /// UI bus). Never plays an AudioClip directly. Hover is throttled and suppressed right after a confirm/back so a
    /// screen change does not double-tick. Silent when no audio service is registered (tests, headless).
    /// </summary>
    public static class UiSounds
    {
        public const string HoverEvent = "UI_Menu_Hover", ConfirmEvent = "UI_Menu_Confirm", BackEvent = "UI_Menu_Back", ErrorEvent = "UI_Menu_Error";
        public const float HoverGap = 0.07f, HoverQuietAfterPress = 0.25f;

        static float _lastHover = -10f, _lastPress = -10f;

        /// <summary>Every sound requested this session, newest last (tests). Capped at 64.</summary>
        public static readonly List<UiSound> History = new List<UiSound>();

        public static void Hover()
        {
            float now = Time.unscaledTime;
            if (now - _lastHover < HoverGap || now - _lastPress < HoverQuietAfterPress) return;
            _lastHover = now;
            Play(UiSound.Hover);
        }

        public static void Confirm() { _lastPress = Time.unscaledTime; Play(UiSound.Confirm); }
        public static void Back() { _lastPress = Time.unscaledTime; Play(UiSound.Back); }
        public static void Error() { _lastPress = Time.unscaledTime; Play(UiSound.Error); }

        public static string EventFor(UiSound s) => s switch
        {
            UiSound.Hover => HoverEvent,
            UiSound.Confirm => ConfirmEvent,
            UiSound.Back => BackEvent,
            _ => ErrorEvent,
        };

        static void Play(UiSound s)
        {
            History.Add(s);
            if (History.Count > 64) History.RemoveAt(0);
            if (Services.TryGet(out IAudioService audio)) audio.Play2D(EventFor(s));
        }

        /// <summary>Hover sound when a focusable under <paramref name="root"/> gains focus (keyboard / gamepad / mouse).</summary>
        public static void AttachFocusSounds(VisualElement root)
        {
            root.RegisterCallback<FocusInEvent>(e =>
            {
                if (e.target is VisualElement ve && ve.focusable && (ve is Button || ve.ClassListContains(UiKit.FocusRowClass) || ve is Slider))
                    Hover();
            }, TrickleDown.TrickleDown);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() { History.Clear(); _lastHover = _lastPress = -10f; }
    }
}
