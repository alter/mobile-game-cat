# Independent verification, 2026-08-27

**Verifier:** a fresh agent context. I wrote none of `game/Assets/Core/Shelf.cs`,
none of `game/Assets/Tests/Core/BoardTests.cs`, none of `tools/solver/rules.py`,
and none of `tools/tests/conformance_test.py`. I did not run a Unity build, the
emulator, or `adb`. I ran `dotnet test build/core-tests/core-tests.csproj -v q
--nologo` and `.venv/bin/python -m pytest tools/ -q` myself, and built two
throwaway probes entirely outside this repository (`/tmp`-scoped, listed under
"How to reproduce") to observe behaviour directly rather than trust either the
old `VERIFY.md` in this directory or `NOTES.md`-style claims elsewhere.

**Provenance.** This file replaces an earlier `VERIFY.md` (2026-08-26, commit
`27f9904`) that found `Match_CompletesAcrossRowBoundary` didn't actually cross
a row — three copies placed into an empty shelf land in slots 0–2, all inside
row 0. That finding was real; the fix (renaming the within-row test, writing a
genuine boundary-spanning one) was applied the same day, but the fixing
context could not sign off its own fix, so `verify:` stayed `pending`. This
check re-examines the fixed state independently, and adds three things the
2026-08-26 pass didn't cover: whether the task's own text still matches D16
(decided the next day), whether capacity growth stays test-only per D4, and
whether the Python mirror is actually compared on shelf mechanics.

## Per-item verdict

| # | Item | Result |
|---|---|---|
| Task VERIFY 1 | match at a slot boundary, full shelf, match after a slot frees | **pass** — all three now real |
| Task VERIFY 2 | a shelf grown by three accepts three more items | **pass** — true of the code, confirmed by direct probe |
| D16 | does `03-shelf-match`'s own text still imply compaction/sorting? | **pass — never claimed it** |
| D4 | `AddSlots`/`AddShelfSlots` called only from tests; the three booster sub-cases tested | **pass** |
| Conformance | is shelf placement/matching inside the C#↔Python comparison? | **No — outcomes and counts only, not mechanics.** Proven by mutation, not inferred. |

### Task VERIFY 1 — all three sub-cases now real

Read `BoardTests.cs`'s current `ShelfTests` fixture directly:

- **Full shelf**: `FullShelf_PlacementRefused` (line 234) — fills all nine,
  asserts `IsFull`, asserts a tenth is refused. Unchanged from the prior
  check, still correct.
- **Match after a slot frees**: `Place_AfterMatchFreesSlots_ReusesFreeSlot`
  (line 166) — three `"x"` placed, the third completes and clears the match,
  `Occupied` is 0, then `"y"` lands at slot 0. Unchanged, still correct.
- **Match at a slot boundary** (line 193, now named
  `Match_CompletesAcrossRowBoundary`): the fix from 2026-08-26 is real. It
  fills slots 0–4 with `a,b,c,a,b`, **asserts the two `a` copies sit at
  indices 0 and 3** (`Assert.That(occupied, Is.EqualTo(new[] { 0, 3 }))`)
  before ever placing the third `a`, then places it and asserts all three `a`
  are gone. This genuinely straddles `SlotsPerRow = 3`, unlike the version the
  2026-08-26 check flagged.

### Task VERIFY 2 — true of the code; no single committed test states it plainly

No test in the current suite grows a *full* shelf by exactly three and takes
exactly three more, refusing a fourth — `AddSlots_GrowsCapacity_KeepsPlacedItems`
(line 247) grows by **one** and accepts **one**; `Booster_ResumesAJammedBoard`
grows by three but only takes **one** item afterward, because its point is
resuming a jam, not saturating the new capacity. So I checked the literal
claim myself, outside the repo:

```
before: capacity=9 occupied=9 full=True
  item 0 placed=True
  item 1 placed=True
  item 2 placed=True
  item 3 placed=False
after +3: capacity=12 accepted(of 4 tried)=3 occupied=12
```

Holds. `Shelf.AddSlots` is a plain array copy with no capacity-dependent
branching (`Shelf.cs:32-39`), so this isn't surprising, but "shouldn't be
surprising" is not the same as tested, and it wasn't, directly, anywhere.

### D16 — `03-shelf-match`'s own text never claimed compaction

`task.txt`'s SCOPE and OUTCOME say nothing about slot order after a match —
only "Shelf.TryPlace, Shelf.TryMatch across all nine slots" and "a single
matching rule over all slots." Its CONTEXT, `cat-shelter-mvp.md` §3, is
equally silent: "Three identical items on a shelf — they disappear, the slot
frees up," nothing about the other slots shifting. Neither document was ever
contradicted by D16; D16 settled a question this task's own text simply never
raised. `Shelf.cs` itself now carries D16 verbatim in `TryMatch`'s XML doc
remarks ("the shelf neither compacts nor sorts... DECISIONS.md D16"), so the
decision is recorded where the behaviour lives, not just in `task.txt`. No
correction needed here — reporting a clean result plainly, per the standing
rule that one is worth as much as a finding.

### D4 — capacity growth stays test-only, and the three sub-cases are real

```
$ grep -rn "AddShelfSlots" game/Assets
$ grep -rn "\.AddSlots(" game/Assets
```

Every call site is `game/Assets/Tests/Core/{BoardTests,BoardSaveTests,SaveResumeTests}.cs`,
plus the two method definitions themselves. `game/Assets/View/DebugGameView.cs:375`
only *mentions* `Board.AddShelfSlots` in a comment explaining why the lose-screen
booster button was removed (D4, revised 2026-08-27) — it does not call it.
D4's three required agreements are each their own test in `BoardTests.cs`,
read and confirmed correct:

- `Booster_ResumesAJammedBoard` (442) — jammed board, `AddShelfSlots(3)`,
  `IsOver` false, `Outcome` null, a further move succeeds.
- `Booster_LeavesAWonBoardWon` (456) — won board, `AddShelfSlots(3)`, still
  `IsOver`, still `Win`.
- `Booster_StaysJammedWhenItOpensNoMove` (471) — jam where every remaining
  item is permanently locked, `AddShelfSlots(3)`, still `IsOver`, still
  `ShelfJammed`.

### Conformance — outcomes and counts are compared, shelf mechanics are not

`tools/tests/conformance_test.py` drives both engines through the same move
scripts via `build/solver-bridge` and asserts three things per case:
`outcome`, `occupied` (a **count**: `capacity - shelf.count(None)`), and
`triples` (a **count**). It never serialises or compares the shelf array's
actual contents or slot order on either side.

**Mutation test, run outside the repository** (full recipe below): copied
`tools/` and `build/solver-bridge/` to `/tmp`, changed Python's placement rule
in the copy from leftmost-free (`self.shelf.index(None)`) to rightmost-free —
a real, checkable divergence from C#'s `Array.IndexOf(_slots, null)`, which
still stays unchanged and real. Confirmed the mutation actually changes
behaviour first (placing `a,b,c` now lands them at slots `8,7,6` instead of
`0,1,2`). Then ran the full conformance suite against the mutated copy:

```
....                                                                     [100%]
4 passed in 4.62s
```

**All four tests still pass.** A C#/Python engine that disagree on which
physical slot every item ends up in are indistinguishable to this harness, as
long as they agree on the win/jam outcome and the final counts. That is a
real, currently-true gap — not a defect in `03-shelf-match` (its own VERIFY
items say nothing about Python), but a fact worth recording plainly: **shelf
placement and matching mechanics are not part of what the two engines are
checked to agree on — only their outcomes.**

## How to reproduce

```bash
# task VERIFY 1 and 2 — read the current tests
sed -n '149,259p' game/Assets/Tests/Core/BoardTests.cs

# task VERIFY 2 — direct probe, outside the repo
mkdir -p /tmp/shelf-probe-03 && cd /tmp/shelf-probe-03
cat > probe.csproj <<'EOF'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework>
    <LangVersion>9</LangVersion><RollForward>Major</RollForward>
    <ImplicitUsings>disable</ImplicitUsings><Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup><Compile Include="<repo>/game/Assets/Core/**/*.cs" /></ItemGroup>
</Project>
EOF
cat > Program.cs <<'EOF'
using System; using CatShelter.Core;
static class Probe { static void Main() {
  var s = new Shelf();
  for (int i = 0; i < 9; i++) s.TryPlace(new Item(i, new ItemKind("k"+i,"k"+i)), out _);
  s.AddSlots(3);
  int accepted = 0;
  for (int i = 0; i < 4; i++)
    if (s.TryPlace(new Item(100+i, new ItemKind("n"+i,"n"+i)), out _)) accepted++;
  Console.WriteLine("capacity=" + s.Capacity + " accepted(of 4)=" + accepted);
} }
EOF
dotnet run --project probe.csproj   # expect: capacity=12 accepted(of 4)=3

# D4 — grep the real repo
grep -rn "AddShelfSlots" game/Assets
grep -rn "\.AddSlots(" game/Assets

# conformance mutation test — outside the repo, nothing here modified
mkdir -p /tmp/shelf-mut && cp -R tools /tmp/shelf-mut/tools
mkdir -p /tmp/shelf-mut/build && cp -R build/solver-bridge /tmp/shelf-mut/build/solver-bridge
rm -rf /tmp/shelf-mut/build/solver-bridge/{bin,obj}
ln -s "$(pwd)/game" /tmp/shelf-mut/game
python3 -c "
path = '/tmp/shelf-mut/tools/solver/rules.py'
t = open(path).read()
old = 'self.shelf[self.shelf.index(None)] = item.kind'
new = 'self.shelf[len(self.shelf) - 1 - self.shelf[::-1].index(None)] = item.kind'
assert t.count(old) == 1
open(path, 'w').write(t.replace(old, new))
"
cd /tmp/shelf-mut
PYTHONPATH=/tmp/shelf-mut <repo>/.venv/bin/python -m pytest tools/tests/conformance_test.py -q
# expect: 4 passed — despite the mutation

# baseline sanity (unmutated, real repo)
cd <repo> && .venv/bin/python -m pytest tools/tests/conformance_test.py -q   # 4 passed
dotnet test build/core-tests/core-tests.csproj -v q --nologo                 # 152 passed
.venv/bin/python -m pytest tools/ -q                                         # 149 passed
```

## What was not checked

- **Whether the conformance gap is this task's problem to fix.** `03-shelf-match`'s
  own VERIFY items say nothing about Python parity; `tools/solver/rules.py`
  and `tools/tests/conformance_test.py` belong to the levels/solver side of
  the tree (`30-levels-solver`), not to this task's SCOPE. This file reports
  the gap because it was asked for, not because closing it belongs here.
- **Whether the gap matters for anything the project currently measures.**
  Win rate and jam rate depend only on availability and full-shelf timing,
  not on which physical slot an item lands in, so the existing solver
  measurements (`30-levels-solver/10-remeasure-curve-partial-info`) are not
  obviously affected — not verified either way, out of scope here.
- **Tie-breaking when two kinds reach three copies simultaneously.** Same gap
  the 2026-08-26 check named: `TryMatch`'s `GroupBy` and `_try_match`'s dict
  iteration both take "first kind encountered scanning from slot 0," and they
  agree by construction, but no test on either side pins it.
- **`Shelf.AddSlots(0)`** and other edge values — not re-checked; the prior
  file already noted `Shelf.cs:35`'s negative-capacity guard has zero
  coverage-report hits, and nothing here changed that.
- **The Unity Editor / on-screen shelf.** No build, no emulator, per this
  check's own constraints — Core-level behaviour only.

## Verdict

`verify:passed` for `20-rules-core/03-shelf-match` on its own stated VERIFY
items, both of which hold against the current `Shelf.cs` and its tests,
independently confirmed. `status:` stays `done` — nothing here requires
moving it. The conformance-scope finding is real and worth a task somewhere
in `30-levels-solver`, but it is not a failure of this task's own OUTCOME
("shelf with variable capacity and a single matching rule over all slots"),
which is what it is.
