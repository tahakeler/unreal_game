# HARVEST PANIC — Game Changelog

Internal changelog for the game. The repo-root `CHANGELOG.md` covers the Claude Code Game Studios framework, not
the game. Player-facing copy is in `production/release/beta-release-notes.md`.

---

## [0.9.0-beta] — Early Beta (unreleased, proposed version)

Date: 2026-10-21 · No git tags exist yet. Range: the last 100 non-merge commits on `integration/unity-slice`
(`ddb5656..6d35695`) plus `feature/release-readiness`. Provenance: 100 of 100 commits are Game commits (they name
HARVEST PANIC systems found in `design/` and `HarvestPanic/Assets/Scripts/`). There are 0 framework commits in the
range. 100 commits have no task ID, because the project tracks work by branch and not by ticket.

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
  `steam/app_build.vdf.template`.

### Bug fixes
- Player builds no longer render pink materials, because the shaders are kept alive in Resources (`06dcbc0`).
- PlayMode cross-team failures fixed: a leaked persistent NetworkManager, and a NoiseService registry slot that was
  not freed (`f50a881`).
- Critters now replicate to late clients, because their spawn waits for the NGO scene event (`8757e51`, `f4c47ee`).
- The weather replication test registers all team NetworkPrefabs lists (`cfd6c68`).

### Technical / test
- Bot full-week soak test and perf capture/hotspot tests (`243ca8f`, `54b20e4`). PerfHotspot now skips when there
  is no GPU (`6d35695`).
- Run Save fault injection (this branch): EditMode `RunSaveRobustnessTests` (14). It covers every kill point of the
  write, truncation at any length, a corrupt byte anywhere in the file, a failed write, and a two-week torn-save
  sweep. PlayMode `RunSaveResumePlayTests` (2) covers quit to desktop then Continue, and a torn save resuming from
  `.bak`.

### Known issues (from `docs/qa/playtest/bot-week-report.md` and the gate report)
- S2 balance: week-1 rent looks out of reach from Muddlebury's loose goods (about $430 against a $540 quota).
- S3: 2 of the 5 daily goods never sell in the pen.
- S3: the prize pumpkin is bruised by "Nobody" at spawn.
- S3: no Landlord barks in an 8-day bot run.
- Voice chat has not shipped. A real Steam App ID and cross-play are untested. The macOS build is not signed or notarised.

### Metrics
- Commits in range: 100 non-merge (no tags). Files and lines changed are not computed, because there is no tag to diff against.
- Commits without a task reference: 100.
