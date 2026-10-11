# HARVEST PANIC — Tripo3D Generation Batch 01

> **Status**: Draft v1 (Art Director, 2026-10-11). Source of gaps: `design/art/asset-gap-list.md`. Style authority: `design/art/art-bible.md` (V1 Toybox Readability, §6 readability, §10 budgets), `design/art/character-style.md` (sockets, hat silhouette rule).
> **Scope**: Vertical Slice (P0) + Alpha (P1) gaps that Emberlight cannot cover, plus [N]-tagged cosmetics (P2, generated early because they are cheap).
> **Physics masses**: no GDD defines kg for most goods. Masses below are **Art proposals** tuned to the carry-hand counts (`goods-value`: player = 2 hands, ~35 kg comfortable per hand-pair) and to the only canon values (bull 900 kg, KO'd player 70 kg). `physics-carry` / `goods-value` owners must confirm before tuning.

## 0. Shared Style Prefix (paste first in EVERY prompt)

```
stylized hand-painted game asset, Emberlight farm style, chunky soft bevels, rounded toy-like proportions, big simple readable silhouette, soft warm palette (honey yellow, barn red, sage green, cream, warm brown), painted gradient albedo with baked soft ambient occlusion, no micro detail, low-poly-ish clean quad-friendly topology, single object, centered, neutral T-pose or rest pose, isolated on plain background, no base, no ground plane, no pedestal
```

**Shared negative prompt** (append item-specific negatives after it):

```
photorealistic, PBR scan, realistic fur strands, hair cards, noisy texture, high-frequency detail, scratches, grime, text, logo, watermark, ground plane, base, pedestal, platform, shadow plane, multiple objects, cropped, floating parts, thin spikes, horror gore, dark desaturated palette, metallic chrome, glossy plastic
```

**Generation defaults**: Tripo text-to-3D, then Smart LowPoly / retopology to the target face count, texture = "HD" (export 2048, downsample per spec), PBR off (albedo-only; roughness is set by `M_HP_*` master material). Export **FBX** for rigged assets, **GLB→FBX** for static. If text-to-3D drifts off style, run image-to-3D from an Emberlight screenshot paintover (preferred for P0 animals).

**Face counts**: Tripo reports faces as tris here. Targets are LOD0; UE auto-LOD generates LOD1 (50%) and LOD2 (25%).

**Naming**: `SK_` rigged, `SM_` static, textures `T_<Asset>_BC`, materials are instances `MI_<Asset>` of `M_HP_Critter` / `M_HP_Prop` / `M_HP_Cosmetic`. Path: `/Game/HarvestPanic/Art/Tripo/<Category>/`.

## 1. Spec Index

| # | ID | Pri | Rig | Tris | Tex |
|---|---|---|---|---|---|
| 1 | SK_Pig_Hamlet | P0 | Quadruped | 5k | 1024 |
| 2 | SK_Chicken | P0 | Biped (bird) | 2k | 512 |
| 3 | SM_PrizePumpkin | P0 | — | 4k | 1024 |
| 4 | SM_EggBasket_Crate | P0 | — | 1.5k | 512 |
| 5 | SM_FeedBucket | P0 | — | 1.5k | 512 |
| 6 | SM_Net (+ SM_Net_Bag) | P0 | — | 3k | 512 |
| 7 | SM_MarketStall | P0 | — | 6k | 1024 |
| 8 | SM_MarketPenGate | P0 | — | 2k | 512 |
| — | Farmer base | P0 | **SKIPPED** | — | — |
| 9 | SK_Goat_Gary | P1 | Quadruped | 5k | 1024 |
| 10 | SK_Cow_BigMartha | P1 | Quadruped | 6k | 1024 |
| 11 | SK_Sheep | P1* | Quadruped | 5k | 1024 |
| 12 | SK_Mandrake | P1 | Biped (tiny) | 2.5k | 512 |
| 13 | SK_Turnip | P1 | — (WPO legs) | 1.2k | 512 |
| 14 | SK_Scarecrow_MrStitches | P1 | Biped (head-turn only) | 6k | 1024 |
| 15 | SK_Crow | P1* | Biped (bird) | 1.5k | 512 |
| 16 | SK_Landlord | P1 | Biped | 12k | 2048 |
| 17–23 | Cosmetics (7) | P2 | — | ≤1.5k | atlas |

*Sheep and Crow are EA in the roster; generated in this batch for style-family consistency (one animal family per art-bible risk note). Crow also unblocks the Crow Passenger cosmetic.

**Farmer base — skipped.** UE5 Manny/Quinn + bone scale + toon material + overalls bib (character-style) covers it and keeps the Game Animation Sample retarget free. Revisit only if the mannequin fails the 40 m team-readability test.

---

## 2. P0 — Vertical Slice

### 1. SK_Pig_Hamlet
- **Prompt**: `[PREFIX], cartoon farm pig named Hamlet, chubby barrel body, oversized round head (chibi, head ~40% of body length), big floppy ears, flat pink snout disc, tiny stubby legs, curly tail, smug half-lidded eyes, soft pink with warmer rose belly gradient, small mud splotch on one hip, quadruped standing on four legs`
- **Negative**: `[SHARED], realistic pig, wrinkles, bristles, tusks, lean body`
- **Tris** 5,000 · **Tex** 1024 BC · **Scale** 110 cm long, 65 cm shoulder · **Pivot** ground centre between feet, +X forward
- **Rig**: Tripo quadruped auto-rig. **Anims**: idle, walk, run (+ request "jump" if offered for under-fence squeeze). Squeal/grabbed struggle authored in UE (additive).
- **UE import**: Physics Asset capsule-per-limb from auto-gen, then simplify to ≤ 8 bodies. Pawn collision = capsule r 35 / hh 35. Mass **50 kg** (2 hands, 1 carrier, pull 500 N). Sockets: `head` (cosmetic hats on livestock, joke), `grab_back`, `mouth` (VO/VFX squeal).
- **Readability**: ears + snout must read at 40 m against wheat; head/ears must clear crop line (art bible §6.2) — wheat ~70 cm, so ears top ≥ 75 cm. Pink saturation +20% vs environment.

### 2. SK_Chicken
- **Prompt**: `[PREFIX], cartoon farm hen, round egg-shaped body, tiny head with big red comb and wattle, orange beak, stubby wings, thin orange legs thickened for readability, cream-white feathers painted as large soft shapes, startled big eyes`
- **Negative**: `[SHARED], individual feathers, realistic bird, rooster tail plumes`
- **Tris** 2,000 (hard cap, 10 on screen) · **Tex** 512 · **Scale** 40 cm tall · **Pivot** ground between feet
- **Rig**: Tripo biped rig (bird); if Tripo offers no bird skeleton, use quadruped template and drop the front legs, or rig in UE with a 12-bone custom skeleton. **Anims**: idle, walk, run; flap and scatter authored in UE.
- **UE import**: capsule r 15 / hh 18. Mass **2.5 kg** (1 hand, net-able SizeClass Small). Sockets: `head`, `grab_body`.
- **Readability**: red comb is the identifier at distance; white body must not vanish against cream UI or hay — rely on rim `#FFF4D6` + dust kick.

### 3. SM_PrizePumpkin ("Lady Gourdiva")
- **Prompt**: `[PREFIX], giant prize-winning pumpkin, slightly squashed sphere with deep chunky ribs, thick curly green stem with one leaf and a tendril, warm saturated orange with lighter ridge tops, faint blue ribbon rosette tied to stem`
- **Negative**: `[SHARED], carved face, jack-o-lantern, small pumpkin`
- **Tris** 4,000 · **Tex** 1024 · **Scale** 160 cm wide, 120 cm tall (gap list: ≥ 1.5 m) · **Pivot** base centre
- **Rig**: none. Squash/wobble via WPO in `M_HP_Prop`.
- **UE import**: collision = simplified convex (8–12 hulls via auto convex, or one sphere + stem box). Mass **180 kg** (8 hands / 4 carriers; one player alone cannot lift). Sockets: `carry_N/E/S/W` (4 handholds at equator), `ribbon`.
- **Readability**: tier glint + size tell value; must look 4-person heavy from first person — at 2 m it should fill > 50% of screen width.

### 4. SM_EggBasket_Crate
- **Prompt**: `[PREFIX], small wicker egg basket with a single arched handle and a folded cream cloth, holding six chunky oversized eggs, honey-brown wicker painted as big woven bands`
- **Negative**: `[SHARED], fine wicker strands, cracked eggs`
- **Tris** 1,500 · **Tex** 512 · **Scale** 40×30×35 cm (with handle) · **Pivot** base centre
- **UE import**: box collision + handle ignored. Mass **4 kg** (1 hand, fragility high — per goods-value). Sockets: `hand_grip`, `egg_01..06` (eggs as separate SM_Egg for spill VFX: generate eggs separately at 150 tris).
- **Readability**: eggs read as the content even when tilted; Emberlight `SM_Crate` remains the bulk crate — this is the fragile variant.

### 5. SM_FeedBucket
- **Prompt**: `[PREFIX], chunky wooden feed bucket with two dark iron bands and a curved wire handle, heaped with golden grain and a few corn kernels on top, slightly tapered`
- **Negative**: `[SHARED], metal pail, rust, thin wire`
- **Tris** 1,500 (held tool ≤ 3k) · **Tex** 512 · **Scale** 30 cm Ø, 32 cm tall (FP view scale ×1.2 applied in the first-person mesh, not the asset) · **Pivot** handle apex (for hand socket)
- **UE import**: cylinder convex. Mass **3 kg**. Sockets: `grip`, `grain_fx` (lure particle).
- **Readability**: grain heap = "food" read for pigs/cows at a glance; golden Golden Bucket upgrade = material swap only.

### 6. SM_Net (+ SM_Net_Bag)
- **Prompt (net)**: `[PREFIX], farm butterfly-style catching net, thick wooden handle with leather grip wraps, large round wooden hoop, chunky rope mesh rendered as big diamond panels, cream rope, net bag hanging softly`
- **Prompt (bag)**: `[PREFIX], closed cream rope net sack tied at the top with a knot, bulging round shape, empty`
- **Negative**: `[SHARED], fishing rod, metal hoop, thin strings, see-through mesh holes`
- **Tris** 3,000 net / 800 bag · **Tex** 512 shared · **Scale** 140 cm total, hoop 50 cm Ø · **Pivot** net: grip point 30 cm from butt; bag: knot
- **UE import**: net = no collision (trace-based swing); bag = sphere collision, mass 1 kg + captured critter. Mesh holes = masked opacity in material, not geometry. Sockets: `grip`, `hoop_center`, `bag_attach`.
- **Readability**: hoop must read in FP periphery; swing arc readable to teammates at 20 m.

### 7. SM_MarketStall
- **Prompt**: `[PREFIX], wooden village market stall, sturdy plank counter, four chunky posts, striped canvas awning in cream and barn red with scalloped edge, small hanging blank wooden sign, a couple of empty crates under the counter`
- **Negative**: `[SHARED], text on sign, produce, people, modern tent`
- **Tris** 6,000 · **Tex** 1024 · **Scale** 300 W × 180 D × 280 H cm · **Pivot** front-centre at ground
- **UE import**: custom UCX collision (counter box + 4 posts; awning no collision). Static, no mass. Sockets: `sign` (decal/text widget), `sell_zone` (day-loop drop volume), `awning_tint` via MPC region swap.
- **Readability**: awning stripe colour = stall identity; must contrast the Landlord's plum. Counter height 95 cm so goods placed on top are visible.

### 8. SM_MarketPenGate
- **Prompt**: `[PREFIX], short sturdy livestock pen gate, five horizontal chunky planks with a diagonal brace, two thick square posts, big iron hinges and a simple drop latch, weathered warm wood`
- **Negative**: `[SHARED], metal gate, chain-link, ornate`
- **Tris** 2,000 · **Tex** 512 (or reuse `T_Gate14` via material if trim matches) · **Scale** 200 W × 110 H cm · **Pivot** gate leaf: hinge axis bottom; export leaf + posts as separate meshes (`SM_MarketPenGate_Leaf`, `SM_MarketPenGate_Post`)
- **UE import**: box collision per leaf; physics constraint hinge in BP. Leaf mass **25 kg**. Sockets: `latch`.
- **Readability**: must match Emberlight `SM_WoodenFence` height (verify ~110 cm) so pens tile.

---

## 3. P1 — Alpha

### 9. SK_Goat_Gary
- **Prompt**: `[PREFIX], cartoon farm goat named Gary, lanky body, big rectangular-pupil goggle eyes with a mischievous look, short curved horns, little chin beard, oversized ears sticking sideways, cream-white with a warm brown saddle patch, quadruped`
- **Negative**: `[SHARED], realistic goat, long sharp horns, shaggy fur`
- **Tris** 5,000 · **Tex** 1024 · **Scale** 100 cm long, 75 cm shoulder · **Pivot** ground centre
- **Rig**: quadruped. **Anims**: idle, walk, run (+ jump if available, for climbing).
- **UE import**: capsule r 30 / hh 40. Mass **40 kg** (2 hands). Sockets: `head`, `horns` (headbutt hit origin), `grab_back`.
- **Readability**: sideways ears + beard make a different head silhouette from the pig; horns rounded (cute shapes, sharp only at edges).

### 10. SK_Cow_BigMartha
- **Prompt**: `[PREFIX], huge cartoon dairy cow named Big Martha, wide blocky body, lazy sleepy half-closed eyes, big pink muzzle, small stubby horns, cowbell on a leather collar, cream with large chocolate-brown patches, quadruped`
- **Negative**: `[SHARED], black and white photo-real Holstein, udder detail, bull, muscles`
- **Tris** 6,000 (cap) · **Tex** 1024 · **Scale** 220 cm long, 140 cm shoulder · **Pivot** ground centre
- **Rig**: quadruped. **Anims**: idle, walk (run optional; she barely flees).
- **UE import**: capsule r 60 / hh 70. Mass **450 kg** (6 hands / 3 carriers; pull 900 N). Sockets: `head`, `bell` (moo VFX/Noise origin), `push_rear`, `push_left`, `push_right`.
- **Readability**: bulk + bell; brown patches (not black) keep the warm palette. **Bull** later = same body, darker plum-brown, larger horns, nose ring (variant prompt, P1 follow-up).

### 11. SK_Sheep
- **Prompt**: `[PREFIX], round fluffy cartoon sheep, cloud-shaped cream wool body made of big soft lumps, small dark charcoal face and legs, sleepy anxious eyes, quadruped`
- **Negative**: `[SHARED], wool strands, realistic fleece`
- **Tris** 5,000 · **Tex** 1024 · **Scale** 100 cm long, 70 cm shoulder · **Pivot** ground centre
- **Rig**: quadruped. **Anims**: idle, walk, run.
- **UE import**: capsule r 35 / hh 35. Mass **45 kg** (2 hands). Sockets: `head`, `bell` (bell-wether variant via socketed SM bell).
- **Readability**: dark face against cream wool = head direction readable at 40 m. Mud-dirty wool via material param (wool bonus loss).

### 12. SK_Mandrake (Screaming)
- **Prompt**: `[PREFIX], cursed mandrake root creature, pale tan carrot-like root body shaped like a chubby little person, stubby root arms and legs, tuft of big green leaves on head, wide open screaming mouth, tiny squeezed shut eyes`
- **Negative**: `[SHARED], realistic root hairs, scary horror face, teeth`
- **Tris** 2,500 · **Tex** 512 · **Scale** 35 cm tall (incl. leaves) · **Pivot** base of root (planted height: bury 60%)
- **Rig**: Tripo biped (small). **Anims**: idle, walk, run. Scream face = blendshape **not available from Tripo** → generate a second model `SM_Mandrake_FaceCalm` mouth-closed and swap via material mask / two mouth meshes on a `mouth` socket.
- **UE import**: capsule r 10 / hh 17. Mass **1.5 kg** (1 hand). Sockets: `mouth` (scream VFX/Noise), `leaves` (grab point when pulled).
- **Readability**: leaf tuft visible above soil = "something is here"; scream mouth must be readable at 10 m.

### 13. SK_Turnip (Burrowing)
- **Prompt**: `[PREFIX], cursed turnip, round white bulb with purple-magenta top half, three big leaves, tiny dot eyes and a nervous little mouth, short root tail`
- **Negative**: `[SHARED], realistic veggie, dirt texture`
- **Tris** 1,200 · **Tex** 512 · **Scale** 25 cm tall · **Pivot** bulb bottom
- **Rig**: none; legs via WPO per gap list (same as Emberlight crops). Optional Tripo biped if WPO legs fail review.
- **UE import**: sphere collision r 10. Mass **1 kg** (1 hand, net-able). Sockets: `head`.
- **Readability**: purple top is unique in palette — no other crop uses magenta. Spawn in bunches of 5: verify 5 together still read as separate units.

### 14. SK_Scarecrow_MrStitches
- **Prompt**: `[PREFIX], creepy-cute scarecrow, burlap sack head with stitched crooked smile and two button eyes, floppy patched straw hat, patched plaid shirt in faded sage and barn red, straw poking out of cuffs and collar, arms outstretched on wooden cross post, tall and thin`
- **Negative**: `[SHARED], gore, blood, realistic horror, pumpkin head`
- **Tris** 6,000 · **Tex** 1024 · **Scale** 230 cm tall incl. post · **Pivot** post base
- **Rig**: biped auto-rig, but only use `head` + `neck` + arm bones (gap list: head-turn rig, no locomotion). **Anims**: idle only. Alternative: keep Emberlight `SM_Scarecrow` + lime eyes if this generation doesn't beat it in side-by-side review.
- **UE import**: capsule r 30 / hh 115; no physics sim. Sockets: `eye_L`, `eye_R` (lime `#B6FF3B`-family emissive, per art bible "green eyes = run"), `head`.
- **Readability**: lime button eyes are the only emissive read at night; silhouette = cross with drooping hat, distinct from players (no team colour, no hat ≥ 30% rule).

### 15. SK_Crow
- **Prompt**: `[PREFIX], cartoon crow, round chunky body, big head, thick pale-yellow beak, beady curious eyes, deep blue-black feathers painted as broad plum-tinted shapes, short tail fan, wings folded`
- **Negative**: `[SHARED], realistic raven, individual feathers, pure black`
- **Tris** 1,500 · **Tex** 512 · **Scale** 40 cm long, 80 cm wingspan · **Pivot** feet
- **Rig**: biped (bird) or custom; **Anims**: idle, walk (hop). Flight cycle authored in UE.
- **UE import**: sphere r 15, no physics sim. Mass 0.5 kg (cosmetic/thief only). Sockets: `beak` (stolen-item attach), `head`.
- **Readability**: plum-tinted black, never pure black (lifted night blacks). Shares mesh with Crow Passenger cosmetic (scaled 0.6).

### 16. SK_Landlord
- **Prompt**: `[PREFIX], cartoon villainous landlord, very tall and thin man, absurdly tall black-plum top hat about 40% of total height, long plum tailcoat, narrow shoulders, pointy nose, thin curled moustache, monocle, holding a ledger book, smug expression, T-pose`
- **Negative**: `[SHARED], realistic human, muscular, gun, cane spikes`
- **Tris** 12,000 · **Tex** 2048 (1 material) · **Scale** 230 cm tall incl. hat (body 140 cm) · **Pivot** feet
- **Rig**: Tripo biped; **retarget to UE Manny** (IK Retargeter) so Game Animation Sample idles work. **Anims**: idle, walk.
- **UE import**: capsule r 30 / hh 115. No carry mass (NPC; landlord-events). Sockets: `hat`, `hand_r` (ledger), `head`.
- **Readability**: hat = 40% height is the whole character (gap list). Plum must not be used by any team colour.

---

## 4. P2 — Cosmetics marked [N] (character-style)

All: ≤ 1.5k tris, **pack into shared 2048 `T_Cosmetics_Atlas`** after generation (re-UV in DCC or Tripo texture re-bake), `M_HP_Cosmetic`, no collision, no mass, attached to the named socket. Scale assumes mannequin head ~25 cm. Pivot = socket attach point.

| # | ID | Socket | Prompt (after PREFIX) | Silhouette check |
|---|---|---|---|---|
| 17 | SM_Hat_Viking | `head` | `cartoon viking helmet, rounded iron cap with warm brown leather band, two huge curved cream horns` | Horns add ≥ 40% width |
| 18 | SM_Hat_DuckCrown | `head` | `small gold paper-style crown with a big yellow rubber duck sitting on top` | Duck adds ≥ 30% height |
| 19 | SM_Hat_BeekeeperVeil | `head` | `wide-brim cream beekeeper hat with soft mesh veil hanging to shoulders` (veil = masked material; optional AnimDynamics) | Brim ≥ 40% width |
| 20 | SM_Neck_ConeOfShame | `neck` (add if missing; else `head` offset) | `pet vet cone collar, soft translucent-looking cream plastic, chunky rim` | Cone ≥ 40% width |
| 21 | SM_Face_SunflowerGlasses | `face` | `novelty sunglasses with sunflower petal frames, dark lenses` | Face slot, no rule |
| 22 | SM_Tool_GrandpaCamcorder | `hand_r` / `back` | `chunky 1990s camcorder with red record light and a strap, rounded toy shapes` | Held tool ≤ 3k; this ≤ 1.5k |
| 23 | SM_Back_DuckFloatie | `spine_03` / waist | `inflatable yellow duck swim ring, glossy-painted but soft` | Width ≥ shoulders |

Reuse, no new generation: **Chicken Hat** = SK_Chicken static pose bake (scale 0.6) on a straw nest; **Crow Passenger** = SK_Crow idle on `shoulder_l`.

---

## 5. QA Acceptance Checklist (per generated model)

Reject and re-roll (max 3 rolls, then switch to image-to-3D from paintover) if any **Must** fails.

**Style (Must)**
- [ ] Side-by-side with Emberlight `SM_Crate`, `SM_Scarecrow`, `SM_Barn` under the day lighting rig: same bevel softness, painted albedo, no photographic noise.
- [ ] Palette sampled within art bible §4 ranges; no pure black/white; no off-palette neon (except lime eyes on creatures).
- [ ] Silhouette passes the **squint test** (thumbnail 64 px, black fill): species/object identifiable.

**Geometry (Must)**
- [ ] Tris ≤ target (critters ≤ 6k, cosmetics ≤ 1.5k, held tools ≤ 3k, player-adjacent ≤ 15k total).
- [ ] No ground plane, base, or floating debris; single watertight-ish mesh (or declared split parts).
- [ ] No inverted normals, no non-manifold spikes at bevels; no fused limbs (legs separated for rigs).
- [ ] Real-world scale correct in UE (place next to 180 cm mannequin) and pivot as specified.

**Texture (Must)**
- [ ] UVs non-overlapping; texture downsampled to spec; sRGB BC only; no baked hard directional shadow (soft AO allowed).
- [ ] Imported as `MI_` of the right `M_HP_*` master; rim/saturation pop params work.

**Rig & Animation (Must for SK_)**
- [ ] ≤ 50 bones; root at ground; skeleton named consistently across all quadrupeds (shared skeleton if Tripo allows — try to retarget all quadrupeds to one `SKEL_HP_Quadruped`).
- [ ] Idle/walk/run loop without pops; feet don't slide > 5 cm at nominal speed; root motion off (in-place) unless specified.
- [ ] No mesh tearing at shoulders/hips at extreme pose; grab/ragdoll physics asset stable (drop test from 2 m).

**Gameplay readability (Must)**
- [ ] Readable at 40 m in tall wheat (art bible §6.2): head/ears clear crop line or wheat parts.
- [ ] Night test (EV100 6): critter eye shine `#FFF4D6` / creature lime visible.
- [ ] Sockets present and named per spec; collision and mass set.

**Hygiene (Should)**
- [ ] Naming per convention, 3 LODs generated, Nanite **off** for skeletal / on optional for static props.
- [ ] Tripo task ID + prompt + seed recorded in the asset's metadata/commit message for reproducibility.
- [ ] Licence: Tripo output terms checked for the account tier (commercial use) before shipping.

## 6. Open Questions (for user / owners)
1. Confirm proposed masses (pig 50, chicken 2.5, pumpkin 180, cow 450, goat 40, sheep 45 kg) with `physics-carry` / `goods-value`.
2. Tripo bird rigs: if unsupported, approve a custom 12-bone bird skeleton (technical-artist).
3. Sheep and Crow pulled forward from EA for style consistency — confirm with producer.
