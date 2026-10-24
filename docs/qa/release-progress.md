# Release readiness progress (branch feature/release-readiness)

Owner: release-manager. Scope: release docs (`production/release/**`), Credits screen, Run Save robustness tests,
packaging scripts (`tools/release/`). Gate report: `production/release/early-beta-gate.md` (in progress).

## Session 1 (2026-10-21)
- Credits screen: `UI/Menu/CreditsText.cs` (THIRD_PARTY_LICENSES.md -> plain text, full MIT text per MIT component,
  OFL font licences appended), `UI/Menu/CreditsPanel.cs`, title button `btn-credits` + `credits-screen` in
  `UI_MainMenu.uxml`, `MenuScreen.Credits`, `MainMenuController.OpenCredits()`.
- Build hook `UI/Editor/CreditsBuildStep.cs` (IPreprocess/IPostprocessBuildWithReport): bakes
  `Assets/Resources/Frontend/Generated/Licenses.txt` (gitignored) and writes `LICENSES.txt` beside the player.
  Runs for every BuildScript method, no BuildScript change needed.
- Tests written: EditMode `UI/CreditsTests` (5), `Economy/RunSaveRobustnessTests` (7 kill points + 6), PlayMode
  `UI/CreditsFlowTests` (1), `Economy/RunSaveResumePlayTests` (2). Not yet compiled at this commit.
- Next: compile + run suites, packaging scripts, Steam depot template, gate report, release notes, changelog.

## Session 2 (2026-10-21)
- Merged `origin/main` twice (`dec81b4` perf/playtest + routing, `366c311` editor settings). No conflicts.
- Tests: EditMode **301/301** (RunSaveRobustnessTests 14, CreditsTests 5). PlayMode **79: 64 passed, 0 failed,
  15 skipped** (all GPU/QA captures, each with a reason), incl. RunSaveResumePlayTests 2, CreditsFlowTests 1,
  BotWeekSoakTests, PerfCaptureTests.
- Fixed (test code): SessionRobustnessTests clients now dial the host transport's real port. `SessionService.Port()`
  prefers `-netPort`, so passing it to the editor made all 4 fail. Reasons added to the 2 `[Explicit]` economy
  QA captures.
- Builds: BuildWindowsRelease 142.1 MB (zip 58.6 MB), BuildMacRelease Universal 161.3 MB (unsigned zip 67.8 MB). Both
  have `LICENSES.txt` beside the player, and the baked Credits TextAsset is byte-identical to it. `package-mac.sh` now
  zips without `._*` AppleDouble entries.
- Findings: the macOS player has no usable Steam library (the package `libsteam_api.bundle` is i386/x86_64 only and not
  included; the build picks `Facepunch.Steamworks.Linux.dll`), so it is LAN only. Each player build rewrites
  `HP_URP.asset` and the URP global settings (reverted, not committed).
- Gate: `production/release/early-beta-gate.md`: 1 MET (5), 5 PARTIAL (3, 4, 6, 8, 10), 4 BLOCKED-ON-OWNER (1, 2, 7, 9).
- Not done: BuildMacDev + `tools/net/two-process-test.sh` on this head; the 2 GPU-only tests with a graphics device.
