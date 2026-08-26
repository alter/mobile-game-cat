# VERIFY — 20-rules-core/04-outcomes

Result: **failed** — item 2 does not hold. No test creates the boundary state it
names, and the branch in `Board` that would decide it never executes.

Verifier: an independent agent context, 2026-08-26, against `dev` at commit
`27f9904`. It did **not** write `game/Assets/Core/Board.cs` or any other Core
source, did **not** write `game/Assets/Tests/Core/BoardTests.cs`, and changed
none of them during this check. It did not run the Unity editor and did not play
the game. Its only writes were this file and `labels.txt`.

## Verdict per VERIFY item

| # | Item | Result |
|---|---|---|
| 1 | One unit test per outcome | pass |
| 2 | A test for the boundary case: last item empties the pile and fills the shelf → Win | **fail** |

### 1. One test per outcome — pass

| outcome | test | line |
|---|---|---|
| `Win` | `Win_PileCleared_EvenWhenLastTakeFillsShelf` | `BoardTests.cs:244` |
| `ShelfJammed` | `ShelfJammed_UnmatchedKindsFillTheShelf` | `BoardTests.cs:262` |

Both were read, not just counted. The jam test takes one of each of five kinds
into a nine-slot shelf and asserts the run ends `ShelfJammed`
(`BoardTests.cs:272-280`); the win test drives a twelve-item pile to empty and
asserts `Win` (`BoardTests.cs:255-258`). `GameOutcome` has exactly the two
members and no `OutOfMoves` (`game/Assets/Core/Board.cs:8-19`), which
`OutcomesAreTwoAndDistinct` (`BoardTests.cs:284`) also pins. `OutcomeTests` runs
green: `Passed! - Failed: 0, Passed: 7, Skipped: 0, Total: 7`; whole suite
`Passed: 60, Total: 60`.

### 2. The boundary case — fail

The test named for it is `Win_PileCleared_EvenWhenLastTakeFillsShelf`. Its
comment (`BoardTests.cs:246-247`) says "12 items in 4 kinds, 8-slot shelf: the
pile empties while the shelf holds 8 unmatched". The code one line below passes
`shelfCapacity: 12` (`BoardTests.cs:253`), and because matching fires on every
third copy of a kind the shelf never holds more than two items.

Replay of exactly that test body by a probe over the same Core sources (output
quoted in "How to reproduce", step 2):

```
D1 outcome=Win shelf.Occupied at end=0 IsFull=False maxOccupancyDuringPlay=2 capacity=12
```

Two of twelve slots at peak. The shelf is never full, so the test does not
exercise "the last item ... fills the shelf" at all — it exercises an ordinary
win.

Nor does any other test reach that state. The code path that handles a placement
refused by a full shelf is `Board.cs:125-135`:

```csharp
if (!Shelf.TryPlace(entry.Item, out var matchedKind))
{
    // shelf full: the win still wins, otherwise it's a jam
    if (_taken.Count == _entries.Count)
    {
        Finish(GameOutcome.Win);
        return true;
    }
    Finish(GameOutcome.ShelfJammed);
    return true;
}
```

In the cobertura report produced in step 3 below, `Board.cs` lines
126, 128, 129, 130, 131, 133 and 134 all carry `hits="0"`, and line 128 carries
`condition-coverage="0% (0/2)"`. The whole block is dead in the current suite —
including the `Finish(GameOutcome.Win)` at line 130, which is precisely the
"ordering bug cannot recur" the task's OUTCOME claims to protect.

It is dead in a stronger sense than "untested": under the present rules it is
unreachable. `Board.TakeItem` ends the game as a jam as soon as a successful
placement leaves the shelf full with the pile non-empty (`Board.cs:142-146`),
and refuses to act once the game is over (`Board.cs:113-114`), so a take is
never attempted against a full shelf. And a win can never coincide with a full
shelf: `Board`'s constructor requires every kind to appear a multiple of three
times (`Board.cs:61-67`) and `Shelf.TryMatch` removes each triple as it forms
(`Shelf.cs:74-96`), so when the pile empties every kind's shelf count is
`0 mod 3` — zero. Brute force agrees (same probe run):

```
D2 any Win ending with a full shelf in 20000 random games = False
D3 a take ever attempted with an already-full shelf = False
```

40 000 random games over 1–4 kinds and shelf capacities 1–11, seeded
`new Random(12345)`, never produced either state.

So item 2 asks for a test of a state the rules make impossible. It cannot be
satisfied as written. That is a defect in the pair (rules, acceptance criterion),
not a rules bug: the win-before-jam ordering is real and correct at
`Board.cs:142-152`, but the *other* ordering site at `Board.cs:128-131` is
unreachable code that no test can pin. Either the criterion should be reworded
to the reachable case — a final placement that fills the shelf at the instant of
placement and then matches, e.g. capacity 3 with a three-item pile, which no
committed test covers either — or `Board.cs:126-134` should be deleted as
unreachable.

## How to reproduce

From a clean state — fresh clone, nothing exported by hand. Network is needed on
the first run for the NuGet restore; no environment variable has to be set
(`RollForward=Major` is in `build/core-tests/core-tests.csproj:12`).

```bash
git clone <repo-url> /tmp/verify-04 && cd /tmp/verify-04
git rev-parse --short HEAD          # expect 27f9904 for the numbers above

# step 1 — item 1
dotnet test build/core-tests/core-tests.csproj -v q --nologo --filter "FullyQualifiedName~OutcomeTests"

# step 2 — item 2, the probe, built outside the repo so nothing here is modified
mkdir -p /tmp/verify-04-probe && cd /tmp/verify-04-probe
cat > probe.csproj <<'EOF'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework>
    <LangVersion>9</LangVersion><RollForward>Major</RollForward>
    <ImplicitUsings>disable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup><Compile Include="/tmp/verify-04/game/Assets/Core/**/*.cs" /></ItemGroup>
</Project>
EOF
cat > Program.cs <<'EOF'
using System; using System.Collections.Generic; using System.Linq; using CatShelter.Core;
static class Probe {
  static PileEntry E(int id, string kind) =>
      new PileEntry(new Item(id, new ItemKind(kind, kind)), new List<int>());
  static void Main() {
    var entries = new List<PileEntry>();
    for (int k=0;k<4;k++) for (int i=0;i<3;i++) entries.Add(E(k*3+i+1, "kind"+k));
    var board = new Board(new Level(7,"room_1",0,entries.ToArray()), 12);
    int maxOcc = 0;
    foreach (var e in entries) { board.TakeItem(e.Item.Id); maxOcc = Math.Max(maxOcc, board.Shelf.Occupied); }
    Console.WriteLine("D1 outcome=" + board.Outcome + " shelf.Occupied at end=" + board.Shelf.Occupied
      + " IsFull=" + board.Shelf.IsFull + " maxOccupancyDuringPlay=" + maxOcc
      + " capacity=" + board.Shelf.Capacity);

    var rnd = new Random(12345);
    bool everWinFull = false, refusedEver = false;
    for (int trial=0; trial<20000; trial++) {
      int kinds = rnd.Next(1,5); var es = new List<PileEntry>(); int id=1;
      for (int k=0;k<kinds;k++) for (int i=0;i<3;i++) es.Add(E(id++, "k"+k));
      var bd = new Board(new Level(1,"r",0,es), rnd.Next(2,12));
      foreach (var oid in es.Select(e=>e.Item.Id).OrderBy(_=>rnd.Next())) { bd.TakeItem(oid); if (bd.IsOver) break; }
      if (bd.Outcome == GameOutcome.Win && bd.Shelf.IsFull) everWinFull = true;
    }
    Console.WriteLine("D2 any Win ending with a full shelf in 20000 random games = " + everWinFull);
    for (int trial=0; trial<20000; trial++) {
      int kinds = rnd.Next(1,5); var es = new List<PileEntry>(); int id=1;
      for (int k=0;k<kinds;k++) for (int i=0;i<3;i++) es.Add(E(id++, "k"+k));
      var bd = new Board(new Level(1,"r",0,es), rnd.Next(1,12));
      foreach (var oid in es.Select(e=>e.Item.Id).OrderBy(_=>rnd.Next())) {
        bool fullBefore = bd.Shelf.IsFull;
        if (bd.IsOver) break;
        if (bd.TakeItem(oid) && fullBefore) refusedEver = true;
      }
    }
    Console.WriteLine("D3 a take ever attempted with an already-full shelf = " + refusedEver);
  }
}
EOF
dotnet run --project probe.csproj

# step 3 — the per-line hit counts quoted above.
# The repo's own coverage command does not work; see
# tasks/20-rules-core/05-coverage/VERIFY.md for the working recipe and why.
```

## What was not checked

- Whether the two outcomes are distinguishable *to a player*. Only the enum and
  `Board.Outcome` were checked; nothing in `game/Assets/View/` or
  `game/Assets/Shell/` was run, and no build was played.
- The SCOPE line "No OutOfMoves" was checked only as "the enum has two members";
  no search was made for a move counter reintroduced elsewhere in the codebase.
- Agreement between `Board` and the Python rules mirror `tools/solver/rules.py`
  on outcomes. That is `30-levels-solver/01-rules-in-solver`; the Python suite
  was not run here.
- The jam-by-lock path (`Board.cs:158-159`, every remaining item locked) is
  covered by `PartialInformationTests.cs:108` but belongs to the complications
  task, not this one, and was not audited.
- `Board.AddShelfSlots` resuming a jam (`Board.cs:171-180`) — covered by tests
  but outside this task's VERIFY list; the 75% condition coverage on
  `Board.cs:176` was not investigated.
- Whether item 2 should instead be read as "the final placement momentarily
  fills the shelf, then matches, and the result is Win". That state *is*
  reachable — capacity 3 with a three-item pile — but no committed test creates
  it either, so item 2 fails under that reading as well.

---

## Follow-up, 2026-08-26: the boundary case cannot happen at all

The finding above is confirmed, and goes further than "no test covers it".
**The state VERIFY item 2 describes is unreachable**, so no test can be written
for it without changing the rules.

Measured over 3000 random games per engine, shelf capacities 3–12
(`shelf capacity` varied, kinds 2–6):

```
C#:     wins 1731, of them with a non-empty shelf 0, peak occupancy on a win 0
Python: wins 1708, same measurement, after the mirror fix: 0
```

The reason is structural. A win means the pile is empty, so every item has been
taken. Kinds come in triples (enforced by `Level` since this date), and three
copies of a kind on the shelf match immediately, so at most two copies of any
kind can sit there. When the last item is placed it completes the triple its two
siblings are waiting in — and that triple leaves. The shelf is therefore
**empty** at the moment of every win, not full.

"The last item empties the pile and fills the shelf" would require occupancy to
equal capacity at that moment, which is 0 = capacity: impossible for any shelf
of one slot or more.

The dead branch this leaves is `Board.TakeItem`'s `if (!Shelf.TryPlace(...))`
arm, which handles a placement refused by a full shelf. It cannot execute
either, for the same reason.

**Left as `verify:failed` on purpose.** The right fix is to rewrite VERIFY item
2 around a state that exists — the jam side of the same branch is the real
boundary — and rewriting an acceptance criterion is a human's call, not an
agent's (`tasks/README.md`, "criteria are not fitted to the result").
