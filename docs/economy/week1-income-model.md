# Week-1 income model: Muddlebury (tuned 2026-10-21)

> **Owner**: economy-designer / gameplay-programmer · **Branch**: `fix/beta-bugs-1`
> **Why**: the bot full-week playtest (`docs/qa/playtest/bot-week-report.md`, finding #1, S2) showed week-1 rent was out of reach.
> **Code**: `EconomyTuning.ExtraDailyGoodsPerExtraPlayer` (asset `Resources/Data/Goods/EconomyTuning.asset`), `EconomyMath.ExtraDailyGoods`, `EconomyDirector.RespawnGoods`.
> **Test that pins every number below**: `HarvestPanic/Assets/Scripts/Tests/EditMode/Economy/BetaBugsBalanceTests.cs`.
> **GDD**: `design/master-gdd/09-economy.md` §9.3 and §9.11 (mirrored in `design/HARVEST-PANIC-GDD.md`).

## 1. What was wrong

The rent formula `Q(w) = round10(1,200 × 1.35^(w−1) × S_p)` scales with crew size (`S_p` = 0.45 / 0.65 / 0.85 / 1.00).
In Muddlebury, the goods that earn that rent did not scale:

| Source | Units | Base $ | Notes |
|---|---|---|---|
| Egg crates | 4 per day (4 Goods markers) | 30 | Product: freshness full for 180 s of Workday, then −0.5 over 600 s. 2 hands (1 carrier) |
| Prize pumpkin | 1 per day | 300 | 8 hands: only a 4-player crew can lift it |
| Livestock | **once per run** (spawned at scene load, never restocked) | pig 60 ×3, chicken 15 ×8, goat 70, cow 150 | cow 6 hands (3 carriers) |

Livestock never appears on the order board in Muddlebury: orders draw only from goods with world spawns (crate, pumpkin).
A 3- or 4-player crew got the same 4 crates a day as a solo player, so even a perfect week fell short (§4).

The bots in the soak test made it look worse than it is: they haul only loose goods, and at day scale 0.05 each bot
fits one 126 m round trip into the 36 s market window, so they sell exactly 3 goods a day (see the bug-1 note in §6).

## 2. The fix

Daily loose goods now scale with the crew; rent is unchanged.

```
crates per Morning = 4 (markers) + PerExtraPlayer × (players − 1),  PerExtraPlayer = 2
                   = 4 / 6 / 8 / 10 for 1 / 2 / 3 / 4 players
```

The extras are dealt round-robin over the crate markers and laid out in the free space around them (no overlap, never
on top of props). `players` is sampled for Monday's Morning, like the quota.

## 3. Assumptions (one week, Region 1, week 1)

| Symbol | Value | Why |
|---|---|---|
| `D_day` | 1.0 | middle of the 0.8–1.2 roll |
| Crate | `30 × 0.85 (Scuffed) × 0.9 (average freshness over a 540 s workday) = $23`; on the order line × 1.5 = **$34** | one bump per crate on the 126 m haul |
| Order line | crates `q(p) = max(1, round(2 × S_p))` = 1 / 1 / 2 / 2 a day at × 1.5 | `EconomyMath.OrderQuantity` |
| Saturation | units 7+ of a good × 0.95 per unit (7th crate $22, 8th $21) | `EconomyMath.Demand` |
| Livestock | Scuffed, sold once: 3 pigs $153, 8 chickens $102 (7th/8th saturated), goat $60, cow $128 | no-cow stock **$315**, full stock **$443** |
| Pumpkin day | `300 × 0.85 × 1.5 = $383` + order-complete bonus `$60` = **$443** | 4p only (8 hands); fills both order lines |
| Respawns | one per player per week: W1 fee $15 / $20 / $25 / $25 | `EconomyMath.RespawnFee(1, 1, t, p)` |
| Start | $100 | |
| Competent | 80% of the day's crates, rounded (4 of 4 is "all") | |

```
Week1(p) = 100 + 5 × Σ crate values that day + livestock + pumpkin days × 443 − p × fee(p)
```

## 4. Results

| Crew | Play | Crates/day | Livestock | Week-1 $ | Rent | $ ÷ rent |
|---|---|---|---|---|---|---|
| 1p | every crate | 4 of 4 | none | 600 | 540 | **1.11** pays, with effort |
| 1p | most crates + stock | 3 of 4 | pigs, chickens, goat | 800 | 540 | 1.48 |
| 1p | most crates, no stock | 3 of 4 | none | 485 | 540 | 0.90 short |
| 1p | casual | 2 of 4 | none | 370 | 540 | 0.69 evicted |
| 2p | competent | 5 of 6 | pigs, chickens, goat (no cow: 6 hands) | 1,005 | 780 | **1.29** |
| 3p | competent | 6 of 8 | all | 1,268 | 1,020 | **1.24** |
| 4p | competent, no pumpkin | 8 of 10 | all | 1,458 | 1,200 | **1.22** |
| 4p | competent + 1 pumpkin day | 8 of 10 | all | 1,901 | 1,200 | 1.58 |
| 3p | before the fix, every crate | 4 of 4 | all | 1,038 | 1,020 | 1.02 |
| 4p | before the fix, every crate, no pumpkin | 4 of 4 | all | 1,013 | 1,200 | 0.84 short |

Worked line (3p competent): crates a day `34 + 34 + 4 × 23 = 160`, × 5 days = 800; + stock 443; + start 100;
− 3 respawns × $25 = 75 → **$1,268** against $1,020 (24% slack).

Reading: every 2–4 crew that sells its livestock and ~80% of the crates pays week 1 with ≥ 20% slack, without
needing the pumpkin. A solo player pays only by moving every crate every day, or most crates plus the animals.

## 5. Week 2 and later (watch item, not tuned here)

Livestock is a one-time stock, so it does not come back in week 2, while rent grows × 1.35 (W2: 730 / 1,050 / 1,380 / 1,620).
With the same crate supply, a competent crew's W2 crate income is about 80% of W2 rent, so the week-1 carryover,
the pumpkin (4p), order bonuses and upgrades must close the gap. That matches the GDD intent ("rent grows faster than
income, upgrades close the gap"), but Muddlebury has few week-2 income sources (no egg laying, no crops). Re-check
with real playtests before tuning week 2.

## 6. Related findings from the same playtest

- **"2 of 5 daily goods never sell"** was a bot-harness artifact, not a pen bug: with 3 bots at 10 m/s and a 36 s market
  window (day scale 0.05), each bot completes one 126 m crate run; the 4th crate and the pumpkin (175 m from the pen) are
  still on the farm at close. Every daily good, including the 8-hand pumpkin, sells when placed in the pen
  (`BetaBugsPlayTests.EveryDailyGood_DroppedInSalePen_Sells`). The bots also re-grabbed their own drop from the pen every
  0.5 s, inflating "deliveries"; fixed in `BotFarmers.cs`.
- The soak test still tops up the wallet before Sunday when the bots are short. Its log now lists the goods unsold at close.
