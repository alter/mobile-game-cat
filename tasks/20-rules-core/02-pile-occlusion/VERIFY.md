# VERIFY — 20-rules-core/02-pile-occlusion

Result: **passed** — both VERIFY items hold.

Verifier: an independent agent context, 2026-08-26, against `dev` at commit
`27f9904`. It did **not** write `game/Assets/Core/Board.cs` or any other Core
source, did **not** write `game/Assets/Tests/Core/BoardTests.cs` or
`PartialInformationTests.cs`, and changed none of them during this check. It did
not run the Unity editor and did not look at any on-screen presentation of a
hidden item — that is out of scope by the task's own SCOPE line. Its only writes
were this file and `labels.txt`.

## Verdict per VERIFY item

| # | Item | Result |
|---|---|---|
| 1 | Unit tests: empty pile, single layer, three layers, circular block | pass |
| 2 | A covered item reports `IsRevealed` false; uncovering flips it | pass |

### 1. The four named tests exist and are green — pass

All four are in `game/Assets/Tests/Core/BoardTests.cs` and each is a real
assertion over `Board.GetAvailable()`, not a placeholder:

| case | test | line |
|---|---|---|
| empty pile | `EmptyPile_NoAvailableItems` | `BoardTests.cs:34` |
| single layer | `SingleLayer_AllItemsAvailable` | `BoardTests.cs:41` |
| three layers | `ThreeLayers_OnlyTopOfEachStackAvailable` | `BoardTests.cs:50` |
| circular block | `CircularBlock_NothingInCycleAvailable` | `BoardTests.cs:67` |

Read, not just counted: the three-layer case builds `1←2←3` plus `4←5` and
asserts both that ids 3 and 4 are available and that the buried 1 and 2 are not
(`BoardTests.cs:59-63`). The circular case makes 1 and 2 block each other and
asserts neither is available while the unrelated 3 is (`BoardTests.cs:73-76`).

Running the fixtures that contain them, from a fresh clone in a scrubbed
environment:

```
Passed!  - Failed: 0, Passed: 19, Skipped: 0, Total: 19 - core-tests.dll (net8.0)
```

(filter `FullyQualifiedName~BoardTests|FullyQualifiedName~PartialInformationTests`;
the whole suite is `Passed! - Failed: 0, Passed: 60, Skipped: 0, Total: 60`.)

### 2. Reveal flips when the cover is taken — pass

`Board.IsRevealed` derives visibility from the same `BlockedBy` data as
reachability — one expression, no second state field
(`game/Assets/Core/Board.cs:97-103` against `GetAvailable` at
`Board.cs:80-88`), which is what the task's OUTCOME asks for.

Behaviour measured directly by a probe over the same Core sources (output quoted
in "How to reproduce", step 2):

```
B1 IsRevealed(covered item 1) before = False
B2 IsRevealed(item 1) after taking its cover = True
```

Committed tests assert the same two directions:
`BuriedItem_KindHidden_UntilReachable`
(`game/Assets/Tests/Core/PartialInformationTests.cs:38`) and
`TakingCover_RevealsBuriedItem` (`PartialInformationTests.cs:48`).

## How to reproduce

From a clean state — fresh clone, nothing exported by hand. Network is needed on
the first run for the NuGet restore; no environment variable has to be set
(`RollForward=Major` is in `build/core-tests/core-tests.csproj:12`).

```bash
git clone <repo-url> /tmp/verify-02 && cd /tmp/verify-02
git rev-parse --short HEAD          # expect 27f9904 for the numbers above

# step 1 — the four named tests, and the whole suite
dotnet test build/core-tests/core-tests.csproj -v q --nologo \
  --filter "FullyQualifiedName~BoardTests|FullyQualifiedName~PartialInformationTests"
dotnet test build/core-tests/core-tests.csproj -v q --nologo

# step 2 — the reveal probe, built outside the repo so nothing here is modified
mkdir -p /tmp/verify-02-probe && cd /tmp/verify-02-probe
cat > probe.csproj <<'EOF'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework>
    <LangVersion>9</LangVersion><RollForward>Major</RollForward>
    <ImplicitUsings>disable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup><Compile Include="/tmp/verify-02/game/Assets/Core/**/*.cs" /></ItemGroup>
</Project>
EOF
cat > Program.cs <<'EOF'
using System; using System.Linq; using System.Collections.Generic; using CatShelter.Core;
static class Probe {
  static PileEntry E(int id, string kind, params int[] b) =>
      new PileEntry(new Item(id, new ItemKind(kind, kind)), b.ToList());
  static void Main() {
    var lv = new Level(1,"room_1",0,new[]{ E(1,"a",2), E(2,"a"), E(3,"a") });
    var b = new Board(lv);
    var it1 = lv.Pile.First(e => e.Item.Id == 1).Item;
    Console.WriteLine("B1 IsRevealed(covered item 1) before = " + b.IsRevealed(it1));
    b.TakeItem(2);
    Console.WriteLine("B2 IsRevealed(item 1) after taking its cover = " + b.IsRevealed(it1));
  }
}
EOF
dotnet run --project probe.csproj
```

## What was not checked

- The visual treatment of a hidden item. Excluded by the task's own SCOPE
  (`− Not the visual treatment of a hidden item`); nothing in
  `game/Assets/View/` was exercised, and no build was run on a device.
- `IsRevealed` for an item that has already been taken. `Board.cs:100` returns
  `false` there and that line has `hits="0"` in the coverage report produced for
  `05-coverage` — no test covers it, and the correct answer for a taken item is
  not stated anywhere in the task.
- `Board.IsRevealed` with an `Item` that is not in this level's pile. `Board.cs:101`
  indexes `_entries` directly and would throw `KeyNotFoundException`; this was
  not exercised and no requirement covers it.
- `Board.IsTaken` (`Board.cs:91`) is uncovered by the suite (`hits="0"`) and was
  not exercised here. It is not named in this task's VERIFY list.
- The circular-block case was checked only for availability. What the game does
  when the *only* remaining items form a cycle — whether that ends as a jam or
  as a hang — was not traced through `Board.TakeItem` for a cycle specifically;
  the corresponding test for locked items exists
  (`PartialInformationTests.cs:108`) but no test covers a pure `blocked_by`
  cycle as the terminal state.
- Hidden kinds were checked through `IsRevealed` only. Whether any consumer
  actually respects it — that the kind is not leaked to the player some other
  way — was not checked; that lives in the view.
