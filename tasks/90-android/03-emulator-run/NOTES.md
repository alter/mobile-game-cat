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

# Set the screen. avdmanager's default is 320x640 at 160 dpi and it says so
# nowhere; the AVD is named after a Galaxy A35, which is 1080x2340 at ~390.
C=~/.android/avd/catshelter-a35.avd/config.ini
sed -i "" -e "s/^hw.lcd.width.*/hw.lcd.width = 1080/" \
          -e "s/^hw.lcd.height.*/hw.lcd.height = 2340/" \
          -e "s/^hw.lcd.density.*/hw.lcd.density = 420/" "$C"
adb shell wm size      # expect 1080x2340, not 320x640

$ANDROID_HOME/emulator/emulator -avd catshelter-a35 -no-snapshot -no-boot-anim \
    -gpu swiftshader_indirect &
adb wait-for-device
adb install -r game/build/android/CatShelter.apk
adb shell am start -n com.DefaultCompany.game/com.unity3d.player.UnityPlayerGameActivity
adb exec-out screencap -p > shot.png
```

**The default screen is 320x640 and it makes the art look broken.** Caught
2026-08-27, when the props landed: the owner asked why Android icons were
blurry next to the iPhone's. Neither the build nor the textures differed. A
tile is 52 units of a 390-unit panel, so on a 320-pixel screen the 256x256
sprite is drawn at 43 pixels — 17% of the file, and every bit of detail in it
is gone. On the A35's real 1080 it is 144 pixels, on an iPhone 17 161. Judging
art on the default AVD judges the downscaler, not the art.

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
