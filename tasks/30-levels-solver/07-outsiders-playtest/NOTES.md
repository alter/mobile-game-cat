# Why this gate exists and why it is easy to skip

Between M0 (does the promise sell) and M8 (do players retain and pay) there is
no check on whether the game is any good. Three weeks of building rest on the
unexamined assumption that the core loop is enjoyable, until the last $400 of
M8 has already been spent.

At the end of M3 a playable game exists in debug rectangles - the cheapest
possible moment to find out the loop is dull: no art, no shell, no store
account required. Five people, ten minutes each, one question.

This is the most likely gate to be skipped in the whole project, precisely
because it needs other people and is uncomfortable. HUMAN tasks in this tree
exist because an agent (or the author) reports that its own work looks good;
nobody is a reliable judge of whether their own game is fun. Note 2.6 - the
author playing one level - does not substitute for this.

**If 3 of 5 say no:** the correct response is to change the mechanic or stop.
Either is far cheaper here than after M6, when art, shell and a paid test have
already been spent on the same mechanic.

## status:done → todo, 2026-08-27 — the label was ahead of the work

This task carried `status:done` while `answers.md` held five empty forms: no
name, no date, no answer. Nobody has played. The label is back to `todo`.

Worth stating plainly, because it is the exact failure GOAL.md names: *"the
report running ahead of the check"*. A `done` on the one gate an agent cannot
perform is worse than a `todo` — it removes the task from view. Of the 41
tasks marked done in this tree, eight carry `verify:passed`; the rest are
claims, and this one was false.

## The prototype is current again, 2026-08-27

Regenerated and checked:

```
.venv/bin/python build/playtest/make_playtest.py
  wrote build/playtest/index.html (107 KB), 37 levels, pile sizes [36, 48, 60]
.venv/bin/python build/playtest/verify_playtest.py
  OK level 1..37: win
  37/37 levels also jam identically in both engines
```

It was stale, but not in the way the paragraph below claimed. **The level data
had moved on** — the embedded levels no longer matched
`game/Assets/Resources/Levels`, which were reshipped after the last build.
That is the real reason to regenerate, and it is now done.

**The `movesLimit` claim was already false when it was written here.** The
string appears nowhere in `index.html`, and `verify_playtest.py` asserts its
absence outright (`assert "movesLimit" not in html, "D1: no move limit
anywhere"`). Its own docstring records that an earlier version of the checker
mirrored a `movesLimit` the page no longer had — this note copied that
already-corrected mistake forward.

**The "35% opacity" claim was not staleness but a decision.** Gate 3.7 runs on
the *visible* pile: kinds always shown, covered items dimmed. That is this
task's own SCOPE ("Not hidden kinds or complications — this gate runs on the
visible pile") and `make_playtest.py` states it in the module docstring and
drops `locked_after_triples` on the way in on purpose. Blanking the pile here
would have made the prototype disagree with the task, not agree with it.

Both wrong claims pointed the same way — *do not run the gate yet* — which is
why neither was caught. A reason to re-run a check is not evidence that the
check fails.

Source: `build/playtest/make_playtest.py` (module docstring),
`verify_playtest.py` (assertions), run output above.

## The build people will actually play, 2026-08-27 (evening)

Three changes, made because the owner will hand this to five people rather
than sit them in front of a rectangle demo.

**Props are drawn, not numbered.** The prototype coloured each tile by hue and
printed the kind's number on it, which asks a person to match digits — a
different task from the one the game asks. The 30 prop sprites already exist
in `game/Assets/Resources/Art`; they are now embedded as WebP data URIs at
128 px, about 150 KB for all thirty against 1.4 MB for the 256 px originals.
The whole page is 258 KB and still one file with no network.

This is a departure from the gate's stated design, and the departure is worth
naming: art was excluded here so that a "no" would be about the loop and not
about ugly rectangles. The reason it is acceptable is that the art cost
nothing — it was drawn for `40-art/01` months before this gate — so the
"cheapest possible moment" argument is untouched. The reading caution is the
other direction now: a **yes** is weaker evidence than it would have been,
because someone may be charmed by the props rather than held by the loop. Ask
the follow-up: *what would you have done next?*

**Covered items are marked by a dark tile, not by fading.** The old convention
faded a blocked tile to 34% opacity, which works on saturated rectangles and
fails on drawn props: a white mitten at 34% on a cream ground is gone, and
this gate's own rule is that every kind stays visible. The tile now carries
the state — white and raised means takeable, dark and flat means covered —
and the prop stays at full strength. Checked by rendering a 60-item pile at
true scale before wiring it in, not after.

**The pile fits on one screen.** Widened from 340 px to `min(358px, 96vw)`,
which puts eight tiles in a row: a 60-item pile is 8 rows and about 355 px,
inside the 56vh the container allows. It used to be 7 across, 9 rows, 398 px
— and a puzzle you have to scroll is a puzzle you cannot see.

**And the fake door is gone.** D4 was revised the same day and the "one more
shelf" button was removed from the game; it was still here. Leaving it would
have handed five strangers exactly the irritation the owner hit — offered a
way out, then refused it — while measuring nothing, since the tap was free.
The jam card now offers replaying the pile or finishing and answering.

Regenerated and rechecked after every one of these: `verify_playtest.py`
plays all 37 levels to a win and drives all 37 into a jam, both engines
agreeing, each time.

`make_playtest.py` also writes `hosted.html` — the same bytes without the
document wrapper, for putting the prototype behind a link someone can open on
their own phone. One generator, so the two cannot drift.

## The kitten is in the build now, 2026-08-27 (evening)

She sits above the board and changes pose as rooms are cleared: hunched for
rooms 1–4, up and alert for 5–8, curled asleep for 9–12. Those two thresholds
are the ones D8 names as the moments worth showing off, and the ones the game
itself uses. The card that appears when a room is finished now says *"Котёнок
изменился — посмотри на него"* at exactly those two crossings instead of the
same "the kitten is better" every room.

**Why this was worth adding to a gate about the loop.** The gate asks whether
someone would keep playing. What they would keep playing *for* was, until now,
a sentence in a dialog. A reward nobody can see is a reward that cannot be
weighed, and answering "no" to a game whose payoff was never shown tells us
less than the question deserves.

**What is honest about it and what is not.** The three bases are used exactly
as drawn — flat grey, three poses. What is **not** reproduced is the weathering
the real game applies at runtime: `CoatBuilder` dulls and dirties state 1 and
lifts it by state 3, so in the shipped game the change is grooming as well as
posture. That code is C# and copying it into the generator would be inventing
the look a second time, in a second language. So the prototype understates the
transformation rather than overstating it, which is the safe direction for a
gate: nobody will say yes because of a before/after this build does not show.

Nothing was invented and nothing was generated. If better cat art arrives it
drops into the same three filenames and the build picks it up.

Page is 289 KB with all three states embedded (8–11 KB each). Regenerated and
rechecked: `verify_playtest.py` still plays 37/37 to a win and drives 37/37
into a jam, both engines agreeing.
