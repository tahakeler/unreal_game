# Systems Index: HARVEST PANIC

> **Status**: Draft (non-interactive /map-systems pass; assumptions recorded below)
> **Created**: 2026-10-10
> **Source Concept**: design/game-concept.md (revision 2)
> **Master GDD**: design/gdd/game-overview.md
> **Engine**: UE 5.8, Apple Silicon Mac, 2–4 player listen server

---

## Overview

HARVEST PANIC is a physics co-op comedy game. The core loop (round up escaping goods, haul them across the bridge, get paid) runs on four foundations: networked players, physics carrying, proximity voice, and critter AI that tries to escape. Everything above that layer exists to escalate the chaos (curses, weather, night creatures, rivals, Landlord events) or to turn failure into content (Blame Ledger, Camcorder, ragdolls). There is one shared wallet, and the weekly quota is the pressure that ties the day, week and campaign loops together. Pillars: **Everything Has Legs**, **Heavy Together, Blamed Together**, **The Farm Gets Worse, You Get Better**, **Failure Is the Content**.

Content scales by multiplying systems (species × curse × weather × Landlord event × PCG layout), so each system must expose clean **modifier hooks** and must not hard-code content.

---

## Systems Enumeration

Priority tiers: **VS** = 2-week Vertical Slice · **Alpha** = Prototype→Alpha (~3 months) · **EA** = Early Access launch · **Post** = post-launch updates.
Layers: **Foundation** → **Core** → **Feature** → **Presentation** → **Polish**.

| # | System | Slug | Layer | Priority | Status | Design Doc | Depends On |
|---|---|---|---|---|---|---|---|
| 1 | Session & Networking (lobby, listen server, join-in-progress, host migration-lite) | `session-networking` | Foundation | VS | Not Started | design/gdd/session-networking.md | — |
| 2 | Player Character (movement, stamina, ragdoll, knockout, barn respawn) | `player-character` | Foundation | VS | Not Started | design/gdd/player-character.md | session-networking |
| 3 | Physics Carry & Tethers (grab, multi-hand carry, throw, lasso/hitch ropes) | `physics-carry` | Foundation | VS | Not Started | design/gdd/physics-carry.md | player-character |
| 4 | Proximity Voice & Noise (EOS 3D voice, loudness → world "noise events") | `proximity-voice` | Foundation | VS | Not Started | design/gdd/proximity-voice.md | session-networking |
| 5 | Critter AI Framework (flee/lure/corner StateTree on AdaptivePathfinding, personality traits) | `critter-ai` | Core | VS | Not Started | design/gdd/critter-ai.md | physics-carry, proximity-voice |
| 6 | Goods Value & Bruising (base value, freshness, bruise types, damage → value) | `goods-value` | Core | VS | Not Started | design/gdd/goods-value.md | physics-carry |
| 7 | Farm Tools (net, lasso, feed bucket, whistle; tool slots) | `farm-tools` | Core | VS | Not Started | design/gdd/farm-tools.md | physics-carry, critter-ai |
| 8 | Wagon & Vehicles (physics wagon, hitching, later rafts / wheelbarrow) | `wagon-vehicles` | Core | VS | Not Started | design/gdd/wagon-vehicles.md | physics-carry |
| 9 | Day Loop (morning order, manifest, sundown timer, market zone, dusk bonus, payout) | `day-loop` | Core | VS | Not Started | design/gdd/day-loop.md | goods-value, session-networking |
| 10 | Shared Wallet & Market Economy (one wallet, market prices, respawn fees, sinks) | `shared-wallet-economy` | Core | VS | Not Started | design/gdd/shared-wallet-economy.md | goods-value, day-loop |
| 11 | Bridge & Environmental Hazards (breakable planks, sway, river spit-out, mud) | `bridge-hazards` | Core | VS | Not Started | design/gdd/bridge-hazards.md | physics-carry, player-character |
| 12 | Livestock Roster (pig, chicken, goat, cow, bull, sheep… species data) | `livestock-roster` | Feature | VS (pig, chicken) / Alpha (5) / EA (8) | Not Started | design/gdd/livestock-roster.md | critter-ai, goods-value |
| 13 | Week Loop, Quota & Landlord Sunday (5 days + Sunday, escalation, fail state) | `week-quota` | Feature | Alpha | Not Started | design/gdd/week-quota.md | day-loop, shared-wallet-economy |
| 14 | Farm Upgrades & Shop (between-day purchases, ~30 upgrades by EA) | `farm-upgrades` | Feature | Alpha | Not Started | design/gdd/farm-upgrades.md | shared-wallet-economy, week-quota |
| 15 | Cursed Crops (burrowing turnips, mandrakes, growing pumpkins, maze corn) | `cursed-crops` | Feature | VS (prize pumpkin) / Alpha (3) | Not Started | design/gdd/cursed-crops.md | critter-ai, goods-value, proximity-voice |
| 16 | Night Creatures (Scarecrow, Barn Thing, Crows; night director) | `night-creatures` | Feature | Alpha (Scarecrow) / EA (2) | Not Started | design/gdd/night-creatures.md | critter-ai, proximity-voice, day-loop |
| 17 | Weather & Daily Twists (mud, wind, storm, fog; twist deck) | `weather-twists` | Feature | Alpha | Not Started | design/gdd/weather-twists.md | day-loop, bridge-hazards |
| 18 | Rival Farmers (NPC thieves/saboteurs, steal-back) | `rival-farmers` | Feature | EA | Not Started | design/gdd/rival-farmers.md | critter-ai, wagon-vehicles, goods-value |
| 19 | Landlord Events (inspections, quota doubles, Landlord walks the farm) | `landlord-events` | Feature | Alpha | Not Started | design/gdd/landlord-events.md | week-quota, day-loop |
| 20 | Blame Ledger & Awards (per-player disaster attribution, MVD, betrayal reveal) | `blame-ledger` | Feature | VS (bruise report) / Alpha (full) | Not Started | design/gdd/blame-ledger.md | goods-value, shared-wallet-economy |
| 21 | Grandpa's Camcorder (rolling 30s replay buffer, disaster trigger, export) | `camcorder-replay` | Feature | Alpha | Not Started | design/gdd/camcorder-replay.md | session-networking, blame-ledger |
| 22 | Region Campaign & PCG Layouts (regions, market towns, biome rules, seeded layouts) | `region-campaign` | Feature | EA (Region 1 + PCG) / Post (2, 3) | Not Started | design/gdd/region-campaign.md | week-quota, livestock-roster, weather-twists |
| 23 | Save & Persistence (run save, account profile, roguelite reset rules) | `save-persistence` | Core | Alpha | Not Started | design/gdd/save-persistence.md | week-quota, session-networking |
| 24 | Meta Progression, Cosmetics & Almanac (account unlocks, hats, almanac entries) | `meta-progression` | Feature | EA | Not Started | design/gdd/meta-progression.md | save-persistence, livestock-roster |
| 25 | Eviction Mode (endless quota ladder, daily seed, leaderboard) | `eviction-mode` | Feature | EA | Not Started | design/gdd/eviction-mode.md | week-quota, region-campaign, save-persistence |
| 26 | HUD, Menus & Ledger Screens (diegetic-first HUD, lobby, payout, shop UI) | `hud-menus` | Presentation | VS (payout) / Alpha | Not Started | design/gdd/hud-menus.md | day-loop, blame-ledger, farm-upgrades |
| 27 | Audio & Comedy Feedback (MetaSounds bruise/impact, critter VO, music states) | `audio-feedback` | Presentation | Alpha | Not Started | design/gdd/audio-feedback.md | goods-value, critter-ai, day-loop |
| 28 | Onboarding & Accessibility (Grandpa's tape tutorial, subtitles, voice-free play) (inferred) | `onboarding-accessibility` | Polish | EA | Not Started | design/gdd/onboarding-accessibility.md | hud-menus, proximity-voice |

28 systems. Rows 23 and 28 are **inferred** (the concept does not list them explicitly).

---

## Dependency Map

### Foundation Layer
1. `session-networking`: everything replicates through it; listen-server authority model decided here.
2. `player-character`: the body that falls over. Ragdoll and respawn rules are the comedy baseline.
3. `physics-carry`: Pillar 2 lives here. Multi-hand carry, throw and tether constraints.
4. `proximity-voice`: owns the **Noise Event** contract (voice loudness + impacts → AI stimuli).

### Core Layer
1. `critter-ai`: depends on physics-carry, proximity-voice
2. `goods-value`: depends on physics-carry
3. `farm-tools`: depends on physics-carry, critter-ai
4. `wagon-vehicles`: depends on physics-carry
5. `day-loop`: depends on goods-value, session-networking
6. `shared-wallet-economy`: depends on goods-value, day-loop
7. `bridge-hazards`: depends on physics-carry, player-character
8. `save-persistence`: depends on session-networking (data schema), consumed by week-quota onward

### Feature Layer
1. `livestock-roster`, `cursed-crops`: depend on critter-ai, goods-value
2. `week-quota` → `farm-upgrades`, `landlord-events`
3. `night-creatures`, `weather-twists`, `rival-farmers`: depend on critter-ai / day-loop / hazards
4. `blame-ledger` → `camcorder-replay`
5. `region-campaign` → `eviction-mode`, `meta-progression`

### Presentation / Polish Layer
1. `hud-menus`, `audio-feedback`: depend on most feature systems
2. `onboarding-accessibility`: last; wraps everything

### Cross-cutting contracts (must be defined before parallel GDD writing)
| Contract | Owner | Consumers |
|---|---|---|
| **Noise Event** (source, loudness 0–1, radius, instigator) | proximity-voice | critter-ai, night-creatures, cursed-crops, rival-farmers |
| **Goods Item** (id, base value, fragility, bruise state, carry hands) | goods-value | physics-carry, wagon, livestock, crops, ledger, economy |
| **Blame Event** (player, verb, item, $ delta, timestamp) | blame-ledger | goods-value, physics-carry, bridge-hazards, camcorder |
| **Day Modifier** (tag, stacking rules, duration) | day-loop | weather-twists, landlord-events, cursed-crops, region-campaign, eviction-mode |
| **Critter Personality** (trait set + params) | critter-ai | livestock-roster, cursed-crops, night-creatures, rival-farmers |

---

## Recommended Design Order

| Order | System | Priority | Layer | Est. Effort |
|---|---|---|---|---|
| 1 | session-networking | VS | Foundation | M |
| 2 | physics-carry | VS | Foundation | L |
| 3 | player-character | VS | Foundation | M |
| 4 | proximity-voice | VS | Foundation | M |
| 5 | goods-value | VS | Core | M |
| 6 | critter-ai | VS | Core | L |
| 7 | day-loop | VS | Core | M |
| 8 | bridge-hazards | VS | Core | M |
| 9 | wagon-vehicles / farm-tools | VS | Core | S each |
| 10 | shared-wallet-economy | VS | Core | M |
| 11 | livestock-roster, blame-ledger (VS slice) | VS | Feature | M |
| 12+ | Alpha systems, then EA, then Post | | | |

---

## Parallel GDD Authoring: 4 Batches

Each batch **owns** its systems outright; cross-batch needs go through the contracts above (owner batch publishes the contract first, in the first ~day of writing). No system appears in two batches.

| Batch | Theme | Systems | Owns contracts |
|---|---|---|---|
| **A: Bodies & Network** | How players exist, touch and talk | session-networking, player-character, physics-carry, proximity-voice, farm-tools, wagon-vehicles, bridge-hazards | Noise Event |
| **B: Things With Legs** | Everything that runs, bites or steals | critter-ai, livestock-roster, cursed-crops, night-creatures, rival-farmers, weather-twists | Critter Personality |
| **C: Money & Time** | The loops and the debt | goods-value, day-loop, shared-wallet-economy, week-quota, landlord-events, farm-upgrades, save-persistence | Goods Item, Day Modifier |
| **D: Content, Comedy & Shell** | Turning failure into clips, long-term pull, presentation | blame-ledger, camcorder-replay, region-campaign, meta-progression, eviction-mode, hud-menus, audio-feedback, onboarding-accessibility | Blame Event |

Batch sizes: A 7, B 6, C 7, D 8. Batch D is mostly later-priority systems, so it can start later. Batches A and C hold the VS-critical systems and should start first.

---

## Assumptions (made non-interactively; user may override)
1. Priority tiers follow the concept roadmap (VS / Alpha / EA / Post) instead of the template's MVP tiers. The VS tier equals the MVP.
2. Weather and daily twists are grouped with the creatures (Batch B) because both are "world acting against you" modifiers authored on the Day Modifier contract.
3. Sessions use Steam lobbies (storefront) with an EOS voice backend. Fallback is EOS for both. The Mac EOS voice risk is flagged in proximity-voice.
4. Save and persistence (inferred) uses run-scoped saves that are wiped on a failed Sunday, plus a separate account profile for unlocks.
5. Onboarding and accessibility (inferred) are needed for streamers and players without a mic. Every voice-dependent mechanic needs a non-voice fallback (emote shout / quiet-walk button).
6. Farm tools are split from physics-carry so that the carry GDD stays focused on the hand/constraint model.
7. The Landlord is an event system and not a creature. He never attacks but does cause chaos (inspections, "helping").
