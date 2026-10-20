# Appendix: Glossary & Document Index

## A. Game Terms

| Term | Meaning |
|---|---|
| **Almanac** | Account-wide collection of every species, curse and bruise type a player has seen |
| **Barn respawn** | Where knocked-out or drowned farmers return; costs a respawn fee from the shared wallet |
| **Blame Event** | Record that a player (or Nature) cost the crew money: who, what verb, which item, how much |
| **Blame Ledger** | Host-side list of Blame Events that prints the itemised receipt and awards |
| **Bruise** | Damage points on goods from impacts; reduces sale value |
| **Carry hands** | How many hands an item needs to be lifted (1, 2, or several players) |
| **Cellar** | Barn storage for unsold goods carried between days (planned) |
| **Critter** | Any living goods that runs: livestock and cursed crops |
| **Critter Personality** | Data asset defining a species' behaviour (fear, speed, flocking, traits) |
| **Cursed Crops** | Crops with behaviours: burrowing turnips, screaming mandrakes, growing pumpkins, shifting corn maze |
| **Day Modifier** | A rule card (weather, twist, Landlord rule) that changes tuning for a day, week or run |
| **Design units** | Impact strength used by designers = Unity impulse × 10 (bruise 400, ragdoll 900, KO 1800) |
| **Dinner Bell** | Lobby Yard bell the host rings to start the day |
| **Dusk** | Optional late phase after sundown: bonus haul, night danger |
| **Eviction** | Failing to pay on Sunday; the run ends |
| **Eviction Mode** | Endless quota ladder with daily seed and leaderboard (planned) |
| **Farm Memories (FM)** | Meta currency for cosmetics |
| **Freshness** | Crop value that decays during the Workday |
| **Grandpa's Camcorder** | Rolling replay that saves the 30 s before each disaster (planned) |
| **Grandpa's Letters** | Milestone achievements that unlock content |
| **Grandpa's Tapes** | Playable tutorial morning (planned) |
| **IOU** | Debt when fees would push the wallet below −$200; added to the week's quota |
| **Landlord** | Smiling antagonist; adds weekly rules, inspections and deals; never attacks |
| **Landlord Sunday** | Week end: pay the quota or be evicted |
| **Lobby Yard** | Walkable farmyard where the crew gathers before a day |
| **Loadout** | A farmer's cosmetics: body, colours, hat/face/back items, name-tag colour, emotes |
| **Lost & Found pile** | River spit-out point downstream where fallen things wash up |
| **Market Pen** | Sale zone in the market square; goods inside are sold |
| **Morning Order** | The day's market request and twist announcement |
| **Most Valuable Disaster (MVD)** | Award to the player with the highest weekly blame |
| **Muddlebury** | Region 1 farm, bridge and market town; the current playable level |
| **Night Creatures** | Light-horror threats after dark: Scarecrow, Barn Thing, Crows (planned) |
| **Noise Event** | A sound with loudness and radius that animals and creatures react to |
| **Payout** | End-of-day receipt: sales, fees, blame |
| **Pocketing** | Secretly keeping a valuable item (betrayal), revealed at the week's end |
| **Quota** | Money owed on Landlord Sunday; rises each week |
| **Ragdoll / Knockout** | Floppy physics fall from big impacts; knockout needs a revive or barn respawn |
| **Region** | Campaign chapter (about 5 paid weeks) unlocking a new town and biome |
| **Rival Farmers** | NPC thieves and saboteurs (planned) |
| **Run** | One attempt at a region's debt; wiped on eviction |
| **Shared wallet** | The crew's single pool of money |
| **Twist deck** | Daily draw of weather and oddity cards |
| **Wagon** | Physics cart for hauling many goods (planned) |
| **Workday** | Main timed phase: round up, haul, sell |

## B. Technical Terms

| Term | Meaning |
|---|---|
| **asmdef** | Unity assembly definition; a separately compiled code module |
| **Batchmode** | Running Unity from the command line without the editor window |
| **Contract** | Shared interface or data type in `HarvestPanic.Core` that teams use to talk |
| **EditMode / PlayMode test** | Automated tests of pure logic / of running scenes |
| **Facepunch.Steamworks** | C# library for Steam lobbies, invites and networking |
| **Host** | The player whose game is also the server and owns the truth |
| **Host migration-lite** | On host loss, resume from the last saved day instead of live hand-over |
| **LFS** | Git Large File Storage, for binary assets |
| **NGO** | Netcode for GameObjects, Unity's multiplayer library |
| **NetworkVariable / RPC** | Replicated value / remote function call |
| **NavMesh** | Walkable-area map used for critter pathfinding |
| **Services** | Core registry systems use to find each other |
| **Two-process test** | Automated host + client run on one machine proving multiplayer works |
| **URP** | Universal Render Pipeline, Unity's lightweight renderer |

## C. Source Document Index

### System GDDs (`design/gdd/`)

| Document | One-line description |
|---|---|
| [game-overview](../gdd/game-overview.md) | Original master overview: premise, pillars, loops |
| [systems-index](../gdd/systems-index.md) | All 31 systems, layers, priorities, dependencies |
| [session-networking](../gdd/session-networking.md) | Hosting, joining, authority, bandwidth, host loss |
| [player-character](../gdd/player-character.md) | Movement, stamina, ragdoll, knockout, respawn |
| [physics-carry](../gdd/physics-carry.md) | Hands, multi-player carry, throw, tethers |
| [proximity-voice](../gdd/proximity-voice.md) | Positional voice and the Noise Event contract |
| [critter-ai](../gdd/critter-ai.md) | Shared brain for everything that runs or hunts |
| [goods-value](../gdd/goods-value.md) | Base value, bruising, freshness |
| [farm-tools](../gdd/farm-tools.md) | Net, lasso, feed bucket, whistle |
| [wagon-vehicles](../gdd/wagon-vehicles.md) | Wagon, hitching, wheelbarrow, rafts |
| [day-loop](../gdd/day-loop.md) | Day phases, order, timer, payout; Day Modifier owner |
| [shared-wallet-economy](../gdd/shared-wallet-economy.md) | Wallet, faucets, sinks, economy model |
| [bridge-hazards](../gdd/bridge-hazards.md) | Breakable bridge, river, mud |
| [livestock-roster](../gdd/livestock-roster.md) | Species data and signature behaviours |
| [week-quota](../gdd/week-quota.md) | Five days + Landlord Sunday, escalation, eviction |
| [farm-upgrades](../gdd/farm-upgrades.md) | Between-day shop and upgrades |
| [cursed-crops](../gdd/cursed-crops.md) | Crops that run, grow and scream |
| [night-creatures](../gdd/night-creatures.md) | Night Director and light-horror creatures |
| [weather-twists](../gdd/weather-twists.md) | Twist deck: weather and oddity cards |
| [rival-farmers](../gdd/rival-farmers.md) | NPC thieves, sabotage, steal-back |
| [landlord-events](../gdd/landlord-events.md) | Weekly rules, inspections, bargains |
| [blame-ledger](../gdd/blame-ledger.md) | Blame Events, receipts, awards, betrayal reveal |
| [camcorder-replay](../gdd/camcorder-replay.md) | Disaster replay buffer and clip export |
| [region-campaign](../gdd/region-campaign.md) | Regions, towns, seeded layouts |
| [save-persistence](../gdd/save-persistence.md) | Run Save and Account Profile |
| [meta-progression](../gdd/meta-progression.md) | Letters, cosmetics, Almanac |
| [eviction-mode](../gdd/eviction-mode.md) | Endless endgame ladder |
| [hud-menus](../gdd/hud-menus.md) | Diegetic-first HUD, receipts, shop UI |
| [audio-feedback](../gdd/audio-feedback.md) | Impacts, critter barks, music states |
| [onboarding-accessibility](../gdd/onboarding-accessibility.md) | Tutorial tapes, voice-free play, accessibility |
| [character-customization](../gdd/character-customization.md) | Loadout slots, wardrobe, emotes |
| [frontend-lobby](../gdd/frontend-lobby.md) | Title flow, Lobby Yard, ready-up, pause |
| [social-comedy-features](../gdd/social-comedy-features.md) | Small bounded-harm comedy features |
| [consistency-report](../gdd/consistency-report.md) | Cross-GDD consistency check results |

### Other sources

| Document | Description |
|---|---|
| `design/game-concept.md` | Concept, market research, roadmap |
| `design/levels/muddlebury-farm.md` | Muddlebury layout and flow |
| `design/art/art-bible.md`, `character-style.md`, `asset-gap-list.md` | Visual direction and asset needs |
| `design/audio/sound-bible.md`, `sfx-list.md`, `music-brief.md` | Audio direction |
| `docs/architecture/unity-contracts.md` | Contracts, ownership, layers |
| `docs/engine/unity-setup.md` | Install, layout, batchmode, Git rules |
| `THIRD_PARTY_LICENSES.md` | Asset licences |
| `tools/net/two-process-test.sh` | Multiplayer smoke test |
