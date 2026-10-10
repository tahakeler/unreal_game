# Cursed Crops

> **Status**: Draft · **Slug**: `cursed-crops` · **Layer**: Feature · **Priority**: VS (prize pumpkin) / Alpha (3: turnip, mandrake, growing pumpkin) / EA (maze corn, tumble melon, moonberry) / Post (region curses) · **Batch**: B
> **Consumes**: Critter Personality (critter-ai), Goods Item (goods-value), Noise Event (proximity-voice), Day Modifier (day-loop)

## 1. Overview
Grandpa's crops are cursed. They are goods with behaviors: some run (critter-ai personalities), some change while carried, some make noise. A curse is active per field and is announced in the morning twist. Crops are high-value and high-risk.

## 2. Player Fantasy
"Even the vegetables hate us." Harvesting is a creepy-funny minigame: chasing turnips through dirt, carrying a screaming mandrake while hushing it, and staggering under a pumpkin that keeps getting bigger as you get closer to market.

## 3. Detailed Rules
| # | Crop | Tier | Personality / behavior | Counter | Base $ | Carry hands | Fragility |
|---|---|---|---|---|---|---|---|
| 1 | **Prize Pumpkin ("Lady Gourdiva")** | VS | Not a critter. Huge and fragile; a fall > 1.5 m adds +60 BP ("splat", per-hit cap in goods-value) | Plan the route, wagon, 4-person carry over the bridge | 300 | 8 hands (4 carriers) | 1.5 |
| 1b | **Growing Pumpkin** | Alpha | Not a critter. Grows while carried: +8% mass and +5% value every 10 s held; at 4× start mass needs 8 hands (4 carriers) | Short routes, wagon (growth pauses) | 40 (grows to 100) | 2 → 8 hands (1 → 4 carriers) | 1.3 |
| 2 | **Burrowing Turnip** | Alpha | `Trait.Burrower`, skittish. Dives underground (`Nav.Underground`) when threat > 0.5, resurfaces 6–12 m away after 3 s; bunches of 5 | Stomp the dirt mound (stun 2 s), net, approach quietly | 10 each | 1 | 1.0 |
| 3 | **Screaming Mandrake** | Alpha | `Trait.Loud`. When pulled or jostled screams (Noise Event Source=Crop, loudness 0.9, tag `Noise.Mandrake`, radius ≈ 43 m per proximity-voice formula) for 3 s, cooldown 8 s; attracts night-creatures and spooks livestock; players within 3 m without earmuffs are stunned 1 s (cover ears) | Earmuff Hat upgrade, Burlap Sack consumable (−70% loudness), gently carry (no sprint) | 45 | 1 | 1.2 |
| 4 | **Maze Corn** | EA | Field, not item: corn maze walls rearrange every 90 s; golden cobs (items) inside | Call out via proximity voice, mark paths with a scarecrow stake | 40 per cob | 1 | 1.0 |
| 5 | **Tumble Melon** (EA) | EA | Rolls downhill whenever set down; `Juke` personality on slopes | Wedge, wagon | 80 | 4 (2 carriers) | 1.4 |
| 6 | **Moonberry Bush** (EA) | EA | Berries ripen only at night; picking them glows (Lure.Light for night creatures) | Pick in pairs, one lookout | 15 per handful (×6) | 1 | 1.8 |

### Region curses (Post / Expansion stubs, from `region-campaign`)
Floating Gourds and Rice that Wanders (Region 2), Hiding Apples, Jack-o'-Lanterns and Whispering Hedges (Region 3; hedges apply voice Noise ×2 radius nearby), Cactus Fruit (Region 4), Snow Turnips (Region 5). Each is a Goods Item + Critter Personality authored with its region update; values to be added to `goods-value` then.

## 4. Formulas
- **Growing Pumpkin growth**: `Mass(t) = M0 × (1.08)^(t_held/10)`, capped at 4× M0 (≈ 180 s held). `Value = Base × (1.05)^(t_held/10)`, capped at 2.5×. `CarryHands = 2 × ceil(Mass/M0)` clamped 2–8 (hands). Growth pauses in wagon.
- **Mandrake scream loudness** = `0.9 × SackMult(0.3) × EarmuffNA` (earmuffs protect the wearer from the stun only). Jostle trigger: carrier acceleration > 800 cm/s² or impact > 200 N.
- **Turnip resurfacing**: random point on navmesh 6–12 m from dive point, away from threat centroid (±60°).
- **Maze rearrange**: every 90 s ± 15 s, swaps 30% of wall segments; never traps a player (path to exit guaranteed).

## 5. Edge Cases
- Growing Pumpkin at 4× while 2 players carry: drops after 1.5 s warning creak (not instant).
- Mandrake screams inside the wagon: still screams; wagon dampens 20%.
- Turnip burrows under bridge: cannot (no Underground nav on bridge); falls in river instead.
- Maze rearranges onto a player: wall spawns around them; never on.
- Mandrake + Scarecrow night: scream draws Scarecrow aggro (night-creatures).

## 6. Dependencies
critter-ai, goods-value, physics-carry, proximity-voice (emits Noise Events), day-loop (curse as Day Modifier `Curse.*`), night-creatures (attraction), farm-upgrades (Earmuff Hat, Burlap Sack), audio-feedback.

## 7. Tuning Knobs
Pumpkin growth rate (8%/10 s, 4–12%), cap (4×), splat height (1.5 m); mandrake loudness (0.9), cooldown (8 s); turnip resurface range (6–12 m); maze interval (90 s, 60–150).

## 8. Acceptance Criteria
1. VS Prize Pumpkin needs 4 carriers and sells for $450 pristine on an order line; the Alpha Growing Pumpkin needs 4 carriers after ~180 s of carry and its value reflects growth and bruising.
2. A mandrake scream registers as a Noise Event that sends livestock within 20 m into Flee.
3. Turnips resurface within the stated band 100% of the time on valid navmesh.
4. The maze always has a path to the exit (automated test over 1000 seeds).
5. Each curse is announced in the morning order with an icon and a one-line joke.

## Comedy beats
- Lady Gourdiva creaking ominously as the team reaches the middle of the bridge.
- Everyone whispering "shhh" at a vegetable.
- Turnip pops up right behind the friend who said "where'd it go?"
- Maze walls closing between two friends mid-sentence.
