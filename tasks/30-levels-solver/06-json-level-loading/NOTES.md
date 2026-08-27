# status:in_progress → done, 2026-08-26

The label lagged the repository. Against this task's VERIFY list:

- `HeadlessRunTests` green via `dotnet test build/core-tests/core-tests.csproj`.
- The stale `Enumerable.Range(1, 12)` is gone: the tests assert 37 files exist
  and parse, and `AllThirtySevenLevelsPlayThroughToWin_Headless` plays every one
  of them to a win under greedy play.
- `CorruptedJsonFailsLoudly` still throws.

Runtime loading is `View/LevelAssets.cs` (Resources + Newtonsoft, `JToken` rather
than `dynamic`, which IL2CPP strips); the editor-time loader used by tests is
`Tests/Core/LevelLoader.cs`.

`verify` stays `pending` — the checker here also edited Core.

## Fix, 2026-08-27 — landed after `verify:passed`

An independent verifier set `verify:passed` on 2026-08-27 and, in the same
pass, found that `LevelAssets.LoadAll()` had no guard: a malformed or missing
shipped file threw straight out of `DebugGameView.OnEnable()`, before `_plan`
and `_progress` ever existed, leaving the player on a blank screen — the
opposite of `Core/SaveResume`'s "a crash on launch loses the player." **That
verdict predates this fix**; it did not check the fix below.

Given `SaveResume`'s shape (decide-in-Core, catch-in-View):

- `Core/LevelLoadPolicy.cs` (new): decides what a room's worth of parsed
  levels is safe to hand to `RoomPlan`. `RoomPlan` requires a gapless run of
  pile indices from 0, so dropping only the one bad pile out of a room's
  middle would leave a gap and crash one call deeper. A bad file costs its
  whole room instead — every other room stays untouched. Only when nothing
  survives at all does `CanStart` come back false. Engine-free, tested by
  `Tests/Core/LevelLoadPolicyTests.cs` (8 tests, `dotnet test`-reachable).
- `View/LevelAssets.LoadAll()` now catches exactly the three exception
  shapes the mutation test in this task's own `VERIFY.md` found —
  `JsonReaderException`, `NullReferenceException`, `ArgumentException`
  (which also covers `ArgumentNullException`/`ArgumentOutOfRangeException`,
  both subclasses) — logs each with `Debug.LogError`, and moves on to the
  next file instead of throwing. It hands the result to `LevelLoadPolicy`.
- `DebugGameView.OnEnable()`: when `CanStart` is false, shows
  `Shell.Copy.Of("levels.unavailable.title"/"levels.unavailable.body")` via
  the existing `ShowCard` (no buttons — there is nothing to retry into) and
  returns before touching `RoomPlan`. Both keys are new in `Shell/Copy.cs`,
  so `tools/tests/test_copy_table.py` still passes rather than catching a
  literal.

This should not fire in practice — `test_ship_levels.py` and
`HeadlessRunTests` both gate shipped level data before release. It is the
floor under that gate, not a path anyone expects to exercise.

`dotnet test build/core-tests/core-tests.csproj -v q --nologo` → 169 passed
(was 156; +13, of which 8 are `LevelLoadPolicyTests`, the rest concurrent
work). `.venv/bin/python -m pytest tools/ -q` → 156 passed (was 155).
`build/check-core-purity.sh` → `Core is engine-free: OK`. No Unity build was
run for this fix (out of scope; another agent is building separately) — the
blank-screen diagnosis and this fix's correctness at the View layer rest on
reading `DebugGameView.cs`/`LevelAssets.cs` and on Unity's documented
MonoBehaviour exception-handling behaviour, not on an observed build.
