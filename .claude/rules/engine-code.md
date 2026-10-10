---
paths:
  - "Source/**/{Core,core}/**/*.{h,cpp}"
  - "HarvestPanic/Source/**/{Core,core}/**/*.{h,cpp}"
---

# Engine Code Rules

- ZERO allocations in hot paths (update loops, rendering, physics) — pre-allocate, pool, reuse
- All engine APIs must be thread-safe OR explicitly documented as single-thread-only
- Profile before AND after every optimization — document the measured numbers
- Engine code must NEVER depend on gameplay code (strict dependency direction: engine <- gameplay)
- Every public API must have usage examples in its doc comment
- Changes to public interfaces require a deprecation period and migration guide
- Use RAII / deterministic cleanup for all resources
- All engine systems must support graceful degradation
- Before writing engine API code, consult `docs/engine-reference/` for the current engine version and verify APIs against the reference docs

## Examples

**Correct** (zero-alloc hot path) — Unreal:

```cpp
// TArray member, reserved once; Reset() keeps the allocation
void UProximityComponent::TickComponent(float DeltaTime, ELevelTick TickType,
                                        FActorComponentTickFunction* ThisTickFunction)
{
    NearbyCache.Reset();
    SpatialGrid->QueryRadius(GetComponentLocation(), Radius, NearbyCache);
}
```

**Incorrect** (allocating in hot path):

```cpp
void UProximityComponent::TickComponent(float DeltaTime, ELevelTick TickType,
                                        FActorComponentTickFunction* ThisTickFunction)
{
    TArray<AActor*> Nearby;  // VIOLATION: allocates every frame
    UGameplayStatics::GetAllActorsWithTag(this, TEXT("Enemy"), Nearby);  // VIOLATION: world query every frame
}
```
