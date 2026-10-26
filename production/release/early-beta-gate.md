# Early Beta Gate — HARVEST PANIC

Gate: GDD `design/master-gdd/18-production.md` §18.7 *Definition of Done — Early Beta* (items 1–10).
Assessed: 2026-10-21 by release-manager on `feature/release-readiness`, which includes `origin/main` @ `366c311`
(merged on this branch; `main` carries the perf capture, hotspot and bot-week soak work from `feature/perf-playtest`).
Every test count, build size and file check below was produced on this branch after that merge.
Proposed version: **0.9.0**. `ProjectSettings` `bundleVersion` is still `1.0` (the macOS app reports
`CFBundleShortVersionString` 1.0), and there are no git tags yet (see Owner actions).

Legend: **MET** means the evidence on disk satisfies the item. **PARTIAL** means part of the work is done and the
rest is team work that can be finished without the owner. **BLOCKED-ON-OWNER** means the item cannot close without
something only the owner can supply: an account, a credential, a legal confirmation or a sign-off.

## Verdict: NOT READY. 1 MET, 5 PARTIAL, 4 BLOCKED-ON-OWNER

| # | DoD item | Status |
|---|---|---|
| 1 | macOS (signed + notarised) and Windows builds from batchmode, no manual steps | BLOCKED-ON-OWNER (signing) |
| 2 | 4 players, full week over **Steam**, Mac ↔ Windows cross-play | BLOCKED-ON-OWNER (App ID, accounts) + team fix (macOS Steam library) |
| 3 | All EditMode + PlayMode pass; two-process passes; no unexplained skips | PARTIAL |
| 4 | 60 fps Mac Medium at 4 players, never < 30, host net ≤ 2 ms/frame | PARTIAL |
| 5 | Run Save survives quit/resume and process-kill | **MET** |
| 6 | Join-in-progress, client drop without soft-lock; host loss → lobby | PARTIAL |
| 7 | Voice chat or fallbacks usable; onboarding teaches grab/carry/sell < 5 min | BLOCKED-ON-OWNER |
| 8 | No known crash; 0 open P0/P1; 30-min 4-player soak, memory growth ≤ 10% | PARTIAL |
| 9 | Every asset in `THIRD_PARTY_LICENSES.md` with confirmed commercial rights | BLOCKED-ON-OWNER (Tripo rights) |
| 10 | Audio pass 1: impacts, critter barks, UI, music states | PARTIAL |

---

## 1. Builds from batchmode: BLOCKED-ON-OWNER (signing)

Evidence (this branch, `project.yaml` `build_*_release` commands, `-batchmode -nographics -quit`, no manual steps):

| Build | Result | Unity report size | On disk | Package (`tools/release/`) |
|---|---|---|---|---|
| `BuildScript.BuildWindowsRelease` → `Builds/Windows/HarvestPanic.exe` | Succeeded, 0 errors, 18 m 45 s (includes the platform switch) | 142,094,597 B | 136 MB, 201 files | `HarvestPanic-20261021-Windows.zip` **58,552,031 B** |
| `BuildScript.BuildMacRelease` → `Builds/macOS/HarvestPanic.app` | Succeeded, 0 errors, 6 m 59 s | 161,310,348 B | 154 MB, Universal (x86_64 + arm64) | `HarvestPanic-1.0-macOS-UNSIGNED.zip` **67,811,182 B** (225 entries, 161.3 MB unpacked) |

- `LICENSES.txt` (20,163 B, 334 lines) is written beside `HarvestPanic.exe` and beside `HarvestPanic.app` by
  `HarvestPanic.UI.EditorTools.CreditsBuildStep` (an `IPostprocessBuildWithReport`), so every `BuildScript` method gets
  it without changing `BuildScript`. The macOS and Windows copies are byte-identical. It contains both MIT notices
  in full ("MIT LICENCE - Facepunch.Steamworks" with Copyright (c) 2020 Facepunch Studios LTD, and "MIT LICENCE -
  Netcode for GameObjects community Facepunch transport" with Copyright (c) 2021 Unity Technologies; each with the
  permission paragraph, the "above copyright notice" condition and the AS IS disclaimer). It also has both OFL 1.1 font
  licences in full and every CC0, Steamworks SDK, Unity Companion and Tripo row.
- The Credits screen text inside the players: the pre-build step bakes the same text into the Resources TextAsset
  `Frontend/Generated/Licenses` that `CreditsText.Load()` reads in a player. In both builds the copy inside
  `resources.assets` is **byte-identical to `LICENSES.txt`** (20,163 B, matching length prefix), and the Resources path
  is registered in `globalgamemanagers`. Flow test: PlayMode `CreditsFlowTests` (title → Credits → Back, shows TEAM,
  the Facepunch copyright and the MIT body).
- Release hygiene: there is no `steam_appid.txt` in either release build. The packages exclude
  `*_BurstDebugInformation_DoNotShip`, `steam_appid.txt` and `*.pdb` (0 such entries in either zip). The macOS zip now
  has no AppleDouble `._*` entries: `package-mac.sh` was fixed on this branch, after the first zip carried 224 of
  them. Extracted by a non-Apple unzip, those entries become stray files inside the `.app` and break the sealed
  signature.
- Packaging: `tools/release/package-mac.sh` signs inside-out with `codesign` (hardened runtime,
  `tools/release/macos.entitlements`), then runs `notarytool submit --wait`, `stapler staple`, `spctl` and zips the
  app with `LICENSES.txt`. Without `HP_DEVELOPER_ID` it prints `SKIP:` and produces the `-UNSIGNED` zip above.
  `tools/release/package-windows.sh` fails if `LICENSES.txt` is missing.

Remaining:
- **Owner:** the Apple Developer ID and notary credentials. There is no codesigning identity in the build
  machine's keychain (`security find-identity` returns 0 valid identities).
- **Team (technical-artist):** each player build rewrites two tracked URP assets: `Assets/Settings/HP_URP.asset`
  (`m_PrefilterUseLegacyLightmaps` 0→1, `m_PrefilterReflectionProbeBlending` 1→0, `m_PrefilterReflectionProbeAtlas` 1→0)
  and `UniversalRenderPipelineGlobalSettings.asset` (one extra `rid`). They were reverted here, not committed. Decide
  whether to commit the build-time values, so the tree stays clean after a release build.

## 2. Four players over Steam, cross-play: BLOCKED-ON-OWNER, plus a team fix

Evidence: Steam integration is done in code (`docs/qa/steam-progress.md`): App ID resolver, lobby create/join, overlay
invites, `+connect_lobby`, rich presence and LAN fallback. The game still runs on App ID **480 (Spacewar)**. The LAN
path is covered by the two-process test (item 3). The 8-day bot soak ran on a LAN host with 3 bots, not over Steam.

**New finding (S2, networking): the macOS release player cannot use Steam.** The `.app` contains no native Steam
library (no `libsteam_api` anywhere in the bundle), and the managed assembly it ships is
`Facepunch.Steamworks.Linux.dll`. The community transport package (`com.community.netcode.transport.facepunch` @
`0fab638`) enables both `Facepunch.Steamworks.MacOS.dll` and `.Linux.dll` for OSXUniversal. Its
`redistributable_bin/osx/libsteam_api.bundle` is a single Mach-O file with only **i386 + x86_64** slices, and it did
not make it into the player. Even if it were included, an arm64 process on Apple Silicon could not load it.
`SteamPlatform` catches the failed `SteamClient.Init`, so a Mac player silently falls back to LAN. The Windows build
is fine: it has `Plugins/x86_64/steam_api64.dll` and `Facepunch.Steamworks.Win64.dll`.
Fix (networking): ship a current Steamworks SDK `libsteam_api.dylib` that has an arm64 slice, make the Mac player
include it and only `Facepunch.Steamworks.MacOS.dll`, then verify `SteamClient.IsValid` in a Mac player launched from
Steam.

Remaining (owner): a real Steam App ID with Windows and macOS depots, 2–4 Steam accounts, and a Windows PC. Then a
recorded full-week session that includes Mac ↔ Windows cross-play.

## 3. Test suites: PARTIAL

Run on this branch after the merge of `main` (Unity 6000.3.26f1, batchmode `-nographics`, a random `-netPort`):
- **EditMode: 301 / 301 passed, 0 failed, 0 skipped** (final run on the head that was built).
- **PlayMode: 79 tests, 64 passed, 0 failed, 15 skipped.** Every skip has a reason. Two of them are the "needs a GPU"
  cases (`CamcorderPlayModeTests.Export_WritesFramesSheetThumbAndTape`, `PerfHotspotTests.Muddlebury_Hotspots`). The
  other 13 are QA screenshot captures that need `HP_QA_DIR`/`HP_SHOTS` and a GPU. The long ones also passed here:
  `BotWeekSoakTests` (230 s) and `PerfCaptureTests` (96 s, Null device, so it says nothing about performance).
- Fixed on this branch (test code only):
  1. All 4 `SessionRobustnessTests` failed whenever the runner passed `-netPort`, because `SessionService.Port()`
     prefers that argument over `NetworkingConfig.lanPort` while the test clients dialled the config port. The clients
     now read the port from the host transport. All 4 pass under `-netPort`. Owner to review: networking.
  2. `EconomyHudScreenshots` and `LandlordScreenshots` were `[Explicit]` with no reason, so they showed as skipped with
     an empty message. They now carry a reason.

Gaps:
- `tools/net/two-process-test.sh` (the LAN test) was **not re-run on this head**, because it needs a fresh
  `BuildMacDev` player. Its last recorded PASS is in `docs/qa/steam-progress.md` (extended mode: version reject,
  late join during the Workday, disconnect and rejoin, cap reject, host quit).
- The two GPU-only tests have not been run with a graphics device on this head. Plain `-batchmode` without
  `-nographics` is enough; no window is needed.
Owner: qa-lead / devops.

## 4. Performance: PARTIAL

Evidence (`docs/qa/perf/perf-baseline.md`): Muddlebury, LAN host + 3 bot farmers, 13 critters, Rain + Night, 1080p
offscreen, 60 s. Frame time avg/p95/p99 is **3.78 / 4.34 / 4.98 ms**, against a 16.6 ms budget. The main thread is
3.78 ms, draw calls 508 (≤ 1,500), and memory 1.08 GB (Unity, editor). Hitches: one hitch of 950 ms per run (GC
and the first GPU Resident Drawer upload).

Gaps: the capture machine was an M2 Pro running the **editor**, not a player on "Mac Medium" (base M1). GPU time was **not
measured**. "Never below 30 fps" is **not met** in the capture, because of that single 950 ms hitch. Host network
time (≤ 2 ms/frame) was **not measured**. Next: capture a development player build on an M1 with 4 real clients,
and add a NetworkManager timing marker. Owner: perf / devops.

## 5. Run Save: quit/resume and process kill: MET

The store (`Economy/Shop/RunSave.cs`) writes to a temp file, copies the main save to `.bak`, deletes the main save,
then moves the temp file into place. It checks a CRC32 and refuses saves from future versions. The director saves at
Shop entry, after every Shop purchase and after a paid Sunday, so the `.bak` is never older than the previous Shop.

Tests on this branch, all passing:
- EditMode `HarvestPanic.Tests.EditMode.Economy.RunSaveRobustnessTests`: **14 / 14 passed**. Each of the 7 kill points
  in the write (before; mid-temp-write; after the temp write; mid-backup copy; after the backup copy; after deleting
  the main save; completed) is reproduced as the exact disk state it would leave. The resume always finds the latest
  or the previous Shop save, never loses more than one Shop phase, and the next save cleans up the stale `.tmp`.
  The tests also cover: a main save truncated to about 70 different lengths; a corrupted byte at about 100
  positions; the main save missing with only `.bak` left; both files corrupt (returns null and does not throw); a
  failed write (the previous save survives and the next boundary retries); a two-week sweep tearing every Shop save
  (always resumes the previous day); and a field-by-field round trip.
- PlayMode `HarvestPanic.Tests.PlayMode.Economy.RunSaveResumePlayTests`: **2 / 2 passed**, on a real host session. It
  plays two Shop phases (with a purchase), quits (despawns the director, shuts down the NetworkManager and resets the
  save service), starts a fresh host and presses Continue. A clean quit resumes Shop day 2 with the same balance, the
  upgrade and its effect, and the run continues. With a torn main save it resumes Shop day 1 from `.bak` and keeps
  that day's purchase.

Why the simulated kills are exhaustive: a process kill cannot interrupt the page cache. Every step of `Write` is either
a single atomic file-system operation (`unlink`, a same-volume `rename`) or a write that can stop part-way (the temp
write, the backup copy). The 7 staged states are every state a kill can leave between or inside those steps.

Findings for the economy owner (not fixed here, because this branch only changes tests):
1. (S3) `Load` falls back to `.bak` on *any* failure, including "saved by a newer build". After a downgrade, an older
   build silently resumes the older `.bak`, and its next save overwrites the newer save. Suggest: refuse to continue
   (and refuse to save) when the main save's version is newer than the build.
2. (S4) A complete `.tmp` left by a kill after the main save was deleted is never considered. It is the newest valid
   save. Losing it costs one Shop phase, which is within the DoD, but recovering it would cost nothing.
3. (S4) Nothing is flushed to disk (`fsync`) before the rename, so a **power loss or OS crash** (not a process kill)
   could leave both files empty on some file systems. This is outside the DoD. Suggest `FileStream.Flush(true)` on the
   temp file before the move.
4. (Optional) No OS-level `kill -9` of a player during a write has been run. Doing that needs a dev player hook.

## 6. Join-in-progress, drops, host loss: PARTIAL

Evidence: `tools/net/two-process-test.sh` (extended mode) passes with these cases: version reject, late join
during Workday, disconnect and rejoin, player cap reject, and host quit (clients get the message and return to the
menu) (`docs/qa/steam-progress.md`). PlayMode `SessionRobustnessTests` (4) cover the same ground in-process and pass on
this branch.
Gaps: this has only been tested over LAN or loopback with 2–4 processes. Joining or dropping during Night, the Shop and Sunday
has not been covered, and nothing has run over Steam relay (blocked by item 2).

## 7. Voice / onboarding: BLOCKED-ON-OWNER

Onboarding is done: six first-day tapes cover move, grab, bridge/sell, shout and shop
(`docs/qa/onboarding-progress.md`). EditMode has 14 tests and PlayMode tests cover the tapes. The fallbacks exist:
text chat, shouts with captions, pings and emotes.
The "under 5 minutes" claim still needs a timed human playtest with someone new to the game.
Voice chat: `feature/voice-chat` has one WIP commit (`9c37f31`, not on `main`). It contains
`docs/architecture/adr-voice-chat.md` (Steam voice first, IMA ADPCM PCM fallback over the host relay, text and pings
last), the `IVoiceChat` contract and the pure codec, jitter-buffer and VAD logic. There is no microphone capture,
playback or network wiring yet. The ADR header reads "Accepted for early beta" from network-programmer, but the
owner has not signed off the backend. Its "Steam voice first" path also depends on the macOS Steam fix in item 2.
**Mac blocker:** `ProjectSettings` `microphoneUsageDescription` is empty, and the built `Info.plist` has no
`NSMicrophoneUsageDescription`. macOS will refuse microphone access whatever the entitlement says. The Networking team
owns this setting.

## 8. Crashes, bugs, soak: PARTIAL

Evidence: the bot full-week soak `BotWeekSoakTests` passed (`docs/qa/playtest/bot-week-report.md`). It ran 8 game
days (W1 Mon to W2 Tue) with 0 errors or exceptions and NetworkObjects flat at 24. Unity allocated memory went from
319 to 291 MB (about −9%). The Mono heap grew from 577 to 644 MB (**+11.6%**, editor heap, just over the 10% limit).
The soak passed again on this branch: 0 errors, NetworkObjects 24 → 24, Unity allocated memory 282 → 277 MB, and the
Mono heap 606 → 682 MB (+12.5%). That growth is not monotonic (693 MB on W1D4, 618 MB on W1D5 after a GC), which points
to GC timing rather than a leak, but it needs re-measuring in a player build.
Bug records: `production/qa/bugs/` does not exist, so the bug count is **NOT ASSESSED** (no bug records). The bot
report lists 1 S2 (week-1 rent is out of reach, a balance issue) and 3 S3 issues: goods that never sell, the
prize pumpkin bruised at spawn, and no Landlord barks. None of them is a crash. They need filing with `/bug-report`,
together with the macOS Steam finding (item 2) and the Run Save findings (item 5).
Gaps: the bot soak is a single-process editor run at day scale 0.05. It is not the 30-minute soak with 4 human or
4-process players that the DoD asks for.

## 9. Licences: BLOCKED-ON-OWNER (Tripo rights)

Every pack under `HarvestPanic/Assets/ThirdParty/` (Animals, Characters, Environment, Fonts, Props, Tripo) and
`Assets/Audio` has a row in `THIRD_PARTY_LICENSES.md`. The rows are CC0 (Quaternius, Kenney, Kay Lousberg), OFL 1.1
(Lilita One, Nunito), MIT (Facepunch.Steamworks, the community transport), the Steamworks SDK agreement, and the
Unity Companion License. The Credits screen and `LICENSES.txt` are generated from that file, with the full MIT and
OFL texts (EditMode `CreditsTests` 5/5, PlayMode `CreditsFlowTests` 1/1). Both players ship it, as described in item 1.
Open:
- **Tripo3D models (8 shipped meshes): commercial rights not confirmed.** The file itself says "owner to confirm".
  This is a legal confirmation only the owner can give.
- Housekeeping for the file's owner: the first table still uses Unreal ids and the path `Content/HarvestPanic/ThirdParty/`.
  The Unity copies live in `Assets/ThirdParty/Animals/` and `Props/Quaternius/`. The fonts are duplicated in
  `Assets/ThirdParty/Fonts/` and `Assets/Art/UI/Fonts/`. All of these are CC0 or OFL, so the rights are not affected.
- The team credit line on the Credits screen (`CreditsText.TeamLines`) needs the owner to confirm the names and roles.

## 10. Audio pass 1: PARTIAL

Evidence (`docs/qa/audio-progress.md`, `polish-2-progress.md`): impacts, footsteps, UI and wallet sounds, critter
barks, music states (day/night threat layer, Landlord sting) and ducking are in, with EditMode and PlayMode audio
tests. However, the critter barks, music loops and stingers are **in-house synthesized placeholders**. The DoD says
"pass 1 in", which this satisfies functionally. Final sounds and the mix still need a sign-off from the audio director and the owner.

---

## Owner actions (only the owner can do these)

1. **Steam**: create the Steamworks app and set its **App ID** (to replace 480 in `NetworkingConfig.asset`).
   Create the Windows and macOS depots and a build account. Put 2–4 Steam accounts and a Windows PC on hand for the
   two-account (or more) Mac ↔ Windows cross-play week (item 2), after the team's macOS Steam library fix. Then run
   `tools/release/steam-upload.sh` with `HP_STEAM_APP_ID`, `HP_STEAM_DEPOT_WIN`, `HP_STEAM_DEPOT_MAC` and
   `HP_STEAM_BUILDER` set (it is preview-only by default; `--upload --set-live beta`).
2. **Apple Developer ID** (Developer Program membership) for code signing: export `HP_DEVELOPER_ID="Developer ID
   Application: … (TEAMID)"`. For notarisation, run `xcrun notarytool store-credentials HP_NOTARY …` once and export
   `HP_NOTARY_PROFILE=HP_NOTARY`. Then `tools/release/package-mac.sh` signs, notarises and staples.
3. **Tripo3D commercial rights**: confirm in writing which plan was used and that its terms allow commercial use and
   redistribution of the 8 generated models. The alternative is to fall back to the Quaternius CC0 meshes, which are
   already in the project.
4. **Voice backend sign-off**: approve `docs/architecture/adr-voice-chat.md` (on `feature/voice-chat`) and its
   provider, covering licence, Mac microphone handling and cost. Or decide that the Beta ships with fallbacks only.
   Either way, set `microphoneUsageDescription` (Networking).
5. **Version**: approve `0.9.0` for Early Beta (Networking sets `bundleVersion`). Then tag `v0.9.0` on the commit that is built.
6. **Credits text**: confirm the team credit lines and the "report issues" link used in the release notes.
7. **Playtests**: a 4-human, 30-minute soak (item 8) and a timed onboarding run with someone new to the game (item 7).

## Team actions (no owner input needed)

| Item | Action | Owner |
|---|---|---|
| 1 | Decide on the URP assets that every player build rewrites (`HP_URP.asset` prefilter flags, global settings `rid`) | technical-artist |
| 2 | macOS Steam: ship an arm64-capable `libsteam_api.dylib`, include only `Facepunch.Steamworks.MacOS.dll` in the Mac player, verify `SteamClient.IsValid` in a Mac player | networking |
| 3 | `BuildMacDev` + `tools/net/two-process-test.sh` on the release head; run the two GPU tests without `-nographics`; review the `SessionRobustnessTests` port fix | qa-lead / devops / networking |
| 4 | M1 development-player capture (GPU time, hitch < 33 ms, host network ms) | perf / devops |
| 5 | Version-newer refusal, `.tmp` recovery and an `fsync` before the rename in `RunSaveStore` (findings above) | economy |
| 6 | Join-in-progress during Night, Shop and Sunday; Steam relay pass after the owner provides the App ID | networking |
| 7 | Set `microphoneUsageDescription` | networking |
| 8 | File the bot-report findings and this report's findings as bug records; re-check Mono heap growth in a player build | qa-lead |
| 9 | Fix the Unreal-era table in `THIRD_PARTY_LICENSES.md`; remove the duplicate fonts | release / art |
| 10 | Replace placeholder barks and music | audio |
