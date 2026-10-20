# Session & Networking

> **Status**: Draft · **Author**: game-designer + network-programmer (Batch A) · **Last Updated**: 2026-10-10
> **Implements Pillar**: Heavy Together, Blamed Together
> **Quick reference**: Layer `Foundation` · Priority `VS` · Key deps: none

## 1. Overview
Session & Networking hosts 2–4 players in one farm run. It uses Steam lobbies for discovery and invites. One player runs a Netcode for GameObjects (NGO) host, i.e. a listen server, and has full authority. Players can join mid-run and drop out. If the host leaves, "host migration-lite" saves the run and lets a remaining player reopen it. Every other system replicates through the rules set here: the server owns the result of all physics, money and AI, and clients only predict their own movement.

## 2. Player Fantasy
"Hey, jump in." Getting a friend into the farm should take one click from a Steam invite. A late friend should land mid-chaos instead of waiting in a menu. Lag should never be the reason the pig died. The blame must always land on a person.

## 3. Detailed Rules
### Core rules
1. **Topology:** a listen server with 1 host and up to 3 clients. Max players is 4. Solo play also runs as a listen server, so there is no separate code path.
2. **Authority:** the server is authoritative for physics bodies (goods, critters, wagons, planks), the wallet, the day timer, AI and Blame Events. Clients own input and use client-side movement prediction for their own player object only.
3. **Physics replication:** server-simulated `Rigidbody`s replicated with NGO `NetworkTransform` + `NetworkRigidbody`, with client-side interpolation; held and thrown objects are predicted locally by the holding client, idle bodies use plain server snapshots. Net update rate is 30 Hz for bodies within 30 m of any player, 10 Hz for 30–80 m and 2 Hz beyond 80 m (dormant once asleep).
4. **Lobby:** a Steam lobby via Facepunch.Steamworks (fallback: LAN session over Unity Transport). The lobby holds: run seed, region, week/day, host build version and an open/friends-only/invite-only flag. A build version mismatch blocks the join and shows "Host is on vX.Y".
5. **Join-in-progress:** allowed at any phase except the Landlord Sunday payout cutscene (players are queued until it ends). A joiner spawns at the barn spawn point with 2 s of spawn protection (no ragdoll from impacts).
6. **Drop:** a dropped client's player object ragdolls ("faints") and any held item is released. The player object despawns after 10 s. Their Blame Ledger entries persist.
7. **Host migration-lite:** no live migration. If the host leaves, the run falls back to the last day-boundary checkpoint (the Shop-phase Run Save, `save-persistence`). Clients see "Host fled the farm" and return to the lobby with a **Resume** button for the checkpoint. The checkpoint is replicated to all roster clients when it is written (Shop phase), so any roster player can host the Resume (`save-persistence` stores the guest copy).
8. **Tick budget:** server main-thread net cost ≤ 2 ms per frame at 4 players with 150 active physics bodies.
9. **Visibility (interest management):** voice and noise use their own channels (`proximity-voice`). Gameplay `NetworkObject`s use distance-based visibility (NGO object visibility checks) with a 150 m cull. Goods worth more than $50 and critters with a Blame tag are always visible to every client.

### States
| State | Entry | Exit | Behavior |
|---|---|---|---|
| Offline | Boot | Host/Join | Menus only |
| Lobby | Lobby created/joined | Host starts the day | Cosmetic pick, ready-up (all ready, or host forces) |
| InRun | Day travel | Host leaves / run ends | Full replication |
| Migrating | Host disconnect | Clients reach the lobby | Checkpoint offered |
| Ended | Week fail / quit | Back to Lobby | Results shown |

## 4. Formulas
**Body net priority**
```
priority = base_priority * (1 + 2*is_held + 1*is_moving) * clamp(30 / max(dist_m, 1), 0.1, 3)
```
| Var | Range | Notes |
|---|---|---|
| base_priority | 1–5 | goods 3, critters 4, wagon 5, planks 1 |
| dist_m | 0–150 | to the nearest player |
Output range: 0.1 to 60.

**Bandwidth budget per client**: `budget_kbps = 64 + 16 * active_players` → 96–128 kbps uplink from the host per client. Voice is excluded (handled by Steam voice).

## 5. Edge Cases
- **Two players grab the same item on the same frame:** the server resolves by earlier server-receive timestamp. The loser gets a "fumble" animation (comedy, not an error).
- **Client sees an item in hand that the server rejected:** roll back within 150 ms, and the item drops from the hand on the client.
- **Host on a Mac sleeps or alt-tabs:** the server keeps ticking (`Application.runInBackground` on; background tick throttled to 30 Hz minimum).
- **Ping over 250 ms:** a "bad connection" icon appears over the player. Over 1000 ms for 15 s → kick to lobby with a rejoin prompt.
- **Joiner arrives during the dusk phase:** spawns at the barn and gets the current Night Creature state.
- **All clients leave:** the host continues solo, and the quota scales down per `week-quota`.

## 6. Dependencies
- **Downstream (consumes this):** every system. `player-character`, `physics-carry`, `proximity-voice`, `day-loop`, `save-persistence` (checkpoint), `camcorder-replay` (records server state), `hud-menus` (lobby UI).
- **Upstream:** none. Platform: Steamworks via Facepunch.Steamworks with the Facepunch transport for NGO; fallback Unity Transport (LAN).

## 7. Tuning Knobs
| Knob | Default | Safe range |
|---|---|---|
| MaxPlayers | 4 | 2–4 (8 = post-launch experiment) |
| NearNetHz / MidNetHz / FarNetHz | 30 / 10 / 2 | 20–60 / 5–15 / 1–5 |
| NearRadius / MidRadius / CullRadius | 30 / 80 / 150 m | ±50% |
| SpawnProtection | 2 s | 1–5 s |
| DropDespawnDelay | 10 s | 5–30 s |
| BadPingThreshold / KickPing | 250 / 1000 ms | — |

## 8. Acceptance Criteria
- 4 players on a 100 ms simulated latency play a full day with 150 bodies and no desync visible after 1 s (server and client positions within 10 cm when at rest).
- Server net cost ≤ 2 ms per frame (Unity Profiler capture) at 4 players.
- A Steam invite to an in-run join completes in ≤ 20 s on LAN.
- Killing the host process → all clients reach the lobby within 5 s with a Resume button that restores the start-of-day state.
- Simultaneous grab test: 100 trials, 0 duplicated items.

## Comedy beats
The "faint" on disconnect turns a technical problem into a joke ("Dave died of Wi-Fi"). Fumbled simultaneous grabs read as two farmers fighting over a chicken. Host migration-lite has its own line ("Host fled the farm"), so a crash becomes a meme instead of rage.
