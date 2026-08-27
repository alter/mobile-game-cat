Verifier: independent NATIVE/QA context. Wrote none of `game/Assets/Core/LevelLoadPolicy.cs`,
`game/Assets/View/LevelAssets.cs`, `game/Assets/View/DebugGameView.cs`,
`game/Assets/Shell/Copy.cs`, this task's own `task.txt`/`NOTES.md`, or the
existing `level-1.png`. Ran every command itself against the live emulator
and the APK on disk, rather than trusting `NOTES.md`'s account of an
earlier run. Did not run a Unity build. Its only writes are to this file,
`labels.txt`, and the four new PNGs listed below.

## Verdict by item

| # | Question | Verdict | Evidence |
|---|---|---|---|
| 1 | VERIFY items, run live | **Pass, all three, on the current APK** | `adb devices -l` → `emulator-5554 ... 1080x2340` confirmed via `adb shell wm size`. Reinstalled the on-disk APK fresh (`adb install -r`, `lastUpdateTime` moved to `2026-08-27 23:42:33`, matching the file's own `2026-08-27 23:00` mtime) rather than trusting whatever was already on the emulator, whose `lastUpdateTime` (22:12:30) predated the APK on disk. **VERIFY 1**: `adb shell pm list packages` → `package:com.DefaultCompany.game`. **VERIFY 2**: `level-1-2026-08-27-board-loaded.png` (this directory) — "Room 1 of 12 · pile 1 of 1", "Items left: 36", a 6×6 grid of 36 tiles, a 9-slot shelf. **VERIFY 3**: tapped tiles live; `level-1-2026-08-27-five-taken-no-match.png` shows "Items left: 33" after three different-kind taps (no match, shelf holds 3 non-matching items — the rules behaving correctly, same as `NOTES.md`'s account); a further two taps plus one more matching tap produced `level-1-2026-08-27-triple-matched-shelf-gaps.png`: "Items left: 30", and the shelf shows slots 1, 4 and 6 empty while 2/3/5 stay occupied — a genuine triple match, three tiles of one kind (`prop_board`) removed **in place**, confirming both VERIFY 3 and, as a bonus, D16's "the shelf does not compact" behavior live on Android, not just on iOS. |
| 2 | Is the recorded evidence (`level-1.png`) still representative? | **No — it is historical, not current, and should be labelled as such** | `level-1.png` shows coloured rectangular tiles carrying two-digit numeric codes ("01", "00", "05"...) with most cells rendered as flat grey placeholders — a pre-art, programmer-art debug rendering. Today's build shows the actual 40-art prop illustrations (cutting boards, plates, crates, books) and the `prop_unknown` draped-cloth silhouette for buried items, none of which existed as art when `level-1.png` was captured. The board's *structure* it documents (36 tiles, 9-slot shelf, "Room 1 of 12 · pile 1 of 1") still matches today's build exactly — the layout claim in `NOTES.md` is not wrong — but the image itself predates the art pass, the analytics package, the notification receiver and the corrupt-level guard, and a reader trusting the picture rather than the layout description would be looking at a different game. Kept `level-1.png` in place rather than deleted (same reasoning as the sibling tasks checked earlier today: history stays visible), replaced its role with the three dated screenshots above for anyone checking what ships now. |
| 3 | The `LevelLoadPolicy` guard — reachable live? | **Not reachable to inject live, and said so rather than skipped; the guarantee confirmed instead from source, tests and the code path that consumes it** | Investigated three routes to reach a level file on-device: (a) the installed APK's own zip (`pm path` → `unzip -l`) shows level content packed inside `sharedassets0.assets.split0`/`split1`, Unity's binary serialized asset format — `Resources.Load<TextAsset>($"Levels/{name}")` (`LevelAssets.cs:49`) reads from this bundle, not from a loose file; there is no plain-text level file to swap without deserialising and repacking Unity's own asset format, which is not an `adb` operation and was judged too destructive to attempt against the one shared build. (b) `adb root` succeeded (the emulator is rooted) and `Application.persistentDataPath` was located at `/storage/emulated/0/Android/data/com.DefaultCompany.game/files/` (external storage, not `/data/data/.../files` — confirmed by finding `board.save` and `reminder-state.txt` there after playing) — but this directory holds only the **save**, never level content, so it is the wrong guard (`SaveResume`, not `LevelLoadPolicy`) even though it is writable. (c) `run-as` refused ("package not debuggable"), closing off app-private internal storage as a route too, independent of (a)/(b). So a live corrupt-level reproduction genuinely needs a rebuild, which this pass is barred from.<br><br>**What was established instead, from the actual shipped code, not simulated:** `LevelAssets.LoadAll()` catches `JsonReaderException`/`NullReferenceException`/`ArgumentException` per file and logs, never throws (`LevelAssets.cs:66-80`); `Core.LevelLoadPolicy.Resolve` drops only the room a bad file belongs to, keeping every other room, and reports `CanStart` false only when *every* room came back incomplete (read directly, `LevelLoadPolicy.cs:60-86`). `DebugGameView.cs:90-101` shows a card only in that all-rooms-lost case, reading `Copy.Of("levels.unavailable.title"/"…body")` = *"Something is missing" / "The rooms could not be loaded this time. Please reinstall or try again later."* — calm, actionable, no blame, consistent with the project's tone rule. For the realistic single-bad-file case, nothing is shown to the player at all beyond a `Debug.LogError` line in `logcat` — the game boots normally on the surviving 11 rooms, and the only visible trace is that `board.title`'s `{1}` (`_plan.RoomCount`, `DebugGameView.cs:172`) would honestly read "of 11" instead of "of 12," since `RoomCount` is computed from what actually loaded, not hardcoded. Re-ran the guard's own unit tests directly, independent of trusting the commit message: `dotnet test build/core-tests/core-tests.csproj --filter "FullyQualifiedName~LevelLoadPolicyTests"` → **8 passed, 0 failed**, covering a gap mid-room, a missing trailing pile, a room entirely missing, and the all-rooms-lost case. |
| 4 | Startup time and anything wrong on screen | **~1s to a fully interactive board; nothing visually wrong** | `adb shell am start -W` reported `TotalTime: 802` (ms to first frame) on a cold launch after `pm clear`; a screenshot taken 3s after the start command already showed the fully loaded, interactive board (title, item count, all 36 tiles, empty shelf) — the interactive delay is on the order of one to a few seconds on this emulator, not the tens of seconds a cold Unity/IL2CPP boot can sometimes take. At 1080×2340 the board, header, item count and shelf are all correctly positioned and legible; the large blank area below the shelf is the debug view's own layout (a full-height root with content anchored at the top), not a rendering defect — the same layout the iOS build uses. |

**Overall verdict: `verify:passed`.** All three of the task's own VERIFY
items hold on the current, freshly-reinstalled APK, checked live rather
than read from `NOTES.md`. `status:` stays `done` — the OUTCOME (a
screenshot of level 1, the exact command sequence) still exists and is now
current. Two things are worth a future task rather than reopening this one:
`level-1.png` should eventually be swapped for one of today's screenshots
so a reader doesn't mistake a two-digit debug placeholder for what ships,
and the corrupt-level-file guard has no live device evidence yet — only
source and unit-test evidence — because reaching it needs a rebuilt APK
with a deliberately bad level file, which is outside a "no Unity build"
pass.

## How to reproduce

From the current tree, no exported variables. Requires the emulator
already running (`emulator-5554`) and the APK already built:

```sh
ADB=/Applications/Unity/Hub/Editor/6000.3.22f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb
$ADB devices -l                                    # emulator-5554, device
$ADB shell wm size                                 # Physical size: 1080x2340
$ADB install -r game/build/android/CatShelter.apk   # Success
$ADB shell pm list packages | grep DefaultCompany   # package:com.DefaultCompany.game
$ADB shell am start -W -n com.DefaultCompany.game/com.unity3d.player.UnityPlayerGameActivity
                                                     # TotalTime: ~800
$ADB exec-out screencap -p > board.png              # the loaded board
# tap three tiles of the same kind (coordinates depend on current layout —
# read the icon grid from a fresh screenshot each time; the pile reflows
# after every removal), then:
$ADB exec-out screencap -p > after-match.png        # shelf shows gaps, not a slide
```

`LevelLoadPolicy` guard, unit level:

```sh
dotnet test build/core-tests/core-tests.csproj --filter "FullyQualifiedName~LevelLoadPolicyTests" -v q --nologo
# -> 8 passed, 0 failed
```

Investigation trail for why live injection was not reachable:

```sh
$ADB shell pm path com.DefaultCompany.game
$ADB shell unzip -l <base.apk path> | grep -i assets
# -> sharedassets0.assets.split0/1 — binary, not loose level files
$ADB root && $ADB shell find /storage/emulated/0/Android/data/com.DefaultCompany.game -type f
# -> board.save, reminder-state.txt, il2cpp runtime data — no level content
$ADB shell run-as com.DefaultCompany.game ls ...
# -> run-as: package not debuggable
```

## What was not checked

- A live, on-device reproduction of a corrupted level file and the
  all-rooms-lost card — established from source and unit tests instead,
  for the reasons in item 3 above. Would need a rebuilt APK carrying a
  deliberately bad level asset.
- Performance beyond a qualitative startup-time read — this task's own
  SCOPE explicitly excludes performance work ("Slow on an emulator is not
  slow on a phone").
- The photo/camera flow — explicitly out of this task's SCOPE.
- Any device other than the one AVD already running; no attempt was made
  to create a second AVD or reproduce `NOTES.md`'s AVD-creation steps from
  scratch, since one was already up and current per the brief.
- Whether `level-1.png` should be replaced outright — flagged as worth a
  follow-up, not decided or acted on here, since this pass was told to
  touch only this task's directory and judged rather than fix.
