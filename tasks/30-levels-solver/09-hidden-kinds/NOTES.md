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
