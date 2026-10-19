# HARVEST PANIC: Master Game Design Document

> **Status**: Draft v0.1 · **Created**: 2026-10-10 · **Engine**: UE 5.8 (Apple Silicon Mac)
> **Source**: design/game-concept.md (rev 2) · **Systems**: design/gdd/systems-index.md
> This hub links every system GDD. Detailed rules, formulas and tuning live in the per-system docs.

## Summary
A 2–4 player online co-op physics comedy. Your crew inherits Grandpa's cursed, debt-ridden farm. Every day you chase down runaway livestock and living crops, haul them across a terrible bridge to market, and pay the Landlord every Sunday. The goods fight back, the weather fights back, the night fights back, and your friends drop the pig in the river.

**Camera:** **first-person** (user decision, 2026-10-11). Other players see your full body. See `character-customization`, `frontend-lobby`, `social-comedy-features`.

**Genre:** co-op "friendslop" physics comedy with roguelite debt loops · **Price:** ~$9.99 premium · **Platform:** PC/Mac (Steam) · **Session:** 10–15 min days, 1–2 h weeks.

## Player Fantasy
"We are four incompetent heirs barely keeping a haunted farm afloat, and it is everyone's fault but mine."

## Pillars
1. **Everything Has Legs.** Every valuable thing fights to escape. *Test: if an item just sits there, give it a personality.*
2. **Heavy Together, Blamed Together.** Shared loads, a shared wallet and shared failure. *Test: does this make players need or blame each other?*
3. **The Farm Gets Worse, You Get Better.** Escalating quotas and curses against your growing toolkit.
4. **Failure Is the Content.** Every disaster is funny, recorded and cheap to recover from. *Test: is losing here a clip, or just a chore?*

**Anti-pillars:** no punishing permadeath of the character, no combat-centric gameplay (you herd, you don't fight), no grind for its own sake, no pay-to-win.

## Target Audience
- **Primary:** friend groups aged 16–30 who play R.E.P.O., PEAK, Lethal Company and Chained Together on voice chat.
- **Secondary:** streamers and content creators who need reliable laugh moments roughly every 3 minutes.
- **Tertiary:** cozy farm fans who want a chaotic twist (Stardew/Overcooked crossover).
- **Hardware:** runs on base M-series Macs and mid-range PCs, with stylized, light rendering (no Nanite/Lumen dependency).

## Loops
### Core loop: The Day (10–15 min) → `day-loop`
Morning order + twist card → **Round up** (nets, lassos, lures vs. fleeing goods) → **Haul** (carry/wagon across the bridge; bruises cut value) → **Sell** at the market → optional **Dusk bonus** haul (night creatures wake) → **Payout + Blame Ledger**.

### Session loop: The Week (1–2 h) → `week-quota`, `farm-upgrades`, `landlord-events`
Five days, then **Landlord Sunday**: meet the quota or the run ends. Between days, spend the shared wallet on upgrades. Weekly escalation: quota × ~1.35 per week (tuned in `week-quota`) plus one new Landlord rule.

### Campaign loop: The Debt (weeks–months) → `region-campaign`, `meta-progression`
5 paid weeks (tuning knob 4–6) clears a **Region**, unlocks a market town, biome, species and curses. A failed week resets the region run, but account unlocks (cosmetics, tools added to the starting pool, almanac) persist.

### Endgame → `eviction-mode`
Endless quota ladder on a daily seed with global and friends leaderboards.

## Regions
| # | Region | Market Town | Signature mechanic | Phase |
|---|---|---|---|---|
| 1 | Grandpa's Farm | Muddlebury | The Bridge, basic livestock | EA |
| 2 | Misty Riverlands | Ferrytown | Rafts, river currents, fish that walk | Update 1 |
| 3 | Haunted Orchard | Hollow Hill | Night-heavy, fruit that hides, three new creatures | Update 2 |
| 4* | Sunburnt Ranch | Dustwater | Tumbleweeds, stampedes, heat that spoils goods | Expansion idea |
| 5* | Frostbite Fjord | Icemouth | Slippery ice physics, yaks, frozen bridges | Expansion idea |

## Progression and Economy Overview
- **One shared wallet** (`shared-wallet-economy`). Income comes from market sales × freshness × bruise multiplier × market demand. Sinks are upgrades, respawn fees (default $25, scaling per week), repairs, Landlord fines and the Sunday quota.
- **Run progression:** farm upgrades (~40 at EA): bigger wagon, electric fence, bridge reinforcement, guard dog (which chases livestock *and* players), scarecrow decoys, a feed silo.
- **Account progression:** "Grandpa's Letters" unlock from milestones (first region cleared, 50 pigs delivered, 10 bridge collapses). They give cosmetics, new starting tool options and almanac pages. No stat power is ever sold.
- **Almanac:** collect every species, curse, bruise type and disaster. This is the completionist hook.

## Creature Roster
### Livestock (`livestock-roster`)
| Species | Personality | Phase |
|---|---|---|
| Pig | Flees, squeezes through gaps, rolls in mud (slippery) | VS |
| Chicken | Scatters as a swarm, flaps over fences | VS |
| Goat | Climbs roofs and the wagon, headbutts | Alpha |
| Cow | Heavy (3 carriers / 6 hands), stubborn, refuses to move | Alpha |
| Bull | Charges anyone wearing red or sprinting, knocks players out | Alpha |
| Sheep | Flock follows the bell-wether; catch the leader and the flock follows | EA |
| Duck | Swims; the river is home turf | EA |
| Prize Llama | Spits at loud players; very valuable | EA |

### Cursed Crops (`cursed-crops`)
Prize pumpkin (4-person, fragile, VS) · Burrowing turnip · Screaming mandrake (loud, attracts night creatures) · Growing pumpkin (gets heavier while carried) · Rearranging corn maze.

### Night Creatures (`night-creatures`)
Scarecrow (moves only when unobserved) · Barn Thing (drags players into hay) · Crows (steal shiny tools and coins).

### Human antagonists
Rival Farmers (`rival-farmers`) steal from wagons and saw bridge planks, and you can steal back. **The Landlord** (`landlord-events`) is an event system and not a creature: inspections, quota doubles and farm walks.

## Chaos Sources (stackable)
Livestock personalities × cursed crops × night creatures × weather (mud, wind, storm washes out the bridge, fog leaves you with voice only) × rival farmers × Landlord events × PCG layout. Every chaos source is a **Day Modifier** with explicit stacking rules (see systems-index contracts). The director caps simultaneous "major" modifiers at 2 (+1 per week, max 4) so chaos stays readable.

## Comedy Systems
1. **Shared Wallet & Blame Ledger** (`blame-ledger`): an itemized blame list ("Dave: dropped pig into river ×3, −$240") and the *Most Valuable Disaster* award.
2. **Proximity voice with consequences** (`proximity-voice`): screaming spooks animals and summons night creatures, so whispering becomes gameplay.
3. **Physics carry and tethers** (`physics-carry`): multi-hand carrying, hitches and lassos. One falls, all fall.
4. **The Bridge** (`bridge-hazards`): the daily clip stage.
5. **Grandpa's Camcorder** (`camcorder-replay`): an auto-saved 30 s buffer before every disaster, with one-click vertical export.
6. **Ragdolls everywhere** (`player-character`): death is a cheap barn respawn.
7. **Optional betrayal:** pocket a golden egg; the ledger reveals it on Sunday.

## Multiplayer Model
- 2–4 players, **listen server** (host authoritative), Steam lobbies with invite and join-in-progress at any time except the Sunday payout cutscene (`session-networking`). Solo is playable with scaled quotas (not a focus).
- **Proximity voice:** EOS Voice with 3D attenuation, plus a radio upgrade for long-range channels. Voice loudness feeds the Noise Event system. A non-mic fallback ("Holler" and "Shush" buttons) is required.
- Physics replication favors a "host-simulated, client-predicted grab" model. Clips beat precision, so acceptable desync is tuned for comedy rather than competitive accuracy (details in `session-networking`, `physics-carry`).
- Host leave: the run auto-saves at day boundaries and the checkpoint is replicated to all roster players, so any of them can host the Resume ("migration-lite"). No live migration at EA.

## Monetization
- **Premium ~$9.99** (launch discount and a 4-pack friend bundle).
- **Cosmetics only** (hats, overalls, tractor paint, wagon decals, voice-filter "megaphones"). Most are earned in-game. Optional paid supporter packs post-launch contain cosmetics only.
- No loot boxes, no paid currency, no gameplay power for sale. Free content updates (regions) drive retention and press.

## Roadmap
| Phase | Duration | Scope | Key systems |
|---|---|---|---|
| Vertical Slice | 2 weeks | Pig, chickens, prize pumpkin, wagon, bridge plank, day loop, payout | Foundation + Core |
| Prototype→Alpha | ~3 months | Week/Landlord loop, 5 species, 3 curses, Scarecrow, weather, upgrades, Ledger, Camcorder, Steam lobbies, saves | Feature layer |
| Early Access | ~6–8 months | Region 1 complete, 8 species, 3 night creatures (Scarecrow, Barn Thing, Crows), Rival Farmers, ~40 upgrades, cosmetics, almanac, Eviction Mode | Region, meta, polish |
| Update 1: Riverlands | EA +2–3 mo | Region 2, rafts, new species and curses | |
| Update 2: Haunted Orchard | EA +5–6 mo | Region 3, 3 new creatures | |
| Ongoing | | Seasonal events, community-voted creatures, mod support | |

## Expansion Ideas (beyond the concept)
*Not committed. These are candidates for updates and events, and each one plugs into an existing system contract.*

**New creatures**
- **The Tax Goose:** honks at whoever holds the most money and steals 10% of their carry value. Can only be distracted by bread.
- **Mimic Melon:** looks like a prize melon and grows teeth on the bridge. Nods to Lethal Company mimics.
- **Union Bees:** a hive that goes "on strike" if you take too much honey. A picket line blocks the barn door.
- **Grandpa's Ghost:** appears at night and gives terrible advice in old-man VO ("Pigs love being thrown"). Sometimes he is right.
- **Escape Artist Alpaca:** picks gate latches and opens the pens for everyone else.
- **Moonlight Moles:** pull an entire carried crop underground. Players must yank it out tug-of-war style.
- **The Hen Queen:** a boss chicken. Every chicken in range follows her, so catching her catches the swarm.

**Events and twists**
- **Livestock Union Day:** every animal demands a hat before it agrees to be sold (pairs with the hat inspection).
- **Gravity Hiccup (curse):** low gravity for 60 s, and everything floats off the bridge.
- **County Fair Sunday:** instead of paying, enter a pig race, pumpkin weigh-in or sheep beauty pageant to halve the quota.
- **The Landlord's Nephew:** an intern who "helps" by re-labelling crates so they sell at the wrong stall.
- **Blood Moon Harvest:** a night-only day with triple value and every creature active.
- **Influencer Visit:** an NPC with a phone wants a "content moment". Bigger disasters earn tips.

**Tools**
- **Leaf-blower:** herds chickens and blows teammates off the bridge.
- **Pig Cannon:** fires goods across the river. Accuracy: poor. Bruising: yes.
- **Duct Tape:** patches planks and wagons, and can also tape a teammate to the cow.
- **Decoy Grandma:** an inflatable that lures night creatures, and sometimes your friends.
- **Megaphone:** a long-range shout that spooks everything in a cone.

**Modes**
- **Bridge Builder Party Mode:** 10-minute arcade sessions on one hazard-laden bridge. Good for streams.
- **Saboteur Week (2–4 players):** one secret saboteur is paid by the Landlord, and the Blame Ledger becomes an accusation vote.
- **Farm vs Farm (Versus, post-launch):** two duos on opposite banks share one bridge and steal each other's goods.
- **Daily Disaster:** a fixed seed with a fixed absurd modifier stack and a global clip-of-the-day vote from Camcorder uploads.
- **Kid Mode / Cozy Mode:** no night creatures, softer quotas, for family streams.

## System GDD Hub
See design/gdd/systems-index.md for all 28 systems, dependencies, priorities and the 4 authoring batches.

## Open Questions
1. Can the Mac build run EOS voice on UE 5.8 arm64? This is a blocker for the proximity-voice pillar and should be spiked first.
2. Livestock asset availability (highest-risk art gap).
3. Solo mode: supported tier or "technically possible"?
4. ~~Should betrayal (golden egg) be opt-in per lobby?~~ Resolved: lobby toggle `BetrayalEnabled`, default ON (`blame-ledger`).
