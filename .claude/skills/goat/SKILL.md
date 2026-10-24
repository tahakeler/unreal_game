---
name: goat
description: "Run one or more coding tasks through the full GOAT model router: triage → quick/build/deep worker → verifier → one escalation step → merge + lesson. Parallel for independent tasks."
argument-hint: "[task description] | [task 1] ;; [task 2] ;; ..."
user-invocable: true
allowed-tools: Agent, SendMessage, Read, Glob, Grep, Bash
---

# GOAT router workflow

The owner's Model Routing Playbook as one end-to-end pipeline. Standing rule (CLAUDE.md "Routing (GOAT)"):
every coding task goes through it. Agents: `triage`, `quick`, `build`, `deep`, `escalate`, `verifier`
(`.claude/agents/`). Never pass a `model` parameter; each agent carries its own model + effort.

Input: `$ARGUMENTS`. Split on `;;` into independent tasks. If empty, use the open items in
`production/session-state/active.md`.

## 1 · Triage (one call for all tasks)

Call `triage` once with every task, numbered. Ask for a JSON array, one object per task:
`{"task","route":"quick|build|deep","risk":"low|medium|high","checks":[...],"owner":"<studio agent>"}`.
Design-only tasks (GDD, art, audio, narrative text) skip the router: send them to the owning studio agent.

## 2 · Isolate

One branch + worktree per task so parallel workers never touch the same files:

```bash
S=<scratchpad>/wt; git worktree add -q -b <feature|fix>/<slug> $S/<slug> main
cp -c -R $S/integ/HarvestPanic/Library $S/<slug>/HarvestPanic/Library   # APFS clone, skips a cold import
```

If two tasks would edit the same system, run them one after the other instead.

## 3 · Work (parallel, background)

Spawn the agent named by `route` for each task with `run_in_background: true`. The prompt carries:
- USER-APPROVED line (write/commit/push the branch without prompts; never push main)
- the triage JSON (worker reads `.claude/agents/<owner>.md` for domain rules)
- the worktree path + branch, the brief, and pointers to the GDD / contracts / progress docs
- Unity rules: batchmode only, never a visible window, absolute `-logFile`, `-runTests` without `-quit`,
  random `-netPort` for networked tests, screenshots via `HP_QA_DIR` tests (Read them)
- "commit + push regularly, keep `docs/qa/<slug>-progress.md`, end with a report: built, tests, gaps, head"

If a worker stops on a usage limit or turn limit, resume it with SendMessage ("continue"), never a new agent.

## 4 · Verify

When a worker reports, call `verifier` with the worktree, the risk and the checks from triage:
low = EditMode · medium = EditMode + PlayMode · high = + `tools/net/two-process-test.sh` on a fresh mac dev
build + careful diff read. It answers PASS or FAIL with evidence and never edits.

## 5 · Escalate once on FAIL

- worker skipped a file or didn't run the tests → **more effort**: re-run one route up (quick → build)
- worker had full context and was still wrong → **bigger model**: build → deep → `escalate`

Pass the verifier's FAIL evidence. Verify again. A second FAIL stops the task and goes to the owner.

## 6 · Merge (lead only)

On PASS: merge the branch into `integration/unity-slice` (`git -c merge.autoStash=false merge`), resolve
conflicts, run EditMode + PlayMode (+ two-process test for networked work) on the combination, then merge
into `main` and push. Read key screenshots yourself before merging visual work.

## 7 · Learn

After any escalation, ask `triage` to append one line to its MEMORY.md:
`"<task pattern> → <route that worked>"`. Update `production/session-state/active.md` with what merged and
what is still open.

## Report to the owner

One table per run: task · route · verifier result · merged? · gaps that need the owner.
