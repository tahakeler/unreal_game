# Grandpa's Camcorder (Replay & Export)

> **Status**: Draft
> **Author**: game-designer + ux-designer (Batch D)
> **Last Updated**: 2026-10-10
> **Last Verified**: 2026-10-10
> **Implements Pillar**: Failure Is the Content

## Summary
A rolling replay buffer that automatically saves the 30 seconds before every disaster, lets players re-watch them at payout, and exports a captioned vertical or horizontal clip with one click. It turns the game's failures into shareable marketing.

> **Quick reference** — Layer: `Feature` · Priority: `Alpha` · Key deps: `blame-ledger`, `session-networking`, `hud-menus`

## Overview
Each client keeps a local, low-cost replay buffer of the last **30 s** (UE replay system: local in-memory DemoNetDriver stream, not video). When `blame-ledger` broadcasts `OnDisaster`, every client snapshots the buffer plus **3 s** of post-roll into a "Tape". Tapes appear on the payout screen as a VHS shelf; any player can watch, pick a camera and export an MP4 encoded locally. Nothing uploads automatically.

## Player Fantasy
"Wait, wait, you have to see this from the cow's angle." The camcorder is the friend who was filming the whole time. Re-watching should be as funny as living it, and exporting should be faster than the urge fades (under 10 s).

## Detailed Design

### Core Rules
1. **Buffer:** per-client replay stream, 30 s ring, recorded at 20 Hz net-relevant actors within **60 m** of any player. Memory budget ≤ **64 MB** per client.
2. **Trigger:** on `OnDisaster(BlameEvent)` the client saves `[t−30 s, t+3 s]` as a Tape tagged with the event's verb, instigators, $ delta and Severity. Cooldown **8 s** between saves; a disaster in cooldown extends the current tape up to a max of 45 s.
3. **Manual save:** hotkey (default F8 / gamepad View+Down) saves a tape with verb "Manual". Max **3** manual saves per day.
4. **Storage:** max **12 tapes per day** held in memory; at payout, the top 6 by Severity plus all manual tapes are kept, rest discarded. Tapes are not persisted across days unless "Kept" (writes to disk, ≤ 20 MB each, cap 50 kept tapes / 1 GB, oldest-first prompt).
5. **Shelf UI (payout screen):** tapes as VHS boxes labeled with auto-titles ("DAVE vs. THE RIVER, $−240"). The day's headline tape plays automatically on loop in the corner of the receipt (muted, 1×).
6. **Viewer:** play/pause, 0.25×/0.5×/1×, scrub, cameras: **Grandpa Cam** (auto-director, default), **Victim Cam** (follows instigator), **Item Cam** (follows the item), **Free Cam**. Only locally viewed; other players' viewing is not synced (no wait).
7. **Export:** 1-click presets: **Vertical 1080×1920 30 fps** (Shorts/TikTok), **Horizontal 1920×1080 30 fps**. Max length 45 s. Burned-in VHS overlay: date stamp (in-game day), "REC", caption from the receipt line, and a small "HARVEST PANIC" watermark (toggleable off in settings). Proximity voice is **excluded** by default; opt-in toggle "Include voices" requires all recorded speakers' consent setting (`onboarding-accessibility` privacy option, default OFF).
8. Encoding runs locally (AVFoundation on Mac, Media Foundation on Windows) in a background thread; gameplay continues. Output folder: `~/Movies/HarvestPanic` or `Videos/HarvestPanic`.

### Grandpa Cam auto-director
Picks shots by priority: (1) wide shot framing instigator + item during t−5..t, (2) slow-mo 0.5× for the 1.5 s around impact, (3) reaction close-up on nearest player for the last 2 s. Camera cuts no faster than every 2 s.

### States and Transitions
| State | Entry | Exit | Behavior |
|---|---|---|---|
| Recording | Day start | Payout | Ring buffer active |
| Capturing | OnDisaster / manual | +3 s post-roll | Snapshot buffer |
| Shelf | Payout | Continue | Tapes viewable |
| Exporting | Export pressed | Done/fail | Background encode, toast on completion |
| Off | Settings "Camcorder: Off" or low-memory mode | Toggle on | No buffer, no tapes |

### Interactions with Other Systems
- **In:** `OnDisaster` and Blame Event metadata from blame-ledger; actor replication from session-networking.
- **Out:** tapes to hud-menus (shelf); "Clip of the Day" to eviction-mode daily seed screen (local only); export counts to meta-progression (letter "Home Video Star": 10 exports).

## Formulas
```
BufferMemoryMB ≈ RelevantActors * BytesPerActorPerTick * TickHz * BufferSeconds / 1e6
```
| Var | Value |
|---|---|
| RelevantActors | ≤ 120 |
| BytesPerActorPerTick | ~ 120 B (quantized transform + state) |
| TickHz | 20 |
| BufferSeconds | 33 |
→ ≈ 9.5 MB typical, 64 MB hard cap (drop to 10 Hz if exceeded).

Tape score for keeping: `KeepScore = Severity + 30 * bManual + 10 * InstigatorCount`.

## Edge Cases
| Scenario | Behavior | Rationale |
|---|---|---|
| Disaster within first 30 s of day | Tape starts at day start | Short is fine |
| Two disasters 4 s apart | One extended tape, both captions | Avoid duplicates |
| Client joined mid-day | Only has buffer since join | Local recording |
| Low-spec / memory warning | Auto-drop TickHz to 10, then Off with toast | Performance first |
| Export during next day load | Encode continues, lower priority | Don't block play |
| Player declines voice consent | Their voice muted in any export | Privacy |
| Disk full | Export fails with clear message, tape kept in memory until day end | No silent loss |

## Dependencies
| System | Direction | Nature |
|---|---|---|
| blame-ledger | This depends on | OnDisaster, metadata, captions |
| session-networking | This depends on | Replicated state for recording |
| hud-menus | Depends on this | Shelf and viewer screens |
| onboarding-accessibility | This depends on | Voice consent, reduced-motion (disables slow-mo zoom) |
| meta-progression, eviction-mode | Depend on this | Counters, Clip of the Day |
| audio-feedback | Both | VHS SFX; replay mixes game audio |

## Tuning Knobs
| Knob | Default | Range |
|---|---|---|
| BufferSeconds | 30 | 15–45 |
| PostRoll | 3 s | 1–5 |
| CaptureCooldown | 8 s | 4–15 |
| TapesKeptPerDay | 6 | 3–12 |
| ManualSavesPerDay | 3 | 0–10 |
| SlowMoFactor | 0.5 | 0.25–1 |
| Watermark | On | toggle |

## Acceptance Criteria
1. A bridge drop triggers a tape containing ≥ 25 s before and 3 s after the event, on every client.
2. Recording costs ≤ 1.0 ms game-thread and ≤ 64 MB on base M1 at 4 players.
3. Vertical export of a 33 s tape completes in ≤ 20 s on base M1 without dropping gameplay below 30 fps.
4. Exports contain no voice audio unless all speakers opted in.
5. Shelf auto-titles match receipt lines for the same Blame Event.

## Comedy beats
- Auto-titles in tabloid style: "LOCAL MAN LOSES PIG, DIGNITY."
- Grandpa Cam occasionally (10%) zooms dramatically on a chicken instead of the disaster.
- Tracking-line glitch and a "PLAY ▶" OSD; the date stamp is always "JUN 12 1994".
