# Weather & Daily Twists

> **Status**: Draft · **Slug**: `weather-twists` · **Layer**: Feature · **Priority**: Alpha · **Batch**: B
> **Consumes**: Day Modifier (day-loop), Critter Personality modifiers (critter-ai), bridge-hazards hooks

## 1. Overview
Each morning a **twist deck** draws one weather card and, from Day 3, possibly one oddity card. Each card is a Day Modifier bundle that changes physics, sight, critters and hazards. Twists exist to make the same farm feel new and to generate disasters.

## 2. Player Fantasy
"It's raining. The pig will be muddy, the bridge will be slick, and we're doomed." Reading the forecast and adapting is a small strategic layer before the chaos.

## 3. Detailed Rules
### 3.1 Weather cards
| Card | Effects | Critter effects | Counter | Weight |
|---|---|---|---|---|
| **Clear** | none | none | — | 30 |
| **Mud Day** | Mud volumes radius ×1.5 and extra patches (mud rules owned by bridge-hazards: speed ×0.6, ragdoll threshold ×0.8), sprint stamina ×1.3 cost, slip chance on turn 10% | Pig speed ×1.15 in mud ("in its element"); clean-wool bonus impossible | Boards, wagon | 20 |
| **Gusty** | Wind vector 4–12 m/s, rotates every 60 s; carried goods get torque; chickens drift when flapping | Chickens fly 2× farther | Carry low, heavier hands | 15 |
| **Storm** | Rain + lightning every 45–90 s (strikes tallest object; ragdoll + 50 $ bruise); river rises: `Bridge.SafeLoadMult` ×0.7 after minute 6; possible bridge washout at minute 10 (one segment) | All critters NoiseSensitivity ×0.6 (thunder masks voices) | Finish early; Rope Rail Netting upgrade | 10 |
| **Fog** | Visibility 15 m; HUD markers off; proximity voice is the only nav aid | Critter AlertRadius ×0.6; Scarecrow sight rule shortened | Call-and-response, bells on animals | 10 |
| **Heatwave** | Freshness decay ×1.5 for crops; stamina regen ×0.8 | Cows Freeze in shade 50% more | Wagon trips | 10 |
| **Full Moon** (night-only) | Night budget ×1.5 | Nocturnal traits active from minute 8 | Leave before dusk | 5 |

### 3.0 Weights
The Weight column is the default deck. A Region Profile (`region-campaign`) overrides weights per region and may add region cards (§3.4).

### 3.2 Oddity cards (Day 3+, 40% chance)
Greased Pig (pig grab break ×2), Chicken Jamboree (+8 chickens, half price), Goat Convention (3 goats), Mandrake Bloom, Upside-down Gravity Hour (Post, 20 s low gravity), Grandpa's Ghost Helps (one free lure).

### 3.4 Region weather cards (Post stubs, from `region-campaign`)
**Drizzle** (Region 2: light Mud Day + Noise env_mult 0.9), **Flood** (Region 2: low fields flood at 2/3 day), **Blood Moon** (Region 3: Full Moon variant, night budget ×2). Defined with their region update.

### 3.3 Deck rules
Weighted draw; no weather repeats more than 2 days in a row; Storm/Fog not on Day 1; Landlord days may force a card (landlord-events). Eviction-mode uses daily-seeded draw.

## 4. Formulas
- **Wind torque on carried good** = `0.5 × ρ(1.2) × v² × A_item × Cd(1.0)`; e.g. 10 m/s on a 0.6 m² pumpkin ≈ 36 N side force.
- **Lightning target** = tallest actor in a 30 m random disc; players holding a metal tool count +1 m.
- **Bridge washout probability** at minute 10 = 0.35 (Storm only).
- Modifiers apply as Day Modifiers `Weather.*`, stack multiplicatively with upgrades.

## 5. Edge Cases
- Fog + Scarecrow: guarantee one lantern spawn near barn.
- Storm washout while players on that segment: 2 s creak warning, then fall to river spit-out.
- Join-in-progress: client receives current twist state immediately.
- Gusty + pumpkin: wind never alone exceeds the carry break force for a full team.

## 6. Dependencies
day-loop (Day Modifier, morning announcement), bridge-hazards (break thresholds, washout), critter-ai (personality modifiers), physics-carry (wind force), night-creatures (Full Moon, fog), goods-value (freshness), landlord-events, region-campaign (biome weights), eviction-mode (seed), audio-feedback.

## 7. Tuning Knobs
Card weights; wind range (4–12 m/s); lightning interval (45–90 s); washout chance (0.35); fog distance (15 m); oddity chance (40%).

## 8. Acceptance Criteria
1. Each card's effects are fully defined as Day Modifiers with no hard-coded checks in other systems.
2. Same seed → same twist sequence.
3. Storm washout is preceded by a visible/audible warning ≥ 2 s.
4. Playtesters correctly name the active weather and one effect after day 1 (≥ 80%).
5. No weather combination makes the order impossible (automated sim of 500 seeds succeeds with a perfect bot).

## Comedy beats
- Lightning striking the tallest player, who was holding the pitchfork.
- Chickens kiting away in the wind.
- Fog: "Marco!" "Polo!" "...that wasn't me."
