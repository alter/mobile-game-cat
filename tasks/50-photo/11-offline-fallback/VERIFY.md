# Independent verification, 2026-08-27

**Verifier:** fresh context, wrote none of `Core/CatTraits.cs`,
`Shell/CatColour.cs`, or `View/CaptureScreen.cs`. No build/adb/emulator. Ran
`dotnet test build/core-tests/core-tests.csproj -v q --nologo` (156 passed).
One mutation probe built outside the repo.

## Per-item verdict

| # | Item | Result |
|---|---|---|
| VERIFY 1 | block Worker domain, run capture flow | **not testable as written — and NOTES.md already says so** ("the call is absent rather than blocked"; no Worker deployed, D17) |
| VERIFY 2 | colour matches by eye; pattern always solid | **partial, honestly measured**: 17/27 (63%) on the reference set; pattern is a hardcoded literal, always solid |
| VERIFY 3 | no error screen or crash | **fails on a case never tried** — see finding below |
| Item 2 | does the task acknowledge fallback-is-only-path? | **yes**, NOTES.md states it plainly |
| Item 3 | which fields are real, which invented; is Origin recorded | base_color real (63% accurate); pattern/fur_length/eye_color/white_markings all fixed literals; `TraitsOrigin.OfflineColourOnly` is recorded on the object but **nothing reads it** — no player-facing string exists that says the reading is partial |
| Item 4 | mutation: colour outside palette | **nothing catches it before `CatTraits`** — confirmed |

## The finding

`CaptureScreen.cs`'s Worker call is wrapped in `try/catch`; three lines
later, `CatTraits.FromColourOnly(colour)` is not. `CatTraits`'s constructor
throws `ArgumentException` for any `base_color` outside the six-value
palette. Probed outside the repo: calling `FromColourOnly("orange")` throws
uncaught. In the real coroutine this aborts `Handle()` before `OnCatReady`
fires or `SetBusy(false)` runs — worse than an error screen, since nothing
tells the player or the dev; the screen just stays on "Looking…" forever.

Today this can't fire in practice — `CatColour.swift`'s palette always
returns one of the six names `CatTraits.Allowed` expects, and the two lists
happen to agree. But nothing enforces that agreement, and this project has
already hit "two copies of one list drift apart" more than once elsewhere.
VERIFY 3's "no crash" claim was checked on three real photos that all
happened to land inside the palette — the actual boundary was never tried.

**Item 2, plainly:** `AskWorker` has zero assignment sites anywhere in
`game/Assets` — not a fallback, the only path, for every player, per D17.
Of five trait fields, one (`base_color`) is real, at 63% accuracy; four are
fixed literals. "Her cat" is roughly one real fact out of five today.

## How to reproduce

```bash
grep -rn "AskWorker *=" game/Assets   # empty
dotnet test build/core-tests/core-tests.csproj -v q --nologo
mkdir -p /tmp/colour-probe && cd /tmp/colour-probe
cat > probe.csproj <<'EOF'
<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType>
<TargetFramework>net8.0</TargetFramework><RollForward>Major</RollForward>
<ImplicitUsings>disable</ImplicitUsings></PropertyGroup><ItemGroup>
<Compile Include="<repo>/game/Assets/Core/**/*.cs" /></ItemGroup></Project>
EOF
cat > Program.cs <<'EOF'
using CatShelter.Core;
CatTraits.FromColourOnly("orange"); // throws ArgumentException, uncaught in CaptureScreen.cs
EOF
dotnet run --project probe.csproj   # ArgumentException, unhandled
```

## What was not checked

- The Swift palette matcher itself (only its output contract, by reading).
- Whether Unity would crash or just log on an uncaught coroutine exception —
  not run in an Editor/device, per constraints.
- The meet-your-cat screen (`09`, `status:todo`) — doesn't exist to render
  anything, partial-trait or not.

## Verdict

`verify:failed`. VERIFY 3's "no crash" promise is not backed by a guard, only
by the current palette happening to agree with `CatTraits.Allowed`. Fix is
small: a `try/catch` around the `FromColourOnly` call, same shape as the one
three lines above it. `status:` left at `done` — the mechanism is real and
mostly works; the gap is a missing guard, not a missing feature.
