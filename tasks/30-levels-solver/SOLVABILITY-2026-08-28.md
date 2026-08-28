# Are the 37 shipped levels solvable, and what does the difficulty curve look like?

Date: 2026-08-28. Levels checked: `game/Assets/Resources/Levels/l*.json` (37 files,
`.meta` excluded).

## Verdict

**All 37 shipped levels are solvable, every one is winnable by the ship-time
"sensible forward play" policy (no backtracking), and none of the 37 was
found to contain a single unrecoverable wrong move.** No sampling — every
level was checked individually, and every claim below is the output of a
command shown in "How to reproduce".

The existing solver at `tools/solver/` answers this question; nothing was
missing for the solvability check, so no production code was added. `solve()`
(`tools/solver/solver.py`) is a memoized DFS over the *entire* legal move
tree (it tries every available item at each state, sorted by a heuristic
only for speed, not for correctness/completeness) — so a `None` it returns
un-timed-out is a real proof of unsolvability, not a guess. It was used
as-is. One derived script was written (not committed to `tools/`, kept in
the scratch workspace, see below) to turn the solver's yes/no answer and its
found solution into the tightness numbers the task asked for — it only calls
existing `tools/solver` functions, it adds no rules logic of its own.

## Method

1. **Solvability.** For each level, `tools/solver/solver._Search` was run
   directly (not through the public `solve()` wrapper, which swallows the
   distinction) with a deadline, and `TimeoutError` was caught separately
   from a `None` return:
   - `TimeoutError` → **inconclusive** (search did not finish, verdict
     unknown).
   - `None` without a timeout → **proven unsolvable** (search exhausted
     every reachable state via `self.failed` memoization).
   - a `Solution` → **solved**, replayed through `tools/solver/rules.replay`
     to confirm it actually reaches `Outcome.WIN` (a solver bug could
     otherwise report a false positive).

   Deadline used: 30s per level (`GENEROUS_CAP_S`), with a 120s retry
   reserved for any level that timed out at 30s. **No level came anywhere
   close to either cap** — every one of the 37 solved in under 1
   millisecond (`solve_time_s` in `results.json`, 0.0002s–0.0007s), so the
   30s/120s figures never mattered in practice; they are reported because
   the task asked for the limit regardless of whether it bound anything.
   Note for anyone reusing `solve()` directly: its public signature accepts
   a `state_cap` parameter that is never actually wired into the search —
   only the wall-clock deadline is enforced. Irrelevant here (nothing timed
   out) but worth knowing before trusting it as a hard cap elsewhere.

2. **Sensible-play winnability.** `tools/solver/ship_levels.greedy_wins()` —
   the same forward-only, no-backtracking, "prefer the kind already 2-of-3
   on the shelf" policy the levels were vetted with at ship time — was
   re-run against the levels as they exist on disk today. This is stricter
   than mere `solve()`-solvability: a level can have *some* clever winning
   order and still strand a player who plays forward without undoing moves.
   All 37 pass.

3. **"Does one wrong tap ever doom you?"** — an exact (not sampled) check.
   Walking the solver's own found win path, at every state with more than
   one available item, *every* alternative item was tried and re-checked for
   feasibility by calling the solver's internal `_dfs` from that resulting
   state (full search, not a one-ply lookahead). Cheap here because each
   solve is sub-millisecond. Result: **zero unsafe branches found on any of
   the 37 levels** — at every branch point actually visited on the solved
   path, every legal alternative still had *some* winning continuation from
   there.

   Caveat, stated plainly: this was checked only at states reached along
   *the solver's own path* (its heuristic prefers completing near-triples,
   so it tends to visit conservative states), not exhaustively over the
   whole reachable state space. A tighter trap sitting off that path was not
   searched for. It also answers "does a win still exist from here", not
   "does the specific policy a real player uses still win from here" — that
   second, harsher question is what step 4 measures instead.

4. **The real difficulty curve.** The exact per-level "no wrong taps" result
   above is close to uninformative for ranking levels — it came back
   identical (safe) for all 37. It measures only whether a mistake is
   *fatal*, not how often *ordinary* play makes one. The existing
   `tools/solver/measure.py` already provides that: it plays each shipped
   level 300 times with three simplified/no-lookahead policies (a real
   player has no perfect-play oracle either), breaking ties at random so
   repeats matter, and reports the win rate:
   - `shelf_only` — the original ship-era heuristic ("prefer a kind already
     2-of-3 on the shelf") and nothing else.
   - `partial` — everything a player can actually see (reachable items'
     kinds + the shelf), no pile lookahead.
   - `oracle` — `partial` plus full knowledge of how deep every remaining
     copy is buried (an upper bound, not something a player has).

   This is the fair tightness measure for a *curve*, because it varies
   level to level and band to band, unlike the exact check above.

## The curve

Average win rate by pile-size band (300 games/level, `partial` policy —
what a real player's information actually supports):

| band (items) | rooms | levels | shelf_only avg | partial avg | oracle avg |
|---|---|---|---|---|---|
| 36 | 1–4 | 9 | 96.1% | 98.1% | 97.6% |
| 48 | 5–8 | 12 | 87.0% | 96.4% | 96.3% |
| 60 | 9–12 | 16 | 63.4% | 87.3% | 87.8% |

The curve is real and monotonic: win rate under ordinary (non-lookahead)
play falls from ~98% to ~87% as the pile grows from 36 to 60 items and
locked kinds (task 3.11, rooms 9+) enter. `shelf_only` — the weakest,
most ship-era-authentic policy — falls harder, from 96% to 63%, meaning the
later rooms increasingly punish a player who doesn't at least look at what's
currently reachable, not just what's on the shelf.

Full per-level table (peak-shelf column = highest shelf occupancy reached
while replaying the solver's own found solution, out of capacity 9; not the
worst case, see caveat below):

| level | room | items | peak shelf (solver path) | shelf_only % (300 games) | partial % (300 games) | oracle % (300 games) |
|---|---|---|---|---|---|---|
| 1 | 1 | 36 | 4/9 | 100.0 | 100.0 | 100.0 |
| 2 | 2 | 36 | 4/9 | 100.0 | 100.0 | 100.0 |
| 3 | 2 | 36 | 5/9 | 74.7 | 83.3 | 78.7 |
| 4 | 3 | 36 | 5/9 | 99.3 | 100.0 | 100.0 |
| 5 | 3 | 36 | 2/9 | 97.0 | 100.0 | 100.0 |
| 6 | 3 | 36 | 2/9 | 98.3 | 99.7 | 100.0 |
| 7 | 4 | 36 | 2/9 | 96.0 | 99.7 | 100.0 |
| 8 | 4 | 36 | 4/9 | 100.0 | 100.0 | 100.0 |
| 9 | 4 | 36 | 2/9 | 99.7 | 100.0 | 100.0 |
| 10 | 5 | 48 | 4/9 | 99.7 | 100.0 | 100.0 |
| 11 | 5 | 48 | 6/9 | 83.7 | 92.0 | 91.3 |
| 12 | 5 | 48 | 3/9 | 67.3 | 88.0 | 83.7 |
| 13 | 6 | 48 | 4/9 | 86.0 | 91.7 | 92.0 |
| 14 | 6 | 48 | 4/9 | 94.0 | 99.3 | 100.0 |
| 15 | 6 | 48 | 2/9 | 80.3 | 97.0 | 96.7 |
| 16 | 7 | 48 | 2/9 | 91.0 | 99.0 | 95.7 |
| 17 | 7 | 48 | 4/9 | 90.3 | 96.7 | 98.3 |
| 18 | 7 | 48 | 2/9 | 91.7 | 95.3 | 99.7 |
| 19 | 8 | 48 | 4/9 | 71.3 | 97.7 | 100.0 |
| 20 | 8 | 48 | 3/9 | 91.0 | 100.0 | 98.7 |
| 21 | 8 | 48 | 2/9 | 98.3 | 100.0 | 100.0 |
| 22 | 9 | 60 | 4/9 | 77.0 | 99.0 | 100.0 |
| 23 | 9 | 60 | 4/9 | 58.7 | 93.0 | 95.3 |
| 24 | 9 | 60 | 6/9 | 68.3 | 85.0 | 93.7 |
| 25 | 9 | 60 | 4/9 | 66.7 | 86.0 | 96.3 |
| 26 | 10 | 60 | 4/9 | 76.0 | 99.3 | 99.3 |
| 27 | 10 | 60 | 4/9 | 59.7 | 91.3 | 88.0 |
| 28 | 10 | 60 | 4/9 | 67.3 | 86.7 | 72.7 |
| 29 | 10 | 60 | 4/9 | 73.7 | 98.7 | 99.3 |
| 30 | 11 | 60 | 4/9 | 65.0 | 89.3 | 75.7 |
| 31 | 11 | 60 | 4/9 | 72.3 | 85.3 | 99.0 |
| 32 | 11 | 60 | 4/9 | 32.3 | 66.0 | 39.7 |
| 33 | 11 | 60 | 6/9 | 71.0 | 96.7 | 96.0 |
| 34 | 12 | 60 | 5/9 | 40.7 | 63.7 | 81.0 |
| 35 | 12 | 60 | 6/9 | 55.7 | 78.7 | 88.0 |
| 36 | 12 | 60 | 6/9 | 71.0 | 86.7 | 90.0 |
| 37 | 12 | 60 | 2/9 | 58.3 | 91.0 | 90.3 |

Every row: `solve()` = solved, `replay()` = win, `greedy_wins()` = true,
zero unsafe branches on the solver's path. All 37 rows carry the same
solvability verdict; what varies is the last three columns.

Outliers worth a human look, not because they're broken (they're all
solvable and greedy-winnable) but because they buck their own room's
pattern: **l32** (room 11) is a low point across all three policies
(66.0/32.3/39.7%) — markedly harder than its room-mates l30/l31/l33
(85–99% partial). **l34** (room 12, 63.7% partial) and **l35** (room 12,
78.7% partial) are similarly rough compared to l36/l37 in the same room
(86.7/91.0%). Also notable: on l32, l28 and l30, the `oracle` policy (which
sees strictly more information than `partial`) scores *lower* than
`partial` — plausible (both are heuristics, not exhaustive search, and
`oracle`'s extra tie-break criterion can steer it away from what `partial`
happens to pick), but not independently explained here; flagged rather than
smoothed over.

## What could not be settled

- The "zero unsafe branches" result (§3) is proven only along the solver's
  own found path, not exhaustively over the whole reachable state space of
  each level — a trap reachable only by a path the solver never walks was
  not searched for and cannot be ruled out from this data.
- The `oracle` scoring below `partial` on l28/l30/l32 is measured and
  reproducible (same seed, both policies replay identical tie-break draws)
  but not diagnosed beyond "both are heuristics, this can happen" — root
  cause not investigated.
- All three measure.py policies are synthetic proxies for a human, not a
  playtest; the win-rate numbers say how a specific decision rule fares, not
  how a person would.
- Nothing hit either time cap (30s/120s), so the report has no case of a
  level whose solvability is genuinely unresolved — this line is here only
  because the task asked to say so either way.

## How to reproduce

From the repo root, with the project venv:

```
# per-level solvability, replay check, greedy-wins check, peak-shelf,
# zero-unsafe-branch check (writes results.json next to the script)
PYTHONPATH="$(pwd)" .venv/bin/python tasks/30-levels-solver/analyze_solvability.py

# the win-rate curve (shelf_only / partial / oracle), 300 games/level
PYTHONPATH="$(pwd)" .venv/bin/python -m tools.solver.measure --shipped --repeats 300 --json
```

`analyze_solvability.py` is a thin script (not a `tools/solver` module: it
adds no rules or solving logic, it only drives `tools.solver.solver`,
`tools.solver.rules`, and `tools.solver.ship_levels.greedy_wins` and reports
what they say) — copied alongside this report at
`tasks/30-levels-solver/analyze_solvability.py` for reproducibility.
