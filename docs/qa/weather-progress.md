# Weather progress (feature/weather)

## Done
- Core add-only: `IWeatherService`, `WeatherKind`, `IDayModifierWriter`, `IDayClockService`/`DayClockPhase`, DayParams `River.CurrentMult`, `Weather.GroundSpeedMult/SoakSeconds/WindSpeed/FogDistance`.
- Economy (minimal, lead-approved): director implements writer + clock; old `DrawWeather` skipped when IWeatherService exists.
- `Scripts/World/Weather` (HarvestPanic.Weather): WeatherDeck (seeded roll + deck rules), WeatherDirector (NGO, rolls at Morning, applies Mod.Weather.Day.*, rain soak, wind gusts, movement source), WeatherLook/WeatherPresenter (phase+weather lighting, rain/leaves), WeatherBootstrapper.
- Builder `HarvestPanic.Weather.Editor.WeatherAssetBuilder.Build` -> mods, materials, prefab, NetworkPrefabs/Weather.asset.
- RiverVolume reads River.CurrentMult.

## Next
- Done: tests (EditMode 136/136, PlayMode 27 pass + 8 GPU-only skips), screenshots, contracts doc. Remaining gaps: see report (lightning, washout, critter AlertRadius, roofs-only soak check, player wind push is feedback only).
