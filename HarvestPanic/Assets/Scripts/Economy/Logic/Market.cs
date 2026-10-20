using System;
using System.Collections.Generic;

namespace HarvestPanic.Economy
{
    /// <summary>One Morning Order line (day-loop §3).</summary>
    [Serializable]
    public struct OrderLine
    {
        public string GoodsId;
        public int Quantity;
        public int Filled;
        public bool IsComplete => Filled >= Quantity;
    }

    /// <summary>
    /// Daily market state: seeded per-goods demand roll, saturation counters and the Morning Order book.
    /// Pure logic (shared-wallet-economy "Market demand", day-loop "Morning Order").
    /// </summary>
    public sealed class Market
    {
        readonly EconomyTuning _t;
        readonly Dictionary<string, float> _dayDemand = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, int> _soldToday = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        readonly List<OrderLine> _order = new List<OrderLine>();
        Random _rng = new Random(1);

        /// <summary>Today's order lines.</summary>
        public IReadOnlyList<OrderLine> Order => _order;
        /// <summary>Gross sale dollars today.</summary>
        public int SalesToday { get; private set; }
        /// <summary>Units sold today.</summary>
        public int UnitsToday { get; private set; }

        public Market(EconomyTuning t) { _t = t; }

        /// <summary>
        /// New day: rerolls demand with <paramref name="seed"/> and draws <paramref name="lines"/> distinct order lines
        /// from <paramref name="available"/> (ids the level can actually supply; falls back to tuning list).
        /// </summary>
        public void StartDay(int seed, int week, int players, int lines, IReadOnlyCollection<string> available)
        {
            _rng = new Random(seed);
            _dayDemand.Clear(); _soldToday.Clear(); _order.Clear();
            SalesToday = 0; UnitsToday = 0;
            var pool = new List<OrderableGoods>();
            foreach (var o in _t.Orderable)
            {
                _dayDemand[o.GoodsId] = (float)(_t.DemandDayMin + _rng.NextDouble() * (_t.DemandDayMax - _t.DemandDayMin));
                if (available == null || available.Count == 0 || Contains(available, o.GoodsId)) pool.Add(o);
            }
            for (int i = 0; i < lines && pool.Count > 0; i++)
            {
                int k = _rng.Next(pool.Count);
                var pick = pool[k]; pool.RemoveAt(k);
                _order.Add(new OrderLine { GoodsId = pick.GoodsId, Quantity = EconomyMath.OrderQuantity(pick.BaseQuantity, players, week, _t) });
            }
        }

        static bool Contains(IReadOnlyCollection<string> c, string id)
        {
            foreach (var s in c) if (string.Equals(s, id, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// <summary>D_day for a goods id (1.0 if unknown).</summary>
        public float DayDemand(string goodsId) => _dayDemand.TryGetValue(goodsId ?? "", out var d) ? d : 1f;

        /// <summary>Demand for the next unit of <paramref name="goodsId"/> (not consumed).</summary>
        public float NextDemand(string goodsId, float demandMult) =>
            EconomyMath.Demand(DayDemand(goodsId), Sold(goodsId) + 1, demandMult, _t);

        /// <summary>Units of a goods id sold today.</summary>
        public int Sold(string goodsId) => _soldToday.TryGetValue(goodsId ?? "", out var n) ? n : 0;

        /// <summary>True if an open order line wants this goods id.</summary>
        public bool WouldFillOrder(string goodsId)
        {
            foreach (var l in _order) if (!l.IsComplete && string.Equals(l.GoodsId, goodsId, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// <summary>Records a sale; fills an order line if one is open. Returns true if an order line was filled.</summary>
        public bool RecordSale(string goodsId, int value)
        {
            _soldToday[goodsId ?? ""] = Sold(goodsId) + 1;
            SalesToday += value; UnitsToday++;
            for (int i = 0; i < _order.Count; i++)
            {
                var l = _order[i];
                if (l.IsComplete || !string.Equals(l.GoodsId, goodsId, StringComparison.OrdinalIgnoreCase)) continue;
                l.Filled++; _order[i] = l;
                return true;
            }
            return false;
        }

        /// <summary>All lines complete (and at least one line exists).</summary>
        public bool OrderComplete
        {
            get
            {
                if (_order.Count == 0) return false;
                foreach (var l in _order) if (!l.IsComplete) return false;
                return true;
            }
        }

        /// <summary>Replaces the order book (client mirror).</summary>
        public void SetOrder(IEnumerable<OrderLine> lines) { _order.Clear(); _order.AddRange(lines); }
    }
}
