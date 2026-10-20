# Perf / Playtest progress (feature/perf-playtest)

## Session 1 (2026-10-20/21)
- Added `HarvestPanic/Assets/Scripts/Tests/PlayMode/Perf/` (asmdef `HarvestPanic.Tests.PlayMode.Perf`):
  `BotFarmers.cs` (scripted dummy farmers), `PerfHarness.cs` (boot + ProfilerRecorder bundle), `PerfCaptureTests.cs`.
- Baseline capture `docs/qa/perf/raw/perf-before.md`: 5.0 ms main thread (M2 Pro editor), 632 SRP batches,
  6.2k draw calls, 6.2k shadow casters, 585k tris, 3.2 KB / 76 GC allocs per frame.
- Run: `Unity -batchmode -projectPath HarvestPanic -runTests -testPlatform PlayMode -testFilter HarvestPanic.Tests.PlayMode.Perf.PerfCaptureTests -netPort <random> -perfLabel <label> -testResults <abs> -logFile <abs>`
- Next: attribute GC/CPU via profiler hierarchy, fix top 3, soak test.
