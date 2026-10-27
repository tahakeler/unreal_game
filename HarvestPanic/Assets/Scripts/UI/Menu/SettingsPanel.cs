using System;
using System.Collections.Generic;
using HarvestPanic.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Direction = UnityEngine.UIElements.NavigationMoveEvent.Direction;

namespace HarvestPanic.UI
{
    /// <summary>
    /// Tabbed settings ledger (Gameplay, Controls, Audio, Video, Accessibility), shared by the main menu and the pause
    /// overlay. Edits apply live through <see cref="SettingsService"/> and are persisted when the panel closes. Every row is
    /// reachable by gamepad: sliders and choice rows use Left/Right, toggles use Submit (or Left/Right), Up/Down walks the
    /// rows, LB/RB or Q/E switch tabs. The side card explains the focused row and previews colour-blind palettes / mic level.
    /// </summary>
    public sealed class SettingsPanel
    {
        public event Action Closed;

        public static readonly string[] TabIds = { "gameplay", "controls", "audio", "video", "access" };

        readonly VisualElement _host, _card, _rows, _side, _preview;
        readonly ScrollView _scroll;
        readonly Label _sideTitle, _sideBody;
        readonly List<Button> _tabButtons = new List<Button>();
        readonly List<VisualElement> _focusables = new List<VisualElement>();
        readonly Button _done, _reset;
        IVisualElementScheduledItem _poll;
        int _tab;

        static InputActionRebindingExtensions.RebindingOperation _rebind;
        /// <summary>True while waiting for a key/button press (menus must not treat Esc as back).</summary>
        public static bool IsListening => _rebind != null;

        public int CurrentTab => _tab;
        public VisualElement Root => _card;
        /// <summary>Focusable rows of the current tab, top to bottom (tests).</summary>
        public IReadOnlyList<VisualElement> Focusables => _focusables;

        public SettingsPanel(VisualElement host)
        {
            _host = host;
            host.Clear();
            _card = new VisualElement { name = "settings-card" }; _card.AddToClassList("paper-card"); _card.AddToClassList("settings-card");
            _card.Add(Tape("masking-tape--tl")); _card.Add(Tape("masking-tape--tr"));

            var head = new VisualElement(); head.AddToClassList("row"); head.AddToClassList("settings-head");
            var title = UiKit.Label("@settings.title", "display", "settings-title"); title.name = "settings-title";
            head.Add(title);
            head.Add(new VisualElement { style = { flexGrow = 1 } });
            var tabs = new VisualElement(); tabs.AddToClassList("row"); tabs.AddToClassList("settings-tabs");
            for (int i = 0; i < TabIds.Length; i++)
            {
                int index = i;
                var b = UiKit.Chip("@settings.tab." + TabIds[i], () => SelectTab(index), "tab-" + TabIds[i]);
                b.AddToClassList("tab-btn");
                _tabButtons.Add(b); tabs.Add(b);
            }
            head.Add(tabs);
            _card.Add(head);
            var underline = new VisualElement(); underline.AddToClassList("ink-underline"); underline.AddToClassList("settings-underline");
            _card.Add(underline);

            var body = new VisualElement(); body.AddToClassList("row"); body.AddToClassList("settings-body");
            var ledger = new VisualElement(); ledger.AddToClassList("settings-ledger");
            _scroll = new ScrollView(ScrollViewMode.Vertical) { name = "settings-scroll" };
            _scroll.AddToClassList("settings-scroll");
            _scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            _rows = _scroll.contentContainer;
            ledger.Add(_scroll);
            body.Add(ledger);
            _side = new VisualElement { name = "settings-side" }; _side.AddToClassList("settings-side");
            _sideTitle = UiKit.Label("", "display", "side-title"); _sideBody = UiKit.Label("", "small-text", "side-body");
            _preview = new VisualElement { name = "settings-preview" }; _preview.AddToClassList("side-preview");
            _side.Add(_sideTitle); _side.Add(_sideBody); _side.Add(_preview);
            body.Add(_side);
            _card.Add(body);
            _scroll.contentContainer.RegisterCallback<FocusInEvent>(e => { if (e.target is VisualElement ve) _scroll.ScrollTo(RowOf(ve) ?? ve); });

            var foot = new VisualElement(); foot.AddToClassList("row"); foot.AddToClassList("settings-foot");
            _done = UiKit.Button("@settings.done", Close, "hp-btn--primary", "hp-btn--small", "hp-btn--center", UiKit.BackClass);
            _done.name = "btn-settings-done";
            _reset = UiKit.Button("@settings.reset", ResetTab, "hp-btn--small", "hp-btn--center");
            _reset.name = "btn-settings-reset";
            foot.Add(_done); foot.Add(_reset);
            foot.Add(new VisualElement { style = { flexGrow = 1 } });
            var prompts = new VisualElement(); prompts.AddToClassList("row");
            prompts.Add(InputGlyphs.Prompt(UiPrompt.Tab, "prompt.tabs", out _));
            prompts.Add(InputGlyphs.Prompt(UiPrompt.Back, "prompt.back", out _));
            foot.Add(prompts);
            _card.Add(foot);
            host.Add(_card);

            UiNav.Row(_tabButtons.ConvertAll(b => (VisualElement)b));
            UiNav.Link(_done, Direction.Right, _reset); UiNav.Link(_reset, Direction.Left, _done);
            _card.RegisterCallback<AttachToPanelEvent>(_ => StartPolling());
            _card.RegisterCallback<DetachFromPanelEvent>(_ => { _poll?.Pause(); _poll = null; });
            StartPolling();
            SelectTab(0);
        }

        static VisualElement Tape(string pos) { var t = new VisualElement { pickingMode = PickingMode.Ignore }; t.AddToClassList("masking-tape"); t.AddToClassList(pos); return t; }

        public void FocusFirst() => UiFocus.FocusLater(_tabButtons[_tab]);

        public void Close()
        {
            if (_rebind != null) { _rebind.Cancel(); }
            _poll?.Pause(); _poll = null;
            SettingsService.Persist();
            Closed?.Invoke();
        }

        void StartPolling()
        {
            if (_poll != null || _card.panel == null) return;
            _poll = _card.schedule.Execute(Poll).Every(16);
        }

        /// <summary>LB/RB and Q/E switch tabs (not while typing or rebinding).</summary>
        void Poll()
        {
            if (_rebind != null || _card.panel == null) return;
            var focused = _card.panel.focusController?.focusedElement as VisualElement;
            bool typing = focused != null && (focused is TextField || focused.parent is TextField || focused.GetFirstAncestorOfType<TextField>() != null);
            var kb = Keyboard.current; var pad = Gamepad.current;
            int step = 0;
            if (pad != null && pad.leftShoulder.wasPressedThisFrame || !typing && kb != null && kb.qKey.wasPressedThisFrame) step = -1;
            if (pad != null && pad.rightShoulder.wasPressedThisFrame || !typing && kb != null && kb.eKey.wasPressedThisFrame) step = 1;
            if (step == 0) return;
            SelectTab((_tab + step + TabIds.Length) % TabIds.Length);
            UiSounds.Hover();
            UiFocus.FocusLater(_tabButtons[_tab]);
        }

        public void SelectTab(int index)
        {
            _tab = Mathf.Clamp(index, 0, TabIds.Length - 1);
            for (int i = 0; i < _tabButtons.Count; i++) UiKit.Select(_tabButtons[i], i == _tab);
            _rows.Clear(); _focusables.Clear(); _preview.Clear();
            _micMeter = null;
            var s = SettingsService.Current.Copy();
            void Commit() => SettingsService.Apply(s, persist: false);
            switch (_tab)
            {
                case 0: BuildGameplay(s, Commit); break;
                case 1: BuildControls(); break;
                case 2: BuildAudio(s, Commit); break;
                case 3: BuildVideo(s, Commit); break;
                default: BuildAccess(s, Commit); break;
            }
            Describe("@settings.tab." + TabIds[_tab], "settings.tabinfo." + TabIds[_tab]);
            LinkRows();
            _scroll.scrollOffset = Vector2.zero;
        }

        void ResetTab()
        {
            var d = new GameSettings();
            var s = SettingsService.Current.Copy();
            switch (_tab)
            {
                case 0: s.Sensitivity = d.Sensitivity; s.InvertY = d.InvertY; s.Fov = d.Fov; s.CameraBob = d.CameraBob; s.CameraShake = d.CameraShake; s.TipsEnabled = d.TipsEnabled; break;
                case 1: InputBindings.ResetAll(); break;
                case 2: s.MasterVolume = d.MasterVolume; s.MusicVolume = d.MusicVolume; s.SfxVolume = d.SfxVolume; s.VoiceVolume = d.VoiceVolume; s.PushToTalk = d.PushToTalk; s.OpenMicThreshold = d.OpenMicThreshold; break;
                case 3: s.QualityLevel = d.QualityLevel; s.WindowMode = d.WindowMode; s.ResolutionWidth = d.ResolutionWidth; s.ResolutionHeight = d.ResolutionHeight; s.VSync = d.VSync; s.FrameRateCap = d.FrameRateCap; s.ResolutionScale = d.ResolutionScale; break;
                default: s.UiScale = d.UiScale; s.TextSize = d.TextSize; s.Colorblind = d.Colorblind; s.ReducedMotion = d.ReducedMotion; s.Subtitles = d.Subtitles; s.CaptionBackground = d.CaptionBackground; break;
            }
            SettingsService.Apply(s);
            SelectTab(_tab);
            UiFocus.FocusLater(_reset);
        }

        void LinkRows()
        {
            var tab = _tabButtons[_tab];
            if (_focusables.Count == 0)
            {
                UiNav.RowVertical(_tabButtons.ConvertAll(b => (VisualElement)b), null, _done);
                UiNav.Link(_done, Direction.Up, tab); UiNav.Link(_reset, Direction.Up, tab);
                return;
            }
            UiNav.Column(_focusables);
            UiNav.RowVertical(_tabButtons.ConvertAll(b => (VisualElement)b), null, _focusables[0]);
            UiNav.Link(_focusables[0], Direction.Up, tab);
            var last = _focusables[_focusables.Count - 1];
            UiNav.Link(last, Direction.Down, _done);
            UiNav.Link(_done, Direction.Up, last); UiNav.Link(_reset, Direction.Up, last);
        }

        // ---------------------------------------------------------------- tabs
        void BuildGameplay(GameSettings s, Action commit)
        {
            AddText("name", s.PlayerName, v => { s.PlayerName = v; commit(); });
            AddSlider("sensitivity", GameSettings.MinSensitivity, GameSettings.MaxSensitivity, s.Sensitivity, v => { s.Sensitivity = v; commit(); }, v => v.ToString("0.0") + "x");
            AddToggle("inverty", s.InvertY, v => { s.InvertY = v; commit(); });
            AddSlider("fov", GameSettings.MinFov, GameSettings.MaxFov, s.Fov, v => { s.Fov = Mathf.Round(v); commit(); }, v => Mathf.RoundToInt(v) + "°");
            AddToggle("headbob", s.CameraBob, v => { s.CameraBob = v; commit(); });
            AddSlider("shake", 0, 1, s.CameraShake, v => { s.CameraShake = Mathf.Round(v * 20f) / 20f; commit(); }, Pct);
            AddToggle("tips", s.TipsEnabled, v => { s.TipsEnabled = v; commit(); });
            AddButtonRow("replaytapes", "@settings.row.replaytapes.button", "btn-replay-tapes", b => { new PlayerPrefsTipStore().ClearAll(); b.text = UiText.Get("settings.row.replaytapes.done"); });
        }

        void BuildAudio(GameSettings s, Action commit)
        {
            AddSlider("master", 0, 1, s.MasterVolume, v => { s.MasterVolume = v; commit(); }, Pct);
            AddSlider("music", 0, 1, s.MusicVolume, v => { s.MusicVolume = v; commit(); }, Pct);
            AddSlider("sfx", 0, 1, s.SfxVolume, v => { s.SfxVolume = v; commit(); }, Pct);
            AddSlider("voice", 0, 1, s.VoiceVolume, v => { s.VoiceVolume = v; commit(); }, Pct);
            AddToggle("ptt", s.PushToTalk, v => { s.PushToTalk = v; commit(); });
            AddSlider("micsens", 0, 1, 1f - s.OpenMicThreshold, v => { s.OpenMicThreshold = 1f - Mathf.Round(v * 20f) / 20f; commit(); }, Pct);
            BuildMicMeter(s);
        }

        VisualElement _micMeter;

        /// <summary>Live mic level (IVoiceChat.LocalLoudness) against the open-mic threshold, in the side card.</summary>
        void BuildMicMeter(GameSettings s)
        {
            var box = new VisualElement(); box.AddToClassList("mic-box");
            box.Add(UiKit.Label("@settings.mic.head", "bold", "preview-head"));
            var bar = new VisualElement(); bar.AddToClassList("mic-bar");
            var fill = new VisualElement { name = "mic-fill" }; fill.AddToClassList("mic-fill");
            var mark = new VisualElement { name = "mic-threshold" }; mark.AddToClassList("mic-threshold");
            bar.Add(fill); bar.Add(mark);
            box.Add(bar);
            var status = UiKit.Label("", "small-text", "mic-status");
            box.Add(status);
            _preview.Add(box);
            _micMeter = box;
            box.schedule.Execute(() =>
            {
                bool has = Services.TryGet(out IVoiceChat voice);
                float level = has ? Mathf.Clamp01(voice.LocalLoudness) : 0f;
                float th = SettingsService.Current.OpenMicThreshold;
                fill.style.width = Length.Percent(level * 100f);
                mark.style.left = Length.Percent(th * 100f);
                status.text = !has ? UiText.Get("settings.mic.none") : SettingsService.Current.PushToTalk ? UiText.Get("settings.mic.ptt") : level >= th ? UiText.Get("settings.mic.sending") : UiText.Get("settings.mic.quiet");
            }).Every(50);
        }

        void BuildVideo(GameSettings s, Action commit)
        {
            var names = VideoOptions.QualityNames;
            var quality = new List<string> { UiText.Get("settings.value.default") };
            foreach (var n in names) quality.Add(n);
            AddChoice("quality", quality.ToArray(), VideoOptions.QualityChoice(s.QualityLevel, names.Length), i => { s.QualityLevel = i - 1; commit(); });
            AddChoice("window", new[] { UiText.Get("settings.value.fullscreen"), UiText.Get("settings.value.borderless"), UiText.Get("settings.value.windowed") }, (int)s.WindowMode, i => { s.WindowMode = (WindowModeSetting)i; commit(); });
            var sizes = VideoOptions.Available();
            var labels = new List<string> { UiText.Get("settings.value.native") };
            foreach (var r in sizes) labels.Add(r.x + " × " + r.y);
            AddChoice("resolution", labels.ToArray(), VideoOptions.IndexOf(sizes, s.ResolutionWidth, s.ResolutionHeight), i =>
            {
                if (i == 0) { s.ResolutionWidth = 0; s.ResolutionHeight = 0; }
                else { s.ResolutionWidth = sizes[i - 1].x; s.ResolutionHeight = sizes[i - 1].y; }
                commit();
            });
            AddToggle("vsync", s.VSync, v => { s.VSync = v; commit(); });
            var caps = new string[GameSettings.FrameRateCaps.Length];
            for (int i = 0; i < caps.Length; i++) caps[i] = GameSettings.FrameRateCaps[i] == 0 ? UiText.Get("settings.value.uncapped") : GameSettings.FrameRateCaps[i] + " fps";
            AddChoice("fpscap", caps, Mathf.Max(0, Array.IndexOf(GameSettings.FrameRateCaps, s.FrameRateCap)), i => { s.FrameRateCap = GameSettings.FrameRateCaps[i]; commit(); });
            AddSlider("renderscale", GameSettings.MinResolutionScale, GameSettings.MaxResolutionScale, s.ResolutionScale, v => { s.ResolutionScale = Mathf.Round(v * 20f) / 20f; commit(); }, Pct);
            if (Application.isEditor) _rows.Add(UiKit.Label("@settings.video.editornote", "small-text", "muted", "settings-note"));
        }

        void BuildAccess(GameSettings s, Action commit)
        {
            AddSlider("uiscale", GameSettings.MinUiScale, GameSettings.MaxUiScale, s.UiScale, v => { s.UiScale = Mathf.Round(v * 20f) / 20f; commit(); }, Pct);
            AddChoice("textsize", new[] { UiText.Get("settings.value.small"), UiText.Get("settings.value.medium"), UiText.Get("settings.value.large") }, s.TextSize, i => { s.TextSize = i; commit(); });
            AddChoice("colorblind", new[] { UiText.Get("settings.value.off"), UiText.Get("settings.value.deutan"), UiText.Get("settings.value.protan"), UiText.Get("settings.value.tritan") }, (int)s.Colorblind,
                i => { s.Colorblind = (ColorblindMode)i; commit(); BuildPalettePreview((ColorblindMode)i); });
            AddToggle("reducedmotion", s.ReducedMotion, v => { s.ReducedMotion = v; commit(); });
            AddToggle("captions", s.Subtitles, v => { s.Subtitles = v; commit(); });
            AddSlider("captionbg", 0, 1, s.CaptionBackground, v => { s.CaptionBackground = Mathf.Round(v * 20f) / 20f; commit(); }, Pct);
            BuildPalettePreview(s.Colorblind);
        }

        /// <summary>Side-card swatches: team colours with their shapes, and the reserved meaning colours, for the chosen mode.</summary>
        void BuildPalettePreview(ColorblindMode mode)
        {
            _preview.Clear();
            _preview.Add(UiKit.Label("@settings.preview.head", "bold", "preview-head"));
            var team = new VisualElement(); team.AddToClassList("row"); team.AddToClassList("preview-row");
            for (int i = 0; i < 4; i++) { var b = new FarmerBadge(i, 74f); b.AddToClassList("preview-badge"); team.Add(b); }
            _preview.Add(team);
            var reserved = new VisualElement(); reserved.AddToClassList("preview-grid");
            foreach (var (c, key, glyph) in new[] { (ReservedColor.Money, "settings.preview.money", "▲"), (ReservedColor.Loss, "settings.preview.loss", "▼"), (ReservedColor.Good, "settings.preview.good", "✓"), (ReservedColor.Danger, "settings.preview.danger", "!") })
            {
                var cell = new VisualElement(); cell.AddToClassList("row"); cell.AddToClassList("swatch-cell");
                var sw = new Label(glyph); sw.AddToClassList("swatch"); sw.style.backgroundColor = UiPalette.Get(c, mode);
                cell.Add(sw); cell.Add(UiKit.Label("@" + key, "small-text", "swatch-label"));
                reserved.Add(cell);
            }
            _preview.Add(reserved);
        }

        // ---------------------------------------------------------------- controls (rebinding)
        Label _bindWarning;

        void BuildControls()
        {
            var head = new VisualElement(); head.AddToClassList("setting-row"); head.AddToClassList("bind-head");
            head.Add(UiKit.Label("@settings.controls.action", "bold", "setting-label"));
            head.Add(new VisualElement { style = { flexGrow = 1 } });
            head.Add(UiKit.Label("@settings.controls.kbm", "bold", "bind-col"));
            head.Add(UiKit.Label("@settings.controls.pad", "bold", "bind-col"));
            _rows.Add(head);
            var rows = new Dictionary<string, (VisualElement row, Button kb, Button pad)>();
            var order = new List<string>();
            foreach (var d in InputBindings.Defaults)
            {
                string key = d.Action + "|" + d.Label;
                if (!rows.TryGetValue(key, out var r))
                {
                    var row = new VisualElement(); row.AddToClassList("setting-row");
                    row.Add(UiKit.Label(d.Label, "bold", "setting-label"));
                    row.Add(new VisualElement { style = { flexGrow = 1 } });
                    var kbSlot = new VisualElement { name = "kb-slot" }; kbSlot.AddToClassList("bind-slot");
                    var padSlot = new VisualElement { name = "pad-slot" }; padSlot.AddToClassList("bind-slot");
                    row.Add(kbSlot); row.Add(padSlot);
                    _rows.Add(row);
                    r = (row, null, null);
                    order.Add(key);
                }
                var b = BindButton(d);
                if (d.IsKeyboardMouse) { r.row.Q("kb-slot").Add(b); if (r.kb == null) r.kb = b; }
                else { r.row.Q("pad-slot").Add(b); if (r.pad == null) r.pad = b; }
                rows[key] = r;
            }
            Button prevKb = null, prevPad = null;
            foreach (var key in order)
            {
                var r = rows[key];
                var first = (VisualElement)r.kb ?? r.pad;
                if (first == null) continue;
                _focusables.Add(first);
                if (r.kb != null && r.pad != null) { UiNav.Link(r.kb, Direction.Right, r.pad); UiNav.Link(r.pad, Direction.Left, r.kb); }
                if (r.pad != null && prevPad != null) { UiNav.Link(r.pad, Direction.Up, prevPad); UiNav.Link(prevPad, Direction.Down, r.pad); }
                if (r.pad != null) prevPad = r.pad;
                if (r.kb != null) prevKb = r.kb;
            }
            _bindWarning = UiKit.Label("", "small-text", "bind-warning");
            _rows.Add(_bindWarning);
            AddButtonRow("resetcontrols", "@settings.row.resetcontrols.button", "btn-bind-reset", _ => { InputBindings.ResetAll(); SelectTab(_tab); });
        }

        Button BindButton(BindingDef def)
        {
            var b = UiKit.Button(InputBindings.Display(def), null, "hp-btn--small", "hp-btn--center", "bind-btn");
            b.name = "bind-" + def.Action.ToLowerInvariant() + "-" + def.DefaultPath.Replace("<", "").Replace(">", "").Replace("/", "-").ToLowerInvariant();
            b.clicked += () => StartRebind(def, b);
            return b;
        }

        void StartRebind(BindingDef def, Button b)
        {
            if (_rebind != null) return;
            var action = InputBindings.Template.FindActionMap(InputBindings.MapName).FindAction(def.Action);
            int index = InputBindings.TemplateIndex(action, def);
            if (index < 0) return;
            b.text = UiText.Get("settings.controls.listening");
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
                    _bindWarning.text = InputBindings.TryFindConflict(def, path, out var other)
                        ? UiText.Format("settings.controls.conflict", InputBindings.DisplayPath(path), other.Label) : "";
                    InputBindings.SetOverride(def.Action, def.DefaultPath, path);
                }
                b.text = InputBindings.Display(def);
            }
            _rebind.OnComplete(_ => Finish(true)).OnCancel(_ => Finish(false)).Start();
        }

        static string Pct(float v) => Mathf.RoundToInt(v * 100f) + "%";

        // ---------------------------------------------------------------- row builders
        VisualElement Row(string id)
        {
            var row = new VisualElement { name = "row-" + id }; row.AddToClassList("setting-row");
            row.Add(UiKit.Label("@settings.row." + id, "bold", "setting-label"));
            _rows.Add(row);
            row.RegisterCallback<FocusInEvent>(_ => Describe("@settings.row." + id, "settings.desc." + id));
            return row;
        }

        void Describe(string titleKey, string bodyKey)
        {
            _sideTitle.text = UiKit.T(titleKey);
            _sideBody.text = UiText.Has(bodyKey) ? UiText.Get(bodyKey) : "";
        }

        static VisualElement RowOf(VisualElement e)
        {
            for (var p = e; p != null; p = p.parent) if (p.ClassListContains("setting-row")) return p;
            return null;
        }

        void AddSlider(string id, float min, float max, float value, Action<float> set, Func<float, string> fmt)
        {
            var row = Row(id);
            var slider = new Slider(min, max) { value = value, name = "slider-" + id };
            slider.AddToClassList("hp-slider");
            var val = UiKit.Label(fmt(value), "bold", "setting-value");
            slider.RegisterValueChangedCallback(e => { val.text = fmt(e.newValue); set(e.newValue); });
            row.Add(slider); row.Add(val);
            _focusables.Add(slider);
        }

        /// <summary>Whole-row toggle: Submit / click / Left (off) / Right (on). Shows an ON/OFF switch stamp.</summary>
        void AddToggle(string id, bool value, Action<bool> set)
        {
            var row = Row(id);
            row.AddToClassList(UiKit.FocusRowClass); row.AddToClassList(UiKit.ChoiceClass);
            row.focusable = true; row.tabIndex = 0;
            row.Add(new VisualElement { style = { flexGrow = 1 } });
            var sw = new VisualElement { name = "switch-" + id }; sw.AddToClassList("switch");
            var knob = new VisualElement(); knob.AddToClassList("switch__knob");
            var text = UiKit.Label("", "switch__text");
            sw.Add(text); sw.Add(knob);
            row.Add(sw);
            void Show() { sw.EnableInClassList("switch--on", value); text.text = UiText.Get(value ? "settings.value.on" : "settings.value.off"); }
            void SetValue(bool v) { if (v == value) return; value = v; Show(); set(value); UiSounds.Confirm(); }
            Show();
            row.RegisterCallback<ClickEvent>(_ => { row.Focus(); SetValue(!value); });
            row.RegisterCallback<NavigationSubmitEvent>(_ => SetValue(!value));
            row.RegisterCallback<NavigationMoveEvent>(e =>
            {
                if (e.direction == Direction.Left) { SetValue(false); e.StopPropagation(); row.focusController?.IgnoreEvent(e); }
                else if (e.direction == Direction.Right) { SetValue(true); e.StopPropagation(); row.focusController?.IgnoreEvent(e); }
            });
            _focusables.Add(row);
        }

        /// <summary>Whole-row choice: Left/Right (or the arrows, or Submit) step through <paramref name="options"/>.</summary>
        void AddChoice(string id, string[] options, int index, Action<int> set)
        {
            var row = Row(id);
            row.AddToClassList(UiKit.FocusRowClass); row.AddToClassList(UiKit.ChoiceClass);
            row.focusable = true; row.tabIndex = 0;
            row.Add(new VisualElement { style = { flexGrow = 1 } });
            index = Mathf.Clamp(index, 0, options.Length - 1);
            var left = UiKit.Button("‹", null, "hp-btn--small", "hp-btn--icon", "arrow-btn");
            var right = UiKit.Button("›", null, "hp-btn--small", "hp-btn--icon", "arrow-btn");
            left.focusable = false; right.focusable = false;
            var cur = UiKit.Label(options[index], "bold", "choice-label"); cur.name = "choice-" + id;
            var pips = new VisualElement(); pips.AddToClassList("choice-pips");
            void Show()
            {
                cur.text = options[index];
                pips.Clear();
                if (options.Length <= 8) for (int i = 0; i < options.Length; i++) { var p = new VisualElement(); p.AddToClassList("choice-pip"); p.EnableInClassList("choice-pip--on", i == index); pips.Add(p); }
            }
            void Step(int d)
            {
                index = (index + d + options.Length) % options.Length;
                Show(); set(index); UiSounds.Hover();
                UiKit.Pulse(cur, "choice-label--bump", 140);
            }
            left.clicked += () => { row.Focus(); Step(-1); };
            right.clicked += () => { row.Focus(); Step(1); };
            row.RegisterCallback<NavigationSubmitEvent>(_ => Step(1));
            row.RegisterCallback<NavigationMoveEvent>(e =>
            {
                if (e.direction != Direction.Left && e.direction != Direction.Right) return;
                Step(e.direction == Direction.Left ? -1 : 1);
                e.StopPropagation(); row.focusController?.IgnoreEvent(e);
            });
            var box = new VisualElement(); box.AddToClassList("choice-box");
            var line = new VisualElement(); line.AddToClassList("row");
            line.Add(left); line.Add(cur); line.Add(right);
            box.Add(line); box.Add(pips);
            row.Add(box);
            Show();
            _focusables.Add(row);
        }

        void AddText(string id, string value, Action<string> set)
        {
            var row = Row(id);
            var tf = new TextField { value = value, maxLength = 20, name = "field-" + id };
            tf.AddToClassList("hp-field"); tf.AddToClassList("name-field");
            tf.RegisterValueChangedCallback(e => set(e.newValue));
            row.Add(new VisualElement { style = { flexGrow = 1 } });
            row.Add(tf);
            _focusables.Add(tf);
        }

        void AddButtonRow(string id, string buttonKey, string name, Action<Button> onClick)
        {
            var row = Row(id);
            row.Add(new VisualElement { style = { flexGrow = 1 } });
            Button b = null;
            b = UiKit.Button(buttonKey, () => onClick(b), "hp-btn--small", "hp-btn--center", "row-btn");
            b.name = name;
            row.Add(b);
            _focusables.Add(b);
        }
    }
}
