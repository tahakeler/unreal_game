using System;
using System.Collections.Generic;
using UnityEngine.UIElements;
using Direction = UnityEngine.UIElements.NavigationMoveEvent.Direction;

namespace HarvestPanic.UI
{
    public enum DialogTone { Info, Error, Busy }

    /// <summary>
    /// Modal paper-card dialog used by every frontend screen (messages, session errors, connecting progress, confirms).
    /// While open it disables the elements it blocks (so gamepad/keyboard focus cannot reach them), traps focus on its own
    /// buttons and restores the previous focus when it closes. Esc / B (<see cref="Back"/>) runs the secondary action or closes.
    /// </summary>
    public sealed class UiDialog
    {
        readonly VisualElement _layer, _card, _buttons, _badgeHolder;
        readonly Label _title, _body, _badge;
        readonly WagonWheel _wheel;
        readonly List<VisualElement> _blocked = new List<VisualElement>();
        Action _primary, _secondary;
        Focusable _restore;

        public Button Primary { get; }
        public Button Secondary { get; }
        public bool IsOpen { get; private set; }
        public DialogTone Tone { get; private set; }
        public string TitleText => _title.text;
        public string BodyText => _body.text;
        public SessionErrorKind? ErrorKind { get; private set; }

        /// <summary>Raised after the dialog closes (any button, Back, or <see cref="Close"/>).</summary>
        public event Action Closed;

        /// <param name="layer">Full-screen host element (class <c>dialog-layer</c>); filled here.</param>
        /// <param name="blockWhileOpen">Elements disabled while the dialog is open (screens, footer).</param>
        public UiDialog(VisualElement layer, params VisualElement[] blockWhileOpen)
        {
            _layer = layer;
            _layer.Clear();
            _layer.AddToClassList("dialog-layer");
            _blocked.AddRange(blockWhileOpen);
            _card = new VisualElement { name = "dialog-card" }; _card.AddToClassList("paper-card"); _card.AddToClassList("dialog-card");
            var tape = new VisualElement(); tape.AddToClassList("masking-tape"); tape.AddToClassList("masking-tape--top");
            _card.Add(tape);
            var head = new VisualElement(); head.AddToClassList("row"); head.AddToClassList("dialog-head");
            _badgeHolder = new VisualElement(); _badgeHolder.AddToClassList("dialog-badge-holder");
            _badge = new Label("!") { name = "dialog-badge" }; _badge.AddToClassList("badge");
            _wheel = new WagonWheel(64f) { name = "dialog-wheel" };
            _badgeHolder.Add(_badge); _badgeHolder.Add(_wheel);
            _title = new Label { name = "dialog-title" }; _title.AddToClassList("display"); _title.AddToClassList("dialog-title");
            head.Add(_badgeHolder); head.Add(_title);
            _card.Add(head);
            _body = new Label { name = "dialog-body" }; _body.AddToClassList("body-text"); _body.AddToClassList("dialog-body");
            _card.Add(_body);
            _buttons = new VisualElement(); _buttons.AddToClassList("row"); _buttons.AddToClassList("dialog-buttons");
            Primary = UiKit.Button("@common.ok", () => Run(_primary), "hp-btn--primary", "hp-btn--small", "hp-btn--center");
            Primary.name = "btn-dialog-primary";
            Secondary = UiKit.Button("@common.cancel", () => Run(_secondary), "hp-btn--small", "hp-btn--center", UiKit.BackClass);
            Secondary.name = "btn-dialog-secondary";
            _buttons.Add(Primary); _buttons.Add(Secondary);
            _card.Add(_buttons);
            _layer.Add(_card);
            UiNav.Row(new VisualElement[] { Primary, Secondary });
            UiNav.Trap(_card);
            _layer.style.display = DisplayStyle.None;
        }

        /// <summary>Message with up to two buttons. <paramref name="secondaryKey"/> null = single button.</summary>
        public void Show(string title, string body, DialogTone tone = DialogTone.Info, string badge = "!",
            string primaryKey = "@common.ok", Action primary = null, string secondaryKey = null, Action secondary = null)
        {
            ErrorKind = null;
            Open(title, body, tone, badge);
            Configure(Primary, primaryKey, primary, ref _primary);
            Configure(Secondary, secondaryKey, secondary, ref _secondary);
            FocusDefault();
            if (tone == DialogTone.Error) UiSounds.Error();
        }

        /// <summary>A classified session error; Retry appears when it can help and <paramref name="retry"/> is given.</summary>
        public void ShowError(SessionError e, Action retry = null, Action dismissed = null)
        {
            bool canRetry = e.CanRetry && retry != null;
            Show(e.Title, e.Body, DialogTone.Error, e.Badge,
                canRetry ? "@common.retry" : "@common.ok", canRetry ? retry : dismissed,
                canRetry ? "@common.close" : null, canRetry ? dismissed : null);
            ErrorKind = e.Kind;
        }

        /// <summary>Busy state with a rolling wheel and a Cancel button.</summary>
        public void ShowProgress(string title, string body, Action cancel, string cancelKey = "@common.cancel")
        {
            ErrorKind = null;
            Open(title, body, DialogTone.Busy, "");
            Configure(Primary, cancelKey, cancel, ref _primary);
            Configure(Secondary, null, null, ref _secondary);
            Primary.AddToClassList(UiKit.BackClass);
            FocusDefault();
        }

        public void SetBody(string body) => _body.text = body ?? "";

        /// <summary>Esc / B: runs the secondary action if any, else the primary of a single-button dialog. False when closed.</summary>
        public bool Back()
        {
            if (!IsOpen) return false;
            if (Secondary.style.display != DisplayStyle.None) Run(_secondary);
            else Run(_primary);
            return true;
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            _layer.RemoveFromClassList("is-active");
            _layer.style.display = DisplayStyle.None;
            foreach (var b in _blocked) b?.SetEnabled(true);
            var restore = _restore; _restore = null;
            if (restore is VisualElement ve && UiNav.CanFocus(ve)) UiFocus.FocusLater(ve);
            Closed?.Invoke();
        }

        void Open(string title, string body, DialogTone tone, string badge)
        {
            if (!IsOpen)
            {
                _restore = _layer.panel?.focusController?.focusedElement;
                foreach (var b in _blocked) b?.SetEnabled(false);
            }
            IsOpen = true;
            Tone = tone;
            _title.text = title ?? "";
            _body.text = body ?? "";
            _body.style.display = string.IsNullOrEmpty(body) ? DisplayStyle.None : DisplayStyle.Flex;
            _badge.text = badge ?? "";
            _badge.style.display = tone == DialogTone.Busy || string.IsNullOrEmpty(badge) ? DisplayStyle.None : DisplayStyle.Flex;
            _wheel.style.display = tone == DialogTone.Busy ? DisplayStyle.Flex : DisplayStyle.None;
            _card.EnableInClassList("dialog-card--error", tone == DialogTone.Error);
            _card.EnableInClassList("dialog-card--busy", tone == DialogTone.Busy);
            Primary.RemoveFromClassList(UiKit.BackClass);
            _layer.style.display = DisplayStyle.Flex;
            UiKit.ShowScreen(_layer);
        }

        static void Configure(Button b, string key, Action action, ref Action slot)
        {
            slot = action;
            b.style.display = key == null ? DisplayStyle.None : DisplayStyle.Flex;
            if (key != null) b.text = UiKit.T(key);
        }

        void FocusDefault() => UiFocus.FocusLater(Primary);

        void Run(Action a)
        {
            Close();
            a?.Invoke();
        }
    }
}
