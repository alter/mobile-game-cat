Verifier: independent QA context, wrote none of `game/Assets/Editor/BuildScript.cs`,
`build/headless-build.sh`, or this task's own `task.txt`/`NOTES.md`. Did **not**
run a Unity build (constraint; another agent needs the toolchain) — verified
the artifacts already on disk under `game/build/android/` directly with
`aapt2` and, for the `.aab`, `bundletool` (found locally under Unity's own
`PlaybackEngines/AndroidPlayer/Tools/`), not by re-reading `NOTES.md`'s
numbers. No adb/emulator. Read-only on `Shell/CatVision.cs`, `Plugins/iOS/`,
`tools/`, art task directories.

## Verdict

| # | Question | Verdict | Evidence |
|---|---|---|---|
| 1 | Task's VERIFY items | **Met** | `game/build/android/CatShelter.apk` (27,988,228 B) and `.aab` (26,477,656 B) both present. `aapt2 dump badging` on the `.apk`: `package: name='com.DefaultCompany.game' versionCode='1' versionName='1.0'`, `minSdkVersion:'25'`, `targetSdkVersion:'36'`. `grep -c "error CS"` → 0 across every android build log found; each ends `result=Succeeded ... errors=0`. |
| 2 | Is `UseTarget` real, applied everywhere, and does the evidence hold up? | **Yes, confirmed on the actual artifacts, not just the source** | `UseTarget` is called from `ConfigureAndroid()` (shared by `BuildAndroidPlayer`/`BuildAndroidBundle`), `BuildIOSXcodeProject()`, `BuildIOSSimulatorProject()` — 3 call sites covering all 4 Android/iOS entry points (`BuildOSXPlayer` has no platform-callback dependency here, out of scope). Dumped the `.apk`'s manifest myself: `uses-permission: name='android.permission.POST_NOTIFICATIONS'` and `receiver ... name="com.unity.androidnotifications.UnityNotificationManager"` are both present. The `.aab` can't be `aapt2 dump`ped directly ("could not identify format"); built a universal `.apk` from it with `bundletool build-apks --mode=universal` and dumped that — same permission, same receiver, `minSdkVersion=25`, `targetSdkVersion=36`. Both shipped entry points currently reflect the fix. |
| 3 | What would have caught this, and should VERIFY be rewritten? | **Yes — add a manifest-content check; the three existing items structurally cannot catch this class of bug** | VERIFY 1 (files exist) and 3 (no `error CS`, exit 0) are blind to a semantically-wrong success by construction — confirmed directly: every preserved android log, including ones from before the fix, shows `result=Succeeded errors=0`. VERIFY 2 as *worded* ("package name, version, minimum API level") is also too narrow — but `aapt2 dump badging`'s actual output already lists `uses-permission` lines (I saw them appear unprompted), so the fix was cheap: broaden VERIFY 2 to assert the manifest entries a known-integrated package is *supposed* to inject — here, `android.permission.POST_NOTIFICATIONS` and the `UnityNotificationManager` receiver, since `com.unity.mobile.notifications` is already part of this project. Propose adding: **"4. `aapt2 dump badging`/`dump xmltree` on the `.apk`, and on a `bundletool`-extracted `.apk` from the `.aab`, list every manifest contribution a currently-integrated Android package is responsible for (today: `POST_NOTIFICATIONS` + the notification receiver); absence means the active build target was not actually Android when the build ran, which is exactly how this defect was found."** This generalizes past notifications: the same check catches the next Unity package that injects manifest/gradle content silently. |
| 4 | Does `headless-build.sh` cover this? | **No — existence-only, and it never builds the `.aab` at all** | `build/headless-build.sh`'s Android stage runs only `BuildAndroidPlayer`, `rm -f`s the old `.apk` first, then checks `[ ! -f "$APK" ]` — file existence, nothing about content. `BuildAndroidBundle`/`.aab` is not invoked by this script at all. So today the `.aab`'s correctness rests entirely on whoever runs `BuildAndroidBundle` by hand remembering to check it — this VERIFY is the first time it was checked. |

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
