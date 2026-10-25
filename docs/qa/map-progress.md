# Map progress: Muddlebury complete (feature/map-complete)

Owner priority #1: finish the Muddlebury map so it looks like a finished, inviting Steam co-op farm, not a grey-box.
Spec: GDD 11.5 (zones, critical path, traversal, landmarks, markers, chaos hotspots), 11.6 (level rules), 7 (bridge 40 m /
20 x 2 m planks), 16 + `design/art/art-bible.md` (look), `docs/architecture/unity-contracts.md` (markers, layers),
`docs/qa/perf/perf-baseline.md` (<= 1,500 draw calls, GPU Resident Drawer on).
Everything is built by `HarvestPanic.World.Editor.BuildMuddlebury.Build` (seeded, reproducible).

## 1. Gap audit (scene at main 5ad5af1, before this branch)

Baseline captures: `MuddleburyQa.CaptureAll` (8 QA cameras). Zone-by-zone against GDD 11.5.3:

| Zone | GDD 11.5 | Built before | Gap |
|---|---|---|---|
| Barn & yard | spawn, respawn, wardrobe, crates | Gambrel barn as one **solid convex collider**; doorway is a black box | **Bug: the Wardrobe marker sits inside the solid barn collider** (unreachable). No interior. Foundation slab would fill any interior |
| Farmhouse | LandlordVisitor walks to "the farmhouse door" | none | Missing (no home for Grandpa) |
| Main road / fork | 5 m road, fork x 7, hay, mud | built | OK; spawn sightline OK (arch visible) |
| Pig pen | 18 x 12, gate, wallow, trough, shelter, hay hole | built (posts + flat roof shelter) | Shelter reads as grey-box |
| Coop yard | coop house + ramp + gaps | built | Sparse |
| Pasture (cow/goat/sheep spawners) | spawners at (2,22), (5,30), (-6,20) | random grove trees can land on spawners | No pasture dressing; spawners not protected from dressing |
| Corn maze / veg / wheat / pumpkins | built | built | Flat painted soil, no tilled rows; wheat is ~420k tris (wheatStageA 720 tris x ~390) with no culling |
| Windmill | 13 m tower, sails | built (untextured) | OK silhouette, plain |
| Orchard / hedge / Lost & Found | built | built | Lost & Found board has no text |
| River | 40 m channel | ruler-straight canal to the horizon | Reads artificial; banks perfectly straight |
| Bridge | 40 m, 20 x 2 m planks, 0.7 m sag | built | Approach undressed, no warning sign |
| Market arch / sale pen / square | arch, pen open west, 3 stalls, bell, fountain, 9 houses | 9 plain box houses with prism roofs, 3 Tripo stalls | Town reads as a stage set; no signage, no town landmark/skyline anchor east |
| Surroundings | "forest ring on the surrounding hills that hides the edge and reads as a cosy bowl" | hill band only 7-13 m wide beyond the walls; flat **"Distant Ground" slabs** at y 4 and a hard horizon line | **Biggest visual gap**: no landscape beyond the play area |
| Boundaries | 4 world-edge colliders | invisible walls only | No soft natural wall on the farm side (flat grass to an invisible wall) |
| Signage | readable signs | sign meshes without text | No text anywhere |
| Lighting | phase look from `WeatherLook` | scene sun/sky slightly off `WeatherLook` afternoon | Small pop when `WeatherPresenter` takes over |
| Rebuild safety | builder reproduces the scene | builder does not re-run `LivestockSceneHook.AddMuddleburyMarkers` | **A rebuild drops the AI team's CursedCrops fields** |
| Markers | GDD 11.5.7 | all present (4/1/5/5/20/1/1/1/1) | wardrobe unreachable (above) |
| Traversal | spawn -> pen ~125 m | 125 m (barn respawn -> pen 128.5 m) | OK |

Perf before (docs/qa/perf/perf-baseline.md, M2 Pro editor): 508 draw calls, 592k tris avg, 3.8 ms main thread.

## 2. Plan
- Landscape: 3 x 3 terrain grid (play tile unchanged in size + 8 low-res outer tiles), hills rising into a bowl, a GPU-instanced
  scenery forest (no scene bloat), river meanders away beyond the play edges, hazy horizon ring.
- Farm: hollow barn with interior + reachable wardrobe, farmhouse with garden, pasture, pig hut, tilled rows, hay meadow,
  windmill/orchard dressing, farm boundary fence, readable signs.
- Bridge: approach dressing, warning sign, abutment rocks/lanterns.
- Town: detailed jettied houses (combined meshes, one renderer per building), town hall clock tower on the road axis
  (skyline anchor east), more stalls, bunting, square furniture, signage.
- Builder keeps other teams' content: re-runs `LivestockSceneHook.AddMuddleburyMarkers` by reflection after saving.
- Perf: architecture combined per building, LOD culling cells for small props, cheaper wheat mix.
- Tests: EditMode (required roles, markers not inside colliders, bounds closed, wardrobe reachable), PlayMode navmesh
  reachability (critters -> sale pen, spawn -> market) and HP_QA_DIR map screenshots incl. dusk.

## 3. Progress (built on feature/map-complete)

### What changed, per zone
| Zone | Now |
|---|---|
| Surroundings | 3 x 3 terrain grid: playable tile unchanged (513 heightmap, 8 layers), 8 scenery tiles (129 heightmap, 4 layers = one pass, no colliders), hills to ~70 m forming a bowl, the river valley bending out of sight beyond both edges, `InstancedForest` (GPU-instanced, ~7k trees, per-camera chunk culling, far LOD, stops at linear fog end), `HP_Horizon` hill ring (aerial-perspective fog share, honours real fog at night/in weather) |
| Barn & yard | Hollow gambrel barn (wall/gable/roof boxes with colliders, open hinged doors, trims, windows, weather vane, "GRANDPA'S FARM"); interior: plank floor, timber frame, hay loft + ladder, 3 stalls, workbench + hung tools, barrels/sacks, 2 lanterns + warm unshadowed fill lights; **wardrobe corner** (marker moved out of the old solid collider to (-20.4, 6.33) facing into the barn; rug, mirror, sign) |
| Farmhouse (new) | Stone/plaster two-storey house (porch, railings, shutters, window boxes, chimney, attic windows), picket garden, path, mailbox ("GRANDPA"), well, woodpile + axe, washing line, doghouse + "BEWARE OF GOAT", kitchen garden, shade trees |
| Pasture (new) | Stone trough, hay rack, salt lick, shade oak, sign; cow/goat/sheep spawners kept on open grass (`KeepClear`) |
| Pens | Pig pen: A-frame hut, trough, hay over the hole, "PIGS"; coop rebuilt (ramp, nest boxes), "CHICKENS" |
| Fields | Veg rows 11 tilled rows; wheat golden (crop palette exception) and thinned 1.25 -> 1.42 m; corn maze mostly the 148-tri stalk; "VEG / CORN MAZE / PUMPKINS"; hay meadow with round bales; patchwork ploughed + hay fields with hedgerows (gaps every ~22 m) on the empty outer acres |
| Windmill | Rebuilt: stone base, plaster tower, gallery deck + rail, shingle cap, windows, lattice cloth sails (spinner kept), "FLOUR MILL", sacks |
| Orchard / hedge / Lost & Found | Apples, mushrooms, crates, ladder, "ORCHARD"; hedge unchanged; "LOST & FOUND" + "(mostly)" |
| River | Water ribbon follows the centreline (straight in play, meandering outside), irregular banks (zero at the bridge), thinner flow lines, reeds/rocks/lilies, rock weirs at both play edges; river volume, sweep path and pile unchanged |
| Bridge | Gameplay geometry untouched (40 m, 20 x 2 m planks, 0.7 m sag); stone footings, bank boulders, lanterns, "ONE AT A TIME!", "MARKET >", staging crates, "MUDDLEBURY MARKET" landing board |
| Market town | Arch rebuilt (lettered both faces); 20 storybook houses (jetties, timber framing, shutters, window boxes, chimneys, attic windows, striped awnings + fascia lettering, incl. "BANK - Loans & Regrets" in landlord plum); **town hall + clock tower** on the road axis (east skyline anchor from the spawn); bell rebuilt; 4 extra produce stalls; fountain, lamps (lit at night), planters, benches, carts, notice board, bunting; ~8 cottages behind; lanes end at "ROAD CLOSED (cows)" |
| Boundaries | Broken boundary rail fence W/N/S, edge forest band with trunk colliders + undergrowth, invisible walls raised to 70 m (y -12..58) |
| Lighting | Scene sun/sky = `WeatherLook` afternoon (no pop when WeatherPresenter takes over); volume per art bible (bloom 0.4 / 1.2, contrast 10, vignette 0.24, violet lifted shadows; saturation +4 / exposure 0.1 baseline kept for WeatherPresenter's delta); `NightWindows` lights window glass and lamps from late Dusk; `LitText` signs dim with the scene |
| Tooling | `WorldMeshBuilder` (one renderer per building), `WorldTextures` (seeded value maps), LOD culling cells for small dressing, `MuddleburyQa -qaCams/-qaLook`, builder re-runs `LivestockSceneHook` (by reflection) and keeps its marker names, `.gitattributes`: binary terrain/navmesh `.asset` -> LFS |

### Traversal (navmesh, `MuddleburyNavTests`, walk 4 m/s / sprint 6.5 m/s)
| Route | Measured | GDD 11.5.5 |
|---|---|---|
| Spawn -> Sale Pen | 125 m (31 s / 19 s) | ~125 m (31 s / 19 s) |
| Barn respawn -> Sale Pen | 129 m | 127.5 m |
| Pig / chicken / cow / goat / sheep spawn -> Sale Pen | 113 / 116 / 119 / 121 / 126 m | pig pen gate ~136 m |
| Pumpkin patch -> Sale Pen | 187 m (47 s) | ~195 m (49 s) |
| Lost & Found -> bridge west end | 134 m (33 s) | ~150 m (37 s) |
| Spawn -> wardrobe | 12 m | (new) |

### Perf (`PerfCaptureTests`, 4 farmers, rain + night, 60 s, M2 Pro editor batchmode, 1080p)
| Metric | Before (main) | Pass 1 | Final | Budget |
|---|---|---|---|---|
| Draw calls avg (p99) | 508 | 6,854 | **959 (994)** | <= 1,500 |
| Triangles avg | 592k | 934k | **807k** | <= 1.5 M |
| Main thread avg (p95 / p99) | 3.8 ms | 13.6 ms | **6.3 (7.8 / 9.9) ms** | <= 10 ms |
| Shadow casters | 172 | 368 | 220 | -- |
| Texture / mesh memory | 134 / 10 MB | 138 / 17 MB | 140 / 17 MB | <= 1.5 GB tex |
| GC per frame | 1.94 KB | 2.49 KB | 2.30 KB | ~0 |
Day views without players (probe): 275-889 draws, 0.43-1.06 M tris. The pass-1 spike was terrain mesh trees (one draw per tree)
and per-patch terrain; fixed by `InstancedForest` + `Terrain.drawInstanced`. GPU time is still unmeasured (needs an M1 dev build).

### Tests
- EditMode: `MuddleburyLayoutTests` (every role placed; spawn-type markers clear of colliders and grounded; all markers inside
  the play rectangle; world edges overlap and 432 rays from 6 interior points never escape; wardrobe cabinet + interact space
  free and reachable through the doorway; 9 tiles, one collider, instanced terrain, forest > 2,000 trees; no renderer without
  mesh/material; signage lit and legible). Existing `MuddleburySceneTests` unchanged (counts 4/1/5/5/20/1/1/1/1).
- PlayMode: `MuddleburyNavTests` (above), `MuddleburyMapScreenshots` (HP_QA_DIR: 17 day + 6 dusk + 3 night captures).

### Results (final scene)
- EditMode full suite: **405 / 405** pass.
- PlayMode full suite (`-nographics`): 86 pass, 19 skipped (GPU-only screenshot/perf), 2 failed: a temporary draw-call probe
  (deleted, it needs a GPU) and `LivestockPlayTests.Goat_HeadbuttKnocksPlayerDown` (passes 2/2 in isolation, see gaps).
- World PlayMode with graphics: **8 / 8** (nav, bridge, scene, map screenshots); `PerfCaptureTests` pass (numbers above).
- `BuildScript.BuildMacDev`: success, 377 MB, 0 errors.
- `tools/net/two-process-test.sh` (random port): **PASS** - version-mismatch and farm-full rejects, late join + rejoin (3
  replicated), host-quit message, voice uplink + relay, host and client both see Muddlebury with critters=18 goods=7 economy=1
  farmers=2.

### Screenshots
`MuddleburyMapScreenshots` (play mode, `HP_QA_DIR/map/`): `map_day_<camera>`, `map_dusk_<camera>`, `map_night_<camera>` for the
17 QA cameras (overview, spawn, farmyard, barn interior, fields, pasture, bridge, arch, market, town hall, market overview,
windmill/wheat, river north, river downstream/pile, forest edge). Editor variant: `MuddleburyQa.CaptureAll -qaLook Dusk|Night`.

### Remaining gaps
- GPU frame time unmeasured (dev build on a base M1 needed); day-time perf capture not automated (the capture forces night).
- Town interiors are closed (houses are solid); only the barn is enterable.
- Bridge sway / rail grab, toll, town folk and the bell behaviour remain gameplay items (GDD 7.2, 11.5.9).
- Water foam is still the shared stylized shader (thinner streaks, no shoreline foam mesh).
- `LivestockPlayTests.Goat_HeadbuttKnocksPlayerDown` failed once in the full PlayMode run and passes in isolation (2/2): an
  order-dependent flake in the AI suite (CritterTest scene), not a map regression.
