# Built and delivered, 2026-08-27

The Android half of `Shell/EveningReminder.cs` exists, and a notification has
been **seen arriving** on the emulator `catshelter-a35` (API 35).

This is the first time in this project that a notification has been proven to
be delivered on any platform. The iOS side has been `status:done` since
26.08 with `verify:pending`, because a simulator schedules nothing and there
is no phone — so the mechanism the whole of metric 3 rests on had never once
been observed working. It has now.

## The three VERIFY items

| # | Item | Evidence |
|---|---|---|
| 1 | No permission dialog before level 2 | `android-no-dialog-before-asking.png` — the board, no dialog. `reminder-state.txt` reads *"launch: permission not asked yet, nothing to schedule"*, and `dumpsys package` shows `POST_NOTIFICATIONS: granted=false` — known to the system, never asked for |
| 2 | Delivered while backgrounded, via the debug hook | `android-permission-dialog.png` then `android-notification-delivered.png` — the shade holds *"Your kitten found something behind the couch / It is waiting to show you, whenever you have a minute"*, the exact strings from `Copy.cs`, with the app sent to the background by HOME before it fired |
| 3 | Reopening moves the pending reminder, does not add a second | `dumpsys alarm`: one pending `RTC_WAKEUP` tagged `UnityNotificationManager`, and a history record `Reason=pi_cancelled` for the one it replaced |

`reminder-state.txt` across the run, verbatim:

```
22:12:33 launch: permission not asked yet, nothing to schedule
22:13:11 launch: permission not asked yet, nothing to schedule
22:14:09 permission answered: status=Allowed
22:14:09 scheduled id=1 on 'catshelter-evening' for 2026-08-27 22:14:44, repeat=once, permission=Allowed, debugDelay=35
22:16:17 scheduled id=1 on 'catshelter-evening' for 2026-08-27 22:21:17, repeat=once, permission=Allowed, debugDelay=300
22:16:31 scheduled id=1 on 'catshelter-evening' for 2026-08-27 22:21:31, repeat=once, permission=Allowed, debugDelay=300
```

Same id, time moved twice. That is what item 3 asks for, said by the app.

## A metric that nearly produced a false failure

The first count of item 3 was `dumpsys alarm | grep -c UnityNotificationManager`,
which went 1 → 2 across two launches and looked exactly like "a second alarm
was added". It was not. Of the two matching lines, one is the pending alarm and
the other sits inside a history block that opens with `Reason=pi_cancelled` —
the record of the *cancellation*. The metric counted the cancellation as an
alarm.

Recorded because the wrong reading was one sentence away from being reported,
and it would have been reported as a defect in working code. A count is not
evidence until you have looked at what it counted.

## What is written, and the two decisions inside it

`Available` now returns true for Android as well as iOS. Everything else lives
behind `#if UNITY_ANDROID && !UNITY_EDITOR`, mirroring the iOS branch:

- **Permission.** `new PermissionRequest()` and poll until `Status` leaves
  `RequestPending`. Below API 33 it completes immediately as `Allowed`, so
  there is no version branch. The package remembers a refusal itself and will
  not re-prompt.
- **One channel, `Importance.Default`.** Not `High`: this is a quiet evening
  nudge, and `High` gives heads-up display and sound — the urgency the tone
  rule in cat-shelter-mvp.md section 4 forbids. The channel's name and
  description are in `Copy.cs` like everything else the player reads, because
  Android shows them in system Settings.
- **Integer id where iOS uses a string.** Android replaces a pending
  notification by id, so `SendNotificationWithExplicitID` with a fixed `1` is
  the exact equivalent of iOS replacing by identifier.
- **No exact alarms.** `SCHEDULE_EXACT_ALARM` is not requested, per this task's
  SCOPE: an evening reminder does not need to be exact, and it is a review
  question nobody wants to answer.
- **The next occurrence of 19:00** is computed here, because Android has no
  equivalent of the iOS calendar trigger with the date left unset. Today if
  19:00 is still ahead, otherwise tomorrow, repeating daily.

## What is not proven

Delivery on real hardware; an emulator is not a phone. The daily repeat was
not observed repeating — a 24-hour wait was not made, and the debug hook fires
once by design. And the after-level-2 path was exercised through
`DebugRequestNow`, which asks at the same moment by the same code, rather than
by clearing two levels by hand.
