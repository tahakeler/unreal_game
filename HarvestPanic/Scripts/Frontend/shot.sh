#!/bin/bash
# Usage: shot.sh <map> <out.png> [extra args...]   Offscreen render only (never opens a visible window).
ROOT="$(cd "$(dirname "$0")/../../.." && pwd)"
MAP="$1"; OUT="$2"; shift 2
"/Applications/UE_5.8/Engine/Binaries/Mac/UnrealEditor" "$ROOT/HarvestPanic/HarvestPanic.uproject" "$MAP" -game -RenderOffscreen \
  -ResX=1920 -ResY=1080 -windowed -nosound -unattended -nosplash -HPForceLAN \
  "-ini:Engine:[/Script/EngineSettings.GameMapsSettings]:GameInstanceClass=/Script/HarvestPanic.HPGameInstance" \
  "-ini:Engine:[/Script/Engine.Engine]:GameUserSettingsClassName=/Script/HarvestPanic.HPGameUserSettings" \
  -HPShot="$OUT" -HPShotDelay=${HPDELAY:-8} -HPQuitAfter=${HPQUIT:-60} "$@" -log -stdout 2>&1 | grep -E "HPQA|HPMenu|HPSession|HPLobby|Fatal|Error:" | head -30
