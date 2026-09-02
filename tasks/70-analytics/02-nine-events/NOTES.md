
# Call sites, 2026-08-27

All nine now fire from the gameplay flow. Eight were already in place from
earlier tasks; two things were wrong with them.

| event | where it fires |
|---|---|
| `app:open` | `GameBoot.Awake` — **added**, was missing entirely |
| `photo:screen_shown` | `CaptureScreen.Build` — **moved**, see below |
| `photo:uploaded` | `CaptureScreen.Handle`, on the accepted branch |
| `photo:rejected` | `CaptureScreen.Handle`, on each refused branch |
| `booster:tap` | `DebugGameView.Finish`, the fake door (D4) |
| `notification:allowed` | `EveningReminder`, on a granted permission |
| `level_start` | `DebugGameView.StartLevel` and on a resumed save |
| `level_win` / `level_fail` | `DebugGameView.Finish` |

## The one that was in the wrong place

`photo:screen_shown` fired inside `Handle`, i.e. **after a photo had been
picked and run through Vision**. Metric one is "reached the capture screen,
threshold >90%" and metric two is "uploaded a photo, threshold >40%" — firing
the first event at the second event's moment would have made metric one read
like metric two, and the >90% gate would have failed on a screen that was
working perfectly.

It now fires in `Build`, when the screen goes up. A test asserts the call sits
between `Build(` and `Handle(` in the file, so it cannot drift back.

## Checked

`tools/tests/test_analytics_call_sites.py`, 13 cases: every event has a caller,
the declared surface is exactly nine (D9 — a tenth is not a bigger funnel, it
is one nobody agreed to read), progression events always carry a level number,
and `app:open` is at launch rather than in the photo flow.

C# cannot check this: `View` and `Shell` are not compiled by
`build/core-tests`, which is why the check reads the sources instead.

## Left open

Both VERIFY items name the **GameAnalytics dashboard**, which needs
`01-sdk-integration`, an account and a game key. Nothing here proves an event
left the device — only that it is raised at the right moment by the right
caller.

One naming caveat for whoever wires the SDK: `photo:uploaded` currently fires
when a photo is *accepted and cropped*, not when it reaches a server, because
there is no server yet (`02-traits-worker`). Once the Worker exists, decide
deliberately whether the event stays at acceptance or moves to a successful
response — the metric-two threshold of 40% means different things either way.

## Observed on a device, 2026-09-02 — the "Left open" gap above is closed

There is still no GameAnalytics account (`01-sdk-integration` is the owner's
own step), so the dashboard cannot be watched. What can be watched without
one: whether each event actually fires, once, at the right moment, in a real
playthrough. Nobody had ever looked.

**`game/Assets/Shell/AnalyticsDebugSink.cs`** (new): drop an `analytics.txt`
file beside the save — presence-only, same convention as `board.txt`/
`coat.txt` — and `GameBoot.Awake` wraps whatever sink
`GameAnalyticsSink.TryConfigure` produced (today, always `(null, null)`, no
keys) with one that also writes every `Analytics.Design`/`Progression` call
to the device log and to `analytics-log.txt` beside the save: event name,
value/level, and seconds since launch (`Time.realtimeSinceStartup`), one line
per call. No file, no change — `Wrap` returns `inner` unchanged. Wired in
right after `GameAnalyticsSink.TryConfigure` in `GameBoot.cs`, so a device
that somehow has both `analytics-keys.txt` and `analytics.txt` still sends
real events and logs them.

Build: `game/build/android/CatShelter.apk`, `Unity.BuildScript.
BuildAndroidPlayer`, `result=Succeeded`. Device: `emulator-5554`, package
`com.sootpaw.game`.

### The run

Real path, one launch, `analytics.txt` + `capture.txt` (pointed at
`tmp/IMG20260829212451.jpg`, stub `fake Cat 0.95`) present:

| event | `analytics-log.txt` line | how it was reached |
|---|---|---|
| `app:open` | `t=2.106s design app:open` | app launch, first line of `GameBoot.Awake` |
| `photo:screen_shown` | `t=2.237s design photo:screen_shown` | first-run branch builds the capture screen; fires in `Build`, before any photo is picked |
| `photo:uploaded` | `t=2.468s design photo:uploaded` | `capture.txt`'s named photo run through the **real** crop pipeline (`capture-state.txt`: `vision said Cat at 0.95 -> Cat`, `accepted a 58165-byte photo`, `cat ready (OfflineColourOnly)`) — the crop succeeded, so this is the accepted branch, not the rejected one |
| (named + house) | — | tapped the name field, typed a name, tapped "That's the one" -> house map shown; tapped room 1 |
| `level_start` | `t=81.951s progression level_start level=1` | `DebugGameView.StartLevel`, tapping into room 1 from the house map — the real map-tap path, not `board.txt` |

Then, separately (see "Reaching win/fail" below), on the **same level**,
resumed mid-level twice: `level_start` fired again both times from
`DebugGameView.Resume()` (`t=2.262s progression level_start level=1` in each
of the two logs below), followed by:

| event | line | outcome |
|---|---|---|
| `level_win` | `t=23.816s progression level_win level=1` | tapped the one remaining tile of a crafted near-win save; win card shown ("The room is clean", before/after room 01 art) |
| `level_fail` | `t=34.212s progression level_fail level=1` | tapped a reachable tile of a crafted near-jam save; "Shelf jammed" card shown |

And, in a fourth launch, permission pre-granted via
`pm grant ... POST_NOTIFICATIONS` and `notify-in-seconds.txt` (3s) present:

| event | line |
|---|---|
| `notification:allowed` | `t=2.229s design notification:allowed` |

### Reaching win/fail without playing 36 tiles by hand

Level 1 (`l01_room01_pile0.json`) is 36 items across 6 kinds — a real
matching playthrough by tapping alone. Reused the technique
`60-shell-build/06-win-screen/NOTES.md` already used for this exact level:
craft a `board.save` one tap from the outcome, using
`tools/solver/rules.RulesState` (the mirror of `Board.cs`, conformance-tested
against it) to compute the exact `taken`/`shelf`/`triples` a legal replay
produces, then let the device's own `SaveResume.TryResume` -> `Board.TakeItem`
replay validate it (it re-derives the shelf and throws `"snapshot corrupt"` on
any mismatch — caught one ordering bug this way, see below).

- **Near-win**: `tools/solver/solver.solve()`'s 36-move winning order, all but
  the last move replayed into the save. One tap on the remaining tile (a
  book) completes the last triple and wins.
- **Near-jam**: 8 items taken (2 board, 2 plate, 2 frame, 1 crate, 1 box —
  every kind under 3, shelf capacity 9), one more reachable book untaken.
  Tapping any reachable book fills the 9th slot without a triple while 27
  items are still on the pile — an immediate jam by the same rule
  `rules.RulesState.take` encodes (checked against the C# side by resuming
  the save on device, not just in the Python mirror).

**Bug found and fixed while crafting the first save**: the script's first
draft wrote `taken` from a Python `set` (`st.taken`), not the actual move
order. A `set`'s iteration order is not guaranteed, so the file sometimes
listed an item before one of its own `blocked_by` dependencies —
`BoardSave.Restore` correctly rejected it (`"snapshot corrupt: cannot retake
item 13"`, logcat) and `DebugGameView.Resume` correctly fell back to a fresh
board rather than resuming a corrupt position. Fixed by writing the ordered
move list instead. Not a product bug — a bug in the throwaway crafting
script, caught by the same validation `BoardSave.Restore`'s own doc comment
describes.

### `booster:tap` — confirmed dormant, not missed

Grepped the same way `test_analytics_call_sites.py` does
(`Analytics\.BoosterTap\s*\(`, comments stripped): zero matches anywhere
under `Assets/View` or `Assets/Shell`. The only text mentioning it is the
comment in `DebugGameView.cs` next to the lose card explaining why the D4
fake-door button was removed on 2026-08-27. Nothing to observe on a device
because there is nothing left in the live path that could call it — matches
`DORMANT` in the test file exactly.

### What this run does and does not prove

Confirms, by direct observation rather than by reading the source: all eight
non-dormant events fire, each exactly once per occasion, at the point in the
flow their names promise — including `notification:allowed` and
`level_fail`, both of which this task allowed for "not observed" (a system
permission dialog and a genuine jam are awkward to reach). Does **not**
prove anything about GameAnalytics itself — no key, no dashboard, no network
call inspected. That gap is `01-sdk-integration`'s, unchanged by this run.

Cleanup: `pm clear com.sootpaw.game` after the run — no `analytics.txt`,
`board.txt`, `board.save`, `capture.txt`, `notify-in-seconds.txt` or other
harness file was left on the device.

### Tests added

`tools/tests/test_analytics_call_sites.py`, five new cases pinning what this
run confirmed statically: `photo:uploaded` and `level_win`/`level_fail` each
have exactly one call site; `photo:uploaded` sits after the crop-failure
check (so it cannot fire on a rejected photo); `level_start` has exactly two
call sites (`StartLevel`, `Resume`). `279 C#` (unchanged) / `261 python`
(was 256) / `build/check-core-purity.sh` clean.
