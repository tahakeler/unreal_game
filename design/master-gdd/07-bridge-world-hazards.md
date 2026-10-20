# 7. The Bridge & Environmental Hazards

> Sources: `design/gdd/bridge-hazards.md`, `rival-farmers.md`, `weather-twists.md`, `wagon-vehicles.md`, `goods-value.md`, `consistency-report.md`; code `World/Runtime/PlankMath.cs`, `BridgePlank.cs`, `BridgeController.cs`, `BridgeNetSync.cs`, `RiverVolume.cs`, `MudZone.cs`, `WorldDayParams.cs`, `World/Editor/BuildMuddlebury.cs`.
> Pillars: **Failure Is the Content**, **The Farm Gets Worse, You Get Better**, **Heavy Together, Blamed Together**.

## 7.1 Design Intent

"Okay, one at a time. ONE AT A TIME." The road to market crosses a sagging rope-and-plank bridge over a river. Every day's money has to cross it, so the bridge is the **boss fight of every day**: a slow, tense crossing where everyone is a backseat driver. Hazards follow one rule: **warn, then punish.** Every collapse is telegraphed by creaks and cracks that everyone hears, so the crew blames each other, not the game. Every hazard is driven by data, so weather, upgrades, the Landlord and rivals can retune it day by day.

Status in build: Built (Muddlebury slice: bridge, river, mud).

## 7.2 The Breakable Bridge

### Layout

| Property | GDD (VS spec) | **Built (Muddlebury scene)** |
|---|---|---|
| Length | 24 m | **40 m** |
| Planks | 20 × 1.2 m | **20 × 2.0 m** (0.14 m thick) |
| Width | 2.4 m | 2.4 m |
| Sag | — | 0.7 m dip at the centre (parabolic) |
| Rails | Rope rails at 1 m. Grabbing a rail stops a fall (uses 1 hand) | Rope rails about 1.05 m above the deck, posts every 2 planks. Rail grab not built |
| Water surface | — | 4.2 m below the farm ground |

The built bridge is longer than the spec on purpose: a 40 m crossing takes about 10 s at walking pace and about 18 s for a loaded team carry, which gives the tension time to build. The scene is canonical. **Owner decision:** update `bridge-hazards.md` §7 to 40 m / 2 m, or shorten the scene.

**Load:** each plank counts the total mass whose centre sits on its footprint. Nothing is counted twice. Players count as 70 kg, plus whatever they carry. Goods, animals and wagons count their physics mass (bull 900 kg). Load is sampled every 0.2 s.

### Integrity, Warnings and Collapse

Every plank starts each day at **integrity 100**.

| Stage | Trigger | Feedback |
|---|---|---|
| **Intact** | Integrity 100–51 | Normal wood thuds. Damage still builds silently whenever the load is over the safe load |
| **Cracked** | Integrity ≤ 50 | Darker cracked-plank material and a creak (Hazard noise 0.3, tag `Noise.Creak`, so it also spooks nearby animals). From now on damage builds **50% faster** |
| **Broken** | Integrity ≤ 0 | The plank drops away (Hazard noise 0.75, `Noise.PlankBreak`, then a splash). Everything on it falls: players get a downward shove so they fall instead of floating. Blame verb `BrokePlank` goes to the **heaviest player on the plank** |

**Damage formulas (`PlankMath`)**
```
damage/s   = max(0, load_kg − 250 × Bridge.SafeLoadMult) × 0.2 × (cracked ? 1.5 : 1) × Bridge.PlankBreakMult
impact dmg = impulse / 40, only for single impacts above 800 N·s (a thrown pumpkin, a falling wagon)
```

**How long a fresh plank lasts under a constant load** (default multipliers):

| Situation | Load per plank | Damage/s | Cracks after | Breaks after |
|---|---|---|---|---|
| One player, empty-handed or carrying a pig | 70–110 kg | 0 | never | never |
| Whole party of 4 standing on one plank | 280 kg | 6 | 8.3 s | 13.9 s |
| Loaded wagon (520 kg over 2 planks) | 260 kg | 2 | 25 s | 42 s |
| 4-player cow carry (580 kg over 2 planks) | 290 kg | 8 | 6.3 s | 10.4 s |
| 4 players + Prize Pumpkin bunched on one plank | 460 kg | 42 | 1.2 s | 2.0 s |
| The bull | 900 kg | 130 | under 0.4 s | under 0.7 s |

**Design read:** a single player crossing is always safe. A team carry that **keeps moving** makes it across, because no plank holds the group for more than a few seconds and planks only crack. The crew **stopping to argue** on one plank snaps it. Bunching up tightly around the pumpkin is the expensive mistake. Lingering is the mistake, not crossing.

### Modifiers

| Day parameter | Effect | Source |
|---|---|---|
| `Bridge.SafeLoadMult` | Storm (after minute 6) ×0.7 → 175 kg. Mabel's sabotage ×0.5 → 125 kg, **only on the 2–3 planks she loosened** | weather-twists, rival-farmers |
| `Bridge.PlankBreakMult` | Bridge Reinforcement Lv1 ×0.7, Lv2 ×0.45. Landlord "No Running" ×1.3. Landlord Helpful Visit ×0.5 | farm-upgrades, landlord-events |

Example: in a Storm, the party of 4 on one plank takes 21 dmg/s and the plank breaks in about 4 s instead of 14 s.

### Sway (Planned)
Moving mass makes the deck swing sideways: `A = min(0.4, 0.02 × Σ(|v_lateral| × mass_kg) / 100)` m. Wind adds a force. The built bridge has a static sag but no sway.

### Repair

| Repair | Cost / time | Build |
|---|---|---|
| **Morning reset:** every plank is repaired for free at the start of each day. It is also reset when a run is resumed (migration-lite) | Free | Built (`BridgeController.RepairAll`) |
| **Mid-day patch kit:** one player places it on a gap and it restores 1 plank | **$30** and 3 s (1 s with the Hammer upgrade). The charge appears on the Payout receipt | Planned |
| **Fix a sabotaged plank:** hold Interact on the loose plank | 1.5 s (instant with the Hammer). Free | Planned |

**Gaps:** one broken plank leaves a 2.0 m gap in the built bridge. Two broken planks leave a 4.0 m gap, which cannot be jumped (the jump apex is 1.1 m). **Note:** the GDD rule "a 2-plank gap (2.4 m) is jumpable empty-handed but not while carrying a 2-hand load" was written for 1.2 m planks. With 2 m planks, a *single* broken plank fills that role. This should be confirmed in playtests.

Status in build: Partial. Plank load, integrity, crack and break, impact damage, day multipliers, network sync and the morning reset are Built, and `PlankMath` is covered by EditMode tests. Sway, rail grab, the patch kit, Hammer fixes and sabotage visuals are Planned.

## 7.3 River & Water

| Rule | GDD | Built |
|---|---|---|
| Current | 2.5 m/s downstream | Not simulated. Bodies are swept along a spline |
| Where things go | Spit-out pile **120 m** downstream, after **15–25 s** | Swept along the river path to the pile at the end of the spline, with ±1.5 m scatter, after 15–25 s. Items bob and spin on the way, which makes a good "watch your pumpkin float by" moment |
| Goods | `Soaked` bruise: crops 15 BP/s, livestock 5 BP/s, duck 0 (goods-value) | A single `Soaked` hit of 600 N·s per entry |
| Players | Barn respawn after 5 s plus the weekly fee ($25 in Week 1), verb `Drowned` | Teleported to the barn respawn point (a 3 s fallback if the respawn system does not take over) |
| Animals | Swimmers swim to the bank. Others take the spit-out | Spit-out for all animals (swimmer species are not built) |
| Splash | `Noise.Splash` | Hazard noise 0.6, tag `Splash` |
| Wagon (Planned) | Floats for 10 s, then sinks. Recovered next morning for $40 | — |
| Tools | Wash up after 30 s or are lost. Restocked for $15 | Planned |
| Ropes (Planned) | Current force on a rope with one end in the water can pull a player in | — |
| Kill height | Below −25 m counts as `Drowned` | Built |

**Owner decision:** the build applies one fixed Soaked hit on entry. The GDD applies damage per second while the item is submerged. Choose one. Per-second soak needs the item to stay underwater, but the spline sweep keeps items on the surface.

Status in build: Built (sweep, spit-out pile, soak hit, drowning respawn, splash). Per-second soak, swimmers, wagon and rope interactions are Planned.

## 7.4 Mud

| Effect | Value | Build |
|---|---|---|
| Player speed | ×0.6 (walk 4.0 → 2.4 m/s). Day parameter `Mud.SpeedMult` can override it | Built |
| Ragdoll threshold | ×0.8 | Built |
| Footsteps | Tag `Noise.Squelch` | Built |
| Wagon rolling resistance | 3.0 (dirt 1.5), so a loaded wagon moves about half as fast | Planned (no wagon yet) |
| Lasso anchor | ×0.6 | Planned |
| Wool | Mud cancels the clean-wool ×1.2 bonus | Planned |
| Weather | Rain: mud radius ×1.5. Mud Day grows the mud areas under the same rules | Partial (day-parameter hook) |

Mud is placed on the approaches to the bridge on purpose, so that tired, loaded and muddy all stack in the same spot right before the boss fight.

Status in build: Built (`MudZone`).

## 7.5 Other Hazards

| Hazard | Effect | Build |
|---|---|---|
| **Slopes** | Sprinting with load ≥ 70% on a slope steeper than 25° makes you trip (Chapter 4). The road down to the bridge is the main slope | Built |
| **Fences and gates** | Block players. Pigs squeeze under fences. An opened gate means a 60% chance each penned animal leaves | Built (fences) / Planned (gate rolls) |
| **Rope rail** | An item lodged on the rail falls into the river after 10 s | Planned |
| **Weather twists** | Storm (safe load ×0.7 after minute 6), Mud Day, wind (affects sway), Drizzle, Flood, Blood Moon | Partial (day-parameter hooks) |
| **Night creatures** | Scarecrow, Barn Thing and Crows (coin theft $10 per hit, up to $50 per day). Night KOs mean a barn respawn and the fee | Planned |
| **Landlord events** | Farm Walk opens the gates. Quiet Hours lowers voice range. No Running makes plank damage ×1.3 | Planned |

## 7.6 Rival Farmers (EA)

Rival farmers are NPC neighbours built as "humanoid critters". They use the critter personality framework with a Thief trait and extra steal and sabotage behaviours.

| Rival | Tactic | Counter | Weakness |
|---|---|---|---|
| **Cousin Clem** | Waits for the wagon to be unattended for 10 s, then grabs the highest-value small item (2 hands or fewer) and runs to his cart | Leave a guard; tackle him (he drops the loot) | Faints if you shout at him from less than 3 m (loudness 0.8 or more: 100%; 0.5–0.8: 40%) |
| **The Pruitt Twins** | One distracts you (honks, lures your animals with a feed bucket) while the other poaches livestock | Split up; catch the distracting twin first and the other gives up | Get confused when more than 30 m apart |
| **Mabel "The Saboteur"** | Loosens 2–3 bridge planks (safe load ×0.5) and greases the ramp | Inspect and fix the planks (hold Interact 1.5 s; instant with the Hammer) | Walks slowly; easy to chase off |
| **Big Earl** (Post) | Shoves goods out of your hands (900 N) and steals the wagon itself | Lasso his boots; a 3-player tackle | Distracted by pie |

**Visit rules**
- Chance per day: `0.15 + 0.05 × DayIndex`, capped at 0.5, starting from Day 2. Never on Day 1 or in the tutorial.
- One rival per day (two on days the Landlord flags).
- The rival arrives 2–5 minutes into the day. They stay until they steal something, get tackled twice, or sundown (rivals fear the night too).
- **Steal target score:** `Value × (1 / CarryHands) × Unattended`. An item counts as unattended when no player has been within 8 m for 10 s.
- **Tackle:** sprint into a rival. The rival ragdolls for 2 s and drops what they are carrying. Two tackles and the rival leaves the farm.
- **Rival cart:** parked at the map edge. Stolen goods sit in it for 90 s before it leaves. You can take anything back. There is a 30% chance of a bonus item worth $50–150 ("steal-back").
- **Blame:** `Stolen` goes to the nearest or last player responsible for the unguarded item. `StealBack` is logged when goods are recovered. Theft never takes the wallet below $0.
- **Comedy edge cases:** a stolen animal keeps its own AI and may escape from the rival and run back to you. A rival on a breaking plank falls into the river, comes out soggy and leaves.

Status in build: Planned (EA).

## 7.7 Network & Authority

The server owns plank integrity, breaks, river sweeps and theft. Plank state is sent to every client reliably whenever it changes (`BridgeNetSync`), with a target of under 200 ms after a break. Clients play the creak, crack and break feedback the moment the new state arrives, so the whole crew hears the same warning at the same time. That is essential for a fair argument.

Status in build: Built.

## 7.8 Edge Cases

- **Wagon wheel over a gap:** the wheel drops. The wagon tips if its roll exceeds 50°.
- **A knocked-out friend on a plank:** counts as 70 kg. Friends can throw them across a 2 m gap.
- **A plank breaks under a group carry:** every carrier on it falls, and the object drops and bruises. The heaviest player on the plank gets `BrokePlank`, and the drop shares follow the touch history.
- **The bull charges onto the bridge:** at 900 kg it breaks a plank in under a second. Blame goes to "Bull".
- **Load on the boundary between two planks:** a body counts only on the plank under its centre, so a team spread across two planks shares the load naturally.

## 7.9 Acceptance Criteria

- A single player crossing alone never cracks a plank.
- A 4-player cow carry that stops mid-bridge breaks a plank in 10–15 s (formula: 10.4 s).
- Every break is preceded by the crack stage (integrity ≤ 50) with the creak cue on all clients.
- Every body that falls into the river appears at the spit-out pile within 25 s. Goods are marked Soaked.
- A broken plank emits a 0.75 noise and a Blame Event naming the heaviest player on it.
- Plank state matches on all clients within 200 ms of a break.
- Mud reduces walk speed to 2.4 m/s.
- (Planned) A plank patch costs $30 and appears on the Payout receipt.
