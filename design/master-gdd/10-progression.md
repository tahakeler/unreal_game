# 10. Progression & Campaign

> **Owner**: systems-designer · **Sources**: `design/gdd/farm-upgrades.md`, `region-campaign.md`, `meta-progression.md`, `eviction-mode.md`, `blame-ledger.md`, `week-quota.md`, `onboarding-accessibility.md`, `consistency-report.md`; code `HarvestPanic/Assets/Scripts/Economy/` (`BlameLedger`, `EconomyMath`), `Customization/CosmeticCatalog*`
> **Convention**: canonical values from the GDDs/code; new values are marked **(proposed)**.

---

## 10.1 Progression Layers

| Layer | Scope | What grows | Lost on eviction? |
|---|---|---|---|
| **Run** | One region attempt | Wallet, upgrades, week, Landlord rules | Yes, all of it |
| **Region** | Campaign | Unlocked regions, deeds | No |
| **Account** | Per player | Letters, cosmetics, Farm Memories, Almanac, Farmer Level, starting-tool options | No |

Rule: **account progression never grants gameplay power**. Starting tools are sidegrades. Failure is meant to pay out too: disaster letters outnumber success letters.

**Status in build:** Partial (run layer Built for wallet/week; the rest Planned).

---

## 10.2 Farm Upgrades

**Shop rules**
- 6 offers per Shop phase: 4 drawn from the unlocked pool by tier, plus 2 staples (Bridge Reinforcement, Bigger Wagon) until they are bought.
- Any player proposes a purchase, followed by a 10 s majority vote (host breaks ties; solo buys instantly). Requires Balance ≥ price.
- Levels (Lv1→Lv3) are separate purchases. Sell-back pays `floor(0.5 × price)`; the highest level must be sold first.
- Consumables (◆) restock daily and cannot be sold back.
- Upgrades last for the run and are implemented as Run-duration Day Modifiers and/or spawned objects.

**Offer weighting**

```
weight(t, w) = max(0, 1 − |t − ceil(w/2)| × 0.5)
```
| Symbol | Type | Range | Description |
|---|---|---|---|
| `t` | int | 1–3 | Upgrade shop tier |
| `w` | int | 1–6 | Week |
| `weight` | float | 0–1 | Relative draw chance |

Worked example: Week 3 → `ceil(1.5) = 2`. A tier 2 upgrade has weight 1, tiers 1 and 3 have weight 0.5. In Week 1 a tier 3 upgrade has weight 0, so it never appears.

**Shop tier assignment (proposed):** T1 ≤ $200 · T2 $201–450 · T3 > $450.

**Payback rule:** `Price ≤ 1.5 × ExpectedWeeklyGain`, so each upgrade must earn at least `Price / 1.5` per week. Example: the Electric Fence earns about $90/day → $450/week ≥ $167 ✓.

**Full catalogue (Region 1)**

| Upgrade | Price $ | Effect | Tier (prop.) | Min gain/wk | Phase |
|---|---|---|---|---|---|
| Bigger Wagon Lv1/2/3 | 400 / 700 / 1,100 | Cargo soft cap 400→550→700→850 kg, larger bed | 2/3/3 | 267/467/733 | Alpha |
| Bridge Reinforcement Lv1/2 | 300 / 600 | Plank break ×0.7 / ×0.45; waives Bridge Toll | 2/3 | 200/400 | Alpha |
| Electric Fence | 250 | Pen escape ×0.4; zaps players (1 s stun) | 2 | 167 | Alpha |
| Guard Dog | 350 | Herds livestock; 20%/min chance to chase a player | 2 | 233 | Alpha |
| Scarecrow Decoy ◆ | 40 | Distracts crows/creatures 60 s | 1 | — | Alpha |
| Feed Silo | 450 | Livestock spawn 25% closer to the barn | 2 | 300 | Alpha |
| Padded Crates | 300 | Bruise ×0.8 in the wagon | 2 | 200 | Alpha |
| Barn Cellar | 500 | Store 6 goods overnight, freshness paused | 3 | 333 | Alpha |
| Better Nets | 200 | Net catch radius +30% | 1 | 133 | Alpha |
| Reinforced Lasso (Lv2) | 250 | Lasso range +40% | 2 | 167 | Alpha |
| Market Stall Sign | 350 | Demand +0.05 | 2 | 233 | Alpha |
| Radio Set | 300 | Global push-to-talk at 60% with static | 2 | 200 | Alpha |
| Wagon Tarp | 150 | Tie-down tarp (free in the VS build) | 1 | 100 | VS/Alpha |
| Big Net | 200 | Net holds 3 critters | 1 | 133 | Alpha |
| Earmuff Hat | 100 | Immune to the mandrake stun | 1 | 67 | Alpha |
| Burlap Sack ◆ | 20 | Mandrake loudness −70% | 1 | — | Alpha |
| Handheld Lantern | 200 | Freezes the Scarecrow 3 s (8 s cooldown) | 1 | 133 | Alpha |
| Lantern Posts | 250 | Creature aggro −25% near the lit path | 2 | 167 | EA |
| Hammer | 120 | Instant plank fix; patch 3 s → 1 s | 1 | 80 | EA |
| Rope Rail Netting | 250 | Bridge side nets catch falling goods | 2 | 167 | EA |
| Iron Wheels | 300 | Wagon wheel HP 100 → 200 | 2 | 200 | EA |
| Golden Bucket | 200 | Feed bucket 20 uses, lure radius 20 m | 1 | 133 | EA |
| Field Repair Kit ◆ | 35 | Re-attach a wheel in the field | 1 | — | EA |
| Hay Bale Ramps | 150 | Landing bruise −50% | 1 | 100 | EA |
| Pig Cannon | 900 | Fires goods across the river; +25 BP on landing | 3 | 600 | EA |
| Zipline | 800 | Second crossing, 1-hand goods only | 3 | 533 | EA |
| Insurance Policy | 400 | First 3 fees each day halved | 2 | 267 | EA |
| Cow Bell Collars | 200 | Livestock emit trackable noise | 1 | 133 | EA |
| Hat Rack ◆ | 30 | 4 hats for Sunday Best | 1 | — | EA |
| Rubber Boots | 150 | Mud slow −50%; immune to the Muddy Boots fine | 1 | 100 | EA |
| Stamina Tonic ◆ | 25 | +50% stamina, one player, one day | 1 | — | EA |
| Second Pen | 300 | Holding +8 | 2 | 200 | EA |
| Rival Traps | 350 | Slow rivals | 2 | 233 | EA |
| Crow Net | 300 | Blocks coin theft | 2 | 200 | EA |
| Morning Coffee | 200 | Morning +15 s, Workday +10 s | 1 | 133 | EA |
| Wheelbarrows ×2 | 250 | 2-slot one-player carts | 2 | 167 | EA |
| Lucky Horseshoe | 600 | One twist reroll per week | 3 | 400 | EA |
| Megaphone | 150 | Shout radius 2× (scares livestock) | 1 | 100 | EA |
| Confetti Shotgun ◆ | 60 | Scatters creatures within 15 m | 1 | — | EA |
| Scale & Ledger Book | 250 | Shows the sale value before acceptance | 2 | 167 | EA |

**Spend target (4p median):** ~$400 W1, $450 W2, $500 W3, $600 W4. That is ≈ 20% of gross early and 12–15% late.

**Status in build:** Planned. No shop or upgrades exist yet; the wallet already rejects upgrades the crew cannot afford.

---

## 10.3 Region Campaign

**Rules**
- Regions unlock in order at account level: clearing N unlocks N+1. The lobby region is the host's choice. A guest who helps clear a region also unlocks it.
- Each run is **5 paid weeks** (knob 4–6). The farm side is re-seeded every Monday (`WeekSeed = xxHash32(RunSeed, week)`): "the farm moved overnight". The bridge and the market are fixed per region.
- The content pool is the region's own species plus returning species at weight 0.5.
- PCG layout per farm (≈180×180 m): 1 barn, 3–5 pens, 4–8 fields, 2–4 hazard clusters, barn-to-bridge path 70–140 m. After 20 failed attempts it falls back to a hand-made layout.
- **Deed Day** after the 5th paid Sunday: no quota, all goods worth 2×. The Landlord hands over the deed and then tears it ("only the front half").

| # | Region → Town | R_mult | W1 / W5 rent (4p) | New critters | Night creature(s) | Hazards & signature | Landlord twist | Phase |
|---|---|---|---|---|---|---|---|---|
| 1 | Grandpa's Farm → Muddlebury | 1.00 | 1,200 / 3,990 | Pig, chicken, goat, cow, bull, sheep, duck, prize llama | Scarecrow, Barn Thing, Crows | Plank bridge, mud, river, hay avalanches; rivals Clem, Pruitt Twins, Mabel | Bridge Toll $5/crossing with goods | EA |
| 2 | Misty Riverlands → Ferrytown | 1.20 | 1,440 / 4,780 | Walking Catfish, Goose, Otter | Bog Lantern | 3 islands, rafts, currents, tide floods at 2/3 day, ferry every 90 s | Waterfront Property: wet goods decay 2×, dry delivery +20% | Update 1 |
| 3 | Haunted Orchard → Hollow Hill | 1.45 | 1,740 / 5,780 | Hiding Apples, Black Cat, Bat Swarm | Headless Picker, Orchard Widow, Cider Ghost | 40% night days, falling apples, sinkholes; gates shut at dark (late sales 50%) | Night Shift: dusk sales ×1.5, day ×0.9 | Update 2 |
| 4* | Sunburnt Ranch → Dustwater | 1.70 | 2,040 / 6,780 | Longhorns, Roadrunner, Armadillo | Copycat Coyote | Tumbleweeds, sun spoilage ×1.5, train every 4 min | Water Rights: troughs cost $ | Expansion idea |
| 5* | Frostbite Fjord → Icemouth | 2.00 | 2,400 / 7,970 | Yak, Penguin, Reindeer | Ice Troll | Ice friction 0.15, cracking bridge, Cold status | Heating Bill: 8% of quota per day | Expansion idea |

Full 4p rent by week: R2 1,440/1,940/2,620/3,540/4,780 · R3 1,740/2,350/3,170/4,280/5,780 · R4 2,040/2,750/3,720/5,020/6,780 · R5 2,400/3,240/4,370/5,900/7,970.

**Clear time:** 5 weeks × (5 × ~13 min + ~2 min) ≈ 5.6 h (≈ 6 h with shopping). Target: median Region 1 clear in 5–8 h, and ≥ 40% of teams fail at least one run first.

**Status in build:** Partial. The Muddlebury (Region 1) map is hand-built and playable through a full day. Region profiles, PCG, region select, Deed Day and Regions 2–5 are Planned.

---

## 10.4 Meta Progression

| System | Rule |
|---|---|
| **Grandpa's Letters** | 60 at EA in 4 tiers; each is a counter + threshold → reward. Examples: Welcome Home (first day → straw hat), Pork Logistics (50 pigs → pig-nose mask), Davy Jones (25 goods in the river → diving helmet), MVD ×5 (bronzed boot hat), Traitor (5 eggs pocketed → gold tooth), Evicted (3 failed runs → cardboard box backpack), Debt Free (clear R1 → starting tool option) |
| **Farm Memories (FM)** | `FM_day = 10 + 2 × min(Disasters, 10) + 5 × QuotaShareMet` (10–35, average ≈ 22); a failed run gives +50 |
| **Grandpa's Trunk** | Cosmetics at 60 / 120 / 240 FM (common/rare/legendary); 70 items ≈ 6,600 FM ≈ 60 h |
| **Almanac** | Entry on first encounter for every species, curse, creature, rival, weather, bruise, verb and region; 3 stars each; 100% → Grandpa's Hat |
| **Starting tools** | The crew picks 2 sidegrades from the unlocked pool (e.g. Long Lasso: +50% range, −30% pull) |

**Farmer Level (proposed).** XP = lifetime FM earned (spending doesn't reduce it).

```
XP_to_next(L) = 100 + 20 × (L − 1)
XP_total(L)   = 100(L − 1) + 10(L − 1)(L − 2)
```
| Symbol | Type | Range | Description |
|---|---|---|---|
| `L` | int | 1–50 (cap) | Farmer Level |
| `XP_total` | int FM | 0–23,520 | Lifetime FM needed to reach L |

| Level | 2 | 5 | 10 | 20 | 30 | 50 |
|---|---|---|---|---|---|---|
| Lifetime FM | 100 | 520 | 1,620 | 5,320 | 11,020 | 23,520 |
| Hours (≈110 FM/h) | ~1 | ~5 | ~15 | ~48 | ~100 | ~214 |

Worked example: Level 10 = `100 × 9 + 10 × 9 × 8 = 1,620 FM`. Rewards are cosmetic only (titles, receipt stamp colours, a Trunk token every 5 levels). Early levels arrive about once an hour, which supports the target of a new cosmetic every ≤ 45 min in the first 10 h.

**Status in build:** Partial. The cosmetic catalogue (hats, face, back, emotes) is Built with unlock-source tags (Starter/Letter/RegionClear/Trunk), but `unlockAllCosmetics = true`. Letters, FM, Trunk, Almanac and Farmer Level are Planned.

---

## 10.5 Run-Over Rules and Eviction Mode

**Campaign run over:** any unpaid Sunday after Collateral ends the run.

| Wiped | Kept |
|---|---|
| Wallet, upgrades, Landlord rules, run save, week | Letters, cosmetics, FM, Almanac, region unlocks, Farmer Level |

**Eviction Mode** (unlocked by clearing Region 1) is an endless quota ladder.
- Daily (one global seed per UTC day; one ranked attempt) or Custom (unranked).
- 3-day weeks + Sunday; starting wallet $150; each paid Sunday is a **Rung** and adds one permanent **Eviction Notice** (vote 1 of 3, 20 s).
- The Major cap starts at 2, +1 every 3 rungs, max 6.
- Score: rung, then total earned, then lowest crew Blame. Quitting mid-day forfeits the day.

```
EvictionQuota(r) = round_to_10( 400 × 1.28^r × S_p )     (round_to_10 proposed)
```
| Rung | 0 | 3 | 5 | 6 | 8 | 10 | 13 | 15 |
|---|---|---|---|---|---|---|---|---|
| 4p quota $ | 400 | 840 | 1,370 | 1,760 | 2,880 | **4,720** | 9,900 | **16,230** |

Correction: `eviction-mode.md` lists Rung 10 ≈ $4,730 and Rung 15 = $16,160. The formula gives $4,722 and $16,226. Targets: median Rung 6, top 1% Rung 13+. The quota is clamped at $9,999,999.

**Status in build:** Partial. Campaign eviction (EVICTED stamp, Try Again restarting at W1 with $100) is Built. Collateral and Eviction Mode are Planned.

---

## 10.6 Blame Ledger and End-of-Day Awards

Money is shared, guilt is not. Every loss is a Blame Event attributed to the last 3 touchers within 3 s at shares 0.6 / 0.3 / 0.1 (renormalized for fewer touchers). With no toucher, the loss goes to "Nature" ("The Wind"). Events within 1.5 s merge, and losses under $2 are dropped.

```
Severity   = clamp(100 × |Δ$| / (Quota / 5), 0, 100) + VerbBonus     (clamped 0–100)
BlameScore = Σ |loss| × share + 15 × Disasters − 0.5 × PositiveDollars   (floor 0)
```
VerbBonus: river drop +25, tipped wagon +30, summoned creature +20, knockout/drowned/stranded +10, yeeted teammate +15. Severity ≥ 60 is a **Disaster** (Camcorder clip; target 3–6 per day).

Worked example: W1 4p, share $240; a $150 cow lost in the river → `62.5 + 25 = 88` → Disaster.

| Award (Sunday) | Winner |
|---|---|
| Most Valuable Disaster | Highest Blame Score (trophy: bronzed boot hat for a week) |
| Butterfingers | Most Dropped + Threw |
| Bridge Troll | Most BrokePlank + DroppedInRiver |
| Pig Whisperer | Highest positive $ |
| Town Crier | Most SpookedHerd + SummonedCreature |

Ties go to the earliest qualifying event. Solo play gets MVD + Pig Whisperer only. If everything was Nature, MVD goes to "The Weather". **Betrayal:** a pocketed Golden Egg is secret until Sunday, when its owner chooses Donate ($150) or Keep (cosmetic tally). The host can toggle this off.

**Status in build:** Partial. Attribution, merge, noise floor, severity, disasters, the daily receipt, weekly counters (Butterfingers, Bridge Troll, Town Crier) and MVD are Built. Award presentation beyond MVD and the betrayal reveal are Planned.

---

## 10.7 Difficulty and Player-Count Scaling

| Lever | Scales with | Values |
|---|---|---|
| Quota, order quantity | Players (`S_p`) | 0.45 / 0.65 / 0.85 / 1.00 |
| Quota | Week / region | ×1.35/week; R_mult 1.00–2.00 |
| Respawn fee | Week | $25 → $55 (not by players; see Ch. 9 open issue 5) |
| Chaos cap | Week | 2 / 3 / 4 |
| Landlord rules / visits | Week | 0 → 3; 0 → 2 visits |
| Night budget | Day, players outside | `10 + 4d + 3p` |
| Rival visit chance | Day | `min(0.5, 0.15 + 0.05d)` |
| Assists (unranked) | Player option | Critter speed −20%, Workday +20% (540 → 648 s), bruise −50% |

Assists never penalise meta progression but make Eviction runs unranked. Join-in-progress never changes the week's quota. A leaver re-scales it pro rata (Ch. 8 §8.8).

**Status in build:** Partial. `S_p` scaling, weekly fee and chaos cap are Built. Assists, night/rival scaling and Landlord visits are Planned.
