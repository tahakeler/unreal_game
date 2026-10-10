# Meta Progression, Cosmetics & Almanac

> **Status**: Draft
> **Author**: game-designer + ux-designer (Batch D)
> **Last Updated**: 2026-10-10
> **Last Verified**: 2026-10-10
> **Implements Pillar**: The Farm Gets Worse, You Get Better · Failure Is the Content

## Summary
Account-level progression that survives failed runs: Grandpa's Letters (milestone unlocks), cosmetics, extra starting-tool options and the Almanac collection. It never sells or grants stat power. It rewards failing in interesting ways as much as succeeding.

> **Quick reference** — Layer: `Feature` · Priority: `EA` · Key deps: `save-persistence`, `blame-ledger`, `livestock-roster`, `region-campaign`

## Overview
A per-player account profile tracks lifetime counters (pigs delivered, planks broken, disasters, regions cleared). Crossing thresholds unlocks **Grandpa's Letters**, each granting a reward: a cosmetic, a starting-tool option, an almanac page or a lobby option. Separately the **Almanac** fills as players encounter species, curses, creatures, bruise types and disaster verbs. A soft currency, **Farm Memories**, is earned per day and spent at **Grandpa's Trunk** on cosmetics; it can't buy power.

## Player Fantasy
"We lost the farm again, but I unlocked the chicken hat for dropping 50 chickens." Every session leaves something behind, even a disaster.

## Detailed Design

### Core Rules
1. Profile is per player, stored locally and in Steam Cloud (save-persistence). Host does not own guests' progress; each client increments its own counters from replicated events (verified by host for ranked eviction-mode only).
2. **Letters:** 60 at EA, in 4 tiers. Each Letter = (counter, threshold, reward). Examples:
   | Letter | Condition | Reward |
   |---|---|---|
   | "Welcome Home" | Finish first day | Straw hat |
   | "Pork Logistics" | Deliver 50 pigs | Pig-nose mask |
   | "Structural Concerns" | Break 10 planks | Plank-patch overalls |
   | "Davy Jones" | 25 goods into river | Diving helmet |
   | "MVD" | Win Most Valuable Disaster 5× | Bronzed boot hat |
   | "Debt Free (sort of)" | Clear Region 1 | Starting tool option: Feed Silo Bucket |
   | "Home Video Star" | Export 10 tapes | VHS camcorder back-prop |
   | "Traitor" | Pocket 5 golden eggs | Gold-tooth grin + lobby title |
   | "Whisperer" | Finish a day with 0 SpookedHerd events | Shush-finger emote |
   | "Evicted" | Fail 3 runs | Cardboard box backpack |
3. **Starting tool pool:** clearing regions and certain letters add tool *options* (not upgrades) to the run's starting pick: crew chooses 2 of N at run start. Options are sidegrades (e.g., Long Lasso: +50% range, −30% pull strength).
4. **Farm Memories (FM):** earned per day: `FM = 10 + 2 * DisastersThatDay(capped 10) + 5 * bDayQuotaShareMet`. Run fail grants a 50 FM consolation. Trunk prices: common 60, rare 120, legendary 240 FM. ~120 cosmetics at EA (hats 40, overalls 25, wagon decals 20, tractor paint 15, megaphone voice filters 10, emotes 10).
5. **Almanac:** entries for every species, curse, night creature, rival, weather, bruise type, Blame verb, region. Entry unlocks on first encounter (silhouette → full page); a page "star" for 3 sub-goals each (e.g., Pig: deliver 1 / deliver unbruised / deliver 10 in one day). 100% Almanac gives the "Grandpa's Hat".
6. Paid supporter packs (post-launch) contain only cosmetics also distinguishable as purchases; never in Trunk rotation exclusivity that affects play.

### States and Transitions
Letters: Locked → Progressing (counter > 0) → Earned (shown at next payout, "Mail" jingle) → Opened (reward claimed in Grandpa's Trunk). Almanac entry: Unknown → Seen → Studied (1–3 stars).

### Interactions with Other Systems
- **In:** counters from blame-ledger (verbs, awards), day-loop (deliveries), region-campaign (clears), camcorder-replay (exports), livestock-roster/cursed-crops/night-creatures (encounters).
- **Out:** cosmetics → player-character (appearance slots), wagon-vehicles (decals); starting tool options → farm-tools at run start; lobby titles → hud-menus.

## Formulas
```
FM_day = 10 + 2*min(Disasters,10) + 5*bQuotaShareMet      // 10..35 per day, avg ≈ 22
TrunkTotal = 40*60 + 25*120 + 5*240 = 6600 FM
HoursToCompleteTrunk = TrunkTotal / (avgFM * DaysPerHour) = 6600 / (22 * 5) ≈ 60 h
```
Trunk holds 70 cosmetics (prices: common 60, rare 120, legendary 240 FM); Letters supply the other ~50 cosmetics free.

## Edge Cases
| Scenario | Behavior |
|---|---|
| Offline/no cloud | Local profile, sync later; conflicts resolved by max(counter) |
| Edited save | Cosmetic-only impact; eviction-mode leaderboard flags mismatches |
| Guest leaves mid-day | Counters already earned that day are kept |
| Cosmetic colored red | Bull still charges it (gameplay hook is diegetic, warned via tooltip) |

## Dependencies
| System | Direction |
|---|---|
| save-persistence | This depends on (profile storage) |
| blame-ledger, day-loop, region-campaign, camcorder-replay, livestock-roster, cursed-crops, night-creatures | Feed counters |
| player-character, wagon-vehicles, farm-tools, hud-menus | Consume rewards |

## Tuning Knobs
| Knob | Default | Range |
|---|---|---|
| FM base/day | 10 | 5–20 |
| FM per disaster | 2 | 0–5 |
| Trunk prices | 60/120/240 | ±50% |
| Letter thresholds | per table | ±50% |
| Starting tool picks | 2 | 1–3 |

## Acceptance Criteria
1. No unlock changes any numeric gameplay stat beyond sidegrade tool options (data audit).
2. Failing a run preserves 100% of profile data (automated test).
3. Earned letters show on the next payout within 2 s; Almanac pages show on first encounter.
4. Median player earns a new cosmetic every ≤ 45 min in the first 10 h (telemetry).

## Comedy beats
- Letters are in Grandpa's shaky handwriting with increasingly unhinged P.S. notes ("P.S. Never trust the goat").
- Disaster-rewarding letters outnumber success letters: failure literally pays.
- The "Evicted" cardboard box backpack can be hidden under, like a cartoon.
