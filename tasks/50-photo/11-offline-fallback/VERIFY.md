# Independent re-verification, 2026-08-27 (second pass)

**Verifier:** fresh context, wrote none of `Core/CatTraits.cs`,
`Shell/CatColour.cs`, `View/CaptureScreen.cs`, or
`Tests/Core/CatColourPaletteParityTests.cs`, and did not write the fix under
review (commit `7ad03cf`). No adb, no emulator, no Unity build. Ran
`dotnet test build/core-tests/core-tests.csproj -v q --nologo` and
`.venv/bin/python -m pytest tools/ -q` in the real repo, and a second,
isolated `dotnet test` run against a copy of the test project + sources
rsynced to a scratch directory outside the repo, used only for the mutation
probes below.

## Per-item verdict

| # | Item | Result |
|---|---|---|
| 1 | guard is real; failure yields a cat, not a message | **pass** |
| 2 | parity test fails (not skips) when the Swift file is missing; mutation-tested | **pass** |
| 3 | fallback-is-only-path finding stays visible; which fields are honest | **pass, with one addition below** |
| 4 | is `TraitsOrigin.OfflineColourOnly` still unread by anything player-facing | **still true — confirmed, unchanged** |

### 1. The guard

`CaptureScreen.cs:244-262` wraps the exact call that used to throw:
`try { traits = colour != null ? CatTraits.FromColourOnly(colour) :
CatTraits.Default; } catch (ArgumentException e) { ...; traits =
CatTraits.Default; }`. On rejection, `OnCatReady?.Invoke(traits)` still runs
three lines later with a real, complete `CatTraits.Default` cat — not a
partial object, not a rethrow, not a swallowed no-op that leaves `_busy` on
"Looking…" forever. Confirmed by reading the committed diff
(`git show 7ad03cf -- game/Assets/View/CaptureScreen.cs`): the change is
exactly this try/catch, same shape as the Worker's own catch nine lines
above it. `dotnet test`: 169 passed, 0 failed, 0 skipped (up from the 161
NOTES.md reported, from unrelated concurrent work elsewhere in the tree).

### 2. The parity test

`CatColourPaletteParityTests.cs:36` asserts `File.Exists(swiftPath)` with
`Assert.That(..., Is.True, ...)` — a real failing assertion, not
`Assert.Ignore`. Verified two ways, both outside the repo (rsynced
`build/core-tests` + `game/Assets/{Core,Tests,Plugins/iOS/CatColour.swift}`
into a scratch dir, `dotnet test --filter
FullyQualifiedName~CatColourPaletteParityTests`):

- Baseline: 1 passed, 0 skipped.
- Mutation — added `("orange", 0.90, 0.50, 0.10),` as a 7th palette entry to
  the copied Swift file: **1 failed, 0 skipped** ("не пройдено 1, пройдено
  0, пропущено 0").
- Deletion — removed the copied Swift file entirely: **1 failed, 0 skipped**,
  same counts. The "file cannot be found" path is a failure, not a quiet
  green.

Both probes ran against an isolated copy; nothing in the actual repository
was left mutated (scratch copy deleted afterward).

### 3. Fallback-is-only-path

`NOTES.md`'s dated section states plainly that `AskWorker` has no assignment
site (confirmed again here: `grep -rn "AskWorker *=" game/Assets` — empty)
and traces it to D17 (`tasks/DECISIONS.md:565`, accounts deferred, no spend
cap, no Worker deployed). Of the five trait fields, `base_color` is read
from the photo (63% accuracy per `ground-truth.txt`/`NOTES.md`); `pattern`,
`fur_length`, `eye_color`, `white_markings` are fixed literals
(`CatTraits.FromColourOnly`, `Core/CatTraits.cs:83-85`). A reader of NOTES.md
would understand this correctly — it says so in those terms.

**One addition the prior VERIFY.md did not make explicit:** the 63% figure
applies only to the branch where `Shell.CatColour.Estimate` returns
non-null; on the newly-guarded exception path *and* on a null estimate, the
player gets `CatTraits.Default` (a fixed grey tabby), not a colour reading
at all — so "roughly one fact in five" overstates the honesty of the guard
path specifically, though it is still the right outcome (a cat, not a
crash).

### 4. `TraitsOrigin.OfflineColourOnly`

Still true that nothing player-facing reads it. `grep -rn "\.Origin\b"
game/Assets --include="*.cs"` (excluding tests) finds exactly two call
sites: `Core/CatSave.cs:41`, which writes it into the save file's `traits`
line (persistence, not display), and `Shell/GameBoot.cs:151`, which prints
`cat ready ({cat.Origin}): {cat}` through `Report()` — a debug console line
reachable only via the `capture.txt` manual-test harness described in the
surrounding comments, not a player-facing screen. `grep -rn
"offline\|partial" game/Assets/Shell` finds no matching player-facing copy
string. The 09-meet-your-cat screen that would render a cat to the player
does not exist yet (`status:todo`, unchanged since the prior VERIFY).
**What it would take:** a `Shell.Copy` string keyed off `TraitsOrigin`
(e.g. distinguishing `OfflineColourOnly` from `Photo`) wired into whatever
09 ends up rendering — today there is no such string and no such screen, so
there is nothing to wire it into yet. This is not a defect in this task —
09 is out of scope here — but it means the honesty gap NOTES.md and the
prior VERIFY.md both flagged is still open, not closed by this fix.

## How to reproduce

```bash
# from a clean checkout of this repo, branch dev
dotnet test build/core-tests/core-tests.csproj -v q --nologo \
  --filter "FullyQualifiedName~CatColourPaletteParityTests"   # 1 passed, 0 skipped

grep -rn "AskWorker *=" game/Assets --include="*.cs"    # empty -> D17 confirmed
grep -rn "\.Origin\b" game/Assets --include="*.cs" | grep -v Tests/
  # -> only CatSave.cs (persistence) and GameBoot.cs Report() (debug line)

# mutation probe (outside the repo, deleted after)
mkdir -p /tmp/parity-probe/build /tmp/parity-probe/game/Assets/Plugins/iOS
rsync -a build/core-tests /tmp/parity-probe/build/
rsync -a game/Assets/Core game/Assets/Tests /tmp/parity-probe/game/Assets/
cp game/Assets/Plugins/iOS/CatColour.swift /tmp/parity-probe/game/Assets/Plugins/iOS/
rm -rf /tmp/parity-probe/build/core-tests/{bin,obj,TestResults}
# add a 7th tuple to the copied palette array, then:
cd /tmp/parity-probe && dotnet test build/core-tests/core-tests.csproj -v q --nologo \
  --filter "FullyQualifiedName~CatColourPaletteParityTests"   # -> 1 failed
rm game/Assets/Plugins/iOS/CatColour.swift
dotnet test build/core-tests/core-tests.csproj -v q --nologo \
  --filter "FullyQualifiedName~CatColourPaletteParityTests"   # -> 1 failed, 0 skipped
rm -rf /tmp/parity-probe

.venv/bin/python -m pytest tools/ -q   # 156 passed
```

## What was not checked

- No Unity Editor, iOS build, simulator, or device run — cannot see the busy
  label actually clear or a cat actually render on screen; verified only
  that the coroutine reaches `OnCatReady?.Invoke` with a valid `CatTraits`
  and that `SetBusy(false)` executes next, by reading `Handle()`.
- The Swift-side compiler is not invoked; the parity test's regex parsing of
  `CatColour.swift` was exercised, not Swift's own syntax validity.
- The 09-meet-your-cat screen and any future honesty-string work — out of
  scope for this task, noted as still open in item 4.
- Whether other agents' concurrent work (the three read-only directories
  named in this task's constraints) affects these numbers beyond the test
  counts observed; only this task's own files were touched.

## Verdict

`verify: passed`. The guard is real, proven by reading the committed diff
and by two independent mutation probes outside the repo (missing file and
7-colour drift both fail loudly, 0 skipped in both cases). The
fallback-is-only-path finding remains correctly recorded in NOTES.md. Item 4
is unchanged and still open — not a defect in this task's scope, but the
game still cannot tell a player which cat she is looking at; that work
belongs to 09-meet-your-cat, not here.

`status: done` (unchanged — the OUTCOME, a cat with no error screen, exists
and is now actually guaranteed rather than accidentally true).
