# HARVEST PANIC — Asset Gap List

> **Status**: Draft v1 (Art Director, 2026-10-11). Inventory taken from `HarvestPanic/Content/Emberlight` file names. **Free-source recommendations must be verified on Fab** (license = Fab Standard/free, UE 5.x compatible, stylized enough to pass the art bible §3 re-material rule) before import. Listing names below are search targets, not confirmed listings.

## 1. What Emberlight Covers

| Category | Assets | Fit |
|---|---|---|
| Structures | `SM_Barn`, `SM_Barn_Door`, `SM_Windmill`(+`_Rotate`), `SM_Gate14`, `SM_WoodenPillar03` | Barn = respawn hub; only one barn and one windmill design, so PCG variety needs reskins |
| Fences | `SM_WoodenFence`, `SM_WoodenFenceLong`, `BP_WoodenFence` | Pens and PCG fence segments; good |
| Crops / goods | Carrot, Corn, Cucumber, Eggplant, Mushroom, Onion, Radish, Watermelon | Cursed crop bodies (add WPO legs); **no pumpkin, no turnip, no mandrake** |
| Props | Bushel, Coop, Crate, Sack, Harvest Basket, Hay Bales (2), Honey, Lanterns (3), Pitchfork, Rake, Rope Coil, Scarecrow, Scythe, Seedling, Shovel, Sickle, Pot, Strawhat, Table, Chair, Watering Can, Wooden Cart, Wheelbarrow, Signpost, Bell Post, Pile of Grain | Very strong. Wooden Cart = wagon base; Scarecrow = night creature base mesh; Bell Post = Sunday bell; Strawhat = default hat |
| Foliage / nature | Bush03, Flower Group, Grass/Moss cards, Wheat cards + `NS_Wheat`, Soil Block, Tree01, Rock09, Background terrain | Field + farm dressing; **only 1 tree and 1 rock**, so forest, cliffs and orchard need more |
| Sky / FX | `BP_Embersky`, `NS_Wheat`, `MF_WorldAligned` | Day sky; night variant needs a material pass |

## 2. Gaps (priority: P0 = vertical slice blocker, P1 = Alpha, P2 = EA, P3 = Post)

| # | Gap | Needed for | Priority | Recommended source | Notes |
|---|---|---|---|---|---|
| 1 | **Pig** (rigged, run/idle/grabbed/squeal) | VS | **P0** | Fab free "Stylized farm animals" / "Low poly animals" (Quaternius *Ultimate Animated Animal Pack* is CC0 and rigged), or "Stylized Pig" free listings | Highest risk. Re-material per art bible; chibi-scale head via bone scale |
| 2 | **Chicken** (flock-friendly, ≤ 2k tris) | VS | **P0** | Quaternius CC0 farm animals; Fab free "Low Poly Chicken" | Needs flap and scatter anims; 10 on screen |
| 3 | **Prize Pumpkin** | VS | **P0** | Quixel/Fab free pumpkin, re-materialed | Must look 4-person heavy; ≥ 1.5 m |
| 4 | **Farmer character** | VS | **P0** | UE5 Manny/Quinn + bone-scale + toon material + overalls bib (see character-style); later a stylized farmer from Fab free | Cosmetic sockets from day one |
| 5 | **Player animations + carry poses** | VS | **P0** | UE **Game Animation Sample** (free, motion matching) + Control Rig IK carry | Coordinate with gameplay/anim owner |
| 6 | **Bridge + river + cliffs** (Ready Map Vol 2 – Bridge, Six Sides Studios) | VS | **P0** | **Owned but not installed**: install from the Fab library | Check stylization vs Emberlight; apply `M_HP_Reskin` to match. Breakable plank needs a split mesh |
| 7 | Tools: net, lasso, feed bucket, Grandpa's Camcorder | VS / Alpha | P0 (net, bucket) / P1 | Kitbash: Emberlight Rope Coil + Shovel handle → net; Quixel bucket re-materialed; lasso = Cable component | First-person scale 1.2x |
| 8 | Goat, Cow, Bull | Alpha | P1 | Quaternius CC0 animal packs; Fab free stylized farm animals | Bull needs readable "charging" anticipation pose |
| 9 | **Scarecrow (night creature)** | Alpha | P1 | Emberlight `SM_Scarecrow` + lime eye emissive + optional head-turn rig (2 bones) | Static mesh is fine; "moves when unseen" means no locomotion anims |
| 10 | Barn Thing, Crows | EA | P2 | Crows: Quaternius CC0 birds; Barn Thing: hay-card body + eyes, mostly VFX | Keep shapes cute, sharp only at edges |
| 11 | **Market Town (Muddlebury)**: stalls, cobbles, buildings, Landlord's office | Alpha | P1 | Fab free "stylized medieval village / market stall" packs; Quixel re-materialed; Emberlight Table/Crate/Bushel for stall dressing | Small zone; reskin per region via MPC |
| 12 | Landlord NPC | Alpha | P1 | Mannequin + too-tall top hat + plum suit material | Hero silhouette: tall, thin, hat = 40% height |
| 13 | Rival Farmer NPC | EA | P2 | Reuse farmer kit, desaturated overalls + villain moustache | |
| 14 | Turnip, Mandrake | Alpha | P1 | Emberlight Radish/Onion reshaped + face decal | Mandrake needs a scream-face swap |
| 15 | Sheep, Duck, Llama | EA | P2 | Quaternius CC0; Fab free | Llama spit VFX |
| 16 | More trees, rocks, cliffs (forest edge, orchard) | EA / Post | P2 | Fab free stylized nature packs; Quixel re-materialed | Must read like Emberlight Tree01 |
| 17 | Rafts, mist, river dressing (Riverlands) | Post | P3 | Bridge pack + kitbashed planks | |
| 18 | Orchard trees, autumn foliage, Region 3 creatures | Post | P3 | Fab free autumn/halloween stylized packs | |
| 19 | VFX set (art bible §8) | VS (dust, stars, bruise) / Alpha (rest) | P0/P1 | Build in Niagara from simple sprites; Fab free "stylized VFX" for reference | technical-artist owns |
| 20 | UI kit (paper, stamps, wooden signs, icons) | VS (minimal) / Alpha | P1 | Google Fonts Lilita One + Nunito; Kenney.nl CC0 UI/icon packs as placeholders | |
| 21 | Night sky + moon variant of `BP_Embersky` | Alpha | P1 | Material params on Embersky; no new asset | |
| 22 | Cosmetics marked [N] | EA | P2 | Commission or simple in-house modeling | See character-style |

## 3. Recommended Order
1. **Week 0 (before VS week 1):** install the Bridge pack; download and evaluate 2–3 candidate animal packs for pig + chicken (license, rig, look). Pick one family so all animals share a style.
2. **VS:** pig, chicken, pumpkin, mannequin farmer, net, feed bucket, dust/stars/bruise VFX, Emberlight + Bridge stitched map.
3. **Alpha:** goat/cow/bull, Scarecrow pass, Market Town kit, Landlord, turnip/mandrake, UI kit, night lighting.
4. **EA:** remaining species, crows/Barn Thing, cosmetics, extra nature.

## 4. Risks
- **Style mismatch between packs** (Emberlight painterly vs Bridge/animal packs). Mitigation: everything routes through `M_HP_*` master materials and the region MPC.
- **Animal pack licences**: CC0 (Quaternius) is safest. Fab free listings need checking for redistribution limits and AI-training clauses.
- **Single barn/tree/rock** meshes make PCG repetitive: plan scale/rotation/colour variation and a second barn variant by kitbash.
