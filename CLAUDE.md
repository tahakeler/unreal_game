# Claude Code Game Studios -- Game Studio Agent Architecture

Indie game development managed through 39 coordinated Claude Code subagents.
Each agent owns a specific domain, enforcing separation of concerns and quality.

## Technology Stack

- **Game**: HARVEST PANIC — first-person 2–4 player co-op; Unity project folder `HarvestPanic/` (`Assets/`, `Packages/`, `ProjectSettings/`)
- **Engine**: Unity 6.3 LTS `6000.3.26f1` (Apple Silicon editor at `/Applications/Unity/Hub/Editor/6000.3.26f1`)
- **Render Pipeline**: Universal Render Pipeline (URP)
- **Language**: C# (gameplay code under `HarvestPanic/Assets/Scripts/`)
- **Networking**: Netcode for GameObjects; Steam transport via Facepunch.Steamworks (ship), Unity Transport/LAN (dev)
- **Version Control**: Git + Git LFS (binary assets only; Unity YAML stays text), one branch per task (see `docs/engine/unity-setup.md`)
- **Build System**: Unity batchmode CLI (`-runTests` for EditMode/PlayMode, `-executeMethod` for player builds) — see `project.yaml` `commands`
- **Asset Pipeline**: Unity Asset Database (Force Text, Visible Meta Files); third-party/CC0 packs under `HarvestPanic/Assets/ThirdParty/<Pack>/`, licences in `THIRD_PARTY_LICENSES.md`
- **Platforms**: develop on macOS (Apple Silicon); ship Windows + macOS on Steam

> **Note**: This repo is Unity-only. Use `unity-specialist` and the `unity-*`
> sub-specialists; Godot and Unreal agent sets have been removed. The previous
> Unreal build of the game is archived on branch `archive/unreal-final`.

## Project Structure

@.claude/docs/directory-structure.md

## Engine Version Reference

<!-- ENGINE-REFERENCE-IMPORT: pinned to Unity by /setup-engine. -->
@docs/engine-reference/unity/VERSION.md


## Technical Preferences

`project.yaml` at the repo root is the primary config store — engine, specialists,
naming, platform, performance, modes. Skills resolve it via `resolve_config`
(see `.claude/docs/config-resolution.md`).

`.claude/docs/technical-preferences.md` is the **legacy fallback**, read on demand
when a key is absent from `project.yaml`. It is no longer imported here: before
`/setup-engine` runs it is almost entirely `[TO BE CONFIGURED]` placeholders, and
after it runs `project.yaml` holds the real values.

## Coordination Rules

@.claude/docs/coordination-rules.md

## Collaboration Protocol

**User-driven collaboration, not autonomous execution.**
Every task follows: **Question -> Options -> Decision -> Draft -> Approval**

- Agents MUST ask "May I write this to [filepath]?" before using Write/Edit tools
  (exempt: `active.md` appends, agent memory, a subagent's new file at the path
  its orchestrating skill named; see `.claude/rules/agent-memory.md`)
- Agents MUST show drafts or summaries before requesting approval
- Multi-file changes require explicit approval for the full changeset
- No commits without user instruction
- A skipped or dismissed question is not an answer: act on nothing, re-ask in
  plain text and wait
- Started outside this file's folder? Warn first: hooks, deny rules and the
  ask-first mode load only when Claude Code starts here

See `docs/COLLABORATIVE-DESIGN-PRINCIPLE.md` for full protocol and examples.

> **First session?** If the project has no engine configured and no game concept,
> run `/start` to begin the guided onboarding flow.

## Coding Standards

@.claude/docs/coding-standards.md

## Context Management

Read `.claude/docs/context-management.md` on demand — it is a reference, not
session context. Two of its conventions are load-bearing and cited by name
elsewhere in the repo, so they are restated here rather than lost:

- **`production/session-state/active.md` is the session checkpoint.** The file is
  the memory, not the conversation. Read it first after any compaction, crash, or
  `/clear`.
- **Helpers in `.claude/scripts/` emit observations, never verdicts.** A script
  that scores or judges will eventually contradict a mode or override it cannot
  see. (Cited by `artifact-check.sh` and `adr-dep-graph.sh`.)
