# Steam readiness / robustness progress (branch feature/steam-ready)

## Done
- App ID: `SteamAppId` resolves `-steamAppId N` > `steam_appid.txt` (cwd or beside the exe/.app) > `NetworkingConfig.steamAppId` (480 Spacewar, TODO owner).
- Dev builds write `steam_appid.txt` beside the player; release builds do not (Steam supplies it).
- Rich presence: `connect` (host + joiners), `status`, `steam_player_group(_size)`.
- Existing (verified): LAN fallback when Steam absent (menu footer "LAN mode"), lobby create/join/invite overlay,
  `+connect_lobby` launch arg + overlay join, host-quit message, version check, max-player cap.
- NetAutomation: -netMaxPlayers, -netFinalPlayers, -netQuitFile, -netWaitHost, -netRejoin, -netExpectReject, -netFakeVersion.
- `tools/net/two-process-test.sh` extended (NET_EXTENDED=1 default): version reject, late join during Workday, disconnect+rejoin, cap reject, host quit -> clients get message. PASS.
- Tests: EditMode `SteamReadinessTests` (30), PlayMode `SessionRobustnessTests` (4). All pass.
- Builds: BuildMacDev/BuildMacRelease/BuildWindowsDev/BuildWindowsRelease. macOS-dev 402 MB, Windows-dev 252 MB (builds OK).

## Owner gaps
- Real Steamworks App ID + depots; real two-account Steam test (invite overlay, relay join, rich presence).
