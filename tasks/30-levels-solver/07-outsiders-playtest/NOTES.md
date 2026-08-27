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
