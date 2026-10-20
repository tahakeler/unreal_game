# Perf / Playtest progress (feature/perf-playtest)

## Session 1 (2026-10-20/21)
- Added `HarvestPanic/Assets/Scripts/Tests/PlayMode/Perf/` (asmdef `HarvestPanic.Tests.PlayMode.Perf`):
  `BotFarmers.cs` (scripted dummy farmers), `PerfHarness.cs` (boot + ProfilerRecorder bundle), `PerfCaptureTests.cs`.
- Baseline capture `docs/qa/perf/raw/perf-before.md`: 5.0 ms main thread (M2 Pro editor), 632 SRP batches,
  6.2k draw calls, 6.2k shadow casters, 585k tris, 3.2 KB / 76 GC allocs per frame.
- Run: `Unity -batchmode -projectPath HarvestPanic -runTests -testPlatform PlayMode -testFilter HarvestPanic.Tests.PlayMode.Perf.PerfCaptureTests -netPort <random> -perfLabel <label> -testResults <abs> -logFile <abs>`
- Next: attribute GC/CPU via profiler hierarchy, fix top 3, soak test.

## Session 2 (2026-10-21)
- `PerfHotspotTests` (profiler hierarchy + renderer audit) -> `docs/qa/perf/raw/hotspots-before.md`.
- Fixes: GPU Resident Drawer on + static batching off + BRG variants kept (6,187 -> 508 draw calls, 5.0 -> 3.8 ms);
  GC fixes EconomyHud (1.2 KB/f), WeatherLook.Hex (650 B/f), MrStitches (300 B/f): 76 -> 38 allocs/frame.
  Written up in `docs/qa/perf/perf-baseline.md`.
- `BotWeekSoakTests` (full week + Sunday + into week 2, night each Dusk) -> `docs/qa/playtest/raw/bot-week-log.md`.
- Deep profiling (-deepprofiling) on Muddlebury exceeds 10 min; don't use it, use the merged hierarchy instead.
