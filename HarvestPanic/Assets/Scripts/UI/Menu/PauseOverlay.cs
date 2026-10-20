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
        VisualElement _main, _settingsLayer;
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
            if (_open) { if (_settingsLayer != null && _settingsLayer.ClassListContains("is-active")) CloseSettings(); else Close(); }
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
            _main.AddToClassList("is-active");
            _settingsLayer.RemoveFromClassList("is-active");
            UiFocus.FocusFirstButton(_main);
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

        void OpenSettings()
        {
            _main.RemoveFromClassList("is-active");
            _settingsLayer.AddToClassList("is-active");
            _settings = new SettingsPanel(_settingsLayer.Q("pause-settings-host"));
            _settings.Closed += CloseSettings;
            _settings.FocusFirst();
        }

        void CloseSettings()
        {
            SettingsService.Persist();
            _settingsLayer.RemoveFromClassList("is-active");
            _main.AddToClassList("is-active");
            UiFocus.FocusFirstButton(_main);
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
            r.Q<Button>("btn-leave").clicked += () =>
            {
                Close();
                if (Services.TryGet(out ISessionService s)) s.Leave();
                SceneLoader.Load(SceneLoader.MainMenu);
            };
        }
    }
}
