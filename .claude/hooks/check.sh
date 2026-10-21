#!/usr/bin/env bash
# Stop hook (GOAT router test gate): don't let the agent finish while the tests fail (one extra pass per turn).
# Works for Claude Code and Codex. Adapted for Unity: runs the EditMode suite (project.yaml `test`), but only
# when game code or assets under HarvestPanic/ changed in this checkout, and never while the Unity Editor has
# the project open (Unity allows one process per project).
input=$(cat)
[ "$(jq -r '.stop_hook_active // false' <<<"$input")" = "true" ] && exit 0

root="${CLAUDE_PROJECT_DIR:-$(git rev-parse --show-toplevel 2>/dev/null)}"
cd "$root" || exit 0

# Only gate when something under the Unity project changed (tracked edits or new files).
changed=$(git status --porcelain -- HarvestPanic/Assets HarvestPanic/Packages HarvestPanic/ProjectSettings 2>/dev/null \
  | grep -v 'InitTestScene' | grep -v 'ProjectSettings/Packages/' | head -1)
[ -z "$changed" ] && exit 0

# Skip (don't block) if another Unity process already has this project open.
if ps -axo command | grep -i 'Unity.app/Contents/MacOS/Unity' | grep -v grep | grep -qiF "$root/HarvestPanic"; then
  exit 0
fi

unity="/Applications/Unity/Hub/Editor/6000.3.26f1/Unity.app/Contents/MacOS/Unity"
[ -x "$unity" ] || exit 0
mkdir -p "$root/Logs"
xml="$root/Logs/stop-gate-editmode.xml"
rm -f "$xml"
"$unity" -batchmode -nographics -projectPath "$root/HarvestPanic" -runTests -testPlatform EditMode \
  -testResults "$xml" -logFile "$root/Logs/stop-gate-editmode.log" >/dev/null 2>&1

if [ ! -f "$xml" ]; then
  out="No test results (compile error?). Last log lines:
$(grep -E 'error CS|Error|Exception' "$root/Logs/stop-gate-editmode.log" | tail -n 20)"
elif grep -q 'result="Failed"' "$xml"; then
  out="$(grep -o '<test-case [^>]*result="Failed"[^>]*' "$xml" | grep -o 'fullname="[^"]*"' | head -n 20)"
else
  exit 0
fi

jq -n --arg log "$(tail -n 40 <<<"$out")" \
  '{decision: "block", reason: ("EditMode tests are failing. Fix them before you stop.\n" + $log)}'
exit 0
