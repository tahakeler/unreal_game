# Critter AI Framework

> **Status**: Draft · **Slug**: `critter-ai` · **Layer**: Core · **Priority**: VS · **Batch**: B
> **Owns contract**: Critter Personality · **Consumes**: Noise Event (proximity-voice), Goods Item (goods-value), Day Modifier (day-loop)

## 1. Overview
Critter AI is the shared brain for everything that runs away, hides, steals or hunts on the farm. One C# state machine (`CritterStateMachine`) drives a small set of states (Idle, Wander, Alert, Flee, Lured, Cornered, Carried, Stunned, Escaped). Movement and pathing sit on the owned **AdaptivePathfindingSystem v2.06** plugin, which handles dynamic obstacles, shortcuts and climbing. Every species, cursed crop, night creature and rival farmer is a data asset that fills in a **Critter Personality** (trait set + parameters); no species logic is hard-coded. Stimuli are player proximity, line of sight, Noise Events, lures (feed bucket, whistle) and being grabbed.

## 2. Player Fantasy
"The pig is smarter than us." Critters should feel like cheeky, readable opponents: they bolt when you shout, double back past the slow friend, squeeze under fences and come running for the feed bucket. Pillar **Everything Has Legs**: anything worth money is trying to leave. You win by out-thinking it as a team (flanking, luring, staying quiet), not by reflexes alone.

## 3. Detailed Rules

### 3.1 States
| State | Enter when | Behavior | Exit |
|---|---|---|---|
| Idle / Wander | No threat in `AlertRadius` | Grazes, wanders ≤ `WanderRadius` around home, idle animations | Threat or lure detected |
| Alert | Threat score ≥ `AlertThreshold` | Faces threat, ears up, 0.4 s "tell" (anim + VO) before fleeing | Threat ≥ `FleeThreshold` → Flee; decays below → Idle after 3 s |
| Flee | Threat ≥ `FleeThreshold` | AdaptivePathfinding flee query: maximize distance from weighted threat centroid, prefer `EscapeTargets` (gaps, river, treeline). Uses shortcuts (under fences, through hay) players cannot | Threat < `CalmThreshold` for `CalmTime`, Cornered, or Grabbed |
| Lured | Lure in range and `LureAffinity` beats threat | Paths to lure, eats for `EatTime`, ignores threats below 0.5 | Lure removed, eat done, threat spike > 0.8 |
| Cornered | Flee path cost > `CorneredCost` and ≥ 2 players within 4 m | Personality-driven: `Freeze`, `Charge`, `Juke` (dash through the widest gap) or `PlayDead` | 2–5 s or grabbed |
| Carried | Grabbed via physics-carry | Struggles: periodic wriggle impulse (`StruggleImpulse`, `StruggleInterval`); may break grip if carriers < `SecureCarriers` | Dropped/thrown → Stunned; delivered → despawn to market |
| Stunned | Thrown, ragdolled, hit hard | Dizzy, no movement for `StunTime` | Timer |
| Escaped | Reaches off-map Escape volume | Despawns; Blame Event `LetEscape` to last toucher (TouchWindow 3 s, else Nature); value lost | — |

### 3.2 Threat model
Each critter tracks a threat list (players, creatures, rivals). Threat sources: proximity, sprinting, line of sight, Noise Events, damage. Highest-threat source and threat centroid feed the flee query.

### 3.3 Noise Event handling
Consumes the `FNoiseEvent` contract exactly as defined in `proximity-voice` (`Source`, `Loudness` 0–1, `Radius`, `Location`, `Instigator`, `BlamedPlayer`, `Tags`). Each personality has a `NoiseSensitivity`, per-`ENoiseSource` multipliers (`NoiseSourceMult`) and per-tag responses (`NoiseTagResponse`, e.g. `Noise.Food` → Lure, `Noise.Whistle` → Flee, `Noise.WhistleLong` → Rally). Quiet-walk / emote-shout fallbacks (non-voice) produce the same events, so no mic is required.

### 3.4 Group behavior
Personality `HerdMode`: `None`, `Flock` (chickens: boids scatter then regroup), `Follow` (sheep follow leader, so catch the leader), `Guard` (bull defends cows). Max 1 flee-path query per critter per 0.25 s; flocks share one query per group leader.

### 3.5 Contract: Critter Personality
Owned here. Every consumer (livestock-roster, cursed-crops, night-creatures, rival-farmers) authors a `UCritterPersonality` primary data asset with these fields. Unused fields keep defaults.

| Field | Type | Default | Meaning |
|---|---|---|---|
| `Id` | FName | — | e.g. `Critter.Pig` |
| `Traits` | GameplayTagContainer | — | `Trait.Skittish`, `Trait.Greedy`, `Trait.Aggressive`, `Trait.Sneaky`, `Trait.Herd`, `Trait.Burrower`, `Trait.Climber`, `Trait.Swimmer`, `Trait.Thief`, `Trait.Loud`, `Trait.Nocturnal`, `Trait.Stalker`, `Trait.Dramatic` |
| `MoveSpeed` / `SprintSpeed` | cm/s | 300 / 550 | Player walk 400, sprint 650 cm/s (canonical, `player-character` 4.0 / 6.5 m/s) |
| `Acceleration` | cm/s² | 1500 | |
| `AlertRadius` | m | 10 | Threat detection |
| `AlertThreshold` / `FleeThreshold` / `CalmThreshold` | 0–1 | 0.3 / 0.55 / 0.15 | |
| `CalmTime` | s | 4 | |
| `NoiseSensitivity` | 0–2 | 1.0 | Multiplier on Noise loudness |
| `NoiseSourceMult` | map ENoiseSource→float | all 1.0 | Keyed by proximity-voice `Source` (Voice, Shout, Footstep, Impact, Tool, Hazard, Critter, Crop, Vehicle, Scripted) |
| `NoiseTagResponse` | map tag→{Lure, Flee, Rally, Ignore} + strength 0–1 | `Noise.Food`→Lure 0.6, `Noise.Whistle`→Flee 0.8, `Noise.WhistleLong`→Rally 0 | How the critter reacts to tool/feed noises (`farm-tools`) |
| `SightCone` | deg | 220 | |
| `LureAffinity` | map lure tag→0–1 | {} | `Lure.FeedBucket`, `Lure.Whistle`, `Lure.Shiny`, `Lure.Light` |
| `EatTime` | s | 3 | |
| `CorneredResponse` | enum | Juke | Freeze / Charge / Juke / PlayDead / Burrow / Teleport |
| `ChargeImpulse` | N·s | 0 | Impulse applied to a player hit by a Charge; compared against player `RagdollImpulse` 900 N·s (≥ 1800 = KO). Bull 1800, goat 600 |
| `SizeClass` | enum | Medium | Tiny / Small / Medium / Large / Huge. Net captures ≤ Small (`farm-tools`); lasso works on Medium+ |
| `Agility` | 0–5 | 2 | Net dodge (`farm-tools` catch chance), juke quality |
| `CritterPullN` | N | 300 | Pull force when tethered/lassoed or hitched (`farm-tools`, `wagon-vehicles`) |
| `SecureCarriers` | int 1–4 | 1 | **Players** holding it needed so it cannot break free. Lift hands come from Goods Item `CarryHands` (hands) |
| `StruggleImpulse` / `StruggleInterval` | N·s / s | 120 / 0.5–1.5 (random) | Wriggle applied to holders (`physics-carry` rule 6) |
| `GrumpyMods` | struct | FleeSpeed ×1.5, Aggression +0.3 | Applied while the critter's Goods Item has tag `Goods.Grumpy` (ruined livestock, `goods-value`) |
| `PenEscapeChance` | 0–1 | 0.6 | Chance per penned critter to leave when a gate is opened (`World.PenGateOpened`, e.g. Landlord Farm Walk); Electric Fence ×0.4 |
| `HerdMode` | enum | None | None / Flock / Follow / Guard |
| `ShortcutAccess` | tag set | {} | `Nav.UnderFence`, `Nav.Hay`, `Nav.Climb`, `Nav.Water`, `Nav.Underground` |
| `ActivePhase` | enum | Day | Day / Night / Both |
| `Aggression` | 0–1 | 0 | Chance to approach instead of flee |
| `StealTargets` | tag set | {} | Goods/tool tags it can grab (thieves) |
| `Curiosity` | 0–1 | 0.2 | Chance to approach a stationary quiet player |
| `BarkSet` | SoundCue/MetaSound ref | — | Audio-feedback VO |
| `ComedyFlags` | tag set | {} | e.g. `Comedy.Screams`, `Comedy.Faints`, `Comedy.Ragdolls` |

Modifiers: Day Modifiers may scale any numeric field through `FCritterModifier {FieldName, Mult, Add}`; stacking is multiplicative then additive, clamped to the field's range.

## 4. Formulas

**Threat from a source** (per tick, 10 Hz):
`T_src = clamp( P·w_prox + S·w_sprint + V·w_sight + N + D, 0, 1 )`
- `P = 1 − d/AlertRadius` (0 beyond radius); `w_prox = 0.5`
- `S = 1` if sprinting within AlertRadius; `w_sprint = 0.2`
- `V = 1` if in SightCone with LOS; `w_sight = 0.15`
- `N = Loudness · NoiseSensitivity · NoiseSourceMult[Source] · (1 − d/Radius)`, floored at 0 (Radius from the Noise Event, `proximity-voice`); decays at 0.25/s. Tags with a `NoiseTagResponse` of Lure/Rally do not add threat.
- `D = 0.6` on damage, decays 0.3/s

Example: pig, player 4 m away sprinting in view: P = 0.6 → 0.3 + 0.2 + 0.15 = 0.65 → Flee.

**Total threat** `T = max(T_src) + 0.25·Σ(other T_src)`, clamped 0–1.

**Lure wins** if `LureAffinity · (1 − d_lure/15 m) > T · 0.8`.

**Break free while carried**: each StruggleInterval, `P_break = clamp( (SecureCarriers − carriers) · 0.35 + 0.05, 0, 0.9 )`. Pig (SecureCarriers 2) held by 1 player: 40% per interval (mean 1.0 s).

**Cornered**: `CorneredCost` = path cost > 3× straight distance to best escape node.

## 5. Edge Cases
- Critter stuck (no path progress 3 s): AdaptivePathfinding re-query with relaxed constraints; after 8 s, teleport to nearest valid navmesh point out of camera view of all players.
- Threats from both sides: flee perpendicular to centroid axis; if both equal, Cornered.
- Lure held by a carried/ragdolled player: lure stays active at its world position.
- Thrown into river: `Trait.Swimmer` swims; otherwise river spit-out per bridge-hazards rules (critter keeps its value minus the `Soaked` water bruise from goods-value).
- Item tagged `Goods.Grumpy`: apply `GrumpyMods` immediately; tag removal (pen healing out of Ruined) reverts them.
- Pen gate opened by anyone (Landlord Farm Walk, rival, player): each penned critter rolls `PenEscapeChance` and enters Flee toward the gate.
- Host migration: critter state is server-authoritative; on migration critters resume in Alert.
- Too many critters (>40 active): far critters (>60 m from any player) drop to 2 Hz tick and pathing on idle.
- Grabbed by two different teams (rival): tug-of-war; the side with more hands wins, ties every 1 s go to the original holder.
- Player stands still and silent: `Curiosity` roll may bring critter closer (deliberate stealth catch tactic).

## 6. Dependencies
- **Upstream**: physics-carry (grab/carry hooks, CarryHands), proximity-voice (Noise Event), goods-value (Goods Item value/bruise on escape/throw), day-loop (Day Modifier, night phase), session-networking (replication), AdaptivePathfindingSystem plugin.
- **Downstream**: livestock-roster, cursed-crops, night-creatures, rival-farmers, farm-tools (lure tags, SizeClass, Agility, CritterPullN), wagon-vehicles (hitch pull), landlord-events (gate opening), audio-feedback (barks), blame-ledger (`LetEscape`).

## 7. Tuning Knobs
| Knob | Default | Safe range | Effect |
|---|---|---|---|
| AI tick rate | 10 Hz | 5–20 | CPU vs reactivity |
| Flee query interval | 0.25 s | 0.1–0.5 | Path cost |
| Alert tell duration | 0.4 s | 0.2–0.8 | Readability vs difficulty |
| Global noise mult | 1.0 | 0.5–1.5 | Voice importance |
| Global speed mult | 1.0 | 0.8–1.2 | Difficulty |
| Stuck teleport time | 8 s | 5–15 | |
| Max active critters | 40 | 20–60 | Perf budget (Apple Silicon) |

## 8. Acceptance Criteria
1. A pig flees a sprinting player within 0.4–0.6 s (tell + reaction) and reaches ≥ 8 m away in open field 90% of runs.
2. A shout (loudness ≥ 0.8) within 10 m sends every Skittish critter into Flee; a walk footstep (loudness 0.10) does not (quiet-walk emits nothing).
3. A feed bucket placed 8 m from a calm pig lures it in ≤ 5 s.
4. Two players flanking a pig against a fence trigger Cornered in ≥ 70% of attempts.
5. A pig held by 1 player breaks free in a mean of 3–5 s; by 2 players, never.
6. 40 active critters cost ≤ 2.0 ms game thread on M-series baseline.
7. New species can be added with only a Personality data asset + mesh (no code).
8. No critter stays stuck > 10 s in a 30-min soak.

## Comedy beats
- The 0.4 s Alert "tell" (ears up, record-scratch sting) lets everyone see the disaster coming.
- PlayDead critters pop back up the moment you turn away.
- Juke through the slowest player's legs, knocking them over.
- Curiosity: the pig walks up to the one friend standing silently in a bush.
