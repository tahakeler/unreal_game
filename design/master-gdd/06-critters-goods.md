# 6. Livestock, Crops & Goods

> Sources: `design/gdd/livestock-roster.md`, `critter-ai.md`, `goods-value.md`, `cursed-crops.md`, `consistency-report.md` (goods-value prices are canonical); code `AI/*` (`CritterTuning`, `CritterState`, `CritterAssetBuilder`), `Economy/Logic/EconomyTuning.cs`, `Economy/Editor/EconomyAssetBuilder.cs`.
> Pillar: **Everything Has Legs**.

## 6.1 Design Intent

"The pig is smarter than us." Anything worth money is trying to leave. Animals are cheeky but readable opponents. They show a clear tell before they bolt, they double back past the slowest friend, and they come running for the feed bucket. You win by out-thinking them as a team: flank them, lure them, stay quiet. Raw reflexes are not enough. Each species is a different puzzle, so mastery means knowing *which friend handles which animal*.

Every species is defined purely by data: a personality asset plus a goods entry. Adding a new animal needs no new code.

Status in build: Built (data-driven framework; 2 species authored).

## 6.2 Livestock Roster

Speeds are in m/s. For comparison, the player walks at 4.0 and sprints at 6.5. Prices follow the canonical goods-value table.

| Species | Tier | Personality | Walk / sprint | Base $ | Hands (carriers) | Fragility | Signature behaviour | Best counter |
|---|---|---|---|---|---|---|---|---|
| **Pig "Hamlet"** | VS | Greedy, sneaky, dramatic | 2.8 / 5.6 | 60 | 2 (1) | 0.8 | Squeezes under fences. Squeals (noise 0.7) when grabbed | Feed bucket (0.9 attraction); drive it into a corner |
| **Chicken** | VS | Skittish, flock | 2.5 / 6.2 | 15 | 1 (1) | 0.6 | A flock of 6–10 bursts in every direction and regroups after 15 s | Net; approach quietly |
| **Goat "Gary"** | Alpha | Curious climber | 3.0 / 5.2 | 70 | 2 (1) | 0.5 | Climbs roofs, wagons and bridge rails. Headbutts goods out of your hands (600 N·s) | Lasso; let it come to you |
| **Cow "Big Martha"** | Alpha | Lazy, stubborn | 1.5 / 3.0 | 150 | 6 (3) | 0.7 | Refuses to move. Moos every 20 s (noise 0.5) | Feed bucket (0.95); push as a team |
| **Bull "Sir Reginald"** | Alpha | Aggressive guard | 2.5 / 6.5 | 200 | 6 (3) | 0.4 | Guards the cows. Charges (1,800 N·s = KO) at sprinters within 10 m or anyone in red; 6 s cooldown | A decoy friend; lasso plus 2 more players |
| **Sheep** | EA | Timid herd-follower | 2.6 / 4.8 | 80 (wool 20) | 2 (1) | 0.6 | The flock follows the bell-wether leader | Grab the leader; long whistle |
| **Duck** | EA | Smug swimmer | 2.2 / 4.0 (swim 5.0) | 35 | 1 (1) | 0.6 | Heads for water and is immune to soaking | Raft; net it from the bridge |
| **Prize Llama "Doris"** | EA | Judgmental spitter | 2.7 / 5.0 | 400 | 4 (2) | 0.9 | Spits at the loudest player within 8 m (2 s screen splat, 10 s cooldown) | Speak softly (loudness under 0.3) |

### Personality fields (key values)

| Species | Alert radius (m) | Noise sensitivity | When cornered | Size | Agility | Pull (N) | Players needed to hold securely | Struggle (N·s) |
|---|---|---|---|---|---|---|---|---|
| Pig | 10 | 1.0 | Juke | Medium | 3 | 500 | 2 | 150 |
| Chicken | 8 | 1.4 | Juke | Small | 4 | 50 | 1 | 40 |
| Goat | 9 | 0.7 | Charge | Medium | 4 | 400 | 1 | 120 |
| Sheep | 12 | 1.2 | Freeze | Medium | 2 | 350 | 1 | 80 |
| Cow | 6 | 0.5 | Freeze | Large | 0 | 900 | 3 | 200 |
| Bull | 14 | 0.8 | Charge | Large | 2 | 1,600 | 3 | 300 |
| Duck | 9 | 1.1 | Juke | Small | 3 | 80 | 1 | 40 |
| Llama | 10 | 1.5 (voice ×2) | Play dead | Large | 1 | 600 | 2 | 150 |

**Mass in the build:** pig 30 kg (critter prefab) or 40 kg (goods prop), chicken 2.5–5 kg. Planned: cow 300 kg, bull 900 kg (both used in the bridge load calculation).

**Spawning:** each day, the number of animals that spawn is the morning order count × 1.25, rounded up, so there are spares to lose. If a gate is opened, each penned animal has a 60% chance to leave (cow 30%, ×0.4 with the Electric Fence).

**Region critters (Post/Expansion, stub data):** Walking Catfish, Goose, Otter, Black Cat, Bat Swarm (Regions 2–3); Longhorns, Roadrunner (9 m/s), Armadillo, Yak, Penguin, Reindeer (Regions 4–5).

Status in build: Partial. Pig and Chicken are Built with the values above. Goat, Cow and Bull (Alpha) and Sheep, Duck and Llama (EA) are Planned.

## 6.3 Critter AI States

The server runs every animal brain at **10 Hz** and sends the current state to clients as a single byte.

| State | Entered when | Behaviour | Leaves when |
|---|---|---|---|
| **Idle** | No threat | Stands or grazes for 2–5 s | It decides to wander, or a threat or lure appears |
| **Wander** (graze) | The idle timer ends | Walks to a random point within 6 m of home at 50% speed | A threat or lure appears |
| **Alert** | Threat ≥ 0.3 (chicken 0.25) | Faces the threat for a **0.4 s tell** (ears up, sound sting) | Threat ≥ 0.55 → Flee. Below 0.3 for 3 s → Idle |
| **Flee** (panic) | Threat ≥ 0.55 (chicken 0.45) | Runs to points 12 m away from the combined threat direction, rechecking every 0.25 s. Uses shortcuts players can't (pig under fences) | Calms down, gets cornered or gets grabbed |
| **Calm** | Threat below 0.15 | Winds down for 4 s (chicken 3 s) | Back to Idle, or a new threat |
| **Lured** | Lure attraction beats threat × 0.8 | Walks to the food and eats. Ignores threats below 0.5. A food noise is remembered for 8 s | Food is gone, eating finishes, or threat jumps above 0.8 |
| **Cornered** | The escape path is more than 3× the straight distance and 2 or more players are within 4 m | Its personality response for 2–5 s: Juke (dash at 1.15× sprint), Freeze, Charge or Play Dead | The timer ends or it is grabbed |
| **Carried** | Grabbed | Struggles every 0.5–1.5 s and may break free | Dropped or thrown → Stunned. Delivered → sold |
| **Stunned** | Thrown, dropped or hit | Dizzy: 1.5 s (0.6 s after a drop, 0.25 s after breaking free) | The timer ends |
| **Escaped** | Reaches an off-map escape area | Despawns. Its value is lost and the last toucher is blamed with `LetEscape` | — |

**Breaking free while carried:**
```
P_break per struggle = clamp((SecureCarriers − carriers) × 0.20 + 0.05, 0, 0.9)
```
For a pig held by one player, that is 25% per struggle, so it breaks free after about 3–5 s on average. Held by two players, it never breaks free. (The GDD used 0.35 per missing carrier. The build uses 0.20 to hit the 3–5 s target, and the build value is canonical.) When it breaks free, it hops away at 3 m/s.

**Stuck animals:** if an animal makes no progress for 3 s, it looks for a new path. After 8 s, it is warped to a nearby walkable point. When more than 40 animals are active, the ones far from all players update less often.

**Flocks (chickens):** a scatter sends each bird outward with ±50° of random spread. Birds within 12 m join the flock, and the flock regroups within 3 m after 15 s.

**Planned:** the sheep herd that follows its leader and the bull's guard behaviour. The bull guards cows (the "Guard" mode).

Status in build: Partial. Every state except Escaped volumes, Follow and Guard is Built (`CritterBrain`, `CritterFlock`, `ThreatTracker`). Escape areas and sheep and bull herding are Planned.

## 6.4 Perception & Noise

**Threat from each source:**
```
T = clamp(0.5 × proximity + 0.2 × sprinting + 0.15 × in_sight + noise + 0.6 × damage, 0, 1)
proximity = 1 − distance / AlertRadius;  sprinting = horizontal speed > 5.2 m/s inside the radius
noise = loudness × sensitivity × source_mult × (1 − distance / noise_radius)   (decays 0.25/s)
damage decays 0.3/s
Total threat = the highest source + 0.25 × the sum of the others
```
Example: a player sprints within sight, 4 m from a pig: 0.3 + 0.2 + 0.15 = 0.65, so the pig flees. Sneaking up quietly at walking pace scores 0.3 + 0.15 = 0.45, which only puts the pig on Alert.

| Noise | Loudness |
|---|---|
| Walk footstep / sprint footstep / crouch | 0.10 / 0.25 / none |
| Holler, shout emote | 0.8 |
| Wave | 0.6 |
| Pig squeal when grabbed | 0.7 |
| Light / heavy impact | 0.3 / 0.6 |
| Feed bucket shake (food tag: **lures**, never adds threat) | 0.4 |
| Whistle short / long (planned) | 0.7 |
| Plank crack | 0.3 |
| Proximity voice | Scales with how loudly you speak |

Sight cone: 220° (chicken 300°). A player who stands still and silent may draw a curious animal closer (pig curiosity 0.2). That is a real stealth-catch tactic.

Status in build: Built (voice-driven noise depends on proximity voice, which is Planned).

## 6.5 Crops & Goods Catalogue

| Goods | Category | Base $ | Fragility | Hands (carriers) | Tier | Build |
|---|---|---|---|---|---|---|
| Egg | Product | 5 | 2.0 | 1 (1) | VS | Built |
| Egg Crate | Product | 30 | 1.5 | 2 (1) | VS | Built |
| Carrot | Crop | 8 | 0.8 | 1 (1) | VS | Built |
| Turnip | Crop | 10 | 1.0 | 1 (1) | Alpha (in build early) | Built |
| Prize Pumpkin | Crop | 300 | 1.5 | 8 (4), 180 kg | VS | Built |
| Golden Egg | Special | 150 (never decays) | 2.0 | 1 (1) | Alpha | Planned |
| Mandrake | Cursed crop | 45 | 1.2 | 1 (1) | Alpha | Planned |
| Growing Pumpkin | Cursed crop | 40 × 1.05^(seconds held/10), capped at 2.5× | 1.3 | 2→8 | Alpha | Planned |
| Golden Cob (Maze Corn) | Cursed crop | 40 | 1.0 | 1 (1) | EA | Planned |
| Tumble Melon | Cursed crop | 80 | 1.4 | 4 (2) | EA | Planned |
| Moonberries | Crop | 15 | 1.8 | 1 (1) | EA | Planned |
| Wool | Product | 20 | 0.3 | 1 (1) | EA | Planned |

Livestock values are in §6.2.

## 6.6 Value Rules: Bruises & Freshness

**Sale value** (fixed at the moment the market accepts the item, which takes 2 s):
```
V = round(BaseValue × Bruise × Freshness × Demand(0.6–1.5) × Order(1.5 if it fills an order line) × Modifiers (Dusk ×1.25))
```
A Pristine Prize Pumpkin that fills an order at normal demand sells for **$450**.

| Bruise state | Bruise points | Value multiplier |
|---|---|---|
| Pristine | under 10 | 1.00 |
| Scuffed | 10–34 | 0.85 |
| Bruised | 35–64 | 0.60 |
| Busted | 65–99 | 0.30 |
| Ruined | 100 | Crops: 0 (sells as "compost" for $1). Livestock: 0.25, and the animal turns **Grumpy** (flees 1.5× faster and gets more aggressive) |

- Bruise points come from hits (Chapter 5) and from water. Fully under water: crops take 15 BP/s, livestock 5 BP/s, ducks 0. The water penalty is called `Soaked`.
- Livestock heal 5 BP every 30 s while in a pen. Crops never heal.
- **Freshness:** `F = clamp(1 − max(0, t − grace)/decay, 0.5, 1)`. For crops, grace is 120 s and decay is 480 s, so a crop hits the 0.5 floor 10 minutes after harvest. For products, grace is 180 s and decay is 600 s. Livestock never lose freshness. Decay pauses at Dusk.
- Unsold goods left in the field at day end despawn, unless they are stored in the Barn Cellar upgrade.
- Every value loss of $2 or more creates one Blame Event naming who caused it.

Status in build: Built (bruise, freshness, sale, compost, Grumpy tag). Grumpy movement mods and pen healing are Partial.

## 6.7 Cursed Crops (Alpha/EA)

Cursed crops are crops that fight back. They are the "Everything Has Legs" pillar applied to vegetables.

| Crop | Curse |
|---|---|
| **Mandrake** | Screams when pulled from the ground. The scream is a crop noise heard up to about 43 m and stuns anyone within 3 m for 1 s. The Earmuff Hat protects you |
| **Growing Pumpkin** | Keeps growing while it is held. Its value rises ×1.05 every 10 s, but it also needs more hands (2→8). If it grows past what you can hold, it pins you, and the blame goes to "Pumpkin" |
| **Golden Cob** | Hidden inside the maze-corn field |
| **Tumble Melon** | Rolls away downhill |

Region curses come later (e.g. Hiding Apples in the Orchard).

Status in build: Planned.

## 6.8 Acceptance Criteria

- A pig flees a sprinting player within 0.4–0.6 s and gets at least 8 m away in open ground in 90% of runs.
- A holler within 10 m sends every skittish animal into Flee. A walk footstep does not.
- Feed shaken 8 m from a calm pig lures it within 5 s.
- One player holding a pig loses it after 3–5 s on average. Two players never lose it.
- Dropping a pig with a 1,200 N·s impact gives 16 BP (Scuffed).
- A crop's freshness is 0.75 at 240 s and 0.5 from 600 s onward.
