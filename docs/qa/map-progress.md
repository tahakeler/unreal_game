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

## 3. Progress
(updated as work lands)
