# Night Phase progress (feature/night-phase)

## Done
- `HarvestPanic.AI.Night` asmdef (Core + Netcode only): NightMath (budget, week caps, pool ceiling, frustum/LOS unseen rule, loudest cell), NightObservers (player HeadPivot views, AFK/downed excluded, test sources), NightDirector (server, Dusk = night, replicated IsNight/budget/lanterns), MrStitches, NightFox, NightPresenter (hand lantern + lantern posts), NightBootstrapper, editor NightAssetBuilder.
- Prefabs/Night + Resources/NetworkPrefabs/Night.asset.
- Core add-only: `DayParams.NightLanternPosts`, `DayParams.NightThreatMult`. Economy minimal hook: Lantern Posts upgrade adds `Night.LanternPosts +1`.

## Next
- EditMode + PlayMode tests, screenshot, contracts doc.
