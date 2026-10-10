# Unreal Setup — HARVEST PANIC

Engine: **Unreal Engine 5.8.3** at `/Applications/UE_5.8` (macOS, Apple Silicon, Xcode).
Project: `HarvestPanic/HarvestPanic.uproject` (C++ + Blueprint). Ships Windows + macOS on Steam.

## First-time setup (once per machine)

```bash
git lfs install                 # enable LFS hooks
git clone <repo> && cd <repo>
git lfs pull                    # fetch binary assets
```

## Open the project

- Double-click `HarvestPanic/HarvestPanic.uproject`, or
- `open -a /Applications/UE_5.8/Engine/Binaries/Mac/UnrealEditor.app HarvestPanic/HarvestPanic.uproject`

If the editor says modules are missing/out of date, let it rebuild, or build manually (below).

## Build (macOS)

Generate the Xcode workspace (gitignored; regenerate any time):

```bash
/Applications/UE_5.8/Engine/Build/BatchFiles/Mac/GenerateProjectFiles.sh \
  -project="$PWD/HarvestPanic/HarvestPanic.uproject" -game
```

Build the editor target with UnrealBuildTool:

```bash
/Applications/UE_5.8/Engine/Build/BatchFiles/Mac/Build.sh \
  HarvestPanicEditor Mac Development -project="$PWD/HarvestPanic/HarvestPanic.uproject"
```

Package/cook (also `commands.build` in `project.yaml`):

```bash
/Applications/UE_5.8/Engine/Build/BatchFiles/RunUAT.sh BuildCookRun \
  -project="$PWD/HarvestPanic/HarvestPanic.uproject" -platform=Mac -build -cook
```

Windows builds use `-platform=Win64` from a Windows machine with Visual Studio.
Always pass an **absolute** `.uproject` path.

## Git LFS

`.gitattributes` routes `*.uasset`, `*.umap`, textures (`png/tga/exr/psd/...`),
meshes (`fbx/obj/glb/...`), audio (`wav/mp3/ogg/...`) and video (`mp4/mov/bk2`) through LFS.
- Check a file is in LFS: `git lfs ls-files | grep <name>`
- `.uasset`/`.umap` cannot be merged — coordinate who edits a map/asset; consider
  `git lfs lock <path>` for shared levels.
- Never commit `Binaries/`, `Intermediate/`, `Saved/`, `DerivedDataCache/` or
  generated `.xcworkspace`/`.sln` files (all gitignored).

## Branch-per-task workflow

1. `git switch main && git pull`
2. `git switch -c <type>/<short-task>` (e.g. `feat/crop-growth`, `fix/tractor-collision`, `chore/...`)
3. Work, commit small; save assets in the editor before committing.
4. `git push -u origin <branch>` to `tahakeler/unreal_game`, open a PR, merge into `main`.
One task = one branch. Don't mix unrelated asset edits into a branch.

## Fab / Marketplace assets

Use Fab's **Add to Project** (Epic Launcher / Fab in-editor). It installs packs at
the standard location `HarvestPanic/Content/<PackName>/` — keep that layout; don't
move pack folders (it breaks internal references). Move or duplicate only the
assets you actually use into `HarvestPanic/Content/HarvestPanic/...` via the editor
(right-click > Migrate/Move, which fixes up redirectors). Commit packs via LFS in a
dedicated branch, e.g. `chore/fab-<packname>`, and record the pack + licence in the PR.
Fab *plugins* (code) go to `HarvestPanic/Plugins/<PluginName>/`.
