# HARVEST PANIC — Game Changelog

Internal changelog for the game. The repo-root `CHANGELOG.md` covers the Claude Code Game Studios framework, not
the game. Player-facing copy is in `production/release/beta-release-notes.md`.

---

## [0.9.0-beta] — Early Beta (unreleased, proposed version)

Date: 2026-10-21 · No git tags exist yet. Range: the 121 non-merge commits on `main` in `ddb5656..366c311`, plus
the release-readiness commits on `feature/release-readiness`. Provenance: 119 of the 121 are Game commits (they name
HARVEST PANIC systems found in `design/` and `HarvestPanic/Assets/Scripts/`). 2 are framework commits (`457fce3`,
`dec81b4`: agent model routing) and are left out of this changelog. No commit has a task ID, because the project
tracks work by branch and not by ticket.

Version note: `ProjectSettings` `bundleVersion` is still `1.0`. The owner and the Networking team need to set
`0.9.0` (the network version check compares versions), then tag `v0.9.0` on the commit that is built.

### New features
- **Shop phase and run progression**: evening Shop, 13 upgrade ScriptableObjects, Collateral sell-back,
  eviction, and run stats (`24765e8`, `87886c8`, `a423b68`). Owner: economy.
- **Run Save / Continue Farm**: versioned JSON with CRC32 and an atomic temp+rename write plus `.bak`. It saves at
  Shop entry, at each purchase, and after a paid Sunday. A menu entry continues the farm (`24765e8`, `87886c8`).
  Fault-injection tests were added in this release (see Technical).
- **Weather**: a seeded WeatherDirector with a deck of five weathers, day modifiers, URP lighting, and host-to-client
  replication (`984b423`, `b0adb2b`).
- **Audio pass 1**: AudioService (pooled voices, buses, ducking), MusicDirector, noise routing, AudioNetRelay,
  economy cues, and Kenney CC0 packs plus in-house placeholder sounds (`56cd536`, `5f93116`, `1f62703`).
- **Landlord events**: 8 weekly rules, Inspection and Helpful visits, Sunday walk-in, and receipt awards (`4f576a3`, `59532a1`).
- **Night phase**: Night Director, Mr. Stitches, the night fox, and lantern posts (`3218d0d`, `5f96483`).
- **Livestock**: cow and goat with personalities, LODs and audio routing (`59fd0e8`, `b9531ab`).
- **Onboarding**: Grandpa's tapes, captions, rebinding, comfort settings, and pause help (`678a91d`, `9735e29`).
- **Camcorder v1**: replay ring buffer, capture rules, ghost viewer and PNG export (`223f209`, `838711f`).
- **Steam readiness**: App ID resolver (`-steamAppId`, `steam_appid.txt`, config), rich presence, release/dev build
  targets, and the extended two-process robustness test (`882190e`).
- **Credits screen** (this branch): Main menu → Credits. It shows the team plus every row of
  `THIRD_PARTY_LICENSES.md`, with the full MIT text for Facepunch.Steamworks and the community transport, and the
  OFL font licences. Release builds ship `LICENSES.txt` beside the player.

### Improvements
- **Performance**: URP GPU Resident Drawer is on and static batching is off. Draw calls went from 6,187 to 508 and
  the main thread from 5.0 ms to 3.8 ms (M2 Pro editor). Per-frame GC fixes landed in EconomyHud, WeatherLook and
  MrStitches: allocations per frame went from 76 to 38 (`76f057e`).
- **Polish 2**: moonlit night, stronger lantern, goat silhouette, NoiseCueFeed captions, owner-predicted footsteps,
  and shader keep-alive materials so player builds have no pink materials (`06dcbc0`, `e44a102`, `6139271`, `e8934c1`).
- **Release tooling** (this branch): `tools/release/package-mac.sh`, `package-windows.sh`, `steam-upload.sh` and
  `steam/app_build.vdf.template`. The macOS zip is written without AppleDouble `._*` entries, which would otherwise
  break the sealed signature when extracted by a non-Apple tool.
- **Release builds verified** (this branch): `BuildWindowsRelease` (142.1 MB report size, zip 58.6 MB) and
  `BuildMacRelease` (Universal, 161.3 MB report size, unsigned zip 67.8 MB) succeed from batchmode with 0 errors.
  `LICENSES.txt` sits beside both players, and the Credits text baked into each player is byte-identical to it.

### Bug fixes
- Player builds no longer render pink materials, because the shaders are kept alive in Resources (`06dcbc0`).
- PlayMode cross-team failures fixed: a leaked persistent NetworkManager, and a NoiseService registry slot that was
  not freed (`f50a881`).
- Critters now replicate to late clients, because their spawn waits for the NGO scene event (`8757e51`, `f4c47ee`).
- The weather replication test registers all team NetworkPrefabs lists (`cfd6c68`).
- `SessionRobustnessTests` (4) no longer fail when the test runner passes `-netPort`: the test clients now dial the
  host transport's real port rather than the config port (this branch).
- The `[Explicit]` QA captures `EconomyHudScreenshots` and `LandlordScreenshots` now report a skip reason (this branch).

### Technical / test
- Bot full-week soak test and perf capture/hotspot tests (`243ca8f`, `54b20e4`). PerfHotspot now skips when there
  is no GPU (`6d35695`).
- Run Save fault injection (this branch): EditMode `RunSaveRobustnessTests` (14). It covers every kill point of the
  write, truncation at any length, a corrupt byte anywhere in the file, a failed write, and a two-week torn-save
  sweep. PlayMode `RunSaveResumePlayTests` (2) covers quit to desktop then Continue, and a torn save resuming from
  `.bak`. All pass.
- Suites on the release head: EditMode 301/301; PlayMode 79, of which 64 passed, 0 failed and 15 skipped (each skip a
  GPU or QA screenshot capture with a reason).

### Known issues (from `docs/qa/playtest/bot-week-report.md` and the gate report)
- S2 balance: week-1 rent looks out of reach from Muddlebury's loose goods (about $430 against a $540 quota).
- S3: 2 of the 5 daily goods never sell in the pen.
- S3: the prize pumpkin is bruised by "Nobody" at spawn.
- S3: no Landlord barks in an 8-day bot run.
- S2: the macOS player has no usable Steam library. The package's `libsteam_api.bundle` is i386/x86_64 only and is
  not included, and the build picks `Facepunch.Steamworks.Linux.dll`. The Mac player falls back to LAN (gate item 2).
- S3: Run Save falls back to `.bak` even when the main save is from a newer build (gate item 5).
- Voice chat has not shipped. A real Steam App ID and cross-play are untested. The macOS build is not signed or notarised.

### Metrics
- Commits in range: 121 non-merge on `main` (119 game, 2 framework) plus the release-readiness branch (no tags). Files and lines
  changed are not computed, because there is no tag to diff against.
- Commits without a task reference: all of them.
