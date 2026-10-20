using HarvestPanic.Core;
using UnityEngine;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;

namespace HarvestPanic.Economy
{
    /// <summary>
    /// Shop chalkboard (farm-upgrades §3, hud-menus), Sunday Collateral and the run-over screen. Same paper-ledger
    /// skin as the receipt. Presentation only: buttons call director proposals; the server validates everything.
    /// </summary>
    public sealed partial class EconomyHud
    {
        bool _shopShown, _modalHeld;
        VisualElement _shopList;
        Label _shopTimer, _shopWallet, _shopBark;
        int _shopBalanceShown = int.MinValue;
        string _shopKey;
        CursorLockMode _prevLock;
        bool _prevVisible;

        /// <summary>True while the shop/collateral panel is on screen (tests).</summary>
        public bool ShopVisible => _shopShown && _overlay != null && _overlay.style.display == DisplayStyle.Flex;

        void OnShopChanged() { _shopKey = null; }
        void OnShopBark(string text)
        {
            if (ShopVisible && _shopBark != null) { _shopBark.text = text; Bounce(_shopBark); return; } // the paper covers toasts
            Toast(text, "hp-toast-shop");
        }

        /// <summary>Builds the shop (or Collateral) panel.</summary>
        public void ShowShop()
        {
            if (_dir == null || _overlay == null) return;
            _shopShown = true;
            _shopKey = null;
            _overlay.Clear();
            _overlay.style.display = DisplayStyle.Flex;
            bool collateral = _dir.Phase == DayPhase.Collateral;
            var paper = Add(_overlay, "hp-receipt", "hp-shop");
            Text(paper, collateral ? "COLLATERAL" : "GRANDPA'S CHALKBOARD", "hp-r-title", true);
            Text(paper, collateral ? "Sell upgrades back at 50% before the Landlord's abacus stops."
                                   : $"Week {_dir.Week} · {DayNames[Mathf.Clamp(_dir.DayOfWeek - 1, 0, 4)]} evening · upgrades last the whole run", "hp-r-sub");
            var head = Add(paper, "hp-r-row");
            _shopWallet = Text(head, "", "hp-shop-wallet", true);
            _shopTimer = Text(head, "", "hp-shop-timer", true);
            if (collateral && _dir.LastReceipt != null) Text(paper, _dir.LastReceipt.SundayLine, "hp-r-headline");
            Rule(paper);
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexShrink = 1;
            paper.Add(scroll);
            _shopList = scroll.contentContainer;
            _shopBark = Text(paper, "", "hp-shop-bark");
            Dash(paper);
            var row = Add(paper, "hp-r-buttons");
            if (_dir.LocalIsHost)
            {
                if (collateral)
                {
                    var give = new Button(() => _dir?.GiveUp()) { text = "Give up the farm" };
                    give.AddToClassList("hp-btn"); give.AddToClassList("hp-btn-secondary"); Font(give); row.Add(give);
                }
                else
                {
                    var done = new Button(() => _dir?.NextDay()) { text = DayNames.Length > 0 && _dir.DayOfWeek >= _dir.Tuning.DaysPerWeek ? "Face the Landlord" : "Done · Next Day" };
                    done.name = "hp-shop-done";
                    done.AddToClassList("hp-btn"); Font(done); row.Add(done);
                }
            }
            else Text(paper, collateral ? "Propose sales; the host signs them." : "Propose an upgrade; the host signs the ledger.", "hp-wait");
            RebuildShopList();
        }

        void RefreshShop()
        {
            if (!_shopShown || _dir == null) return;
            if (!_dir.IsShopOpen) return;
            int bal = _dir.Balance;
            string key = string.Join(",", _dir.OwnedUpgrades) + "|" + string.Join(",", _dir.ShopOffers) + "|" + _dir.ShopQueue.Count;
            if (bal != _shopBalanceShown || key != _shopKey) RebuildShopList();
            if (_shopTimer != null)
            {
                float rem = _dir.PhaseRemaining;
                _shopTimer.text = _dir.PhaseDuration > 0f ? $"{(int)rem / 60}:{(int)rem % 60:00}" : $"Rent {ReceiptBuilder.Money(_dir.Quota + _dir.Iou)} on Sunday";
                _shopTimer.EnableInClassList("hp-clock-urgent", _dir.Phase == DayPhase.Collateral && rem < 15f);
            }
        }

        void RebuildShopList()
        {
            if (_shopList == null || _dir == null) return;
            _shopBalanceShown = _dir.Balance;
            _shopKey = string.Join(",", _dir.OwnedUpgrades) + "|" + string.Join(",", _dir.ShopOffers) + "|" + _dir.ShopQueue.Count;
            _shopList.Clear();
            int bal = _dir.Balance;
            bool collateral = _dir.Phase == DayPhase.Collateral;
            int due = _dir.Quota + _dir.Iou;
            _shopWallet.text = collateral ? $"Wallet {ReceiptBuilder.Money(bal)} / due {ReceiptBuilder.Money(due)}" : $"Wallet {ReceiptBuilder.Money(bal)}";
            _shopWallet.EnableInClassList("hp-r-loss", bal < 0 || collateral);

            var q = _dir.ShopQueue;
            if (q.Count > 0 && _dir.Shop.TryGet(q[0].UpgradeId, out var pd))
            {
                var box = Add(_shopList, "hp-shop-proposal");
                Text(box, $"{_dir.PlayerName(q[0].Proposer)} proposes: {(q[0].Sell ? "SELL" : "BUY")} {pd.DisplayName} ({(q[0].Sell ? "+" + ReceiptBuilder.Money(_dir.RefundFor(pd.UpgradeId)) : ReceiptBuilder.Money(pd.Price))})", "hp-r-text", true);
                if (q.Count > 1) Text(box, $"+{q.Count - 1} more ideas waiting", "hp-r-more");
                if (_dir.LocalIsHost)
                {
                    var r = Add(box, "hp-r-buttons");
                    var ok = new Button(() => _dir?.ConfirmPending()) { text = "Sign it" }; ok.AddToClassList("hp-btn"); Font(ok); r.Add(ok);
                    var no = new Button(() => _dir?.RejectPending()) { text = "Veto" }; no.AddToClassList("hp-btn"); no.AddToClassList("hp-btn-secondary"); Font(no); r.Add(no);
                }
            }

            if (!collateral)
            {
                Text(_shopList, "FOR SALE TONIGHT", "hp-r-section", true);
                int shown = 0;
                foreach (var id in _dir.ShopOffers)
                {
                    if (!_dir.Shop.TryGet(id, out var d)) continue;
                    shown++;
                    bool afford = bal >= 0 && bal >= d.Price;
                    var row = Add(_shopList, "hp-shop-row");
                    var info = Add(row, "hp-shop-info");
                    Text(info, d.DisplayName, "hp-shop-name", true);
                    Text(info, d.Description, "hp-shop-desc");
                    var price = Text(row, ReceiptBuilder.Money(d.Price), "hp-shop-price", true);
                    if (!afford) price.AddToClassList("hp-r-loss");
                    string uid = d.UpgradeId;
                    var b = new Button(() => _dir?.ProposeBuy(uid)) { text = _dir.LocalIsHost ? "Buy" : "Propose" };
                    b.AddToClassList("hp-btn"); b.AddToClassList("hp-btn-small"); Font(b);
                    b.SetEnabled(afford);
                    row.Add(b);
                }
                if (shown == 0) Text(_shopList, "Grandpa's IOU (not for sale). That's everything.", "hp-r-more");
                if (bal < 0) Text(_shopList, "We're in the red: the shop is locked until the wallet is above $0.", "hp-r-more");
            }

            Text(_shopList, "OWNED (sell back 50%)", "hp-r-section", true);
            if (_dir.OwnedUpgrades.Count == 0) Text(_shopList, "Nothing yet. Just vibes and a pig.", "hp-r-more");
            foreach (var id in _dir.OwnedUpgrades)
            {
                if (!_dir.Shop.TryGet(id, out var d)) continue;
                var row = Add(_shopList, "hp-shop-row", "hp-shop-owned");
                var info = Add(row, "hp-shop-info");
                Text(info, "✔ " + d.DisplayName, "hp-shop-name", true);
                Text(info, d.Description, "hp-shop-desc");
                Text(row, "+" + ReceiptBuilder.Money(_dir.RefundFor(id)), "hp-shop-price", true).AddToClassList("hp-r-gain");
                string uid = d.UpgradeId;
                var b = new Button(() => _dir?.ProposeSell(uid)) { text = "Sell" };
                b.AddToClassList("hp-btn"); b.AddToClassList("hp-btn-secondary"); b.AddToClassList("hp-btn-small"); Font(b);
                b.SetEnabled(_dir.Shop.CanSell(uid) == ShopResult.Ok || !_dir.IsEconomyAuthority);
                row.Add(b);
            }
        }

        /// <summary>Run-over block appended to the EVICTED Sunday receipt.</summary>
        void AddRunOver(VisualElement body, ReceiptData r)
        {
            Rule(body);
            Text(body, "RUN OVER", "hp-r-section", true);
            Row(body, "Sundays paid", r.WeeksPaid.ToString(), null);
            Row(body, "Days farmed", r.DaysPlayed.ToString(), null);
            Row(body, "Money earned", ReceiptBuilder.Money(r.TotalEarned), "hp-r-gain");
            Row(body, "Spent on upgrades", ReceiptBuilder.Money(r.UpgradesBought), null);
            Text(body, "The farm save is gone. Your hats remember.", "hp-r-more");
        }

        void AddRunOverButtons(VisualElement row)
        {
            var lobby = new Button(() => _dir?.ReturnToLobby()) { text = "Back to Lobby" };
            lobby.AddToClassList("hp-btn"); lobby.AddToClassList("hp-btn-secondary"); Font(lobby); row.Add(lobby);
        }

        // ---------------- input/cursor while a paper is up ----------------

        void UpdateModal()
        {
            bool want = _overlay != null && _overlay.style.display == DisplayStyle.Flex;
            if (want == _modalHeld) return;
            if (want)
            {
                _modalHeld = true;
                _prevLock = Cursor.lockState; _prevVisible = Cursor.visible;
                InputModal.Push(this);
                Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            }
            else ReleaseModal();
        }

        void ReleaseModal()
        {
            if (!_modalHeld) return;
            _modalHeld = false;
            InputModal.Pop(this);
            Cursor.lockState = _prevLock; Cursor.visible = _prevVisible;
        }
    }
}
