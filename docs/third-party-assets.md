# Third-Party (CC0) Asset Mapping — for the integration lead

All assets are CC0 (see `THIRD_PARTY_LICENSES.md`). Imported headless by
`HarvestPanic/Scripts/AssetGen/import_thirdparty.py` from `tools/freeassets/manifest.json`;
per-asset metrics in `tools/freeassets/import_report.json` (bounds, tris/verts, bones, anim lengths).
Re-run: see the script docstring (uses a content-only sandbox project; output copied under `Content/HarvestPanic/ThirdParty/`).

## Conventions / gotchas
- **Critters face +Y** (Quaternius rigs), Z up, root at the feet. In the Character BP set the Mesh component **yaw -90** so they face +X.
- **No skeletal sockets were authored** (UE 5.8 Python cannot set `SkeletalMeshSocket.socket_name`). Attach to the **bone** below (`AttachToComponent(..., SocketName=<bone>)` works with bone names), or add a `head` socket once in the Skeleton editor on that bone. Bone name is also in asset metadata `HP_HeadBone`.
- Anim sequence names are long (Interchange naming): `<file><Armature>_<Clip>`. Use the clip suffix below.
- Materials are the source flat-colour materials (not yet on `M_HP_*`); re-material/tint per art bible is a follow-up (pig pink `#F2A3A0`).
- Size metadata: SkeletalMesh *bounds* read wrong for these GLBs (100x); trust the bone extents listed here. Verify physics asset capsules once in-editor.
- Mass is stored as asset metadata `HP_MassKg` (from the Tripo spec).

## Critters (SkeletalMesh + Skeleton + AnimSequences in the same folder)
| Gameplay class | Mesh | Head bone | Idle / Walk / Run anim (suffix) | Size (bone extent) | Notes |
|---|---|---|---|---|---|
| **AHPCritter_Pig** | `/Game/HarvestPanic/ThirdParty/Quaternius/SK_Pig/SK_Pig` | `Head` (also `Neck`, `Body` for grab_back) | `..._Idle` 6.67 s / `..._Walk` 1.00 s / `..._Run` 0.67 s; extra `Idle_Eating`, `Headbutt`, `Jump_Start`, `Jump_Loop`, `Death` | ~110 L x 85 H cm, 18 bones, 2934 verts | prefix `pigAnimalArmature_AnimalArmature_AnimalArmature_` |
| **AHPCritter_Chicken** | `/Game/HarvestPanic/ThirdParty/Quaternius/SK_Chicken/SK_Chicken` | `Body_end` (head/neck tip; no separate head bone) | `..._Idle` 2.00 s / **no Walk** (use `..._Run` 0.38 s at 0.5 play rate) / `..._Run` 0.38 s; extra `Idle_Peck`, `Attack`, `Death` | ~33 cm tall, 11 bones, 1760 verts | prefix `chicken_bAnimalArmature_AnimalArmature_AnimalArmature_`. Flap/scatter not included |
| AHPCritter_Cow (Alpha) | `/Game/HarvestPanic/ThirdParty/Quaternius/SK_Cow/SK_Cow` | `Head` | `cowIdle` 3.33 / `cowWalk` 1.17 / `cowGallop` 0.58 | ~220 L x 112 H cm, 57 bones | Duplicate `cowAnimalArmature_*` clips exist; use the short `cow*` ones |
| AHPCritter_Bull (Alpha) | `/Game/HarvestPanic/ThirdParty/Quaternius/SK_Bull/SK_Bull` | `Head` | `bullIdle` / `bullWalk` / `bullGallop`; `bullAttack_Headbutt` = charge | ~230 L cm | same rig as cow |
| AHPCritter_Goat (stand-in) | `/Game/HarvestPanic/ThirdParty/Quaternius/SK_Alpaca/SK_Alpaca` | `Head` | `alpacaIdle` 4.17 / `alpacaWalk` 1.46 / `alpacaGallop` 0.67 | ~110 L x 109 H cm | **No CC0 goat found**; alpaca is placeholder |
| AHPCritter_Sheep (EA) | `/Game/HarvestPanic/ThirdParty/Quaternius/SK_Sheep/SK_Sheep` | `Head` | `..._Idle` / `..._Walk` / `..._Run` (same set as pig) | bone extent 169 cm incl. tail/root helpers; check visually | |
| AHPCritter_Crow (stand-in) | `/Game/HarvestPanic/ThirdParty/Quaternius/SK_Pigeon/SK_Pigeon` | `Head` | `pigeonCharacterArmature_Idle` 2.0 / `_Walk` 0.42 / no flight | ~30 cm | Tint black via MI; no fly anim |

## Goods / props (StaticMesh, base-centre pivot, collision baked, sockets on mesh)
| Use | Asset | Size cm | Tris | Collision | Sockets |
|---|---|---|---|---|---|
| **AHPGoodsProp PrizePumpkin** | `/Game/HarvestPanic/ThirdParty/Quaternius/SM_PrizePumpkin/SM_PrizePumpkin` | 158x160x99 | 644 | convex | carry_N/E/S/W, ribbon (180 kg) |
| **AHPGoodsProp Crate** (egg crate) | `/Game/HarvestPanic/ThirdParty/Quaternius/SM_Crate/SM_Crate` | 40x40x40 | 732 | box | hand_grip, egg_01..06 (all at top centre — reposition) |
| Egg (goods inside crate) | `/Game/HarvestPanic/ThirdParty/Quaternius/SM_Egg/SM_Egg` | 5.5x5.5x7 | 288 | sphere | — |
| Feed bucket tool | `/Game/HarvestPanic/ThirdParty/Quaternius/SM_FeedBucket/SM_FeedBucket` | 23x24x32 | 360 | convex | grip, grain_fx (top centre; pivot is base, not handle) |
| Net tool (in-house stand-in) | `/Game/HarvestPanic/ThirdParty/InHouse/SM_Net/SM_Net` | 140x50x43 | 368 | none | grip (30 cm from butt), hoop_center, bag_attach |
| Market stall (level) | `/Game/HarvestPanic/ThirdParty/Quaternius/SM_MarketStall/SM_MarketStall` | 134x300x271 | 1132 | convex (UCX TODO) | sign, sell_zone, awning_tint |
| Market dressing set (level) | `/Game/HarvestPanic/ThirdParty/Quaternius/SM_MarketStalls_Set/SM_MarketStalls_Set` | 190x187x55 (unscaled, check) | 6064 | convex | — |
| Pen gate (level) | `/Game/HarvestPanic/ThirdParty/KayLousberg/SM_PenGate/SM_PenGate` (+ `fence_gate_left`, `fence_gate_right` leaves) | 200x25x110 | 188 (frame) | box | latch. Leaves are separate meshes for hinged doors; pivots need checking |
| Turnip crop | `/Game/HarvestPanic/ThirdParty/Quaternius/SM_Turnip/SM_Turnip` | 13x13x25 | 976 | sphere | head |
| Carrot crop | `/Game/HarvestPanic/ThirdParty/Quaternius/SM_Carrot/SM_Carrot` | 8x8x25 | 864 | capsule | — |

## Still missing
Goat (alpaca stand-in), crow (pigeon stand-in, no flight), scarecrow (use Emberlight `SM_Scarecrow`), chicken walk + flap anims, egg *basket* (crate used), real net with rope mesh, art-bible re-material pass.
