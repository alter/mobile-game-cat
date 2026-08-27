Verifier: independent QA context, wrote none of `game/Assets/Core/Board.cs`,
`game/Assets/Core/Item.cs`, `tools/solver/rules.py`, `tools/solver/generate.py`,
`tools/tests/conformance_test.py`, `tools/tests/test_rules.py`,
`game/Assets/Tests/Core/PartialInformationTests.cs`, `tasks/DECISIONS.md` D15,
or this task's own `task.txt`/`NOTES.md`. Read D15 in full before judging this
task, as instructed, since it postdates this task's `status:done` by one day
and changes what a locked item does. Built a scratch mirror of
`tools/solver` + `tools/tests/conformance_test.py` + `build/solver-bridge` +
`game/Assets/Core` outside the repo to mutation-test the cross-engine mirror.
Did **not** run any Unity build, PlayMode test, the Android emulator, or adb
— `dotnet test` and the conformance suite's own `dotnet run --project
build/solver-bridge` need none of them.

## Verdict by item

| # | Question | Verdict | Evidence |
|---|---|---|---|
| 1 | Task's own VERIFY items | **1: pass. 2: correctly left open. 3: pass.** | VERIFY 1 (unlocks exactly at N, not before): `PartialInformationTests.LockedItem_NotAvailable_UntilThreshold` and `test_rules.py::test_locked_item_not_available_until_threshold` both check locked-before/available-after at the N=1 boundary; ran `dotnet test --filter PartialInformationTests` → `8 passed`, and `pytest tools/tests/test_rules.py` is part of the 34/34 passing solver-test run below. Neither test exercises a threshold >1 with an intermediate triple count still locked (e.g. N=2 after 1 triple) — the implementation is a single `<` comparison in both languages, so this is a minor coverage gap, not a failure. VERIFY 2 ("reads as distinct to someone who did not build it") is correctly left `verify:pending`-worthy by the task's own NOTES.md: it names `07-outsiders-playtest` as the only context that can close it, and no such playtest exists yet — an agent cannot substitute (the independence rule, `role:HUMAN` clause). VERIFY 3: `bash build/check-core-purity.sh` → `Core is engine-free: OK`. |
| 2 | How many complications actually exist? | **Exactly one — the task's own title overclaims** | The title is "Three complications, one per room band," but GOAL/SCOPE both narrow to a single complication (locked items) and explicitly exclude "the full ladder." `cat-shelter-mvp.md` section 14 states outright: "In the MVP only **one** complication from this list is taken, task 3.11; the rest is second wave and beyond." Grepped `tools/solver` and `game/Assets/Core` for the other five ladder items (paired items, four-of-a-kind, external supply, order shuffle, blocked slot) by name and by mechanism — zero matches; `PileItem`/`Item` carry only `blocked_by`/`BlockedBy` (burial, pre-existing task 3.9/09) and `locked_after_triples`/`LockedAfterTriples` (this task, 3.11). Hidden kinds (09) is not a complication this task introduces — it is combined-with, per SCOPE's own second bullet. So: one complication in code, one in shipped data, matching the MVP doc; the "three" in the title refers to nothing that exists in code — it is stale, not a shipping gap. |
| 3 | Do the two engines agree on complications? | **Partially — takeability is conformance-tested, visibility is not** | `tools/tests/conformance_test.py::test_locked_items_agree` and `::test_locked_level_solution_agrees` drive both `rules.py`'s `RulesState` and the real C# `Board` (via `build/solver-bridge`, a subprocess that replays a move script through `Core`) on locked-item levels and compare `legal`/`outcome`/`occupied`/`triples`/`capacity` (read `build/solver-bridge/Program.cs`'s JSON output fields directly — no `revealed` field anywhere). D15 changed `IsRevealed`/`is_revealed`, not availability or outcome, so the bridge's comparison never touches the exact thing D15 changed. Both engines are separately unit-tested for D15's behaviour (`PartialInformationTests.LockedItem_IsRevealedButNotTakeable` in C#, `test_rules.py::test_locked_item_is_revealed_but_not_takeable` in Python, both dated 2026-08-27) — but nothing compares the two results to each other. |
| 4 | Mutation: make one engine disagree with the other on a complication | **Nothing failed — confirms the gap is real, same shape as the pacing-curve finding** | Built a scratch mirror (`tools/solver`, `tools/tests/conformance_test.py`, `build/solver-bridge`, `game/Assets/Core`, none of it the repo's own files). Baseline: `pytest tools/tests/conformance_test.py -v` → `4 passed`. Mutated only the scratch `Board.cs`'s `IsRevealed` to re-add `&& !IsLockedByComplication(item)` (the exact pre-D15 clause D15 removed), leaving `rules.py`'s `is_revealed` untouched — the two engines now genuinely disagree on whether a locked item is visible. Re-ran the same 4 conformance tests against the mutated mirror: **`4 passed`, unchanged.** Nothing in the suite noticed. `git status --short` in the real repo confirms no repo files were touched by this mutation. |
| 5 | Does this task's documentation reflect D15? | **No — `NOTES.md` still states the pre-D15 behaviour as current fact** | `tasks/30-levels-solver/11-complications/NOTES.md:32` (dated 2026-08-26, the day *before* D15): "`Item.LockedAfterTriples` gates both `GetAvailable` and `IsRevealed`" — that clause is exactly what D15 (2026-08-27) reversed ("`IsRevealed` no longer counts a locked item as hidden"). The line was not touched after D15 landed; grepped the whole task directory for `IsRevealed`/`hidden`/`visible`/`reveal` and this is the only hit besides the ladder-list mention of "hidden kinds" as a different complication. `task.txt` itself makes no visibility claim either way, so it is not contradicted, but `NOTES.md` is. |

## How to reproduce

From a clean checkout of `dev`, no exported variables:

```sh
cd game && git worktree add /tmp/verify-check-4 dev
cd /tmp/verify-check-4
bash build/check-core-purity.sh                                    # -> Core is engine-free: OK
dotnet test build/core-tests/core-tests.csproj -v q --nologo --filter "FullyQualifiedName~PartialInformationTests"  # -> 8 passed
.venv/bin/python -m pytest tools/tests/test_rules.py tools/tests/conformance_test.py tools/tests/test_generate.py -q  # -> 34 passed
grep -n "IsRevealed" game/Assets/Core/Board.cs tasks/30-levels-solver/11-complications/NOTES.md
grep -n "PilesPerRoom" build/solver-bridge/Program.cs  # (sanity: bridge reports legal/outcome/occupied/triples/capacity, no "revealed" field)
```

Mutation test (outside the repository — do not apply to the repo's own files):

```sh
SP=$(mktemp -d)/complications-mutation
mkdir -p "$SP/tools/solver" "$SP/tools/tests" "$SP/build/solver-bridge" "$SP/game/Assets/Core"
cp tools/__init__.py "$SP/tools/"
cp tools/solver/{__init__.py,generate.py,rules.py,solver.py,schema.py,pacing.py} "$SP/tools/solver/"
cp tools/tests/{__init__.py,conformance_test.py} "$SP/tools/tests/"
cp build/solver-bridge/{solver-bridge.csproj,Program.cs} "$SP/build/solver-bridge/"
cp game/Assets/Core/*.cs "$SP/game/Assets/Core/"
cd "$SP"
.venv_or_repo_python -m pytest tools/tests/conformance_test.py -v   # baseline: 4 passed
# edit "$SP/game/Assets/Core/Board.cs": in IsRevealed, add back
#   "&& !IsLockedByComplication(item)" to the return statement (the pre-D15 clause)
.venv_or_repo_python -m pytest tools/tests/conformance_test.py -v   # still 4 passed -- the gap
git status --short   # repo untouched
```

## What was not checked

- No Unity build, PlayMode test, Android emulator, or adb.
- VERIFY 2 (a late room "reads as distinct... to someone who did not build
  it") was not attempted by simulation or by me reading level files and
  judging — this is explicitly a `role:HUMAN`-shaped check per the
  independence rule, and NOTES.md already correctly defers it to
  `07-outsiders-playtest`, which has not run.
- Did not check whether `30-levels-solver/07-outsiders-playtest` or
  `30-levels-solver/10-remeasure-curve-partial-info` account for D15's
  visibility change in their own numbers — D15 itself already flags that the
  win-rate measurement in `10` predates the visibility fix and needs
  rerunning; not re-litigated here.
- Did not attempt a boundary-threshold (N>1, intermediate triple count) test
  for VERIFY 1 beyond noting the gap; the existing N=1 tests plus the
  single-comparison implementation make an off-by-one unlikely but not
  proven.
- A full `pytest tools/ -q` run at the time of this check reports `1 failed,
  147 passed` — `test_no_secrets.py::test_no_credential_value_is_tracked`,
  about `game/Assets/Shell/GameAnalyticsSink.cs` and `worker/src/index.ts` /
  `worker/test/traits.test.ts`. Confirmed via `git status --short` that none
  of those files are part of this task or were touched by this verification;
  this is concurrent, unrelated work in progress elsewhere in the repo. The
  solver/complications-relevant subset (`test_rules.py`,
  `conformance_test.py`, `test_generate.py`) passes cleanly in isolation (34
  passed), which is what this VERIFY relies on.
- Did not check `Item.cs`'s doc comment or other non-task documentation for
  D15 staleness beyond this task's own directory, per the "touch only that
  task directory" instruction.
