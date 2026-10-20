using HarvestPanic.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;

namespace HarvestPanic.Camcorder
{
    /// <summary>
    /// Camcorder UI (UI Toolkit, built in code, client-local): "Clip saved!" toast, VHS tape shelf, replay viewer bar
    /// (play/pause, 0.25x/0.5x/1x, camera, scrub, export) and the "Moment of the day" card shown during payout next to the
    /// receipt. Uses the Frontend theme (Resources/Frontend/HP_Theme) when present. No gameplay references.
    /// </summary>
    public sealed class CamcorderHud : MonoBehaviour
    {
        CamcorderService _svc;
        UIDocument _doc;
        VisualElement _root, _shelf, _shelfList, _viewer, _card;
        Label _toast, _vTitle, _vTime, _vStatus, _cardTitle;
        Image _cardImage;
        Slider _scrub;
        Button _play, _camBtn;
        readonly Button[] _speedBtns = new Button[3];
        float _toastUntil;
        bool _scrubbing;
        bool _cursorSaved, _prevVisible;
        CursorLockMode _prevLock;
        Coroutine _export;

        public bool ShelfOpen { get; private set; }
        public bool CardVisible => _card != null && _card.style.display == DisplayStyle.Flex;
        public string LastToast { get; private set; } = "";
        public string LastExportFolder { get; private set; }

        static readonly Color Ink = new Color(0.12f, 0.1f, 0.08f, 0.92f);
        static readonly Color Paper = new Color(0.98f, 0.94f, 0.82f);
        static readonly Color RecRed = new Color(0.9f, 0.15f, 0.12f);

        int _closedFrame = -1;
        /// <summary>True on the frame Esc closed the shelf/viewer (so the pause menu does not open on the same press).</summary>
        public bool ClosedThisFrame => _closedFrame == Time.frameCount;

        void Start() => EnsureBuilt();

        void EnsureBuilt()
        {
            if (_root != null) return;
            _svc = GetComponent<CamcorderService>();
            Build();
            _svc.Toast += ShowToast;
            _svc.PayoutReached += ShowCard;
            _svc.Viewer.Closed += OnViewerClosed;
        }

        void OnViewerClosed()
        {
            if (_export != null) { StopCoroutine(_export); _export = null; }
            if (_viewer != null) _viewer.style.display = DisplayStyle.None;
            RestoreOtherUi();
            if (!ShelfOpen) Release();
        }

        void OnDestroy()
        {
            if (_svc != null) { _svc.Toast -= ShowToast; _svc.PayoutReached -= ShowCard; if (_svc.Viewer != null) _svc.Viewer.Closed -= OnViewerClosed; }
            RestoreCursor();
            InputModal.Pop(this);
        }

        void Build()
        {
            var ps = ScriptableObject.CreateInstance<PanelSettings>();
            ps.name = "HP_Panel_Camcorder";
            ps.themeStyleSheet = Resources.Load<ThemeStyleSheet>("Frontend/HP_Theme");
            ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            ps.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            ps.match = 0.5f;
            ps.referenceResolution = new Vector2Int(1920, 1080);
            ps.sortingOrder = 400;
            ps.clearColor = false;
            var go = new GameObject("UI_Camcorder");
            go.transform.SetParent(transform, false);
            _doc = go.AddComponent<UIDocument>();
            _doc.panelSettings = ps;
            _root = _doc.rootVisualElement;
            _root.pickingMode = PickingMode.Ignore;
            _root.style.position = Position.Absolute;
            _root.style.left = 0; _root.style.right = 0; _root.style.top = 0; _root.style.bottom = 0;

            _toast = new Label { name = "cc-toast" };
            Style(_toast, Ink, Paper, 26);
            _toast.style.position = Position.Absolute; _toast.style.top = 90; _toast.style.alignSelf = Align.Center;
            _toast.style.left = new Length(50, LengthUnit.Percent); _toast.style.translate = new Translate(new Length(-50, LengthUnit.Percent), 0);
            _toast.style.display = DisplayStyle.None;
            _toast.pickingMode = PickingMode.Ignore;
            _root.Add(_toast);

            BuildShelf();
            BuildViewer();
            BuildCard();
        }

        static void Style(VisualElement e, Color bg, Color fg, int size)
        {
            e.style.backgroundColor = bg; e.style.color = fg; e.style.fontSize = size;
            e.style.paddingLeft = e.style.paddingRight = 18; e.style.paddingTop = e.style.paddingBottom = 8;
            e.style.borderTopLeftRadius = e.style.borderTopRightRadius = e.style.borderBottomLeftRadius = e.style.borderBottomRightRadius = 6;
        }

        Button Btn(string text, System.Action onClick, VisualElement parent, string name = null)
        {
            var b = new Button(onClick) { text = text, name = name };
            b.style.fontSize = 22; b.style.minWidth = 70; b.style.marginLeft = b.style.marginRight = 4;
            parent.Add(b);
            return b;
        }

        // ---------------------------------------------------------------- toast

        public void ShowToast(string msg)
        {
            LastToast = msg ?? "";
            if (_toast == null) return;
            _toast.text = LastToast;
            _toast.style.display = DisplayStyle.Flex;
            _toastUntil = Time.unscaledTime + 3f;
        }

        void Update()
        {
            if (_toast != null && _toast.style.display == DisplayStyle.Flex && Time.unscaledTime > _toastUntil) _toast.style.display = DisplayStyle.None;
            var v = _svc != null ? _svc.Viewer : null;
            if (v != null && v.IsOpen) RefreshViewer(v);
            else if (_viewer != null && _viewer.style.display == DisplayStyle.Flex) _viewer.style.display = DisplayStyle.None;
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                if (v != null && v.IsOpen) { CloseViewer(); _closedFrame = Time.frameCount; }
                else if (ShelfOpen) { CloseShelf(); _closedFrame = Time.frameCount; }
            }
            if (CardVisible && Services.TryGet(out IDayClockService clock))
            {
                var ph = clock.Phase;
                if (ph != DayClockPhase.Payout && ph != DayClockPhase.Sunday && ph != DayClockPhase.Evicted) HideCard();
            }
        }

        // ---------------------------------------------------------------- shelf

        void BuildShelf()
        {
            _shelf = new VisualElement { name = "cc-shelf" };
            _shelf.style.position = Position.Absolute;
            _shelf.style.left = new Length(20, LengthUnit.Percent); _shelf.style.right = new Length(20, LengthUnit.Percent);
            _shelf.style.top = new Length(12, LengthUnit.Percent); _shelf.style.bottom = new Length(12, LengthUnit.Percent);
            Style(_shelf, new Color(0.16f, 0.12f, 0.1f, 0.96f), Paper, 24);
            _shelf.style.display = DisplayStyle.None;
            var head = new Label("GRANDPA'S TAPES  ● REC  JUN 12 1994");
            head.style.fontSize = 34; head.style.color = Paper; head.style.marginBottom = 12; head.style.unityFontStyleAndWeight = FontStyle.Bold;
            _shelf.Add(head);
            _shelfList = new ScrollView { name = "cc-shelf-list" };
            _shelfList.style.flexGrow = 1;
            _shelf.Add(_shelfList);
            var row = new VisualElement(); row.style.flexDirection = FlexDirection.Row; row.style.justifyContent = Justify.FlexEnd;
            Btn("Close (Esc)", CloseShelf, row, "cc-shelf-close");
            _shelf.Add(row);
            _root.Add(_shelf);
        }

        public void OpenShelf()
        {
            EnsureBuilt();
            if (_svc == null || _shelf == null) return;
            if (_svc.Viewer.IsOpen) CloseViewer();
            HideCard();
            _shelfList.Clear();
            var items = _svc.Shelf.Items;
            if (items.Count == 0)
            {
                var empty = new Label($"No tapes yet. Disasters are filmed automatically; press {_svc.SaveKeyDisplay} to keep the last {Mathf.RoundToInt(_svc.Tuning.preRollSeconds)} s.");
                empty.style.whiteSpace = WhiteSpace.Normal; empty.style.color = Paper;
                _shelfList.Add(empty);
            }
            for (int i = items.Count - 1; i >= 0; i--)
            {
                var m = items[i];
                var row = new VisualElement { name = "cc-tape" };
                row.style.flexDirection = FlexDirection.Row; row.style.alignItems = Align.Center; row.style.marginBottom = 8;
                Style(row, new Color(0.08f, 0.07f, 0.06f, 0.9f), Paper, 22);
                if (m.Thumbnail != null)
                {
                    var img = new Image { image = m.Thumbnail, scaleMode = ScaleMode.ScaleAndCrop };
                    img.style.width = 160; img.style.height = 90; img.style.marginRight = 12;
                    row.Add(img);
                }
                var label = new Label($"{(m.Manual ? "▶ " : "● ")}{m.Title}\n<size=18>{Clock(m.Duration)} · severity {m.Severity}</size>");
                label.enableRichText = true; label.style.flexGrow = 1; label.style.color = Paper; label.style.whiteSpace = WhiteSpace.Normal;
                row.Add(label);
                Btn("Watch", () => { CloseShelf(); _svc.Watch(m); }, row);
                _shelfList.Add(row);
            }
            ShelfOpen = true;
            _shelf.style.display = DisplayStyle.Flex;
            Grab();
        }

        public void CloseShelf()
        {
            if (!ShelfOpen) return;
            ShelfOpen = false;
            _shelf.style.display = DisplayStyle.None;
            if (!_svc.Viewer.IsOpen) Release();
        }

        // ---------------------------------------------------------------- viewer bar

        void BuildViewer()
        {
            _viewer = new VisualElement { name = "cc-viewer" };
            _viewer.style.position = Position.Absolute; _viewer.style.left = 0; _viewer.style.right = 0; _viewer.style.top = 0; _viewer.style.bottom = 0;
            _viewer.pickingMode = PickingMode.Ignore;
            _viewer.style.display = DisplayStyle.None;

            var top = new VisualElement(); top.pickingMode = PickingMode.Ignore;
            top.style.flexDirection = FlexDirection.Row; top.style.justifyContent = Justify.SpaceBetween; top.style.paddingLeft = top.style.paddingRight = 40; top.style.paddingTop = 30;
            _vStatus = new Label("PLAY ▶"); _vStatus.style.fontSize = 44; _vStatus.style.color = Color.white; _vStatus.style.unityFontStyleAndWeight = FontStyle.Bold;
            var date = new Label("JUN 12 1994"); date.style.fontSize = 40; date.style.color = Color.white; date.style.unityFontStyleAndWeight = FontStyle.Bold;
            top.Add(_vStatus); top.Add(date);
            _viewer.Add(top);

            var bar = new VisualElement { name = "cc-viewer-bar" };
            bar.style.position = Position.Absolute; bar.style.left = 40; bar.style.right = 40; bar.style.bottom = 30;
            Style(bar, Ink, Paper, 22);
            _vTitle = new Label(); _vTitle.style.fontSize = 28; _vTitle.style.color = Paper; _vTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            bar.Add(_vTitle);
            _scrub = new Slider(0f, 1f) { name = "cc-scrub" };
            _scrub.style.marginTop = 6; _scrub.style.marginBottom = 6;
            _scrub.RegisterValueChangedCallback(e => { if (_scrubbing) _svc.Viewer.SetTime(e.newValue); });
            _scrub.RegisterCallback<PointerDownEvent>(_ => _scrubbing = true, TrickleDown.TrickleDown);
            _scrub.RegisterCallback<PointerUpEvent>(_ => _scrubbing = false, TrickleDown.TrickleDown);
            _scrub.RegisterCallback<PointerCaptureOutEvent>(_ => _scrubbing = false);
            bar.Add(_scrub);
            var row = new VisualElement(); row.style.flexDirection = FlexDirection.Row; row.style.alignItems = Align.Center;
            _play = Btn("Pause", () => _svc.Viewer.Playing = !_svc.Viewer.Playing, row, "cc-play");
            _speedBtns[0] = Btn("0.25×", () => _svc.Viewer.Speed = 0.25f, row);
            _speedBtns[1] = Btn("0.5×", () => _svc.Viewer.Speed = 0.5f, row);
            _speedBtns[2] = Btn("1×", () => _svc.Viewer.Speed = 1f, row);
            _camBtn = Btn("Grandpa Cam", () => _svc.Viewer.CycleCamera(), row, "cc-cam");
            _vTime = new Label(); _vTime.style.flexGrow = 1; _vTime.style.unityTextAlign = TextAnchor.MiddleCenter; _vTime.style.color = Paper;
            row.Add(_vTime);
            Btn("Export clip", StartExport, row, "cc-export");
            Btn("Tapes", () => { CloseViewer(); OpenShelf(); }, row);
            Btn("Close", CloseViewer, row, "cc-close");
            bar.Add(row);
            var help = new Label("Space play/pause · 1/2/3 speed · C camera · RMB look · WASD/QE fly (Free Cam) · ←/→ skip");
            help.style.fontSize = 16; help.style.color = new Color(1, 1, 1, 0.6f);
            bar.Add(help);
            _viewer.Add(bar);
            _root.Add(_viewer);
        }

        readonly System.Collections.Generic.List<(VisualElement el, StyleEnum<DisplayStyle> was)> _hiddenRoots = new System.Collections.Generic.List<(VisualElement, StyleEnum<DisplayStyle>)>();

        /// <summary>Hides every other UIDocument (gameplay HUD) while the replay plays; restored on close. Presentation only.</summary>
        void HideOtherUi()
        {
            foreach (var d in FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
            {
                if (d == _doc || !d.isActiveAndEnabled || d.rootVisualElement == null) continue;
                var r = d.rootVisualElement;
                if (r.resolvedStyle.display == DisplayStyle.None || r.style.display == DisplayStyle.None) continue;
                _hiddenRoots.Add((r, r.style.display));
                r.style.display = DisplayStyle.None;
            }
        }

        void RestoreOtherUi()
        {
            foreach (var (el, was) in _hiddenRoots) if (el != null) el.style.display = was;
            _hiddenRoots.Clear();
        }

        public void ShowViewer(Moment m)
        {
            EnsureBuilt();
            HideOtherUi();
            HideCard();
            _vTitle.text = m.Title;
            _scrub.highValue = Mathf.Max(0.01f, m.Duration);
            _viewer.style.display = DisplayStyle.Flex;
            Grab();
        }

        void RefreshViewer(ReplayViewer v)
        {
            if (_viewer.style.display != DisplayStyle.Flex) _viewer.style.display = DisplayStyle.Flex;
            if (!_scrubbing) _scrub.SetValueWithoutNotify(v.Time);
            bool slow = v.EffectiveSpeed < v.Speed - 1e-3f;
            _vStatus.text = v.Exporting ? "● REC  EXPORTING" : v.Playing ? (slow ? "SLOW ▶" : "PLAY ▶") : "PAUSE ‖";
            _play.text = v.Playing ? "Pause" : "Play";
            _camBtn.text = v.Mode == ViewerCam.Grandpa ? "Grandpa Cam" : v.Mode == ViewerCam.Victim ? "Victim Cam" : "Free Cam";
            _vTime.text = $"{Clock(v.Time)} / {Clock(v.Current.Duration)}  ·  {v.EffectiveSpeed:0.##}×";
            float[] speeds = { 0.25f, 0.5f, 1f };
            for (int i = 0; i < 3; i++) _speedBtns[i].style.unityFontStyleAndWeight = Mathf.Approximately(v.Speed, speeds[i]) ? FontStyle.Bold : FontStyle.Normal;
        }

        public void CloseViewer() => _svc.Viewer.Close(); // OnViewerClosed tidies up

        public void StartExport()
        {
            if (_export != null || !_svc.Viewer.IsOpen) return;
            ShowToast("● REC  Exporting frames...");
            _export = StartCoroutine(ClipExporter.Export(_svc.Viewer, _svc.Tuning, (folder, err) =>
            {
                _export = null;
                if (err != null) { ShowToast(err); return; }
                LastExportFolder = folder;
                ShowToast("Clip saved! " + folder);
            }));
        }

        /// <summary>True while an export coroutine runs (tests poll this).</summary>
        public bool IsExporting => _export != null;

        // ---------------------------------------------------------------- payout card

        void BuildCard()
        {
            _card = new VisualElement { name = "cc-moment-card" };
            _card.style.position = Position.Absolute; _card.style.right = 40; _card.style.bottom = 150; _card.style.width = 420;
            Style(_card, Paper, Ink, 22);
            _card.style.borderTopWidth = _card.style.borderBottomWidth = _card.style.borderLeftWidth = _card.style.borderRightWidth = 4;
            _card.style.borderTopColor = _card.style.borderBottomColor = _card.style.borderLeftColor = _card.style.borderRightColor = Ink;
            _card.style.rotate = new Rotate(new Angle(-2f));
            _card.style.display = DisplayStyle.None;
            var head = new Label("● MOMENT OF THE DAY");
            head.style.fontSize = 26; head.style.color = RecRed; head.style.unityFontStyleAndWeight = FontStyle.Bold;
            _card.Add(head);
            _cardImage = new Image { scaleMode = ScaleMode.ScaleAndCrop, name = "cc-moment-thumb" };
            _cardImage.style.width = 380; _cardImage.style.height = 214; _cardImage.style.marginTop = 6; _cardImage.style.marginBottom = 6;
            _cardImage.style.backgroundColor = Color.black;
            _card.Add(_cardImage);
            _cardTitle = new Label(); _cardTitle.style.whiteSpace = WhiteSpace.Normal; _cardTitle.style.fontSize = 24; _cardTitle.style.unityFontStyleAndWeight = FontStyle.Bold; _cardTitle.style.color = Ink;
            _card.Add(_cardTitle);
            var row = new VisualElement(); row.style.flexDirection = FlexDirection.Row; row.style.marginTop = 6;
            Btn("▶ Watch", () => { var b = _svc.Shelf.Best(); if (b != null) _svc.Watch(b); }, row, "cc-card-watch");
            Btn("All tapes", OpenShelf, row);
            _card.Add(row);
            _root.Add(_card);
        }

        public void ShowCard()
        {
            EnsureBuilt();
            var best = _svc != null ? _svc.Shelf.Best() : null;
            if (best == null || _card == null) return;
            _cardImage.image = best.Thumbnail;
            _cardTitle.text = best.Title;
            _card.style.display = DisplayStyle.Flex;
            // The receipt owns the cursor during payout; make sure the card is clickable.
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }

        public void HideCard() { if (_card != null) _card.style.display = DisplayStyle.None; }

        // ---------------------------------------------------------------- helpers

        void Grab()
        {
            InputModal.Push(this);
            if (!_cursorSaved) { _cursorSaved = true; _prevLock = Cursor.lockState; _prevVisible = Cursor.visible; }
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }

        void Release()
        {
            InputModal.Pop(this);
            RestoreCursor();
        }

        void RestoreCursor()
        {
            if (!_cursorSaved) return;
            _cursorSaved = false;
            Cursor.lockState = _prevLock; Cursor.visible = _prevVisible;
        }

        static string Clock(float s) => $"{(int)(s / 60)}:{(int)(s % 60):00}.{(int)(s * 10 % 10)}";
    }
}
