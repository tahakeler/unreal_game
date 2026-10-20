# Onboarding + Accessibility + Menu polish (feature/onboarding)

## Built
- **Grandpa's tapes** (`Scripts/UI/Onboarding/`): `TipRules` (pure: once per profile, armed by signals, 12 s spacing,
  10 s disaster quiet, never while `InputModal` blocks, prerequisites/timers), `PlayerPrefsTipStore`
  (`HarvestPanic.TipsSeen.v1`), `OnboardingOverlay` (VHS card, 8 s, Backspace / gamepad View skips).
  Six first-day tapes: move (IDayClockService Morning/Workday), grab (20 s after move), bridge / sell (near
  BridgePlank / MarketPen `LevelMarker`, 14 m), shout (critter noise within 25 m via INoiseService), shop
  (`IDayPhaseInfo.PhaseName` contains "Shop"). Learned-already: a Sale txn retires "sell", a local Shout retires "shout".
  Key labels resolve from current bindings. Toggle + "Replay all tapes" in Settings.
- **Captions** (`Scripts/UI/Access/CaptionFeed.cs`): INoiseService -> caption lines with direction (< > ^ v), max 3,
  priority replacement, danger lines use the colour-blind palette, background opacity setting.
- **Settings**: new `Subtitles`, `CaptionBackground`, `TipsEnabled`, `CameraBob`, `CameraShake` (old JSON loads with defaults).
  New Controls tab with interactive rebinding (Input System `PerformInteractiveRebinding`, KB/M and gamepad columns,
  conflict warning, reset). Gameplay tab: head bob, camera shake, tapes.
- **Bindings** (`Scripts/UI/Settings/InputBindings.cs`): overrides keyed by (action, default path), stored in
  `HarvestPanic.Bindings.v1`, applied to every enabled "Player" map (InputSystem.onActionChange), so the Player
  assembly is untouched. Look sensitivity + invert-Y via `ScaleVector2` processor overrides. Defaults table is
  checked against `PlayerControls.inputactions` by an EditMode test.
- **Colour-blind palette** `UiPalette` (Okabe-Ito based) + `hp-root--cb-*` classes on every UiPanel.
- **Pause menu**: Resume, Settings, Controls & Help (KB/M + gamepad table), Invite, Leave -> confirm (Stay / Leave).
  Esc/B steps back through layers; Esc is ignored while a rebind is listening.
- **Motion comfort**: new Core contract `CameraComfort` (BaseFov, BobScale, ShakeScale, FovKickScale) written by
  `UiRuntime.ApplyComfort`; reduced motion = no bob, no sprint FOV kick, shake <= 25%.

## Cross-team edits (need lead / Player review)
- `Scripts/Core/Contracts/CameraComfort.cs` (new, add-only).
- `Scripts/Gameplay/Player/PlayerView.cs`: 3 multiplications + base FOV override read from `CameraComfort`
  (FOV slider previously did nothing in game).

## Tests
- EditMode `Tests/EditMode/UI/OnboardingAccessibilityTests.cs` (14): tip order/once/spacing/disaster/modal/learned,
  token resolve, settings persistence + migration, comfort mapping, bindings save/load/apply/conflict/asset parity,
  captions, palette.
- PlayMode `Tests/PlayMode/UI/OnboardingPlayTests.cs`: first tape on day 1 and not on a second run, tapes off,
  pause blocks input/time/tapes + help + leave-confirm, captions, screenshots (HP_QA_DIR).

## Gaps
- Captions come from INoiseService, which fires on the server only: clients get no captions until Audio's
  relay exposes a client-side cue event (request to Audio / lead).
- "Never during carry" gate not applied: no Core carry-state contract for the local player.
- Tape #1 scripted tutorial day, JIT tapes beyond the first six, rookie sprout, hold-to-toggle, aim assist,
  difficulty assists, ping wheel: not built.
- Strings are not yet routed through a localization table (no loc system exists in the project).
- HUD (Economy-owned) does not yet read `UiPalette`; reserved colours there are unchanged.
