# VERIFY — 90-android/09-notifications

Verifier: an independent context that wrote neither `EveningReminder.cs`,
`Copy.cs`, `GameBoot.cs`, nor `Editor/BuildScript.cs`, and did not author
`NOTES.md`. Did not rebuild the APK (a Unity build takes minutes and would
collide with other agents working in this repo, and the task instructions
say not to). Did not test on real hardware — only the existing emulator
`emulator-5554` (API level match to `catshelter-a35` not independently
re-confirmed; `dumpsys package` shows targetSdk=36). Did not observe the
real 19:00 daily path or a 24h repeat — only the debug-delay path, same as
the original author. Did not re-derive the "asking on launch doubles
refusals" claim (NOTES.md already disclaims it; out of scope here). Read the
source of the three files named in CONTEXT and re-ran the on-device
experiment myself, independently, from a `pm clear` state, rather than
trusting the screenshots and log lines already in the task directory.

## Per-item verdict

| # | Item | Verdict | Evidence |
|---|---|---|---|
| 1 | No permission dialog before level 2 | PASS | Own run: `adb shell pm clear com.DefaultCompany.game` (reset both the `asked` flag and the OS grant — confirmed by `dumpsys package` showing `granted=false` immediately after clear), launched with no debug file present, screenshot shows the board with no dialog (`item1-no-dialog.png`, see reproduction below), `reminder-state.txt` on-device read back as `22:34:01 launch: permission not asked yet, nothing to schedule`, and `dumpsys package com.DefaultCompany.game` confirmed `POST_NOTIFICATIONS: granted=false` after that launch. |
| 2 | Delivered while backgrounded, via the debug hook | PASS | Own run: pushed `notify-in-seconds.txt` containing `40`, force-stopped and relaunched — dialog appeared (matches `android-permission-dialog.png`), tapped Allow, `reminder-state.txt` recorded `scheduled id=1 ... for 2026-08-27 22:35:14 ... debugDelay=40`, pressed HOME, waited past the fire time, then `adb shell dumpsys notification --noredact` showed a live `NotificationRecord` for `com.DefaultCompany.game` with `android.title=Your kitten found something behind the couch` / `android.text=It is waiting to show you, whenever you have a minute.` on channel `catshelter-evening` — the exact `Copy.cs` strings — and a screenshot of the shade confirms it visually. |
| 3 | Reopening moves the pending reminder rather than adding a second | PASS | Own two-relaunch run (below) reproduces the exact false-failure NOTES.md describes: `dumpsys alarm | grep -c UnityNotificationManager` read 2, then 3, across relaunches, but in both cases only one match is the pending `RTC_WAKEUP` alarm (`ELAPSED #9`, `whenElapsed` matching the latest `reminder-state.txt` schedule line) — every other match sits inside a per-app history block under `u0a210:` whose record opens `Reason=pi_cancelled`. After the second relaunch there were two such `pi_cancelled` records (one timestamped `22:37:32.629`, ~5s after this run's relaunch at `22:37:37`; one stale from `22:16:27`, left over from a prior verification session) and exactly one pending alarm. Confirms both the mechanism (id=1 is cancelled and re-armed, never duplicated) and NOTES.md's claim about the naive grep. |
| 4 | The three PNGs show what NOTES.md says | PASS | Opened all three. `android-no-dialog-before-asking.png` — the board (Room 1 of 12), no dialog, matches item 1's own screenshot content. `android-permission-dialog.png` — "Allow game to send you notifications?" system dialog, matches what appeared in this run's own relaunch. `android-notification-delivered.png` — notification shade with "game" / "Your kitten found something behind the couch" / "It is waiting to show you, whenever you have a minute.", matching this run's own delivered notification pixel-for-pixel in text content. |
| 5 | Build pipeline: `UseTarget` called before building, APK actually declares the permission and receiver | PASS | `game/Assets/Editor/BuildScript.cs`: `ConfigureAndroid()` (called by both `BuildAndroidPlayer` and `BuildAndroidBundle`) calls `UseTarget(BuildTargetGroup.Android, BuildTarget.Android)` as its first line, before any `PlayerSettings`/`BuildPipeline.BuildPlayer` call. Checked against the **already-built** `game/build/android/CatShelter.apk` (not rebuilt, per instructions) with aapt2: `aapt2 dump permissions game/build/android/CatShelter.apk` lists `uses-permission: name='android.permission.POST_NOTIFICATIONS'`; `aapt2 dump xmltree ... --file AndroidManifest.xml` shows `E: receiver` with `android:name="com.unity.androidnotifications.UnityNotificationManager"` and a sibling `meta-data android:name="com.unity.androidnotifications.exact_scheduling" android:value=0` (consistent with the "no exact alarms" SCOPE line). |

## How to reproduce

From a clean checkout, with the emulator `emulator-5554` already running
(depends on `90-android/03-emulator-run`) and the APK at
`game/build/android/CatShelter.apk` already installed as
`com.DefaultCompany.game`:

```sh
ADB=/Applications/Unity/Hub/Editor/6000.3.22f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb
AAPT2=/Applications/Unity/Hub/Editor/6000.3.22f1/PlaybackEngines/AndroidPlayer/SDK/build-tools/36.0.0/aapt2

# item 5 — static check on the already-built APK, no rebuild
$AAPT2 dump permissions game/build/android/CatShelter.apk | grep POST_NOTIFICATIONS
$AAPT2 dump xmltree game/build/android/CatShelter.apk --file AndroidManifest.xml | grep -A2 UnityNotificationManager

# item 1 — reset state, launch with no debug file, expect no dialog
$ADB shell pm clear com.DefaultCompany.game
$ADB shell am start -n com.DefaultCompany.game/com.unity3d.player.UnityPlayerGameActivity
sleep 6
$ADB exec-out screencap -p > /tmp/no-dialog.png   # inspect: board, no dialog
$ADB shell dumpsys package com.DefaultCompany.game | grep "POST_NOTIFICATIONS: granted"  # expect false

# item 2 — debug hook, grant, background, wait, check delivery
echo 40 > /tmp/notify-in-seconds.txt
$ADB push /tmp/notify-in-seconds.txt /sdcard/Android/data/com.DefaultCompany.game/files/notify-in-seconds.txt
$ADB shell am force-stop com.DefaultCompany.game
$ADB shell am start -n com.DefaultCompany.game/com.unity3d.player.UnityPlayerGameActivity
sleep 6
$ADB shell input tap 540 1250   # "Allow" button
$ADB shell input keyevent KEYCODE_HOME
sleep 45
$ADB shell dumpsys notification --noredact | grep -A5 "com.DefaultCompany.game"  # look for the notification text

# item 3 — relaunch twice with a long debug delay, check dumpsys alarm carefully
echo 600 > /tmp/notify-in-seconds.txt
$ADB push /tmp/notify-in-seconds.txt /sdcard/Android/data/com.DefaultCompany.game/files/notify-in-seconds.txt
$ADB shell am force-stop com.DefaultCompany.game && $ADB shell am start -n com.DefaultCompany.game/com.unity3d.player.UnityPlayerGameActivity
sleep 6; $ADB shell input keyevent KEYCODE_HOME
$ADB shell am force-stop com.DefaultCompany.game && $ADB shell am start -n com.DefaultCompany.game/com.unity3d.player.UnityPlayerGameActivity
sleep 6; $ADB shell input keyevent KEYCODE_HOME
$ADB shell dumpsys alarm > /tmp/alarm.txt
grep -c UnityNotificationManager /tmp/alarm.txt   # naive count, will be >1
grep -n UnityNotificationManager /tmp/alarm.txt | cut -d: -f1 | while read ln; do sed -n "$((ln-8)),$((ln+3))p" /tmp/alarm.txt; done
# read the context of each match by hand: exactly one is a pending
# "RTC_WAKEUP #N: Alarm{...}" entry; the rest sit under a per-app history
# block whose record opens "Reason=pi_cancelled"

# cleanup
$ADB shell rm -f /sdcard/Android/data/com.DefaultCompany.game/files/notify-in-seconds.txt
```

## What was not checked

- Real hardware — everything above ran on the emulator only, as the task's
  ROLE/CONTEXT and the available tooling require.
- The actual 19:00 daily trigger and a real 24-hour repeat: verified only
  via the `notify-in-seconds.txt` debug hook, same limitation NOTES.md
  already states under "What is not proven."
- The after-level-2 path exercised by playing two levels by hand rather
  than through `DebugRequestNow` — not done here either, same as the
  original author's run.
- Whether `SCHEDULE_EXACT_ALARM` or other exact-alarm permissions are
  absent from the manifest was checked only indirectly, via the
  `exact_scheduling` meta-data value of `0`; a direct
  `aapt2 dump permissions` listing was also inspected and does not
  contain `SCHEDULE_EXACT_ALARM`.
- Rebuilding the APK from source was deliberately not attempted, per this
  verification's own instructions; the build-pipeline claim (item 5) was
  therefore checked against the artefact already on disk
  (`game/build/android/CatShelter.apk`, timestamped 2026-08-27 22:08 by
  `ls -la`), not against a fresh build.
- The `catshelter-a35` emulator NOTES.md names could not be cross-checked
  against `emulator-5554`'s exact AVD identity; only `targetSdk=36` /
  `minSdk=25` from `dumpsys package` and `device product:sdk_gphone64_arm64`
  from `adb devices -l` were confirmed.
