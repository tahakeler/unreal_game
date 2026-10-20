using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using HarvestPanic.AI;
using HarvestPanic.AI.Night;
using HarvestPanic.Core;
using HarvestPanic.Economy;
using HarvestPanic.Gameplay;
using HarvestPanic.Weather;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HarvestPanic.Tests.PlayMode.Perf
{
    /// <summary>
    /// Bot soak playtest: Muddlebury LAN host + 3 bot farmers play a full WEEK (5 days, shop each evening, Sunday
    /// Landlord) and into week 2 at a small day scale, with the real night phase at every Dusk. Bots grab the nearest
    /// goods, carry them over the bridge into the Sale Pen and shout at random. Writes a day-by-day log to
    /// docs/qa/playtest/raw/bot-week-log.md and asserts: no errors/exceptions, no leaked NetworkObjects or night
    /// creatures, wallet == sum of transactions, phases sane, memory growth bounded.
    /// </summary>
    [Category("Soak")]
    public class BotWeekSoakTests
    {
        float _prevScale;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _prevScale = EconomyDevCvars.DayScale;
            EconomyDevCvars.DayScale = 0.05f; // workday long enough for a bot round trip (0.02 gave 0 deliveries)
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return PerfHarness.Shutdown();
            EconomyDevCvars.DayScale = _prevScale;
        }

        sealed class DayLog
        {
            public int Week, Dow, DayIndex;
            public int StartBalance, EndBalance, Sales, SaleUnits, Disasters, Deliveries, Shouts, NetObjsAtMorning, Goods, Critters;
            public long MemMB, MonoMB;
            public string Weather, Mods = "", Notes = ""; public int Quota; public float WorkdaySec;
            public readonly List<string> DisasterNames = new List<string>();
            public bool NightSeen; public int StitchesMax, FoxesMax;
            public ReceiptData Receipt;
        }

        [UnityTest, Timeout(3600000)]
        public IEnumerator BotFarmers_FullWeek_IntoWeek2()
        {
            LogAssert.ignoreFailingMessages = true; // collected and asserted at the end so one error doesn't hide the rest
            var errors = new List<string>();
            Application.LogCallback onLog = (c, s, t) =>
            {
                if (t == LogType.Error || t == LogType.Exception || t == LogType.Assert)
                    errors.Add($"[{t}] {c.Split('\n')[0]}" + (string.IsNullOrEmpty(s) ? "" : " @ " + s.Split('\n')[0]));
            };
            Application.logMessageReceived += onLog;

            yield return PerfHarness.BootMuddlebury();
            var dir = EconomyDirector.Instance;
            dir.Restart();
            yield return null;
            Assert.AreEqual(DayPhase.Morning, dir.Phase);

            var bots = PerfHarness.SpawnBots(3, 777);
            foreach (var b in bots) b.Speed = 10f;
            var pen = PerfHarness.PenDropPoint();
            var rng = new System.Random(4242);
            var days = new List<DayLog>();
            var phaseTrace = new List<string>();
            var issues = new List<string>();
            var landlordBarks = new List<string>();
            var rentShortfalls = new List<string>();
            var shopLog = new List<string>();
            long txnSum = 0; int startBalance = dir.Balance;
            DayLog cur = null;

            dir.OnWalletTxn += t =>
            {
                txnSum += t.Amount;
                if (cur != null && t.Type == WalletTxnType.Sale && t.Amount > 0) { cur.Sales += t.Amount; cur.SaleUnits++; }
            };
            dir.OnDisaster += e => { if (cur != null) { cur.Disasters++; if (cur.DisasterNames.Count < 6) cur.DisasterNames.Add(e.Verb + ":" + e.ItemId); } };
            dir.OnLandlordBark += b => landlordBarks.Add($"W{dir.Week}D{dir.DayOfWeek} {dir.Phase}: {b}");
            dir.OnReceipt += r => { if (cur != null && !r.IsSunday) cur.Receipt = r; };
            dir.OnPhaseChanged += (a, b) => phaseTrace.Add($"W{dir.Week}D{dir.DayOfWeek}:{a}->{b}");

            long mem0 = 0; int netObjs0 = -1, netObjsMax = 0;
            DayLog BeginDay()
            {
                var d = new DayLog
                {
                    Week = dir.Week, Dow = dir.DayOfWeek, DayIndex = dir.DayIndex, StartBalance = dir.Balance,
                    NetObjsAtMorning = PerfHarness.SpawnedNetworkObjects, Goods = Goods.All.Count(g => g != null && g.IsSpawned),
                    Critters = CritterSpawnDirector.Spawned.Count(c => c != null),
                    MemMB = Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024), MonoMB = Profiler.GetMonoUsedSizeLong() / (1024 * 1024),
                    Weather = WeatherDirector.Instance != null ? WeatherDirector.Instance.Current.ToString() : "n/a",
                    Mods = string.Join(", ", dir.ActiveModifiers.Select(m => m.ModifierId)),
                };
                d.Quota = dir.Quota;
                foreach (var b in bots) { d.Deliveries -= b.Delivered; d.Shouts -= b.Shouts; }
                if (Object.FindObjectsByType<MrStitches>(FindObjectsSortMode.None).Length > 0 || Object.FindObjectsByType<NightFox>(FindObjectsSortMode.None).Length > 0)
                    issues.Add($"W{d.Week}D{d.Dow}: night creatures still present at Morning");
                if (netObjs0 < 0) { netObjs0 = d.NetObjsAtMorning; mem0 = d.MemMB; }
                netObjsMax = Math.Max(netObjsMax, d.NetObjsAtMorning);
                days.Add(d);
                return d;
            }
            void EndDay(DayLog d)
            {
                d.EndBalance = dir.Balance;
                foreach (var b in bots) { d.Deliveries += b.Delivered; d.Shouts += b.Shouts; }
            }

            cur = BeginDay();
            float deadline = Time.realtimeSinceStartup + 50 * 60f;
            var lastPhase = dir.Phase;
            bool done = false;
            int shopVisits = 0, sundays = 0;
            while (!done)
            {
                if (Time.realtimeSinceStartup > deadline) { issues.Add("soak timed out at " + dir.Phase + " W" + dir.Week + "D" + dir.DayOfWeek); break; }
                float dt = Time.deltaTime;
                foreach (var b in bots) b.Tick(dt, pen, dir.IsMarketOpen);
                var ph = dir.Phase;
                if (ph == DayPhase.Workday && cur != null && cur.WorkdaySec <= 0f) cur.WorkdaySec = dir.PhaseDuration;
                if (NightDirector.Instance != null && NightDirector.Instance.IsNight && cur != null)
                {
                    cur.NightSeen = true;
                    cur.StitchesMax = Math.Max(cur.StitchesMax, NightDirector.Instance.Stitches.Count);
                    cur.FoxesMax = Math.Max(cur.FoxesMax, NightDirector.Instance.Foxes.Count);
                }
                if (dir.Balance < -100000) issues.Add("wallet absurd: " + dir.Balance);

                switch (ph)
                {
                    case DayPhase.Payout:
                        yield return new WaitForSeconds(0.5f);
                        if (cur != null) EndDay(cur);
                        dir.NextDay();
                        break;
                    case DayPhase.Shop:
                        shopVisits++;
                        var offers = dir.ShopOffers.ToList();
                        if (offers.Count > 0 && rng.NextDouble() < 0.6)
                        {
                            string id = offers[rng.Next(offers.Count)];
                            int before = dir.Balance;
                            dir.ProposeBuy(id);
                            yield return null;
                            if (dir.ShopQueue.Count > 0) dir.ConfirmPending();
                            yield return null;
                            shopLog.Add($"W{dir.Week}D{dir.DayOfWeek}: try buy {id} -> {(dir.OwnedUpgrades.Contains(id) ? "BOUGHT" : "not bought")} (balance {before} -> {dir.Balance})");
                        }
                        else shopLog.Add($"W{dir.Week}D{dir.DayOfWeek}: browsed ({offers.Count} offers), bought nothing");
                        if (dir.DayOfWeek >= dir.Tuning.DaysPerWeek)
                        {
                            // Bots only haul the ~5 loose goods per day (no livestock), so they can't earn rent on their
                            // own. Record the shortfall, then top up with a logged dev txn so Sunday-paid -> week 2 runs.
                            int due = dir.Quota + dir.Iou;
                            if (dir.Balance < due)
                            {
                                int gap = due - dir.Balance + 50;
                                rentShortfalls.Add($"W{dir.Week}: balance {dir.Balance} vs due {due} (quota {dir.Quota} + IOU {dir.Iou}); bots short by {due - dir.Balance}");
                                dir.AddWalletTxn(new WalletTxn { Type = WalletTxnType.Refund, Amount = gap, PlayerId = PlayerIds.None });
                                shopLog.Add($"W{dir.Week}D{dir.DayOfWeek}: DEV TOP-UP +{gap} so the Sunday-paid path is exercised");
                                yield return null;
                            }
                        }
                        dir.NextDay();
                        yield return null;
                        if (dir.Phase == DayPhase.Morning) cur = BeginDay();
                        break;
                    case DayPhase.Sunday:
                        sundays++;
                        yield return new WaitForSeconds(1f);
                        var r = dir.LastReceipt;
                        if (r != null && r.IsSunday)
                            shopLog.Add($"SUNDAY W{r.Week}: due {r.Due} (quota {r.Quota} + IOU {r.Iou}), paid={r.Paid}, carryover {r.Carryover}, MVD {r.Mvd}, line \"{r.SundayLine}\"");
                        dir.NextDay();
                        yield return null;
                        if (dir.Phase == DayPhase.Morning) cur = BeginDay();
                        break;
                    case DayPhase.Collateral:
                        var owned = dir.OwnedUpgrades.ToList();
                        if (owned.Count > 0)
                        {
                            shopLog.Add($"COLLATERAL W{dir.Week}: selling {owned[0]} (refund {dir.RefundFor(owned[0])}), balance {dir.Balance}");
                            dir.ProposeSell(owned[0]); yield return null;
                            if (dir.ShopQueue.Count > 0) dir.ConfirmPending();
                            yield return null;
                            if (dir.OwnedUpgrades.Contains(owned[0])) { shopLog.Add("COLLATERAL: sell failed, giving up"); dir.GiveUp(); }
                        }
                        else { shopLog.Add($"COLLATERAL W{dir.Week}: nothing left to sell, giving up"); dir.GiveUp(); }
                        yield return new WaitForSeconds(0.3f);
                        break;
                    case DayPhase.Evicted:
                        issues.Add($"EVICTED in week {dir.Week}");
                        done = true;
                        break;
                }
                if (dir.Week >= 2 && dir.DayOfWeek >= 2 && dir.Phase == DayPhase.Morning) done = true; // full week + first day of week 2
                if (ph != lastPhase) lastPhase = ph;
                yield return null;
            }

            foreach (var b in bots) b.Destroy();
            yield return null;
            Application.logMessageReceived -= onLog;

            long memEnd = Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024);
            int netEnd = PerfHarness.SpawnedNetworkObjects;
            long walletDelta = dir.Balance - startBalance;
            if (walletDelta != txnSum) issues.Add($"wallet delta {walletDelta} != sum of txns {txnSum}");

            // ---- report ----
            var sb = new StringBuilder();
            sb.AppendLine($"# Bot week soak log -- {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC");
            sb.AppendLine();
            sb.AppendLine($"- Day scale {EconomyDevCvars.DayScale}, host + {bots.Count} bots, reached W{dir.Week}D{dir.DayOfWeek} {dir.Phase}, shop visits {shopVisits}, Sundays {sundays}");
            sb.AppendLine($"- Wallet: start {startBalance}, end {dir.Balance}, sum of txns {txnSum}");
            sb.AppendLine($"- Spawned NetworkObjects: first Morning {netObjs0}, max at Morning {netObjsMax}, end {netEnd}");
            sb.AppendLine($"- Memory (Unity total allocated): first Morning {mem0} MB, end {memEnd} MB");
            sb.AppendLine($"- Errors/exceptions logged: {errors.Count}; issues: {issues.Count}");
            sb.AppendLine();
            sb.AppendLine("| Day | Quota | Workday s | Weather | Start $ | End $ | Sales $ (units) | Bot deliveries | Shouts | Disasters | Night (Stitches/Foxes) | NetObjs@AM | Goods@AM | Critters@AM | Mem MB | Mono MB | Modifiers |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
            foreach (var d in days)
                sb.AppendLine($"| W{d.Week}D{d.Dow} | {d.Quota} | {d.WorkdaySec:0} | {d.Weather} | {d.StartBalance} | {d.EndBalance} | {d.Sales} ({d.SaleUnits}) | {d.Deliveries} | {d.Shouts} | {d.Disasters} {string.Join(" ", d.DisasterNames)} | {(d.NightSeen ? $"yes ({d.StitchesMax}/{d.FoxesMax})" : "no")} | {d.NetObjsAtMorning} | {d.Goods} | {d.Critters} | {d.MemMB} | {d.MonoMB} | {d.Mods} |");
            sb.AppendLine(); sb.AppendLine("## Receipts");
            foreach (var d in days) if (d.Receipt != null) sb.AppendLine($"- W{d.Week}D{d.Dow}: units {d.Receipt.UnitsSold}, sales {d.Receipt.Sales}, headline \"{d.Receipt.Headline}\"");
            sb.AppendLine(); sb.AppendLine("## Rent shortfalls (bots alone)"); foreach (var l in rentShortfalls) sb.AppendLine("- " + l);
            sb.AppendLine(); sb.AppendLine("## Shop / Sunday"); foreach (var l in shopLog) sb.AppendLine("- " + l);
            sb.AppendLine(); sb.AppendLine("## Landlord barks"); foreach (var l in landlordBarks.Take(40)) sb.AppendLine("- " + l);
            sb.AppendLine(); sb.AppendLine("## Issues"); foreach (var l in issues) sb.AppendLine("- " + l);
            sb.AppendLine(); sb.AppendLine("## Errors (deduped, first 40)");
            foreach (var g in errors.GroupBy(e => e).Take(40)) sb.AppendLine($"- x{g.Count()} {g.Key}");
            sb.AppendLine(); sb.AppendLine("## Phase trace"); sb.AppendLine(string.Join(" | ", phaseTrace));
            var outDir = Path.Combine(PerfHarness.RepoRoot, "docs", "qa", "playtest", "raw"); Directory.CreateDirectory(outDir);
            File.WriteAllText(Path.Combine(outDir, "bot-week-log.md"), sb.ToString());
            Debug.Log(sb.ToString());

            Assert.That(dir.Week, Is.GreaterThanOrEqualTo(2), "did not reach week 2\n" + string.Join("\n", issues));
            Assert.That(sundays, Is.GreaterThanOrEqualTo(1), "no Sunday");
            CollectionAssert.IsEmpty(errors.Distinct().ToList(), "errors/exceptions logged during the soak");
            CollectionAssert.IsEmpty(issues, "soak issues");
            Assert.That(netObjsMax - netObjs0, Is.LessThanOrEqualTo(40), "NetworkObjects growing day over day (leak?)");
            Assert.That(memEnd - mem0, Is.LessThan(512), "memory growth over the week > 512 MB");
        }
    }
}
