# HUD, Menus & Ledger Screens

> **Status**: Draft
> **Author**: ux-designer + game-designer (Batch D)
> **Last Updated**: 2026-10-10
> **Last Verified**: 2026-10-10
> **Implements Pillar**: Heavy Together, Blamed Together · Failure Is the Content

## Summary
All screen-space and diegetic UI: a minimal in-world HUD during the day, the lobby, the payout receipt, the shop, the Sunday awards and settings. The rule is "diegetic first": look at the world, not the corners of the screen.

> **Quick reference** — Layer: `Presentation` · Priority: `VS (payout) / Alpha` · Key deps: `day-loop`, `blame-ledger`, `farm-upgrades`, `shared-wallet-economy`

## Overview
Built in UMG with CommonUI (gamepad + mouse parity). The day HUD shows only four persistent elements; everything else lives on diegetic props (Grandpa's pocket watch, the manifest clipboard on the wagon, price chalkboards at the market). Between days, full-screen menus present the receipt, shop and awards.

## Player Fantasy
The screen is clean so the chaos reads. You learn the time by glancing at the sunset and the watch, the order by the clipboard. Menus feel like paperwork from a hostile bank: funny, fast, never a chore.

## Detailed Design

### Core Rules: Day HUD (persistent)
1. **Pocket watch** (bottom-left, 140 px @1080p): sundown countdown; ticks red under 60 s.
2. **Wallet + day target** (top-center): "$412 / $600 today-share"; animates ±$ with color.
3. **Carry indicator** (center-bottom, contextual): hands on item (1–4 icons), item value with bruise tint, only while carrying.
4. **Teammate markers:** name + status icon over heads, clamped to screen edges when off-screen ≤ 80 m; hidden in fog days except when within voice range (consistent with weather-twists fog rule).
- Contextual prompts (grab, hitch, tool) appear world-anchored near the object, max 1 at a time.
- Disaster toasts: when `OnDisaster` fires, a 2.5 s toast "−$90 Dave → river" slides from the top; max 1 queued, others merged.
- Voice indicator: speaking ring around own portrait; loudness meter (0–1) only when the Noise Event loudness > 0.6 ("TOO LOUD" wobble).
- Total HUD coverage ≤ **8%** of screen area in day play.

### Menus & Screens
| Screen | Entry | Key content | Time budget |
|---|---|---|---|
| Title | Boot | Play, Continue, Eviction, Almanac, Trunk, Settings | — |
| Lobby | Host/Join | Region pick, crew slots (4), ready, lobby options (Betrayal, Voice, Difficulty) | — |
| Morning Order | Day start | Manifest (≤ 6 items), twist card(s), Region twist; auto-closes at 12 s or all-ready | 12 s |
| Payout Receipt | Day end | Earnings, receipt per player (blame-ledger), headline tape, letters earned | ≤ 25 s |
| Shop | After receipt | Upgrades grid, shared wallet, **purchase vote**: item needs majority (ties: host), 5 s veto window | ≤ 90 s soft |
| Sunday Awards | Sunday | 5 awards, betrayal audit, quota payment animation, pass/fail | ~45 s |
| Camcorder Shelf/Viewer | From receipt | See camcorder-replay | — |
| Settings | Anywhere | Controls, audio, video, accessibility (onboarding-accessibility) | — |
| Pause | Esc/Start | Does **not** pause in multiplayer; overlay only | — |

Rules:
1. Every between-day screen shows a per-player **ready** row; the screen advances when all ready or timer ends.
2. All screens fully navigable by gamepad, mouse and keyboard; focus always visible.
3. Min text size 24 px @1080p (scales with UI Scale 80–150%).
4. Text is localization-ready: all strings in StringTables; receipt templates use FText formatting with plurals.

### States and Transitions
Title → Lobby → (Morning Order → Day HUD → Payout → Shop)×n → Sunday Awards → Shop/Run end. Pause overlay available in any state except Sunday payment animation.

### Interactions with Other Systems
- **In:** timer/manifest (day-loop), wallet (shared-wallet-economy), upgrades (farm-upgrades), receipt/awards (blame-ledger), tapes (camcorder-replay), carry state (physics-carry), loudness (proximity-voice), letters/cosmetics (meta-progression), results (eviction-mode), region info (region-campaign).
- **Out:** purchase votes → farm-upgrades; ready states → day-loop; settings → onboarding-accessibility, audio-feedback.

## Formulas
```
ToastPriority = Severity (from Blame Event); show if ToastPriority ≥ 40 and no toast shown in last 1.0 s
EdgeMarkerAlpha = clamp(1 − (distance − 60)/20, 0, 1)   // fades 60→80 m
```

## Edge Cases
| Scenario | Behavior |
|---|---|
| 4 disasters in 2 s | One merged toast "−$310 (4 disasters)" |
| Player disconnects on shop vote | Vote majority recomputed on remaining players |
| Ultrawide 21:9 | HUD anchored to 16:9 safe area by default; option to stretch |
| Steam Deck 800p | UI Scale default 120% |
| Receipt for 0 events | "Spotless. Suspicious." line |

## Dependencies
| System | Direction |
|---|---|
| day-loop, shared-wallet-economy, farm-upgrades, blame-ledger | This depends on |
| camcorder-replay, meta-progression, eviction-mode, region-campaign | Provide screens' data |
| onboarding-accessibility | Applies settings to this |
| audio-feedback | UI sounds |

## Tuning Knobs
| Knob | Default |
|---|---|
| MorningOrderTimeout | 12 s |
| ReceiptTimeout | 25 s |
| ShopSoftTimer | 90 s |
| ToastDuration | 2.5 s |
| ToastSeverityMin | 40 |
| MarkerFadeRange | 60–80 m |

## Acceptance Criteria
1. Day HUD covers ≤ 8% screen area (screenshot analysis).
2. Payout → Shop → next day achievable in ≤ 40 s if everyone readies.
3. All menus completable with gamepad only (QA pass).
4. No hard-coded strings (localize scan).
5. VS: payout receipt shows itemized bruise lines per player.

## Comedy beats
- Receipt looks like a crumpled bank slip; the shop vote veto button says "OBJECTION".
- Pause menu in multiplayer: "The farm does not pause." while the pig keeps running.
- Wallet counter flinches with a tiny "ow" animation on big losses.
