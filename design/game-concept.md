# Game Concept: HARVEST PANIC

**Status:** Final concept, revision 2: reworked into a long-form game (Creative Director). The user makes the final call.
**Engine:** UE 5.8 on Apple Silicon (Mac). Online co-op for 2–4 players with proximity voice.

## Hook
Your crew inherited a cursed, debt-ridden farm from Grandpa. The Landlord, a smiling man in a too-tall hat, wants his money weekly. Round up feral livestock and runaway crops, haul them across the rickety bridge to market, and survive the nights when the farm wakes up. You can pay off the debt, but you'll never escape the chaos.

## Market Research (2025–2026 viral co-op)
| Game | What makes streamers laugh | What keeps people playing for weeks |
|---|---|---|
| **R.E.P.O.** | Physics-wobbly carrying of fragile loot, monsters interrupting, friends breaking each other's loot | Escalating quotas, upgrades, levels that scale in difficulty (a "Moon Phase" system in v0.2), and big updates every few months: Museum, Monster with 10 enemies, and Cosmetics |
| **PEAK** | Ragdoll climbing failures, rescuing friends, food and status-effect chaos | Procedural daily mountain, cosmetics and badges, very cheap ($7.99), 61% player overlap with R.E.P.O. |
| **Lethal Company** | Proximity voice cutting off when someone dies, monster jump scares, mimics | Quota loop, moons that get harder, a huge modding scene |
| **Schedule I** | Absurd systems run by friends, NPC chaos | Long empire-building progression with expanding territory and automation. Streamer-driven (caseoh_ alone had 745K hours watched) |
| **Chained Together** | Physics tether, one person's mistake drops everyone, blame | Leaderboards and the "we'll get it this time" climb |
| **Content Warning** | Filming your friends' failures is the actual goal | Viral footage shared outside the game |
| **Meccha Chameleon** | Hide-and-seek by painting yourself to blend in | Called 2026's biggest "friendslop" hit |
| **Gamble With Your Friends** (2026) | One shared bank account, so blame and betrayal | 1M copies in a week |

**Takeaways:**
1. Physics plus proximity voice plus failure is the comedy engine. Success is boring and failure is the clip.
2. A shared resource (quota, bank, tether) creates blame, and blame is funny.
3. Quotas that escalate, combined with an "a little further" goal, give the one-more-run pull. Long-term retention comes from meta progression and updates every 2–4 months.
4. Low price ($8–15), low hardware requirements and a goofy tone are the formula. Streamers drive discovery.
5. Procedural or daily variation replaces handcrafted content.
6. Light horror or monsters add spikes of panic between comedic moments.

**Sources:** [Alinea Analytics, Steam June 2025](https://alineaanalytics.com/blog/steam_june_2025/) · [Alinea on streamer-friendly co-op](https://alineaanalytics.substack.com/p/last-week-was-wild-on-steam-and-not) · [Stream Hatchet, Schedule I](https://streamhatchet.com/blog/schedule-is-success-live-streaming-in-q1-2025/) · [Gfinity, Schedule I charts](https://www.gfinityesports.com/article/schedule-i-beats-inzoi-and-mh-wilds-steam-top-charts) · [R.E.P.O. on Wikipedia](https://en.wikipedia.org/wiki/R.E.P.O.) · [Automaton, R.E.P.O. v0.2](https://automaton-media.com/articles/newsjp/repo-20250627-346311/) · [Playday, R.E.P.O. patch notes](https://playday.one/2025/10/30/repo-patch-notes/) · [The Sixth Axis, R.E.P.O. roadmap](https://www.thesixthaxis.com/2025/11/07/semiwork-wants-your-input-for-the-next-r-e-p-o-update/) · [GosuGamers, Gamble With Your Friends](https://www.gosugamers.net/entertainment/news/78409-gamble-with-your-friends-sells-one-million-copies-in-a-week) · [Insider Gaming, best friendslop](https://insider-gaming.com/best-friendslop-games-you-need-to-play/)

## Long-Term Structure

### Layer 1: The Day (10–15 minutes, the core loop)
1. **Morning:** read the market order and the day's twist (weather or a curse).
2. **Round up:** chase fleeing livestock and crops (AdaptivePathfinding) with nets, lassos and lures.
3. **Haul:** load the wagon or carry by hand across the bridge to market. Bruises reduce value.
4. **Dusk return:** an optional bonus haul at night, which is when Night Creatures appear.

### Layer 2: The Week (one session, 1–2 hours)
- Five days, then **Landlord Sunday**: pay the weekly quota or lose. Quotas escalate the way they do in Lethal Company and R.E.P.O.
- Between days, spend money on farm upgrades (a bigger wagon, an electric fence, a bridge reinforcement, a guard dog that can also cause chaos).
- **Failing a week resets the save, but you keep account-level unlocks.** It works like a roguelite, so each "season" is one attempt at the debt.

### Layer 3: The Debt Campaign (weeks to months of play)
- Paying off the farm takes about 4–6 weeks of in-game Sundays per **Region**.
- Each paid region unlocks the next **market town** and biome, each with new species, curses and Landlord twists. Biomes are built from the owned packs plus free Quixel content, reskinned and rearranged with PCG:
  1. Grandpa's Farm (Harvest pack plus the Bridge pack)
  2. Misty Riverlands (Bridge/Nature pack used as the main map, with rafts)
  3. Haunted Orchard (night-heavy, owned assets with a fog and lighting pass)
  4. and more after launch
- **Meta unlocks:** cosmetics (hats, overalls, tractor paint), new tools, and an almanac to fill with every species, curse and bruise type.
- **Endgame:** "Eviction Mode" ladders, where the quota scales without limit, a daily seed and a leaderboard.

## Chaos Sources (systemic and stackable)
- **Livestock personalities:** chickens scatter, pigs bulldoze, goats climb roofs, bulls charge whoever is wearing red, and sheep flock-follow the panicking player.
- **Cursed crops:** turnips that burrow, screaming mandrakes (loud sounds attract night creatures), pumpkins that grow while you carry them, and a corn maze that rearranges.
- **Night Creatures (light horror):** the Scarecrow moves only when nobody is looking, the Barn Thing drags players into hay, and Crows steal shiny tools.
- **Weather:** mud slows you, wind tips carried cargo, a storm can wash out the bridge, and fog limits sight to proximity voice only.
- **Rival Farmers (NPC):** they steal from your wagon, sabotage the bridge, and you can steal back.
- **Landlord Events:** surprise inspections ("all cows must be wearing hats"), quota doubles, and the Landlord personally walking the farm.

## Comedy Systems (built to produce streamable moments)
1. **Shared Wallet and Blame Ledger:** the end-of-day screen itemizes who bruised what ("Dave: dropped pig into river ×3, −$240"), and the Most Valuable Disaster award goes to the worst player.
2. **Proximity voice with livestock:** loud players spook animals and attract night creatures, so whispering and screaming both matter.
3. **Physics carrying and tethers:** carrying with several hands, wagon hitches and lasso ropes connect players in a Chained Together style, so one person falls and everyone falls.
4. **The Bridge:** planks break, the bridge sways, and the river spits you back out downstream. It is the clip stage every day.
5. **Grandpa's Camcorder:** a built-in replay that auto-saves the last 30 seconds before every disaster, with one-click export for TikTok and Shorts. It is free marketing in the spirit of Content Warning.
6. **Ragdolls everywhere:** kicked by a cow, launched by a bull, or carried away by crows. Death is temporary (respawn at the barn for a fee), so the comedy never becomes punishing.
7. **Optional betrayal:** secretly pocket a golden egg, which shows up in the Blame Ledger at the end of the week.

## Pillars (revised)
1. **Everything Has Legs.** Every valuable thing fights to escape.
2. **Heavy Together, Blamed Together.** Shared loads, a shared wallet and shared failure.
3. **The Farm Gets Worse, You Get Better.** Escalating quotas and curses against your growing toolkit.
4. **Failure Is the Content.** Every disaster is funny, recorded and cheap to recover from.

## Phased Roadmap (small team)
| Phase | Duration | Scope |
|---|---|---|
| **Vertical slice** | 2 weeks | See below: one day loop, the pig, the bridge, payout |
| **Prototype to Alpha** | about 3 months | The full Week/Landlord loop, 5 species, 3 cursed crops, the Scarecrow, weather, upgrades, Blame Ledger, Camcorder, Steam lobbies |
| **Early Access launch** (around $9.99) | about 6–8 months | Region 1 complete, 8 species, 2 night creatures, Rival Farmer, 30 upgrades, cosmetics, almanac, Eviction Mode |
| **Update 1: Riverlands** | EA +2–3 months | Region 2, rafts, new species and curses |
| **Update 2: Haunted Orchard** | EA +5–6 months | Region 3, night-focused, 3 new creatures |
| **Ongoing** | | Seasonal events (Harvest Fest, Halloween), cosmetics, community-voted creatures, mod support |

Content scales by **combining systems** (species × curse × weather × Landlord event × PCG layout), not by authoring levels.

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
