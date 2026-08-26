# Why one pile is not one room

A level was tied 1:1 to a room (`level_01 -> room_01`), which is the only
reason there were twelve of them. Nothing justified the coupling. Levels are
free - the generator makes a hundred per run - while **rooms are the expensive
part**: a dirty/clean art pair per room, and a hundred rooms would mean two
hundred images.

So a room now holds several piles; a level clears one of them. Twelve rooms
stay, content triples to 37 levels (curve: 1,2,3,3,3,3,3,3,4,4,4,4 - see
12-piles-per-room-curve). The fiction improves too: a room is not tidied in
one sitting, and "three heaps of junk" reads as obviously unfinished where "the
room advances every third level" would read as an arbitrary rule.

**Two-tier payoff.** Each level visibly clears a corner (small, frequent
reward). Completing a room changes the light, moves the cat, and may hand over
a possession (large, earned reward). Today it is all-or-nothing per room.

**Art cost stays flat.** A room still needs exactly one dirty and one clean
background; the clutter is composed from the ~30 prop sprites already planned
for 40-art. "A third cleared" is just fewer sprites on the same background - no
new room art for the extra levels. (A further saving - a grey-brown grade
instead of a second background - is not budgeted until an artist proves it at
thumbnail size; a real "before" is usually different light, not a filter.)

**Makes hidden kinds (09) physical.** Drawn as an actual heap in a room, items
overlap for real, so "you cannot see what's underneath" stops being a 35%
opacity convention (as in `build/playtest/index.html` today) and becomes
something the eye reads directly.

Source: cat-shelter-tasks.md lines 484-514 (pre-restructure); DECISIONS.md D2.

## status:todo → done, 2026-08-26

The label lagged the repository: the 37 definitions had already shipped and the
game loads them. Checked against this task's own VERIFY list before flipping it:

- 37 files in `game/Assets/Resources/Levels/` — not `Assets/Levels/`; they moved
  under `Resources/` so the player can load them at runtime, and the OUTCOME
  line was corrected to match.
- Every `room_id` + `pile_index` pair unique; piles per room come out
  `[1,2,3,3,3,3,3,3,4,4,4,4]`, matching `tools/solver/pacing.py`.
- `solve()` returns a solution for all 37 — zero dead ends.

`verify` stays `pending`: the context that ran these checks is the one editing
the rules engine, and it cannot sign its own work (`tasks/README.md`, the
independence rule).

### The first VERIFY item was not actually met — fixed the same day

"`python -m tools.solver.ship_levels` produces exactly 37 files" was checked by
looking at the 37 files on disk, which is a different claim. The script still
shipped twelve `level_NN.json` — one per room, from the era when a level *was*
a room — and knew nothing of `pacing.py`. **The 37 levels the game loads could
not be reproduced from the repository at all**; whatever made them was not
committed.

`ship_levels.py` now walks `pacing.level_map()`, sizes each pile by its room
(`generate.items_for_room`, renamed from `_items_for_level` because the band
belongs to the room, not to the level's place in the run), verifies each
candidate with `solve()` and writes `l<seq>_room<room>_pile<index>.json`. On
seed 7 it produces exactly the filenames now on disk, the same 36/48/60 bands
and locked kinds in rooms 9–12 only.

`tools/tests/test_ship_levels.py` turns all three VERIFY items into runnable
checks — file count, unique room/pile pairs against the pacing curve,
solvability of every level — plus the band and complication rules. Six tests,
green (67 → 73 Python tests).
