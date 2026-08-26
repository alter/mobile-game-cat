# Why this task exists, with an example already in hand

Raised by the owner on 2026-08-26: minimise permissions, justify each one.

The example that prompted it is not hypothetical. Adding
`com.unity.mobile.notifications` turned on
`UnityNotificationRequestAuthorizationOnAppLaunch` **by default**, and the app
began asking for notification permission on the very first launch — before the
player had cleared a single pile, and against this milestone's own acceptance
criterion ("the dialog appears after level 2 and not before"). It was found by
building and looking at the screen, not by reading the code, because the code
that did it belongs to the package.

That is the shape of the problem: permissions arrive as side effects of
dependencies. Three more packages are in the manifest — purchasing, analytics,
services core — and the vision plugin and the capture screen will each add
their own. Nobody has looked at the full list yet.

## Baseline as of 2026-08-26

`plutil -p` on the generated Info.plist finds **no** `Ns*UsageDescription` keys
at all, no `UIBackgroundModes`, and no `aps-environment`. So the app currently
asks for nothing except notifications, which is the state to defend rather
than to repair. The audit is cheap now and expensive after the photo phase.

## What the finished table should answer per row

| key | who added it | what a player does that needs it | what breaks without it |

A row that cannot fill the third column is a row to delete.
