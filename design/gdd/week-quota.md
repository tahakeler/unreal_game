# Week Loop, Quota & Landlord Sunday

> **Status**: Draft
> **Author**: economy-designer + systems-designer (Batch C)
> **Last Updated**: 2026-10-10
> **Implements Pillar**: The Farm Gets Worse, You Get Better · Failure Is the Content
> **Quick reference**: Layer `Feature` · Priority `Alpha` · Key deps `day-loop`, `shared-wallet-economy`

## Summary

A week is five working days followed by Landlord Sunday, when the crew must pay an escalating quota from the shared wallet or the run ends. This is the session-scale pressure that makes every bruise and every respawn matter.

## 1. Overview

The week loop counts days (Mon–Fri), triggers Landlord Sunday, computes the quota, resolves payment or failure, applies weekly escalation (quota ×1.35, one new Landlord rule from `landlord-events`, higher chaos cap) and hands the crew to the next week or to the region-clear / eviction screens. A Region is cleared by paying **5** consecutive Sundays (Region 1); `region-campaign` may set 4–6 for later regions.

## 2. Player Fantasy

"The Landlord is coming and we are $60 short." Friday should feel like a heist's last minutes; Sunday should be a courtroom with a ticking cash counter, a gasp, and either a hug or a beautifully catastrophic eviction cutscene.

## 3. Detailed Rules

1. Week = Day 1..5 (Mon–Fri) via `day-loop`, then Sunday (no work phase). Saturday doesn't exist ("The Landlord doesn't believe in Saturdays").
2. Quota for week `w` is shown from Monday morning on the barn chalkboard and HUD.
3. **Sunday sequence** (~90 s, skippable after first time per run):
   a. Landlord walks to the barn. Wallet counter rolls down by the quota.
   b. **Pocket reveal**: any pocketed Golden Eggs are revealed by `blame-ledger`; the pocketing player may "donate" now (adds its $150 at demand 1.0) or keep it for account Almanac credit (betrayal).
   c. If `Balance ≥ Q(w) + IOU`: **Paid**. Remainder carries over. Week MVP/MVD awards.
   d. If short: **Collateral** prompt, 60 s: the crew may sell back any upgrades at 50% (`farm-upgrades`). Any player may vote "Give up".
   e. Still short at timer end: **Evicted**. Run ends; `save-persistence` wipes the run save; account unlocks persist; eviction cutscene plays, Camcorder montage of the week.
4. After a paid Sunday: week counter +1; new Landlord weekly rule drawn; chaos cap rises (`day-loop`); respawn fee rises (`shared-wallet-economy`); new order species may unlock (`region-campaign`).
5. Paying the final week of a region → **Region Cleared**: rewards to account profile, run ends in victory (next region starts a fresh run with its own quota base).
6. Player count is sampled at **Monday morning**; quota is fixed for the week even if players join/leave (joiners mid-week don't raise it; leavers don't lower it, but see Edge Cases).

## 4. Formulas

**Quota**
```
Q(w) = round_to_10( Q_base × G^(w−1) × S_p × R_mult )
```
- `Q_base` = $1,200, `G` = 1.35, `R_mult` = region multiplier (Region 1 = 1.0; Region 2 = 1.6; Region 3 = 2.4, set by `region-campaign`).
- `S_p` player scale: 1p 0.45, 2p 0.65, 3p 0.85, 4p 1.00.

| w | 4p | 3p | 2p | 1p |
|---|---|---|---|---|
| 1 | 1,200 | 1,020 | 780 | 540 |
| 2 | 1,620 | 1,380 | 1,050 | 730 |
| 3 | 2,190 | 1,860 | 1,420 | 980 |
| 4 | 2,950 | 2,510 | 1,920 | 1,330 |
| 5 | 3,990 | 3,390 | 2,590 | 1,790 |

(All docs use these rounded values.)

**Amount due Sunday**: `Due = Q(w) + IOU_w` where `IOU_w` is overdraft overflow from `shared-wallet-economy`, plus any Landlord "Double Down" term (`landlord-events`).

**Week 1 sanity (4p)**: start $100 + 5 days × ~$400 − ~$360 costs = ~$1,740 vs $1,200 → ~$540 slack for 1–2 cheap upgrades. Intentionally generous: teaching week.

**Region 1 total paid** (4p) = Σ Q(1..5) ≈ $11,950.

## 5. Edge Cases
- **Player leaves mid-week** in 4p → 3p: from the next day, quota is re-scaled pro-rata for remaining days: `Q' = Q × (d_done + d_left × S_new/S_old) / 5`. Prevents a ragequit from dooming the others.
- **Host leaves on Sunday mid-sequence**: Sunday re-runs on resume from the Friday Shop save.
- **Balance exactly equals Due**: Paid with $0 carryover ("the Landlord seems disappointed").
- **Collateral sells everything and still short**: evicted; sold upgrades don't matter (run wipes).
- **All players vote Give up on Collateral**: immediate eviction.
- **Eviction Mode**: uses this system with `G` and region overridden by `eviction-mode` and no region end.
- **Solo**: S_p 0.45; Landlord Farm Walk events still apply.

## 6. Dependencies
- **Upstream**: `day-loop` (day count, Payout/Shop), `shared-wallet-economy` (balance, IOU, payment).
- **Downstream**: `landlord-events` (weekly rule, Sunday host), `farm-upgrades` (sell-back on Collateral), `save-persistence` (week boundary save, wipe on eviction), `region-campaign` (region length, R_mult), `eviction-mode`, `blame-ledger` (pocket reveal, weekly awards), `camcorder-replay` (eviction montage), `meta-progression` (region clear rewards), `hud-menus`.

## 7. Tuning Knobs
| Knob | Default | Range | Note |
|---|---|---|---|
| `Q_base` | $1,200 | 900–1,600 | week 1 difficulty |
| `G` growth | 1.35 | 1.25–1.45 | must exceed income growth (1.25) |
| `S_p` | .45/.65/.85/1.0 | ±0.1 | player-count fairness |
| Weeks per region | 5 | 4–6 | campaign length |
| Collateral timer | 60 s | 30–120 | drama window |
| Sell-back rate | 50% | 25–75% | owned by `farm-upgrades` |

## 8. Acceptance Criteria
1. Q(3) for 4p Region 1 = $2,190; 2p = $1,420.
2. Balance $2,335 vs Due $2,190 → Paid, carryover $145.
3. Balance below Due with sellable upgrades opens Collateral for 60 s; after selling enough, payment succeeds automatically.
4. Eviction deletes the run save and leaves account profile untouched (verified in `save-persistence` test).
5. Player leaving after Day 2 in a 4p week re-scales quota to `Q × (2 + 3×0.85)/5`.
6. Playtest KPI: ≥ 80% of first-time 4p crews pay Week 1; ≤ 50% pay Week 5 on first attempt.

## Comedy beats
- The Landlord counts money with a tiny abacus, out loud, very slowly when you're short.
- Golden Egg betrayal reveal: spotlight on the pocketing player, the crew's ragdolls face him.
- Eviction cutscene: the barn is towed away by the Landlord's car while the pig rides on the roof.
- "Exactly paid" gets a silent Landlord stare and a single sad trombone.
