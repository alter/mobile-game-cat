# VERIFY — 20-rules-core/01-entities

Result, as of the 2026-08-27 re-verification at the bottom of this file:
**passed**. Item 3 (below) records the original failure and the fix that
followed it; superseded, kept for the record.

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

---

## Re-verification, 2026-08-27

Verifier: independent QA context. Wrote none of `game/Assets/Core/*`,
`game/Assets/Tests/Core/*`, `build/check-core-purity.sh`, this task's own
`task.txt`, or any prior content in this file. Ran every command itself
against the current tree rather than trusting the "what changed" note
above; re-ran the full `dotnet test` twice after one run showed a single
failure, per the brief for this pass (another agent is actively editing
`Level.cs`/the solver bridge). Did **not** run the Unity editor, a Unity
build, adb, or an emulator. Its only writes are to this file.

**Note on the one inconsistent run.** The first `dotnet test` invocation in
this pass reported `не пройдено 1, пройдено 155, всего 156` with no
identifiable failing test name in the output. Re-run twice immediately
after: `не пройдено 0, пройдено 156, всего 156` both times, stable. Read as
a transient collision with the other agent's concurrent edit (a save
mid-write, most likely), not a real regression — consistent with the
warning given for this pass. Not investigated further per that same
warning; flagged here rather than silently discarded.

### Verdict per VERIFY item (task.txt)

| # | Item | Result | Evidence |
|---|---|---|---|
| 1 | Project builds | **pass** | `dotnet build build/core-tests/core-tests.csproj -v q --nologo` → `Build succeeded`, 0 errors (via the full `dotnet test` runs below, which build first). |
| 2 | `bash build/check-core-purity.sh` passes | **pass** | `Core is engine-free: OK`, exit 0, against the live tree. Also mutation-tested — see below. |
| 3 | A level with a kind appearing four times is rejected at construction | **pass — fixed since the last verification** | Read `game/Assets/Core/Level.cs:41-53` directly: the constructor now counts kinds per pile and throws `ArgumentException` when a count is not divisible by three, and separately rejects duplicate item ids (`Level.cs:55-61`), both **before** `Number`/`RoomId`/`Pile` are even fully assigned in the old sense — the check runs inside the constructor body, so no `Level` instance escapes it. `game/Assets/Tests/Core/LevelTests.cs` carries 13 `[Test]`/`[TestCase]` attributes exercising this (counts of 1/2/4/5 rejected, triples accepted, duplicate ids rejected, rejection proven before any `Board` exists) and passes in the 156-test run below. |

### The purity gate — read, then mutation-tested outside the repo

`build/check-core-purity.sh` resolves its target from its own location
(`ROOT="$(cd "$(dirname "$0")/.." && pwd)"`, `TARGET="$ROOT/game/Assets/Core"`),
not from a hardcoded path — so copying the script alongside a copied `game/`
tree makes it check the copy, not the real repository. Confirmed this is
not "the shape of a check that passes because it looks in the wrong place"
by doing exactly that:

```
$ SP=$(mktemp -d)
$ mkdir -p "$SP/game/Assets/Core" "$SP/build"
$ cp game/Assets/Core/Item.cs "$SP/game/Assets/Core/Item.cs"
$ cp build/check-core-purity.sh "$SP/build/check-core-purity.sh"
$ python3 -c "p='$SP/game/Assets/Core/Item.cs'; open(p,'w').write('using UnityEngine;\n'+open(p).read())"
$ bash "$SP/build/check-core-purity.sh"; echo "exit=$?"
ARCH VIOLATION — engine references under Assets/Core:
/tmp/.../game/Assets/Core/Item.cs:1:using UnityEngine;
exit=1
```

The script found the injected line, printed the exact offending path and
line number, and exited non-zero — against the copy, confirmed by the
printed path pointing at the sandbox, not the real repository (`git status`
in the real repo showed no changes to `Item.cs` throughout). The check is
real, not decorative. It is also backed structurally by
`CatShelter.Core.asmdef`'s `"noEngineReferences": true` with an empty
`"references"` array, which Unity itself enforces at the asmdef level
independent of the grep script — two independent mechanisms, not one.

**One gap the script itself does not close**, worth naming rather than
leaving implicit: it greps only for the literal strings `using UnityEngine`
and `UnityEngine.` — an engine reference spelled through a different route
(a type alias, `global using`, or a fully-qualified `global::UnityEngine.X`
without the literal substring `UnityEngine.` immediately preceding the
member, or a reference to a different assembly Unity ships, like
`UnityEditor` or `com.unity.*` packages under a different root namespace)
would not be caught by this script — though `noEngineReferences` on the
asmdef is the backstop for the packages case, since it forbids all
references outright, not just `UnityEngine`-named ones. No such alias
exists in the tree today (`grep -rn "UnityEditor\|global::" game/Assets/Core`
→ no matches), so this is a theoretical gap, not a live one.

### `.NET Standard 2.1` and `LangVersion` — checked against both toolchains directly, not inferred

**LangVersion.** `build/core-tests/core-tests.csproj:9` sets
`<LangVersion>9</LangVersion>`. `game/ProjectSettings/ProjectVersion.txt`
pins the editor to `6000.3.22f1` (Unity 6.3 LTS, matching `DECISIONS.md`
D13). Unity's own manual for that exact version —
`https://docs.unity3d.com/6000.3/Documentation/Manual/csharp-compiler.html`,
fetched 2026-08-27 — states plainly: *"C# compiler: Roslyn; C# language
version: C# 9.0."* The two agree, sourced from Unity's own documentation
for the pinned version, not inferred across versions.

**`.NET Standard 2.1`.** `game/ProjectSettings/ProjectSettings.asset:875`
reads `apiCompatibilityLevel: 6`. Unity's own enum source
(`Editor/Mono/PlayerSettings.bindings.cs`, github.com/Unity-Technologies/UnityCsReference,
fetched 2026-08-27) defines `NET_Standard_2_0 = 6` with `NET_Standard` as an
alias for the same value (`NET_Standard = NET_Standard_2_0`); Unity's own
scripting API page for `ApiCompatibilityLevel`
(docs.unity3d.com/ScriptReference/ApiCompatibilityLevel.html, fetched
2026-08-27) describes that value's meaning directly: *"NET_Standard;
Profile that targets .NET Standard 2.1."* So the project setting, despite
being internally numbered/named `NET_Standard_2_0`, targets .NET Standard
2.1 by Unity's own description — a legacy-naming quirk, not a discrepancy.

**Whether `core-tests.csproj`'s `net8.0` build is sufficient evidence of
netstandard2.1 compatibility — checked directly, not just cross-referenced.**
`net8.0` is a superset of the netstandard2.1 API surface, so a green
`core-tests.csproj` build alone does not prove the code avoids every
net8.0-only API. Rather than leave that as an open gap, compiled
`game/Assets/Core/**/*.cs` directly against `netstandard2.1` in a throwaway
project outside the repository:

```
$ SP=$(mktemp -d)/netstd-check && mkdir -p "$SP"
$ cat > "$SP/netstd-check.csproj" <<'EOF'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.1</TargetFramework>
    <LangVersion>9</LangVersion>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="/…/game/Assets/Core/**/*.cs" />
  </ItemGroup>
</Project>
EOF
$ cd "$SP" && dotnet build netstd-check.csproj -v q --nologo
...
    Предупреждений: 35
    Ошибок: 0
```

Zero errors, 35 nullable-reference warnings (the same class the `net8.0`
build already emits — none new). **The `netstandard2.1` claim in OUTCOME is
directly confirmed**, not merely cross-referenced from documentation — this
supersedes the previous verification's "not verified" note on this point.

### Item 4 — what "entity" means here, and whether it held

The original SCOPE names five types: **Item, Shelf, Level, Room, Board**
(plus the two construction rules, covered above). `game/Assets/Core/` today
holds those files plus ten more:
`Analytics.cs, BoardSave.cs, Cat.cs, CatSave.cs, CatTraits.cs, GameSave.cs,
PhotoOutcome.cs, PlayerProgress.cs, RoomPlan.cs, SaveResume.cs, TraitsRequest.cs`
(eleven counting `ItemKind`/`PileEntry`/`GameOutcome`, which are companion
types inside `Item.cs`/`Level.cs`/`Board.cs`, not separate files).

**No `Room` type exists.** SCOPE names it explicitly; it was never built.
`Level.RoomId` (a plain `string`) and `RoomPlan` (task `60-shell-build/02`,
tracking which pile of which room and how far through it the player is)
cover parts of what a `Room` entity would have, but neither is a `Room`.
This is the one item in the original SCOPE that was simply never delivered
— not drift into extra types, the opposite: a named type that is still
missing eleven tasks later, papered over by adjacent types built for other
purposes.

**Every one of the ten additions carries an explicit task citation in its
own doc comment** (`Analytics.cs` → `70-analytics/02`; `CatTraits.cs` →
`cat-shelter-tech.md` §3; `PhotoOutcome.cs`/`PhotoJudge` → `50-photo/06`,
with the comment arguing directly for Core: *"This is a rule, not
presentation, so it lives here and is tested here"*; `Cat.cs`/`CatSave.cs`
→ `50-photo/10`; `GameSave.cs`/`BoardSave.cs` → task `6.7`/`60-shell-build/08`;
`SaveResume.cs` → `60-shell-build/08`, arguing *"deciding whether a save can
be resumed is a rule, so it lives here rather than in the view"*;
`PlayerProgress.cs` → tasks `6.2`/`6.2.1`; `RoomPlan.cs` →
`60-shell-build/02`; `TraitsRequest.cs` → `50-photo/07`). None arrived
silently. Judged against `ROLES.md`'s own charter for CORE — "rules engine,
unit tests" — three are unambiguous fits (`PhotoOutcome`/`PhotoJudge`,
`CatTraits`, the whole save/resume/progress family, which the project's own
comments justify as rules about *what* can be resumed, not presentation).
`Analytics` (protocol name constants + generic sink delegates) and
`TraitsRequest` (a JSON-body encoder) are the two furthest from "rules
engine" in the literal sense — both are still engine-free, tested, and each
is justified in its own file specifically *as* a boundary decision (Analytics:
"engine-free on purpose... lets Core stay engine-free," per
`tasks/70-analytics/01-sdk-integration/NOTES.md`'s reading of it), not as
an accident of where the author happened to be working.

**`Cat` is the more interesting case.** It is not new scope invented later —
`cat-shelter-mvp.md` §8, the exact section `01-entities/task.txt` cites as
CONTEXT, lists `Cat name, traits{}, state(1..3), owned_items[]` as an
entity from day one. `01-entities`'s own SCOPE simply never included it —
an omission in the original task, not a decision to exclude it — and it was
filled in eleven tasks later, under a different, real task number
(`50-photo/10`), whose own `VERIFY.md` (`tasks/50-photo/10-skip-default-cat/VERIFY.md`
item 4) already interrogated exactly this boundary question and left a
recommendation on record rather than a silent shrug.

**Verdict on item 4: `Core` is still what the task said it would be, in the
sense that matters — engine-free, and every addition is deliberate and
task-tracked, confirmed by reading each file rather than trusting a doc
comment — but not in the literal sense of "the five named types plus
nothing else," which was never realistic and the project itself stopped
pretending to hold by task 2. The one real gap is negative, not additive:
`Room` was promised and never built.**

## How to reproduce

From the current tree (not a fresh clone — another agent has uncommitted
edits to `Level.cs`/the solver bridge during this pass; re-run if a result
looks inconsistent):

```sh
dotnet test build/core-tests/core-tests.csproj -v q --nologo
# -> пройдено 156, всего 156 (run at least twice if any run shows a failure)
bash build/check-core-purity.sh; echo "exit=$?"
# -> Core is engine-free: OK, exit=0
.venv/bin/python -m pytest tools/ -q
# -> 155 passed
sed -n '41,61p' game/Assets/Core/Level.cs   # the two construction-time checks
```

Mutation test (purity gate), run outside the repository:

```sh
SP=$(mktemp -d)
mkdir -p "$SP/game/Assets/Core" "$SP/build"
cp game/Assets/Core/Item.cs "$SP/game/Assets/Core/Item.cs"
cp build/check-core-purity.sh "$SP/build/check-core-purity.sh"
python3 -c "p='$SP/game/Assets/Core/Item.cs'; open(p,'w').write('using UnityEngine;\n'+open(p).read())"
bash "$SP/build/check-core-purity.sh"; echo "exit=$?"
# -> ARCH VIOLATION ..., exit=1
```

`netstandard2.1` compile check, run outside the repository:

```sh
SP=$(mktemp -d)/netstd-check && mkdir -p "$SP"
cat > "$SP/netstd-check.csproj" <<'EOF'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFramework>netstandard2.1</TargetFramework><LangVersion>9</LangVersion></PropertyGroup>
  <ItemGroup><Compile Include="ABSOLUTE/PATH/TO/game/Assets/Core/**/*.cs" /></ItemGroup>
</Project>
EOF
cd "$SP" && dotnet build netstd-check.csproj -v q --nologo
# -> Ошибок: 0 (35 nullable warnings, same class as the net8.0 build)
```

## What was not checked

- No Unity editor, no Unity build, no adb, no emulator — out of scope for
  this pass per the brief.
- Whether the Unity **editor's own** compile of `Core` (via the asmdef,
  inside `Library/ScriptAssemblies`) actually agrees with the standalone
  `netstandard2.1` throwaway build above — both target the same declared
  API surface and the throwaway build's zero-error result is strong
  evidence, but the editor's own Roslyn invocation was not run directly
  (would require the Unity editor, out of scope here).
- Whether an engine reference could evade `check-core-purity.sh` through a
  route other than the literal strings it greps for (see "one gap the
  script itself does not close" above) — reasoned about and checked that no
  such route exists *today*, not proven impossible in general.
- A full field-by-field comparison of `Item`/`Shelf`/`Level`/`Board` against
  `cat-shelter-mvp.md` §8's entity list — only the two SCOPE-named
  construction rules and the missing `Room` type were checked; individual
  field names were not re-audited beyond what compiles and what the
  existing 156 tests exercise.
- The solver bridge and `Level.cs` were read, not modified, per the brief —
  their correctness beyond the two rules named in VERIFY item 3 was not
  independently re-derived in this pass.
