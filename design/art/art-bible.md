# HARVEST PANIC — Art Bible

> **Status**: Draft v1 (Art Director, non-interactive pass 2026-10-11). The user approves; items marked **[DECIDE]** need sign-off.
> **Engine**: UE 5.8.3, Mac (Apple Silicon) primary target. **Base pack**: Stylized Harvest Farm Environment (Emberlight Creations), `/Game/Emberlight`.
> **Companions**: `design/art/character-style.md`, `design/art/asset-gap-list.md`.

## 1. Visual Identity Statement

**"A storybook farm that gets dented."** The world is soft, warm, hand-painted and toy-like, the Emberlight look. It feels like a farm on a picture-book cover. Everything that moves (animals, crops with legs, friends, the Landlord) is chunkier, brighter and more saturated than the world it runs through. At night, the same storybook turns into a bedtime story that went wrong: the shapes stay cute, but the light goes cold and the shadows lie.

Test for any visual decision: *Would it look funny in a 15-second vertical clip with no sound, and would a viewer instantly know what was valuable and what went wrong?*

## 2. Visual Pillars

| # | Pillar | Means | Serves game pillar |
|---|---|---|---|
| V1 | **Toybox Readability** | Big simple shapes, painted gradients, no micro-detail. The silhouette carries the meaning. | Everything Has Legs (you can spot what is fleeing) |
| V2 | **Loot Pops, World Rests** | The world uses muted, warm, mid-value colors. Goods and critters use saturated, high-contrast accents with a value step of at least 2 against the ground. | Everything Has Legs; Failure Is the Content |
| V3 | **Slapstick Physics Is Visible** | Every impact, bruise and drop leaves a cartoon mark: dust puff, feathers, stars, bruise decals. Value loss always has a visual. | Heavy Together, Blamed Together |
| V4 | **Cozy Day, Crooked Night** | The time of day moves the whole palette. Day is honey-gold, dusk is coral and violet, night is teal-black lit by lanterns. Horror comes from lighting, never from gore. | The Farm Gets Worse |

## 3. Stylization Rules (matched to Emberlight)

The Emberlight pack sets the baseline: low-to-mid poly, chunky bevels, painted albedo with baked gradient, almost no normal detail (the pack ships a flat `T_DefaultNormal`), world-aligned tiling (`MF_WorldAligned`), card-based wheat and grass (`SM_WheatCard02_Combined`, `NS_Wheat` wind), and a stylized sky BP (`BP_Embersky`).

1. **Shape language**: rounded rectangles and soft wedges. Bevels are always visible at play distance. No sharp spikes except on night creatures, where *sharpness = danger*.
2. **Proportion**: props run about 1.2x real-world scale, so tools read in first-person hands. Animals are **chibi-scaled**: heads 1.3–1.5x, legs short, bodies round (a pig is a bean with legs).
3. **Surface**: albedo carries the shading. Roughness 0.6–0.9 everywhere, metallic 0 except tool heads (0.6, roughness 0.4). No photoscanned textures in the playable space. Quixel assets must be **re-materialed** with a flat-colour or gradient master (see §3.1) before use.
4. **Texel density**: 256 px/m for environment, 512 px/m for critters and hero goods, 1024 px/m for first-person held tools. Gradient textures can go lower.
5. **Outline**: no full-screen outline post (cost, plus it fights the Emberlight painterly look). Use **rim light/fresnel on interactables only** (§6).
6. **Animation style**: snappy, with anticipation and overshoot. Squash and stretch on grab, drop and land (vertex WPO or bone scale). Crops wobble with a WPO wiggle material.

### 3.1 Master materials (owned by technical-artist; spec here)
- `M_HP_StylizedOpaque`: albedo × gradient tint, optional world-aligned (reuse `MF_WorldAligned`), roughness scalar, emissive mask, **InteractRim** param, **BruiseMask** (dynamic decal channel), **WetMud** layer (weather).
- `M_HP_Foliage`: two-sided, wind (match `NS_Wheat` timing), dither LOD fade, colour variation by world position.
- `M_HP_Critter`: as Opaque, plus a squash/stretch WPO, a **ValueGlint** sparkle mask, and **Muddy/Wet/Feathered** overlays.
- `M_HP_Reskin`: a palette-swap material driven by a 4-colour Material Parameter Collection per region, so the same Emberlight meshes re-skin for Regions 2–5.

## 4. Color System

### 4.1 Semantic colors (global, never reused for decoration)
| Meaning | Colour | Hex | Use |
|---|---|---|---|
| Money / gain | Coin gold | `#F5C542` | +$ popups, golden egg, market stall trim |
| Loss / bruise / blame | Tomato red | `#E5483B` | −$ popups, bruise decals, Blame Ledger |
| Interactable / grab | Cream-white rim | `#FFF4D6` | grab rim, reticle |
| Team / friendly | Per-player colour (§7.3) | — | nameplates, pings, overalls |
| Danger (night) | Sick lime | `#B7F04A` | creature eyes, Scarecrow glint, cursed crop glow |
| Landlord / debt | Royal plum | `#5B2A6E` | Landlord, quota UI, Sunday screens |

**Rule:** gold and red are **reserved**. Environment art may not use saturated gold or red at more than 30% saturation, so money and loss always pop. (Emberlight barns are red. Desaturate barn red to a brick `#9C4A3A` in the reskin MPC, **[DECIDE]**.) Note the bull's "red hat" trigger: red hats are in-world exceptions and are meant to draw the eye.

### 4.2 Region palettes (MPC `MPC_RegionPalette`: Ground, Foliage, Wood, Accent)
| Region / Town | Ground | Foliage | Wood | Accent | Mood |
|---|---|---|---|---|---|
| 1 Grandpa's Farm / Muddlebury | `#A88A5A` warm soil | `#7FA34A` | `#8A6040` | `#E9B949` harvest | Golden afternoon, Emberlight default |
| 2 Misty Riverlands / Ferrytown | `#6E7A62` | `#5E8C6A` mossy | `#6B5A4A` wet | `#7EC8C8` river teal | Soft overcast, mist layers |
| 3 Haunted Orchard / Hollow Hill | `#5A4A44` | `#8A6A3A` autumn | `#4A3A34` | `#E07A2E` pumpkin | Permanent late dusk, long shadows |
| 4* Sunburnt Ranch / Dustwater | `#D2A26A` | `#9AA05A` dry | `#A0704A` bleached | `#3FA7D6` sky | Hard noon, heat haze |
| 5* Frostbite Fjord / Icemouth | `#DDE6EE` snow | `#3E6A5E` pine | `#5A4A3E` | `#F08A5D` lantern | Cold blue, warm windows |

Critter and goods colours are **fixed across regions**. Only the environment shifts, which keeps the V2 contrast rule true.

### 4.3 Time of day (per region, driven by the day-loop sundown timer)
| Phase | Sun colour / intensity (lux) | Sky light | Fog | Grade |
|---|---|---|---|---|
| Morning | `#FFE2B8` / 6 | 1.0, warm | 0.01, light gold | Sat 1.05, temp 6000K |
| Midday | `#FFF3DC` / 8 | 1.0 | 0.005 | Sat 1.1, neutral |
| Dusk (last 20%) | `#FF9A6A` / 3, low angle 8° | 0.7, violet `#8A7AB8` | 0.02 coral | Sat 1.15, shadows tinted violet |
| Night | Moon `#9EC4FF` / 0.4 | 0.25, teal `#1E3A4A` | 0.035 teal-black | Sat 0.8, shadows teal, highlights left alone, so lanterns stay warm |

Night rule: **players must never be in the dark without a source.** Every player has a hat or handheld lantern pool (point light radius 6 m, `#FFB866`, 2000 cd, no shadow casting on low scalability). Darkness is for the *edges* of the screen.

## 5. Lighting and Post-Process Recipe (UE 5.8)

**Default target: "Mac Medium"**, Apple M1/M2 base, 1080p at 60 fps, with Lumen off.

| Setting | Mac Low/Med | Mac High (M-Pro/Max) |
|---|---|---|
| Global illumination | Lumen **off**; Sky Light real-time capture + DFAO off; baked-feel via Sky Light 1.0 and Directional | Lumen GI **Software**, Scene Detail 1.0, Final Gather 1.0 |
| Reflections | Screen Space | Lumen reflections low (roughness cutoff 0.4) |
| Shadows | Virtual Shadow Maps **off**; CSM 3 cascades, dist 6000 cm, far shadow off | VSM on, distance 12000 cm |
| Nanite | Off for foliage (cards + WPO); allowed on static rock/barn if ≥ 2x perf win measured | Same |
| AA | TSR at 67% screen percentage (Low), 80% (Med) | TSR 100% |

**Post-process volume (global, unbound)**
- Exposure: Manual, EV100 = 11 day / 9 dusk / 6 night (interpolated by the day-loop). No auto-exposure, because readability must not drift.
- Bloom: Standard, intensity 0.4, threshold 1.2 (lanterns and coin glints bloom; the sky does not).
- Vignette 0.3 (0.45 at night). Chromatic aberration 0. Film grain 0 (it ruins clip compression).
- Color grading: Saturation 1.1, Contrast 1.05, Shadows tint per §4.3. Tone curve: Filmic default, Toe 0.6 (lifted blacks so night stays readable).
- Motion blur **0** (first-person comedy clips must be crisp; also reduces motion sickness).
- Exponential Height Fog + Volumetric fog **only on High**; on Low/Med, the fog weather twist uses height fog density 0.08 plus a card-based mist ring.
- Directional light: Source Angle 2° (soft), cast shadows; Sky Atmosphere optional (Emberlight `BP_Embersky` preferred, cheaper).

## 6. Readability Rules (first-person co-op)

1. **Value contrast test**: grayscale the frame. Every critter and good must sit at least 2 value steps (on a 10-step scale) from what's directly behind it. Pigs are pink `#F2A3A0` against brown soil; chickens are white against green; goats are cream with dark horns.
2. **Critter pop kit**: (a) saturation +20% over environment, (b) fresnel rim `#FFF4D6` at 0.3 intensity when within 15 m and fleeing, (c) a **dust kick** VFX trail when running, so motion is readable even in tall wheat. In tall crops, the critter's head or ears must clear the crop line, or the wheat must part visibly (WPO interaction).
3. **Goods value readability**: value is told by **tier glint + size**, not by text.
   | Tier | Base $ | Visual |
   |---|---|---|
   | Cheap | < 50 | No glint |
   | Mid | 50–149 | Soft white sparkle every 3 s |
   | Prize | 150–399 | Gold sparkle every 1.5 s plus a faint gold rim |
   | Jackpot | ≥ 400 / golden egg | Constant gold particles, bloom, chime (audio-director) |
   Bruise state is shown with **bruise decals** (tomato-red/purple splats on the mesh), 1 per 20% value lost, max 4. A glint dims one tier per heavy bruise.
4. **Teammates readable at 40 m**: each player gets a **team colour** on overalls (largest area) and a hat silhouette from the cosmetics list. Hats must break the head outline by ≥ 30% height (the "hat silhouette rule", see character-style). A small nameplate shows at > 20 m only, with a colour-matched pip. Downed or ragdolled players get a cartoon swirl of stars above them that's visible through walls (team only).
5. **Night readability**: critters get a subtle emissive eye shine (`#FFF4D6`, 2 nits) at night so they stay findable. Creatures use **sick lime** eyes. Same mechanic, different colour, so players learn "white eyes = money, green eyes = run".
6. **First-person framing**: held items occupy the lower-right third max; carried big goods (pumpkin, cow) are shown from the players' hands with FOV-locked offset, and can't cover the reticle. Default FOV 90 (slider 70–110).
7. **Interactable rim**: a grab-able object gets the cream rim at 0.6 intensity when the reticle is on it. No other object may use cream-white emissive.

## 7. UI Visual Language

1. **Theme: "Grandpa's farm ledger and feed-store signage."** Paper cards, stamped ink, painted wooden signs for diegetic elements (market order board, Landlord notices).
2. **Fonts** (Google Fonts, OFL): **Lilita One** for headlines and money numbers, **Nunito** (ExtraBold for HUD, Regular for body) for text. Minimum HUD text size 22 px at 1080p; scale with the UI slider (80–150%).
3. **Colour**: paper `#F6EBD3`, ink `#2B2118`, plus semantic colours from §4.1. HUD elements use a 2 px ink outline and soft drop shadow so they read on bright wheat and on night.
4. **Motion**: numbers "bounce" (scale 1.0 → 1.25 → 1.0 over 0.2 s). Money gain floats up in gold, losses drop down in red with a small shake. The Blame Ledger uses typed-stamp entries with a rubber-stamp "THUNK".
5. **Diegetic first**: the manifest and market order are a clipboard you can raise; the sundown timer is a sun disc on the HUD's top edge that turns coral at dusk.
6. **Accessibility**: never encode by colour alone. Money gain/loss also uses ▲/▼ glyphs; team colours also pair with a shape icon (circle, square, triangle, diamond). Colourblind presets (Deuteranopia, Protanopia, Tritanopia) swap the team and danger colours; danger lime switches to magenta `#E040C8` in Deuteranopia/Protanopia.
7. **Ownership**: layout and flow belong to ux-designer (`hud-menus.md`); this section defines skin only.

## 8. VFX Language

All VFX are **cartoon-graphic**: flat-shaded sprite or mesh particles, 2–4 colour, hard-edged or stepped alpha. No realistic smoke, sparks or blood.

| Effect | Trigger | Look | Budget |
|---|---|---|---|
| `vfx_dust_puff` | landing, running critter, drop | 3–6 cream-tan round puffs, scale up and fade 0.4 s | ≤ 8 particles |
| `vfx_feathers_burst` | chicken grabbed/hit, chicken bruise | 6–12 white feather meshes, flutter down 1.5 s | ≤ 12 |
| `vfx_impact_stars` | heavy impact, ragdoll, KO | 3–5 yellow 5-point stars orbit the head 2 s, with a "pow" flash card | ≤ 6 |
| `vfx_bruise_splat` | value loss | red-purple splat decal + "−$" number | 1 decal |
| `vfx_coin_burst` | sale at market | gold coins spray and arc into the wallet | ≤ 20 |
| `vfx_mud_splash` | mud weather, falling | brown blob sprites + mud overlay on mesh | ≤ 10 |
| `vfx_water_splash` | river fall | white stylized splash ring + droplets | ≤ 16 |
| `vfx_sweat_drops` | carrying heavy, panic | 2 blue droplets pop from head | ≤ 2 |
| `vfx_curse_wisp` | cursed crop, creature | lime wisps, slow rise | ≤ 10 |
| `vfx_spit_splat` | llama spit | screen-space green splat (2 s) | UI overlay |
| `vfx_value_glint` | goods tier (§6.3) | star sparkle | ≤ 3 |

Rules: impact VFX must spawn within 1 frame of the physics event (sell the comedy); max on-screen VFX budget: 300 particles total on Mac Med, GPU sim only on High.

## 9. Asset Standards

- **Naming**: `[category]_[name]_[variant]_[size].[ext]` for source files; UE assets keep the UE prefix convention: `SM_`, `SK_`, `M_`, `MI_`, `T_`, `NS_`, `WBP_`, e.g. `SK_Critter_Pig`, `MI_Critter_Pig_Muddy`, `T_UI_Btn_Primary_Hover`.
- **Folders**: new art goes in `/Game/HarvestPanic/Art/{Characters,Critters,Goods,Environment,VFX,UI}`. Never edit `/Game/Emberlight` in place. Duplicate into `/Game/HarvestPanic/Art/Environment/EmberlightOverrides` instead.
- **Textures**: sRGB for albedo, linear for masks, BC7 for albedo, BC5 normal (only if needed), packed ORM. Max 2048 for hero, 1024 for critters, 512 for props.

## 10. Performance Budgets (Mac Medium, 1080p, 60 fps = 16.6 ms)

| Item | Budget |
|---|---|
| GPU frame | ≤ 12 ms (leave headroom for voice/net) |
| Draw calls | ≤ 2,500 visible (instance fences, crops, hay with HISM/PCG) |
| Triangles on screen | ≤ 2.5 M (non-Nanite) |
| Critter | ≤ 6k tris LOD0, 3 LODs, ≤ 50 bones, 1 material (2 max) |
| Player character | ≤ 15k tris incl. cosmetics, ≤ 3 materials |
| Hat/back cosmetic | ≤ 1.5k tris, shares a cosmetics atlas |
| Held tool (first-person) | ≤ 3k tris |
| Simultaneous active critters | 30 skeletal, URO (update rate optimisation) beyond 25 m |
| Dynamic lights | ≤ 4 shadowed, ≤ 16 unshadowed (player lanterns don't cast shadows on Med) |
| Texture pool | ≤ 1.5 GB (8 GB unified-memory Macs) |
| Particles | ≤ 300 on screen (§8) |

Scalability presets must ship as `Mac Low/Med/High` with Lumen, VSM and volumetrics only in High.

## 11. Open Decisions
1. **[DECIDE]** Desaturate the Emberlight barn red to brick (recommended) or keep it as a landmark exception.
2. **[DECIDE]** Lumen on High only (recommended) or no Lumen at all, for a single lighting path that is easier to QA.
3. **[DECIDE]** Fresnel rim on fleeing critters (recommended) vs. purely relying on saturation; the rim is cheap but slightly "gamey".
