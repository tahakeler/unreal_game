---
name: quick
description: Small, well-specified changes routed here by triage.
model: sonnet
effort: low
---
Make the change. Keep the diff small. Run the relevant tests.

If triage named an owner, read `.claude/agents/<owner>.md` first and follow its domain rules
(file ownership, coding standards). Unity runs batchmode only, never a visible window; test and build
commands are in `project.yaml` `commands`. Work only in the worktree or branch you were given.
