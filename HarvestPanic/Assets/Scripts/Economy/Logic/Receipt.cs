using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using HarvestPanic.Core;

namespace HarvestPanic.Economy
{
    /// <summary>One printed receipt line.</summary>
    [Serializable]
    public sealed class ReceiptLine
    {
        public string Text;
        public int Dollars;
        public int Count;
    }

    /// <summary>One culprit block (player or Nature).</summary>
    [Serializable]
    public sealed class ReceiptBlock
    {
        public string Name;
        public ulong PlayerId;
        public bool IsNature;
        public int Total;
        public int MoreLines;
        public List<ReceiptLine> Lines = new List<ReceiptLine>();
    }

    /// <summary>End-of-day payout + Blame Ledger receipt, replicated to clients as JSON.</summary>
    [Serializable]
    public sealed class ReceiptData
    {
        public int DayIndex, Week, DayOfWeek;
        public int Sales, OrderBonus, Costs, Net, Balance, Quota, Iou, UnitsSold;
        public bool OrderComplete;
        public string Headline;
        public List<ReceiptBlock> Blocks = new List<ReceiptBlock>();
        public List<ReceiptLine> Money = new List<ReceiptLine>();
        // Sunday
        public bool IsSunday, Paid;
        public int Due, Carryover;
        public string Mvd;
        public string SundayLine;
    }

    /// <summary>
    /// Builds the itemized receipt (blame-ledger "Payout Receipt"): group by primary instigator then verb+item,
    /// top N lines per player with "+n more", positives after negatives, Nature last, headline = top severity.
    /// Text templates live in <see cref="LedgerText"/>.
    /// </summary>
    public static class ReceiptBuilder
    {
        public static void AddBlame(ReceiptData r, IReadOnlyList<LedgerEntry> day, Func<ulong, string> nameOf, EconomyTuning t)
        {
            var blocks = new Dictionary<ulong, (ReceiptBlock block, Dictionary<string, ReceiptLine> lines, List<(ReceiptLine line, bool positive)> order)>();
            foreach (var entry in day)
            {
                var e = entry.Event;
                if (e.IsSecret) continue;
                ulong who = e.Instigators != null && e.Instigators.Length > 0 ? e.PrimaryInstigator : PlayerIds.Nature;
                if (!blocks.TryGetValue(who, out var b))
                {
                    var blk = new ReceiptBlock { PlayerId = who, IsNature = who == PlayerIds.Nature, Name = who == PlayerIds.Nature ? LedgerText.NatureName(e.CauseTag) : nameOf(who) };
                    b = (blk, new Dictionary<string, ReceiptLine>(), new List<(ReceiptLine, bool)>());
                    blocks[who] = b;
                }
                string key = e.Verb + "|" + e.ItemId;
                if (!b.lines.TryGetValue(key, out var line))
                {
                    line = new ReceiptLine { Text = "" };
                    b.lines[key] = line;
                    b.order.Add((line, e.DollarDelta > 0));
                }
                line.Count += entry.Count;
                line.Dollars += e.DollarDelta;
                line.Text = LedgerText.Line(e.Verb, LedgerText.ItemName(e.ItemId, e.ItemTag), line.Count);
                b.block.Total += e.DollarDelta;
            }

            var ordered = new List<ReceiptBlock>();
            foreach (var kv in blocks)
            {
                var (block, _, order) = kv.Value;
                order.Sort((a, c) => a.positive != c.positive ? (a.positive ? 1 : -1) : a.line.Dollars.CompareTo(c.line.Dollars));
                int shown = Math.Min(order.Count, t.ReceiptLinesPerPlayer);
                for (int i = 0; i < shown; i++) block.Lines.Add(order[i].line);
                block.MoreLines = order.Count - shown;
                ordered.Add(block);
            }
            ordered.Sort((a, c) => a.IsNature != c.IsNature ? (a.IsNature ? 1 : -1) : a.Total.CompareTo(c.Total));
            r.Blocks.AddRange(ordered);
        }

        /// <summary>"$1,234" / "−$40".</summary>
        public static string Money(int dollars) =>
            (dollars < 0 ? "−$" : "$") + Math.Abs(dollars).ToString("N0", CultureInfo.InvariantCulture);

        /// <summary>Signed with explicit plus.</summary>
        public static string SignedMoney(int dollars) => dollars > 0 ? "+" + Money(dollars) : Money(dollars);
    }

    /// <summary>
    /// Receipt copy (blame-ledger receipt line templates + comedy beats). Kept in one table so it can move to a
    /// localization StringTable without touching logic.
    /// </summary>
    public static class LedgerText
    {
        public const string Spotless = "Spotless. Suspicious.";
        public const string MoreLines = "+{0} more crimes";
        public const string NoHeadline = "A quiet day. Grandpa is worried.";

        /// <summary>Line for a verb (count &gt; 1 appends ×n).</summary>
        public static string Line(BlameVerb v, string item, int n)
        {
            string x = n > 1 ? " ×" + n : "";
            switch (v)
            {
                case BlameVerb.Bruised: return $"bruised the {item}{x}";
                case BlameVerb.Dropped: return $"dropped the {item}{x}";
                case BlameVerb.Slipped: return $"lost grip on the {item}{x}";
                case BlameVerb.DroppedInRiver: return $"dropped the {item} into the river{x}";
                case BlameVerb.Threw: return $"yeeted the {item}{x}";
                case BlameVerb.Crashed: return $"crashed with the {item} aboard{x}";
                case BlameVerb.SpilledCargo: return $"bounced the {item} out of the wagon{x}";
                case BlameVerb.LetEscape: return $"let the {item} escape{x}";
                case BlameVerb.BrokePlank: return $"broke a plank (again){x}";
                case BlameVerb.TippedWagon: return $"tipped the wagon{x}";
                case BlameVerb.Drowned: return $"drowned (respawn fee){x}";
                case BlameVerb.KnockedOut: return $"got knocked out (respawn fee){x}";
                case BlameVerb.Stranded: return $"lost in the dark (respawn fee){x}";
                case BlameVerb.YeetedTeammate: return $"threw a teammate into the river{x}";
                case BlameVerb.LostTool: return $"lost the {item}{x}";
                case BlameVerb.LassoedIntoRiver: return $"lassoed the {item} into the river{x}";
                case BlameVerb.SpookedHerd: return $"screamed, animals fled{x}";
                case BlameVerb.SummonedCreature: return $"woke something in the dark{x}";
                case BlameVerb.Stolen: return $"left the {item} unguarded{x}";
                case BlameVerb.Pocketed: return $"pocketed the {item}{x}";
                case BlameVerb.LandlordFine: return $"got fined by the Landlord{x}";
                case BlameVerb.Rescue: return $"saved the {item}{x}";
                case BlameVerb.StealBack: return $"stole the {item} back{x}";
                default: return v.ToString() + x;
            }
        }

        /// <summary>"Livestock.Pig" → "pig", "Crop.PrizePumpkin" → "prize pumpkin"; falls back to the tag's last segment.</summary>
        public static string ItemName(string itemId, string itemTag)
        {
            string src = !string.IsNullOrEmpty(itemId) ? itemId : itemTag;
            if (string.IsNullOrEmpty(src)) return "thing";
            int dot = src.LastIndexOf('.');
            string last = dot >= 0 ? src.Substring(dot + 1) : src;
            var sb = new StringBuilder();
            for (int i = 0; i < last.Length; i++)
            {
                char c = last[i];
                if (i > 0 && char.IsUpper(c)) sb.Append(' ');
                sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString();
        }

        /// <summary>Culprit name for Nature events ("The Wind" from Weather.Wind).</summary>
        public static string NatureName(string causeTag)
        {
            if (string.IsNullOrEmpty(causeTag)) return "Nature";
            int dot = causeTag.LastIndexOf('.');
            string last = dot >= 0 ? causeTag.Substring(dot + 1) : causeTag;
            return "The " + last;
        }

        /// <summary>Sunday result line.</summary>
        public static string Sunday(bool paid, int carry)
        {
            if (!paid) return "EVICTED. The Landlord is towing the barn. The pig is on the roof.";
            if (carry == 0) return "Paid exactly. The Landlord seems disappointed.";
            return "PAID. The Landlord counts it twice, slowly.";
        }

        /// <summary>Order line label: "3 pigs".</summary>
        public static string OrderLabel(OrderLine l)
        {
            string n = ItemName(l.GoodsId, null);
            return l.Quantity > 1 ? $"{l.Quantity} {Plural(n)}" : $"1 {n}";
        }

        static string Plural(string n) => n.EndsWith("s") ? n : n + "s";
    }
}
