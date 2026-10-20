#!/usr/bin/env bash
# Two-process multiplayer smoke test: headless host + client dev builds over 127.0.0.1.
# Proves: connect + approval, replicated player count = 2, NGO scene switch (Lobby -> Muddlebury; NET_GAME_SCENE=NetTest_Farm for the stub) together.
# Build first: Unity -batchmode -nographics -quit -projectPath HarvestPanic -executeMethod HarvestPanic.Editor.BuildScript.BuildMacDev -logFile /abs/build.log
# Usage: tools/net/two-process-test.sh [/abs/out-dir]   (exit 0 = pass)
set -u
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
APP="$ROOT/HarvestPanic/Builds/macOS-dev/HarvestPanic.app/Contents/MacOS/HARVEST PANIC"
OUT="${1:-$ROOT/HarvestPanic/Logs/nettest}"
PORT="${NET_PORT:-7843}"
mkdir -p "$OUT"; rm -f "$OUT"/*.txt "$OUT"/*.log
[ -x "$APP" ] || { echo "Missing build: $APP"; exit 2; }
COMMON=(-batchmode -nographics -nosteam -netPort "$PORT" -netGameScene "${NET_GAME_SCENE:-Muddlebury}" -netExpectPlayers 2 -netTimeout 90)
"$APP" "${COMMON[@]}" -netHost -netListen 127.0.0.1 -netNoDiscovery -netName "Host Hank" -netResult "$OUT/host.txt" -logFile "$OUT/host.log" &
HOST=$!
sleep 3
"$APP" "${COMMON[@]}" -netJoin "127.0.0.1:$PORT" -netName "Client Cora" -netResult "$OUT/client.txt" -logFile "$OUT/client.log" &
CLIENT=$!
wait $HOST; HC=$?
wait $CLIENT; CC=$?
echo "--- host ($HC)";   cat "$OUT/host.txt" 2>/dev/null
echo "--- client ($CC)"; cat "$OUT/client.txt" 2>/dev/null
# Muddlebury state seen by each peer (SessionStateProbe lines): host has critters+goods+economy, both see 2 farmers with loadouts.
if [ "${NET_GAME_SCENE:-Muddlebury}" = "Muddlebury" ]; then
  grep -E "state role=host critters=[1-9][0-9]* goods=[1-9][0-9]* economy=1 .* farmers=2 loadouts=.*body" "$OUT/host.txt" | tail -1 | sed 's/^/host-state: /'
  grep -E "state role=client critters=[1-9][0-9]* .* farmers=2 loadouts=.*;.*body" "$OUT/client.txt" | tail -1 | sed 's/^/client-state: /'
  grep -qE "state role=host critters=[1-9][0-9]* goods=[1-9][0-9]* economy=1 .* farmers=2 loadouts=.*body" "$OUT/host.txt" || { echo "FAIL: host state"; exit 1; }
  grep -qE "state role=client critters=[1-9][0-9]* .* farmers=2 loadouts=.*;.*body" "$OUT/client.txt" || { echo "FAIL: client does not see host player + loadout"; exit 1; }
fi
if [ $HC -eq 0 ] && [ $CC -eq 0 ] && grep -q "ok=1" "$OUT/host.txt" && grep -q "ok=1" "$OUT/client.txt"; then echo PASS; exit 0; fi
echo FAIL; exit 1
