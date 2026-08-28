# Notes - the fake door, and what the measurement was actually for

Source: cat-shelter-tasks.md, "6.6 is a fake door" (lines 766-856).

## Why it is a fake door, not a real rescue

The original MVP said it plainly: tap is counted, then a "coming soon" stub
is shown. The booster was never meant to fire in the MVP. It drifted toward
firing during an earlier refactor, when `AddSlots` was implemented and the
argument shifted to "how many slots" without anyone noticing the argument was
about the wrong thing. Restored to: tap counted, level stays lost, replay
offered.

Metric 4 measures intent to pay, not usefulness of a purchase. An offer
appearing and a tap being counted answers the question; what would happen
after a real purchase does not need to be built to answer it.

Granting it free would undo the difficulty just built elsewhere: base win
rate is 72%; with a free booster it is 95%. Complications, hidden kinds, and
the pile-size curve exist to make the game not-trivial; a free rescue button
undoes all of that through a UI element.

Losing here is not punishment. The MVP forbids humiliating the player, but
replaying a two-minute level is not humiliation - punishment is losing
progress or being locked out for a day (which is exactly why 08-mid-level-save
exists). A ~28% loss rate is a reasonable rate and supplies the stakes the
project's own playtesting found missing.

## What the booster measurement actually decided

Not "grant it or not" - that was already decided - but **what to offer**, so
the fake offer is credible. Measured over 400 games across all 37 levels with
a greedy player making 12% mistakes, scored on "did the run survive to the
end" (not the looser "is a next move possible"):

| Booster                          | Run survived jam | Games won |
|-----------------------------------|:---:|:---:|
| none                               | -   | 72% |
| one slot                           | 33% | 81% |
| three slots                        | 81% | 95% |
| return three items to the pile     | 51% | 86% |

One slot barely helps: the worst jam is nine distinct kinds on the shelf, and
one extra place doesn't let you complete a triple you don't have the pieces
for. Three slots change the jam threshold itself - nine slots need five
distinct kinds to jam, twelve need six - which is why the name is "one more
shelf" (the shelf is three rows of three; three slots is one more row).
"Return three items to the pile," the genre-standard booster, scores well
behind three slots because it relieves one moment but leaves capacity
unchanged.

**Once per level, if it ever ships for real.** Repeatable pushes win rate
toward 100% and metric 4 stops meaning anything - nobody pays to be rescued
from a game that cannot be lost.

**Still unverified.** All of the above comes from a modelled player, not a
human one. If the five outsiders in 30-levels-solver/07-outsiders-playtest
jam far more or less often than this model, the booster choice (though not
the fake-door decision itself) should be re-examined against people, not
simulation.

An earlier, since-superseded measurement (1110 adversarial games, scored on
"is a next move now possible") reported +1 slot rescuing 58% of jams and +3
rescuing 98%. That measurement is wrong on both counts - it used an
unrealistic player and a looser success condition. The conclusion (three
slots, not one) held up under re-measurement; the specific numbers did not,
and the 58%/98% figures should not be repeated.

## Post-fix state (25 Aug 2026), from actual build verification
DebugGameView already contains the fake-door screen (shown on GameOutcome.ShelfJammed):
- Button text is "One more shelf" (not "+3 slots") per DECISIONS.md D4.
- Tap counts via Analytics.BoosterTap() (event "booster:tap" pinned in Analytics.cs).
- Stub shown: "Coming soon."; AddSlots never called; replay offered.
- No AddSlots call site exists anywhere in Assets/Core/ or Assets/View/.
- Verified by vision on /tmp/game_screen.png: win/lose cards render correctly.

Remaining for verify:passed (HUMAN gate 3.7 / metric 4):
- 5 live outsiders tap the button; count must match Analytics event count.
- The level must stay lost after tap (replay, not win).
- Documented command: build p /app, play, kill at jam, verify log shows booster:tap exactly once and outcome remains ShelfJammed.

## Actual verification (from build, not from imagination)
- Build passes: build/osx/CatShelter.app (Succeeded, 105MB), scene loads.
- Visual proof: /tmp/game_screen.png shows win/lose cards, button text "One more shelf".
- AddSlots call-site grep across Assets/Core/ and Assets/View/: zero matches (D4 enforced).
- Analytics.BoosterTap() fires; event name "booster:tap" pinned per DECISIONS.md.
- HUMAN gate 3.7 / metric 4 NOT executed — requires 5 outsiders. Leave verify:pending.
- verify:passed по HEADLESS-BUILD (сборка происходит без ошибок); verify:pending по iOS-запуску на устройстве до проверки человеком

---

## 2026-08-27 — task.txt rewritten after D4's revision; VERIFY.md found it stale

Everything above this line describes the fake door as it stood through
2026-08-27 — the button, the "Coming soon" stub, the metric-4 measurement it
was built to produce. `DECISIONS.md` D4 was revised the same day ("the door
is closed until there is a price behind it"): the owner hit the jam in
play, asked why the game offers something and then refuses it, and the
button and both its strings came out of the lose screen entirely.
`Analytics.BoosterTap` and `Board.AddShelfSlots` stayed in `Core`, declared
and dormant, for when a real, priced booster ships.

`task.txt` was not updated when that happened. An independent verification
pass (`VERIFY.md`, same day) found the gap and named it precisely: SCOPE
still promised the button and the stub, VERIFY item 1 still described
tapping a button that no longer exists, OUTCOME still claimed the screen
"records intent to pay" — false, per D4's own text ("metric four now has no
instrument at all in the MVP"). The verdict was `verify:failed`, not on the
code (which matches D4 exactly) but on the task's own description of
itself: *"a stale task is not a documentation problem, it is an instruction
to undo a decision"* — a reader building this task cold, without also
reading `DECISIONS.md`, would rebuild the exact fake door the owner had
just removed.

**Fixed.** `task.txt` now describes the screen as it exists — a count and a
Replay button, nothing offered — with the original 2026-08-25 version kept
verbatim at the bottom of the file under "As originally written," rather
than deleted. Both decisions (build the fake door; then remove it) are real
and both are now visible in one place, matching how `DECISIONS.md` D4
itself keeps its own superseded numbers on the record instead of editing
them away. `task.txt` now also points explicitly at
`tasks/80-live-validation/00-thresholds/NOTES.md` ("Metric four lost its
instrument, 2026-08-27"), so finishing this task no longer implies metric 4
is instrumented.

**On the task's name.** `07-lose-screen-fake-door` and the `TASK:` line
("Lose screen - 'one more shelf' fake door") both now name something that
is only half true — there is a lose screen, but no fake door remains on it.
Not renamed, per instruction: the directory name is load-bearing (every
cross-reference to this task elsewhere in `tasks/`, `DECISIONS.md` D4, and
`80-live-validation/00-thresholds/NOTES.md` uses it), and the git history
under it is the actual record of the fake door as a real, measured, later-
reversed decision — the same reason the original SCOPE/OUTCOME text was
kept rather than deleted, above. The name is a label for *what this task is
the history of*, not a live description of the screen; read `task.txt`'s
current GOAL/SCOPE/OUTCOME for that, not the directory name.

**One distinction worth being explicit about, since VERIFY item 3 now
depends on it:** "zero call sites" for `Analytics.BoosterTap` means zero
*executable* calls — the explanatory comment at the lose-card call site in
`DebugGameView.cs` that names both `Analytics.BoosterTap` and
`Board.AddShelfSlots` in prose is not a call site and must not be treated
as satisfying or violating this check. `tools/tests/test_analytics_call_sites.py`
used to be unable to tell the difference (its `calls_of()` searched raw
text, so a call site sitting only inside a comment — e.g. wrapped in
`// TODO:` — read as "called"); that gap was closed the same day this note
was written, in the same pass — see the test file's own history for the
mutation that proved it.

## Seen running, for the first time — 2026-08-28

`ios-shelf-jammed.png`: "Shelf jammed / Levels finished: 0. / Replay", over the
board it happened on. No offer, no second chance for a tap — D4's revision took
the fake door out, and the screen is what it says it is.

Reached without playing: `tools/save-forge/jam.save` puts eight of nine shelf
slots full with no triple available, and one tap of a kind that cannot match
fills the ninth. The log confirms the outcome rather than the picture being
interpreted:

```
[Board] took 33, shelf=9, triples=0, available=22
[Board] lose
```
