# VERIFY — 30-levels-solver/01-rules-in-solver

Verifier: an independent agent context, 2026-08-28, against `dev` at `c5bf5cc`.
I wrote neither `tools/solver/rules.py`, nor `tools/tests/conformance_test.py`,
nor `build/solver-bridge/Program.cs`, nor `game/Assets/Core/Board.cs`. I did
**not** run Unity, did **not** run `Unity -runTests`, did **not** run a
simulator or emulator, and did **not** change any file in the repository. Every
figure below came from a command I ran; nothing is quoted from a note.

Written because the task carried `verify:passed` with no `VERIFY.md`, which
`tasks/README.md` makes a precondition of that label.

## The claim under test

OUTCOME: *"`pytest tools/tests/conformance_test.py` passes from a clean checkout
with no hand-exported environment variables; win, jam and booster cases all
agree."*

The phrase "from a clean checkout" is the whole point — this suite once failed
for exactly that reason — so I did not accept the working tree as evidence.

## Item 1 — green from a clean checkout, no hand-exported variables

Fresh clone into an empty directory, its own venv built only from
`requirements.txt`, and the run under `env -i` so nothing could leak in:

```sh
git clone --depth 1 --branch dev file://<repo> <scratch>/clean && cd <scratch>/clean
git log --oneline -1     # -> c5bf5cc
python3.11 -m venv .venv && .venv/bin/pip install -r requirements.txt
env -i HOME="$HOME" PATH="/usr/local/share/dotnet:/usr/bin:/bin:/usr/sbin:/sbin" TMPDIR="$TMPDIR" \
  ./.venv/bin/python -m pytest tools/tests/conformance_test.py -q
```

```
....                                                                     [100%]
4 passed in 3.89s
```

Four passed, none skipped, in a directory where Unity has never been opened.
Verbose, in the working tree, naming them:

```
tools/tests/conformance_test.py::test_csharp_and_python_agree PASSED     [ 25%]
tools/tests/conformance_test.py::test_booster_recovery_agrees PASSED     [ 50%]
tools/tests/conformance_test.py::test_locked_items_agree PASSED          [ 75%]
tools/tests/conformance_test.py::test_locked_level_solution_agrees PASSED [100%]
```

These are the same four that once errored with `CS0246` when the bridge pointed
at a deleted project (`10-accounts/07-build-wiring-fix/NOTES.md`). They error no
longer. **PASS.**

Worth stating plainly, because it cuts the other way for a neighbouring task:
this file survives the clean checkout **only because it does not import numpy**.
The suite as a whole, `pytest tools/tests`, is interrupted during collection on a
clean checkout today by `tools/tests/test_coat_split.py:22 import numpy as np`
against a `requirements.txt` that does not declare numpy. I confirmed the
independence by uninstalling numpy from the clean clone's venv and rerunning
this file alone: still `4 passed`. So item 1 as written passes; the wider suite
does not. Detail in `tasks/10-accounts/07-build-wiring-fix/VERIFY.md`.

## Item 2 — both WIN and SHELF_JAMMED are compared, not wins only

This is the item the review `reviews/2026-08-24-m2-m3.md` forced. The assertion
is in the file, at the end of `test_csharp_and_python_agree`:

```
tools/tests/conformance_test.py:214      outcomes_seen.add(exp["outcome"])
tools/tests/conformance_test.py:216     # acceptance: BOTH outcomes covered by the comparison
tools/tests/conformance_test.py:217     assert Outcome.WIN.value in outcomes_seen
tools/tests/conformance_test.py:218     assert Outcome.SHELF_JAMMED.value in outcomes_seen
```

`outcomes_seen` is built from the cases actually compared inside the loop, so
this cannot be satisfied by a case that was constructed and then skipped. The
case counts are declared at the top of the file:

```
tools/tests/conformance_test.py:20  N_WIN_CASES = 20
tools/tests/conformance_test.py:21  N_JAM_CASES = 10
```

and pinned at line 121, `assert len(expected) == N_WIN_CASES + N_JAM_CASES` — so
a generator that quietly produced fewer jams would fail the fixture rather than
shrink the comparison. Thirty cases, twenty wins and ten jams.

The comparison is also wider than the outcome, which matters because matching
outcomes are what hid the last divergence. Per case it compares `occupied`
(:177), `triples` (:180), the full per-move `revealed` trace item by item
(:193-199) and the full per-move `shelf` trace slot by slot (:205-212).

**PASS**, and more strongly than the item asks.

## Item 3 — no reliance on `DOTNET_ROLL_FORWARD` exported by the caller

```
build/solver-bridge/solver-bridge.csproj:5     <!-- Only .NET 10 runtime is installed on the dev machine; roll forward
build/solver-bridge/solver-bridge.csproj:6          instead of requiring DOTNET_ROLL_FORWARD by hand (review blocker). -->
build/solver-bridge/solver-bridge.csproj:7     <RollForward>Major</RollForward>
```

Pinned in the project file. Searched the tree for any place that sets the
variable instead:

```sh
grep -rn "DOTNET_ROLL_FORWARD" . --include='*.py' --include='*.csproj' --include='*.sh'
# -> build/core-tests/core-tests.csproj:11   (a comment)
# -> build/solver-bridge/solver-bridge.csproj:6  (a comment)
```

Two hits, both comments explaining why the variable is *not* needed. No code
exports it. And the `env -i` run above is the proof that matters: the variable
cannot have been inherited from a shell that had none.

**PASS.**

## The OUTCOME's third clause — booster

The OUTCOME names booster agreement separately, so I checked it separately.
`test_booster_recovery_agrees` (:221-278) drives Python into a real jam
(`assert st.outcome == Outcome.SHELF_JAMMED`, :242), applies `add_slots(3)`,
plays greedily to the end, and compares capacity (:274), whether C# finished
(:276) and whether the two agree on the win (:277). Its own comment records the
bug this shape was written against: attaching the booster to the jamming move
grew the shelf before the jam, so C# never jammed and recovery went unchecked.
It passes. **PASS.**

## How to reproduce

```sh
git clone --depth 1 --branch dev <repo-url> clean && cd clean
python3.11 -m venv .venv && .venv/bin/pip install -r requirements.txt
env -i HOME="$HOME" PATH="/usr/local/share/dotnet:/usr/bin:/bin:/usr/sbin:/sbin" TMPDIR="$TMPDIR" \
  ./.venv/bin/python -m pytest tools/tests/conformance_test.py -v
# -> 4 passed

sed -n '216,218p' tools/tests/conformance_test.py     # both outcomes asserted
grep -n "RollForward" build/solver-bridge/solver-bridge.csproj
grep -rn "DOTNET_ROLL_FORWARD" . --include='*.py' --include='*.sh'   # only comments
```

`dotnet` must be on `PATH` — the fixture shells out to
`dotnet run --project build/solver-bridge` (:150).

## What was not checked

- **Unity.** The C# side of the comparison is compiled by
  `build/solver-bridge/solver-bridge.csproj` at `net8.0`, pulling
  `game/Assets/Core/**/*.cs` by glob. That the *editor* compiles the same
  sources the same way is not established here, and cannot be without a Unity
  run.
- **That `rules.py` mirrors Core in cases the tests do not reach.** The suite
  compares thirty generated cases plus three hand-built ones, all from fixed
  seeds (`random.Random(31337)`, `777`, `4242`). It is a diff over a sample, not
  a proof of equivalence. The port-plus-diff approach is deliberate
  (`reviews/2026-08-24-refactor-difficulty.md`) and its residual risk is
  unmeasured.
- **`AddSlots` beyond one booster of three slots.** One recovery scenario is
  compared; repeated or larger boosters are not.
- **Determinism across machines.** The seeds fix the levels, but I ran on one
  machine (macOS 25.0 / arm64, CPython 3.11.13). Whether another Python's
  `random` produces the same thirty cases was not tried.
- **The wider suite.** `pytest tools/tests` is red from a clean checkout today
  for a reason outside this task; see item 1.

## Verdict

**All three VERIFY items pass, and the OUTCOME's three clauses — win, jam,
booster — are each pinned by an assertion I read and a run I made.**

This is one of the few places in the tree where a prose claim is backed by a
check that would break if the claim stopped being true: the `outcomes_seen`
assertion cannot pass on a wins-only comparison, and `assert len(expected) == 30`
cannot pass on a shrunken one. That is the shape the 2026-08-28 audit asks for.

**`verify: passed` is warranted on the substance.** It was formally unearned
only in that no `VERIFY.md` existed; this file removes that gap. No label change
recommended. I have changed no label.
