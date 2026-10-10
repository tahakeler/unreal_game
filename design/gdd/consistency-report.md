# GDD Consistency Report: HARVEST PANIC

> **Date**: 2026-10-10 · **Reviewer**: game-designer (lead designer authority) · **Scope**: all 28 GDDs in `design/gdd/` + systems-index + game-overview
> **Method**: holistic cross-GDD review (review-all-gdds) + grep-first conflict scan (consistency-check). Each conflict below was resolved by editing the GDDs. Rule used: the **contract or data owner wins** unless doing so breaks a worked example or a pillar.

## Canonical values (single source of truth after this pass)

| Topic | Canonical value | Owner |
|---|---|---|
| Player walk / sprint / crouch | 4.0 / 6.5 / 2.0 m/s = **400 / 650 / 200 cm/s** | player-character |
| Bruise impulse threshold `I_min` | **400 N·s** | goods-value |
| Blame touch window | **3 s**, last 3 touchers, shares 0.6/0.3/0.1 | blame-ledger |
| RegionDifficulty (R_mult) | **1.00 / 1.20 / 1.45 / 1.70 / 2.00** | region-campaign |
| Regions × weeks | **5 regions** (4–5 are expansion ideas) × **5 paid weeks** (knob 4–6) | region-campaign |
| Respawn fee | **Per week: $25/30/40/45/50/55** (× `Economy.RespawnFeeMult`), only on barn respawn | shared-wallet-economy |
| Repairs | Plank patch **$30**, wheel **$50**, fence **$20**, wagon recovery **$40**, tool restock **$15** | shared-wallet-economy |
| Overdraft | Floor **−$200**; overflow → Landlord IOU added to this week's Due; upgrades need Balance ≥ price; theft never below $0 | shared-wallet-economy |
| Goods base values / fragility / hands | goods-value table | goods-value |
| `CarryHands` unit | **hands** (player = 2 hands; carriers = ceil(hands/2)) | goods-value |
| Critter "can't break free" | `SecureCarriers` (players) | critter-ai |
| Player-count scale S_p | **0.45 / 0.65 / 0.85 / 1.00** | week-quota |

## Conflicts found and resolved

| # | Conflict | Resolution | Files changed |
|---|---|---|---|
| 1 | Player speed: 4.0/6.5 m/s (player-character) vs 350/600 cm/s assumed by critter-ai | player-character wins: 400/650 cm/s. Critter speeds unchanged (pig 560 < sprint 650 so it can be run down; bull 650 = sprint, intentional) | player-character, critter-ai, livestock-roster |
| 2 | Bruise threshold 300 N·s (physics-carry) vs 400 N·s (goods-value) | 400 N·s (goods-value owns the bruise model; its worked example of 16 BP depends on it). physics-carry mirrors it; voice "light impact" band now 400–800 | physics-carry, proximity-voice |
| 3 | Touch window 3 s (physics-carry, goods-value) vs 4 s / last 3 touchers (blame-ledger) | **3 s** + last-3-toucher history with shares. physics-carry supplies the history; the Ledger owns the knob (range 2–5) | blame-ledger, physics-carry, goods-value, critter-ai |
| 4 | Region multipliers 1.0/1.2/1.45/1.7/2.0 (region-campaign) vs 1.0/1.6/2.4 (week-quota) | region-campaign wins (owns the Region Profile; gentler curve on top of 1.35×/week). week-quota R_mult now reads it | week-quota |
| 5 | Region run length "4–6 weeks" (overview) vs 5 (region, week-quota); clear-time math used "6 days/week" | 5 weeks for all regions (knob 4–6). Clear time recomputed (≈5.6–6 h) | game-overview, region-campaign |
| 6 | Plank repair $10 (bridge) vs $30 (economy); wheel $20 vs $50; recovery $40 / tool $15 missing from economy | Economy prices win (worked example and AC 6 use $30 planks). Recovery $40 and tool $15 added to the economy sink table. Tutorial receipt changed to −$30 | bridge-hazards, wagon-vehicles, farm-tools, shared-wallet-economy, onboarding-accessibility |
| 7 | Respawn fee $25 + $10/death cap $75 (player-character) vs per-week $25–55 (economy) | Per-week schedule wins (economy owns sinks; matches week-quota escalation). The player-character formula was removed | player-character, shared-wallet-economy |
| 8 | What triggers a fee: "every knockout" (economy, night-creatures) vs only OOB/drowning (player-character) | Fee only on **barn respawn**: drowning/OOB, night-creature KO, stranded at day end. Daytime KOs recover in place and are free | player-character, shared-wallet-economy, night-creatures, day-loop |
| 9 | Debt handling: "wallet goes negative, debt added to quota" (player-character) was looser than the economy's −$200 floor + IOU | Economy rule spelled out everywhere | player-character |
| 10 | Crows: steal coins $10/hit (economy, overview, Crow Net) vs only tools/goods (night-creatures) | Crows do both. Coin theft $10/hit, max $50/day, never below $0, blocked by Crow Net | night-creatures, shared-wallet-economy, farm-upgrades |
| 11 | Host quit: any player can resume from a replicated checkpoint (session) vs host-only saves, "different host not supported" (save) | Migration-lite: the Shop-phase Run Save is replicated to roster clients, so any roster player can host the Resume. No live migration | session-networking, save-persistence, week-quota, blame-ledger, game-overview |
| 12 | Join-in-progress: any phase except Sunday payout (session) vs spectate until Payout (day-loop) vs between days only (overview) | session-networking (owner) wins: spawn at the barn with 2 s protection | day-loop, game-overview |
| 13 | Noise Event fields: critter-ai used `{source, position, loudness, type}` with types `crop_scream`… vs the owner's `FNoiseEvent {Source, Loudness, Radius, Location, Instigator, BlamedPlayer, Tags}` | Consumers now use the owner fields. Per-type multipliers are keyed by `ENoiseSource`. Threat falloff uses the event's `Radius`. Tags `Noise.Food/Whistle/WhistleLong/Squelch/Creak/PlankBreak/Squeal` registered | critter-ai, night-creatures, cursed-crops, farm-tools, bridge-hazards, livestock-roster, proximity-voice |
| 14 | Loudness: Holler 0.9 / 6 s (onboarding) vs shout emote 0.8 / 2 s (player-character, voice table, rivals) | Holler = shout emote = 0.8 / 2 s. Shush = quiet-walk. Added feed bucket 0.4, plank crack 0.3 and critter calls to the table. The critter-ai AC used "quiet walk 0.1" (quiet-walk emits nothing), so it now says walk footstep 0.10 | onboarding-accessibility, player-character, proximity-voice, critter-ai |
| 15 | Critter Personality missing fields used by consumers | Added `SizeClass`, `Agility`, `CritterPullN`, `SecureCarriers`, `StruggleImpulse` (N·s) + interval 0.5–1.5 s, `ChargeImpulse` (N·s, replacing `ChargeKnockback` in N), `NoiseSourceMult`, `NoiseTagResponse` (Noise.Food → Lure, Noise.Whistle → Flee, WhistleLong → Rally), `GrumpyMods` (Goods.Grumpy: flee ×1.5), `PenEscapeChance` (World.PenGateOpened, e.g. the Landlord's Farm Walk). Per-species values in livestock-roster | critter-ai, livestock-roster, physics-carry, farm-tools, wagon-vehicles, landlord-events |
| 16 | Bull charge 1800 **N** vs player ragdoll in **N·s**; goat headbutt 600 N | Units → N·s. Bull 1800 N·s = 2× RagdollImpulse = KO; goat 600 N·s | livestock-roster, player-character |
| 17 | Blame verbs used by emitters missing from the Ledger (`drowned`, `yeeted_teammate`, `slipped`, `Crashed`, `Hit`, `broke_plank`, `fell_in_river`, `escaped`, `stunned`, `lassoed_into_river`, `whistled_away`, `summoned_scarecrow`, `let_clem_steal`, wagon bounce-out, stranded) | Verb table grown to 23 add-only PascalCase verbs (+Drowned, Stranded, YeetedTeammate, Slipped, Crashed, SpilledCargo, LassoedIntoRiver). Others mapped: escaped→LetEscape, whistled_away→SpookedHerd, summoned_scarecrow→SummonedCreature, let_clem_steal→Stolen, fell_in_river→DroppedInRiver/Drowned, Hit→Bruised. `stunned` dropped (no $ impact). All emitter docs updated | blame-ledger, player-character, physics-carry, goods-value, bridge-hazards, wagon-vehicles, farm-tools, critter-ai, night-creatures, rival-farmers, landlord-events, day-loop, systems-index |
| 18 | Region critters/creatures missing from the rosters | Stub entries added: livestock-roster §3.4 (Catfish, Goose, Otter, Black Cat, Bat Swarm, Longhorns, Roadrunner, Armadillo, Yak, Penguin, Reindeer); night-creatures §3.4 (Bog Lantern, Headless Picker, Orchard Widow, Cider Ghost, Copycat Coyote, Ice Troll); region curses in cursed-crops; region weather cards in weather-twists | livestock-roster, night-creatures, cursed-crops, weather-twists, region-campaign |
| 19 | Upgrades referenced but not in the shop | Added Wagon Tarp, Big Net, Earmuff Hat, Burlap Sack ◆, Handheld Lantern, Hammer, Rope Rail Netting, Iron Wheels, Golden Bucket, Field Repair Kit ◆. Radio Set clarified, Lasso Lv2 aliased "Reinforced Lasso". Electric Fence and Bigger Wagon already existed. Catalog ≈ 40 | farm-upgrades, systems-index, game-overview |
| 20 | Bigger Wagon "slots 4→10" vs the wagon's physical 400 kg soft cap | Bigger Wagon raises the soft cap to 550/700/850 kg with a larger bed | farm-upgrades, wagon-vehicles |
| 21 | Bridge Reinforcement: SafeLoad → 400 kg (bridge) vs PlankBreakMult ×0.7/×0.45 (upgrades); storm/Mabel "break threshold" undefined | PlankBreakMult ×0.7/×0.45 wins. New Day param `Bridge.SafeLoadMult` covers storm ×0.7 and Mabel ×0.5 per plank. Both params are in the damage formula | bridge-hazards, farm-upgrades, weather-twists, rival-farmers, day-loop |
| 22 | Goods base values/hands/fragility differ between livestock-roster/cursed-crops and goods-value (pig 120 vs 60, cow 300 vs 150, llama 220 vs 400, turnip 30 vs 10, mandrake 150 vs 45, pumpkin 200 vs 300…) | goods-value table is canonical (its ACs, the $450 pumpkin, the economy model). Roster tables now mirror it with numeric fragility. Missing EA crops added to goods-value | livestock-roster, cursed-crops, goods-value |
| 23 | `CarryHands` meant players (goods "1–4 players", critter-ai) and hands (physics-carry "6-hand cow") | Hands everywhere (1–8). Carriers = ceil(hands/2). Break-free logic moved to `SecureCarriers` | goods-value, physics-carry, critter-ai, livestock-roster, cursed-crops, game-overview |
| 24 | Prize Pumpkin vs Growing Pumpkin merged in cursed-crops (VS pumpkin grows, $200) but separate in goods-value/overview | Split: Prize Pumpkin (VS, $300, 4 carriers, splat = +60 BP) and Growing Pumpkin (Alpha, $40 ×1.05/10 s, 1→4 carriers) | cursed-crops, goods-value |
| 25 | Tiers: Bull EA vs Alpha, Sheep Alpha vs EA (roster vs goods/overview/index "Alpha (5)"); Maze Corn Alpha makes 4 Alpha curses vs "Alpha (3)" | Bull Alpha, Sheep EA. Maze Corn moved to EA | livestock-roster, cursed-crops, systems-index |
| 26 | Night-creature counts: index "EA (2)", overview "2 night creatures", Region 1 lists only Scarecrow + Crows (Barn Thing missing); rival named "Pruitt brothers" | 3 creatures at EA (Scarecrow, Barn Thing, Crows); Region 1 lists all three; rivals per rival-farmers | systems-index, game-overview, region-campaign, night-creatures |
| 27 | Sunday order: Ledger audit "before quota payment" vs week-quota reveal at step b, awards after Paid; Donate vs "never to wallet" | Sequence: arrival → SundayAudit (awards + reveal, Donate/Keep) → payment | week-quota, blame-ledger |
| 28 | Severity example used a $600 W1 quota (actual $1,200); WeeklyQuota min 300 (actual 540) | Example and range fixed | blame-ledger |
| 29 | HUD "$412 / $600 today-share" (share = Q/5 = $240) | Corrected | hud-menus |
| 30 | Shop vote 10 s (farm-upgrades) vs 5 s veto (hud) | 10 s vote | hud-menus |
| 31 | S_p 0.5/0.7 (day-loop orders), 0.7 (eviction) vs 0.45/0.65 (week-quota) | Unified to week-quota | day-loop, eviction-mode |
| 32 | Economy upgrade range $100–$1,500 vs catalog $20–$1,100; Bridge Toll sink missing | Range fixed; region-twist sinks listed | shared-wallet-economy, region-campaign |
| 33 | Region 1 weather deck names (sun/wind) didn't match weather-twists cards; Regions 2–3 used undefined cards | Card names aligned; region deck overrides defaults; Drizzle/Flood/Blood Moon stubbed | region-campaign, weather-twists |
| 34 | Mud: Mud Day ×0.8 speed vs bridge-hazards mud ×0.6 | Mud Day grows mud volumes and uses the bridge-hazards mud rules | weather-twists |
| 35 | Water bruise named "Soggy" (critter-ai, roster) vs "Soaked" (bridge-hazards) | `Soaked` | critter-ai, livestock-roster |
| 36 | "Dinner Bell" award (night-creatures) doesn't exist | → Town Crier | night-creatures |
| 37 | Landlord "Quiet Hours" and weather env_mult had no registered Day params | `Noise.VoiceRadiusMult`, `Noise.EnvMult` registered | day-loop, proximity-voice, landlord-events |
| 38 | Overview creature blurbs contradicted the roster (sheep follow loudest player; cow kicks) | Aligned to livestock-roster | game-overview |
| 39 | Eviction Rung 15 quota stated $16,300 (formula gives $16,160) | Fixed | eviction-mode |
| 40 | Mandrake "earmuffs protect from stun" but no stun existed; radius 40 m vs formula | Defined 1 s stun within 3 m; radius ≈ 43 m from the voice formula | cursed-crops |
| 41 | Overview open question on betrayal opt-in already answered by blame-ledger | Marked resolved | game-overview |

## Open questions for the user

1. **Respawn fee feel:** I removed the per-death escalation ($25 + $10 each death) in favour of the per-week schedule. Do you want a per-player escalating *surcharge* on top, to keep the "Dave died 4 times" sting?
2. **Repair prices:** I set the plank patch to $30 (the economy value) instead of $10. A $10 patch fits "cheap to recover" better, but the Week 3 worked example and acceptance test would need to be recalculated. Confirm $30.
3. **Livestock values halved:** I used the goods-value prices (pig $60, cow $150, bull $200), so Batch B's higher prices are gone. The economy model ($400/day in W1) should be re-checked with `/balance-check` against real order mixes.
4. **"Skinwalking Coyote" renamed to "Copycat Coyote"** for cultural-sensitivity reasons. Keep the rename?
5. **Bull walk/sprint 650 cm/s equals the player sprint.** Is this intended ("you never outrun the bull"), or should it be 700?
6. **Wagon Tarp in VS:** granted free in the VS build and sold from Alpha. Or should it always be free?
7. **Region 4–5 (Expansion ideas):** the RegionDifficulty values and roster stubs are placeholders. Confirm that the 5-region scope stays in the plan.
8. **Morning phase:** day-loop has a 30 s Morning while hud-menus auto-closes the order screen at 12 s. These are compatible (overlay vs phase) and were left as they are. Confirm.
