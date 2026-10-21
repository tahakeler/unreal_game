# HARVEST PANIC — Tripo Generation Batch 02: Character Creation

> **Status**: Draft v1 (Art Director, 2026-10-21). Engine: **Unity 6.3 LTS + URP** (supersedes Unreal conventions in batch 01).
> **Style authority**: `design/art/art-bible.md`, `design/art/character-style.md` (sockets, silhouette rule, team colours), reference render `~/Desktop/HarvestPanic Tripo References/01_Pig_Hamlet.png`.
> **Design authority**: `design/gdd/character-customization.md`, `design/master-gdd/13-customization.md`.
> **Goal**: a character creator that reads as polished top-tier Steam co-op (Lethal Company / PEAK / R.E.P.O. charm): instantly recognisable idiots at 40 m, funny in a mirror and in the Crew Photo.

---

## 0. Read Me First (5 rules)

1. **Paste the prompt exactly** — the style prefix is already merged in. Paste the matching negative prompt into Tripo's negative field.
2. **Flat colour zones.** Overalls, shirt, boots/gloves and skin must each be one flat, distinct colour (see §2). No patterns, no stitching, no painted shading across zone borders. Unity recolours these.
3. **Bodies are headless** and in **A-pose**; heads are separate; hats/hair/face/back items are separate static meshes. One object per generation.
4. **Topology pass**: after generation run Smart LowPoly / retopology to the target tris, texture "HD" (export 2048, downscale per spec), PBR off (albedo only; URP material sets smoothness).
5. **Save** to `~/Desktop/HarvestPanic Tripo Models/<Category>/<FileName>` (exact names below). Import later goes to `HarvestPanic/Assets/ThirdParty/Tripo/<FileName without ext>/` with the FBX + textures together.

---

## 1. Shared Blocks (already merged into every prompt below)

**Style prefix**
```
soft toy-like stylized cartoon 3D, smooth rounded chunky forms, vinyl-toy proportions, big simple readable silhouette, warm friendly palette, clean flat colour zones with gentle baked ambient occlusion, no micro detail, clean low-poly quad-friendly topology, single object, centered, plain white background, no base, no ground plane
```

**Negative prompt (paste for every asset; item extras listed per asset)**
```
photorealistic, realistic skin pores, PBR scan, hair strands, hair cards, fur strands, noisy texture, high-frequency detail, scratches, grime, text, logo, watermark, ground plane, base, pedestal, multiple objects, cropped, floating parts, thin spikes, gore, horror, creepy, dark desaturated palette, metallic chrome, glossy plastic, patterned fabric, plaid, stripes, painted shadows
```

**Unity conventions**
- **Format**: FBX (binary) for everything. Rigged bodies: export from Tripo with skin; accessories: GLB→FBX is fine.
- **Rig**: bodies = Tripo auto-rig **humanoid biped** → Unity Import Settings `Animation Type: Humanoid`, `Avatar: Create From This Model`. Heads: no rig of their own; skinned 100% to the body's `Head` bone at import (or parented). Accessories: none.
- **Scale/axes**: 1 unit = 1 m, Y-up, facing +Z. Apply scale in Tripo export (scale factor 1).
- **Sockets**: Unity child transforms created by code at runtime on `Animator.GetBoneTransform(HumanBodyBones.X)`. Names: `Socket_Head` (Head), `Socket_Face` (Head, offset to eye line), `Socket_Back` (UpperChest, fallback Chest = character-style `spine_03`), `Socket_HairTop` (Head).
- **Naming**: `SK_` rigged, `SM_` static, textures `T_<Asset>_BC.png` + `T_<Asset>_Mask.png` (§2), materials `MI_<Asset>` (URP Lit/toon variant).
- **Budgets** (character-style §9): body ≤ 8k tris, head ≤ 2.5k, hats/back ≤ 1.5k, face items ≤ 600. Accessories later pack into the shared 2048 cosmetics atlas.

---

## 2. Recolouring in Unity (generate for this)

The GDD loadout is: **Primary** (overalls), **Secondary** (shirt), **Accent** (boots/gloves), **Skin** (12 + 2 fantasy), from a 24-swatch palette. Unity recolours via a **mask texture**:

| Zone | Generate it as (in Tripo) | Mask channel | Runtime param |
|---|---|---|---|
| Overalls / outfit main | flat **mid grey `#9A9A9A`** | R | `_PrimaryColor` (team colour stays ≥ 60% of overalls) |
| Shirt / sleeves | flat **off-white `#E8E4DA`** | G | `_SecondaryColor` |
| Boots + gloves | flat **dark grey `#4A4A4A`** | B | `_AccentColor` |
| Skin (hands, neck, head) | flat **neutral beige `#D9B59A`** | A | `_SkinColor` |
| Fixed details (buttons, eyes, buckles) | their real colour | none (0) | not tinted |

- Shader = `albedo × lerp(1, tint, mask)` in a URP Shader Graph (technical-artist owns). Neutral greys tint cleanly; already-coloured albedo turns muddy, which is why prompts ask for grey/white.
- **Owner's job**: just keep zones flat and separated by a hard edge. The mask is painted once per body/head (5–10 min in any paint tool, or auto-split by colour in a script); you do not need to make it in Tripo.
- Overall **patterns** (Mud Season, Camo) = an overlay texture inside the R zone, not new meshes.
- Hair colour: hair generated flat mid-grey → tinted (mask R). Hats: generate in real colours (not tinted at VS).

---

## 3. Outfits — recommendation

| Outfit | Method | Why |
|---|---|---|
| Overalls (default) | **Built into base body mesh** + tint | Largest visible area; must be on every body. |
| Flannel shirt + jeans | **Texture/mask swap** on the same body (no new mesh) | Same silhouette as overalls-over-shirt; flannel check is a pattern overlay in G, jeans = R zone recoloured blue. Zero rig/clip risk. |
| Raincoat | **Swappable outer mesh** (`SK_Outfit_Raincoat_*`), skinned to the same humanoid skeleton | Changes the silhouette (hood, long hem) — that is what makes it worth an item. Hem must not clip carry pose. |

So: 1 extra mesh per body type (raincoat), everything else is material. Generate the raincoat for Sturdy first; Lanky via the same prompt.

---

## 4. Spec Index (ordered by impact)

| # | ID | Pri | Category | Tris | Tex | Rig |
|---|---|---|---|---|---|---|
| 1 | SK_Farmer_Sturdy | P0 | Bodies | 8k | 2048 | Humanoid |
| 2 | SK_Farmer_Lanky | P0 | Bodies | 8k | 2048 | Humanoid |
| 3 | SM_Head_Round | P0 | Heads | 2.5k | 1024 | (Head bone) |
| 4 | SM_Hat_StrawHat | P0 | Hats | 1.2k | 512 | — |
| 5 | SM_Hat_TruckerCap | P0 | Hats | 1k | 512 | — |
| 6 | SM_Hat_CowboyHat | P0 | Hats | 1.2k | 512 | — |
| 7 | SM_Hat_TrafficCone | P0 | Hats | 600 | 512 | — |
| 8 | SM_Hat_PumpkinHead | P0 | Hats | 1.5k | 512 | — |
| 9 | SM_Hat_BucketHelmet | P0 | Hats | 1k | 512 | — |
| 10 | SM_Hat_ChefToque | P0 | Hats | 1k | 512 | — |
| 11 | SM_Hat_FlowerCrown | P0 | Hats | 1.5k | 512 | — |
| 12 | SM_Hat_VikingHorns | P0 | Hats | 1.2k | 512 | — |
| 13 | SM_Hat_GrandpasFedora | P0 | Hats | 1.2k | 512 | — |
| 14 | SM_Face_Moustache | P0 | Face | 400 | 256 | — |
| 15 | SM_Face_Goggles | P0 | Face | 600 | 512 | — |
| 16 | SM_Face_Sunglasses | P0 | Face | 500 | 256 | — |
| 17 | SM_Back_BackpackBasket | P0 | Back | 1.5k | 512 | — |
| 18 | SK_Back_Cape | P0 | Back | 1.2k | 512 | 4-bone chain / cloth |
| 19 | SM_Head_Long | P1 | Heads | 2.5k | 1024 | (Head bone) |
| 20 | SM_Head_Freckles | P1 | Heads | 2.5k | 1024 | (Head bone) |
| 21 | SM_Head_BigNose | P1 | Heads | 2.5k | 1024 | (Head bone) |
| 22 | SM_Hair_Mop | P1 | Hair | 1k | 512 | — |
| 23 | SM_Hair_Ponytail | P1 | Hair | 1k | 512 | — |
| 24 | SM_Hair_Beard | P1 | Hair | 800 | 512 | — |
| 25 | SK_Outfit_Raincoat_Sturdy (+ _Lanky) | P1 | Outfits | 3k | 1024 | Humanoid (same skel) |
| 26 | SK_Farmer_Stout | P2 (EA) | Bodies | 8k | 2048 | Humanoid |
| 27 | SK_Farmer_Tiny | P2 (EA) | Bodies | 8k | 2048 | Humanoid |

**Bald** = no hair mesh (all heads are generated bald). **Flannel + jeans** = texture only (§3).

> **GDD flags (for game-designer / creative-director, not decided here)**: the GDD has no **Head** or **Hair** slot, and its `FFarmerLoadout` (≤ 32 bytes) has no field for them. Items 19–24 assume two u8 fields are added (`head`, `hair`); fits the budget. Outfit also needs a field (or reuse the Overalls Pattern param). Until confirmed, generate them P1 so they do not block VS.

---

## 5. Bodies (`~/Desktop/HarvestPanic Tripo Models/Bodies/`)

All bodies: **headless** (clean neck cap at collar), A-pose (arms ~45° down), feet flat, 4-finger cartoon gloved hands with fingers slightly apart (rig-friendly), overalls bib over short-sleeve shirt, rubber boots. Same height band (~1.7–1.8 m with head) so every body uses the **same collision capsule** (GDD). Pivot: ground, between feet. Neck joint aligned to Head bone so any head fits any body.

### 1. SK_Farmer_Sturdy — P0
- **Prompt**: `soft toy-like stylized cartoon 3D, smooth rounded chunky forms, vinyl-toy proportions, big simple readable silhouette, warm friendly palette, clean flat colour zones with gentle baked ambient occlusion, no micro detail, clean low-poly quad-friendly topology, single object, centered, plain white background, no base, no ground plane, headless cartoon farmer body for character rig, sturdy stocky build, wide barrel torso, short thick legs, big chunky hands with four fingers each and thick cartoon work gloves, plain flat mid-grey denim overalls with bib and two straps, plain flat off-white short-sleeve shirt, chunky flat dark-grey rubber boots, small round brass buttons, neutral A-pose arms angled down 45 degrees, legs slightly apart, clean flat neck opening, no head`
- **Negative**: shared + `head, face, hair, hat, five fingers, thin fingers, T-pose, pockets full of detail, belts, tools, plaid, pattern, muscular, realistic anatomy`
- **Tris** 8,000 · **Tex** 2048 (BC + Mask) · **Height** 1.25 m to neck · **Rig** Tripo auto-rig humanoid → Unity Humanoid avatar · **Export** FBX with skin
- **Sockets**: `Socket_Back` (UpperChest). First-person: arms + gloves + sleeve cuffs must look good up close (they are always on screen).
- **File**: `Bodies/SK_Farmer_Sturdy.fbx`

### 2. SK_Farmer_Lanky — P0
- **Prompt**: `soft toy-like stylized cartoon 3D, smooth rounded chunky forms, vinyl-toy proportions, big simple readable silhouette, warm friendly palette, clean flat colour zones with gentle baked ambient occlusion, no micro detail, clean low-poly quad-friendly topology, single object, centered, plain white background, no base, no ground plane, headless cartoon farmer body for character rig, tall lanky noodle build, narrow torso, long thin-but-rounded arms and legs, slightly hunched, big chunky hands with four fingers each and thick cartoon work gloves, plain flat mid-grey denim overalls with bib and two straps, plain flat off-white short-sleeve shirt, tall flat dark-grey rubber boots, small round brass buttons, neutral A-pose arms angled down 45 degrees, legs slightly apart, clean flat neck opening, no head`
- **Negative**: shared + `head, face, hair, hat, five fingers, skinny realistic limbs, T-pose, belts, tools, plaid, pattern`
- **Tris** 8,000 · **Tex** 2048 · **Height** 1.45 m to neck · Rig/Export/Sockets as #1
- **File**: `Bodies/SK_Farmer_Lanky.fbx`

### 26. SK_Farmer_Stout — P2 (EA)
- **Prompt**: same as #1 but replace build text with `short round pear-shaped build, big soft belly, tiny legs, short arms`
- **Negative**: as #1 · **Tris** 8k · **Tex** 2048 · Rig/Export as #1 · **File**: `Bodies/SK_Farmer_Stout.fbx`

### 27. SK_Farmer_Tiny — P2 (EA)
- **Prompt**: same as #1 but replace build text with `small kid-sized chibi build, short torso, stubby limbs, oversized hands and boots`
- **Negative**: as #1 · **Tris** 8k · **Tex** 2048 · Rig/Export as #1 · **Height note**: shorter visual, same capsule — check that hats still clear the crop line (~75 cm).
- **File**: `Bodies/SK_Farmer_Tiny.fbx`

---

## 6. Heads (`.../Heads/`)

All heads: **bald**, big expressive eyes (large whites + simple dark pupils, sclera fixed colour), simple mouth, flat beige skin (tinted), neck stub matching the body neck opening, chibi-large (~30% of total height). Face front-on, neutral friendly expression (emotes will drive brow/eye via blendshapes later if Tripo topology allows; otherwise texture swaps). Pivot: base of neck. Sockets `Socket_Head` (top of skull), `Socket_Face` (eye line), `Socket_HairTop`.
Common negative: shared + `hair, hat, ears too large, realistic face, wrinkles, teeth detail, makeup, body, shoulders`

| # | ID | Pri | Prompt (paste whole line) | File |
|---|---|---|---|---|
| 3 | SM_Head_Round | P0 | `soft toy-like stylized cartoon 3D, smooth rounded chunky forms, vinyl-toy proportions, big simple readable silhouette, warm friendly palette, clean flat colour zones with gentle baked ambient occlusion, no micro detail, clean low-poly quad-friendly topology, single object, centered, plain white background, no base, no ground plane, bald cartoon farmer head only with short neck stub, big round ball-shaped head, huge expressive round eyes with white sclera and dark pupils, small button nose, wide friendly goofy smile, small round ears, flat neutral beige skin, front view` | `Heads/SM_Head_Round.fbx` |
| 19 | SM_Head_Long | P1 | `[same prefix as above], bald cartoon farmer head only with short neck stub, tall long egg-shaped head with long chin, huge expressive droopy eyes with white sclera and dark pupils, small nose, worried crooked mouth, small round ears, flat neutral beige skin, front view` | `Heads/SM_Head_Long.fbx` |
| 20 | SM_Head_Freckles | P1 | `[same prefix], bald cartoon farmer head only with short neck stub, slightly squarish rounded head, chubby cheeks with a few large simple freckle dots, huge excited eyes with white sclera and dark pupils, gap-tooth grin, small round ears, flat neutral beige skin, front view` | `Heads/SM_Head_Freckles.fbx` |
| 21 | SM_Head_BigNose | P1 | `[same prefix], bald cartoon farmer head only with short neck stub, round head with a huge bulbous potato nose, big expressive half-lidded eyes with white sclera and dark pupils, thick simple eyebrows shapes, smug small mouth, small round ears, flat neutral beige skin, front view` | `Heads/SM_Head_BigNose.fbx` |

`[same prefix]` = the §1 style prefix (copy the full text from #3; Tripo has no macro). Tris 2.5k · Tex 1024 (BC + Mask A = skin) · Rig none (bound to body Head bone in Unity) · Export FBX.
**Freckles**: generate freckles as darker spots *outside* the skin mask (mask = 0 there) so they stay visible on every skin tone — or skip them in Tripo and add as a decal texture. Same rule for eyes/mouth.

---

## 7. Hair (`.../Hair/`) — P1

Generate on a mannequin-free plain background, sized for `SM_Head_Round` (≈ 32 cm head width). Flat mid-grey (tinted). Big clumped shapes, no strands. Pivot = head-top contact point, attaches to `Socket_HairTop`. Hats hide hair: Unity swaps to a "hat-safe" scaled version or hides hair under full-cover hats (Pumpkin, Bucket, Cone).
Negative: shared + `head, face, scalp, individual strands, wig stand, mannequin`

| # | ID | Prompt suffix (after §1 prefix) | Tris | File |
|---|---|---|---|---|
| 22 | SM_Hair_Mop | `cartoon mop-top hair piece only, big chunky rounded clumps, shaggy bowl shape covering forehead, flat mid-grey colour, hollow inside to fit a round head` | 1k | `Hair/SM_Hair_Mop.fbx` |
| 23 | SM_Hair_Ponytail | `cartoon hair piece only, smooth pulled-back hair cap with one big bouncy rounded ponytail, chunky hair tie, flat mid-grey colour, hollow inside to fit a round head` | 1k | `Hair/SM_Hair_Ponytail.fbx` |
| 24 | SM_Hair_Beard | `cartoon big bushy farmer beard only, chunky rounded cloud-like clumps, chin and jaw shape with mouth gap, flat mid-grey colour, hollow back to fit a face` | 800 | `Hair/SM_Hair_Beard.fbx` (attach `Socket_Face`, offset to chin) |

Tex 512 · Rig none · Export FBX. Ponytail may get a 2-bone spring chain in Unity (technical-artist).

---

## 8. Hats (`.../Hats/`) — all P0 (VS catalogue)

**Silhouette rule** (character-style): each hat changes head outline by **≥ 30% head height or ≥ 40% width**. Real colours (not tinted). Pivot = inner brim centre (where it meets the skull) → `Socket_Head`. Sized for a 32 cm-wide head. Rig none · Export FBX · Tex 512 (atlas later). Each hat also becomes a physics prop on pop-off: Unity box/capsule collider, so keep the shape compact and closed.
Negative for all hats: shared + `head, face, mannequin, hair, hat stand, multiple hats, thin brim edge, logo text` (+ extras in table)

Prompt = §1 prefix + `, ` + suffix.

| # | ID | Prompt suffix | Extra negative | Tris | File |
|---|---|---|---|---|---|
| 4 | SM_Hat_StrawHat | `cartoon farmer straw hat only, very wide floppy round brim, rounded crown, simple woven straw texture painted as big soft bands, honey-yellow straw, red cloth hat band, slightly wonky` | `detailed weave, holes` | 1.2k | `Hats/SM_Hat_StrawHat.fbx` |
| 5 | SM_Hat_TruckerCap | `cartoon trucker cap only, tall puffy rounded front panel, oversized curved bill, mesh back painted as simple shapes, cream front panel with a plain barn-red bill and back, small button on top` | `text, logo on front` | 1k | `Hats/SM_Hat_TruckerCap.fbx` |
| 6 | SM_Hat_CowboyHat | `cartoon cowboy hat only, tall pinched crown, wide brim curled up on both sides, warm brown felt, darker brown band with small star-shaped buckle` | `realistic leather` | 1.2k | `Hats/SM_Hat_CowboyHat.fbx` |
| 7 | SM_Hat_TrafficCone | `cartoon orange traffic cone worn as a hat only, tall chunky rounded cone, two thick white bands, hollow bottom to sit on a head, slightly squashed and dented` | `square base, sharp tip` | 600 | `Hats/SM_Hat_TrafficCone.fbx` |
| 8 | SM_Hat_PumpkinHead | `cartoon carved pumpkin worn as a full helmet only, round orange pumpkin with chunky ribs, goofy carved triangle eyes and a big toothy grin, curly green stem on top, hollow bottom opening for a neck` | `scary face, glowing, candle` | 1.5k | `Hats/SM_Hat_PumpkinHead.fbx` |
| 9 | SM_Hat_BucketHelmet | `cartoon dented metal farm bucket worn upside down as a helmet only, chunky rounded rim, big wire handle hanging like a chin strap, two eye holes punched in front, painted soft grey-blue tin with warm highlights` | `chrome, reflective, rust` | 1k | `Hats/SM_Hat_BucketHelmet.fbx` |
| 10 | SM_Hat_ChefToque | `cartoon chef toque hat only, very tall puffy white mushroom-shaped chef hat, big soft pleats, cream band at the bottom, slightly leaning` | `short hat, flat` | 1k | `Hats/SM_Hat_ChefToque.fbx` |
| 11 | SM_Hat_FlowerCrown | `cartoon flower crown only, thick ring of woven green leaves with large chunky simple daisies sunflowers and pink blossoms, flowers sticking up and out for a big silhouette` | `thin wire, tiny flowers` | 1.5k | `Hats/SM_Hat_FlowerCrown.fbx` |
| 12 | SM_Hat_VikingHorns | `cartoon viking helmet only, rounded dome metal cap painted warm grey with brass rim and rivets as big dots, two huge curved cream horns pointing up and out, chunky rounded horn tips` | `sharp spikes, realistic metal` | 1.2k | `Hats/SM_Hat_VikingHorns.fbx` |
| 13 | SM_Hat_GrandpasFedora | `cartoon old grandpa fedora hat only, pinched rounded crown, short slightly droopy brim, faded olive-brown felt, dark band with a small bright feather tucked in, a patch sewn on the side` | `slick modern fedora` | 1.2k | `Hats/SM_Hat_GrandpasFedora.fbx` |

Notes: **Pumpkin Head** and **Bucket Helmet** are full-cover (hide hair, wearer sees 2% vignette, not the mesh). **Traffic Cone** and **Chef Toque** are the 40 m silhouette heroes — keep them tall. Pumpkin Head must stay visually distinct from the sellable prize pumpkin (carved face, smaller, stem) — GDD tags it non-sellable.

---

## 9. Face Items (`.../Face/`) — P0

Pivot = bridge of nose / centre of upper lip → `Socket_Face`. Sized for `SM_Head_Round`; Unity offsets per head. Rig none · FBX. First-person: Goggles/Sunglasses also apply a lens tint (no mesh in view).
Negative: shared + `face, head, mannequin, thin wire frame, transparent glass, reflections`

| # | ID | Prompt suffix (after §1 prefix) | Tris | Tex | File |
|---|---|---|---|---|---|
| 14 | SM_Face_Moustache | `cartoon big bushy handlebar moustache only, chunky rounded curled tips, solid soft shape, warm brown` | 400 | 256 | `Face/SM_Face_Moustache.fbx` |
| 15 | SM_Face_Goggles | `cartoon chunky welding aviator goggles only, two big round lenses with thick brass rims, opaque soft teal lenses with a simple painted shine, thick brown leather strap ring` | 600 | 512 | `Face/SM_Face_Goggles.fbx` |
| 16 | SM_Face_Sunglasses | `cartoon oversized round sunglasses only, thick chunky honey-yellow plastic frames, opaque dark plum lenses with one simple white shine stroke, short stubby arms` | 500 | 256 | `Face/SM_Face_Sunglasses.fbx` |

Moustache: generate flat brown; optionally re-tint to hair colour in Unity.

---

## 10. Back Items (`.../Back/`) — P0

Pivot = contact point at upper spine → `Socket_Back`. **≤ 35 cm protrusion**, must not clip the two-hand carry pose (character-style).
Negative: shared + `person, mannequin, straps floating, thin rope`

| # | ID | Prompt suffix (after §1 prefix) | Tris | Tex | Rig | File |
|---|---|---|---|---|---|---|
| 17 | SM_Back_BackpackBasket | `cartoon woven wicker backpack basket only, chunky rounded cylinder basket with big soft weave bands, two thick leather shoulder straps, a few cartoon carrots and a leek poking out the top, warm honey and brown colours` | 1.5k | 512 | none | `Back/SM_Back_BackpackBasket.fbx` |
| 18 | SK_Back_Cape | `cartoon short superhero cape only, made from a red-and-cream checked picnic blanket painted as big simple squares, rounded hem, knotted at the neck, hanging straight down flat, thick soft cloth` | 1.2k | 512 | 4-bone spine chain added in Unity (cloth sim LOD0 ≤ 15 m, else baked — GDD) | `Back/SK_Back_Cape.fbx` |

Cape is a picnic blanket on purpose: red is not a team colour, and the joke reads instantly. If the cape needs to tint, regenerate in flat grey instead.

---

## 11. Outfits (`.../Outfits/`) — P1

### 25. SK_Outfit_Raincoat_Sturdy / _Lanky
- **Prompt**: `soft toy-like stylized cartoon 3D, smooth rounded chunky forms, vinyl-toy proportions, big simple readable silhouette, warm friendly palette, clean flat colour zones with gentle baked ambient occlusion, no micro detail, clean low-poly quad-friendly topology, single object, centered, plain white background, no base, no ground plane, headless cartoon farmer body wearing a big puffy rain slicker coat, sturdy stocky build, coat reaching mid-thigh, large toggle buttons, hood down resting on shoulders, flat mid-grey coat, chunky flat dark-grey rubber boots, thick cartoon work gloves with four fingers, neutral A-pose arms angled down 45 degrees, clean flat neck opening, no head` (Lanky: swap `sturdy stocky build` → `tall lanky noodle build`)
- **Negative**: shared + `head, face, hat, long coat to the ground, five fingers, T-pose, pattern, shiny vinyl`
- **Tris** 3k (coat only after cleanup; or keep full body 8k and use as body-swap — technical-artist picks) · **Tex** 1024 · **Rig** Tripo humanoid, then re-skinned to the base body skeleton (same bone names) · **Export** FBX with skin
- **Files**: `Outfits/SK_Outfit_Raincoat_Sturdy.fbx`, `Outfits/SK_Outfit_Raincoat_Lanky.fbx`
- Flannel + jeans: no file — a mask/pattern texture (§3).

---

## 12. Owner Checklist per Asset

- [ ] Matches pig reference: soft, rounded, warm, toy-like — no grime, no realism.
- [ ] One object, no ground, centred, correct pose (A-pose for bodies).
- [ ] Recolour zones flat and separated (bodies/heads/hair/raincoat).
- [ ] Tris at or under target after Smart LowPoly.
- [ ] Hats: silhouette rule passes (squint test on a thumbnail next to the Straw Hat).
- [ ] Saved with exact file name in the right `~/Desktop/HarvestPanic Tripo Models/<Category>/` folder. Keep the Tripo preview PNG next to it (`<FileName>.png`) for review.
- [ ] If text-to-3D drifts off style 3 times: switch to image-to-3D with `01_Pig_Hamlet.png` style or a paintover sketch.

**Generate order**: #1 → #3 → #4 (one complete farmer + default hat; test in Unity before anything else) → #2 → remaining hats → face/back → P1 → P2.
