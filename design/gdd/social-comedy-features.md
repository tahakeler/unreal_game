# Social Comedy Features

> **Status**: Draft v0.1 · **Created**: 2026-10-11 · **Slug**: `social-comedy-features` · **Priority**: mixed (tagged per feature)
> **Depends on**: player-character, physics-carry, proximity-voice, blame-ledger, camcorder-replay, character-customization, shared-wallet-economy · **Camera**: first-person; others see your full body.

## Overview
This is a bundle of small, cheap features whose job is to produce laughs and clips between the big systems. They are ranked by **fun-per-effort** (F/E). Each feature has strict **bounded harm**: no feature here can cost the crew more than a small, capped amount of money, items or time, and every prank can be refused or escaped.

## Player Fantasy
"The stuff we talk about afterwards isn't the quota, it's when Sam became a chicken and pecked the Landlord." Aesthetics: **Fellowship** (primary), **Sensation** and **Expression**. Pillar: **Failure Is the Content**, plus **Heavy Together, Blamed Together**. SDT Relatedness. Supports the target of a laugh moment roughly every 3 minutes for streamers.

## Detailed Design

### Priority table (F/E = fun 1–5 ÷ effort 1–5)
| # | Feature | Fun | Effort | F/E | Tier |
|---|---|---|---|---|---|
| 1 | Carrying & throwing friends | 5 | 1 | 5.0 | **VS** |
| 2 | Ragdoll slapstick (slap, trip, hat pop) | 5 | 2 | 2.5 | **VS** |
| 3 | Blame nicknames ("Butterfingers") | 4 | 1 | 4.0 | **VS** |
| 4 | Ghost Chicken spectator | 5 | 2 | 2.5 | **Alpha** |
| 5 | Crew Photo at day end | 4 | 2 | 2.0 | **Alpha** |
| 6 | Helium balloon voice pitch | 4 | 2 | 2.0 | **Alpha** |
| 7 | Megaphone | 3 | 2 | 1.5 | **Alpha** |
| 8 | In-world chalkboard | 3 | 2 | 1.5 | **Alpha** |
| 9 | Bounded pranks | 4 | 3 | 1.3 | **EA** |
| 10 | Highlight reel (end of week) | 4 | 3 | 1.3 | **EA** |

### 1. Carrying & throwing friends (VS)
- A player is a 2-hand carry item of **80 kg** in physics-carry. A carried player can **struggle free** by mashing Jump: 6 presses within 2 s (or holding Jump for 3 s in accessibility mode).
- A thrown player ragdolls on landing. Throw speed is at most 9 m/s, so it can't launch you across the river.
- A carried player can still use their own hands, so they can carry a chicken while being carried (a "totem").
- Dropping a friend in the river produces a Blame Event `verb=Dunked` with $0 cost.

### 2. Ragdoll slapstick (VS)
- **Slap** (melee key, F): 0.4 s windup, 1.5 m reach, applies a 250 N·s impulse. A target at < 30% stamina ragdolls. Cooldown 1.5 s. It can't knock items out of hands (to bound harm), but a ragdolled carrier drops them under the usual rules.
- Hats pop off (character-customization). Ragdoll bodies are grabbable.
- Mud or bananas: handled by bridge-hazards. Here we only guarantee ragdoll is always ≤ 3 s before you can get up.

### 3. Blame nicknames (VS)
- At payout, each player gets a nickname from their top Ledger verb that day: Dropped → "Butterfingers", Dunked → "Lifeguard", Bruised → "Tenderizer", Lost critter → "Gatekeeper", Broke plank → "Structural Engineer", No blame → "Suspiciously Innocent". There are 3 variants per verb in `DT_Nicknames`.
- The nickname shows under the name tag the next day (opt-out in Social settings). Only the latest nickname is shown.

### 4. Ghost Chicken spectator (Alpha)
- When knocked out (player-character), you become a **translucent ghost chicken** until respawn. Its speed is 5 m/s, it can flap-hover at 1.5 m, and it can't carry anything.
- Abilities: **Cluck** (Noise Event loudness 0.4, radius 12 m, cooldown 4 s; it can lure or scare critters, so the knocked-out player is still useful) and **Peck** (on a living player, makes them flinch, no damage, cooldown 3 s).
- Live players see it. Proximity voice keeps working, but the chicken's voice is pitch-shifted +3 semitones.
- If you still have your spectator camera, you can toggle to free-follow a teammate.

### 5. Crew Photo (Alpha)
- At day end, before the payout screen, all players are teleported to the market steps with a **5 s timer**. They can pose: emote, hold an item, stand on others. A flash then freezes the frame. The camera is a 3rd-person rig at a fixed spot.
- Polaroid overlay: day number, $ earned, MVD (most valuable disaster) caption from the Ledger, each player's nickname.
- Saved locally as PNG (1920×1080) in `Saved/CrewPhotos/`. The Polaroids pin to the barn wall (the last 12 are shown).

### 6. Helium balloons (Alpha)
- A market item costing **$15** from the shared wallet, purchasable once per day per player. Holding it in a hand gives your voice a pitch shift of **+7 semitones** for 60 s, and you float (gravity × 0.8).
- Popping it (by thorns, crows, or a friend's slap) ends the effect with a squeak.
- Fallback for players without a mic: your emotes play squeaky SFX.

### 7. Megaphone (Alpha)
- A tool-slot item (farm-upgrades, $40, one per farm). Voice radius goes ×2.5 and loudness +6 dB, with a bullhorn filter. It also emits a Noise Event of loudness **0.9** while you talk, which scares critters. This is a double-edged sword. Megaphone voice filters are cosmetics in meta-progression.

### 8. Chalkboard (Alpha)
- One in the barn and one in the lobby. Players draw with a mouse (one colour per player, 3 widths, eraser). Up to 4 strokes at a time.
- Stroke data is ≤ 64 KB per board, replicated reliably. The board persists in the run save (barn board only). The barn board auto-writes the week's quota and the Ledger's "Blame Champion" in the corner.
- Moderation: any player can "Wipe Board" (host only in public lobbies). The "Hide chalkboard drawings" setting shows only the auto text.

### 9. Bounded pranks (EA)
| Prank | Effect | Cap |
|---|---|---|
| Tie shoelaces (sneak up behind a friend, hold E 2 s) | Their next sprint makes them trip | 1 per target per day |
| Whoopee cushion on the wagon seat | Sound only | Unlimited, sound only |
| Bucket on head (place a feed bucket) | Blinds the wearer for 3 s, or until they press E | Cooldown 30 s per target |
| Swap the pig for a scarecrow in the wagon | The swap is revealed at the market. The pig is still in the barn and isn't lost | 1 per day; never affects quota |
**Harm rule**: no prank can directly destroy goods, spend wallet money, or cost more than 5 s of a player's control. Pranks produce `verb=Prank` blame events worth $0 and do appear in the Ledger.

### 10. Highlight reel (EA)
On Landlord Sunday, the game plays the top 5 tapes of the week (ranked by the camcorder's disaster score) as a 60–90 s montage with Grandpa's narration cards. Players can skip by majority vote. Export reuses the camcorder MP4 pipeline.

## Formulas
- `F/E = Fun / Effort` (designer estimates, revisited after the VS playtest).
- Struggle-free: `escape if presses_in_window ≥ 6 within 2.0 s`. With an average mash speed of 5 presses/s, escape takes ~1.2 s. This is enough for a throw, but not for a long carry.
- Slap ragdoll threshold: `ragdoll if Stamina/MaxStamina < 0.30`.
- Helium daily cost cap: `$15 × players ≤ $60/day`, which is < 5% of the target Day 1 payout. Bounded.
- Laugh cadence target: ≥ 1 social-comedy trigger (throw, slap ragdoll, chicken peck, prank) per player every 3 minutes, measured by telemetry.

## Edge Cases
- Throwing a friend into the market zone: players are not goods and are never sold. A "Not for sale!" bark plays.
- Throwing a friend across the river: capped speed, plus river spit-out (bridge-hazards) returns them.
- Infinite carry of a friend to grief them: struggle-free always works. In addition, a carried player auto-drops after 20 s.
- Slap-locking (repeated slaps): after a ragdoll, the target is immune to slap ragdoll for 4 s.
- A ghost chicken blocking critters: it has no collision with critters, only Cluck.
- All players knocked out: day-loop rules apply (respawn fees). Chickens can still cluck.
- A Crew Photo when a player is in a menu: they're posed idle with a "zzz".
- Chalkboard offensive drawings: board wipe, the per-player hide setting, and Streamer Mode hides drawings by default.
- A helium balloon with the megaphone: both stack (+7 st, ×2.5 radius). This is intended to be the funniest combo.

## Dependencies
| System | Direction | Contract |
|---|---|---|
| physics-carry | uses | Player-as-item (80 kg, 2 hands), throw cap |
| player-character | uses | Ragdoll, stamina, knockout state |
| proximity-voice | uses | Pitch-shift DSP slot, radius multiplier, Noise Event emit |
| blame-ledger | uses / emits | Top verb per player; emits Dunked/Prank events |
| camcorder-replay | uses | Tape ranking, MP4 export |
| character-customization | uses | Emotes, hat pop, Crew Photo poses |
| shared-wallet-economy / farm-upgrades | uses | Balloon and megaphone prices |
| frontend-lobby | provides | Chalkboard + toys in lobby |

## Tuning Knobs
| Knob | Default | Range | Category |
|---|---|---|---|
| `PlayerCarryMass` | 80 kg | 50–100 | Feel |
| `StruggleEscapePresses` / window | 6 / 2 s | 3–12 / 1–4 | Feel |
| `MaxFriendCarryTime` | 20 s | 10–60 | Gate |
| `PlayerThrowSpeedCap` | 9 m/s | 5–14 | Feel |
| `SlapImpulse` / `SlapCooldown` | 250 N·s / 1.5 s | 100–500 / 0.5–4 | Feel |
| `SlapRagdollStaminaFrac` | 0.30 | 0.1–0.6 | Feel |
| `HeliumPitch` / duration / price | +7 st / 60 s / $15 | +3–12 / 20–120 / 5–40 | Feel/Curve |
| `MegaphoneRadiusMult` / noise | 2.5 / 0.9 | 1.5–4 / 0.5–1 | Feel |
| `GhostChickenSpeed` | 5 m/s | 3–8 | Feel |
| `CrewPhotoTimer` | 5 s | 3–10 | Feel |
| `NicknameEnabled` | true | bool | Gate |
Data: `DA_SocialComedyTuning`, `DT_Nicknames`.

## Comedy beats
- Throwing your friend onto the wagon "to save time". They land in the goat pen.
- A ghost chicken clucking a pig straight into the river.
- A helium-voiced apology to the crew for dropping the prize pumpkin.
- The Crew Photo with "Butterfingers" glowing under the guy holding a bruised pumpkin.
- The megaphone shouting "QUIET, YOU'LL SCARE THEM" scares them.

## Acceptance Criteria
**Functional**
1. A player can carry, throw and escape another player. Auto-drop happens at 20 s.
2. Slap ragdolls only below 30% stamina. There is a 4 s immunity after.
3. Nicknames appear at payout and under name tags the next day, and can be disabled.
4. Knocked-out players become ghost chickens with Cluck emitting a Noise Event.
5. The Crew Photo saves a PNG with day/earnings/nicknames.
6. No prank changes the wallet or destroys goods (automated test).

**Experiential**
- VS playtest: ≥ 3 friend-throws per 4-player day, and ≥ 80% of groups laugh out loud at a friend-throw within the first day.
- Alpha: telemetry shows ≥ 1 social trigger per player per 3 min. Fewer than 10% of testers report a feature as "annoying" griefing.

## Delta vs implementation
None known. This GDD is new and nothing has been implemented yet. The emote keys 5–8 and hat pop-off are referenced from character-customization.
