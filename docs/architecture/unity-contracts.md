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
| Voice chat | `IVoiceChat` (`Mode`, `OpenMicThreshold`, `MicMuted`, `IsTransmitting`, `LocalLoudness`, `BackendName`, `IsSpeaking`, `IsMuted`, `SetMuted`, `OnSpeakingChanged`), `VoiceTransmitMode` | Voice (`HarvestPanic.Voice`) -> Frontend UI (settings, crew list, pause menu) |
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
| Networking | `Scripts/Networking`, `Scripts/Voice`, `Scripts/Editor`, `Packages/manifest.json`, `ProjectSettings/**`, `Scenes/Bootstrap`, `Prefabs/Networking`, NetworkManager, `Resources/NetworkPrefabs` loader |
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
## Night phase (feature/night-phase)
- New AI-team asmdef `HarvestPanic.AI.Night` (`Scripts/AI/Night`, refs Core + Netcode only). `NightBootstrapper` spawns the `NightDirector` (prefab in `Resources/NetworkPrefabs/Night.asset`, with `MrStitches` and `NightFox`) on the server once a MarketPen marker exists. Rebuild: `-executeMethod HarvestPanic.AI.Night.Editor.NightAssetBuilder.Build`.
- Night = `IDayClockService.Phase == Dusk` (darkness comes from WeatherPresenter's existing Dusk->night blend; `NightPresenter` adds a client-local hand-lantern glow and lantern-post lights). Replicated: `IsNight`, `Budget`, `Spent`, `LanternPosts`; creature transforms via server NetworkTransform.
- Threat budget plateau (GDD 8.9 flag) decision: **B is a per-night pool that also pays re-spawns** (slot re-bought `RespawnCooldown` s after a creature is repelled/finishes) **and caps grow by week** (foxes 2 +1/week up to 5; a second Mr. Stitches from week 4). `NightMath.DeliverableThreat` strictly increases every day of a 5-week run (EditMode-tested).
- Weeping-angel rule: server checks every connected player's `HeadPivot` child (pitch replicated by Player) as a frustum (camera FOV, 16:9) within 40 m (15 m when `Weather.FogDistance` > 0) with `Physics.Linecast` LOS, >= 15% of 9 bounds samples; downed and AFK (60 s) players don't count. Tests/bots add `INightViewerSource`s via `NightObservers.Register`.
- Creature KO: `IKnockable.ApplyImpulse(yank toward the field, PlayerIds.Nature, BlameVerb.KnockedOut)` (2400 design units = KO). Stitches/foxes move carryables by `ICarryable.OnGrabbed/OnReleased(PlayerIds.Nature)` + kinematic follow.
- Core add-only: `DayParams.NightLanternPosts`, `DayParams.NightThreatMult`. Economy minimal hook: the Lantern Posts upgrade modifier also adds `Night.LanternPosts +1` (asset + `UpgradeAssetBuilder`); each lit post is a 6 m safe circle (barn 10 m, market 14 m, bridge planks 3.5 m are always safe).
## Proximity voice (feature/voice-chat)
- New Networking-owned asmdef `HarvestPanic.Voice` (`Scripts/Voice`, refs Core + Netcode + Collections + Input System + Facepunch transport; no team assembly). Decision record: `docs/architecture/adr-voice-chat.md`.
- `VoiceChatService` auto-creates after the first scene loads (skipped in the test runner's InitTestScene and with `-hpVoiceOff`), follows `NetworkManager.Singleton` and registers `IVoiceChat`. Messages: `HP_VoiceUp` (client->host, unreliable), `HP_VoiceDown` (host->clients, unreliable), `HP_VoiceMute` (client->host, reliable), versioned byte layouts in `VoicePacket`.
- Steam voice on the Steam transport, else Unity Microphone -> IMA ADPCM 3-bit 8 kHz (26.2 kbps up / 28.0 kbps down per talker). The real microphone is never opened in batchmode unless `-hpVoiceMic`; `-hpVoiceSynth` sends a test tone.
- Range 25 m x `DayParams.NoiseVoiceRadiusMult` (Megaphone upgrade x2), resolved by the host. Heads: player object child `HeadPivot`. The host emits `NoiseSource.Voice` events (loudness >= 0.35, 1 per 0.5 s per player) through `INoiseService`.
- Ducking: the primary service calls `IAudioService.ReportVoiceActivity` while a speaker within 15 m (or the local talker) is speaking.
- Minimal Frontend edits (scoped by the feature brief): `GameSettings.OpenMicThreshold` + `PushToTalk` default true, "Open mic sensitivity" slider, `UiRuntime` applies both to `IVoiceChat`, Lobby crew rows get Mute + TALKING, pause menu gets per-player Mute buttons. Speaking marker is parented at runtime under the farmer's `NameTag` (no Customization edit).
- ProjectSettings: `microphoneUsageDescription` set (macOS permission prompt).

## Scene switches and late joiners (fix/join-during-load)
- NGO 2.x synchronises a client the moment it is approved, even mid scene load, which made a joiner load the Lobby twice
  ("Server Scene Handle (...) already exist!") and never spawn. `SessionService` now orders joins against NGO scene
  switches with `SceneTransitionGate` (Networking, no contract change): a join arriving while the host is loading a scene
  is held (approval `Pending`) until the host's own `LoadComplete`, and `StartGame` / the initial Lobby load wait for
  joiners that are still synchronising. Limits live in `NetworkingConfig` (`approvalTimeoutSeconds` = NGO
  `ClientConnectionBufferTimeout`, `joinHoldMaxSeconds`, after which the join is refused with
  `ConnectionApproval.BusyReason`, and `switchWaitForJoinerSeconds`).
- Held joins are detected from NGO `OnSceneEvent`, so they also cover loads started outside `SessionService`
  (`EconomyDirector.ReturnToLobby` calls `NetworkManager.SceneManager.LoadScene` directly). Only switches started through
  `SessionService` wait for synchronising joiners. If another team needs a host scene switch, ask Networking for an
  add-only `ISessionService` method rather than calling NGO directly.
- Test hooks: `-netHostLoadDelay S` (dev builds, NetAutomation host) holds every host NGO load open for S seconds;
  `NET_LOAD_DELAY=S tools/net/two-process-test.sh` runs the LAN test under that stress.
