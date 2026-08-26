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
