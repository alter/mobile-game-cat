Verifier: independent QA context, wrote none of `game/Assets/View/LevelAssets.cs`,
`game/Assets/Core/Level.cs`, `game/Assets/Core/Item.cs`,
`game/Assets/Tests/Core/LevelLoader.cs`, `game/Assets/Tests/Core/HeadlessRunTests.cs`,
`game/Assets/View/DebugGameView.cs`, `tasks/DECISIONS.md` D13, or this task's own
`task.txt`/`NOTES.md`. Built a scratch harness outside the repo that
re-implements `LevelAssets.Parse` verbatim to empirically test nine corruption
shapes against a real shipped level, rather than reasoning about Newtonsoft's
null-handling from memory. Did **not** run a Unity build, PlayMode test, the
Android emulator, or adb — the coordinator is running the Unity build
separately; this check is code-level and `dotnet test`-level only, and says so
plainly wherever it depends on Unity's own exception-handling behaviour rather
than something observed directly.

## Verdict by item

| # | Question | Verdict | Evidence |
|---|---|---|---|
| 1 | Task's VERIFY items | **Pass** | VERIFY 1: `dotnet test build/core-tests/core-tests.csproj -v q --nologo --filter FullyQualifiedName~HeadlessRunTests` → `5 passed`. VERIFY 2: `LevelAssets.LoadAll()` no longer loops `Enumerable.Range(1,12)` — it iterates 12 rooms × `PilesPerRoom[room-1]` piles (sums to 37, the actual `pacing.py`-mirroring array), and `Resources.Load<TextAsset>` returning null throws `InvalidOperationException($"missing level asset {name}")` for any of the 37 — file existence is asserted for all of them, not a stale count. `HeadlessRunTests.AllShippedLevelFilesExistAndParse` independently asserts `levels.Count == 37`. VERIFY 3: `CorruptedJsonFailsLoudly` still throws — confirmed both by the passing test and directly, below. |
| 2 | What happens to a malformed shipped file at runtime? | **Nothing catches it — it propagates all the way out of `DebugGameView.OnEnable()`, unlike `SaveResume`'s deliberate fallback** | Traced the only runtime call site: `DebugGameView.OnEnable()` (`game/Assets/View/DebugGameView.cs:87`) calls `LevelAssets.LoadAll().OrderBy(...)` with no try/catch, before `_plan`/`_progress` are ever assigned. `LevelAssets.LoadAll()`/`Parse()` (`game/Assets/View/LevelAssets.cs`) have no try/catch either — a `JsonReaderException` from malformed JSON, or any of `Level`'s five constructor checks (kind-count%3, duplicate id, self-block, dangling blocker, cycle — the last three added 2026-08-27) throwing `ArgumentException`/`ArgumentOutOfRangeException`/`ArgumentNullException`, reaches `OnEnable()` uncaught. Compare `DebugGameView.Resume()` (lines ~96-107), which explicitly wraps `SaveResume.TryResume` and falls back to a fresh board with the doc comment "a launch crash loses the player" — that design exists for the save file and was never extended to level loading. **What a player would see**: Unity's own script-message dispatcher catches an exception thrown from a `MonoBehaviour` lifecycle callback (well-documented Unity behaviour: it is logged via `Debug.LogException` and the engine loop continues rather than terminating the process) — so the app most likely does not hard-crash. But `OnEnable()` aborts at the point of the throw, before `_plan`, `_progress`, `_board`, `_level` are ever set and before `StartLevel`/`Resume` ever run. The screen is left showing whatever the bare UXML skeleton already rendered (empty pile/shelf containers, unset title/status) — a blank, unresponsive screen with no error message, no retry, and no fallback to a working level. This is inferred from Unity's documented exception-handling model, not observed in a running build (no Unity build was run here); flagged accordingly under "what was not checked." |
| 3 | Newtonsoft under IL2CPP | **The actual code uses the IL2CPP-safe pattern; a second, independent safety net exists too** | `LevelAssets.cs`'s own doc comment: "JToken instead of dynamic: dynamic needs Microsoft.CSharp which IL2CPP strips." Confirmed no `dynamic` anywhere in `game/Assets` (`grep -rn "dynamic " game/Assets --include="*.cs"` → 0 real usages, only this comment). Confirmed no `JsonConvert.DeserializeObject<T>()` or `[JsonProperty]`/reflection-based POCO deserialization anywhere — the loader manually walks `JObject`/`JArray`/`JToken.Value<T>()`, which are the package's own directly-referenced types (not activated by reflection at a call site IL2CPP's static analysis can't see), the standard safe pattern for Newtonsoft under IL2CPP. `game/Packages/manifest.json:52` confirms `"com.unity.nuget.newtonsoft-json": "3.2.1"` is actually the package in use, matching the task's SCOPE claim. As a second, independent safety net: `game/Library/PackageCache/com.unity.nuget.newtonsoft-json@.../link.xml` ships with the package itself and instructs IL2CPP's stripper to preserve the whole Newtonsoft.Json assembly regardless of static reachability — this is Unity's own standard fix for exactly this class of risk, present here without anyone in this repo having to add it by hand. Also confirmed `tasks/DECISIONS.md` D13 states the reasoning (`System.Text.Json` needs `Reflection.Emit`, unavailable on iOS under IL2CPP) and that `Core/GameSave.cs` deliberately avoids Newtonsoft entirely rather than leaking it into `Core`; `bash build/check-core-purity.sh` → `Core is engine-free: OK`, confirming no Newtonsoft import ever reached `Core` (the two "Newtonsoft" hits inside `Core/GameSave.cs` and `Core/TraitsRequest.cs` are doc-comment mentions only, not `using` statements — checked directly). |
| 4 | Mutation: corrupt a shipped level several ways, on copies outside the repo | **All nine tried, all failed loudly — zero silent wrong levels** | Built a scratch program outside the repo that copies `Level.cs`/`Item.cs` verbatim and re-implements `LevelAssets.Parse` line-for-line, then ran it against nine corruptions of a real shipped file (`l01_room01_pile0.json`, copied read-only): (1) truncated/invalid JSON syntax → `Newtonsoft.Json.JsonReaderException: Unterminated string...`; (2) `room_id` removed → `ArgumentNullException (Parameter 'roomId')`; (3) `number` removed → `ArgumentOutOfRangeException (Parameter 'number')` (Newtonsoft's `Value<int>` on a missing key silently defaults to `0`, but `Level`'s `number < 1` check catches it); (4) the whole `pile` array removed → `NullReferenceException`; (5) a `blocked_by` id pointing at a nonexistent item → `ArgumentException: item 11: blocked_by 99999 does not exist`; (6) the `blocked_by` key entirely absent (not empty) on one item → `ArgumentNullException (Parameter 'source')` (LINQ's `.Select` on a null `JToken`); (7) a duplicated item id → `ArgumentException: duplicate item id 1`; (8) an item blocking itself → `ArgumentException: item 11 blocks itself`; (9) `kind` removed on one item → `ArgumentNullException (Parameter 'id')` from `ItemKind`'s constructor (misleading parameter name — the null value was actually `kind` — a `nameof` bug worth a note, not a correctness bug). Every case threw; none produced a `Level` object silently. The failure *types* vary (`JsonReaderException`, `NullReferenceException`, three flavours of `ArgumentException`) rather than one uniform exception — worth knowing for anyone who later wants to catch-and-fallback the way `SaveResume` does, since a single `catch (ArgumentException)` would miss cases 1, 4 and 6. |

## How to reproduce

From a clean checkout of `dev`, no exported variables:

```sh
cd game && git worktree add /tmp/verify-check-5 dev
cd /tmp/verify-check-5
dotnet test build/core-tests/core-tests.csproj -v q --nologo --filter "FullyQualifiedName~HeadlessRunTests"  # -> 5 passed
dotnet test build/core-tests/core-tests.csproj -v q --nologo   # -> 156 passed
bash build/check-core-purity.sh                                # -> Core is engine-free: OK
grep -n "dynamic \|JsonConvert.DeserializeObject" game/Assets/View/LevelAssets.cs game/Assets/Tests/Core/LevelLoader.cs   # -> no matches
grep -n "com.unity.nuget.newtonsoft-json" game/Packages/manifest.json
grep -n "Newtonsoft" game/Assets/Core/GameSave.cs game/Assets/Core/TraitsRequest.cs   # -> comment mentions only, no `using`
```

Mutation test (outside the repository — do not apply to the repo's own files):

```sh
SP=$(mktemp -d)/json-loading-mutation
mkdir -p "$SP/Core"
cp game/Assets/Core/Level.cs game/Assets/Core/Item.cs "$SP/Core/"
cp game/Assets/Resources/Levels/l01_room01_pile0.json "$SP/sample.json"
cd "$SP"
# write a Program.cs that re-implements View/LevelAssets.Parse verbatim
# (JObject.Parse -> Value<int>/Value<string> -> foreach pile -> new Level(...))
# and feed it: truncated JSON, a field removed (room_id/number/pile/kind/
# blocked_by), a dangling blocker, a duplicate id, a self-block.
dotnet run -c Release
# -> every case above throws; none silently succeeds
git status --short   # repo untouched
```

## What was not checked

- No Unity build, PlayMode test, Android emulator, or adb. Item 2's claim
  about what a player sees ("blank, unresponsive screen, no crash") is
  derived from Unity's documented exception-handling model for
  `MonoBehaviour` lifecycle callbacks (an uncaught exception is logged and
  the engine loop continues), not observed by actually launching the game
  with a corrupted level file — the coordinator is running the Unity build
  separately, and this verification did not have one available.
- Did not check IL2CPP stripping empirically (would require an actual IL2CPP
  build, out of scope per constraints) — the finding for item 3 is a code
  pattern match against the documented risk (`dynamic`/reflection-based
  deserialization) plus confirmation that the package's own `link.xml`
  preserves the assembly regardless; it is not a build-verified guarantee.
- Did not mutation-test every VERIFY item mechanically (e.g., did not corrupt
  `pile_index` or `locked_after_triples` specifically) — the nine corruptions
  chosen cover JSON-syntax failure, every top-level field, one nested field,
  and the three validity rules `Level.cs` gained on 2026-08-27, which is what
  the coordinator's brief named.
- Did not check whether `View/LevelAssets.cs` or `DebugGameView.cs` should be
  changed to catch-and-fallback the way `SaveResume` does — that is a design
  decision for whoever owns those files, not something this verifier fixes.
  Flagged in item 2/4 as a real gap, not acted on.
- Did not re-derive or re-litigate `tasks/DECISIONS.md` D13's reasoning about
  `System.Text.Json`/`Reflection.Emit`/iOS; took it as a given, correctly
  reflected in the actual `Core/GameSave.cs` design.
