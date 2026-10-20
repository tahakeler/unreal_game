# Hotspots `before` -- 268 profiled frames (main thread, merged by marker name, per-frame averages)

## Top 30 by self time
| Marker | self ms/frame | calls/frame | GC B/frame |
|---|---|---|---|
| `SRPBatcher.Flush` | 1.538 | 26 | 0 |
| `ExecuteRenderQueueJob` | 0.539 | 5.4 | 0 |
| `WaitForJobGroupID` | 0.536 | 29.2 | 0 |
| `EditorLoop` | 0.345 | 3 | 0 |
| `QuadTreeNode.Render` | 0.294 | 192 | 0 |
| `PrepareRenderTarget` | 0.251 | 332 | 0 |
| `ScheduleGeometryJobs` | 0.229 | 2 | 0 |
| `Shadows.DrawSRPBatcher` | 0.194 | 9 | 0 |
| `RenderLoop.DrawSRPBatcher` | 0.184 | 8 | 0 |
| `Profiler.FlushMemoryCounters` | 0.158 | 1 | 0 |
| `Shadows.Sort` | 0.141 | 0.9 | 0 |
| `PostLateUpdate.BatchModeUpdate` | 0.140 | 1 | 0 |
| `Inl_On Record Render Graph` | 0.119 | 1 | 0 |
| `UnityEngine.CoreModule.dll!::UpdateFunction.Invoke() [Invoke]` | 0.116 | 8.4 | 77 |
| `RenderLoop.CleanupNodeQueue` | 0.096 | 7 | 0 |
| `SRPBRender.ApplyShader` | 0.093 | 16 | 0 |
| `Inl_Forward+ Upload` | 0.068 | 1 | 0 |
| `Idle` | 0.068 | 0.2 | 0 |
| `Shadows.SplitAndSortCasters` | 0.058 | 0.9 | 0 |
| `Inl_UniversalRenderPipeline.RenderSingleCameraInternal: ViewCamera` | 0.050 | 1 | 0 |
| `PxScene.simulate` | 0.049 | 0.4 | 0 |
| `BehaviourUpdate` | 0.049 | 1 | 2167 |
| `QueuePrepareIntegrateMainThreadObjects` | 0.048 | 2 | 0 |
| `GpuProgramMetal.GetCachedPipeline` | 0.047 | 723.3 | 0 |
| `TransformChangeSystem` | 0.047 | 42.6 | 0 |
| `RenderLoop.Draw` | 0.046 | 7 | 0 |
| `UpdateScene` | 0.046 | 1 | 2983 |
| `RG_BloomDownsample` | 0.041 | 1 | 0 |
| `Graphics.DrawProcedural` | 0.040 | 18 | 0 |
| `HarvestPanic.Economy.dll!HarvestPanic.Economy::EconomyHud.Update() [Invoke]` | 0.037 | 1 | 1228 |

## Top 30 by GC alloc
| Marker | GC B/frame | self ms/frame | calls/frame |
|---|---|---|---|
| `PlayerLoop` | 2983 | 0.000 | 2 |
| `UpdateScene` | 2983 | 0.046 | 1 |
| `GC.Alloc` | 2983 | 0.002 | 68.2 |
| `Update.ScriptRunBehaviourUpdate` | 2167 | 0.000 | 1 |
| `BehaviourUpdate` | 2167 | 0.049 | 1 |
| `HarvestPanic.Economy.dll!HarvestPanic.Economy::EconomyHud.Update() [Invoke]` | 1228 | 0.037 | 1 |
| `PreLateUpdate.ScriptRunBehaviourLateUpdate` | 648 | 0.000 | 1 |
| `LateBehaviourUpdate` | 648 | 0.018 | 1 |
| `HarvestPanic.Weather.dll!HarvestPanic.Weather::WeatherPresenter.LateUpdate() [Invoke]` | 648 | 0.035 | 1 |
| `HarvestPanic.AI.Night.dll!HarvestPanic.AI.Night::MrStitches.Update() [Invoke]` | 296 | 0.030 | 1 |
| `HarvestPanic.UI.dll!HarvestPanic.UI::OnboardingOverlay.Update() [Invoke]` | 266 | 0.016 | 1 |
| `HarvestPanic.AI.Night.dll!HarvestPanic.AI.Night::NightDirector.Update() [Invoke]` | 128 | 0.002 | 1 |
| `HarvestPanic.Weather.dll!HarvestPanic.Weather::WeatherDirector.Update() [Invoke]` | 109 | 0.021 | 1 |
| `Update.ScriptRunDelayedDynamicFrameRate` | 80 | 0.000 | 1 |
| `CoroutinesDelayedCalls` | 80 | 0.006 | 3.4 |
| `UnityEngine.TestRunner.dll!UnityEngine.TestTools.TestRunner::PlaymodeTestsController.WrapEnumeratorForChecks() [Coroutine: MoveNext] [Invoke]` | 80 | 0.015 | 1 |
| `UnityEngine.CoreModule.dll!::UpdateFunction.Invoke() [Invoke]` | 77 | 0.116 | 8.4 |
| `NetworkPreUpdate` | 77 | 0.000 | 1 |
| `UnityEngine.UI.dll!UnityEngine.EventSystems::EventSystem.Update() [Invoke]` | 64 | 0.006 | 1 |
| `HarvestPanic.UI.dll!HarvestPanic.UI::PauseOverlay.Update() [Invoke]` | 42 | 0.003 | 1 |
| `HarvestPanic.Audio.dll!HarvestPanic.Audio::AudioService.Update() [Invoke]` | 34 | 0.018 | 1 |
| `PreLateUpdate.UIElementsUpdatePanels` | 11 | 0.000 | 1 |
| `UnityEngine.UIElementsModule.dll!UnityEngine.UIElements::UIElementsRuntimeUtilityNative.UpdatePanels() [Invoke]` | 11 | 0.011 | 1 |
| `HP_Panel_UI_Overlay.TickScheduledActions` | 11 | 0.001 | 1 |
| `UIElements.UpdateAssetsInEditor` | 11 | 0.001 | 2 |

## Renderer audit
- active renderers 5785 (skinned 18), shadow casting 5676, static-batched 5522, distinct materials 102 (instancing on 21)
- terrains 1, detailDist 80, treeDist 5000, pixelError 4, castShadows TwoSided
- lights: Sun (golden afternoon)(Directional,Soft), NightHandLantern(Point,None)

| mesh @ root | renderers | shadow casters | tris (sum) |
|---|---|---|---|
| `Combined Mesh (root: scene) 21 @ Muddlebury` | 496 | 496 | 16215232 |
| `Combined Mesh (root: scene) 24 @ Muddlebury` | 493 | 493 | 16002780 |
| `Combined Mesh (root: scene) 19 @ Muddlebury` | 489 | 489 | 15889566 |
| `Combined Mesh (root: scene) 23 @ Muddlebury` | 486 | 486 | 15974820 |
| `Combined Mesh (root: scene) 20 @ Muddlebury` | 484 | 484 | 15870360 |
| `Combined Mesh (root: scene) 25 @ Muddlebury` | 481 | 481 | 15705612 |
| `Combined Mesh (root: scene) 26 @ Muddlebury` | 476 | 476 | 15518552 |
| `Combined Mesh (root: scene) 22 @ Muddlebury` | 445 | 445 | 14536815 |
| `Combined Mesh (root: scene) 18 @ Muddlebury` | 432 | 432 | 14648256 |
| `Combined Mesh (root: scene) 27 @ Muddlebury` | 188 | 188 | 3525940 |
| `Combined Mesh (root: scene) 17 @ Muddlebury` | 127 | 127 | 4238498 |
| `Combined Mesh (root: scene) 3 @ Muddlebury` | 108 | 108 | 139968 |
| `Combined Mesh (root: scene) 5 @ Muddlebury` | 79 | 79 | 2504616 |
| `Combined Mesh (root: scene) 16 @ Muddlebury` | 78 | 78 | 2458248 |
| `Cylinder @ Muddlebury` | 73 | 3 | 5840 |
| `Combined Mesh (root: scene) 15 @ Muddlebury` | 69 | 69 | 2190888 |
| `Combined Mesh (root: scene) 14 @ Muddlebury` | 67 | 67 | 2102460 |
| `Combined Mesh (root: scene) 12 @ Muddlebury` | 65 | 65 | 2041260 |
| `Combined Mesh (root: scene) 13 @ Muddlebury` | 65 | 65 | 2055300 |
| `Combined Mesh (root: scene) 10 @ Muddlebury` | 63 | 63 | 1992816 |
| `Combined Mesh (root: scene) 7 @ Muddlebury` | 61 | 61 | 1921744 |
| `Combined Mesh (root: scene) 8 @ Muddlebury` | 61 | 61 | 1908690 |
| `Combined Mesh (root: scene) 6 @ Muddlebury` | 61 | 61 | 1928820 |
| `Combined Mesh (root: scene) 9 @ Muddlebury` | 60 | 60 | 1899480 |
| `Combined Mesh (root: scene) 11 @ Muddlebury` | 59 | 59 | 1834546 |

Materials without GPU instancing (top 20 by name):
- `FPArms_Cuff` (Universal Render Pipeline/Lit)
- `FPArms_Glove` (Universal Render Pipeline/Lit)
- `FPArms_Shirt` (Universal Render Pipeline/Lit)
- `FPArms_Skin` (Universal Render Pipeline/Lit)
- `HP_CosmeticFallback_5C3B24` (Universal Render Pipeline/Lit)
- `HP_CosmeticFallback_8C5C38` (Universal Render Pipeline/Lit)
- `HP_CosmeticFallback_99BFD9` (Universal Render Pipeline/Lit)
- `HP_CosmeticFallback_E8C773` (Universal Render Pipeline/Lit)
- `HP_CosmeticFallback_F5C442` (Universal Render Pipeline/Lit)
- `M_Critter_Cow` (Universal Render Pipeline/Lit)
- `M_Critter_Goat` (Universal Render Pipeline/Lit)
- `M_Tripo_Chicken` (Universal Render Pipeline/Lit)
- `M_Tripo_EggBasket` (Universal Render Pipeline/Lit)
- `M_Tripo_MarketStall` (Universal Render Pipeline/Lit)
- `M_Tripo_MarketStall_Greengrocer` (Universal Render Pipeline/Lit)
- `M_Tripo_MarketStall_Oddities` (Universal Render Pipeline/Lit)
- `M_Tripo_PenGate` (Universal Render Pipeline/Lit)
- `M_Tripo_Pig_Hamlet` (Universal Render Pipeline/Lit)
- `M_Tripo_PrizePumpkin` (Universal Render Pipeline/Lit)
- `M_World_BarnBrick` (Universal Render Pipeline/Lit)
