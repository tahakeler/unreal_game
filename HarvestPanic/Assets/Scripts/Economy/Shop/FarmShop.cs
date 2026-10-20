using System;
using System.Collections.Generic;

namespace HarvestPanic.Economy
{
    /// <summary>Why a shop action was refused (shown as a bark).</summary>
    public enum ShopResult { Ok, NotInShop, Unknown, AlreadyOwned, MissingPrereq, NotEnoughMoney, WalletNegative, NotOwned, SellHigherFirst, NotOffered, Busy }

    /// <summary>A pending crew proposal (farm-upgrades §3 rule 2: any player proposes, host confirms).</summary>
    [Serializable]
    public struct ShopProposal
    {
        public ulong Proposer;
        public bool Sell;
        public string UpgradeId;
    }

    /// <summary>
    /// Pure shop rules (farm-upgrades §3–§5, shared-wallet-economy §3): owned set, offers, purchase/sell-back
    /// validation, 50% floor refund, highest-level-first selling, one proposal at a time with a FIFO queue.
    /// No Unity state; the director applies money and modifiers.
    /// </summary>
    public sealed class FarmShop
    {
        public const int MaxQueue = 4;
        readonly Dictionary<string, UpgradeDefinition> _defs = new Dictionary<string, UpgradeDefinition>(StringComparer.OrdinalIgnoreCase);
        readonly List<string> _owned = new List<string>();
        readonly Dictionary<string, int> _paid = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        readonly List<string> _offers = new List<string>();
        readonly List<ShopProposal> _queue = new List<ShopProposal>();

        public float SellBackRate = 0.5f;
        public int OffersPerShop = 6;

        public IReadOnlyList<string> Owned => _owned;
        public IReadOnlyList<string> Offers => _offers;
        public IReadOnlyList<ShopProposal> Queue => _queue;
        public bool HasPending => _queue.Count > 0;
        public ShopProposal Pending => _queue.Count > 0 ? _queue[0] : default;
        public IEnumerable<UpgradeDefinition> Definitions => _defs.Values;

        public FarmShop(IEnumerable<UpgradeDefinition> defs)
        {
            if (defs == null) return;
            foreach (var d in defs) if (d != null && !string.IsNullOrEmpty(d.UpgradeId)) _defs[d.UpgradeId] = d;
        }

        public bool TryGet(string id, out UpgradeDefinition d) => _defs.TryGetValue(id ?? "", out d);
        public bool IsOwned(string id) => _owned.Exists(o => string.Equals(o, id, StringComparison.OrdinalIgnoreCase));
        public int PaidFor(string id) => _paid.TryGetValue(id ?? "", out int p) ? p : (TryGet(id, out var d) ? d.Price : 0);

        /// <summary>Refund = floor(rate × price paid).</summary>
        public static int Refund(int pricePaid, float rate) => (int)Math.Floor(pricePaid * (double)rate + 1e-9);
        public int RefundFor(string id) => Refund(PaidFor(id), SellBackRate);

        /// <summary>Weight max(0, 1 − |t − ceil(w/2)| × 0.5).</summary>
        public static float OfferWeight(int tier, int week) => Math.Max(0f, 1f - Math.Abs(tier - (int)Math.Ceiling(week / 2.0)) * 0.5f);

        public void Reset() { _owned.Clear(); _paid.Clear(); _offers.Clear(); _queue.Clear(); }

        /// <summary>Restores ownership (save load / client mirror) without money.</summary>
        public void SetOwned(IEnumerable<string> ids, IDictionary<string, int> paid = null)
        {
            _owned.Clear(); _paid.Clear();
            if (ids == null) return;
            foreach (var id in ids)
            {
                if (!_defs.ContainsKey(id) || IsOwned(id)) continue;
                _owned.Add(id);
                if (paid != null && paid.TryGetValue(id, out int p)) _paid[id] = p;
            }
        }

        public void SetOffers(IEnumerable<string> ids) { _offers.Clear(); if (ids != null) foreach (var i in ids) if (_defs.ContainsKey(i)) _offers.Add(i); }

        bool Buyable(UpgradeDefinition d) => !IsOwned(d.UpgradeId) && (string.IsNullOrEmpty(d.RequiresId) || IsOwned(d.RequiresId));

        /// <summary>Rolls this shop's offers: staples first, then weighted picks by week tier; fallback any remaining.</summary>
        public void RollOffers(int week, Random rng)
        {
            _offers.Clear();
            var pool = new List<UpgradeDefinition>();
            foreach (var d in _defs.Values) if (Buyable(d)) pool.Add(d);
            pool.Sort((a, b) => string.CompareOrdinal(a.UpgradeId, b.UpgradeId)); // deterministic for a seed
            foreach (var d in pool) if (d.Staple && _offers.Count < OffersPerShop) _offers.Add(d.UpgradeId);
            pool.RemoveAll(d => _offers.Contains(d.UpgradeId));
            while (_offers.Count < OffersPerShop && pool.Count > 0)
            {
                float total = 0f;
                foreach (var d in pool) total += OfferWeight(d.Tier, week);
                int pick = 0;
                if (total > 0f)
                {
                    double r = rng.NextDouble() * total;
                    for (pick = 0; pick < pool.Count - 1; pick++) { r -= OfferWeight(pool[pick].Tier, week); if (r < 0) break; }
                    if (OfferWeight(pool[pick].Tier, week) <= 0f) pick = pool.FindIndex(d => OfferWeight(d.Tier, week) > 0f);
                }
                else pick = rng.Next(pool.Count);
                _offers.Add(pool[pick].UpgradeId);
                pool.RemoveAt(pick);
            }
        }

        /// <summary>Purchase check: offered, not owned, prereq owned, balance ≥ 0 and ≥ price.</summary>
        public ShopResult CanBuy(string id, int balance)
        {
            if (!TryGet(id, out var d)) return ShopResult.Unknown;
            if (IsOwned(id)) return ShopResult.AlreadyOwned;
            if (!string.IsNullOrEmpty(d.RequiresId) && !IsOwned(d.RequiresId)) return ShopResult.MissingPrereq;
            if (!_offers.Contains(d.UpgradeId)) return ShopResult.NotOffered;
            if (balance < 0) return ShopResult.WalletNegative;
            if (balance < d.Price) return ShopResult.NotEnoughMoney;
            return ShopResult.Ok;
        }

        /// <summary>Sell check: owned and nothing owned depends on it.</summary>
        public ShopResult CanSell(string id)
        {
            if (!TryGet(id, out _)) return ShopResult.Unknown;
            if (!IsOwned(id)) return ShopResult.NotOwned;
            foreach (var o in _owned) if (TryGet(o, out var od) && string.Equals(od.RequiresId, id, StringComparison.OrdinalIgnoreCase)) return ShopResult.SellHigherFirst;
            return ShopResult.Ok;
        }

        /// <summary>Records a successful purchase.</summary>
        public void MarkBought(string id, int pricePaid) { if (!IsOwned(id)) _owned.Add(id); _paid[id] = pricePaid; _offers.Remove(id); }
        /// <summary>Records a sell-back.</summary>
        public void MarkSold(string id) { _owned.RemoveAll(o => string.Equals(o, id, StringComparison.OrdinalIgnoreCase)); _paid.Remove(id); }

        /// <summary>Queues a proposal (one per player, cap <see cref="MaxQueue"/>).</summary>
        public ShopResult Propose(ShopProposal p)
        {
            if (!TryGet(p.UpgradeId, out _)) return ShopResult.Unknown;
            if (_queue.Count >= MaxQueue || _queue.Exists(q => q.Proposer == p.Proposer)) return ShopResult.Busy;
            _queue.Add(p);
            return ShopResult.Ok;
        }

        /// <summary>Pops the head proposal.</summary>
        public bool TakePending(out ShopProposal p)
        {
            p = default;
            if (_queue.Count == 0) return false;
            p = _queue[0]; _queue.RemoveAt(0);
            return true;
        }

        public void ClearQueue() => _queue.Clear();

        /// <summary>Bark copy for a refusal.</summary>
        public static string Bark(ShopResult r)
        {
            switch (r)
            {
                case ShopResult.NotEnoughMoney: return "The wallet says no.";
                case ShopResult.WalletNegative: return "We're in the red. Shop's locked.";
                case ShopResult.AlreadyOwned: return "We already own that.";
                case ShopResult.MissingPrereq: return "Buy the first level first.";
                case ShopResult.SellHigherFirst: return "Sell the higher level first.";
                case ShopResult.Busy: return "One idea at a time, please.";
                case ShopResult.NotInShop: return "The shop is closed.";
                case ShopResult.NotOffered: return "Not on the chalkboard today.";
                case ShopResult.NotOwned: return "We don't own that.";
                default: return "Grandpa doesn't sell that.";
            }
        }
    }
}
