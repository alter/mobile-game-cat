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

---

# Re-verification, 2026-08-27 — the rewritten criteria

Result: **passed** — VERIFY items 1, 2 and 3 as they now stand in `task.txt`
are all satisfied by committed tests, and the tests reach the states their
names claim.

Verifier: a second independent agent context, 2026-08-27, against `dev` at
commit `2bdc8e9`. It did **not** write `game/Assets/Core/Board.cs`,
`game/Assets/Core/Shelf.cs`, `game/Assets/Core/Level.cs` or any other Core
source; did **not** write `game/Assets/Tests/Core/BoardTests.cs`; did **not**
reword `task.txt`; and modified none of those files during this check — the
mutation experiment below was run on a **copy** outside the repository. It did
not write the earlier verdict in this file either. It did not run the Unity
editor, did not build for a device and did not play the game. Its only writes
were this section and `labels.txt`.

## Verdict per VERIFY item

| # | Item (current wording) | Test | Result |
|---|---|---|---|
| 1 | One unit test per outcome | `Win_PileCleared` (`BoardTests.cs:275`), `ShelfJammed_UnmatchedKindsFillTheShelf` (`BoardTests.cs:354`) | pass |
| 2 | Final placement takes the shelf's LAST free slot and completes a triple on the way in → Win, not a jam | `FinalPlacementTakesTheLastSlotAndMatches_IsAWin_NotAJam` (`BoardTests.cs:309`) | pass |
| 3 | A test pinning that at a win the shelf is empty | `AtAWin_TheShelfIsEmpty_WhichIsWhyTheOutcomesCannotCompete` (`BoardTests.cs:334`) | pass, with a caveat below |

### 1 — pass

`Win_PileCleared` has been renamed and now claims only what it does. Its peak
occupancy is 2 of 12 slots (measured, step 2 of the probe below); the old name
`..._EvenWhenLastTakeFillsShelf`, which the previous verdict caught as a false
claim, is gone and the comment at `BoardTests.cs:278-281` records why.

`ShelfJammed_UnmatchedKindsFillTheShelf` was traced take by take, not merely
run. It reaches a genuinely full, genuinely unmatchable shelf:

```
J take#8 id=8 occ=8/9 over=False outcome=
J take#9 id=11 occ=9/9 over=True outcome=ShelfJammed
```

Five kinds hold two copies each on nine slots — no triple exists — and the jam
fires at `Board.cs:150-153`. The inline comment "(15 slots needed) jams at slot
ten" is loose prose: it jams on take **nine**, when the ninth item fills the
ninth slot. The comment is wrong, the assertion is right; not grounds to fail.

### 2 — pass, and the test reaches the state its name claims

Traced against the real `Board`, showing shelf occupancy *before* each take
(`lastFreeSlotTaken` means occupancy was capacity − 1, so the placement took
the only remaining slot):

```
P1 take id=1 occBefore=0/3 lastFreeSlotTaken=False occAfter=1 over=False outcome=
P1 take id=2 occBefore=1/3 lastFreeSlotTaken=False occAfter=2 over=False outcome=
P1 take id=3 occBefore=2/3 lastFreeSlotTaken=True  occAfter=0 over=False outcome=
P1 take id=4 occBefore=0/3 lastFreeSlotTaken=False occAfter=1 over=False outcome=
P1 take id=5 occBefore=1/3 lastFreeSlotTaken=False occAfter=2 over=False outcome=
P1 take id=6 occBefore=2/3 lastFreeSlotTaken=True  occAfter=0 over=True  outcome=Win
```

The final take (id 6) goes into slot index 2 of a three-slot shelf — the last
free slot, the shelf is momentarily full inside `Shelf.TryPlace`
(`Shelf.cs:62-68`) — `TryMatch` then removes the triple, and the pile-empty
check at `Board.cs:156-159` returns `Win`. That is exactly the state item 2
names. Unlike the test the previous verdict struck down, this one does not pass
for a different reason than its name suggests.

**The criterion is falsifiable, not fitted to the code.** Tested by mutation on
a copy of the Core sources in a scratch directory (the repository was not
touched): `Board.TakeItem` was changed to judge fullness at the moment of
placement instead of after the match —

```csharp
bool fullOnPlacement = Shelf.Occupied + 1 == Shelf.Capacity;   // added
...
if (fullOnPlacement && _taken.Count != _entries.Count)         // was: Shelf.IsFull && ...
```

and the same scenario then produces:

```
M1 (mutated Board, boundary test scenario) outcome=ShelfJammed over=True
M2 (mutated Board, item-3 test scenario)  outcome=Win occ=0
```

So a plausible reordering of the win/jam decision turns this scenario into a
jam on a winning move and the test fails. A criterion fitted to whatever the
code happens to do could not be broken by a mutation of that code; this one is.

### 3 — pass, with a recorded caveat

`AtAWin_TheShelfIsEmpty_WhichIsWhyTheOutcomesCannotCompete` reaches what it
says: nine items in three kinds on a four-slot shelf end in `Win` with an empty
shelf.

```
P2 outcome=Win occAtEnd=0 peakBetweenTakes=2 cap=4
```

The literal demand of item 3 — a test pinning that at a win the shelf is empty
— is met, and the reasoning behind it holds independently: `Level` rejects any
pile whose kind count is not a multiple of three (`Level.cs:49-52`) and
`Shelf.TryMatch` removes each triple as it forms (`Shelf.cs:85-107`), so every
kind's shelf count is 0 mod 3 — zero — when the pile empties.

Caveat, recorded rather than waived: item 3's second sentence promises that
"if a future rule lets a pile hold a kind in non-triples, this test fails". It
would not. The test builds its own pile in triples, so loosening `Level`'s
validation leaves it green. As a tripwire the test is weaker than the criterion
claims; the invariant is pinned for one fixed pile, not as a property. A
property-style check over random piles (the shape of the probe in the previous
verdict) would deliver what the sentence promises. This does not sink item 3,
whose requirement is the assertion itself, but the sentence overstates the
test's reach and should not be quoted as if the tripwire existed.

## Is the rewritten item 2 a legitimate correction?

**Legitimate correction, not a criterion fitted to the result.** Three reasons,
in order of weight:

1. The old wording named a state the rules make impossible — proven above in
   this file by structure and by 40 000 random games, and independently
   re-derived here from `Level.cs:49-52` and `Shelf.cs:85-107`. A criterion that
   no correct implementation can satisfy is a defect in the criterion. Removing
   it is not lowering a bar; it is deleting a bar that stood in mid-air.
2. The replacement is falsifiable by the mutation shown above. `GOAL.md`'s
   anti-pattern is "fitting thresholds to the result already obtained" — a
   criterion that cannot fail. This one fails against a one-line reordering of
   `Board.TakeItem`, which is the very bug the task's OUTCOME says must not
   recur.
3. The rewrite is declared, dated and reasoned in `task.txt:36-39`, the old
   verdict was left standing in this file, and `Board.cs:122-135` now states
   plainly that the branch it kept is unreachable and why it is kept anyway
   (the item is already in `_taken`, so falling through would lose it). That is
   `GOAL.md`'s "say so; do not leave it in the code", honoured.

The one thing the rewrite does concede: the ordering site at `Board.cs:136-142`
remains dead code that no test can reach, and the OUTCOME line "the ordering bug
cannot recur" is now carried by `Board.cs:150-159` alone. `Board.cs` says so
itself. That is a documented boundary, not a hidden one.

## How to reproduce

From a clean state — fresh clone, nothing exported by hand. Network is needed
on the first run for the NuGet restore.

```bash
git clone <repo-url> /tmp/verify-04b && cd /tmp/verify-04b
git rev-parse --short HEAD          # expect 2bdc8e9 for the numbers below

dotnet test build/core-tests/core-tests.csproj -v q --nologo \
  --filter "FullyQualifiedName~OutcomeTests"
dotnet test build/core-tests/core-tests.csproj -v q --nologo
```

Raw output of both, this machine, 2026-08-27 (the filtered run repeated with
`DOTNET_CLI_UI_LANGUAGE=en` for a legible line; the localised run above it is
the same result):

```
Тестовый запуск для /Users/rdolgov/workflow/git/mobile-game-cat/build/core-tests/bin/Debug/net8.0/core-tests.dll (.NETCoreApp,Version=v8.0)
Общее количество тестовых файлов (1), соответствующих указанному шаблону.

Пройден!   : не пройдено     0, пройдено     9, пропущено     0, всего     9, длительность 9 ms. - core-tests.dll (net8.0)
```

```
Test run for /Users/rdolgov/workflow/git/mobile-game-cat/build/core-tests/bin/Debug/net8.0/core-tests.dll (.NETCoreApp,Version=v8.0)
A total of 1 test files matched the specified pattern.

Passed!  - Failed:     0, Passed:     9, Skipped:     0, Total:     9, Duration: 9 ms - core-tests.dll (net8.0)
```

Full suite, no filter:

```
Тестовый запуск для /Users/rdolgov/workflow/git/mobile-game-cat/build/core-tests/bin/Debug/net8.0/core-tests.dll (.NETCoreApp,Version=v8.0)
Общее количество тестовых файлов (1), соответствующих указанному шаблону.

Пройден!   : не пройдено     0, пройдено   123, пропущено     0, всего   123, длительность 257 ms. - core-tests.dll (net8.0)
```

(9 in `OutcomeTests`, 123 in the whole Core suite. The previous verdict recorded
7 and 60 at commit `27f9904`; the growth is the two tests added for items 2 and
3 plus other tasks' tests committed since.)

The `P*`, `J` and `M*` lines quoted above come from two small probe programs
compiled outside the repository — one including
`game/Assets/Core/**/*.cs` directly, one including a **copy** of those sources
with the single mutation shown in section 2. Both were built in a scratch
directory; no file in this repository was modified by them. The probe pattern,
including the `.csproj`, is the one written down in the previous verdict's
"How to reproduce", step 2.

## What was not checked

- The two other VERIFY sections of this task's SCOPE — "No OutOfMoves" was
  confirmed only as "the enum still has exactly two members"
  (`Board.cs:8-19`, pinned by `OutcomesAreTwoAndDistinct`); no search was made
  for a move counter reintroduced elsewhere.
- Whether the outcomes are distinguishable **to a player**. Nothing under
  `game/Assets/View/` or `game/Assets/Shell/` was read or run, no build was
  played.
- Agreement with the Python rules mirror `tools/solver/rules.py`. The Python
  suite was not run here; that is `30-levels-solver/01-rules-in-solver`.
- Coverage. No cobertura report was produced this time, so the claim that
  `Board.cs:136-142` is still unhit rests on the reachability argument and on
  the previous verdict's report, not on a fresh measurement.
- The other 114 tests in the suite were run but not read. Only the
  `OutcomeTests` fixture was audited line by line.
- Mutation testing was applied to exactly one mutation, aimed at item 2. The
  item-1 and item-3 tests were not mutation-tested; `M2` above shows the item-3
  test is insensitive to that particular mutation, which is expected — it pins a
  different invariant — but no mutation was found that does break it.
