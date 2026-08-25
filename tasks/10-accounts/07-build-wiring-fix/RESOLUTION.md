# 07-build-wiring-fix — resolution

Status: **closed** on `dev` as of 26 August 2026. Both halves, including the
silent one.

## The choice: Option B, written down

`AGENT-BRIEF.md` offered two ways out of the two-build-systems-over-one-sources
collision:

- **Option A** — move the engine-free C# out of `Assets/` entirely; Unity
  references it through links.
- **Option B** — keep the sources in `Assets/`, add hand-written dotnet
  projects **outside** `Assets/` that pull them in by glob.

**Option B taken.** Reasons:

1. Unity owns `Assets/`; anything inside it is subject to its import pipeline,
   meta-file generation and special folder rules. Hand-written project files
   there fight the editor on every regeneration.
2. The asmdef arrangement (`CatShelter.Core`, `noEngineReferences: true`)
   already works and is verified by `build/check-core-purity.sh`. Option A
   would redo that wiring for no gain.
3. The solver-bridge had already proven the glob pattern compiles the Core
   sources cleanly under plain dotnet.

**Rejected:** Option A — it invalidates the working asmdefs and moves sources
out of the layout every Unity-facing document assumes. Also rejected: leaving
the generated `.csproj` files committed (see below).

## What was broken, in both halves

### Half one — loud: the bridge reference

`solver-bridge.csproj` pointed at `game/Assets/Core/CatShelter.Core.csproj`,
which creating the Unity project deleted. Four conformance tests errored with
CS0246. **Fixed in `266d933`:** the bridge now pulls Core sources by glob:

```xml
<Compile Include="../../game/Assets/Core/**/*.cs" />
```

### Half two — silent: green with zero tests

The Core tests "ran" through `game/CatShelter.Core.Tests.csproj` — a file Unity
generates, matched by `*.csproj` in `game/.gitignore`, therefore absent from any
clean clone. Worse, when the generated files were committed by mistake, running

```
dotnet test game/CatShelter.Core.Tests.csproj
```

exited 0 having discovered **zero tests**: that file is a Unity assembly
description (`OutputType=Library`, no test SDK, no NUnit adapter), not a runnable
test project. Green proved nothing.

**Fixed:**

1. `build/core-tests/core-tests.csproj` — hand-written test project outside
   `Assets/`, carrying `Microsoft.NET.Test.Sdk`, `NUnit`,
   `NUnit3TestAdapter`, pulling both Core and test sources by glob, with
   `<RollForward>Major</RollForward>` baked in (commit `c0a4bcc`).
2. All four Unity-generated `.csproj` removed from git (they were never
   supposed to be tracked; `game/.gitignore` already excludes them).
3. `LevelLoader.LoadAllFromAssets()` gained a `#else` branch so the same test
   code runs under Unity (asset database) and dotnet (filesystem walk).

## The documented commands (clean checkout, empty environment)

```bash
# Core unit tests — must report a count matching game/Assets/Tests/Core/*.cs
dotnet test build/core-tests

# Conformance + solver suite
python3 -m venv .venv && . .venv/bin/activate
uv pip install pytest        # or: pip install pytest
python -m pytest tools/tests
```

Both verified from a fresh `git clone` into an empty directory, no editor run,
no hand-exported variables: **54 / 54 passed** and **67 passed** respectively.

## Guard against the silent failure returning

A run that executes zero tests must fail, not pass — green is exactly what the
broken state produced. Enforced by comparing the printed executed-count against
the declared count before trusting green:

```bash
dotnet test build/core-tests 2>&1 | grep -E 'Пройден|passed'
grep -rc '\[Test\]' game/Assets/Tests/Core --include='*.cs' | awk -F: '{s+=$2} END {print s}'
```

If discovery breaks again the total drops to 0 and the mismatch is visible
immediately.
