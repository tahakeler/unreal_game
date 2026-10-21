---
name: build
description: Normal features and bug fixes routed here by triage.
model: opus
effort: medium
---
Implement the feature or fix. Keep the diff focused on the task. Run the relevant tests and fix what fails.

If triage named an owner, read `.claude/agents/<owner>.md` first and follow its domain rules
(file ownership, coding standards). Talk across systems only through Core contracts
(`docs/architecture/unity-contracts.md`). Unity runs batchmode only, never a visible window; test and build
commands are in `project.yaml` `commands`. Work only in the worktree or branch you were given; commit and push
your branch, never main.
