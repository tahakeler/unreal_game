using System.Collections.Generic;

namespace HarvestPanic.Camcorder
{
    /// <summary>
    /// The day's tapes in memory (GDD rule 4). Caps: <see cref="MaxTapes"/> per day and <see cref="BudgetBytes"/> total;
    /// when over either, the lowest <see cref="Moment.KeepScore"/> non-manual tape is evicted first (manual before nothing).
    /// At payout <see cref="PruneForPayout"/> keeps the top N by severity plus all manual tapes.
    /// </summary>
    public sealed class MomentShelf
    {
        readonly List<Moment> _items = new List<Moment>();
        public int MaxTapes = 12;
        public long BudgetBytes = 48L * 1024 * 1024;
        public IReadOnlyList<Moment> Items => _items;
        public int Count => _items.Count;

        public long MemoryBytes { get { long b = 0; foreach (var m in _items) b += m.MemoryBytes; return b; } }

        /// <summary>Adds a tape (or replaces one with the same id). Returns evicted tapes' count.</summary>
        public int Add(Moment m)
        {
            int existing = _items.FindIndex(x => x.Id == m.Id);
            if (existing >= 0)
            {
                var old = _items[existing];
                if (!ReferenceEquals(old, m))
                {
                    if (m.Thumbnail == null) m.Thumbnail = old.Thumbnail;
                    else if (old.Thumbnail != m.Thumbnail) old.ReleaseRuntime();
                }
                _items[existing] = m;
            }
            else _items.Add(m);
            int evicted = 0;
            while (_items.Count > 1 && (_items.Count > MaxTapes || MemoryBytes > BudgetBytes))
            {
                int victim = Weakest(m);
                if (victim < 0) break;
                _items[victim].ReleaseRuntime();
                _items.RemoveAt(victim);
                evicted++;
            }
            return evicted;
        }

        int Weakest(Moment protect)
        {
            int best = -1;
            for (int pass = 0; pass < 2 && best < 0; pass++)
                for (int i = 0; i < _items.Count; i++)
                {
                    var x = _items[i];
                    if (ReferenceEquals(x, protect) || (pass == 0 && x.Manual)) continue;
                    if (best < 0 || x.KeepScore < _items[best].KeepScore) best = i;
                }
            return best;
        }

        /// <summary>Keeps the <paramref name="keepTop"/> highest-severity disaster tapes plus every manual tape.</summary>
        public void PruneForPayout(int keepTop)
        {
            var auto = _items.FindAll(x => !x.Manual);
            auto.Sort((a, b) => b.Severity != a.Severity ? b.Severity.CompareTo(a.Severity) : a.Id.CompareTo(b.Id));
            for (int i = keepTop; i < auto.Count; i++) { auto[i].ReleaseRuntime(); _items.Remove(auto[i]); }
        }

        /// <summary>"Moment of the day": the biggest disaster (severity, then $ lost); falls back to the latest manual tape.</summary>
        public Moment Best()
        {
            Moment best = null;
            foreach (var m in _items)
            {
                if (m.Manual) continue;
                if (best == null || m.Severity > best.Severity || (m.Severity == best.Severity && m.DollarDelta < best.DollarDelta)) best = m;
            }
            if (best != null) return best;
            for (int i = _items.Count - 1; i >= 0; i--) return _items[i];
            return null;
        }

        public Moment Find(long id) => _items.Find(x => x.Id == id);

        public void Clear() { foreach (var m in _items) m.ReleaseRuntime(); _items.Clear(); }
    }
}
