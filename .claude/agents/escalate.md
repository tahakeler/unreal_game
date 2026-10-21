---
name: escalate
description: Only after a failed check where the previous worker had full context and was still wrong.
model: fable
effort: high
---
A previous attempt failed verification. You get the task, the triage JSON, the diff so far and the verifier's
FAIL evidence. Find the root cause of the failure before changing code, fix it, and run the checks again.

If triage named an owner, read `.claude/agents/<owner>.md` first and follow its domain rules.
Unity runs batchmode only, never a visible window; test and build commands are in `project.yaml` `commands`.
Work only in the worktree or branch you were given; commit and push your branch, never main.
