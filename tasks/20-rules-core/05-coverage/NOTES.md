
---

## status:done → in_progress, 2026-08-27

The OUTCOME artefact this task names is not there. What is missing, what does
exist, and why it matters: `tasks/AUDIT-2026-08-27.md`.

---

## status:in_progress → review, 2026-08-27

The tooling fix recorded above (26 Aug: `coverlet.collector` added to
`build/core-tests/core-tests.csproj`, `build/core-tests/coverage.runsettings`
added, the `CatShelter.Core.Tests` namespace excluded from the count,
`--min` added to `build/coverage-summary.py`) is now actually wired into a
build: `build/headless-build.sh` (task 60-shell-build/13-headless-build,
written today) runs, in order, `dotnet test
build/core-tests/core-tests.csproj --settings
build/core-tests/coverage.runsettings --results-directory TestResults` and
then `python3 build/coverage-summary.py --min 90` as its own stage, and exits
non-zero (via the script's stage-failure trap) if that stage exits non-zero.
That is the CI step VERIFY item 2 asked for — there still isn't a hosted CI
runner, but "the build" now means something concrete and runnable, which is
what OUTCOME asks for ("threshold enforced by the build").

Confirmed by running the gate directly against a fresh coverage report from a
clean `TestResults/`, both directions:

```
$ dotnet test build/core-tests/core-tests.csproj --nologo \
    --settings build/core-tests/coverage.runsettings --results-directory TestResults
...Пройден!   : не пройдено     0, пройдено   136, ... - core-tests.dll (net8.0)

$ .venv/bin/python3 build/coverage-summary.py --min 99.9
...
TOTAL Core: 585/624 = 93.8%  (uncovered methods listed above)
FAIL: line rate 93.8% is below the required 99.9%
$ echo $?
1

$ .venv/bin/python3 build/coverage-summary.py --min 90
...
TOTAL Core: 585/624 = 93.8%  (uncovered methods listed above)
$ echo $?
0
```

Current line rate on Core is 93.8% (585/624 lines), above the 90% floor; the
uncovered methods are listed by the script itself (`Board.IsTaken`,
`CatTraits.ToString`, etc.) — none of that list was touched, this task is
about the gate existing and firing, not about closing every method.

Note the count differs from the 91.8%/462/490-line figures quoted in the
2026-08-26 verification above: the working tree has moved since (more Core
code and tests landed, per that verification's own "not checked" section),
and the homebrew `python3` on this machine cannot even import
`xml.etree.ElementTree` (broken `pyexpat` under Python 3.14) — the commands
above and in `headless-build.sh` use `.venv/bin/python3`, which works.

`status:review`, not `passed`: this context wrote/wired the enforcement it is
now claiming works, so per the independence rule (`tasks/README.md`) it
cannot also sign off `verify:`. That line is left `pending` for a third,
independent context to flip, per the same rule already applied to this task
on 26 Aug.

What is still not built, and is out of this task's SCOPE anyway: branch
coverage enforcement (SCOPE/GOAL mentions "every termination branch reached";
only line coverage is gated) and a hosted CI runner — the gate is a script
stage today, not a server that runs on every push.
