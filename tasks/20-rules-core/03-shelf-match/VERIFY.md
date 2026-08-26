# VERIFY — 20-rules-core/03-shelf-match

Result: **failed** — item 1 is not fully covered: no unit test matches a triple
across a slot-row boundary, although the test that claims to do so is named for
it. The rule itself is correct when probed; what is missing is the test.

Verifier: an independent agent context, 2026-08-26, against `dev` at commit
`27f9904`. It did **not** write `game/Assets/Core/Shelf.cs` or any other Core
source, did **not** write `game/Assets/Tests/Core/BoardTests.cs`, and changed
none of them during this check. It did not run the Unity editor and did not look
at the shelf on screen. Its only writes were this file and `labels.txt`.

## Verdict per VERIFY item

| # | Item | Result |
|---|---|---|
| 1 | Unit tests: match at a slot boundary, full shelf, match after a slot frees | **fail** — one of the three sub-cases is not tested |
| 2 | A shelf grown by three accepts three more items | pass |

### 1. The three sub-cases — fail on the boundary case

| sub-case | test | line | real? |
|---|---|---|---|
| full shelf | `FullShelf_PlacementRefused` | `BoardTests.cs:203` | yes |
| match after a slot frees | `Place_AfterMatchFreesSlots_ReusesFreeSlot` | `BoardTests.cs:166` | yes |
| match at a slot boundary | `Match_CompletesAcrossRowBoundary` | `BoardTests.cs:180` | **no — it never crosses one** |

`Shelf.SlotsPerRow` is `3` (`game/Assets/Core/Shelf.cs:15`), so row 0 is slots
0–2. `Shelf.TryPlace` always fills the leftmost free slot
(`Shelf.cs:60`, `Array.IndexOf(_slots, null)`). The test places three `m` into an
empty nine-slot shelf, so they land in slots 0, 1, 2 — entirely inside row 0.
Its own comment (`BoardTests.cs:182`) claims "a triple spanning row 0 and row 1
matches"; the code it runs does not span anything.

Measured by a probe over the same Core sources (output quoted in "How to
reproduce", step 2):

```
E1 before third copy: [m,m,.,.,.,.,.,.,.]  (SlotsPerRow=3)
E2 occupied slot indices before the match: 0,1 -> rows 0
E3 matched=m after: [.,.,.,.,.,.,.,.,.]
```

The rule the item is about does hold — the same probe builds a triple at slots
2, 3 and 5, straddling the row-0/row-1 boundary, and it matches:

```
E4 before the third m: [x,y,m,m,z,.,.,.,.]
E5 placed=True matched=m after: [x,y,.,.,z,.,.,.,.]
E6 before: [f0,f1,f2,f3,f4,f5,m,m,.]
E7 matched=m after: [f0,f1,f2,f3,f4,f5,.,.,.]
```

So the implementation is right and the committed test suite does not
demonstrate it. The whole suite is green — `Passed! - Failed: 0, Passed: 60,
Skipped: 0, Total: 60`, `ShelfTests` alone `Passed: 6` — which is exactly why
this needed reading rather than counting. `Shelf.TryMatch` groups over all slots
with no row arithmetic anywhere (`Shelf.cs:74-96`), which is consistent with the
SCOPE line "Matching is NOT per row", but a test that asserts it is the thing
item 1 asks for.

Fix is three lines: place two filler items first, or assert the occupied slot
indices, so the triple really lands across slot 3.

### 2. A shelf grown by three accepts three more items — pass

Probe output (same run, step 2):

```
C1 capacity=9 occupied=9 isFull=True
C2 place on full shelf = False
C3 after AddSlots(3): capacity=12 accepted=3 occupied=12
C4 fourth extra item accepted = False
```

Nine distinct kinds fill the shelf, a tenth is refused, `AddSlots(3)` takes
capacity to 12, exactly three further items are accepted and a fourth is
refused. `Shelf.AddSlots` copies the existing contents into the wider array
(`Shelf.cs:32-39`), so nothing placed is lost.

The committed test for this, `AddSlots_GrowsCapacity_KeepsPlacedItems`
(`BoardTests.cs:216-227`), grows by **one** and accepts **one**, not three; and
`Booster_ResumesAJammedBoard` (`BoardTests.cs:330`) grows by three but takes only
one item afterwards. Item 2 is worded as an observation rather than as "a unit
test exists", so the probe above satisfies it — but note that no committed test
asserts the "+3 accepts 3" case either.

## How to reproduce

From a clean state — fresh clone, nothing exported by hand. Network is needed on
the first run for the NuGet restore; no environment variable has to be set
(`RollForward=Major` is in `build/core-tests/core-tests.csproj:12`).

```bash
git clone <repo-url> /tmp/verify-03 && cd /tmp/verify-03
git rev-parse --short HEAD          # expect 27f9904 for the numbers above

# step 1 — the suite is green, which is not by itself evidence for item 1
dotnet test build/core-tests/core-tests.csproj -v q --nologo --filter "FullyQualifiedName~ShelfTests"
dotnet test build/core-tests/core-tests.csproj -v q --nologo

# step 2 — the probe, built outside the repo so nothing here is modified
mkdir -p /tmp/verify-03-probe && cd /tmp/verify-03-probe
cat > probe.csproj <<'EOF'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework>
    <LangVersion>9</LangVersion><RollForward>Major</RollForward>
    <ImplicitUsings>disable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup><Compile Include="/tmp/verify-03/game/Assets/Core/**/*.cs" /></ItemGroup>
</Project>
EOF
cat > Program.cs <<'EOF'
using System; using System.Linq; using CatShelter.Core;
static class Probe {
  static Item I(string kind, int id) => new Item(id, new ItemKind(kind, kind));
  static string S(Shelf s) => "[" + string.Join(",", s.Slots.Select(x => x is null ? "." : x.Kind.Id)) + "]";
  static void Main() {
    var s = new Shelf();
    s.TryPlace(I("m",1), out _); s.TryPlace(I("m",2), out _);
    Console.WriteLine("E1 before third copy: " + S(s) + "  (SlotsPerRow=" + Shelf.SlotsPerRow + ")");
    var used = s.Slots.Select((x,i)=>(x,i)).Where(p=>p.x!=null).Select(p=>p.i).ToArray();
    Console.WriteLine("E2 occupied slot indices before the match: " + string.Join(",", used)
      + " -> rows " + string.Join(",", used.Select(i => i / Shelf.SlotsPerRow).Distinct()));
    s.TryPlace(I("m",3), out var mk);
    Console.WriteLine("E3 matched=" + (mk?.Id ?? "null") + " after: " + S(s));

    var t = new Shelf();
    t.TryPlace(I("x",1), out _); t.TryPlace(I("y",2), out _);
    t.TryPlace(I("m",3), out _); t.TryPlace(I("m",4), out _); t.TryPlace(I("z",5), out _);
    Console.WriteLine("E4 before the third m: " + S(t));
    bool ok = t.TryPlace(I("m",6), out var mk2);
    Console.WriteLine("E5 placed=" + ok + " matched=" + (mk2?.Id ?? "null") + " after: " + S(t));

    var u = new Shelf();
    for (int i = 0; i < 6; i++) u.TryPlace(I("f"+i, i), out _);
    u.TryPlace(I("m",10), out _); u.TryPlace(I("m",11), out _);
    Console.WriteLine("E6 before: " + S(u));
    u.TryPlace(I("m",12), out var mk3);
    Console.WriteLine("E7 matched=" + (mk3?.Id ?? "null") + " after: " + S(u));

    var sh = new Shelf();
    for (int i=0;i<9;i++) sh.TryPlace(new Item(i, new ItemKind("k"+i,"k"+i)), out _);
    Console.WriteLine("C1 capacity=" + sh.Capacity + " occupied=" + sh.Occupied + " isFull=" + sh.IsFull);
    Console.WriteLine("C2 place on full shelf = " + sh.TryPlace(new Item(99,new ItemKind("z","z")), out _));
    sh.AddSlots(3);
    int accepted = 0;
    for (int i=0;i<3;i++) if (sh.TryPlace(new Item(100+i,new ItemKind("n"+i,"n"+i)), out _)) accepted++;
    Console.WriteLine("C3 after AddSlots(3): capacity=" + sh.Capacity + " accepted=" + accepted + " occupied=" + sh.Occupied);
    Console.WriteLine("C4 fourth extra item accepted = " + sh.TryPlace(new Item(200,new ItemKind("q","q")), out _));
  }
}
EOF
dotnet run --project probe.csproj
```

## What was not checked

- The SCOPE line "AddSlots must not be called from the MVP lose screen". Nothing
  under `game/Assets/Shell/` or `game/Assets/View/` was inspected for calls to
  `Shelf.AddSlots` or `Board.AddShelfSlots`; that belongs to
  `60-shell-build/07-lose-screen-fake-door`, whose own notes claim it, and this
  verifier did not confirm it.
- Matching more than one triple in a single call. `Shelf.TryMatch` returns after
  the first triple (`Shelf.cs:91`) and its docstring argues one placement can
  complete at most one triple; that argument was not tested against a shelf
  seeded with six of a kind by some other route.
- Tie-breaking when two kinds are both at three copies. `TryMatch` takes
  whichever group `GroupBy` yields first; no requirement states what should
  happen and no test pins it.
- `Shelf.AddSlots(0)` and negative arguments. `Shelf.cs:35` throws for negative;
  that line has `hits="0"` in the coverage report produced for `05-coverage`.
- Capacity growth beyond one call, and whether capacity is persisted. Save and
  resume are `60-shell-build`, not this task.
- Whether "match at a slot boundary" was meant as "across a row boundary" is a
  reading. This verifier took it from the test's own name and comment and from
  the SCOPE line "Matching is NOT per row". Under a looser reading — any triple
  anywhere — item 1 would pass. Either way the named test does not do what its
  name and comment say.

---

## What changed after this verification, 2026-08-26

The finding was accurate: `Match_CompletesAcrossRowBoundary` placed three
copies into an empty shelf, so they landed in slots 0–2 — inside row 0 — while
its comment claimed a triple spanning rows 0 and 1. The rule itself held; the
test for it did not exist.

- That test is renamed `Match_CompletesWithinOneRow`, which is what it actually
  does and is worth keeping.
- A new `Match_CompletesAcrossRowBoundary` fills the slots between the copies
  so they genuinely straddle the boundary: it asserts the two `a` copies sit at
  slots 0 and 3 with `SlotsPerRow == 3` **before** placing the third, then that
  all three leave the shelf together.

84 tests pass. `verify` left **pending**: the context that wrote this test
cannot sign it off.
