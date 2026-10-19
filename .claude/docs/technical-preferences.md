# Technical Preferences

<!-- project.yaml at the repo root is the machine-readable source of truth for
     engine, specialists, naming, performance, platform, and testing.framework.
     This file is the human-readable LEGACY FALLBACK: agents and skills resolve
     each key from project.yaml first and fall back here only when the
     project.yaml key is absent. /setup-engine dual-writes both.
     Forbidden patterns and allowed libraries are NOT migrated — they live only
     in this file. Populated by /setup-engine; updated as decisions are made. -->

## Engine & Language

- **Engine**: Unity 6.3 LTS (6000.3.26f1)
- **Language**: C#
- **Rendering**: Universal Render Pipeline (URP)
- **Physics**: PhysX (built-in 3D physics)
- **Networking**: Netcode for GameObjects; Steam transport via Facepunch.Steamworks (ship), Unity Transport (UTP) on LAN (dev)

## Input & Platform

<!-- Written by /setup-engine. Read by /ux-design, /ux-review, /test-setup, /team-ui, and /dev-story -->
<!-- to scope interaction specs, test helpers, and implementation to the correct input methods. -->

- **Target Platforms**: PC — Windows + macOS (Steam); development on macOS Apple Silicon
- **Input Methods**: Keyboard/Mouse, Gamepad
- **Primary Input**: Keyboard/Mouse (first-person, 2–4 player co-op)
- **Gamepad Support**: Partial (recommended for PC / Steam Deck-friendly)
- **Touch Support**: None
- **Platform Notes**: [TO BE CONFIGURED — any platform-specific UX constraints]

## Naming Conventions

- **Classes**: PascalCase (e.g. `PlayerController`); namespaces `HarvestPanic.<Area>`
- **Variables**: public fields/properties PascalCase (`MoveSpeed`); private fields `_camelCase` (`_moveSpeed`); locals/parameters camelCase
- **Signals/Events**: C# events / UnityEvents PascalCase `On<Event>` (e.g. `OnCropHarvested`)
- **Files**: PascalCase matching the class (e.g. `PlayerController.cs`); one public type per file
- **Scenes/Prefabs**: PascalCase (e.g. `Farm.unity`, `PlayerCharacter.prefab`)
- **Constants**: SCREAMING_SNAKE_CASE (unsourced default; revisit — Unity docs also use PascalCase)

## Performance Budgets

- **Target Framerate**: [TO BE CONFIGURED]
- **Frame Budget**: [TO BE CONFIGURED]
- **Draw Calls**: [TO BE CONFIGURED]
- **Memory Ceiling**: [TO BE CONFIGURED]

## Testing

- **Framework**: Unity Test Framework (NUnit) — EditMode + PlayMode under `HarvestPanic/Assets/Tests/` (not yet scaffolded — run /test-setup)
- **Minimum Coverage**: [TO BE CONFIGURED]
- **Required Tests**: Balance formulas, gameplay systems, networking (if applicable)

## Forbidden Patterns

<!-- Add patterns that should never appear in this project's codebase -->
- [None configured yet — add as architectural decisions are made]

## Allowed Libraries / Addons

<!-- Add approved third-party dependencies here -->
- [None configured yet — add as dependencies are approved]

## Architecture Decisions Log

<!-- Quick reference linking to full ADRs in docs/architecture/ -->
- [No ADRs yet — use /architecture-decision to create one]

## Engine Specialists

<!-- Written by /setup-engine when engine is configured. -->
<!-- Read by /code-review, /architecture-decision, /architecture-review, and team skills -->
<!-- to know which specialist to spawn for engine-specific validation. -->

- **Primary**: unity-specialist
- **Language/Code Specialist**: unity-specialist (C# review — primary covers it)
- **Shader Specialist**: unity-shader-specialist (Shader Graph, HLSL, URP materials)
- **UI Specialist**: unity-ui-specialist (UI Toolkit UXML/USS, UGUI Canvas, runtime UI)
- **Additional Specialists**: unity-addressables-specialist (asset loading, memory, content catalogs), unity-dots-specialist (ECS, Jobs, Burst — only if adopted)
- **Routing Notes**: Primary for architecture and general C# review, including Netcode for GameObjects code (pair with network-programmer for netcode architecture). Shader specialist for rendering/VFX. UI specialist for all interface work. Addressables specialist for asset-loading systems. DOTS specialist only for ECS/Jobs/Burst code.

### File Extension Routing

<!-- Skills use this table to select the right specialist per file type. -->

| File Extension / Type | Specialist to Spawn |
|-----------------------|---------------------|
| Game code (.cs files) | unity-specialist |
| Shader / material files (.shader, .shadergraph, .hlsl, .mat) | unity-shader-specialist |
| UI / screen files (.uxml, .uss, Canvas prefabs) | unity-ui-specialist |
| Scene / prefab / level files (.unity, .prefab) | unity-specialist |
| Native extension / plugin files (.dll, native plugins, .asmdef) | unity-specialist |
| General architecture review | Primary |
