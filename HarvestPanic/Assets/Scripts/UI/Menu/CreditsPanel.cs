using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace HarvestPanic.UI
{
    /// <summary>
    /// Main menu Credits screen: team credit, then every third-party asset/library and the full licence texts
    /// (<see cref="CreditsText"/>). One Label per paragraph keeps each text mesh small. Up/Down (keys or D-pad)
    /// scroll while the Back button keeps focus; mouse wheel scrolls natively.
    /// </summary>
    public sealed class CreditsPanel
    {
        public const float ScrollStep = 160f;
        public event Action Closed;
        public ScrollView Scroll { get; }
        public IReadOnlyList<Label> Paragraphs => _paragraphs;
        readonly Button _back;
        readonly List<Label> _paragraphs = new List<Label>();

        public CreditsPanel(VisualElement host, string text = null)
        {
            host.Clear();
            var card = new VisualElement { name = "credits-card" }; card.AddToClassList("paper-card"); card.AddToClassList("credits-card"); card.AddToClassList("paper-card--tilt-r");
            var tape = new VisualElement { pickingMode = PickingMode.Ignore }; tape.AddToClassList("masking-tape"); tape.AddToClassList("masking-tape--top");
            card.Add(tape);
            var title = new Label(UiText.Get("credits.title")) { name = "credits-title" }; title.AddToClassList("display"); title.AddToClassList("settings-title");
            card.Add(title);
            var underline = new VisualElement(); underline.AddToClassList("ink-underline"); card.Add(underline);
            var paper = new VisualElement(); paper.AddToClassList("paper-inset");
            Scroll = new ScrollView(ScrollViewMode.Vertical) { name = "credits-scroll" };
            Scroll.AddToClassList("credits-scroll");
            Scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            paper.Add(Scroll);
            foreach (var block in (text ?? CreditsText.Load()).Replace("\r\n", "\n").Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries))
            {
                string raw = block.Trim('\n');
                string b = StripRules(raw);
                if (b.Trim().Length == 0) continue;
                var l = new Label(b) { enableRichText = false };
                l.AddToClassList(IsHeading(raw) ? "credits-heading" : "credits-para");
                l.style.whiteSpace = WhiteSpace.Normal;
                Scroll.Add(l);
                _paragraphs.Add(l);
            }
            card.Add(paper);
            var foot = new VisualElement(); foot.AddToClassList("row"); foot.AddToClassList("settings-foot");
            _back = new Button(() => Closed?.Invoke()) { text = UiText.Get("common.back"), name = "btn-credits-back" };
            _back.AddToClassList("hp-btn"); _back.AddToClassList("hp-btn--primary"); _back.AddToClassList("hp-btn--small"); _back.AddToClassList("hp-btn--center"); _back.AddToClassList(UiKit.BackClass);
            _back.RegisterCallback<NavigationMoveEvent>(e =>
            {
                if (e.direction == NavigationMoveEvent.Direction.Up) { ScrollBy(-ScrollStep); e.StopPropagation(); _back.focusController?.IgnoreEvent(e); }
                else if (e.direction == NavigationMoveEvent.Direction.Down) { ScrollBy(ScrollStep); e.StopPropagation(); _back.focusController?.IgnoreEvent(e); }
            });
            foot.Add(_back);
            foot.Add(new VisualElement { style = { flexGrow = 1 } });
            var hint = new Label(UiText.Get("credits.hint")); hint.AddToClassList("small-text"); hint.AddToClassList("muted");
            foot.Add(hint);
            card.Add(foot);
            host.Add(card);
            // Slow auto-scroll (stops for good once the player scrolls; never with reduced motion).
            Scroll.RegisterCallback<WheelEvent>(_ => _userScrolled = true, TrickleDown.TrickleDown);
            Scroll.schedule.Execute(t =>
            {
                if (_userScrolled || UiKit.ReducedMotion || Scroll.panel == null) return;
                if (UnityEngine.Time.unscaledTime - _openedAt < 2.5f) return;
                var o = Scroll.scrollOffset;
                Scroll.scrollOffset = new UnityEngine.Vector2(o.x, o.y + t.deltaTime * 0.03f);
            }).Every(16);
            _openedAt = UnityEngine.Time.unscaledTime;
        }

        bool _userScrolled;
        readonly float _openedAt;

        public void FocusFirst() => UiFocus.FocusLater(_back);

        public void ScrollBy(float dy)
        {
            _userScrolled = true;
            var o = Scroll.scrollOffset;
            Scroll.scrollOffset = new UnityEngine.Vector2(o.x, UnityEngine.Mathf.Max(0f, o.y + dy));
        }

        /// <summary>True when any paragraph contains <paramref name="s"/> (tests).</summary>
        public bool Contains(string s)
        {
            foreach (var l in _paragraphs) if (l.text.Contains(s)) return true;
            return false;
        }

        /// <summary>Removes plain-text underline rows ("=====", "-----") that read as noise in the styled card.</summary>
        static string StripRules(string block)
        {
            var lines = block.Split('\n');
            var keep = new System.Text.StringBuilder();
            foreach (var l in lines)
            {
                string t = l.Trim();
                if (t.Length >= 3 && t.Trim('=', '-') .Length == 0) continue;
                if (keep.Length > 0) keep.Append('\n');
                keep.Append(l);
            }
            return keep.ToString();
        }

        static bool IsHeading(string block)
        {
            int nl = block.IndexOf('\n');
            return nl > 0 && nl + 1 < block.Length && block[nl + 1] == '-' || block.StartsWith("HARVEST PANIC -", StringComparison.Ordinal);
        }
    }
}
