# Eviction Mode

> **Status**: Draft
> **Author**: game-designer + ux-designer (Batch D)
> **Last Updated**: 2026-10-10
> **Last Verified**: 2026-10-10
> **Implements Pillar**: The Farm Gets Worse, You Get Better · Failure Is the Content

## Summary
The endgame: an endless quota ladder on a shared daily seed. Weeks never stop, the quota never stops rising, and the crew's score is how far they got before eviction. Global and friends leaderboards make "one more run" a social challenge.

> **Quick reference** — Layer: `Feature` · Priority: `EA` · Key deps: `week-quota`, `region-campaign`, `save-persistence`

## Overview
Unlocked after clearing Region 1. A run uses a region profile (Daily: rotating; Custom: any unlocked) with a fixed seed, shortened 3-day weeks, and an exponential quota with no cap. Each Sunday paid adds a "Rung". Each rung also adds one permanent **Eviction Notice** modifier (random from a curated deck). The run ends at the first unpaid Sunday. Score = rungs reached, then total $ earned as tie-break.

## Player Fantasy
"We got to Rung 9 before the bull, the fog and the Landlord's nephew ended us. The Swedish team got 14. Again."

## Detailed Design

### Core Rules
1. **Modes:** **Daily Eviction** (one seed per UTC day, same region/modifiers for everyone, 1 ranked attempt per lobby leader per day, unlimited unranked retries) and **Custom Eviction** (any seed, unranked, friends board only).
2. **Week length:** 3 days + Sunday (knob). Upgrades shop between days as normal.
3. **Starting state:** $150 wallet, region's default upgrades, 2 starting tools picked from the account pool.
4. **Eviction Notices:** after each paid Sunday draw 1 of 3 offered Notices (crew votes, host breaks ties, 20 s). Notices are permanent Day Modifiers (e.g., "Bridge planks −1", "All pigs greased", "Crows +2", "Fog every third day", "Quota +10%, upgrades −15% cost"). Max 2 "major" modifiers active per the overview's director cap, rising +1 every 3 rungs, max 6 in Eviction.
5. **Scoring:** primary `Rung` (int), secondary `TotalEarned`, tertiary lowest `CrewBlameScore` (cleaner team wins ties).
6. **Leaderboards (Steam):** Daily global per crew size (1, 2, 3, 4 players), All-time, Friends. Entry submitted by host with seed hash + run checksum; mismatched entries shown flagged and excluded from global.
7. **Save:** run saved at day boundaries; quitting mid-day forfeits the day (ranked) to block save-scumming.

### States and Transitions
| State | Entry | Exit |
|---|---|---|
| Lobby | Mode chosen | Start |
| Climbing | Start / rung paid | Sunday unpaid → Evicted |
| NoticeDraft | Sunday paid | Vote done → Climbing |
| Evicted | Unpaid Sunday | Results screen → Lobby |

### Interactions with Other Systems
- **In:** quota/Sunday from week-quota; Region Profile + seed from region-campaign; Day Modifier contract (day-loop); Blame totals (blame-ledger).
- **Out:** score to Steam leaderboards; counters to meta-progression (letters "Rung 5", "Rung 10"); daily headline tape slot (camcorder-replay, local display).

## Formulas
```
EvictionQuota(r) = Q0 * G^r * CrewScale(players)
```
| Var | Default | Notes |
|---|---|---|
| Q0 | $400 | rung 0 quota (3-day week) |
| G | 1.28 | growth per rung |
| r | 0.. | rung index |
| CrewScale | 1p 0.45, 2p 0.7, 3p 0.85, 4p 1.0 | |

Rung 5 ≈ $1,375; Rung 10 ≈ $4,730; Rung 15 ≈ $16,300. Target median: Rung 6; top 1%: Rung 13+.

Daily seed: `DailySeed = xxHash32("HP-EVICT", YYYYMMDD)`; region = `DailySeed % UnlockedRegionCountForLaunchPool`.

## Edge Cases
| Scenario | Behavior |
|---|---|
| Player lacks today's region | Daily pool only uses released regions all owners have; Region 1 fallback |
| Host disconnects | Run saved at last day boundary; ranked attempt resumable once within 24 h |
| Crew size changes mid-run | Scored under max crew size reached |
| Notice vote no input | Random of 3 |
| Integer overflow at absurd rungs | Quota clamps at $9,999,999 and displays "∞ (The Landlord laughs)" |

## Dependencies
| System | Direction |
|---|---|
| week-quota | This depends on (Sunday flow) |
| region-campaign | This depends on (profiles, seeds) |
| save-persistence | This depends on |
| day-loop | Notices use Day Modifier contract |
| blame-ledger | Tie-break input |
| meta-progression, hud-menus | Consume results |

## Tuning Knobs
| Knob | Default | Range |
|---|---|---|
| Q0 | 400 | 250–600 |
| G | 1.28 | 1.15–1.4 |
| DaysPerWeek | 3 | 2–5 |
| NoticesOffered | 3 | 2–4 |
| MajorCapGrowth | +1 / 3 rungs | |
| StartWallet | 150 | 0–400 |

## Acceptance Criteria
1. Two lobbies on the same Daily seed get identical region, layouts per week and Notice offers.
2. Leaderboard submission rejected/flagged when checksum mismatches (test with tampered save).
3. Playtest median eviction at Rung 5–7 for experienced 4-player crews.
4. Mid-day quit on ranked forfeits the day (automated test).

## Comedy beats
- Eviction cutscene: the crew's belongings tossed onto the road, the pig is kept by the Landlord, who waves its trotter.
- Notices are written as passive-aggressive memos: "Per management, gravity is now optional on Tuesdays."
- Results screen headline is the run's worst Blame line, framed like a newspaper.
