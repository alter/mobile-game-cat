#!/usr/bin/env bash
# Headless build entry point — task 60-shell-build/13-headless-build.
#
# Stages, in order: core-purity check, C# tests, Python tests, coverage gate
# (task 20-rules-core/05-coverage), then the Unity builds (Android APK, iOS
# Xcode project), then the signing/.ipa stage.
#
# The Android .aab (BuildScript.BuildAndroidBundle) is deliberately NOT built
# here: this script's job, stated above, is one APK for local install plus
# the iOS project — a second full Android build just to produce a Play
# bundle nobody uploads from a headless dev run would double this stage's
# time for no reader of this script. The .aab matters once there is
# somewhere to upload it, at 90-android/13-internal-testing (needs the Play
# account) — that is where a build-and-check-the-bundle stage belongs.
# build/check-android-manifest.py itself is not APK-specific: it takes any
# .apk path, including a universal one bundletool builds from an .aab, so
# reuse it there rather than writing a second check.
#
# Signing is not possible right now: there is no Apple Developer Program
# account and no team ID (tasks/DECISIONS.md, decision D17). This script does
# not fake a signed .ipa. Unless --no-sign is passed, it reaches the signing
# stage and exits non-zero, naming the missing APPLE_TEAM_ID and the task
# that closes this (60-shell-build/14-testflight).
#
# Usage: build/headless-build.sh [--tests-only] [--no-sign] [-h|--help]
#   --tests-only  run core-purity + C# tests + Python tests + coverage gate,
#                 skip both Unity build stages and the signing stage entirely.
#   --no-sign     run everything including the Unity builds, but skip the
#                 signing/.ipa stage instead of failing on it.
#   -h, --help    print this usage line and exit 0.
set -euo pipefail

usage() {
  echo "Usage: $0 [--tests-only] [--no-sign] [-h|--help]"
  echo "  --tests-only  run only core-purity, C# tests, Python tests, coverage gate"
  echo "  --no-sign     run the Unity builds but skip the signing/.ipa stage"
  echo "  -h, --help    show this message"
}

TESTS_ONLY=0
NO_SIGN=0
for arg in "$@"; do
  case "$arg" in
    --tests-only) TESTS_ONLY=1 ;;
    --no-sign) NO_SIGN=1 ;;
    -h|--help) usage; exit 0 ;;
    *) echo "unknown argument: $arg" >&2; usage >&2; exit 1 ;;
  esac
done

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

STAGE="(none)"
on_error() {
  local code=$?
  echo "" >&2
  echo "== STAGE FAILED: ${STAGE} (exit ${code}) ==" >&2
  exit "$code"
}
trap on_error ERR

stage() {
  STAGE="$1"
  echo ""
  echo "== STAGE: ${STAGE} =="
}

# Run a dotnet command, retrying once after 30s if it hits the file-lock
# errors dotnet throws when another process is also building (MSB3021,
# MSB3027, "being used by another process").
run_dotnet() {
  local out
  if out=$("$@" 2>&1); then
    echo "$out"
    return 0
  fi
  if echo "$out" | grep -qE "MSB3021|MSB3027|being used by another process"; then
    echo "$out"
    echo "[headless-build] dotnet hit a file lock, waiting 30s and retrying once..." >&2
    sleep 30
    "$@"
    return $?
  fi
  echo "$out"
  return 1
}

# ---------------------------------------------------------------------------
stage "core-purity check"
build/check-core-purity.sh

# ---------------------------------------------------------------------------
stage "C# tests (dotnet test, Core)"
rm -rf "$ROOT/TestResults"
run_dotnet dotnet test build/core-tests/core-tests.csproj \
  --nologo \
  --settings build/core-tests/coverage.runsettings \
  --results-directory TestResults

# ---------------------------------------------------------------------------
stage "Python tests (pytest, tools/)"
PYTEST_BIN="${PYTHON:-python3}"
if [ -z "${PYTHON:-}" ] && [ -x "$ROOT/.venv/bin/python3" ]; then
  PYTEST_BIN="$ROOT/.venv/bin/python3"
fi

# A clean checkout has no .venv, and this falls back to the system python —
# which on this machine cannot do either job: no pytest, and a pyexpat that
# cannot parse the cobertura report the coverage gate reads. Both would fail
# further down with an error about something else entirely. The OUTCOME of
# 60-shell-build/13 says "from a clean checkout", so say plainly what is
# missing and how to fix it, before spending a test run finding out.
if ! "$PYTEST_BIN" -c "import pytest, xml.parsers.expat" >/dev/null 2>&1; then
  echo "" >&2
  echo "$PYTEST_BIN cannot run this stage: it needs pytest and a working" >&2
  echo "xml.parsers.expat (the coverage gate below parses cobertura XML)." >&2
  echo "" >&2
  echo "  python3 -m venv .venv && .venv/bin/pip install -r requirements.txt" >&2
  echo "" >&2
  echo "or point the script at an interpreter that has them:" >&2
  echo "" >&2
  echo "  PYTHON=/path/to/python3 $0 $*" >&2
  exit 1
fi

"$PYTEST_BIN" -m pytest tools/tests -q

# ---------------------------------------------------------------------------
stage "coverage gate (>= 90% on Core, task 20-rules-core/05-coverage)"
"$PYTEST_BIN" build/coverage-summary.py --min 90

# ---------------------------------------------------------------------------
stage "Android photo rotation check (tools/tests/android-photo, task 60-shell-build/24)"
# Same JDK the rest of the Android build path uses (see the Unity Android
# build stage and tools/tests/android-vision/run.sh's JAVA_HOME default).
# Needs only javac/java, runs in about a second — no excuse to skip it if
# that JDK is there, so a missing JDK is a named skip, not a silent one.
ANDROID_JDK_BIN="/Applications/Unity/Hub/Editor/6000.3.22f1/PlaybackEngines/AndroidPlayer/OpenJDK/bin"
if [ -x "$ANDROID_JDK_BIN/javac" ]; then
  PATH="$ANDROID_JDK_BIN:$PATH" tools/tests/android-photo/run.sh
else
  echo "No JDK at $ANDROID_JDK_BIN (Unity's Android player JDK) —" >&2
  echo "skipping tools/tests/android-photo. Run it by hand once a JDK is" >&2
  echo "there: tools/tests/android-photo/run.sh" >&2
fi

# ---------------------------------------------------------------------------
stage "Android vision check availability (tools/tests/android-vision, task 60-shell-build/24) — optional"
# Optional on purpose: the real run needs an emulator with Play services,
# installs an APK, and takes minutes — not a build's job. This stage only
# says, honestly, whether that run is possible right now, instead of the
# silent nothing it got before this task.
ANDROID_ADB="${ANDROID_HOME:-/Applications/Unity/Hub/Editor/6000.3.22f1/PlaybackEngines/AndroidPlayer/SDK}/platform-tools/adb"
if [ ! -x "$ANDROID_ADB" ]; then
  echo "No adb at $ANDROID_ADB — skipping tools/tests/android-vision." >&2
  echo "Run it by hand once the Android SDK is there: tools/tests/android-vision/run.sh" >&2
elif ! "$ANDROID_ADB" devices | grep -qE $'\tdevice$'; then
  echo "adb sees no device or emulator — skipping tools/tests/android-vision." >&2
  echo "Run it by hand once one is attached: tools/tests/android-vision/run.sh" >&2
else
  echo "adb sees a device — tools/tests/android-vision/run.sh was NOT run" >&2
  echo "automatically (it installs an APK and takes minutes; not this stage's" >&2
  echo "job). Run it by hand: tools/tests/android-vision/run.sh" >&2
fi

# ---------------------------------------------------------------------------
stage "Swift plugins parse (game/Assets/Plugins/iOS/*.swift, task 60-shell-build/24)"
# swiftc -parse catches syntax errors from a source read alone — it does not
# resolve imports (UIKit/Vision/PhotosUI are iOS-only and unavailable to a
# macOS-hosted compile), so no SDK flag or target triple is needed and there
# is nothing here to link. Real logic checks are a separate decision — see
# NOTES.md in tasks/60-shell-build/24-checks-wired.
shopt -s nullglob
SWIFT_FILES=("$ROOT"/game/Assets/Plugins/iOS/*.swift)
shopt -u nullglob
if [ "${#SWIFT_FILES[@]}" -eq 0 ]; then
  echo "No files matched game/Assets/Plugins/iOS/*.swift — nothing to parse." >&2
elif ! xcrun --find swiftc >/dev/null 2>&1; then
  echo "No swiftc via xcrun on this machine — skipping the Swift parse" >&2
  echo "check. See NOTES.md in tasks/60-shell-build/24-checks-wired." >&2
else
  for f in "${SWIFT_FILES[@]}"; do
    echo "swiftc -parse: $f"
    xcrun swiftc -parse "$f"
  done
fi

if [ "$TESTS_ONLY" -eq 1 ]; then
  echo ""
  echo "== --tests-only: skipping Unity build stages and the signing stage =="
  exit 0
fi

# ---------------------------------------------------------------------------
stage "locate Unity editor"
if [ -n "${UNITY_PATH:-}" ]; then
  UNITY="$UNITY_PATH"
else
  # Newest installed editor by version-sorted directory name.
  shopt -s nullglob
  candidates=(/Applications/Unity/Hub/Editor/*/Unity.app/Contents/MacOS/Unity)
  shopt -u nullglob
  if [ "${#candidates[@]}" -eq 0 ]; then
    echo "No Unity editor found under /Applications/Unity/Hub/Editor/*/Unity.app," >&2
    echo "and \$UNITY_PATH is not set. Install Unity via Unity Hub, or set" >&2
    echo "UNITY_PATH=/path/to/Unity.app/Contents/MacOS/Unity" >&2
    exit 1
  fi
  IFS=$'\n' sorted=($(printf '%s\n' "${candidates[@]}" | sort -V))
  unset IFS
  UNITY="${sorted[-1]}"
fi
if [ ! -x "$UNITY" ]; then
  echo "Unity editor not executable at: $UNITY" >&2
  exit 1
fi
echo "Using Unity editor: $UNITY"

# ---------------------------------------------------------------------------
stage "Unity Android build (BuildScript.BuildAndroidPlayer)"
APK="$ROOT/game/build/android/CatShelter.apk"
rm -f "$APK"
"$UNITY" -batchmode -nographics -quit -projectPath "$ROOT/game" \
  -executeMethod BuildScript.BuildAndroidPlayer \
  -logFile "$ROOT/game/build/android-build.log"
if [ ! -f "$APK" ]; then
  echo "Android build reported success but the expected .apk is missing:" >&2
  echo "  $APK" >&2
  echo "See log: $ROOT/game/build/android-build.log" >&2
  exit 1
fi
echo "Android APK: $APK ($(du -h "$APK" | cut -f1))"

# ---------------------------------------------------------------------------
stage "Android manifest carries what every integrated package needs (task 90-android/02)"
# "The build succeeded" and "the file exists" (the check just above) are both
# blind to the 2026-08-27 defect: BuildTarget.Android was passed to
# BuildPlayer without making Android the active target, so the notification
# package's editor callback never ran, and the APK shipped with the Java
# classes but neither the permission nor the receiver they need. This reads
# the built APK's manifest with aapt2 and fails the whole build if it is
# missing anything a currently-integrated package (game/Packages/manifest.json)
# is known to inject.
"$PYTEST_BIN" build/check-android-manifest.py --apk "$APK"

# ---------------------------------------------------------------------------
stage "Unity iOS Xcode project (BuildScript.BuildIOSXcodeProject)"
IOS_PROJECT="$ROOT/game/build/ios/CatShelter/Unity-iPhone.xcodeproj"
rm -rf "$IOS_PROJECT"
"$UNITY" -batchmode -nographics -quit -projectPath "$ROOT/game" \
  -executeMethod BuildScript.BuildIOSXcodeProject \
  -logFile "$ROOT/game/build/ios-build.log"
if [ ! -d "$IOS_PROJECT" ]; then
  echo "iOS build reported success but the expected Xcode project is missing:" >&2
  echo "  $IOS_PROJECT" >&2
  echo "See log: $ROOT/game/build/ios-build.log" >&2
  exit 1
fi
echo "iOS Xcode project: $IOS_PROJECT"

# ---------------------------------------------------------------------------
if [ "$NO_SIGN" -eq 1 ]; then
  echo ""
  echo "== --no-sign: skipping the signing/.ipa stage =="
  exit 0
fi

stage "sign and produce .ipa"
if [ -z "${APPLE_TEAM_ID:-}" ]; then
  echo "" >&2
  echo "Cannot produce a signed .ipa: APPLE_TEAM_ID is not set." >&2
  echo "There is no Apple Developer Program account and no team ID yet —" >&2
  echo "see tasks/DECISIONS.md, decision D17. Closing this is the job of" >&2
  echo "task 60-shell-build/14-testflight, not this script." >&2
  echo "Run with --no-sign to build without attempting to sign, or set" >&2
  echo "APPLE_TEAM_ID once the account exists." >&2
  exit 1
fi

# Deliberately no xcodebuild archive/export here yet: that path has never
# been exercised (no team ID to exercise it with) and belongs to
# 60-shell-build/14-testflight once APPLE_TEAM_ID is real.
echo "APPLE_TEAM_ID is set but the archive/export path is not implemented yet" >&2
echo "(see 60-shell-build/14-testflight)." >&2
exit 1
