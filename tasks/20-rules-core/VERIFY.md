# VERIFY — 20-rules-core (phase)

Verifier: an independent agent context, 2026-08-28, against `dev` at `c5bf5cc`.
I wrote no file under `game/Assets/Core`, none of the tests under
`game/Assets/Tests/Core`, neither `build/check-core-purity.sh` nor
`build/coverage-summary.py`. I did **not** run Unity, did **not** run
`Unity -runTests` (AGENT-BRIEF.md: it exits 0 having executed nothing), did
**not** produce a player build, did **not** run the iOS simulator or the Android
emulator, and did **not** hand a phone to anybody. I changed no file in the
repository.

This file is written because the phase carried `verify:passed` with no
`VERIFY.md`, which `tasks/README.md` makes a precondition of that label.

## The claim under test

OUTCOME: *"A netstandard2.1 rules library that compiles both inside and outside
Unity, with zero occurrences of `using UnityEngine` under Assets/Core."*

Four numbered VERIFY items. Three are machine-checkable; the fourth is not, and
it decides this document.

## Item 1 — `bash build/check-core-purity.sh`

```sh
bash build/check-core-purity.sh; echo "EXIT=$?"
```

```
Core is engine-free: OK
EXIT=0
```

The script greps `game/Assets/Core` for `using UnityEngine` **and** for bare
`UnityEngine.` qualifications, and exits 1 on any hit — so it catches the
fully-qualified evasion, not just the using directive. Independently:

```sh
grep -rn "using UnityEngine" game/Assets/Core | wc -l
# -> 0
```

The OUTCOME's "zero occurrences" is literally true today. **PASS.**

## Item 1b — the netstandard2.1 half of the OUTCOME

`check-core-purity.sh` proves the sources name no engine type. It does not prove
they *compile* to netstandard2.1 — the repository's own dotnet project
(`build/core-tests/core-tests.csproj:6`) targets `net8.0`, which is a strictly
larger surface. A Core file could use an API that exists in net8.0 and not in
netstandard2.1 and every check in the tree would stay green.

So I compiled the sources against the framework the OUTCOME actually names, in a
throwaway project **outside the repository** (scratchpad), touching nothing:

```xml
<TargetFramework>netstandard2.1</TargetFramework>
<LangVersion>9</LangVersion>
<Compile Include="…/game/Assets/Core/**/*.cs" />
<PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
```

```
    Предупреждений: 35
    Ошибок: 0
```

**0 errors.** 35 warnings, all nullable-reference warnings (`CS8603`, `CS8625`)
concentrated in `GameSave.cs`; none is an API-availability error. The
netstandard2.1 claim holds under a compiler, not only under a grep. **PASS.**

For the record, Unity's own setting agrees:
`game/ProjectSettings/ProjectSettings.asset:875` — `apiCompatibilityLevel: 6`,
and `game/Assets/Core/CatShelter.Core.asmdef` carries
`"noEngineReferences": true` with an empty `references` array. Neither is a
compile; both are consistent with one.

## Item 2 — tests green from a clean checkout, no hand-exported variables

Checked in a fresh clone into an empty directory, under `env -i`, with no editor
ever launched there:

```sh
git clone --depth 1 --branch dev file://<repo> <scratch>/clean && cd <scratch>/clean
env -i HOME="$HOME" PATH="/usr/local/share/dotnet:/usr/bin:/bin:/usr/sbin:/sbin" TMPDIR="$TMPDIR" \
  dotnet test build/core-tests/core-tests.csproj
```

```
Passed!  - Failed:     0, Passed:   195, Skipped:     0, Total:   195, Duration: 311 ms - core-tests.dll (net8.0)
```

Green alone would prove nothing here — this project has already seen a run exit
0 having discovered zero tests. So the count was checked against the
declaration, re-derived today:

```sh
grep -rho '\[Test\]'   game/Assets/Tests/Core --include='*.cs' | wc -l   # -> 166
grep -rho '\[TestCase' game/Assets/Tests/Core --include='*.cs' | wc -l   # ->  29
```

166 + 29 = **195 declared**, **195 executed**. No `[TestCaseSource]`,
`[Values]`, `[Theory]` or `[Repeat]` anywhere in that tree (each grep → 0), and
no method carries both `[Test]` and `[TestCase]`, so the arithmetic is exact
rather than approximate. **PASS.**

Caveat, and it belongs to the neighbouring task: the *Python* half of "the tests"
is red from a clean checkout today — `pytest tools/tests` is interrupted during
collection by an undeclared `numpy` import. That is recorded in full in
`tasks/10-accounts/07-build-wiring-fix/VERIFY.md`. It does not touch Core, and
this phase's item 2 concerns Core, so I read item 2 as passing.

## Item 3 — line coverage on Core ≥ 90%, report attached

Regenerated today rather than read off yesterday's file, into a scratch results
directory so nothing in the repository moved:

```sh
dotnet test build/core-tests/core-tests.csproj \
  --settings build/core-tests/coverage.runsettings \
  --collect:"XPlat Code Coverage" --results-directory <scratch>/TestResults
cd <scratch> && python build/coverage-summary.py --min 90; echo "GATE_EXIT=$?"
```

```
Analytics.Design: 5/6
Analytics.Progression: 6/8
Board.IsTaken: 0/1
Board.IsRevealed: 5/6
Board.TakeItem: 26/33
…
TOTAL Core: 767/808 = 94.9%  (uncovered methods listed above)
GATE_EXIT=0
```

**94.9% ≥ 90%. PASS on the number.**

Two things worth writing down, because both are the kind of detail that rots:

1. **The `--settings` flag is not optional.** Running the same command without
   `--settings build/core-tests/coverage.runsettings` produces
   `<coverage line-rate="0" … lines-valid="0">` with an empty `<packages />`,
   and `coverage-summary.py` then exits with
   `no CatShelter.Core classes in the report — check the runsettings filter`.
   Core and its tests compile into one assembly, and coverlet skips the test
   assembly by default. `build/headless-build.sh:96-99` does pass the flag, so
   the wired path is correct; a hand-run one is easy to get wrong.
2. **"the report is attached" is not satisfied.** `TestResults/` is gitignored
   (`git check-ignore -v TestResults` → `.gitignore:4:TestResults/`), and no
   coverage report is committed in this directory or in `05-coverage/`. What
   exists instead is a gate that regenerates and re-checks the number
   (`build/headless-build.sh:131`, `coverage-summary.py --min 90`), which is
   strictly better than an attached artefact — but the item as written asks for
   a file, and there is no file. I count the substance as met and the wording as
   not.

## Item 4 — "A human plays one level through on a phone" — **NOT DONE**

Not by me, and not by anybody so far. This is not a limitation I hit; it is the
item's content. `tasks/README.md`: *"For `role:HUMAN` tasks substitution is
impossible in principle: an agent almost always reports that the result looks
good."*

The phase's SCOPE lists `+ 06-debug-view` as part of it, and that sub-task's own
labels say:

```sh
grep -h "verify:" tasks/20-rules-core/06-debug-view/labels.txt
# -> verify:pending
```

Its `VERIFY.md:149` is explicit: *"A human plays one level through on a phone |
**Still pending — not passable by this context, on any hardware**"*, written by
a context that had just played level 1 through on an Android emulator and
declined to call that the item.

So the phase carries `verify:passed` over a sub-task that carries
`verify:pending`, for the same check. **FAIL on item 4**, and it is unfixable by
any agent.

## How to reproduce

```sh
git clone --depth 1 --branch dev <repo-url> clean && cd clean

# item 1
bash build/check-core-purity.sh; echo $?          # -> OK, 0

# item 1b — netstandard2.1, outside the repo
mkdir /tmp/nscheck && cd /tmp/nscheck
cat > nscheck.csproj <<'XML'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.1</TargetFramework><LangVersion>9</LangVersion>
    <Nullable>enable</Nullable><ImplicitUsings>disable</ImplicitUsings>
    <RollForward>Major</RollForward><EnableDefaultCompileItems>false</EnableDefaultCompileItems>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="<abs-path-to-clone>/game/Assets/Core/**/*.cs" />
    <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
  </ItemGroup>
</Project>
XML
dotnet build                                      # -> Errors: 0

# item 2 — count must equal count
cd <clone>
env -i HOME="$HOME" PATH="/usr/local/share/dotnet:/usr/bin:/bin" TMPDIR="$TMPDIR" \
  dotnet test build/core-tests/core-tests.csproj
echo $(( $(grep -rho '\[Test\]' game/Assets/Tests/Core --include='*.cs' | wc -l) \
       + $(grep -rho '\[TestCase' game/Assets/Tests/Core --include='*.cs' | wc -l) ))

# item 3 — note --settings
dotnet test build/core-tests/core-tests.csproj --settings build/core-tests/coverage.runsettings \
  --collect:"XPlat Code Coverage" --results-directory TestResults
python3 build/coverage-summary.py --min 90; echo $?

# item 4 — hand a phone to a person. There is no command.
```

## What was not checked

- **That Core compiles inside Unity.** Barred from running the editor. The
  netstandard2.1 compile and the asmdef are evidence *about* the sources, not
  about Unity's compilation of them. The OUTCOME says "both inside and outside
  Unity"; only the outside half is established here.
- **Item 4, at all.** No phone, no person, no play session. Neither performed
  nor simulated, per the independence rule.
- **The sub-tasks 01–05 individually.** I checked the phase's four items. Each
  sub-task has its own label and, where required, its own `VERIFY.md`; the
  2026-08-28 audit covers them and I did not duplicate it.
- **Branch coverage.** The gate reads line coverage. The fresh report shows
  `branch-rate="0.8728"`; no item asks about it and I make no claim.
- **Whether the 41 uncovered lines matter.** `coverage-summary.py` lists them by
  method; I read the list and judged nothing.
- **The 05-coverage mutation proof.** That the gate can fail was established by
  the 2026-08-28 audit at `--min 99.9`; I ran only `--min 90`, which passed. I
  did not re-run the failing direction.

## Verdict

**Items 1, 2 and 3 pass under commands I ran today. Item 4 has never been
performed and no agent can perform it.**

The engine-free rules library is real: 0 errors against netstandard2.1 outside
the repository, 0 engine references, 195 declared tests all discovered and all
green from a clone with a wiped environment, 94.9% line coverage behind a gate
that runs in the build.

**`verify: passed` is not warranted.** Not because the code is doubtful — it is
the best-evidenced part of this tree — but because the label asserts a check
that has not happened, and the phase's own sub-task `06-debug-view` says so on
its `verify:pending` line. A phase cannot be greener than the sub-task it
contains.

I would set the phase to `verify: pending` with a note naming item 4 as the one
outstanding check, exactly as `06-debug-view` already does. Alternatively, split
item 4 out of the phase and let it live only in `06`, where it is already
tracked honestly — but that is an owner's decision about the task tree, not
mine. I have changed no label.
