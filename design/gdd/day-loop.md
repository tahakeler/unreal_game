# Day Loop

> **Status**: Draft
> **Author**: systems-designer + economy-designer (Batch C)
> **Last Updated**: 2026-10-10
> **Implements Pillar**: Everything Has Legs · Failure Is the Content
> **Quick reference**: Layer `Core` · Priority `VS` · Key deps `goods-value`, `session-networking` · **Owns contract: Day Modifier**

## Summary

One day is a 10–15 minute run: read the Morning Order and twist, round up goods, haul them across the bridge, sell at the market before sundown, optionally gamble on a dusk haul, then see the payout and the Blame Ledger. The day loop is the clock and referee of the game, and it owns the Day Modifier contract every chaos source plugs into.

## 1. Overview

The day loop is a host-authoritative state machine (Morning → Workday → Sundown → Dusk (optional) → Payout → Shop/Next) that owns the day timer, the Morning Order, the market zone acceptance rules, the dusk bonus and the day payout summary. It applies Day Modifiers (weather, curses, Landlord events, region rules) through a single parameter-resolution pipeline so no system hard-codes content.

## 2. Player Fantasy

"We have nine minutes, two pigs loose, a pumpkin on the bridge and Dave is in the river." The day should feel like a cooking-show countdown run by farmers who can't cook: clear goal in the morning, mounting panic, a last-second dive across the market line, and a payout screen that's half celebration, half court case.

## 3. Detailed Rules

### Phases
| Phase | Duration | What happens |
|---|---|---|
| **Morning** | 30 s (skippable when all ready) | Morning Order (3 lines) + twist card(s) revealed; players spawn at barn; timers frozen |
| **Workday** | 540 s (9:00) | Round up, haul, sell. Market open. Goods sold on acceptance |
| **Sundown** | 10 s warning at 0:30, then bell | Last 30 s: music shift, sun dips. At 0:00 market closes. Items mid-air over market line still count if they land within 2 s |
| **Dusk** (optional) | 180 s | Vote (majority, host breaks ties, 10 s). Night creatures active (`night-creatures`). Market reopens as "Night Market" with Dusk Bonus ×1.25 |
| **Payout** | ~30 s | Day income, costs, order bonus, Blame Ledger day report (`blame-ledger`) |
| **Shop** | untimed | `farm-upgrades`; autosave (`save-persistence`); ready-up → next day or Sunday |

Day length: 9:30 without dusk, ~12:40 with dusk, ~14 min with payout/shop → fits 10–15 min target.

### Morning Order
- 3 order lines, each `GoodsId × quantity` drawn from species available this region/week (e.g., "3 Pigs · 6 Eggs · 1 Prize Pumpkin").
- Items filling an open line get order multiplier O = 1.5 (see `goods-value`).
- Completing **all 3 lines** pays an **Order Complete Bonus** = $60 × week number (paid once at payout).
- Order budget: sum of BaseValue of all lines ≈ 45% of the expected day gross (see `shared-wallet-economy` economy model).

### Market zone
- A fenced Sale Pen on the far bank. An item is **accepted** when it rests inside the pen for **2.0 s** continuously (living goods can escape back out, on purpose). Accepted items are teleported into the "SOLD" crate with a cash-register sound.
- Sale value computed by `goods-value`; credited by `shared-wallet-economy` instantly; shown above the pen.

### Day end / stranded players
- Players not inside the barn zone or market zone at end of Workday (if no dusk) or end of Dusk are "lost in the dark": sent to the barn and charged a respawn fee (`shared-wallet-economy`), Blame verb `Stranded`.
- Unsold goods: livestock wander back to the wild pool; crops and products despawn (unless stored in Barn Cellar from `farm-upgrades`).

### Contract: Day Modifier

Owned here. Authored as `UDayModifierDefinition` data assets by `weather-twists`, `landlord-events`, `cursed-crops`, `region-campaign`, `eviction-mode`, `farm-upgrades` (negative-sign modifiers allowed) and resolved by `UDayModifierSubsystem` (server, replicated to clients as active-tag list).

| Field | Type | Notes |
|---|---|---|
| `Tag` | GameplayTag | Unique id, e.g. `Mod.Weather.Storm`, `Mod.Landlord.Inspection`, `Mod.Curse.Mandrake` |
| `Category` | enum {Weather, Curse, Creature, Landlord, Region, Eviction, Upgrade} | for UI grouping and caps |
| `Weight` | enum {Minor, Major} | Majors count toward the chaos cap |
| `StackRule` | enum {Unique, Refresh, Stack(MaxStacks), ReplaceGroup(GroupTag)} | see rules below |
| `Duration` | enum {Day (Morning→end of Payout), WorkdayOnly, DuskOnly, Seconds(N), Week, Run} | Run/Week persisted by `save-persistence` |
| `Priority` | int | resolution order for Override ops (higher wins) |
| `Effects` | array of {ParamName (FName), Op {Add, Mul, Override}, Value (float)} | params listed below |
| `GrantTags` | GameplayTagContainer | tags applied to world (e.g. `World.BridgeOut`) for systems to react |
| `CardText` | FText | twist card, comedic one-liner |
| `bAnnounced` | bool | false = secret modifier (revealed by Ledger) |

**Stacking rules**
- `Unique`: second apply ignored. `Refresh`: resets duration. `Stack(N)`: Mul effects multiply per stack, Add effects add, up to N. `ReplaceGroup`: removes any active modifier with the same GroupTag (e.g. one weather at a time: `Group.Weather`).
- **Chaos cap**: active Major count ≤ `MajorCap(w) = min(4, 2 + (w − 1))` → Week 1: 2, Week 2: 3, Week 3+: 4. Excess Majors queue to the next day. Landlord Sunday modifiers bypass the cap.

**Parameter resolution** (each frame a param is read):
```
P = Override_highestPriority ?? clamp( (P_base + Σ Add) × Π Mul , P_min, P_max )
```

**Registered parameters (initial)**: `Day.WorkdaySeconds`, `Day.DuskSeconds`, `Day.DuskBonus`, `Goods.ValueMult`, `Goods.BruiseMult`, `Goods.FreshnessDecayMult`, `Market.DemandMult`, `Market.AcceptSeconds`, `Economy.RespawnFeeMult`, `Economy.IncomeTaxRate`, `Order.LineCount`, `Critter.SpeedMult`, `Player.StaminaMult`, `Bridge.PlankBreakMult`, `Bridge.SafeLoadMult`, `Noise.VoiceRadiusMult` (Landlord "Quiet Hours"), `Noise.EnvMult` (weather/night env_mult for `proximity-voice`). New params are added by registering an `FDayParamSpec {Name, Base, Min, Max}`; consumer systems read via `GetDayParam(Name)` only.

## 4. Formulas

**Workday length**: `T_work = Day.WorkdaySeconds` (base 540, min 300, max 720).

**Dusk value**: during Dusk, `ValueModifierMult *= Day.DuskBonus` (base 1.25, range 1.0–2.0).

**Order Complete Bonus**: `OCB = 60 × w` (w = week number 1..6; Eviction uses ladder step).

**Order quantity per line**: `q = max(1, round(BaseQ_species × S_p × (1 + 0.15 (w−1))))` with `S_p` player scale = the canonical `week-quota` table {1p 0.45, 2p 0.65, 3p 0.85, 4p 1.0}.

**Day gross (reported)**: `G_day = Σ V_sold + OCB` (V from `goods-value`).

**Chaos cap**: `MajorCap(w) = min(4, 1 + w)`.

## 5. Edge Cases
- **Host disconnects mid-day**: day is lost; run resumes from last day-boundary save (Shop autosave). Income from the lost day is not kept (documented in `save-persistence`).
- **Join-in-progress**: allowed in any phase except the Sunday payout cutscene; mid-day joiners spawn at the barn with 2 s spawn protection (`session-networking` owns this rule).
- **All players knocked out**: Workday continues ticking; respawns at barn per `player-character`; fees still apply.
- **Item accepted at 0:00.01 after sundown**: grace window 2 s for items already inside the pen at bell; nothing new accepted after.
- **Override conflict** (two modifiers override `Day.WorkdaySeconds`): highest Priority wins; tie → most recently applied.
- **Modifier changes WorkdaySeconds mid-day**: remaining time scales proportionally (`T_remaining_new = T_remaining × new/old`), never below 30 s.
- **Dusk vote with 2 players split 1–1**: host decides. Solo: prompt.
- **Order line requests a species that is all Ruined/escaped**: line stays open; no reroll (it's the crew's fault).

## 6. Dependencies
- **Upstream**: `goods-value` (sale value), `session-networking` (authority, ready-up).
- **Downstream / consumers**: `shared-wallet-economy` (credits, fees), `week-quota` (day counter, Sunday trigger), `landlord-events`, `weather-twists`, `cursed-crops`, `night-creatures` (dusk), `region-campaign`, `eviction-mode` (Day Modifier contract), `blame-ledger` (day report), `hud-menus` (timer, order board, payout), `audio-feedback` (music states), `save-persistence` (day-boundary save), `farm-upgrades` (Shop phase).

## 7. Tuning Knobs
| Knob | Default | Range | Note |
|---|---|---|---|
| Morning duration | 30 s | 15–60 | |
| `Day.WorkdaySeconds` | 540 | 300–720 | main pacing lever |
| `Day.DuskSeconds` | 180 | 90–300 | |
| `Day.DuskBonus` | 1.25 | 1.0–2.0 | risk/reward of night |
| `Market.AcceptSeconds` | 2.0 | 0.5–5 | escape chance at the pen |
| Order lines | 3 | 2–4 | |
| OCB per week | $60 | 30–120 | |
| MajorCap base / max | 2 / 4 | 1–3 / 3–5 | chaos readability |
| Sundown grace | 2 s | 0–4 | |

## 8. Acceptance Criteria
1. With no modifiers, a day runs Morning 30 s → Workday 540 s → Payout; with dusk voted, Dusk 180 s precedes Payout; timer is identical on all clients (±0.1 s).
2. An item resting in the pen for 2.0 s is sold exactly once; an item leaving at 1.9 s is not sold.
3. Applying `Mod.Weather.Storm` (ReplaceGroup Weather) removes active `Mod.Weather.Fog`.
4. In Week 1, a third Major modifier is queued to the next day, not applied.
5. `GetDayParam("Goods.ValueMult")` with Add +0.2 and Mul ×1.5 and ×0.8 returns (1+0.2)×1.2 = 1.44.
6. Completing all 3 order lines in Week 3 adds exactly $180 to the payout.
7. No system other than `UDayModifierSubsystem` mutates registered day params (code review check).

## Comedy beats
- The market pen's 2-second acceptance means a pig can walk back out of being sold. The cash register goes *ka-ch...* and stops.
- Twist cards are written as Grandpa's diary: "Storm today. Bridge will leave. Do not follow bridge."
- The sundown bell rings while someone is mid-dive; the 2-second grace turns it into a photo finish.
- Dusk vote screen: "Go out at night for 25% more money?" with a Scarecrow visible in the background of the vote UI.
