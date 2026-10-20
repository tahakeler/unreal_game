using System;
using System.Collections.Generic;
using HarvestPanic.Core;

namespace HarvestPanic.Economy
{
    /// <summary>One ledger line: a (possibly merged) blame event plus how many incidents it covers.</summary>
    [Serializable]
    public sealed class LedgerEntry
    {
        public BlameEvent Event;
        public int Count = 1;
        public float LastTimestamp;
    }

    /// <summary>
    /// Host-side Blame Ledger (blame-ledger.md): attribution shares, 1.5 s merge window, $2 noise floor,
    /// severity, disaster detection, day list and weekly per-player rollup. Never touches the wallet.
    /// </summary>
    public sealed class BlameLedger
    {
        readonly EconomyTuning _t;
        readonly List<LedgerEntry> _day = new List<LedgerEntry>();
        readonly Dictionary<ulong, PlayerWeek> _week = new Dictionary<ulong, PlayerWeek>();
        uint _nextId = 1;

        /// <summary>Weekly per-player rollup.</summary>
        public sealed class PlayerWeek
        {
            public float BlameScore;
            public int Disasters, Butterfingers, BridgeTroll, TownCrier, PositiveDollars, LostDollars;
            public float FirstEventTime = float.MaxValue;
        }

        /// <summary>Weekly quota used for severity (set by the director each Monday).</summary>
        public int WeeklyQuota { get; set; } = 1200;

        /// <summary>Today's entries in arrival order.</summary>
        public IReadOnlyList<LedgerEntry> Day => _day;
        /// <summary>This week's per-player rollup.</summary>
        public IReadOnlyDictionary<ulong, PlayerWeek> Week => _week;

        /// <summary>Raised when an accepted (new or merged) entry is a disaster.</summary>
        public event Action<BlameEvent> Disaster;

        public BlameLedger(EconomyTuning tuning) { _t = tuning; }

        /// <summary>
        /// Submits an incident. Fills shares/primary from the instigator order (newest first), merges, filters noise,
        /// computes severity. Returns the stored entry or null when discarded.
        /// </summary>
        public LedgerEntry Submit(BlameEvent e)
        {
            Normalise(ref e);
            var merged = FindMergeTarget(e);
            if (merged != null)
            {
                merged.Event.DollarDelta += e.DollarDelta;
                merged.Count++;
                merged.LastTimestamp = e.Timestamp;
                bool wasDisaster = IsDisaster(merged.Event);
                merged.Event.Severity = EconomyMath.Severity(merged.Event.DollarDelta, WeeklyQuota, merged.Event.Verb);
                AddToWeek(e, IsDisaster(merged.Event) && !wasDisaster);
                if (IsDisaster(merged.Event) && !wasDisaster) Disaster?.Invoke(merged.Event);
                return merged;
            }

            if (Math.Abs(e.DollarDelta) < _t.NoiseFloorDollars && !EconomyMath.IsNonEconomic(e.Verb)) return null;

            e.EventId = _nextId++;
            e.Severity = EconomyMath.Severity(e.DollarDelta, WeeklyQuota, e.Verb);
            var entry = new LedgerEntry { Event = e, LastTimestamp = e.Timestamp };
            if (_day.Count >= _t.MaxEventsPerDay) CompactOldest();
            _day.Add(entry);
            bool disaster = IsDisaster(e);
            AddToWeek(e, disaster);
            if (disaster) Disaster?.Invoke(e);
            return entry;
        }

        /// <summary>Severity ≥ threshold, or river drop / tipped wagon / plank break with an item lost.</summary>
        public bool IsDisaster(in BlameEvent e) =>
            e.Severity >= _t.DisasterSeverity || e.Verb == BlameVerb.DroppedInRiver || e.Verb == BlameVerb.TippedWagon ||
            (e.Verb == BlameVerb.BrokePlank && !string.IsNullOrEmpty(e.ItemId));

        /// <summary>Highest-severity entry of the day (headline), or null.</summary>
        public LedgerEntry Headline()
        {
            LedgerEntry best = null;
            foreach (var x in _day)
                if (x.Event.DollarDelta <= 0 && (best == null || x.Event.Severity > best.Event.Severity)) best = x;
            return best;
        }

        /// <summary>Clears the day list (weekly rollup stays).</summary>
        public void StartDay() => _day.Clear();

        /// <summary>Clears the weekly rollup.</summary>
        public void StartWeek() => _week.Clear();

        /// <summary>Player with the highest weekly Blame Score (MVD); ties → earlier first event. None if empty.</summary>
        public ulong MostValuableDisaster()
        {
            ulong best = PlayerIds.None; PlayerWeek bw = null;
            foreach (var kv in _week)
            {
                if (!PlayerIds.IsPlayer(kv.Key)) continue;
                var w = kv.Value;
                if (bw == null || w.BlameScore > bw.BlameScore || (w.BlameScore == bw.BlameScore && w.FirstEventTime < bw.FirstEventTime))
                { best = kv.Key; bw = w; }
            }
            return best;
        }

        void Normalise(ref BlameEvent e)
        {
            if (e.Instigators == null) e.Instigators = Array.Empty<ulong>();
            // Drop sentinels and duplicates; keep newest-first order.
            var ids = new List<ulong>(4);
            foreach (var id in e.Instigators) if (PlayerIds.IsPlayer(id) && !ids.Contains(id) && ids.Count < 4) ids.Add(id);
            e.Instigators = ids.ToArray();
            if (e.BlameShares == null || e.BlameShares.Length != e.Instigators.Length)
                e.BlameShares = EconomyMath.TouchShares(e.Instigators.Length, _t);
            if (e.Instigators.Length > e.BlameShares.Length)
            {
                Array.Resize(ref e.Instigators, e.BlameShares.Length);
            }
            if (e.Instigators.Length == 0) e.PrimaryInstigator = PlayerIds.Nature;
            else
            {
                int bi = 0;
                for (int i = 1; i < e.BlameShares.Length; i++) if (e.BlameShares[i] > e.BlameShares[bi]) bi = i;
                e.PrimaryInstigator = e.Instigators[bi];
            }
            if (e.DollarDelta > 0 && e.Verb != BlameVerb.Rescue && e.Verb != BlameVerb.StealBack) e.DollarDelta = -e.DollarDelta;
            if (e.ItemId == null) e.ItemId = string.Empty;
        }

        LedgerEntry FindMergeTarget(in BlameEvent e)
        {
            for (int i = _day.Count - 1; i >= 0; i--)
            {
                var x = _day[i];
                if (e.Timestamp - x.LastTimestamp > _t.MergeWindow) continue;
                if (x.Event.Verb == e.Verb && x.Event.PrimaryInstigator == e.PrimaryInstigator &&
                    string.Equals(x.Event.ItemId, e.ItemId, StringComparison.Ordinal) &&
                    Math.Sign(x.Event.DollarDelta) == Math.Sign(e.DollarDelta) && x.Event.IsSecret == e.IsSecret)
                    return x;
            }
            return null;
        }

        void AddToWeek(in BlameEvent e, bool newDisaster)
        {
            ulong[] ids = e.Instigators.Length > 0 ? e.Instigators : new[] { PlayerIds.Nature };
            float[] shares = e.Instigators.Length > 0 ? e.BlameShares : new[] { 1f };
            for (int i = 0; i < ids.Length; i++)
            {
                if (!_week.TryGetValue(ids[i], out var w)) _week[ids[i]] = w = new PlayerWeek();
                w.FirstEventTime = Math.Min(w.FirstEventTime, e.DayIndex * 10000f + e.Timestamp);
                if (e.DollarDelta < 0) { w.BlameScore += -e.DollarDelta * shares[i]; w.LostDollars += (int)Math.Round(-e.DollarDelta * shares[i]); }
                else { w.PositiveDollars += (int)Math.Round(e.DollarDelta * shares[i]); w.BlameScore = Math.Max(0f, w.BlameScore - 0.5f * e.DollarDelta * shares[i]); }
                if (ids[i] != e.PrimaryInstigator) continue;
                if (newDisaster) { w.Disasters++; w.BlameScore += _t.DisasterScoreBonus; }
                switch (e.Verb)
                {
                    case BlameVerb.Dropped: case BlameVerb.Threw: w.Butterfingers++; break;
                    case BlameVerb.BrokePlank: case BlameVerb.DroppedInRiver: w.BridgeTroll++; break;
                    case BlameVerb.SpookedHerd: case BlameVerb.SummonedCreature: w.TownCrier++; break;
                }
            }
        }

        void CompactOldest()
        {
            for (int i = 0; i < _day.Count; i++)
                if (_day[i].Event.Severity < 20) { _day.RemoveAt(i); return; }
            _day.RemoveAt(0);
        }
    }
}
