# Early Beta Gate — HARVEST PANIC

Gate: GDD `design/master-gdd/18-production.md` §18.7 *Definition of Done — Early Beta* (items 1–10).
Assessed: 2026-10-21 by release-manager on `feature/release-readiness`, which includes `origin/main` @ `dec81b4`
(merged on this branch; `main` carries the perf capture, hotspot and bot-week soak work from `feature/perf-playtest`).
Every test count, build size and file check below was produced on this branch after that merge.
Proposed version: **0.9.0**. `ProjectSettings` `bundleVersion` is still `1.0`, and there are no git tags yet (see Owner actions).

Legend: **MET** means the evidence on disk satisfies the item. **PARTIAL** means part of the work is done and the
rest is team work that can be finished without the owner. **BLOCKED-ON-OWNER** means the item cannot close without
something only the owner can supply: an account, a credential, a legal confirmation or a sign-off.

## Verdict: NOT READY. 1 MET, 6 PARTIAL, 3 BLOCKED-ON-OWNER

| # | DoD item | Status |
|---|---|---|
| 1 | macOS (signed + notarised) and Windows builds from batchmode, no manual steps | BLOCKED-ON-OWNER |
| 2 | 4 players, full week over **Steam**, Mac ↔ Windows cross-play | BLOCKED-ON-OWNER |
| 3 | All EditMode + PlayMode pass; two-process passes; no unexplained skips | <<S3>> |
| 4 | 60 fps Mac Medium at 4 players, never < 30, host net ≤ 2 ms/frame | PARTIAL |
| 5 | Run Save survives quit/resume and process-kill | <<S5>> |
| 6 | Join-in-progress, client drop without soft-lock; host loss → lobby | PARTIAL |
| 7 | Voice chat or fallbacks usable; onboarding teaches grab/carry/sell < 5 min | BLOCKED-ON-OWNER |
| 8 | No known crash; 0 open P0/P1; 30-min 4-player soak, memory growth ≤ 10% | PARTIAL |
| 9 | Every asset in `THIRD_PARTY_LICENSES.md` with confirmed commercial rights | PARTIAL (Tripo row needs the owner) |
| 10 | Audio pass 1: impacts, critter barks, UI, music states | PARTIAL |

---

## 1. Builds from batchmode: BLOCKED-ON-OWNER (signing)

Evidence:
<<B1>>
- Packaging (this branch): `tools/release/package-mac.sh` handles inside-out `codesign` with hardened runtime and
  `tools/release/macos.entitlements` (JIT, unsigned executable memory, library validation off for the Steam dylib,
  and `audio-input` for voice), then `notarytool submit --wait`, `stapler staple`, `spctl` and a zip that includes
  `LICENSES.txt`. Without `HP_DEVELOPER_ID` it prints `SKIP:` and produces an `-UNSIGNED` zip. `tools/release/package-windows.sh` zips the release folder, leaving out
  `*_BurstDebugInformation_DoNotShip`, `steam_appid.txt` and `*.pdb`, and fails if `LICENSES.txt` is missing.
- `LICENSES.txt` beside the player is written by `HarvestPanic.UI.EditorTools.CreditsBuildStep` (an
  `IPostprocessBuildWithReport`) on every build, so every `BuildScript` method gets it without changing `BuildScript`.

Remaining: the Apple Developer ID and notary credentials. There is no codesigning identity in the build machine's
keychain (`security find-identity` returns 0 valid identities).

## 2. Four players over Steam, cross-play: BLOCKED-ON-OWNER

Evidence: Steam integration is done in code (`docs/qa/steam-progress.md`): App ID resolver, lobby create/join, overlay
invites, `+connect_lobby`, rich presence and LAN fallback. The game still runs on App ID **480 (Spacewar)**; these are the only
3 `TODO` markers in 284 scanned `.cs` files (`Networking/SteamAppId.cs`, `Networking/NetworkingConfig.cs` ×2). No
Steam session with 2 or more real accounts has been run, and no Mac ↔ Windows session has been run. The LAN path is covered by the
two-process test (item 3). The 8-day bot soak ran on a LAN host with 3 bots, not over Steam.

Remaining: a real Steam App ID with Windows and macOS depots, 2–4 Steam accounts, and a Windows PC. Then a recorded full-week
session that includes cross-play.

## 3. Test suites: <<S3>>

<<T3>>

## 4. Performance: PARTIAL

Evidence (`docs/qa/perf/perf-baseline.md`): Muddlebury, LAN host + 3 bot farmers, 13 critters, Rain + Night, 1080p
offscreen, 60 s. Frame time avg/p95/p99 is **3.78 / 4.34 / 4.98 ms**, against a 16.6 ms budget. The main thread is
3.78 ms, draw calls 508 (≤ 1,500), and memory 1.08 GB (Unity, editor). Hitches: one hitch of 950 ms per run (GC
and the first GPU Resident Drawer upload).

Gaps: the capture machine was an M2 Pro running the **editor**, not a player on "Mac Medium" (base M1). GPU time was **not
measured**. "Never below 30 fps" is **not met** in the capture, because of that single 950 ms hitch. Host network
time (≤ 2 ms/frame) was **not measured**. Next: capture a development player build on an M1 with 4 real clients,
and add a NetworkManager timing marker. Owner: perf / devops.

## 5. Run Save: quit/resume and process kill: <<S5>>

The store (`Economy/Shop/RunSave.cs`) writes to a temp file, copies the main save to `.bak`, deletes the main save,
then moves the temp file into place. It checks a CRC32 and refuses saves from future versions.
New tests on this branch:
- EditMode `HarvestPanic.Tests.EditMode.Economy.RunSaveRobustnessTests` (14). Each of the 7 kill points in the write
  (before; mid-temp-write; after the temp write; mid-backup copy; after the backup copy; after deleting the main
  save; completed) is reproduced as the exact disk state it would leave. The resume always finds the latest or the
  previous Shop save, never loses more than one Shop phase, and the next save cleans up the stale `.tmp`.
  The tests also cover: a main save truncated to about 70 different lengths; a corrupted byte at about 100
  positions; the main save missing with only `.bak` left; both files corrupt (returns null and does not throw); a
  failed write (the previous save survives and the next boundary retries); a two-week sweep tearing every Shop save
  (always resumes the previous day); and a field-by-field round trip.
- PlayMode `HarvestPanic.Tests.PlayMode.Economy.RunSaveResumePlayTests` (2), on a real host session. It plays two
  Shop phases (with a purchase), quits (despawns the director, shuts down the NetworkManager and resets the save
  service), starts a fresh host and presses Continue. A clean quit resumes Shop day 2 with the same balance, the
  upgrade and its effect, and the run continues. With a torn main save it resumes Shop day 1 from `.bak` and keeps
  that day's purchase.
<<R5>>

Findings for the economy owner (S3 each; not fixed here because this branch only changes tests):
1. `Load` falls back to `.bak` on *any* failure, including "saved by a newer build". After a downgrade, an older build
   silently resumes the older `.bak`, and its next save overwrites the newer save. Suggest: refuse to continue (and
   refuse to save) when the main save's version is newer than the build.
2. A complete `.tmp` left by a kill after the main save was deleted is never considered. It is the newest valid save.
   Losing it costs one Shop phase, which is within the DoD, but recovering it would cost nothing.
3. The test kills are simulated as disk states. No OS-level `kill -9` of a player during a write has been run. Doing
   that needs a dev player hook, so this is optional.

## 6. Join-in-progress, drops, host loss: PARTIAL

Evidence: `tools/net/two-process-test.sh` (extended mode) passes with these cases: version reject, late join
during Workday, disconnect and rejoin, player cap reject, and host quit (clients get the message and return to the
menu). PlayMode `SessionRobustnessTests` (4) cover the same ground (`docs/qa/steam-progress.md`).
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
owner has not signed off the backend.
**Mac blocker:** `ProjectSettings` `microphoneUsageDescription` is empty. macOS will refuse microphone access
(`NSMicrophoneUsageDescription`) whatever the entitlement says. The Networking team owns this setting.

## 8. Crashes, bugs, soak: PARTIAL

Evidence: the bot full-week soak `BotWeekSoakTests` passed (`docs/qa/playtest/bot-week-report.md`). It ran 8 game
days (W1 Mon to W2 Tue) with 0 errors or exceptions and NetworkObjects flat at 24. Unity allocated memory went from
319 to 291 MB (about −9%). The Mono heap grew from 577 to 644 MB (**+11.6%**, editor heap, just over the 10% limit;
it needs re-measuring in a player build).
Bug records: `production/qa/bugs/` does not exist, so the bug count is **NOT ASSESSED** (no bug records). The bot
report lists 1 S2 (week-1 rent is out of reach, a balance issue) and 3 S3 issues: goods that never sell, the
prize pumpkin bruised at spawn, and no Landlord barks. None of them is a crash. They need filing with `/bug-report`.
Gaps: the bot soak is a single-process editor run at day scale 0.05. It is not the 30-minute soak with 4 human or
4-process players that the DoD asks for.

## 9. Licences: PARTIAL (Tripo row needs the owner)

Every pack under `HarvestPanic/Assets/ThirdParty/` (Animals, Characters, Environment, Fonts, Props, Tripo) and
`Assets/Audio` has a row in `THIRD_PARTY_LICENSES.md`. The rows are CC0 (Quaternius, Kenney, Kay Lousberg), OFL 1.1
(Lilita One, Nunito), MIT (Facepunch.Steamworks, the community transport), the Steamworks SDK agreement, and the
Unity Companion License. The Credits screen and `LICENSES.txt` are generated from that file, with the full MIT and
OFL texts (EditMode `CreditsTests` 5, PlayMode `CreditsFlowTests` 1).
Open:
- **Tripo3D models (8 shipped meshes): commercial rights not confirmed.** The file itself says "owner to confirm".
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
   cross-play week (item 2). Then run `tools/release/steam-upload.sh` with `HP_STEAM_APP_ID`, `HP_STEAM_DEPOT_WIN`,
   `HP_STEAM_DEPOT_MAC` and `HP_STEAM_BUILDER` set (it is preview-only by default; `--upload --set-live beta`).
2. **Apple Developer ID** (Developer Program membership) for code signing: export `HP_DEVELOPER_ID="Developer ID
   Application: … (TEAMID)"`. For notarisation, run `xcrun notarytool store-credentials HP_NOTARY …` once and export
   `HP_NOTARY_PROFILE=HP_NOTARY`.
3. **Tripo3D commercial rights**: confirm in writing which plan was used and that its terms allow commercial use and
   redistribution of the 8 generated models. The alternative is to fall back to the Quaternius CC0 meshes, which are
   already in the project.
4. **Voice backend sign-off**: approve the voice ADR and its provider (licence, Mac microphone handling, cost), or
   decide that the Beta ships with fallbacks only. Either way, set `microphoneUsageDescription` (Networking).
5. **Version**: approve `0.9.0` for Early Beta (Networking sets `bundleVersion`). Then tag `v0.9.0` on the commit that is built.
6. **Credits text**: confirm the team credit lines and the "report issues" link used in the release notes.
7. **Playtests**: a 4-human, 30-minute soak (item 8) and a timed onboarding run with someone new to the game (item 7).

## Team actions (no owner input needed)

| Item | Action | Owner |
|---|---|---|
| 3 | Keep suites green on integration; document every skip | qa-lead |
| 4 | M1 development-player capture (GPU time, hitch < 33 ms, host network ms) | perf / devops |
| 5 | Version-newer refusal and `.tmp` recovery in `RunSaveStore` (findings above) | economy |
| 6 | Join-in-progress during Night, Shop and Sunday; Steam relay pass after the owner provides the App ID | networking |
| 8 | File the bot-report findings as bug records; re-check Mono heap growth in a player build | qa-lead |
| 9 | Fix the Unreal-era table in `THIRD_PARTY_LICENSES.md`; remove the duplicate fonts | release / art |
| 10 | Replace placeholder barks and music | audio |
