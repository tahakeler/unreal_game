# Asset Pipeline: Tripo3D generation to UE import

Source spec: `design/art/asset-specs/tripo-batch-01.md`. Tools: `tools/tripo/tripo_gen.py` (generate) and `HarvestPanic/Scripts/AssetGen/import_generated.py` (import into UE 5.8).

## 1. One-time setup
- Put the key in your shell profile, then open a new terminal:
  `echo 'export TRIPO_API_KEY=...' >> ~/.zshrc`
  The key is read only from `TRIPO_API_KEY`. Never paste it into files, commits, or chat.
- Install the official CLI (used for balance, rig and retarget): `npm i -g tripo-cli`, then run `tripo doctor`.
- Check your credits with `tripo balance`. The generator checks the balance before it submits anything and stops cleanly if the balance is below the estimate.

## 2. Generate
```
python3 tools/tripo/tripo_gen.py --priority P0 --dry-run    # rebuild tools/tripo/batch01.json, print plan + credit estimate
python3 tools/tripo/tripo_gen.py --priority P0 --max-credits 400
python3 tools/tripo/tripo_gen.py --only SK_Pig_Hamlet --reroll   # re-roll one item
python3 tools/tripo/tripo_gen.py --priority P0 --no-rig          # skip rig/anim
```
- The manifest is rebuilt from the spec on every run. Edit the spec, not the JSON.
- Text-to-model goes through `POST /v3/generation/text-to-model` with model `v3.1-20260211`, plus `negative_prompt` and `face_limit`. It requests texture with PBR off.
- Rigged items (SK_) are rigged with `tripo anim rig --rig-type quadruped|biped|avian --out-format fbx`, then `tripo anim retarget --animation preset:idle ... --animate-in-place`.
- Outputs go to `tools/tripo/out/<id>/`: `<id>.glb`, `<id>_rN.glb` per roll, `preview_rN.*`, and FBX files for rigs. Progress is saved in `tools/tripo/state.json`, so an interrupted run picks up where it stopped. Both paths are gitignored.
- Credit numbers are estimates (30 per model, 25 per rig, 10 per anim). `tripo usage` shows what was actually billed.

## 3. Review (spec section 5 QA checklist)
Open each `preview_rN` image (or run `tripo view <task_id>`) and check it against the Must items:
style match with Emberlight props, palette, a 64 px squint test, no base or ground plane, and no fused limbs on rigged items.
To record a verdict, edit `state.json` and set `"qa": "accept"` or `"qa": "reject: <reason>"` on the item.
Re-roll with `--reroll`. After 3 rolls the tool refuses to roll again: switch to image-to-3D from a paintover (`tripo generate image-to-model`).

## 4. Import (headless, no window)
```
/Applications/UE_5.8/Engine/Binaries/Mac/UnrealEditor-Cmd "$PWD/HarvestPanic/HarvestPanic.uproject" \
  -run=pythonscript -script="$PWD/HarvestPanic/Scripts/AssetGen/import_generated.py" \
  -unattended -nullrhi -nosplash -nosound
# options: HP_TRIPO_ONLY=SK_Pig_Hamlet,SM_FeedBucket   HP_TRIPO_ACCEPTED=1 (only qa=accept)
```
The import script does the following:
- Imports GLBs via Interchange into `/Game/HarvestPanic/Generated/<Category>/<id>`.
- Scales the largest dimension to the spec's `max_dim_cm` and moves the pivot to base-centre. Non-base pivots (handle apex, hinge, grip, knot) are flagged `PIVOT-MANUAL`.
- Adds simple collision: box, sphere, capsule, or auto-convex. UCX shapes for the stall are still a TODO.
- Adds the spec sockets. Positions are heuristic and need hand adjustment.
- Writes `HP_MassKg`, `TripoTaskId` and `TripoPrompt` metadata tags. Static meshes have no mass, so the carry BP reads `HP_MassKg`.
- Assigns the `M_HP_Prop` material if it exists and a slot is empty.
- Saves, reloads to verify, and logs a summary table (`[TripoImport]` in the log).

Rigged FBX files from `out/<id>/` are imported by hand into a shared `SKEL_HP_Quadruped` (see the spec section 5 rig checks).

## Known limits
- Net/Net_Bag and the PenGate leaf/post split are generated as single meshes. Split them in a DCC tool.
- No headless test has been run yet against a real GLB. Verify the import script on the first batch.
