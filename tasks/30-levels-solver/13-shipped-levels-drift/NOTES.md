# The drift was the fix, not decay

Both sides looked equally plausible before checking: maybe someone touched
`ship_levels.py`/`generate.py` after 28.08 and the shipped files fell behind,
or maybe the shipped files were hand-edited and the generator never caught
up. `git log --since=2026-08-25 -- tools/solver/` is empty — nobody touched
the generator. `git log --follow -p -- .../l32_room11_pile2.json` finds
exactly one change: commit `8fa3651` ("Three levels were harder than their
neighbours for one measurable reason each", 2026-08-28 17:09 +0300, same day
as the 28.08 audit but *after* it ran).

That commit is a deliberate, measured design fix, not a slip: l32, l34, l35
sat at 66.0/63.7/78.7% win rate against 85-100% for their room-mates, and the
commit message walks through the diagnosis (risky-fork frequency, not level
structure) and the fix per level, checked against a second weaker policy and
against `dotnet test`/`pytest` before and after. It edited only three files —
`git show 8fa3651 --stat` — and kept the pre-edit originals under
`tasks/30-levels-solver/level-originals/`, which is what a deliberate,
reversible edit looks like, not an accident.

Diffed each file against its kept original to confirm the diagnosis was
mechanical, not just narrative:

- `l32_room11_pile2.json`: item 1 `prop_lamp` → `prop_ball`, item 58
  `prop_ball` → `prop_lamp` (the "swaps one item's kind" from the message).
- `l34_room12_pile0.json`: item loses `"blocked_by": [9]` → `[]` (the
  "loses one blocked_by edge").
- `l35_room12_pile1.json`: six items' `locked_after_triples` 2 → 1 (the
  "moves one locked kind's threshold from 2 to 1").

**Source of truth: the shipped files.** `ship_levels.py` only knows random
search under a seed (`generate_level` + `solve` + `greedy_wins`, retried up
to `MAX_ATTEMPTS`) — it has no parameter for "swap item 1's kind" or "drop
this one blocked_by edge". Teaching it one, just to reproduce three
hand-tuned edits, would be scope creep no task asked for and would still not
be "the generator caught up" in any real sense — it would be a special case
bolted on to make a diff tool happy. The three files stay hand-edited; the
generator stays what it was.

## The test (tools/tests/test_ship_levels.py)

`test_regeneration_matches_the_shipped_files_byte_for_byte`: for all 34
untouched levels, seed-7 regeneration must equal the shipped file byte for
byte (this is what the 28.08 audit ran by hand and flagged as unenforced —
VERIFY.md item 1: "nothing in the test suite enforces that equivalence going
forward"). For the three hand-edited files, content is pinned by SHA-256
instead (`HAND_EDITED_SHA256` at the top of the test file, with the same
reasoning as above in a comment) — regeneration for those three is expected
to disagree forever, and the hash catches any *further* unrecorded change to
them, hand-made or generated.

Mutation-checked: temporarily changed one byte of `l01_room01_pile0.json` on
disk, reran just this test — failed with the expected message, reverted,
confirmed `git status --short` clean and full suite green again.

`.venv/bin/python -m pytest tools/tests -q` → **248 passed** (was 247 before
this test; `.venv` is the repo's venv, plain `python3 -m pytest` has no
pytest installed — noted for whoever runs this next).

`dotnet test build/core-tests/core-tests.csproj -v q --nologo` → **279
passed**, untouched by this change (levels on disk were not touched, so
`HeadlessRunTests` sees the same 37 files as before).

`python -m tools.solver.ship_levels --out /tmp/... --seed 7 && diff -rq ...
--exclude="*.meta"` → **3 differences**, on exactly l32/l34/l35, and expected
to report exactly these three from now on — a literal empty diff was the
task.txt VERIFY's original ask, but is unreachable without either reverting
the balance fix or building a patch mechanism into the generator for a
one-off; the pytest hash-pin is the honest substitute per this task's own
SCOPE ("если файлы правились руками осознанно — истина файлы, ... ИЛИ
проверка должна закреплять файлы напрямую").

## Room 12's last corner — checked separately

Owner observation: "the last corner of room 12 doesn't open" — same rooms as
l34/l35. Investigated `game/Assets/Core/RoomPlan.cs` and the View code that
decides quarter/corner opening, and whether the l34/l35 hand edits (dropped
`blocked_by` edge, `locked_after_triples` 2→1) could be the mechanism.

**Not connected to the l34/l35 content edits.** Which room quadrant is drawn
clean comes only from `Level.PileIndex`, never from pile content — documented
in-code at `game/Assets/View/DebugGameView.cs:379-383`: "Which quadrants are
clean comes from the pile index, not from the level data... they carry id,
kind and blocked_by and nothing spatial." `blocked_by` and
`locked_after_triples` are exactly the fields l34/l35 touched, and this code
path never reads them.

**But a real, separate mechanism does explain the observation.**
`RenderRoom()` (`DebugGameView.cs:490-539`) sets `cleaned =
Mathf.Clamp(_level.PileIndex, 0, 4)` (line 531): while playing a room's 4th
pile (`PileIndex == 3`), only 3 of 4 quadrants render clean. Deliberate, per
the comment at lines 528-530 — the board "never needs to show four" because a
room's last pile is supposed to show the whole clean room instead, via
`ShowRoomTransformation()`, called at line 1193 only `if (lastPileOfRoom)`.

For room 12 that payoff never fires. Room 12's last pile (`l37`, `PileIndex
== 3`) is also the last pile of the whole house. `Finish()`
(`DebugGameView.cs:1114`) checks `_plan.Next(_level) == null` at line 1149 —
true only for `l37` — and takes that branch to `ShowEndingCard()` and
**returns** at line 1172, before reaching the `ShowCard(...)` /
`if (lastPileOfRoom) ShowRoomTransformation(_level)` block at lines
1180-1193. `ShowEndingCard()` (line 1280) shows the ending kitten, its
actions, and the way back to the map; it never calls
`ShowRoomTransformation`, and `ShowCard()` itself unconditionally calls
`HideRoomTransformation()` (line 1228) first.

Net effect: winning `l37` still completes the pile logically
(`_progress.CompletePile(3)` at line 1135 runs before the branch, room 12 is
recorded done in save data), but the player is never shown the 4th corner —
or the whole room — turning clean: the before/after that normally proves a
room's last corner opened is replaced by the ending card for this one room
only, because it is also the house's last room.

This is a `Finish()` control-flow gap (the early return for the house's last
level skips the transformation before that level's own room-completion
payoff runs), independent of the l32/l34/l35 content edits — same rooms,
unrelated cause. **Not fixed here** (SCOPE: describe, don't fix) — worth its
own task.

## status:todo → done, 2026-09-02

Both VERIFY items from task.txt hold under the honest substitute above:
`pytest tools/tests` green (248 passed), `diff -rq` against seed-7
regeneration reports exactly the three expected, recorded differences — no
unexplained drift. `verify` stays `pending`: a context does not sign off its
own tests (`tasks/README.md`, the independence rule).
