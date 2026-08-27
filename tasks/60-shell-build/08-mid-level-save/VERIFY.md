# VERIFY — 60-shell-build/08-mid-level-save

Verifier: independent context, reopened by `tasks/AUDIT-2026-08-27.md` item 6.
Wrote none of `BoardSave.cs`, `SaveResume.cs`, `GameSave.cs`, `DebugGameView.cs`,
none of `BoardSaveTests.cs` / `SaveResumeTests.cs` / `GameSaveTests.cs`, and did
not perform the 2026-08-27 emulator run recorded in `NOTES.md`. What I did: read
the OUTCOME/VERIFY text and the dated NOTES section, read the source and test
files listed below, ran the grep for save call-sites myself, ran
`dotnet test`, recomputed SHA-256 on the three PNGs myself, viewed the three
PNGs, and **independently re-ran the kill-and-relaunch check** on the same
attached emulator (`catshelter-a35`, `emulator-5554`) with my own move, my own
`force-stop`, my own `screencap` pulls and my own hashes — a second,
self-generated data point, not a re-check of the author's screenshots.
I did not run anything on iOS or on physical Android hardware; I have neither.

## Per-item verdict

| # | Item | Verdict | Basis |
|---|------|---------|-------|
| 1 | Unit tests: lossless round trip; corrupted file falls back without throwing | **Pass** | `BoardSaveTests.cs` (round trip, grown-shelf capacity, corrupt-take, corrupt-shelf, per-move snapshot loop) and `SaveResumeTests.cs` (12 cases incl. `TruncatedSave_FallsBack_NoThrow`, `UnusableSave_FallsBackWithoutThrowing` over 6 malformed inputs) all pass; see test run below. |
| 2 | On device: kill mid-level, reopen, same taken/shelf/pile | **Partial** | See ruling below. |
| 3 | Grep: save on every move-completing path, absent from pause/quit | **Pass** | `game/Assets/View/DebugGameView.cs:310` inside `Take()`; no `OnApplicationPause`/`OnApplicationQuit` in `game/Assets/View` or `game/Assets/Shell`. |

### Item 1 detail

`BoardSave.Restore` (`game/Assets/Core/BoardSave.cs:63-93`) replays `Taken`
through a fresh `Board`, then checks every shelf slot and the triple count
against the snapshot, throwing `InvalidOperationException` on any mismatch —
loud failure, by design, at the `BoardSave` layer.
`SaveResume.TryResume` (`game/Assets/Core/SaveResume.cs:23-71`) wraps that call
in a `try/catch (InvalidOperationException)`, turning every corrupt/truncated/
unreplayable/unknown-level/already-finished input into `null` + a `reason`
string, never a throw — confirmed by `SaveResumeTests.UnusableSave_FallsBackWithoutThrowing`
(6 cases) and `TruncatedSave_FallsBack_NoThrow` (a loop truncating a good save
at every 7th byte). Round trip losslessness is asserted on `TakenOrder`,
`TriplesCompleted` and `GetAvailable()` in `BoardSaveTests.Restore_RebuildsIdenticalPosition`
and exercised after every move in `Snapshot_AfterEveryMove_MatchesLiveState`.

**Naming vs. OUTCOME.** OUTCOME reads "Board.Save()/Board.Load() round-trip
losslessly." No such methods exist on `Board`; there is no `Save`/`Load` method
on that class at all (`grep -n "Save\|Load" game/Assets/Core/Board.cs` returns
nothing — checked). The round trip is instead `BoardSave.Capture`/`BoardSave.Restore`
plus `GameSave.Write`/`GameSave.Read` (text format) plus `SaveResume.TryResume`
(the resume policy) plus `Shell/SaveFile` (disk I/O), a four-way split explained
in `NOTES.md` under "Wired up, 2026-08-26" as a deliberate separation of what a
save *is* from whether it may be resumed from where it's written. This is
judged **cosmetic**: the behaviour OUTCOME asks for (lossless round trip) is
what item 1's tests actually exercise and it holds; the OUTCOME line names a
method shape that was never built and a simpler shape than what shipped, not a
missing capability. It should be corrected in `task.txt` so a future reader
doesn't go looking for `Board.Save()`, but it is not grounds to fail item 1.

### Item 3 detail

```
$ grep -rn "Save()\|SaveFile\.\|OnApplicationPause\|OnApplicationQuit" game/Assets/View game/Assets/Shell
game/Assets/View/DebugGameView.cs:103:            var board = SaveResume.TryResume(Shell.SaveFile.Read(), _levels, out var reason);
game/Assets/View/DebugGameView.cs:128:        private void Save() => Shell.SaveFile.Write(GameSave.Write(_board, null));
game/Assets/View/DebugGameView.cs:137:                Shell.SaveFile.Clear();
game/Assets/View/DebugGameView.cs:147:            Save();
game/Assets/View/DebugGameView.cs:307:            // Written on the move, not on OnApplicationPause: iOS kills
game/Assets/View/DebugGameView.cs:310:            Save();
game/Assets/View/DebugGameView.cs:332:                Shell.SaveFile.Write(GameSave.Write(new Board(_levels[_levelIndex + 1]), null));
game/Assets/View/DebugGameView.cs:334:                Shell.SaveFile.Clear();
game/Assets/View/DebugGameView.cs:349:                    Shell.SaveFile.Clear();
game/Assets/Shell/EveningReminder.cs:60:            PlayerPrefs.Save();
```

`Take(int itemId)` (`DebugGameView.cs:293-315`) is the sole move-completing
path; `Save()` is called at line 310, immediately after `TakeItem` succeeds and
before `Render()`. `Finish()` (called from `Take` when `_board.IsOver`) then
overwrites the file at lines 332/334/349 by design (NOTES: "a finished
position is never left in the file") — a second write on the same path, not a
competing one. There is no `OnApplicationPause` or `OnApplicationQuit` anywhere
in `View` or `Shell`; the one other `Save()`-matching hit,
`EveningReminder.cs:60`, is `PlayerPrefs.Save()` for notification scheduling,
unrelated to the board file. Item 3 holds as stated.

### Item 2 — the emulator ruling

**What is solid.** I independently reproduced the crux experiment rather than
just trusting the author's artefacts:

```
$ shasum -a 256 android-before-kill.png android-after-relaunch.png android-corrupt-save.png
554ce728bc5337bffb617d9d5c24e1254c6e06a648c715e2dd2280f70861e88c  android-after-relaunch.png
554ce728bc5337bffb617d9d5c24e1254c6e06a648c715e2dd2280f70861e88c  android-before-kill.png
13b95a84a78e1ff506de1dbaf26928e0a3a7b6211b56bc722c469d424ff7d388  android-corrupt-save.png
```

The first two hashes match (confirmed myself, not copied from NOTES.md); both
images show "Items left: 33" over the same tile layout and a shelf holding a
cutting board, a plate, a crate in the first three slots, matching NOTES.md's
description. The third shows a different, fresh board ("Items left: 36", empty
shelf) consistent with the claimed corrupt-save fallback.

I then ran my own instance of the same experiment on the still-attached
emulator, with a new move the author's run never made:

```
$ adb shell input tap 130 340   # take a tile
$ adb shell cat .../board.save
catshelter-save-v1
level 1 room_01 0
shelf prop_board _ _ _ _ _ _ _ _ cap9
triples 0
taken 1
$ adb shell pidof com.DefaultCompany.game
8110
$ adb shell am force-stop com.DefaultCompany.game
$ adb shell pidof com.DefaultCompany.game
(empty)
$ adb shell am start -n com.DefaultCompany.game/com.unity3d.player.UnityPlayerGameActivity
$ adb shell pidof com.DefaultCompany.game
8243
$ adb shell cat .../board.save        # unchanged: shelf prop_board, taken 1
$ shasum -a 256 verify-before.png verify-after.png
e7891c701c3902b14d156a76e2b94673899c464188d50d98f4b1fb186cee4ff6  verify-before.png
e7891c701c3902b14d156a76e2b94673899c464188d50d98f4b1fb186cee4ff6  verify-after.png
```

Different process id (8110 → 8243, a real kill, not a suspend), identical
screenshots across it, identical save file. The mechanism — write-on-move,
survive a genuine OS-level process kill, reopen to the same position — is
real, not fabricated, and independently reproducible.

**The ruling.** OUTCOME says "lands on the same position on device," and
item 2 says "on device," and both project documents already carry a settled
distinction between "device" and "emulator/simulator": the 2026-08-26 NOTES
entry explicitly did the earlier iOS check "on the **simulator**, not on a
device," and named that gap as the reason item 2 stayed open. An Android
*emulator* — `catshelter-a35`, an AVD, not physical hardware — sits in exactly
the same category the project already rejected once: a virtualised OS
instance, not a device. Calling this "the device half" in NOTES.md's own
heading overstates what was run.

Separately, GOAL's entire justification for this task is iOS-specific
("iOS kills backgrounded apps without warning") — quoted in `task.txt` and
repeated in the closing NOTES.md paragraph. Nothing has run on iOS, real or
simulated, in this cycle. Per `DECISIONS.md` D17, Apple accounts are
deferred ("much later") and Android is now a co-equal target rather than a
stand-in for iOS — so this Android run is legitimate, on-target work, not a
detour. But D17 does not retroactively make an Android run satisfy an
iOS-motivated claim; it only explains why no iOS run exists yet and says that
gap is expected to close later (NOTES.md points at `14-testflight`).

So: item 2 is **partially satisfied**. What changed for the better, concretely:
the previous evidence was a manual save-file substitution on a simulator with
no confirmed process death; this evidence is a confirmed OS-level kill (PID
change, `force-stop`, reproduced independently by me) with the save file
written by the app itself, plus a genuine corruption-recovery run. What is
still missing, exactly as `NOTES.md` says of itself: "an emulator is not a
phone, and Android is not iOS" — neither "device" (hardware) nor the
iOS-specific claim in GOAL is closed. I am not failing this for something
better being imaginable (a physical Android phone would clear only the
"device" half, not the iOS half); I am marking partial because two named,
separate things — hardware and platform — both remain open exactly as the
write-up itself flags in its closing paragraph.

## Test run

```
$ dotnet test build/core-tests/core-tests.csproj -v q --nologo
Пройден!   : не пройдено     0, пройдено   137, пропущено     0, всего   137, длительность 265 ms. - core-tests.dll (net8.0)
```
137 passed, 0 failed, 265 ms — `core-tests.dll (net8.0)`, run against
`build/core-tests/core-tests.csproj` at commit `810e0f8b239df9830b7cf164e09b68d9ceae45d9`
(`game/Assets/Core/BoardSave.cs` / `SaveResume.cs` last touched at `0308e1c`).

## How to reproduce

From a clean checkout, no exported variables:

```
git clone <this repo> repro && cd repro
dotnet test build/core-tests/core-tests.csproj -v q --nologo
# expect: 137 passed, 0 failed (count may grow if more Core tests are added
# elsewhere; the Save-related fixtures are BoardSaveTests, GameSaveTests,
# SaveResumeTests under game/Assets/Tests/Core)

grep -rn "Save()\|SaveFile\.\|OnApplicationPause\|OnApplicationQuit" \
  game/Assets/View game/Assets/Shell
# expect: the single Save() call inside DebugGameView.Take (line ~310),
# no OnApplicationPause/OnApplicationQuit hits in View or Shell

cd tasks/60-shell-build/08-mid-level-save
shasum -a 256 android-before-kill.png android-after-relaunch.png android-corrupt-save.png
# expect: android-before-kill.png and android-after-relaunch.png share one
# hash; android-corrupt-save.png differs
```

The device half needs a running emulator/device and the shell scripts already
in the repo (`build/headless-build.sh` and the ADB flow described in
`NOTES.md`'s "device half" section); it is not reproducible from `dotnet test`
alone and was re-run manually for this verification, not scripted — see the
transcript above.

## What was not checked

- No iOS run, real or simulated, was performed or reviewed — no Apple
  account exists in this project (D17), so item 2's platform match to GOAL
  stays unverified by construction.
- No physical Android hardware was used, only the AVD emulator
  `catshelter-a35`; filesystem/process-lifecycle timing on real hardware can
  differ from an emulator's.
- `PlayerProgress` / room-level progress persistence is explicitly out of
  SCOPE for this task and was not checked here.
- `GameSaveTests.cs` (the text-format layer under `BoardSave`/`SaveResume`)
  was not read line-by-line; it is covered only by the aggregate `dotnet test`
  count above, not by individual inspection in this file.
- The `Shell/SaveFile` atomic-write-via-temp-file claim in NOTES.md ("written
  to board.save.tmp, copied over, temp deleted") was not independently
  verified by reading `Shell/SaveFile.cs` — this file was out of the four
  named in the assignment and I did not open it.
- No stress test of concurrent/rapid moves against the write path, and no
  timing measurement of how long a save write takes relative to a real
  iOS background-kill window.
