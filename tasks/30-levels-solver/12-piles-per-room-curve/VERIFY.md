Verifier: independent QA context, wrote none of `tools/solver/pacing.py`,
`tools/tests/test_pacing.py`, `game/Assets/View/LevelAssets.cs`, the 37
shipped level files, or this task's own `task.txt`/`NOTES.md`. In particular
did not write `test_csharp_mirror_of_the_curve_still_agrees` — the
coordinator wrote it today (per its own docstring, citing
`tasks/AUDIT-2026-08-27.md` item 7) and said so explicitly, which is why an
independent check was needed. Did **not** run any Unity build, PlayMode
test, the Android emulator, or adb — out of scope per the task brief and not
needed: nothing here depends on the engine running.

## Verdict by item

| # | Question | Verdict | Evidence |
|---|---|---|---|
| 1 | Task's own VERIFY items (cap ≤4, sum=37, shape matches) | **Pass** | `.venv/bin/python -m pytest tools/ -q` → `145 passed`. Directly relevant: `test_no_room_needs_more_than_four_piles`, `test_no_shipped_room_holds_more_than_four_piles` (VERIFY 1, generated and shipped), `test_shipped_piles_per_room_match_the_curve` and `test_level_map_covers_every_pile` (VERIFY 2, sum=37), `test_curve_matches_spec` and `test_monotone_nondecreasing` (VERIFY 3, shape `1,2,3,3,3,3,3,3,4,4,4,4`, not a permutation). `dotnet test build/core-tests/core-tests.csproj -v q --nologo` → `152 passed`, unaffected (this task touches no `Core` file). |
| 2 | Is the mirror regex brittle — would it silently test nothing under ordinary reformatting? | **Not silently vacuous, but genuinely brittle — flagged, not failed** | Stress-tested `r"PilesPerRoom\s*=\s*\{([^}]*)\}"` against five reformattings of `LevelAssets.cs` in a scratch script (not the repo). Result: (a) the line break that is *already* in the real file today parses fine — `\s*` spans newlines; (b) `new int[] { ... }` breaks the match entirely — `assert match` fires an `AssertionError`, a **loud** failure, not a silent pass; (c) a C#-12 collection expression `[ ... ]` likewise breaks the match, loud failure; (d) a comment placed *between* the braces that contains any digit (e.g. `// see pacing.py line 12`) is captured inside the group and its digits get spliced into the parsed tuple, producing a **false mismatch** — the test fails even though the two curves still agree, i.e. it cries wolf rather than staying silent. In every case tried, the test either raises loudly on "pattern not found" or fails loudly on a corrupted-but-visible tuple; no case produced a match that quietly and incorrectly passed. So the specific danger named ("quietly matches nothing") was not reproduced — but the regex is fragile against ordinary refactors of that one field and will need updating if `LevelAssets.cs` is ever reformatted, which is a real maintenance cost worth recording. |
| 3 | Mutation-test both directions, on copies outside the repo | **Pass — both directions caught** | Built a scratch mirror at `/private/tmp/.../scratchpad/mirror-mutation/{tools/solver/pacing.py, tools/tests/test_pacing.py, game/Assets/View/LevelAssets.cs}` (with the `tools`/`tools.solver`/`tools.tests` `__init__.py` files, so the import path matches the real repo). Baseline: `pytest -k test_csharp_mirror_of_the_curve_still_agrees` → 1 passed. Mutation A: changed the *C#* copy's last value `4`→`5`. Result: `AssertionError: View/LevelAssets.cs has (...,4,5), tools/solver/pacing.py has (...,4,4)` — failed as expected. Reverted, then mutation B: changed the *Python* copy's last value `4`→`3`. Result: `AssertionError: View/LevelAssets.cs has (...,4,4), tools/solver/pacing.py has (...,4,3)` — failed as expected. `git status --short game/Assets/View/LevelAssets.cs tools/solver/pacing.py tools/tests/test_pacing.py` in the real repo → empty, confirming the repo itself was untouched throughout. |
| 4 | Does the shipped 37-file set on disk actually match the curve, and is that third copy guarded? | **Matches, and is guarded (transitively)** | Independently parsed all 37 files under `game/Assets/Resources/Levels` (ignoring `.meta`): filenames give per-room counts `(1,2,3,3,3,3,3,3,4,4,4,4)`, identical to `PILES_PER_ROOM`; every file's internal `number`/`room_id`/`pile_index` agrees with its filename (0 mismatches out of 37, computed directly, not by re-running the test suite). `git status --short game/Assets/Resources/Levels/` → empty (nothing uncommitted). This third copy is guarded, but not by a *direct* disk-vs-C# comparison — no test reads both `LevelAssets.cs` and the shipped files together. The guard is transitive: `test_shipped_piles_per_room_match_the_curve`, `test_shipped_set_is_the_thirty_seven_levels`, `test_shipped_pile_indices_are_contiguous_from_zero`, `test_shipped_play_order_matches_the_level_map` and `test_shipped_files_agree_with_their_own_filenames` (added 2026-08-26, still passing) tie the shipped files to `pacing.py`; today's new `test_csharp_mirror_of_the_curve_still_agrees` ties `pacing.py` to `LevelAssets.cs`. Together the chain covers all three copies, closing the gap `AUDIT-2026-08-27.md` item 7 named — but it is a two-hop chain through `pacing.py`, not a single test naming all three, so a bug that happened to corrupt both `pacing.py` and the shipped set identically (e.g. a bad regeneration from a stale curve) would still be invisible to this test suite. |

## How to reproduce

From a clean checkout of `dev`, no exported variables:

```sh
cd game && git worktree add /tmp/verify-check-2 dev
cd /tmp/verify-check-2
.venv/bin/python -m pytest tools/ -q
# -> 145 passed
dotnet test build/core-tests/core-tests.csproj -v q --nologo
# -> Пройден!   : не пройдено 0, пройдено 152, ..., всего 152
ls game/Assets/Resources/Levels/*.json | wc -l    # -> 37
```

Mutation test (outside the repository — do not apply to the repo's own files):

```sh
SP=$(mktemp -d)/mirror-mutation
mkdir -p "$SP/tools/solver" "$SP/tools/tests" "$SP/game/Assets/View"
cp tools/__init__.py tools/solver/__init__.py tools/tests/__init__.py "$SP/tools/" 2>/dev/null
cp tools/solver/__init__.py "$SP/tools/solver/"; cp tools/solver/pacing.py "$SP/tools/solver/"
cp tools/tests/__init__.py "$SP/tools/tests/"; cp tools/tests/test_pacing.py "$SP/tools/tests/"
cp game/Assets/View/LevelAssets.cs "$SP/game/Assets/View/"
cd "$SP"
.venv_or_repo_python -m pytest tools/tests/test_pacing.py -k test_csharp_mirror_of_the_curve_still_agrees -q   # baseline: 1 passed
# edit "$SP/game/Assets/View/LevelAssets.cs": change the last `4` to `5`
.venv_or_repo_python -m pytest tools/tests/test_pacing.py -k test_csharp_mirror_of_the_curve_still_agrees -q   # fails
# revert, then edit "$SP/tools/solver/pacing.py": change the last `4` to `3`
.venv_or_repo_python -m pytest tools/tests/test_pacing.py -k test_csharp_mirror_of_the_curve_still_agrees -q   # fails
git status --short   # repo untouched
```

## What was not checked

- No Unity build, PlayMode test, Android emulator, or adb — out of scope,
  and nothing in this task depends on the engine running.
- The regex-brittleness stress test (item 2) covered five plausible
  reformattings, chosen from the coordinator's own examples plus one more
  (a C#-12 collection expression); it is not an exhaustive search of every
  way `LevelAssets.cs` could be reformatted. A genuinely adversarial rewrite
  (e.g. splitting the twelve numbers across twelve separate `const` fields)
  was not tried.
- NOTES.md records that the curve is also written at `Tests/Core/
  GameSaveTests.cs:36` and `Tests/Core/PlayerProgressTests.cs:15` (a fourth
  and fifth copy). Neither was re-checked here — NOTES already scopes that
  out ("belongs to whoever owns level loading"), and today's mirror test
  does not touch them either, so they remain unguarded exactly as NOTES
  already says. Whether NOTES' claim still reads accurately against the
  current file line numbers was not re-grepped in this pass.
- Did not check whether `ship_levels.py` (the generator that presumably
  wrote the 37 files) would reproduce byte-identical output if re-run today
  — only that the files currently on disk match the curve and their own
  filenames.
- No performance/CI-time concern was evaluated for `pytest tools/ -q`
  running 145 tests (5.46s, measured, not a hard budget in the task).
