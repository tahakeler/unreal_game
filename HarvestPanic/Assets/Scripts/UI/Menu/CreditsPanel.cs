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
            var card = new VisualElement { name = "credits-card" }; card.AddToClassList("sign"); card.AddToClassList("help-card");
            card.style.width = 1180;
            var title = new Label("Credits") { name = "credits-title" }; title.AddToClassList("display"); title.AddToClassList("sign-title");
            card.Add(title);
            var paper = new VisualElement(); paper.AddToClassList("paper-card");
            Scroll = new ScrollView(ScrollViewMode.Vertical) { name = "credits-scroll" };
            Scroll.style.maxHeight = 640; Scroll.style.minHeight = 400;
            paper.Add(Scroll);
            foreach (var block in (text ?? CreditsText.Load()).Replace("\r\n", "\n").Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries))
            {
                string b = block.Trim('\n');
                if (b.Length == 0) continue;
                var l = new Label(b) { enableRichText = false };
                l.AddToClassList("small-text");
                l.style.whiteSpace = WhiteSpace.Normal;
                l.style.marginBottom = 10;
                if (IsHeading(b)) l.AddToClassList("bold");
                Scroll.Add(l);
                _paragraphs.Add(l);
            }
            card.Add(paper);
            _back = new Button(() => Closed?.Invoke()) { text = "Back", name = "btn-credits-back" };
            _back.AddToClassList("hp-btn"); _back.AddToClassList("hp-btn--primary"); _back.AddToClassList("hp-btn--small"); _back.AddToClassList("hp-btn--center");
            _back.RegisterCallback<NavigationMoveEvent>(e =>
            {
                if (e.direction == NavigationMoveEvent.Direction.Up) { ScrollBy(-ScrollStep); e.StopPropagation(); }
                else if (e.direction == NavigationMoveEvent.Direction.Down) { ScrollBy(ScrollStep); e.StopPropagation(); }
            });
            card.Add(_back);
            host.Add(card);
        }

        public void FocusFirst() => UiFocus.FocusLater(_back);

        public void ScrollBy(float dy)
        {
            var o = Scroll.scrollOffset;
            Scroll.scrollOffset = new UnityEngine.Vector2(o.x, UnityEngine.Mathf.Max(0f, o.y + dy));
        }

        /// <summary>True when any paragraph contains <paramref name="s"/> (tests).</summary>
        public bool Contains(string s)
        {
            foreach (var l in _paragraphs) if (l.text.Contains(s)) return true;
            return false;
        }

        static bool IsHeading(string block)
        {
            int nl = block.IndexOf('\n');
            return nl > 0 && nl + 1 < block.Length && block[nl + 1] == '-' || block.StartsWith("HARVEST PANIC -", StringComparison.Ordinal);
        }
    }
}
