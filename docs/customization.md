# Character Customization: Implementation Notes

GDD: `design/gdd/character-customization.md`. Code: `HarvestPanic/Source/HarvestPanic/Customization/`.

## Architecture
| Class | Role |
|---|---|
| `FHPCosmeticLoadout` | Replicated struct. Custom `NetSerialize` gives a **12-byte** payload: body u8, primary/secondary/accent u8 (indices into the 24-swatch palette), skin u8, hat/face/back u16 NetIds (0 = None), name-tag u8 |
| `UHPCosmeticCatalog` | `UPrimaryDataAsset` whose defaults are authored in C++. If an asset exists at `/Game/HarvestPanic/Customization/DA_HPCosmeticCatalog`, it overrides the defaults. Holds items, palette, skins, emotes, tuning and `bUnlockAllCosmetics` (default **true**) |
| `FHPCosmeticApplier` | Stateless. Swaps Manny/Quinn and tints body materials (`Paint Tint`). It builds accessory parts as StaticMeshComponents on bones. Part offsets are in mesh-component axes and are converted with the reference pose |
| `UHPCosmeticsComponent` | Lives on the **PlayerState** and is added at runtime by the server. `Server_SetLoadout` sanitizes and rate-limits, then the loadout replicates (`OnRep`) and is applied to the owned pawn. It re-applies on `OnPawnSet` (respawn) and every 0.5 s if the pawn changed. It also handles emotes and the name tag |
| `UHPCustomizationSubsystem` | GameInstance subsystem. `FGameModeEvents::GameModePostLoginEvent` plus a 0.25 s sweep of `GameState->PlayerArray` make sure every PlayerState has the component. Adds a runtime IMC (priority 100): keys 5-8 trigger emotes and C opens the wardrobe |
| `UHPLoadoutSaveGame` | Save slot `HP_Loadout`. A first-time player gets a random silly loadout, which is saved immediately |
| `UHPCustomizationWidget` | Pure C++ UMG with tabs (Body/Colours/Hat/Face/Back/Name Tag), pickers, swatches, *Surprise me!*, Cancel and Confirm. The live preview comes from `AHPCustomizationPreview` (scene capture to a render target, spawned at Z=60000 with a show-only list) |
| `AHPWardrobe` | Placeable barn wardrobe. Has an overlap sphere and a local "[C] Change outfit" prompt |

## Frontend embedding
```cpp
UClass* C = StaticLoadClass(UUserWidget::StaticClass(), nullptr, TEXT("/Script/HarvestPanic.HPCustomizationWidget"));
UUserWidget* W = CreateWidget(PC, C); W->AddToViewport();
```
Bind `OnConfirmed(FHPCosmeticLoadout)` / `OnClosed`. Confirm always saves locally and, if a cosmetics component exists for the player, sends the loadout to the server. Set `bCloseOnConfirm=false` if the frontend wants to manage the widget's lifetime itself.

## Hooks for other owners
- `UHPCosmeticsComponent::OnAnyEmotePlayed` (server, native multicast `(APawn*, EHPEmote)`) is a TODO: Wave should emit a noise event and Faint should drop the carried item.
- Faint ragdoll: the server calls the first UFUNCTION it finds on the pawn among `HPEmoteFaint(float)`, `StartEmoteRagdoll(float)` and `StartRagdoll(float)`. **None exists yet**, so Faint plays `MM_Death_Back_01` as a held fall pose.
- 1P arms: `UFUNCTION GetArmsMesh1P()` returning a mesh component gets the accent colour. Not present yet, so this is skipped.

## Deviations / limitations
- The template mannequin has no skin material. Skin tone is stored, replicated and saved but has **no visible effect** until a body with skin is added.
- The widget tree is built in `NativeOnInitialized`, not `NativeConstruct`, because the root must exist before `TakeWidget`. There is no Esc/C-to-close, since that needs `FReply`, which needs the SlateCore link dependency; use the Cancel button.
- Meshes load synchronously (small assets). The GDD's async load with a straw-hat placeholder is still to do.
- Not built yet: hat pop-off on ragdoll, Pumpkin/Bucket 1P vignette, equipped-emote slots.
- Emote anims switch the body to single-node playback and then restore the ABP. Root motion on the emote sequences is locked at runtime.

## QA
- Tests: `Automation RunTests HarvestPanic.Customization` (CatalogDefaults, LoadoutValidation, SerializationRoundTrip).
- Screenshots: `HarvestPanic/Scripts/Customization/run_showcase.sh` (offscreen) writes `production/qa/evidence/customization-*.png`. Console commands: `HP.Customization.Showcase [dir] [quit]`, `HP.Customization.Open`, `HP.Emote 0-3`.
