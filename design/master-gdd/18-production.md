# 18. Production Plan & Status

> **Owner**: Technical Director with Producer · **Status date**: 2026-10-20
> **Baseline**: `main` — EditMode 129/129, PlayMode 24 pass / 7 skipped, two-process LAN test PASS. Active branches: `feature/audio`, `feature/shop-progression`, `feature/weather`.

This chapter answers three questions: what exists today, what we build next and in what order, and what could go wrong. Week estimates assume the current team model (one owner directing a set of AI specialist agents, 18.5) and are ranges, not promises.

---

## 18.1 Feature Status Matrix

**Built** = playable in Muddlebury and covered by tests. **Partial** = core works, listed pieces missing. **In progress** = active branch. **Planned** = design only.

| # | System | Status | What exists / what is missing | GDD |
|---|---|---|---|---|
| 1 | Session & Networking | **Built (LAN)** | Host/join, approval, roster, kick, chat, LAN discovery, scene sync, two-process test. Missing: Steam verified end-to-end, host migration-lite, bad-ping UI | [session-networking](../gdd/session-networking.md) |
| 2 | Player Character | **Built** | Walk/sprint/jump/crouch, stamina, ragdoll, knockout, revive, barn respawn, first-person arms | [player-character](../gdd/player-character.md) |
| 3 | Physics Carry & Tethers | **Partial** | Multi-hand spring carry, drag, charged throw, bruise/blame reporting. Missing: lasso/rope tethers, fumble anim | [physics-carry](../gdd/physics-carry.md) |
| 4 | Proximity Voice & Noise | **Partial** | Noise Events from impacts, emotes, movement; critters react. Missing: voice chat entirely | [proximity-voice](../gdd/proximity-voice.md) |
| 5 | Critter AI | **Built** | NavMesh brain, flee/lure/flock, spawn director, procedural anim, critter goods. Missing: LODs | [critter-ai](../gdd/critter-ai.md) |
| 6 | Goods Value & Bruising | **Built** | 7 goods (pig, chicken, egg, egg crate, carrot, turnip, prize pumpkin), bruise + freshness | [goods-value](../gdd/goods-value.md) |
| 7 | Farm Tools | **Partial** | Net, feed bucket. Missing: lasso, whistle, tool racks | [farm-tools](../gdd/farm-tools.md) |
| 8 | Wagon & Vehicles | **Planned** | — | [wagon-vehicles](../gdd/wagon-vehicles.md) |
| 9 | Day Loop | **Built** | Morning → Workday → Dusk → Payout, order, timer, market pen, full-day playtest | [day-loop](../gdd/day-loop.md) |
| 10 | Shared Wallet & Economy | **Built** | One wallet, fees, IOU, market, receipts | [shared-wallet-economy](../gdd/shared-wallet-economy.md) |
| 11 | Bridge & Hazards | **Built** | Breakable planks, river spit-out pile, mud zones. Missing: sway tuning, storm wash-out | [bridge-hazards](../gdd/bridge-hazards.md) |
| 12 | Livestock Roster | **Partial** | Pig, chicken playable; cow, bull, sheep, alpaca, pigeon models imported, not wired | [livestock-roster](../gdd/livestock-roster.md) |
| 13 | Week Loop & Quota | **Partial** | Sunday resolution, quota, carry-over, eviction, quota proration on join/leave. Missing: Sunday ceremony polish, long-run tuning | [week-quota](../gdd/week-quota.md) |
| 14 | Farm Upgrades & Shop | **In progress** | `feature/shop-progression` | [farm-upgrades](../gdd/farm-upgrades.md) |
| 15 | Cursed Crops | **Partial** | Prize pumpkin as fragile goods. Missing: all curses | [cursed-crops](../gdd/cursed-crops.md) |
| 16 | Night Creatures | **Planned** | — | [night-creatures](../gdd/night-creatures.md) |
| 17 | Weather & Twists | **In progress** | Fog/Heatwave/Storm modifier data; `feature/weather` | [weather-twists](../gdd/weather-twists.md) |
| 18 | Rival Farmers | **Planned** | — | [rival-farmers](../gdd/rival-farmers.md) |
| 19 | Landlord Events | **Partial** | 5 Landlord rule modifiers (Early Curfew, Landlord's Cut, No Running on Bridge, Quiet Hours, Respawn Paperwork). Missing: inspections, walking Landlord, deals | [landlord-events](../gdd/landlord-events.md) |
| 20 | Blame Ledger & Awards | **Built** | 23 verbs, itemised receipts, Most Valuable Disaster, pocketed-goods tracking | [blame-ledger](../gdd/blame-ledger.md) |
| 21 | Grandpa's Camcorder | **Planned** | — | [camcorder-replay](../gdd/camcorder-replay.md) |
| 22 | Region Campaign & Layouts | **Planned** | Muddlebury is one hand-built level | [region-campaign](../gdd/region-campaign.md) |
| 23 | Save & Persistence | **Partial** | Settings + loadout in PlayerPrefs. Missing: Run Save, Account Profile | [save-persistence](../gdd/save-persistence.md) |
| 24 | Meta Progression | **Planned** | — | [meta-progression](../gdd/meta-progression.md) |
| 25 | Eviction Mode | **Planned** | — | [eviction-mode](../gdd/eviction-mode.md) |
| 26 | HUD & Menus | **Built** | Centre HUD, clock ribbon, receipts, pause, settings. Shop UI in progress | [hud-menus](../gdd/hud-menus.md) |
| 27 | Audio & Feedback | **In progress** | `feature/audio`; game currently near-silent | [audio-feedback](../gdd/audio-feedback.md) |
| 28 | Onboarding & Accessibility | **Planned** | Settings panel only | [onboarding-accessibility](../gdd/onboarding-accessibility.md) |
| 29 | Character Customization | **Built** | Loadout, wardrobe + preview, emotes, name tags, ping | [character-customization](../gdd/character-customization.md) |
| 30 | Frontend & Lobby | **Built (LAN)** | Title, host/join, Lobby Yard, dinner-bell start, pause, settings. Missing: Steam invites UI, server browser, rejoin | [frontend-lobby](../gdd/frontend-lobby.md) |
| 31 | Social Comedy Features | **Partial** | Emotes, ping. Missing: friend throwing, Crew Photo, megaphone etc. | [social-comedy-features](../gdd/social-comedy-features.md) |

**Totals**: 12 Built, 10 Partial, 3 In progress, 6 Planned (of 31).

---

## 18.2 Milestones

| Milestone | Goal | Content target | Exit criteria |
|---|---|---|---|
| **Vertical Slice** — *done* | Prove chasing and carrying together is fun | 1 farm (Muddlebury), pig + chicken + prize pumpkin, bridge plank break, day loop, payout receipt, 2–4 players on LAN | Full day playable; tests green; two-process test passes |
| **Alpha** — *current* | Complete week loop, all core verbs | Shop with ~10 upgrades, 3 weather cards, Landlord rules, week + Sunday, audio pass 1, Run Save, lasso + whistle, wagon, 4 species, Steam lobbies working | 4 friends play a full week over Steam without a blocker |
| **Early Beta** | Outside players, stability | 5 species, 2 cursed crops, Scarecrow (first night creature), Camcorder v1, voice chat, onboarding tape, critter LODs, Windows build | Definition of Done (18.6) met |
| **Steam Demo / Next Fest** | Wishlists | Day 1–3 of week 1, capped; trailer; store page; Camcorder export for clips | Crash-free rate ≥ 99%; demo session median ≥ 25 min |
| **Early Access** (~$9.99) | Launch | Region 1 complete, 8 species, 2 night creatures, Rival Farmer, ~30 upgrades, cosmetics + Almanac, Eviction Mode, Account Profile + Steam Cloud | Store-ready; review score target "Very Positive" |
| **1.0** | Full game | Regions 2–3 (Riverlands with rafts, Haunted Orchard), PCG layouts, seasonal events | Content complete, localisation, controller support |

---

## 18.3 Roadmap (week estimates)

```
Week:        1   2   3   4   5   6   7   8   9  10  11  12  13 ...
Alpha       [=======================]
  shop/upgrades  ===
  weather         ====
  audio pass 1   =====
  run save          ===
  steam e2e          ==
  wagon/lasso/whistle  ====
Early Beta                          [===================]  (3-5 wks)
Demo prep                                                [========]
```

| Phase | Estimate | Main work |
|---|---|---|
| Alpha completion | **5–7 weeks** | Merge shop, weather, audio branches; Run Save; Steam end-to-end with real App ID; wagon, lasso, whistle; 2 more species; Landlord inspection event |
| Early Beta | **3–5 weeks** after Alpha | Voice chat (after ADR), Scarecrow, Camcorder v1, onboarding, LODs + performance pass, Windows build + cross-play, bug bash |
| Steam Demo / Next Fest | 3–4 weeks | Demo cut, store page, trailer capture, Steam review; submit ≥ 6 weeks before the chosen Next Fest |
| Early Access | ~4–6 months after Beta | Region 1 content breadth, meta progression, Eviction Mode |
| 1.0 | EA + 6–9 months | Regions 2–3, PCG, seasonal content |

---

## 18.4 Work Breakdown for the Next Sprint

1. Merge `feature/shop-progression`, `feature/weather`, `feature/audio` through `integration/*` with all suites green.
2. Register a Steam App ID; replace 480; run two machines over Steam.
3. Install Windows Build Support; produce the first Windows build.
4. Implement Run Save (shop boundary) with checksum and backup.
5. Confirm Tripo commercial rights; add Facepunch MIT licence entry.

---

## 18.5 Team and Agent Structure

The project owner is the only human: creative director, final approver and playtester. Work is executed by Claude Code agents with fixed domains, mirroring a small studio.

| Tier | Roles | Responsibility |
|---|---|---|
| Directors | Creative, Technical, Producer | Vision, architecture, schedule; escalation point |
| Leads | Lead programmer, game designer, art, audio, narrative, QA lead | Own a department, review specialists |
| Unity specialists | unity-specialist, UI, shader, addressables, DOTS | Engine-specific implementation guidance |
| Feature teams (code ownership, 17.2) | Networking, Player, Critter AI, Economy+HUD, World, Customization, Frontend UI | One assembly each; talk only through Core contracts |

Rules: one branch per task, the owner approves every write and merge, contracts are add-only and owned by the lead, and Networking alone edits packages and project settings.

---

## 18.6 Risk Register (Top 10)

| # | Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|---|
| 1 | Steam path fails (relay, invites, init on Mac) | Medium | High | Real App ID now; two-machine Steam test each sprint; LAN fallback kept |
| 2 | No Windows build yet; platform-specific bugs late | High | High | Install module this sprint; weekly Windows build; cross-play test |
| 3 | Voice chat provider unsuitable (Mac mic, 3D, loudness) | Medium | High | ADR + spike before Beta; non-voice fallbacks for every voice mechanic |
| 4 | Carry feels laggy for clients at real ping | Medium | High | Test at 100–150 ms simulated latency; cosmetic client smoothing fallback |
| 5 | Performance with 30 critters, no LODs, 4 players | Medium | Medium | Add LODs; profiler captures in full-day test; budgets in 17.7 |
| 6 | Tripo asset rights unclear | Medium | High | Owner confirms plan terms; Quaternius CC0 fallbacks already in project |
| 7 | Save corruption / version drift breaks runs | Low | High | Atomic write, CRC, backup, migrations, fault-injection test |
| 8 | Scope creep (31 systems, solo owner) | High | High | Milestone gates; Planned items stay out until milestone; fun-per-effort ranking |
| 9 | Merge conflicts in shared scenes/prefabs | Medium | Medium | Prefab split, Unity SmartMerge, one owner per scene |
| 10 | Not funny / not sticky enough for streamers | Medium | High | Weekly 4-player playtests; laugh-within-3-minutes metric; Camcorder for clips |

---

## 18.7 Definition of Done — Early Beta

A Beta build is done when **all** of the following are true:

1. Builds for macOS (signed and notarised) and Windows from batchmode with no manual steps.
2. 4 players complete a full week (5 days + Sunday) over **Steam** on both platforms, including Mac ↔ Windows cross-play.
3. All EditMode and PlayMode suites pass; two-process test passes; no skipped test without a reason.
4. 60 fps on Mac Medium at 4 players in Muddlebury; never below 30 fps; host network ≤ 2 ms/frame.
5. Run Save survives quit/resume and process-kill tests.
6. Join-in-progress and client drop work without soft-locks; host loss returns everyone to the lobby.
7. Voice chat or its fallbacks fully usable; onboarding tape teaches grab, carry, sell in under 5 minutes.
8. No known crash; zero open P0/P1 bugs; 30-minute soak with 4 players without memory growth > 10%.
9. Every asset is listed in `THIRD_PARTY_LICENSES.md` with confirmed commercial rights.
10. Audio pass 1 in: impacts, critter barks, UI, music states.
