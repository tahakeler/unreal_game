using System;
using UnityEngine;

namespace HarvestPanic.Core
{
    /// <summary>Colour-vision deficiency the UI compensates for (values match Frontend's ColorblindMode).</summary>
    public enum UiColorblind : byte { Off = 0, Deuteranopia = 1, Protanopia = 2, Tritanopia = 3 }

    /// <summary>
    /// Snapshot of the accessibility options that screens outside Frontend UI must honour (character creator, HUDs).
    /// Written by Frontend UI from its settings (like <see cref="CameraComfort"/>); read by anyone. Client-local
    /// presentation only. Add-only contract.
    /// </summary>
    public static class UiAccessibility
    {
        /// <summary>Colour-blind mode.</summary>
        public static UiColorblind Colorblind { get; private set; }
        /// <summary>Reduced motion: no transitions, no auto-spin, no bouncy feedback.</summary>
        public static bool ReducedMotion { get; private set; }
        /// <summary>UI scale 0.8..1.5 (1 = 1080p reference).</summary>
        public static float UiScale { get; private set; } = 1f;
        /// <summary>Text size: 0 small, 1 default, 2 large.</summary>
        public static int TextSize { get; private set; } = 1;

        /// <summary>Raised after <see cref="Set"/> changes anything.</summary>
        public static event Action Changed;

        /// <summary>Publishes new values (Frontend UI).</summary>
        public static void Set(UiColorblind colorblind, bool reducedMotion, float uiScale, int textSize)
        {
            if (float.IsNaN(uiScale) || uiScale <= 0f) uiScale = 1f;
            textSize = textSize < 0 ? 0 : textSize > 2 ? 2 : textSize;
            bool same = Colorblind == colorblind && ReducedMotion == reducedMotion && Math.Abs(UiScale - uiScale) < 1e-4f && TextSize == textSize;
            Colorblind = colorblind; ReducedMotion = reducedMotion; UiScale = uiScale; TextSize = textSize;
            if (!same) Changed?.Invoke();
        }

        /// <summary>Restores default values (tests); subscribers are kept and notified.</summary>
        public static void Reset() => Set(UiColorblind.Off, false, 1f, 1);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay()
        {
            Changed = null;
            Colorblind = UiColorblind.Off; ReducedMotion = false; UiScale = 1f; TextSize = 1;
        }
    }
}
