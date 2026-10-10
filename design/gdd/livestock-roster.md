# Livestock Roster

> **Status**: Draft · **Slug**: `livestock-roster` · **Layer**: Feature · **Priority**: VS (pig, chicken) / Alpha (5: + goat, cow, bull) / EA (8: + sheep, duck, llama) / Post (region critters, §3.4) · **Batch**: B
> **Consumes**: Critter Personality (critter-ai), Goods Item (goods-value), Noise Event (proximity-voice)

## 1. Overview
The roster is the data layer for farm animals. Each species is a Critter Personality asset plus a Goods Item entry (base value, fragility, carry hands) plus a signature behavior. Species get added by data; signature behaviors are small StateTree tasks plugged into `ST_Critter` slots.

## 2. Player Fantasy
Every animal is a different puzzle with a different personality. You learn that the goat climbs the roof, the sheep follow whoever is in front, and you never, ever run at the bull. Mastery is knowing which friend handles which animal.

## 3. Detailed Rules

### 3.1 Roster
Base $, Fragility and lift hands mirror the canonical `goods-value` table (consistency pass 2026-10-10). Hands = lift hands (carriers = hands/2).

| # | Species | Tier | Personality | Signature behavior | Counter | Base $ | Hands (carriers) | Fragility |
|---|---|---|---|---|---|---|---|---|
| 1 | **Pig ("Hamlet")** | VS | Greedy, sneaky, dramatic | Squeezes under fences (`Nav.UnderFence`); squeals loudly when grabbed (Noise Source=Critter 0.7, tag `Noise.Squeal`) | Feed bucket (affinity 0.9); flank toward a corner | 60 | 2 (1) | 0.8 |
| 2 | **Chicken** | VS | Skittish, flock | Flock scatter: 6–10 birds burst in all directions, regroup 15 s later; short flaps over 1.5 m fences | Net catches 1 (3 with Big Net); quiet approach; grain whistle | 15 each | 1 (1) | 0.6 (feathers fly) |
| 3 | **Goat ("Gary")** | Alpha | Curious, climber, chaotic | Climbs roofs, wagons and the bridge rails; headbutts carried goods out of hands (impulse 600 N·s) | Lasso; let it come to you (Curiosity 0.6) | 70 | 2 (1) | 0.5 |
| 4 | **Sheep** | EA | Herd follower, timid | `Follow` herd: catch the bell-wether and the flock follows it into the wagon; startled flock stampedes | Grab the leader; whistle | 80 each | 2 (1) | 0.6 (wool = value bonus if clean) |
| 5 | **Cow ("Big Martha")** | Alpha | Lazy, stubborn | Barely flees, refuses to move (Freeze); needs 3-hand push or feed lure; one moo per 20 s (Noise 0.5) | Feed bucket, wagon ramp, teamwork | 150 | 6 (3) | 0.7 |
| 6 | **Bull ("Sir Reginald")** | Alpha | Aggressive guard | Guards cows; Charge on sprinting/red-hatted players (`ChargeImpulse` 1800 N·s = KO per player-character) | Distract with a decoy friend; lasso + 2 players | 200 | 6 (3) | 0.4 |
| 7 | **Duck** | EA | Swimmer, smug | Heads for river/pond; swims faster than players; quacks mock on escape | Raft, net from bridge | 35 | 1 (1) | 0.6 |
| 8 | **Llama ("Doris")** | EA | Judgmental, spitter | Spits on the loudest player (blinding screen splat 2 s); refuses to move if shouted at | Speak softly (loudness < 0.3) to make her calm | 400 | 4 (2) | 0.9 |

### 3.2 Personality values (key fields)
Speeds in cm/s; player walk/sprint = 400/650 cm/s (`player-character`).

| Species | Speed walk/sprint | AlertRadius | NoiseSens | Cornered | LureAffinity | HerdMode | SizeClass | Agility | CritterPullN | SecureCarriers | StruggleImpulse | ChargeImpulse | Whistle short / long |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Pig | 280/560 | 10 | 1.0 | Juke | FeedBucket 0.9 | None | Medium | 3 | 500 | 2 | 150 N·s | — | Flee / Ignore |
| Chicken | 250/620 | 8 | 1.4 | Juke | Whistle 0.6, FeedBucket 0.5 | Flock | Small | 4 | 50 | 1 | 40 N·s | — | Flee / Ignore |
| Goat | 300/520 | 9 | 0.7 | Charge | FeedBucket 0.4, Shiny 0.5 | None | Medium | 4 | 400 | 1 | 120 N·s | 600 N·s | Ignore / Ignore |
| Sheep | 260/480 | 12 | 1.2 | Freeze | Whistle 0.8 | Follow | Medium | 2 | 350 | 1 | 80 N·s | — | Flee / Rally |
| Cow | 150/300 | 6 | 0.5 | Freeze | FeedBucket 0.95 | Guard target | Large | 0 | 900 | 3 | 200 N·s | — | Ignore / Ignore |
| Bull | 250/650 | 14 | 0.8 | Charge | FeedBucket 0.3 | Guard | Large | 2 | 1600 | 3 | 300 N·s | 1800 N·s | Charge-trigger / Ignore |
| Duck | 220/400 (swim 500) | 9 | 1.1 | Juke | FeedBucket 0.6 | Flock (3–5) | Small | 3 | 80 | 1 | 40 N·s | — | Flee / Ignore |
| Llama | 270/500 | 10 | 1.5 (voice ×2) | PlayDead | FeedBucket 0.5 | None | Large | 1 | 600 | 2 | 150 N·s | — | Refuse-to-move / Ignore |

All species: `Noise.Food` → Lure at their FeedBucket affinity; `Goods.Grumpy` → default `GrumpyMods`; penned animals use default `PenEscapeChance` 0.6 (cow 0.3, she can't be bothered).

### 3.4 Region critters (Post / Expansion stubs, from `region-campaign`)
Brief entries so every region critter has a home; full personality tables are written with each region update. Values are placeholders owned here until then (goods values to be added to `goods-value` at that time).

| Region | Critter | Tier | Personality / signature | SizeClass | Hands (carriers) | Notes |
|---|---|---|---|---|---|---|
| 2 Riverlands | Walking Catfish | Post | `Trait.Swimmer`; flops toward water; slippery (grip slip time ×0.5) | Medium | 2 (1) | Low fragility |
| 2 Riverlands | Goose | Post | `Trait.Aggressive`, `Trait.Loud`; honk = Noise Source=Critter 0.8; bites carriers (drop chance 30%) | Small | 1 (1) | Charge 300 N·s |
| 2 Riverlands | Otter | Post | `Trait.Thief`, `Trait.Swimmer`; steals ≤ 1-hand goods, swims off | Small | 1 (1) | StealTargets Goods small |
| 3 Orchard | Black Cat | Post | `Trait.Sneaky`; crossing a player's path applies −20% carry speed for 30 s | Small | 1 (1) | Not sellable (Almanac only) |
| 3 Orchard | Bat Swarm | Post | `Trait.Nocturnal`, Flock; night only; chicken-like scatter | Tiny | 1 (1) per bat | Sold by the bag |
| 4 Ranch | Longhorns | Expansion | Herd stampede (`HerdMode` Follow + Charge) | Large | 6 (3) | Charge 1600 N·s |
| 4 Ranch | Roadrunner | Expansion | Fastest critter (sprint 900 cm/s), Agility 5 | Small | 1 (1) | Low value, order-required |
| 4 Ranch | Armadillo | Expansion | Rolls into a ball and rolls downhill (`CorneredResponse` Burrow-like roll) | Small | 1 (1) | Fragility 0.2 |
| 5 Fjord | Yak | Expansion | Lazy; carriers immune to "Cold" | Huge | 8 (4) | Freeze when cornered |
| 5 Fjord | Penguin | Expansion | Belly-slides on ice (speed ×2 on ice) | Small | 1 (1) | Flock |
| 5 Fjord | Reindeer | Expansion | Antlers snag ropes (tethers break ×2 faster) | Large | 4 (2) | — |

Hiding Apples (Region 3) are a cursed crop (`cursed-crops` §3.1).

### 3.3 Spawning
Day-loop's morning order lists species and counts. Species spawn in pens/fields per region-campaign layout. Spawn count per day = order count × 1.25 (rounded up), so there are spares to lose.

## 4. Formulas
- **Sale value** = `Base$ × FreshnessMult × BruiseMult × SpeciesBonus` (Freshness/Bruise owned by goods-value). SpeciesBonus: Sheep clean wool ×1.2 (not dropped in mud); Chicken flock bonus +10% if all ordered chickens delivered.
- **Bull charge trigger** = target sprinting within 10 m, or wearing `Cosmetic.Red`, or threat > 0.7. Cooldown 6 s.
- **Llama spit target** = player with highest voice loudness in last 5 s within 8 m; cooldown 10 s.
- **Sheep follow**: followers path to within 2–4 m of leader; if leader is carried, followers trail at 70% speed.

## 5. Edge Cases
- Pig squeal while carried also triggers other critters (by design).
- Leader sheep delivered: followers choose a new leader (oldest) after 3 s and keep fleeing.
- Bull charges onto the bridge: bridge-hazards plank-break check uses bull mass 900 kg.
- Goat on a roof unreachable: after 45 s it gets bored and jumps down (Curiosity).
- Duck carried to market wet: no `Soaked` penalty (Swimmer; goods-value water BP = 0 for Duck).
- Cow can't fit wagon: wagon-vehicles ramp required, otherwise cow carries on foot only (3 hands).

## 6. Dependencies
critter-ai (Personality, states), goods-value (Goods Item), physics-carry (carry hands), farm-tools (lures, net, lasso), proximity-voice (Noise Event), day-loop (order), region-campaign (where they spawn), meta-progression (almanac entries), audio-feedback (barks).

## 7. Tuning Knobs
Per-species Base $, speeds, AlertRadius, CarryHands; spawn spare ratio (1.25, range 1.0–1.5); bull charge cooldown (6 s, 4–10); llama spit cooldown (10 s, 6–15); chicken regroup time (15 s, 8–25).

## 8. Acceptance Criteria
1. Pig and chicken playable in VS with base values $60/$15 (goods-value) and behaviors above.
2. In playtest, a 4-player team catches a pig in a median 45–90 s without tools and 20–40 s with feed bucket.
3. A chicken flock scatter covers a ≥ 15 m radius within 2 s.
4. Catching the leader sheep pulls ≥ 80% of followers within 10 m in 10 s.
5. Bull charge ragdolls the target and never instantly kills (uses player-character knockout rules).
6. Each species distinguishable by silhouette and sound alone (audio/art review).
7. All 8 species defined purely by data assets + signature tasks.

## Comedy beats
- Hamlet's operatic squeal on grab alerts the whole farm.
- Gary the goat standing on the wagon you're pulling, staring at you.
- Big Martha refusing to move while 3 players push and sweat; then she walks away the moment they stop.
- Doris spits on the friend who keeps yelling instructions.
- Sir Reginald targets the player who bought the red hat.
