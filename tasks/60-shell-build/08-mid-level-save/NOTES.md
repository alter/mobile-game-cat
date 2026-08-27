# Notes - this task was under-specified, and the gap punishes exactly our player

Source: cat-shelter-tasks.md, "6.7 was under-specified..." (lines 858-882).

The original acceptance line read "close and reopen preserves progress," and
the `Player` entity in the MVP holds `levels_done, current_level` - level
granularity only. Nothing stored which items had been taken or what sat on
the shelf, and `Board._taken` was private with no way out. So, as specified:
leave mid-level, lose the room.

The audience is defined as playing "10-20 minutes in gaps between other
things." Interruption is their normal case, not an edge case. Riding the
metro, the stop arrives, the app closes - and a half-cleared room evaporates.
That is a punishment, and the MVP's own rule forbids punishments ("the kitten
doesn't get sick").

Three things this task requires, restated as scope above:

1. Serialise the board, not the level number: taken items, shelf contents,
   current level, shelf capacity (the booster can change it, even if the
   MVP's booster never actually fires - see 07-lose-screen-fake-door).
2. Write on every move, not on OnApplicationPause. iOS kills backgrounded
   apps without warning; the pause callback is not a reliable last chance.
3. Make Board reconstructable from that state - today it can only be built
   fresh from a Level.

Cheap to build, and it removes the single most common way this audience will
lose work.

---

# Wired up, 2026-08-26

The serialisation half had existed since `c0a4bcc` and was called by nothing:
`GameSave`, `BoardSave` and `PlayerProgress` sat in `Core` with tests and no
caller, so the game did not save at all. Now connected.

## Where each piece lives, and why

- `Core/GameSave` — what a save *is*: a line format, no files, no engine.
- `Core/SaveResume` (new) — **whether a save may be resumed**. That is a rule,
  so it does not live in the view. It answers with a `Board`, or with null and
  a reason, and never throws.
- `Shell/SaveFile` (new) — the only code that touches the disk.
- `View/DebugGameView` — calls those two and decides nothing.

The split matters because this codebase already made the opposite mistake once:
`DebugGameView` had grown its own copy of `Board.IsRevealed`. A rule in the view
is a rule that drifts and that no test covers.

## Writing

On every move, inside `Take`, right after `TakeItem` succeeds — never in
`OnApplicationPause`, per point 2 above and D12.

The file is replaced atomically: written to `board.save.tmp`, copied over,
temp deleted. It is rewritten hundreds of times per level, and a save
half-written when the process died would be worse than none — it would destroy
the position that was still good.

**A finished position is never left in the file.** The move that ends a level is
written like any other, and resuming into it would strand the player: the
outcome card dies with the process, leaving a board that refuses every tap. So
`Finish` overwrites it — the start of the next level after a win, a cleared file
after a jam, which is where "Replay" goes anyway. `SaveResume` also refuses an
already-finished position, so a file left by an older build cannot strand
anyone either.

## Against the VERIFY list

**1. Round-trip identical; corrupted file falls back without throwing.**
`Tests/Core/SaveResumeTests.cs` — 12 cases: a shelf that contradicts its own
replay, a take that cannot be replayed, a level that no longer ships, a grown
shelf, a finished position, and truncation at every seventh byte. 60 → 72 tests
under `dotnet test build/core-tests/core-tests.csproj`.

**2. Kill and reopen: same position.** Done on the **simulator**, not on a
device — there is no developer team yet (`10-accounts/02`), so nothing can run
on hardware. Method: install, launch, `simctl terminate`, overwrite
`Documents/board.save` in the app container with a known position, launch again,
screenshot.

- Injected level 3, `taken 1 7 16 2 11 27 3 17 19 4 5 6`, 4 triples, empty shelf.
- After relaunch: "pile 2", "Items left: 24", empty shelf, 24 tiles — that exact
  position.
- Injected garbage (`cap-5`, `triples 99`, an unreplayable take): the app stayed
  up, started level 1, and rewrote the file with a fresh save.

A device can still differ — background kills, slower filesystem — so this item
stays open until `14-testflight`.

**3. A save call on every move-completing path.** One path takes an item,
`DebugGameView.Take`, and it saves immediately;`OnApplicationPause` and
`OnApplicationQuit` appear nowhere in the project:

```
grep -rn "Save()\|SaveFile\.\|OnApplicationPause\|OnApplicationQuit" \
  game/Assets/View game/Assets/Shell
```

## Left undone on purpose

Player-level progress — rooms done, cat state — is still not persisted;
`PlayerProgress` remains uncalled. SCOPE excludes it ("Not Player-level progress
beyond what's needed to resume the current board") and it belongs with
`02-room-piles`. Consequence today: quitting on the win card and reopening lands
on the next level, which is right, but nothing remembers how many rooms are
done.

---

## status:done → in_progress, 2026-08-27

The OUTCOME artefact this task names is not there. What is missing, what does
exist, and why it matters: `tasks/AUDIT-2026-08-27.md`.

---

## The device half, done on Android, 2026-08-27

The audit (`tasks/AUDIT-2026-08-27.md`, item 6) reopened this task because
OUTCOME says a killed-and-reopened app "lands on the same position on device"
and nothing on file showed that. It does now, on the Android emulator
`catshelter-a35`, against the APK built the same evening by
`build/headless-build.sh`.

**A true kill, not a background.** The process id is the proof:

```
pid before:         7822
adb shell am force-stop com.DefaultCompany.game
pid after kill:     ''          <- gone
adb shell am start -n com.DefaultCompany.game/com.unity3d.player.UnityPlayerGameActivity
pid after relaunch: 8011        <- a different process
```

**The two screenshots are byte-identical.** `android-before-kill.png` was taken
after three tiles were tapped; `android-after-relaunch.png` after the new
process had drawn its first frame. Two separate `screencap` calls across a
process death:

```
554ce728bc5337bffb617d9d5c24e1254c6e06a648c715e2dd2280f70861e88c  android-before-kill.png
554ce728bc5337bffb617d9d5c24e1254c6e06a648c715e2dd2280f70861e88c  android-after-relaunch.png
```

Both read "Items left: 33" over the same pile, with the shelf holding a cutting
board, a plate and a crate in the first three slots.

**The save itself, read off the device verbatim** — no `run-as` needed, because
`Application.persistentDataPath` on Android is
`/sdcard/Android/data/com.DefaultCompany.game/files/`, which `adb shell cat`
reaches on a release build. `90-android/11-save-parity` left this open believing
it needed `run-as`; it does not.

```
catshelter-save-v1
level 1 room_01 0
shelf prop_board prop_plate prop_crate _ _ _ _ _ _ cap9
triples 0
taken 1 3 6
```

Three ids taken, three kinds on the shelf, capacity 9 — the whole board, which
is what D12 asks for.

**A corrupted save starts fresh instead of crashing.** The file was overwritten
with `level 1 / room room_01 / this is not a save / <NUL><SOH>garbage` and the
app relaunched: it came up alive (pid 8110) on a fresh board — "Items left: 36",
empty shelf — in `android-corrupt-save.png`. That is the promise
`Core/SaveResume` states in its own summary: *"Losing a pile is a setback; a
crash on launch loses the player."*

## Items 1 and 3, checked by reading rather than by running

**Item 3 — a save on every move-completing path.** `DebugGameView.Take` is the
only path a move can complete on, and `Save()` is its second-to-last statement
(`DebugGameView.cs:310`), before the redraw. There is no `OnApplicationPause`
or `OnApplicationQuit` in the file at all, which is the point of D12.
`Finish()` then overwrites that save deliberately — a finished board is a dead
end to resume into.

**Item 1 — round trip and corruption.** `BoardSaveTests` covers the round trip
and the two corrupt-snapshot cases; `SaveResumeTests` covers the layer above,
including a loop that feeds every truncated prefix of a good save through
`TryResume` and asserts none of them throws. The split is right: `BoardSave.Restore`
fails loudly, `SaveResume.TryResume` catches it and returns null so the caller
starts fresh.

## What is still not proven, and it is not nothing

An emulator is not a phone, and Android is not iOS. This task's GOAL is
motivated by iOS specifically — "iOS kills backgrounded apps without warning" —
and no iOS device run has happened. Under D17 there is no Apple account, so
that half waits with `14-testflight`. What is shown here is that the mechanism
survives a real process death and a corrupted file; what is not shown is that
iOS behaves the same.

## Verdict on the above: failed, and rightly — 2026-08-27

An independent context re-ran the whole emulator experiment itself (its own
move, its own force-stop, pids 8110 → 8243, byte-identical screenshots) and
confirmed the mechanism is real. It still ruled item 2 **partial** and the task
`verify:failed`, because `catshelter-a35` is an AVD and not hardware, and this
task's GOAL is motivated by iOS specifically.

That is the right call and worth leaving here so nobody repeats the emulator
run expecting it to close the item. D17 makes Android co-equal work; it does
not make an Android emulator a substitute for the iOS claim this OUTCOME
makes. The item waits for `14-testflight` and a phone.

**What the emulator run did buy**, and it is not nothing: the save format is
now known to survive a true process death and a corrupted file, so if the iOS
run ever fails it will fail for an iOS reason and not because the mechanism was
never checked at all. It also retired a false blocker — `90-android/11` had
recorded that reading the save needed `run-as`, which a release build refuses.
It does not: `Application.persistentDataPath` on Android is under
`/sdcard/Android/data/`, and plain `adb shell cat` reads it.
