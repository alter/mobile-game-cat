# Why hidden kinds, and what it invalidates

The whole pile is visible today: tiles in a flat grid, blocked ones merely
dimmed to 35% opacity (see `build/playtest/index.html`). Nothing is concealed,
so a level solves at a glance. The tension this genre runs on - not knowing
what lies underneath - is absent, and that is the likeliest reason the
prototype wore thin after twenty minutes (08-measure-playtime's verdict:
"mildly enjoyable, doubtful it lasts").

This is the cheapest fix available and repairs both the mechanic and the
metric: one field on the item, one condition in the renderer. It buys
discovery, slows play, and turns sorting back into a puzzle. Sheep a Sheep,
named as a reference in the MVP itself, works exactly this way.

## It invalidates the measured win-rate table

The table in 04-difficulty-curve - 98% / 87% / 66% at 36 / 48 / 60 items - was
measured by a policy that could see every kind in the pile before choosing a
move. Once kinds are hidden, a player cannot plan ahead that way, and those
rates will fall, possibly a long way. **That table must not be reused, or
trusted, once this task ships.**

The solver (02-solver) remains useful as a feasibility oracle - "does a
solution exist" - but stops being a difficulty oracle, because it too assumes
full information. Difficulty must be measured separately, by a policy that
only sees currently-available (reachable) kinds - see
10-remeasure-curve-partial-info.

**Order matters and is not arbitrary:** hide first (this task), measure second
(10), tune pile size third. Tuning pile size against the old, full-information
numbers would be tuning a game nobody will actually play, since nobody plays
with the solver's perfect information.

Source: cat-shelter-tasks.md lines 554-576; DECISIONS.md D3.

## status:todo → done, 2026-08-26

The label lagged the repository. Against this task's VERIFY list:

- `Board.IsRevealed` reports an item hidden while anything covers it and
  revealed once every blocker is taken; `PartialInformationTests` covers both
  directions, and `tools/solver/rules.py::is_revealed` mirrors it.
- `bash build/check-core-purity.sh` passes — no engine reference in `Core`.
- The debug view renders buried items as blank tiles, per D3, not faded.

Fixed here on the same day: `DebugGameView` carried its own hand-copied version
of the reveal rule instead of calling `Board.IsRevealed`. Two copies of one rule
drift apart, and the view is not covered by tests; it now asks Core.

**What is done is the hiding, not the consequence.** The re-measurement is
`10-remeasure-curve-partial-info`, still `todo`, so the warning above stands:
the 98/87/66 table describes a game nobody plays.

`verify` stays `pending` — the checker also edited Core.

---

## The prediction in this note was measured and was wrong — 2026-08-27

This note says hiding will make win rates "fall, possibly a long way". It was
a reasonable expectation and it did not survive measurement.
`30-levels-solver/10-remeasure-curve-partial-info` measured one policy playing
the same levels with the buried pile hidden and with it visible: the difference
is **0.0 ± 1.2 percentage points**. D3, the decision this task implements,
carries that correction in its own text — *"this decision aimed at the wrong
half"* — and the correction was never propagated back here, which a
verification of this task caught.

The reason is structural and could have been foreseen. A move is chosen among
*reachable* items, and a reachable item always shows its kind; hiding removes
only the planning past them, and 17 to 28 items are reachable at once — a
quarter to a half of the pile. The level solved at a glance because the front
is wide, not because the pile was visible.

**Hiding stays and this note is not an argument to remove it.** It is free, the
genre does it, and whether it changes how the game *feels* — the small reveal —
is a question for `07-outsiders-playtest`, not for a simulation. What must not
happen is this task being cited as evidence that the game was made harder,
because it was not.

Re-measured again on 27.08 after D15: the price of hiding is 0.6 / 0.0 / −0.2
percentage points across the three bands.
