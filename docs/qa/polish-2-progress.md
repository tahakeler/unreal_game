# Polish 2 (feature/polish-2) progress

## Done (code)
- **2 Client-side cues**: Core add-only `NoiseCueFeed` (static `OnNoiseCue`, `Raise`) fires on every peer. Host: `NoiseService` raises it next to `OnNoise`. Clients: `AudioNetRelay.BroadcastNoise(NoiseEvent)` (new RPCs, `SendTo.SpecifiedInParams`, footsteps unreliable) sends the full noise to remote clients, who raise the feed and route/play locally (`AudioService.HandleRemoteNoise`). Unrouted noises (whistle, food...) now caption on clients too. `OnboardingOverlay` (captions, tips) subscribes to `NoiseCueFeed` instead of server-only `INoiseService.OnNoise`. Footsteps are not sent to their owner; `PlayerCharacter.OwnerTickFootsteps` plays them locally (predicted) on remote-client owners (host still hears the server-emitted one). Legacy `Broadcast(SoundCue)` kept, unused.
- **3 Stitches body drag**: Core add-only `IDraggable.RequestDrag(Vector3 toward, float seconds)` (in `IKnockable.cs`). `PlayerCharacter` implements it: server -> `DragOwnerRpc` -> owner's FixedUpdate pulls the ragdoll horizontally at `PlayerTuning.DragSpeed` (2.5 m/s, accel 12) until the time ends, it's within 1 m, it gets carried, or physics mode ends. `MrStitches.TryGrab` calls it after the KO yank toward the nearest field post for `NightTuning.StitchesFarmerDragSeconds` (3.5 s).

- **1 Night look**: `WeatherLook` night preset is moonlit (sun 0.16 cool #8FAEE6, sky exposure 0.28, ambient #24324A, fog #0B1420 14-150 m, sat -28, post-exposure -0.75); `NightBlend` smoothstep reaches full night at 55% of Dusk (was capped at 60% night). Hand lantern 4.5 intensity / 14 m. Stitches reads as a silhouette against the sky.
- **4 Landlord stamp**: world-space PAID/EVICTED stamp moved from 4.4 m above his head (under the HUD wallet card) to beside him at 1.7 m, tilted 14 deg, smaller.
- **5 Goat**: `GoatSilhouetteBuilder.Build` (AI/Editor, idempotent) adds colliderless primitive swept horns (2 segments each), a chin beard and dark shin blocks on the leg bones of `Critter_Goat.prefab`; mats `M_Goat_Horn/Beard/Leg`.
- **6 Audio**: Core add-only `BruiseFeed` (server; Goods raises it after `OnBruised`) -> `AudioService.HandleBruise`: thud (<8 bp) / crunch + register clunk when dollars lost, relayed to clients. `STG_Landlord_Tuba` on entering Sunday; `MUS_Night_Threat` loop layer fades in over 4 s during Dusk (night). Both appended to `AudioEvents.All`; placeholders appended to `make_audio_placeholders.py` (existing outputs byte-identical). Bull telegraph: no bull exists yet (event kept, unused).
- **7 Shaders**: `Resources/ShaderKeepAlive/*.mat` (one per Shader.Find name; builder `HarvestPanic.Weather.Editor.ShaderKeepAliveBuilder.Build`) instead of editing ProjectSettings; EditMode test scans all runtime Shader.Find calls.

## Tests
EditMode 228/228 passed. PlayMode 65: 53 passed, 0 failed, 12 skipped (QA screenshot tests under -nographics).
New: `Polish2AudioTests` (4), `ShaderKeepAliveTests`, `WeatherEditModeTests.Night_IsMoonlitDark_AndReachedEarlyInDusk`, `GoatScreenshots` (prefab parts check + capture).

## Screenshots (docs/qa/polish-2/)
night_before/after.png, goat_before/after.png, landlord_stamp_before/after.png.

## Gaps
- No networked PlayMode test yet for client-side NoiseCueFeed delivery / footstep owner exclusion or for IDraggable (owner drag); logic is small but only compile + host-path verified.
- Goat beard reads slightly off-centre at 3/4 view (sheep head geometry); horns/shins carry the silhouette.
- Night threat layer is phase-driven only (Audio cannot see NightDirector threat level).
- Keep-alive materials keep base shader variants; keyword-heavy variants set at runtime might still need a ShaderVariantCollection; verify in a real player build.
