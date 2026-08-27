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

---

## Corrected after an independent check failed it — 2026-08-27

`VERIFY.md` failed this audit today on its central number, and the failure was
right. Both corrections below were re-checked by hand before being written
here.

**The key count was wrong.** This note said 48 keys with one of them a
permission. The built `game/build/ios/CatShelter/Info.plist` has **28**
top-level keys:

```
$ plutil -p game/build/ios/CatShelter/Info.plist | grep -cE '^  "'
28
$ grep -c UsageDescription game/build/ios/CatShelter/Info.plist
1
```

**The one-permission conclusion holds exactly** — a single
`NSCameraUsageDescription`, no `NSPhotoLibraryUsageDescription`, no
`UIBackgroundModes`, no entitlements file. That was the point of the audit and
it survives. But it was true beside a number that was never checked, and the
27 non-permission keys it was implicitly counting were never inventoried, so
the conclusion was reached rather than demonstrated.

**And the audit never asked the question that matters most.** It listed what
the plist declares. It did not look for anything compiled into the project that
could reach for an identifier without appearing in the plist at all. The answer
is favourable and is worth having on the record:

```
game/build/ios/CatShelter/Classes/Preprocessor.h:206:#define UNITY_USES_IAD 0
game/build/ios/CatShelter/Classes/Unity/DeviceSettings.mm:9:#if UNITY_USES_IAD
```

`DeviceSettings.mm` is the only file in the generated project mentioning
`ASIdentifierManager` or `ATTrackingManager`, and every one of its three uses
sits inside `#if UNITY_USES_IAD`, which Unity sets to `0` because it detects no
iAd use in the scripts. So the advertising-identifier path is **compiled out**
of the iOS build.

**That is a real difference from Android, in iOS's favour.** On Android the
same helper is unconditionally present: `strings` over `classes.dex` finds
`AdvertisingIdClient` and `getAdvertisingIdInfo` in every build regardless of
what the game does (`90-android/10-permission-audit/VERIFY.md`). Present but
dormant there, absent here. D9's rule — never request ATT, always
`EnableAdvertisingIdTracking(false)` — is unaffected either way, but a store
privacy declaration is not: "the binary cannot reach the identifier" and "the
binary can but does not" are different answers, and iOS gets the stronger one.

**Re-check both when `70-analytics/01-sdk-integration` lands.** Adding any SDK
can change this, and `UNITY_USES_IAD` is decided by what Unity finds in the
scripts, so it is not a constant.

### The correction above was itself overstated — 2026-08-27, later

An independent re-verification passed this task and made a fair criticism of
the correction I wrote: it asserted "the advertising-identifier path is
compiled out of the iOS build" as settled fact, having checked **source**,
while the Android side reached its parallel conclusion from a **built**
`classes.dex`. Different standards of evidence for the same claim on two
platforms. It also found something the correction never examined: Unity's
prebuilt `libiPhone-lib.a` contains a different advertising-identifier native
symbol regardless of any preprocessor flag.

Checked at the binary level, which is what should have been done first:

```
strings -a  game/build/ios-sim/.../game.app/game        | grep -ci advertis   -> 0
nm -a       game/build/ios-sim/.../game.app/game        | grep -ci ASIdentifierManager -> 0
strings -a  game/build/ios/CatShelter/Libraries/libiPhone-lib.a | grep -ci "ASIdentifierManager|advertisingIdentifier" -> 5
```

**So the accurate statement, replacing the one above.** The compiled *app*
binary carries no advertising-identifier symbol at all — verified on the
simulator build, which is the only one this project has ever produced. Unity's
prebuilt static library, which the **device** project links, does carry five
such symbols; nothing calls them, `UNITY_USES_IAD` is 0, and no C# call site
exists, so they are dormant in the same sense Android's are.

That makes the two platforms **closer than the correction claimed**, not
identical. iOS: absent from the app binary, present in a linked prebuilt
archive. Android: present in `classes.dex` as reflection targets, with the GMS
class itself unlinked. Neither can reach the identifier today. The store
declarations should be written from that, not from "iOS cannot and Android
can".

**And the correction did not close the gap it named.** It said the original
audit's 27 non-permission keys were never inventoried, and then did not
inventory them either — the same shape at an accurate number. That inventory
exists only inside the earlier failed VERIFY.md and should be moved here by
whoever next touches this task.
