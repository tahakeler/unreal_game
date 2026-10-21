#!/usr/bin/env bash
# Two-process multiplayer smoke test: headless host + client dev builds over 127.0.0.1.
# Proves: connect + approval, replicated player count = 2, NGO scene switch (Lobby -> Muddlebury; NET_GAME_SCENE=NetTest_Farm for the stub) together.
# Build first: Unity -batchmode -nographics -quit -projectPath HarvestPanic -executeMethod HarvestPanic.Editor.BuildScript.BuildMacDev -logFile /abs/build.log
# Robustness phase (NET_EXTENDED=1, default; set 0 for the plain 2-player run):
#   - a client with a mismatched build version is refused with "different version"
#   - a late joiner ("Lou") joins during the Workday, spawns (3 replicated players), disconnects and rejoins
#   - with the host capped at 3, a 4th client is refused with "farm is full"
#   - the host quits; the remaining clients get the "host went to bed" message and return to the menu
# Proximity voice (always): "Client Cora" runs with -hpVoiceSynth (a 330 Hz tone, never the microphone); the host must
#   receive her uplink over UDP and play it (PlayerStarts are 2 m apart), and with NET_EXTENDED=1 "Late Lou" must hear
#   her through the host relay (HP_VoiceDown).
# Usage: tools/net/two-process-test.sh [/abs/out-dir]   (exit 0 = pass). NET_PORT overrides the (random) port.
set -u
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
APP="$ROOT/HarvestPanic/Builds/macOS-dev/HarvestPanic.app/Contents/MacOS/HARVEST PANIC"
OUT="${1:-$ROOT/HarvestPanic/Logs/nettest}"
PORT="${NET_PORT:-$((20000 + RANDOM % 20000))}"
EXT="${NET_EXTENDED:-1}"
mkdir -p "$OUT"; rm -f "$OUT"/*.txt "$OUT"/*.log "$OUT"/quit
[ -x "$APP" ] || { echo "Missing build: $APP"; exit 2; }
COMMON=(-batchmode -nographics -nosteam -netPort "$PORT" -netGameScene "${NET_GAME_SCENE:-Muddlebury}" -netExpectPlayers 2 -netTimeout 90)
HOSTX=(); CLIX=()
if [ "$EXT" = 1 ]; then
  COMMON=(-batchmode -nographics -nosteam -netPort "$PORT" -netGameScene "${NET_GAME_SCENE:-Muddlebury}" -netTimeout 240)
  HOSTX=(-netExpectPlayers 2 -netMaxPlayers 3 -netFinalPlayers 3 -netQuitFile "$OUT/quit")
  CLIX=(-netExpectPlayers 2 -netWaitHost)
fi
JOIN=(-netJoin "127.0.0.1:$PORT")
# wait_for <file> <regex> <seconds>
wait_for() { local i=0; while [ $i -lt "$3" ]; do grep -qE "$2" "$1" 2>/dev/null && return 0; sleep 1; i=$((i+1)); done; echo "timeout waiting for '$2' in $1"; return 1; }

"$APP" "${COMMON[@]}" "${HOSTX[@]}" -netHost -netListen 127.0.0.1 -netNoDiscovery -netName "Host Hank" -netResult "$OUT/host.txt" -logFile "$OUT/host.log" &
HOST=$!
sleep 3
"$APP" "${COMMON[@]}" "${CLIX[@]}" "${JOIN[@]}" -hpVoiceSynth -netName "Client Cora" -netResult "$OUT/client.txt" -logFile "$OUT/client.log" &
CLIENT=$!
EC=0
if [ "$EXT" = 1 ]; then
  "$APP" -batchmode -nographics -nosteam -netTimeout 60 "${JOIN[@]}" -netFakeVersion 0.0.0-old -netExpectReject "different version" -netName "Old Olga" -netResult "$OUT/oldver.txt" -logFile "$OUT/oldver.log"; VC=$?
  wait_for "$OUT/host.txt" "serverSceneLoaded=${NET_GAME_SCENE:-Muddlebury}" 150
  "$APP" "${COMMON[@]}" "${JOIN[@]}" -netExpectPlayers 3 -netRejoin -netWaitHost -netName "Late Lou" -netResult "$OUT/late.txt" -logFile "$OUT/late.log" &
  LATE=$!
  wait_for "$OUT/late.txt" "rejoin ok=1" 180
  "$APP" -batchmode -nographics -nosteam -netTimeout 60 "${JOIN[@]}" -netExpectReject "full" -netName "Fourth Fay" -netResult "$OUT/full.txt" -logFile "$OUT/full.log"; FC=$?
  touch "$OUT/quit"
  wait $LATE; LC=$?
  for f in oldver full late; do echo "--- $f"; cat "$OUT/$f.txt" 2>/dev/null; done
  [ $VC -eq 0 ] && grep -q "ok=1 rejected reason=.*different_version" "$OUT/oldver.txt" || { echo "FAIL: version mismatch not rejected"; EC=1; }
  [ $FC -eq 0 ] && grep -q "ok=1 rejected reason=.*full" "$OUT/full.txt" || { echo "FAIL: 4th player over cap not rejected"; EC=1; }
  [ $LC -eq 0 ] && grep -q "^.* ok=1 .*replicated=3" "$OUT/late.txt" || { echo "FAIL: late join during Workday"; EC=1; }
  grep -q "rejoin ok=1 players=3 replicated=3" "$OUT/late.txt" || { echo "FAIL: disconnect/rejoin"; EC=1; }
  grep -q "roster n=2" <(sed -n '/roster n=3/,$p' "$OUT/host.txt") || { echo "FAIL: host roster did not drop on disconnect"; EC=1; }
  grep -q "hostLeft reason=The_host_went_to_bed" "$OUT/late.txt" || { echo "FAIL: late client did not get host-quit message"; EC=1; }
fi
wait $HOST; HC=$?
wait $CLIENT; CC=$?
[ "$EXT" = 1 ] && { grep -q "hostLeft reason=The_host_went_to_bed" "$OUT/client.txt" || { echo "FAIL: client did not get host-quit message"; EC=1; }; }
# Proximity voice over real UDP between player processes (feature/voice-chat).
grep -E "\[Voice\] (uplink|heard) speaker=" "$OUT/host.log" | head -2 | sed 's/^/voice-host: /'
grep -qE "\[Voice\] uplink speaker=[1-9][0-9]* frames=25 bytes=131 codec=Adpcm3" "$OUT/host.log" || { echo "FAIL: host did not receive client voice"; EC=1; }
grep -qE "\[Voice\] heard speaker=[1-9][0-9]* " "$OUT/host.log" || { echo "FAIL: host did not play client voice"; EC=1; }
if [ "$EXT" = 1 ]; then
  grep -E "\[Voice\] heard speaker=" "$OUT/late.log" | head -1 | sed 's/^/voice-late: /'
  grep -qE "\[Voice\] heard speaker=[1-9][0-9]* " "$OUT/late.log" || { echo "FAIL: late joiner did not hear relayed voice"; EC=1; }
fi
echo "--- host ($HC)";   cat "$OUT/host.txt" 2>/dev/null
echo "--- client ($CC)"; cat "$OUT/client.txt" 2>/dev/null
# Muddlebury state seen by each peer (SessionStateProbe lines): host has critters+goods+economy, both see 2 farmers with loadouts.
if [ "${NET_GAME_SCENE:-Muddlebury}" = "Muddlebury" ]; then
  grep -E "state role=host critters=[1-9][0-9]* goods=[1-9][0-9]* economy=1 .* farmers=2 loadouts=.*body" "$OUT/host.txt" | tail -1 | sed 's/^/host-state: /'
  grep -E "state role=client critters=[1-9][0-9]* .* farmers=2 loadouts=.*;.*body" "$OUT/client.txt" | tail -1 | sed 's/^/client-state: /'
  grep -qE "state role=host critters=[1-9][0-9]* goods=[1-9][0-9]* economy=1 .* farmers=2 loadouts=.*body" "$OUT/host.txt" || { echo "FAIL: host state"; exit 1; }
  grep -qE "state role=client critters=[1-9][0-9]* .* farmers=2 loadouts=.*;.*body" "$OUT/client.txt" || { echo "FAIL: client does not see host player + loadout"; exit 1; }
fi
if [ $EC -eq 0 ] && [ $HC -eq 0 ] && [ $CC -eq 0 ] && grep -q "ok=1" "$OUT/host.txt" && grep -q "ok=1" "$OUT/client.txt"; then echo PASS; exit 0; fi
echo FAIL; exit 1
