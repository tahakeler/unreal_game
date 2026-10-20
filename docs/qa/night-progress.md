# Night Phase progress (feature/night-phase)

## Done
- `HarvestPanic.AI.Night` (Scripts/AI/Night): NightMath, NightObservers, NightDirector, MrStitches, NightFox, NightVisuals (primitive scarecrow with sack head / fox), NightPresenter (hand lantern + lantern posts using Kenney lantern.fbx), NightBootstrapper, Editor/NightAssetBuilder.
- Prefabs/Night (NightDirector, MrStitches, NightFox) + Resources/NetworkPrefabs/Night.asset.
- Budget plateau fix: pool + weekly caps (see docs/architecture/unity-contracts.md "Night phase").
- Core add-only DayParams Night.LanternPosts / Night.ThreatMult; Lantern Posts upgrade feeds Night.LanternPosts.
- Tests: EditMode Night 10/10; PlayMode Night 4/4 (+1 QA screenshot). Screenshot: docs/qa/screens/night_stitches.png.

## Next / gaps
- Dragging a KO'd farmer's body: player physics is owner-authoritative; Stitches yanks (KO impulse toward the field) and drags goods only. Needs a Player-team hook (e.g. `IKnockable` drag API).
- Handheld Lantern freeze (3 s) needs a Player tool; currently only Lantern Posts + safe zones.
- Dusk vote (Economy), stranded charging, Barn Thing, crows, blame `SummonedCreature` not built.
- Night darkness is WeatherPresenter's Dusk blend (moonlit, fairly bright); World could deepen it.
- Creature visuals use Shader.Find("Universal Render Pipeline/Lit") at runtime: ensure the shader is included in player builds.
