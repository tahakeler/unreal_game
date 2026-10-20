# Third-Party (CC0) Asset Mapping — for the integration lead

All assets are CC0 (see `THIRD_PARTY_LICENSES.md`). Sources are listed in `tools/freeassets/manifest.json`;
GLBs are converted headless to FBX with Blender (`tools/freeassets/scripts/glb2fbx_blender.py`) and imported by Unity under
`HarvestPanic/Assets/ThirdParty/`. Per-asset metrics are in `tools/freeassets/import_report.json` (bounds, tris/verts, bones, anim lengths).
Re-run: see the script header (one `blender --background` call per GLB; copy the FBX into `Assets/ThirdParty/`).

## Conventions / gotchas
- **Critters face +Y** in the source rigs (Quaternius), Z up, root at the feet. The FBX export converts to Unity axes (Y up, -Z forward); if a model still faces sideways, rotate the **model child** of the critter prefab, not the root `NetworkObject`, so it faces +Z.
- **No attach points were authored.** Attach to the **bone** below (find the bone `Transform` by name and parent to it), or add an empty `head` child under that bone once in the prefab.
- Animation clip names may be long (`<file><Armature>_<Clip>`); the Blender export strips the `Armature|` prefix where possible. Use the clip suffix below.
- Materials are the source flat-colour materials (not yet on the `M_HP_*` URP set); re-material/tint per art bible is a follow-up (pig pink `#F2A3A0`).
- Size: renderer *bounds* can read wrong for these GLBs (100x scale); trust the bone extents listed here and check the model import **Scale Factor**. Verify collider sizes once in-editor.
- Mass (from the Tripo spec) is listed in `tools/freeassets/manifest.json`; set it on the prefab's `Rigidbody`/tuning asset.

## Critters (one rigged FBX per animal, clips embedded; prefabs in `Assets/Prefabs/Critters/`)
| Prefab | Model | Head bone | Idle / Walk / Run anim (suffix) | Size (bone extent) | Notes |
|---|---|---|---|---|---|
| **Critter_Pig** | `Assets/ThirdParty/Animals/Pig/Pig.fbx` | `Head` (also `Neck`, `Body` for grab_back) | `..._Idle` 6.67 s / `..._Walk` 1.00 s / `..._Run` 0.67 s; extra `Idle_Eating`, `Headbutt`, `Jump_Start`, `Jump_Loop`, `Death` | ~110 L x 85 H cm, 18 bones, 2934 verts | prefix `pigAnimalArmature_AnimalArmature_AnimalArmature_` |
| **Critter_Chicken** | `Assets/ThirdParty/Animals/Chicken/Chicken.fbx` | `Body_end` (head/neck tip; no separate head bone) | `..._Idle` 2.00 s / **no Walk** (use `..._Run` 0.38 s at 0.5 play rate) / `..._Run` 0.38 s; extra `Idle_Peck`, `Attack`, `Death` | ~33 cm tall, 11 bones, 1760 verts | prefix `chicken_bAnimalArmature_AnimalArmature_AnimalArmature_`. Flap/scatter not included |
| Critter_Cow (Alpha, no prefab yet) | `Assets/ThirdParty/Animals/Cow/Cow.fbx` | `Head` | `cowIdle` 3.33 / `cowWalk` 1.17 / `cowGallop` 0.58 | ~220 L x 112 H cm, 57 bones | Duplicate `cowAnimalArmature_*` clips exist; use the short `cow*` ones |
| Critter_Bull (Alpha, no prefab yet) | `Assets/ThirdParty/Animals/Bull/Bull.fbx` | `Head` | `bullIdle` / `bullWalk` / `bullGallop`; `bullAttack_Headbutt` = charge | ~230 L cm | same rig as cow |
| Critter_Goat (stand-in, no prefab yet) | `Assets/ThirdParty/Animals/Alpaca/Alpaca.fbx` | `Head` | `alpacaIdle` 4.17 / `alpacaWalk` 1.46 / `alpacaGallop` 0.67 | ~110 L x 109 H cm | **No CC0 goat found**; alpaca is placeholder |
| Critter_Sheep (EA, no prefab yet) | `Assets/ThirdParty/Animals/Sheep/Sheep.fbx` | `Head` | `..._Idle` / `..._Walk` / `..._Run` (same set as pig) | bone extent 169 cm incl. tail/root helpers; check visually | |
| Critter_Crow (stand-in, no prefab yet) | `Assets/ThirdParty/Animals/Pigeon/Pigeon.fbx` | `Head` | `pigeonCharacterArmature_Idle` 2.0 / `_Walk` 0.42 / no flight | ~30 cm | Tint black via material variant; no fly anim |

## Goods / props (static FBX, base-centre pivot; colliders and attach points set on the prefab)
| Use | Asset | Size cm | Tris | Collider | Attach points |
|---|---|---|---|---|---|
| **GoodsProp_PrizePumpkin** | `Assets/ThirdParty/Props/Quaternius/SM_PrizePumpkin.fbx` | 158x160x99 | 644 | convex | carry_N/E/S/W, ribbon (180 kg) |
| **GoodsProp_EggCrate** (egg crate) | `Assets/ThirdParty/Props/Quaternius/SM_Crate.fbx` | 40x40x40 | 732 | box | hand_grip, egg_01..06 (all at top centre — reposition) |
| GoodsProp_Egg (goods inside crate) | `Assets/ThirdParty/Props/Quaternius/SM_Egg.fbx` | 5.5x5.5x7 | 288 | sphere | — |
| Feed bucket tool (`Assets/Prefabs/Tools/FeedBucket.prefab`) | `SM_FeedBucket` (Quaternius; FBX not yet in `Assets/ThirdParty/`) | 23x24x32 | 360 | convex | grip, grain_fx (top centre; pivot is base, not handle) |
| Net tool (in-house stand-in, `Assets/Prefabs/Tools/Net.prefab`) | `SM_Net` (`tools/freeassets/scripts/make_net.py`) | 140x50x43 | 368 | none | grip (30 cm from butt), hoop_center, bag_attach |
| Market stall (level) | `SM_MarketStall` (Quaternius; FBX not yet in `Assets/ThirdParty/`) | 134x300x271 | 1132 | convex (custom collider TODO) | sign, sell_zone, awning_tint |
| Market dressing set (level) | `SM_MarketStalls_Set` (Quaternius; FBX not yet in `Assets/ThirdParty/`) | 190x187x55 (unscaled, check) | 6064 | convex | — |
| Pen gate (level) | `SM_PenGate` (KayLousberg; FBX not yet in `Assets/ThirdParty/`) (+ `fence_gate_left`, `fence_gate_right` leaves) | 200x25x110 | 188 (frame) | box | latch. Leaves are separate meshes for hinged doors; pivots need checking |
| GoodsProp_Turnip | `Assets/ThirdParty/Props/Quaternius/SM_Turnip.fbx` | 13x13x25 | 976 | sphere | head |
| GoodsProp_Carrot | `Assets/ThirdParty/Props/Quaternius/SM_Carrot.fbx` | 8x8x25 | 864 | capsule | — |

## Still missing
Goat (alpaca stand-in), crow (pigeon stand-in, no flight), scarecrow (no source picked yet), chicken walk + flap anims, egg *basket* (crate used), real net with rope mesh, art-bible re-material pass.
