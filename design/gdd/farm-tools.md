# Farm Tools

> **Status**: Draft · **Author**: game-designer + network-programmer (Batch A) · **Last Updated**: 2026-10-10
> **Implements Pillar**: The Farm Gets Worse, You Get Better
> **Quick reference**: Layer `Core` · Priority `VS` · Key deps: physics-carry, critter-ai

## 1. Overview
Farm tools are the herding verbs: the **net** (catch small runners), the **lasso** (rope a big animal), the **feed bucket** (lure) and the **whistle** (scare or rally). Each player has 2 tool slots plus their hands. Using a tool occupies one hand, so tools compete with carrying. Tools are physical objects that sit on racks in the barn, can be dropped, thrown and lost in the river.

## 2. Player Fantasy
"I've got this — lasso!" Tools let you feel like a real farmer for a moment, which makes it funnier when the bull drags you across the field.

## 3. Detailed Rules
1. **Slots:** 2 tool slots per player. Equip swaps take 0.4 s. An equipped tool uses the Right hand. Two-hand carries auto-holster the tool.
2. **Net:** swing (0.5 s wind-up, 1.0 s cooldown), 1.5 m reach, 90° arc. Captures a critter with `size_class ≤ Small` (chickens, turnips) into a bag that becomes a 1-hand Goods body. Holds 1 critter (3 with the Big Net upgrade). Captured critters struggle; 20% chance per 5 s to escape unless the bag is in a wagon or pen.
3. **Lasso:** hold to aim (arc preview), release to throw, range 12 m. On hit, it creates a 6 m tether (`physics-carry`). Works on Medium/Large critters. A Large critter (bull, cow) pulls the player — the player is dragged if critter pull > player anchor force (formula). Pulls cost 10 stamina/s.
4. **Feed bucket:** hold Use to shake (Noise loudness 0.4, tag `Noise.Food`) — attracts hungry critters within 15 m via `critter-ai`'s lure response. Throwing feed places a lure point for 20 s. 10 uses per bucket, refill at the barn.
5. **Whistle:** blow (Noise loudness 0.7, tag `Noise.Whistle`). Short blow = scare (critters flee away from the player), long blow (1 s) = rally (sheep and the guard dog follow). 3 s cooldown.
6. **Tool loss:** tools are physics bodies (1-hand). If they fall in the river they wash up at the river exit after 30 s or are lost; the barn rack restocks lost tools each morning for $15 each.
7. **Server-authoritative hits:** client plays the swing; the server does the overlap/trace. Lasso uses a server-simulated projectile, replicated at 30 Hz.

### States (lasso)
| State | Entry | Exit |
|---|---|---|
| Idle | Equip | Aim |
| Aiming | Hold Use | Release |
| Flying | Release | Hit / 12 m miss |
| Tethered | Hit | Release / rope break / critter escape |
| Reeling | Hold Use while tethered | Rope ≤ 1.5 m → auto-grab |

## 4. Formulas
**Lasso tug-of-war**
```
player_anchor = 600 N * (stamina > 0 ? 1 : 0.4) * (helpers_on_rope + 1) * ground_mult
dragged = critter_pull > player_anchor
```
critter_pull from Critter Personality (`critter-ai`): chicken 50, pig 500, goat 400, cow 900, bull 1600 N. ground_mult: grass 1.0, mud 0.6, plank 0.8. One player can hold a pig; it takes 3 to hold a bull on grass.

**Net catch chance**: `p = clamp(0.95 - 0.1 * critter_agility, 0.4, 0.95)` (agility 0–5 from Critter Personality).

## 5. Edge Cases
- **Lasso a teammate:** allowed only on KO'd players (same rule as grabbing). Otherwise the lasso bounces.
- **Lasso a goods item:** allowed; acts as a tow rope.
- **Whistle scare during a carry:** carried critters get +50% struggle for 2 s (and they will escape).
- **Feed lure near the bridge:** critters path toward it and may fall in the river — intended.
- **Two lassos on one bull:** both ropes pull; the tether system sums anchors.
- **Tool slot full and you pick up a tool:** swaps with the current tool, dropping it.

## 6. Dependencies
- **Upstream:** `physics-carry` (hands, tethers), `critter-ai` (Critter Personality: size_class, agility, pull, lure/scare responses), `proximity-voice` (Noise Events).
- **Downstream:** `farm-upgrades` (Big Net, Reinforced Lasso, Golden Bucket), `meta-progression` (new tool unlocks), `blame-ledger` (verbs `lassoed_into_river`, `whistled_away`), `hud-menus` (tool slot UI).

## 7. Tuning Knobs
| Knob | Default | Range |
|---|---|---|
| ToolSlots | 2 | 1–3 |
| NetReach / Arc / Cooldown | 1.5 m / 90° / 1.0 s | — |
| NetEscapeChance | 20% per 5 s | 0–40% |
| LassoRange / Length | 12 / 6 m | 8–18 / 4–10 |
| PlayerAnchorBase | 600 N | 400–900 |
| FeedRadius / LureDuration / Uses | 15 m / 20 s / 10 | — |
| WhistleLoudness / Cooldown | 0.7 / 3 s | — |
| ToolReplaceCost | $15 | $5–30 |

## 8. Acceptance Criteria
- A single player can net a chicken in ≤ 3 attempts on average (10 playtesters).
- A lassoed bull drags one player on grass; three players holding the rope stop it.
- Feed shake attracts a hungry pig from 14 m within 3 s; whistle scares it away.
- A tool dropped in the river is restocked next morning and $15 is charged.
- All tool hits resolve on the server; no double-catch from client and server.

## Comedy beats
The lasso water-ski: a bull dragging a farmer through the mud. Feed-luring a pig straight off the bridge. A panicked whistle that scatters the herd your friend just rounded up ("WHY would you whistle?!").
