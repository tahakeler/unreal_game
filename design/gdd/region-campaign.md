# Region Campaign & PCG Layouts

> **Status**: Draft
> **Author**: game-designer + ux-designer (Batch D)
> **Last Updated**: 2026-10-10
> **Last Verified**: 2026-10-10
> **Implements Pillar**: The Farm Gets Worse, You Get Better · Everything Has Legs

## Summary
The campaign is a chain of Regions, each a debt of 5 paid weeks that unlocks the next market town, biome, species, curses and Landlord twist. Each region's farm layout is generated from a seed with PCG rules so every run plays differently while keeping the bridge as the fixed chokepoint.

> **Quick reference** — Layer: `Feature` · Priority: `EA (Region 1 + PCG) / Post (2, 3) / Expansion idea (4, 5)` · Key deps: `week-quota`, `livestock-roster`, `weather-twists`, `save-persistence`

## Overview
A **Region Run** is a sequence of weeks (from `week-quota`) on one region's map. Pay the Sunday quota **5 times** and the region's debt is cleared: the crew earns the region deed, the next region unlocks at account level, and the run ends with a credits-style "Deed Day". Failing any Sunday ends the run (roguelite reset per save-persistence); account unlocks persist. Each region defines a **Region Profile**: biome PCG rules, species/curse/creature pools, weather deck weights, market town rules and one **Landlord Twist** rule that is always active there.

## Player Fantasy
"We beat the farm. Now there's a worse farm." Each region is a new comedic stage with its own signature disaster. Arriving at a new market town should feel like a reward and a threat.

## Detailed Design

### Core Rules
1. Regions unlock linearly at account level: clearing Region N unlocks N+1 for any lobby the clearing players host. Lobby region = host's choice among host's unlocked regions.
2. Run length: **5 weeks** per region for all 5 regions (EA tuning; knob 4–6). Week numbers within a run drive quota (week-quota); region index applies a **RegionDifficulty** multiplier (canonical owner; `week-quota` reads it as `R_mult`).
3. Content pools: a region's day draws from **its own pool + 50% of previous regions' species** (so the roster widens). New species of the region appear from Week 1; returning species are weighted 0.5.
4. **Seeded layouts:** at run start, `RunSeed` (uint32) is generated (or provided by eviction-mode). Each week reseeds the farm side (`WeekSeed = hash(RunSeed, week)`), so the farm reshapes each Monday ("the farm moved overnight"). The bridge and market sides are fixed per region.
5. **PCG rules (all regions):** map = Farm zone (≈ 180×180 m) + Bridge chokepoint + Market Town zone. PCG places: 1 barn (respawn), 3–5 pens, 4–8 field plots, 2–4 hazard clusters, 12–30 fence segments with 2–4 gaps, cover props. Constraints: barn → bridge walking path ≤ 140 m and ≥ 70 m; no pen within 25 m of the bridge; every field reachable by wagon (navmesh width ≥ 3 m); at least one alternative route (ford, raft, zipline per region) unlocked by upgrades.
6. **Market town:** each region's town has 3 stalls with distinct demand curves (handled by shared-wallet-economy; this doc supplies the stall definitions) and one town quirk.
7. **Landlord Twist:** a permanent region rule registered as a Day Modifier (tag `Region.Twist.*`, non-stacking, duration = run) via the day-loop Day Modifier contract.
8. **Deed Day:** after the 5th paid Sunday, an unscored celebratory day: no quota, all goods worth 2×, Landlord hands over the deed and then "remembers" the next region's debt.

### Region Profiles

#### Region 1: Grandpa's Farm → Muddlebury (EA)
- **Theme:** sunny, muddy, sun-bleached Americana farm; the rickety plank bridge over a lazy river. Assets: Harvest pack + Bridge pack.
- **Species:** pig, chicken, goat, cow, bull, sheep, duck, prize llama (full EA roster).
- **Curses:** prize pumpkin, burrowing turnip, screaming mandrake, growing pumpkin, maze corn (EA), tumble melon (EA), moonberry bush (EA).
- **Night creatures:** Scarecrow, Barn Thing, Crows. **Rivals:** Cousin Clem, the Pruitt Twins, Mabel (`rival-farmers`).
- **Hazards:** breakable planks, mud pits, river spit-out, hay bale avalanches.
- **Weather deck** (card names per `weather-twists`; overrides default weights): Clear 40%, Mud Day 25%, Gusty 20%, Storm 10%, Fog 5% (Heatwave and Full Moon not in Region 1's deck).
- **Market town Muddlebury:** stalls: Butcher (livestock), Greengrocer (crops), Oddities (cursed goods, high volatility ±40%). Quirk: the town bell rings at sundown −60 s and every critter in earshot panics toward it.
- **Landlord twist "Bridge Toll":** each crossing *with goods* costs $5 (wallet sink, `shared-wallet-economy`) unless Bridge Reinforcement (any level) is owned. Teaches route economy.
- **RegionDifficulty:** 1.00.

#### Region 2: Misty Riverlands → Ferrytown (Update 1)
- **Theme:** wide braided river, reed islands, stilt houses, constant drizzle. Farm sits on 3 islands.
- **New critters** (defined in `livestock-roster` §3.4): **Walking Catfish** (flops on land toward water, slippery 2-hand carry), **Goose** (honks = Noise Event 0.8, bites carriers), **Otter** (steals small goods and swims off).
- **New curses:** **Floating Gourds** (drift away unless weighted), **Rice that Wanders** (bundles migrate at dusk).
- **Night creature** (`night-creatures` §3.4): **The Bog Lantern** (lures the loudest player into deep water).
- **Hazards:** currents that drag rafts, tide that floods low fields at 2/3 day, rope ferry with crank.
- **Signature mechanic:** rafts (wagon-vehicles) are the main haul vehicle; the bridge is a long rope bridge with 2 mid islands.
- **Weather deck:** Drizzle 40%, Fog 25%, Flood 15%, Gusty 15%, Clear 5% (Drizzle/Flood are region cards stubbed in `weather-twists` §3.4).
- **Market town Ferrytown:** stalls: Fishmonger, Floating Market barge (moves along dock every 3 min), Mill. Quirk: market is only reachable via the ferry, which departs every 90 s.
- **Landlord twist "Waterfront Property":** goods that touch water lose freshness 2× faster, but the Landlord pays +20% for "dry-delivered" items (zero water contact).
- **RegionDifficulty:** 1.20.

#### Region 3: Haunted Orchard → Hollow Hill (Update 2)
- **Theme:** autumn apple orchard, crooked trees, perpetual twilight, lantern-lit paths. Days are 40% night.
- **New critters** (`livestock-roster` §3.4; Hiding Apples are a cursed crop in `cursed-crops`): **Hiding Apples** (play dead among normal apples, sprint when looked away), **Black Cat** (curses whoever it crosses: −20% carry speed 30 s), **Bat Swarm** (chicken-like swarm, only at night).
- **New curses:** **Jack-o'-Lanterns** that relight and run when dropped, **Whispering Hedges** (amplify voice ×2 nearby).
- **Night creatures (3 new, `night-creatures` §3.4):** **The Headless Picker** (takes goods out of wagons), **Orchard Widow** (webs between trees immobilize carriers), **The Cider Ghost** (possesses one player's controls briefly, swaying).
- **Hazards:** falling apples (bruise carried goods), sinkhole graves, a creaking covered bridge that closes its doors at random.
- **Weather deck:** Fog 40%, Gusty 25%, Storm 15%, Blood Moon 10% (region variant of Full Moon), Clear 10%.
- **Market town Hollow Hill:** stalls: Cider Press (buys bruised fruit at full price!), Apothecary (cursed goods), Pumpkin Patch auction (one lot per day, bid-price spike). Quirk: town gates shut at true dark; late arrivals sell at 50%.
- **Landlord twist "Night Shift":** quota counts dusk-bonus sales at 1.5× but day sales at 0.9×; pushes players into the night.
- **RegionDifficulty:** 1.45.

#### Region 4*: Sunburnt Ranch → Dustwater (Expansion idea)
- **Theme:** desert ranch, red mesas, a dry gulch crossed by a rail trestle.
- **New critters** (`livestock-roster` §3.4): **Longhorns** (stampede herd AI), **Roadrunner** (fastest critter, 1-hand, worth little but needed by order), **Armadillo** (rolls into balls, rolls downhill).
- **Hazards/curses:** tumbleweeds (pick up small goods), heat that spoils goods (freshness decay ×1.5 in sun, shade props matter), dust devils, **Cactus Fruit** that throws spines.
- **Night creature:** **The Copycat Coyote** (mimics teammates' footsteps; no voice mimicry). *(Renamed from "Skinwalking Coyote" to avoid appropriating a Navajo sacred concept; see consistency report open question.)*
- **Market town Dustwater:** saloon stall (pays per haul speed), Rail Depot (train every 4 min: miss it, wait), General Store. Quirk: noon showdown, all critters freeze 10 s, then scatter.
- **Landlord twist "Water Rights":** troughs cost $ to fill; thirsty livestock move 1.3× faster.
- **RegionDifficulty:** 1.70.

#### Region 5*: Frostbite Fjord → Icemouth (Expansion idea)
- **Theme:** Nordic fjord, ice shelves, sauna huts, northern lights.
- **New critters** (`livestock-roster` §3.4): **Yaks** (8 hands / 4 carriers, warm: carriers don't get "Cold"), **Penguins** (slide on bellies), **Reindeer** (antlers snag ropes).
- **Hazards/curses:** slippery ice physics (friction 0.15), frozen bridge that cracks over 3 crossings, "Cold" status (stamina regen −40% away from braziers), **Snow Turnips** that build snowmen decoys.
- **Night creature** (`night-creatures` §3.4): **The Ice Troll** under the bridge, demands a toll good per crossing at night.
- **Market town Icemouth:** Fish Auction, Fur Trader (cosmetic only, no animal harm shown), Sauna Bar. Quirk: market on a floating ice floe that drifts 10 m per minute.
- **Landlord twist "Heating Bill":** a $ cost per day equal to 8% of quota, reduced by warm-weather upgrades.
- **RegionDifficulty:** 2.00.

### States and Transitions
| State | Entry | Exit |
|---|---|---|
| RegionSelect | Lobby | Host confirms |
| RunActive (Week n) | Run start / week paid | Sunday fail → RunFailed; 5th paid → DeedDay |
| DeedDay | 5th paid Sunday | Day end → RegionCleared |
| RegionCleared | DeedDay end | Account unlock written; back to lobby |
| RunFailed | Sunday unpaid | Eviction cutscene; save wiped; lobby |

### Interactions with Other Systems
- **Out:** Region Profile (pools, deck weights, RegionDifficulty, twist modifier) → day-loop, week-quota, livestock-roster, cursed-crops, night-creatures, weather-twists, rival-farmers, shared-wallet-economy (stall definitions).
- **Out:** RegionCleared → meta-progression, save-persistence. Seed → eviction-mode (which reuses Region Profiles at endless difficulty).
- **In:** paid/failed Sunday from week-quota.

## Formulas
```
EffectiveQuota(week, region) = BaseQuotaFromWeekQuota(week) * RegionDifficulty[region]
SpeciesWeight = NewToRegion ? 1.0 : 0.5
WeekSeed = xxHash32(RunSeed, WeekIndex)
```
Expected region clear time: 5 weeks × (5 working days × ~13 min + ~2 min Sunday) ≈ 5.6 h for Region 1 (≈ 6 h with shop time).

## Edge Cases
| Scenario | Behavior |
|---|---|
| Guest hasn't unlocked host's region | Can join; clearing grants the guest the region unlock too |
| PCG fails constraints after 20 attempts | Fall back to the region's hand-authored fallback layout |
| Run saved mid-region, content update changes profile | Save stores profile version; old version used until run ends |
| Region 4/5 not shipped | Hidden in UI; no "coming soon" teaser unless announced |
| Solo | RegionDifficulty unchanged; week-quota applies solo scaling |

## Dependencies
| System | Direction | Nature |
|---|---|---|
| week-quota | This depends on | Week cadence, Sunday result |
| livestock-roster, cursed-crops, night-creatures, rival-farmers | This depends on / supplies pools | Species data |
| weather-twists, day-loop | Consume profile | Deck weights, Day Modifier twist |
| shared-wallet-economy | Consumes | Stall definitions |
| save-persistence, meta-progression, eviction-mode | Depend on this | Run state, unlocks, seeds |

## Tuning Knobs
| Knob | Default | Range |
|---|---|---|
| WeeksPerRegion | 5 | 4–6 |
| RegionDifficulty | 1.0/1.2/1.45/1.7/2.0 | ±0.15 each |
| ReturningSpeciesWeight | 0.5 | 0.2–1 |
| BarnToBridgeDistance | 70–140 m | 50–180 |
| DeedDayMultiplier | 2× | 1.5–3 |
| PCGRetryLimit | 20 | 5–50 |

## Acceptance Criteria
1. 100 random seeds per region pass PCG constraints (≥ 95% without fallback); each generates in ≤ 3 s on base M1.
2. Same RunSeed produces identical layout on host and all clients.
3. Clearing 5 Sundays unlocks the next region in the account profile for every participating player.
4. Region twist appears as a Day Modifier visible in the morning order every day of the run.
5. Region 1 playtest: median clear in 5–8 h, ≥ 40% of teams fail at least one run before clearing.

## Comedy beats
- Monday morning cutscene: the barn is in a different place and Grandpa's note says "Farm moved again. Don't ask."
- Deed Day ends with the Landlord tearing the deed in half: "This only covers the *front* half of the farm."
- Each town has a crier NPC who reads the previous day's top Blame line aloud as the news.
