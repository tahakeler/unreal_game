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
