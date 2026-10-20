using System.Collections.Generic;
using HarvestPanic.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace HarvestPanic.Economy
{
    /// <summary>
    /// Day HUD + payout/Sunday receipt (hud-menus.md; art-bible §7 paper-ledger skin). UI Toolkit, built in code,
    /// styled by EconomyHud.uss. Reads only <see cref="EconomyDirector"/> (same team); presentation only, never
    /// mutates game state except the host's Next Day / Restart buttons.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class EconomyHud : MonoBehaviour
    {
        [SerializeField] StyleSheet styleSheet;
        [SerializeField] Font headlineFont;
        [SerializeField] Font bodyFont;
        [SerializeField, Tooltip("Seconds a toast stays (hud-menus ToastDuration).")] float toastSeconds = 2.5f;
        [SerializeField] int toastSeverityMin = 40;

        static readonly string[] DayNames = { "MON", "TUE", "WED", "THU", "FRI" };

        UIDocument _doc;
        VisualElement _root, _toasts, _overlay, _clock, _barFill, _orderLines;
        Label _money, _iou, _dayLabel, _quotaLabel, _clockPhase, _clockTime, _clockSub, _orderTitle, _orderBonus, _twist;
        EconomyDirector _dir;
        readonly List<(VisualElement el, float until)> _activeToasts = new List<(VisualElement, float)>();
        float _lastDisasterToast = -10f;
        int _shownMoney = int.MinValue;
        string _orderKey;

        void OnEnable()
        {
            _doc = GetComponent<UIDocument>();
            Build();
        }

        void OnDisable() => Unbind();

        void Build()
        {
            var root = _doc.rootVisualElement;
            root.Clear();
            if (styleSheet != null) root.styleSheets.Add(styleSheet);
            root.pickingMode = PickingMode.Ignore;
            if (bodyFont != null) root.style.unityFontDefinition = FontDefinition.FromFont(bodyFont);

            _root = Add(root, "hp-root");
            _root.pickingMode = PickingMode.Ignore;

            var wallet = Add(_root, "hp-card", "hp-shadow", "hp-wallet");
            _dayLabel = Text(wallet, "WEEK 1 · MON", "hp-daylabel");
            var row = Add(wallet, "hp-wallet-row");
            _money = Text(row, "$0", "hp-money", true);
            _iou = Text(row, "IOU", "hp-iou", true);
            _quotaLabel = Text(wallet, "", "hp-quota-label");
            var bar = Add(wallet, "hp-bar");
            _barFill = Add(bar, "hp-bar-fill");

            _clock = Add(_root, "hp-clock");
            _clockPhase = Text(_clock, "MORNING", "hp-clock-phase", true);
            _clockTime = Text(_clock, "0:30", "hp-clock-time", true);
            _clockSub = Text(_clock, "", "hp-clock-sub");

            var order = Add(_root, "hp-card", "hp-shadow", "hp-order");
            Add(order, "hp-clip");
            _orderTitle = Text(order, "MORNING ORDER", "hp-order-title", true);
            _orderLines = Add(order);
            _orderBonus = Text(order, "", "hp-order-bonus");
            _twist = Text(order, "", "hp-twist");

            _toasts = Add(_root, "hp-toasts");
            _toasts.pickingMode = PickingMode.Ignore;
            _overlay = Add(_root, "hp-dim");
            _overlay.style.display = DisplayStyle.None;
            foreach (var ve in _root.Query<VisualElement>().ToList()) if (ve != _overlay && !(ve is Button)) ve.pickingMode = PickingMode.Ignore;
        }

        VisualElement Add(VisualElement parent, params string[] classes)
        {
            var v = new VisualElement();
            foreach (var c in classes) v.AddToClassList(c);
            parent.Add(v);
            return v;
        }

        Label Text(VisualElement parent, string text, string cls, bool headline = false)
        {
            var l = new Label(text);
            l.AddToClassList(cls);
            if (headline && headlineFont != null) l.style.unityFontDefinition = FontDefinition.FromFont(headlineFont);
            parent.Add(l);
            return l;
        }

        // ---------------- binding ----------------

        void Bind(EconomyDirector d)
        {
            if (_dir == d) return;
            Unbind();
            _dir = d;
            if (_dir == null) return;
            _dir.OnWalletTxn += OnTxn;
            _dir.OnDisaster += OnDisaster;
            _dir.OnReceipt += ShowReceipt;
            _dir.OnPhaseChanged += OnPhase;
            if (_dir.LastReceipt != null && (_dir.Phase == DayPhase.Payout || _dir.Phase == DayPhase.Sunday || _dir.Phase == DayPhase.Evicted)) ShowReceipt(_dir.LastReceipt);
        }

        void Unbind()
        {
            if (_dir == null) return;
            _dir.OnWalletTxn -= OnTxn;
            _dir.OnDisaster -= OnDisaster;
            _dir.OnReceipt -= ShowReceipt;
            _dir.OnPhaseChanged -= OnPhase;
            _dir = null;
        }

        void OnPhase(DayPhase from, DayPhase to)
        {
            if (to == DayPhase.Morning || to == DayPhase.Workday) _overlay.style.display = DisplayStyle.None;
        }

        void Update()
        {
            if (_root == null) return;
            Bind(EconomyDirector.Instance);
            for (int i = _activeToasts.Count - 1; i >= 0; i--)
                if (Time.unscaledTime > _activeToasts[i].until) { _activeToasts[i].el.RemoveFromHierarchy(); _activeToasts.RemoveAt(i); }
            if (_dir == null) { _root.style.display = DisplayStyle.None; return; }
            _root.style.display = DisplayStyle.Flex;
            Refresh();
        }

        void Refresh()
        {
            var d = _dir;
            int bal = d.Balance;
            if (bal != _shownMoney)
            {
                if (_shownMoney != int.MinValue) Bounce(_money);
                _shownMoney = bal;
                _money.text = ReceiptBuilder.Money(bal);
                _money.EnableInClassList("hp-money-neg", bal < 0);
            }
            _iou.style.display = d.Iou > 0 || bal < 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _iou.text = d.Iou > 0 ? "IOU " + ReceiptBuilder.Money(d.Iou) : "IOU";
            _dayLabel.text = $"WEEK {d.Week} · {DayNames[Mathf.Clamp(d.DayOfWeek - 1, 0, 4)]}";
            int due = d.Quota + d.Iou;
            int dayShare = Mathf.RoundToInt(d.Quota / 5f);
            _quotaLabel.text = $"Rent {ReceiptBuilder.Money(due)} Sunday · today's share {ReceiptBuilder.Money(dayShare)}";
            float frac = due > 0 ? Mathf.Clamp01(bal / (float)due) : 0f;
            _barFill.style.width = Length.Percent(frac * 100f);
            _barFill.EnableInClassList("hp-bar-fill-done", frac >= 1f);

            var phase = d.Phase;
            float rem = d.PhaseRemaining;
            _clockPhase.text = PhaseName(phase);
            bool timed = d.PhaseDuration > 0f && (phase == DayPhase.Morning || phase == DayPhase.Workday || phase == DayPhase.Dusk);
            _clockTime.text = timed ? $"{(int)rem / 60}:{(int)rem % 60:00}" : "--:--";
            _clockTime.EnableInClassList("hp-clock-urgent", timed && phase != DayPhase.Morning && rem < 60f);
            _clock.EnableInClassList("hp-clock-dusk", phase == DayPhase.Dusk);
            _clockSub.text = phase == DayPhase.Workday && rem < 30f ? "SUNDOWN!" : phase == DayPhase.Dusk ? "night market ×1.25" : phase == DayPhase.Morning ? "market opens soon" : "";

            var order = d.Order;
            string key = "";
            foreach (var l in order) key += l.GoodsId + l.Filled + "/" + l.Quantity + ";";
            if (key != _orderKey)
            {
                _orderKey = key;
                _orderLines.Clear();
                bool all = order.Count > 0;
                foreach (var l in order)
                {
                    var row = Add(_orderLines, "hp-order-line");
                    var txt = Text(row, (l.IsComplete ? "✔ " : "☐ ") + LedgerText.OrderLabel(l), "hp-order-text");
                    var cnt = Text(row, $"{Mathf.Min(l.Filled, l.Quantity)}/{l.Quantity}", "hp-order-count", true);
                    if (l.IsComplete) { txt.AddToClassList("hp-order-done"); cnt.AddToClassList("hp-order-tick"); }
                    all &= l.IsComplete;
                }
                _orderBonus.text = all ? "ORDER COMPLETE! bonus at payout" : $"All lines: +{ReceiptBuilder.Money(60 * d.Week)} · items ×1.5";
            }
            string twist = "";
            foreach (var m in d.ActiveModifiers) if (m.Announced && !string.IsNullOrEmpty(m.CardText)) twist += (twist.Length > 0 ? "\n" : "") + m.CardText;
            _twist.text = twist;
            _twist.style.display = twist.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        static string PhaseName(DayPhase p)
        {
            switch (p)
            {
                case DayPhase.Morning: return "MORNING";
                case DayPhase.Workday: return "WORKDAY";
                case DayPhase.Dusk: return "DUSK";
                case DayPhase.Payout: return "PAYOUT";
                case DayPhase.Sunday: return "SUNDAY";
                case DayPhase.Evicted: return "EVICTED";
                default: return "";
            }
        }

        void Bounce(VisualElement v)
        {
            v.style.scale = new Scale(new Vector2(1.25f, 1.25f));
            v.schedule.Execute(() => v.style.scale = new Scale(Vector2.one)).StartingIn(200);
        }

        // ---------------- toasts ----------------

        void OnTxn(WalletTxn t)
        {
            if (t.Type == WalletTxnType.Quota) return;
            string what = t.Type == WalletTxnType.Sale ? (t.Amount <= 1 && t.Amount > 0 ? $"{LedgerText.ItemName(t.GoodsId, null)} (compost)" : $"sold {LedgerText.ItemName(t.GoodsId, null)}") :
                          t.Type == WalletTxnType.OrderBonus ? "order complete" : t.Type.ToString().ToLowerInvariant();
            Toast((t.Amount >= 0 ? "▲ " : "▼ ") + ReceiptBuilder.SignedMoney(t.Amount) + "  " + what, t.Amount >= 0 ? "hp-toast-gain" : "hp-toast-loss");
        }

        void OnDisaster(BlameEvent e)
        {
            if (e.Severity < toastSeverityMin && e.Verb != BlameVerb.DroppedInRiver) return;
            if (Time.unscaledTime - _lastDisasterToast < 1f) return;
            _lastDisasterToast = Time.unscaledTime;
            string who = _dir != null && !string.IsNullOrEmpty(_dir.LastDisasterName) ? _dir.LastDisasterName : "Someone";
            Toast($"{ReceiptBuilder.Money(e.DollarDelta)}  {who}: {LedgerText.Line(e.Verb, LedgerText.ItemName(e.ItemId, e.ItemTag), 1)}", "hp-toast-disaster");
        }

        void Toast(string text, string cls)
        {
            while (_activeToasts.Count >= 3) { _activeToasts[0].el.RemoveFromHierarchy(); _activeToasts.RemoveAt(0); }
            var l = new Label(text);
            l.AddToClassList("hp-toast"); l.AddToClassList(cls);
            if (headlineFont != null) l.style.unityFontDefinition = FontDefinition.FromFont(headlineFont);
            l.pickingMode = PickingMode.Ignore;
            _toasts.Add(l);
            _activeToasts.Add((l, Time.unscaledTime + toastSeconds));
        }

        // ---------------- receipt ----------------

        /// <summary>Shows the payout or Sunday receipt.</summary>
        public void ShowReceipt(ReceiptData r)
        {
            _overlay.Clear();
            _overlay.style.display = DisplayStyle.Flex;
            var paper = Add(_overlay, "hp-receipt");
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexShrink = 1;
            paper.Add(scroll);
            var body = scroll.contentContainer;

            if (r.IsSunday)
            {
                Text(body, "LANDLORD SUNDAY", "hp-r-title", true);
                Text(body, $"Week {r.Week} · rent due", "hp-r-sub");
                var stamp = Text(body, r.Paid ? "PAID" : "EVICTED", "hp-r-stamp", true);
                stamp.AddToClassList(r.Paid ? "hp-stamp-paid" : "hp-stamp-evicted");
                Rule(body);
                Row(body, "Weekly quota", ReceiptBuilder.Money(r.Quota), null);
                if (r.Iou > 0) Row(body, "Landlord IOU (overdraft)", ReceiptBuilder.Money(r.Iou), "hp-r-loss");
                Row(body, "Total due", ReceiptBuilder.Money(r.Due), null, true);
                Row(body, r.Paid ? "Left in the wallet" : "Wallet", ReceiptBuilder.Money(r.Balance), r.Paid ? "hp-r-gain" : "hp-r-loss", true);
                Dash(body);
                Text(body, "MOST VALUABLE DISASTER: " + r.Mvd, "hp-r-section", true);
                Text(body, r.SundayLine, "hp-r-headline");
            }
            else
            {
                Text(body, "DAILY RECEIPT", "hp-r-title", true);
                Text(body, $"Week {r.Week} · {DayNames[Mathf.Clamp(r.DayOfWeek - 1, 0, 4)]} · Grandpa's Farm Ledger", "hp-r-sub");
                Text(body, r.Headline, "hp-r-headline");
                Rule(body);
                Row(body, $"Market sales ({r.UnitsSold} items)", ReceiptBuilder.Money(r.Sales), r.Sales > 0 ? "hp-r-gain" : null);
                Row(body, r.OrderComplete ? "Morning order complete!" : "Morning order (incomplete)", ReceiptBuilder.SignedMoney(r.OrderBonus), r.OrderBonus > 0 ? "hp-r-gain" : null);
                foreach (var o in r.Money) Row(body, "   " + (o.Count >= o.Dollars ? "✔ " : "☐ ") + o.Text, $"{Mathf.Min(o.Count, o.Dollars)}/{o.Dollars}", null);
                Row(body, "Fees, fines & repairs", ReceiptBuilder.Money(r.Costs), r.Costs < 0 ? "hp-r-loss" : null);
                Dash(body);
                Row(body, "TODAY'S NET", ReceiptBuilder.SignedMoney(r.Net), r.Net >= 0 ? "hp-r-gain" : "hp-r-loss", true);
                Row(body, "Wallet", ReceiptBuilder.Money(r.Balance), r.Balance < 0 ? "hp-r-loss" : null, true);
                Row(body, "Rent due Sunday", ReceiptBuilder.Money(r.Quota + r.Iou), null);
                Rule(body);
                Text(body, "BLAME LEDGER", "hp-r-section", true);
                if (r.Blocks.Count == 0) Text(body, LedgerText.Spotless, "hp-r-more");
                foreach (var b in r.Blocks)
                {
                    Row(body, b.Name, ReceiptBuilder.SignedMoney(b.Total), b.Total < 0 ? "hp-r-loss" : "hp-r-gain", false, "hp-r-player");
                    foreach (var l in b.Lines)
                    {
                        var row = Row(body, l.Text, ReceiptBuilder.SignedMoney(l.Dollars), l.Dollars < 0 ? "hp-r-loss" : "hp-r-gain");
                        row.AddToClassList("hp-r-line");
                    }
                    if (b.MoreLines > 0) Text(body, string.Format(LedgerText.MoreLines, b.MoreLines), "hp-r-more");
                }
            }
            Dash(paper);
            AddButtons(paper, r);
        }

        void AddButtons(VisualElement paper, ReceiptData r)
        {
            var row = Add(paper, "hp-r-buttons");
            bool host = _dir == null || _dir.LocalIsHost;
            if (host)
            {
                bool evicted = r.IsSunday && !r.Paid;
                if (!evicted)
                {
                    var next = new Button(() => _dir?.NextDay()) { text = r.IsSunday ? "Next Week" : "Next Day" };
                    next.AddToClassList("hp-btn"); Font(next); row.Add(next);
                    next.schedule.Execute(() => next.Focus()).StartingIn(50);
                }
                var restart = new Button(() => _dir?.Restart()) { text = evicted ? "Try Again" : "Restart" };
                restart.AddToClassList("hp-btn"); if (!evicted) restart.AddToClassList("hp-btn-secondary");
                Font(restart); row.Add(restart);
            }
            else Text(paper, "Waiting for the host to sign the ledger…", "hp-wait");
        }

        void Font(VisualElement v) { if (headlineFont != null) v.style.unityFontDefinition = FontDefinition.FromFont(headlineFont); }

        VisualElement Row(VisualElement parent, string left, string right, string amtCls, bool total = false, string leftCls = null)
        {
            var row = Add(parent, "hp-r-row");
            var l = Text(row, left, leftCls ?? "hp-r-text", total || leftCls != null);
            var r = Text(row, right, "hp-r-amt", true);
            if (amtCls != null) r.AddToClassList(amtCls);
            if (total) { l.AddToClassList("hp-r-total"); r.AddToClassList("hp-r-total"); }
            return row;
        }

        void Rule(VisualElement p) => Add(p, "hp-r-rule");
        void Dash(VisualElement p) => Add(p, "hp-r-dash");
    }
}
