using HarvestPanic.Core;
using HarvestPanic.Economy;
using NUnit.Framework;
using UnityEngine;

namespace HarvestPanic.Tests.EditMode.Economy
{
    /// <summary>
    /// fix/beta-bugs-1 regressions (docs/qa/playtest/bot-week-report.md): week-1 balance (GDD 9.7 / 9.11, tuned
    /// 2026-10-21), spawn-settle bruise rule, Landlord bark lines.
    /// </summary>
    public class BetaBugsBalanceTests
    {
        EconomyTuning _t;
        [SetUp] public void SetUp() => _t = ScriptableObject.CreateInstance<EconomyTuning>();
        [TearDown] public void TearDown() => Object.DestroyImmediate(_t);

        [Test] public void Quota_Week1_LearningWeekRelief_LaterWeeksUnchanged()
        {
            Assert.AreEqual(new[] { 460, 660, 870, 1020 }, new[] { EconomyMath.Quota(1, 1, _t), EconomyMath.Quota(1, 2, _t), EconomyMath.Quota(1, 3, _t), EconomyMath.Quota(1, 4, _t) });
            Assert.AreEqual(1620, EconomyMath.Quota(2, 4, _t));
            Assert.AreEqual(730, EconomyMath.Quota(2, 1, _t));
            _t.QuotaWeekOneMult = 1f;
            Assert.AreEqual(540, EconomyMath.Quota(1, 1, _t), "mult 1 restores the untuned GDD table");
        }

        [Test] public void ExtraDailyCrates_ScaleWithCrew()
        {
            Assert.AreEqual(0, EconomyMath.ExtraDailyGoods("Product.Crate", 1, _t));
            Assert.AreEqual(1, EconomyMath.ExtraDailyGoods("Product.Crate", 2, _t));
            Assert.AreEqual(3, EconomyMath.ExtraDailyGoods("Product.Crate", 4, _t));
            Assert.AreEqual(3, EconomyMath.ExtraDailyGoods("Product.Crate", 9, _t), "clamped to 4 players");
            Assert.AreEqual(0, EconomyMath.ExtraDailyGoods("Crop.PrizePumpkin", 4, _t));
        }

        /// <summary>
        /// The income model written in GDD 9.11 (tuned 2026-10-21) for Muddlebury week 1, Region 1, base demand,
        /// every unit Scuffed (x0.85), no order multiplier / order bonus / dusk bonus (all upside):
        /// crates 4 + (p-1) per day at $26, prize pumpkin $255 per sale, starter livestock stock (3 pigs, 8 chickens,
        /// cow, goat) sold once per run.
        /// </summary>
        [Test] public void Week1_IncomeModel_PaysRentWithSlack()
        {
            int crate = EconomyMath.SaleValue(30, GoodsCategory.Product, BruiseState.Scuffed, 1f, 1f, 1f, 1f, _t);
            int pumpkin = EconomyMath.SaleValue(300, GoodsCategory.Crop, BruiseState.Scuffed, 1f, 1f, 1f, 1f, _t);
            int pig = EconomyMath.SaleValue(60, GoodsCategory.Livestock, BruiseState.Scuffed, 1f, 1f, 1f, 1f, _t);
            int chicken = EconomyMath.SaleValue(15, GoodsCategory.Livestock, BruiseState.Scuffed, 1f, 1f, 1f, 1f, _t);
            int cow = EconomyMath.SaleValue(150, GoodsCategory.Livestock, BruiseState.Scuffed, 1f, 1f, 1f, 1f, _t);
            int goat = EconomyMath.SaleValue(70, GoodsCategory.Livestock, BruiseState.Scuffed, 1f, 1f, 1f, 1f, _t);
            Assert.AreEqual(26, crate); Assert.AreEqual(255, pumpkin);
            int start = _t.StartingBalance, days = _t.DaysPerWeek;
            int Crates(int p) => (4 + EconomyMath.ExtraDailyGoods("Product.Crate", p, _t)) * crate * days;

            // Solo "with effort": every crate every day (20 hauls of ~126 m) + 6 chickens, no pumpkin, no pigs.
            int solo = start + Crates(1) + 6 * chicken;
            Assert.GreaterOrEqual(solo, EconomyMath.Quota(1, 1, _t), "solo pays W1 with effort");
            Assert.Less(start + Crates(1) * 0.6f, EconomyMath.Quota(1, 1, _t), "solo must actually work (60% of crates is not enough)");

            // 2p competent: 80% of crates, all pigs + chickens + goat, pumpkin twice in the week.
            int duo = start + Mathf.RoundToInt(Crates(2) * 0.8f) + 3 * pig + 8 * chicken + goat + 2 * pumpkin;
            Assert.GreaterOrEqual(duo, EconomyMath.Quota(1, 2, _t) * 1.25f, "2p slack >= 25%");

            // 4p competent: 80% of crates, all starter livestock, pumpkin 3 of 5 days.
            int quad = start + Mathf.RoundToInt(Crates(4) * 0.8f) + 3 * pig + 8 * chicken + cow + goat + 3 * pumpkin;
            Assert.GreaterOrEqual(quad, EconomyMath.Quota(1, 4, _t) * 1.25f, "4p slack >= 25%");
            // ...and a 4p crew that never moves the pumpkin can still pay (supply no longer caps them).
            int quadNoPumpkin = start + Crates(4) + 3 * pig + 8 * chicken + cow + goat;
            Assert.GreaterOrEqual(quadNoPumpkin, EconomyMath.Quota(1, 4, _t));
        }

        [Test] public void SpawnSettle_IgnoresWorldImpactsUntilHandled()
        {
            Assert.IsTrue(Goods.IgnoreSettleImpact(false, 0.1f, 3f, true), "any impact inside the spawn grace");
            Assert.IsTrue(Goods.IgnoreSettleImpact(false, 30f, 3f, false), "untouched goods settling against the ground");
            Assert.IsFalse(Goods.IgnoreSettleImpact(false, 30f, 3f, true), "a moving body hitting untouched goods still bruises");
            Assert.IsFalse(Goods.IgnoreSettleImpact(true, 0.1f, 3f, false), "once handled, drops bruise normally");
        }

        [Test] public void LandlordBarkLines()
        {
            StringAssert.Contains("$460", LandlordText.WeekOneWelcome(460));
            StringAssert.Contains("Quiet Hours", LandlordText.NewRule("Quiet Hours: voices carry further. Whisper.", "Mod.Landlord.QuietHours"));
            StringAssert.Contains("QuietHours", LandlordText.NewRule(null, "Mod.Landlord.QuietHours"));
        }
    }
}
