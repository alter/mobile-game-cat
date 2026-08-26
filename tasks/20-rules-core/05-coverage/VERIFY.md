# VERIFY — 20-rules-core/05-coverage

Result: **failed** — both VERIFY items. No command in the repository produces a
coverage report at all, and there is no CI step to lower a threshold in.

Verifier: an independent agent context, 2026-08-26, against `dev` at commit
`27f9904`. It did **not** write `build/coverage-summary.py`, did **not** write
`build/core-tests/core-tests.csproj`, did **not** write any Core source or test,
and changed none of them during this check. It did not add `coverlet.collector`
to the repository — the measurement below was made from a project outside the
repo, precisely so the repo's own broken state stayed visible. Its only writes
were this file and `labels.txt`.

## Verdict per VERIFY item

| # | Item | Result |
|---|---|---|
| 1 | Coverage report shows line rate at or above 90% on Core | **fail** — no report can be produced from a clean checkout |
| 2 | Lowering the threshold in CI and reverting proves the step actually fails | **fail** — there is no CI |

### 1. No coverage report — fail

`build/coverage-summary.py:7` reads `TestResults/*/coverage.cobertura.xml`, i.e.
it expects `dotnet test --collect:"XPlat Code Coverage"`. That collector ships in
the `coverlet.collector` NuGet package. `build/core-tests/core-tests.csproj:19-24`
references four packages — `Microsoft.NET.Test.Sdk`, `NUnit`,
`NUnit3TestAdapter`, `Newtonsoft.Json` — and `coverlet.collector` is not among
them. In a fresh clone with a scrubbed environment:

```
Data collection : Unable to find a datacollector with friendly name 'XPlat Code Coverage'.
Data collection : Could not find data collector 'XPlat Code Coverage'
Passed!  - Failed: 0, Passed: 60, Skipped: 0, Total: 60 - core-tests.dll (net8.0)
```

`TestResults/` is left empty (`find TestResults -type f` returns nothing), and
the summary script then exits 1 with its own message:

```
no coverage.cobertura.xml found under TestResults/
```

That is the exact failure mode the `tasks/README.md` rule was written against:
the report ran ahead of the check. `reviews/2026-08-24-m2-m3.md:19` records
"Core coverage 91% | 90.9% by lines" for a tree that had 23 tests; that number
cannot be reproduced today by any command in the repository.

**What the real number is.** Reconstructed out-of-tree — a `net8.0` project in a
temp directory compiling the same two globs as `core-tests.csproj` plus
`coverlet.collector` 6.0.2, with `IncludeTestAssembly=true` (needed because Core
is compiled *into* the test assembly rather than referenced as a project), run
against a fresh clone at `27f9904`, all 60 tests green:

| class | lines covered | rate |
|---|---|---|
| `CatShelter.Core.Analytics` | 43/46 | 93.5% |
| `CatShelter.Core.AnalyticsEventNames` | 6/6 | 100.0% |
| `CatShelter.Core.Board` | 80/90 | 88.9% |
| `CatShelter.Core.BoardSave` | 34/36 | 94.4% |
| `CatShelter.Core.BoardSnapshot` | 18/18 | 100.0% |
| `CatShelter.Core.GameSave` | 85/89 | 95.5% |
| `CatShelter.Core.Item` | 10/12 | 83.3% |
| `CatShelter.Core.ItemKind` | 6/8 | 75.0% |
| `CatShelter.Core.Level` | 14/16 | 87.5% |
| `CatShelter.Core.PileEntry` | 7/7 | 100.0% |
| `CatShelter.Core.PlayerProgress` | 35/38 | 92.1% |
| `CatShelter.Core.SavedGame` | 21/27 | 77.8% |
| `CatShelter.Core.Shelf` | 46/48 | 95.8% |
| **production Core total** | **405/441** | **91.8%** |

So the 90% target is in fact met — 91.8% — but item 1 says "coverage report
shows", and no report exists. `Board` itself is at 88.9% and `SavedGame` at
77.8%, both below the threshold; the task asks for the figure on Core as a
whole, which passes.

**A second defect in the tooling.** `build/coverage-summary.py:13` filters on
`name.startswith("CatShelter.Core")`, which also matches the test namespace
`CatShelter.Core.Tests`. Run against the same report, the script counts test
methods as covered production code and prints:

```
TOTAL Core: 1049/1086 = 97%
```

97% instead of 91.8%. Had the collector been present, the script would have
overstated coverage by 5.2 points.

### 2. No CI step — fail

There is no continuous-integration configuration in the repository at all:

```
$ git ls-files | grep -icE "^\.github|^\.gitlab|Jenkinsfile|azure-pipelines|Makefile|justfile|\.circleci"
0
```

`build/` contains `check-core-purity.sh`, `coverage-summary.py`,
`core-tests/`, `solver-bridge/` and `playtest/` — no build or CI driver.
`coverage-summary.py` has no threshold and no non-zero exit on a low number: its
only `sys.exit` is the missing-file message at line 9, and the last statement is
a `print` (line 24). The only script in the tree that fails a build on a gate is
`build/check-core-purity.sh:12`, which is about engine references, not coverage.

The task's SCOPE line "+ A CI step that fails the build below the threshold" is
therefore not implemented, and item 2 — "lowering the threshold and reverting
proves the step actually fails" — has nothing to lower.

## How to reproduce

From a clean state — fresh clone, nothing exported by hand. Network is needed
for the NuGet restore.

```bash
git clone <repo-url> /tmp/verify-05 && cd /tmp/verify-05
git rev-parse --short HEAD          # expect 27f9904 for the numbers above

# item 1 — the repo's own coverage path, which does not work
cd /tmp/verify-05/build/core-tests
dotnet test core-tests.csproj --nologo --collect:"XPlat Code Coverage" 2>&1 \
  | grep -i "data collect"
find TestResults -type f          # empty
python3 ../coverage-summary.py; echo "exit=$?"   # "no coverage.cobertura.xml found"

# item 2 — no CI to lower a threshold in
cd /tmp/verify-05
git ls-files | grep -icE "^\.github|^\.gitlab|Jenkinsfile|azure-pipelines|Makefile|justfile|\.circleci"
grep -n "exit\|threshold" build/coverage-summary.py

# the real coverage figure, measured outside the repo so the repo stays untouched.
# The symlink is needed because HeadlessRunTests walks up from the assembly
# location looking for game/Assets/Resources/Levels (LevelLoader.cs:66-74);
# without it four tests fail and the figure is understated.
mkdir -p /tmp/verify-05-cov/cov && ln -sfn /tmp/verify-05/game /tmp/verify-05-cov/game
cd /tmp/verify-05-cov/cov
cat > cov.csproj <<'EOF'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework><LangVersion>9</LangVersion>
    <Nullable>enable</Nullable><ImplicitUsings>disable</ImplicitUsings>
    <RollForward>Major</RollForward><IsPackable>false</IsPackable>
    <NoWarn>CS8600;CS8603;CS8604;CS8625</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="/tmp/verify-05/game/Assets/Core/**/*.cs" />
    <Compile Include="/tmp/verify-05/game/Assets/Tests/Core/**/*.cs" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="NUnit" Version="4.2.2" />
    <PackageReference Include="NUnit3TestAdapter" Version="4.6.0" />
    <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
    <PackageReference Include="coverlet.collector" Version="6.0.2" />
  </ItemGroup>
</Project>
EOF
cat > cov.runsettings <<'EOF'
<?xml version="1.0" encoding="utf-8"?>
<RunSettings><DataCollectionRunSettings><DataCollectors>
  <DataCollector friendlyName="XPlat code coverage">
    <Configuration><Format>cobertura</Format><IncludeTestAssembly>true</IncludeTestAssembly></Configuration>
  </DataCollector>
</DataCollectors></DataCollectionRunSettings></RunSettings>
EOF
dotnet test cov.csproj -v q --nologo --collect:"XPlat Code Coverage" --settings cov.runsettings

# the repo's script, which over-counts by including CatShelter.Core.Tests:
python3 /tmp/verify-05/build/coverage-summary.py     # prints "TOTAL Core: ... = 97%"

# production-only figure, the 91.8% quoted above:
python3 - <<'EOF'
import glob, xml.etree.ElementTree as ET
f = sorted(glob.glob("TestResults/*/coverage.cobertura.xml"))[-1]
c_, t_ = 0, 0
for c in ET.parse(f).getroot().iter("class"):
    n = c.get("name") or ""
    if not n.startswith("CatShelter.Core") or ".Tests" in n: continue
    lines = c.find("lines")
    if lines is None: continue
    cov = sum(1 for l in lines if l.get("hits") != "0"); tot = len(list(lines))
    print(f"{n}: {cov}/{tot} = {100*cov/tot:.1f}%"); c_ += cov; t_ += tot
print(f"production Core: {c_}/{t_} = {100*c_/t_:.1f}%")
EOF
```

## What was not checked

- Branch coverage. Only line coverage was measured, which is what the task asks
  for. The task's GOAL adds "with every termination branch reached" — that was
  **not** verified in general. One counter-example was found while checking
  `04-outcomes`: `Board.cs:128` has `condition-coverage="0% (0/2)"` and
  `Board.cs:126,128-131,133-134` have `hits="0"`, so the win/jam decision inside
  the refused-placement block is a termination branch that is never reached.
- Coverage measured the way the project actually ships. The figures above come
  from a `net8.0` build of the same sources, not from Unity's
  `com.unity.testtools.codecoverage` under IL2CPP; the two need not agree.
- Whether `IncludeTestAssembly=true` distorts the production-class figures. It
  is required here because Core is compiled into the test assembly, and the
  per-class filter excludes `CatShelter.Core.Tests`, but no cross-check against
  a project-reference layout was done.
- The working tree, as opposed to commit `27f9904`. While this check ran,
  another context added `game/Assets/Core/SaveResume.cs` and
  `game/Assets/Tests/Core/SaveResumeTests.cs`, still untracked. Measured over
  the working tree the suite is 72 tests and production Core is 444/479 =
  92.7%; every figure in this file is the clean-clone one, 60 tests and
  405/441 = 91.8%, so that the reproduce recipe above matches it. The untracked
  work was not reviewed.
- Historical claims were not re-derived. `reviews/2026-08-24-m2-m3.md:19` says
  90.9%; that tree had 23 tests against today's 60 and a smaller Core, and no
  attempt was made to reproduce it at that commit.
- No fix was applied. Adding `coverlet.collector` to
  `build/core-tests/core-tests.csproj` and correcting the namespace filter in
  `build/coverage-summary.py:13` are the obvious repairs, plus some script that
  exits non-zero below 90; all three are out of this verifier's scope.

---

## What changed after this verification, 2026-08-26

The failure above was accurate and has been acted on by the context that owns
the build (not by this verifier):

- `build/core-tests/core-tests.csproj` now references `coverlet.collector`, so
  a coverage run actually writes `TestResults/*/coverage.cobertura.xml`.
- `build/core-tests/coverage.runsettings` (new) sets `IncludeTestAssembly`,
  because Core and its tests compile into one assembly here and coverlet
  otherwise skips it — the first report produced had zero classes in it.
- `build/coverage-summary.py` now excludes `CatShelter.Core.Tests.*` from the
  total. Counting test code as covered code inflated the first number produced
  (97% including tests, 92.7% without), which is exactly the way this gate
  could have been passed while measuring nothing.
- The same script takes `--min` and exits 1 below it, which is the CI step
  VERIFY item 2 asks for.

Current numbers, reproducible from a clean checkout:

```
dotnet test build/core-tests/core-tests.csproj \
  --settings build/core-tests/coverage.runsettings --results-directory TestResults
python build/coverage-summary.py --min 90
```

- 83 tests pass, line rate on Core **94.3%** (462/490), exit code 0.
- `python build/coverage-summary.py --min 99` prints the shortfall and exits 1,
  which is item 2's "lowering the threshold proves the step actually fails",
  run in the direction that does not require breaking anything.

`verify` is left **pending**, not passed: the context that fixed the tooling
cannot also sign it off. A third context should re-run the two commands above.
