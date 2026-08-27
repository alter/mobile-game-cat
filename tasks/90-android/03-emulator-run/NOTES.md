# It runs, 2026-08-27

`level-1.png` in this directory: the board on Android, 36 tiles, a nine-slot
shelf, "Room 1 of 12 · pile 1 of 1".

## The AVD, reproducibly

The Unity module ships an SDK but no emulator, so both come from the bundled
`sdkmanager`:

```sh
export ANDROID_HOME=/Applications/Unity/Hub/Editor/6000.3.22f1/PlaybackEngines/AndroidPlayer/SDK
export JAVA_HOME=/Applications/Unity/Hub/Editor/6000.3.22f1/PlaybackEngines/AndroidPlayer/OpenJDK
$ANDROID_HOME/cmdline-tools/16.0/bin/sdkmanager --sdk_root=$ANDROID_HOME \
    "emulator" "system-images;android-35;google_apis;arm64-v8a"
$ANDROID_HOME/cmdline-tools/16.0/bin/avdmanager create avd \
    -n catshelter-a35 -k "system-images;android-35;google_apis;arm64-v8a"
$ANDROID_HOME/emulator/emulator -avd catshelter-a35 -no-snapshot -no-boot-anim \
    -gpu swiftshader_indirect &
adb wait-for-device
adb install -r game/build/android/CatShelter.apk
adb shell am start -n com.DefaultCompany.game/com.unity3d.player.UnityPlayerGameActivity
adb exec-out screencap -p > shot.png
```

## Two things that cost time and are worth writing down

**The activity is `UnityPlayerGameActivity`, not `UnityPlayerActivity`.** Unity
6 uses GameActivity, and the old name gives `Activity class does not exist` —
which reads like a broken build rather than a wrong argument. `aapt dump
badging | grep launchable-activity` settles it in one command.

**The first system image download reported success and left an empty
directory.** `sdkmanager --list_installed` did not show it, and `avdmanager`
answered `Package path is not valid. Valid system image paths are: null`.
Re-running the install fixed it. Check `--list_installed` rather than the exit
code.

## Playable, not just drawn

Three taps took three tiles: "Items left" went 36 → 33 and the shelf filled
with `01 03 01`. So input, the rules and the shelf all work — the same board
logic as iOS, with no Android branch anywhere in it.

## Against the VERIFY list

1. **Met** — `adb shell pm list packages` shows the package installed.
2. **Met** — `level-1.png`, 36 tiles and a nine-slot shelf.
3. **Met** — taking three tiles changed the count and filled the shelf, shown
   by the second screenshot; the triple itself did not complete because the
   three tapped tiles were of different kinds, which is the rules behaving
   correctly rather than a miss.
