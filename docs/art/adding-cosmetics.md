# Adding cosmetics (Tripo or any FBX)

How a model becomes something players can pick in **Create your farmer** / Grandpa's Wardrobe.
Spec for the models themselves: `design/art/asset-specs/tripo-batch-02-character.md`.

## TL;DR (owner workflow)

1. Generate the model in Tripo per the spec and save it in `~/Desktop/HarvestPanic Tripo Models/<Category>/`
   (`Hats`, `Face`, `Back`, `Hair`, `Heads`; `Bodies` and `Outfits` are rigged, see below).
   Use the spec file name (`SM_Hat_CowboyHat.fbx`) if you can. A raw Tripo download name
   (`cartoon+cowboy+hat+3d+model.fbx`) also works: the category folder plus keywords pick the item.
   Keep Tripo's `.fbm` texture folder next to the FBX. Optional mask: `<Name>_Mask.png` (see "Recolouring").
2. In Unity: **HarvestPanic → Cosmetics → Copy + Import Tripo Models From Desktop**.
   That copies each model to `Assets/ThirdParty/Tripo/<SpecName>/<SpecName>.fbx` (+ `<SpecName>_BaseColor.*`,
   `<SpecName>_Mask.*`) and imports it.
3. Open the creator (Play from `Scenes/Bootstrap`, then Main menu → Customize; in the Lobby press C) and check it.
   Fine-tune placement in the entry asset (below) if needed. Commit the FBX, textures, prefab, material and entry.

Already have the FBX inside the project? Put it in `Assets/ThirdParty/Tripo/<Name>/`, select the FBX (or its folder)
and run **HarvestPanic → Cosmetics → Import Tripo Cosmetic**. **Import All Tripo Cosmetics** re-imports every
cosmetic folder (critter/prop folders such as `Pig_Hamlet` are ignored). Batch:
`Unity -batchmode -nographics -quit -projectPath HarvestPanic -executeMethod HarvestPanic.Customization.Editor.TripoCosmeticImporter.ImportAllBatch`.

## What the importer does

| Step | Detail |
|---|---|
| Identify | `SM_<Slot>_<Name>` / `SK_Back_Cape` (spec names), else category folder + keywords. Spec items in batch 02 map to the existing placeholder (same **id and key**), e.g. `SM_Hat_StrawHat` → `hat.straw` id 1. Anything else is a **new** item with the next free id in its slot. |
| Import settings | Static (no rig/animation), no cameras/lights/blend shapes/colliders, scale factor 1. |
| Material | `Assets/Art/Cosmetics/Items/Materials/MI_<Name>.mat`. With a `*_Mask` texture: `HarvestPanic/Cosmetics/TintMask`; otherwise URP Lit (matte, no specular). The FBX's own materials are remapped to it. |
| Fit + anchor | Scaled so its largest dimension matches the slot (hats 0.3–0.62 m, face 0.2–0.26 m, back 0.45–0.62 m, hair 0.3–0.42 m, heads 0.34–0.36 m; the farmer head is ~22 cm wide before the body's chunky-head scale) and anchored: hat brim on the crown, full-cover hats (Pumpkin, Bucket) over the head, glasses just in front of the eyes, moustache under the nose, back items with their front on the back, hair top on the crown, beards on the face. |
| Prefab | `Assets/Art/Cosmetics/Items/Prefabs/<Name>.prefab` (root → `Model`). |
| Entry | `Assets/Art/Cosmetics/Items/<Slot>/<key>.asset` (`CosmeticEntry`), added to `Art/Cosmetics/CosmeticCatalog.asset → entries`. A spec item copies the placeholder's name, flavour, unlock rule and hint. Re-importing updates the prefab/material and **keeps your edits** (name, flavour, unlock, offsets). |
| Report | Console line per model: slot, key, id, "replaces the placeholder" or "new item", texture/mask found, triangle count vs budget (hats/back 1.5k, face 600, hair 1k, heads 2.5k). |

Card thumbnails are rendered at runtime from the item on the player's farmer, so no icon is needed. To force one,
set `Icon` on the entry (any Texture2D).

## Tweaking an item

Select the entry asset (`Assets/Art/Cosmetics/Items/<Slot>/<key>.asset`) and edit **Item**:

- `Display Name`, `Flavour`, `Unlock` + `Unlock Hint` (shown on locked cards), `Random Weight` (0 = never rolled by Randomize).
- Placement: `Attach` (Auto = slot default), `Offset` (metres), `Rotation` (degrees), `Scale`.
- Hats: `Blocks First Person View` (Pumpkin/Bucket style), `Hides Hair` (full-cover).
- Hair: `Hide Base Hair` (bald styles). Heads: an authored head **replaces** the base head mesh.
- `Tint Mode`: None (real colours), MaskTexture (TintMask shader), PerMaterial (`Material Tints` per material slot).
  `Mask Red` = what the mask's red channel follows (Primary; Hair for hair/beards).

Never change an `Id` once shipped: it is what saves and the network loadout store.

## Recolouring (tint masks)

Paint zones flat in Tripo (overalls mid grey `#9A9A9A`, shirt off-white `#E8E4DA`, boots/gloves dark grey `#4A4A4A`,
skin beige `#D9B59A`), then add `<Name>_Mask.png` (same UVs): **R** = primary (overalls; hair colour for hair items),
**G** = secondary (shirt), **B** = accent (boots/gloves), **A** = skin; 0 = keep the texture's real colour (eyes,
buttons, buckles). The shader keeps the baked shading: `tint × (albedo luminance / zone grey)`.
Hats are authored in real colours at VS, so they need no mask. Hair without a mask is tinted whole by the hair colour.

## Not automatic yet

- **Bodies (`SK_Farmer_*`) and outfit meshes (`SK_Outfit_*`)** are imported as Humanoid but not added to the
  catalogue: the farmer uses the CC0 Quaternius Generic rig and its controller, so a Tripo body needs retargeting
  (Humanoid controller + clips) by the technical artist before it can replace the body. Until then, body types are
  proportion variants of the current farmer and outfits are pattern/colour variants (+ a few attached parts).
- The cape is attached as a static mesh (no cloth sim yet).
- Gloves and overall patterns as separate slots (character-style §4/§8) are not in the loadout yet; patterns live in
  the Outfit slot for now.

## Adding a cosmetic by hand (no FBX)

`Create → HarvestPanic → Cosmetic Entry`, set `Slot`, a new `Id` and `Key` (`hat.mything`), then either a `Prefab`
or primitive `Parts` (the same recipe format the placeholders use in `CosmeticCatalog.Defaults.cs`/`.Looks.cs`), and
drag the entry into the catalogue's `Entries` list.
