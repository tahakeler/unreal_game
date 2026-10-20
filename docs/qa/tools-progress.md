# Tools, wagon, lasso, whistle — progress (feature/wagon-lasso-whistle)

Owner: Player team (gameplay-programmer). Scope: `Scripts/Gameplay/Tools`, new `Scripts/Gameplay/Vehicles`, tool/wagon prefabs.
Specs: `design/gdd/farm-tools.md`, `design/gdd/wagon-vehicles.md`, `design/gdd/physics-carry.md`, GDD ch.5 and 7.

## Plan (session 1)
- Lasso tool (aim, server-simulated throw, 6 m tether, reel, auto-grab at 1.5 m, tie to post/wagon hitch, tug-of-war, LassoedIntoRiver).
- Whistle tool (short = scare `NoiseTag.Whistle` 0.7; long 1 s = `NoiseTag.WhistleLong`; 3 s cooldown; carried critters wobble harder).
- Wagon (Vehicles): self-simulated `ICarryable` push/pull handle, cargo slots + soft cap, tipping/righting, blame, bridge load, BruiseMult.
- Toolbelt: slots Hands / Net / Bucket / Lasso / Whistle, keys 1-5 + scroll, 0.4 s equip, arm poses.

## Status
- [ ] Lasso
- [ ] Whistle
- [ ] Wagon
- [ ] Toolbelt + arm poses
- [ ] EditMode tests
- [ ] PlayMode tests
- [ ] Screenshots

## Session 1 findings (code survey, no code yet)
- Real game scene (Muddlebury) spawns no tools at all; nets/buckets only exist in `Scenes/Dev/PlayerTest`. Decision: a per-player
  `PlayerToolbelt` spawns a personal Net, FeedBucket, Lasso and Whistle (holstered) on the server when the player spawns.
- `CritterBrain.HearNoise` treats `NoiseReaction.Rally` as Ignore and picks the FIRST matching tag response; so the long blow
  emits only `NoiseTag.WhistleLong` (not Whistle|WhistleLong) or it would scare. "Toward the whistler" works today through a
  `Lure` response on WhistleLong; true Rally-follow is AI-owned (sheep branch) and not edited here to avoid conflicts.
- Pig asset ignores `Whistle` (chicken and goat flee). Whistle PlayMode test therefore uses chickens.
- Bridge planks (`BridgePlank.SampleLoad`) count `ICarryable.MassKg` else non-kinematic `rb.mass` per body centre, so the wagon
  (rb 120 kg, not double counted) plus each cargo body (ICarryable) load planks with no World change.
- Wagon pushing reuses PlayerCarry grips via `ISelfSimulatedCarryable` (CarryBody tracks grips, the wagon applies its own 900 N/player).
