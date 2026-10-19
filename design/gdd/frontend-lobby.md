# Frontend & Lobby

> **Status**: Draft v0.1 · **Created**: 2026-10-11 · **Slug**: `frontend-lobby` · **Priority**: VS (title, host/join Steam + LAN, lobby map, ready, start, pause) / Alpha (server browser, lobby toys complete) / EA (rejoin polish)
> **Depends on**: session-networking, character-customization, hud-menus, onboarding-accessibility · **Camera**: first-person throughout. The lobby is a playable space, not a menu screen.

## Overview
You go from launch to playing in **≤ 3 clicks**. The title screen leads to Host, Join, Settings or Quit. Hosting loads you into **the Lobby Yard**, a small playable farmyard in front of Grandpa's porch. You walk around as your farmer while friends join via Steam invite, LAN or the server browser. The yard has toys (throwable hay bales, a scale that weighs friends, a mirror, the wardrobe). Players ready up by standing on the porch; the host rings the dinner bell to start.

## Player Fantasy
"We were already messing around before the game even started." Aesthetics: **Fellowship**, **Sensation**, **Expression**. The lobby teaches the controls (grab, throw, ragdoll) with no stakes while waiting for the last friend. Pillar: **Heavy Together, Blamed Together**, since the scale introduces shared physics. Flow: zero-fail onboarding.

## Detailed Design

### Flow
`Splash (skippable, ≤ 3 s) → Title → [Host | Join | Settings | Quit]`
- **Host**: choose Privacy (Friends Only (default) / Invite Only / Public / LAN), max players 2–4 (default 4) and Continue Run / New Run (save slot). Then the Lobby Yard loads.
- **Join**: tabs **Friends** (Steam friends in joinable lobbies), **Browser** (public lobbies), **LAN**, and **Code** (6-character lobby code, letters only, no ambiguous letters I/O). Accepting a Steam invite from the overlay skips the title screen entirely.
- **Title diorama**: a first-person view through the farmhouse window. A pig occasionally escapes across the yard (an ambient preview of "Everything Has Legs").

### Server browser
Columns: Host name, Farm name, Players x/4, Week/Region, Ping (bars), Language tag, Mode (Run/Eviction), and an "In progress" icon. Filters: hide full, hide in-progress, region ping < 150 ms. Sort by ping by default. Refresh every 15 s or manually. Rows that are full are greyed out.

### Lobby Yard (map `L_LobbyYard`, ≤ 40 × 40 m)
| Toy | Behaviour | Teaches |
|---|---|---|
| Hay bales ×6 | Grab/throw physics props (12 kg). Respawn at the pile after 30 s idle off-pile | Grab, throw |
| **Friend Scale** | Platform scale that shows the total kg on a big dial plus a funny verdict ("Prize Hog", "Featherweight", "Structural Concern"). Carrying adds the carried item's weight | Carry weights, teamwork |
| **Mirror** | Full-length mirror (render target at 512², updated at 15 fps, only while a player is within 6 m). The only place you see your own face before the day starts | Customization |
| Wardrobe | See character-customization | |
| Practice pig ×1 | Docile pig with flee disabled after 3 catches. "Training pig Gerald" | Catching |
| Dinner bell | Host-only start interaction | |
| Chalkboard | See social-comedy-features | |
| Porch | Ready zone | Ready-up |

Ragdoll and slapping are enabled in the lobby. Bruises and Blame events are disabled (the Ledger is off).

### Ready-up and start
- Standing on the **porch** for 1 s marks you Ready (your name tag gets a check mark and the porch light for your colour turns on). Leaving the porch un-readies you. There is also a menu toggle (key **R**) for accessibility.
- The host rings the dinner bell (or presses Start in the pause menu). If everyone is Ready, a 3 s countdown starts. If not, the host sees "Start anyway? (2/3 ready)", which needs a second ring within 3 s.
- Minimum player count is 1 (solo is allowed, with a warning that the game is tuned for 2–4).
- During the countdown, any player can cancel by leaving the porch. The bell then rings sadly.

### Settings (title + pause)
Tabs: Gameplay (FOV 70–110, default 90; sensitivity; hold vs toggle grab), Audio (master/music/SFX/voice; mic mode Open/PTT; input device), Video (quality presets, fps cap, resolution scale), Controls (rebind), Accessibility (from onboarding-accessibility), Social (name display, Streamer Mode, mute all, hide chalkboard drawings).

### Pause
- **Pause never stops the world** in multiplayer. It opens an overlay. Your body stands idle with a "zzz" bubble above it (comedy: friends can carry you away). In solo, pause actually stops the game.
- Menu: Resume, Settings, Invite Friends, Players (mute/kick-if-host/view profile), Leave to Title, and (host) Return to Lobby.

### Leaving and rejoin
- A client who leaves mid-day: their carried items drop and their body poofs into hay. Their slot is reserved for **5 min** (rejoin restores their Ledger stats and tools). After that the slot opens.
- Join-in-progress: allowed at any time unless the host has Privacy = Invite Only and has locked it. A late joiner spawns at the barn with a 3 s "late for work" emote.
- If the host leaves, the session ends (host migration-lite in session-networking saves the run at the last day boundary). Clients return to the title with the message "The host went to bed. The run was saved at the start of Day N."
- Disconnect by timeout: the same as a leave, plus a "Reconnect" button on the title screen for 5 min.

## Formulas
- Friend Scale reading: `W = Σ(player mass 80 kg) + Σ(carried/stacked prop mass)`, shown rounded to 1 kg. Verdict thresholds: < 60 "Featherweight" (empty scale + bale), 60–100 "Regular Farmhand", 100–200 "Two's Company", 200–320 "Structural Concern", > 320 "Call the Landlord".
- Time to play target: `T(launch→first input in yard) ≤ 25 s` on an M1 base (Lobby Yard load ≤ 8 s).
- Mirror cost: one 512² scene capture at 15 Hz while in range is ≈ 0.4 ms GPU on M1 (budget ≤ 0.6 ms).

## Edge Cases
- Invite accepted while already in a run: prompt "Leave your current farm?".
- Two players try to ring the bell: host-only. A non-host bell ring plays a "clonk" and posts "<name> wants to start!" to the host.
- Lobby code collision: regenerate on create.
- A player stacks friends on the scale beyond 4 bodies: the dial pegs at "Call the Landlord" and the scale breaks for 10 s (comedy).
- A player AFK on the porch: they count as ready. The host can kick them.
- Version mismatch: blocked at join with "Your farm is a different version (vX vs vY)".
- A hay bale thrown out of bounds: the kill-Z respawns it.
- The mirror never reflects ghost-chicken spectators (social-comedy) to save cost.

## Dependencies
| System | Direction | Contract |
|---|---|---|
| session-networking | uses | Create/find/join session, Steam invites, LAN, lobby codes, rejoin reservation |
| character-customization | uses | Wardrobe, loadout replication, mirror |
| hud-menus | uses | Widget style, name tags |
| onboarding-accessibility | uses | Settings tab content, Streamer Mode |
| physics-carry / player-character | uses | Grab, throw, ragdoll in the lobby |
| save-persistence | uses | Continue/New run |
| social-comedy-features | provides | Hosting space for chalkboard, lobby toys |

## Tuning Knobs
| Knob | Default | Range | Category |
|---|---|---|---|
| `ReadyHoldTime` | 1 s | 0.5–3 | Feel |
| `StartCountdown` | 3 s | 0–10 | Feel |
| `RejoinReserveTime` | 300 s | 60–900 | Gate |
| `BaleRespawnIdle` | 30 s | 10–120 | Feel |
| `PracticePigCatchesBeforeDocile` | 3 | 1–10 | Feel |
| `MirrorCaptureHz` / `MirrorRange` | 15 / 6 m | 5–30 / 3–10 | Feel/Perf |
| `BrowserRefresh` | 15 s | 5–60 | Gate |
| `MaxPlayers` | 4 | 2–4 | Gate |
Data: `DA_FrontendTuning` in `assets/data/`.

## Comedy beats
- Weighing your whole crew on the scale: "Structural Concern".
- Throwing a hay bale at the friend admiring themselves in the mirror.
- Carrying the AFK "zzz" friend onto the porch to ready them up, or off it.
- The sad bell when someone steps off the porch during the countdown.

## Acceptance Criteria
**Functional**
1. Host → friend accepts Steam invite → both in the Lobby Yard in ≤ 30 s on an M1.
2. LAN and Code join both work with 4 players.
3. Ready via porch and via key R both work. The bell starts the game only for the host. The "Start anyway" confirmation works.
4. Pause in multiplayer does not stop simulation. Pause in solo does.
5. A leaver can rejoin within 5 min with their Ledger stats restored.
6. Host leave → clients get the "host went to bed" message and the run save is intact.
7. Mirror ≤ 0.6 ms GPU on M1. The lobby holds 60 fps with 4 players.

**Experiential**
- ≥ 70% of first-time testers throw something or step on the scale before the game starts, without any prompt.
- The median wait in the lobby feels "fine/fun" in a survey (≥ 4/5).

## Delta vs implementation
- If the provisional frontend implements the lobby as a 2D menu, this GDD requires a **walkable Lobby Yard**. The menu can stay as a fallback (key Tab shows the player list).
- Ready-up is a **porch zone plus key R**, not just a button.
- Pause does **not** stop the world in multiplayer.
