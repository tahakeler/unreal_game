#!/usr/bin/env bash
# HARVEST PANIC - macOS release packaging: codesign (hardened runtime) -> notarise -> staple -> zip.
#
# Usage (from the repo root, after BuildMacRelease):
#   tools/release/package-mac.sh [--app PATH] [--out DIR] [--version X.Y.Z] [--dry-run]
#
# The owner must provide (see production/release/early-beta-gate.md, owner actions):
#   HP_DEVELOPER_ID     codesign identity, e.g. "Developer ID Application: Your Name (TEAMID1234)"
#                       (Apple Developer Program membership; certificate installed in the login keychain).
#   Notarisation credentials, either
#     HP_NOTARY_PROFILE a keychain profile created once with:
#                       xcrun notarytool store-credentials HP_NOTARY --apple-id you@x --team-id TEAMID1234 --password <app-specific>
#   or HP_APPLE_ID + HP_TEAM_ID + HP_APP_PASSWORD (app-specific password from appleid.apple.com).
# Optional: HP_ENTITLEMENTS (default tools/release/macos.entitlements).
#
# Without HP_DEVELOPER_ID the script SKIPS signing/notarisation, says so, and still produces an UNSIGNED zip
# (fine for internal LAN tests; Gatekeeper will block it on other Macs). Exit code 0 in that case; non-zero on errors.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
APP="$ROOT/HarvestPanic/Builds/macOS/HarvestPanic.app"
OUT="$ROOT/HarvestPanic/Builds/packages"
VERSION=""
DRY=0
while [[ $# -gt 0 ]]; do
  case "$1" in
    --app) APP="$2"; shift 2;;
    --out) OUT="$2"; shift 2;;
    --version) VERSION="$2"; shift 2;;
    --dry-run) DRY=1; shift;;
    -h|--help) sed -n '2,20p' "$0"; exit 0;;
    *) echo "unknown arg: $1" >&2; exit 2;;
  esac
done
ENT="${HP_ENTITLEMENTS:-$ROOT/tools/release/macos.entitlements}"

run() { echo "+ $*"; if [[ $DRY -eq 0 ]]; then "$@"; fi; }
die() { echo "ERROR: $*" >&2; exit 1; }

[[ -d "$APP" ]] || die "no app at $APP (run BuildScript.BuildMacRelease first)"
BUILD_DIR="$(cd "$(dirname "$APP")" && pwd)"
APP_NAME="$(basename "$APP")"
[[ -z "$VERSION" ]] && VERSION="$(/usr/libexec/PlistBuddy -c 'Print :CFBundleShortVersionString' "$APP/Contents/Info.plist" 2>/dev/null || echo 0.0.0)"
LIC="$BUILD_DIR/LICENSES.txt"
[[ -f "$LIC" ]] || die "LICENSES.txt missing beside $APP_NAME (CreditsBuildStep writes it on every build; rebuild)"
[[ -f "$BUILD_DIR/steam_appid.txt" ]] && echo "WARN: steam_appid.txt beside a release app - it is NOT packaged (dev builds only)."
mkdir -p "$OUT"

if [[ -z "${HP_DEVELOPER_ID:-}" ]]; then
  echo "SKIP: HP_DEVELOPER_ID not set - no codesign / notarisation (owner action: Apple Developer ID)."
  SIGNED="UNSIGNED"
else
  [[ -f "$ENT" ]] || die "entitlements file missing: $ENT"
  security find-identity -v -p codesigning | grep -q "$HP_DEVELOPER_ID" || die "identity not in keychain: $HP_DEVELOPER_ID"
  run xattr -cr "$APP"
  # Inside-out signing (no --deep): nested dylibs/bundles/frameworks first, then the app.
  while IFS= read -r -d '' f; do
    run codesign --force --timestamp --options runtime --entitlements "$ENT" --sign "$HP_DEVELOPER_ID" "$f"
  done < <(find "$APP/Contents" \( -name '*.dylib' -o -name '*.bundle' -o -name '*.so' -o -name '*.framework' \) -print0 | sort -rz)
  run codesign --force --timestamp --options runtime --entitlements "$ENT" --sign "$HP_DEVELOPER_ID" "$APP"
  run codesign --verify --deep --strict --verbose=2 "$APP"

  NOTARY_ARGS=()
  if [[ -n "${HP_NOTARY_PROFILE:-}" ]]; then NOTARY_ARGS=(--keychain-profile "$HP_NOTARY_PROFILE")
  elif [[ -n "${HP_APPLE_ID:-}" && -n "${HP_TEAM_ID:-}" && -n "${HP_APP_PASSWORD:-}" ]]; then
    NOTARY_ARGS=(--apple-id "$HP_APPLE_ID" --team-id "$HP_TEAM_ID" --password "$HP_APP_PASSWORD")
  fi
  if [[ ${#NOTARY_ARGS[@]} -eq 0 ]]; then
    echo "SKIP: notarisation credentials not set (HP_NOTARY_PROFILE or HP_APPLE_ID/HP_TEAM_ID/HP_APP_PASSWORD). Signed only."
    SIGNED="SIGNED-NOT-NOTARISED"
  else
    SUBMIT="$OUT/HarvestPanic-$VERSION-notarize.zip"
    run ditto -c -k --keepParent "$APP" "$SUBMIT"
    run xcrun notarytool submit "$SUBMIT" "${NOTARY_ARGS[@]}" --wait
    run xcrun stapler staple "$APP"
    run xcrun stapler validate "$APP"
    run spctl --assess --type execute --verbose=2 "$APP"
    run rm -f "$SUBMIT"
    SIGNED="NOTARISED"
  fi
fi

# Final archive: the .app + LICENSES.txt (never *_BurstDebugInformation_DoNotShip or steam_appid.txt).
STAGE="$(mktemp -d)"
trap 'rm -rf "$STAGE"' EXIT
run ditto "$APP" "$STAGE/HarvestPanic/$APP_NAME"
run cp "$LIC" "$STAGE/HarvestPanic/LICENSES.txt"
ZIP="$OUT/HarvestPanic-$VERSION-macOS$([[ $SIGNED == NOTARISED ]] || echo "-$SIGNED").zip"
run rm -f "$ZIP"
# --norsrc --noextattr: no AppleDouble "._*" entries (provenance/quarantine xattrs). Unzipped by a non-Apple tool
# they become stray files inside the .app and break the sealed code signature. Signatures/tickets live in files.
run ditto -c -k --norsrc --noextattr --keepParent "$STAGE/HarvestPanic" "$ZIP"
if [[ $DRY -eq 0 ]] && unzip -l "$ZIP" | grep -q '/\._'; then die "AppleDouble ._ entries in $ZIP"; fi
[[ $DRY -eq 0 ]] && shasum -a 256 "$ZIP"
echo "macOS package: $ZIP ($SIGNED)"
echo "Steam depot content root for macOS: $STAGE/HarvestPanic equivalent = folder with $APP_NAME + LICENSES.txt"
