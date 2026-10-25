# Unity Setup — HARVEST PANIC

Engine: **Unity 6.3 LTS `6000.3.26f1`** (Apple Silicon), **URP**, **C#**.
Networking: **Netcode for GameObjects** + Steam transport (Facepunch.Steamworks);
Unity Transport (UTP) over LAN for development. Ship: Windows + macOS on Steam.

## 1. Install and open

1. Install Unity Hub, then editor `6000.3.26f1` with **Mac Build Support (Mono/IL2CPP)**
   and **Windows Build Support (Mono)**. Editor path:
   `/Applications/Unity/Hub/Editor/6000.3.26f1/Unity.app`.
2. `git lfs install` once per machine, then clone and `git lfs pull`.
3. Unity Hub → **Add project from disk** → select `HarvestPanic/` (the folder that
   contains `Assets/`, `Packages/`, `ProjectSettings/`) — not the repo root.

## 2. Project layout

```
HarvestPanic/
  Assets/
    Scripts/            # C# code root (resolve_code_root → HarvestPanic/Assets)
      Core/ Gameplay/ AI/ Networking/ UI/ Editor/
    Tests/
      EditMode/         # NUnit, own .asmdef (Editor platform only)
      PlayMode/         # NUnit, own .asmdef
    Scenes/ Prefabs/ Materials/ Settings/ (URP assets) Data/ (ScriptableObjects)
    ThirdParty/<Pack>/  # all third-party / CC0 / Asset Store packs, unmodified
  Packages/manifest.json, packages-lock.json   # tracked
  ProjectSettings/                              # tracked
  Library/ Temp/ Logs/ UserSettings/ Obj/ Build(s)/  # gitignored
```

Path-scoped rules in `.claude/rules/` match `HarvestPanic/Assets/Scripts/{Gameplay,Core,AI,Networking,UI,Tests}/**`.

## 3. Required editor settings (verify after first open)

- **Edit → Project Settings → Editor → Asset Serialization Mode: Force Text**
- **Version Control Mode: Visible Meta Files** — always commit `.meta` with its asset.
- **Graphics**: URP asset assigned (Project Settings → Graphics and Quality).
- **Player → Active Input Handling**: Input System Package (New).

## 4. Batchmode commands (also in `project.yaml` → `commands`)

Any `-batchmode` process (test runs, headless players) is silent: `BatchModeSilence` forces `AudioListener.volume = 0`. Pass `-hpAudible` to hear audio.

```bash
UNITY="/Applications/Unity/Hub/Editor/6000.3.26f1/Unity.app/Contents/MacOS/Unity"

# EditMode / PlayMode tests (no -quit with -runTests; exit code != 0 on failure)
"$UNITY" -batchmode -nographics -projectPath HarvestPanic -runTests \
  -testPlatform EditMode -testResults "$(pwd)/Logs/editmode-results.xml" -logFile Logs/editmode.log
"$UNITY" -batchmode -nographics -projectPath HarvestPanic -runTests \
  -testPlatform PlayMode -testResults "$(pwd)/Logs/playmode-results.xml" -logFile Logs/playmode.log

# Player builds (Mono backend; output under HarvestPanic/Builds/, gitignored)
for M in BuildMacDev BuildMacRelease BuildWindowsDev BuildWindowsRelease; do
  "$UNITY" -batchmode -nographics -quit -projectPath "$(pwd)/HarvestPanic" \
    -executeMethod HarvestPanic.Editor.BuildScript.$M -logFile "$(pwd)/Logs/build-$M.log"
done

# Multiplayer smoke + robustness test (needs BuildMacDev; random UDP port; exit 0 = PASS)
tools/net/two-process-test.sh "$(pwd)/Logs/nettest"
```

| Method | Output | Notes |
|---|---|---|
| `BuildMacDev` | `Builds/macOS-dev/HarvestPanic.app` | Development, NetTest scenes, `steam_appid.txt` beside the .app |
| `BuildMacRelease` | `Builds/macOS/HarvestPanic.app` | Release, no test scenes, no `steam_appid.txt` |
| `BuildWindowsDev` | `Builds/Windows-dev/HarvestPanic.exe` | Development, NetTest scenes, `steam_appid.txt` beside the exe |
| `BuildWindowsRelease` | `Builds/Windows/HarvestPanic.exe` | Release (needs Windows Build Support (Mono)) |

`BuildMac` / `BuildWindows` / `BuildWindows64` remain as aliases (release unless `-development`).
`-buildOutput <path>` overrides the output path. Methods switch the active build target, call
`BuildPipeline.BuildPlayer`, and exit non-zero on failure (`EditorApplication.Exit(1)`).
The editor must not be open on the same project while running batchmode.

### Steam App ID

`NetworkingConfig.steamAppId` (asset `Assets/Resources/Data/Networking/NetworkingConfig.asset`) defaults to
**480** (Valve's Spacewar test app). **TODO(owner): set the real App ID** once the Steamworks app exists.
Without rebuilding, `steam_appid.txt` (working dir, or beside the exe / .app) or `-steamAppId N` override it.
Steam is skipped with `-nosteam` and in batchmode; when the Steam client isn't running the game falls back to LAN
(menu footer shows "LAN mode"). Invites: `+connect_lobby <id>` launch arg and overlay "Join Game" are handled.

## 5. Git: LFS, merges, branches

- **LFS** (`.gitattributes`): models, textures, audio, video, fonts, native plugins.
- **Unity YAML stays text** (`*.unity *.prefab *.asset *.mat *.meta …`) with
  `merge=unityyamlmerge eol=lf`. Register the merge driver once per machine:

```bash
git config merge.unityyamlmerge.name "Unity SmartMerge"
git config merge.unityyamlmerge.driver \
  '"/Applications/Unity/Hub/Editor/6000.3.26f1/Unity.app/Contents/Tools/UnityYAMLMerge" merge -p %O %B %A %A'
git config merge.unityyamlmerge.recursive binary
```

- **Branch per task**: `feature/<topic>`, `fix/<topic>`, `chore/<topic>` from `main`;
  push the branch, open a PR, never commit to `main` directly.
- Avoid two branches editing the same scene; split work into prefabs.

## 6. Third-party and CC0 assets

- Import every external pack under `HarvestPanic/Assets/ThirdParty/<Pack>/` and
  leave it unmodified (copy to `Assets/` before editing).
- Record each pack — name, source URL, author, licence, date — in
  `THIRD_PARTY_LICENSES.md` at the repo root (create it with the first pack).
- Packages from the Unity registry (NGO, Input System, URP) live in
  `Packages/manifest.json`. Dev runs need `steam_appid.txt`.
- **Steamworks plugins** (Facepunch.Steamworks 2.5.2 + Valve SDK 1.61 natives) live only in
  `HarvestPanic/Assets/Plugins/Steamworks/`. Each has explicit per-platform import settings:
  `Facepunch.Steamworks.Posix.dll` + `macOS/libsteam_api.dylib` (universal arm64/x86_64) for the macOS editor and
  player; `Facepunch.Steamworks.Win64.dll` + `Windows/steam_api64.dll` for the Windows editor and player.
  Replace managed DLLs and natives together, from one Facepunch release: the native flat-API exports must match the
  managed DLL's imports.
- The Facepunch NGO transport is an **embedded package**
  (`HarvestPanic/Packages/com.community.netcode.transport.facepunch/`), byte-identical to upstream except that its
  bundled `Runtime/Facepunch/` plugins are removed. Those plugins were Facepunch.Steamworks 2.3.2, with an
  i386/x86_64-only macOS library and a Linux DLL enabled for macOS players. Update procedure:
  `HARVESTPANIC-PATCH.md` in that folder.
- `BuildScript` guards Steam on every player build (`HarvestPanic.Editor.SteamPluginCheck`):
  - **Preflight** (before building): fails if any Facepunch.Steamworks / steam_api plugin is found outside
    `Assets/Plugins/Steamworks/` (for example, a package update re-adding its own copy), or if the set enabled for the
    target is wrong.
  - **Post-build**: fails a macOS build when `Contents/PlugIns/libsteam_api.dylib` is missing, or `lipo -archs` shows it
    lacks arm64 or any architecture of the player executable, or the managed assembly isn't exactly
    `Facepunch.Steamworks.Posix.dll` (for example, the Linux variant is shipped). A Windows build fails without an x64
    `HarvestPanic_Data/Plugins/x86_64/steam_api64.dll` and `Facepunch.Steamworks.Win64.dll`.
  - To check an existing build without rebuilding:
    `-executeMethod HarvestPanic.Editor.SteamPluginCheck.VerifyPlayerCli -checkTarget mac|win -checkPlayer <.app|.exe>`.
  - EditMode `SteamPluginCheckTests` covers the same rules.
