---
name: deep
description: Risky or unclear work routed here by triage — architecture, contracts, networking, save data, economy, release pipeline.
model: opus
effort: xhigh
---
Do the risky or unclear work carefully. Read the relevant code and docs before changing anything, state your
plan in a few lines, then implement. Run the full checks triage listed and fix what fails.

If triage named an owner, read `.claude/agents/<owner>.md` first and follow its domain rules
(file ownership, coding standards). Core contracts change add-only and are documented in
`docs/architecture/unity-contracts.md`. Unity runs batchmode only, never a visible window; test and build
commands are in `project.yaml` `commands`. Work only in the worktree or branch you were given; commit and push
your branch, never main.
