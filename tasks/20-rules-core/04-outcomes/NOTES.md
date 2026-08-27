# The acceptance criterion asked for an impossible state, 2026-08-27

`verify:failed` had stood since 26.08 on item 2: *"last item empties the pile
and fills the shelf → Win"*. The verifier proved, and 40 000 random games
confirmed, that no such state exists — a pile carries every kind in multiples
of three, `Shelf.TryMatch` removes each triple as it forms, so the shelf is
empty exactly when the pile is. The win-before-jam ordering is real, correct,
and **vacuous**: the two outcomes never contend for the same move.

That verdict ended with the right call and stopped short of it: *"rewriting an
acceptance criterion is a human's call, not an agent's."* The owner asked for
the task tree to be corrected, so it was corrected here — and the change is
worth their eye, because GOAL.md forbids fitting criteria to results.

**It is not fitted, and this was tested rather than argued.** The old criterion
named a state the rules make impossible; the new one names a state the rules
make reachable *and* that a plausible refactor breaks. The re-verifier
established the second half by mutation: on a copy of `Board.cs` outside the
repository, with fullness judged at placement time instead of after the match,
the new test's scenario flips from `Win` to `ShelfJammed`. A criterion that can
fail is not a criterion fitted to what the code happens to do.

## What the tests pin now

| item | test | what breaks it |
|---|---|---|
| 2 | `FinalPlacementTakesTheLastSlotAndMatches_IsAWin_NotAJam` | judging shelf fullness before the match instead of after |
| 3 | `AtAWin_TheShelfIsEmpty_...` | a win that strands items on the shelf |
| 3 | `PileHoldsEveryKindInTriples_...` | `Level` accepting a kind in non-triples |

## Board.cs:120-130 kept, not deleted

The unreachable branch stays. Deleting it would let a refused placement fall
through with the item already in `_taken` — neither in the pile nor on the
shelf, lost silently. An outcome is the safe failure. What was wrong was the
claim that this branch protected the ordering; that is now stated in the code
and in `task.txt`.

## Two errors the re-verifier caught in the first cut

Recorded because both were mine and both were prose, which is where this
project keeps losing accuracy:

- `ShelfJammed_UnmatchedKindsFillTheShelf` said "jams at slot ten". It jams on
  the **ninth** take — the ninth is the one that fills a nine-slot shelf. The
  comment was inherited unchecked; corrected.
- `AtAWin_TheShelfIsEmpty_...` promised it would fail "if a future rule lets a
  pile hold a kind in non-triples". It would not: it builds its own triple
  pile. The real tripwire is now a separate test asserting `Level` rejects such
  a pile, and the false promise is gone.

Both slipped through because they described the right idea about the wrong
line — a test that passes is not evidence its comment is true.
