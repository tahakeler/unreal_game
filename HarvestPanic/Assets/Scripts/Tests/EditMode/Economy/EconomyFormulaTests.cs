using System.Collections.Generic;
using HarvestPanic.Core;
using HarvestPanic.Economy;
using NUnit.Framework;
using UnityEngine;

namespace HarvestPanic.Tests.EditMode.Economy
{
    /// <summary>GDD formula pins: goods-value, shared-wallet-economy, week-quota, day-loop, blame-ledger acceptance criteria.</summary>
    public class EconomyFormulaTests
    {
        EconomyTuning _t;

        [SetUp] public void SetUp() => _t = ScriptableObject.CreateInstance<EconomyTuning>();
        [TearDown] public void TearDown() => Object.DestroyImmediate(_t);

        // ---- goods-value ----
        [Test] public void BruiseGain_PigDrop1200_Is16BpScuffed()
        {
            float bp = EconomyMath.BruiseGain(1200f, 0.8f, 1f, _t);
            Assert.AreEqual(16f, bp, 0.1f);
            Assert.AreEqual(BruiseState.Scuffed, EconomyMath.StateFor(bp, _t));
        }

        [Test] public void BruiseGain_BelowThreshold_SolverSpike_AndPerHitCap()
        {
            Assert.AreEqual(0f, EconomyMath.BruiseGain(399f, 2f, 1f, _t));
            Assert.AreEqual(0f, EconomyMath.BruiseGain(25000f, 2f, 1f, _t));
            Assert.AreEqual(60f, EconomyMath.BruiseGain(3000f, 1.5f, 1f, _t), 1e-3); // 97.5 capped
        }

        [TestCase(0f, BruiseState.Pristine)] [TestCase(9.9f, BruiseState.Pristine)] [TestCase(10f, BruiseState.Scuffed)]
        [TestCase(34.9f, BruiseState.Scuffed)] [TestCase(35f, BruiseState.Bruised)] [TestCase(64.9f, BruiseState.Bruised)]
        [TestCase(65f, BruiseState.Busted)] [TestCase(99.9f, BruiseState.Busted)] [TestCase(100f, BruiseState.Ruined)]
        public void BruiseTiers(float bp, BruiseState expected) => Assert.AreEqual(expected, EconomyMath.StateFor(bp, _t));

        [Test] public void BruiseMultipliers()
        {
            Assert.AreEqual(1f, EconomyMath.BruiseMult(BruiseState.Pristine, GoodsCategory.Crop, _t));
            Assert.AreEqual(0.85f, EconomyMath.BruiseMult(BruiseState.Scuffed, GoodsCategory.Crop, _t));
            Assert.AreEqual(0.6f, EconomyMath.BruiseMult(BruiseState.Bruised, GoodsCategory.Crop, _t));
            Assert.AreEqual(0.3f, EconomyMath.BruiseMult(BruiseState.Busted, GoodsCategory.Crop, _t));
            Assert.AreEqual(0f, EconomyMath.BruiseMult(BruiseState.Ruined, GoodsCategory.Crop, _t));
            Assert.AreEqual(0.25f, EconomyMath.BruiseMult(BruiseState.Ruined, GoodsCategory.Livestock, _t));
        }

        [Test] public void Freshness_CropCurve()
        {
            Assert.AreEqual(1f, EconomyMath.FreshnessFor(GoodsCategory.Crop, 100f, _t), 1e-4);
            Assert.AreEqual(0.75f, EconomyMath.FreshnessFor(GoodsCategory.Crop, 240f, _t), 1e-4);
            Assert.AreEqual(0.5f, EconomyMath.FreshnessFor(GoodsCategory.Crop, 600f, _t), 1e-4);
            Assert.AreEqual(0.5f, EconomyMath.FreshnessFor(GoodsCategory.Crop, 900f, _t), 1e-4);
            Assert.AreEqual(1f, EconomyMath.FreshnessFor(GoodsCategory.Livestock, 9000f, _t));
        }

        [Test] public void SaleValue_PristinePumpkinOnOrder_Is450()
        {
            Assert.AreEqual(450, EconomyMath.SaleValue(300, GoodsCategory.Crop, BruiseState.Pristine, 1f, 1f, 1.5f, 1f, _t));
        }

        [Test] public void SaleValue_RuinedCropIsCompost_RuinedLivestockFloor()
        {
            Assert.AreEqual(1, EconomyMath.SaleValue(300, GoodsCategory.Crop, BruiseState.Ruined, 1f, 1f, 1f, 1f, _t));
            Assert.AreEqual(15, EconomyMath.SaleValue(60, GoodsCategory.Livestock, BruiseState.Ruined, 1f, 1f, 1f, 1f, _t));
            Assert.AreEqual(135, EconomyMath.SaleValue(300, GoodsCategory.Crop, BruiseState.Busted, 1f, 1f, 1.5f, 1f, _t));
        }

        [Test] public void BruiseDollarLoss_UsesExpectedDemand()
        {
            Assert.AreEqual(9, EconomyMath.BruiseDollarLoss(60, 1f, 1f, 0.85f));
            Assert.AreEqual(0, EconomyMath.BruiseDollarLoss(60, 1f, 0.85f, 0.85f));
        }

        // ---- market ----
        [Test] public void Demand_Saturation()
        {
            Assert.AreEqual(1f, EconomyMath.Demand(1f, 6, 1f, _t), 1e-4);
            Assert.AreEqual(0.95f, EconomyMath.Demand(1f, 7, 1f, _t), 1e-4);
            Assert.AreEqual(0.6f, EconomyMath.Demand(1f, 20, 1f, _t), 1e-4);
        }

        // ---- wallet ----
        [Test] public void RespawnFeeTable()
        {
            int[] expected = { 25, 30, 40, 45, 50, 55 };
            for (int w = 1; w <= 6; w++) Assert.AreEqual(expected[w - 1], EconomyMath.RespawnFee(w, 1f, _t), "week " + w);
        }

        [Test] public void WalletFloor_OverflowGoesToIou()
        {
            var wallet = new Wallet(_t);
            wallet.Apply(new WalletTxn { Type = WalletTxnType.Fine, Amount = -280 }, out _);
            Assert.AreEqual(-180, wallet.Balance);
            wallet.Apply(new WalletTxn { Type = WalletTxnType.RespawnFee, Amount = -40 }, out var applied);
            Assert.AreEqual(-200, wallet.Balance);
            Assert.AreEqual(-20, applied.Amount);
            Assert.AreEqual(20, wallet.IouThisWeek);
        }

        [Test] public void Wallet_TheftNeverBelowZero_UpgradeNeedsCash_AuditSums()
        {
            var wallet = new Wallet(_t);
            Assert.IsTrue(wallet.Apply(new WalletTxn { Type = WalletTxnType.Theft, Amount = -150 }, out var theft));
            Assert.AreEqual(-100, theft.Amount);
            Assert.AreEqual(0, wallet.Balance);
            Assert.IsFalse(wallet.Apply(new WalletTxn { Type = WalletTxnType.Upgrade, Amount = -25 }, out _));
            wallet.Apply(new WalletTxn { Type = WalletTxnType.Sale, Amount = 450 }, out _);
            int sum = 0; foreach (var t in wallet.History) sum += t.Amount;
            Assert.AreEqual(wallet.Balance - wallet.StartingBalance, sum);
        }

        // ---- quota ----
        [Test] public void QuotaTable()
        {
            int[,] table = { { 1020, 870, 660, 460 }, // W1 x QuotaWeekOneMult 0.85 (tuned 2026-10-21)
                            { 1620, 1380, 1050, 730 }, { 2190, 1860, 1420, 980 }, { 2950, 2510, 1920, 1330 }, { 3990, 3390, 2590, 1790 } };
            for (int w = 1; w <= 5; w++)
                for (int p = 4; p >= 1; p--)
                    Assert.AreEqual(table[w - 1, 4 - p], EconomyMath.Quota(w, p, _t), $"w{w} {p}p");
        }

        [Test] public void Quota_ProrateLeaverAfterDay2()
        {
            int q = EconomyMath.Quota(3, 4, _t);
            Assert.AreEqual(EconomyMath.RoundTo(q * (2 + 3 * 0.85) / 5.0, 10), EconomyMath.ProratedQuota(q, 2, 5, 4, 3, _t));
        }

        [Test] public void OrderBonus_Week3_Is180() => Assert.AreEqual(180, EconomyMath.OrderCompleteBonus(3, _t));

        // ---- blame ----
        [Test] public void Severity_CowInRiverWeek1()
        {
            Assert.AreEqual(88, EconomyMath.Severity(-150, 1200, BlameVerb.DroppedInRiver));
            Assert.AreEqual(100, EconomyMath.Severity(-5000, 1200, BlameVerb.Dropped));
        }

        [Test] public void TouchShares_Renormalised()
        {
            CollectionAssert.AreEqual(new[] { 0.6f, 0.3f, 0.1f }, EconomyMath.TouchShares(3, _t));
            var two = EconomyMath.TouchShares(2, _t);
            Assert.AreEqual(2f / 3f, two[0], 1e-4); Assert.AreEqual(1f / 3f, two[1], 1e-4);
            CollectionAssert.AreEqual(new[] { 1f }, EconomyMath.TouchShares(1, _t));
        }

        static BlameEvent Ev(float t, BlameVerb v, int dollars, params ulong[] who) =>
            new BlameEvent { Timestamp = t, Verb = v, DollarDelta = dollars, ItemId = "Livestock.Pig", Instigators = who };

        [Test] public void Ledger_SharesPrimaryMergeAndNoiseFloor()
        {
            var ledger = new BlameLedger(_t) { WeeklyQuota = 1200 };
            var a = ledger.Submit(Ev(1f, BlameVerb.Bruised, -9, 2, 1, 0));
            Assert.NotNull(a);
            Assert.AreEqual(2UL, a.Event.PrimaryInstigator);
            CollectionAssert.AreEqual(new[] { 0.6f, 0.3f, 0.1f }, a.Event.BlameShares);
            var b = ledger.Submit(Ev(2.2f, BlameVerb.Bruised, -5, 2));
            Assert.AreSame(a, b, "same verb/item/primary within 1.5 s merges");
            Assert.AreEqual(-14, a.Event.DollarDelta);
            Assert.AreEqual(2, a.Count);
            Assert.IsNull(ledger.Submit(Ev(10f, BlameVerb.Bruised, -1, 2)), "below $2 floor is noise");
            Assert.NotNull(ledger.Submit(Ev(11f, BlameVerb.KnockedOut, 0, 3)), "non-economic verbs survive the floor");
            Assert.NotNull(ledger.Submit(Ev(11.5f, BlameVerb.Rescue, 20, 2)), "positive never merges with negative");
            Assert.AreEqual(3, ledger.Day.Count);
        }

        [Test] public void Ledger_NatureAndDisaster()
        {
            var ledger = new BlameLedger(_t) { WeeklyQuota = 1200 };
            var fired = new List<BlameEvent>();
            ledger.Disaster += fired.Add;
            var n = ledger.Submit(new BlameEvent { Timestamp = 1, Verb = BlameVerb.Bruised, DollarDelta = -40, CauseTag = "Weather.Wind" });
            Assert.AreEqual(PlayerIds.Nature, n.Event.PrimaryInstigator);
            Assert.AreEqual(0, fired.Count);
            ledger.Submit(Ev(5f, BlameVerb.DroppedInRiver, -60, 1));
            ledger.Submit(Ev(9f, BlameVerb.Dropped, -150, 2)); // severity 62.5 -> 63
            Assert.AreEqual(2, fired.Count);
            Assert.AreEqual(2UL, ledger.MostValuableDisaster());
        }

        [Test] public void Receipt_GroupsByPlayerTopLinesNatureLast()
        {
            var ledger = new BlameLedger(_t) { WeeklyQuota = 1200 };
            ledger.Submit(Ev(1f, BlameVerb.Dropped, -20, 1));
            ledger.Submit(Ev(5f, BlameVerb.Threw, -30, 1));
            ledger.Submit(Ev(9f, BlameVerb.Bruised, -12, 2));
            ledger.Submit(new BlameEvent { Timestamp = 20, Verb = BlameVerb.Stolen, DollarDelta = -10, CauseTag = "Critter.Crow" });
            var r = new ReceiptData();
            ReceiptBuilder.AddBlame(r, ledger.Day, id => "P" + id, _t);
            Assert.AreEqual(3, r.Blocks.Count);
            Assert.AreEqual("P1", r.Blocks[0].Name);
            Assert.AreEqual(-50, r.Blocks[0].Total);
            Assert.AreEqual("yeeted the pig", r.Blocks[0].Lines[0].Text);
            Assert.IsTrue(r.Blocks[2].IsNature);
            Assert.AreEqual("The Crow", r.Blocks[2].Name);
        }

        // ---- day loop ----
        [Test] public void DayClock_TransitionsAndCalendar()
        {
            var c = new DayClock { DurationOf = p => p == DayPhase.Morning ? 30 : p == DayPhase.Workday ? 540 : p == DayPhase.Dusk ? 180 : 0 };
            DayPhase timedOut = DayPhase.None;
            c.PhaseTimedOut += p => timedOut = p;
            c.GoTo(DayPhase.Morning);
            c.Tick(29.9f); Assert.AreEqual(DayPhase.None, timedOut);
            c.Tick(0.2f); Assert.AreEqual(DayPhase.Morning, timedOut);
            c.GoTo(DayPhase.Workday);
            Assert.Throws<System.InvalidOperationException>(() => c.GoTo(DayPhase.Sunday));
            c.GoTo(DayPhase.Payout);
            c.GoTo(DayPhase.Morning);
            Assert.AreEqual(1, c.DayIndex); Assert.AreEqual(2, c.DayOfWeek);
        }

        [Test] public void DayClock_RescaleKeepsProportionAndMinimum()
        {
            var c = new DayClock { DurationOf = p => 540 };
            c.GoTo(DayPhase.Morning);
            c.Tick(270f);
            c.Rescale(270f);
            Assert.AreEqual(135f, c.Remaining, 1e-3);
            c.Tick(130f);
            c.Rescale(20f);
            Assert.AreEqual(5f, c.Remaining, 1e-3, "never extends beyond what was left, floors at 30 s");
        }

        // ---- day modifiers ----
        static DayModifier Mod(string id, DayModifierWeight w, DayModifierStackRule rule, string group, params DayParamEffect[] fx)
        {
            var m = ScriptableObject.CreateInstance<DayModifier>();
            var so = new UnityEditor.SerializedObject(m);
            so.FindProperty("<ModifierId>k__BackingField").stringValue = id;
            so.FindProperty("<Weight>k__BackingField").enumValueIndex = (int)w;
            so.FindProperty("<StackRule>k__BackingField").enumValueIndex = (int)rule;
            so.FindProperty("<GroupTag>k__BackingField").stringValue = group;
            so.FindProperty("<MaxStacks>k__BackingField").intValue = 3;
            var arr = so.FindProperty("<Effects>k__BackingField");
            arr.arraySize = fx.Length;
            for (int i = 0; i < fx.Length; i++)
            {
                var el = arr.GetArrayElementAtIndex(i);
                el.FindPropertyRelative("ParamName").stringValue = fx[i].ParamName;
                el.FindPropertyRelative("Op").enumValueIndex = (int)fx[i].Op;
                el.FindPropertyRelative("Value").floatValue = fx[i].Value;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return m;
        }

        static DayParamEffect Fx(string p, DayParamOp op, float v) => new DayParamEffect { ParamName = p, Op = op, Value = v };

        [Test] public void Modifiers_ResolveAddThenMul()
        {
            var s = new DayModifierStack();
            s.Apply(Mod("A", DayModifierWeight.Minor, DayModifierStackRule.Unique, "", Fx("Goods.ValueMult", DayParamOp.Add, 0.2f)), 1);
            s.Apply(Mod("B", DayModifierWeight.Minor, DayModifierStackRule.Unique, "", Fx("Goods.ValueMult", DayParamOp.Mul, 1.5f)), 1);
            s.Apply(Mod("C", DayModifierWeight.Minor, DayModifierStackRule.Unique, "", Fx("Goods.ValueMult", DayParamOp.Mul, 0.8f)), 1);
            Assert.AreEqual(1.44f, s.Resolve("Goods.ValueMult", 1f), 1e-4);
        }

        [Test] public void Modifiers_ReplaceGroupAndChaosCap()
        {
            var s = new DayModifierStack();
            s.Apply(Mod("Mod.Weather.Fog", DayModifierWeight.Minor, DayModifierStackRule.ReplaceGroup, "Group.Weather"), 1);
            s.Apply(Mod("Mod.Weather.Storm", DayModifierWeight.Major, DayModifierStackRule.ReplaceGroup, "Group.Weather"), 1);
            Assert.AreEqual(1, s.ActiveList.Count);
            Assert.AreEqual("Mod.Weather.Storm", s.ActiveList[0].Mod.ModifierId);
            Assert.IsTrue(s.Apply(Mod("M2", DayModifierWeight.Major, DayModifierStackRule.Unique, ""), 1));
            Assert.IsFalse(s.Apply(Mod("M3", DayModifierWeight.Major, DayModifierStackRule.Unique, ""), 1), "week 1 cap = 2");
            Assert.AreEqual(1, s.Queued.Count);
        }

        [Test] public void Modifiers_OverridePriorityAndClamp()
        {
            var s = new DayModifierStack();
            s.Apply(Mod("A", DayModifierWeight.Minor, DayModifierStackRule.Unique, "", Fx("Day.WorkdaySeconds", DayParamOp.Add, -1000f)), 1);
            Assert.AreEqual(300f, s.Resolve("Day.WorkdaySeconds", 540f), "clamped to spec min");
            s.Apply(Mod("B", DayModifierWeight.Minor, DayModifierStackRule.Unique, "", Fx("Day.WorkdaySeconds", DayParamOp.Override, 400f)), 1);
            Assert.AreEqual(400f, s.Resolve("Day.WorkdaySeconds", 540f));
        }

        // ---- order ----
        [Test] public void Market_OrderFillAndComplete()
        {
            var m = new Market(_t);
            m.StartDay(42, 1, 4, 3, new[] { "Product.Crate", "Crop.PrizePumpkin", "Livestock.Pig" });
            Assert.AreEqual(3, m.Order.Count);
            foreach (var line in new List<OrderLine>(m.Order))
                for (int i = 0; i < line.Quantity; i++) Assert.IsTrue(m.RecordSale(line.GoodsId, 10));
            Assert.IsTrue(m.OrderComplete);
            Assert.IsFalse(m.WouldFillOrder("Livestock.Pig"));
        }
    }
}
