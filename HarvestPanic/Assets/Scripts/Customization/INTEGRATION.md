# Customization integration (Player / Networking / World / UI teams)

Owner: Customization. Implements `design/gdd/character-customization.md` (loadout, wardrobe, emotes).
Rebuild all generated assets: `Unity -batchmode -nographics -quit -projectPath HarvestPanic -executeMethod HarvestPanic.Customization.Editor.FarmerAssetBuilder.BuildAll`.

## Player team: hosting the farmer body
1. Nest `Assets/Prefabs/Farmer/FarmerBody.prefab` as a child of your player prefab root (the object with the `NetworkObject`).
   Do **not** add a NetworkObject to FarmerBody; its `FarmerLoadoutSync` / `FarmerEmotes` are NetworkBehaviours that NGO
   picks up from your NetworkObject. Pivot is at the feet, +Z forward, ~1.8 m tall.
2. Your root implements `IPlayerHooks` (ClientId, IsLocal, FirstPersonArms, Faint = your ragdoll). Forward the visual members
   to the body, which also implements `IPlayerHooks` (no assembly reference needed):
   ```csharp
   IPlayerHooks _body; // = transform.Find("FarmerBody").GetComponent<IPlayerHooks>();
   public Renderer BodyRenderer => _body.BodyRenderer;
   public Animator BodyAnimator => _body.BodyAnimator;
   public Transform HeadAttachPoint => _body.HeadAttachPoint;
   public void PlayEmote(string id) => _body.PlayEmote(id);   // visual only; see note on noise below
   public void Faint(bool f) { /* ragdoll + drop carried items on the server */ _body.Faint(f); /* pose, if not ragdolling */ }
   ```
   FarmerVisual reads `ClientId` / `IsLocal` from the first *other* `IPlayerHooks` above it, so do not forward those back.
3. Locomotion animation is automatic (FarmerVisual derives `Speed`, `Grounded`, `Jump` from the body's motion). If you prefer to
   drive it, untick `autoLocomotion` and set Animator params `Speed` (m/s, float), `Grounded` (bool), `Jump` (trigger), `Fainted` (bool).
4. Local player: body and cosmetics render **shadows only** and the name tag is hidden once `IsLocal` is true
   (FarmerLoadoutSync re-evaluates on spawn / ownership change).
5. Emotes: keys 5–8 are handled by `FarmerEmotes` (own InputActions, owner only). The server emits the Wave noise
   (`INoiseService.Emit(Shout, 0.6)`), so your `PlayEmote` must **not** emit noise again. Faint calls your `IPlayerHooks.Faint(true/false)`
   on every peer via a reliable RPC (true, then false after 2 s); make item-dropping server-only.
6. Layers: body renderers are on Default; put them on `Player` if your raycasts need it (Point's ping raycast already ignores `Player`).

## Networking
- `Resources/NetworkPrefabs/Customization.asset` exists and is intentionally empty (FarmerBody nests under the Player NetworkObject;
  wardrobes are local props).
- Loadout replication: owner sends its saved loadout (`PlayerPrefs` key `HP_Loadout`, JSON v2) on every spawn and every scene load;
  the server sanitises it (unowned/unknown -> slot default, duplicate name-tag colour -> next free by join order) and writes a
  server-owned `NetworkVariable<FarmerLoadout>`. Late joiners get it on initial sync.
- Loadout v2 wire format (25 bytes, GDD budget 32): format byte, body, primary, secondary, accent, skin, tag, **head, hair,
  hair colour, outfit** (u8 each), hat/face/back (u16), 4 emotes (u16). v1 saves (JSON without `v`/`v:1`) and v1 20-byte blobs
  load with the base head/hair/outfit and the default hair colour, and are re-saved as v2. Byte table: `FarmerLoadout.cs`.
- Connection approval (Networking) replaces a profane player name with "Farmhand #N" (Core `NameFilter`).
- Names on tags come from `ISessionService.Players` (fallback "Farmer N").

## World
Place an empty with `LevelMarker` role `Wardrobe` where a wardrobe should stand (barn, lobby). `WardrobeSpawner` builds one at each
marker on scene load on every peer, facing the marker's forward; interaction range 2.2 m in front, key E.

## UI / HUD
- Player colour for HUD/Ledger: `CosmeticCatalog.NameTag(loadout.NameTag)` (colour + `TagShape`). Customization does not reference UI.
- The character creator ("Create your farmer" / Grandpa's Wardrobe) is UI Toolkit (`WardrobeUI` + `Art/Cosmetics/UI/CharacterCreator.uss`,
  panel `Art/Cosmetics/UI/WardrobePanel.asset`). Frontend opens it through Core `ICharacterCreator` (registered by
  `CharacterCreatorService`): main menu first run (`NeedsFirstRun`, never in batchmode unless a test opts in), main menu
  Customize, lobby C. The wardrobe prop opens it for `FarmerLoadoutSync.Local`. While `IsOpen` (and on the frame it closed)
  Frontend's menu, lobby hotkeys and pause ignore Esc/keys.
- Frontend provides `IPlayerNameService` (Settings player name + session platform) for the Name tab and publishes
  `UiAccessibility` (colour-blind mode, reduced motion, UI scale, text size) which the creator follows.
- The main menu finds the catalogue through `Resources/Customization/CustomizationLink.asset`.

## Art pipeline
- New cosmetics are `CosmeticEntry` assets in the catalogue's `entries` (same slot + id replaces a placeholder). Tripo FBXs:
  HarvestPanic/Cosmetics/Import Tripo Cosmetic (see docs/art/adding-cosmetics.md). Tint-mask shader: `Art/Cosmetics/Shaders/CosmeticTintMask.shader`.
- `FarmerAssetBuilder.BuildAll` also writes UV'd copies of the farmer meshes (`Art/Cosmetics/Meshes`, for outfit patterns),
  the Shrug/Salute/Chicken Dance clips, merges new default items into an existing catalogue (`EnsureDefaults`) and builds the
  test-only network prefab `Prefabs/Farmer/Test/FarmerNetTest.prefab` (not in any NetworkPrefabs list).

## Open requests (need a Core contract or another team)
- Wardrobe holds `InputModal` (Push on open, Pop on close/destroy); emotes and the wardrobe prop also respect `InputModal.IsBlocking`.
- No day-phase contract: "wardrobe blocked during the active day timer" is not enforced yet.
- E for the wardrobe may overlap the Player's interact key.
- Ownership on the server only trusts starter items + `unlockAllCosmetics` (no profile/unlock service yet); `CosmeticCatalog.LocalOwnedKeys` is the client hook.
- First-person arms get the outfit's sleeve colour through the existing `IFirstPersonArmsSkin` (Muscle Tank = bare arms, Rain Slicker = overalls colour); patterns are not drawn on the arms.
