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

---

# The two open items, closed by reading rather than by running, 2026-08-27

**Constraint on this pass: no adb, no emulator** — another agent owns them.
Everything below is either a judgment on a device capture someone else
already recorded, or a macOS/.NET run of the exact `Core` code, compared
against that recorded text. Nothing here is a fresh device capture.

The `run-as` blocker this file recorded is retired: `Application.persistentDataPath`
on Android is `/sdcard/Android/data/com.DefaultCompany.game/files/`, which
plain `adb shell cat` reads on a release build — no `run-as` needed. Established
today in `60-shell-build/08-mid-level-save/NOTES.md`, "The device half, done
on Android."

## Item 1: does the corrupted-save result in `08-mid-level-save` satisfy this task's item too?

**Yes — judged, not just cross-referenced.** `08`'s Android run overwrote the
save file with `level 1 / room room_01 / this is not a save / <NUL><SOH>garbage`,
relaunched, and the app came up alive (a new pid, 8110) on a fresh board —
"Items left: 36", empty shelf, screenshotted as `android-corrupt-save.png`.
That is exactly this task's SCOPE line, "a corrupted save falls back to a
fresh board without crashing, as on iOS."

The one thing `08`'s Android paragraph does not itself state is the second
half of this task's VERIFY 2 — "rewrites the file." Rather than assume it, I
read the code path that Android run actually exercised.
`View/DebugGameView.Awake` calls `Resume()`; `Resume()` calls
`SaveResume.TryResume(Shell.SaveFile.Read(), ...)`, and a corrupt file makes
`GameSave.Read` return null immediately (`Core/GameSave.cs`, first line of
`Read`: "Returns null on anything malformed"), so `TryResume` returns null with
`reason = "no readable save"` and `Resume()` returns `false`
(`DebugGameView.cs:101-109`). The caller then does exactly one thing on that
path: `if (!Resume()) StartLevel(0);` (`DebugGameView.cs:90-91`), and
`StartLevel` ends with `Save()` (`DebugGameView.cs:143-148`), which is
`Shell.SaveFile.Write(GameSave.Write(_board, null))` — an unconditional write,
not gated on a move happening first. So the corrupt file is overwritten with
a fresh save the instant the app decides it cannot be resumed, before the
player has touched anything. There is no Android branch anywhere in this
chain — `Resume`, `StartLevel`, `Save`, `SaveResume`, `GameSave` are all in
`Core`/`View`, platform-neutral. The Android screenshot proves the "starts
fresh, does not crash" half by observation; the code proves the "rewrites the
file" half by the fact that no other path reaches a fresh `StartLevel` without
calling `Save()`.

**Verdict: item 1 (VERIFY 2) is settled — fresh start confirmed on-device,
file rewrite confirmed by the single code path both platforms share.**

## Item 2: an iOS-written save against an Android-written one, byte for byte

There is no iOS device or simulator save available in this pass, and D13
still holds — `Core/GameSave` is deliberately not `JsonUtility` and carries no
iOS shape (`GameSave.cs`'s own docstring: "Deliberately NOT System.Text.Json
... and not Newtonsoft-inside-Core"). So the honest version of this check,
per the coordinator's instruction, is: reconstruct the exact board the
recorded **Android**-written save describes, run it through the real,
shipped `GameSave.Write` on a different OS (macOS, not Android), and see if
the bytes match. That is not "iOS vs Android" — see the caveat below.

**The recorded Android save**, verbatim, from `60-shell-build/08-mid-level-save/NOTES.md`:

```
catshelter-save-v1
level 1 room_01 0
shelf prop_board prop_plate prop_crate _ _ _ _ _ _ cap9
triples 0
taken 1 3 6
```

**The harness**, outside the repository at
`/private/tmp/claude-501/-Users-rdolgov-workflow-git-mobile-game-cat-game-build-ios-CatShelter/ddd84cea-13ed-486a-b25a-247b960f9cd7/scratchpad/save-parity-harness/`
(`harness.csproj` + `Program.cs`), built the same way `build/core-tests/core-tests.csproj`
does — a `<Compile Include>` of `game/Assets/Core/**/*.cs`, so it runs the
actual shipped `GameSave`/`Board`/`Shelf`/`Level` code, not a reimplementation.
It builds a 9-item pile (3 kinds × 3, `prop_board`/`prop_plate`/`prop_crate`,
satisfying `Level`'s own "every kind count % 3 == 0" check), constructs
`Level(1, "room_01", 0, pile)`, `Board(level, 9)` — capacity 9, matching
`cap9` — and calls `TakeItem(1)`, `TakeItem(3)`, `TakeItem(6)` in that order,
reproducing the exact `taken 1 3 6` / three-different-kinds-on-the-shelf
position the recorded save names. Then `GameSave.Write(board, null)`.

```
$ cd .../save-parity-harness && dotnet run
...
=== GameSave.Write output (macOS/.NET, from the real Core code) ===
catshelter-save-v1\n
level 1 room_01 0\n
shelf prop_board prop_plate prop_crate _ _ _ _ _ _ cap9\n
triples 0\n
taken 1 3 6\n

=== Recorded Android-written save (60-shell-build/08-mid-level-save/NOTES.md) ===
catshelter-save-v1\n
level 1 room_01 0\n
shelf prop_board prop_plate prop_crate _ _ _ _ _ _ cap9\n
triples 0\n
taken 1 3 6\n

RESULT: BYTE-IDENTICAL
```

(Build produced nullable-reference warnings only — `CS8600`/`CS8603`/`CS8625`
from passing `null` where `Core` isn't annotated `?`, pre-existing in `Core`
and irrelevant here — no errors.)

**Verdict: byte-identical.** The line format, the `_`-for-empty shelf
encoding, the `cap9` suffix, the taken-order list — all match with no
platform-conditional code anywhere in the path.

### What this proves, and what it does not — stated plainly so it is not misread

**What it proves:** `GameSave.Write`, given the same `Board` state, produces
the same bytes on macOS/.NET 8 that the Android build produced on-device
(Mono/IL2CPP on an Android emulator). That is real evidence the format is
free of platform-conditional formatting (locale-dependent number formatting,
line-ending differences, encoding quirks) between at least these two
runtimes, because `CultureInfo.InvariantCulture` is used throughout `GameSave.Read`/`Write`
and no OS-specific API appears in the file.

**What it does not prove:** that an iPhone would produce this. No iOS device
or simulator wrote anything in this pass — the "Android" side of the
comparison is a **recorded capture from `08-mid-level-save`**, not a fresh
one (this pass used no adb, per the coordinator's constraint), and the "iOS"
side is **absent entirely, replaced by a second run of the same code on a
third runtime (macOS/.NET), not by iOS/Mono/IL2CPP**. Two runs of identical
C# source producing identical output is expected regardless of platform
unless something OS-specific leaked in — it is a weaker claim than "an
iPhone and an Android phone wrote the same bytes for the same position,"
which remains unverified and stays blocked on the same thing `08` names:
no Apple developer account, no device, `14-testflight`. This closes the
"format has nothing platform-shaped in it, checked by inspection and by a
second runtime" half of VERIFY 3; it does not close "an iOS device and an
Android device agree," which needs an actual iOS run.

## Status

VERIFY 1 (inject/kill/relaunch, exact position) was already settled before
this pass — the top of this file, "Three tiles taken... Items left: 33," the
exact position after a real force-stop and relaunch. Not re-litigated here.

Of the two items this pass was asked to close: item 1 (corrupted save →
fresh, no crash, file rewritten) is settled — a real device capture plus the
single shared code path that both platforms run. Item 2 is settled only in
the weaker sense stated above: the format is proven platform-neutral by a
second runtime (macOS/.NET) reproducing a real Android capture byte for
byte, which is genuine evidence nothing iOS- or Android-shaped leaked into
`GameSave`. It is **not** the literal thing VERIFY 3 and this task's SCOPE
ask for — "a save written on iOS restored on Android and back, byte for
byte" — because no iOS device or simulator wrote anything in this pass. That
piece is the same gap `08-mid-level-save` already named and defers to
`14-testflight`.

Because VERIFY 3 is not genuinely settled in its literal form, `status`
stays `in_progress` rather than moving to `review`. `verify:` is left
untouched. What changed: the false blocker (`run-as`) is retired, item 1 is
closed, and item 2 has real (if partial) evidence instead of none — the
remaining gap is narrowed to exactly one thing, an iOS device.
