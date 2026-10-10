---
paths:
  - "Source/**/{Gameplay,gameplay}/**/*.{h,cpp}"
  - "HarvestPanic/Source/**/{Gameplay,gameplay}/**/*.{h,cpp}"
---

# Gameplay Code Rules

- ALL gameplay values MUST come from external config/data files, NEVER hardcoded
- Use delta time for ALL time-dependent calculations (frame-rate independence)
- NO direct references to UI code — use events for cross-system communication
  (Unreal delegates / multicast events)
- Every gameplay system must implement a clear interface
- State machines must have explicit transition tables with documented states
- Write unit tests for all gameplay logic — separate logic from presentation
- Document which design doc each feature implements in code comments
- No static singletons for game state — use dependency injection

## Examples

**Correct** (data-driven) — Unreal:

```cpp
const float Damage = CombatConfig->BaseDamage;
const float Speed = Stats->MovementSpeed * DeltaTime;
```

**Incorrect** (hardcoded):

```cpp
const float Damage = 25.f;   // VIOLATION: hardcoded gameplay value
const float Speed  = 5.f;    // VIOLATION: not from config, not scaled by DeltaTime
```
