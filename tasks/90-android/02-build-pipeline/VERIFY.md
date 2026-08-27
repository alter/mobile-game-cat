Verifier: independent QA context for items 1-2 and the original item 3
proposal below — wrote none of `game/Assets/Editor/BuildScript.cs`,
`build/headless-build.sh`, or this task's own `task.txt`/`NOTES.md` at the
time those were checked. Did **not** run a Unity build (constraint; another
agent needs the toolchain) — verified the artifacts already on disk under
`game/build/android/` directly with `aapt2` and, for the `.aab`,
`bundletool` (found locally under Unity's own `PlaybackEngines/AndroidPlayer/Tools/`),
not by re-reading `NOTES.md`'s numbers. No adb/emulator. Read-only on
`Shell/CatVision.cs`, `Plugins/iOS/`, `tools/`, art task directories.
**Not independent for item 4's implementation**: `build/check-android-manifest.py`
and the `headless-build.sh` stage that runs it were written by this same
context, at the coordinator's direct request, after proposing them below —
their correctness rests on the empirical pass/fail/unrecognised-package
demonstrations quoted here, not on a separate reviewer. A genuinely
independent check of that script is still owed, per this project's own
independence rule.

## Verdict

| # | Question | Verdict | Evidence |
|---|---|---|---|
| 1 | Task's VERIFY items | **Met** | `game/build/android/CatShelter.apk` (27,988,228 B) and `.aab` (26,477,656 B) both present. `aapt2 dump badging` on the `.apk`: `package: name='com.DefaultCompany.game' versionCode='1' versionName='1.0'`, `minSdkVersion:'25'`, `targetSdkVersion:'36'`. `grep -c "error CS"` → 0 across every android build log found; each ends `result=Succeeded ... errors=0`. |
| 2 | Is `UseTarget` real, applied everywhere, and does the evidence hold up? | **Yes, confirmed on the actual artifacts, not just the source** | `UseTarget` is called from `ConfigureAndroid()` (shared by `BuildAndroidPlayer`/`BuildAndroidBundle`), `BuildIOSXcodeProject()`, `BuildIOSSimulatorProject()` — 3 call sites covering all 4 Android/iOS entry points (`BuildOSXPlayer` has no platform-callback dependency here, out of scope). Dumped the `.apk`'s manifest myself: `uses-permission: name='android.permission.POST_NOTIFICATIONS'` and `receiver ... name="com.unity.androidnotifications.UnityNotificationManager"` are both present. The `.aab` can't be `aapt2 dump`ped directly ("could not identify format"); built a universal `.apk` from it with `bundletool build-apks --mode=universal` and dumped that — same permission, same receiver, `minSdkVersion=25`, `targetSdkVersion=36`. Both shipped entry points currently reflect the fix. |
| 3 | What would catch this, and should VERIFY be rewritten? | **Implemented, not just proposed — `build/check-android-manifest.py`, task.txt VERIFY 4** | Original finding stands: VERIFY 1/3 are blind to a semantically-wrong success by construction (every preserved log, pre- and post-fix, shows `result=Succeeded errors=0`); VERIFY 2 as worded never asked about permissions. The proposed check is now real: it reads `game/Packages/manifest.json` for which Android packages are integrated (not a hardcoded list — it rots the day a package is added), looks up each one's known manifest contribution in a table in the script (`com.gameanalytics.sdk` → `INTERNET`/`ACCESS_NETWORK_STATE`, `com.unity.mobile.notifications` → `POST_NOTIFICATIONS`/`UnityNotificationManager`), excludes only engine-module stubs (`com.unity.modules.*`) and packages that never compile into a player build (`EDITOR_ONLY_PACKAGES`) by a documented rule, and **fails loudly on any other package it doesn't recognise** rather than skipping it. Run against the real APK: `check-android-manifest: game/build/android/CatShelter.apk carries all 4 expected manifest contribution(s) from 2 package(s). OK` (exit 0). |
| 3a | Mutation proof — can the check actually fail? | **Yes, both directions, without a Unity build** | No broken artefact survives to rebuild (constraint: no Unity build unless justified, and none was needed). Built two minimal APKs with `aapt2 compile`/`link` alone (no Unity) from hand-written manifests: one with none of the four expected entries, one with all four. Bad: `check-android-manifest: .../bad.apk is missing 4 of 4 expected manifest contribution(s): com.gameanalytics.sdk: 'android.permission.INTERNET' not found ... com.unity.mobile.notifications: 'com.unity.androidnotifications.UnityNotificationManager' not found ...` (exit 1). Good: `carries all 4 expected manifest contribution(s) from 2 package(s). OK` (exit 0). Also proved the "unknown package" path: pointed the script at a fabricated `manifest.json` naming an unrecognised package — `game/Packages/manifest.json names 1 package(s) this check does not recognise: com.some.new.sdk. Add each to PACKAGE_CONTRIBUTIONS ...` (exit 1) — an unrecognised package fails, it does not skip silently. |
| 4 | Does `headless-build.sh` cover this? | **Now yes for the .apk; the `.aab` gap is closed by an explicit, documented decision, not left unaddressed** | Added a stage right after the Android build succeeds: `"$PYTEST_BIN" build/check-android-manifest.py --apk "$APK"`, under the script's existing `set -euo pipefail`/`trap ERR`, so a missing entry fails the whole run. For the `.aab`: decided **out of scope for this script**, and said so in a comment at the top of `headless-build.sh` — its stated job is one APK plus the iOS project for a headless dev loop; a second full Android build to produce a Play bundle nobody uploads from that loop would double the stage's time for no reader of this script. `13-internal-testing` (needs the Play account) is where a build-and-check-the-bundle stage belongs, and `check-android-manifest.py` is not APK-specific — it takes any `.apk` path, including a `bundletool`-built universal one from an `.aab` — so no second check needs writing there, only a call site. |

## How to reproduce

```sh
AAPT2=/Applications/Unity/Hub/Editor/6000.3.22f1/PlaybackEngines/AndroidPlayer/SDK/build-tools/36.0.0/aapt2
"$AAPT2" dump badging game/build/android/CatShelter.apk | grep -i "package\|sdkVersion\|uses-permission"
"$AAPT2" dump xmltree game/build/android/CatShelter.apk --file AndroidManifest.xml | grep -i "receiver\|UnityNotificationManager"
# -> POST_NOTIFICATIONS and the receiver are both present

BT=/Applications/Unity/Hub/Editor/6000.3.22f1/PlaybackEngines/AndroidPlayer/Tools/bundletool-all-1.17.2.jar
java -jar "$BT" build-apks --bundle=game/build/android/CatShelter.aab --output=/tmp/aab-check/out.apks --mode=universal
unzip -o /tmp/aab-check/out.apks -d /tmp/aab-check/extracted
"$AAPT2" dump badging /tmp/aab-check/extracted/universal.apk | grep -i "uses-permission"
# -> same result for the .aab

grep -c "error CS" game/build/android-build.log game/build/android-aab.log

# The new check itself:
python3 build/check-android-manifest.py --apk game/build/android/CatShelter.apk
# -> "carries all 4 expected manifest contribution(s) from 2 package(s). OK", exit 0

# Mutation proof (outside the repo, no Unity needed):
ANDROIDJAR=/Applications/Unity/Hub/Editor/6000.3.22f1/PlaybackEngines/AndroidPlayer/SDK/platforms/android-36/android.jar
# write a minimal AndroidManifest.xml with no uses-permission/receiver, then:
"$AAPT2" link -I "$ANDROIDJAR" --manifest AndroidManifest.xml -o bad.apk
python3 build/check-android-manifest.py --apk bad.apk
# -> lists all 4 missing entries by package, exit 1
```

## What was not checked

- No Unity build was run (constraint) — everything here checks artifacts and
  logs already on disk, not a fresh reproduction.
- Could not identify, among the preserved logs, the exact one produced the
  current `.apk` (mtime 23:00:57, newer than every candidate log's mtime) —
  its content was still verified directly, which matters more, but the
  build-to-log pairing for that specific run is not fully traceable from
  what's on disk.
- Did not reconstruct the pre-fix manifest byte-for-byte (no defective
  artifact survives to compare); relied on the current artifacts plus the
  logs' uniform `errors=0` to establish that the log alone gives no signal.
- Did not evaluate signing/`.aab` upload validity for Play — out of scope
  (`13-internal-testing`).
- `build/check-android-manifest.py` was not independently reviewed by a
  context other than the one that wrote it (noted in the `Verifier:` line).
- Did not run `headless-build.sh` end to end with a live Unity build to
  confirm the new stage's placement and `trap ERR` interaction in situ —
  confirmed its logic by running the script directly against the real and
  crafted APKs, and by `bash -n`-checking the edited script.
- Did not add a case for a package that injects a `<meta-data>` tag or a
  `<queries>` entry rather than a permission/receiver — the two current
  table entries only needed those two shapes; the substring match against
  combined `dump badging` + `dump xmltree` output should generalise, but
  that specific shape was not exercised.
