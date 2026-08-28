# Why are l32, l34, l35 harder than their room-mates, and what to do about it

Date: 2026-08-28. Builds on `tasks/30-levels-solver/SOLVABILITY-2026-08-28.md`
and reuses its script (`analyze_solvability.py`, specifically
`solve_from_state`) rather than re-implementing feasibility checks. All new
scripts live in `tasks/30-levels-solver/hard_levels_*.py`; every number below
names the exact command that produced it and its sample size.

**Update (same day, second pass): the fixes below are now applied to the
shipped files.** See §5 for what was applied, why, and the full 37-level
before/after. Originals are preserved at
`tasks/30-levels-solver/level-originals/`.

## TL;DR

l32/l34/l35 are not booby-trapped: **every legal opening move on all eight
room-11/12 levels still leads to a win** (exhaustive check), and every
sampled loss, on every level in the band including the well-behaved ones,
first goes wrong in the last ~15% of moves with an avoidable safe
alternative sitting right next to the bad one. What actually separates the
hard three is **frequency**: l32 and l34 hit roughly 4-16x more of these
late risky forks per game than their siblings, and one concrete structural
cause was found and fixed for two of the three (see §3). The `oracle`<
`partial` anomaly on l30/l32 is real and reproducible across 20 seeds (not
noise); on l28 it is noise — it does not reproduce.

## 1. What makes them hard? (structural comparison)

`PYTHONPATH=$(pwd) .venv/bin/python tasks/30-levels-solver/hard_levels_structural.py`
— all 37 levels, output below filtered to room 11/12.

Kind count, kind-count evenness, and locked-item count/threshold are
**identical across every 60-item level, hard or not**: the generator
(`generate.py`) always makes exactly `round(60/3/2) = 10` kinds of 6 items
each (stdev 0.0) for a 60-item pile, and always locks exactly one kind (6
items) at `locked_after_triples = 2`. These are not the source of the
variance — they cannot be, they don't vary. What does vary:

| level | room | blocked_by depth | blocked_by max width | opening items | opening kinds | partial win % (300 games)¹ |
|---|---|---|---|---|---|---|
| l30 | 11 | 4 | 34 | 28 | 9 | 89.3 |
| l31 | 11 | 3 | 32 | 30 | 9 | 85.3 |
| **l32** | 11 | 3 | 29 | 26 | 9 | **66.0** |
| l33 | 11 | 2 | 34 | 29 | 8 | 96.7 |
| **l34** | 12 | 3 | 31 | 28 | 9 | **63.7** |
| **l35** | 12 | 4 | 38 | 33 | 9 | **78.7** |
| l36 | 12 | 3 | 31 | 30 | 9 | 86.7 |
| l37 | 12 | 3 | 35 | 33 | 9 | 91.0 |

¹ from the original report, reproduced verbatim below.

Depth/width/opening-set size do not separate the hard three from their
siblings either — l35 has the *widest* graph (38) and *most* opening items
(33) of its whole room, yet l37 (35 wide, 33 opening) is fine. l32 has the
*narrowest* opening set (26) but l30 (28) is fine too. **None of the
aggregate structural stats predict which level is hard** — the cause has to
be found by simulation, not by summary statistics (§3).

## 2. Is the difficulty the good kind?

**A. Opening moves — exhaustive, not sampled.**
`hard_levels_opening_moves.py` takes every legal first move on all 8 levels
and re-solves from the resulting state.

| level | legal opening moves | still lead to a win |
|---|---|---|
| l30 | 28 | 28 (100%) |
| l31 | 30 | 30 (100%) |
| l32 | 26 | 26 (100%) |
| l33 | 29 | 29 (100%) |
| l34 | 28 | 28 (100%) |
| l35 | 33 | 33 (100%) |
| l36 | 30 | 30 (100%) |
| l37 | 33 | 33 (100%) |

Zero unrecoverable first moves on any of the 8. This matches
`SOLVABILITY-2026-08-28.md`'s "zero unsafe branches" result, extended here
from the solver's own conservative path to the true first branch.

**B. Where do real losses actually go wrong?**
`hard_levels_mistakes.py` plays 400 `partial`-policy games per level (seeds
`20260826+trial`, trial 0-399), and for the first 8 losses per level finds
the *exact* move where the taken choice (not just some alternative) first
turns a still-winnable state into an unwinnable one, re-solving with
`analyze_solvability.solve_from_state`.

| level | losses / 400 | mean mistake move | mean moves left after it | mean choices open | all 8 sampled mistakes avoidable? |
|---|---|---|---|---|---|
| l30 | 63 | 46.6 | 1.0 | 5.8 | yes |
| l31 | 51 | 44.8 | 1.0 | 6.8 | yes |
| l32 | 134 | 43.6 | 1.0 | 6.6 | yes |
| l33 | 16 | 43.6 | 1.0 | 7.9 | yes |
| l34 | 123 | 45.4 | 1.1 | 6.8 | yes |
| l35 | 73 | 45.4 | 1.1 | 6.4 | yes |
| l36 | 45 | 48.5 | 1.0 | 5.6 | yes |
| l37 | 51 | 46.6 | 1.0 | 6.2 | yes |

Every sampled mistake, on all 8 levels (64 mistakes inspected total), is
late (mean ~45th of ~48 moves — the last 1-4 moves of that particular game)
and *avoidable* (a safe alternative existed at that exact fork). No early
irrecoverable trap exists anywhere in this data, on the hard levels or the
easy ones — **the character of the risk is identical across the whole
room-11/12 band.**

**C. So what actually differs? Frequency of risky forks.**
`hard_levels_risky_density.py`: 15 `partial`-policy seeds/level (2000+2000+…
= 120 games total), counting how many per-move forks contain at least one
unsafe alternative (`0 < safe < avail`, re-solved exactly):

| level | avg risky forks / game | risky-fork density |
|---|---|---|
| l30 | 0.00 | 0.0% |
| l31 | 0.27 | 0.5% |
| **l32** | **1.27** | **2.3%** |
| l33 | 0.53 | 0.9% |
| **l34** | **2.13** | **3.8%** |
| **l35** | **1.07** | **2.0%** |
| l36 | 0.13 | 0.2% |
| l37 | 0.60 | 1.0% |

This is the real signal, and it lines up cleanly with the win-rate ranking:
l32 and l34 sit at 4-16x their well-behaved room-mates' risky-fork rate;
l35 is elevated ~2-8x. **Verdict: the difficulty is the good kind in
character** (no move-one trap, every mistake avoidable, identical failure
mechanics to the easy levels) **but the bad kind in dosage** — a real
player using no more lookahead than `partial` sees several more
indistinguishable-looking late "which one do I grab" gambles per game on
l32/l34/l35 than on their siblings, and can't reliably tell the safe pick
from the fatal one without deeper lookahead than the visible information
supports.

## 3. What would fix it — proposed edits, measured before/after

`hard_levels_fork_inspect.py` found, per hard level, which *kind* sits on
the unsafe side of risky forks most often (25 seeds, tallying which items
get flagged unsafe): **l32 → `prop_ball`** (57 of the top-10 unsafe-item
hits), **l34 → `prop_pillow`** (47), **l35 → `prop_ball`** (20). In each
case the guilty kind has most of its 6 copies gated behind *different*
individual blockers, so they surface in scattered dribbles instead of
clumps — e.g. l32's `prop_ball`: only item 59 is open at the start, the
other five each wait on a different single item (2, 10×2, 11, 14, 15&13).

`hard_levels_edits.py` measures each edit with the **exact original
methodology** (`tools.solver.measure.play`, 300 games/policy, seed
`20260826`) — in memory only, nothing written to the shipped JSON.
Baselines reproduce the original report exactly (cross-check).

| level | edit | shelf_only | partial | oracle |
|---|---|---|---|---|
| l32 | baseline | 32.3 | 66.0 | 39.7 |
| l32 | drop `blocked_by` 14 from item 58 (`prop_ball`) | 31.0 | 66.7 (+0.7) | 42.3 (+2.6) |
| l32 | `prop_clock` locked_after_triples 2→1 | 30.7 | 66.0 (+0.0) | 39.7 (+0.0) |
| l32 | drop `blocked_by` 11 from item 22 (`prop_ball`) | 33.7 | 64.3 (**−1.7**) | 38.3 (−1.4) |
| l32 | **swap kind: item 58 (`prop_ball`) ↔ item 1 (`prop_lamp`)** | 30.3 | **70.0 (+4.0)** | **64.0 (+24.3)** |
| l32 | stack: also swap item 24 ↔ item 2 (`prop_suitcase`), on top of the above | 34.0 | 62.7 (**−7.3 vs single swap**) | 51.3 |
| l34 | baseline | 40.7 | 63.7 | 81.0 |
| l34 | **drop `blocked_by` 9 from item 14 (`prop_pillow`)** | **52.7 (+12.0)** | **84.3 (+20.6)** | **98.7 (+17.7)** |
| l34 | `prop_crate` locked_after_triples 2→1 | 51.0 (+10.3) | 63.7 (+0.0) | 81.0 (+0.0) |
| l35 | baseline | 55.7 | 78.7 | 88.0 |
| l35 | drop `blocked_by` 52 from item 60 (`prop_ball`) | 56.0 | 81.7 (+3.0) | 89.7 (+1.7) |
| l35 | `prop_lamp` locked_after_triples 2→1 | 59.0 (+3.3) | 83.3 (+4.6) | 88.0 (+0.0) |

**Findings, plainly:**
- **l34's fix is real and large**: dropping one `blocked_by` edge (item 14,
  `prop_pillow`, no longer waits on item 9) takes `partial` from 63.7% to
  84.3% — above two of its own siblings. One edge, +20.6pp.
- **l32's best fix is a kind swap**, not an edge drop: swapping item 58's
  kind with an already-open item's kind (so `prop_ball` has 2 copies open
  from move 1 instead of 1) took `partial` 66.0%→70.0% and — notably —
  nearly closed the `oracle` gap (39.7%→64.0%, see §4). Plain edge-drops on
  the same kind (items 58, 22) did **not** help and one made it slightly
  *worse* (64.3%, a measured non-improvement, reported as such).
- **Edits do not stack linearly**: applying the same kind-swap idea a
  second time on l32 *regressed* `partial` from 70.0% to 62.7% — moving a
  second late-blocked item onto an already-open donor apparently hurt the
  donor kind (`prop_suitcase`) more than it helped `prop_ball` further. One
  swap helps; a second, naive repeat of the same idea does not.
- **`locked_after_triples` threshold moves are the weakest lever tested**:
  2→1 never moved `partial` on l32 or l34 at all (only `shelf_only` moved),
  and only moved it modestly on l35 (+4.6pp). The locked kind is not the
  main driver of any of the three levels' difficulty.
- **Recommendation**: ship l34's edge-drop (item 14) — largest, cleanest,
  no downside found. For l32, the kind-swap (item 58 ↔ item 1) is a real
  but smaller win; further tuning would need more than one iteration. l35's
  elevation is mild (78.7%, within shouting distance of l37's 91.0%) and
  both tested edits give only +3-5pp — a nice-to-have, not urgent.

## 4. The oracle < partial anomaly on l28, l30, l32

`hard_levels_oracle_anomaly.py`, part A: replayed `partial` vs `oracle` over
20 different seeds × 100 repeats each (2000 games/policy/level, not the
single default seed the original report used).

| level | mean (oracle − partial) wins/100 | seeds oracle worse / better / tied (of 20) |
|---|---|---|
| l28 | **0.0** | 5 / 5 / 10 |
| l30 | **−30.0** | 7 / 1 / 12 |
| l32 | **−30.0** | 7 / 1 / 12 |
| l33 (control) | 0.0 | 0 / 0 / 20 |

**l28's anomaly does not reproduce — it was seed noise.** l30 and l32's
anomaly is real: consistently negative mean across 20 independent seeds,
worse far more often than better (never a coin flip).

Part B explains the mechanism on l32: at the 257 states across 30 seeds
where `oracle`'s dig-cost tie-break actually narrowed `partial`'s tied
choice set, it excluded a safe id 253 times, and in **5 cases the entire
narrowed set was unsafe** while `partial`'s wider set still contained a
winning move (e.g. seed 2028: `partial` could pick from `{8,49,51,31}`,
where 8 is safe; `oracle`'s dig-cost narrowed to `{49,31}`, neither safe).
On l30, none of 239 divergent forks had oracle's narrowed set entirely
unsafe — oracle's own pick is always locally feasible there, yet the
realized win rate is still measurably lower across seeds.

**What's confirmed:** oracle's 4th tie-break criterion (prefer the kind
with the lowest total remaining dig-cost) sometimes actively discards the
one move that keeps l32 winnable — a real, if infrequent (~2% of divergent
forks), defect in that heuristic, not noise. **What's not confirmed:** why
l30's realized win rate suffers when oracle's own picks at the checked
forks are never provably unsafe. The plausible mechanism — a locally-safe
but narrower pick removes some of the "luck surface" that `partial`'s wider
random tie-break gets to sample, and the loss shows up several moves later
under oracle's own continued (imperfect) heuristic rather than at the fork
itself — was not traced move-by-move and is reported as unverified, not as
established.

## Reproduce

```
PYTHONPATH=$(pwd) .venv/bin/python tasks/30-levels-solver/hard_levels_structural.py
PYTHONPATH=$(pwd) .venv/bin/python tasks/30-levels-solver/hard_levels_opening_moves.py
PYTHONPATH=$(pwd) .venv/bin/python tasks/30-levels-solver/hard_levels_mistakes.py
PYTHONPATH=$(pwd) .venv/bin/python tasks/30-levels-solver/hard_levels_risky_density.py
PYTHONPATH=$(pwd) .venv/bin/python tasks/30-levels-solver/hard_levels_fork_inspect.py
PYTHONPATH=$(pwd) .venv/bin/python tasks/30-levels-solver/hard_levels_edits.py
PYTHONPATH=$(pwd) .venv/bin/python tasks/30-levels-solver/hard_levels_oracle_anomaly.py
```

Each writes its own JSON next to itself for inspection. All scripts import
`tools.solver.*` and `analyze_solvability.solve_from_state` only — no new
rules logic was written; every feasibility check is the existing DFS
solver.

## 5. Applied to the shipped files, and proof it cost nothing elsewhere

`tasks/30-levels-solver/apply_hard_level_fixes.py` made these three edits to
`game/Assets/Resources/Levels/`. Originals kept at
`tasks/30-levels-solver/level-originals/*.json` (diffed against the shipped
files below — each diff is exactly the one intended change, nothing else).

1. **l34 — applied.** Item 14 (`prop_pillow`)'s `blocked_by` edge on item 9
   dropped: `[9]` → `[]`. Largest, cleanest win found, no downside on any
   policy.
2. **l32 — applied, single swap only.** Item 58 (`prop_ball`) and item 1
   (`prop_lamp`) swapped `kind` labels, so `prop_ball` has 2 copies open
   from move 1 instead of 1. Chosen over "leave it" because the single swap
   improved all three tracked numbers with no downside found; chosen over
   the double-swap variant because stacking a second swap *regressed*
   `partial` from 70.0% to 62.7% in testing (§3) — that variant was
   discarded, only the single swap shipped.
3. **l35 — applied.** `prop_lamp` (the room's locked kind)
   `locked_after_triples`: 2 → 1, on all 6 copies. Chosen over the
   `blocked_by`-drop alternative tested in §3 (+3.0pp partial) because the
   threshold move scored higher (+4.6pp partial) with no downside on any
   policy either.

**Full 37-level curve, before vs. after, same methodology as the original
report** (`PYTHONPATH=$(pwd) .venv/bin/python -m tools.solver.measure
--shipped --repeats 300 --json`, seed `20260826`, 300 games/policy/level,
run once before the edits and once after):

| level | shelf_only before→after | partial before→after | oracle before→after |
|---|---|---|---|
| l32 | 32.3 → 30.3 | **66.0 → 70.0** | **39.7 → 64.0** |
| l34 | 40.7 → 52.7 | **63.7 → 84.3** | 81.0 → 98.7 |
| l35 | 55.7 → 59.0 | **78.7 → 83.3** | 88.0 → 88.0 |
| all other 34 levels | unchanged | unchanged | unchanged |

Confirmed by diffing the full 37-row JSON output (`/tmp/curve_before.json`
vs `/tmp/curve_after.json`): **all 34 untouched levels are numerically
identical, to the decimal, before and after** — editing one level's file
cannot and did not affect another's, but this proves it rather than assumes
it, per the ask.

**Solvability re-run, not assumed:** `analyze_solvability.py` re-run on all
37 files after the edits. Result: all 37 still `solved`, all 37 replay to
`Outcome.WIN`, all 37 still pass `greedy_wins()`, and the exact
"zero-unsafe-branch-on-the-solved-path" check (`min_safe_ratio`) is still
`1.0` on every one of the 37, including the three edited levels
(`l32`/`l34`/`l35` peak shelf 4/5/6 out of 9 respectively). Before the
edits: same verdict, all 37 (this was the "before" run, done first, so the
comparison is real, not inferred from yesterday's report).

**The game's own reader, both sides:**
- `dotnet test build/core-tests/core-tests.csproj` — **195/195 passed**,
  both before the edits (clean baseline) and after. This exercises
  `LevelLoader.LoadAllFromAssets()`, which reads every `l*.json` straight
  off disk from `game/Assets/Resources/Levels` (the real path, not a copy)
  through the same C# `Level` constructor that runs at ship time —
  `HeadlessRunTests` and `RoomPlanTests` actually load and play all 37
  through Core's own `Board`. The three edited files parse, validate
  (kind-count-%-3, no dangling/self/cyclic `blocked_by`), and play to a win
  exactly like before.
- `pytest tools/tests/` — **170/170 passed**, before and after (includes
  `test_schema.py`, `test_pacing.py`, `test_ship_levels.py`, and
  `conformance_test.py`, which cross-checks Python vs. the C# solver-bridge
  on generated cases — unaffected by this edit but run in full as asked).

No edit broke a schema check, a neighbour's win rate, solvability, or the
C# reader.

## What was not settled

- The l30 leg of the oracle anomaly: confirmed real and reproducible, not
  fully explained (see §4). Not affected by the l32 edit — l30's own file
  was not touched.
- Only l32/l34/l35's dominant unsafe kind was targeted for edits; other
  kinds contributing a smaller share of risky forks were not tried. l32's
  70.0% partial win rate is now closer to its room-mates but still the
  lowest in room 11 (l30 89.3%, l31 85.3%, l33 96.7%) — a further pass could
  chase it lower, not attempted here.
- l32's `oracle` policy (64.0% after) is still below its own `partial`
  (70.0%) — the fix narrowed but did not close the anomaly investigated in
  §4.
