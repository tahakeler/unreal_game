using System;
using System.Collections.Generic;
using System.Text;
using HarvestPanic.Core;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace HarvestPanic.Economy
{
    /// <summary>
    /// Shop phase, farm upgrades, Sunday Collateral, eviction and the host Run Save (farm-upgrades, week-quota,
    /// save-persistence). Server-authoritative: clients only send proposals; the host confirms. Every purchase is
    /// re-validated on the server (phase, offer, prereq, balance ≥ 0 and ≥ price).
    /// </summary>
    public sealed partial class EconomyDirector
    {
        readonly NetworkVariable<FixedString4096Bytes> _nvShop = new NetworkVariable<FixedString4096Bytes>();

        FarmShop _shop;
        float _proposalAge;
        int _runEarned, _runUpgradeSpend, _runDaysPlayed, _runWeeksPaid, _runSeed;
        string _clientShopKey;
        readonly List<string> _clientOwned = new List<string>(), _clientOffers = new List<string>();
        readonly List<ShopProposal> _clientQueue = new List<ShopProposal>();

        /// <summary>Raised on every peer with a short shop bark ("The wallet says no.").</summary>
        public event Action<string> OnShopBark;
        /// <summary>Raised on every peer when owned/offers/proposals change.</summary>
        public event Action OnShopChanged;

        /// <summary>Shop rules (server copy; tests).</summary>
        public FarmShop Shop => _shop;
        public IReadOnlyList<string> OwnedUpgrades => ReadRemote ? (IReadOnlyList<string>)_clientOwned : _shop.Owned;
        public IReadOnlyList<string> ShopOffers => ReadRemote ? (IReadOnlyList<string>)_clientOffers : _shop.Offers;
        public IReadOnlyList<ShopProposal> ShopQueue => ReadRemote ? (IReadOnlyList<ShopProposal>)_clientQueue : _shop.Queue;
        public bool IsShopOpen => Phase == DayPhase.Shop || Phase == DayPhase.Collateral;
        public int RefundFor(string id) => FarmShop.Refund(PaidFor(id), Tuning.SellBackRate);
        int PaidFor(string id) => _shop.PaidFor(id);

        void ShopAwake()
        {
            _shop = new FarmShop(UpgradeCatalog.All.All) { SellBackRate = Tuning.SellBackRate, OffersPerShop = Tuning.OffersPerShop };
            _wallet.Applied += t => { if (t.Amount > 0 && (t.Type == WalletTxnType.Sale || t.Type == WalletTxnType.OrderBonus)) _runEarned += t.Amount; };
        }

        void ShopResetRun()
        {
            _shop.Reset();
            _runEarned = _runUpgradeSpend = _runDaysPlayed = _runWeeksPaid = 0;
            _runSeed = _rng.Next();
            PushShop();
        }

        void EnterShop()
        {
            _runDaysPlayed++;
            _shop.ClearQueue();
            _shop.RollOffers(_clock.Week, new System.Random(_runSeed ^ (_clock.DayIndex * 7919)));
            PushShop();
            SaveRun(RunResumePoint.Shop);
        }

        void ShopTick(float dt)
        {
            if (!_shop.HasPending) { _proposalAge = 0f; return; }
            if ((_proposalAge += dt) < Tuning.ProposalTimeoutSeconds) return;
            _proposalAge = 0f;
            _shop.TakePending(out var p);
            Bark($"{NameOf(p.Proposer, null)}'s idea expired. Nobody signed it.");
            PushShop();
        }

        // ---------------- public API (HUD buttons; any peer) ----------------

        /// <summary>Proposes buying <paramref name="upgradeId"/>. Host proposals are confirmed at once.</summary>
        public void ProposeBuy(string upgradeId) => Propose(upgradeId, false);
        /// <summary>Proposes selling back <paramref name="upgradeId"/> at the sell-back rate.</summary>
        public void ProposeSell(string upgradeId) => Propose(upgradeId, true);

        void Propose(string id, bool sell)
        {
            if (IsEconomyAuthority) { ServerPropose(LocalId, id, sell); return; }
            if (IsSpawned) ProposeRpc(id ?? "", sell);
        }

        /// <summary>Host: accept the head proposal.</summary>
        public void ConfirmPending() { if (IsEconomyAuthority) ServerResolvePending(true); else if (IsSpawned) ResolvePendingRpc(true); }
        /// <summary>Host: reject the head proposal.</summary>
        public void RejectPending() { if (IsEconomyAuthority) ServerResolvePending(false); else if (IsSpawned) ResolvePendingRpc(false); }
        /// <summary>Host: give up during Collateral (immediate eviction).</summary>
        public void GiveUp() { if (IsEconomyAuthority) { if (_clock.Phase == DayPhase.Collateral) Evict(); } else if (IsSpawned) GiveUpRpc(); }

        /// <summary>Host: back to the lobby scene after the run is over.</summary>
        public void ReturnToLobby()
        {
            if (!IsEconomyAuthority) { if (IsSpawned) LobbyRpc(); return; }
            const string lobby = "Lobby";
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.IsServer && nm.SceneManager != null) nm.SceneManager.LoadScene(lobby, UnityEngine.SceneManagement.LoadSceneMode.Single);
            else if (Application.CanStreamedLevelBeLoaded(lobby)) UnityEngine.SceneManagement.SceneManager.LoadScene(lobby);
        }

        ulong LocalId => IsSpawned && NetworkManager != null ? NetworkManager.LocalClientId : 0UL;

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        void ProposeRpc(string id, bool sell, RpcParams p = default) => ServerPropose(p.Receive.SenderClientId, id, sell);

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        void ResolvePendingRpc(bool accept, RpcParams p = default) { if (p.Receive.SenderClientId == NetworkManager.ServerClientId) ServerResolvePending(accept); }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        void GiveUpRpc(RpcParams p = default) { if (p.Receive.SenderClientId == NetworkManager.ServerClientId) GiveUp(); }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        void LobbyRpc(RpcParams p = default) { if (p.Receive.SenderClientId == NetworkManager.ServerClientId && Phase == DayPhase.Evicted) ReturnToLobby(); }

        [Rpc(SendTo.NotServer)]
        void BarkRpc(string text) => OnShopBark?.Invoke(text);

        void Bark(string text)
        {
            OnShopBark?.Invoke(text);
            if (IsSpawned) BarkRpc(text);
        }

        // ---------------- server ----------------

        bool IsHostId(ulong id) => !IsSpawned || id == NetworkManager.ServerClientId;

        void ServerPropose(ulong who, string id, bool sell)
        {
            if (!IsEconomyAuthority) return;
            var phase = _clock.Phase;
            if (phase != DayPhase.Shop && !(sell && phase == DayPhase.Collateral)) { Bark(FarmShop.Bark(ShopResult.NotInShop)); return; }
            var check = sell ? _shop.CanSell(id) : _shop.CanBuy(id, _wallet.Balance);
            if (check != ShopResult.Ok) { Bark(FarmShop.Bark(check)); return; }
            var prop = new ShopProposal { Proposer = who, Sell = sell, UpgradeId = id };
            if (IsHostId(who) || CountPlayers() <= 1) { Execute(prop); return; } // host / solo: instant
            var r = _shop.Propose(prop);
            if (r != ShopResult.Ok) { Bark(FarmShop.Bark(r)); return; }
            _shop.TryGet(id, out var d);
            Bark($"{NameOf(who, null)} wants to {(sell ? "sell" : "buy")} {d.DisplayName}. Host, sign it?");
            PushShop();
        }

        void ServerResolvePending(bool accept)
        {
            if (!IsEconomyAuthority || !_shop.TakePending(out var p)) return;
            _proposalAge = 0f;
            if (accept) Execute(p);
            else { _shop.TryGet(p.UpgradeId, out var d); Bark($"Host vetoed {NameOf(p.Proposer, null)}'s {d?.DisplayName}."); }
            PushShop();
        }

        /// <summary>Server: performs a validated buy/sell (re-checked here: state may have changed since proposing).</summary>
        bool Execute(ShopProposal p)
        {
            if (!_shop.TryGet(p.UpgradeId, out var d)) return false;
            var phase = _clock.Phase;
            if (phase != DayPhase.Shop && !(p.Sell && phase == DayPhase.Collateral)) { Bark(FarmShop.Bark(ShopResult.NotInShop)); return false; }
            if (!p.Sell)
            {
                var c = _shop.CanBuy(d.UpgradeId, _wallet.Balance);
                if (c != ShopResult.Ok) { Bark(FarmShop.Bark(c)); return false; }
                int before = _wallet.Balance;
                AddWalletTxn(new WalletTxn { Type = WalletTxnType.Upgrade, Amount = -d.Price, PlayerId = p.Proposer, GoodsId = d.UpgradeId });
                if (_wallet.Balance != before - d.Price) { Bark(FarmShop.Bark(ShopResult.NotEnoughMoney)); return false; }
                _shop.MarkBought(d.UpgradeId, d.Price);
                _runUpgradeSpend += d.Price;
                if (d.Modifier != null) _mods.Apply(d.Modifier, _clock.Week, bypassCap: true);
                Bark($"{NameOf(p.Proposer, null)} bought {d.DisplayName}! {d.FlavorLine}");
            }
            else
            {
                var c = _shop.CanSell(d.UpgradeId);
                if (c != ShopResult.Ok) { Bark(FarmShop.Bark(c)); return false; }
                int refund = _shop.RefundFor(d.UpgradeId);
                _shop.MarkSold(d.UpgradeId);
                if (d.Modifier != null) _mods.Remove(d.Modifier.ModifierId);
                if (refund > 0) AddWalletTxn(new WalletTxn { Type = WalletTxnType.SellBack, Amount = refund, PlayerId = p.Proposer, GoodsId = d.UpgradeId });
                Bark($"Sold {d.DisplayName} back for {ReceiptBuilder.Money(refund)}. Grandpa sighs.");
                if (phase == DayPhase.Collateral) TrySettleCollateral();
            }
            PushShop();
            if (_clock.Phase == DayPhase.Shop) SaveRun(RunResumePoint.Shop);
            return true;
        }

        void TrySettleCollateral()
        {
            int due = _quota + _wallet.IouThisWeek;
            if (_wallet.Balance < due) return;
            AddWalletTxn(new WalletTxn { Type = WalletTxnType.Quota, Amount = -due, PlayerId = PlayerIds.None });
            _runWeeksPaid++;
            Go(DayPhase.Sunday);
            var r = SundayReceipt(due, true);
            r.Carryover = _wallet.Balance;
            r.SundayLine = "PAID (with collateral). The Landlord pockets the receipts. " + LedgerText.Sunday(true, r.Carryover);
            PublishReceipt(r);
        }

        ReceiptData SundayReceipt(int due, bool paid)
        {
            var r = new ReceiptData { IsSunday = true, Week = _clock.Week, DayIndex = _clock.DayIndex, Quota = _quota, Iou = _wallet.IouThisWeek, Due = due, Paid = paid };
            ulong mvd = _ledger.MostValuableDisaster();
            r.Mvd = mvd == PlayerIds.None ? "The Weather" : NameOf(mvd, null);
            r.Balance = _wallet.Balance;
            return r;
        }

        void Evict()
        {
            int due = _quota + _wallet.IouThisWeek;
            var r = SundayReceipt(due, false);
            r.SundayLine = LedgerText.Sunday(false, 0);
            FinishEviction(r);
        }

        /// <summary>Run over: wipe the save (save-scum safe: done before the screen), stats, Evicted phase.</summary>
        void FinishEviction(ReceiptData r)
        {
            r.Paid = false;
            r.WeeksPaid = _runWeeksPaid; r.DaysPlayed = _runDaysPlayed; r.TotalEarned = _runEarned; r.UpgradesBought = _runUpgradeSpend;
            r.Balance = _wallet.Balance;
            RunSaveStore.Delete();
            _shop.ClearQueue();
            Go(DayPhase.Evicted);
            PublishReceipt(r);
            PushShop();
        }

        /// <summary>Called by ResolveSunday on a paid week.</summary>
        void CountPaidWeek() => _runWeeksPaid++;

        // ---------------- save / load ----------------

        RunSaveData BuildSave(RunResumePoint point)
        {
            var d = new RunSaveData
            {
                Resume = point, Week = _clock.Week, DayOfWeek = _clock.DayOfWeek, DayIndex = _clock.DayIndex,
                Balance = _wallet.Balance, Iou = _wallet.IouThisWeek, WeekIncome = _weekIncome, Quota = _quota, Players = _players,
                TotalEarned = _runEarned, TotalSpentUpgrades = _runUpgradeSpend, DaysPlayed = _runDaysPlayed, Seed = _runSeed,
            };
            foreach (var id in _shop.Owned) { d.Upgrades.Add(id); d.UpgradePaid.Add(_shop.PaidFor(id)); }
            foreach (var m in _landlordRules) d.LandlordRules.Add(m.ModifierId);
            d.Offers.AddRange(_shop.Offers);
            return d;
        }

        /// <summary>Host: writes the run save (Shop entry, each shop purchase, after a paid Sunday).</summary>
        public void SaveRun(RunResumePoint point)
        {
            if (!IsEconomyAuthority || !_started) return;
            RunSaveStore.Write(BuildSave(point));
        }

        bool TryContinueRun()
        {
            var svc = RunSaveService.Instance;
            if (!svc.ContinueRequested) return false;
            svc.ContinueRequested = false;
            var d = RunSaveStore.Load(out string err);
            if (d == null) { Debug.LogWarning("[Economy] Continue failed: " + err); return false; }
            ApplySave(d);
            return true;
        }

        /// <summary>Server: restores a run from save data and enters its resume phase.</summary>
        public void ApplySave(RunSaveData d)
        {
            if (!IsEconomyAuthority) return;
            _started = true;
            _wallet.Restore(d.Balance, d.Iou);
            _ledger.StartWeek();
            _mods.Clear();
            _landlordRules.Clear();
            foreach (var id in d.LandlordRules)
                if (Content.DayModifiers.TryGet(id, out var m)) { _landlordRules.Add(m); _mods.Apply(m, d.Week, bypassCap: true); }
            _shop.Reset();
            var paid = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < d.Upgrades.Count; i++) paid[d.Upgrades[i]] = i < d.UpgradePaid.Count ? d.UpgradePaid[i] : 0;
            _shop.SetOwned(d.Upgrades, paid);
            foreach (var id in _shop.Owned) if (_shop.TryGet(id, out var u) && u.Modifier != null) _mods.Apply(u.Modifier, d.Week, bypassCap: true);
            _shop.SetOffers(d.Offers);
            _runEarned = d.TotalEarned; _runUpgradeSpend = d.TotalSpentUpgrades; _runDaysPlayed = d.DaysPlayed; _runSeed = d.Seed; _runWeeksPaid = Math.Max(0, d.Week - 1);
            _weekIncome = d.WeekIncome;
            _players = Mathf.Clamp(d.Players, 1, GameInfo.MaxPlayers);
            _quota = d.Quota > 0 ? d.Quota : EconomyMath.Quota(d.Week, _players, Tuning);
            _ledger.WeeklyQuota = _quota;
            if (IsSpawned) _nvQuota.Value = _quota;
            _clock.DurationScale = EconomyDevCvars.DayScale;
            var phase = d.Resume == RunResumePoint.Shop ? DayPhase.Shop : DayPhase.Morning;
            _clock.Restore(phase, d.DayIndex, d.Week, d.DayOfWeek);
            if (phase == DayPhase.Morning) EnterMorning();
            else if (_shop.Offers.Count == 0) _shop.RollOffers(_clock.Week, new System.Random(_runSeed ^ (_clock.DayIndex * 7919)));
            PushWallet();
            PushShop();
            PushCalendar();
            OnPhaseChanged?.Invoke(DayPhase.None, phase);
            Bark($"Welcome back. Week {d.Week}, the farm remembers.");
        }

        void PushCalendar()
        {
            if (!IsSpawned) return;
            _nvPhase.Value = (byte)_clock.Phase;
            _nvDuration.Value = _clock.Duration;
            _nvRemaining.Value = _clock.Remaining;
            _nvCalendar.Value = (_clock.DayIndex & 0xFFFF) | ((_clock.Week & 0x3FF) << 16) | ((_clock.DayOfWeek & 0x1F) << 26);
        }

        // ---------------- replication ----------------

        void PushShop()
        {
            OnShopChanged?.Invoke();
            if (!IsSpawned || !IsServer) return;
            var sb = new StringBuilder();
            sb.Append(string.Join(",", _shop.Owned)).Append('|').Append(string.Join(",", _shop.Offers)).Append('|');
            foreach (var q in _shop.Queue) sb.Append(q.Proposer).Append(':').Append(q.Sell ? 1 : 0).Append(':').Append(q.UpgradeId).Append(';');
            string s = sb.ToString();
            if (s.Length > 4000) s = s.Substring(0, 4000);
            _nvShop.Value = new FixedString4096Bytes(s);
        }

        void HookClientShop()
        {
            _nvShop.OnValueChanged += (a, b) => RebuildClientShop();
            RebuildClientShop();
        }

        void RebuildClientShop()
        {
            if (!ReadRemote) return;
            string key = _nvShop.Value.ToString();
            if (key == _clientShopKey) return;
            _clientShopKey = key;
            var parts = key.Split('|');
            _clientOwned.Clear(); _clientOffers.Clear(); _clientQueue.Clear();
            if (parts.Length > 0) _clientOwned.AddRange(parts[0].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
            if (parts.Length > 1) _clientOffers.AddRange(parts[1].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
            if (parts.Length > 2)
                foreach (var e in parts[2].Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var f = e.Split(new[] { ':' }, 3);
                    if (f.Length == 3 && ulong.TryParse(f[0], out ulong who)) _clientQueue.Add(new ShopProposal { Proposer = who, Sell = f[1] == "1", UpgradeId = f[2] });
                }
            OnShopChanged?.Invoke();
        }

        /// <summary>Display name for a player id (shop UI).</summary>
        public string PlayerName(ulong id) => NameOf(id, null);
    }
}
