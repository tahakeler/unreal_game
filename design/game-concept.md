# Game Concept: HARVEST PANIC

**Status:** Final concept recommendation (Creative Director). The user makes the final call.
**Engine:** UE 5.8 on Apple Silicon (Mac). Online co-op for 2–4 players.
**Derived from:** `brainstorm-coop-concepts.md`. This is a mutation of HAUL ASS (physics carrying, chaos for damages, payout loop) and SOUP OR DIE (live ingredients that fight back), rebuilt around the owned assets.

## Re-evaluation Against Owned Assets

| Concept | Fit with Harvest Farm + Bridge + Pathfinding | Verdict |
|---|---|---|
| HAUL ASS | The farm reskin works and the farm buildings fit. The bridge is only set dressing. | Mutate |
| SOUP OR DIE | Fleeing livestock and crops are an ideal use of AI pathfinding, and the farm kit fits. Cooking pushes it toward Overcooked. | Mutate |
| THE BLIND HEIST | Needs indoor security assets that we don't own. | Drop |
| DUCT TAPE DIVERS | Needs water and submarine assets. Nothing we own fits. | Drop |
| LAST SIGNAL | Towers aren't covered. The bridge pack helps only a little. | Drop |

**Merge:** keep HAUL ASS's physics carrying and payout loop, keep SOUP OR DIE's living, fleeing goods, and use the bridge as the chokepoint every run has to cross.

## Hook
Your crew of broke farmhands has to round up feral livestock and runaway crops on a farm that changes every run, then haul them across one rickety bridge to the market before sundown.

## Core Loop (one 12–15 minute run, called a "Market Day")
1. **Order.** The market posts a manifest, for example "3 pigs, 1 prize pumpkin (4-person carry), 6 chickens, 0 bruises."
2. **Round up.** Chase, corner and grab animals and crops that flee with smart pathfinding, using nets, lassos and buckets of feed as lures.
3. **Haul.** Load a physics wagon or carry by hand across the bridge chokepoint. Cargo wriggles, struggles and escapes.
4. **Sell and upgrade.** Get paid by value minus a bruise penalty, then spend the money on tools (better nets, wagon parts, a scarecrow decoy). The next Market Day has nastier modifiers.

## Clip-Worthy Friendship Moment
Halfway across the swaying bridge, four friends are carrying a prize pumpkin while a pig escapes out of the wagon. Someone lets go to dive for it. The pumpkin tips, the wagon rolls, and the whole crew, the pumpkin and the pig ragdoll into the river while the last friend screams "THE PIG IS STILL WORTH MONEY" over proximity voice.

## Why It's Replayable
- **Systemic critters:** each species has a flee personality: chickens scatter, pigs bulldoze, goats climb roofs, and the turnips burrow. The pathfinding plugin means they really outsmart you.
- **Daily modifiers:** rain (mud slows you down), stampede, a "golden goose" bonus target, a broken bridge plank, or a fox that steals cargo.
- **Layout variation:** farm props and fences are rearranged with PCG on a fixed map footprint, so every run uses a new herding geometry with no new art.
- **Manifest combinations** plus tool upgrades make each day a different puzzle.

## Design Pillars
1. **Everything Has Legs.** Every valuable thing fights to escape. Nothing sits still waiting to be collected.
2. **Heavy Together.** The best loot needs coordinated carrying, so solo players earn less than a team.
3. **The Bridge Is the Climax.** Every run funnels through one dramatic chokepoint, which is where the clips happen.
4. **Failure Is Funny, Never Fatal.** Drops, river dunks and ragdolls cost money, not fun. Runs are short and restarts are instant.

## Owned Asset Coverage

| Need | Covered by |
|---|---|
| Farm map: barns, silos, fences, fields, crops, props | **Stylized Harvest Farm Environment** (Emberlight Creations) |
| Bridge chokepoint, river, cliffs, foliage, transition biome | **Ready Map Vol 2 – Bridge Environment / Nature** (Six Sides Studios) |
| Critter AI: fleeing, cornering, climbing, avoiding dynamic obstacles | **AdaptivePathfindingSystem v2.06** (NordVader), layered under StateTree flee/lure behaviors |
| Physics carrying, wagon, ragdolls, bridge sway | UE 5.8 Chaos Physics with physics constraints (engine built-in) |
| Prop scatter and layout variation | UE PCG framework (engine built-in) |

## Gaps to Fill (free Fab/Quixel or engine content)

| Gap | Plan |
|---|---|
| Player characters | A stylized farmer character from the free Fab content, or the UE Mannequin with hats and overalls. Must match the stylized art direction. |
| Player animations | UE Game Animation Sample (free) or Lyra animation sets, with motion matching. Add custom carry poses using Control Rig and IK. |
| Livestock meshes and animations (pig, chicken, goat, cow) | Free stylized animal packs on Fab. **This is the highest-risk gap** and must be checked first. |
| Living crop "creatures" | Reuse the farm pack's crop meshes with procedural legs, or squash-and-stretch with a vertex wiggle material. No new rig needed. |
| Audio: animal SFX, footsteps, ambience, UI | Free Fab audio packs and Quixel/Fab freebies. MetaSounds for bruise/impact variation. |
| Proximity voice | EOS Voice with 3D attenuation, or Steam with UE VOIP. Check Mac support. |
| Tools (net, lasso, feed bucket) | Free Quixel props plus simple kitbash. Lasso uses the Cable component. |
| UI | UMG with a stylized font from Google Fonts. |

**Mac risk notes:** before committing, confirm on Mac that the pathfinding plugin compiles for UE 5.8 (arm64) and that EOS voice works. Avoid Nanite-only or Lumen-heavy setups on lower-end Macs and keep the art stylized and light.

## 2-Week Vertical Slice
**Goal:** Prove that chasing fleeing animals and carrying them across the bridge together is fun with 2–4 players on proximity voice.

**Week 1: Feel**
- Listen-server session for 2–4 players with proximity voice.
- Grab, carry and throw with physics handles, where carry speed scales with the number of hands. Ragdolls on big impacts.
- One critter (the **pig**) uses AdaptivePathfinding to flee from players, take shortcuts, and get lured by a feed bucket.
- One physics wagon.

**Week 2: Loop**
- One map: a farm area from the Harvest pack joined to the bridge and river from the Ready Map pack (hand placed, no PCG yet).
- Two more goods: **chickens** (scatter swarm) and a **prize pumpkin** (4-person carry, fragile).
- A manifest, a sundown timer, the market zone, and a payout screen with an itemized bruise report and instant restart.
- One bridge hazard: a plank that breaks when overloaded.

**Success criteria:** In a 4-player playtest, players laugh within 3 minutes, at least one shareable bridge disaster happens without prompting, and at least 3 of 4 players ask to go again.
**Out of scope:** upgrades, modifiers, PCG layouts, extra species, progression, polish.
