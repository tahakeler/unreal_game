#!/usr/bin/env bash
# HARVEST PANIC - Windows release packaging (runs on macOS or Linux; the build comes from BuildWindowsRelease).
#
# Usage: tools/release/package-windows.sh [--build DIR] [--out DIR] [--version X.Y.Z] [--dry-run]
#   --build  folder containing HarvestPanic.exe (default HarvestPanic/Builds/Windows)
# Produces HarvestPanic-<version>-Windows.zip with the player, HarvestPanic_Data, UnityPlayer.dll, MonoBleedingEdge,
# steam_api64.dll and LICENSES.txt; excludes *_BurstDebugInformation_DoNotShip, steam_appid.txt and *.pdb.
# Windows code signing (Authenticode) is not done here: Steam does not require it for early beta.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
BUILD="$ROOT/HarvestPanic/Builds/Windows"
OUT="$ROOT/HarvestPanic/Builds/packages"
VERSION="${HP_VERSION:-}"
DRY=0
while [[ $# -gt 0 ]]; do
  case "$1" in
    --build) BUILD="$2"; shift 2;;
    --out) OUT="$2"; shift 2;;
    --version) VERSION="$2"; shift 2;;
    --dry-run) DRY=1; shift;;
    -h|--help) sed -n '2,9p' "$0"; exit 0;;
    *) echo "unknown arg: $1" >&2; exit 2;;
  esac
done
die() { echo "ERROR: $*" >&2; exit 1; }
[[ -f "$BUILD/HarvestPanic.exe" ]] || die "no HarvestPanic.exe in $BUILD (run BuildScript.BuildWindowsRelease)"
for f in UnityPlayer.dll HarvestPanic_Data LICENSES.txt; do [[ -e "$BUILD/$f" ]] || die "missing $f in $BUILD"; done
[[ -n "$VERSION" ]] || VERSION="$(date +%Y%m%d)"
[[ -f "$BUILD/steam_appid.txt" ]] && echo "WARN: steam_appid.txt found (dev build?) - excluded from the package."
find "$BUILD" -maxdepth 4 -name 'steam_api64.dll' | grep -q . || echo "WARN: steam_api64.dll not found - Steam will be unavailable (LAN only)."

ZIP="$OUT/HarvestPanic-$VERSION-Windows.zip"
STAGE="$(mktemp -d)"; trap 'rm -rf "$STAGE"' EXIT
mkdir -p "$OUT" "$STAGE/HarvestPanic"
rsync -a --exclude='*_BurstDebugInformation_DoNotShip' --exclude='steam_appid.txt' --exclude='*.pdb' "$BUILD/" "$STAGE/HarvestPanic/"
if find "$STAGE" -name '*_DoNotShip*' | grep -q .; then die "DoNotShip content leaked into the stage"; fi
echo "Staged $(find "$STAGE/HarvestPanic" -type f | wc -l | tr -d ' ') files, $(du -sh "$STAGE/HarvestPanic" | cut -f1)"
if [[ $DRY -eq 1 ]]; then echo "(dry run) would write $ZIP"; exit 0; fi
rm -f "$ZIP"
(cd "$STAGE" && zip -qr -9 -X "$ZIP" HarvestPanic)
unzip -l "$ZIP" | grep -q 'HarvestPanic/LICENSES.txt' || die "LICENSES.txt not in zip"
shasum -a 256 "$ZIP"
echo "Windows package: $ZIP"
