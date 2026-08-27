# VERIFY — 20-rules-core/05-coverage

Result: **passed** — both VERIFY items.

Verifier: an independent agent context, 2026-08-27, against `dev` at commit
`e0e949f`. It did **not** write `build/coverage-summary.py`, did **not** write
`build/core-tests/core-tests.csproj` or `coverage.runsettings`, did **not**
write `build/headless-build.sh`, and did **not** write any Core source or
test. It changed none of them during this check — only read them and ran
them. Its only writes are this file and `labels.txt`. It reused an existing
`.venv` already present in the working tree rather than creating a fresh one
(see "How to reproduce" for the from-scratch equivalent) and ran everything
from the existing working tree rather than a fresh `git clone`, noted under
"What was not checked".

## Verdict per VERIFY item

| # | Item | Result |
|---|---|---|
| 1 | Coverage report shows line rate at or above 90% on Core | **pass** — 94.4% (592/627), reproduced below |
| 2 | Lowering the threshold in CI and reverting proves the step actually fails | **pass** — gate exits 1 above the line rate, 0 below it, and is a real stage inside `build/headless-build.sh` that stops the script |

## What was checked

**1. The report and the number.** `dotnet test build/core-tests/core-tests.csproj --settings build/core-tests/coverage.runsettings --results-directory TestResults` (run fresh, `TestResults/` deleted first) passed 137/137 tests and wrote
`TestResults/4c917d81-.../coverage.cobertura.xml`. `.venv/bin/python build/coverage-summary.py --min 90` read that file and printed:

```
TOTAL Core: 592/627 = 94.4%  (uncovered methods listed above)
```
exit code `0`. 94.4% ≥ 90%, so item 1 holds. `build/coverage-summary.py:20-26` filters classes to `name.startswith("CatShelter.Core")` and excludes `CatShelter.Core.Tests`, and `build/core-tests/coverage.runsettings:14-15` applies the same `Include`/`Exclude` at the coverlet-collector level — the double filter that fixes the defect the 2026-08-26 `VERIFY.md` found (test code inflating the count to 97%). `build/core-tests/core-tests.csproj:29-32` now references `coverlet.collector` 6.0.2 with a comment explaining why (`XPlat Code Coverage` silently wrote no report at all without it — the exact prior failure).

**2. The gate, both directions, raw output:**

```
$ .venv/bin/python build/coverage-summary.py --min 95
[... 21 uncovered-method lines ...]
TOTAL Core: 592/627 = 94.4%  (uncovered methods listed above)
FAIL: line rate 94.4% is below the required 95.0%
$ echo $?
1
```

```
$ .venv/bin/python build/coverage-summary.py --min 90
[... same 21 lines ...]
TOTAL Core: 592/627 = 94.4%  (uncovered methods listed above)
$ echo $?
0
```

A gate never seen failing is not a gate — this one was, deliberately, at a threshold (95) picked above the measured 94.4%, and it failed with the exact message `coverage-summary.py:42` produces (`sys.exit` on a low rate).

**3. Where the gate lives, and that it is real.** `build/headless-build.sh:119-120`:
```
stage "coverage gate (>= 90% on Core, task 20-rules-core/05-coverage)"
"$PYTEST_BIN" build/coverage-summary.py --min 90
```
runs bare (not wrapped in an `if`) under `set -euo pipefail` (line 20) and `trap on_error ERR` (line 50), so any non-zero exit here trips `on_error`, prints `== STAGE FAILED: ... ==`, and exits the whole script with that code. This is not a line that always passes — it is the same mechanism that gates every other stage in the file (core-purity, C# tests, Python tests). To confirm the mechanism itself, not just that `coverage-summary.py` returns 1, an isolated bash snippet reproducing the exact same `set -euo pipefail` / `trap on_error ERR` / bare-stage-call pattern was run with `--min 95`:
```
$ bash -c 'set -euo pipefail; STAGE="(none)"; on_error(){ local c=$?; echo "== STAGE FAILED: $STAGE (exit $c) ==" >&2; exit "$c"; }; trap on_error ERR; STAGE="fake coverage gate"; .venv/bin/python build/coverage-summary.py --min 95 >/dev/null; STAGE="stage after the gate"; echo "THIS LINE MUST NOT PRINT if the gate really stops the build"'
FAIL: line rate 94.4% is below the required 95.0%
== STAGE FAILED: fake coverage gate (exit 1) ==
$ echo $?
1
```
The line after the gate never printed — the pattern used in the real script genuinely halts, not merely logs.

**4. Full stage, `--tests-only`:**

```
$ ./build/headless-build.sh --tests-only
== STAGE: core-purity check ==
Core is engine-free: OK

== STAGE: C# tests (dotnet test, Core) ==
Пройден!   : не пройдено     0, пройдено   137, ... - core-tests.dll (net8.0)

== STAGE: Python tests (pytest, tools/) ==
144 passed in 5.86s

== STAGE: coverage gate (>= 90% on Core, task 20-rules-core/05-coverage) ==
TOTAL Core: 592/627 = 94.4%  (uncovered methods listed above)

== --tests-only: skipping Unity build stages and the signing stage ==
$ echo $?
0
```
All four stages ran in order, including the coverage gate at its default `--min 90`, and the whole script exited 0.

**5. The system-python trap is real, not hypothetical.** Running the summary script under the Homebrew `python3` (no `.venv`) on this machine:
```
$ python3 -c "import xml.etree.ElementTree as ET; ET.parse('x')"
...ImportError: dlopen(.../pyexpat...): Symbol not found: _XML_SetAllocTrackerActivationThreshold
```
confirms `headless-build.sh:97-114`'s stated reason for requiring `.venv/bin/python3` (or `$PYTHON`) is accurate on this machine, not a guess.

## Judgment on "enforced by the build"

OUTCOME says "threshold enforced by the build", not "enforced by CI" — and this project has no hosted CI server anywhere (`tasks/AUDIT-2026-08-27.md` item 1, `git ls-files | grep -icE '^\.github|^\.gitlab|Jenkinsfile|azure-pipelines|\.circleci'` → 0, unchanged today). `build/headless-build.sh` is the project's only build, by the same project's own naming (task `60-shell-build/13-headless-build`). Run today, it genuinely stops — proven above in both a raw-script direction and an isolated reproduction of its exact trap mechanism. On the literal wording of OUTCOME and VERIFY item 2 (which also says "in CI" while none exists), I judge this **satisfied, not merely approximated**: the gate is a real, working stage of the one build this project has, and it fails when it should and only when it should.

That said, there is a real limit worth stating plainly so it is not mistaken for more than it is: nothing in this repository *invokes* `headless-build.sh` automatically. There is no pre-push hook, no branch protection, nothing that stops a human or an agent from committing a coverage regression without ever running this script. "Enforced by the build" is true in the sense that running the build enforces it; it is not true in the sense of "cannot be bypassed" — that would need a hook or a hosted runner, neither of which exists or was promised by this OUTCOME. I am not failing the item over this, because OUTCOME does not ask for un-bypassable enforcement, but a reader should not infer more automation than exists.

## How to reproduce

From a clean checkout — nothing exported by hand, no `.venv` assumed to pre-exist (a clean checkout has none: `.gitignore:8` excludes it):

```bash
git clone <repo-url> /tmp/verify-05 && cd /tmp/verify-05
git rev-parse --short HEAD          # expect e0e949f for the numbers in this file

# .NET SDK must already be on PATH (this check used dotnet 10.0.400,
# core-tests.csproj rolls forward from its declared net8.0 — see
# core-tests.csproj:10-12). Python venv must be created first — a clean
# checkout has neither .venv nor a working system python3 (pyexpat is broken
# under this machine's Homebrew Python 3.14):
python3 -m venv .venv && .venv/bin/pip install -r requirements.txt

# the gate directly, both directions
rm -rf TestResults
dotnet test build/core-tests/core-tests.csproj --nologo \
  --settings build/core-tests/coverage.runsettings --results-directory TestResults
.venv/bin/python build/coverage-summary.py --min 95   # expect FAIL, exit 1
.venv/bin/python build/coverage-summary.py --min 90    # expect TOTAL line + exit 0

# the whole gated build
./build/headless-build.sh --tests-only   # expect exit 0, four stages, coverage gate included
```

## What was not checked

- **Branch coverage.** GOAL says "every termination branch reached"; only line
  coverage is measured and gated (OUTCOME/SCOPE only say "line rate" and
  "threshold" — branch coverage is not in VERIFY's two items either). The
  cobertura report from this run still shows a zero-hit branch:
  `Board.cs:136` (`condition-coverage="0% (0/2)"`, `hits="0"`) — the same
  defensive win/jam branch flagged in `20-rules-core/04-outcomes/VERIFY.md`,
  documented in `Board.cs:125-134` as unreachable by construction. Not a
  failure of this task's VERIFY items, but the GOAL sentence about branches
  is not enforced by anything in this repository.
- **A fresh `git clone`.** This check ran in the existing working tree
  (`git status --porcelain -- tasks/20-rules-core/05-coverage/` was clean
  before this file was written), reusing the `.venv` already present rather
  than building one from scratch. The "How to reproduce" commands above were
  not executed in an actual `/tmp` clone; they were assembled from the exact
  commands that were run in place, plus the documented `.venv` setup.
- **Automatic invocation.** Nothing was checked or found that runs
  `build/headless-build.sh` on a schedule, on push, or via any hook — see
  "Judgment" above. If nobody runs the script, the gate does not fire.
- **Whether 90% is the right number**, or whether `IncludeTestAssembly=true`
  in `coverage.runsettings` distorts the production-class figures compared to
  a project-reference layout. Neither was re-litigated; both were already
  covered by the 2026-08-26 verification of this same task.
- **Unity/IL2CPP coverage.** The measured 94.4% comes from a `net8.0` console
  build of the same sources (`core-tests.csproj`), not from Unity's own
  `com.unity.testtools.codecoverage` inside the editor or an IL2CPP build;
  the two are not guaranteed to agree.
- **The Unity build and signing stages of `headless-build.sh`.** Only
  `--tests-only` was run. The Android/iOS build stages and the signing stage
  were not exercised — they are out of this task's scope (task
  `60-shell-build/13-headless-build` and `/14-testflight` own those).
