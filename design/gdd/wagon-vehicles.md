# Wagon & Vehicles

> **Status**: Draft · **Author**: game-designer + network-programmer (Batch A) · **Last Updated**: 2026-10-10
> **Implements Pillar**: Heavy Together, Blamed Together
> **Quick reference**: Layer `Core` · Priority `VS` (wagon) / Alpha (wheelbarrow) / Post (raft) · Key deps: physics-carry

## 1. Overview
The wagon is a large physics cart that carries many goods at once. It has no engine: players push or pull it by the handle, or hitch it to an animal (a mule, or a lassoed cow). Goods sit loose in the bed and can bounce out. Later vehicles are the 1-player wheelbarrow and river rafts (Misty Riverlands).

## 2. Player Fantasy
"Load it all and go!" A full wagon feels like a jackpot rolling toward market, which makes it devastating when one wheel goes off the bridge edge.

## 3. Detailed Rules
1. **Wagon:** Chaos physics body, 120 kg empty, 4 wheels, bed 2.0 × 1.2 m, side walls 0.4 m. Capacity is physical (whatever fits), soft cap 400 kg cargo.
2. **Moving it:** the front handle has 2 grab points, the back has 2 push points. Force per player = 900 N. A hitched animal pulls at its `critter_pull` toward its own flee/lure target — it is not steered directly; a rider can lead it with the feed bucket.
3. **Steering:** the front handle sets the axle angle (max 35°). With no one at the front, the axle centers slowly.
4. **Brake:** a lever at the side, 1 hand. Locks the wheels (sliding friction 0.8).
5. **Cargo security:** goods are free bodies in the bed. A **tarp** (1 hand, 1.5 s to tie) welds cargo positions until untied but adds −10% speed. Tarp is a VS upgrade item (`farm-upgrades`).
6. **Tipping:** if roll > 50° the wagon tips; contents spill. Righting it takes 2 players for 2 s.
7. **Damage:** wheels have 100 HP. Impacts > 1500 N·s deal `impulse/50` damage. 0 HP → wheel falls off, the corner drags. Repair at the barn ($20) or field-repair kit.
8. **Wheelbarrow (Alpha):** 1 player, 1 wheel, 80 kg capacity, tips at 30° roll.
9. **Raft (Post, Riverlands):** floats on the river current (1.5 m/s), poled by players, capacity 250 kg.
10. **Network:** server-simulated; Predictive Interpolation for the wagon; cargo bodies inside the bed replicate at Near rate regardless of distance while the wagon moves.
11. Every cargo body that leaves the bed by bouncing emits a Blame Event attributed to the player at the front handle (the driver), or the last pusher if none.

## 4. Formulas
**Wagon speed**
```
v_max = clamp(sum_pull_force / (total_mass_kg * rolling_k), 0, 6.0) m/s
rolling_k = 1.5 (dirt) · 3.0 (mud) · 1.2 (plank)
```
2 players, 220 kg total, dirt → 900·2/(220·1.5) = 5.45 m/s. Same in mud → 2.7 m/s.

**Bounce-out chance per bump**: cargo leaves the bed when vertical bump acceleration > 9.8 m/s² × (1 + wall_height/0.4 − 0.3) — emergent from physics; target: ~1 item lost per 100 m of rough road at 5 m/s without a tarp.

## 5. Edge Cases
- **Pig rides in the wagon and jumps out:** critters in the bed get escape attempts per `critter-ai` unless tarped or caged.
- **Hitched bull flees:** the wagon follows the bull at bull speed. Brake lever reduces it to 40%.
- **Wagon on a breaking plank:** the plank system sees the combined mass (`bridge-hazards`).
- **Player standing in the bed:** allowed (rides), counts as cargo mass 70 kg.
- **Wagon falls into the river:** floats for 10 s then sinks; cargo flows with the river spit-out (`bridge-hazards`). The wagon is returned to the barn next morning for $40.

## 6. Dependencies
- **Upstream:** `physics-carry` (grab points, tethers/hitch), `player-character`, `session-networking`.
- **Contracts:** Goods Item (`goods-value`) for cargo mass and bruising; Blame Event (`blame-ledger`).
- **Downstream:** `bridge-hazards` (mass on planks), `rival-farmers` (steal wagon contents), `farm-upgrades` (bigger wagon, tarp, iron wheels), `day-loop` (market drop-off zone accepts a whole wagon), `goods-value`.

## 7. Tuning Knobs
| Knob | Default | Range |
|---|---|---|
| WagonMass / CargoSoftCap | 120 / 400 kg | — |
| PushForcePerPlayer | 900 N | 600–1200 |
| VMax | 6.0 m/s | 4–8 |
| RollingK dirt/mud/plank | 1.5 / 3.0 / 1.2 | — |
| TipAngle | 50° | 35–65 |
| WheelHP / DamageDivisor | 100 / 50 | — |
| WagonRecoveryFee / WheelRepair | $40 / $20 | — |
| TarpSpeedPenalty | 10% | 0–25% |

## 8. Acceptance Criteria
- 2 players move a wagon with 100 kg cargo at ≥ 5 m/s on dirt; 1 player ≤ 3 m/s.
- Untarped wagon on the VS rough road loses 0.5–2 items per 100 m at full speed.
- Tipping at 50° spills cargo; 2 players right it in 2 s.
- A tarped wagon loses 0 items on the same road.
- Cargo bounce-outs produce Blame Events naming the driver.

## Comedy beats
The slow-motion wagon tip at the bridge edge. A hitched bull that decides the market is the other way. "Who's driving?!" is answered by the Blame Ledger. Riding in the wagon bed as cargo is a free laugh.
