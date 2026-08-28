# VERIFY — 30-levels-solver/03-level-generation

Verifier: an independent agent context, 2026-08-28, against `dev` at `c5bf5cc`.
I wrote neither `tools/solver/generate.py`, nor `tools/solver/schema.py`, nor
`tools/tests/test_generate.py`. I did **not** run Unity, `Unity -runTests`, the
iOS simulator or the Android emulator, and I changed no file in the repository.
Every number below came out of a command I ran myself; the batch I inspected is
one I generated today, not one already on disk.

Written because the task carried `verify:passed` with no `VERIFY.md`, which
`tasks/README.md` makes a precondition of that label.

## The claim under test

OUTCOME: *"`python -m tools.solver.generate --count 100 --out <dir>` produces a
batch where every saved level parses and solves; rejects are reported, not
shipped."*

That is a command, so I ran it rather than reading about it.

## The OUTCOME command, run today

```sh
PYTHONPATH=$PWD .venv/bin/python -m tools.solver.generate --count 100 --out <scratch>/gen100
```

```
{"generated": 100, "rejected_unsolvable": 0}
```

```sh
ls <scratch>/gen100 | wc -l
# -> 100
```

100 requested, 100 generated, 100 files on disk, 0 rejected. The counts agree
three ways, so the printed number is not a claim about a directory that holds
something else.

## Every saved level parses **and** solves — re-derived, not assumed

The OUTCOME makes two claims about the saved batch. I checked both against the
100 files I had just written, using the loader the task names
(`schema.load_level`) and the solver, and replaying each solution to confirm the
outcome is actually a win rather than merely a non-`None` answer:

```
files=100 parsed_by_schema.load_level=100 solved=100 replay_WIN=100
```

100/100 on all three. A level that parsed but did not solve, or solved with a
sequence that replayed to a jam, would show as a smaller number in the second or
third column. **PASS.**

## Rejects are reported, not shipped

Read in `tools/solver/generate.py`, `main()` at :152-175. The loop is:

```
generate.py:169         if solve(level) is None:
generate.py:170             rejected += 1
generate.py:171             continue
generate.py:172         save_level(level, str(out_dir / f"pool_{args.seed}_{i:03d}.json"))
generate.py:173         made += 1
generate.py:175     print(json.dumps({"generated": made, "rejected_unsolvable": rejected}))
```

`continue` sits between the rejection and `save_level`, so an unsolvable level
cannot reach the disk; and `rejected` is printed alongside `generated` rather
than swallowed. The structure matches the claim. **PASS.**

Today's run rejected nothing, so the rejection path is *unexercised by my
evidence* — the counter printed 0 and I did not force it to be non-zero. Noted
below under what was not checked.

## Item 1 — `pytest tools/tests/test_generate.py -v` green

Run from a fresh clone into an empty directory, own venv from
`requirements.txt`, under `env -i`:

```sh
git clone --depth 1 --branch dev file://<repo> <scratch>/clean && cd <scratch>/clean
python3.11 -m venv .venv && .venv/bin/pip install -r requirements.txt
env -i HOME="$HOME" PATH="/usr/local/share/dotnet:/usr/bin:/bin:/usr/sbin:/sbin" TMPDIR="$TMPDIR" \
  ./.venv/bin/python -m pytest tools/tests/test_generate.py -q
# -> 9 passed in 0.04s
```

Verbose, naming all nine:

```
tools/tests/test_generate.py::test_pile_size_curve PASSED                [ 11%]
tools/tests/test_generate.py::test_every_generated_level_is_valid[0] PASSED [ 22%]
tools/tests/test_generate.py::test_every_generated_level_is_valid[1] PASSED [ 33%]
tools/tests/test_generate.py::test_every_generated_level_is_valid[2] PASSED [ 44%]
tools/tests/test_generate.py::test_every_generated_level_is_valid[3] PASSED [ 55%]
tools/tests/test_generate.py::test_every_generated_level_is_valid[4] PASSED [ 66%]
tools/tests/test_generate.py::test_batch_of_100_all_parse_all_solvable PASSED [ 77%]
tools/tests/test_generate.py::test_explicit_kind_count_is_respected PASSED [ 88%]
tools/tests/test_generate.py::test_item_counts_are_multiples_of_three PASSED [100%]
```

`test_batch_of_100_all_parse_all_solvable` — the one the item names as the
milestone's key test, the one whose single failure rejects M3 — is present and
green. **PASS.**

## Item 2 — a batch of 100 all parse in the loader

Covered twice over: by `test_batch_of_100_all_parse_all_solvable` in the suite,
and independently by my own run above, which drove `schema.load_level` over 100
freshly written files and got 100 back. **PASS.**

## Item 3 — `test_explicit_kind_count_is_respected`

Present in the collection above and green, so `kind_count` is exercised as a
real lever rather than being derived from the item count. **PASS.**

## How to reproduce

```sh
git clone --depth 1 --branch dev <repo-url> clean && cd clean
python3.11 -m venv .venv && .venv/bin/pip install -r requirements.txt

# the three VERIFY items
env -i HOME="$HOME" PATH="/usr/bin:/bin" TMPDIR="$TMPDIR" \
  ./.venv/bin/python -m pytest tools/tests/test_generate.py -v      # -> 9 passed

# the OUTCOME's own command
PYTHONPATH=$PWD ./.venv/bin/python -m tools.solver.generate --count 100 --out /tmp/gen100
ls /tmp/gen100 | wc -l                                              # -> 100

# and the claim it makes about what it wrote
PYTHONPATH=$PWD ./.venv/bin/python - /tmp/gen100 <<'PY'
import sys, pathlib
from tools.solver.schema import load_level
from tools.solver.solver import solve
from tools.solver.rules import Outcome, replay
files = sorted(pathlib.Path(sys.argv[1]).glob("*.json"))
parsed = solved = won = 0
for f in files:
    lvl = load_level(f); parsed += 1
    sol = solve(lvl)
    if sol is not None:
        solved += 1
        if replay(lvl, list(sol.moves))[0] == Outcome.WIN:
            won += 1
print(f"files={len(files)} parsed={parsed} solved={solved} replay_WIN={won}")
PY
# -> files=100 parsed=100 solved=100 replay_WIN=100
```

Note `PYTHONPATH=$PWD`: `tools` is not an installed package, and `python -m
tools.solver.generate` needs the repository root on the path. Running from the
repository root supplies this in most shells; I set it explicitly so the
reproduction does not depend on that.

## What was not checked

- **The rejection path under load.** `rejected_unsolvable` printed 0 today, so I
  never saw a level rejected. That unsolvable levels are counted and withheld is
  established from the structure of `main()` (:169-173), not from an observation.
  Forcing a rejection would need a generator setting that produces dead ends, and
  I did not construct one.
- **`--seed` and `--items`.** `main()` accepts both (:156-157). I ran with the
  defaults (`seed=1`, `items=36`) only, and the difficulty curve within a batch
  (`number = i % 12 + 1`, :166) was not examined — that belongs to
  `04-difficulty-curve`, which I was asked to skip.
- **The shipped 37 levels.** This task generates a pool; assignment to rooms and
  piles is `05-ship-37-levels`. I did not compare `game/Assets/Resources/Levels`
  to anything.
- **`validate` / acyclicity as a property.** The CONTEXT names
  `schema.py validate` (acyclic `blocked_by`, unique ids, counts divisible by
  three). `test_every_generated_level_is_valid` and
  `test_item_counts_are_multiples_of_three` cover it for five and for generated
  batches; I read their names in the passing collection and did not read their
  bodies.
- **That the generator cannot produce a cycle *by construction*.** The SCOPE
  claims a layered DAG makes cycles impossible. I verified that no cycle appeared
  in 100 levels, which is a sample, not the structural argument.
- **Unity, devices, the loader on the C# side.** None touched. JSON loading in
  the game is `06-json-level-loading`.

## Verdict

**All three VERIFY items pass, and the OUTCOME's own command reproduces exactly
what it promises, on a batch generated today.**

This is the strongest of the six tasks I examined. The OUTCOME names a command;
the command runs; it prints counts that match the directory it wrote; and every
one of the 100 files it wrote parses through the named loader, solves, and
replays to a win. The claim is also pinned inside the repository by
`test_batch_of_100_all_parse_all_solvable`, so it is not resting on this
document — which is the property the 2026-08-28 audit found missing almost
everywhere else.

**`verify: passed` is warranted on the substance.** It was formally unearned
only in that no `VERIFY.md` existed; this file removes that gap. No label change
recommended. I have changed no label.
