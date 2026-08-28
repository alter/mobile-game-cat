# VERIFY — 10-accounts/07-build-wiring-fix

Verifier: an independent agent context, 2026-08-28, against `dev` at `c5bf5cc`.
I wrote neither `build/core-tests/core-tests.csproj`, nor
`build/solver-bridge/solver-bridge.csproj`, nor any test under
`game/Assets/Tests/Core`, nor `RESOLUTION.md`. I did **not** run Unity, did
**not** run `Unity -runTests` (AGENT-BRIEF.md: it reports success having run
nothing), did **not** open the editor, did **not** build for a device, and did
**not** change any file in the repository. Every number below came out of a
command I ran myself; nothing is quoted from `NOTES.md` or `RESOLUTION.md`
without being re-derived.

This file is written because the task carried `verify:passed` with no
`VERIFY.md`, which `tasks/README.md` makes a precondition of that label.

## The claim under test

OUTCOME: *"Two commands, runnable from a fresh clone with an empty environment,
that run the Core unit tests and the conformance suite. Both green."*

The clean-state half is the whole point of the task, so I did not test it in the
working tree. I cloned the repository into an empty directory and ran everything
there under `env -i`.

## Setting up the clean state

```sh
git clone --depth 1 --branch dev file:///Users/rdolgov/workflow/git/mobile-game-cat <scratch>/clean
cd <scratch>/clean && git log --oneline -1
# -> c5bf5cc The coat harness hung for minutes on iOS, and its fur texture is invisible
```

No editor was launched in that directory; `game/Library` does not exist there.

## Item 1 — Core test command, clean clone, empty environment

```sh
cd <scratch>/clean
env -i HOME="$HOME" PATH="/usr/local/share/dotnet:/opt/homebrew/bin:/usr/bin:/bin:/usr/sbin:/sbin" TMPDIR="$TMPDIR" \
  dotnet test build/core-tests/core-tests.csproj
```

```
Passed!  - Failed:     0, Passed:   195, Skipped:     0, Total:   195, Duration: 311 ms - core-tests.dll (net8.0)
```

`env -i` wipes the environment, so `DOTNET_ROLL_FORWARD` cannot have been
inherited. `RollForward` is pinned in the project file instead —
`build/core-tests/core-tests.csproj:12`: `<RollForward>Major</RollForward>`.

**PASS.**

## Item 2 — the executed count matches the declared count

This is the item that exists because green alone once meant nothing. The
task.txt says "43 as of 25.08.2026"; that number has moved, so I re-derived
both sides today rather than trusting it.

```sh
grep -rho '\[Test\]'     game/Assets/Tests/Core --include='*.cs' | wc -l   # -> 166
grep -rho '\[TestCase'   game/Assets/Tests/Core --include='*.cs' | wc -l   # ->  29
grep -rho '\[TestCaseSource' game/Assets/Tests/Core --include='*.cs' | wc -l  # -> 0
grep -rho '\[Values'     game/Assets/Tests/Core --include='*.cs' | wc -l   # ->   0
grep -rho '\[Theory'     game/Assets/Tests/Core --include='*.cs' | wc -l   # ->   0
grep -rho '\[Repeat'     game/Assets/Tests/Core --include='*.cs' | wc -l   # ->   0
```

166 + 29 = **195 declared**, and no attribute form is present that would make
one declaration expand into several. I also checked that no method carries both
`[Test]` and `[TestCase]`, which would double-count: a small script over the
attribute blocks preceding each `public` member returned
`methods carrying both [Test] and [TestCase]: 0`.

Executed: **195**. Declared: **195**. Equal.

The zero-test failure mode is therefore not present: were discovery to break,
the executed side would drop to 0 and the equality would fail loudly.

**PASS.**

## Item 3 — `pytest tools/tests` from the same clean state — **FAIL**

This is the finding of this document.

```sh
cd <scratch>/clean
python3.11 -m venv .venv
.venv/bin/pip install -r requirements.txt        # the documented clean-state install
env -i HOME="$HOME" PATH="/usr/local/share/dotnet:/usr/bin:/bin:/usr/sbin:/sbin" TMPDIR="$TMPDIR" \
  ./.venv/bin/python -m pytest tools/tests -q
```

```
tools/tests/test_coat_split.py:22: in <module>
    import numpy as np
E   ModuleNotFoundError: No module named 'numpy'
=========================== short test summary info ============================
ERROR tools/tests/test_coat_split.py
!!!!!!!!!!!!!!!!!!!! Interrupted: 1 error during collection !!!!!!!!!!!!!!!!!!!!
1 error in 0.14s
```

Collection is interrupted, so **not one test in `tools/tests` runs** — not the
conformance suite, not the solver suite, none of the 170. The command is red
from a clean checkout today.

Cause, established rather than guessed:

- `tools/tests/test_coat_split.py:22` — `import numpy as np`. It is the only
  file under `tools/tests` that imports numpy (`grep -rn numpy tools/tests/*.py`
  returns that one line).
- `grep -in numpy requirements.txt` → no match, exit 1. numpy is not declared.
- It is installed in the author's working-tree venv:
  `.venv/bin/python -c "import numpy; print(numpy.__version__)"` → `2.4.6`.
  That is why the suite is green on this machine and red anywhere else.
- The file arrived in `1b6c582` "The coat splitter works, and its headline
  number does not survive a second look" — today's coat work, not part of this
  task.

numpy is the only gap. Installing it into the clean clone and rerunning:

```
170 passed in 18.03s
```

and removing it again reproduces the interrupt. So the fix is one line in
`requirements.txt`; the label, meanwhile, asserts something that is false.

This is the same class of defect the task was created to remove, in a new
costume. `NOTES.md` records the original: a suite that "appears green on a
machine where the editor has been opened once, and does not exist anywhere
else". Today it is a suite that appears green on a machine where numpy happens
to be installed. The C# half of the task fixed its own failure durably; the
Python half has no guard, and drifted within three days.

**FAIL** — as documented. Green only with an undeclared package.

## Item 4 — no Unity-generated `.csproj` tracked

```sh
git ls-files '*.csproj'
# -> build/core-tests/core-tests.csproj
# -> build/solver-bridge/solver-bridge.csproj
```

Checked by content, not by name, as the item requires:

```sh
for f in $(git ls-files '*.csproj'); do printf "%s: " "$f"; grep -c "Generated file, do not modify" "$f"; done
# -> build/core-tests/core-tests.csproj: 0
# -> build/solver-bridge/solver-bridge.csproj: 0
```

Two tracked project files, neither generated. **PASS.**

## Item 5 — the written choice, naming the rejected option

`RESOLUTION.md` exists in this directory and says, in its own words:
"**Option B taken.**" with three numbered reasons, and "**Rejected:** Option A —
it invalidates the working asmdefs and moves sources out of the layout every
Unity-facing document assumes." Both options are described before the choice.

**PASS.**

## Cross-check the RESOLUTION.md numbers, since they are load-bearing

`RESOLUTION.md` records "**54 / 54 passed** and **67 passed**". Both have moved:
today the same two commands give **195** and **170**. The document is dated 26
August and the drift is growth, not error — but the numbers in it are stale and
should not be quoted forward.

## How to reproduce

From nothing but a clone URL and a machine with `dotnet` and `python3.11`:

```sh
git clone --depth 1 --branch dev <repo-url> clean && cd clean

# Item 1 + 2 — Core tests, and the count that makes green mean something
env -i HOME="$HOME" PATH="/usr/local/share/dotnet:/usr/bin:/bin:/usr/sbin:/sbin" TMPDIR="$TMPDIR" \
  dotnet test build/core-tests/core-tests.csproj
echo $(( $(grep -rho '\[Test\]' game/Assets/Tests/Core --include='*.cs' | wc -l) \
       + $(grep -rho '\[TestCase' game/Assets/Tests/Core --include='*.cs' | wc -l) ))
# the two numbers must be equal; today both are 195

# Item 3 — reproduces the failure
python3.11 -m venv .venv && .venv/bin/pip install -r requirements.txt
env -i HOME="$HOME" PATH="/usr/bin:/bin" TMPDIR="$TMPDIR" ./.venv/bin/python -m pytest tools/tests -q
# -> Interrupted: 1 error during collection (numpy)

# and confirms numpy is the whole of it
.venv/bin/pip install numpy
env -i HOME="$HOME" PATH="/usr/local/share/dotnet:/usr/bin:/bin" TMPDIR="$TMPDIR" ./.venv/bin/python -m pytest tools/tests -q
# -> 170 passed

# Item 4
git ls-files '*.csproj' | xargs grep -l "Generated file, do not modify"   # must print nothing
```

## What was not checked

- **Unity.** No editor run, no `Unity -runTests`, no player build. Whether the
  same sources still compile *inside* Unity is not established here; the
  asmdef's `"noEngineReferences": true` and `build/check-core-purity.sh` are the
  only standing evidence, and neither is a Unity compile.
- **`build/headless-build.sh` end to end.** Its Unity and signing stages are
  unexercised. I ran the test stages by hand instead.
- **Whether `test_coat_split.py` itself is correct.** I only established that
  its import breaks collection from a clean state. It belongs to another
  worker's in-flight coat task and I did not read it beyond line 22.
- **Windows or Linux.** Everything ran on macOS 25.0 / arm64.
- **Whether `dotnet` 8 specifically is needed.** Only .NET 10 is installed here;
  `RollForward=Major` carried it. A machine with the exact SDK was not tried.

## Verdict

**Three of the five VERIFY items pass; item 3 fails; the OUTCOME is half true.**

The C# half of this task is genuinely and durably fixed: a hand-written project
outside `Assets/`, 195 tests discovered and run from a clone with a wiped
environment, the count pinned against the declaration, no generated project
tracked, and the choice written down with its alternative named. That is the
harder half and it holds.

The Python half does not hold today. `pytest tools/tests` — one of the two
commands the OUTCOME names — runs zero tests from a fresh clone because
`requirements.txt` omits numpy.

**`verify: passed` is not warranted as it stands.** I would set
`verify: failed` until `numpy` is added to `requirements.txt`, at which point
the label is earned on the evidence above and nothing else needs redoing. I have
changed no label.

The durable repair is smaller than the label change: `requirements.txt` is not
compared to anything, so it can omit a dependency indefinitely without a single
test noticing. One check that installs from `requirements.txt` into an empty
environment and imports what the suite imports would have caught this within
minutes of `1b6c582`.
