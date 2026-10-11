#!/bin/bash
# Headless two-process LAN test: host (listen) + client discovering it via LAN search, both -nullrhi.
# Usage: lan_test.sh [find|direct]   Logs: $OUTDIR/host.log, $OUTDIR/client.log
ROOT="$(cd "$(dirname "$0")/../../.." && pwd)"
MODE="${1:-find}"
OUTDIR="${OUTDIR:-$ROOT/HarvestPanic/Saved/LanTest}"
mkdir -p "$OUTDIR"
UE="/Applications/UE_5.8/Engine/Binaries/Mac/UnrealEditor-Cmd"
COMMON=("$ROOT/HarvestPanic/HarvestPanic.uproject" /Game/HarvestPanic/Frontend/Lvl_MainMenu -game -nullrhi -nosound -unattended -HPForceLAN
  "-ini:Engine:[/Script/EngineSettings.GameMapsSettings]:GameInstanceClass=/Script/HarvestPanic.HPGameInstance"
  "-ini:Engine:[/Script/Engine.Engine]:GameUserSettingsClassName=/Script/HarvestPanic.HPGameUserSettings" -log -stdout)
"$UE" "${COMMON[@]}" -HPAutoHost -HPAutoReady -HPAutoBell -HPAutoChat -HPName=HostFarmer -HPQuitAfter=${HOSTTIME:-75} > "$OUTDIR/host.log" 2>&1 &
HOST=$!
sleep ${JOINDELAY:-20}
if [ "$MODE" = "direct" ]; then JOIN=(-HPAutoJoin=127.0.0.1); else JOIN=(-HPAutoFind); fi
"$UE" "${COMMON[@]}" "${JOIN[@]}" -HPAutoReady -HPName=GuestFarmer -HPQuitAfter=${CLIENTTIME:-50} > "$OUTDIR/client.log" 2>&1 &
CLIENT=$!
wait $CLIENT; wait $HOST
echo "== HOST";   grep -E "HPSession|HPLobby|HPMenu|Browse:|LoadMap:" "$OUTDIR/host.log" | sed 's/^\[[^]]*\]\[[^]]*\]//' | head -40
echo "== CLIENT"; grep -E "HPSession|HPLobby|HPMenu|Browse:|LoadMap:|NetworkFailure|failure" "$OUTDIR/client.log" | sed 's/^\[[^]]*\]\[[^]]*\]//' | head -40
