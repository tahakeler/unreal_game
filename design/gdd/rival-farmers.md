# Rival Farmers

> **Status**: Draft · **Slug**: `rival-farmers` · **Layer**: Feature · **Priority**: EA · **Batch**: B
> **Consumes**: Critter Personality (critter-ai), Goods Item (goods-value), Noise Event (proximity-voice)

## 1. Overview
NPC rivals from neighboring farms visit some days to steal from your wagon, sabotage the bridge, or poach livestock. They are humanoid critters: a Critter Personality with `Trait.Thief`, using the same state machine with extra Steal/Sabotage tasks. Players can chase, tackle and steal back, including raiding the rival's cart.

## 2. Player Fantasy
"Oh no you DON'T, Clem." A grudge match with recurring cartoon villains you love to hate, and the joy of stealing your pig back from them.

## 3. Detailed Rules
### 3.1 Roster
| Rival | Personality | Tactic | Counter | Weakness |
|---|---|---|---|---|
| **Cousin Clem** | Sneaky, cowardly | Waits until wagon unattended 10 s, grabs the highest-value small good (≤ 2 hands), runs to his cart | Leave a guard; tackle (he drops loot) | Faints if shouted at from < 3 m (loudness ≥ 0.8) |
| **The Pruitt Twins** | Coordinated, smug | One distracts (honks, waves a feed bucket to lure your animals), the other poaches livestock | Split the team; catch the distracting twin first and the other gives up | Get confused if separated > 30 m |
| **Mabel "The Saboteur"** | Aggressive, crafty | Loosens 2–3 bridge planks (`Bridge.SafeLoadMult` ×0.5 on those planks, bridge-hazards) and greases the ramp | Inspect planks (hold interact 1.5 s to fix; instant with the Hammer upgrade, `farm-upgrades`) | Slow walker; easy to chase off |
| **Big Earl** (Post) | Bully | Pushes carried goods out of hands (900 N shove); steals the wagon itself | Lasso his boots; 3-player tackle | Distracted by pie |

### 3.2 Visit rules
- Visit chance per day = `0.15 + 0.05 × DayIndex` (cap 0.5), from Day 2. One rival per day (two on Landlord-flagged days).
- Rival arrives 2–5 min into the day, stays until steal success, tackle ×2, or sundown.
- Rival cart parked at map edge; stolen goods sit in it for 90 s before it leaves. Players can take goods back (and anything else in the cart; "steal back" bonus item 30% chance).
- Tackle: sprint into rival → rival ragdolls 2 s, drops carried item. Two tackles = rival flees the farm.

## 4. Formulas
- **Steal target score** = `Value × (1 / CarryHands) × Unattended`, Unattended = 1 if no player within 8 m for ≥ 10 s.
- **Faint chance (Clem)** = loudness ≥ 0.8 within 3 m → 100%; 0.5–0.8 → 40%.
- **Steal-back bonus**: 30% chance cart contains a random good worth 50–150.

## 5. Edge Cases
- Rival steals a critter: the critter keeps its own AI and may escape from the rival (ironic flee); Clem then chases it.
- Rival on bridge when it breaks: rival falls in river, emerges soggy, leaves.
- Players steal from rival cart before it arrives: nothing there.
- Night: rivals leave at sundown (they fear night-creatures too).
- No voice players: emote-shout counts at loudness 0.8.

## 6. Dependencies
critter-ai, goods-value, wagon-vehicles (wagon as target), bridge-hazards (sabotage hook), physics-carry (tackle/drop), proximity-voice, day-loop (visit schedule), blame-ledger (`Stolen` verb to the nearest/last player responsible for the unguarded item; `StealBack` on recovery), landlord-events (Landlord may "invite" rivals).

## 7. Tuning Knobs
Visit chance base/per-day (0.15/0.05), unattended time (10 s), cart linger (90 s), tackles to repel (2), plank weaken (−50%).

## 8. Acceptance Criteria
1. Unattended wagon for 10 s with a rival present → steal attempt within 15 s.
2. A tackle always drops the carried item.
3. Stolen goods are recoverable from the cart for 90 s; sale value unaffected beyond bruising.
4. Sabotaged planks are visible (loose nails VFX) and fixable.
5. Rivals never appear on Day 1 or in the tutorial.

## Comedy beats
- Clem fainting dramatically when shouted at.
- The pig escaping from Clem and running back to you.
- Mabel waving from the far bank as your bridge collapses.
