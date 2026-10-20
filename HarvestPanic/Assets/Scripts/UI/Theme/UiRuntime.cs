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
            SettingsService.Changed += ApplyToEngine;
            ApplyToEngine(SettingsService.Current);
            Services.WhenAvailable<IAudioSettingsSink>(_ => ApplyToEngine(SettingsService.Current));
            EnsureEventSystem();
            gameObject.AddComponent<PauseOverlay>();
        }

        void OnDestroy()
        {
            SettingsService.Changed -= ApplyToEngine;
            if (_instance == this) _instance = null;
        }

        void Update()
        {
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

        static void ApplyToEngine(GameSettings s)
        {
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
}
