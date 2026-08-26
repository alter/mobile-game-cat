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
