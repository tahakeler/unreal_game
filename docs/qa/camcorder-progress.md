# Camcorder (feature/camcorder) progress

Implements `design/gdd/camcorder-replay.md` / GDD 12.7 at **beta scope** (local-only, no video encoder).

## Built
- `Scripts/Camcorder` (`HarvestPanic.Camcorder`, refs Core + Netcode + InputSystem), auto-created after first scene load, registered as Core `ICamcorderService` (new add-only contract).
- Recording: `ReplayRingBuffer` (preallocated, zero-GC per tick) at 20 Hz of NGO spawned objects classified by layer (Player/Critter/Carryable) + Bridge-layer planks; 60 m relevance; `SampleFlags.Visible` captures broken planks / sold goods. Events ring: `OnDisaster` (every client) + server noise >= 0.5 loudness.
- Triggers (`CaptureRules`): every `OnDisaster` (optional severity floor), pre-roll 15 s / post-roll 3 s, 8 s cooldown re-opens & extends the same tape up to 45 s, manual F8 (gamepad View+D-pad down) capped 3/day. Rebind: `CamcorderService.RebindSaveKey(path)` (PlayerPrefs). F9 = tape shelf.
- Storage (`MomentShelf`): 12 tapes/day, memory budget eviction by KeepScore (manual protected), payout prune top 6 + manual, "Moment of the day" = biggest severity (ties: biggest loss).
- Viewer (`ReplayViewer`): visual-only ghost copies (mesh+material recipe captured at first sighting, so destroyed/sold actors still replay), live renderers hidden via `forceRenderingOff`, gameplay cameras disabled/restored, Grandpa Cam (auto orbit + 0.5x slow-mo near impact, off with reduced motion), Victim Cam, Free Cam; 0.25/0.5/1x, scrub, unscaled time.
- Export (`ClipExporter`): PNG frame sequence (15 fps, 960x540, threaded PNG encode, <=4 frames in flight), 4x4 `contact_sheet.png`, `thumb.png` (ghost render at impact) + `thumb_live.png` (live POV), `moment.hpm` (binary tape), `README.txt` with ffmpeg commands, to `Application.persistentDataPath/Clips/<stamp>_<title>/`.
- UI (`CamcorderHud`, UI Toolkit in code, Frontend theme): toast ("Clip saved!", "TAPE SAVED: ..."), shelf, viewer bar ("PLAY ▶" / "JUN 12 1994" OSD), "Moment of the day" card during Payout/Sunday/Evicted. Other UIDocuments (gameplay HUD) are hidden while the viewer is open and restored after.
- Best shot: on trigger a temporary camera (copied from the player camera) renders a wide angle of the disaster location (manual tapes use the player's own view).

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
- Closing the viewer mid-export stops the coroutine; Unity does not run the iterator's `finally`, so that export's RenderTexture leaks until scene unload (rare, small).
- Noise and disaster events are recorded in the tape but not yet drawn as markers on the scrub bar.
- Nature-instigated tapes (e.g. a pumpkin bruised by physics at spawn) are titled "NATURE vs. ..."; the receipt calls the same actor "The Nobody".

## Tests
- EditMode `HarvestPanic.Tests.EditMode.Camcorder`: 24/24 (ring, events ring, zero-alloc, budget, interpolation, serialization round-trip + corruption, capture rules, shelf, titles, export naming).
- PlayMode `HarvestPanic.Tests.PlayMode.Camcorder`: 3 tests + 1 QA screenshot test. Covers record → save → replay (ghost poses match the recorded path within 1 mm and 0.1°; visibility; slow-mo; serialized replay), manual cap, 0 B GC per sample, and export files.
- Full project suites at 838711f+: EditMode 246/246; PlayMode 55 passed, 0 failed, 12 skipped (the HP_QA_DIR screenshot tests). The camcorder QA run with HP_QA_DIR: 4/4.

## QA screenshots (docs/qa/screens/camcorder/)
- `camcorder_viewer_grandpa.png`: Grandpa Cam at the impact (0.5x slow-mo), farmer ghost, viewer bar, gameplay HUD hidden.
- `camcorder_viewer_victim.png`: Victim Cam behind the instigator's ghost.
- `camcorder_receipt_moment.png`: Daily Receipt plus the "Moment of the day" card. Its headline tape is the same incident as the receipt headline (bruised prize pumpkin).
- `export_contact_sheet.png`, `export_thumb.png`, `export_thumb_live.png`, `export_README.txt`: from a real 13 s Muddlebury export (195 frames, not committed).
Re-run: `HP_QA_DIR=<dir>` + PlayMode `-assemblyNames HarvestPanic.Tests.PlayMode.Camcorder -netPort <random>` without `-nographics`.
Measured in Muddlebury: 36 tracked actors; 13 s tape = 262 frames / 8,497 samples = 0.70 MB including the thumbnail; ring 4.16 MB.

## Log
- Session 1: logic + runtime + HUD + tests written; EditMode green.
- Session 2: PlayMode green (record → save → replay positions, manual cap + 0 B/tick, export files); Muddlebury QA run. Two fix rounds after reading the screenshots: (1) the gameplay HUD overlapped the viewer, so it is now hidden while watching; (2) the best-shot thumbnail was first-person hands, so it is now a wide angle on the disaster. Also fixed: `PlayerIds.Nature` printed as a number in titles; HUD input/cursor are now released when the viewer closes from any path.
