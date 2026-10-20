# Camcorder (feature/camcorder) progress

Implements `design/gdd/camcorder-replay.md` / GDD 12.7 at **beta scope** (local-only, no video encoder).

## Built
- `Scripts/Camcorder` (`HarvestPanic.Camcorder`, refs Core + Netcode + InputSystem), auto-created after first scene load, registered as Core `ICamcorderService` (new add-only contract).
- Recording: `ReplayRingBuffer` (preallocated, zero-GC per tick) at 20 Hz of NGO spawned objects classified by layer (Player/Critter/Carryable) + Bridge-layer planks; 60 m relevance; `SampleFlags.Visible` captures broken planks / sold goods. Events ring: `OnDisaster` (every client) + server noise >= 0.5 loudness.
- Triggers (`CaptureRules`): every `OnDisaster` (optional severity floor), pre-roll 15 s / post-roll 3 s, 8 s cooldown re-opens & extends the same tape up to 45 s, manual F8 (gamepad View+D-pad down) capped 3/day. Rebind: `CamcorderService.RebindSaveKey(path)` (PlayerPrefs). F9 = tape shelf.
- Storage (`MomentShelf`): 12 tapes/day, memory budget eviction by KeepScore (manual protected), payout prune top 6 + manual, "Moment of the day" = biggest severity (ties: biggest loss).
- Viewer (`ReplayViewer`): visual-only ghost copies (mesh+material recipe captured at first sighting, so destroyed/sold actors still replay), live renderers hidden via `forceRenderingOff`, gameplay cameras disabled/restored, Grandpa Cam (auto orbit + 0.5x slow-mo near impact, off with reduced motion), Victim Cam, Free Cam; 0.25/0.5/1x, scrub, unscaled time.
- Export (`ClipExporter`): PNG frame sequence (15 fps, 960x540, threaded PNG encode, <=4 frames in flight), 4x4 `contact_sheet.png`, `thumb.png` (ghost render at impact) + `thumb_live.png` (live POV), `moment.hpm` (binary tape), `README.txt` with ffmpeg commands, to `Application.persistentDataPath/Clips/<stamp>_<title>/`.
- UI (`CamcorderHud`, UI Toolkit in code, Frontend theme): toast ("Clip saved!", "TAPE SAVED: ..."), shelf, viewer bar, "Moment of the day" card during Payout/Sunday/Evicted.

## Turning frames into video
```
cd "<persistentDataPath>/Clips/<clip>"
ffmpeg -framerate 15 -i frames/frame_%04d.png -vf "scale=trunc(iw/2)*2:trunc(ih/2)*2" -c:v libx264 -pix_fmt yuv420p -movflags +faststart clip.mp4
ffmpeg -framerate 15 -i frames/frame_%04d.png -vf "fps=15,scale=480:-1:flags=lanczos" clip.gif
```
macOS persistentDataPath: `~/Library/Application Support/<company>/HarvestPanic/Clips`.

## Memory budget
| Item | Formula | Default |
|---|---|---|
| Ring | (45 s x 20 Hz + 2) frames x 128 actors x 36 B | 4.2 MB (fixed, allocated once; EditMode-tested < 8 MB) |
| Tapes | frames x actual actors x 36 B + 480x270 RGB thumb (0.39 MB) | ~0.7-2 MB each; capped at 48 MB total (`tapeMemoryBudgetMB`) |
| Export | 4 in-flight RGBA frames 960x540 | ~8 MB transient |
Per-frame GC: 0 B while sampling (PlayMode-tested). Scene scans every 1 s allocate only for new actors; Bridge-layer collider scan every 10 s.

## Edits outside Scripts/Camcorder (minimal)
- `Core/Contracts/ICamcorderService.cs` (new add-only contract; lead review requested).
- `UI/Menu/PauseOverlay.cs`: "Replays" button inserted after Help when the service exists; Esc ignored while the camcorder shelf/viewer is open.

## Deviations / gaps (beta)
- Pre-roll 15 s (GDD 30 s) per beta brief; tunable in `CamcorderTuning` (Resources/Camcorder/CamcorderTuning, defaults when absent).
- No MP4 encoder / vertical preset / VHS burn-in on exported frames (overlay is on-screen only). Item Cam and Grandpa Cam cut rules (2 s cuts, chicken zoom) not yet.
- Skinned meshes replay in bind pose (no animation recording). Noise events only on host (clients have no noise stream).
- Save key not in the Frontend rebinding list (would need Player-owned input asset change).
- Tapes not persisted across days except via export.

## Tests
- EditMode `HarvestPanic.Tests.EditMode.Camcorder`: 24/24 (ring, events ring, zero-alloc, budget, interpolation, serialization round-trip + corruption, capture rules, shelf, titles, export naming).
- PlayMode `HarvestPanic.Tests.PlayMode.Camcorder`: pending first run.

## Log
- Session 1: logic + runtime + HUD + tests written; EditMode green.
