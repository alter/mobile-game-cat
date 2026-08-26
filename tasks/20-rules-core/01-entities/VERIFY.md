# VERIFY — 20-rules-core/01-entities

Result: **failed** — item 3 does not hold.

Verifier: an independent agent context, 2026-08-26, against `dev` at commit
`27f9904`. It did **not** write `game/Assets/Core/*`, did **not** write
`game/Assets/Tests/Core/*`, did **not** write `build/check-core-purity.sh`, and
changed none of them during this check. It did not run the Unity editor and did
not build the player. Its only writes were this file and `labels.txt`.

## Verdict per VERIFY item

| # | Item | Result |
|---|---|---|
| 1 | Project builds | pass |
| 2 | `bash build/check-core-purity.sh` passes | pass |
| 3 | A level with a kind appearing four times is rejected at construction | **fail** |

### 1. Project builds — pass

`dotnet build build/core-tests/core-tests.csproj` in a fresh clone with a
scrubbed environment printed `Build succeeded.` / `0 Warning(s)`. That project
compiles `game/Assets/Core/**/*.cs` (`core-tests.csproj:17`), so the Core
sources compile. `LangVersion` is `9` (`core-tests.csproj:9`).

### 2. Core purity — pass

`bash build/check-core-purity.sh` printed `Core is engine-free: OK`, exit code
`0`. Structurally this is also pinned by
`game/Assets/Core/CatShelter.Core.asmdef`, which carries
`"noEngineReferences": true` and an empty `"references"` list.

### 3. Rejection at construction — fail

`Level`'s constructor validates only `number >= 1`, `pileIndex >= 0` and the two
null arguments (`game/Assets/Core/Level.cs:26-33`). It does not look at the pile
contents at all. The kind-count rule lives in `Board`
(`game/Assets/Core/Board.cs:61-67`).

Measured with a probe compiled against the same Core sources (source of every
line below: the probe output quoted in "How to reproduce", step 4):

```
A1 new Level(kind x4): NO THROW
A2 new Board(kind x4): THREW ArgumentException: kind 'a' appears 4 times, not a multiple of three (Parameter 'level')
A3 new Level(dup ids): NO THROW
A4 new Board(dup ids): THREW ArgumentException: An item with the same key has already been added. Key: 1
```

So a `Level` carrying a kind four times is a valid, constructible object. The
task's SCOPE line — "Level rejects duplicate item ids and kind counts not
divisible by three" — is not implemented in `Level`. The check exists one type
later, in `Board`.

This is not only a wording quibble. `game/Assets/View/LevelAssets.cs:62` builds
a `Level` straight out of shipped JSON with no validation of pile contents, so a
malformed level file survives loading and only fails when a `Board` is
constructed over it.

Secondary finding, not itself a VERIFY item: the explicit duplicate-id check at
`Board.cs:71-72` is unreachable. `ToDictionary` at `Board.cs:68` throws first,
which is why probe line A4 reports the framework message rather than
"Duplicate item ids in the pile". Coverage confirms it — `Board.cs:72` has
`hits="0"` in the cobertura report produced in step 5 below. The existing test
`BoardTests.DuplicateItemIds_ConstructorThrows`
(`game/Assets/Tests/Core/BoardTests.cs:115-120`) asserts only
`Assert.Throws<ArgumentException>`, so it passes on the wrong exception.

## How to reproduce

From a clean state — fresh clone, nothing exported by hand. Requires network on
first run for the NuGet restore. `RollForward=Major` is set in the csproj
(`core-tests.csproj:12`), so no `DOTNET_ROLL_FORWARD` is needed.

```bash
git clone <repo-url> /tmp/verify-01 && cd /tmp/verify-01
git rev-parse --short HEAD          # expect 27f9904 for the numbers above

# item 1
dotnet build build/core-tests/core-tests.csproj -v q --nologo

# item 2
bash build/check-core-purity.sh; echo "exit=$?"

# item 3 — the probe. Written outside the repo so nothing here is modified.
mkdir -p /tmp/verify-01-probe && cd /tmp/verify-01-probe
cat > probe.csproj <<'EOF'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework>
    <LangVersion>9</LangVersion><RollForward>Major</RollForward>
    <ImplicitUsings>disable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="/tmp/verify-01/game/Assets/Core/**/*.cs" />
  </ItemGroup>
</Project>
EOF
cat > Program.cs <<'EOF'
using System; using System.Linq; using System.Collections.Generic; using CatShelter.Core;
static class Probe {
  static PileEntry E(int id, string kind) =>
      new PileEntry(new Item(id, new ItemKind(kind, kind)), new List<int>());
  static void Main() {
    var pile4 = new[] { E(1,"a"), E(2,"a"), E(3,"a"), E(4,"a") };
    try { new Level(1,"room_1",0,pile4); Console.WriteLine("A1 new Level(kind x4): NO THROW"); }
    catch (Exception ex) { Console.WriteLine("A1 new Level(kind x4): THREW " + ex.GetType().Name); }
    try { new Board(new Level(1,"room_1",0,pile4)); Console.WriteLine("A2 new Board(kind x4): NO THROW"); }
    catch (Exception ex) { Console.WriteLine("A2 new Board(kind x4): THREW " + ex.GetType().Name + ": " + ex.Message.Split('\n')[0]); }
    var dup = new[] { E(1,"a"), E(1,"b"), E(2,"a"), E(3,"a"), E(4,"b"), E(5,"b") };
    try { new Level(1,"room_1",0,dup); Console.WriteLine("A3 new Level(dup ids): NO THROW"); }
    catch (Exception ex) { Console.WriteLine("A3 new Level(dup ids): THREW " + ex.GetType().Name); }
    try { new Board(new Level(1,"room_1",0,dup)); Console.WriteLine("A4 new Board(dup ids): NO THROW"); }
    catch (Exception ex) { Console.WriteLine("A4 new Board(dup ids): THREW " + ex.GetType().Name + ": " + ex.Message.Split('\n')[0]); }
  }
}
EOF
dotnet run --project probe.csproj
```

Step 5, the coverage report used for the `Board.cs:72` claim, is reproduced in
`tasks/20-rules-core/05-coverage/VERIFY.md`.

## What was not checked

- The OUTCOME line "Types compile as netstandard2.1". Only a `net8.0` build was
  run (`core-tests.csproj:6`). `game/ProjectSettings/ProjectSettings.asset:873`
  reads `apiCompatibilityLevel: 6`; the mapping of that number to
  .NET Standard 2.1 was **not verified**, and no netstandard2.1 compile was
  performed.
- Compilation inside the Unity editor. `game/Library/ScriptAssemblies/
  CatShelter.Core.dll` exists on this machine, but `Library/` is not in a fresh
  checkout, so it is not clean-state evidence and is not relied on above.
- `Room` and `Board` as *entity* shapes against `cat-shelter-mvp.md` section 8.
  Only the two rules named in the VERIFY list were checked; no field-by-field
  comparison with the specification was done. There is no `Room` type under
  `game/Assets/Core/`.
- The SCOPE line "No moves_limit field" was not audited beyond noting that no
  such field appears in `Level.cs`.
- Whether item 3 should be read as "rejected by `Level`" or "rejected somewhere
  before play begins" is a judgement. This verifier read it against the SCOPE
  line that assigns the rule to `Level`, and marked it failed. Under the looser
  reading — a bad level can never reach play, since `Board` is its only consumer
  in `game/Assets/Core/` — the item would pass. The fix is small either way:
  move the two checks from `Board`'s constructor into `Level`'s, or amend SCOPE
  and item 3 to name `Board`.

---

## What changed after this verification, 2026-08-26

Item 3 failed because a level with a kind appearing four times was accepted by
`Level` and only rejected later, when a `Board` was built from it. Confirmed
independently by probe: `Level` constructed without complaint, `Board` threw
"kind 'a' appears 4 times, not a multiple of three".

The invariant moved into `Level` itself, along with the duplicate-id check that
had also lived in `Board`. An unwinnable level can no longer be constructed at
all, so it can no longer be written to a save file, generated into an asset, or
passed around until something tries to play it. `Board` no longer duplicates
either check — it cannot receive a pile that breaks them.

`game/Assets/Tests/Core/LevelTests.cs` (new, 9 cases) pins it: counts of 1, 2,
4 and 5 rejected, triples accepted, the empty pile accepted, duplicate ids
rejected, and rejection proven to happen before any `Board` exists.

83 tests pass. `verify` is left **pending** rather than passed: the context that
made this change cannot sign it off.
