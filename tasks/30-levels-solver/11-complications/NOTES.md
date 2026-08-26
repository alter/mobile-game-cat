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

- `Item.LockedAfterTriples` gates both `GetAvailable` and `IsRevealed`;
  `PartialInformationTests` pins that it opens exactly at N triples and not
  before. `tools/solver/rules.py::is_locked` mirrors it, and two conformance
  tests compare the implementations on locked levels.
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
