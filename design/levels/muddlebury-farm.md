# Level: Muddlebury Farm (Region 1)

Built by `HarvestPanic.World.Editor.BuildMuddlebury.Build` (Unity, seeded, reproducible) -> `HarvestPanic/Assets/Scenes/Muddlebury.unity`.
Units metres; +X east (toward bridge/market), +Z north, the river flows -Z. Farm ground y 0, water y -4.2.
Canonical spec: GDD 11.5 (zones, critical path, traversal, markers, hotspots) and 11.6 (rules). Progress and QA: `docs/qa/map-progress.md`.
The archived Unreal version of this doc lives on `archive/unreal-final`.

## Layout
| Zone | Location (x, z) | Purpose / contents |
|---|---|---|
| Barn & yard | (-26, 0), doors east | Hollow red gambrel barn: plank floor, hay loft + ladder, 3 stalls, workbench + tools, lanterns. **Wardrobe corner** inside (-20.4, 6.3), facing into the barn. 4 `PlayerStart` at x -11 facing east, `BarnRespawn` (-14.5, 0), 4 crates (-12.5, 6.5) |
| Grandpa's farmhouse | (-47, 24), porch faces south | Stone/plaster two-storey house, porch, picket garden, mailbox, well, woodpile, washing line, doghouse ("BEWARE OF GOAT"), kitchen garden |
| Main road + fork | x -17 -> 62, fork at x 7 | Fingerpost (MARKET / PIGS / CHICKENS), hay bales, fork mud (-4, -7) |
| Pasture | (-20..10, 12..36) | Cow (2, 22), goat (5, 30), sheep x5 (-6, 20) spawn on open grass; stone trough, hay rack, salt lick, shade oak |
| Pig pen | (16, 34), 18 x 12 | `Critter.Pig x3`, Tripo gate (south), mud wallow, A-frame hut, trough, hay bale over the north critter hole |
| Coop yard | (16, -36), 15 x 15 | `Critter.Chicken x8`, coop with ramp + nest boxes, north/east gaps toward the orchard |
| Corn maze | x -8 -> 42, z 56 -> 86 | 20 x 12 seeded maze, entrances south and east |
| Veg rows | x -78 -> -62, z 26 -> 42 | 11 tilled rows; AI's Mandrake field at (-74, 34) |
| Wheat field | x -108 -> -68, z -15 -> 18 | Soft hide, trampled path through the middle |
| Pumpkin patch | (-56, -44) | `Crop.PrizePumpkin`; AI's Tumble Melon field at (-43, -44) |
| Windmill | (-86, -38) | West skyline anchor: stone base, gallery, spinning lattice sails, flour sacks |
| Hay meadow | (-112..-92, -70..-48) | Round bales, hay cart |
| Orchard | x 0 -> 50, z -80 -> -56 | 15 trees, apples, crates, ladder |
| Patchwork fields | outer acres (N-W, S) | Ploughed and hay fields with hedgerows (gaps every ~22 m) |
| Bank hedge | x ~55 | Bush line with gaps |
| River | x 60 -> 100 inside the map | Straight inside the play area, bends away beyond both edges; reeds, rocks, lilies; boundary weirs at z 101 / -145 |
| Bridge | x 60 -> 100, z 0 | 40 m, 20 x 2 m planks, 0.7 m sag (gameplay unchanged); stone footings, lanterns, "ONE AT A TIME!" sign |
| Lost & Found | (52, -120) | `RiverPile`, "LOST & FOUND (mostly)" signs, crates |
| Market arch | x 104 | Stone pillars, "MUDDLEBURY MARKET" board, banners |
| Sale Pen | (114, 0), 8 x 8, open west | `MarketPen` trigger, straw floor, "SALE PEN" sign |
| Market square | x 108 -> 162, z -28 -> 28 | Butcher / Greengrocer / Oddities (Tripo) + 4 produce stalls, town bell (134, 0), fountain, lamps, planters, bunting, order board |
| Town | around the square | 20 storybook houses ringing the square + ~8 cottages behind (shops: BAKERY, TEA ROOM, HATS & BONNETS, SEEDS, POST, INN, TAILOR, CHEESE, TOOLS, BOOKS, ODDS & ENDS, "BANK - Loans & Regrets", APOTHECARY, SADDLERY); **town hall clock tower** (165, 0) on the road axis = the east skyline anchor seen from the spawn |
| Surroundings | beyond x -122 / 177, z -148 / 104 | 3 x 3 terrain grid: wooded hills (instanced forest), river valley, hazy horizon ring |
| Boundaries | play rectangle | Boundary rail fence (west/north/south), edge forest band with trunk colliders, lane ends "ROAD CLOSED (cows)", 4 invisible 70 m walls |

## Flow (4 players)
Spawn faces east straight down the road at the bridge rails, the arch and the clock tower behind it. The fork at x 7 splits
the crew: pigs north, chickens south, livestock on the pasture between. Critters flee into the cover ring (corn maze N,
orchard S, hedge E, wheat W). Everything funnels to the single 2.4 m bridge, then through the arch into the Sale Pen in
full view of the square. Measured navmesh distances: see `docs/qa/map-progress.md` (spawn -> Sale Pen ~125 m).

## Comedy hotspots
Unchanged from GDD 11.5.8: mid-bridge sag, bridge-approach mud, Sale Pen open west, town bell, stepping-rock bait 65 m
downstream, Lost & Found loop, pig-pen hay hole, coop gaps -> orchard, fork mud, corn maze at night.

## Deviations / open questions
- Bridge 40 m / 20 x 2 m planks is canonical (GDD 7.2, 11.7 Q4).
- The farmhouse and the town hall are new set pieces (not in GDD 11.5.3); they carry no markers or gameplay.
- Patchwork fields, hay meadow and pasture props are dressing only; critters may cross them.
- The forest beyond the walls is presentation-only (`InstancedForest`, no colliders); the river outside the play area has no
  river volume (nobody can reach it).
