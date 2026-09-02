# 07-first-move-guard — notes

## What changed

- `Board(Level, int)` constructor (Assets/Core/Board.cs): after `_entries`/
  `_taken` are set, if the pile is non-empty and `GetAvailable().Count == 0`,
  calls `Finish(GameOutcome.ShelfJammed)` right there. Both public
  constructors funnel through this one, so `BoardSave.Restore`'s fresh
  rebuild goes through the same check before it replays saved moves.
- `Level` constructor (Assets/Core/Level.cs): added an upper bound on
  `LockedAfterTriples` — `Pile.Count / 3`, the most triples any playthrough
  of the pile can ever complete. Anything above it throws `ArgumentException`
  (same style as the multiplicity/duplicate-id/blocked-by checks next to it).

## Restore safety (the caution in task.txt)

`BoardSave.Restore` builds `new Board(level, capacity)` fresh — 0 triples,
nothing taken — then replays. The new guard runs at that fresh point, which
is identical to how the level looked to the original game at move one, so
it can't fire differently. Added
`Restore_WithEarnedTriples_DoesNotFalselyJamOnRebuild` in BoardSaveTests.cs
to pin this: a save where the unlocked kind was cleared first (unlocking a
`LockedAfterTriples` kind) restores with `IsOver == false`.

## Existing tests touched

Two fixtures used `LockedAfterTriples` values (5) that exceeded the new
Level ceiling for their small 6-item test piles (max achievable = 2), so
Level's new check rejected them:
- `PartialInformationTests.LockedItem_IsRevealedButNotTakeable`
- `PartialInformationTests.EveryRemainingItemLocked_EndsAsJam_NotAHang`
- `BoardTests.Booster_StaysJammedWhenItOpensNoMove`

All three changed 5 -> 2 (the reachable ceiling for their piles); the
behaviour under test (locked/never-reachable-in-practice) is unaffected —
2 is still unreachable given the actual triples those fixtures ever earn.

Verified none of the 37 shipped level JSON files (Assets/Resources/Levels)
violate the new bound before landing it (checked via a one-off script over
the raw JSON — not committed).

## Tests added

- `PartialInformationTests.EveryItemLockedFromTheStart_EndsAsJam_BeforeFirstMove`
  — the actual hole: a pile of nothing but locked items ends as
  `ShelfJammed` straight out of the constructor, no `TakeItem` call at all.
- `LevelTests.LockedAfterTriples_AboveWhatThePileCanEverReach_IsRejected` /
  `..._AtTheReachableCeiling_IsAccepted` — the Level-side bound.
- `BoardSaveTests.Restore_WithEarnedTriples_DoesNotFalselyJamOnRebuild` —
  the restore-safety case above.

## Not done

Nothing deferred; scope was Board + Level + tests, all covered.
