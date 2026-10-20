# Polish 2 (feature/polish-2) progress

## Done (code)
- **2 Client-side cues**: Core add-only `NoiseCueFeed` (static `OnNoiseCue`, `Raise`) fires on every peer. Host: `NoiseService` raises it next to `OnNoise`. Clients: `AudioNetRelay.BroadcastNoise(NoiseEvent)` (new RPCs, `SendTo.SpecifiedInParams`, footsteps unreliable) sends the full noise to remote clients, who raise the feed and route/play locally (`AudioService.HandleRemoteNoise`). Unrouted noises (whistle, food...) now caption on clients too. `OnboardingOverlay` (captions, tips) subscribes to `NoiseCueFeed` instead of server-only `INoiseService.OnNoise`. Footsteps are not sent to their owner; `PlayerCharacter.OwnerTickFootsteps` plays them locally (predicted) on remote-client owners (host still hears the server-emitted one). Legacy `Broadcast(SoundCue)` kept, unused.
- **3 Stitches body drag**: Core add-only `IDraggable.RequestDrag(Vector3 toward, float seconds)` (in `IKnockable.cs`). `PlayerCharacter` implements it: server -> `DragOwnerRpc` -> owner's FixedUpdate pulls the ragdoll horizontally at `PlayerTuning.DragSpeed` (2.5 m/s, accel 12) until the time ends, it's within 1 m, it gets carried, or physics mode ends. `MrStitches.TryGrab` calls it after the KO yank toward the nearest field post for `NightTuning.StitchesFarmerDragSeconds` (3.5 s).

## Todo
1 night look, 4 landlord stamp, 5 goat silhouette, 6 audio wiring, 7 always-included shaders, tests, screenshots.
