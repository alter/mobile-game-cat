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

# The audit, 2026-08-27

`plutil -p` over the built `Info.plist`: **48 keys, one of them a permission.**

| key | who put it there | what a player does that needs it | what breaks without it |
|---|---|---|---|
| `NSCameraUsageDescription` | us, `50-photo/08` | taps "Take a photo" | the camera path; the gallery path still works |

That is the whole list. No `NSPhotoLibraryUsageDescription`, no
`UIBackgroundModes`, no `aps-environment`, no entitlements file, no
`SKAdNetworkItems`.

**Photo library needs no permission** because the gallery goes through
`PHPickerViewController`, which runs outside the app's process and hands over
only what the player picked. That was a design choice in `08` and it pays here:
one prompt instead of two, on the screen the whole concept rests on.

**ATT is absent, as D9 requires.** `grep` over `Assets` and the package
manifest finds no `RequestTrackingAuthorization`, no `AppTrackingTransparency`,
no `advertisingIdentifier`. Nothing to remove, only something to keep out.

## Two packages removed

`com.unity.purchasing`, `com.unity.analytics` and
`com.unity.modules.unityanalytics` were in the manifest and **called by no
code** — `grep` for `UnityEngine.Purchasing`, `Unity.Services` and
`UnityEngine.Analytics` across `Assets` returns nothing. They were pulling
StoreKit into the launch path: the app's own log showed

```
game: (StoreKit) Registering for 'storefrontchanged' daemon notification
game: (StoreKit) Registering for 'receivedpurchaseintents' daemon notification
```

on every start, for a game that sells nothing. Removed; the project still
builds and the app still runs. This is the shape of the problem the task was
opened for: permissions and capabilities arrive as side effects of
dependencies, not as decisions.

When in-app purchase becomes real, the package comes back — with a reason
written in this table.

## The privacy manifest

`UnityFramework/PrivacyInfo.xcprivacy` ships from Unity and declares the API
categories the engine itself touches: system boot time, disk space, user
defaults, file timestamps. All four are engine internals with Apple's standard
reason codes, none of them ours. Nothing to add while the app collects nothing
— and it collects nothing today, because analytics is not wired.

**Re-run this audit after `70-analytics/01`.** GameAnalytics declares
`NSPrivacyTracking = true` and a tracking domain (D9), which is exactly the
kind of thing that arrives with a dependency and changes this table.

## Against the VERIFY list

1. **Met** — every key in the shipped plist appears above with a reason.
2. **Met** — a clean launch raises no dialog; checked on an erased simulator
   after `09-notification` fixed the package default that used to prompt on
   the first frame.
3. **Met** — no `RequestTrackingAuthorization` call site anywhere.

`verify` stays `pending`: the context that removed the packages should not also
sign off that removing them was right.
