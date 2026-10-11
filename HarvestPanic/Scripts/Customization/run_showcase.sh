#!/bin/bash
# Offscreen customization showcase: lineups + wardrobe widget -> production/qa/evidence/customization-*.png
# Usage: run_showcase.sh [MapPath]   (never opens a visible window: -RenderOffscreen)
set -e
HERE="$(cd "$(dirname "$0")/../../.." && pwd)"
MAP="${1:-/Game/ThirdPerson/Lvl_ThirdPerson}"
OUT="$HERE/production/qa/evidence"
mkdir -p "$OUT"
UE=/Applications/UE_5.8/Engine/Binaries/Mac/UnrealEditor.app/Contents/MacOS/UnrealEditor
"$UE" "$HERE/HarvestPanic/HarvestPanic.uproject" "$MAP" -game -RenderOffscreen -ResX=1600 -ResY=900 -windowed \
  -nosound -unattended -nosplash -stdout \
  -ExecCmds="HP.Customization.Showcase $OUT quit"
