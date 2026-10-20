# Audio progress (feature/audio)

## Done
- Sound-designer drop committed (IAudioService contract, AudioMath, Kenney CC0 + placeholder clips, generator).
- Generator extended: pig squeal, chicken panic, bull telegraph, player shout, AMB_Farm_Day loop. Licences in THIRD_PARTY_LICENSES.md.
- Logic: AudioEvents table (bus, attenuation preset, voice cap), NoiseRouting, DuckingState, DisasterStingerRules, WalletCues, MusicRules, VoiceStealing.
- Runtime: AudioService (40-voice pool, software buses, caps 8/12/1, ducking, PlayerPrefs volumes), MusicDirector (phase loops, crunch + pocket watch, lead stem at layer>=2, disaster cut, ambience, sundown bell), AudioNetRelay + AudioNetBootstrapper, AudioAssetBuilder (relay prefab, NetworkPrefabs/Audio.asset, import settings).
- Economy: add-only EconomyDayPhaseInfo (IDayPhaseInfo). UI: UiRuntime pushes volumes to IAudioSettingsSink.
- Tests: EditMode Audio (math, table, routing, ducking, disaster, wallet, music, stealing); PlayMode Audio (noise->SFX bus, wallet->UI bus + ticks, footstep cap, bus volume + shout duck, disaster cut + crunch).

## Next
- Bar-quantized music transitions (currently 1.5 s crossfades).
- Real FleeingCritters / ActiveMajorModifiers inputs for ChaosLevel (proxy: recent squeals + disasters).
- Critter state barks (alert/flee/charge) need an AI hook; only grab squeal exists as a noise.
- Bruise feedback (OnBruised squeak + crunch/register layers) not wired.
- Host+client PlayMode test for AudioNetRelay.
