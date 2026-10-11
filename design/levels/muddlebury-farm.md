# Level: Muddlebury Farm (Region 1)

Built by `HarvestPanic/Scripts/LevelBuild/build_muddlebury.py` -> `/Game/HarvestPanic/Maps/Lvl_Muddlebury`.
Verified by `verify_muddlebury.py`. Units cm; +X east (toward bridge/market), +Y north, river flows -Y.

## Layout
| Zone | Location (m) | Purpose |
|---|---|---|
| Farm plateau | x -120..60, y -140..95 (farm zone ~180x180) | everything west of the river |
| Barn (brick-toned) | (-26, 0), doors face east | spawn: 4 PlayerStarts at x -11; HP_BarnRespawn; 4 HP_Goods_Crate in yard |
| Main road | barn -> bridge, 5 m dirt | primary sightline: spawn looks straight down the road at the bridge and market arch |
| Pig pen 18x12 m | (16, 34), mud wallow | HP_CritterSpawner_Pig:3; south gate, north critter hole behind hay |
| Coop yard 15x15 m | (16, -36) | HP_CritterSpawner_Chicken:8; north + east gaps toward orchard |
| Corn maze | x -8..42, y 56..86 | tall cards with corridors: main hiding spot, LOS breaker |
| Veg rows | x -78..-62, y 26..42 | low, open: easy catch zone |
| Wheat field + NS_Wheat | x -108..-68, y -15..18 | soft hide, wind VFX |
| Pumpkin patch | (-56, -44) fenced | HP_Goods_PrizePumpkin (watermelon meshes stand in for pumpkins) |
| Orchard | x 0..50, y -80..-56 | 15 trees, shade/hide |
| Windmill landmark | (-86, -38) | west skyline anchor |
| Bank hedge | x ~55 along river | bush hide spots with gaps |
| River | channel x 60..100, bed -6 m, water -4.2 m | HP_River box over water; current to -Y |
| Bridge | x 60..100 at y 0, 40 m, 20 planks (2 m), 70 cm sag, rope rails at 1 m | each plank tagged HP_BridgePlank (+ HP_BridgePlank_NN), Movable |
| Spit-out pile | (54, -120) west bank, 120 m downstream | HP_RiverPile + "Lost & Found" sign |
| Market square | x 108..162, y -28..28 | arch, Sale Pen 8x8 m at (114, 0) open to the west (HP_MarketPen box), Butcher/Greengrocer/Oddities stalls, town bell |

Barn -> market pen: 127.5 m (spec 70-140). Edge: 4 BlockingVolumes (HP_WorldEdge); NavMeshBoundsVolume covers the whole playable area including the river channel.

## Flow (4 players)
Spawn faces east; road splits at x~7 north to the pig pen and south to the coop, so a crew naturally splits in pairs. Critters flee toward the cover ring (corn maze N, orchard S, hedge E, wheat W); fence gaps and the hay hole are critter-only shortcuts. All goods funnel to one 2.4 m wide bridge: the only crossing, and the daily "boss fight". Market sits on open cobbles so sales are visible to the whole team.

## Comedy hotspots
- Mid-bridge sag (lowest planks at the centre): a 4-player cow carry stalls exactly where the drop is worst.
- Sale pen opens toward the bridge: critters can wander back out toward the river.
- Spit-out pile is on the farm bank, 120 m downstream: soaked goods have to be carried back and over the bridge again (toll again).
- Stepping-rock bait in the river 65 m downstream (gaps too wide): tempts a shortcut that ends in a swim.
- The town bell sits central in the square so the sundown panic pulls critters across the open cobbles.

## Deviations / open questions
- bridge-hazards.md says 24 m with 1.2 m planks; this brief asked for 40 m, so planks are 2 m. Game-design should pick one.
- Spawner tags use the literal strings `HP_CritterSpawner_Pig:3` / `HP_CritterSpawner_Chicken:8`, one actor each (the count is in the tag).
- No landscape (it can't be generated reliably headless). The ground is scaled cube slabs with a world-aligned material (`M_HP_WorldGround`).
- No pumpkin mesh in the pack, so watermelons stand in.
- Art bible folder rule says `/Game/HarvestPanic/Art/Environment`. Materials are under `/Game/HarvestPanic/Environment/Materials` because that is the folder this branch owns.
