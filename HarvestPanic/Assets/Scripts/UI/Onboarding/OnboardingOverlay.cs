using System.Collections.Generic;
using HarvestPanic.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace HarvestPanic.UI
{
    /// <summary>
    /// In-game presentation layer for Grandpa's tapes and sound captions. Client-local; reads only Core contracts:
    /// <see cref="IDayClockService"/> / <see cref="IDayPhaseInfo"/> (day start, shop), <see cref="IEconomyService"/>
    /// (sales = already learned, disasters = quiet period), <see cref="INoiseService"/> (captions, critter noise; raised on
    /// the server/host), <see cref="IPlayerHooks"/> (local player position) and <see cref="LevelMarker"/>s (bridge, pen).
    /// </summary>
    public sealed class OnboardingOverlay : MonoBehaviour
    {
        public const float TipSeconds = 8f;
        const float NearMarkerMetres = 14f, CritterNoiseMetres = 25f;

        public static ITipStore StoreOverride; // tests

        UiPanel _panel;
        VisualElement _tip, _captions;
        Label _tipTitle, _tipBody, _tipHint;
        TipRules _rules;
        CaptionFeed _feed = new CaptionFeed();
        TipDef _showing;
        float _tipUntil, _markerScanAt;
        readonly List<Vector3> _bridges = new List<Vector3>(), _pens = new List<Vector3>();
        bool _noiseHooked;
        IEconomyService _economy;
        bool _dayStartedSent, _shopSent, _captionsDirty;

        public TipRules Rules => _rules ??= new TipRules(StoreOverride ?? new PlayerPrefsTipStore());
        public TipDef Showing => _showing;
        public CaptionFeed Feed => _feed;
        public VisualElement Root => _panel != null ? _panel.Root : null;

        static bool InGame(string scene) => PauseOverlay.SceneAllowsPause(scene) && scene != SceneLoader.Lobby;

        void OnEnable()
        {
            SettingsService.Changed += OnSettings;
            SceneManager.activeSceneChanged += OnScene;
        }

        void OnDisable()
        {
            SettingsService.Changed -= OnSettings;
            SceneManager.activeSceneChanged -= OnScene;
            Unhook();
        }

        void OnScene(Scene a, Scene b)
        {
            _dayStartedSent = _shopSent = false;
            _markerScanAt = 0f;
            _feed.Clear(); _captionsDirty = true;
            HideTip();
        }

        void OnSettings(GameSettings s)
        {
            Rules.Enabled = s.TipsEnabled;
            if (!s.TipsEnabled) HideTip();
            if (!s.Subtitles) { _feed.Clear(); _captionsDirty = true; }
            _captionsDirty = true;
        }

        void Build()
        {
            if (_panel != null) return;
            _panel = UiPanel.Create(transform, "UI_Overlay", 150);
            var r = _panel.Root;
            r.pickingMode = PickingMode.Ignore;
            _tip = r.Q("tape"); _tipTitle = r.Q<Label>("tape-title"); _tipBody = r.Q<Label>("tape-body"); _tipHint = r.Q<Label>("tape-hint");
            _captions = r.Q("captions");
            _tip.style.display = DisplayStyle.None;
            OnSettings(SettingsService.Current);
        }

        // ------------------------------------------------------------------ signals
        void Hook()
        {
            if (!_noiseHooked) { _noiseHooked = true; NoiseCueFeed.OnNoiseCue += OnNoise; } // every peer (polish-2), not server-only OnNoise
            if (_economy == null && Services.TryGet(out IEconomyService e))
            {
                _economy = e;
                _economy.OnWalletTxn += OnTxn;
                _economy.OnDisaster += OnDisaster;
            }
        }

        void Unhook()
        {
            if (_noiseHooked) NoiseCueFeed.OnNoiseCue -= OnNoise;
            _noiseHooked = false;
            if (_economy != null) { _economy.OnWalletTxn -= OnTxn; _economy.OnDisaster -= OnDisaster; }
            _economy = null;
        }

        void OnTxn(WalletTxn t) { if (t.Type == WalletTxnType.Sale) Rules.MarkLearned("sell"); }
        void OnDisaster(BlameEvent _) => Rules.NotifyDisaster(Time.unscaledTime);

        /// <summary>Feeds a noise (from INoiseService or a test) into captions and tip triggers.</summary>
        public void OnNoise(NoiseEvent e)
        {
            if (!ListenerPose(out var pos, out var fwd)) return;
            float dist = Vector3.Distance(pos, e.Location);
            if (e.Source == NoiseSource.Shout && dist < 3f) Rules.MarkLearned("shout"); // the local player hollered
            if (e.Source == NoiseSource.Critter && dist <= CritterNoiseMetres) Rules.Signal(TipSignal.CritterNoiseNearby);
            if (!SettingsService.Current.Subtitles || dist > Mathf.Max(e.Radius, 4f)) return;
            string text = CaptionFeed.Describe(e, out int prio, out bool danger);
            if (text == null) return;
            string dir = CaptionFeed.Direction(pos, fwd, e.Location);
            string line = dir.Length == 0 ? "[" + text + "]" : dir == ">" ? "[" + text + "] >" : dir + " [" + text + "]";
            if (_feed.Push(line, prio, Time.unscaledTime, danger)) _captionsDirty = true;
        }

        bool ListenerPose(out Vector3 pos, out Vector3 fwd)
        {
            var cam = Camera.main;
            if (cam != null) { pos = cam.transform.position; fwd = cam.transform.forward; return true; }
            foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                if (mb is IPlayerHooks h && h.IsLocal) { pos = mb.transform.position; fwd = mb.transform.forward; return true; }
            pos = fwd = Vector3.zero;
            return false;
        }

        void ScanMarkers()
        {
            _bridges.Clear(); _pens.Clear();
            foreach (var m in LevelMarker.FindAll(MarkerRole.BridgePlank)) _bridges.Add(m.transform.position);
            foreach (var m in LevelMarker.FindAll(MarkerRole.MarketPen)) _pens.Add(m.transform.position);
        }

        static bool Near(List<Vector3> points, Vector3 p)
        {
            foreach (var q in points) if ((q - p).sqrMagnitude <= NearMarkerMetres * NearMarkerMetres) return true;
            return false;
        }

        // ------------------------------------------------------------------ loop
        void Update()
        {
            if (!InGame(SceneManager.GetActiveScene().name)) { HideTip(); return; }
            Build();
            Hook();
            float now = Time.unscaledTime;

            if (!_dayStartedSent && Services.TryGet(out IDayClockService clock) && (clock.Phase == DayClockPhase.Morning || clock.Phase == DayClockPhase.Workday))
            { _dayStartedSent = true; Rules.Signal(TipSignal.DayStarted); }
            if (Services.TryGet(out IDayPhaseInfo phase) && phase.PhaseName != null && phase.PhaseName.IndexOf("Shop", System.StringComparison.OrdinalIgnoreCase) >= 0)
            { if (!_shopSent) { _shopSent = true; Rules.Signal(TipSignal.ShopOpened); } }
            else _shopSent = false;

            if (now >= _markerScanAt) { _markerScanAt = now + 2f; ScanMarkers(); }
            if (ListenerPose(out var pos, out _))
            {
                if (Near(_bridges, pos)) Rules.Signal(TipSignal.NearBridge);
                if (Near(_pens, pos)) Rules.Signal(TipSignal.NearPen);
            }

            if (_tip != null) _tip.style.visibility = InputModal.IsBlocking ? Visibility.Hidden : Visibility.Visible;
            if (_showing != null)
            {
                bool skip = (Keyboard.current != null && Keyboard.current.backspaceKey.wasPressedThisFrame)
                            || (Gamepad.current != null && Gamepad.current.selectButton.wasPressedThisFrame);
                if (skip || now >= _tipUntil) HideTip();
            }
            var next = Rules.Tick(now, InputModal.IsBlocking || _showing != null);
            if (next != null) ShowTip(next, now);

            if (_feed.Expire(now)) _captionsDirty = true;
            if (_captionsDirty) RedrawCaptions();
        }

        public void ShowTip(TipDef def, float now)
        {
            Build();
            _showing = def;
            _tipUntil = now + TipSeconds;
            bool pad = Gamepad.current != null && Gamepad.current.lastUpdateTime > (Keyboard.current?.lastUpdateTime ?? 0);
            _tipTitle.text = def.Title;
            _tipBody.text = TipRules.Resolve(def.Body, pad);
            _tipHint.text = TipRules.Resolve("{SkipTip} skip  ·  turn tapes off in Settings > Access", pad);
            _tip.style.display = DisplayStyle.Flex;
            if (Services.TryGet(out IAudioService audio)) audio.Play2D("UI_Tape");
        }

        public void HideTip()
        {
            _showing = null;
            if (_tip != null) _tip.style.display = DisplayStyle.None;
        }

        void RedrawCaptions()
        {
            _captionsDirty = false;
            if (_captions == null) return;
            _captions.Clear();
            var mode = SettingsService.Current.Colorblind;
            foreach (var c in _feed.Lines)
            {
                var l = new Label(c.Text);
                l.AddToClassList("caption-line"); l.AddToClassList("bold");
                l.style.backgroundColor = new Color(0.17f, 0.13f, 0.09f, SettingsService.Current.CaptionBackground);
                if (c.Danger) l.style.color = UiPalette.Get(ReservedColor.Danger, mode);
                _captions.Add(l);
            }
            _captions.style.display = _feed.Lines.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
