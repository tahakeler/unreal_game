using System;
using UnityEngine;

namespace HarvestPanic.UI
{
    public enum WindowModeSetting { Fullscreen = 0, Borderless = 1, Windowed = 2 }
    public enum ColorblindMode { Off = 0, Deuteranopia = 1, Protanopia = 2, Tritanopia = 3 }

    /// <summary>Player-facing options. Plain serializable data; call <see cref="Clamp"/> after any edit.</summary>
    [Serializable]
    public sealed class GameSettings
    {
        public const float MinSensitivity = 0.1f, MaxSensitivity = 5f;
        public const float MinFov = 70f, MaxFov = 110f;
        public const float MinResolutionScale = 0.5f, MaxResolutionScale = 1f;
        public const float MinUiScale = 0.8f, MaxUiScale = 1.5f;

        public float Sensitivity = 1f;
        public bool InvertY;
        public float Fov = 90f;
        public float MasterVolume = 1f;
        public float MusicVolume = 0.8f;
        public float SfxVolume = 1f;
        public float VoiceVolume = 1f;
        public bool PushToTalk;
        public WindowModeSetting WindowMode = WindowModeSetting.Fullscreen;
        public float ResolutionScale = 1f;
        public float UiScale = 1f;
        public ColorblindMode Colorblind = ColorblindMode.Off;
        public bool ReducedMotion;
        /// <summary>0 small, 1 default, 2 large (scales UI text through USS variables).</summary>
        public int TextSize = 1;
        public string PlayerName = "";
        /// <summary>Captions for gameplay sounds (onboarding-accessibility: subtitles on by default).</summary>
        public bool Subtitles = true;
        /// <summary>Caption background opacity 0..1 (default 60%).</summary>
        public float CaptionBackground = 0.6f;
        /// <summary>Grandpa's tapes (contextual tips).</summary>
        public bool TipsEnabled = true;
        /// <summary>First-person head bob.</summary>
        public bool CameraBob = true;
        /// <summary>Impact camera shake 0..1 (default 70%).</summary>
        public float CameraShake = 0.7f;

        /// <summary>Forces every field into its legal range (NaN becomes the default).</summary>
        public GameSettings Clamp()
        {
            Sensitivity = ClampF(Sensitivity, MinSensitivity, MaxSensitivity, 1f);
            Fov = ClampF(Fov, MinFov, MaxFov, 90f);
            MasterVolume = ClampF(MasterVolume, 0f, 1f, 1f);
            MusicVolume = ClampF(MusicVolume, 0f, 1f, 0.8f);
            SfxVolume = ClampF(SfxVolume, 0f, 1f, 1f);
            VoiceVolume = ClampF(VoiceVolume, 0f, 1f, 1f);
            ResolutionScale = ClampF(ResolutionScale, MinResolutionScale, MaxResolutionScale, 1f);
            UiScale = ClampF(UiScale, MinUiScale, MaxUiScale, 1f);
            if (!Enum.IsDefined(typeof(WindowModeSetting), WindowMode)) WindowMode = WindowModeSetting.Fullscreen;
            if (!Enum.IsDefined(typeof(ColorblindMode), Colorblind)) Colorblind = ColorblindMode.Off;
            TextSize = Mathf.Clamp(TextSize, 0, 2);
            CaptionBackground = ClampF(CaptionBackground, 0f, 1f, 0.6f);
            CameraShake = ClampF(CameraShake, 0f, 1f, 0.7f);
            PlayerName = (PlayerName ?? "").Trim();
            if (PlayerName.Length > 20) PlayerName = PlayerName.Substring(0, 20);
            return this;
        }

        public GameSettings Copy() => (GameSettings)MemberwiseClone();

        static float ClampF(float v, float min, float max, float fallback)
            => float.IsNaN(v) || float.IsInfinity(v) ? fallback : Mathf.Clamp(v, min, max);
    }
}
