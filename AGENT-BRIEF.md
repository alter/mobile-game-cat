# Development brief for the agent: "Rescued Kitten"

You are leading development of a mobile 2D puzzle game for iOS. This is not a
product, but a three-week test of the concept: find out whether an install can
be bought cheaply and whether players come back, before building the economy.

## Where the truth is

Repository: `git@github.com:alter/mobile-game-cat.git`

Read in this order, before the first line of code:

1. `knowledge/README.md` — index of the knowledge base and findings that change decisions
2. `knowledge/00-versions.md` — verified versions of the entire toolset
3. `cat-shelter-tasks.md` — tasks, acceptance, roles, gates
4. `cat-shelter-tech.md` — architecture and rationale
5. `cat-shelter-mvp.md` — the concept and what it's all for

The `knowledge/` directory holds excerpts from primary sources for specific
versions, with a link for every fact. It exists exactly so you don't
reconstruct calls from memory.

## Rule number one

**If you don't remember it, don't write it.** Before applying an unfamiliar
call, parameter, command-line flag, or package name, find it in `knowledge/`.
If it isn't there, open the primary source, verify it, add it to `knowledge/`
with a link. A made-up call costs hours of debugging; verification costs a
minute.

Any number — version, limit, price, share — is given with a link or not given
at all. "About 95%" without a source is worse than "no exact data."

## Toolset, change only with justification

| Layer | Decision |
|---|---|
| Engine | Unity 6.3 LTS, **6000.3.22f1** — install from the [archive](https://unity.com/releases/editor/archive); by default Unity offers 6.5, which is an Update release and doesn't fit |
| Language | C#, .NET Standard 2.1 |
| Rendering | URP 2D Renderer |
| Interface | UI Toolkit, layout in UXML/USS |
| Save | `JsonUtility` |
| Level loading | `com.unity.nuget.newtonsoft-json` |
| Tests | Unity Test Framework, NUnit |
| Level solver | Python 3, outside the Unity project, `/tools` directory |
| Intermediary node | Cloudflare Workers, TypeScript, `/worker` directory |
| Measurement | GameAnalytics + App Store Connect analytics |
| Build | Xcode 26+, iOS 26 SDK, headless from the command line |

## What never to do

- `System.Text.Json` — under IL2CPP it hits `Reflection.Emit`, doesn't work on iOS
- `using UnityEngine` anywhere under `/Assets/Core` — this is a condition, not
  a wish, and is checked by a grep step in the build
- DOTween, Zenject, Odin, off-the-shelf mechanics kits
- paid services, bank card binding, GCP and the like — there is no budget
- writing your own where a free ready-made solution exists: measurement is not
  homegrown, App Store Connect counts retention
- calling `RequestTrackingAuthorization` — we don't need the ATT dialog and it
  costs installs

## Where to start right now

Gate 0.7 — testing ad creatives — is human work, you don't do it. **Don't
build the shell, artwork, the cat-photographing screen, or store integration
before a "yes" on this gate.**

What can and should be done: **M2 and M3** — the rules core and the level
solver. They don't depend on the game's theme and survive a change of concept,
so building them before the gate carries no risk. This is exactly the
"machine tool" for whose sake the layer split was undertaken.

Order: 2.1 → 2.6, then 3.1 → 3.8.

First action: read what's listed above, create a Unity project of the
required version with the directory tree from section 6 of
`cat-shelter-tech.md`, set up the assembly definition so `Core` builds without
referencing the engine, put the grep check into the build. Show the plan for
M2 before writing code.

## What "done" means

Acceptance for every task is written in `cat-shelter-tasks.md` **before** work
begins. Don't tailor it to the result you got.

Green tests are not progress toward the goal. There are about 65 tasks on the
list, and the goal is checked by three gates: 0.7 (does the promise sell), 3.7
(is it worth building), 8.0 and M8 (do they come back and pay). Everything
else is scaffolding. Don't report "twelve tasks closed" as an achievement:
that is exactly the trap the throughput section of this document was written
for.

Tasks marked HUMAN you don't perform and don't simulate. Especially 3.7 — five
outside people play the debug build. They cannot be replaced by your own
judgment: an agent almost always reports that things turned out well.

## How to work

- **code** — a branch per task, don't commit straight to `main`
- **documents — only in `main`.** `cat-shelter-mvp.md`, `cat-shelter-tech.md`,
  `cat-shelter-tasks.md`, `AGENT-BRIEF.md`, `knowledge/`, `reviews/` are edited
  in a separate commit to `main`, not inside a task branch. This is the only
  shared point of truth: a document sitting in a branch doesn't exist for
  anyone else, and the next person to pick up work will read a stale version.
- update the task branch from `main` before working, to pick up documents
- the rules core is covered by tests, tests run on every change
- keep data in JSON and ScriptableObject, interface in UXML/USS, assemble
  scenes with code — this way the agent breaks less
- don't lose `.meta` files, don't rewrite GUIDs
- write long files in parts, not in one piece
- before declaring work done, show the output of commands, not a retelling
- all documentation, task files, code comments, and commit messages are in
  English; the reason is that agents are measurably more accurate in English,
  and this repository is read mostly by agents; the sole exception is a
  verbatim quote from a Russian-language source, which stays in the original,
  because translating a quote stops it being a quote

## Environment: verified 25 August 2026, everything ready

Nothing needs to be installed further. Verified by running it, not by the
presence of icons.

| What | State |
|---|---|
| Unity | `6000.3.22f1`, binary: `/Applications/Unity/Hub/Editor/6000.3.22f1/Unity.app/Contents/MacOS/Unity` |
| Unity modules | iOSSupport, AndroidPlayer, WebGLSupport |
| Unity license | Unity Personal, active; entitlements include `com.unity.editor.headless` and `com.unity.editor.platforms.ios` — see command below |
| Xcode | 26.3, build 17C529 |
| `xcode-select` | points to `/Applications/Xcode.app/Contents/Developer` |
| iOS SDK | 26.2 and simulator 26.2 — the store requirement from 28 April 2026 is met |
| .NET SDK | 10.x, for core tests outside Unity |

Since batch mode is permitted by the license, run the build and tests
yourself, don't ask a human to click in the editor.

### How to check the license correctly

Don't search for a file and don't run a trial build. Unity has a licensing
client with its own command:

```bash
"/Applications/Unity/Hub/Editor/6000.3.22f1/Unity.app/Contents/Helpers/\
UnityLicensingClient.app/Contents/MacOS/Unity.Licensing.Client" --showEntitlements
```

It outputs the license name, terms, and **the list of entitlements**. Two
lines matter:

- `com.unity.editor.headless` — running without graphics is allowed. Without
  it, all agent-driven development runs into a human at the editor;
- `com.unity.editor.platforms.ios` — building for iOS is included in the
  license.

Related commands: `--showAllEntitlements` (everything the machine knows),
`--showContext` (which account).

**Two mistakes that have already been made here — don't repeat them.** The
license file sits at `/Library/Application Support/Unity/Unity_lic.ulf` —
system-wide, **not** in `~/Library/...`; checking the home directory gives a
false "no license" answer. And "Unity launched in batch mode" is indirect
proof: it shows that entitlements exist *now and for this action*, but not
which ones exactly and until when.

Other places to dig if needed: `~/Library/Unity/licenses/UnityEntitlementLicense.xml`
(the license assigned to the account), `~/Library/Application Support/UnityHub/`
(Hub state), `~/Library/Logs/Unity/` (logs with `[Licensing::…]` lines).

## How you control Unity: command line now, MCP after 1.4

**Right now, and by default — two methods, neither uses MCP.**

Direct file editing: C# code, level descriptions in JSON, interface in UXML
and USS — all of this is plain text. And Unity from the command line:

```bash
UNITY="/Applications/Unity/Hub/Editor/6000.3.22f1/Unity.app/Contents/MacOS/Unity"

# build: Xcode project for a real device
"$UNITY" -batchmode -nographics -quit -projectPath game \
         -executeMethod BuildScript.BuildIOSXcodeProject -logFile build.log

# build: Xcode project for the simulator — a separate SDK, a separate folder
"$UNITY" -batchmode -nographics -quit -projectPath game \
         -executeMethod BuildScript.BuildIOSSimulatorProject -logFile build.log

# tests — NOT through Unity, see the warning below
dotnet test build/core-tests/core-tests.csproj   # 54 tests over Assets/Core
python -m pytest tools/tests -q                  # 67 tests over tools/
```

Unity only generates the Xcode project; compiling it, installing it into the
simulator and taking the screenshot is `xcodebuild` plus `xcrun simctl`. The
full verified sequence is in `cat-shelter-tech.md`, section 5 — use it whenever
a claim about the game needs a picture behind it.

**Never verify anything with `Unity -runTests`.** On this project it reports
success while running nothing at all:

```
total="0" passed="0" failed="0" result="Passed"
Test run completed. Exiting with code 0 (Ok). No tests were executed.
```

Exit code is 0. `CatShelter.Core.Tests.dll` compiles without errors and lands in
`Library/ScriptAssemblies`, but the runner does not pick it up — reproduced on
2026-08-26 with a cleared `Temp`, with `-assemblyNames CatShelter.Core.Tests`,
with `defineConstraints` emptied and with `overrideReferences` disabled. **The
cause is not established.** Until someone finds it, the two commands above are
the only evidence that the code works; a green Unity test run is evidence of
nothing.

This isn't a workaround, it's the design: data in JSON and ScriptableObject,
interface in UXML/USS, scenes assembled by code — **precisely so as not to
depend on MCP.** Unity scenes are machine-generated YAML with identifiers, the
agent breaks them, and the architecture is built to barely touch them. Don't
destroy this by getting MCP.

**Iteration cost measured on this machine:** creating an empty project takes
6 seconds, reopening 2–3. The argument "batch mode is too slow" wasn't
confirmed. The measurement is on an empty project — with levels and packages
it will be slower; measure again once the project exists, and record it here.

**MCP: `CoplayDev/unity-mcp`, connect after task 1.4.**

The official one (`com.unity.ai.assistant`) is out — it requires Unity Cloud
and a paid subscription, and our rule is not to pay. Among third-party
options, CoplayDev was chosen: MIT, 13,643 stars, active (checked 25.08.2026).
Fallback — `IvanMurzak/Unity-MCP`.

Its benefit is **not speed**, but reading the Unity console: compile and
runtime errors arrive parsed, instead of being fished out of `Editor.log`.
Plus scenes and play mode.

Limitation: MCP lives inside an **open editor with a window**, meaning it
requires someone to keep Unity running. Build and CI stay on batch mode —
don't substitute one for the other.

Connecting it before 1.4 is pointless: there's no project, nothing to talk to.

## Task 1.4: there's no Unity project yet, and there's a collision

`game/` contains `Assets/` and `Tests/`, but neither `ProjectSettings/` nor
`Packages/manifest.json` — Unity doesn't consider this directory a project.
The rules core and the solver have lived as an ordinary .NET project until
now, and that was correct, but it can't continue that way.

**Collision you'll run into.** The file
`game/Assets/Core/CatShelter.Core.csproj` will end up **inside** the Assets
folder once Unity opens `game/` as a project. It's referenced by
`game/Tests/Core/CatShelter.Core.Tests.csproj` via the line
`<ProjectReference Include="../../Assets/Core/CatShelter.Core.csproj" />`.
That means two build systems will sit over the same source: Unity with its
generated `.csproj` files, and our `dotnet test`.

This doesn't break by itself — Unity will just mark the foreign `.csproj` as
an unknown asset and create a `.meta` for it. But there will be confusion, and
it needs to be resolved deliberately. Two paths:

1. **Move plain C# out of Assets.** The core lives in `core/` next to `game/`,
   and enters Unity as a compiled library or via a source reference. Cleaner,
   but adds a build step.
2. **Leave it as is, only removing the `.csproj` from Assets.** The core
   remains as source in `Assets/Core`, built inside Unity via `.asmdef`, and
   for `dotnet test` a separate project is set up **outside** Assets, pulling
   in the same files via `<Compile Include="../../game/Assets/Core/**/*.cs" />`.

The second path is shorter and preserves the current test run. The choice is
yours, but **choose explicitly and write down why** — silently leaving two
`.csproj` files over the same code is not acceptable.

On `.meta`: on first opening, Unity will create them for everything under
`Assets`, including 37 level descriptions. They need to be committed to git
together with the project, or references will shift for everyone else.

## Known traps in this project

- **7.0**: GameAnalytics declares a tracking domain in its privacy manifest,
  and Apple blocks such domains without ATT permission. Verify on a real
  device that events arrive without the dialog, **before** wiring up
  measurement collection. The failure is silent: events simply won't happen.
- **4.4**: six cat silhouettes is the one task the agent can fail completely.
  Acceptance is deliberately handed to an outside person. Two attempts, then
  hire an artist.
- Thresholds in M8 aren't finalized yet: the recorded "day-1 return > 35%" is
  roughly twice the genre median. The decision is made by a human in task 8.0,
  before spending money.

## How to check a claim — added 2026-08-27, after ten of them fell in a day

Thirty-one tasks marked done were checked against what they claimed. Ten
failed. Not one was a broken feature: every failure was a document claiming
more than could be shown, and in seven of the ten the code underneath was
sound. The full record is `tasks/AUDIT-2026-08-27.md`. Three shapes recurred,
and they are what to look for first.

**A number nobody counted.** "48 plist keys" when there are 28. "No
advertising strings in the APK" when `classes.dex` has four. "18 of 20 cats"
from a program that was not in the repository. Every one was written by
somebody who had done the work and believed it. The cure is not more care, it
is re-deriving the number from the artefact rather than from the sentence
about it — open the binary, count the keys, run the script.

**A constant living in two places.** The pacing curve in Python and in C#. The
traits schema in six places, two unguarded. The colour palette in Swift and in
`CatTraits`. Each pair agreed the day it was written and nothing compared them
after. When you write a value that already exists somewhere else, write the
comparison in the same commit or expect the drift.

**A guard that cannot fail.** The worst of the three, because it reads as
safety. A coverage gate no script invoked. Cross-language checks that called
`Assert.Ignore` when a path did not resolve. An analytics guard that counted a
commented-out call as a call site. And the largest: the conformance suite
between the two rule engines compared three scalars, so a decision could be
reverted in one engine and not the other with all four tests still green.

### The method that found all of them

**Mutate the thing the guard is supposed to catch, and see whether it
screams.** On a copy outside the repository, never on the tree. Break the
invariant, run the check, and read the failure. If nothing fails, the guard is
decoration and you have found something.

Reading a test tells you what it was meant to do. Only breaking the code tells
you what it does.

Corollaries worth stating, because each cost something today:

- A check that skips itself when it cannot find its input has stopped running.
  Make it **fail** instead — a missing file is a finding.
- A test that passes is not evidence that its name or its comment is true. Two
  tests were found today whose names described states they never reached.
- A build that succeeds is not evidence its output is right. An Android build
  reported success for months while silently omitting every manifest entry its
  packages were supposed to inject.
- `verify:` is set by a context that wrote neither the code nor the tests, and
  the reason is not procedural. It is the only reason those ten claims fell
  instead of standing.

## When to stop and speak up

- work runs into a HUMAN task — say plainly which one is needed
- a task's acceptance is unreachable and the task itself needs to change,
  rather than tailoring the result
- a primary source contradicts what's recorded in the project documents —
  show the discrepancy, don't silently choose
</content>

## Where tasks live now

`cat-shelter-tasks.md` no longer exists. Tasks are a directory tree `tasks/`,
modeled on `hft/task_manager`.

You don't need to read everything, only your own:

1. `tasks/README.md` — format, labels, the `verify:passed` rule.
2. `tasks/GOAL.md` — the goal and the three gates.
3. `tasks/<epic>/<task>/task.txt` — a single task, up to forty lines.
4. Whatever is listed in its `CONTEXT` section, and nothing beyond that.

`tasks/DECISIONS.md` — cross-cutting decisions. Open it when you're about to
challenge something already decided, not before every task.

The previous flat list required 20 thousand tokens for the sake of one table
row: 154 lines of tasks for 962 lines of explanations. Now a single task costs
about a thousand tokens.

## Run it on both platforms, and read the file it leaves

Added 2026-08-28 after the game was found to be **completely blank on iOS** —
every screen that built a coat drew nothing, and had done since the coat shader
landed, because nobody had run it there. Android was checked; iOS was assumed.
Two platforms means two runs, and the second is not optional.

Three files beside the save now answer "why is the screen wrong" without a
console, because Unity's `Debug.Log` reaches neither a device nor a simulator:

| file | written by | says |
|---|---|---|
| `errors.txt` | `Shell/DeviceLog` | every error and exception, including the ones Unity catches in `Awake`/`OnEnable` and only logs |
| `boot-state.txt` | `GameBoot.BootState` | post-layout sizes of root/`game-root`/`pile`/first tile, and whether the coat read went through the GPU. **Written by the board branch only** so far — a flag-file screen leaves no boot-state, and extending it to the other branches is a loose end |
| `screen-failure.txt` | `GameBoot.SafeBuild` | a screen that threw where it was built, with the stack |

Read them before theorising. The iOS blank screen produced no exception at all
and every size and colour measured correct; the cause was found by a controlled
experiment (`nocat.txt`, which skips the coat) and not by reasoning.

Pulling them:

```bash
# iOS simulator
D=$(xcrun simctl get_app_container booted com.DefaultCompany.game data)
cat "$D/Documents/boot-state.txt"

# Android
ADB=/Applications/Unity/Hub/Editor/6000.3.22f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb
"$ADB" exec-out run-as com.DefaultCompany.game cat files/boot-state.txt
```

Two things that cost a build each and are easy to repeat:

- **The Android entry point is `BuildScript.BuildAndroidPlayer`**, not
  `BuildAndroid` — the latter takes arguments and Unity refuses it with
  "Only methods with 0 arguments are supported", exit code 0, no APK.
- **`xcodebuild` needs an absolute `-project` path.** Run from the wrong
  working directory it builds the device project into the simulator's
  DerivedData, reports `** BUILD SUCCEEDED **`, and produces no `game.app`
  where you expect one.
