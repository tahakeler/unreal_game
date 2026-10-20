using System;
using System.Collections.Generic;
using HarvestPanic.Core;

namespace HarvestPanic.Economy
{
    /// <summary>One award printed on a receipt.</summary>
    [Serializable]
    public sealed class ReceiptAward
    {
        public string Title;
        public string Name;
        public string Reason;
        public ulong PlayerId;
    }

    /// <summary>
    /// End-of-day and Sunday awards from the Blame Ledger (blame-ledger §3 rule 4, GDD §2.7 solo rule).
    /// Daily: picked from today's entries (max <c>DailyAwardsMax</c>, distinct titles, MVD and Pig Whisperer first).
    /// Weekly: the five ledger awards from the weekly rollup. Ties go to the earlier first qualifying event.
    /// Solo: only MVD + Pig Whisperer, with self-deprecating copy.
    /// </summary>
    public static class AwardPicker
    {
        public const string Mvd = "Most Valuable Disaster", PigWhisperer = "Pig Whisperer", BridgeInspector = "Bridge Inspector",
            Butterfingers = "Butterfingers", TownCrier = "Town Crier", FrequentSwimmer = "Frequent Swimmer", EscapeAgent = "Escape Artist's Agent",
            BridgeTroll = "Bridge Troll";

        sealed class Tally { public int Loss, Positive, Bridge, Butter, Crier, Swim, Escape; public int First = int.MaxValue; }

        /// <summary>Daily awards for the payout receipt.</summary>
        public static List<ReceiptAward> Daily(IReadOnlyList<LedgerEntry> day, Func<ulong, string> nameOf, int players, int max)
        {
            var tallies = new Dictionary<ulong, Tally>();
            for (int i = 0; i < (day?.Count ?? 0); i++)
            {
                var e = day[i].Event;
                if (e.IsSecret) continue;
                ulong who = e.PrimaryInstigator;
                if (!PlayerIds.IsPlayer(who)) continue;
                if (!tallies.TryGetValue(who, out var t)) tallies[who] = t = new Tally { First = i };
                int n = Math.Max(1, day[i].Count);
                if (e.DollarDelta < 0) t.Loss += -e.DollarDelta; else t.Positive += e.DollarDelta;
                switch (e.Verb)
                {
                    case BlameVerb.BrokePlank: case BlameVerb.DroppedInRiver: t.Bridge += n; break;
                    case BlameVerb.Dropped: case BlameVerb.Threw: case BlameVerb.Slipped: t.Butter += n; break;
                    case BlameVerb.SpookedHerd: case BlameVerb.SummonedCreature: t.Crier += n; break;
                    case BlameVerb.Drowned: t.Swim += n; break;
                    case BlameVerb.LetEscape: t.Escape += n; break;
                }
            }
            bool solo = players <= 1;
            var list = new List<ReceiptAward>();
            void Try(string title, Func<Tally, int> score, Func<int, string> reason)
            {
                if (list.Count >= max) return;
                ulong best = PlayerIds.None; int bs = 0, bf = int.MaxValue;
                foreach (var kv in tallies)
                {
                    int s = score(kv.Value);
                    if (s <= 0) continue;
                    if (s > bs || (s == bs && kv.Value.First < bf)) { best = kv.Key; bs = s; bf = kv.Value.First; }
                }
                if (best == PlayerIds.None) return;
                list.Add(new ReceiptAward { Title = title, PlayerId = best, Name = nameOf(best), Reason = reason(bs) });
            }
            Try(Mvd, t => t.Loss, v => solo ? $"{ReceiptBuilder.Money(-v)}. Also the only candidate." : $"{ReceiptBuilder.Money(-v)} of pure chaos");
            Try(PigWhisperer, t => t.Positive, v => solo ? $"+{ReceiptBuilder.Money(v)}. Won by default. Still counts." : $"+{ReceiptBuilder.Money(v)} of heroics");
            if (solo) return list;
            Try(BridgeInspector, t => t.Bridge, v => v == 1 ? "personally tested the bridge" : $"tested the bridge {v} times");
            Try(Butterfingers, t => t.Butter, v => $"dropped things {v} times");
            Try(FrequentSwimmer, t => t.Swim, v => v == 1 ? "went for a swim" : $"{v} swims, zero towels");
            Try(TownCrier, t => t.Crier, v => $"screamed at livestock {v} times");
            Try(EscapeAgent, t => t.Escape, v => $"freed {v} animals");
            return list;
        }

        /// <summary>The five weekly awards (Sunday) from the ledger's weekly rollup; solo gets two.</summary>
        public static List<ReceiptAward> Weekly(IReadOnlyDictionary<ulong, BlameLedger.PlayerWeek> week, Func<ulong, string> nameOf, int players)
        {
            var list = new List<ReceiptAward>();
            bool solo = players <= 1;
            void Try(string title, Func<BlameLedger.PlayerWeek, float> score, Func<float, string> reason)
            {
                ulong best = PlayerIds.None; float bs = 0f, bf = float.MaxValue;
                foreach (var kv in week)
                {
                    if (!PlayerIds.IsPlayer(kv.Key)) continue;
                    float s = score(kv.Value);
                    if (s <= 0f) continue;
                    if (s > bs || (s == bs && kv.Value.FirstEventTime < bf)) { best = kv.Key; bs = s; bf = kv.Value.FirstEventTime; }
                }
                if (best == PlayerIds.None) return;
                list.Add(new ReceiptAward { Title = title, PlayerId = best, Name = nameOf(best), Reason = reason(bs) });
            }
            if (week == null) return list;
            Try(Mvd, w => w.BlameScore, v => solo ? "Unopposed. Somehow still close." : $"blame score {Math.Round(v)}");
            Try(PigWhisperer, w => w.PositiveDollars, v => solo ? "Saved yourself from yourself." : $"+{ReceiptBuilder.Money((int)v)} rescued");
            if (solo) return list;
            Try(Butterfingers, w => w.Butterfingers, v => $"{v} drops and throws");
            Try(BridgeTroll, w => w.BridgeTroll, v => $"{v} planks and splashes");
            Try(TownCrier, w => w.TownCrier, v => $"{v} herds spooked");
            return list;
        }
    }
}
