# The save survives on Android, 2026-08-27

Three tiles taken, the app force-stopped, then relaunched. `resumed.png` in
this directory is the screen after the relaunch: **"Items left: 33" and the
shelf holding `01 03 01`** — the exact position, not a fresh board.

That is the whole point of this task. `Core/GameSave` writes a hand-rolled line
format precisely so it is not `JsonUtility` and not iOS-shaped, and
`Shell/SaveFile` is the only code that touches the disk. Neither needed a
single Android branch.

```sh
adb shell am force-stop com.DefaultCompany.game
adb shell am start -n com.DefaultCompany.game/com.unity3d.player.UnityPlayerGameActivity
```

## What could not be checked this way

Reading the file directly needs `adb shell run-as`, which a release build
refuses — `run-as` returns nothing here. The other two VERIFY items therefore
stay open until there is a development build to inspect:

- injecting a corrupted save and confirming a fresh start (the iOS equivalent
  passed);
- diffing an iOS-written save against an Android-written one byte for byte.

Both are cheap once `BuildAndroidPlayer` gains a development variant. What is
proven today is the part that matters most to a player: kill the app mid-pile,
reopen it, and the pile is where she left it.
