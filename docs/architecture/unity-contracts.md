# Unity Contracts and Ownership

Seven teams build in parallel. They share **only** `HarvestPanic.Core` (`Assets/Scripts/Core/Contracts/`),
the `Services` locator, data assets and marker tags. Never reference another team's assembly.

## Asmdef graph

```
HarvestPanic.Core  <-  Gameplay (Player), AI, World, Customization, UI (Frontend), Economy (+HUD), Networking
```
Teams reference Core + Unity packages only, never each other. Tests: each team adds its own
`Tests/EditMode/<Team>/` asmdef (tests may reference any assembly); do not edit the shared
`HarvestPanic.Tests.EditMode` asmdef beyond the Core contract tests. New asmdef `HarvestPanic.Economy` was added for Economy+HUD.
Cross-talk = register an interface in `Services` (`Services.Register<T>`, `Get<T>`, `TryGet`, `WhenAvailable`) at OnEnable, `Unregister` at OnDisable.

## Contracts (all in `HarvestPanic.Core`)

| Contract | Type(s) | Provider -> consumers |
|---|---|---|
| Carry | `ICarryable` | carryable prefabs -> Player carry |
| Noise | `NoiseEvent`, `NoiseSource`, `NoiseTag`, `INoiseService` | Player (voice/impact) -> AI, World |
| Critter Personality | `CritterPersonality` SO (+enums) | AI team authors; all spawners read |
| Goods | `GoodsDefinition` SO, `IGoods`, `BruiseReport`, `TouchHistory` | Economy -> Player, AI |
| Blame | `BlameEvent`, `BlameVerb` (23, add-only) | any host code -> `IEconomyService.EmitBlame` |
| Day Modifier | `DayModifier` SO, `DayParams`, `IDayModifierService` | Economy -> AI, noise, World |
| Economy | `IEconomyService`, `WalletTxn` | Economy -> everyone |
| Player hooks | `IPlayerHooks` | Player prefab -> Customization |
| Session | `ISessionService`, `PlayerInfo` | Networking -> Frontend UI (v0, add-only) |
| Markers | `MarkerRole`, `MarkerTag`, `LevelMarker` | World places -> owners spawn |
| Layers | `GameLayers` | all |
| Data lookup | `Content.Goods/Critters/DayModifiers` (`ContentCatalog<T>`), `PlayerIds` | all |

Conventions: player id = NGO `ulong` client id (`PlayerIds.None`, `PlayerIds.Nature`); units are metres/seconds/newton-seconds;
all state-changing calls are server-only; contracts are add-only (changing one needs lead review).

### Markers
World places empties with a `LevelMarker` (role, key, count, index); the component also sets the Unity tag from `MarkerTag`.
Spawn owners: PlayerStart -> Networking; CritterSpawner -> AI (key = critter id, count); Goods -> Economy (key = goods id);
BarnRespawn -> Player; MarketPen/River/RiverPile/BridgePlank -> World + Economy/Player read via `LevelMarker.FindAll`; Wardrobe -> Customization.
Legacy `HP_CritterSpawner_Pig:3` strings parse via `MarkerTag.TryParseSpawner`.

### Data folders (ScriptableObjects found by id)
`Assets/Resources/Data/Goods` (Economy), `Data/Critters` (AI), `Data/DayModifiers` (Economy).

## Networking prefab lists
Each team owns `Assets/Resources/NetworkPrefabs/<Team>.asset` (a `NetworkPrefabsList`) and lists only its own networked prefabs
(Player.asset, Critter.asset, Economy.asset, World.asset, Customization.asset). The Networking team's NetworkManager bootstrap loads every list via
`Resources.LoadAll<NetworkPrefabsList>("NetworkPrefabs")` and registers them at startup, so nobody edits `DefaultNetworkPrefabs.asset`.

## Ownership

| Team | Owns (folders / scenes / prefabs / data) |
|---|---|
| Networking | `Scripts/Networking`, `Scripts/Editor`, `Packages/manifest.json`, `ProjectSettings/**`, `Scenes/Bootstrap`, `Prefabs/Networking`, NetworkManager, `Resources/NetworkPrefabs` loader |
| Player | `Scripts/Gameplay`, `Prefabs/Player`, `Prefabs/Tools`, input actions, `Resources/NetworkPrefabs/Player.asset` |
| Critter AI | `Scripts/AI`, `Prefabs/Critters`, `Resources/Data/Critters`, `NetworkPrefabs/Critter.asset` |
| Economy+HUD | `Scripts/Economy`, `Prefabs/Goods`, `Prefabs/HUD`, `Resources/Data/Goods`, `Data/DayModifiers`, `NetworkPrefabs/Economy.asset` |
| World | `Scripts/World`, `Scenes/Muddlebury*`, `Prefabs/World`, `Art/World`, `NetworkPrefabs/World.asset` |
| Customization | `Scripts/Customization`, `Prefabs/Farmer`, `Art/Cosmetics`, `NetworkPrefabs/Customization.asset` |
| Frontend UI | `Scripts/UI`, `Scenes/MainMenu`, `Scenes/Lobby`, `Prefabs/UI`, UI Toolkit assets |
| Lead (this doc) | `Scripts/Core/**`, `docs/architecture/**` (changes via request) |

Only Networking edits `Packages/manifest.json` and `ProjectSettings/**`; others send a request. Avoid two teams editing one scene: split into prefabs.

## Layers and tags (TagManager, Networking-owned)
Layers: 3 Player, 4 Water, 6 Critter, 7 Carryable, 8 Bridge, 9 Interactable, 10 Ragdoll (Default/UI built in).
Tags: HP_PlayerStart, HP_MarketPen, HP_CritterSpawner, HP_Goods, HP_BridgePlank, HP_River, HP_RiverPile, HP_BarnRespawn, HP_Wardrobe.

Collision matrix: everything collides by default except Water, Interactable (triggers; they only see Player, Critter, Carryable),
Ragdoll-Ragdoll, Ragdoll-Player (capsule disabled while ragdolled), Bridge-Bridge, Interactable-Interactable.
Verified by `ContractsTests.Layers_AreDefinedAndCollisionMatrixIsSensible`. Note: Unity stores each matrix row as little-endian bytes.
