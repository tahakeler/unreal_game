#!/usr/bin/env bash
# HARVEST PANIC - Steam depot upload via steamcmd, from tools/release/steam/app_build.vdf.template.
#
# Usage: tools/release/steam-upload.sh [--upload] [--set-live beta] [--desc "text"]
#   Default is PREVIEW (steamcmd validates the mapping, uploads nothing). --upload does the real upload.
# Inputs:
#   HP_STEAM_APP_ID, HP_STEAM_DEPOT_WIN, HP_STEAM_DEPOT_MAC   (owner action: Steamworks app + depots)
#   HP_STEAM_BUILDER   Steam account with "Edit App Metadata" + "Publish App Changes" (a dedicated build account is best;
#                      first login asks for Steam Guard once, steamcmd then caches the session)
#   Builds: HarvestPanic/Builds/Windows (BuildWindowsRelease) and HarvestPanic/Builds/macOS (BuildMacRelease, then
#           tools/release/package-mac.sh so the .app is signed + notarised + stapled), each with LICENSES.txt.
# Skips with a message (exit 0) when steamcmd or the IDs are missing.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
PREVIEW=1; SET_LIVE=""; DESC="HARVEST PANIC $(git -C "$ROOT" describe --tags --always 2>/dev/null || echo dev)"
while [[ $# -gt 0 ]]; do
  case "$1" in
    --upload) PREVIEW=0; shift;;
    --set-live) SET_LIVE="$2"; shift 2;;
    --desc) DESC="$2"; shift 2;;
    -h|--help) sed -n '2,15p' "$0"; exit 0;;
    *) echo "unknown arg: $1" >&2; exit 2;;
  esac
done
[[ "$SET_LIVE" == "default" ]] && { echo "ERROR: never SetLive default from a script; promote in Steamworks." >&2; exit 2; }
missing=()
for v in HP_STEAM_APP_ID HP_STEAM_DEPOT_WIN HP_STEAM_DEPOT_MAC HP_STEAM_BUILDER; do [[ -n "${!v:-}" ]] || missing+=("$v"); done
if ! command -v steamcmd >/dev/null; then echo "SKIP: steamcmd not installed (brew install --cask steamcmd, or Valve's SDK tools/ContentBuilder)."; exit 0; fi
if [[ ${#missing[@]} -gt 0 ]]; then echo "SKIP: owner must set ${missing[*]} (Steamworks App ID / depots / build account)."; exit 0; fi
[[ "$HP_STEAM_APP_ID" == "480" ]] && { echo "ERROR: 480 is Valve's Spacewar test app; use the real App ID." >&2; exit 2; }

WIN="$ROOT/HarvestPanic/Builds/Windows"; MAC="$ROOT/HarvestPanic/Builds/macOS"
[[ -f "$WIN/HarvestPanic.exe" && -f "$WIN/LICENSES.txt" ]] || { echo "ERROR: Windows release build + LICENSES.txt missing in $WIN" >&2; exit 1; }
[[ -d "$MAC/HarvestPanic.app" && -f "$MAC/LICENSES.txt" ]] || { echo "ERROR: macOS release build + LICENSES.txt missing in $MAC" >&2; exit 1; }
xcrun stapler validate "$MAC/HarvestPanic.app" >/dev/null 2>&1 || echo "WARN: macOS app is not notarised/stapled - Mac players will be blocked by Gatekeeper."

WORK="$ROOT/HarvestPanic/Builds/steam"; rm -rf "$WORK/content"; mkdir -p "$WORK/content/windows" "$WORK/content/macos" "$WORK/output"
rsync -a --exclude='*_BurstDebugInformation_DoNotShip' --exclude='steam_appid.txt' --exclude='*.pdb' "$WIN/" "$WORK/content/windows/"
rsync -a --exclude='*_BurstDebugInformation_DoNotShip' --exclude='steam_appid.txt' "$MAC/HarvestPanic.app" "$MAC/LICENSES.txt" "$WORK/content/macos/"
VDF="$WORK/app_build_$HP_STEAM_APP_ID.vdf"
sed -e "s|__APP_ID__|$HP_STEAM_APP_ID|g" -e "s|__DEPOT_WIN__|$HP_STEAM_DEPOT_WIN|g" -e "s|__DEPOT_MAC__|$HP_STEAM_DEPOT_MAC|g" \
    -e "s|__DESC__|${DESC//|/-}|g" -e "s|__CONTENT__|$WORK/content|g" -e "s|__OUTPUT__|$WORK/output|g" \
    -e "s|__SET_LIVE__|$SET_LIVE|g" -e "s|__PREVIEW__|$PREVIEW|g" "$ROOT/tools/release/steam/app_build.vdf.template" > "$VDF"
grep -q '__[A-Z_]*__' "$VDF" && { echo "ERROR: unfilled placeholder in $VDF" >&2; exit 1; }
echo "steamcmd $( [[ $PREVIEW == 1 ]] && echo PREVIEW || echo UPLOAD ) -> app $HP_STEAM_APP_ID (win $HP_STEAM_DEPOT_WIN, mac $HP_STEAM_DEPOT_MAC) set-live='${SET_LIVE}'"
steamcmd +login "$HP_STEAM_BUILDER" +run_app_build "$VDF" +quit
