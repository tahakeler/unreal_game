using Cursor = UnityEngine.Cursor;
using System.Collections.Generic;
using HarvestPanic.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Direction = UnityEngine.UIElements.NavigationMoveEvent.Direction;

namespace HarvestPanic.UI
{
    /// <summary>
    /// In-game pause overlay (Esc / Start). Never pauses the world in multiplayer ("The farm does not pause."); with one player
    /// it really pauses via Time.timeScale. Resume, Settings, Controls &amp; Help, Replays, Invite, Leave (with confirm), and a
    /// crew card with per-player mute and talking indicators (proximity voice). Lives on the persistent UiRuntime object.
    /// </summary>
    public sealed class PauseOverlay : MonoBehaviour
    {
        UiPanel _panel;
        VisualElement _main, _settingsLayer, _helpLayer, _confirmLayer, _players, _voiceList;
        public enum Layer { Main, Settings, Help, Confirm }
        public Layer Current { get; private set; }
        Label _title, _sub, _toast;
        SettingsPanel _settings;
        bool _open;
        float _prevScale = 1f;
        CursorLockMode _prevLock;
        bool _prevVisible;
        IVoiceChat _voice;
        readonly List<VisualElement> _mainButtons = new List<VisualElement>();

        public bool IsOpen => _open;
        public VisualElement Root => _panel != null ? _panel.Root : null;

        public static bool SceneAllowsPause(string sceneName) =>
            sceneName != SceneLoader.MainMenu && sceneName != GameInfo.BootstrapScene;

        void Update()
        {
            if (!SceneAllowsPause(SceneManager.GetActiveScene().name)) { if (_open) Close(); return; }
            bool esc = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
            bool start = Gamepad.current != null && (Gamepad.current.startButton.wasPressedThisFrame || (_open && Gamepad.current.buttonEast.wasPressedThisFrame));
            if (!(esc || start)) return;
            if (SettingsPanel.IsListening) return; // rebinding owns Esc (cancel)
            if (Services.TryGet(out ICamcorderService cam) && cam.IsViewing) return; // camcorder viewer/shelf owns Esc (feature/camcorder)
            if (Services.TryGet(out ICharacterCreator creator) && creator.IsOpen) return; // wardrobe / character creator owns Esc
            if (LoadingScreen.Instance != null && LoadingScreen.Instance.IsVisible) return;
            if (_open) Back();
            else if (UiFocus.ModalCount == 0) Open();
        }

        public void Open()
        {
            Build();
            _open = true;
            UiFocus.Push(this);
            bool solo = !Services.TryGet(out ISessionService s) || !s.IsConnected || s.Players.Count <= 1;
            _title.text = UiText.Get(solo ? "pause.title.solo" : "pause.title.multi");
            _sub.text = UiText.Get(solo ? "pause.sub.solo" : "pause.sub.multi");
            _toast.text = "";
            if (solo) { _prevScale = Time.timeScale; Time.timeScale = 0f; }
            _prevLock = Cursor.lockState; _prevVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            _panel.Root.style.display = DisplayStyle.Flex;
            if (Services.TryGet(out _voice)) { _voice.OnSpeakingChanged -= OnSpeaking; _voice.OnSpeakingChanged += OnSpeaking; }
            RefreshVoiceRow();
            ShowLayer(Layer.Main);
        }

        void OnSpeaking(ulong id, bool speaking) { if (_open) RefreshVoiceRow(); }

        /// <summary>Crew card: one row per player with their badge, a TALKING tag and a Mute / Unmute button for others.</summary>
        void RefreshVoiceRow()
        {
            _voiceList.Clear();
            int n = 0;
            var buttons = new List<VisualElement>();
            if (Services.TryGet(out ISessionService s) && s.IsConnected)
            {
                Services.TryGet(out IVoiceChat voice);
                for (int i = 0; i < s.Players.Count; i++)
                {
                    var p = s.Players[i];
                    bool me = p.ClientId == s.LocalClientId;
                    var row = new VisualElement(); row.AddToClassList("row"); row.AddToClassList("pause-crew-row");
                    var badge = new FarmerBadge(i, 58f);
                    row.Add(badge);
                    string who = string.IsNullOrEmpty(p.Name) ? UiText.Format("common.farmer.n", i + 1) : p.Name;
                    var name = UiKit.Label(who, "bold", "pause-crew-name");
                    name.style.color = Color.Lerp(ShapePip.TeamColor(i), UiKit.Ink, 0.55f);
                    row.Add(name);
                    if (me) row.Add(UiKit.Label("@lobby.tag.you", "tag"));
                    if (p.IsHost) row.Add(UiKit.Label("@lobby.tag.host", "tag", "tag--pumpkin"));
                    row.Add(new VisualElement { style = { flexGrow = 1 } });
                    if (voice != null && !me)
                    {
                        ulong id = p.ClientId;
                        bool talking = voice.IsSpeaking(id), muted = voice.IsMuted(id);
                        badge.EnableInClassList("farmer-badge--talking", talking);
                        if (talking) row.Add(UiKit.Label("@lobby.talking", "tag", "tag--talking"));
                        var b = UiKit.Button(muted ? "@lobby.unmute" : "@lobby.mute", () => { voice.SetMuted(id, !voice.IsMuted(id)); RefreshVoiceRow(); }, "hp-btn--small", "hp-btn--center", "mute-btn");
                        b.name = "btn-mute-" + id;
                        b.EnableInClassList(UiKit.SelectedClass, muted);
                        row.Add(b);
                        buttons.Add(b);
                    }
                    _voiceList.Add(row);
                    n++;
                }
            }
            _players.style.display = n > 1 ? DisplayStyle.Flex : DisplayStyle.None;
            UiNav.Column(buttons);
            foreach (var b in buttons) UiNav.Link(b, Direction.Left, _mainButtons[0]);
            foreach (var m in _mainButtons) UiNav.Link(m, Direction.Right, buttons.Count > 0 ? buttons[0] : null);
        }

        /// <summary>Esc/B: sub-layer back to main, main closes.</summary>
        public void Back()
        {
            UiSounds.Back();
            if (Current == Layer.Settings) { _settings?.Close(); if (Current == Layer.Settings) CloseSettings(); }
            else if (Current != Layer.Main) ShowLayer(Layer.Main);
            else Close();
        }

        public void ShowLayer(Layer layer)
        {
            Build();
            var prev = Current;
            Current = layer;
            Toggle(_main, layer == Layer.Main);
            Toggle(_settingsLayer, layer == Layer.Settings);
            Toggle(_helpLayer, layer == Layer.Help);
            Toggle(_confirmLayer, layer == Layer.Confirm);
            if (layer == Layer.Main)
            {
                var focus = prev == Layer.Settings ? _main.Q<Button>("btn-pause-settings") : prev == Layer.Help ? _main.Q<Button>("btn-help") : prev == Layer.Confirm ? _main.Q<Button>("btn-leave") : _main.Q<Button>("btn-resume");
                UiFocus.FocusLater(focus);
            }
            if (layer == Layer.Confirm) UiFocus.FocusLater(_confirmLayer.Q<Button>("btn-confirm-stay"));
        }

        static void Toggle(VisualElement e, bool on) { if (on) UiKit.ShowScreen(e); else UiKit.HideScreen(e); }

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
            LoadingScreen.Show(SceneLoader.MainMenu);
            if (Services.TryGet(out ISessionService s)) s.Leave();
            SceneLoader.Load(SceneLoader.MainMenu);
        }

        public void Close()
        {
            if (!_open) return;
            _open = false;
            UiFocus.Pop(this);
            if (_voice != null) _voice.OnSpeakingChanged -= OnSpeaking;
            if (Time.timeScale == 0f) Time.timeScale = _prevScale <= 0f ? 1f : _prevScale;
            Cursor.lockState = _prevLock; Cursor.visible = _prevVisible;
            _panel.Root.panel?.focusController?.focusedElement?.Blur();
            _panel.Root.style.display = DisplayStyle.None;
        }

        public SettingsPanel OpenSettings()
        {
            ShowLayer(Layer.Settings);
            _settings = new SettingsPanel(_settingsLayer.Q("pause-settings-host"));
            _settings.Closed += CloseSettings;
            _settings.FocusFirst();
            return _settings;
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
            _players = r.Q("pause-players"); _voiceList = r.Q("pause-voice");
            r.Q<Button>("btn-resume").clicked += Close;
            r.Q<Button>("btn-pause-settings").clicked += () => OpenSettings();
            r.Q<Button>("btn-invite").clicked += () =>
            {
                _toast.text = UiText.Get(SessionAccess.TryOpenSteamOverlay() ? "pause.invite.steam" : "pause.invite.lan");
            };
            _helpLayer = r.Q("pause-help"); _confirmLayer = r.Q("pause-confirm");
            r.Q<Button>("btn-help").clicked += OpenHelp;
            // Camcorder (feature/camcorder): "Replays" opens the tape shelf when the camcorder service exists.
            var help = r.Q<Button>("btn-help");
            if (help != null && help.parent != null)
            {
                var replays = new Button(() => { if (Services.TryGet(out ICamcorderService cc)) { Close(); cc.OpenShelf(); } }) { name = "btn-replays", text = UiText.Get("pause.replays") };
                foreach (var c in help.GetClasses()) replays.AddToClassList(c);
                help.parent.Insert(help.parent.IndexOf(help) + 1, replays);
            }
            r.Q<Button>("btn-leave").clicked += () => ShowLayer(Layer.Confirm);
            r.Q<Button>("btn-confirm-stay").clicked += () => ShowLayer(Layer.Main);
            r.Q<Button>("btn-confirm-leave").clicked += ConfirmLeave;
            r.Q("pause-card").Query<Button>().ForEach(b => _mainButtons.Add(b));
            UiNav.Column(_mainButtons, wrap: true);
            UiNav.Row(new[] { r.Q("btn-confirm-stay"), r.Q("btn-confirm-leave") });
            UiNav.Trap(_confirmLayer);
        }
    }
}
