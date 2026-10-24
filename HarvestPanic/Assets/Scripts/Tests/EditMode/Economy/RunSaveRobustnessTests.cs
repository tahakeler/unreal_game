using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using HarvestPanic.Economy;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HarvestPanic.Tests.EditMode.Economy
{
    /// <summary>
    /// Run Save fault injection (GDD 18.7 DoD item 5, risk #7). <see cref="RunSaveStore.Write"/> is
    /// temp-write -> copy main to .bak -> delete main -> move temp; a process kill can land between any two steps or
    /// part-way through a file write. Each kill point is reproduced as the exact disk state it leaves behind, then the
    /// next launch's <see cref="RunSaveStore.Load"/> must return the newest or the previous Shop save - never nothing,
    /// never garbage, never more than one Shop phase lost.
    /// </summary>
    public class RunSaveRobustnessTests
    {
        public enum Kill { BeforeWrite, MidTempWrite, AfterTempWrite, MidBackupCopy, AfterBackupCopy, AfterDeleteMain, Completed }

        string _dir;
        string Main => RunSaveStore.PathFor(_dir);
        string Bak => Main + ".bak";
        string Tmp => Main + ".tmp";

        [SetUp] public void SetUp() { _dir = Path.Combine(Path.GetTempPath(), "hp_save_fault_" + System.Guid.NewGuid().ToString("N")); Directory.CreateDirectory(_dir); }
        [TearDown] public void TearDown() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

        /// <summary>Host save at the Shop of run day <paramref name="day"/> (0-based), as the EconomyDirector writes it.</summary>
        static RunSaveData Shop(int day, int balance = -1)
        {
            var d = new RunSaveData
            {
                Resume = RunResumePoint.Shop, Week = 1 + day / 5, DayOfWeek = 1 + day % 5, DayIndex = day,
                Balance = balance >= 0 ? balance : 100 + 37 * day, Iou = day % 3 == 0 ? 50 : 0, WeekIncome = 80 * (day % 5 + 1),
                Quota = 540, Players = 4, TotalEarned = 90 * (day + 1), TotalSpentUpgrades = 150 * (day / 2), DaysPlayed = day + 1, Seed = 1234,
            };
            for (int i = 0; i < day / 2; i++) { d.Upgrades.Add("Upgrade.Test" + i); d.UpgradePaid.Add(100 + i); }
            if (day >= 5) d.LandlordRules.Add("Landlord.QuietHours");
            d.Offers.AddRange(new[] { "Upgrade.BridgeReinforcement1", "Upgrade.PaddedCrates" });
            return d;
        }

        static void WriteBytes(string path, byte[] b) => File.WriteAllBytes(path, b);
        static byte[] Prefix(byte[] b, int n) { var r = new byte[n]; System.Array.Copy(b, r, n); return r; }

        /// <summary>Leaves the disk as a kill at <paramref name="k"/> during the save of <paramref name="next"/> would.</summary>
        void Stage(Kill k, RunSaveData older, RunSaveData prev, RunSaveData next)
        {
            Assert.IsTrue(RunSaveStore.Write(older, _dir));
            Assert.IsTrue(RunSaveStore.Write(prev, _dir)); // main = prev, bak = older
            byte[] nextBytes = System.Text.Encoding.UTF8.GetBytes(RunSaveStore.Serialize(next));
            byte[] prevBytes = File.ReadAllBytes(Main);
            switch (k)
            {
                case Kill.BeforeWrite: break;
                case Kill.MidTempWrite: WriteBytes(Tmp, Prefix(nextBytes, nextBytes.Length / 2)); break;
                case Kill.AfterTempWrite: WriteBytes(Tmp, nextBytes); break;
                case Kill.MidBackupCopy: WriteBytes(Tmp, nextBytes); WriteBytes(Bak, Prefix(prevBytes, prevBytes.Length / 3)); break;
                case Kill.AfterBackupCopy: WriteBytes(Tmp, nextBytes); File.Copy(Main, Bak, true); break;
                case Kill.AfterDeleteMain: WriteBytes(Tmp, nextBytes); File.Copy(Main, Bak, true); File.Delete(Main); break;
                case Kill.Completed: Assert.IsTrue(RunSaveStore.Write(next, _dir)); break;
            }
        }

        [Test]
        public void ProcessKill_AtEveryWriteStep_ResumesLatestOrPreviousShop([Values] Kill k)
        {
            RunSaveData older = Shop(2), prev = Shop(3), next = Shop(4);
            Stage(k, older, prev, next);

            Assert.IsTrue(RunSaveStore.Exists(_dir), "a save is still visible to the menu");
            var got = RunSaveStore.Load(out string err, _dir);
            Assert.NotNull(got, "resume must find a save after a kill at " + k + " (" + err + ")");
            int expected = k == Kill.Completed ? next.DayIndex : prev.DayIndex;
            Assert.AreEqual(expected, got.DayIndex, "resume point after kill at " + k);
            Assert.LessOrEqual(next.DayIndex - got.DayIndex, 1, "never more than the last Shop phase lost");
            Assert.AreEqual(expected == next.DayIndex ? next.Balance : prev.Balance, got.Balance);

            // The relaunched game keeps saving: the stale .tmp is overwritten and consumed, the new save loads.
            Assert.IsTrue(RunSaveStore.Write(Shop(5), _dir), "next boundary save after the crash");
            Assert.AreEqual(5, RunSaveStore.Load(out _, _dir).DayIndex);
            Assert.IsFalse(File.Exists(Tmp), "no leftover temp file");
        }

        [Test]
        public void TruncatedMain_AtAnyLength_FallsBackToBak()
        {
            Assert.IsTrue(RunSaveStore.Write(Shop(0), _dir));
            Assert.IsTrue(RunSaveStore.Write(Shop(1), _dir));
            byte[] full = File.ReadAllBytes(Main);
            var lengths = new SortedSet<int> { 0, 1, 2, 3, 4, 8, 10, 15, 16, 17, full.Length - 2, full.Length - 1 };
            for (int n = 0; n < full.Length; n += System.Math.Max(1, full.Length / 64)) lengths.Add(n);
            foreach (int n in lengths)
            {
                WriteBytes(Main, Prefix(full, n));
                var got = RunSaveStore.Load(out string err, _dir);
                Assert.NotNull(got, $"truncated to {n}/{full.Length} bytes: {err}");
                Assert.AreEqual(0, got.DayIndex, $"truncated to {n} bytes loads the backup, never a partial main");
            }
        }

        [Test]
        public void CorruptByte_AnywhereInMain_FallsBackToBak()
        {
            Assert.IsTrue(RunSaveStore.Write(Shop(6), _dir));
            Assert.IsTrue(RunSaveStore.Write(Shop(7), _dir));
            byte[] good = File.ReadAllBytes(Main);
            for (int i = 0; i < good.Length; i += System.Math.Max(1, good.Length / 97))
            {
                var bad = (byte[])good.Clone();
                bad[i] ^= 0x01; // XOR 1 changes every hex digit's value (case-insensitive CRC compare cannot mask it)
                WriteBytes(Main, bad);
                var got = RunSaveStore.Load(out _, _dir);
                Assert.NotNull(got, "byte " + i);
                Assert.AreEqual(6, got.DayIndex, "corrupt byte " + i + " must not load");
            }
        }

        [Test]
        public void MainMissing_BakOnly_LoadsAndShowsContinue()
        {
            Assert.IsTrue(RunSaveStore.Write(Shop(1), _dir));
            Assert.IsTrue(RunSaveStore.Write(Shop(2), _dir));
            File.Delete(Main);
            Assert.IsTrue(RunSaveStore.Exists(_dir));
            Assert.AreEqual(1, RunSaveStore.Load(out _, _dir).DayIndex);
        }

        [Test]
        public void MainAndBakBothCorrupt_ReturnsNull_WithoutThrowing()
        {
            Assert.IsTrue(RunSaveStore.Write(Shop(1), _dir));
            Assert.IsTrue(RunSaveStore.Write(Shop(2), _dir));
            File.WriteAllText(Main, "HPSAVE deadbeef\n{\"Week\":9}");
            File.WriteAllBytes(Bak, new byte[0]);
            Assert.IsNull(RunSaveStore.Load(out string err, _dir));
            Assert.IsFalse(string.IsNullOrEmpty(err));
        }

        [Test]
        public void FailedWrite_KeepsPreviousSaveIntact()
        {
            Assert.IsTrue(RunSaveStore.Write(Shop(3), _dir));
            Directory.CreateDirectory(Tmp); // temp path unwritable (stand-in for disk full / permission denied)
            LogAssert.Expect(LogType.Warning, new Regex("Run save failed"));
            Assert.IsFalse(RunSaveStore.Write(Shop(4), _dir));
            Assert.AreEqual(3, RunSaveStore.Load(out _, _dir).DayIndex, "previous save untouched");
            Directory.Delete(Tmp);
            Assert.IsTrue(RunSaveStore.Write(Shop(4), _dir), "next boundary retries and succeeds");
            Assert.AreEqual(4, RunSaveStore.Load(out _, _dir).DayIndex);
        }

        [Test]
        public void TwoWeeks_KillAfterEveryShopSave_NeverLosesMoreThanOneShop()
        {
            string crash = Path.Combine(_dir, "crash");
            for (int day = 0; day < 10; day++)
            {
                Assert.IsTrue(RunSaveStore.Write(Shop(day), _dir));
                Assert.AreEqual(day, RunSaveStore.Load(out _, _dir).DayIndex, "clean quit/resume returns the latest Shop");

                // Copy the folder as a killed process would leave it with a torn main file, then resume from the copy.
                if (Directory.Exists(crash)) Directory.Delete(crash, true);
                Directory.CreateDirectory(crash);
                foreach (var f in Directory.GetFiles(_dir)) File.Copy(f, Path.Combine(crash, Path.GetFileName(f)));
                string cm = RunSaveStore.PathFor(crash);
                byte[] b = File.ReadAllBytes(cm);
                File.WriteAllBytes(cm, Prefix(b, b.Length * 2 / 3));
                var got = RunSaveStore.Load(out string err, crash);
                if (day == 0) { Assert.IsNull(got, "first ever save torn: nothing older to fall back to"); continue; }
                Assert.NotNull(got, err);
                Assert.AreEqual(day - 1, got.DayIndex, "torn save on day " + day + " resumes the previous Shop");
            }
        }

        [Test]
        public void RoundTrip_PreservesEveryResumeField()
        {
            var d = Shop(7);
            d.Resume = RunResumePoint.WeekStart;
            Assert.IsTrue(RunSaveStore.Write(d, _dir));
            var g = RunSaveStore.Load(out _, _dir);
            Assert.AreEqual(d.Resume, g.Resume);
            Assert.AreEqual(new[] { d.Week, d.DayOfWeek, d.DayIndex, d.Balance, d.Iou, d.WeekIncome, d.Quota, d.Players, d.TotalEarned, d.TotalSpentUpgrades, d.DaysPlayed, d.Seed },
                new[] { g.Week, g.DayOfWeek, g.DayIndex, g.Balance, g.Iou, g.WeekIncome, g.Quota, g.Players, g.TotalEarned, g.TotalSpentUpgrades, g.DaysPlayed, g.Seed });
            CollectionAssert.AreEqual(d.Upgrades, g.Upgrades);
            CollectionAssert.AreEqual(d.UpgradePaid, g.UpgradePaid);
            CollectionAssert.AreEqual(d.LandlordRules, g.LandlordRules);
            CollectionAssert.AreEqual(d.Offers, g.Offers);
            Assert.IsFalse(string.IsNullOrEmpty(g.SavedAtUtc));
        }
    }
}
