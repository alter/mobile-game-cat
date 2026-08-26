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
