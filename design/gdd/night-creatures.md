# Night Creatures

> **Status**: Draft · **Slug**: `night-creatures` · **Layer**: Feature · **Priority**: Alpha (Scarecrow) / EA (2 + Crows) · **Batch**: B
> **Consumes**: Critter Personality (critter-ai), Noise Event (proximity-voice), Day Modifier + night phase (day-loop)

## 1. Overview
After sundown the farm wakes up. A **Night Director** spends a threat budget to spawn and steer creatures toward noisy players. Creatures are light horror: they knock out, drag or steal, never permanently kill. Knockouts use player-character's barn respawn rules and the respawn fee from shared-wallet-economy.

## 2. Player Fantasy
"We should NOT have stayed out this late." Night is a gamble: the dusk bonus is tempting, but the farm gets scary. Silence and teamwork keep you safe; the funny screams on voice chat are the point.

## 3. Detailed Rules

### 3.1 Roster
| Creature | Tier | Personality | Behavior | Counter | Penalty |
|---|---|---|---|---|---|
| **The Scarecrow ("Mr. Stitches")** | Alpha | Stalker, patient, petty | Moves only when no player is looking at it (weeping-angel rule). Teleports between field posts. Grabbing touch = knockout | Keep eyes on it; assign a watcher; lantern upgrade freezes it 3 s | Knockout + drops carried goods |
| **The Barn Thing** | EA | Hungry, territorial | Lives in the hay; drags the nearest player within 6 m into the hay over 4 s. Others can pull them out (tug, 2 hands) | Stay out of hay at night; rescue by pulling; feed it a turnip (distraction 10 s) | Dragged player knocked out if not rescued |
| **The Crows ("The Committee")** | EA | Thieves, gossipy, flock | Flocks of 5–8 swoop and steal `Lure.Shiny` tools (whistle, lantern) and small goods (chickens' eggs, berries); fly to a nest tree | Shoo with noise ≥ 0.6 (yes, shouting helps here); retrieve from nest | Lost tool until recovered from nest |
| **The Headless Hen** (Post) | Post | Dramatic | Runs at full speed in random directions; touching it gives a 5 s "possessed" control inversion | Avoid | Inverted controls |

### 3.2 Night Director
- Night begins at sundown (day-loop). Budget per night = `B = 10 + 4 × DayIndex + 3 × PlayersOutside`.
- Costs: Scarecrow 8, Barn Thing 10, Crow flock 4. Max 1 Scarecrow, 1 Barn Thing, 2 flocks active.
- Every 20 s the Director picks the loudest-zone (sum of Noise Event loudness in last 20 s per 20 m cell) and steers creatures there.
- Mandrake screams count ×2.

### 3.3 Scarecrow "looked at" rule
Observed if inside any player's view frustum within 40 m with LOS (≥ 15% of bounding box visible). Fog (weather-twists) reduces to 15 m. When unobserved: moves at 700 cm/s toward target.

## 4. Formulas
- **Night budget**: above. Example Day 3, 3 players outside: 10 + 12 + 9 = 31.
- **Attraction** of a cell = `Σ loudness × typeMult` (crop_scream 2.0, voice 1.0, impact 0.7, tool 0.5).
- **Barn Thing drag**: `DragProgress += 0.25/s − 0.2/s × RescuerHands`; at 1.0 player knocked out; 2 rescuers reverse it.
- **Crow steal chance** per swoop = `0.6 − 0.3 × (holder loudness ≥ 0.6)`.

## 5. Edge Cases
- All players looking at the Scarecrow from behind walls (no LOS): it moves.
- One player AFK in a corner staring at the Scarecrow: AFK detection (60 s idle) ignores that view.
- Player knocked out while carrying the pumpkin: pumpkin drops (bruise per goods-value).
- Crow steals a tool mid-use: tool interaction cancels cleanly.
- Host's view counts the same as clients' (server-validated frustum from replicated camera).
- Scarecrow cannot enter the market zone or the bridge (safe zones).

## 6. Dependencies
critter-ai (Personalities: Stalker, Thief, Nocturnal), proximity-voice (Noise), day-loop (night phase, dusk bonus), player-character (knockout/respawn), farm-tools / farm-upgrades (lantern), weather-twists (fog), cursed-crops (mandrake), audio-feedback (music states), camcorder-replay (jump-scare trigger), blame-ledger (`summoned_scarecrow` verb to the loudest player).

## 7. Tuning Knobs
Budget base/per-day/per-player (10/4/3); creature costs; Scarecrow speed (700, 400–900); view distance (40 m); drag rate (0.25/s); steer interval (20 s).

## 8. Acceptance Criteria
1. Scarecrow never moves while observed by any player (automated test with bots in 4 views).
2. The loudest cell receives creature attention within 25 s in ≥ 90% of trials.
3. Two rescuers always free a dragged player if they start before 0.8 progress.
4. No creature causes permanent death or run loss directly.
5. Playtesters rate night "scary-funny" (≥ 4/5) and ≥ 50% of teams choose to risk dusk at least once.

## Comedy beats
- "Nobody blink." Four friends backing toward the bridge, staring at a scarecrow.
- The Barn Thing tug-of-war, with the friend's legs sticking out of the hay.
- Crows stealing the whistle and you hearing it whistle from the treetop.
- The Blame Ledger awards "Dinner Bell" to whoever screamed the most.
