# Wired, 2026-09-02

Four new stages in `build/headless-build.sh`, all before the `--tests-only`
early exit (so `--tests-only` covers them too), right after the coverage
gate:

1. **Android photo rotation check** — hard gate. Looks for `javac` at
   `/Applications/Unity/Hub/Editor/6000.3.22f1/PlaybackEngines/AndroidPlayer/OpenJDK/bin`
   (the same JDK `tools/tests/android-vision/run.sh`'s own `JAVA_HOME`
   default points at). Found → prepends it to `PATH` and runs
   `tools/tests/android-photo/run.sh`; a red run now fails the build (see
   the RotCheck fix below — it didn't, before this task). Missing → named
   skip, build continues.
2. **Android vision check availability** — optional, never fails the
   build. `adb` missing at `${ANDROID_HOME:-.../SDK}/platform-tools/adb` →
   named skip. `adb` present but `adb devices` shows no `device` line →
   named skip. Device present → does **not** auto-run `run.sh` (it installs
   an APK and takes minutes) — prints a one-line reminder to run it by
   hand.
3. **Swift plugins parse** — hard gate. `xcrun swiftc -parse` on each
   `game/Assets/Plugins/iOS/*.swift` file, no `-sdk`/`-target`/`-parse-as-library`
   needed. No `xcrun`/`swiftc` on the machine → named skip.

## A bug found while proving the mutation test (VERIFY #1)

`tools/tests/android-photo/RotCheck.java`'s `main()` never called
`System.exit(1)` — it printed `ЕСТЬ ОШИБКИ: N` on a red run but the `java`
process still exited 0. Wiring `run.sh` into `headless-build.sh` as written
would not have gated anything: a broken `back()` would print red text and
the build would sail on. Added

```java
if (bad != 0) System.exit(1);
```

right after the summary line. This is a defect in the existing test tool,
not something introduced by the wiring — worth a second look wherever else
a shell script's exit code is trusted without checking whether the program
underneath ever sets a nonzero one.

## Mutation test, dead literal transcript

Broke `back()`'s `case 6` (`width - 1 - x` → `width - 2 - x`), ran
`build/headless-build.sh --tests-only`:

```
НЕ СОШЛОСЬ exif=6 1x9: 0,6 -> 2,0 -> 0,5
НЕ СОШЛОСЬ exif=6 1x9: 0,7 -> 1,0 -> 0,6
НЕ СОШЛОСЬ exif=6 1x9: 0,8 -> 0,0 -> 0,7
точек проверено: 24632, расхождений: 3079
exif 1: весь кадр -> 0,0 3000x4000  ок
exif 2: весь кадр -> 0,0 3000x4000  ок
exif 3: весь кадр -> 0,0 3000x4000  ок
exif 4: весь кадр -> 0,0 3000x4000  ок
exif 5: весь кадр -> 0,0 3000x4000  ок
exif 6: весь кадр -> 0,-1 3000x4000  ОШИБКА
exif 7: весь кадр -> 0,0 3000x4000  ок
exif 8: весь кадр -> 0,0 3000x4000  ок
ЕСТЬ ОШИБКИ: 3080

== STAGE FAILED: Android photo rotation check (tools/tests/android-photo, task 60-shell-build/24) (exit 1) ==
```

`echo $?` → `1`. Reverted the literal, reran — `exit 0`, stage prints
`ВСЁ СОШЛОСЬ` and the script continues to the Swift stage. `git diff
--stat tools/tests/android-photo/RotCheck.java` after the revert showed
only the `System.exit(1)` line — the mutation itself left no trace.

## Swift mutation test, dead literal transcript

Appended `\nfunc broken( {\n` to a copy-then-restore of
`game/Assets/Plugins/iOS/CatColour.swift`, ran the same command:

```
== STAGE: Swift plugins parse (game/Assets/Plugins/iOS/*.swift, task 60-shell-build/24) ==
swiftc -parse: /Users/rdolgov/workflow/git/mobile-game-cat/game/Assets/Plugins/iOS/CatColour.swift
/Users/rdolgov/workflow/git/mobile-game-cat/game/Assets/Plugins/iOS/CatColour.swift:107:14: error: expected parameter name followed by ':'
105 | #endif
106 |
107 | func broken( {
    |              `- error: expected parameter name followed by ':'
108 |

== STAGE FAILED: Swift plugins parse (game/Assets/Plugins/iOS/*.swift, task 60-shell-build/24) (exit 1) ==
```

`echo $?` → `1`. Restored the file from the pre-edit copy, reran — all
seven files parse, stage prints seven `swiftc -parse: ...` lines and the
script continues to `--tests-only: skipping Unity build stages...`.

## Android vision, on this machine

An `emulator-5554` (`device`) was already attached while this task ran, so
the actual observed branch was the third one:

```
== STAGE: Android vision check availability (tools/tests/android-vision, task 60-shell-build/24) — optional ==
adb sees a device — tools/tests/android-vision/run.sh was NOT run
automatically (it installs an APK and takes minutes; not this stage's
job). Run it by hand: tools/tests/android-vision/run.sh
```

The other two branches (`adb` missing entirely; `adb` present but `adb
devices` empty) were verified separately by running the same shell logic
against `ANDROID_HOME` pointed at a nonexistent SDK, and against a stub
`adb` script that only prints the header line with no device rows — both
printed the intended skip message and neither raised under `set -e`. Not
run through the full `headless-build.sh` because forcing "no device" would
have meant detaching the one attached to this machine.

## Swift, "real checks are a separate decision" — what NOTES.md is required to record

`-parse` only catches syntax errors read from the source text; it never
resolves `import UIKit`/`import Vision`/`import PhotosUI` (both attempted
variants below skip type-checking of anything from those frameworks, so a
call with the wrong argument label, a renamed API, or a type mismatch
against UIKit/Vision would not be caught here). Two invocations were tried:

- `xcrun swiftc -parse <file>` — works standalone, no `-sdk` needed, exits
  0 on the real files and correctly exits 1 on the injected syntax error.
  This is what got wired in.
- `xcrun -sdk iphoneos swiftc -parse -target arm64-apple-ios15.0 <file>` —
  also works, same result. Not needed since the plain form already
  succeeds and needs no SDK/target bookkeeping.

A real logic check (type-checking against the actual UIKit/Vision APIs, or
running the Swift code's behavior) needs either `-typecheck` against a real
SDK target (catches API misuse, still no linking, no device/simulator
needed — worth trying in a follow-up task) or building/running inside the
generated Xcode project (`build/headless-build.sh`'s own
"Unity iOS Xcode project" stage produces `Unity-iPhone.xcodeproj`), which
needs a signing team ID this project does not have yet (`tasks/DECISIONS.md`
D17) to run on a device, though `xcodebuild build` for the Simulator
destination needs no signing and could plausibly compile the whole plugin
against real UIKit/Vision headers — not attempted here, left as the next
step for whoever picks this up.

## Against the task's VERIFY list

1. **Met** — RotCheck mutation transcript above; build fails with the
   RotCheck fix, and would NOT have failed before it (a real defect this
   task's own mutation test caught).
2. **Met** — with no emulator attached (checked by direct shell-logic
   test, not the full script, see above), the android-vision stage prints
   the skip message and does not fail the build.

## Not done

- No cloud CI — out of scope per this task's SCOPE, and per the standing
  project rule not to pay for cloud.
- `-typecheck`/`xcodebuild`-based real Swift logic checks — left as a
  named follow-up above, not implemented.
- `tools/tests/test_ship_levels.py` — untouched, as instructed.
