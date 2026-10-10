# Player Character

> **Status**: Draft · **Author**: game-designer + network-programmer (Batch A) · **Last Updated**: 2026-10-10
> **Implements Pillar**: Failure Is the Content
> **Quick reference**: Layer `Foundation` · Priority `VS` · Key deps: session-networking

## 1. Overview
The player is a clumsy farmer heir. They can walk, sprint, jump, crouch (quiet-walk), dive and shout (an emote). A stamina bar limits sprinting and carrying. The body switches to a physics ragdoll when hit hard, when tripping while running with a heavy load, or when knocked out. A knocked-out player either gets up on their own or is revived by a friend. If they fall into the river or off the map, they respawn at the barn for a fee from the shared wallet.

## 2. Player Fantasy
"I am a competent farmer for about four seconds at a time." Movement feels responsive. Falling over feels earned and funny, never random. Getting back up is fast, so the fall is a joke instead of a punishment.

## 3. Detailed Rules
### Movement
1. Walk 4.0 m/s, sprint 6.5 m/s, crouch 2.0 m/s, jump apex 1.1 m. Carry speed multiplier comes from `physics-carry`.
2. Stamina: 100. Sprint drains 15/s. Carrying drains `carry_load_drain` (see Formulas). Regenerates 25/s after a 1.0 s delay when not sprinting. At 0, sprint is disabled until 30 stamina.
3. **Dive:** costs 20 stamina, 3 m lunge forward. Ends in a 0.8 s belly-flop (ragdoll-lite). Used to tackle critters (grab on contact).
4. **Shout (emote, a.k.a. "Holler" button in `onboarding-accessibility`):** a non-voice alternative to voice for players without a mic. Emits a Noise Event of loudness 0.8 (see `proximity-voice`). 2 s cooldown.
5. **Quiet-walk (a.k.a. "Shush"):** crouch movement. Player footsteps produce no Noise Events.

### Camera and view (first person — direction change 2026-10-10)
5a. **First-person camera** at eye height (`EyeHeight` 64 cm above capsule centre ≈ 1.6 m), like R.E.P.O. / Lethal Company / PEAK. The body yaws with the camera.
5b. **Local view:** the owner sees procedurally posed first-person arms (owner-only) that rest low in front, reach forward when aiming at a carryable, and reach to the sides of a held object. The owner never sees their own full body while upright.
5c. **Other players** always see the full third-person body (owner-no-see), so co-op slapstick reads.
5d. **Feel:** subtle head bob scaled by ground speed; sprint FOV kick (+`SprintFOVKick`); camera shake on received impacts (trauma = impulse / `ShakeFullImpulse`, decays).
5e. **Ragdoll / KO camera:** the camera attaches to the ragdoll's head and tumbles with it (the owner sees their own body), then snaps back to the eyes on get-up / revive.
5f. **Targeting:** a centre-screen trace (reach `GrabRange` from the camera, `physics-carry`) with a small centre dot and a prompt "E: Grab (attached/required hands)"; while holding: "E: Drop  LMB: Throw".

### Ragdoll and knockout
6. **Ragdoll triggers:** an impact impulse above `RagdollImpulse` (default 900 N·s, scaled by the formula below), sprinting with load ≥ 70% of max at a slope > 25°, standing on a plank when it breaks, or being charged by a bull (`livestock-roster`: `ChargeImpulse` 1800 N·s = 2× RagdollImpulse, so a direct bull hit is a KO).
7. **Ragdoll recovery:** after the body velocity < 0.5 m/s for 0.5 s, the player can stand up with a button press (1.2 s get-up anim). Mashing has no effect (accessibility).
8. **Knockout (KO):** an impulse ≥ 2× RagdollImpulse, or 3 ragdolls within 20 s. The player is KO for 8 s (stars over head). A teammate can hold Interact for 1.5 s to revive at once. Teammates can also carry a KO'd player as a 2-hand Goods-like body (weight 70 kg) — yes, you can throw your friend into the wagon.
9. **Barn respawn (the only thing that costs a respawn fee):** a player is sent to the barn (5 s, `Respawning` state) when they (a) fall into the river or below the kill-Z → Blame verb `Drowned`; (b) are knocked out by a night creature (`night-creatures`) → verb `KnockedOut`; (c) are stranded outside the barn/market zone at day end (`day-loop`) → verb `Stranded`. Fee: `RespawnFee(w)` owned by `shared-wallet-economy` (W1 $25 … W6 $55), attributed to that player. A daytime KO (rule 8) recovers in place and costs nothing.
10. The server is authoritative for ragdoll state. Clients predict the start of a ragdoll from local impacts, and the server corrects within 200 ms.

### States
| State | Entry | Exit | Behavior |
|---|---|---|---|
| Normal | Spawn / get-up | Trigger | Full control |
| Diving | Dive input | Land 0.8 s | Lunge, can grab |
| Ragdoll | Trigger | Get-up | No control, physics body |
| KO | KO trigger | 8 s / revive | Carriable, voice still works (but muffled 50%) |
| Respawning | OOB / drown | 5 s | Spectator cam on the body |

## 4. Formulas
**Ragdoll threshold**
```
threshold = RagdollImpulse * (1 - 0.4 * load_ratio) * (stamina < 20 ? 0.7 : 1.0) * mud_mult
```
load_ratio 0–1 (from `physics-carry`). mud_mult 0.8 in mud (`bridge-hazards`), 1.0 otherwise. Range: 302–900 N·s. Carrying a full load in mud while tired makes you about 3× easier to knock over.

**Carry stamina drain**: `carry_load_drain = 8 * load_ratio` per second (sprinting adds 15). Range 0–23/s.

**Respawn fee**: not defined here. Use `RespawnFee(w)` from `shared-wallet-economy` (per-week schedule $25/30/40/45/50/55, scaled by `Economy.RespawnFeeMult`). *(Consistency pass 2026-10-10: the former per-player escalating formula $25 + $10/death cap $75 was removed.)*

## 5. Edge Cases
- **Ragdoll while holding an item:** the item is released with the hand's velocity (it flies — on purpose).
- **KO'd player thrown into the river:** respawn rule applies. The thrower gets the Blame Event (`verb=YeetedTeammate`); the drowned player still gets `Drowned` for the fee.
- **Wallet below the respawn fee:** respawn still happens. The wallet may go negative down to the −$200 overdraft floor; any excess becomes a Landlord IOU added to this week's Due (`shared-wallet-economy` owns it).
- **All players KO at once:** everyone gets up after 8 s. There is no wipe state during the day.
- **Ragdoll on a moving wagon:** body inherits the wagon's velocity.
- **Get-up inside geometry:** teleport to the nearest navmesh point within 2 m.

## 6. Dependencies
- **Upstream:** `session-networking` (prediction, authority).
- **Downstream:** `physics-carry` (hands, load_ratio), `bridge-hazards` (mud, plank breaks), `proximity-voice` (footstep and shout noise), `livestock-roster` (bull charge impulse), `blame-ledger` (Blame Events), `shared-wallet-economy` (respawn fee), `camcorder-replay` (ragdoll = disaster trigger), `meta-progression` (cosmetics on the body).

## 7. Tuning Knobs
| Knob | Default | Range |
|---|---|---|
| WalkSpeed / SprintSpeed / CrouchSpeed | 4.0 / 6.5 / 2.0 m/s (= 400 / 650 / 200 cm/s, canonical) | ±25% |
| StaminaMax / SprintDrain / Regen / RegenDelay | 100 / 15 / 25 / 1.0 s | — |
| RagdollImpulse | 900 N·s | 600–1500 |
| KODuration / ReviveHold | 8 s / 1.5 s | 4–15 / 0.5–3 |
| RespawnDelay | 5 s | 3–10 |
| EyeHeight / BaseFOV / SprintFOVKick | 64 cm / 90° / +8° | — / 80–110 / 0–15 |
| HeadBobAmplitude / Frequency | 1.6 cm / 1.8 Hz | 0–4 / — |
| ShakeMaxDegrees / ShakeFullImpulse / Decay | 4° / 900 N·s / 2 per s | 0–8 / — / — |
| RespawnFee | see `shared-wallet-economy` (per week) | — |

## 8. Acceptance Criteria
- A full-load sprint down a 30° slope ragdolls the player 100% of the time. An empty-handed sprint never does.
- Get-up is available ≤ 1.5 s after the body comes to rest.
- A KO'd player can be picked up, carried and thrown by one teammate.
- A river fall in Week 1 → barn respawn in 5 s, wallet −$25, a `Drowned` Blame Event logged.
- Shout emote triggers a Noise Event (loudness 0.8) visible in the AI debugger.

## Comedy beats
Tired + loaded + mud = guaranteed pratfalls on the bridge. Carrying and throwing a KO'd friend into the wagon is a top clip. Respawn fees put a dollar figure on every death in the Blame Ledger ("Dave drowned 4 times: −$100" in Week 1, −$160 by Week 3).
