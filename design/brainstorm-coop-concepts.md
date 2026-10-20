# Co-op Concept Brainstorm — Creative Director Pass

**Brief:** Online co-op for 2–4 friends, Unity. Emergent chaos, proximity-voice comedy, physics, clips you want to share, short sessions that make you want one more run. Small team: stylized art, marketplace assets, systemic content instead of handcrafted content.
**Techniques used:** verb-first ideation, "what's the friendship moment?" test, constraint inversion (turn a limitation into the core mechanic).
**Status:** Brainstorm. The user makes the final call.

---

## Concept 1 — HAUL ASS (Furniture Movers From Hell)
- **Hook:** You're a cheap moving crew that has to get one fragile, cursed heirloom out of collapsing houses that are different every run, before the house notices.
- **Core loop:** 1) Get a job with a client's wishlist and payout. 2) Breach a procedurally assembled house and find the items. 3) Carry them out with physics (couches take 2 people, pianos take 4) while the house fights back (doors slam, stairs fold, floors tilt). 4) Get paid minus damages, then buy better dollies, straps and a truck.
- **Friendship moment:** Two of you are pivoting a couch down a spiral staircase while yelling "LEFT. MY LEFT." The third opens a door and the piano slides into all of you.
- **Replayability:** Modular house kits, randomized cursed-item modifiers ("gets heavier when unwatched"), house personalities, and a rising client difficulty.
- **Unity tech:** PhysX joints for multi-carry, scripted destruction, procedural generation for room kits, proximity voice chat.
- **Scope:** **M**

## Concept 2 — LAST SIGNAL (Radio Tower Repair Crew)
- **Hook:** You climb strange antenna towers in a storm to fix the signal, and the only way to talk across distance is the radios you are repairing.
- **Core loop:** 1) Drop at the base of a randomly generated tower. 2) Climb with stamina and rope while carrying parts. 3) Fix junction puzzles that need people on different floors at the same time. 4) Get off the tower before the lightning hits.
- **Friendship moment:** You're tied to your friend by rope. They slip and you hold them with your last bit of stamina, then the radio crackles: "...I dropped the fuse."
- **Replayability:** Procedural towers, a weather system, and cross-floor puzzles that recombine.
- **Unity tech:** Cable components and physics ropes, VFX Graph storms, URP lightning flashes, a radio-filter voice DSP.
- **Scope:** **M/L** (climbing feel is hard to get right, and PEAK owns a lot of this space)

## Concept 3 — SOUP OR DIE (Monster Kitchen)
- **Hook:** You run a kitchen that feeds a giant sleeping monster, and you have to catch the live ingredients yourselves.
- **Core loop:** 1) The monster announces a craving made of procedural traits (spicy, wriggly, loud). 2) Hunt live ingredients that run, fight back and mutate. 3) Cook with physics stations that can explode. 4) Serve it, and if the monster is unhappy it eats a cook.
- **Friendship moment:** You're holding a screaming carrot over the pot and your friend lights the stove too early.
- **Replayability:** Trait-combo recipes, ingredient AI ecosystems, and mutation chains.
- **Unity tech:** State-machine AI for critters, PhysX physics, VFX Graph.
- **Scope:** **M** (comes close to Overcooked, so its identity is at risk)

## Concept 4 — THE BLIND HEIST (Asymmetric Info Co-op)
- **Hook:** One player can see the security systems, one can see the loot and one can see the guards, but nobody sees everything, so you have to talk.
- **Core loop:** 1) Each player gets a different "lens". 2) Plan by voice. 3) Infiltrate with physics loot and improvised distractions. 4) Escape and split the take (betrayal is optional).
- **Friendship moment:** "There's no laser there." It turns out there was.
- **Replayability:** Lens assignments rotate, building layouts are procedural, and the guard AI varies.
- **Unity tech:** Per-player post-process and custom depth rendering, custom ability components, procedural generation.
- **Scope:** **M** (balancing the lenses for 2 vs 4 players is risky)

## Concept 5 — DUCT TAPE DIVERS (Scrap-Built Submarine)
- **Hook:** You build a submarine out of physics junk, dive into a procedural trench for salvage and keep it afloat with duct tape.
- **Core loop:** 1) Snap scrap into a sub (hull, engine, lights). 2) Dive, pilot and salvage. 3) Fix leaks while creatures circle. 4) Surface with loot and upgrade the parts catalog.
- **Friendship moment:** Water is up to your knees. One friend is plugging a leak with their body and another is outside in a diving suit when the sub drives off.
- **Replayability:** Emergent builds, procedural trenches and creature ecologies.
- **Unity tech:** Modular physics assembly, water shader, fluid-volume simulation (expensive), PCG.
- **Scope:** **L** (building plus water plus netcode is heavy)

---

## Ranking

| Rank | Concept | Fun ceiling | Clip-ability | Originality | Indie scope | Notes |
|---|---|---|---|---|---|---|
| 1 | **HAUL ASS** | High | Very high | High | Good | Physics carrying is a clip machine on its own |
| 2 | THE BLIND HEIST | High | High | Very high | OK | Strong hook but fragile balance |
| 3 | SOUP OR DIE | Med-High | High | Medium | Good | Too close to Overcooked |
| 4 | DUCT TAPE DIVERS | Very high | High | Medium | Poor | Best game for a bigger team |
| 5 | LAST SIGNAL | Medium | Med | Low-Med | Poor | Overlaps with PEAK |

## Recommendation: **HAUL ASS**

The core verb, carrying heavy awkward things together, is funny before any content exists, which makes it the cheapest to prototype and the most likely to hold up. It runs on marketplace furniture and house kits, PhysX physics does most of the comedy work, and "the house fights back" gives us endless systemic variety without handcrafted levels. Nothing in the current market owns cooperative physical carrying as a core loop. R.E.P.O. touches it but treats it as a side mechanic.

### Design Pillars
1. **Everything Is Heavy Together.** Valuable objects need coordination between bodies. If one player can solve something alone, it isn't core.
2. **The House Is a Player.** The environment reacts, escalates and interrupts through systems, never scripts.
3. **Failure Is Content.** Breakage, drops and deaths should be funnier than success. Damage reports, ragdolls and replays all reward chaos.
4. **Jobs in 15 Minutes.** Every run is short, readable and ends with a payout screen that pulls you into the next job.

### 2-Week Vertical Slice
**Goal:** Prove that carrying stuff together over proximity voice is fun in a single house.

- **Week 1: Feel**
  - Listen-server multiplayer for 2–4 players using the Unity Netcode for GameObjects with Steam sessions.
  - Grab and carry with physics handles. Objects get weight classes (1/2/4 people) and carry speed scales with how many hands are on them.
  - Proximity voice through EOS Voice or the built-in VOIP with attenuation.
  - Ragdoll on big impacts, and toss/drop.
- **Week 2: Loop**
  - One hand-assembled house built from marketplace kits (two floors, a staircase, a narrow door). PCG comes after the slice.
  - 6–8 items, each with a value and fragility (Physics break threshold reduces the payout).
  - Two house behaviors: doors that slam and a tilting floor, triggered by a simple escalating "anger" meter.
  - Truck zone, payout screen with an itemized damage report, and a restart.
- **Success criteria:** In a 4-person playtest, players laugh unprompted within 3 minutes, and at least 3 of 4 say "again" without being asked.
- **Out of scope:** upgrades, procedural houses, cursed modifiers, progression, art polish.
