
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
