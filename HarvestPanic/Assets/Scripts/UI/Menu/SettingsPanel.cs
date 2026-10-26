using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace HarvestPanic.UI
{
    /// <summary>
    /// Tabbed settings screen (Gameplay, Audio, Video, Access). Edits apply live through <see cref="SettingsService"/> and are
    /// persisted when the panel closes. Shared by the main menu and the pause overlay.
    /// </summary>
    public sealed class SettingsPanel
    {
        public event Action Closed;

        readonly VisualElement _host;
        readonly VisualElement _tabContent;
        readonly List<Button> _tabButtons = new List<Button>();
        readonly Action _rebuild;
        Button _firstTab;
        int _tab;

        static readonly string[] TabNames = { "Gameplay", "Controls", "Audio", "Video", "Access" };

        static InputActionRebindingExtensions.RebindingOperation _rebind;
        /// <summary>True while waiting for a key/button press (pause menu must not treat Esc as back).</summary>
        public static bool IsListening => _rebind != null;

        public SettingsPanel(VisualElement host)
        {
            _host = host;
            host.Clear();
            var card = new VisualElement(); card.AddToClassList("sign"); card.AddToClassList("settings-card");
            var title = new Label("Settings"); title.AddToClassList("display"); title.AddToClassList("sign-title");
            card.Add(title);

            var tabs = new VisualElement(); tabs.AddToClassList("row"); tabs.AddToClassList("settings-tabs");
            for (int i = 0; i < TabNames.Length; i++)
            {
                int index = i;
                var b = new Button(() => SelectTab(index)) { text = TabNames[i], name = "tab-" + TabNames[i].ToLowerInvariant() };
                b.AddToClassList("hp-btn"); b.AddToClassList("hp-btn--small"); b.AddToClassList("hp-btn--center"); b.AddToClassList("tab-btn");
                _tabButtons.Add(b); tabs.Add(b);
            }
            _firstTab = _tabButtons[0];
            card.Add(tabs);

            var paper = new VisualElement(); paper.AddToClassList("paper-card"); paper.AddToClassList("settings-paper");
            var scroll = new ScrollView(ScrollViewMode.Vertical) { name = "settings-scroll" };
            scroll.AddToClassList("settings-scroll");
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            _tabContent = scroll.contentContainer;
            paper.Add(scroll);
            card.Add(paper);

            var foot = new VisualElement(); foot.AddToClassList("row"); foot.AddToClassList("buttons-row");
            var reset = new Button(() => { SettingsService.ResetToDefaults(); SelectTab(_tab); }) { text = "Reset", name = "btn-settings-reset" };
            reset.AddToClassList("hp-btn"); reset.AddToClassList("hp-btn--small");
            var done = new Button(Close) { text = "Done", name = "btn-settings-done" };
            done.AddToClassList("hp-btn"); done.AddToClassList("hp-btn--primary"); done.AddToClassList("hp-btn--small"); done.AddToClassList("hp-btn--center");
            foot.Add(done); foot.Add(reset);
            card.Add(foot);
            host.Add(card);
            SelectTab(0);
        }

        public void FocusFirst() => _firstTab.schedule.Execute(() => _firstTab.Focus()).StartingIn(30);

        public void Close()
        {
            if (_rebind != null) { _rebind.Cancel(); }
            SettingsService.Persist();
            Closed?.Invoke();
        }

        void SelectTab(int index)
        {
            _tab = index;
            for (int i = 0; i < _tabButtons.Count; i++) _tabButtons[i].EnableInClassList("hp-btn--selected", i == index);
            _tabContent.Clear();
            var s = SettingsService.Current.Copy();
            void Commit() => SettingsService.Apply(s, persist: false);
            switch (index)
            {
                case 0: BuildGameplay(s, Commit); break;
                case 1: BuildControls(); break;
                case 2: BuildAudio(s, Commit); break;
                case 3: BuildVideo(s, Commit); break;
                default: BuildAccess(s, Commit); break;
            }
        }

        // ---------------------------------------------------------------- tabs
        void BuildGameplay(GameSettings s, Action commit)
        {
            AddText("Your name", s.PlayerName, "Farmer", v => { s.PlayerName = v; commit(); });
            AddSlider("Look sensitivity", GameSettings.MinSensitivity, GameSettings.MaxSensitivity, s.Sensitivity, v => { s.Sensitivity = v; commit(); }, v => v.ToString("0.0") + "x");
            AddToggle("Invert Y axis", s.InvertY, v => { s.InvertY = v; commit(); });
            AddSlider("Field of view", GameSettings.MinFov, GameSettings.MaxFov, s.Fov, v => { s.Fov = Mathf.Round(v); commit(); }, v => Mathf.RoundToInt(v) + "");
            AddToggle("Head bob", s.CameraBob, v => { s.CameraBob = v; commit(); });
            AddSlider("Camera shake", 0, 1, s.CameraShake, v => { s.CameraShake = Mathf.Round(v * 20f) / 20f; commit(); }, Pct);
            AddToggle("Grandpa's tapes (tips)", s.TipsEnabled, v => { s.TipsEnabled = v; commit(); });
            var replay = new Button(() => { new PlayerPrefsTipStore().ClearAll(); }) { text = "Replay all tapes", name = "btn-replay-tapes" };
            replay.AddToClassList("hp-btn"); replay.AddToClassList("hp-btn--small");
            _tabContent.Add(replay);
        }

        // ---------------------------------------------------------------- controls (rebinding)
        Label _bindWarning;

        void BuildControls()
        {
            var head = Row("Action");
            head.Add(new VisualElement { style = { flexGrow = 1 } });
            foreach (var t in new[] { "Keyboard + mouse", "Gamepad" }) { var l = new Label(t); l.AddToClassList("bold"); l.AddToClassList("bind-btn"); head.Add(l); }
            var rows = new Dictionary<string, VisualElement>();
            foreach (var d in InputBindings.Defaults)
            {
                string key = d.Action + "|" + d.Label;
                if (!rows.TryGetValue(key, out var row))
                {
                    row = Row(d.Label); row.Add(new VisualElement { style = { flexGrow = 1 } });
                    rows[key] = row;
                    if (d.Group == InputBindings.PadGroup) row.Add(new VisualElement { name = "kb-gap", style = { minWidth = 230 } });
                }
                row.Add(BindButton(d));
                if (d.Action == "Move" && d.Group == InputBindings.KbmGroup) row.Add(new VisualElement { style = { minWidth = 230 } });
            }
            _bindWarning = new Label(""); _bindWarning.AddToClassList("small-text"); _bindWarning.AddToClassList("bind-warning");
            _tabContent.Add(_bindWarning);
            var reset = new Button(() => { InputBindings.ResetAll(); SelectTab(_tab); }) { text = "Reset controls", name = "btn-bind-reset" };
            reset.AddToClassList("hp-btn"); reset.AddToClassList("hp-btn--small");
            _tabContent.Add(reset);
        }

        Button BindButton(BindingDef def)
        {
            var b = new Button { text = InputBindings.Display(def), name = "bind-" + def.Action.ToLowerInvariant() + "-" + def.DefaultPath.Replace("<", "").Replace(">", "").Replace("/", "-").ToLowerInvariant() };
            b.AddToClassList("hp-btn"); b.AddToClassList("hp-btn--small"); b.AddToClassList("hp-btn--center"); b.AddToClassList("bind-btn");
            b.clicked += () => StartRebind(def, b);
            return b;
        }

        void StartRebind(BindingDef def, Button b)
        {
            if (_rebind != null) return;
            var action = InputBindings.Template.FindActionMap(InputBindings.MapName).FindAction(def.Action);
            int index = InputBindings.TemplateIndex(action, def);
            if (index < 0) return;
            b.text = "Press a key... (Esc cancels)";
            b.AddToClassList("bind-btn--listening");
            _rebind = action.PerformInteractiveRebinding(index)
                .WithControlsExcluding("<Mouse>/position").WithControlsExcluding("<Mouse>/delta").WithControlsExcluding("<Pointer>/position")
                .WithCancelingThrough("<Keyboard>/escape")
                .OnMatchWaitForAnother(0.1f);
            if (def.IsKeyboardMouse) _rebind.WithControlsHavingToMatchPath("<Keyboard>").WithControlsHavingToMatchPath("<Mouse>");
            else _rebind.WithControlsHavingToMatchPath("<Gamepad>");
            void Finish(bool apply)
            {
                string path = apply ? action.bindings[index].effectivePath : null;
                _rebind?.Dispose(); _rebind = null;
                b.RemoveFromClassList("bind-btn--listening");
                if (apply)
                {
                    action.RemoveBindingOverride(index);
                    if (InputBindings.TryFindConflict(def, path, out var other))
                        _bindWarning.text = $"Heads up: {InputBindings.DisplayPath(path)} is also bound to {other.Label}.";
                    else _bindWarning.text = "";
                    InputBindings.SetOverride(def.Action, def.DefaultPath, path);
                }
                b.text = InputBindings.Display(def);
            }
            _rebind.OnComplete(_ => Finish(true)).OnCancel(_ => Finish(false)).Start();
        }

        void BuildAudio(GameSettings s, Action commit)
        {
            AddSlider("Master", 0, 1, s.MasterVolume, v => { s.MasterVolume = v; commit(); }, Pct);
            AddSlider("Music", 0, 1, s.MusicVolume, v => { s.MusicVolume = v; commit(); }, Pct);
            AddSlider("Effects", 0, 1, s.SfxVolume, v => { s.SfxVolume = v; commit(); }, Pct);
            AddSlider("Voice chat", 0, 1, s.VoiceVolume, v => { s.VoiceVolume = v; commit(); }, Pct);
            AddToggle("Push to talk (V)", s.PushToTalk, v => { s.PushToTalk = v; commit(); });
            AddSlider("Open mic sensitivity", 0, 1, 1f - s.OpenMicThreshold, v => { s.OpenMicThreshold = 1f - Mathf.Round(v * 20f) / 20f; commit(); }, Pct);
        }

        void BuildVideo(GameSettings s, Action commit)
        {
            AddChoice("Window mode", Enum.GetNames(typeof(WindowModeSetting)), (int)s.WindowMode, i => { s.WindowMode = (WindowModeSetting)i; commit(); });
            AddSlider("Resolution scale", GameSettings.MinResolutionScale, GameSettings.MaxResolutionScale, s.ResolutionScale, v => { s.ResolutionScale = Mathf.Round(v * 20f) / 20f; commit(); }, Pct);
            AddSlider("UI scale", GameSettings.MinUiScale, GameSettings.MaxUiScale, s.UiScale, v => { s.UiScale = Mathf.Round(v * 20f) / 20f; commit(); }, Pct);
        }

        void BuildAccess(GameSettings s, Action commit)
        {
            AddChoice("Text size", new[] { "Small", "Default", "Large" }, s.TextSize, i => { s.TextSize = i; commit(); });
            AddChoice("Colour-blind mode", new[] { "Off", "Deuteranopia", "Protanopia", "Tritanopia" }, (int)s.Colorblind, i => { s.Colorblind = (ColorblindMode)i; commit(); });
            AddToggle("Reduced motion", s.ReducedMotion, v => { s.ReducedMotion = v; commit(); });
            AddToggle("Captions for sounds", s.Subtitles, v => { s.Subtitles = v; commit(); });
            AddSlider("Caption background", 0, 1, s.CaptionBackground, v => { s.CaptionBackground = Mathf.Round(v * 20f) / 20f; commit(); }, Pct);
            AddToggle("Grandpa's tapes (tips)", s.TipsEnabled, v => { s.TipsEnabled = v; commit(); });
            var note = new Label("Shapes and labels always back up colours, so every mode stays readable.");
            note.AddToClassList("small-text"); note.AddToClassList("muted");
            _tabContent.Add(note);
        }

        static string Pct(float v) => Mathf.RoundToInt(v * 100f) + "%";

        // ---------------------------------------------------------------- row builders
        VisualElement Row(string label)
        {
            var row = new VisualElement(); row.AddToClassList("setting-row");
            var l = new Label(label); l.AddToClassList("bold"); l.AddToClassList("setting-label");
            row.Add(l);
            _tabContent.Add(row);
            return row;
        }

        void AddSlider(string label, float min, float max, float value, Action<float> set, Func<float, string> fmt)
        {
            var row = Row(label);
            var slider = new Slider(min, max) { value = value };
            slider.AddToClassList("hp-slider");
            var val = new Label(fmt(value)); val.AddToClassList("bold"); val.AddToClassList("setting-value");
            slider.RegisterValueChangedCallback(e => { val.text = fmt(e.newValue); set(e.newValue); });
            row.Add(slider); row.Add(val);
        }

        void AddToggle(string label, bool value, Action<bool> set)
        {
            var row = Row(label);
            var b = new Button { text = value ? "ON" : "OFF" };
            b.AddToClassList("hp-btn"); b.AddToClassList("hp-btn--small"); b.AddToClassList("hp-btn--center"); b.AddToClassList("toggle-btn");
            b.EnableInClassList("toggle-on", value);
            b.clicked += () =>
            {
                value = !value; b.text = value ? "ON" : "OFF"; b.EnableInClassList("toggle-on", value); set(value);
            };
            row.Add(new VisualElement { style = { flexGrow = 1 } });
            row.Add(b);
        }

        void AddChoice(string label, string[] options, int index, Action<int> set)
        {
            var row = Row(label);
            var left = new Button { text = "<" }; var right = new Button { text = ">" };
            var cur = new Label(options[Mathf.Clamp(index, 0, options.Length - 1)]); cur.AddToClassList("bold"); cur.AddToClassList("choice-label");
            foreach (var b in new[] { left, right }) { b.AddToClassList("hp-btn"); b.AddToClassList("hp-btn--small"); b.AddToClassList("hp-btn--center"); b.AddToClassList("arrow-btn"); }
            void Step(int d)
            {
                index = (index + d + options.Length) % options.Length;
                cur.text = options[index]; set(index);
            }
            left.clicked += () => Step(-1); right.clicked += () => Step(1);
            row.Add(new VisualElement { style = { flexGrow = 1 } });
            row.Add(left); row.Add(cur); row.Add(right);
        }

        void AddText(string label, string value, string placeholder, Action<string> set)
        {
            var row = Row(label);
            var tf = new TextField { value = value, maxLength = 20 };
            tf.AddToClassList("hp-field"); tf.AddToClassList("bold"); tf.AddToClassList("name-field");
            tf.RegisterValueChangedCallback(e => set(e.newValue));
            row.Add(new VisualElement { style = { flexGrow = 1 } });
            row.Add(tf);
        }
    }
}
