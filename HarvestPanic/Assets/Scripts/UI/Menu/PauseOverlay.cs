using Cursor = UnityEngine.Cursor;
using HarvestPanic.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace HarvestPanic.UI
{
    /// <summary>
    /// In-game pause overlay (Esc / Start). Never pauses the world in multiplayer ("The farm does not pause."); with one player
    /// it really pauses via Time.timeScale. Resume, Settings, Invite, Leave. Lives on the persistent UiRuntime object.
    /// </summary>
    public sealed class PauseOverlay : MonoBehaviour
    {
        UiPanel _panel;
        VisualElement _main, _settingsLayer, _helpLayer, _confirmLayer;
        public enum Layer { Main, Settings, Help, Confirm }
        public Layer Current { get; private set; }
        Label _title, _sub, _toast;
        SettingsPanel _settings;
        bool _open;
        float _prevScale = 1f;
        CursorLockMode _prevLock;
        bool _prevVisible;

        public bool IsOpen => _open;

        public static bool SceneAllowsPause(string sceneName) =>
            sceneName != SceneLoader.MainMenu && sceneName != GameInfo.BootstrapScene;

        void Update()
        {
            if (!SceneAllowsPause(SceneManager.GetActiveScene().name)) { if (_open) Close(); return; }
            bool esc = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
            bool start = Gamepad.current != null && (Gamepad.current.startButton.wasPressedThisFrame || (_open && Gamepad.current.buttonEast.wasPressedThisFrame));
            if (!(esc || start)) return;
            if (SettingsPanel.IsListening) return; // rebinding owns Esc (cancel)
            if (_open) Back();
            else if (UiFocus.ModalCount == 0) Open();
        }

        public void Open()
        {
            Build();
            _open = true;
            UiFocus.Push(this);
            bool solo = !Services.TryGet(out ISessionService s) || !s.IsConnected || s.Players.Count <= 1;
            _title.text = solo ? "Paused" : "The farm does not pause.";
            _sub.text = solo ? "Take a breath. The pigs are waiting." : "Your farmer is standing around with a 'zzz' above their head.";
            _toast.text = "";
            if (solo) { _prevScale = Time.timeScale; Time.timeScale = 0f; }
            _prevLock = Cursor.lockState; _prevVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            _panel.Root.style.display = DisplayStyle.Flex;
            ShowLayer(Layer.Main);
        }

        /// <summary>Esc/B: sub-layer back to main, main closes.</summary>
        public void Back()
        {
            if (Current == Layer.Settings) CloseSettings();
            else if (Current != Layer.Main) ShowLayer(Layer.Main);
            else Close();
        }

        public void ShowLayer(Layer layer)
        {
            Build();
            Current = layer;
            _main.EnableInClassList("is-active", layer == Layer.Main);
            _settingsLayer.EnableInClassList("is-active", layer == Layer.Settings);
            _helpLayer.EnableInClassList("is-active", layer == Layer.Help);
            _confirmLayer.EnableInClassList("is-active", layer == Layer.Confirm);
            if (layer == Layer.Main) UiFocus.FocusFirstButton(_main);
            if (layer == Layer.Confirm) UiFocus.FocusFirstButton(_confirmLayer);
        }

        public void OpenHelp()
        {
            ShowLayer(Layer.Help);
            var help = new HelpPanel(_helpLayer.Q("pause-help-host"));
            help.Closed += () => ShowLayer(Layer.Main);
            help.FocusFirst();
        }

        public void ConfirmLeave()
        {
            Close();
            if (Services.TryGet(out ISessionService s)) s.Leave();
            SceneLoader.Load(SceneLoader.MainMenu);
        }

        public void Close()
        {
            if (!_open) return;
            _open = false;
            UiFocus.Pop(this);
            if (Time.timeScale == 0f) Time.timeScale = _prevScale <= 0f ? 1f : _prevScale;
            Cursor.lockState = _prevLock; Cursor.visible = _prevVisible;
            _panel.Root.style.display = DisplayStyle.None;
        }

        public void OpenSettings()
        {
            ShowLayer(Layer.Settings);
            _settings = new SettingsPanel(_settingsLayer.Q("pause-settings-host"));
            _settings.Closed += CloseSettings;
            _settings.FocusFirst();
        }

        void CloseSettings()
        {
            SettingsService.Persist();
            ShowLayer(Layer.Main);
        }

        void Build()
        {
            if (_panel != null) return;
            _panel = UiPanel.Create(transform, "UI_Pause", 200);
            var r = _panel.Root;
            _main = r.Q("pause-main"); _settingsLayer = r.Q("pause-settings");
            _title = r.Q<Label>("pause-title"); _sub = r.Q<Label>("pause-sub"); _toast = r.Q<Label>("pause-toast");
            r.Q<Button>("btn-resume").clicked += Close;
            r.Q<Button>("btn-pause-settings").clicked += OpenSettings;
            r.Q<Button>("btn-invite").clicked += () =>
            {
                _toast.text = SessionAccess.TryOpenSteamOverlay() ? "Steam friends opened." : "Tell friends to join your address on the LAN list.";
            };
            _helpLayer = r.Q("pause-help"); _confirmLayer = r.Q("pause-confirm");
            r.Q<Button>("btn-help").clicked += OpenHelp;
            r.Q<Button>("btn-leave").clicked += () => ShowLayer(Layer.Confirm);
            r.Q<Button>("btn-confirm-stay").clicked += () => ShowLayer(Layer.Main);
            r.Q<Button>("btn-confirm-leave").clicked += ConfirmLeave;
        }
    }
}
