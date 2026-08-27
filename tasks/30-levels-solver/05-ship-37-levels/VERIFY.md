Verifier: independent QA context, wrote none of `tools/solver/ship_levels.py`,
`tools/solver/generate.py`, `tools/solver/solver.py`, `tools/solver/rules.py`,
`tools/tests/test_ship_levels.py`, `game/Assets/Core/Board.cs`,
`game/Assets/Tests/Core/HeadlessRunTests.cs`, the 37 shipped level files, D15
in `tasks/DECISIONS.md`, or this task's own `task.txt`/`NOTES.md`. Ran the
real shipping script (`python -m tools.solver.ship_levels`) and diffed its
output against disk myself, wrote independent one-off scripts against the
actual shipped files (not just re-running the existing test suite), and built
two scratch programs (one Python, one C#) outside the repo for the mutation
test. Did **not** run any Unity build, PlayMode test, the Android emulator,
or adb — `dotnet test` needs none of them, and that is what was used for the
C# side throughout.

## Verdict by item

| # | Question | Verdict | Evidence |
|---|---|---|---|
| 1 | Winnability: is either check real, or does one replay the other's solution? | **Both real, independent proofs — with an asymmetry in what each actually proves about the shipped artifact** | Read `solver.py`'s `solve()`: a DFS that branches over *every* available item at each state (not a fixed heuristic order) with `(taken, shelf, triples)` memoization of failed states — a real existence search, not a replay. `ship_levels.py`'s `greedy_wins()` builds a **fresh** `RulesState` via `rules.new_state()` and plays it forward with its own heuristic, never reading `solve()`'s returned path — confirmed by reading the code, `solution` is unused inside `greedy_wins`. `HeadlessRunTests.cs`'s `AllThirtySevenLevelsPlayThroughToWin_Headless` is a third, independently-coded greedy simulation in C#, driven off `Board.TakeItem`/`GetAvailable` — it never touches Python output. Ran it directly: `dotnet test ... --filter FullyQualifiedName~HeadlessRunTests` → `5 passed`. Also ran `solve()`/`greedy_wins()` directly against the **actual files on disk** (not the test suite's regenerated copy): `load_level` + `solve()` + `greedy_wins()` over all 37 → 0 failures either way. **The asymmetry**: `test_ship_levels.py`'s solvability test calls `ship(tmp_path, seed=7)` and checks *that* output — it does not read `game/Assets/Resources/Levels` at all except in `test_filenames_match_what_the_player_loads`, which compares filenames only, not content. I confirmed the two are identical today by running `python -m tools.solver.ship_levels --out /tmp/x --seed 7` and `diff -rq` against `game/Assets/Resources/Levels` (excluding `.meta`) → 0 differences — but nothing in the test suite enforces that equivalence going forward. `HeadlessRunTests.cs`, by contrast, reads the shipped files straight off disk (`LevelLoader.LoadAllFromAssets`, dotnet-test branch walks up from `AppContext.BaseDirectory` to `game/Assets/Resources/Levels`), so it is the one check that directly proves winnability of the actual artifact, not of a reproducible-today generation process. |
| 2 | Pile-size band (36/48/60) and complications, against files on disk | **Matches exactly** | Parsed all 37 files directly: room 1–4 → 36 items each, room 5–8 → 48, room 9–12 → 60, zero band mismatches (script output: `band mismatches: []`). Locked kinds appear only in rooms 9–12, one kind per level, count always a multiple of 3 (verified per-file: `l22..l25` room 9 through `l34..l37` room 12, all `divisible by 3: True`). |
| 3 | D15: 16 of 37 carry a locked kind, visible in 16 of 16 | **Confirmed independently, both counts** | Counted directly from the 37 files: exactly 16 carry a `locked_after_triples` item (rooms 9–12, 4 levels each). Wrote a fresh simulation (not reusing D15's own script) against `tools/solver/rules.RulesState`, greedy play on all 16: the locked item is `is_revealed()`-true at move 0 on 15 of them and move 1 on one (`l22_room09_pile0.json`) — 16 of 16, matching D15's "first appearing on move 0 or 1" claim. Confirmed the code actually matches the decision: `Board.cs:85-98`'s `IsRevealed` no longer excludes locked items (the removed clause is documented in a comment on the same lines), `rules.py:61-67`'s `is_revealed` mirrors it, and `PartialInformationTests.cs`'s renamed `LockedItem_IsRevealedButNotTakeable` asserts `IsRevealed == true` for a locked item. `dotnet test --filter FullyQualifiedName~PartialInformationTests` → `8 passed`. |
| 4 | Mutation: corrupt a shipped level so it's unwinnable, on a copy outside the repo | **Something fails — at two different, meaningful layers** | Two mutations on scratch copies (never the repo's own files, confirmed by `git status --short` after both = empty). (a) A 2-item mutual block (`item 1 blocked_by [2]`, `item 2 blocked_by [1]`) — caught immediately by `schema.validate()`: `LevelValidationError: cycle in blocked_by`. (b) A **non-cyclic** mutation (every item `i≥5` blocked by `i-1,i-2,i-3`, ids always pointing downward, so it passes acyclic and dangling-ref checks) — `validate()` says OK, but `solve()` returns `None` (UNSOLVED): the real search-based winnability check, not schema validation, is what catches this one. Built the same non-cyclic mutation independently in a scratch C# console program (`Core/*.cs` copied out, `Level`/`Board` used directly, same kind assignments read read-only from the real `l01_room01_pile0.json`, corruption applied only in memory): `Level` construction succeeds (the C# `Level` constructor, unlike Python's `schema.validate`, does **not** check for cycles or dangling refs — an asymmetry worth noting), but playing it through the real `Board` with the same greedy policy `HeadlessRunTests` uses ends in `board.Outcome == ShelfJammed` after 12 moves, not a win. So mutation (b) is caught independently by both engines' actual play-through logic, which is the strongest possible confirmation that "all 37 are winnable" is a live, working claim and not an untested one. |

## How to reproduce

From a clean checkout of `dev`, no exported variables:

```sh
cd game && git worktree add /tmp/verify-check-3 dev
cd /tmp/verify-check-3
.venv/bin/python -m pytest tools/ -q                              # -> 145 passed
dotnet test build/core-tests/core-tests.csproj -v q --nologo      # -> 152 passed
dotnet test build/core-tests/core-tests.csproj -v q --nologo --filter "FullyQualifiedName~HeadlessRunTests"       # -> 5 passed
dotnet test build/core-tests/core-tests.csproj -v q --nologo --filter "FullyQualifiedName~PartialInformationTests" # -> 8 passed

# Reproduce the shipped set from the script and diff against disk:
mkdir -p /tmp/ship-check
.venv/bin/python -m tools.solver.ship_levels --out /tmp/ship-check --seed 7 > /dev/null
diff -rq /tmp/ship-check game/Assets/Resources/Levels --exclude="*.meta"   # -> no output (identical)

# Band / complication check directly off disk:
.venv/bin/python3 - <<'EOF'
import json, collections
from pathlib import Path
files = sorted(Path("game/Assets/Resources/Levels").glob("*.json"))
band = {}
locked_rooms = set()
for p in files:
    d = json.loads(p.read_text())
    room = int(d["room_id"].split("_")[1])
    band.setdefault(room, set()).add(len(d["pile"]))
    if any(i.get("locked_after_triples", 0) for i in d["pile"]):
        locked_rooms.add(room)
print(band, locked_rooms)
EOF
```

Mutation test (outside the repository):

```sh
cp game/Assets/Resources/Levels/l01_room01_pile0.json /tmp/mut.json
.venv/bin/python3 - <<'EOF'
import json
from tools.solver.schema import load_level, validate, LevelValidationError
data = json.load(open("/tmp/mut.json"))
by_id = {i["id"]: i for i in data["pile"]}
by_id[1]["blocked_by"] = [2]; by_id[2]["blocked_by"] = [1]
json.dump(data, open("/tmp/mut.json", "w"))
try:
    validate(load_level("/tmp/mut.json"))
    print("BUG: validate() did not catch the cycle")
except LevelValidationError as e:
    print("caught as expected:", e)
EOF
git status --short   # repo untouched
```

## What was not checked

- No Unity build, PlayMode test, Android emulator, or adb.
- Did not mutation-test every VERIFY item mechanically (e.g., did not
  separately corrupt the pacing curve, the pile count, or the room_id/pile
  pairing) — only the winnability claim named explicitly in the coordinator's
  brief.
- Did not re-derive `items_for_room`'s 36/48/60 band values or
  `LOCKED_KIND_FROM_ROOM = 9` from first principles (difficulty tuning); took
  them as given design constants and checked the shipped files match them.
- Did not check whether re-running `ship_levels.py` with a **different**
  seed than 7 still produces winnable levels for all 37 slots within the
  50-attempt budget — only seed 7 (the one actually shipped) was exercised.
- Did not check `30-levels-solver/07-outsiders-playtest` or
  `30-levels-solver/10-remeasure-curve-partial-info` — D15 itself says the
  human-playtest consequence of making the lock visible is still unmeasured;
  that is out of scope for this task and this VERIFY.
- The C# `Level` constructor's missing cycle/dangling-ref check (item 4) was
  observed but not filed or fixed anywhere — noted here as a finding, not
  acted on, since this verifier does not fix code.
