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
| Carry | `ICarryable`, optional `ICarryableEvents` (`OnForcedRelease(ulong[])`, `OnStruggle(Vector3)`) | carryable prefabs -> Player carry; carry subscribes only if `carryable is ICarryableEvents` |
| Noise | `NoiseEvent`, `NoiseSource`, `NoiseTag`, `INoiseService` | Player (voice/impact) -> AI, World |
| Critter Personality | `CritterPersonality` SO (+enums) | AI team authors; all spawners read |
| Goods | `GoodsDefinition` SO, `IGoods`, `BruiseReport`, `TouchHistory` | Economy -> Player, AI |
| Blame | `BlameEvent`, `BlameVerb` (23, add-only) | any host code -> `IEconomyService.EmitBlame` |
| Day Modifier | `DayModifier` SO, `DayParams`, `IDayModifierService` | Economy -> AI, noise, World |
| Economy | `IEconomyService`, `WalletTxn` | Economy -> everyone |
| Player hooks | `IPlayerHooks` | Player prefab -> Customization |
| Knock/shove | `IKnockable`, `Knockables.TryGetFor` | Player root -> AI, hazards, wagons |
| Movement zones | `IMovementModifierSource`, `MovementModifiers.Register/Unregister/Evaluate(pos)` | World zones (mud) -> Player controller multiplies speed by `Evaluate` |
| Soak | `BruiseKind`, optional `IGoodsSoakable.ApplySoak(instigator)` | Economy implements; river/rain callers check `goods is IGoodsSoakable` |
| Input gating | `InputModal.Push/Pop(owner)`, `IsBlocking`, `Count`, `Changed` | UI (pause, crew overlay) and Customization (wardrobe) push; Player skips look/move/interact while blocking |
| Social (optional) | `ISessionSocial` (`Kick`, `SendChat`, `OnChat`) | Networking may implement; UI checks `is`/`TryGet` |
| Audio | `IAudioService` (`PlayAt`, `Play2D`, `ReportVoiceActivity`), `IAudioSettingsSink` (`Get/SetBusVolume`), `AudioBus` | Audio -> any team (play), Frontend UI (volumes) |
| Day phase (read-only) | `IDayPhaseInfo` (`PhaseName`, `PhaseRemaining`) | Economy (`EconomyDayPhaseInfo` adapter) -> Audio |
| Weather | `IWeatherService` (`Current`, `ForecastText`, `Wind`, `OnWeatherChanged`), `WeatherKind` (byte, add-only) | World/Weather `WeatherDirector` -> HUD, Audio, AI (presentation only; gameplay via `Mod.Weather.Day.*`) |
| Day modifier write | `IDayModifierWriter.ApplyModifier(mod, bypassCap)` (server) | Economy director -> Weather (and future landlord/curse sources) |
| Day clock | `IDayClockService` (`Phase` as `DayClockPhase`, `PhaseProgress01`) | Economy director -> lighting, Audio |
| Impact units | `ImpactUnits` | all (see below) |
| Run save | `IRunSaveService` (`HasSave`, `Summary`, `ContinueRequested`, `DeleteSave`) | Economy (registered at startup) -> Frontend main menu "Continue Farm" |
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
(Player.asset, Critter.asset, Economy.asset, World.asset, Customization.asset, Audio.asset). The Networking team's NetworkManager bootstrap loads every list via
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
| Audio | `Scripts/Audio`, `Assets/Audio/**`, `Prefabs/Audio`, `NetworkPrefabs/Audio.asset`, `tools/freeassets/scripts/make_audio_placeholders.py` |
| Lead (this doc) | `Scripts/Core/**`, `docs/architecture/**` (changes via request) |

Only Networking edits `Packages/manifest.json` and `ProjectSettings/**`; others send a request. Avoid two teams editing one scene: split into prefabs.

## Layers and tags (TagManager, Networking-owned)
Layers: 3 Player, 4 Water, 6 Critter, 7 Carryable, 8 Bridge, 9 Interactable, 10 Ragdoll (Default/UI built in).
Tags: HP_PlayerStart, HP_MarketPen, HP_CritterSpawner, HP_Goods, HP_BridgePlank, HP_River, HP_RiverPile, HP_BarnRespawn, HP_Wardrobe.

Collision matrix: everything collides by default except Water, Interactable (triggers; they only see Player, Critter, Carryable),
Ragdoll-Ragdoll, Ragdoll-Player (capsule disabled while ragdolled), Bridge-Bridge, Interactable-Interactable.
Verified by `ContractsTests.Layers_AreDefinedAndCollisionMatrixIsSensible`. Note: Unity stores each matrix row as little-endian bytes.

## Impact units
All GDD impulse thresholds (bruise 400, ragdoll 900, KO 1800, plank 800, critter charge) are design units =
Unity collision impulse (N*s) x `ImpactUnits.ImpulseScale` (10). Always convert with
`ImpactUnits.ToDesign(collision.impulse.magnitude)` before comparing; `IKnockable.ApplyImpulse` takes design units.

New `DayParams`: `Bridge.SafeLoadMult`, `Bridge.PlankBreakMult`, `Mud.SpeedMult`, `Player.StaminaMult` (Player motor), `Tools.NetReachMult` (NetTool).
Farm upgrades: `Resources/Data/Upgrades` (`UpgradeDefinition`, Economy) each referencing a Run-duration `Upgrade` modifier in `Data/DayModifiers/Mod_Upgrade_*`.

## Integration decisions
- `IGoods.ApplyBruise(impulse, ...)` takes DESIGN units. Callers convert raw Unity impulse with `ImpactUnits.ToDesign`. River soak (600) is already design units.
- `IPlayerHooks.PlayEmote`: the implementer (Customization) emits the emote noise; Player does not.
- Critters are sold via AI's `CritterGoods` (the single `IGoods` on critter prefabs); MarketPen accepts any `IGoods`.

## Audio
`HarvestPanic.Audio` (refs Core + Netcode) provides `AudioService`, auto-created after the first scene loads and
registered as `IAudioService` and `IAudioSettingsSink`. Everything is client-local presentation; nothing here changes gameplay.
- **Events**: folder name under `Assets/Audio/Resources/Audio/<EVENT>/` = event name (`SFX_*`, `STG_*`, `UI_*`, `MUS_*`, `AMB_*`);
  files are random variants. The event table `AudioEvents.All` is append-only (index = relay wire id). Unknown names are ignored.
- **Buses**: Master/Music/Sfx/UI/Voice are software gains (Master x bus x duck) on pooled AudioSources; no AudioMixer asset.
  Frontend `UiRuntime` pushes `GameSettings` volumes into `IAudioSettingsSink` (UI bus follows the SFX slider) and leaves
  `AudioListener.volume` at 1 while the sink exists.
- **Noise**: Audio subscribes to `INoiseService.OnNoise` (server only), routes it (`NoiseRouting`), plays locally and forwards
  the cue to clients through `AudioNetRelay` (`Rpc(SendTo.NotServer)`, so the host never plays twice). The relay prefab is in
  `Resources/NetworkPrefabs/Audio.asset`; `AudioNetBootstrapper` spawns it once the server is listening.
- **Economy**: `OnWalletTxn` / `OnDisaster` are already raised on clients by Economy's RPCs, so Audio plays them locally (no relay).
- **Day phase**: `IDayPhaseInfo` is registered by Economy's add-only `EconomyDayPhaseInfo` (reads `EconomyDirector.Instance`).
- **Voice ducking**: proximity voice (when it exists) calls `IAudioService.ReportVoiceActivity`; until then Shout/Voice noises
  within 15 m of the listener duck for 0.6 s.
## Weather (feature/weather)
- The WeatherDirector (prefab in `Resources/NetworkPrefabs/Weather.asset`, auto-spawned by `WeatherBootstrapper` on the server when a MarketPen/River marker exists) is the single source of truth for weather. It rolls `WeatherDeck.Roll(seed, dayIndex)` at Morning (seed replicated; `-hpWeatherSeed`, `-hpWeather <Kind>` for dev) and applies `Mod.Weather.Day.<Kind>` (ReplaceGroup `Group.Weather`, bypasses the chaos cap so sky and rules never disagree).
- Economy's old `DrawWeather` returns early whenever an `IWeatherService` is registered (lead-approved minimal edit). The legacy `Mod.Weather.Fog/Storm/Heatwave` assets are no longer drawn while weather runs.
- New DayParams: `River.CurrentMult` (RiverVolume sweep speed), `Weather.GroundSpeedMult` (global movement source), `Weather.SoakSeconds` (outdoor exposure before `IGoodsSoakable.ApplySoak(PlayerIds.Nature)`), `Weather.WindSpeed`, `Weather.FogDistance`.
- Rebuild data: `-executeMethod HarvestPanic.Weather.Editor.WeatherAssetBuilder.Build`.
