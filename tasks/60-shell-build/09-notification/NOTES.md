# Notes - why this is P0, and a debunked number to not repeat

Source: cat-shelter-tasks.md, lines 883-897; knowledge/ios/05-notifications-
permissions.md section 4.

## Why P0, not P1

This task was raised from P1 to P0 because the original priorities
contradicted each other. Metric 3 (return on day 1) is one of four numbers
that decide the project, and the evening notification is the only mechanism
in the entire MVP designed to cause that return. Leaving the mechanism
optional while the metric it drives is a go/no-go threshold means a slipped
P1 task would quietly guarantee a bad reading on a P0 measurement. Either the
notification ships, or 80-live-validation/00-thresholds has to lower the
day-1 threshold to account for its absence - and shipping the notification is
cheaper than renegotiating the gate.

## Ask after level 2, not on the first screen - but don't cite a number for it

Keep the practice, drop the justification that used to travel with it. The
claim that asking for permission on first launch "doubles the refusal rate"
was traced back through the sources that repeat it and does not hold up: a
marketing blog (vmobify) cites Pushwoosh for "55-70% opt-in vs 30-40%"; the
actual Pushwoosh post contains no such figures, only a qualitative
recommendation to ask at a moment of high intent. A second source
(semnexus.com) makes the same qualitative claim with no numbers either.

The reasoning that survives is plain, not measured: a permission request
means more once the player already knows what the notification is for.
Treat "ask after level 2" as judgement, not as a measured fact, and do not
repeat "doubles the refusal rate" or any specific percentage to a publisher
or in analytics reporting - see
knowledge/ios/05-notifications-permissions.md section 4 for the full citation
chain.

---

# Built, 2026-08-26

`Assets/Shell/EveningReminder.cs`, package `com.unity.mobile.notifications@2.4.3`
added to the manifest.

## Copy

> **Your kitten found something behind the couch**
> It is waiting to show you, whenever you have a minute.

Modelled directly on the line in `cat-shelter-mvp.md` section 4 — "Murzik found
something behind the couch". A discovery, not a chore: the design rule in that
section is that **the kitten never gets sick**, because punishing a skipped day
in a game about caring drives off exactly this audience and "kills permission
for notifications" too. So: no guilt, no urgency, no counting of missed days,
nothing owed. "Whenever you have a minute" is the whole posture.

The first draft read "There is still a little tidying to do" and was replaced —
that is a chore reminder, which is the tone this section rules out.

## When permission is asked

After **level 2** is cleared, once ever, recorded in `PlayerPrefs`. Not on first
launch: at that point the player has nothing to be reminded about yet.

The number that used to justify this — "asking on launch doubles refusals" —
stays out of the code and out of this note, per the section above: it was traced
to a marketing blog citing a source that does not contain it. The practice is
kept, the invented figure is not.

`Analytics.NotificationAllowed()` fires on a grant — one of the nine pinned
events, and the denominator for whatever metric 3 turns out to be.

## Why it fires only on days of inactivity

`Reschedule()` runs on every launch and replaces the pending reminder by
identifier. Opening the game today pushes the next one to tomorrow evening, so a
player who plays daily never sees it. `RemoveScheduledNotification(id)` rather
than clearing everything, so a future notification of another kind is not taken
down with it.

The trigger is a calendar trigger at 19:00 with `Repeats = true` and no
year/month/day, which is how the package expresses "the next occurrence of this
hour, daily" (knowledge/ios/05, section 2.3).

## Checking delivery without waiting until seven

A file called `notify-in-seconds.txt` next to the save switches the trigger to a
time interval and asks for permission immediately instead of after level 2.
Delivery is otherwise observable only by waiting for the evening, which is not a
check anyone will run. Absent in any normal run, and the file never ships.

## What was checked on the simulator, and what could not be

**Met: the dialog does not appear on launch.** And it very nearly did. Adding
`com.unity.mobile.notifications` turns on
`UnityNotificationRequestAuthorizationOnAppLaunch` **by default**, so the first
build asked for permission on the very first frame — exactly what VERIFY 1
forbids. Found by building and looking at the screen; the code responsible
belongs to the package, not to this repository. Turned off in
`ProjectSettings/NotificationsSettings.asset`, and a clean simulator now
launches straight into the game with no dialog.

(That one default is why `17-permission-audit` was opened.)

**Met: permission is requested at the right moment and only once.** Driven
through the debug path, the system dialog appeared, `Allow` was accepted, and
`authorizationStatus: Authorized` shows in `usernotificationsd`;
`catshelter.notifications.asked` is set to 1 in `PlayerPrefs` so it is never
asked again.

**NOT met: delivery.** `ScheduleNotification` is reached — `reminder-state.txt`
records each call with the authorization status — but
`GetScheduledNotifications()` returns **0 pending** immediately afterwards, and
no notification is delivered, at a 20-second or a 65-second interval, with the
app backgrounded, on a freshly erased simulator. Nothing appears in
`usernotificationsd` under our identifier either.

So on the simulator this package schedules nothing. Whether that is a simulator
limitation (as with Vision, which cannot create an inference context there) or
a real defect in this code **is not established**, and it should not be claimed
either way. It is the second thing in a row that only a device can settle.

VERIFY 2 stays open until `14-testflight`. The debug hook stays in for that
run: drop `notify-in-seconds.txt` next to the save, launch, background the app,
and watch. `reminder-state.txt` records what the app thought it did.
