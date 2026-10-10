# Physics Carry & Tethers

> **Status**: Draft · **Author**: game-designer + network-programmer (Batch A) · **Last Updated**: 2026-10-10
> **Implements Pillar**: Heavy Together, Blamed Together
> **Quick reference**: Layer `Foundation` · Priority `VS` · Key deps: player-character

## 1. Overview
Every player has two hands. Grabbing attaches a physics constraint between a hand and a body. Light goods need 1 hand. Heavy goods need 2 hands, 2 players or more. Players can throw what they hold and can tie ropes (tethers) between bodies to lead animals or tow loads. Carrying is wobbly on purpose: held objects are spring-damped, not snapped to the hands, so they swing, bump and bruise.

## 2. Player Fantasy
"PIVOT! PIVOT!" Moving a screaming pig with three friends is a negotiation. Lifting together should feel strong, and one person letting go should feel catastrophic.

## 3. Detailed Rules
1. **Hands:** each player has a Left and Right grab slot. Grab range is 1.8 m from the camera ray. Holding the grab button keeps hold, releasing drops.
2. **Carry hands:** each Goods Item has `carry_hands` (from the Goods Item contract, owned by `goods-value`): 1, 2, 4 or 6 hands. A body lifts only when `hands_attached ≥ carry_hands`. Below that it is dragged (friction ×1.0 vs ground) at drag speed.
3. **Constraint:** physics handle per hand, linear stiffness 1500, damping 120, max force 2500 N per hand. If the constraint stretches beyond 1.2 m for 0.3 s, the grip breaks ("slip").
4. **Multi-carrier:** the body's movement is the average of carriers' targets. Carriers moving in opposite directions create torque (spin) instead of cancelling.
5. **Throw:** release while charging (hold Throw up to 1.0 s). Throw speed formula below. 2-hand items can only be thrown by one player if their mass ≤ 40 kg ("heave").
6. **Critters fight back:** a held critter applies a struggle impulse every 0.5–1.5 s (critter-ai sets the strength). If it breaks the grip, it escapes.
7. **Tethers (rope):** created by the lasso (`farm-tools`) or a hitch point. A rope is a cable constraint with max length 6 m (lasso) or 3 m (hitch), breaking force 4000 N. Pulling a rope tight drags the attached body. A rope can be tied to a wagon, post or another player.
8. **Bruise reporting:** any impact on a held or thrown goods body over 300 N·s is sent to `goods-value` as a bruise hit with the instigator = the last player to touch it within 3 s. A drop counts as the dropper's fault.
9. **Network:** grabs are requested by the client and confirmed by the server (≤ 1 RTT). Clients predict the hand pose. Held bodies use predictive interpolation (`session-networking`).

### States per body
| State | Entry | Exit |
|---|---|---|
| Free | Spawn / release | Grab |
| Dragged | hands < carry_hands | Enough hands / release |
| Carried | hands ≥ carry_hands | Release / slip |
| Thrown | Throw release | Contact or 2 s |
| Tethered | Rope attach | Rope break / untie |

## 4. Formulas
**Carry speed multiplier**
```
carry_speed = clamp(1 - 0.5 * (mass_kg / (hands_attached * 25)), 0.35, 1.0)
```
1-hand 5 kg chicken → 0.9. 4 players on a 300 kg cow (8 hands) → 0.35 (the floor). Per-player `load_ratio = mass_kg / (hands_attached * 25)` clamped to 0–1 (for `player-character`).

**Throw speed**: `v = lerp(4, 14, charge) * clamp(10 / mass_kg, 0.3, 1.0)` m/s. A 2 kg turnip at full charge = 14 m/s. A 40 kg pig = 4.2 m/s.

**Wobble**: target offset gets noise `amp = 0.05 m * (1 + 2 * speed_ratio) * (1 + struggle)`.

## 5. Edge Cases
- **Two players pull a 1-hand item:** the stronger force keeps it. The other slips (tug-of-war).
- **Carrier ragdolls:** their hands release. If hands now < carry_hands, the body drops (and bruises).
- **Grabbing a player:** only KO'd players can be grabbed (anti-grief).
- **Rope between two players walking opposite ways:** both stop at 6 m. Above 4000 N the rope snaps.
- **Rope across the bridge with one end in the river:** the river current force applies. It can pull the player in.
- **Item stuck in geometry:** after 5 s with no movement while grabbed, nudge to the nearest free point.
- **Carrying while crossing a host lag spike:** the server keeps the last target for 300 ms before releasing.

## 6. Dependencies
- **Upstream:** `player-character` (hands, stamina, ragdoll), `session-networking`.
- **Contracts consumed:** **Goods Item** (`goods-value`: mass, carry_hands, fragility); **Blame Event** (`blame-ledger`: verbs `dropped`, `threw`, `slipped`).
- **Downstream:** `farm-tools`, `wagon-vehicles`, `bridge-hazards`, `critter-ai` (struggle), `goods-value` (bruise hits), `proximity-voice` (impact noise).

## 7. Tuning Knobs
| Knob | Default | Range |
|---|---|---|
| GrabRange | 1.8 m | 1.2–2.5 |
| HandStiffness / Damping / MaxForce | 1500 / 120 / 2500 N | — |
| SlipDistance / SlipTime | 1.2 m / 0.3 s | — |
| KgPerHand | 25 | 15–40 |
| ThrowSpeedMin/Max | 4 / 14 m/s | — |
| LassoLength / HitchLength / RopeBreak | 6 / 3 m / 4000 N | — |
| BruiseImpactMin | 300 N·s | 150–600 |
| BlameWindow | 3 s | 1–5 |

## 8. Acceptance Criteria
- A 6-hand item (cow) cannot be lifted by 2 players and can be lifted by 3.
- One of four carriers releasing a cow drops it within 0.5 s.
- Thrown 2 kg item at full charge travels ≥ 15 m on flat ground.
- Every impact > 300 N·s on a goods body produces one bruise hit with the correct instigator (automated test, 50 drops).
- Grab confirm ≤ 1 RTT at 150 ms ping; no item ever held by two players' 1-hand grips at once.

## Comedy beats
Opposing carriers spin the cow like a helicopter. Single heaves of heavy goods just about work. A rope tied to a friend becomes an accidental leash into the river. Every drop has a name on it, which feeds the blame argument.
