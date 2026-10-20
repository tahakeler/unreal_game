# 14. UI, HUD, Menus & Accessibility

> **Sources**: `design/gdd/hud-menus.md`, `frontend-lobby.md`, `onboarding-accessibility.md`, `camcorder-replay.md`, art-bible §7; built state from `HarvestPanic/Assets/Scripts/UI/`, `Customization/` and `Economy/Hud/`.
> **Tech**: Unity **UI Toolkit** (UXML + USS) for every screen; keyboard, mouse and gamepad navigation through the Input System UI module.

**Rule: diegetic first.** Look at the world, not the corners of the screen. During the day only a few small HUD elements are on screen; everything else lives on props (Grandpa's pocket watch, the wagon manifest clipboard, market price chalkboards). Between days, menus feel like paperwork from a hostile bank: funny, fast, never a chore.

---

## 14.1 Screen Flow

```
Boot / splash (<=3 s, skippable)
   |
Title ("HARVEST PANIC!")  --Steam invite accepted--> skips straight to Lobby
   |-- Play --> Host a Farm | Join a Farm (LAN list / address / Steam friends)
   |-- Customize (points you to the lobby wardrobe)
   |-- Settings
   |-- Quit
   v
Lobby Yard (walkable)  -- porch = ready, host rings dinner bell, 3 s countdown
   v
+--> Morning (30 s: order + twist card) --> Workday (9 min) --> Dusk (optional)
|          --> Payout Receipt --> Shop (vote) --> Barn / Next Day ---+
|                                                                  |
+----------------------------- x5 days ---------------------------+
   v
Landlord Sunday (awards, audit, quota payment) --> pass: next week | fail: run end
```

Targets: launch → first input in the yard ≤ 25 s; Payout → Shop → next day ≤ 40 s if everyone readies; every between-day screen shows a ready row per player and advances when all are ready or its timer ends.

| Screen | Key content | Timer | Status |
|---|---|---|---|
| Title | Play, Customize, Settings, Quit; footer shows version, Steam/LAN mode and control hints | — | Built |
| Host | Crew size 1–4, transport, "Open the Gate"; solo warning | — | Built |
| Join | LAN list with Refresh, address field, Steam Friends | — | Built |
| Connecting / errors | "Knocking on the farm gate…", Cancel; plain-language failures | — | Built |
| Lobby Yard | Crew overlay, chat, ready, dinner bell | — | Built |
| Morning Order | Manifest (≤ 6 items), twist cards; closes at 12 s or all ready | 12 s | Planned |
| Day HUD | 14.2 | — | Partial |
| Payout Receipt | Earnings, per-player receipt, headline tape, letters | ≤ 25 s | Partial |
| Shop | Upgrade grid, wallet, 10 s purchase vote (majority, host breaks ties) | 90 s soft | Planned |
| Sunday Awards | 5 awards, audit, quota payment animation | ~45 s | Planned |
| Camcorder Shelf/Viewer | Chapter 12.7 | — | Planned |
| Pause | 14.4 | — | Built |

---

## 14.2 Day HUD

Total HUD coverage ≤ **8% of the screen**. Four persistent elements:

```
+--------------------------------------------------------------------+
|                    $412  ▲   today's share $240                    |
|                    (sun disc on top edge turns coral at dusk)      |
|                                                                    |
|     [Sam ■]                                        [Jo ▲]          |
|      (teammate tag)                    (edge marker if off-screen) |
|                                                                    |
|                              +                                     |
|                         [E] Grab pig                               |
|                                                                    |
|  .---.                                                             |
| ( 3:12)  pocket watch            [✋✋  Pig $60, bruised ▼]          |
|  '---'                            (carry indicator, only carrying) |
+--------------------------------------------------------------------+
```

| Element | Rules |
|---|---|
| **Pocket watch** (bottom-left, ~140 px @1080p) | Sundown countdown; turns red and ticks in the last 60 s. |
| **Wallet + day target** (top-centre) | Shared money and today's share of the weekly quota (quota ÷ 5). Gains float up in gold with ▲, losses drop in red with ▼ and a small "ow" flinch. |
| **Carry indicator** (bottom-centre) | Only while carrying: hands needed (1–4 icons), item value, bruise tint and shape-coded bruise tier. |
| **Teammate markers** | Name + shape + status over heads; clamped to screen edges when off-screen, fading from 60 to 80 m; hidden in fog unless in voice range. |
| Contextual prompt | World-anchored near the object, one at a time ("[E] Grab", "[E] Hitch"). |
| Disaster toast | 2.5 s from the top: "−$90 Dave → river"; only for severity ≥ 40; merges bursts ("−$310 (4 disasters)"). |
| Voice indicator | Speaking ring; "TOO LOUD" wobble only when your noise > 0.6. |

Status in build: **Partial** — the economy HUD (wallet, day clock) and contextual interaction prompts run in the Muddlebury day; carry indicator, edge markers, toasts and voice ring are planned.

---

## 14.3 Lobby Yard

A small walkable farmyard in front of Grandpa's porch, not a menu. Players mess around while friends join.

```
+--------------------------------------------------------------+
| THE YARD   Lobby  1 / 4 farmers                              |
|                                                              |
|                     Dinner's up!   3                         |
|                                                              |
|  [R] Ready   [Tab] Crew   [C] Wardrobe   [B] Dinner bell   [Esc] Menu |
+--------------------------------------------------------------+
  Tab -> THE CREW: list with colour+shape pip, ready check, host crown,
         CHAT box, Close
```

- **Ready**: stand on the porch for 1 s (your porch light turns on) or press **R**. Leaving the porch un-readies.
- **Start**: host rings the bell (**B**). All ready → 3 s countdown. Not all ready → "Start anyway? 2/3 ready. Ring the bell again!" (second ring within the window). Stepping off during the countdown cancels; the bell droops sadly.
- Non-host bell ring: "clonk" and "<name> wants to start!".
- Toys: wardrobe (built); hay bales, Friend Scale ("Structural Concern"), mirror, training pig Gerald, chalkboard (planned).

Status in build: **Built** (porch, R, bell rules, countdown, crew overlay, chat, wardrobe); toys **Planned**.

---

## 14.4 Pause

Esc / Start. **In multiplayer the farm does not pause**: an overlay opens and your farmer stands with a "zzz" bubble (friends can carry you off). Solo pause truly stops time.
Menu: Resume · Settings · Invite Friends · Leave to Title. Planned: Players (mute / kick if host / profile), host "Return to Lobby".

Status in build: **Built** (core menu); Players panel **Planned**.

---

## 14.5 Settings

Tabbed panel shared by title and pause; edits apply live and save on close.

| Tab | Built | Planned |
|---|---|---|
| **Gameplay** | Mouse/stick sensitivity 0.1–5, invert Y, FOV 70–110 (default 90), player name (≤ 20 chars) | Hold vs toggle grab, aim assist, camera shake slider |
| **Audio** | Master, Music (default 80%), SFX, Voice; push-to-talk toggle | Mic device, calibration, mic gain, UI volume, "voice priority boost", reduce night ambience |
| **Video** | Window mode (Fullscreen/Borderless/Windowed), resolution scale 50–100% | Quality presets, fps cap |
| **Accessibility** | UI scale 80–150%, text size S/M/L, colourblind mode (Deuteranopia/Protanopia/Tritanopia), reduced motion | Subtitles, flash reduction, assists (14.7) |
| **Controls** | — | Full remapping, one-handed preset |
| **Social** | — | Name display, Streamer Mode, mute all, hide chalkboard drawings, nickname opt-out |

Status in build: **Partial**.

---

## 14.6 Onboarding

| Layer | Plan |
|---|---|
| **Lobby as tutorial** | Grab, throw, ragdoll and carry weights are learned on toys with no stakes. |
| **Grandpa's Tape #1** (~4 min, solo, offered once, replayable, skippable) | Move/jump → catch a slow pig → throw into wagon → push across the bridge (one plank always breaks, rescue prompt) → sell → mini receipt "Grandpa: −$30 for the plank". |
| **Quick Tape** | 60 s video offered while a newcomer waits in a friend's lobby; never blocks joining. |
| **Just-in-time tapes** (14 at EA) | ≤ 8 s VHS popups the first time something matters (wagon, lasso, feed bucket, noise, night, Sunday, shop vote, Ledger, camcorder, upgrades, weather, each new species). Max one per 90 s, never while carrying or within 10 s of a disaster. |
| **Struggle rescue** | 3 failed grabs in 60 s → 4 s grab tip. |
| **Rookie sprout** | Under 2 h played: teammates see a sprout icon; first 3 Blame events captioned "(rookie)". All-rookie crews get −20% Day 1 quota. |

Goal: 80% of new players finish Tape #1 in ≤ 5 min; 90% deliver a good on their first real day.

Status in build: **Planned** — the control-hint footer and lobby prompts are the only onboarding today.

---

## 14.7 Accessibility

| Area | Options (defaults in bold) | Status |
|---|---|---|
| **Voice-free play** | Holler, Shush, Point ping (built), ping wheel, quick-chat + optional TTS | Partial |
| **Colour** | Colourblind modes (built); colour never alone: team shapes (built), ▲/▼ on money, shape-coded bruise tiers; danger lime → magenta in red-green modes | Partial |
| **Text** | UI scale 80–150%, text size (built); min 24 px @1080p; Steam Deck default 120% | Partial |
| **Subtitles & captions** | **On**; speaker names; critter barks and stingers captioned with direction arrows; S/**M**/L/XL; background 0–100% (**60%**); max 3 lines | Planned |
| **First-person comfort** | FOV 70–110 (built), reduced motion (built), **no motion blur** (art rule), camera shake 0–100% (**70%**), ragdoll "stable camera", no slow-mo zoom in replays, optional centre dot, head-bob off | Partial |
| **Photosensitivity** | Lightning / blood-moon flashes ≤ 3 per second; flash reduction toggle | Planned |
| **Input** | Full keyboard and gamepad navigation (built); remapping, hold-to-toggle grab, gamepad throw aim assist **on**, one-handed preset, accessibility struggle-free (hold Jump 3 s) | Partial |
| **Assists** (unranked in Eviction only) | Critter speed −20%, sundown +20%, bruise damage −50% | Planned |
| **Privacy** | "Include my voice in clips" **off**; Streamer Mode hides codes and names | Planned |

Internal bar: Xbox Accessibility Guidelines for subtitles. Every gameplay sound has a caption id; all options persist and apply without restart.

Status in build: **Partial**.
