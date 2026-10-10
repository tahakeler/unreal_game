# Blame Ledger & Awards

> **Status**: Draft
> **Author**: game-designer + ux-designer (Batch D)
> **Last Updated**: 2026-10-10
> **Last Verified**: 2026-10-10
> **Implements Pillar**: Heavy Together, Blamed Together · Failure Is the Content

## Summary
The Blame Ledger records every costly thing a player does (bruising, dropping, losing, stealing, dying) as a **Blame Event**, then turns the pile into an itemized end-of-day receipt, end-of-week awards and the Sunday betrayal reveal. It exists to make the shared wallet personal: money is shared, guilt is not.

> **Quick reference** — Layer: `Feature` · Priority: `VS (bruise report) / Alpha (full)` · Key deps: `goods-value`, `shared-wallet-economy`, `day-loop`, `physics-carry`

## Overview
Every system that can cost the crew money emits a Blame Event to the host-side Ledger service. The Ledger attributes each event to one or more players, aggregates them per day and per week, and feeds three outputs: the **Payout Receipt** (end of day), the **Weekly Awards** (Sunday, including Most Valuable Disaster and the golden-egg betrayal reveal), and the **Camcorder trigger** (big events start a clip save). The Ledger never changes the wallet itself; it only records what the economy already deducted or failed to earn.

## Player Fantasy
"It wasn't me." Then the receipt prints, and it was you, three times, for $240. The Ledger is a deadpan accountant reading your crimes aloud to your friends. Being top of the list should feel like winning a prize nobody wants, and the MVD trophy should be fought over.

## Detailed Design

### Contract: Blame Event
Owned by this document. All emitters must use this struct; consumers must not extend it without a revision here.

```
struct FBlameEvent
  EventId        : uint32      // host-assigned, monotonic per session
  Timestamp      : float       // server world time (s) since day start
  DayIndex       : int16       // day within run (0-based)
  Instigators    : array<PlayerId, max 4>   // who is blamed; empty = "Nature"
  PrimaryInstigator : PlayerId | None       // biggest share; ties -> earliest toucher
  BlameShares    : array<float> // same length as Instigators, sums to 1.0
  Verb           : EBlameVerb   // see table
  ItemId         : GoodsItemId | None  // per goods-value Goods Item contract
  ItemTag        : GameplayTag  // e.g. Goods.Livestock.Pig, Tool.Lasso, Bridge.Plank
  DollarDelta    : int32        // <= 0 for losses, > 0 only for Verb=Rescue/StealBack
  Severity       : uint8 0-100  // computed (see Formulas)
  Location       : FVector_NetQuantize
  CauseTag       : GameplayTag  // optional context: Weather.Wind, Critter.Bull, Hazard.River
  bSecret        : bool         // hidden until Sunday (golden egg)
```

**EBlameVerb** (v1, 16 verbs; add-only):

| Verb | Typical emitter | Receipt line template |
|---|---|---|
| Bruised | goods-value | "{P}: bruised {item} ×{n}" |
| Dropped | physics-carry | "{P}: dropped {item}" |
| DroppedInRiver | bridge-hazards | "{P}: dropped {item} into river ×{n}" |
| Threw | physics-carry | "{P}: yeeted {item}" |
| LetEscape | critter-ai | "{P}: let {item} escape" |
| BrokePlank | bridge-hazards | "{P}: broke a plank (again)" |
| TippedWagon | wagon-vehicles | "{P}: tipped the wagon" |
| KnockedOut | player-character | "{P}: got knocked out (respawn fee)" |
| LostTool | farm-tools | "{P}: lost the {tool}" |
| SpookedHerd | proximity-voice | "{P}: screamed, {n} animals fled" |
| SummonedCreature | night-creatures | "{P}: woke the {creature}" |
| Stolen | rival-farmers | "{P}: left {item} unguarded" |
| Pocketed | goods-value (golden egg) | secret until Sunday |
| LandlordFine | landlord-events | "{P}: failed hat inspection" |
| Rescue | any | "{P}: saved {item}" (positive) |
| StealBack | rival-farmers | "{P}: stole {item} back" (positive) |

**Emission rules**
1. Only the host emits. Clients never construct events (anti-cheat, single source of truth).
2. Emit once per incident. Repeated damage on the same item by the same instigator within **1.5 s** merges into one event (DollarDelta summed, Severity recomputed).
3. Events with |DollarDelta| < **$2** and no non-economic verb (KnockedOut, SummonedCreature) are discarded as noise.
4. Attribution: the emitter supplies a "touch history" (last 3 players who touched the item in the last **4 s**). Shares: last toucher 0.6, previous 0.3, earlier 0.1, renormalized for fewer touchers. No toucher in 4 s → "Nature" (shown with CauseTag, e.g. "The Wind: −$40").
5. Severity ≥ **60** (or Verb in {DroppedInRiver, TippedWagon, BrokePlank with item loss}) broadcasts `OnDisaster(BlameEvent)` for `camcorder-replay` and `audio-feedback`.

### Core Rules
1. Ledger stores all events for the current day and a per-player rollup for the current week. Raw events older than the current week are discarded; weekly rollups go to `save-persistence` for the run.
2. **Payout Receipt (end of day):** groups events by PrimaryInstigator then Verb, shows top **5** lines per player (collapsed "+n more"), each with count and $ total. Positive lines are shown in green after negative ones. A "Nature" block appears last.
3. **Daily headline:** the single highest-Severity event of the day becomes the receipt headline and the Camcorder "Clip of the Day" candidate.
4. **Weekly Awards (Sunday, before quota payment):** five awards, each given to one player:
   - **Most Valuable Disaster (MVD):** highest weekly Blame Score.
   - **Butterfingers:** most Dropped + Threw events.
   - **Bridge Troll:** most BrokePlank + DroppedInRiver.
   - **Pig Whisperer:** highest positive $ (Rescue + StealBack + clean deliveries reported by day-loop).
   - **Town Crier:** most SpookedHerd + SummonedCreature.
   Ties go to the player with the earlier first qualifying event.
5. **Betrayal reveal:** Pocketed events are bSecret. On Sunday after awards, the Ledger plays "Grandpa's audit": each pocketed item is revealed with the thief's name and value. Pocketed value is added to the thief's personal cosmetic tally (`meta-progression`) and not to the wallet. Lobby option `BetrayalEnabled` (default ON, host toggle) disables pocketing entirely.
6. Ledger is read-only toward money. `shared-wallet-economy` remains the authority on balances; Ledger mirrors DollarDelta for display.

### States and Transitions
| State | Entry | Exit | Behavior |
|---|---|---|---|
| Recording | Day start (day-loop) | Sundown/payout | Accept events, merge, broadcast disasters |
| Receipt | Payout phase begins | All players press Continue or 25 s | Locked; renders receipt via hud-menus |
| WeekRollup | Between-day shop | Next day start | Daily totals added to week rollup |
| SundayAudit | Landlord Sunday begins | Awards finished | Awards + betrayal reveal, then hand off to week-quota |

### Interactions with Other Systems
- **In:** Blame Events from goods-value, physics-carry, bridge-hazards, wagon-vehicles, player-character, farm-tools, proximity-voice, night-creatures, rival-farmers, landlord-events, critter-ai.
- **Out:** `OnDisaster` → camcorder-replay, audio-feedback; receipt/award data → hud-menus; awards + lifetime counters → meta-progression (Grandpa's Letters milestones); weekly rollup → save-persistence; per-run Blame totals → eviction-mode (score tie-break).

## Formulas

### Severity
```
Severity = clamp( 100 * |DollarDelta| / DayQuotaShare , 0, 100 ) + VerbBonus
DayQuotaShare = WeeklyQuota / 5
```
| Variable | Type | Range | Source | Description |
|---|---|---|---|---|
| DollarDelta | int | −5000..+2000 | emitter | value lost/gained |
| WeeklyQuota | int | 300..∞ | week-quota | current week quota |
| VerbBonus | int | 0..40 | data table | DroppedInRiver +25, TippedWagon +30, SummonedCreature +20, KnockedOut +10, others 0 |

Result clamped 0–100. Example: Week 1 quota $600 → share $120; dropping a $90 pig in the river = 75 + 25 = 100.

### Blame Score (weekly, per player)
```
BlameScore_p = Σ_events ( |min(DollarDelta,0)| * Share_p ) + 15 * Disasters_p − 0.5 * PositiveDollars_p
```
Disasters_p = events with Severity ≥ 60 where p is PrimaryInstigator. Score floor 0. Typical week-1 range: 0–900.

## Edge Cases
| Scenario | Behavior | Rationale |
|---|---|---|
| Player disconnects mid-day | Events kept under their PlayerId; shown greyed "(fled the farm)" | Leaving doesn't erase guilt; it's funnier |
| Join-in-progress player | Starts at zero; ineligible for MVD if present < 2 days | Fairness |
| Solo play | Receipt shown; awards reduced to MVD + Pig Whisperer, both to the solo player with self-deprecating copy | Keeps joke without empty categories |
| All events are "Nature" | MVD goes to "The Weather" (or top CauseTag) | Comedy fallback |
| Same item bruised by 4 players in 1 s | Single merged event, shares by touch order | Avoid receipt spam |
| Positive and negative events in same merge window | Never merged; different verbs | Clarity |
| Betrayal disabled mid-week | Existing secrets still revealed Sunday | No lost data |
| Host leaves | Day ledger lost with unsaved day (save-persistence boundary) | No migration at EA |
| > 500 events in a day | Oldest Severity < 20 events compacted into per-player "misc" totals | Memory cap |

## Dependencies
| System | Direction | Nature |
|---|---|---|
| goods-value | Ledger depends on | Goods Item ids, values, bruise deltas |
| shared-wallet-economy | Ledger depends on | Authoritative $ deltas, respawn fees |
| day-loop | Ledger depends on | Day start/payout phase events |
| week-quota | Ledger depends on | Quota for Severity; Sunday hook |
| physics-carry, bridge-hazards, wagon-vehicles, player-character, farm-tools, proximity-voice, critter-ai, night-creatures, rival-farmers, landlord-events | Emit into Ledger | Blame Events + touch history |
| camcorder-replay, audio-feedback | Depend on Ledger | OnDisaster |
| hud-menus | Depends on Ledger | Receipt and award data |
| meta-progression, save-persistence, eviction-mode | Depend on Ledger | Counters, rollups, tie-breaks |

## Tuning Knobs
| Knob | Default | Safe range | Effect |
|---|---|---|---|
| MergeWindow | 1.5 s | 0.5–3 | Receipt line density |
| NoiseFloorDollars | $2 | 0–10 | Filters trivial events |
| TouchWindow | 4 s | 2–8 | Attribution memory |
| TouchShares | 0.6/0.3/0.1 | — | Blame spread |
| DisasterSeverity | 60 | 40–80 | Camcorder trigger frequency (target 3–6 per day) |
| ReceiptLinesPerPlayer | 5 | 3–8 | Readability |
| ReceiptTimeout | 25 s | 15–45 | Pacing |
| DisasterBonus (score) | 15 | 0–40 | Weights spectacle vs $ |
| BetrayalEnabled | ON | — | Lobby toggle |

## Acceptance Criteria
1. Dropping a pig off the bridge produces exactly one DroppedInRiver event naming the last carrier with share ≥ 0.6, visible on the receipt.
2. Receipt appears within 1 s of payout phase and lists events grouped per player, max 5 lines each.
3. Severity ≥ 60 events fire OnDisaster; in a 4-player 12-min test day, 3–6 disasters fire (tune knob if not).
4. Clients cannot inject events (server RPC rejected; automated test).
5. Sunday shows all 5 awards in 4-player games and a correct golden-egg reveal; disabled toggle prevents Pocketed events.
6. Ledger never alters wallet balance (unit test: wallet unchanged with ledger disabled vs enabled).

## Comedy beats
- Receipt printed by a dot-matrix printer that jams on the worst line, then prints it larger.
- "The Wind: −$40" as a named culprit; MVD to "The Weather" when nobody's at fault.
- Golden-egg audit: Grandpa's voice reads "…and one egg, *gold*, pocketed by… Dave," followed by a sad trombone sting.
- MVD trophy is a bronzed muddy boot the winner must wear as a hat next week (cosmetic only).
