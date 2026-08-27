# Why this tests a pattern, not a feature

Games of this kind that run for hundreds of rounds do not scale by enlarging
the board. They introduce a new complication every thirty to fifty rounds -
blockers, reordering, a different way items arrive - and it is the change, not
the size, that holds the player. Twelve levels of "the same thing, more of it"
is the shape that bores, and that was the shape measured in 08.

One complication, introduced once, is enough for the MVP: it proves the rhythm
works and makes at least one room memorable. The full ladder - in ascending
cost: hidden kinds (09, already MVP), locked items (this task, MVP), a
temporarily blocked shelf slot, paired items taken only consecutively, a kind
requiring four matches instead of three, external supply (items arrive mid-run
rather than lying in the pile from the start), and full reordering after every
triple - is post-MVP design and belongs in cat-shelter-mvp.md section 14, not
in this milestone.

**The delivery rule that must survive into implementation:** one complication
is introduced in its own room, explained wordlessly by the level's own
construction, and only then combined with earlier ones. The room where
something new appears is the room that gets remembered - which also treats the
sameness of the twelve rooms found in 08.

Source: cat-shelter-tasks.md lines 577-586; cat-shelter-mvp.md section 14
("The complication ladder — how the game grows into hundreds of rounds").

## status:todo → done, 2026-08-26

The label lagged the repository: the locked item is implemented, tested and
present in shipped data.

- `Item.LockedAfterTriples` gates `GetAvailable` — **and, until D15 the next
  day, also gated `IsRevealed`; it no longer does, see the correction dated
  2026-08-27 below.** `PartialInformationTests` pins that it opens exactly
  at N triples and not before. `tools/solver/rules.py::is_locked` mirrors
  it, and two conformance tests compare the implementations on locked
  levels.
- 16 of the 37 shipped levels carry locked kinds (rooms 9–12, six items each,
  always in triples, one threshold per kind — asserted by
  `HeadlessRunTests.LockedKinds_AreValid`).
- `bash build/check-core-purity.sh` passes.

Two corrections made the same day:

- `Board.LockThreshold` was dead — a constructor parameter, validated and
  stored, read by nothing. Locking has always worked through
  `Item.LockedAfterTriples`. Removed, so there is one mechanism rather than the
  appearance of two.
- A board where every remaining item was locked had **no outcome at all**: no
  legal move, `IsOver` false, and on a phone no way off the screen. It is now a
  `ShelfJammed` — one outcome with the full shelf, because to the player they
  are the same thing: no move exists. Both implementations changed together.
  It does not occur on the 37 shipped levels (0 in 11,100 simulated games), so
  this was a trap set for the next batch of locked levels, not a live bug.

**VERIFY 2 cannot be closed by an agent.** "A late room reads as distinct from
an early room to someone who did not build it" needs that someone; it belongs
with `07-outsiders-playtest`.

`verify` stays `pending`.

## Corrections, 2026-08-27 — landed after this task's `verify:passed`

An independent verifier checked this task and set `verify:passed` on
2026-08-27, then found two things the verdict itself did not cover, because
they surfaced only while closing the gaps the same verification pass had
already found. **The verdict on file predates everything below**; a reader
should not assume `verify:passed` means these corrections were checked too.

**The stale sentence above is fixed.** `tasks/DECISIONS.md` D15 ("A locked
item is seen, not hidden," 2026-08-27) reversed the claim this NOTES.md made
the day before: `IsRevealed` no longer counts a locked item as hidden, in
both `Board.cs` and `tools/solver/rules.py`. The sentence above was never
updated to match and stated the pre-D15 behaviour as current fact for a full
day. Corrected in place rather than silently: struck through in spirit,
marked and dated, so the history of what this file claimed and when stays
readable.

**The title is a naming error, not a scope cut.** This task's title reads
"Three complications, one per room band." Its own GOAL and SCOPE, and
`cat-shelter-mvp.md` section 14 ("In the MVP only **one** complication from
this list is taken, task 3.11") all agree on exactly one: locked items.
Hidden kinds (09) is combined with, not introduced by, this task. Nothing
was cut to reach "one" — the title overclaimed from the start and the body
never did. **Read this as a title correction only.** If a later pass adds a
second or third complication, that is new scope on top of this task, not a
restoration of something dropped here.

**The conformance suite compared only outcomes, and both gaps were found by
mutation, not by review.** `tools/tests/conformance_test.py` +
`build/solver-bridge` are the only guard against `Board.cs` and
`tools/solver/rules.py` drifting apart. Until today they compared `outcome`,
`occupied` and `triples` only. Two properties turned out to be completely
outside that comparison, both found the same way — reverting a decision in a
scratch copy of *one* engine only and watching the real conformance suite
still pass:

- **Visibility (D15).** Reverting D15 in a scratch copy of `Board.cs` alone
  (Python left untouched) left all four conformance tests green. The bridge
  never asked either engine whether an item was revealed, so a locked item
  silently hidden on one side and shown on the other was invisible to the
  suite that exists to catch exactly this kind of drift.
- **Slot order (D16 — "the shelf neither compacts nor sorts").** Mutating a
  scratch copy of `rules.py` to place a new item in the *rightmost* free slot
  instead of the leftmost (C# untouched) also left all four tests green.
  `occupied` and `triples` do not change when only the slot changes, so a
  placement-order bug — the exact shape D16 pins down — had no way to surface
  through the old comparison either.

Both are now fixed: `build/solver-bridge/Program.cs` emits, after every
move, `IsRevealed` for every pile item and the shelf's contents by slot
(including empty slots, since a gap staying put is D16's whole point);
`tools/tests/conformance_test.py` compares both, move by move, and names the
item/slot and the move number on a mismatch. Re-running the same two
mutations against the corrected suite: the D15 mutation now fails on
`test_locked_items_agree` ("move 0 (took item 4): item 1 revealed
python=True csharp=False"), and the D16 mutation now fails on both
`test_csharp_and_python_agree` and `test_locked_items_agree` ("move 0, slot
0: python=None csharp='prop_casket'" / `'a'`). The unmutated suite stays
green throughout.

**The lesson, for whoever reads this next:** a conformance suite that
compares a summary (outcome, a count) instead of the full state can pass
forever while two engines quietly disagree on the parts of the state a
summary doesn't carry. Every time this project has caught that kind of gap,
it was by deliberately mutating one side and watching the suite fail to
notice — never by reading the comparison code and reasoning about what it
covers. If a fourth D-decision changes what one engine does without a
matching change to the other, assume the conformance suite does not see it
until proven otherwise the same way.
