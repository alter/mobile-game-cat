# 21-analytics-never-throws

Replaced the two throwing paths in `Analytics.Design`/`Analytics.Progression`
(unbidden Configure, levelNumber outside 1..999) with warn-and-return. Added
`Analytics.WarnSink` (`Action<string>`, nullable, silent no-op by default) —
Core has no UnityEngine, so the diagnostic sink is injected the same way as
the existing design/progression sinks; wiring it to `Debug.LogWarning` is a
Shell-side job outside this task's scope.

Out-of-range level numbers are **dropped, not clamped** — clamping 1000 to
999 would misreport which level fired the event, which is worse than losing
the sample.

`EnsureValid(name)` is untouched: still throws on unknown/malformed names,
as SCOPE requires (typo trap).

Added `internal static Analytics.ResetForTests()` so a test can exercise the
pre-Configure state — Core and Tests compile into one assembly via
build/core-tests/core-tests.csproj, so `internal` is enough (no
InternalsVisibleTo needed).

New tests in AnalyticsTests.cs: before-Configure for both Design and
Progression, and level-number boundaries 0/1/999/1000.
