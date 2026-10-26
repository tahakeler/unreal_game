# Tools, wagon, lasso, whistle — progress (feature/wagon-lasso-whistle)

Owner: Player team (gameplay-programmer). Scope: `Scripts/Gameplay/Tools`, `Scripts/Gameplay/Vehicles`, small add-only hooks in
`Scripts/Gameplay/Carry` + `Player`, prefabs in `Prefabs/Tools` and `Prefabs/Vehicles`.
Specs: `design/gdd/farm-tools.md`, `design/gdd/wagon-vehicles.md`, `design/gdd/physics-carry.md`, GDD 4.7-4.8, 5.8-5.10, 7.2.

## Status
- [x] Lasso: aim (twirl + arc preview), throw (12 m), loop flight, 6 m tether, reel (2 m/s, 10 stamina/s), auto-grab at 1.5 m,
  tie to post / wagon hitch (3 m), untie by picking the coil up, GDD tug-of-war + water-ski, tow rope joint for goods,
  knocked-out teammate drag, awake teammate bounce, LassoedIntoRiver
- [x] Whistle: short blow `Whistle`, long blow (1 s) `WhistleLong` only, 0.7 loudness, 3 s cooldown, carried critters +50% struggle
  for 2 s, SpookedHerd when one escapes in that window
- [x] Wagon: networked server-simulated cart, 4 grips, GDD rolling drag / v_max, 6 cargo slots + 400 kg soft cap, bump bounce-outs
  (SpilledCargo), tipping past 50 deg (TippedWagon), 2-player (or solo) righting, plank load, Padded Crates via Goods.BruiseMult,
  spawns at the barn yard with a hitching post, morning return
- [x] Toolbelt hotbar (Hands / Net / Bucket / Lasso / Whistle; keys 1-4 + Q, wheel, LB/RB; rebindable actions) + HUD strip
- [x] First-person arm poses: lasso twirl / hold / reel (hand over hand), whistle at the lips, wagon push
- [x] EditMode tests (`Tests/EditMode/Player/ToolsLogicTests.cs`, 21)
- [x] PlayMode tests (`Tests/PlayMode/Tools/ToolsPlayModeTests.cs`, 7) + QA captures (`ToolsScreenshots.cs`, `docs/qa/tools/`)

## Results (2026-10-22, branch head after 2a172fd)
- EditMode: 303/303 passed (21 new in `ToolsLogicTests`).
- PlayMode, full suite (`-netPort` random): 85 total, 64 passed, 17 skipped (GPU / HP_QA_DIR captures), 4 failed: the
  SessionRobustness tests, because `SessionService.Port()` prefers `-netPort` over their own random `lanPort`. Re-run without
  `-netPort`: 4/4 passed. Tools PlayMode on the final code: 7/7.
- QA captures (HP_QA_DIR, GPU): 2/2, images in `docs/qa/tools/` (lasso aim / tethered / reel, roped pig, loaded wagon on the
  bridge, push pose, tipped wagon with spilled crates, whistle, HUD hotbar).
- macOS dev build: success. `tools/net/two-process-test.sh` (extended: version reject, late join + rejoin, cap, host quit): PASS;
  host spawned the wagon at the barn yard, every client (incl. the late joiner) logged `[Wagon] replicated on client`; no
  exceptions in host/client/late logs.
- Re-run captures: `HP_QA_DIR=<dir>` + PlayMode `-assemblyNames HarvestPanic.Tests.PlayMode.Tools -testFilter ToolsScreenshots`
  without `-nographics`.

## How it works (short)
- Lasso on critters keeps the AI in charge: the rope only pulls the NavMeshAgent back to the rope length (`agent.Move`), so a
  roped pig runs circles at the end of the rope. Pull vs `LassoMath.PlayerAnchor` (summed over every rope on the catch) decides
  the tug-of-war; a winner drags the holder (ragdoll + IDraggable) for 2.5 s. Where the NavMesh ends (bank, bridge edge) the
  critter is yanked off its feet (`ICarryable.OnGrabbed(lasser)`), dragged, and over water it falls in. No Carried-state
  break-free rolls while roped (a pig would wriggle free ~50% of reels otherwise).
- Wagon: a holder's handle is a tow bar (spring to 0.75-1.15 m, capped 900 N). Holders walk at most 1.5 m/s faster than the cart
  rolls and crawl at their own bar, so pushing ramps up with the cart and nobody walks through it. Rear pushers push along the
  cart and, with nobody steering at the front, side-slip and yaw are removed (rule 3: the front axle centres); a front-handle
  driver steers. Measured: one farmer, 132 kg, dirt: 4.47-4.50 m/s (GDD 4.5).
- Bridge planks: plank 10 with the wagon + 3 crates parked on it read exactly 132.0 kg, the farmer's plank 70.0 kg.

## Deviations from the GDD (flagged)
- Tool slots: a 4-tool belt + hands instead of 2 slots + barn racks (the slice has no racks). Tool loss/restock ($15) not built.
- Lasso range is the GDD's 12 m (the task brief said ~10 m). Awake teammates cannot be lassoed (GDD 5.11 rule 6); knocked-out
  ones can and are dragged.
- Wagon cargo rides in slots (kinematic) instead of loose bodies; bounce-outs are a per-bump roll above the GDD threshold.
  No tarp, brake lever or wheel damage yet; the $40 recovery fee is not charged. One farmer cannot heave 300 kg over the 8 cm
  bridge abutment lip (needs a second pusher).
- Whistle "rally" toward the whistler only happens for Lure/Rally responders; no current species has one (sheep/dog not built).

## Requests to other teams
- Economy: the $2 noise floor drops the $0 `LassoedIntoRiver` and `SpookedHerd` incidents (the river already books the soak on the
  lasser). Add both to `EconomyMath.IsNonEconomic` like `YeetedTeammate`, or let the river use the lasso verb. Padded Crates is a
  global `Goods.BruiseMult` x0.8; the GDD says "in wagon" only.
- Audio: every Tool noise plays `SFX_Tool_Metal`; route `NoiseTag.Whistle`/`WhistleLong` to a whistle cue (and a rope creak).
- AI: `CritterBrain.HearNoise` ignores noise while Carried, so a whistle cannot raise a carried critter's real break-free chance;
  the +50% struggle is the carry wobble only.
