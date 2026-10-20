# 11. World, Characters & Levels

> **Owner:** narrative-director (with level-designer and world-builder input)
> **Sources:** `design/game-concept.md`, `design/gdd/region-campaign.md`, `landlord-events.md`, `night-creatures.md`, `rival-farmers.md`, `livestock-roster.md`, `cursed-crops.md`, `game-overview.md`, `design/levels/muddlebury-farm.md`, and the built scene from `HarvestPanic/Assets/Scripts/World/Editor/BuildMuddlebury.cs` (`Assets/Scenes/Muddlebury.unity`)
> **Engine:** Unity 6.3 LTS, URP, Netcode for GameObjects
> **Pillars served:** Everything Has Legs · Heavy Together, Blamed Together · The Farm Gets Worse, You Get Better · Failure Is the Content

This chapter covers where HARVEST PANIC takes place, who lives there, how story reaches the player without getting in the way of the pig, and how levels are laid out. Muddlebury Farm, the only level built so far, gets a zone-by-zone breakdown. The chapter closes with the rules every future region has to follow.

**Canon convention:** anything marked **(proposed)** is new in this chapter and is not yet in a system GDD. It needs the creative director's sign-off before the writer or world-builder treats it as canon.

---

## 11.1 Setting & Lore of the Valley

### 11.1.1 Core concept
A storybook farming valley where Grandpa's land has been cursed, mortgaged and generally mishandled for decades. You inherit all three problems at once.

### 11.1.2 The world in one paragraph
Muddlebury is a sleepy river town at the bottom of a green bowl of hills. On the west bank sits Grandpa's farm: a red barn, a windmill, a pig pen, a coop, and fields that don't stay put. On the east bank is the market square, with its cobbles, pastel houses, three stalls and a bronze bell. The only way between the two is one rope bridge with twenty planks. Nobody in town agrees on when it was last repaired. The farm is now yours, along with Grandpa's debt to the Landlord, a smiling man in a too-tall hat who collects every Sunday. Grandpa himself is gone. He has left behind letters, 1994 home-video tapes, a diary, a camcorder that never stops recording, and a lot of livestock with opinions.

### 11.1.3 World rules (what is possible and impossible)

| Rule | Implication for play | System owner |
|---|---|---|
| Everything valuable tries to escape | Pigs, chickens, turnips and pumpkins all flee, burrow, grow or scream | `critter-ai`, `cursed-crops` |
| Nobody dies permanently | Drowning, bull charges and night creatures all send you to the barn for a fee | `player-character`, `shared-wallet-economy` |
| The river always returns what it takes | Anything that falls in comes out soaked at the Lost & Found pile 120 m downstream | `bridge-hazards` |
| The Landlord never attacks | He fines, inspects, "helps" and opens gates. He is never a combat threat | `landlord-events` |
| The farm rearranges between weeks | "The farm moved overnight." The farm side reseeds every Monday. The bridge and market stay fixed | `region-campaign` |
| The night belongs to the farm | After sundown the creatures wake. The market and the bridge are safe zones for the Scarecrow | `night-creatures` |
| Noise is a resource | Shouting scares animals, attracts creatures and makes Cousin Clem faint | `proximity-voice` |

### 11.1.4 History (key events behind the current state)

| When | Event | How the player learns it |
|---|---|---|
| Decades ago | Grandpa buys the farm from the Landlord "on very reasonable terms" | Grandpa's Letters (`meta-progression`) |
| Unknown | The crops become cursed. Grandpa's diary blames, in turn, the goat, the moon, a "seed catalogue from the wrong address" and himself | Twist cards written as diary entries (`day-loop`) |
| 1994 | Grandpa records instructional tapes. He gets every instruction wrong and a younger-sounding Landlord corrects him off camera | Grandpa's Tapes tutorial (`onboarding-accessibility`) |
| Recently | Grandpa "retires" (left unspecified). The farm, the debt and the camcorder pass to the crew | Opening of Tape #1 |
| Now | Week 1 of the debt. The Landlord's first memo arrives, stamped FRIENDLY REMINDER | Morning order board |

### 11.1.5 The curse: deliberately unexplained (recommendation)
The cause of the curse is never stated. Every source that claims to explain it contradicts another source. This works for three reasons. It keeps the tone light, since a curse with a backstory starts to demand drama. It turns the Almanac and Letters into a running joke instead of a mystery the crew has to solve. And it leaves room for new cursed crops in any region without a lore retcon. **(proposed)** One recurring hook: the Landlord sounds exactly the same on the 1994 tapes as he does today, and his hat is just as tall. The game never comments on this.

### 11.1.6 Connections & player relevance
- **Economy:** the debt is the reason the crew works at all. The quota is the plot (`week-quota`).
- **Campaign:** paying off a region "frees" it, until the Landlord tears the deed in half and reveals the next property (`region-campaign`, Deed Day).
- **Meta:** lore arrives through unlocks (Letters, Almanac pages, cosmetics with flavour text such as Grandpa's Straw Hat: "Smells like Grandpa. Smells like regret.").

### 11.1.7 Contradictions check
- The "Eviction cutscene" and "Monday morning cutscene" in `region-campaign` are short vignettes under the storytelling rules in 11.2. They are not long cinematics. There is no conflict, but the word "cutscene" in that doc should read "vignette".
- `game-concept.md` still names an older engine and a vendor asset pack for the farm. The built Unity scene uses Kenney CC0 packs plus owner-generated Tripo set pieces. Lore and layout are unaffected.
- No other contradictions found against the listed sources.

**Status in build:** Partial. The physical setting (farm bank, river, bridge, market town) is built in `Muddlebury.unity`. No lore text (Letters, diary twist cards, tapes) is in the build yet.

---

## 11.2 Tone & Storytelling Approach

### 11.2.1 The tone in one line
"A storybook farm that gets dented" (art bible): warm, cosy and toy-like by day, and a bedtime story gone wrong by night. The writing matches that. It is dry, polite, bureaucratic and slightly unhinged.

### 11.2.2 Storytelling principles

| # | Principle | In practice |
|---|---|---|
| 1 | **The players are the story.** | Our narrative is scaffolding. The real story is "Dave dropped the pig in the river three times." The Blame Ledger and Camcorder are the main narrative systems. |
| 2 | **Never stop the chaos for story.** | No cinematic longer than 10 s. Every vignette is skippable by majority vote and never plays during a carry or within 10 s of a disaster. |
| 3 | **Environmental first.** | Story lives in props: the Lost & Found sign, the stamped memos on the order board, the patched planks, the wardrobe in the barn, Grandpa's hand-painted signposts. |
| 4 | **Diegetic text over UI text.** | Rules arrive as typed Landlord memos, twist cards as Grandpa's diary, unlocks as letters in shaky handwriting, tutorials as VHS tapes. |
| 5 | **Barks, not dialogue trees.** | NPCs speak in one-liners triggered by game state. No conversation wheels and no choices. Players choose with their hands, not menus. |
| 6 | **Light horror, never cruel.** | Night is scary-funny. Creatures knock out, drag or steal. Nobody bleeds and nothing is permanent. |
| 7 | **Text is short enough to read while running.** | Barks ≤ 10 words. Memos ≤ 25 words. Twist cards ≤ 15 words. |

### 11.2.3 Narrative delivery channels

| Channel | Format | Frequency | Owner system |
|---|---|---|---|
| Morning order board | Landlord memo + Grandpa diary twist card | Every morning | `day-loop`, `hud-menus` |
| Barks | 3D-positioned VO/gibberish plus subtitle | Event-triggered, rate-limited | `audio-feedback` |
| Grandpa's Tapes | VHS overlay, ≤ 8 s (Tape #1 is a playable 4-minute morning) | First encounter only | `onboarding-accessibility` |
| Grandpa's Letters | Letter UI at payout, P.S. joke | On milestone | `meta-progression` |
| Blame Ledger and Town Crier | Itemised receipt plus the crier reading yesterday's top line aloud | Every payout / morning | `blame-ledger` |
| Sunday montage | Top 5 camcorder tapes with Grandpa narration cards | Weekly | `social-comedy-features` |
| Vignettes | In-engine, ≤ 10 s: Monday "farm moved", Deed Day, Eviction | Run milestones | `region-campaign` |

### 11.2.4 Pacing across a run
- **Week 1:** a quiet introduction. No Landlord rules and no visits. Grandpa's voice dominates through tapes and twist cards.
- **Weeks 2–3:** the Landlord takes over the channel with memos, inspections and Farm Walks. Rivals start appearing.
- **Weeks 4–5:** the cast is complete and night gets longer and louder. The Double Down offer is the run's "temptation" beat.
- **Deed Day:** a celebratory release (no quota, 2× goods), then the punchline: the deed only covers "the *front* half of the farm."

### 11.2.5 Ludonarrative check
The story says "you are desperate, indebted amateurs" and the mechanics reward exactly that: a shared wallet, fees for failure, and rewards for risky dusk runs. There is one possible tension. Pillar 4 celebrates failure (Most Valuable Disaster) while the quota punishes it. That is intended. The debt is the straight man and the crew is the comedian. No change is recommended.

**Status in build:** Planned. Diegetic carriers exist as geometry (Lost & Found board, arch sign, signposts, barn wardrobe), and the lobby dinner bell already uses this voice ("Only the host can ring the bell. Ask them nicely."). Memos, diary cards, tapes, letters and barks are not implemented.

---

## 11.3 Key Characters

Voice lines below are **tone samples** that set direction for the writer. They are not final dialogue.

### 11.3.1 The Landlord

| Field | Detail |
|---|---|
| Role | Antagonist, the debt in human form. Hosts Landlord Sunday. |
| Look | Tall and thin, with a stovepipe hat that is 40% of his height. Royal plum (`#5B2A6E`) is his colour and the colour of every debt UI element. |
| Personality | Unfailingly polite and smiling. A cartoon bureaucrat. He believes he is helping. |
| Leitmotif | Tuba. Cheerful oompah on Sunday that shifts to the parallel minor when the crew is short (`MUS_Sunday_Landlord`). |
| Name | Always "the Landlord." **(proposed)** He never gives a name and the Almanac page reads "Name: On file." |
| Gameplay role | Event deck, not a creature: weekly rules (max 3 active), mid-week visits (Inspection, Farm Walk, Double Down, Helpful Visit). He never deals damage. Hitting him costs $50 for "dry-cleaning". If knocked into the river he respawns at his car after 10 s. |

**Voice samples**
- Arrival (Inspection): "Don't mind me. Just a friendly little look around."
- Fining a muddy player: "Lovely boots. Twenty-five dollars."
- Complimenting, then fining, a pig: "What a handsome animal. Hat violation, I'm afraid."
- Opening a pen gate on a Farm Walk: "Let me get that for you."
- Hit by a pumpkin (slow-motion hat fall): "...I'll add it to the account."
- Double Down offer: "Pay double now, rest next week. Think of it as a holiday."
- Sunday, quota met: "Splendid! Same time next week. Slightly more, of course."
- Sunday, quota missed: "Oh dear. Oh dear, oh dear. Pack light."
- Deed Day: "Congratulations! This covers the *front* half of the farm."

### 11.3.2 Grandpa (off-screen)

| Field | Detail |
|---|---|
| Role | Absent mentor. The voice of the farm and of every tutorial. |
| Presence | Letters, 1994 tapes, diary twist cards, the camcorder, signposts, the default straw hat. |
| Personality | Warm, confident and almost always wrong. His P.S. notes get more unhinged ("P.S. Never trust the goat"). |
| Gameplay role | Tutorials (Tape #1, plus 14 just-in-time tapes at EA), daily twist cards, milestone unlocks, Sunday montage narration. |

**Voice samples (written or VHS)**
- Twist card: "Storm today. Bridge will leave. Do not follow bridge."
- Tape: "Pigs love being carried by the ears." (Off camera: "They do not.")
- Letter P.S.: "If the scarecrow moves, it didn't."
- Monday note: "Farm moved again. Don't ask."

*Backlog idea (game-overview):* Grandpa's Ghost appears at night with terrible advice ("Pigs love being thrown"). Sometimes he's right. Post-EA only.

### 11.3.3 Mr. Stitches, the Scarecrow

| Field | Detail |
|---|---|
| Role | The signature night creature and the game's horror mascot. |
| Personality | Stalker. Patient and petty. |
| Rule | Moves only when nobody is looking: inside a player's view frustum within 40 m with line of sight, 15 m in fog. Teleports between field posts. Moves at 7 m/s when unobserved. |
| Counter | Keep eyes on him, assign a watcher, or use the Handheld Lantern upgrade (freezes him for 3 s). |
| Penalty | Knockout, carried goods drop, barn respawn fee. |
| Limits | Cannot enter the bridge or the market (safe zones). AFK stares don't count. |
| Voice | **None.** His only "lines" are sounds: a straw rustle when someone blinks, a creak of his post. Keeping him silent is a deliberate choice, because his horror comes from players narrating him ("He's closer. He's CLOSER."). |

**Environmental tells (proposed):** he appears in a different field post each night, his head tilts toward the loudest player, and the morning after a knockout his post holds a dropped hat.

**Supporting night cast:** **The Barn Thing** lives in the hay and drags a player in over 4 s; two rescuers can tug them back out. **The Crows ("The Committee")** are gossipy thieves that steal shiny tools and peck coins from the barn wallet sign. Shouting does help against them.

### 11.3.4 Rival Farmers

| Rival | Personality | Gameplay role | Voice samples |
|---|---|---|---|
| **Cousin Clem** | Sneaky, cowardly | Steals the most valuable small good from a wagon left unattended for 10 s. Faints if shouted at from under 3 m | "Just borrowin' it!" / (shouted at) "Not the face!" *thud* / (pig escapes him) "Come back, I had plans for you!" |
| **The Pruitt Twins** | Smug, coordinated | One lures your animals with a feed bucket while the other poaches. Confused if separated by more than 30 m | "Here, piggy piggy." / "Oh, was that *yours*?" / (separated) "Pruitt? ...PRUITT?" |
| **Mabel "The Saboteur"** | Aggressive, crafty | Loosens 2–3 bridge planks and greases the ramp. Slow walker | "Bridge looks a bit tired today." / (from far bank, waving) "Mind the gap!" |
| **Big Earl** (Post) | Bully | Shoves goods out of hands and steals the wagon itself. Distracted by pie | "That's a nice wagon. Was." / "...Is that pie?" |

Rivals never appear on Day 1 or in the tutorial. They leave at sundown because they fear the night creatures too. Their carts at the map edge are fair game: "steal back" is a core fantasy ("Oh no you DON'T, Clem").

### 11.3.5 Town Folk of Muddlebury

The market is the crew's daily stage audience. The townsfolk are **ambient and reactive**. They never give quests, and their barks comment on what the crew just did. Names are **(proposed)** working names pending the writer's pass.

| Character | Location | Personality | Gameplay role | Voice samples |
|---|---|---|---|---|
| **The Butcher** ("Hank") | North stall (130, 22) | Gruff, secretly sentimental about every animal | Livestock buyer. Bruise reactions | "Is it supposed to be that colour?" / "That pig's been through things." |
| **The Greengrocer** ("Mrs. Leek") | East stall (150, 0) | Fussy, judges produce like a pageant | Crop buyer. Freshness feedback | "Firm. Fragrant. Only slightly screaming." |
| **The Oddities Dealer** ("Madame Odette") | South stall (130, −22) | Theatrical; prices swing ±40% | Buys cursed goods. High volatility | "Ooh, it bit you? I'll pay extra." |
| **The Town Crier** | By the bell (134, 0) | Booming and pompous, with no tact | Reads yesterday's top Blame line aloud each morning. Gives the "Town Crier" award to the loudest screamer | "HEAR YE! DAVE DROPPED THE PIG IN THE RIVER! THRICE!" |
| **Townsfolk extras** (crowd) | Square, windows | Gawkers | React to sales and disasters with gasps, applause and boos (crowd audio bed) | "Ooooh." / "Again!" |

**Rule:** town folk never block the sale pen, never move goods and never become physics hazards. The market is the one place where the world is on the crew's side, apart from the bell (see 11.5.7).

**Status in build:** Planned. No character NPCs are in the build. The Landlord exists only as data: 5 of 8 weekly rules are authored as Day Modifier assets (`Mod_Landlord_EarlyCurfew`, `LandlordsCut`, `NoRunningOnBridge`, `QuietHours`, `RespawnPaperwork`). The three market stalls are built as static set pieces with no vendors.

---

## 11.4 The Region Campaign Map

### 11.4.1 Structure
The campaign is a linear chain of regions. Each one is a debt of **5 paid Sundays** (knob 4–6), followed by Deed Day, which unlocks the next region at account level for every participating player. Each region swaps the market town, biome, species pool, curses, night creature and one permanent **Landlord Twist**. The farm side is regenerated from a seed every Monday. The bridge and the market are fixed per region.

**Campaign map presentation (proposed):** Grandpa's hand-drawn map, coffee-stained and pinned to the barn wall in the Lobby Yard. Cleared regions get a stamped deed. Locked regions are covered by Landlord memos ("Not yet, friend"). Regions 4 and 5 stay hidden until they are announced. There are no "coming soon" teasers.

### 11.4.2 Region overview

| # | Region → Town | Ship | Biome | Signature hazard | RegionDifficulty | Unlock |
|---|---|---|---|---|---|---|
| 1 | Grandpa's Farm → **Muddlebury** | EA | Sunny, muddy Americana | The rope bridge | 1.00 | Default |
| 2 | Misty Riverlands → **Ferrytown** | Update 1 | Braided river, reed islands, drizzle | Rafts and tide | 1.20 | Clear R1 |
| 3 | Haunted Orchard → **Hollow Hill** | Update 2 | Autumn twilight orchard | The night (days are 40% dark) | 1.45 | Clear R2 |
| 4* | Sunburnt Ranch → **Dustwater** | Expansion idea | Red mesa desert | Heat spoilage and the stampede | 1.70 | Clear R3 |
| 5* | Frostbite Fjord → **Icemouth** | Expansion idea | Nordic fjord, ice | Ice physics and the drifting market | 2.00 | Clear R4 |

### 11.4.3 Region 1: Grandpa's Farm → Muddlebury
- **Theme:** golden afternoon, warm soil (`#A88A5A`), sun-bleached barn red. The tutorial region and the comedic home base.
- **Layout idea:** one farm plateau ringed by cover, one road to one bridge, one open market square. Everything funnels into the bridge.
- **Signature hazard:** the 20-plank rope bridge (breakable planks, sway, river spit-out).
- **Critters:** pig, chicken, goat, cow, bull, sheep, duck, prize llama.
- **Crops:** prize pumpkin, burrowing turnip, screaming mandrake, growing pumpkin, maze corn, tumble melon, moonberry bush.
- **Night:** Mr. Stitches, the Barn Thing, the Crows. **Rivals:** Clem, the Pruitt Twins, Mabel.
- **Town quirk:** the town bell rings at sundown −60 s and every critter in earshot panics toward it.
- **Landlord Twist, "Bridge Toll":** $5 per crossing with goods unless the crew owns Bridge Reinforcement.
- **Weather deck:** Clear 40 / Mud Day 25 / Gusty 20 / Storm 10 / Fog 5.

### 11.4.4 Region 2: Misty Riverlands → Ferrytown
- **Theme:** constant drizzle, stilt houses, reeds. The farm sits on three islands.
- **Layout idea:** island-hopping. A long rope bridge with two mid-river islands replaces the single span. Rafts are the main haul vehicle. The market can only be reached by a crank ferry that leaves every 90 s.
- **Signature hazard:** currents drag rafts, and the tide floods low fields at 2/3 of the day.
- **New critters:** Walking Catfish (flops toward water), Goose (honks and bites carriers), Otter (steals and swims off).
- **New crops:** Floating Gourds (drift unless weighted), Rice that Wanders (migrates at dusk).
- **Night:** the Bog Lantern lures the loudest player into deep water.
- **Landlord Twist, "Waterfront Property":** wet goods lose freshness 2× faster, and dry-delivered goods pay +20%.
- **Story beat (proposed):** Ferrytown's stallkeepers all owe the Landlord too. They're friendlier, and more desperate.

### 11.4.5 Region 3: Haunted Orchard → Hollow Hill
- **Theme:** crooked apple trees, lanterns and perpetual twilight. The horror-forward region.
- **Layout idea:** lantern-lit paths through dense tree rows. The covered bridge closes its doors at random, and the market gates shut at true dark (late sales pay 50%).
- **Signature hazard:** night itself. Falling apples bruise carried goods, and sinkhole graves open up.
- **New critters/crops:** Hiding Apples (play dead, sprint when unobserved, a nod to Mr. Stitches), Black Cat (−20% carry speed curse), Bat Swarm (night only), Jack-o'-Lanterns (relight and run), Whispering Hedges (×2 voice).
- **Night:** the Headless Picker, the Orchard Widow, the Cider Ghost.
- **Landlord Twist, "Night Shift":** dusk sales count 1.5×, day sales 0.9×.
- **Story beat (proposed):** Mr. Stitches' post is empty in this region. The Almanac says "On holiday."

### 11.4.6 Region 4*: Sunburnt Ranch → Dustwater (Expansion idea)
- **Theme:** red mesas and a dry gulch crossed by a rail trestle.
- **Layout idea:** a wide-open ranch with sparse shade props that matter (heat spoilage ×1.5 in sun). The trestle is the chokepoint, and the market train leaves every 4 min.
- **Signature hazard:** the Longhorn stampede, plus dust devils and tumbleweeds that pick up small goods.
- **New critters/crops:** Longhorns, Roadrunner, Armadillo, Cactus Fruit.
- **Night:** the Copycat Coyote mimics footsteps only, never voices.
- **Landlord Twist, "Water Rights":** troughs cost money, and thirsty livestock run 1.3× faster.

### 11.4.7 Region 5*: Frostbite Fjord → Icemouth (Expansion idea)
- **Theme:** ice shelves, sauna huts, northern lights.
- **Layout idea:** a frozen bridge that cracks over 3 crossings, and a market on an ice floe drifting 10 m per minute. Players have to keep re-finding it.
- **Signature hazard:** ice friction 0.15, and the "Cold" status away from braziers.
- **New critters/crops:** Yaks (4 carriers), Penguins, Reindeer, Snow Turnips that build snowman decoys.
- **Night:** the Ice Troll under the bridge demands a toll good per crossing. It is the Landlord's toll rule made literal.
- **Landlord Twist, "Heating Bill":** a daily cost of 8% of quota.

**Status in build:** Partial. Region 1 exists as one hand-built, fixed-seed layout (`BuildMuddlebury.Build`, seed 1977). No PCG weekly reseed, Region Profiles, campaign map, Deed Day or Regions 2–5 are built.

---

## 11.5 Level Breakdown: Muddlebury Farm (Region 1)

### 11.5.1 Overview
- **Scene:** `HarvestPanic/Assets/Scenes/Muddlebury.unity`, rebuilt reproducibly by `HarvestPanic.World.Editor.BuildMuddlebury.Build` (menu: Harvest Panic → World → Build Muddlebury). It is seeded, so every rebuild produces the same scene.
- **Coordinates:** metres. +X is east (toward the bridge and market), +Z is north, and the river flows toward −Z. Farm ground is at y = 0, water at y = −4.2, riverbed at y = −6.
- **Playable bounds:** x −122 to 177, z −148 to 104 (four world-edge colliders), inside a forest ring on the surrounding hills that hides the edge and reads as a cosy bowl.
- **Ground:** Unity Terrain (320 × 270 m) with painted layers (meadow, road, soil, mud, cobble, bank). The NavMesh covers the whole playable area, including the river channel.
- **Design intent:** one level that teaches the whole game in its first 30 seconds. You spawn looking straight at the problem: a road, a bridge, a market, and something squealing to your left.

### 11.5.2 Schematic map

```
                                    N
   (schematic, not to scale; numbers are world metres, x east / z north)

  x: -120      -86      -56     -26       0     16      42  55 60     100 104  114     134    150  162
 z +95 +-------------------------------------------------------+~~~~~~~~+---------------------------+
       |                                ######################  |~~~~~~~~|   houses  houses  houses  |
 z +70 |                                #  (A) CORN MAZE      #  |~~~~~~~~|                           |
       |                                ##########   ##########  |~ RIVER~|        [BUTCHER]  (fount.)|
 z +40 |          [VEG ROWS]   (grove)     [(B) PIG PEN]       H |~ flows~|                           |
       |  WHEAT                             mud  hay-hole      H |~  |   ~|                    houses |
 z +18 |  FIELD                (grove)         |               H |~  v   ~|                           |
 z   0 |  ~wind~   =====[BARN]>>S===fork==========hay=======mud=[=BRIDGE=]=ARCH=[PEN]===(BELL) [GREEN-|
       |                  wardrobe    \  mud   |      hay      H |~ 40 m ~|   (C)                GROCER]|
 z -24 |                       (grove) \       |      (grove)  H |~      ~|                    houses |
 z -38 | (WINDMILL)  [PUMPKIN PATCH]    \  [(D) COOP]          H |~      ~|        [ODDITIES]         |
 z -65 |                                   [ ORCHARD  15 trees ] H |o  o  o~|<- stepping-rock bait      |
 z -80 |                                                 \     H |~      ~|   houses  houses  houses  |
z -120 |                                       bank path  \_[LOST & FOUND]~|                           |
       +-------------------------------------------------------+~~~~~~~~+---------------------------+
          WEST: farm plateau (~180 x 180 m)         H = bank hedge      EAST: Muddlebury market town
   S = 4 player starts facing east   ===  main road (5 m)   >> = spawn sightline toward the arch
```

### 11.5.3 Zones

| Zone | Location (x, z) m | Size | Purpose | Key props / markers |
|---|---|---|---|---|
| **Barn & yard** | (−26, 0), doors face east | ~16 m deep, 12 m yard radius | Spawn, respawn, wardrobe, starter goods | 4 `PlayerStart`, `BarnRespawn` (−14.5, 0), 4 `Goods_Crate` (−12.5, 6.5), wardrobe, hay stack, cart, barrels, signpost (5, −3.8) |
| **Main road** | x −17 → 62, z 0 | 5 m wide, 79 m | Primary artery and sightline | Flower beds, hay bales at (−2, 6), (14, 5.5), (26, −5.8), (40, 6.2), fence lines near the bridge |
| **The Fork** | x ≈ 7 | 1.7 m spurs | Splits a crew of four into pairs | North spur to the pig pen, south spur to the coop, mud patch (−4, −7) |
| **Pig Pen (B)** | (16, 34) | 18 × 12 m | Pig source | `CritterSpawner Critter.Pig ×3`, south gate, mud wallow, trough, shelter, **hay bale hiding a north critter hole** |
| **Coop Yard (D)** | (16, −36) | 15 × 15 m | Chicken source | `CritterSpawner Critter.Chicken ×8`, coop house with ramp, north and east fence gaps toward the orchard |
| **Corn Maze (A)** | x −8 → 42, z 56 → 86 | 20 × 12 cells of 2.5 m | Main hiding ground, LOS breaker | Seeded maze with extra loops ("a hiding ground, not a puzzle"). Entrances south (toward the pen) and east. Sign at the south mouth |
| **Veg Rows** | x −78 → −62, z 26 → 42 | 18 × 18 m fenced | Easy catch zone (low, open) | Crop rows, 2 fence gaps |
| **Wheat Field** | x −108 → −68, z −15 → 18 | 40 × 33 m | Soft hide, wind | Two wheat growth stages |
| **Pumpkin Patch** | (−56, −44) | ~16 × 16 m fenced | Prize Pumpkin, the 4-carrier set piece | `Goods Crop.PrizePumpkin`, decorative pumpkins, signpost |
| **Windmill** | (−86, −38) | 13 m tower | West skyline anchor | Spinning sails, flour sacks |
| **Orchard** | x 0 → 50, z −80 → −56 | 15 trees | Shade and hiding, the chicken escape route | Barrel, stall bench, apples |
| **Bank Hedge** | x ≈ 55, full length | Bush line with gaps | Hide spots right before the bridge | Large bushes |
| **River** | x 60 → 100 | 40 m channel | Hazard, the spit-out conveyor | `River` trigger volume, downstream spline, lily pads, bank rocks |
| **The Bridge** | x 60 → 100, z 0 | 40 m × 2.4 m, 20 planks of 2 m, 0.7 m sag, rope rails at ~1 m | The only crossing. The daily boss fight | `BridgePlank` markers 0–19, networked `BridgeController` + `BridgeNetSync`, abutments, mud patch on the west approach (53, 4.5) |
| **Lost & Found** | (52, −120), west bank | Small pile | River output | `RiverPile` marker, sign and board, crates, barrel, log stack. Bank path back to the road |
| **Market Arch** | x 104 | 8.4 m wide | Threshold: "you made it" | Stone pillars, sign board, green banner |
| **Sale Pen (C)** | (114, 0) | 8 × 8 m, **open to the west** | Delivery point | `MarketPen` trigger (8 × 3 × 8), straw floor |
| **Market Square** | x 108 → 162, z −28 → 28 (cobbles to 168, ±32) | ~55 × 55 m | Selling stage, visible to the whole team | Butcher (130, 22), Greengrocer (150, 0), Oddities (130, −22), **Town Bell (134, 0)**, fountain (146, 22), lanterns, 9 pastel houses ringing the square |

### 11.5.4 Critical path: farm → fields → bridge → market

1. **Wake up at the barn.** Four players spawn in a row at x −11, facing east. The first frame shows the road, the bridge's rope rails and the market arch in a single line. The level's goal is understood before anyone opens a menu.
2. **The fork (x ≈ 7).** Pigs are north and chickens are south. Crews naturally split into pairs, which builds proximity-voice separation ("Where are you?" "By the pigs!" "WHICH pigs?").
3. **The chase.** Critters flee toward the **cover ring**: corn maze to the north, orchard to the south, hedge to the east, wheat to the west. Fence gaps and the hay hole are critter-only shortcuts, so the animals always know one route the players don't.
4. **The haul.** Everything rejoins the main road. Hay bales and the fork mud patch are minor obstacles that make a two-person carry wobble.
5. **The bridge approach.** Fences narrow the road at x 44–57, then comes a mud patch right at the abutment. The crew arrives slowed, filthy and in a line.
6. **The crossing.** 40 m of planks with the deepest sag in the middle. This is where planks break, sway builds and goods go in the river.
7. **The arch and the pen.** Four metres past the bridge, through the arch and into the Sale Pen, in full view of the square.
8. **The way back** is the same road, empty-handed and fast, or the long walk from the Lost & Found if the river got involved.

### 11.5.5 Traversal times

Speeds come from canonical sources: walk 4.0 m/s, sprint 6.5 m/s (`player-character`), mud ×0.6 (`bridge-hazards`), and carry speed `clamp(1 − 0.5 · mass / (hands · 25), 0.35, 1)` (`physics-carry`). Distances follow the built roads. Carry estimates assume walking pace.

| Route | Distance | Empty walk | Sprint | Carrying |
|---|---|---|---|---|
| Spawn → bridge west end | ~71 m | 18 s | 11 s | n/a |
| Bridge crossing | 40 m | 10 s | 6 s | 4-carrier floor speed (1.4 m/s): ~29 s |
| Bridge east end → Sale Pen | ~14 m | 4 s | 2 s | n/a |
| **Spawn → Sale Pen** (spec 70–140 m) | **~125 m** | **31 s** | **19 s** | n/a |
| Pig pen gate → Sale Pen | ~136 m | 34 s | 21 s | 40 kg pig, 1 carrier (×0.6): ~57 s |
| Coop → Sale Pen | ~137 m | 34 s | 21 s | Chicken (×0.9): ~38 s |
| Pumpkin patch → Sale Pen | ~195 m | 49 s | 30 s | Prize Pumpkin, 4 carriers near floor: ~100–140 s |
| Lost & Found → bridge west end | ~150 m | 37 s | 23 s | Soaked pig: ~62 s, then the bridge again (and the toll again) |
| Windmill → barn | ~72 m | 18 s | 11 s | n/a |

**Reading:** a clean single-pig delivery is about one minute out of a ~13-minute workday. A Prize Pumpkin run takes over two minutes of committed teamwork. A river accident costs an extra 1.5–2 minutes plus the respawn fee. That's enough to hurt, and short enough to laugh about.

### 11.5.6 Landmarks & sightlines

| Landmark | Seen from | Job |
|---|---|---|
| **Market arch and bell** | Spawn, the whole main road | The goal is always visible. A "go east" compass with no HUD arrow |
| **Bridge rope rails** | Spawn, the fork, the bank hedge | Marks the chokepoint and builds dread |
| **Windmill** (west skyline) | The bridge and the market, looking back | Orients the return trip. "Pumpkins are by the windmill." |
| **Red barn** | Most of the farm | Home and respawn. Bright barn red is reserved for it |
| **Corn maze wall** | North half of the farm | A visual "dense, don't go there" signal that animals ignore |
| **Lost & Found sign** | The river bank path | Teaches the river rule on the first walk of shame |
| **Field posts** (planned for Mr. Stitches) | Fields, from the road | Night tension. Every post is a "was he there before?" check |

**Sightline rules applied:** the spawn view is clear for about 118 m to the arch. Groves at (−8, ±24) and (36–38, 16/−20) give depth without blocking the road. The corn maze and orchard are deliberate LOS breakers placed opposite each other, so critters have somewhere to vanish on both sides of the fork.

### 11.5.7 Spawn points & markers

| Marker (`LevelMarker` role) | Position (x, z) | Count | Notes |
|---|---|---|---|
| `PlayerStart` 0–3 | (−11, −3 … +3), 2 m apart | 4 | Yaw 90°, facing east down the road |
| `BarnRespawn` | (−14.5, 0) | 1 | Drowning and knockouts return here (fee) |
| `CritterSpawner` `Critter.Pig` | (19, 33) | 3 | Inside the pig pen |
| `CritterSpawner` `Critter.Chicken` | (18, −38) | 8 | Inside the coop yard |
| `Goods` `Crop.PrizePumpkin` | (−56, −44) | 1 | Pumpkin patch centre |
| `Goods` `Product.Crate` | (−12.5 … −11.1, 6.5 … 7.9) | 4 | Barn yard, the first thing to grab |
| `MarketPen` | (114, 0) | 1 | 8 × 3 × 8 trigger, Interactable layer |
| `RiverPile` | (52, −120) | 1 | End of the river sweep spline |
| `River` | x 60–100, full length | 1 | Sweeps goods and critters for 15–25 s, sends players to the barn |
| `BridgePlank` 0–19 | x 60–100 | 20 | Networked break and sag state |
| `Wardrobe` | Inside the barn | 1 | Cosmetics |

**Planned markers (not yet placed):** Scarecrow field posts (6–8 across the fields), Barn Thing hay zones, rival cart parking at the map edges (north road end and south bank path), Landlord car and Farm Walk spline, town-folk idle points, Town Crier spot.

### 11.5.8 Chaos hotspots

| # | Hotspot | Why it produces clips | Systems involved |
|---|---|---|---|
| 1 | **Mid-bridge sag** (planks 9–10) | The lowest planks sit at the centre. A 4-carrier cow or pumpkin carry stalls exactly where the drop is worst | Bridge, physics carry |
| 2 | **Bridge approach mud** (53, 4.5) | Everyone arrives slowed and slipping, with ragdoll thresholds ×0.8, right before the most dangerous 40 m | Mud, ragdoll |
| 3 | **Sale Pen opens west** | Delivered critters can wander back out toward the bridge and the river | Critter AI, market pen |
| 4 | **Town bell (sundown −60 s)** | Every critter in earshot panics toward the bell, across open cobbles, in front of the whole crew | Region quirk, critter AI |
| 5 | **Stepping-rock bait** (~65 m downstream) | Six rocks 6.4 m apart look like a shortcut, but the gaps are too wide. It ends in a swim | River |
| 6 | **Lost & Found loop** | Soaked goods appear on the farm bank, 120 m downstream, so you walk back and cross again (and pay the toll again) | River, toll twist |
| 7 | **Pig pen hay hole** | Pigs know the north exit behind the hay bale. Players find it the hard way | Critter navigation |
| 8 | **Coop gaps → orchard** | Eight chickens scatter into 15 trees of shade | Flock scatter |
| 9 | **The fork mud** (−4, −7) | A small mud patch on the busiest junction trips sprinting carriers | Mud |
| 10 | **Corn maze at night** (planned) | No sightlines, plus a scarecrow that moves when you aren't looking | Night creatures, maze corn |

### 11.5.9 Deviations & open items
- **Bridge length:** built at 40 m with 20 × 2 m planks, per the level brief. `bridge-hazards.md` still specifies 24 m with 1.2 m planks. The game designer needs to pick one. The level's traversal times and the "boss fight" pacing assume 40 m.
- **Town bell** is geometry only. The sundown panic behaviour is not implemented.
- **Bridge Toll** twist is not implemented.
- The level doc's coordinate axes use "y north". The Unity build uses +Z north. This chapter uses the Unity convention.

**Status in build:** Built. The scene, zones, bridge (networked planks), river sweep to the Lost & Found pile, mud zones, market pen, stalls, bell, landmarks and all listed markers exist and are exercised by the full-day PlayMode playtest (Morning → Workday → carry and pen sale → Dusk → Payout → Next Day). Night content, NPCs, the bell behaviour and the toll are Planned.

---

## 11.6 Level Design Rules for Future Regions

These rules are binding for Regions 2+ and for the PCG layout generator. A region may break one rule only with a written rationale in its level doc, approved by the narrative director and the game designer.

### 11.6.1 Structure rules

| # | Rule | Why |
|---|---|---|
| 1 | **Three-part map:** farm zone (≈ 180 × 180 m) → one fixed chokepoint → market town | The bridge is the boss fight. Every region needs its own version |
| 2 | **Barn → chokepoint walking path between 70 and 140 m** | Long enough to make a haul matter, short enough that a run takes ~1 minute |
| 3 | **The chokepoint and market are hand-authored and fixed per region.** Only the farm side reseeds weekly | Players learn the boss arena while the farm stays fresh ("the farm moved overnight") |
| 4 | **Spawn faces the goal.** The first frame shows the chokepoint and the market landmark | Instant orientation without HUD arrows |
| 5 | **No pen within 25 m of the chokepoint** | Escapes need room to become chases, not instant river losses |
| 6 | **Every field is reachable by wagon** (navmesh ≥ 3 m wide) | The vehicle fantasy can't dead-end |
| 7 | **At least one alternative route** (ford, raft, zipline), unlocked by upgrades | The toll and chokepoint pressure need a counterplay the crew can buy |
| 8 | **A spit-out location on the farm side, far downstream** | Failure costs time, not progress, and the walk of shame is part of the joke |

### 11.6.2 Chaos & readability rules

| # | Rule | Why |
|---|---|---|
| 9 | **A cover ring around the pens:** at least one LOS breaker in each compass direction | Critters always have somewhere to flee. Everything Has Legs |
| 10 | **Critter-only shortcuts** (2–4 fence gaps, holes) in every pen cluster | The animals should feel smarter than the crew |
| 11 | **The market is open and visible.** Sales happen in plain view of teammates | Success and failure both need an audience |
| 12 | **At least 4 authored chaos hotspots**, documented in the level doc with the systems they combine | Comedy is designed, not hoped for |
| 13 | **Night-safe zones:** chokepoint and market. Night creatures need posts, hay or trees on the farm side | Night is a gamble, not a trap |
| 14 | **Every hazard gets a clear visual tell** (loose nails, cracked planks, mud sheen, fog edge) | Players should blame each other, not the game |
| 15 | **Landmark per compass quadrant**, including one skyline anchor visible from the chokepoint | Navigation on the return trip, without a minimap |

### 11.6.3 Narrative rules

| # | Rule | Why |
|---|---|---|
| 16 | **Each region tells one joke about the debt** through its Landlord Twist and Deed Day line | Keeps the campaign spine consistent |
| 17 | **The town has a quirk and a crier.** Town folk react; they never quest-give | The market is an audience, not a hub |
| 18 | **Environmental storytelling only.** Signs, memos, props. No mandatory cinematic over 10 s | Never stop the chaos for story |
| 19 | **One returning character beat per region** (Grandpa letter, Mr. Stitches gag, rival cameo) | Continuity without exposition |
| 20 | **Cultural care.** Creatures and themes must not appropriate sacred concepts (precedent: the Copycat Coyote rename) | Tone stays inclusive and comedic |

### 11.6.4 Technical rules (Unity)
- Each region has a reproducible editor builder (precedent: `BuildMuddlebury`), with seeded randomness and no wall-clock values.
- Gameplay anchors use `LevelMarker` roles (`PlayerStart`, `BarnRespawn`, `CritterSpawner`, `Goods`, `MarketPen`, `River`, `RiverPile`, `BridgePlank`, `Wardrobe`). Systems never look up objects by name.
- A PCG-generated layout must pass the constraint checks in ≤ 20 attempts and generate in ≤ 3 s on a base M1. Otherwise it falls back to the region's hand-authored layout. The same `RunSeed` must produce an identical layout on host and clients.
- Third-party art goes under `Assets/ThirdParty/<Pack>/`, with licences recorded in `THIRD_PARTY_LICENSES.md`.

**Status in build:** Planned (rules for future regions). Rules 1–6, 8–12 and 14–15 are already satisfied by Muddlebury. Rule 7 (an alternative route) and rule 13 (night content) are not yet met there.

---

## 11.7 Open Questions for the Creative Director

| # | Question | Recommendation |
|---|---|---|
| 1 | Does the Landlord get a name? | No. "Name: On file." Namelessness is the joke |
| 2 | Is the curse ever explained? | Never. Contradictory clues only |
| 3 | Town folk working names (Hank, Mrs. Leek, Madame Odette) | Approve as placeholders and have the writer finalise |
| 4 | Final bridge spec: 40 m / 2 m planks (built) vs 24 m / 1.2 m (`bridge-hazards`) | Keep 40 m. Traversal pacing in 11.5.5 depends on it |
| 5 | Is the "younger Landlord on the 1994 tapes" hook canon? | Yes, as a background gag, never paid off |
