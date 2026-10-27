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
            var card = new VisualElement(); card.AddToClassList("paper-card"); card.AddToClassList("help-card");
            var tape = new VisualElement { pickingMode = PickingMode.Ignore }; tape.AddToClassList("masking-tape"); tape.AddToClassList("masking-tape--top"); card.Add(tape);
            var title = new Label(UiText.Get("help.title")) { name = "help-title" }; title.AddToClassList("display"); title.AddToClassList("settings-title");
            card.Add(title);
            var paper = new VisualElement(); paper.AddToClassList("paper-inset");
            var scroll = new ScrollView(ScrollViewMode.Vertical) { name = "help-scroll" }; scroll.AddToClassList("help-scroll");
            paper.Add(scroll);
            AddRow(scroll, UiText.Get("settings.controls.action"), UiText.Get("settings.controls.kbm"), UiText.Get("settings.controls.pad"), true);
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
                    AddRow(scroll, UiText.Get("help.move"), kb, InputBindings.ActionLabel("Move", true), false);
                    i += 3; continue;
                }
                kb = InputBindings.Display(d);
                pad = InputBindings.ActionLabel(d.Action, true);
                AddRow(scroll, d.Label, kb, pad, false);
            }
            AddRow(scroll, UiText.Get("help.look"), UiText.Get("help.look.kbm"), UiText.Get("help.look.pad"), false);
            AddRow(scroll, UiText.Get("help.pause"), UiText.Get("glyph.key.esc"), UiText.Get("help.pause.pad"), false);
            AddRow(scroll, UiText.Get("help.skiptape"), UiText.Get("help.skiptape.kbm"), UiText.Get("glyph.pad.view"), false);
            card.Add(paper);
            var note = new Label(UiText.Get("help.note")); note.AddToClassList("small-text"); note.AddToClassList("muted"); note.AddToClassList("help-note");
            card.Add(note);
            _back = new Button(() => Closed?.Invoke()) { text = UiText.Get("common.back"), name = "btn-help-back" };
            _back.AddToClassList("hp-btn"); _back.AddToClassList("hp-btn--primary"); _back.AddToClassList("hp-btn--small"); _back.AddToClassList("hp-btn--center"); _back.AddToClassList(UiKit.BackClass);
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
