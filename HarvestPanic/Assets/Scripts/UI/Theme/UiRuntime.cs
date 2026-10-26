using HarvestPanic.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HarvestPanic.UI
{
    /// <summary>
    /// Persistent frontend plumbing, created automatically after the first scene loads: guarantees an EventSystem with the
    /// Input System UI module (keyboard, mouse and gamepad navigation), a session provider (the Networking one, or the
    /// <see cref="FakeSessionService"/> dev stand-in), applies <see cref="GameSettings"/> to the engine and owns the pause overlay.
    /// </summary>
    public sealed class UiRuntime : MonoBehaviour
    {
        static UiRuntime _instance;
        public static UiRuntime Instance => _instance;

        public FakeSessionService Fake { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Auto() => Ensure();

        /// <summary>Creates the runtime object once. Safe to call from any controller.</summary>
        public static UiRuntime Ensure()
        {
            if (_instance != null) return _instance;
            var go = new GameObject("UI_Runtime");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<UiRuntime>();
            return _instance;
        }

        /// <summary>Returns the registered session service, registering the fake one if nobody provides it.</summary>
        public static ISessionService EnsureSession()
        {
            if (Services.TryGet(out ISessionService existing)) return existing;
            var rt = Ensure();
            rt.Fake = new FakeSessionService { ConnectDelay = 0.6f, LocalName = string.IsNullOrEmpty(SettingsService.Current.PlayerName) ? "Farmer" : SettingsService.Current.PlayerName };
            Services.Register<ISessionService>(rt.Fake);
            return rt.Fake;
        }

        void Awake()
        {
            Services.Register<IPlayerNameService>(PlayerNameFromSettings.Instance);
            SettingsService.Changed += ApplyToEngine;
            ApplyToEngine(SettingsService.Current);
            Services.WhenAvailable<IAudioSettingsSink>(_ => ApplyToEngine(SettingsService.Current));
            Services.WhenAvailable<IVoiceChat>(_ => ApplyToEngine(SettingsService.Current));
            EnsureEventSystem();
            gameObject.AddComponent<PauseOverlay>();
            gameObject.AddComponent<OnboardingOverlay>();
            InputBindings.Hook();
        }

        void OnDestroy()
        {
            SettingsService.Changed -= ApplyToEngine;
            if (_instance == this) _instance = null;
        }

        void Update()
        {
            if (!Services.TryGet(out IPlayerNameService _)) Services.Register<IPlayerNameService>(PlayerNameFromSettings.Instance);
            InputBindings.Pump();
            if (Fake != null && Services.TryGet(out ISessionService cur) && ReferenceEquals(cur, Fake)) Fake.Tick(Time.unscaledDeltaTime);
        }

        public static void EnsureEventSystem()
        {
            var es = FindFirstObjectByType<EventSystem>();
            if (es == null)
            {
                var go = new GameObject("EventSystem");
                es = go.AddComponent<EventSystem>();
                DontDestroyOnLoad(go);
            }
            if (es.GetComponent<InputSystemUIInputModule>() == null)
            {
                var legacy = es.GetComponent<StandaloneInputModule>();
                if (legacy != null) Destroy(legacy);
                es.gameObject.AddComponent<InputSystemUIInputModule>();
            }
        }

        /// <summary>First-person motion comfort into the Core contract read by the Player camera.</summary>
        public static void ApplyComfort(GameSettings s)
        {
            CameraComfort.BaseFov = s.Fov;
            CameraComfort.BobScale = s.ReducedMotion || !s.CameraBob ? 0f : 1f;
            CameraComfort.ShakeScale = s.ReducedMotion ? Mathf.Min(s.CameraShake, 0.25f) : s.CameraShake;
            CameraComfort.FovKickScale = s.ReducedMotion ? 0f : 1f;
        }

        static void ApplyToEngine(GameSettings s)
        {
            UiAccessibility.Set((UiColorblind)(int)s.Colorblind, s.ReducedMotion, s.UiScale, s.TextSize); // Core snapshot for non-Frontend screens
            // Audio owns bus gains when present (IAudioSettingsSink applies Master x bus in software); otherwise fall back to the listener.
            if (Services.TryGet(out IAudioSettingsSink sink))
            {
                AudioListener.volume = 1f;
                sink.SetBusVolume(AudioBus.Master, s.MasterVolume);
                sink.SetBusVolume(AudioBus.Music, s.MusicVolume);
                sink.SetBusVolume(AudioBus.Sfx, s.SfxVolume);
                sink.SetBusVolume(AudioBus.UI, s.SfxVolume);
                sink.SetBusVolume(AudioBus.Voice, s.VoiceVolume);
            }
            else AudioListener.volume = s.MasterVolume;
            // Proximity voice (feature/voice-chat): transmit mode and open-mic threshold; the Voice bus volume is above.
            if (Services.TryGet(out IVoiceChat voice))
            {
                voice.Mode = s.PushToTalk ? VoiceTransmitMode.PushToTalk : VoiceTransmitMode.OpenMic;
                voice.OpenMicThreshold = s.OpenMicThreshold;
            }
            HarvestPanic.Core.BatchModeSilence.Apply();
            ApplyComfort(s);
            if (Application.isEditor || Application.isBatchMode) return; // never touch window/URP asset from the editor
            switch (s.WindowMode)
            {
                case WindowModeSetting.Fullscreen: Screen.fullScreenMode = FullScreenMode.ExclusiveFullScreen; break;
                case WindowModeSetting.Borderless: Screen.fullScreenMode = FullScreenMode.FullScreenWindow; break;
                default: Screen.fullScreenMode = FullScreenMode.Windowed; break;
            }
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp) urp.renderScale = s.ResolutionScale;
        }
    }

    /// <summary>
    /// Core <see cref="IPlayerNameService"/> backed by <see cref="GameSettings.PlayerName"/> (the Settings name field) and the
    /// session platform, so the character creator's Farm Name and Settings never disagree.
    /// </summary>
    public sealed class PlayerNameFromSettings : IPlayerNameService
    {
        public static readonly PlayerNameFromSettings Instance = new PlayerNameFromSettings();

        public string PlayerName => SettingsService.Current.PlayerName ?? "";

        public void SetPlayerName(string name)
        {
            var next = SettingsService.Current.Copy();
            next.PlayerName = name ?? "";
            SettingsService.Apply(next);
            var platform = SessionAccess.Platform;
            if (platform != null && !string.IsNullOrEmpty(next.PlayerName)) platform.SetLocalPlayerName(next.PlayerName);
        }
    }
}
