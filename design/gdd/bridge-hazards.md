# Bridge & Environmental Hazards

> **Status**: Draft · **Author**: game-designer + network-programmer (Batch A) · **Last Updated**: 2026-10-10
> **Implements Pillar**: Failure Is the Content, The Farm Gets Worse
> **Quick reference**: Layer `Core` · Priority `VS` · Key deps: physics-carry, player-character

## 1. Overview
The only road to market crosses a rickety rope bridge over a river. Planks can crack and break under load, the bridge sways when people move on it, and anything that falls into the river floats downstream and is "spat out" at a riverbank pile far away. Mud patches slow players and wagons and make them fall over. Hazards are data-driven so `weather-twists` can modify them.

## 2. Player Fantasy
"Okay, one at a time. ONE AT A TIME." The bridge is the boss fight of every day: a tense, slow crossing where everyone is a backseat driver.

## 3. Detailed Rules
### Bridge
1. VS bridge: 24 m long, 2.4 m wide, 20 planks (1.2 m each), rope rails at 1 m height (players can grab the rail to stop a fall — counts as 1 hand).
2. **Planks** have `integrity` 100. Each plank reads the total supported mass on it every 0.2 s (players 70 kg, goods/wagon via physics).
3. Damage happens when load > `SafeLoad` (250 kg per plank): `damage/s` formula below. Impacts on a plank > 800 N·s deal `impulse/40` instantly.
4. Integrity ≤ 50: the plank cracks (visual + creak, Noise Event Source=Hazard, loudness 0.3, tag `Noise.Creak`). ≤ 0: the plank breaks (Source=Hazard, loudness 0.75, tag `Noise.PlankBreak`; plus `Noise.Splash` on water entry). Anything on it falls.
5. Broken planks are repaired each morning for free. Plank damage is multiplied by Day param `Bridge.PlankBreakMult` (Bridge Reinforcement Lv1 ×0.7 / Lv2 ×0.45, `farm-upgrades`; Landlord "No Running" ×1.3; Helpful Visit ×0.5) and SafeLoad by `Bridge.SafeLoadMult` (Storm after minute 6 ×0.7; Mabel's sabotage ×0.5 on the loosened planks only). A mid-day patch kit (1 plank, **$30**, canonical price from `shared-wallet-economy`) can be placed by one player in 3 s (1 s with the Hammer upgrade).
6. **Sway:** lateral oscillation driven by moving mass: amplitude formula below, max 0.4 m. Wind (`weather-twists`) adds a force.
### River
7. Current 2.5 m/s downstream. Anything that enters the river is carried to the **spit-out pile** 120 m downstream after 15–25 s (bodies are teleported along a spline, not simulated, to save cost). Goods take a water bruise (`goods-value` decides the value hit; this system reports `bruise_type=Soaked`). Critters swim to the nearest bank and run off. Players respawn at the barn (`player-character`).
### Mud
8. Mud volumes: speed ×0.6 for players, `rolling_k` 3.0 for wagons, ragdoll threshold ×0.8, footsteps get Noise tag `Noise.Squelch`. Weather can grow mud volumes (radius ×1.5 in rain).

## 4. Formulas
**Plank damage**
```
damage_per_s = max(0, load_kg - SafeLoad * Bridge.SafeLoadMult) * 0.2 * (1 + 0.5 * cracked) * Bridge.PlankBreakMult
```
A 4-player cow carry (280 + 300 = 580 kg spread over 2 planks ≈ 290 kg each) → 8 dmg/s per plank: ~6 s to crack a plank, ~12 s to break it. A loaded wagon (520 kg on 2 planks, 260 each) → 2 dmg/s: survivable for a quick crossing.

**Sway amplitude**: `A = min(0.4, 0.02 * sum(|v_lateral| * mass_kg) / 100)` metres.

**Spit-out time**: `t = 120 m / 2.5 m/s * rand(0.8, 1.2)` ≈ 38–58 s if simulated; the spline teleport targets 15–25 s for pacing.

## 5. Edge Cases
- **Entire party on one plank:** 280 kg → slow damage, intended as "get off the plank!" tension.
- **Wagon wheel over a broken gap:** the wheel drops; wagon tips if roll > 50° (`wagon-vehicles`).
- **Critter deliberately jumps into the river:** `Trait.Swimmer` critters (duck, catfish, otter) do; the spit-out rule still applies to non-swimmers.
- **Two adjacent planks broken:** a 2.4 m gap — jumpable empty-handed, not while carrying 2-hand loads.
- **Host migration-lite resume:** bridge state resets to the start-of-day (all planks repaired).
- **Item lodged on the rope rail:** after 10 s it falls to the river.

## 6. Dependencies
- **Upstream:** `physics-carry` (masses and impulses), `player-character` (ragdoll, respawn), `session-networking` (plank state replicated reliably on change).
- **Contracts:** Noise Event (`proximity-voice`), Blame Event (`blame-ledger` — verb `BrokePlank` to the heaviest player on it; `DroppedInRiver` for goods, `Drowned` for players (emitted by player-character)), Day Modifier (`day-loop` — weather changes SafeLoad, current, mud).
- **Downstream:** `goods-value` (Soaked bruise), `wagon-vehicles`, `weather-twists`, `farm-upgrades`, `camcorder-replay` (plank break = disaster trigger), `region-campaign` (PCG bridge variants).

## 7. Tuning Knobs
| Knob | Default | Range |
|---|---|---|
| BridgeLength / Planks | 24 m / 20 | per layout |
| SafeLoad | 250 kg | 150–400 |
| DamageRate | 0.2 /kg/s | 0.1–0.5 |
| CrackThreshold | 50 | — |
| PlankPatchCost / Time | $30 / 3 s | — |
| SwayMax | 0.4 m | 0.1–0.8 |
| RiverCurrent | 2.5 m/s | 1–4 |
| SpitOutDelay | 15–25 s | — |
| MudSpeedMult / MudRagdollMult | 0.6 / 0.8 | — |

## 8. Acceptance Criteria
- A 4-player cow carry stopping mid-bridge breaks a plank in 10–15 s.
- A single player crossing alone never cracks a plank.
- Every body that enters the river appears at the spit-out pile within 25 s, with goods marked Soaked.
- A broken plank emits a Noise Event 0.75 and a Blame Event naming the heaviest player on it.
- Plank state matches on all clients within 200 ms of a break.

## Comedy beats
The cow-carry stall mid-bridge with creaking planks. The Blame Ledger naming the "heaviest" player for the break. Watching your bruised pumpkin bob past the camera to the spit-out pile 20 seconds later. Mud plus a full load equals a slapstick fall onto the bridge.
