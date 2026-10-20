using System;
using System.Collections.Generic;
using HarvestPanic.Core;
using UnityEngine;

namespace HarvestPanic.Economy
{
    /// <summary>
    /// Landlord parameter and tag names (landlord-events §3). Economy owns and consumes all of them.
    /// </summary>
    public static class LandlordParams
    {
        /// <summary>Sunday Best: value multiplier for livestock sold without a hat (no hats exist yet, so all livestock).</summary>
        public const string HatlessLivestockValueMult = "Landlord.HatlessLivestockValueMult";
        /// <summary>Helpful Visit: dollars charged at Payout as a "service fee".</summary>
        public const string ServiceFee = "Landlord.ServiceFee";
        /// <summary>Muddy Boots rule active: the muddy check fines per muddy player instead of once.</summary>
        public const string TagMuddyBootsRule = "World.Landlord.MuddyBootsRule";
        /// <summary>One Pumpkin Per Family active (order lines are distinct by construction; adds a line).</summary>
        public const string TagOnePerFamily = "World.Landlord.OnePerFamily";
        /// <summary>An Inspection visit is scheduled today.</summary>
        public const string TagInspection = "World.Landlord.Inspection";
        /// <summary>The Landlord is on the farm today (any visit). Presentation / AI hook.</summary>
        public const string TagVisiting = "World.Landlord.Visiting";
        /// <summary>Weather tag the muddy check reads (granted by Rain and Storm).</summary>
        public const string TagRain = "World.Rain";
        /// <summary>Id prefix for weekly rule modifiers.</summary>
        public const string RulePrefix = "Mod.Landlord.";
        /// <summary>Id prefix for visit modifiers.</summary>
        public const string VisitPrefix = "Mod.Landlord.Visit.";

        /// <summary>True for a visit modifier (Major, Day duration).</summary>
        public static bool IsVisit(DayModifier m) => m != null && m.Category == DayModifierCategory.Landlord && m.ModifierId.StartsWith(VisitPrefix, StringComparison.Ordinal);
        /// <summary>True for a weekly rule (Run duration, drawn after paid Sundays).</summary>
        public static bool IsRule(DayModifier m) => m != null && m.Category == DayModifierCategory.Landlord && m.Duration == DayModifierDuration.Run && !IsVisit(m);
    }

    /// <summary>The four Inspection checks (landlord-events §3 Visits).</summary>
    public enum InspectionCheck : byte { MuddyBoots, LooseLivestock, BrokenFence, BruisedOrderItem }

    /// <summary>World state the inspector looks at, gathered by the director (pure input so EditMode can test it).</summary>
    public struct InspectionSnapshot
    {
        public int MuddyPlayers;
        public int LooseLivestock;
        public bool BrokePlankToday;
        public int BruisedOrderItems;
        public bool MuddyBootsRule;
    }

    /// <summary>Outcome of one inspection.</summary>
    public sealed class InspectionResult
    {
        public readonly List<InspectionCheck> Checks = new List<InspectionCheck>();
        public readonly List<InspectionCheck> Failed = new List<InspectionCheck>();
        /// <summary>Failed "units" (muddy check counts per player under Muddy Boots).</summary>
        public int FailedUnits;
        /// <summary>Dollars fined (already capped by the weekly cap).</summary>
        public int Fine;
    }

    /// <summary>Pure Landlord formulas and scheduling (landlord-events §3–4). No Unity state.</summary>
    public static class LandlordMath
    {
        /// <summary>Fine per failed check in week w: 25 × (1 + 0.2 (w − 2)), half-up, never below the base.</summary>
        public static int InspectionFinePerCheck(int week, EconomyTuning t) =>
            Math.Max(t.InspectionFinePerCheck, EconomyMath.RoundHalfUp(t.InspectionFinePerCheck * (1.0 + t.InspectionFineWeeklyGrowth * (week - 2))));

        /// <summary>Fine = min(cap, failed × perCheck(w)); also limited by what is left of the weekly cap.</summary>
        public static int InspectionFine(int failedUnits, int week, EconomyTuning t, int alreadyFinedThisWeek = 0)
        {
            int raw = Math.Max(0, failedUnits) * InspectionFinePerCheck(week, t);
            int capLeft = Math.Max(0, t.InspectionCap - Math.Max(0, alreadyFinedThisWeek));
            return Math.Min(Math.Min(t.InspectionCap, raw), capLeft);
        }

        /// <summary>Farm Walk: $50 × hits, at most 3 hits counted.</summary>
        public static int FarmWalkFine(int hits, EconomyTuning t) => Math.Min(Math.Max(0, hits), t.FarmWalkMaxHits) * t.FarmWalkFinePerHit;

        /// <summary>Visits per week: 0 in W1, 1 in W2–3, 2 from W4.</summary>
        public static int VisitsPerWeek(int week, EconomyTuning t) =>
            week < 2 ? 0 : week < 4 ? t.VisitsPerWeekEarly : t.VisitsPerWeekLate;

        /// <summary>
        /// Plans this week's visits: distinct days in Tue..Thu (dayOfWeek 2..4, clamped to the week length), visit ids
        /// drawn from <paramref name="pool"/> (repeats allowed across days only when the pool is smaller than the count).
        /// </summary>
        public static List<(int dayOfWeek, string visitId)> PlanVisits(int week, IReadOnlyList<string> pool, System.Random rng, EconomyTuning t)
        {
            var plan = new List<(int, string)>();
            int n = VisitsPerWeek(week, t);
            if (n <= 0 || pool == null || pool.Count == 0) return plan;
            var days = new List<int>();
            int last = Math.Min(4, Math.Max(2, t.DaysPerWeek - 1));
            for (int d = 2; d <= last; d++) days.Add(d);
            var ids = new List<string>(pool);
            for (int i = 0; i < n && days.Count > 0; i++)
            {
                int di = rng.Next(days.Count);
                int day = days[di]; days.RemoveAt(di);
                if (ids.Count == 0) ids.AddRange(pool);
                int vi = rng.Next(ids.Count);
                plan.Add((day, ids[vi])); ids.RemoveAt(vi);
            }
            plan.Sort((a, b) => a.Item1.CompareTo(b.Item1));
            return plan;
        }

        /// <summary>Picks <c>InspectionChecksPicked</c> checks out of the four and scores the snapshot.</summary>
        public static InspectionResult Inspect(in InspectionSnapshot s, int week, System.Random rng, EconomyTuning t, int alreadyFinedThisWeek = 0)
        {
            var r = new InspectionResult();
            var all = new List<InspectionCheck> { InspectionCheck.MuddyBoots, InspectionCheck.LooseLivestock, InspectionCheck.BrokenFence, InspectionCheck.BruisedOrderItem };
            int pick = Mathf.Clamp(t.InspectionChecksPicked, 1, all.Count);
            for (int i = 0; i < pick; i++) { int k = rng.Next(all.Count); r.Checks.Add(all[k]); all.RemoveAt(k); }
            foreach (var c in r.Checks)
            {
                int units = FailedUnits(c, s);
                if (units <= 0) continue;
                r.Failed.Add(c);
                r.FailedUnits += units;
            }
            r.Fine = InspectionFine(r.FailedUnits, week, t, alreadyFinedThisWeek);
            return r;
        }

        /// <summary>Failed units for one check (Muddy Boots rule: one per muddy player).</summary>
        public static int FailedUnits(InspectionCheck c, in InspectionSnapshot s)
        {
            switch (c)
            {
                case InspectionCheck.MuddyBoots: return s.MuddyPlayers <= 0 ? 0 : s.MuddyBootsRule ? s.MuddyPlayers : 1;
                case InspectionCheck.LooseLivestock: return s.LooseLivestock > 0 ? 1 : 0;
                case InspectionCheck.BrokenFence: return s.BrokePlankToday ? 1 : 0;
                default: return s.BruisedOrderItems > 0 ? 1 : 0;
            }
        }
    }

    /// <summary>
    /// Landlord copy (GDD §11.3.1 voice samples are tone direction; these are the build's lines). One table so it can
    /// move to a StringTable.
    /// </summary>
    public static class LandlordText
    {
        public const string Name = "The Landlord";
        public const string InspectionArrive = "Don't mind me. Just a friendly little look around.";
        public const string InspectionClean = "Spotless! How... unusual. No fine today.";
        public const string HelpfulArrive = "I've taken the liberty of fixing your bridge. Small fee, of course.";
        public const string MissedAppointment = "You missed our appointment. Fifty dollars for my time.";
        public const string SundayArrive = "Good morning! Rent day. My favourite day.";
        public const string SundayCount = "Let's see... one... two... (abacus clicks)";
        public const string SundayPaid = "Splendid! Same time next week. Slightly more, of course.";
        public const string SundayExact = "Exactly enough. How... disappointing.";
        public const string SundayShort = "Hmm. Short. Let's talk about collateral.";
        public const string SundayEvicted = "Oh dear. Oh dear, oh dear. Pack light.";

        /// <summary>One line per failed check, voiced.</summary>
        public static string CheckLine(InspectionCheck c, int perCheck)
        {
            switch (c)
            {
                case InspectionCheck.MuddyBoots: return $"Lovely boots. {perCheck} dollars.";
                case InspectionCheck.LooseLivestock: return "What a handsome animal. Loose, I'm afraid.";
                case InspectionCheck.BrokenFence: return "Is that a hole in my bridge? I'll add it to the account.";
                default: return "This one's bruised. The customer will be devastated.";
            }
        }

        /// <summary>Receipt label for a failed check.</summary>
        public static string CheckName(InspectionCheck c)
        {
            switch (c)
            {
                case InspectionCheck.MuddyBoots: return "muddy boots";
                case InspectionCheck.LooseLivestock: return "loose livestock";
                case InspectionCheck.BrokenFence: return "broken plank";
                default: return "bruised order item";
            }
        }
    }
}
