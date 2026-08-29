#!/bin/sh
# tools/tests/android-vision — the Android counterpart of tools/vision-probe.
#
#   ./run.sh                 build, install, push the 41 fixtures, run, pull
#   ./run.sh size            build both arms of the APK size A/B and print both
#
# Everything it needs comes from the Unity Android player's own SDK, so there is
# nothing to install: ANDROID_HOME below is the path 60-shell-build already uses.
# One emulator or device must be attached; ANDROID_SERIAL picks between several.
#
# WHICH EMULATOR MATTERS, and it decides which half of this measurement you get.
# The subject-segmentation model is an optional Google Play services module, so
# a `google_apis` AVD can never fetch it — Phonesky is not there to bind to, and
# every photograph comes back on rung 2 with no mask. For the mask you need a
# `google_apis_playstore` image AND `PlayStore.enabled=yes` in the AVD's
# config.ini, which avdmanager does not set on its own:
#
#   sdkmanager "system-images;android-35;google_apis_playstore;arm64-v8a"
#   avdmanager create avd -n catvision-play35 \
#       -k "system-images;android-35;google_apis_playstore;arm64-v8a" -d pixel_6
#   sed -i '' 's/PlayStore.enabled=no/PlayStore.enabled=yes/' \
#       ~/.android/avd/catvision-play35.avd/config.ini
#
# The module then downloads about a minute after the first request. Both
# outcomes are worth having; NOTES-android.md reports the set on each rung.
set -eu

here=$(cd "$(dirname "$0")" && pwd)
root=$(cd "$here/../../.." && pwd)

ANDROID_HOME=${ANDROID_HOME:-/Applications/Unity/Hub/Editor/6000.3.22f1/PlaybackEngines/AndroidPlayer/SDK}
JAVA_HOME=${JAVA_HOME:-/Applications/Unity/Hub/Editor/6000.3.22f1/PlaybackEngines/AndroidPlayer/OpenJDK}
export ANDROID_HOME JAVA_HOME
adb=$ANDROID_HOME/platform-tools/adb
pkg=com.catshelter.visionprobe
files=/sdcard/Android/data/$pkg/files

cd "$here"
printf 'sdk.dir=%s\n' "$ANDROID_HOME" > local.properties

if [ "${1:-run}" = size ]; then
    gradle -q --console=plain :app:assembleRelease
    with=app/build/outputs/apk/release/app-release-unsigned.apk
    cp "$with" /tmp/catvision-with.apk
    gradle -q --console=plain -PnoMlKit :app:assembleRelease
    cp "$with" /tmp/catvision-without.apk
    ls -l /tmp/catvision-with.apk /tmp/catvision-without.apk
    exit 0
fi

gradle --console=plain :app:assembleDebug :app:assembleDebugAndroidTest
"$adb" install -r -t app/build/outputs/apk/debug/app-debug.apk
"$adb" install -r -t app/build/outputs/apk/androidTest/debug/app-debug-androidTest.apk

# The app's own external files dir: writable by adb, readable by the app, and
# needing no storage permission from either side.
# The fixtures go flat into the directory the FRAMEWORK made for the app. A
# subfolder created by `adb shell mkdir` belongs to the shell user and the app
# cannot list it; and on a device where the app has never run, files/ does not
# exist yet and adb push fails with "Is a directory". So: run one test first,
# purely so getExternalFilesDir() creates it.
"$adb" shell am instrument -w -e class \
    com.catshelter.visionprobe.ProbeTest#segmentationDiagnostic \
    "$pkg.test/androidx.test.runner.AndroidJUnitRunner" >/dev/null 2>&1 || true
for f in "$root"/fixtures/reference-photos/*.jpg; do
    "$adb" push -q "$f" "$files/" >/dev/null
done

# Both classes: ProbeTest over the reference set, and MaskGeometryTest, which
# checks the mask arithmetic without needing the model at all.
"$adb" shell am instrument -w \
    "$pkg.test/androidx.test.runner.AndroidJUnitRunner"

"$adb" pull "$files/out.jsonl" "$here/out.jsonl"
wc -l "$here/out.jsonl"
