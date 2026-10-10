# Cursed Crops

> **Status**: Draft · **Slug**: `cursed-crops` · **Layer**: Feature · **Priority**: VS (prize pumpkin) / Alpha (3) · **Batch**: B
> **Consumes**: Critter Personality (critter-ai), Goods Item (goods-value), Noise Event (proximity-voice), Day Modifier (day-loop)

## 1. Overview
Grandpa's crops are cursed. They are goods with behaviors: some run (critter-ai personalities), some change while carried, some make noise. A curse is active per field and is announced in the morning twist. Crops are high-value and high-risk.

## 2. Player Fantasy
"Even the vegetables hate us." Harvesting is a creepy-funny minigame: chasing turnips through dirt, carrying a screaming mandrake while hushing it, and staggering under a pumpkin that keeps getting bigger as you get closer to market.

## 3. Detailed Rules
| # | Crop | Tier | Personality / behavior | Counter | Base $ | Carry hands | Fragility |
|---|---|---|---|---|---|---|---|
| 1 | **Prize Pumpkin ("Lady Gourdiva")** | VS | Not a critter. Grows while carried: +8% mass and +5% value every 10 s held; at 4× start mass needs 4 hands; drop from > 1.5 m = splat (−60%) | Plan the route, wagon, 4-person carry over the bridge | 200 (grows to ~500) | 2 → 4 | Very high |
| 2 | **Burrowing Turnip** | Alpha | `Trait.Burrower`, skittish. Dives underground (`Nav.Underground`) when threat > 0.5, resurfaces 6–12 m away after 3 s; bunches of 5 | Stomp the dirt mound (stun 2 s), net, approach quietly | 30 each | 1 | Low |
| 3 | **Screaming Mandrake** | Alpha | `Trait.Loud`. When pulled or jostled screams (Noise 0.9, type crop_scream, radius 40 m) for 3 s, cooldown 8 s; attracts night-creatures and spooks livestock | Earmuff hat upgrade, burlap sack (−70% loudness), gently carry (no sprint) | 150 | 1 | Medium |
| 4 | **Maze Corn** | Alpha | Field, not item: corn maze walls rearrange every 90 s; golden cobs (items) inside | Call out via proximity voice, mark paths with a scarecrow stake | 40 per cob | 1 | Low |
| 5 | **Tumble Melon** (EA) | EA | Rolls downhill whenever set down; `Juke` personality on slopes | Wedge, wagon | 80 | 2 | High |
| 6 | **Moonberry Bush** (EA) | EA | Berries ripen only at night; picking them glows (Lure.Light for night creatures) | Pick in pairs, one lookout | 15 per handful (×6) | 1 | Very high |

## 4. Formulas
- **Pumpkin growth**: `Mass(t) = M0 × (1.08)^(t_held/10)`, capped at 4× M0 (≈ 180 s held). `Value = Base × (1.05)^(t_held/10)`, capped at 2.5×. `HandsRequired = ceil(Mass/M0 × 1)` clamped 2–4. Growth pauses in wagon.
- **Mandrake scream loudness** = `0.9 × SackMult(0.3) × EarmuffNA` (earmuffs protect the wearer from the stun only). Jostle trigger: carrier acceleration > 800 cm/s² or impact > 200 N.
- **Turnip resurfacing**: random point on navmesh 6–12 m from dive point, away from threat centroid (±60°).
- **Maze rearrange**: every 90 s ± 15 s, swaps 30% of wall segments; never traps a player (path to exit guaranteed).

## 5. Edge Cases
- Pumpkin at 4× while 2 players carry: drops after 1.5 s warning creak (not instant).
- Mandrake screams inside the wagon: still screams; wagon dampens 20%.
- Turnip burrows under bridge: cannot (no Underground nav on bridge); falls in river instead.
- Maze rearranges onto a player: wall spawns around them; never on.
- Mandrake + Scarecrow night: scream draws Scarecrow aggro (night-creatures).

## 6. Dependencies
critter-ai, goods-value, physics-carry, proximity-voice (emits Noise Events), day-loop (curse as Day Modifier `Curse.*`), night-creatures (attraction), farm-upgrades (earmuffs, sacks), audio-feedback.

## 7. Tuning Knobs
Pumpkin growth rate (8%/10 s, 4–12%), cap (4×), splat height (1.5 m); mandrake loudness (0.9), cooldown (8 s); turnip resurface range (6–12 m); maze interval (90 s, 60–150).

## 8. Acceptance Criteria
1. VS pumpkin needs 4 hands after ~180 s of carry and its value at market reflects growth and bruising.
2. A mandrake scream registers as a Noise Event that sends livestock within 20 m into Flee.
3. Turnips resurface within the stated band 100% of the time on valid navmesh.
4. The maze always has a path to the exit (automated test over 1000 seeds).
5. Each curse is announced in the morning order with an icon and a one-line joke.

## Comedy beats
- Lady Gourdiva creaking ominously as the team reaches the middle of the bridge.
- Everyone whispering "shhh" at a vegetable.
- Turnip pops up right behind the friend who said "where'd it go?"
- Maze walls closing between two friends mid-sentence.
