# 8. Time Structure: Day, Week, Night

> **Owner**: systems-designer · **Sources**: `design/gdd/day-loop.md`, `week-quota.md`, `night-creatures.md`, `weather-twists.md`, `landlord-events.md`, `rival-farmers.md`, `consistency-report.md`; code `HarvestPanic/Assets/Scripts/Economy/` (`DayClock`, `EconomyDirector`, `EconomyTuning`, `DayModifierStack`, `EconomyAssetBuilder`)
> **Convention**: values come from the canonical GDDs or the code. New values are marked **(proposed)**. Every section ends with **Status in build** (Built / Partial / Planned).

---

## 8.1 Time Scales at a Glance

HARVEST PANIC runs on three nested clocks. The **day** is the unit of play: one panicked haul across the bridge. The **week** is the unit of pressure: five days of earning, then the Landlord collects rent on Sunday. The **region** is the unit of victory: pay five Sundays in a row and the farm is yours (until the next one).

| Scale | Length (real time) | Contents | Pressure source |
|---|---|---|---|
| Day | ~10–14 min | Morning → Workday → (Dusk) → Payout → Shop | Workday timer, Morning Order |
| Week | ~65–75 min | Days 1–5 (Mon–Fri) + Landlord Sunday (~90 s) | Weekly quota (rent) |
| Region run | ~5.6–6 h | 5 paid weeks + Deed Day | Quota grows ×1.35 per week |
| Eviction rung | ~40–45 min | 3 days + Sunday | Endless quota ladder (see Ch. 10) |

**Why these lengths.** A 10–15 minute day is long enough for a full plan → disaster → recovery arc and short enough that a failed day never feels like wasted evening. Five days give the crew room to recover from one bad day before rent is due, so a single disaster rarely ends a run on its own, but two or three can.

**Status in build:** Built for day and week. The calendar (`DayClock`) tracks the run day (0-based), the week (1-based) and the day of the week (1–5). Region runs and Eviction rungs are Planned.

---

## 8.2 The Day: Phase Sequence

The day is a host-authoritative state machine: the host runs the clock and every client mirrors it. Timed phases move on by themselves. Payout, Sunday and Evicted wait for a button press.

```
 Morning ──30 s──► Workday ──540 s──► [Dusk 180 s] ──► Payout ──► Shop ──► next Morning
   (order + twist)   (crunch at T−60)   (night market)   (receipt)  (upgrades)
                                                              └─ after Day 5 ─► Landlord Sunday ─► Paid → Week+1
                                                                                                └► Evicted → run over
```

| Phase | Duration | What players do | Key rules | HUD / audio cue |
|---|---|---|---|---|
| **Morning** | 30 s (15–60 knob); skippable once everyone is ready | Read the Morning Order (3 lines) and the twist card(s); plan who does what | Timers frozen; players spawn at the barn; goods spawn at their markers | Calm banjo; order board; clock reads "market opens soon" |
| **Workday** | 540 s = 9:00 (300–720 via `Day.WorkdaySeconds`) | Round up, carry, cross the bridge, sell | Market open; an item is sold once it rests 2.0 s in the Sale Pen; crop freshness decays | Roundup music, layered by chaos |
| **Crunch** (last 60 s of Workday) | 60 s inside Workday | All-in final runs | No rule change: pure pressure | Clock turns red; Crunch music (+15% tempo, fiddle); pocket-watch tick |
| **Sundown** (last 30 s of Workday) | 30 s warning, then the bell | Dive for the pen | At 0:00 the market closes; items already in the pen still count if they finish resting within the 2 s grace | "SUNDOWN!" label; key-lift on the bell |
| **Dusk** (optional) | 180 s (90–300) | Night Market: sell at ×1.25 while night creatures hunt | 10 s majority vote to enter (host breaks ties); freshness decay paused | Sparse creepy pump organ; Night Threat layer when a creature is within 30 m |
| **Payout** | ~25–30 s (receipt timeout 25 s) | Read the receipt and argue about who did it | Order Complete Bonus paid; Day-length modifiers expire | Dot-matrix receipt printer |
| **Shop** | Untimed; ready-up | Vote on upgrades; autosave | Purchases need Balance ≥ price | Shop chalkboard |
| **Next Day** | — | Ready-up | After Day 5 the game goes to Landlord Sunday instead | — |

**Day length check:** 30 + 540 = 9:30 without dusk; + 180 = 12:30 with dusk; + ~30 s payout + ~60–90 s shop ≈ **13–14 min**. That sits inside the 10–15 minute target.

**Stranded rule.** Any player who is outside both the barn zone and the market zone when the last timed phase ends (Workday if no dusk, otherwise Dusk) is "lost in the dark". They are sent to the barn, charged the respawn fee, and the Ledger logs `Stranded`.

**Unsold goods** at day end: livestock wander back to the wild pool. Crops and products despawn unless they are stored in the Barn Cellar upgrade.

**Status in build:** Partial. Built: Morning 30 s → Workday 540 s → Dusk 180 s → Payout → (Sunday | next Morning), the 2 s sundown grace, the red clock under 60 s, the "SUNDOWN!" label under 30 s, the pen's 2 s acceptance, modifier-driven phase lengths, and the `-hpDayScale` dev time-scale. Not yet built: the dusk vote (the build sets `DuskEnabled = true`, so every day has a dusk), skipping Morning when everyone is ready, the Shop phase (Payout goes straight to the next Morning on the host's **Next Day** button), the Payout auto-timeout (`PayoutAutoAdvanceSeconds = 0`), the Stranded rule, and the Crunch music and pocket-watch.

---

## 8.3 Morning: The Order and the Twist

Each morning the crew gets a three-line **Morning Order** (for example "3 Pigs · 6 Eggs · 1 Prize Pumpkin") plus one or more **twist cards** (weather, Landlord visit, curse). Order lines are drawn without repeats from the goods the level can actually supply.

**Order rewards**
- Every item that fills an open line sells at **×1.5** (order multiplier `O`).
- Filling **all three lines** pays the **Order Complete Bonus** once at Payout: `OCB = $60 × w`.

**Order quantity per line**

```
q = max(1, round( BaseQ × S_p × (1 + 0.15 × (w − 1)) ))
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `q` | int | 1–~12 | Units requested on this line |
| `BaseQ` | int | 1–6 | Per-goods base quantity (build: Pig 2, Chicken 3, Egg 6, Egg Crate 2, Prize Pumpkin 1, Turnip 5, Carrot 5) |
| `S_p` | float | 0.45 / 0.65 / 0.85 / 1.00 | Player-count scale for 1/2/3/4 players |
| `w` | int | 1–6 | Week number |

**Output range:** bounded below at 1, so an order line is never empty. The largest value in Region 1 is Egg at 4p in Week 5: 6 × 1.0 × 1.6 = 9.6 → 10. Rounding is half-up.

**Worked example.** Pigs, 4 players, Week 3: `2 × 1.00 × 1.30 = 2.6 → 3 pigs`. The same line for a 2-player crew in Week 1: `2 × 0.65 × 1.0 = 1.3 → 1 pig`.

**Budget guideline:** the summed base value of all order lines should be about 45% of the expected day gross, so that finishing the order is worth chasing but is never the only way to make rent.

**Status in build:** Built. The order is drawn from goods present in the level, the quantity formula is implemented, ×1.5 is applied on sale, and the OCB is paid at Payout. Egg Crate ($30) and Carrot ($8) are build-only goods that are not yet in the canonical goods table (see Ch. 9 §9.4). The twist card display is Partial: active modifiers are listed, but there is no card presentation yet.

---

## 8.4 The Last 60 Seconds: Crunch and Sundown

The end of the Workday is the game's signature tension beat, a "photo finish" at the market fence. It is built as a staged timeline so players feel it coming rather than being cut off.

| Time remaining | Event | Purpose |
|---|---|---|
| 1:00 | **Crunch** begins: clock turns red, music jumps +15% tempo with a fiddle lead, pocket-watch ticks on eighth notes | "Last run" decision: one more trip or start selling what you carry? |
| 1:00 (Region 1 only) | Muddlebury town bell rings at sundown −60 s; every critter in earshot panics toward it | Regional chaos spike right when hands are full |
| 0:30 | **Sundown** warning: "SUNDOWN!", sun dips, music key-lift | Commit now |
| 0:00 | Market bell; pens stop accepting new items | Hard stop |
| 0:00 → +2 s | **Grace window**: items already resting in the pen finish their 2 s count and still sell | Turns the buzzer into a dive-catch, not a robbery |
| +2 s | Dusk vote (if enabled) or Payout | — |

**Edge rules**
- If a modifier changes `Day.WorkdaySeconds` mid-day, the remaining time scales proportionally: `T_remaining_new = T_remaining × new/old`, never below 30 s. The Crunch and Sundown cues follow the new remaining time.
- A pig that walks back out of the pen at 1.9 s is not sold. The register goes "ka-ch..." and stops. This is intended.

**Status in build:** Partial. Built: the red clock under 60 s, "SUNDOWN!" under 30 s, the 2 s grace (`SundownGraceSeconds`), and the proportional rescale with a 30 s floor (`DayClock.Rescale`). Planned: Crunch music, pocket-watch tick, the sun-dip lighting cue, and the Muddlebury bell panic.

---

## 8.5 Dusk: The Night Market Gamble

After sundown the crew may vote to stay out for **180 s** of **Night Market**: every sale is worth **×1.25** (`Day.DuskBonus`), but night creatures become active and anyone caught outside at the end is stranded and charged.

**Dusk expected value** (designer guideline, **proposed**)

```
EV_dusk = r_sale × T_dusk × (DuskBonus) − (E_KO + E_stranded) × Fee(w)
r_sale  = G_day(w) / T_work
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `r_sale` | float $/s | ~0.3–2 | The crew's Workday earning rate |
| `G_day(w)` | int $ | 180–1,000 | Expected day gross from the economy model (Ch. 9) |
| `T_work`, `T_dusk` | s | 540, 180 | Phase lengths |
| `DuskBonus` | float | 1.0–2.0 | Default 1.25 |
| `E_KO`, `E_stranded` | float | 0–4 | Expected creature knockouts and stranded players |
| `Fee(w)` | int $ | 25–55 | Respawn fee this week |
| `EV_dusk` | float $ | can be negative | Net value of choosing dusk |

**Output range:** unbounded below. A crew that loses everyone to the Scarecrow loses money. This is intentional, because dusk is a gamble.

**Worked example (4p, Week 3).** `r_sale = 625 / 540 = $1.16/s` → `1.16 × 180 × 1.25 = $260` gross. With two knockouts at $40 each: `260 − 80 = +$180`. With two knockouts and two stranded players: `260 − 160 = +$100`. With the bonus and no creatures it would be +$260. The canonical Week 3 worked example (Ch. 9) books a Tuesday dusk worth +$180 sales against +$80 extra fees.

**Rules:** the vote is a majority (host breaks ties; 2-player 1–1 goes to the host; solo gets a prompt). Crop freshness decay is paused at night ("it's cold"). Rivals leave at sundown because they fear the creatures too.

**Status in build:** Partial. Built: the 180 s Dusk phase, the ×1.25 Dusk bonus in the sale price, the "night market ×1.25" HUD label, and the freshness pause (freshness runs only during Workday). Planned: the vote (dusk is currently always on), creatures, and stranded charging.

---

## 8.6 Payout, Shop and Next Day

**Payout** closes the books: day sales, order bonus, costs, net, balance, rent due Sunday, any Landlord IOU, then the Blame Ledger receipt grouped by player (see Ch. 9 §9.9 for the receipt layout and Ch. 10 §10.6 for blame). Day-length modifiers (weather, curses) expire when Payout begins.

**Shop** (after Payout) offers 6 upgrades; purchases go through a 10 s vote (Ch. 10 §10.2). The run autosaves here: the Shop-phase save is the **day boundary** every resume rolls back to. If the host drops mid-day, that day's income is lost and the run resumes from the last Shop save. Any roster player can host the resume.

**Next Day** advances the calendar. After Day 5 it leads to Landlord Sunday.

**Status in build:** Partial. Payout and its receipt are Built (host presses **Next Day**). Shop, autosave and resume are Planned.

---

## 8.7 Day Modifiers and the Chaos Cap

Every source of chaos (weather, curses, Landlord, region rules, upgrades, Eviction notices) is a **Day Modifier**: a data asset that adds to, multiplies or overrides named day parameters such as `Day.WorkdaySeconds`, `Goods.ValueMult` or `Economy.RespawnFeeMult`. No system hard-codes content; each one reads the resolved parameter.

**Resolution**

```
P = Override(highest priority)  if any
  = clamp( (P_base + Σ Add) × Π Mul , P_min, P_max )  otherwise
```

**Worked example.** `Goods.ValueMult` with base 1.0, one +0.2 Add, and Muls of ×1.5 and ×0.8: `(1 + 0.2) × 1.5 × 0.8 = 1.44`.

**Chaos cap:** the number of active **Major** modifiers is limited to `MajorCap(w) = min(4, 1 + w)`, which gives **2 in Week 1, 3 in Week 2, 4 from Week 3**. Extra Majors queue to the next morning. Landlord Sunday modifiers and weekly rules bypass the cap. Weather uses one slot at a time (a new weather card replaces the old one).

**Status in build:** Built. `DayModifierStack` implements Add/Mul/Override, clamp specs for 13 registered parameters, Unique/Refresh/Stack/ReplaceGroup, the chaos cap with a next-day queue, and durations (Day, WorkdayOnly, DuskOnly, Seconds, Week, Run).

---

## 8.8 The Week and Landlord Sunday

A week is **five working days (Mon–Fri)** followed by **Landlord Sunday**. "The Landlord doesn't believe in Saturdays." The week's quota is shown from Monday morning on the barn chalkboard and the HUD ("Rent $X Sunday · today's share $X/5"). The player count is sampled **on Monday morning** and the quota is fixed for that week.

**Sunday sequence (~90 s, skippable after the first time in a run)**

| Step | What happens |
|---|---|
| a. Arrival | The Landlord walks to the barn |
| b. SundayAudit | Weekly awards (MVD, Butterfingers, Bridge Troll, Pig Whisperer, Town Crier), then the golden-egg reveal: the pocketing player chooses **Donate** (+$150 to the wallet) or **Keep** |
| c. Payment | The wallet counter rolls down by `Due = Q(w) + IOU (+ Double Down)`. If Balance ≥ Due, the week is **Paid** and the remainder carries over |
| d. Collateral | If short: 60 s to sell upgrades back at 50%. Anyone can vote "Give up" |
| e. Eviction | Still short when the timer ends: **Evicted**. The run save is wiped, account unlocks persist, and the eviction cutscene plays with the Camcorder montage |

**Weekly escalation ladder (Region 1, 4 players)**

| Week | Quota | Respawn fee | Order bonus | Order qty × | Chaos cap | Landlord rules active | Landlord visits |
|---|---|---|---|---|---|---|---|
| 1 | $1,200 | $25 | $60 | 1.00 | 2 | 0 (tutorial week) | 0 |
| 2 | $1,620 | $30 | $120 | 1.15 | 3 | 1 | 1 (Tue–Thu) |
| 3 | $2,190 | $40 | $180 | 1.30 | 4 | 2 | 1 |
| 4 | $2,950 | $45 | $240 | 1.45 | 4 | 3 | 2 |
| 5 | $3,990 | $50 | $300 | 1.60 | 4 | 3 (4th replaces oldest) | 2 |

The rival visit chance and the night budget also grow with the run day (§8.9, §8.11). Region totals: Region 1 rent across 5 weeks is about **$11,950** for a 4-player crew.

**Mid-week leavers.** From the next day, the quota is re-scaled pro rata: `Q' = Q × (d_done + d_left × S_new/S_old) / 5`. Example: a 4p crew loses one player after Day 2 of Week 3: `2,190 × (2 + 3 × 0.85) / 5 = 2,190 × 0.91 = $1,992.9 → $1,990`.

**Status in build:** Partial. Built: the 5-day week, Sunday payment of `Quota + IOU`, carryover, immediate eviction if short, MVD named on the Sunday receipt, weekly Landlord rule draw (max 3, oldest replaced), Monday player sampling, and the **Next Week / Try Again** buttons. The pro-rata formula exists in `EconomyMath.ProratedQuota` but is not wired in. Planned: Landlord walk-in, the full awards set, the golden-egg reveal, Collateral, the eviction cutscene, and visits.

---

## 8.9 The Night Phase and Night Creatures

When the crew stays out for Dusk, a **Night Director** spends a threat budget to spawn creatures and steer them toward the **loudest** part of the farm. Creatures are light horror: they knock out, drag or steal, but never kill permanently and never end a run directly. A creature knockout sends the player to the barn and charges the weekly respawn fee (Ledger verb `KnockedOut`).

**Night budget**

```
B = 10 + 4 × DayIndex + 3 × PlayersOutside
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `DayIndex` | int | 0–24 (5-week run) | Run day as the GDD example uses it |
| `PlayersOutside` | int | 0–4 | Players outside the barn zone at sundown |
| `B` | int | 10–118 | Points available to spawn creatures |

Costs: **Scarecrow 8, Barn Thing 10, Crow flock 4**. At most 1 Scarecrow, 1 Barn Thing and 2 flocks can be active.

**Worked example.** Day 3, 3 players outside: `10 + 12 + 9 = 31` points, enough for the Scarecrow + Barn Thing + 2 flocks (8 + 10 + 8 = 26).

**Design flag:** the maximum spend allowed by the concurrency caps is 26, and the budget reaches 26 by DayIndex 3 with 2 players out. After the first week, the budget no longer adds threat. **Proposed fix:** treat `B` as a per-night pool that also pays for re-spawns after a creature is repelled, or raise the caps by week (for example +1 flock per week). This needs a decision from the owner.

**Steering:** every 20 s the Director picks the 20 m cell with the highest attraction, `Σ Loudness × SourceMult`, where Crop (mandrake) counts ×2.0, Voice/Shout ×1.0, Impact/Hazard ×0.7, and Tool/other ×0.5. Shouting at night is dangerous, except at crows.

### Region 1 roster

| Creature | Tier | Behaviour | Counter | Penalty |
|---|---|---|---|---|
| **The Scarecrow, "Mr. Stitches"** | Alpha | Moves only when **no player is looking at it** (in a view within 40 m with line of sight, ≥15% visible; 15 m in fog). Unobserved, it moves at 7 m/s and teleports between field posts. Its touch is a knockout | Keep eyes on him and assign a watcher. The Handheld Lantern freezes him for 3 s (8 s cooldown). He cannot enter the market or the bridge | Knockout; carried goods are dropped (and bruised) |
| **The Barn Thing** | EA | Lives in the hay; drags the nearest player within 6 m into it over 4 s | Rescuers pull: `Drag += 0.25/s − 0.2/s × RescuerHands`, so two rescuers reverse it. A turnip distracts it for 10 s | Knockout if not rescued |
| **The Crows, "The Committee"** | EA | Flocks of 5–8 steal shiny tools, small goods, and coins from the barn wallet sign ($10 per hit, max $50/day, never below $0) | Noise ≥ 0.6 shoos them (steal chance 0.6 → 0.3). Crow Net blocks coin theft. Stolen items can be recovered from the nest tree | Lost tool or cash |
| **The Headless Hen** | Post | Runs at full speed in random directions | Avoid | 5 s inverted controls |

### Region creatures (Post / Expansion stubs)

| Region | Creature | Behaviour | Cost |
|---|---|---|---|
| 2 Riverlands | The Bog Lantern | Lures the loudest player toward deep water | 8 |
| 3 Orchard | The Headless Picker | Takes goods out of wagons to the treeline | 8 |
| 3 Orchard | Orchard Widow | Webs immobilize carriers for 4 s | 6 |
| 3 Orchard | The Cider Ghost | Sways one player's input for 5 s | 6 |
| 4 Ranch | The Copycat Coyote | Fakes teammates' footsteps | 6 |
| 5 Fjord | The Ice Troll | Demands one good per night crossing or breaks a plank | 10 |

**Status in build:** Planned. No creature, Night Director or Crow code exists yet. The wallet already supports the "theft never below $0" rule the crows need (`WalletTxnType.Theft`, tested).

---

## 8.10 Weather and Daily Twists

Each morning the twist deck draws **one weather card**. From Day 3 there is a **40%** chance of an extra **oddity card**. Each card is a bundle of Day Modifiers. Deck rules: weighted draw; no weather more than 2 days in a row; no Storm or Fog on Day 1; the same seed always gives the same sequence. A region's deck overrides the default weights.

| Card | Default wt | Region 1 wt | Gameplay effects | Critter effects | Counter | Weight class |
|---|---|---|---|---|---|---|
| **Clear** | 30 | 40% | None | — | — | — |
| **Mud Day** | 20 | 25% | Mud patches ×1.5 radius plus extras (mud: speed ×0.6, ragdoll threshold ×0.8); sprint stamina cost ×1.3; 10% slip on sharp turns | Pigs ×1.15 speed in mud | Boards, wagon | Minor |
| **Gusty** | 15 | 20% | Wind 4–12 m/s, direction rotates every 60 s; side force on carried goods (`0.5 × 1.2 × v² × A`: 10 m/s on a 0.6 m² pumpkin ≈ 36 N) | Chickens glide 2× farther | Carry low, add hands | Minor |
| **Storm** | 10 | 10% | Lightning every 45–90 s strikes the tallest object in a 30 m disc (ragdoll + $50 bruise; metal tool counts +1 m); `Bridge.SafeLoadMult` ×0.7 after minute 6; 35% chance a bridge segment washes out at minute 10 (2 s creak warning) | Critter noise sensitivity ×0.6 | Finish early; Rope Rail Netting | Major |
| **Fog** | 10 | 5% | Visibility 15 m; HUD markers off; voice is the only navigation aid | Critter alert radius ×0.6; Scarecrow sight range 15 m | Call-and-response, cow bells | Minor |
| **Heatwave** | 10 | — | Crop freshness decay ×1.5; stamina regen ×0.8 | Cows freeze in shade 50% more | Wagon trips | Minor |
| **Full Moon** (night) | 5 | — | Night budget ×1.5 | Nocturnal traits from minute 8 | Skip dusk | Minor |
| Drizzle / Flood / Blood Moon | region | — | Region 2–3 cards (light mud; low fields flood at 2/3 day; night budget ×2) | — | — | — |

**Oddity cards (Day 3+, 40%)**: Greased Pig (grab break ×2), Chicken Jamboree (+8 chickens at half price), Goat Convention (3 goats), Mandrake Bloom, Upside-down Gravity Hour (Post; 20 s low gravity), Grandpa's Ghost Helps (one free lure).

**Status in build:** Partial. Three weather cards exist as data (Fog, Storm, Heatwave), drawn with a **flat 50% chance per day, uniformly** (`WeatherChancePerDay = 0.5`), not from the weighted deck. They differ from the design as follows. Built Fog = voice/noise environment ×0.8 + `World.Fog` tag. Built Storm (Major) = noise ×0.6 + **bruise ×1.25** (not in the GDD; proposed to keep as the "rain makes everything slippery" stand-in). Built Heatwave = freshness ×1.5 + **demand ×1.1** (not in the GDD). Heatwave can be drawn in Region 1 even though the region deck excludes it, and Storm is not excluded from Day 1. Planned: Clear/Mud/Gusty/Full Moon cards, physical effects (wind, lightning, washout, visibility), oddities, deck rules and seeding.

---

## 8.11 Other Timed Pressure: Landlord Visits and Rivals

| Source | When | Rule |
|---|---|---|
| Landlord **Inspection** | From Week 2: 1 visit/week on Tue–Thu; from Week 4: 2 visits | 120 s window; 3 checks; fine per failed check `25 × (1 + 0.2(w−2))`, capped at $150 |
| Landlord **Farm Walk** | Same slots | 90 s walk; $50 per hit on him (max 3); opens one pen gate |
| Landlord **Helpful Visit** | Same slots | Bridge plank break ×0.5 for the day; $100 fee at Payout |
| **Double Down** offer | Morning, 20 s vote, once per run, never in the final week | This week's Due ×2, next week waived |
| **Rival visit** | From Day 2; chance `min(0.5, 0.15 + 0.05 × DayIndex)`; arrives 2–5 min into the day; leaves at sundown | Cousin Clem steals unattended goods, the Pruitt Twins poach livestock, Mabel loosens planks |

Visits are Major modifiers. If the chaos cap is full they are deferred, never dropped. A visit deferred past Friday becomes a **$50 "missed appointment"** fee on Sunday.

**Status in build:** Planned (Landlord weekly rules are Built; see Ch. 9 §9.10).

---

## 8.12 Chapter Status Summary

| Element | Status |
|---|---|
| Phase machine, timers, grace, rescale, dev time-scale | Built |
| Morning Order + OCB | Built |
| Crunch/Sundown HUD cues | Built (audio Planned) |
| Dusk phase + ×1.25 | Built (vote Planned) |
| Shop phase, autosave, resume | Planned |
| Week/Sunday pay-or-evict | Built (audit, Collateral, cutscene Planned) |
| Day Modifier stack + chaos cap | Built |
| Weather | Partial (3 cards, flat draw) |
| Night creatures, rivals, Landlord visits | Planned |
