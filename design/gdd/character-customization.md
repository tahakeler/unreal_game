# Character Customization & Emotes

> **Status**: Draft v0.1 · **Created**: 2026-10-11 · **Slug**: `character-customization` · **Priority**: VS (slots, 10 hats, 4 emotes) / Alpha (wardrobe, unlocks) / EA (full catalogue)
> **Depends on**: player-character, session-networking, meta-progression, save-persistence · **Consumers**: frontend-lobby, social-comedy-features, camcorder-replay, hud-menus
> **Camera**: the game is **first-person**. You see your hands and the hat brim; everyone else sees your full body. Cosmetics are mainly for *other people*, so mirrors, the Crew Photo and replays are how you see yourself.

## Overview
Each player has one **Farmer Loadout**: a body type, 3 colours, a skin tone, and one item each in Hat, Face and Back slots, plus a name-tag colour and 4 equipped emotes. Loadouts are stored in the account profile, sent to the server when you join, and replicated to everyone as a compact struct. Cosmetics are unlocked with Farm Memories (Grandpa's Trunk), achievements (Grandpa's Letters) and region clears. They never affect gameplay. The exception is that they get physics comedy: hats fly off when you ragdoll.

## Player Fantasy
"My friends will recognise my idiot from across the field." Target aesthetics: **Expression** (primary), **Fellowship** (matching crew outfits, laughing at the Traffic Cone), and **Sensation** (your hat bouncing off when you faint). SDT: Autonomy (self-presentation) and Relatedness (crew identity). Pillar: **Failure Is the Content**, because a well-dressed disaster makes a better clip.

## Detailed Design

### Loadout slots
| Slot | Options (VS → EA) | Notes |
|---|---|---|
| Body type | 2 at VS (A "Sturdy", B "Lanky"), 4 at EA | Same collision capsule for all (no gameplay difference) |
| Colours | Primary (overalls), Secondary (shirt), Accent (boots/gloves) | Pick from a 24-swatch palette; there is no free RGB picker, so colours stay readable on the farm |
| Skin tone | 12 swatches + 2 "fantasy" (green, blue) unlocked by Letters | All base tones are free from the start, always |
| Hat | ≥10 at VS, 40 at EA | See catalogue |
| Face | None, Moustache, Goggles, Sunglasses at VS; 15 at EA | Visible to others; first-person view gets a subtle lens tint for Goggles/Sunglasses (can be turned off) |
| Back | None, Backpack Basket, Cape at VS; 12 at EA | Cape uses cloth sim only on LOD0 within 15 m, otherwise baked |
| Name tag colour | 8 colours, all free | Also the player's colour in the Blame Ledger and on the HUD. Duplicates are resolved by join order (see Edge Cases) |
| Emotes | 4 equipped (keys **5–8**), from 4 free + 10 unlockable at EA | |

### VS hat catalogue (matches the implementation's provisional spec)
Straw Hat (default), Trucker Cap, Bucket Helmet, Pumpkin Head, Cowboy Hat, Chef Toque, Flower Crown, Traffic Cone, Viking Horns, plus **Grandpa's Fedora** (10th, unlocked by finishing the first day). Pumpkin Head and Bucket Helmet block the first-person brim view, so they render a 2% vignette for the wearer instead of the mesh.

### Unlock sources
| Source | Grants | Example |
|---|---|---|
| Starter (free) | Straw Hat, Trucker Cap, Cowboy Hat, all colours/skins/tags, None face/back, emotes Wave/Point/Dance/Faint | |
| Grandpa's Trunk (Farm Memories) | Common 60 / Rare 120 / Legendary 240 FM (from meta-progression) | Chef Toque (C), Flower Crown (C), Moustache (C), Goggles (C), Sunglasses (C), Backpack Basket (R), Viking Horns (R), Cape (R) |
| Grandpa's Letters (achievements) | Fixed cosmetics, not for sale | Bucket Helmet: "Fall in the river 10 times"; Traffic Cone: "Get blamed for $1,000 in one day"; Grandpa's Fedora: finish Day 1 |
| Region clear | 1 themed hat + 1 back + 1 emote per region | Pumpkin Head: clear Region 1 |
| Debug/VS build flag | `bUnlockAllCosmetics` (default **true** in VS builds) | So the VS can show everything off |

Trunk stock: 6 items rotate each in-game week (with seeded RNG per account). Duplicates are never offered. Once all items in a rarity are owned, that rarity's slots show owned items as "Owned" and cannot be bought.

### Wardrobe (barn)
- A physical wardrobe stands inside the barn (the hub between days) and another stands in the lobby map. Interact (E) to open it.
- Opening it shows a **3rd-person preview**: the camera pulls out to 2.5 m in front of your farmer and orbits with the mouse. Zoom ranges from 1.2 to 3.5 m. **Randomize** and **Revert** buttons are included.
- The world keeps running while you're in the wardrobe. Your body stands still and can still be grabbed and knocked over. If you are ragdolled, the wardrobe closes (comedy beat: friends throw you out of the wardrobe).
- Changes apply on **Confirm**. They replicate immediately, and other players see a 0.3 s "poof" of hay.
- Wardrobe access is blocked during the active day timer, except in the lobby. This keeps cosmetics from becoming a way to hide.

### Emotes
| Key | Emote | Duration | Notes |
|---|---|---|---|
| 5 | Wave | 1.5 s | Also the "non-voice shout": emits a Noise Event (loudness 0.3, radius 10 m) for the accessibility fallback |
| 6 | Point | 1.2 s, hold to sustain | Places a 3 s world ping at the aimed location (≤60 m), visible to all players |
| 7 | Dance | Loops until you move | Upper body only while walking is allowed (comedy) |
| 8 | Faint | Ragdoll for 2.0 s, then get up | Uses the player-character ragdoll; you **drop** carried items (bruise rules apply), so it can't be used to dodge blame. Cooldown 5 s |

Unlockable emotes at EA (10): Shrug, Facepalm, Chicken Dance, Salute, Cry, Flex, Slow Clap, Pig Squeal, Grandpa Shake-Fist, Sit. Emotes use the upper-body slot when your hands are full, except Faint and Dance, which are blocked while carrying with 2 hands. The emote wheel (hold **G**) lets you pick from all owned emotes without equipping them.

### Replication & persistence
- `FFarmerLoadout` is ≤ **32 bytes**: body u8, 3 colours u8 (palette index), skin u8, hat u16, face u16, back u16, tag u8, emotes 4×u16.
- The client sends the loadout in its join RPC. The server **validates ownership** against the profile (see Edge Cases) and replicates it on the PlayerState with `COND_None`, `OnRep` → rebuild meshes.
- Cosmetic meshes are soft references, loaded asynchronously. Until the load finishes, the default Straw Hat is shown (max 1 frame of pop allowed in the lobby, because everything is preloaded there).
- Persistence: stored in the account profile (save-persistence, not the run save). Unlocks are never wiped by a run fail. The last-used loadout is restored on launch.
- Emote playback: client → server RPC (rate-limited to 2/s) → multicast unreliable. Faint is reliable, because it changes physics state.

### Anti-abuse: names
- The display name defaults to the Steam persona name. An optional **Farm Name** (3–16 chars, Latin + extended Latin + digits + space/-/_) overrides it in game.
- Farm Names are filtered by a profanity list (local, data-driven `DT_NameFilter`) with leetspeak normalisation. A failed name falls back to "Farmhand #N".
- Each player can **hide all other players' custom names** (Settings → Social → "Show Steam names only" / "Show generic names"). Streamer Mode forces "Farmer 1–4".
- The host can **kick** a player (lobby or pause). Kicking also resets that player's name tag for the session.

### Accessibility
- Name tag colours have redundant **shape icons** (circle, square, triangle, diamond…) for colour-blind players.
- Palette swatches have text names and a high-contrast outline option.
- Lens tint on Goggles/Sunglasses is off when the "Reduce visual effects" setting is on.
- Emotes are rebindable and available from the wheel with a single hold.

## Formulas
- `TrunkPrice(rarity) = {Common: 60, Rare: 120, Legendary: 240}` FM (owned by meta-progression).
- Expected days to buy a Common: FM/day ≈ 10 + 2·D + 5·Q. With D = 3 disasters and quota met (Q = 1), that is 21 FM/day, so a Common takes about **3 days** and a Legendary about **11.4 days** (roughly 2 weeks of play). Target: 1 new cosmetic every 1–2 sessions.
- Full Trunk catalogue (about 90 purchasable of 120; the rest are Letters or region clears) at a mean price of ~110 FM is ≈ 9,900 FM, or ≈ 470 days at 21 FM/day (about 60–90 h). This works as a long-tail Achiever goal, not a requirement.
- Hat pop-off on ragdoll: `P(pop) = clamp((ImpactSpeed − 4 m/s) / 6, 0, 1)`. At 7 m/s the chance is 50%. A popped hat is a physics prop for 20 s or until you pick it up, then it re-equips.

## Edge Cases
- **Unowned item in loadout** (modded save): the server replaces it with the slot default and logs it. There is no kick.
- **Duplicate name tag colour**: the later joiner gets the next free colour. Their choice is restored in the next session.
- **Late joiner**: they receive all PlayerState loadouts on initial replication. Meshes load asynchronously.
- **Hat knocked off into the river**: it respawns on the head after 20 s. A hat is never lost.
- **Someone else grabs your popped hat**: they can wear it for 30 s ("Hat Theft", see social-comedy-features), then it returns to you.
- **Faint spam to grief carriers**: Faint only affects yourself. You can't cause others to drop items. Cooldown is 5 s.
- **Pumpkin Head + a real pumpkin** in the market: the hat has the `Cosmetic` tag and is never sellable or grabbable as goods.
- **All 4 players in identical outfits**: allowed. Name tags and shapes keep them distinguishable.

## Dependencies
| System | Direction | Contract |
|---|---|---|
| player-character | uses | Skeletal mesh sockets (head, face, back), ragdoll events, first-person arms mesh coloured by Accent/skin |
| meta-progression | uses | FM balance, Trunk purchase API, Letter unlock events |
| save-persistence | uses | Account profile: owned IDs bitset, last loadout |
| session-networking | uses | Join RPC payload, PlayerState replication |
| proximity-voice | provides | Wave emits a Noise Event (non-voice fallback) |
| frontend-lobby | provides | Lobby wardrobe + mirror rendering |
| social-comedy-features | provides | Hat pop-off props, emote hooks, Crew Photo poses |
| hud-menus | provides | Name tag colour + shape per player |

## Tuning Knobs
| Knob | Default | Range | Category | Rationale |
|---|---|---|---|---|
| `HatPopMinSpeed` | 4 m/s | 2–8 | Feel | Normal stumbles keep the hat on; real falls send it flying |
| `HatPopSpeedRange` | 6 m/s | 3–10 | Feel | |
| `HatRespawnTime` | 20 s | 10–60 | Feel | Long enough to chase, short enough not to matter |
| `FaintDuration` / `FaintCooldown` | 2.0 s / 5 s | 1–4 / 2–15 | Feel | |
| `EmoteRateLimit` | 2/s | 1–5 | Gate | Bandwidth/spam |
| `TrunkRotationSize` | 6 | 4–10 | Gate | |
| `WardrobePreviewDistance` | 2.5 m | 1.2–3.5 | Feel | |
| `bUnlockAllCosmetics` | true (VS), false (ship) | bool | Gate | |
All knobs live in `assets/data/` (`DA_CustomizationTuning`, `DT_Cosmetics`).

## Comedy beats
- Your hat pops off as you faceplant and rolls into the river. Your friend wears it home.
- Faint emote mid-bridge while carrying a pig: the pig escapes, and the Ledger blames you.
- The whole crew in Traffic Cones gets mistaken for road works by... nobody, but the Crew Photo is great.
- Friends shove you out of the wardrobe while you're choosing a moustache.

## Acceptance Criteria
**Functional**
1. All 8 slot types plus 4 emote bindings can be set in the wardrobe and are visible to all 4 clients within 0.5 s of Confirm.
2. The loadout persists across game restarts and run fails.
3. An unowned item injected client-side is replaced by the default on all clients.
4. Keys 5–8 trigger Wave/Point/Dance/Faint by default. Faint drops carried items.
5. The ≥10 VS hats, 3 face and 2 back items listed above all exist and attach without clipping on both body types.
6. Bad Farm Names are replaced by "Farmhand #N". Streamer Mode shows "Farmer 1–4".
7. The loadout payload is ≤ 32 bytes.

**Experiential (playtest)**
- In 4-player sessions, ≥ 75% of testers can name which friend is which from 20 m without name tags visible.
- ≥ 1 hat pop-off per player per day on average, and testers report it as funny, not annoying.
- ≥ 50% of testers change their loadout before their first day.

## Delta vs implementation
| Provisional spec | This GDD | Action |
|---|---|---|
| ≥10 hats (9 named) | Adds **Grandpa's Fedora** as the 10th named hat | Add 1 asset, or use any placeholder as the 10th |
| Face: Moustache, Goggles, Sunglasses | Same, plus explicit "None" | Compatible |
| Back: Backpack Basket, Cape | Same, plus "None" | Compatible |
| Emotes on keys 5–8 | Wave/Point/Dance/Faint in that order; Faint drops carried items; Wave emits a Noise Event | Confirm order and drop rule |
| (unspecified) colours | 24-swatch palette indices, not free RGB | Change if implemented as RGB |
| (unspecified) | Server-side ownership validation, `bUnlockAllCosmetics` true in VS | Add flag |
