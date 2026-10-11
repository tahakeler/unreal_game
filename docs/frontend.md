# Frontend & Lobby (feature/frontend-lobby)

Code: `HarvestPanic/Source/HarvestPanic/Frontend/` · Maps: `/Game/HarvestPanic/Frontend/Lvl_MainMenu`, `Lvl_Lobby` · Scripts: `HarvestPanic/Scripts/Frontend/`

## Flow
Title (Play / Customize / Settings / Quit) → Play (Host a farm (Steam) · Host a LAN farm · Join) → Browser (LAN/Online, Refresh, join by address) → Connecting → **Lobby Yard** (walkable). There's a Tab crew overlay. You ready up by standing on the porch for 1 s or pressing R. The host presses B (or the overlay button) to ring the dinner bell, which starts a 3 s countdown. If not everyone is ready, a "Start anyway? (x/y)" prompt appears and the host has to ring again within 3 s. A player who leaves the porch cancels the countdown. When the countdown ends the host runs `ServerTravel` to `Lvl_Muddlebury?listen`, or to `/Game/ThirdPerson/Lvl_ThirdPerson?listen` if that map is missing.
Pause (Esc / Start) is an overlay only and **never** calls SetPause.

## Classes
| Class | Role |
|---|---|
| `UHPGameInstance` | All `IOnlineSession` use: host/find/join/invite. Falls back to the NULL subsystem (LAN) when Steam is down. Handles network/travel failures by returning to the menu with an error popup. |
| `UHPGameUserSettings` | Sensitivity, invert Y, FOV 80–110, master/music/sfx/voice, PTT, window mode, res scale 50–100, UI scale 0.8–1.5. Every setter clamps. Values are saved to GameUserSettings.ini. Other systems listen on `UHPGameUserSettings::OnHPSettingsApplied`. |
| `UHPMenuStateMachine` | Stack-based screen flow with a legal-transition table (unit tested). |
| `UHPFrontendUISubsystem` (LocalPlayer) | Maps menu states to panels and sets the input mode. Gameplay hook: `UHPFrontendUISubsystem::Get(PC)->ShowRoot(EHPMenuState::InGame)` on BeginPlay, and `->HandleBackKey()` on Esc/Start. |
| `AHPMenuGameMode` / `AHPMenuPlayerController` / `AHPMenuCameraPawn` | Menu map with a slow camera sway. |
| `AHPLobbyGameMode` / `GameState` / `PlayerState` / `PlayerController` | Roster, ready flags, chat, kick (host only), dinner bell, countdown, travel. Max 4 players (`PreLogin`). |
| `AHPPorchReadyZone`, `AHPDinnerBell` | Diegetic ready zone and bell. |
| `UI/*` | Pure C++ UMG panels (no .uasset). `UHPMenuPanel` is the base and gives a 0.15 s fade and visible focus, structured so it can be ported to `UCommonActivatableWidget` later. |

Customize uses `StaticLoadClass(UUserWidget, "/Script/HarvestPanic.HPCustomizationWidget")`. If that class isn't merged yet, the panel shows "Coming soon".

## Integration changes required (lead applies)
`Config/DefaultEngine.ini`:
```ini
[/Script/EngineSettings.GameMapsSettings]
GameDefaultMap=/Game/HarvestPanic/Frontend/Lvl_MainMenu.Lvl_MainMenu
GameInstanceClass=/Script/HarvestPanic.HPGameInstance

[/Script/Engine.Engine]
GameUserSettingsClassName=/Script/HarvestPanic.HPGameUserSettings
```
Optional: `EditorStartupMap` stays as it is. OnlineSubsystemNull is enabled by default in 5.8, so the LAN fallback needs nothing more. To keep Steam primary, leave `DefaultPlatformService=Steam`; the GI switches to NULL automatically when Steam can't start, and `-nosteam` or `-HPForceLAN` forces LAN.
Build.cs: **no change required.** The code avoids SlateCore link symbols by editing styles in place. Adding `"SlateCore"` to PrivateDependencyModuleNames is still recommended, so styling can use normal `FSlateBrush`/`FButtonStyle` values later.
Day map PlayerController (gameplay owner): on BeginPlay, call `ShowRoot(EHPMenuState::InGame)`; bind Esc/Gamepad Start to `HandleBackKey()`.

## GDD deltas (for design to reconcile)
- The Title screen follows the task (Play/Customize/Settings/Quit), not the hud-menus list (Continue/Eviction/Almanac/Trunk).
- Not built yet: Privacy options, Code join, Friends tab, browser filters/columns, Friend Scale, mirror, practice pig, rejoin reservation, solo real pause, FOV floor of 70 (implemented as 80–110 per the task).
- Chat is sent with a "Say" button, not Enter. Enter-to-send would need SlateCore for the `ETextCommit` delegate.

## QA
- Tests: `Automation RunTests HarvestPanic.Frontend` (4 tests).
- LAN: `Scripts/Frontend/lan_test.sh [find|direct]` runs two `-nullrhi` processes with automatic host, find, join, ready, chat, bell and travel.
- Screenshots: `Scripts/Frontend/shot.sh <map> <png>` uses `-RenderOffscreen` only.
- Maps: `Scripts/Frontend/build_frontend_maps.py`.
