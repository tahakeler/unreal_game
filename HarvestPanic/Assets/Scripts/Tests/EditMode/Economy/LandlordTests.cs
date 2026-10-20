using System.Collections.Generic;
using System.Linq;
using HarvestPanic.Core;
using HarvestPanic.Economy;
using NUnit.Framework;
using UnityEngine;

namespace HarvestPanic.Tests.EditMode.Economy
{
    /// <summary>landlord-events rules data + formulas, respawn fee player scaling (GDD §9 issue 5), awards selection, Sunday visit timeline.</summary>
    public class LandlordTests
    {
        EconomyTuning _t;
        [SetUp] public void SetUp() => _t = ScriptableObject.CreateInstance<EconomyTuning>();
        [TearDown] public void TearDown() => Object.DestroyImmediate(_t);

        static List<DayModifier> Landlord() => Resources.LoadAll<DayModifier>("Data/DayModifiers").Where(m => m.Category == DayModifierCategory.Landlord).ToList();

        [Test] public void AllEightWeeklyRules_AreAuthored_AsMinorRunModifiers()
        {
            var rules = Landlord().Where(LandlordParams.IsRule).ToList();
            Assert.AreEqual(8, rules.Count, string.Join(",", rules.Select(r => r.ModifierId)));
            foreach (var r in rules)
            {
                Assert.AreEqual(DayModifierWeight.Minor, r.Weight, r.ModifierId);
                Assert.IsFalse(string.IsNullOrEmpty(r.CardText), r.ModifierId);
            }
            Assert.AreEqual(rules.Count, rules.Select(r => r.ModifierId).Distinct().Count());
        }

        [Test] public void Visits_AreMajorDayModifiers()
        {
            var visits = Landlord().Where(LandlordParams.IsVisit).ToList();
            CollectionAssert.IsSubsetOf(new[] { "Mod.Landlord.Visit.Inspection", "Mod.Landlord.Visit.HelpfulVisit" }, visits.Select(v => v.ModifierId).ToList());
            foreach (var v in visits) { Assert.AreEqual(DayModifierWeight.Major, v.Weight); Assert.AreEqual(DayModifierDuration.Day, v.Duration); }
        }

        [Test] public void RuleEffects_ResolveThroughStack()
        {
            var all = Landlord().ToDictionary(m => m.ModifierId);
            var s = new DayModifierStack();
            s.Apply(all["Mod.Landlord.SundayBest"], 2, true);
            s.Apply(all["Mod.Landlord.OnePumpkinPerFamily"], 2, true);
            s.Apply(all["Mod.Landlord.MuddyBoots"], 2, true);
            Assert.AreEqual(0.9f, s.Resolve(LandlordParams.HatlessLivestockValueMult, 1f), 1e-4);
            Assert.AreEqual(4f, s.Resolve(DayModifierStack.Params.OrderLineCount, 3f), 1e-4);
            Assert.IsTrue(s.HasWorldTag(LandlordParams.TagMuddyBootsRule));
            var h = new DayModifierStack();
            h.Apply(all["Mod.Landlord.Visit.HelpfulVisit"], 2);
            Assert.AreEqual(100f, h.Resolve(LandlordParams.ServiceFee, 0f), 1e-4);
            Assert.AreEqual(0.5f, h.Resolve(DayParams.BridgePlankBreakMult, 1f), 1e-4);
        }

        [Test] public void InspectionFine_MatchesAcceptanceCriteria()
        {
            Assert.AreEqual(25, LandlordMath.InspectionFinePerCheck(2, _t));
            Assert.AreEqual(35, LandlordMath.InspectionFinePerCheck(4, _t));
            Assert.AreEqual(105, LandlordMath.InspectionFine(3, 4, _t));         // AC3
            Assert.AreEqual(150, LandlordMath.InspectionFine(9, 4, _t));         // cap
            Assert.AreEqual(50, LandlordMath.InspectionFine(3, 4, _t, 100));     // weekly cap left
            Assert.AreEqual(150, LandlordMath.FarmWalkFine(4, _t));              // AC4
        }

        [Test] public void MuddyBootsRule_FinesPerPlayer()
        {
            var s = new InspectionSnapshot { MuddyPlayers = 3 };
            Assert.AreEqual(1, LandlordMath.FailedUnits(InspectionCheck.MuddyBoots, s));
            s.MuddyBootsRule = true;
            Assert.AreEqual(3, LandlordMath.FailedUnits(InspectionCheck.MuddyBoots, s));
            _t.InspectionChecksPicked = 4;
            var r = LandlordMath.Inspect(new InspectionSnapshot { MuddyPlayers = 2, MuddyBootsRule = true, BrokePlankToday = true }, 2, new System.Random(1), _t);
            Assert.AreEqual(3, r.FailedUnits);
            Assert.AreEqual(75, r.Fine);
        }

        [Test] public void VisitPlan_Week1None_Week2One_Week4TwoOnDistinctMidweekDays()
        {
            var pool = new[] { "A", "B" };
            for (int seed = 0; seed < 20; seed++)
            {
                Assert.AreEqual(0, LandlordMath.PlanVisits(1, pool, new System.Random(seed), _t).Count);
                var w2 = LandlordMath.PlanVisits(2, pool, new System.Random(seed), _t);
                Assert.AreEqual(1, w2.Count);
                var w4 = LandlordMath.PlanVisits(4, pool, new System.Random(seed), _t);
                Assert.AreEqual(2, w4.Count);
                Assert.AreNotEqual(w4[0].dayOfWeek, w4[1].dayOfWeek);
                foreach (var v in w4) Assert.That(v.dayOfWeek, Is.InRange(2, 4));
            }
        }

        [Test] public void RespawnFee_ScalesBySqrtPlayerScale()
        {
            Assert.AreEqual(25, EconomyMath.RespawnFee(1, 1f, _t, 4));
            Assert.AreEqual(25, EconomyMath.RespawnFee(1, 1f, _t, 3));
            Assert.AreEqual(20, EconomyMath.RespawnFee(1, 1f, _t, 2));
            Assert.AreEqual(15, EconomyMath.RespawnFee(1, 1f, _t, 1));
            Assert.AreEqual(40, EconomyMath.RespawnFee(3, 1f, _t, 4));
            Assert.AreEqual(25, EconomyMath.RespawnFee(3, 1f, _t, 1));   // 40 × 0.671 = 26.8 → 25
            Assert.AreEqual(45, EconomyMath.RespawnFee(3, 1.2f, _t, 4)); // GDD worked example unchanged at 4p
            _t.RespawnFeePlayerExponent = 0f;
            Assert.AreEqual(25, EconomyMath.RespawnFee(1, 1f, _t, 1));
        }

        static LedgerEntry E(ulong who, BlameVerb v, int dollars, int count = 1) =>
            new LedgerEntry { Event = new BlameEvent { Instigators = new[] { who }, PrimaryInstigator = who, Verb = v, DollarDelta = dollars }, Count = count };

        [Test] public void DailyAwards_PickFunnyTitles_MvdFirst()
        {
            var day = new List<LedgerEntry>
            {
                E(0, BlameVerb.BrokePlank, -30, 2), E(1, BlameVerb.DroppedInRiver, -210), E(2, BlameVerb.Rescue, 30), E(0, BlameVerb.Dropped, -5, 3),
            };
            var a = AwardPicker.Daily(day, id => "P" + id, 3, 3);
            Assert.AreEqual(3, a.Count);
            Assert.AreEqual(AwardPicker.Mvd, a[0].Title); Assert.AreEqual("P1", a[0].Name);
            Assert.AreEqual(AwardPicker.PigWhisperer, a[1].Title); Assert.AreEqual("P2", a[1].Name);
            Assert.AreEqual(AwardPicker.BridgeInspector, a[2].Title); Assert.AreEqual("P0", a[2].Name); // 2 planks beats 1 river
        }

        [Test] public void DailyAwards_Solo_OnlyMvdAndPigWhisperer()
        {
            var day = new List<LedgerEntry> { E(0, BlameVerb.BrokePlank, -30), E(0, BlameVerb.Rescue, 20) };
            var a = AwardPicker.Daily(day, id => "Solo", 1, 5);
            CollectionAssert.AreEqual(new[] { AwardPicker.Mvd, AwardPicker.PigWhisperer }, a.Select(x => x.Title).ToArray());
            StringAssert.Contains("only candidate", a[0].Reason);
        }

        [Test] public void DailyAwards_TieGoesToEarlierEvent_EmptyDayNoAwards()
        {
            var day = new List<LedgerEntry> { E(3, BlameVerb.Drowned, -25), E(1, BlameVerb.Drowned, -25) };
            Assert.AreEqual("P3", AwardPicker.Daily(day, id => "P" + id, 4, 1)[0].Name);
            Assert.AreEqual(0, AwardPicker.Daily(new List<LedgerEntry>(), id => "", 4, 3).Count);
        }

        [Test] public void VisitScript_Timeline_PaidAndEvicted()
        {
            var s = new LandlordVisitScript(1f, 1f, 1f);
            Assert.AreEqual(LandlordVisitState.Walking, s.State);
            s.Arrived(); Assert.AreEqual(LandlordVisitState.Greeting, s.State);
            s.Tick(1.1f); Assert.AreEqual(LandlordVisitState.Counting, s.State);
            s.Tick(1.1f); Assert.AreEqual(LandlordVisitState.AwaitVerdict, s.State);
            s.SetVerdict(LandlordVerdict.Collateral); Assert.AreEqual(LandlordVisitState.Verdict, s.State);
            s.Tick(5f); Assert.AreEqual(LandlordVisitState.Verdict, s.State, "collateral waits");
            s.SetVerdict(LandlordVerdict.Evicted); Assert.AreEqual("EVICTED", s.Stamp);
            Assert.AreEqual(LandlordText.SundayEvicted, s.Line);
            s.Tick(1.1f); Assert.AreEqual(LandlordVisitState.Done, s.State);
            s.SetVerdict(LandlordVerdict.Paid); Assert.AreEqual(LandlordVerdict.Evicted, s.Verdict, "final verdicts are final");

            var p = new LandlordVisitScript(0.1f, 0.1f, 0.1f);
            p.SetVerdict(LandlordVerdict.Paid); p.Arrived(); p.Tick(0.2f); p.Tick(0.2f);
            Assert.AreEqual(LandlordVisitState.Verdict, p.State); Assert.AreEqual("PAID", p.Stamp);
        }
    }
}
