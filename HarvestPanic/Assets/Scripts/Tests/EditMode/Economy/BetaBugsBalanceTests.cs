using HarvestPanic.Core;
using HarvestPanic.Economy;
using NUnit.Framework;
using UnityEngine;

namespace HarvestPanic.Tests.EditMode.Economy
{
    /// <summary>
    /// fix/beta-bugs-1 regressions (docs/qa/playtest/bot-week-report.md): week-1 balance (GDD 9.3 / 9.11, tuned
    /// 2026-10-21, math in docs/economy/week1-income-model.md), spawn-settle bruise rule, spawn layout, Landlord lines.
    /// </summary>
    public class BetaBugsBalanceTests
    {
        EconomyTuning _t;
        [SetUp] public void SetUp() => _t = ScriptableObject.CreateInstance<EconomyTuning>();
        [TearDown] public void TearDown() => Object.DestroyImmediate(_t);

        [Test] public void Tuning_AssetMatchesCodeDefaults()
        {
            var asset = EconomyTuning.Load();
            Assert.That(asset, Is.Not.Null);
            Assert.AreEqual(2, EconomyMath.ExtraDailyGoods("Product.Crate", 2, asset), "asset: +2 crates per extra player");
            Assert.AreEqual(3f, asset.SpawnGraceSeconds);
            Assert.AreEqual(1200, EconomyMath.Quota(1, 4, asset), "rent formula is unchanged (supply was the problem)");
        }

        [Test] public void ExtraDailyCrates_ScaleWithCrew()
        {
            Assert.AreEqual(0, EconomyMath.ExtraDailyGoods("Product.Crate", 1, _t));
            Assert.AreEqual(2, EconomyMath.ExtraDailyGoods("Product.Crate", 2, _t));
            Assert.AreEqual(4, EconomyMath.ExtraDailyGoods("Product.Crate", 3, _t));
            Assert.AreEqual(6, EconomyMath.ExtraDailyGoods("Product.Crate", 4, _t));
            Assert.AreEqual(6, EconomyMath.ExtraDailyGoods("Product.Crate", 9, _t), "clamped to 4 players");
            Assert.AreEqual(0, EconomyMath.ExtraDailyGoods("Crop.PrizePumpkin", 4, _t), "the pumpkin stays one per day");
            Assert.AreEqual(0, EconomyMath.ExtraDailyGoods(null, 4, _t));
        }

        // ---- week-1 income model (docs/economy/week1-income-model.md) ----
        // Muddlebury, Region 1, week 1, D_day 1.0. Crates: 4 markers + crew extras per day, Scuffed, average product
        // freshness 0.9 over a 540 s workday, the first q(p) crates fill the order line (x1.5), saturation after 6 units.
        // Livestock is a one-time stock (3 pigs, 8 chickens, goat, cow), Scuffed, sold once. The pumpkin (8 hands) needs
        // 4 carriers: only 4p crews lift it; a pumpkin day also completes the order (+$60). One respawn per player.

        const float Fresh = 0.9f;
        int Crate(int unit, int players) =>
            EconomyMath.SaleValue(30, GoodsCategory.Product, BruiseState.Scuffed, Fresh, EconomyMath.Demand(1f, unit, 1f, _t),
                unit <= EconomyMath.OrderQuantity(2, players, 1, _t) ? _t.OrderMultiplier : 1f, 1f, _t);

        int CratesPerDay(int delivered, int players)
        {
            int sum = 0;
            for (int u = 1; u <= delivered; u++) sum += Crate(u, players);
            return sum;
        }

        int Livestock(int baseValue, int units)
        {
            int sum = 0;
            for (int u = 1; u <= units; u++)
                sum += EconomyMath.SaleValue(baseValue, GoodsCategory.Livestock, BruiseState.Scuffed, 1f, EconomyMath.Demand(1f, u, 1f, _t), 1f, 1f, _t);
            return sum;
        }

        int StockNoCow => Livestock(60, 3) + Livestock(15, 8) + Livestock(70, 1);
        int StockAll => StockNoCow + Livestock(150, 1);
        int PumpkinDay => EconomyMath.SaleValue(300, GoodsCategory.Crop, BruiseState.Scuffed, 1f, 1f, _t.OrderMultiplier, 1f, _t) + EconomyMath.OrderCompleteBonus(1, _t);
        int DailyCrates(int players) => 4 + EconomyMath.ExtraDailyGoods("Product.Crate", players, _t);

        int Week1(int players, int cratesPerDay, int stock, int pumpkinDays) =>
            _t.StartingBalance + _t.DaysPerWeek * CratesPerDay(cratesPerDay, players) + stock + pumpkinDays * PumpkinDay
            - players * EconomyMath.RespawnFee(1, 1f, _t, players);

        static int Competent(int dailyCrates) => Mathf.RoundToInt(dailyCrates * 0.8f);

        [Test] public void Week1_ModelInputs_MatchTheDoc()
        {
            Assert.AreEqual(34, Crate(1, 4), "order-line crate");
            Assert.AreEqual(23, Crate(3, 4), "plain crate");
            Assert.AreEqual(22, Crate(7, 4), "7th crate (saturation 0.95)");
            Assert.AreEqual(315, StockNoCow);
            Assert.AreEqual(443, StockAll);
            Assert.AreEqual(443, PumpkinDay, "$383 order-line pumpkin + $60 order bonus");
            Assert.AreEqual(new[] { 4, 6, 8, 10 }, new[] { DailyCrates(1), DailyCrates(2), DailyCrates(3), DailyCrates(4) });
        }

        [Test] public void Week1_Solo_PaysOnlyWithEffort()
        {
            int q = EconomyMath.Quota(1, 1, _t);
            Assert.AreEqual(600, Week1(1, 4, 0, 0));
            Assert.GreaterOrEqual(Week1(1, 4, 0, 0), q, "every crate every day pays");
            Assert.GreaterOrEqual(Week1(1, 3, StockNoCow, 0), q, "3 crates a day plus the pigs, chickens and goat pays");
            Assert.Less(Week1(1, 3, 0, 0), q, "3 crates a day and no livestock does not");
            Assert.Less(Week1(1, 2, 0, 0), q, "a casual solo week (2 crates a day) is evicted");
        }

        [Test] public void Week1_Crews_PayWithSlack_IncludingLivestock()
        {
            // 2p can't lift the cow (6 hands) or the pumpkin (8); 3p can lift the cow.
            Assert.AreEqual(1005, Week1(2, Competent(DailyCrates(2)), StockNoCow, 0));
            Assert.AreEqual(1268, Week1(3, Competent(DailyCrates(3)), StockAll, 0));
            Assert.AreEqual(1458, Week1(4, Competent(DailyCrates(4)), StockAll, 0));
            for (int p = 2; p <= 4; p++)
            {
                int week = Week1(p, Competent(DailyCrates(p)), p == 2 ? StockNoCow : StockAll, 0);
                Assert.GreaterOrEqual(week, EconomyMath.Quota(1, p, _t) * 1.2f, $"{p}p: 80% of crates + livestock leaves >= 20% slack");
            }
            Assert.GreaterOrEqual(Week1(4, Competent(DailyCrates(4)), StockAll, 1), EconomyMath.Quota(1, 4, _t) * 1.5f, "one pumpkin day: >= 50% slack");
        }

        [Test] public void Week1_OldSupply_WasShort()
        {
            // The bug: 4 crates a day whatever the crew size. 3p/4p crews couldn't pay even with every crate and all livestock.
            Assert.Less(Week1(4, 4, StockAll, 0), EconomyMath.Quota(1, 4, _t));
            Assert.Less(Week1(3, 4, StockAll, 0), EconomyMath.Quota(1, 3, _t) * 1.05f);
        }

        // ---- spawn settle / layout ----

        [Test] public void SpawnSettle_GraceOnlyUntilTouchedOrExpired()
        {
            Assert.IsTrue(Goods.IgnoreSettleImpact(false, 0.1f, 3f), "untouched, inside the grace");
            Assert.IsFalse(Goods.IgnoreSettleImpact(false, 3.5f, 3f), "after the grace, impacts bruise again");
            Assert.IsFalse(Goods.IgnoreSettleImpact(true, 0.1f, 3f), "a grab/throw/touch ends the grace early");
        }

        [Test] public void SpawnCandidates_StartAtTheMarker_AndNeverOverlapEachOther()
        {
            Assert.AreEqual(Vector3.zero, EconomyDirector.SpawnCandidateOffset(0, 0.6f));
            const float step = 0.6f;
            for (int a = 0; a < 32; a++)
            for (int b = a + 1; b < 32; b++)
            {
                var pa = EconomyDirector.SpawnCandidateOffset(a, step);
                var pb = EconomyDirector.SpawnCandidateOffset(b, step);
                Assert.AreEqual(0f, pa.y);
                if (a == 0) Assert.GreaterOrEqual(pb.magnitude, step * 0.99f, $"candidate {b} clears the marker spot");
            }
        }

        // ---- Landlord lines ----

        [Test] public void LandlordBarkLines()
        {
            StringAssert.Contains("$540", LandlordText.WeekOneWelcome(540));
            StringAssert.Contains("Quiet Hours", LandlordText.NewRule("Quiet Hours: voices carry further. Whisper.", "Mod.Landlord.QuietHours"));
            StringAssert.Contains("QuietHours", LandlordText.NewRule(null, "Mod.Landlord.QuietHours"));
        }
    }
}
