# VERIFY — 30-levels-solver/02-solver

Verifier: an independent agent context, 2026-08-28, against `dev` at `c5bf5cc`.
I wrote neither `tools/solver/solver.py` nor `tools/tests/test_solver.py` nor
`tools/solver/generate.py`. I did **not** run Unity, `Unity -runTests`, the iOS
simulator or the Android emulator, and I changed no file in the repository. Every
number below came out of a command I ran myself.

Written because the task carried `verify:passed` with no `VERIFY.md`, which
`tasks/README.md` makes a precondition of that label.

## The claim under test

OUTCOME: *"`solve()` answers under 2 seconds per level on known-solvable and
known-dead-end fixtures."*

## Item 1 — `pytest tools/tests/test_solver.py -v` green

Run in a fresh clone into an empty directory, own venv from `requirements.txt`,
under `env -i`:

```sh
git clone --depth 1 --branch dev file://<repo> <scratch>/clean && cd <scratch>/clean
python3.11 -m venv .venv && .venv/bin/pip install -r requirements.txt
env -i HOME="$HOME" PATH="/usr/local/share/dotnet:/usr/bin:/bin:/usr/sbin:/sbin" TMPDIR="$TMPDIR" \
  ./.venv/bin/python -m pytest tools/tests/test_solver.py -q
# -> 7 passed in 0.01s
```

Verbose, naming all seven:

```
tools/tests/test_solver.py::test_five_known_solvable PASSED              [ 14%]
tools/tests/test_solver.py::test_solution_is_legal_and_winning_on_generated_levels PASSED [ 28%]
tools/tests/test_solver.py::test_locked_items_in_search PASSED           [ 42%]
tools/tests/test_solver.py::test_circular_block_unsolvable PASSED        [ 57%]
tools/tests/test_solver.py::test_shelf_jam_dead_end PASSED               [ 71%]
tools/tests/test_solver.py::test_solver_speed_under_two_seconds_realistic PASSED [ 85%]
tools/tests/test_solver.py::test_minimal_move_count_matches_replay PASSED [100%]
```

Green, none skipped. **PASS.**

## Item 2 — the two named fixtures — **half of it does not exist**

The item reads: *"Five known-solvable and five known-dead-end fixtures both
resolve correctly (test_five_known_solvable, test_five_known_dead_ends)."*

**`test_five_known_solvable` exists and is honest.** `test_solver.py:24-38`,
five cases in the list at :25-33 — a plain triple, a stack of one kind, an
interleaved pair set, a four-kinds-wide level, and a generated 24-item level —
each solved and each replayed to `Outcome.WIN` (:37-38). Five, resolved
correctly. **PASS.**

**`test_five_known_dead_ends` does not exist.** Searched the whole repository:

```sh
grep -rn "five_known_dead_ends" . --include='*.py' --include='*.md' --include='*.txt'
# -> tasks/30-levels-solver/02-solver/task.txt:29:     correctly (test_five_known_solvable, test_five_known_dead_ends).
```

The only occurrence in the tree is the line of `task.txt` that names it. No test
by that name was ever written, and pytest's collection above lists seven tests,
none of them it.

What exists instead is **three** dead-end fixtures, spread across three tests,
and only two of them go through `solve()`:

| fixture | where | reaches `solve()`? |
|---|---|---|
| circular `blocked_by` (1↔2) | `test_circular_block_unsolvable`, :64-67 | yes — `assert solve(cycled, state_cap=50000) is None` |
| locked kind needing 2 triples when only 1 exists | `test_locked_items_in_search`, :59-61 | yes — `assert solve(hard) is None` |
| shelf jammed by a kind-spread take order | `test_shelf_jam_dead_end`, :70-82 | **no** — it calls `new_state` and `st.take` directly and asserts `st.outcome == Outcome.SHELF_JAMMED`. `solve()` is never invoked. |

So against "five known-dead-end fixtures" the tree holds three, of which two
exercise the solver. **FAIL** on the count, on the name, and partly on the
mechanism.

This is not a claim that rotted — `git log` shows no test of that name was ever
removed. It was written into `task.txt` and never built, and with no `VERIFY.md`
in the directory nothing in three days compared the item to the file.

## Item 3 — timing on a 60-item level — **the test uses 45, so I ran 60 myself**

The repository's timing test is `test_solver_speed_under_two_seconds_realistic`
(:85-91), and it generates `item_count=45`:

```
tools/tests/test_solver.py:86     level = generate_level(random.Random(7), item_count=45)
tools/tests/test_solver.py:91     assert dt < 2.0, f"solver took {dt:.2f}s"
```

The item says 60. 60 is the real number — `tools/solver/generate.py`
`items_for_room` returns 60 for rooms 9-12, and those levels ship. So the
committed test is a bar below the shipped worst case, and the item's claim is
untested by the suite.

Re-derived today, ten 60-item levels, seeds 1-10, each solved and each solution
replayed to a win:

```
seed  1: 0.0006s  solved=True win=True moves=60
seed  2: 0.0005s  solved=True win=True moves=60
seed  3: 0.0005s  solved=True win=True moves=60
seed  4: 0.0005s  solved=True win=True moves=60
seed  5: 0.0006s  solved=True win=True moves=60
seed  6: 0.0005s  solved=True win=True moves=60
seed  7: 0.0005s  solved=True win=True moves=60
seed  8: 0.0005s  solved=True win=True moves=60
seed  9: 0.0005s  solved=True win=True moves=60
seed 10: 0.0005s  solved=True win=True moves=60
worst 60-item: 0.0006s  under2s=True
```

I also confirmed the pile really holds 60 items rather than 60 being a request
the generator rounds: `len(l.pile)` → `60`.

And the dead-end side of the OUTCOME, timed through `solve()`:

```
circular_block:          0.0001s  solve()=None  under2s=True
locked_never_unlockable: 0.0000s  solve()=None  under2s=True
```

**PASS on the substance** — 0.0006 s against a 2 s bar is four orders of
magnitude of headroom — but the pass is mine, not the suite's. Nothing in the
repository would notice if the 60-item case regressed.

## How to reproduce

```sh
git clone --depth 1 --branch dev <repo-url> clean && cd clean
python3.11 -m venv .venv && .venv/bin/pip install -r requirements.txt
env -i HOME="$HOME" PATH="/usr/bin:/bin" TMPDIR="$TMPDIR" \
  ./.venv/bin/python -m pytest tools/tests/test_solver.py -v      # -> 7 passed

# item 2 — the missing test
grep -rn "five_known_dead_ends" . --include='*.py'                # -> no match

# item 3 — the 60-item timing the suite does not do
PYTHONPATH=$PWD ./.venv/bin/python - <<'PY'
import random, time
from tools.solver.generate import generate_level
from tools.solver.rules import Outcome, replay
from tools.solver.solver import solve
worst = 0.0
for seed in range(1, 11):
    lvl = generate_level(random.Random(seed), item_count=60)
    t0 = time.monotonic(); sol = solve(lvl); dt = time.monotonic() - t0
    worst = max(worst, dt)
    assert sol and replay(lvl, list(sol.moves))[0] == Outcome.WIN
    print(f"seed {seed:2d}: {dt:.4f}s  items={len(lvl.pile)}")
print(f"worst: {worst:.4f}s  under2s={worst < 2.0}")
PY
```

## What was not checked

- **Whether `solve()` is *correct* on levels it calls unsolvable.** Returning
  `None` is checked against three hand-built dead ends. That the search is
  complete — that no `None` is a false negative caused by `state_cap` or by the
  sensible-play heuristic pruning the only path — is not established here and
  would need a brute-force cross-check I did not write.
- **Larger or pathological levels.** I timed 60 items, the shipped maximum. 100+
  items, adversarially constructed blockers, and deep lock chains were not tried.
- **`state_cap` behaviour.** `test_circular_block_unsolvable` passes
  `state_cap=50000` explicitly; the default and what happens when the cap is hit
  were not examined.
- **Memoization.** The SCOPE names failure-state memoization as a deliverable. I
  measured that the solver is fast; I did not verify that memoization is what
  makes it fast, nor that it is present and correct.
- **Unity, devices, the shipped build.** None touched.
- **Machine variation.** All timings on macOS 25.0 / arm64, CPython 3.11.13.

## Verdict

**Item 1 passes. Item 3 passes, but only because I ran the missing measurement
by hand. Item 2 fails: `test_five_known_dead_ends` has never existed, and the
tree holds three dead-end fixtures rather than five.**

The underlying solver is in good shape — 7/7 green from a clean clone, correct
on every fixture present, and 0.0006 s on the shipped worst case against a 2 s
budget. Nothing here suggests the code is wrong. What is wrong is the paperwork
around it: an acceptance item names a test that was never written, and the
timing item names a size the suite never runs.

**`verify: passed` is not warranted as it stands.** I would set
`verify: failed` until either two more dead-end fixtures are added under the
name the item uses, or the item is rewritten to describe the three that exist —
and until the timing test moves from 45 items to 60, so the number in the
OUTCOME is pinned by something other than this document. Both are small pieces
of work; neither is a rewrite. I have changed no label.
