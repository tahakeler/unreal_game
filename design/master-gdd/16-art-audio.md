# 16. Art & Audio Direction

> **Sources**: `design/art/art-bible.md`, `character-style.md`, `asset-gap-list.md`, `design/audio/sound-bible.md`, `sfx-list.md`, `music-brief.md`, `design/gdd/audio-feedback.md`, `THIRD_PARTY_LICENSES.md`. Older notes written for the archived Unreal build are restated here for **Unity 6.3 LTS + URP**.

---

## 16.1 Visual Identity

**"A storybook farm that gets dented."** The world is soft, warm and toy-like. Everything that moves (animals, crops with legs, friends, the Landlord) is chunkier, brighter and more saturated than the world it runs through. At night the same storybook becomes a bedtime story gone wrong: shapes stay cute, the light goes cold.

Test for any visual decision: *Would it look funny in a 15-second silent vertical clip, and would a viewer instantly see what was valuable and what went wrong?*

| Visual pillar | Means |
|---|---|
| **Toybox Readability** | Big simple shapes, flat or gradient colour, no micro-detail. Silhouette carries meaning. |
| **Loot Pops, World Rests** | World in muted warm mid-values; goods and critters saturated, at least 2 value steps from the ground. |
| **Slapstick Physics Is Visible** | Every impact, bruise and drop leaves a cartoon mark (dust, feathers, stars, splats). |
| **Cozy Day, Crooked Night** | Honey-gold day, coral-violet dusk, teal-black night lit by lanterns. Horror from lighting, never gore. |

**Stylisation rules**: rounded shapes with visible bevels; sharp spikes only on night creatures (sharpness = danger); props ~1.2× real scale so tools read in hand; chibi animals (heads 1.3–1.5×, short legs; "a pig is a bean with legs"); flat low-poly colour or a single painted basecolor, matte surfaces; no photoscanned textures; no full-screen outline (rim highlight on interactables only); snappy animation with squash and stretch.

Status in build: **Partial** — the Muddlebury farm uses the low-poly CC0 look; squash/stretch and critter pop treatments are planned.

---

## 16.2 Colour System

### Reserved semantic colours (never decorative)

| Meaning | Colour | Hex |
|---|---|---|
| Money / gain | Coin gold | `#F5C542` |
| Loss / bruise / blame | Tomato red | `#E5483B` |
| Interactable / grab rim, reticle | Cream-white | `#FFF4D6` |
| Danger (night) | Sick lime (magenta `#E040C8` in red-green colourblind modes) | `#B7F04A` |
| Landlord / debt | Royal plum | `#5B2A6E` |
| Team | Denim `#3B6FD1`, Pumpkin `#F08A24`, Clover `#3FAE5A`, Lilac `#A77BE0` + shapes | — |

Environment art may not use gold or red above ~30% saturation, so money and loss always pop. Barn red becomes brick `#9C4A3A` (recommended, awaiting sign-off). Red is never a team colour. The deliberate exception is the Red Cowboy Hat, which really triggers the bull.

### Region palettes (environment only; critters and goods keep fixed colours)

| Region | Ground | Foliage | Accent | Mood |
|---|---|---|---|---|
| 1 Grandpa's Farm / Muddlebury | `#A88A5A` | `#7FA34A` | `#E9B949` | Golden afternoon |
| 2 Misty Riverlands | `#6E7A62` | `#5E8C6A` | `#7EC8C8` | Soft overcast, mist |
| 3 Haunted Orchard | `#5A4A44` | `#8A6A3A` | `#E07A2E` | Permanent late dusk |
| 4* Sunburnt Ranch | `#D2A26A` | `#9AA05A` | `#3FA7D6` | Hard noon, heat haze |
| 5* Frostbite Fjord | `#DDE6EE` | `#3E6A5E` | `#F08A5D` | Cold blue, warm windows |

\* Expansion ideas. Regions re-skin the same kits through palette materials.

Status in build: **Partial** — Region 1 palette materials and warmed atlas copies exist; other regions planned.

---

## 16.3 Characters, Critters and Props

- **Farmer**: CC0 Quaternius farmer; colour zones; hat silhouette rule; face expressions planned as swaps ("X_X" on KO, "O_O" when scared). Details in Chapter 13.
- **Critters**: chibi, saturated, readable in tall crops (ears clear the crop line or the wheat parts). White eye-shine at night for animals vs **lime eyes** for creatures: "white eyes = money, green eyes = run". Fleeing critters get a dust trail and (recommended) a cream rim within 15 m.
- **Goods value by glint + size, not text**:

| Tier | Base value | Look |
|---|---|---|
| Cheap | < $50 | No glint |
| Mid | $50–149 | Soft white sparkle every 3 s |
| Prize | $150–399 | Gold sparkle every 1.5 s + faint gold rim |
| Jackpot | ≥ $400 / golden egg | Constant gold particles + chime |

Bruises show as red-purple splats, one per 20% value lost (max 4); heavy bruises dim the glint one tier.

- **First-person framing**: held items in the lower-right third, never covering the reticle; FOV 90 default.

Status in build: **Partial** — tuned first-person arms and held-item placement, Tripo pig/chicken/pumpkin in game; glint tiers, bruise decals and night eyes planned.

---

## 16.4 Lighting per Phase and Weather (URP)

| Phase | Sun | Ambient | Fog | Grade |
|---|---|---|---|---|
| Morning | `#FFE2B8`, warm, low | warm | light gold | sat 1.05 |
| Workday | `#FFF3DC`, high | neutral | minimal | sat 1.1 |
| Dusk (last 20%) | `#FF9A6A`, 8° low | violet `#8A7AB8` | coral | sat 1.15, violet shadows |
| Night | moon `#9EC4FF`, dim | teal `#1E3A4A` | teal-black | sat 0.8, lifted blacks |

**Night rule**: no player is ever in the dark without a light. Each farmer carries a warm lantern pool (6 m, `#FFB866`, no shadows on lower quality). Darkness lives at the screen edges.

**Weather**: fog (height fog + card mist ring, reduces noise radius), storm (darker grade, lightning flashes capped at 3 per second with flash-reduction option), mud day (wet/mud overlay on materials), drizzle/flood/blood moon (later).

**URP recipe**: one directional sun animated by the day clock; baked or mixed lighting for static scenery; a global post-processing Volume with fixed exposure (no auto-exposure, so readability doesn't drift), bloom 0.4 above threshold (lanterns and coins bloom, the sky doesn't), vignette 0.3 (0.45 night), **no motion blur, no film grain, no chromatic aberration** (clips stay crisp, less motion sickness). Target: base Apple Silicon Mac, 1080p, 60 fps, GPU ≤ 12 ms.

Status in build: **Partial** — Morning → Workday → Dusk progression runs in the Muddlebury playtest; weather and night grading planned.

---

## 16.5 VFX Language

Cartoon-graphic only: flat-shaded sprite or mesh particles, 2–4 colours, hard edges. No realistic smoke, sparks or blood. Impact VFX must appear within 1 frame of the physics event. Budget ≤ 300 particles on screen.

| Effect | Trigger |
|---|---|
| Dust puff | landing, running critter, drop |
| Feathers burst | chicken grabbed/hit |
| Impact stars + "pow" card | heavy impact, ragdoll, KO |
| Bruise splat + "−$" | value loss |
| Coin burst into wallet | market sale |
| Mud / water splash | mud, river fall |
| Sweat drops | heavy carry, panic |
| Curse wisps (lime) | cursed crops, creatures |
| Llama spit splat | screen overlay, 2 s |
| Value glint | goods tier |
| Hay poof | loadout change (built) |

Status in build: **Partial** (hay poof only).

---

## 16.6 Asset Sources

| Source | Licence | Used for |
|---|---|---|
| **Kenney** Nature Kit, Fantasy Town Kit, Food Kit, Survival Kit | CC0 | Muddlebury environment, produce, tools |
| **Quaternius** | CC0 | Farmer (animated), pig, chicken, cow, bull, sheep, alpaca, pigeon, pumpkin, crate, egg, bucket, turnip, carrot, market stalls |
| **Kay Lousberg** | CC0 | Fence/pen gate |
| **Owner-made Tripo3D models** | Owner's Tripo plan (commercial rights to confirm) | Hero props: Pig "Hamlet", Chicken, Prize Pumpkin, Egg Basket, Feed Bucket, Net, Market Stall, Pen Gate — decimated to 2k–6k tris, 1–2k basecolor |
| **In-house** | CC0 | Net generator, primitive cosmetics, authored Dance/Jump clips |
| **Fonts** | SIL OFL | Lilita One (headlines, money), Nunito Regular/ExtraBold (text) |

Rules: third-party files live unmodified under `Assets/ThirdParty/<Pack>/`; project materials and edited copies live under `Assets/Art/`. Only CC0 or CC-BY (with attribution) may be added, and every addition is logged in `THIRD_PARTY_LICENSES.md`. Budgets: critter ≤ 6k tris, player ≤ 15k with cosmetics, cosmetic ≤ 1.5k, held tool ≤ 3k.

**UI skin**: "Grandpa's farm ledger and feed-store signage": paper `#F6EBD3`, ink `#2B2118`, 2 px ink outline, stamped entries with a rubber-stamp "THUNK", numbers bounce (1.0 → 1.25 → 1.0 in 0.2 s).

Status in build: **Built** for the listed packs and fonts; Tripo rights confirmation **open**.

---

## 16.7 Sound Identity

**"A Saturday-morning cartoon recorded on a real farm, with the lights going out at the end."** Real, close farm Foley (mud, wood, straw, animals) cut with cartoon timing (slide whistles, boings, trombone wah-wahs). At night the cartoon layer drops away and real sounds get quiet and close.

Rules: every important event has a sound you can identify with eyes closed in < 150 ms; let silence land (the 0.6 s disaster cut is the funniest sound in the game); randomise pitch (±1.5 st, ±3 st critters) and never repeat the same variant twice; **no** laugh tracks, announcer quips, meme sounds or real jump-scare screams; nothing carried by music alone (every cue also has a caption or visual).

Status in build: **Planned** (no shipped audio yet).

---

## 16.8 Music per Phase

"The Porch Band": banjo, fiddle, upright bass, washboard, jug, harmonica, guitar, accordion, tuba (Landlord), pump organ (night). All cues are stemmed loops.

| Phase | Cue | Character | Tempo / key |
|---|---|---|---|
| Morning (30 s) | Porch | Lazy solo banjo + harmonica, rooster on the downbeat | 92 BPM, G |
| Workday | Roundup L1–L3 | Bluegrass; layers added with chaos (fiddle, jug, second banjo, stomps) | 132 BPM, D |
| Last 60 s | Crunch | Same tune +15%, fiddle lead, pocket-watch tick; key lift at the final 30 s bell | 152 BPM |
| Dusk / Night Threat | Dusk | Sparse pump organ, bowed saw, drone; heartbeat + tremolo when a creature is within 30 m | 70 BPM, D Dorian |
| Payout / Shop | Market | Accordion waltz; receipt printer locked to tempo (Shop: porch-only stems, −6 dB) | 108 BPM 3/4, C |
| Sunday | Landlord | Cheerful oompah, slides to minor if the crew is short | 100 BPM, B♭ |

**Chaos level** (fleeing critters, active twists, recent disasters) picks layer 1–3; layers rise on the next bar and fall only after 8 s below threshold. **Disaster cut**: music stops for 0.6 s (record scratch at severity ≥ 80), a verb stinger plays (river: slide whistle + splash; plank: trombone wah-wah; wagon: cymbal crash; other: sad tuba), then music returns one layer lower; the first time each day the banjo player clears his throat.

Unity implementation: stems started together with `AudioSource.PlayScheduled` on the DSP clock for bar-accurate changes; music is local to each client, driven by the replicated day clock.

Status in build: **Planned**. Sourcing (commissioned vs CC0/royalty-free + stem edits) is open.

---

## 16.9 Key SFX (VS priority list)

| Group | P0 sounds |
|---|---|
| Player | Footsteps (grass, dirt, mud, wood), KO bonk + birdies, drowned "glub" |
| Carry | Grab light/heavy/critter, drop, throw |
| Impacts & bruises | Wood/soft/dirt impacts scaled by mass and speed; crunch layer above $10 lost, cash-register clunk above $40, coin ticks = $lost ÷ 10 (max 5); item break, egg crack |
| Pig / chicken | Pig alert + squeal; chicken flock swarm (one emitter), alert, panic |
| Bridge & river | Plank creak (pitch drops with load), crack, break; splashes; river bed |
| Market | Cha-ching by value tier, sundown bell, 30 s warning bell |
| Ledger & payout | Disaster stingers, record scratch, receipt printer, headline snare + gavel, order line/complete hits |
| UI | Confirm (wood block + cowbell), pocket-watch tick, shout emote, twist-card flip |

~120 VS events, ≤ 60 MB. Sources: freesound CC0, Sonniss GDC bundles, in-house Foley recordings, synthesis. Naming `SFX_<Category>_<Subject>_<Action>`; every bark and stinger gets a caption id.

Status in build: **Planned**.

---

## 16.10 Mixing Rules

Built around **four people yelling on voice chat**.

| Priority | Bus | Level vs voice |
|---|---|---|
| 1 | Voice (never ducked) | 0 dB |
| 2 | Bull charge wind-up (0.8 s, mandatory), creature-close warnings | −2 dB, no occlusion |
| 3 | UI / stingers | −4 dB |
| 4 | Impacts & bruises | −6 dB |
| 5 | Critter barks | −8 dB |
| 6 | Player Foley | −12 dB |
| 7 | Music | −14 dB |
| 8 | Ambience | −18 dB |

- Anyone speaking within 15 m ducks Music −6 dB, Critters −3 dB, Ambience −3 dB (80 ms attack / 600 ms release). Stingers duck music −9 dB. In voice-off lobbies, shout emotes drive the ducking.
- SFX keep out of the 300 Hz–3 kHz speech band; music stems carry a small 1–3 kHz pocket when dense.
- Limits: 64 voices, 12 impacts, 8 footsteps, 6 barks/s (2 s per critter), 1 stinger at a time (extra disasters within 3 s get a small sweetener).
- Noise-Event sounds get a short echo tail so players learn "loud = consequences".
- Whole-game loudness −16 LUFS, peaks ≤ −1 dBTP. Audio CPU ≤ 1.5 ms on a base M1.
- Unity: Audio Mixer groups (Voice, Critters, Impacts, Player, Ambience, Music, UI/Stingers) with snapshots/exposed parameters for ducking and the user sliders; barn reverb zone.

Status in build: **Planned** — Master/Music/SFX/Voice volume settings exist and are saved.
