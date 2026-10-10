# Farm Upgrades & Shop

> **Status**: Draft
> **Author**: economy-designer (Batch C)
> **Last Updated**: 2026-10-10
> **Implements Pillar**: The Farm Gets Worse, You Get Better · Heavy Together, Blamed Together
> **Quick reference**: Layer `Feature` · Priority `Alpha` (~10) / `EA` (~30) · Key deps `shared-wallet-economy`, `week-quota`

## Summary

Between days the crew spends the shared wallet on farm upgrades: a bigger wagon, an electric fence, a guard dog that bites everyone. Upgrades are the main lever that lets income grow nearly as fast as the quota, and the main thing crews argue about while broke.

## 1. Overview

The Shop opens in the Shop phase of `day-loop` (after Payout). Upgrades are run-scoped (wiped on eviction), bought with the shared wallet by **majority vote**, and each one is implemented as a permanent `Upgrade`-category Day Modifier (Duration `Run`) and/or a spawned actor. Account-level unlocks (`meta-progression`) can add upgrades to the shop pool but never grant them for free.

## 2. Player Fantasy

"If we buy the fence, the pigs stop escaping. If we buy the wagon, we carry twice as much. We can only afford one and Sarah wants the guard dog." Buying should feel like a group bet; seeing it pay off should feel like the farm finally working for you, briefly.

## 3. Detailed Rules
1. Shop shows 6 offers per Shop phase: 4 from the unlocked pool weighted by week tier, plus 2 "staples" (Bridge Reinforcement, Bigger Wagon) until bought.
2. Purchase: any player proposes; 10 s vote; majority (host breaks tie). Solo: instant.
3. Requires Balance ≥ price (no overdraft).
4. Tiers: some upgrades have Lv1→Lv3; each level is a separate purchase.
5. **Sell-back**: 50% of price, in Shop phase or Sunday Collateral (`week-quota`).
6. Consumables (single-use, marked ◆) are restocked each day and don't sell back.
7. Upgrades that create chaos (Guard Dog, Pig Cannon) are intentionally double-edged.

### Catalog (Region 1, EA target ≈ 30)

| Upgrade | Price | Effect | Phase |
|---|---|---|---|
| Bigger Wagon Lv1/2/3 | 400 / 700 / 1,100 | Wagon slots 4→6→8→10 | Alpha |
| Bridge Reinforcement Lv1/2 | 300 / 600 | `Bridge.PlankBreakMult` ×0.7 / ×0.45 | Alpha |
| Electric Fence | 250 | Pen escape −60%; zaps players too (stun 1 s) | Alpha |
| Guard Dog | 350 | Herds livestock toward pens; 20% chance/min to chase a player | Alpha |
| Scarecrow Decoy ◆ | 40 | Distracts crows/night creatures 60 s | Alpha |
| Feed Silo | 450 | Lures: livestock spawn 25% closer to barn | Alpha |
| Padded Crates | 300 | `Goods.BruiseMult` ×0.8 in wagon | Alpha |
| Barn Cellar | 500 | Store 6 goods overnight (freshness paused) | Alpha |
| Better Nets | 200 | Net catch radius +30% | Alpha |
| Lasso Lv2 | 250 | Lasso range +40% | Alpha |
| Market Stall Sign | 350 | `Market.DemandMult` +0.05 | Alpha |
| Radio Set | 300 | Long-range voice channel (`proximity-voice`) | Alpha |
| Lantern Posts | 250 | Night creature aggro −25% near lit path | EA |
| Hay Bale Ramps | 150 | Reposition ramps; cushions falls (BP −50% on landing) | EA |
| Pig Cannon | 900 | Fires goods across river; adds 25 BP on landing | EA |
| Zipline | 800 | Second crossing route; 1-hand goods only | EA |
| Insurance Policy | 400 | First 3 respawn fees each day are halved | EA |
| Cow Bell Collars | 200 | Livestock emit noise, easy to track | EA |
| Hat Rack ◆ | 30 | 4 hats for "Sunday Best" rule | EA |
| Rubber Boots | 150 | Mud slow −50%, immune to "Muddy Boots" fine | EA |
| Stamina Tonic ◆ | 25 | +50% stamina for one day, one player | EA |
| Second Pen | 300 | Holding capacity +8 | EA |
| Rival Traps | 350 | Slow rival farmers (`rival-farmers`) | EA |
| Crow Net | 300 | Crows can't steal coins | EA |
| Morning Coffee | 200 | Morning phase lasts 15 s longer, +10 s workday | EA |
| Wheelbarrows ×2 | 250 | 2-slot one-player carts | EA |
| Lucky Horseshoe | 600 | Daily twist offers a reroll once per week | EA |
| Megaphone | 150 | Shout reaches 2× radius (also scares livestock) | EA |
| Grandpa's Shotgun-of-Confetti ◆ | 60 | Scatters all creatures within 15 m | EA |
| Scale & Ledger Book | 250 | Shows expected sale value before acceptance | EA |

## 4. Formulas

**Payback target** (pricing rule): `Price ≤ 1.5 × ExpectedWeeklyGain(w_unlock)`, where `ExpectedWeeklyGain` = simulated extra net income per week. E.g. Electric Fence: +$90/day × 5 = $450/wk ≥ 250/1.5 ✓ (payback ≈ 0.55 wk of full use; buying mid-week gives ~3 days).

**Offer weighting**: tier `t` upgrade appears with weight `max(0, 1 − |t − w_tier(w)|×0.5)`, `w_tier = ceil(w / 2)`.

**Sell-back**: `Refund = floor(0.5 × Price_paid)`.

**Cumulative spend target** (4p median): ~$400 W1, ~$450 W2, ~$500 W3, ~$600 W4 → ≈ 20% of gross early, 12–15% late.

## 5. Edge Cases
- **Two votes at once**: one proposal at a time; queue others.
- **Sell-back of an upgrade others depend on** (Lv2 when Lv3 owned): must sell highest level first.
- **Guard dog bites the host during vote**: no effect on vote (it's just funny).
- **Pool exhausted**: show only remaining; empty slots display "Grandpa's IOU (not for sale)".
- **Join-in-progress player**: sees owned upgrades; can vote immediately.
- **Upgrade effect conflicts with Landlord rule**: both apply via Day Modifier resolution.

## 6. Dependencies
- **Upstream**: `shared-wallet-economy` (spend/refund), `week-quota` (Collateral sell-back, week tier), `day-loop` (Shop phase, Day Modifier).
- **Downstream**: `wagon-vehicles`, `bridge-hazards`, `critter-ai`, `farm-tools`, `night-creatures`, `rival-farmers`, `proximity-voice` (effect targets), `save-persistence` (owned list), `hud-menus` (shop UI), `meta-progression` (pool unlocks).

## 7. Tuning Knobs
| Knob | Default | Range |
|---|---|---|
| Offers per shop | 6 | 4–8 |
| Sell-back | 50% | 25–75% |
| Vote time | 10 s | 5–20 |
| Prices | table | ±25% |
| Payback rule | ≤1.5 wk | 1–2.5 wk |

## 8. Acceptance Criteria
1. Buying the Electric Fence at $250 with Balance $260 leaves $10 and applies its modifier next day for the rest of the run.
2. Purchase rejected when Balance < price, including negative balance.
3. Sell-back refunds exactly 50% (floor) and removes the effect immediately.
4. All upgrades are wiped after eviction; account-unlocked pool membership persists.
5. Simulation: every non-consumable meets payback ≤ 1.5 weeks at its unlock week (balance sheet in `/balance-check`).

## Comedy beats
- The Guard Dog has a 20%-per-minute chance to decide the slowest player is livestock.
- Electric Fence zaps the person who voted for it with a ledger line "Self-inflicted −0".
- Pig Cannon: accuracy poor, bruising yes, clips guaranteed.
- Arguing over the vote happens on proximity voice in front of the shop chalkboard.
