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

## The prototype is currently stale

`build/playtest/index.html` was generated before D1 (move counter deleted) and
before D3 (hidden kinds). It still embeds a `movesLimit` field and renders
blocked items at 35% opacity rather than blank - the exact convention D3
replaces. Running the gate on this build risks measuring a game that no longer
matches the shipped rules. Regenerate it from `make_playtest.py` against the
current rules before seating anyone.

Source: cat-shelter-tasks.md lines 588-605 (3.7 rationale); DECISIONS.md D3;
build/playtest/index.html, verify_playtest.py (stale movesLimit references).
