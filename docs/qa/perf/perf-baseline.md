# HARVEST PANIC -- Perf baseline vs GDD 17.7 budgets

**Scenario** (`HarvestPanic.Tests.PlayMode.Perf.PerfCaptureTests.Muddlebury_4Farmers_RainNight_60s`):
Muddlebury, LAN host + 3 scripted bot farmers (4 farmer bodies), all spawned critters (13), forced **Rain** + **Night**
(Mr. Stitches active), bots carrying goods over the bridge to the Sale Pen and shouting, the host's first-person
URP camera rendered offscreen at **1920x1080** every frame, 5 s warm-up, then **60 s** of ProfilerRecorder samples.
Raw tables: `docs/qa/perf/raw/perf-before.md`, `perf-after-grd.md`; hotspot/renderer audit: `raw/hotspots-before.md`.

**Capture machine**: Apple M2 Pro / 16 GB, Metal, Unity 6000.3.26f1 **Editor batchmode** (not a player build).
Read the numbers as relative: editor overhead inflates CPU and memory; the M2 Pro is faster than the base-M1
"Mac Medium" reference. GPU time is **not measured** (no GPU timer in batchmode editor) -- a dev-build capture on an
M1 is still required for the GPU <= 12 ms line.

## Result vs budget

| Budget (GDD 17.7) | Target | Before | After | Status |
|---|---|---|---|---|
| Frame time (avg / p95 / p99) | 16.6 ms | 5.02 / 5.75 / 6.85 ms | **3.78 / 4.34 / 4.98 ms** | OK (M2 Pro editor; re-verify on M1) |
| CPU main thread avg | <= 10 ms | 5.01 ms | **3.78 ms** | OK |
| Physics (Physics.Simulate avg / p99) | <= 3 ms | 0.03 / 0.19 ms | 0.03 / 0.23 ms | OK |
| Scripts Update+LateUpdate avg | (AI <= 1.5 ms) | 0.23 ms | 0.20 ms | OK |
| GPU | <= 12 ms | not measured | not measured | **UNVERIFIED** |
| Draw calls | <= 1,500 | **6,187 (OVER 4x)** | **508** | OK after fix |
| SRP batches | <= 1,500 | 632 | 508 | OK |
| Shadow casters (stat) | -- | 6,241 | 172 | -- |
| Triangles (avg / max) | <= 1.5 M | 585 k / 623 k | 592 k / 624 k | OK (same geometry) |
| GC alloc per frame (avg) | -- (target ~0) | 3.49 KB / 76 allocs | **1.94 KB / 38 allocs** | improved; residual listed below |
| Memory: total used (Unity) | <= 3 GB process | 1.16 GB (editor) | 1.08 GB (editor) | OK |
| Memory: system used (editor process) | <= 3 GB | 1.97 GB | 1.89 GB | OK (editor incl.) |
| Texture memory | <= 1.5 GB | 131 MB | 134 MB | OK |
| Mesh memory | -- | 151 MB | **10 MB** | static-batch combined meshes gone |
| Awake physics bodies (max) | <= 150 | 6 | 6 | OK |
| Active critters | <= 30 | 13 | 13 | OK |

Worst single frame: 68 ms before / 950 ms after -- one hitch inside the window in each run (GC.Collect / first GRD
instance-data upload); **not** steady state (p99 is 5 ms). Track in the dev-build M1 capture.

## Top 3 offenders and fixes

1. **Draw calls 4x over budget -- 1,733 Kenney crop/fence prefab instances (5.5 k static-batched renderers, all
   shadow-casting).** Static batching does not reduce draw calls under the SRP Batcher; every crop was a separate
   draw in both the colour and shadow pass (`SRPBatcher.Flush` 1.2 ms/frame was the #1 main-thread marker).
   **Fix (project settings, owner: technical-artist / engine to sign off):** enable the URP **GPU Resident Drawer**
   (`Assets/Settings/HP_URP.asset` `m_GPUResidentDrawerMode: 1`, instanced drawing; renderer already Forward+),
   **turn off static batching** for Standalone (`ProjectSettings.asset`, GRD ignores statically batched renderers),
   and keep BatchRendererGroup variants (`GraphicsSettings.asset` `m_BrgStripping: 2`) so player builds contain the
   BRG shader variants. Result: 6,187 -> 508 draw calls, 5.0 -> 3.8 ms main thread, mesh memory 151 -> 10 MB.
   Caveat: renderers using MaterialPropertyBlocks or non-SRP-batcher shaders fall back to the normal path (fine).
2. **`EconomyHud.Update` -- 1.2 KB GC every frame** (calendar/rent/clock strings, order key string concat,
   twist text rebuilt each frame). Fix: change-detection keys; strings only rebuilt when their inputs change.
   (`Economy/Hud/EconomyHud.cs`, comments tagged `perf:`.)
3. **`WeatherPresenter.LateUpdate` -- 650 B GC every frame** from `WeatherLook.Hex()` doing `"#" + h` and parsing
   ~20 colour literals per frame. Fix: memoized `Hex()` (`World/Weather/Runtime/WeatherLook.cs`, one function).
   Plus **`MrStitches.Update` -- ~300 B/frame**: new `List<Vector3>` + method-group delegate per call. Fix: reused
   list + cached delegate (`AI/Night/MrStitches.cs`, 3 lines).

## Residual / follow-ups (not fixed here)

| Item | Evidence | Owner |
|---|---|---|
| `OnboardingOverlay.Update` 265 B/frame | hotspots-before.md | ui-programmer |
| `PauseOverlay.Update` 42 B, `AudioService.Update` 34 B, `NightDirector.Update` 128 B, `WeatherDirector.Update` 108 B per frame | hotspots-before.md | owning teams |
| `EconomyDirector.ActiveModifiers` is a `yield` iterator (alloc per call, used per frame by HUD) | code | gameplay (economy) |
| Critter LODs still missing (GDD known gap) -- tris are fine today at 13 critters, re-check at 30 | renderer audit | technical-artist |
| GPU time + memory need a **development player build on a base M1** | -- | devops / perf |
| Terrain `treeDistance` 5000, castShadows TwoSided | renderer audit | technical-artist |
