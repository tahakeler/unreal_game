# 5. Carrying, Physics & Tools

> Sources: `design/gdd/physics-carry.md`, `farm-tools.md`, `wagon-vehicles.md`, `goods-value.md`, `consistency-report.md`; code `Gameplay/Carry/*`, `Gameplay/Config/CarryTuning.cs`, `ToolTuning.cs`, `Gameplay/Tools/NetTool.cs`, `FeedBucketTool.cs`.
> Pillar: **Heavy Together, Blamed Together**.

## 5.1 Design Intent

"PIVOT! PIVOT!" Carrying is the core verb of HARVEST PANIC. A held object hangs from springs at your hands. It is never snapped rigidly in place, so it swings, bumps and bruises. Lifting together should feel strong. One person letting go should feel catastrophic. Every drop has a name on it, and that feeds the Blame Ledger.

Status in build: Built.

## 5.2 Pick Up, Hold, Drop

1. **Aim and grab:** look at an object within **1.8 m** and tap **E**. The client asks the server, and the server confirms within one round trip. The server accepts grabs up to 2.55 m to allow for lag.
2. **Hands:** each player gives **2 hands** to one held object. In the current build a player holds one thing at a time.
3. **Hold point:** the object floats 1.05 m in front of your eyes and 0.3 m below your eye line. Each grip is a spring: stiffness 1,500, damping 120, maximum force 2,500 N.
4. **Drop:** tap **E** again. A drop counts as the dropper's fault for any bruise in the next 2 s.
5. **Slip:** if your grip is stretched more than **1.2 m** for **0.3 s**, you lose your hold. This happens when you snag on fences or when an animal yanks free.
6. **Stuck item:** if a held object does not move for 5 s, it is nudged to free space (proposed in the GDD, not yet coded).
7. **Lag spike:** the server keeps your last hold target for 300 ms before it releases the object.

Status in build: Built (the stuck-item nudge is Planned).

## 5.3 Weight Classes

Each goods item lists the **hands** needed to lift it. Each player supplies 2, so the number of carriers needed is half the hands, rounded up. With fewer hands than that, the object is **dragged** along the ground instead of lifted.

| Class | Hands (carriers) | Examples | Mass (build) | Feel |
|---|---|---|---|---|
| Pocket | 1 (1) | Egg, carrot, turnip, chicken, tools | 0.06–5 kg | Fast, throwable, easy to lose |
| Armful | 2 (1) | Pig, egg crate, goat, sheep, KO'd friend | 4–70 kg | One player, slowed |
| Heavy | 4 (2) | Prize llama, Tumble Melon | ~60–80 kg (proposed) | Needs two people |
| Team | 6 (3) | Cow, bull | 300 / 900 kg | Three people, at the speed floor |
| Monster | 8 (4) | Prize Pumpkin (180 kg), fully grown Growing Pumpkin | 180 kg | Whole crew. Comedy guaranteed |

**Carry speed**
```
load_ratio  = clamp(mass_kg / (hands_attached × 25), 0, 1)
carry_speed = clamp(1 − 0.5 × load_ratio, 0.35, 1.0)
```
| Example | Result |
|---|---|
| Chicken, 5 kg, 2 hands | 0.95 → 3.8 m/s walk |
| Pig, 40 kg, 1 player | load 0.8 → 0.6 → 2.4 m/s |
| Pumpkin, 180 kg, 4 players | load 0.9 → 0.55 |
| Cow, 300 kg, 3 players | 0.35 (the floor) |

The load ratio also drives stamina drain (8 × load/s) and lowers your ragdoll threshold (Chapter 4).

Status in build: Built (`CarryMath`).

## 5.4 Two-Player and Team Carries

- **Grabbing a shared object:** players grab the same object at different points. The prompt shows the progress, e.g. "E: Grab (2/8 hands)".
- **Steering:** the object moves toward the average of all carriers' targets. If carriers pull in opposite directions, the object **spins** instead of the pulls cancelling. This is the "helicopter cow".
- **Losing a carrier:** if a carrier lets go, slips or ragdolls and the hands fall below what is needed, the object drops within 0.5 s and bruises. The person who let go is blamed.
- **Tug-of-war:** two players pulling one 1-hand item: the stronger pull keeps it and the other slips. A 1-hand item is never held by two people at once.
- **Heave:** a single player can throw a 2-hand item only if it weighs 40 kg or less.

Status in build: Built.

## 5.5 Throwing

Hold **LMB** to charge (full charge at 1.0 s), then release to throw along your view, aimed slightly upward. Your own velocity is added.
```
v = lerp(4, 14, charge) × clamp(10 / mass_kg, 0.3, 1.0)   m/s
```
| Item | Full-charge speed |
|---|---|
| Turnip, 2 kg | 14 m/s (travels 15 m or more) |
| Chicken, 5 kg | 14 m/s |
| Pig, 40 kg | 4.2 m/s (a heave) |

A thrown object stays "Thrown" until it hits something or 2 s pass. Its impacts are blamed on the thrower (`Threw`). Throwing an object that needs more hands than you have just drops it.

Status in build: Built.

## 5.6 Bruising & Goods Damage

Any impact on a held object, or on an object released less than 2 s ago, is converted to design impulse units and checked against the bruise threshold.

| Rule | Value |
|---|---|
| Bruise threshold `I_min` | 400 N·s |
| Bruise gain | `ΔBP = (I − 400) / 40 × Fragility × BruiseMult` |
| Cap per hit | 60 BP. Impulses above 20,000 are ignored as physics glitches |
| "Heavy impact" | Above 800 N·s (louder: noise 0.6 instead of 0.3) |
| Who is blamed | The last 3 people to touch it within 3 s, newest first (shares 0.6 / 0.3 / 0.1). Holding the object refreshes your touch every 1 s |
| Held object hits a player | Knocks them back (feeds into their ragdoll threshold) |

Example: dropping a pig from shoulder height gives about 1,200 N·s → 16 BP → Scuffed (−15% value). Bruise states and their value effects are in Chapter 6.

Status in build: Built.

## 5.7 Carry Stacking (proposed)

No source GDD defines stacking. Proposed rule for the owner to decide: a player carrying a **crate or basket** (Egg Crate, 2 hands) can place up to 6 Pocket items in it. The container then becomes one body, with a mass equal to the sum of its contents. Each hit on the container passes its bruise on to every item inside, at ×0.5. Stacking crates on top of each other is not allowed. Players use the wagon for that. The point is to reward loading a basket without letting a player carry 20 eggs safely. Today the Egg Crate is a single $30 item and does not hold anything.

Status in build: Planned (proposed).

## 5.8 Farm Tools

Tools are physical 1-hand objects that hang on barn racks. You can drop them, throw them or lose them in the river. The design gives each player 2 tool slots (switching takes 0.4 s). Using a tool takes one hand, so tools compete with carrying. Two-hand carries put the tool away automatically. In the current build you hold a tool like any other item, and RMB uses it.

| Tool | How it works | Key numbers | Build |
|---|---|---|---|
| **Net** | Hold RMB: a 0.5 s wind-up, then a swing. Catches one Small critter or Pocket item into a bag, which becomes a 1-hand item | Reach 1.5 m, 90° arc, 1.0 s cooldown. Catch chance `clamp(0.95 − 0.1 × Agility, 0.4, 0.95)` (chicken 55%). A caught animal has a 20% chance to escape every 5 s unless the bag is in a pen or wagon. Big Net: holds 3 | Built |
| **Feed bucket** | Hold RMB to shake. Makes food noise 0.4 every 0.5 s, which lures hungry animals within 15 m. Throwing feed makes a lure that lasts 20 s | 10 uses. Refills within 6 m of the barn | Built (shake). Thrown lure Planned |
| **Lasso** | Hold to aim with an arc preview, release to throw. A hit ties a 6 m rope. Hold to reel in; at 1.5 m you grab the animal automatically | Range 12 m. Rope breaks at 4,000 N. Pulling costs 10 stamina/s | Planned |
| **Whistle** | A short blow scares animals (noise 0.7). A long blow (1 s) rallies sheep and the guard dog | 3 s cooldown. Scaring a carried animal gives it +50% struggle for 2 s | Planned |

**Lasso tug-of-war**
```
player_anchor = 600 N × (stamina > 0 ? 1 : 0.4) × (helpers_on_rope + 1) × ground (grass 1.0, plank 0.8, mud 0.6)
dragged if critter_pull > player_anchor
```
Pull forces: chicken 50, goat 400, pig 500, cow 900, bull 1,600 N. One player can hold a pig. It takes three on grass to hold a bull. Otherwise you get the "lasso water-ski".

**Losing tools:** a tool that falls in the river washes up downstream after 30 s, or is lost. Lost tools are restocked each morning for **$15** each (Blame verb `LostTool`). Upgrades: Big Net, Reinforced Lasso, Golden Bucket, Handheld Lantern (Chapter on upgrades).

Status in build: Partial. Net and feed bucket are Built. Lasso, whistle, tool slots and thrown feed are Planned.

## 5.9 Ropes (Tethers)

A rope connects two bodies with a cable: 6 m for a lasso, 3 m for a hitch point. It breaks at 4,000 N. You can tie a rope to a wagon, a post, an animal or another player. If one end of the rope is in the river, the current pulls on it and can drag the other person in.

Status in build: Planned.

## 5.10 Wagons & Vehicles

| Vehicle | Spec | Build |
|---|---|---|
| **Wagon** | 120 kg, 4 wheels, bed 2.0 × 1.2 m, 0.4 m walls. Soft cap 400 kg of cargo (Bigger Wagon: 550/700/850) | Planned |
| **Wheelbarrow** (Alpha) | 1 player, 1 wheel, 80 kg, tips at 30° | Planned |
| **Raft** (Post, Riverlands) | Drifts with a 1.5 m/s current, poled by players, carries 250 kg | Planned |

**Wagon rules**
- **Moving it:** there are 2 handle grips at the front and 2 push points at the back. Each player adds 900 N. The front handle steers (axle up to 35°). A brake lever (1 hand) locks the wheels.
- **Top speed:** `v_max = clamp(Σforce / (total_mass × k), 0, 6) m/s`, where k is dirt 1.5, mud 3.0, plank 1.2. Example: 2 players with 220 kg on dirt reach 5.45 m/s; in mud, 2.7 m/s.
- **Cargo:** cargo sits loose in the bed. The target is about 1 item bouncing out per 100 m of rough road at 5 m/s. A **tarp** (1 hand, 1.5 s to tie) locks the cargo in place but costs 10% speed. The tarp is free in the VS build.
- **Tipping:** the wagon tips past 50° of roll and spills everything. Two players need 2 s to set it upright.
- **Wheels:** 100 HP each. Hits above 1,500 N·s deal impulse/50 damage. A wheel at 0 HP falls off. Repair costs $50, or use a Field Repair Kit.
- **Animals:** a hitched animal pulls with its own pull force toward whatever *it* wants. A rider can lead it with the feed bucket.
- **River:** a wagon that falls in floats for 10 s and then sinks. It is returned next morning for $40.
- **Blame:** cargo that bounces out logs `SpilledCargo` against the driver (or the last pusher if nobody is driving). A tip logs `TippedWagon`. A player riding in the bed counts as 70 kg of cargo.

Status in build: Planned. The Muddlebury slice moves goods by hand and with the market pen.

## 5.11 Physics Comedy Rules

These rules keep the chaos funny instead of frustrating.

1. **Readable cause:** every failure has a visible trigger: wobble, slip distance, a creak, the 0.4 s alert "tell". Nothing happens off-screen.
2. **Wobble on purpose:** the hold point shakes with `amp = 0.05 m × (1 + 2 × speed_ratio) × (1 + struggle)` at 1.3 Hz. Faster and more panicked means more wobble.
3. **Cheap bodies, expensive cargo:** falls cost seconds. Bruises cost dollars.
4. **Damage has a ceiling:** at most 60 BP per hit, so one bounce cannot destroy a Pristine pumpkin.
5. **A name on everything:** a 3-second touch history means the Ledger always has someone to blame, or "Nobody" or "the Pumpkin" when that is funnier.
6. **No griefing friends:** you cannot grab or lasso a teammate who is awake. Throwing a knocked-out friend is allowed and logged.
7. **The server decides:** the server runs all physics. Clients predict hand poses only.

## 5.12 Acceptance Criteria

- 2 players cannot lift a 6-hand cow. 3 players can.
- If 1 of 4 carriers lets go of the cow, it drops within 0.5 s.
- A 2 kg item thrown at full charge travels at least 15 m on flat ground.
- Every hit above 400 N·s on a goods item produces exactly one bruise with the correct instigator (50-drop automated test).
- A single player nets a chicken in 3 attempts or fewer on average.
