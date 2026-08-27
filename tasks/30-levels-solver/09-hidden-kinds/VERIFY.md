# Independent verification, 2026-08-27

**Verifier:** a fresh agent context. Wrote none of `Core/Board.cs`, `Core/Item.cs`,
`tools/solver/rules.py`, `PartialInformationTests.cs`, `test_rules.py`, or the
view code. No Unity build, no adb/emulator. Ran `dotnet test
build/core-tests/core-tests.csproj -v q --nologo` and `.venv/bin/python -m
pytest` myself; one run hit a transient build error (`Ошибка сборки`) while
another agent's concurrent edit to `game/Assets/Core/Level.cs` was mid-flight —
re-ran per the standing instruction and got a clean, real result. Two mutation
tests built entirely outside this repo (`/tmp`-scoped). **Note on scope:**
`build/solver-bridge/Program.cs` and `tools/tests/conformance_test.py` are
being actively widened by another agent right now (to compare shelf slots and
revealed-ness, motivated by my earlier `03-shelf-match` mutation finding); I
read both but wrote neither, and I distinguish committed (`HEAD`) state from
today's uncommitted working-tree state throughout, since they now disagree.

## Per-item verdict

| # | Item | Result |
|---|---|---|
| Task VERIFY 1 | hidden while `blocked_by` non-empty; revealed once blockers taken | **pass** |
| Task VERIFY 2 | `check-core-purity.sh` | **pass** |
| Task VERIFY 3 | blocks `10-remeasure-curve-partial-info` until green | **pass — and satisfied; that task has since run** |
| Difficulty claim | does the task's own text still claim an effect the measurement refuted? | **Yes — `NOTES.md`, uncorrected** |
| D15 interaction | both engines implement it; no test pins the old behaviour | **pass, both sides** |
| Mutation | `IsRevealed`/`is_revealed` → always `true` | **Same-language tests catch it on both sides. Cross-engine: caught today, missed at `HEAD`.** |

### Task VERIFY 1 — real

`PartialInformationTests.cs`: `BuriedItem_KindHidden_UntilReachable` (a
covered item is not revealed, an uncovered one is) and
`TakingCover_RevealsBuriedItem` (taking the cover flips it) both pass, 8/8 in
the fixture. `tools/tests/test_rules.py::test_buried_kind_hidden_until_reachable`
mirrors it exactly, 21/21 pass in the file.

### Difficulty claim — still there, still wrong, never corrected here

`NOTES.md`'s "It invalidates the measured win-rate table" section: *"Once
kinds are hidden, a player cannot plan ahead that way, and those rates will
fall, **possibly a long way**."* That is a difficulty-effect prediction.
`DECISIONS.md` D3 — the decision this task implements — carries its own
correction, appended 2026-08-26: *"hiding changed nothing... 0.0 ± 1.2
percentage points... this decision aimed at the wrong half."* This task's own
`NOTES.md` was never updated to say so; it stops at a 2026-08-26
`status:todo → done` entry about whether the mechanism exists, not about
whether the effect it predicted was real. The prediction was reasonable when
written and wrong when checked — both true, but only one is recorded here.

### D15 — both engines agree, no stale test

`Board.IsRevealed` (`Board.cs:85-97`): revealed iff not taken and all
blockers taken — lock status not checked. `rules.py::is_revealed`: identical,
with a comment citing D15 by name. `PartialInformationTests.LockedItem_IsRevealedButNotTakeable`
and `test_rules.py::test_locked_item_is_revealed_but_not_takeable` both assert
`is_revealed`/`IsRevealed` **true** for a locked item — the old
`LockedItem_IsNotRevealed` test D15 itself names as the prior pin is gone from
both files; grepped for it, no hits. One loose end, not in either engine but
in the doc comment three lines above the C# method: the `<summary>` still
reads *"nothing covers it and no complication locks it"* — pre-D15 wording —
while the method body and the remark immediately below it correctly implement
D15. Worth a one-line fix in `Board.cs`, out of this touch scope to make.

### Mutation — same-language tests catch it; cross-engine parity depends on when you ask

Set `IsRevealed`/`is_revealed` to unconditionally return `true` in throwaway
copies (recipe below):

- **C#, own tests:** 1 failed, 7 passed, 8 total —
  `BuriedItem_KindHidden_UntilReachable` catches it.
- **Python, own tests:** 1 failed, 20 passed, 21 total — the mirror test
  catches it.
- **Cross-engine, against `HEAD`'s conformance test:** at the last commit,
  `expected[...]`/`results.Add(...)` carry no `revealed` key at all (`git show
  HEAD:tools/tests/conformance_test.py` / `Program.cs` — confirmed by
  reading, not assumed). A one-sided revealed-ness mutation would have passed
  silently — the same asymmetry family as the shelf-placement gap.
- **Cross-engine, against today's widened (uncommitted) conformance test:**
  mutated only the Python copy, ran it against the real, unmutated C# bridge:
  `test_csharp_and_python_agree` and `test_locked_items_agree` both fail
  (`revealed python=True csharp=False`); `test_booster_recovery_agrees` and
  `test_locked_level_solution_agrees` still pass, since they never compared
  revealed-ness. **So: as of right now the gap is closed, but it did not
  exist by accident of testing — it existed at `HEAD` and was closed today,
  in response to exactly this kind of check.**

## How to reproduce

```bash
# task VERIFY 1-2
dotnet test build/core-tests/core-tests.csproj -v q --nologo --filter "FullyQualifiedName~PartialInformationTests"
bash build/check-core-purity.sh

# D15 — no stale test
grep -n "IsNotRevealed\|is_not_revealed" game/Assets/Tests/Core/PartialInformationTests.cs tools/tests/test_rules.py   # empty

# HEAD vs working tree, conformance scope
git diff --stat -- build/solver-bridge/Program.cs tools/tests/conformance_test.py
git show HEAD:tools/tests/conformance_test.py | grep -n "revealed"   # empty at HEAD

# mutation — Python, own tests + cross-engine (outside the repo)
mkdir -p /tmp/hidden-mut && cp -R tools /tmp/hidden-mut/tools
mkdir -p /tmp/hidden-mut/build && cp -R build/solver-bridge /tmp/hidden-mut/build/solver-bridge
rm -rf /tmp/hidden-mut/build/solver-bridge/{bin,obj}
ln -s "$(pwd)/game" /tmp/hidden-mut/game
python3 -c "
p='/tmp/hidden-mut/tools/solver/rules.py'; t=open(p).read()
old='''    def is_revealed(self, item: PileItem) -> bool:
        \"\"\"Task 3.9: kind visible only once reachable.\"\"\"
        if item.id in self.taken:
            return False
        # Locked is not hidden - the player must see which kind is withheld.
        # Mirrors Board.IsRevealed; see tasks/DECISIONS.md D15.
        return all(b in self.taken for b in item.blocked_by)'''
new='    def is_revealed(self, item): return True'
assert t.count(old)==1; open(p,'w').write(t.replace(old,new))
"
cd /tmp/hidden-mut
PYTHONPATH=/tmp/hidden-mut <repo>/.venv/bin/python -m pytest tools/tests/test_rules.py -q       # 1 failed, 20 passed
PYTHONPATH=/tmp/hidden-mut <repo>/.venv/bin/python -m pytest tools/tests/conformance_test.py -q  # 2 failed, 2 passed

# mutation — C#, own tests (outside the repo)
mkdir -p /tmp/hidden-mut-cs && cp -R game/Assets/Core /tmp/hidden-mut-cs/Core
cp -R game/Assets/Tests/Core /tmp/hidden-mut-cs/Tests
# write mut-tests.csproj with EnableDefaultCompileItems=false, Include Core/**/*.cs and Tests/**/*.cs,
# NUnit + Microsoft.NET.Test.Sdk + NUnit3TestAdapter + Newtonsoft.Json (see build/core-tests/core-tests.csproj)
# then in Tests copy's Board.cs, replace IsRevealed's body with `return true;`
cd /tmp/hidden-mut-cs
dotnet test mut-tests.csproj -v q --nologo --filter "FullyQualifiedName~PartialInformationTests"  # 1 failed, 7 passed
```

## What was not checked

- **The DebugGameView rendering, on screen.** Read `MakeTile` (calls
  `_board.IsRevealed`, applies `game__tile--hidden`, draws `prop_unknown`) —
  not run in a build/emulator, per constraints.
- **Whether the widened bridge itself is fully correct** (only that it does
  catch the specific mutation tried). It's mid-edit by another agent; not
  mine to sign off.
- **The stale `<summary>` wording on `Board.IsRevealed`** — named above, not
  fixed, outside this touch scope.
- **Tie-breaking and other gaps already on record** from `03-shelf-match`'s
  `VERIFY.md` (shared `TryMatch`/`_try_match` code path) — not re-derived.

## Verdict

`verify:passed` for `30-levels-solver/09-hidden-kinds` on its own three
VERIFY items, all confirmed. `status:` stays `done`. The uncorrected difficulty-effect
sentence in `NOTES.md` and the stale doc-comment in `Board.cs` are both real,
both small, and both outside what this task's own VERIFY asked for — flagged
above rather than fixed, since fixing `Board.cs` is outside this touch scope
and `NOTES.md`'s prose was reporting, not testing.
