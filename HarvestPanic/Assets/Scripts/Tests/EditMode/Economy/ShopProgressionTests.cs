using System.Collections.Generic;
using System.IO;
using HarvestPanic.Core;
using HarvestPanic.Economy;
using NUnit.Framework;
using UnityEngine;

namespace HarvestPanic.Tests.EditMode.Economy
{
    /// <summary>farm-upgrades, week-quota, save-persistence acceptance pins (pure logic).</summary>
    public class ShopProgressionTests
    {
        readonly List<Object> _made = new List<Object>();
        EconomyTuning _t;
        string _dir;

        [SetUp] public void SetUp()
        {
            _t = ScriptableObject.CreateInstance<EconomyTuning>();
            _dir = Path.Combine(Path.GetTempPath(), "hp_save_test_" + System.Guid.NewGuid().ToString("N"));
        }

        [TearDown] public void TearDown()
        {
            foreach (var o in _made) Object.DestroyImmediate(o);
            _made.Clear();
            Object.DestroyImmediate(_t);
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        UpgradeDefinition Def(string id, int price, int tier = 1, string requires = null, bool staple = false, DayModifier mod = null)
        {
            var d = ScriptableObject.CreateInstance<UpgradeDefinition>();
            d.UpgradeId = id; d.DisplayName = id; d.Price = price; d.Tier = tier; d.RequiresId = requires; d.Staple = staple; d.Modifier = mod;
            _made.Add(d);
            return d;
        }

        FarmShop Shop(params UpgradeDefinition[] defs) => new FarmShop(defs) { OffersPerShop = 6, SellBackRate = 0.5f };

        // ---- pricing / wallet floor ----
        [Test] public void Buy_Fence250_WithBalance260_Ok_LeavesTen()
        {
            var s = Shop(Def("Upgrade.Fence", 250));
            s.RollOffers(1, new System.Random(1));
            Assert.AreEqual(ShopResult.Ok, s.CanBuy("Upgrade.Fence", 260));
            s.MarkBought("Upgrade.Fence", 250);
            Assert.IsTrue(s.IsOwned("Upgrade.Fence"));
            Assert.AreEqual(ShopResult.AlreadyOwned, s.CanBuy("Upgrade.Fence", 1000));
        }

        [Test] public void Buy_Rejected_WhenShort_OrNegative_OrNotOffered()
        {
            var s = Shop(Def("A", 250));
            Assert.AreEqual(ShopResult.NotOffered, s.CanBuy("A", 1000));
            s.RollOffers(1, new System.Random(1));
            Assert.AreEqual(ShopResult.NotEnoughMoney, s.CanBuy("A", 249));
            Assert.AreEqual(ShopResult.WalletNegative, s.CanBuy("A", -10));
            Assert.AreEqual(ShopResult.Unknown, s.CanBuy("Nope", 9999));
        }

        [Test] public void Wallet_UpgradeTxn_NeverOverdrafts()
        {
            var w = new Wallet(_t);
            Assert.IsFalse(w.Apply(new WalletTxn { Type = WalletTxnType.Upgrade, Amount = -101 }, out _));
            Assert.AreEqual(100, w.Balance);
            Assert.IsTrue(w.Apply(new WalletTxn { Type = WalletTxnType.Upgrade, Amount = -100 }, out _));
            Assert.AreEqual(0, w.Balance);
        }

        // ---- sell-back ----
        [Test] public void SellBack_IsFloorHalf()
        {
            Assert.AreEqual(125, FarmShop.Refund(250, 0.5f));
            Assert.AreEqual(150, FarmShop.Refund(301, 0.5f));
            Assert.AreEqual(0, FarmShop.Refund(1, 0.5f));
            var s = Shop(Def("A", 301));
            s.RollOffers(1, new System.Random(1));
            s.MarkBought("A", 301);
            Assert.AreEqual(150, s.RefundFor("A"));
        }

        [Test] public void SellBack_HighestLevelFirst()
        {
            var s = Shop(Def("L1", 300), Def("L2", 600, 1, "L1"));
            s.RollOffers(1, new System.Random(1));
            Assert.AreEqual(ShopResult.MissingPrereq, s.CanBuy("L2", 9999));
            s.MarkBought("L1", 300);
            s.RollOffers(1, new System.Random(2));
            Assert.AreEqual(ShopResult.Ok, s.CanBuy("L2", 9999));
            s.MarkBought("L2", 600);
            Assert.AreEqual(ShopResult.SellHigherFirst, s.CanSell("L1"));
            Assert.AreEqual(ShopResult.Ok, s.CanSell("L2"));
            s.MarkSold("L2");
            Assert.AreEqual(ShopResult.Ok, s.CanSell("L1"));
            Assert.AreEqual(ShopResult.NotOwned, s.CanSell("L2"));
        }

        // ---- offers ----
        [Test] public void Offers_StaplesFirst_NoOwned_PrereqRespected_CappedAndDeterministic()
        {
            var defs = new List<UpgradeDefinition> { Def("S", 300, 1, null, true), Def("L2", 600, 2, "S") };
            for (int i = 0; i < 8; i++) defs.Add(Def("U" + i, 100, 1 + i % 3));
            var s = Shop(defs.ToArray());
            s.RollOffers(1, new System.Random(42));
            Assert.AreEqual(6, s.Offers.Count);
            Assert.AreEqual("S", s.Offers[0]);
            CollectionAssert.DoesNotContain(s.Offers, "L2");
            var first = new List<string>(s.Offers);
            s.RollOffers(1, new System.Random(42));
            CollectionAssert.AreEqual(first, s.Offers);
            s.MarkBought("S", 300);
            s.RollOffers(1, new System.Random(42));
            CollectionAssert.DoesNotContain(s.Offers, "S");
        }

        [Test] public void OfferWeight_ByWeekTier()
        {
            Assert.AreEqual(1f, FarmShop.OfferWeight(1, 1));
            Assert.AreEqual(0.5f, FarmShop.OfferWeight(2, 1));
            Assert.AreEqual(0f, FarmShop.OfferWeight(3, 1));
            Assert.AreEqual(1f, FarmShop.OfferWeight(2, 3));
        }

        [Test] public void Proposals_OnePerPlayer_QueueCap_Fifo()
        {
            var s = Shop(Def("A", 1));
            Assert.AreEqual(ShopResult.Ok, s.Propose(new ShopProposal { Proposer = 1, UpgradeId = "A" }));
            Assert.AreEqual(ShopResult.Busy, s.Propose(new ShopProposal { Proposer = 1, UpgradeId = "A" }));
            for (ulong p = 2; p <= 4; p++) Assert.AreEqual(ShopResult.Ok, s.Propose(new ShopProposal { Proposer = p, UpgradeId = "A" }));
            Assert.AreEqual(ShopResult.Busy, s.Propose(new ShopProposal { Proposer = 9, UpgradeId = "A" }));
            Assert.IsTrue(s.TakePending(out var head));
            Assert.AreEqual(1UL, head.Proposer);
            Assert.AreEqual(ShopResult.Unknown, s.Propose(new ShopProposal { Proposer = 9, UpgradeId = "Nope" }));
        }

        // ---- modifiers ----
        [Test] public void CatalogAssets_AllRunUpgradeModifiers_WithEffects()
        {
            UpgradeCatalog.All.Invalidate();
            var all = new List<UpgradeDefinition>(UpgradeCatalog.All.All);
            Assert.GreaterOrEqual(all.Count, 10);
            foreach (var d in all)
            {
                Assert.NotNull(d.Modifier, d.UpgradeId);
                Assert.AreEqual(DayModifierDuration.Run, d.Modifier.Duration, d.UpgradeId);
                Assert.AreEqual(DayModifierCategory.Upgrade, d.Modifier.Category, d.UpgradeId);
                Assert.Greater(d.Modifier.Effects.Length, 0, d.UpgradeId);
                Assert.Greater(d.Price, 0);
                Assert.IsTrue(Content.DayModifiers.TryGet(d.Modifier.ModifierId, out _), "client mirror lookup " + d.Modifier.ModifierId);
                if (!string.IsNullOrEmpty(d.RequiresId)) Assert.IsTrue(UpgradeCatalog.All.TryGet(d.RequiresId, out _), d.RequiresId);
            }
        }

        [Test] public void UpgradeModifier_PersistsAcrossDaysAndWeeks_RemovedOnSell()
        {
            Assert.IsTrue(UpgradeCatalog.All.TryGet("Upgrade.PaddedCrates", out var crates));
            Assert.IsTrue(UpgradeCatalog.All.TryGet("Upgrade.BridgeReinforcement1", out var b1));
            Assert.IsTrue(UpgradeCatalog.All.TryGet("Upgrade.BridgeReinforcement2", out var b2));
            var m = new DayModifierStack();
            Assert.IsTrue(m.Apply(crates.Modifier, 1, true));
            Assert.AreEqual(0.8f, m.Resolve(DayModifierStack.Params.GoodsBruiseMult, 1f), 1e-4);
            m.ExpireAt(DayModifierDuration.Day);
            m.ExpireAt(DayModifierDuration.Week);
            Assert.AreEqual(0.8f, m.Resolve(DayModifierStack.Params.GoodsBruiseMult, 1f), 1e-4);
            m.Apply(b1.Modifier, 1, true); m.Apply(b2.Modifier, 1, true);
            Assert.AreEqual(0.45f, m.Resolve(DayParams.BridgePlankBreakMult, 1f), 0.005f);
            Assert.Greater(m.Resolve(DayParams.BridgeSafeLoadMult, 1f), 1.3f);
            m.Remove(crates.Modifier.ModifierId);
            Assert.AreEqual(1f, m.Resolve(DayModifierStack.Params.GoodsBruiseMult, 1f), 1e-4);
        }

        // ---- save ----
        static RunSaveData Sample() => new RunSaveData
        {
            Resume = RunResumePoint.Shop, Week = 2, DayOfWeek = 3, DayIndex = 7, Balance = 345, Iou = 20, WeekIncome = 200, Quota = 1050, Players = 2,
            Upgrades = { "Upgrade.PaddedCrates", "Upgrade.BridgeReinforcement1" }, UpgradePaid = { 300, 300 },
            LandlordRules = { "Mod.Landlord.QuietHours" }, Offers = { "Upgrade.Megaphone" }, TotalEarned = 2000, TotalSpentUpgrades = 600, DaysPlayed = 8, Seed = 99,
        };

        [Test] public void Save_RoundTrip_ThroughDisk()
        {
            var d = Sample();
            Assert.IsTrue(RunSaveStore.Write(d, _dir));
            Assert.IsTrue(RunSaveStore.Exists(_dir));
            var back = RunSaveStore.Load(out string err, _dir);
            Assert.NotNull(back, err);
            Assert.AreEqual(JsonUtility.ToJson(d), JsonUtility.ToJson(back));
            Assert.Less(new FileInfo(RunSaveStore.PathFor(_dir)).Length, 64 * 1024);
            RunSaveStore.Delete(_dir);
            Assert.IsFalse(RunSaveStore.Exists(_dir));
            Assert.IsNull(RunSaveStore.Load(out _, _dir));
        }

        [Test] public void Save_BadChecksum_FallsBackToBak()
        {
            var d = Sample();
            RunSaveStore.Write(d, _dir);
            d.Balance = 999;
            RunSaveStore.Write(d, _dir); // previous goes to .bak
            string path = RunSaveStore.PathFor(_dir);
            File.WriteAllText(path, File.ReadAllText(path).Replace("999", "9999"));
            var back = RunSaveStore.Load(out _, _dir);
            Assert.NotNull(back);
            Assert.AreEqual(345, back.Balance, "fell back to .bak");
        }

        [Test] public void Save_FutureVersion_Refused()
        {
            var d = Sample(); d.Version = RunSaveData.CurrentVersion + 1;
            Assert.IsNull(RunSaveStore.Deserialize(RunSaveStore.Serialize(d), out string err));
            StringAssert.Contains("newer", err);
            Assert.IsNull(RunSaveStore.Deserialize("garbage", out _));
        }

        [Test] public void Wallet_Restore_ClampsAndKeepsIou()
        {
            var w = new Wallet(_t);
            w.Restore(-500, 30);
            Assert.AreEqual(_t.OverdraftFloor, w.Balance);
            Assert.AreEqual(30, w.IouThisWeek);
        }

        // ---- week loop ----
        [Test] public void Quota_Week1To2_Escalates()
        {
            Assert.AreEqual(1200, EconomyMath.Quota(1, 4, _t));
            Assert.AreEqual(1620, EconomyMath.Quota(2, 4, _t));
            Assert.AreEqual(780, EconomyMath.Quota(1, 2, _t));
            Assert.AreEqual(1050, EconomyMath.Quota(2, 2, _t));
        }

        [Test] public void DayClock_ShopPhase_WeekRollover()
        {
            var c = new DayClock { DaysPerWeek = 5 };
            c.GoTo(DayPhase.Morning);
            for (int day = 1; day <= 5; day++)
            {
                c.GoTo(DayPhase.Workday); c.GoTo(DayPhase.Payout); c.GoTo(DayPhase.Shop);
                if (day < 5) { c.GoTo(DayPhase.Morning); Assert.AreEqual(day + 1, c.DayOfWeek); }
            }
            Assert.IsTrue(c.IsLastDayOfWeek);
            c.GoTo(DayPhase.Sunday);
            c.GoTo(DayPhase.Collateral);
            Assert.Throws<System.InvalidOperationException>(() => c.GoTo(DayPhase.Morning));
            c.GoTo(DayPhase.Sunday);
            c.GoTo(DayPhase.Morning);
            Assert.AreEqual(2, c.Week); Assert.AreEqual(1, c.DayOfWeek); Assert.AreEqual(5, c.DayIndex);
        }

        [Test] public void DayClock_Restore_JumpsToShop()
        {
            var c = new DayClock();
            c.Restore(DayPhase.Shop, 7, 2, 3);
            Assert.AreEqual(DayPhase.Shop, c.Phase);
            c.GoTo(DayPhase.Morning);
            Assert.AreEqual(8, c.DayIndex); Assert.AreEqual(4, c.DayOfWeek); Assert.AreEqual(2, c.Week);
        }
    }
}
