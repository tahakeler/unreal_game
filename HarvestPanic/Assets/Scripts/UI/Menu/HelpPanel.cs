using System;
using UnityEngine.UIElements;

namespace HarvestPanic.UI
{
    /// <summary>Controls/help screen: action | keyboard and mouse | gamepad, built from current bindings.</summary>
    public sealed class HelpPanel
    {
        public event Action Closed;
        readonly Button _back;

        public HelpPanel(VisualElement host)
        {
            host.Clear();
            var card = new VisualElement(); card.AddToClassList("sign"); card.AddToClassList("help-card");
            var title = new Label("Controls") { name = "help-title" }; title.AddToClassList("display"); title.AddToClassList("sign-title");
            card.Add(title);
            var paper = new VisualElement(); paper.AddToClassList("paper-card");
            var scroll = new ScrollView(ScrollViewMode.Vertical) { name = "help-scroll" }; scroll.AddToClassList("help-scroll");
            paper.Add(scroll);
            AddRow(scroll, "Action", "Keyboard + mouse", "Gamepad", true);
            string lastAction = null;
            for (int i = 0; i < InputBindings.Defaults.Length; i++)
            {
                var d = InputBindings.Defaults[i];
                if (d.Action == lastAction || d.Group != InputBindings.KbmGroup) continue;
                lastAction = d.Action;
                string kb, pad = "-";
                if (d.Action == "Move")
                {
                    kb = InputBindings.Display(InputBindings.Defaults[0]) + " " + InputBindings.Display(InputBindings.Defaults[2]) + " "
                         + InputBindings.Display(InputBindings.Defaults[1]) + " " + InputBindings.Display(InputBindings.Defaults[3]);
                    AddRow(scroll, "Move", kb, InputBindings.ActionLabel("Move", true), false);
                    i += 3; continue;
                }
                kb = InputBindings.Display(d);
                pad = InputBindings.ActionLabel(d.Action, true);
                AddRow(scroll, d.Label, kb, pad, false);
            }
            AddRow(scroll, "Look", "Mouse", "Right Stick", false);
            AddRow(scroll, "Pause / back", "Esc", "Start / B", false);
            AddRow(scroll, "Skip Grandpa's tape", "Backspace", "View", false);
            card.Add(paper);
            var note = new Label("Rebind keys in Settings > Controls."); note.AddToClassList("small-text"); note.AddToClassList("on-dark");
            card.Add(note);
            _back = new Button(() => Closed?.Invoke()) { text = "Back", name = "btn-help-back" };
            _back.AddToClassList("hp-btn"); _back.AddToClassList("hp-btn--primary"); _back.AddToClassList("hp-btn--small"); _back.AddToClassList("hp-btn--center");
            card.Add(_back);
            host.Add(card);
        }

        public void FocusFirst() => UiFocus.FocusLater(_back);

        static void AddRow(VisualElement parent, string a, string kb, string pad, bool head)
        {
            var row = new VisualElement(); row.AddToClassList("help-row"); if (head) row.AddToClassList("help-head");
            var la = new Label(a); la.AddToClassList("help-cell"); la.AddToClassList("help-cell--action"); la.AddToClassList("bold");
            row.Add(la);
            foreach (var t in new[] { kb, pad })
            {
                var cell = new VisualElement(); cell.AddToClassList("help-cell");
                var l = new Label(t); if (!head) l.AddToClassList("keycap"); else l.AddToClassList("bold");
                cell.Add(l); row.Add(cell);
            }
            parent.Add(row);
        }
    }
}
