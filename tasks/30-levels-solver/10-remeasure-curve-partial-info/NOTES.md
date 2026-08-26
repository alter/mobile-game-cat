# Why this task exists at all

This task exists purely because 09-hidden-kinds invalidates the numbers
already measured in 04-difficulty-curve. The measured table - 98% / 87% / 66%
at 36 / 48 / 60 items - came from a policy that could see every kind in the
pile before choosing. Under hiding a player cannot plan ahead the same way,
and those rates will fall, possibly a long way.

The solver remains useful as a feasibility oracle ("does a solution exist")
but stops being a difficulty oracle once information is partial. **Order is
load-bearing:** hide first (09), measure second (this task), tune pile size
third (a follow-on tuning pass, not itself numbered in M3). Tuning against the
old numbers would be tuning against a game nobody will actually play, since no
real player has the solver's perfect information.

Source: cat-shelter-tasks.md lines 566-576; DECISIONS.md D3.

---

# The measurement, 2026-08-26

`python -m tools.solver.measure` is the source of every number below; nothing
here is typed by hand. 400 generated, solver-verified levels per band, nine
shelf slots, no mistakes:

| pile size | rooms | open at once | shelf-only | reachable-aware, pile hidden | reachable-aware, pile visible | price of hiding |
|---|---|---|---|---|---|---|
| 36 | 1–4 | 17.0 | 98.0% | 99.0% | 98.8% | -0.2 pp |
| 48 | 5–8 | 24.2 | 87.0% | 96.5% | 96.5% | 0.0 pp |
| 60 | 9–12 | 28.1 | 69.5% | 89.8% | 91.0% | 1.2 pp |

With 12% careless taps: 95.2 / 85.5 / 59.8 for shelf-only, 99.2 / 94.2 / 88.8
for the reachable-aware player, price of hiding 0.6 / 0.0 / -1.8 pp.

The same measurement on the 37 levels actually shipped, 200 games each
(`--shipped`), band average and per-level range:

| band | shelf-only | reachable-aware |
|---|---|---|
| 36 items | 97.2% (90.0–100) | 98.4% (92.0–100) |
| 48 items | 83.4% (56.5–97.0) | 95.1% (84.0–100) |
| 60 items | 64.4% (31.5–86.0) | 90.8% (67.5–99.5) |

The shipped levels behave like the generated ones on average and vary wildly
one to the next: `l24_room09_pile2` wins 31.5% / 67.5% and is the hardest level
in the game, while several later levels of the same band sit near the top of
their range. Difficulty runs on one knob that is constant within a band, so
nothing evens this out.

**Hiding costs between -1.8 and +1.2 percentage points. It is noise.**

## Why, and why it was predictable

A move is chosen among *reachable* items, and a reachable item always shows its
kind — that is what `IsRevealed` means. Hiding cannot change the choice being
made; it can only remove the ability to plan past it. And there is little to
plan past: **17 to 28 items are open at once**, a quarter to a half of the
pile. The player picks from a wide, fully visible front, and what lies under it
barely matters until it surfaces.

So D3's stated purpose — "with the whole pile visible, a level solves at a
glance" — is not what the numbers show. The level solves at a glance because
the *front* is wide, not because the *pile* is visible. Hiding was aimed at the
wrong half.

This is not an argument for removing it. Hiding is free, it is what the genre
does, and it may well change how the game *feels* — discovery, the small
reveal — which is a question for the outsiders' playtest, not for a simulation.
It is only an argument against the claim that it made the game harder.

## The old table was not wrong; it described a simpler player

`shelf_only_policy` reproduces the retired measurement's player — "prefers
kinds already 2-of-3 on the shelf" — and reproduces its numbers: **98.0 / 87.0
/ 69.5** against the recorded 98 / 86.5 / 66. The 98/87/66 row is retired for
describing a weaker player than the one who will actually play, not for being
miscalculated.

One extra habit — noticing which kinds have most copies open in front of you,
which anyone picks up within a room or two — is worth **+20 pp** at 60 items
(69.5% → 89.8%). That single habit moves difficulty seventeen times further
than hiding does.

## What actually sets difficulty, in measured order

1. **How well the player plays** — 20 pp between two plausible habits.
2. **Pile size, through the width of the open front** — 17 → 28 items reachable
   at once across the bands, about 9 pp for the reachable-aware player.
3. **Carelessness** — 12% random taps costs 1–2 pp for that player.
4. **Hiding buried kinds** — nothing measurable.

The lever nobody has pulled is inside (2): the *front*, not the pile. The
generator gives each item 0–2 blockers across `item_count // 8` layers, which
leaves a quarter of a 60-item pile open at any moment. Deepen the stack and
difficulty moves; add items to a flat pile and it mostly does not.

**Deliberately not tuned here.** Tuning is the third step of D3's order — hide,
measure, tune — and this task is the second. Choosing new numbers needs
`07-outsiders-playtest` first: five people will say whether ~90% wins feels
easy or fair, and no simulated player can answer that.

## Consequence for metric 4, which nobody should discover in production

At ~90% wins for a competent player, roughly one run in ten ends in a jam. The
fourth go/no-go metric is "tapped *one more shelf* > 15% **of those who reached
the lose screen**", and the review of 2026-08-24 already warned that the
denominator would be nearly empty because a sensible player does not lose. That
warning was answered by making levels bigger; this measurement says bigger did
not do it. Either the lose screen has to be reachable more often, or metric 4
will be read off a handful of players. GOAL.md already requires recording that
denominator separately — it now needs a threshold of its own, fixed before the
money moves (`80-live-validation/00-thresholds`).

## About the VERIFY list

Item 3 — "new table is lower than the 98/87/66 baseline at every band" — cannot
be satisfied as written, and should not be. It compares two different players.
The reachable-aware numbers are *higher* (99.0 / 96.5 / 89.8) because that
player thinks more, not because hiding helps. The comparison the item was
reaching for is the last column: one policy, pile hidden versus pile visible.
That column is 0.0 ± 1.2 pp.

Items 1 and 2 are met. The table is script output, and the player policies take
`(choices, shelf)` and nothing else — `test_measure.py` asserts that from their
signatures rather than by review, so a future edit cannot quietly hand them the
buried pile.

## A mistake worth recording

The first version of this script reported 100% wins everywhere. Its scoring
tuple ended in `-item_id`, so every tie went to the lowest id — and ids follow
the generator's layer order, which made the policy quietly excellent. With a
wide-open front most moves are ties, so that one detail *was* the result: 100%
against 79%. It was caught only because the number disagreed with an earlier
C# probe and the disagreement was chased rather than explained away. Ties now
return every equally-good move and the caller picks among them at random, which
is what a player who cannot see item ids does.
