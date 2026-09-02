Verifier: independent QA context, wrote none of `game/Assets/Core/Cat.cs`,
`game/Assets/Core/CatSave.cs`, `game/Assets/Tests/Core/CatTests.cs` or
`game/Assets/Tests/Core/CatSaveTests.cs`, and wrote none of the task's own
`task.txt`/`NOTES.md`. Ran `dotnet test`, `check-core-purity.sh`,
`test_copy_table.py` and a mutation test against a scratch copy outside the
repo; read `GameBoot.cs` and `CaptureScreen.cs` directly rather than trusting
NOTES.md's description of them. Did **not** run any Unity build, PlayMode
test, the Android emulator, or adb — out of scope per the task brief. Did not
audit files outside the four named plus their direct Core dependency
(`CatTraits.cs`) and the two Shell/View files needed for item 1.

## Verdict by item

| # | Question | Verdict | Evidence |
|---|---|---|---|
| 1 | Is `in_progress` (not `done`) the honest status, and is the "no 09" reasoning real? | **Correct as-is** | `grep -rn "MeetYourCat" game/Assets` → no matches; `tasks/50-photo/09-meet-your-cat/labels.txt` → `status:todo`. `game/Assets/View/CaptureScreen.cs:233-239` — `Skip()` still calls only `OnCatReady?.Invoke(CatTraits.Default)`, an untraits-only `CatTraits`, never `Cat.Skipped` — the new `Cat`/`CatSave` classes have no caller anywhere in `game/Assets` outside their own tests (`grep -rn "Cat.Skipped\|CatSave\." game/Assets --include=*.cs` outside `Tests/` → nothing). `GameBoot.cs:136-140` its own comment: "[09 and 10] decide what happens after a photo is accepted" — not wired. So OUTCOME's "named and playable" is not true end-to-end today: the skip button still hands over an unnamed `CatTraits`, not a `Cat`. NOTES.md's narrower claim — that only the Core naming primitive was added, no UI — matches what is on disk. `in_progress` is the right call, and for the right reason. |
| 2 | Do the tests reach the states their names claim (the empty-white-markings double-space case)? | **Pass** | `CatSave.Write` for `CatTraits.Default` (empty `WhiteMarkings`) emits `...green  Skipped` (two spaces, since `string.Join(",", Array.Empty<string>())` is `""`). `CatSave.Read` splits that line with plain `raw.Split(' ')` (no `RemoveEmptyEntries`), which keeps the empty token, so `parts.Length == 7` still holds and `parts[5] == ""` maps to `Array.Empty<string>()` — a correct round trip, not an off-by-one. `CatSaveTests.RoundTrip_TheSkippedCat_NoWhiteMarkings` genuinely exercises this: it writes `Cat.Skipped` (which does have empty markings) and asserts `restored.Traits.WhiteMarkings, Is.Empty` and `Origin == Skipped` back. Confirmed by running the suite: `dotnet test build/core-tests/core-tests.csproj -v q --nologo` → `пройдено 152, всего 152` (full log below). `CatSaveTests.CorruptedFile_ReturnsNull_NoThrow` also deliberately reuses the double-space shape by hand for its "base colour outside palette"/"bad origin" cases, showing the format was understood, not stumbled into. |
| 3 | Mutation test: "malformed input returns null, never throws" | **Pass — the test can fail, and does when the guarantee is broken** | Copied `Cat.cs`, `CatSave.cs`, `CatTraits.cs` plus a minimal NUnit project into `/private/tmp/.../scratchpad/mutation` (outside the repo). Baseline: 2/2 pass. Mutated the scratch copy only — deleted the `catch (ArgumentException)` block in `CatSave.Read` — leaving the two `FormatException`/`IndexOutOfRangeException` catches. Re-ran: `Не пройден!: не пройдено 1, пройдено 1, всего 2` — `CorruptedFile_ReturnsNull_NoThrow` fails (it asserts `Read(...)` returns `null`, not that it throws, for a save naming a base colour or origin outside `CatTraits.Allowed`; with the catch removed, `CatTraits`'s own `ArgumentException` now propagates out of `Read` uncaught). `TruncatedSave_FallsBackCleanly` still passes because that particular input dies earlier (index/format path), which is expected and does not weaken the result. Confirmed `git status --short` in the repo shows no changes to the four reviewed files or to `tasks/50-photo/10-skip-default-cat/` (unrelated changes from other concurrent agents are present elsewhere in the tree — see reproduction command). |
| 4 | Is `Cat.DefaultName = "Kitty"` bypassing `test_copy_table.py` a defect or defensible? | **Defensible today, but the NOTES's fix is a plan, not an enforced guarantee — flagged, not failed** | `test_copy_table.py` only globs `game/Assets/View` and `game/Assets/Shell` (`UI_DIRS`, line 14) — confirmed by reading the file. Right now that blind spot costs nothing: `grep -rn "Cat.DefaultName\|Kitty" game/Assets --include=*.cs` outside `Tests/` finds only the definition in `Cat.cs` itself — no View/Shell file references it, because no screen exists yet to show it. So today there is no live escape of untranslated player-visible text; NOTES's claim that the stored value belongs in Core (it's part of the save format, must exist before any `Board` or screen) is correct — a save format constant is not "copy" in the sense `Copy.cs` exists for. I partly disagree with treating this as fully settled, though: `16-localisation-ready`'s own OUTCOME is "No user-visible English string literal remains outside the table" with no carve-out for names, and the *easy*, most likely way `09-meet-your-cat` gets built is `nameField.value = Cat.DefaultName` — a bare symbol reference, which `test_copy_table.py`'s literal-string regex can never catch even after 09 ships, because no string literal appears in View/Shell at all in that case. NOTES documents the risk but leaves it as a future decision with no test guarding it. Since no UI exists yet, this cannot fail task 10's own scope; recorded here as a known gap for whoever picks up 09 or 16, not as a defect in this task's deliverable. |

## How to reproduce

From a clean checkout of this branch (`dev`), no exported variables:

```sh
cd game && git worktree add /tmp/verify-check dev   # or a plain clone; any clean checkout works
cd /tmp/verify-check
dotnet test build/core-tests/core-tests.csproj -v q --nologo
# -> Пройден!   : не пройдено 0, пройдено 152, ..., всего 152
bash build/check-core-purity.sh
# -> Core is engine-free: OK
.venv/bin/python -m pytest tools/tests/test_copy_table.py -q
# -> 20 passed
grep -rn "MeetYourCat" game/Assets   # -> no matches: no meet-your-cat screen exists
sed -n '230,240p' game/Assets/View/CaptureScreen.cs   # Skip() still hands over CatTraits.Default only, not Cat.Skipped
```

Mutation test (run outside the repository — do not apply to the repo's own files):

```sh
SP=$(mktemp -d)/mutation && mkdir -p "$SP/Core"
cp game/Assets/Core/{Cat.cs,CatSave.cs,CatTraits.cs} "$SP/Core/"
cat > "$SP/mutation-test.csproj" <<'EOF'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <RollForward>Major</RollForward>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.9.0" />
    <PackageReference Include="NUnit" Version="4.1.0" />
    <PackageReference Include="NUnit3TestAdapter" Version="4.5.0" />
  </ItemGroup>
</Project>
EOF
# add a test file with CorruptedFile_ReturnsNull_NoThrow (copy from
# game/Assets/Tests/Core/CatSaveTests.cs), then:
dotnet test "$SP/mutation-test.csproj" -v q --nologo   # baseline: 2/2 pass
# delete the `catch (ArgumentException)` block in "$SP/Core/CatSave.cs"
dotnet test "$SP/mutation-test.csproj" -v q --nologo   # mutated: 1/2 pass, CorruptedFile_ReturnsNull_NoThrow fails
git status --short   # confirm the repo itself is untouched
```

Raw baseline test log used for this VERIFY (run in the repo, 2026-08-27):
`dotnet test build/core-tests/core-tests.csproj -v q --nologo` →
`Пройден!   : не пройдено 0, пройдено 152, пропущено 0, всего 152`.

## What was not checked

- No Unity build, no PlayMode test, no Android emulator, no adb — explicitly
  out of scope for this verification pass (another agent owns the emulator).
  VERIFY items 1 and 2 in `task.txt` ("tap skip, reach meet-your-cat...",
  "airplane mode and camera permission denied") were not run and cannot be
  run: there is no meet-your-cat screen to tap through to (see item 1 above).
- iOS/on-device behaviour, camera permission prompts, and network conditions
  were not exercised on any device or simulator.
- `CatTraits.cs`'s own correctness (palette membership, `Origin` enum) was
  read but not independently re-derived beyond what the existing 152 tests
  already cover; no new coverage was added for it.
- Files outside this task's four (`Cat.cs`, `CatSave.cs`, `CatTests.cs`,
  `CatSaveTests.cs`) were not reviewed for regressions beyond confirming the
  full `dotnet test` suite (152 tests, unchanged files included) still passes.
- The mutation test covers exactly one guarantee (`CatSave.Read` never throws
  on an out-of-palette field). Other malformed-input branches (wrong header,
  wrong field count, bad enum) were not separately mutation-tested — the
  existing `CorruptedFile_ReturnsNull_NoThrow` test asserts all of them in one
  method, and killing the `ArgumentException` catch was enough to show the
  test is not vacuous.
- The `Cat.DefaultName`/`Copy.cs` question (item 4) was judged on the code as
  it stands today; it was not re-checked against whatever `09-meet-your-cat`
  or `16-localisation-ready` eventually implement, since neither exists yet.

---

# Дополнение 2026-09-02 — причина `in_progress` истекла

Проверка выше признавала `in_progress` честным статусом по одной причине,
записанной в пункте 1: экрана `09-meet-your-cat` не существовало, `Skip()`
отдавал безымянный `CatTraits`, и «названный и играбельный» из OUTCOME не
выполнялось от начала до конца.

Сегодня выполняется. `CaptureScreen.Skip()` (см. его тело) зовёт
`OnCatReady?.Invoke(CatTraits.Default)`, а `GameBoot.cs:728` ведёт ОБА пути —
снимок и пропуск — в один и тот же `ShowMeetYourCat`, где игрок вводит имя, и
единственная в игре запись `cat.save` происходит в его `OnNamed`
(проверено grep'ом при закрытии `60-shell-build/20`). Задача переведена в
`status:done`.

Что от той проверки осталось в силе и не закрыто: пункт 4 — `Cat.DefaultName`
живёт в Core и потому вне охвата `test_copy_table.py`, а сегодняшний
`test_font_coverage.py` (60-shell-build/23) эту дыру тоже не закрывает: он
проверяет знаки таблиц `Copy*.cs`, а не имена из Core. Записано здесь, чтобы
не пропало.
