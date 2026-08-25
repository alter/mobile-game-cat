# Local notifications and permissions on iOS (Unity)

Date collected: 2026-08-24. Stack: Unity 6.3 LTS, `com.unity.mobile.notifications` package, iOS `UNUserNotificationCenter`, `AppTrackingTransparency`.

## Summary

- Local notifications on iOS are scheduled via `UNUserNotificationCenter.current().add(_:)` with a `UNNotificationRequest`, which can contain a trigger, e.g. `UNCalendarNotificationTrigger`. [Apple — UNUserNotificationCenter.add(_:withCompletionHandler:)](https://developer.apple.com/documentation/usernotifications/unusernotificationcenter/add(_:withcompletionhandler:))
- Hard limit — no more than 64 simultaneously scheduled (pending) local notifications per app; this is confirmed by an Apple engineer on the official developer forum, not directly in public documentation. [Apple Developer Forums — Does UNNotificationRequest have a 64-notification scheduling limit?](https://developer.apple.com/forums/thread/811171)
- In Unity 6.x, local notifications on iOS use the `com.unity.mobile.notifications` package; the current branch (as of data collection) is 2.4.x, with version 2.4.3 bundled with editor 6000.5. [Unity — Mobile Notifications changelog 2.4](https://docs.unity3d.com/Packages/com.unity.mobile.notifications@2.4/changelog/CHANGELOG.html)
- Requesting notification permission in Unity is done via `AuthorizationRequest` (a coroutine), and scheduling — via `iOSNotificationCenter.ScheduleNotification(iOSNotification)` with `iOSNotificationCalendarTrigger` for "one notification at a given time of day." [Unity — iOS notifications manual](https://docs.unity3d.com/Packages/com.unity.mobile.notifications@2.4/manual/iOS.html)
- Provisional authorization (`UNAuthorizationOptions.provisional`) allows sending notifications without a request dialog — they arrive quietly in the Notification Center, and the user decides whether to keep them after seeing the actual content. [Apple — UNAuthorizationOptions.provisional](https://developer.apple.com/documentation/usernotifications/unauthorizationoptions/provisional)
- No reliable, verifiable quantitative data on how the timing of the permission request affects opt-in rate (specifically for push/local notifications on iOS) could be found within this research — marketing sources cite figures, but the figures don't hold up when the primary source is checked. Details in section 4.
- For ATT (App Tracking Transparency), Unity has an official package, `com.unity.ads.ios-support`, providing the `ATTrackingStatusBinding` class with the methods `RequestAuthorizationTracking()` and `GetAuthorizationTrackingStatus()`. [GitHub — Unity-Technologies/com.unity.ads.ios-support](https://github.com/Unity-Technologies/com.unity.ads.ios-support)
- `NSUserTrackingUsageDescription` in Info.plist is mandatory for `ATTrackingManager.requestTrackingAuthorization(completionHandler:)` to work — without this key the authorization request doesn't work properly. [Apple — ATTrackingManager.requestTrackingAuthorization(completionHandler:)](https://developer.apple.com/documentation/apptrackingtransparency/attrackingmanager/requesttrackingauthorization(completionhandler:))

## 1. `UNUserNotificationCenter`: permission, scheduling, limit

### 1.1. Requesting permission and scheduling (native Swift API)

Method for scheduling a local notification:

```swift
func add(_ request: UNNotificationRequest, withCompletionHandler completionHandler: (@Sendable ((any Error)?) -> Void)? = nil)

// async/await variant
func add(_ request: UNNotificationRequest) async throws
```

Official description: «Schedules the delivery of a local notification… This method schedules local notifications only; you cannot use it to schedule the delivery of remote notifications… If the request does not contain a `UNNotificationTrigger` object, the notification is delivered right away.» The method can be called from any thread in the app. [Apple — UNUserNotificationCenter.add(_:withCompletionHandler:)](https://developer.apple.com/documentation/usernotifications/unusernotificationcenter/add(_:withcompletionhandler:))

Example from Apple's documentation:

```swift
let center = UNUserNotificationCenter.current()
let content = UNMutableNotificationContent()
content.title = "My notification title"
content.body = "My notification body"
let notification = UNNotificationRequest(identifier: "com.example.mynotification", content: content, trigger: nil)
do {
    try await center.add(notification)
} catch {
    // Handle any errors.
}
```

[Apple — UNUserNotificationCenter.add(_:withCompletionHandler:)](https://developer.apple.com/documentation/usernotifications/unusernotificationcenter/add(_:withcompletionhandler:))

### 1.2. Scheduling one notification per day in the evening: `UNCalendarNotificationTrigger`

For "one notification per day at a given time," a calendar trigger is needed, set via `DateComponents` — if only `hour`/`minute` are specified (without `day`/`month`/`year`), the system finds the next matching time on its own, and with `repeats: true`, repeats the notification every day at that time. Concrete code examples for `UNCalendarNotificationTrigger` specifically from Apple's documentation were not accessed within this research — the example above shows only the base `add(_:)` call without a trigger; for a scheduled daily evening notification in native Swift code, the `UNCalendarNotificationTrigger(dateMatching:repeats:)` class is used, where `dateMatching` is `DateComponents` with `hour`/`minute` set. This construct is not confirmed by a separate Apple citation within this research — "not verified" regarding the exact initializer signature.

### 1.3. The 64-scheduled-notification limit

Apple does not describe this limit explicitly in the public documentation of the `UNUserNotificationCenter` class, but an Apple engineer confirmed it directly on the official developer forum: «Yes, there is a limit of 64 for how many simultaneous notification requests can be active/pending at one time per app. This is a system limit and there is no way around it.» [Apple Developer Forums — Does UNNotificationRequest have a 64-notification scheduling limit?](https://developer.apple.com/forums/thread/811171)

Practical consequences the community draws from this limit:
- The system holds the 64 nearest-to-fire notifications and drops the rest (when attempting to schedule more).
- The recommended pattern is to keep only the nearest ~64 firings queued and recompute/reschedule them on every app launch, calling `removeAllPendingNotificationRequests()` before rescheduling.
- No official mechanism exists to raise the limit or grant an exception for specific apps.

[Apple Developer Forums — Does UNNotificationRequest have a 64-notification scheduling limit?](https://developer.apple.com/forums/thread/811171)

## 2. Unity Mobile Notifications (`com.unity.mobile.notifications`)

### 2.1. Version for Unity 6.x

The package adds support for scheduling one-time or repeating local notifications on Android and iOS, with push notification support on iOS. As of data collection (2026-08-24), the current branch is 2.4.x, specifically version 2.4.3, shipped with Unity editor 6000.5. From this branch's changes relevant to iOS: a new API, `QueryLastRespondedNotification`, was added — for getting details of the notification that was tapped to launch the app. [Unity — Mobile Notifications changelog 2.4](https://docs.unity3d.com/Packages/com.unity.mobile.notifications@2.4/changelog/CHANGELOG.html)

The minimum supported Unity version for the package overall is "Compatible with Unity 2021.3 or above"; the package also supports push notifications via APNs, grouping notifications into threads (iOS 12+), attachments, and custom actions. [Unity — Mobile Notifications manual (overview)](https://docs.unity3d.com/Packages/com.unity.mobile.notifications@2.4/manual/index.html)

### 2.2. Requesting permission

Official Unity example (the `RequestAuthorization` coroutine):

```csharp
IEnumerator RequestAuthorization()
{
    var authorizationOption = AuthorizationOption.Alert | AuthorizationOption.Badge;
    using (var req = new AuthorizationRequest(authorizationOption, true))
    {
        while (!req.IsFinished)
        {
            yield return null;
        };

        string res = "\n RequestAuthorization:";
        res += "\n finished: " + req.IsFinished;
        res += "\n granted :  " + req.Granted;
        res += "\n error:  " + req.Error;
        res += "\n deviceToken:  " + req.DeviceToken;
        Debug.Log(res);
    }
}
```

[Unity — iOS notifications manual](https://docs.unity3d.com/Packages/com.unity.mobile.notifications@2.4/manual/iOS.html)

### 2.3. Scheduling via `iOSNotificationCalendarTrigger`

`iOSNotificationCalendarTrigger` is a struct in the `Unity.Notifications.iOS` namespace implementing `iOSNotificationTrigger`; it is used "when you need to schedule the delivery of a local notification at a specified date and time." Not all fields need to be set — if `Year`/`Month`/`Day` are left unfilled, the system picks the nearest matching time based on the remaining fields (`Hour`/`Minute`). [Unity — iOSNotificationCalendarTrigger API (2.1)](https://docs.unity3d.com/Packages/com.unity.mobile.notifications@2.1/api/Unity.Notifications.iOS.iOSNotificationCalendarTrigger.html)

Official example from the manual (a notification at 12:00 noon, no repeat in the example):

```csharp
var calendarTrigger = new iOSNotificationCalendarTrigger()
{
    // Year = 2020,
    // Month = 6,
    // Day = 1,
    Hour = 12,
    Minute = 0,
    // Second = 0
    Repeats = false
};
```

[Unity — iOS notifications manual](https://docs.unity3d.com/Packages/com.unity.mobile.notifications@2.4/manual/iOS.html)

For our task (one notification per day in the evening, e.g. 20:00, repeating daily) the construct would be as follows (composed by us from the structure's documented fields, similar to the example above):

```csharp
var eveningTrigger = new iOSNotificationCalendarTrigger()
{
    Hour = 20,
    Minute = 0,
    Repeats = true
};

var notification = new iOSNotification()
{
    Identifier = "daily_evening_reminder",
    Title = "Your cat is waiting!",
    Body = "Come back to the game and take a new photo.",
    ShowInForeground = true,
    ForegroundPresentationOption = (PresentationOption.Alert | PresentationOption.Sound),
    CategoryIdentifier = "daily_reminder",
    ThreadIdentifier = "daily_reminder_thread",
    Trigger = eveningTrigger,
};

iOSNotificationCenter.ScheduleNotification(notification);
```

The scheduling method and the fields of `iOSNotification` (`Identifier`, `Title`, `Body`, `Subtitle`, `ShowInForeground`, `ForegroundPresentationOption`, `CategoryIdentifier`, `ThreadIdentifier`, `Trigger`) come from Unity's official manual; specifically, the "at 20:00, every day" assembly with these values is our own composition from the documented API, not a verbatim citation of a single example. [Unity — iOS notifications manual](https://docs.unity3d.com/Packages/com.unity.mobile.notifications@2.4/manual/iOS.html)

Method for cancelling a notification that hasn't fired yet:

```csharp
iOSNotificationCenter.RemoveScheduledNotification(notification.Identifier);
```

[Unity — iOS notifications manual](https://docs.unity3d.com/Packages/com.unity.mobile.notifications@2.4/manual/iOS.html)

### 2.4. Deferred permission request (not on first launch)

The `com.unity.mobile.notifications` package itself has no separate built-in "deferred request wizard" — control over the timing of the request is implemented manually in game code: the developer decides when to call `AuthorizationRequest` (for example, after the first successful cat photo, rather than immediately on the title screen). The package documentation notes only a technical detail: «If the user has already granted or denied authorization, the permissions request dialog doesn't display again» — meaning a repeated call to `AuthorizationRequest` is safe and won't show the system dialog twice. [Unity — iOS notifications manual](https://docs.unity3d.com/Packages/com.unity.mobile.notifications@2.4/manual/iOS.html)

## 3. Provisional authorization

`UNAuthorizationOptions` is an enumeration of options defining the allowed capabilities of local and remote notifications; one of the values is `.provisional`. Official description: `.provisional` provides «the ability to post noninterrupting notifications provisionally to the Notification Center», and the corresponding status `UNAuthorizationStatus.provisional` means the app is temporarily allowed to send non-interrupting notifications to the user. [Apple — UNAuthorizationOptions](https://developer.apple.com/documentation/usernotifications/unauthorizationoptions), [Apple — UNAuthorizationOptions.provisional](https://developer.apple.com/documentation/usernotifications/unauthorizationoptions/provisional)

How this works in practice (from independent write-ups on the topic, not from Apple's primary documentation): when requesting with the `.provisional` option, the system **does not show a request dialog** — notifications are immediately delivered quietly to the Notification Center, where the user has the option to either keep them or turn them off entirely. This is a way to give the user a "trial period" with a specific app's notifications without an explicit permission dialog. The feature is available from iOS 12. [Use Your Loaf — Provisional Authorization of User Notificatons](https://useyourloaf.com/blog/provisional-authorization-of-user-notificatons/)

**Is provisional authorization worth using for our game:** it's a trade-off. The upside — it doesn't spend the "limited attempt" at showing the system dialog and doesn't scare the user with an extra request; the downside — the notification arrives without sound or a banner (only into the Notification Center), i.e. it's less noticeable, which makes it worse suited if the goal is specifically to bring the player back with a sound/visual reminder. No direct comparative data (which option is more effective for user return) was found — "no reliable source found."

## 4. Effect of permission-request timing on opt-in rate

The task specifically states: cite numbers only if a source with figures is found, otherwise "no data." Within this research, the verification chain is as follows:

- The vmobify.com blog claims: «apps that show a soft-ask modal at the moment of first value (rather than on first launch) achieve 55–70% opt-in versus 30–40% for apps that trigger the prompt immediately», citing "Pushwoosh's opt-in rate research" with the figure «30–50% higher acceptance rates». [vmobify — Push Notification Strategy 2026](https://vmobify.com/blog/push-notification-strategy)
- When the primary source was opened (the Pushwoosh blog that vmobify cites), these specific figures **were not confirmed** — the Pushwoosh article contains only a general recommendation to pick a "moment of high intent" for showing the request, with no conversion figures, no description of a controlled experiment or survey. [Pushwoosh — How to Increase Your Push Notification Opt-In Rate](https://www.pushwoosh.com/blog/increase-push-notifications-opt-in/)
- Another source (semnexus.com) contains a similar qualitative claim («apps that trigger the native prompt within the first 30 seconds of first launch typically see lower opt-in rates than apps that delay the ask»), but likewise with no numerical data and no verifiable source cited. [SEM Nexus — Push Notification Timing: What the Data Says About Opt-In Rates](https://semnexus.com/push-notification-timing-data-opt-in-rates)

**Conclusion:** specific percentages ("55–70% versus 30–40%", "25–50% higher") on this topic appear only in marketing blogs, and when traced through the citation chain to the primary source, the numbers are not backed by documentably verifiable research. Formally, a source with figures was found (vmobify.com), but it is unreliable — it cites a source that does not contain those figures. Therefore these figures should not be used for practical decisions in the project; the correct statement is "no data" (in the sense of "no trustworthy source"). The general, qualitative recommendation (don't tie the permission request to first launch, show it after demonstrating the feature's value) recurs repeatedly across sources, but without a verifiable quantitative estimate of the effect.

## 5. ATT and `NSUserTrackingUsageDescription` in combination with Unity

### 5.1. Apple's native API

```swift
class func requestTrackingAuthorization(completionHandler completion: @escaping @Sendable (ATTrackingManager.AuthorizationStatus) -> Void)

// async/await variant
class func requestTrackingAuthorization() async -> ATTrackingManager.AuthorizationStatus
```

Key usage rules, per Apple's documentation:
- The request is one-time per app install — the system remembers the user's choice and won't ask again unless the app was deleted and reinstalled.
- Before calling again, check `trackingAuthorizationStatus` for `.notDetermined`.
- The dialog is shown only when the app state is `UIApplicationStateActive`.
- The dialog won't appear if there is already another pending permission request (concurrent requests are not retained by the system).
- A call from an app extension does not show the dialog.
- **`NSUserTrackingUsageDescription` in Info.plist is mandatory** — without this key the authorization request won't work correctly.

[Apple — ATTrackingManager.requestTrackingAuthorization(completionHandler:)](https://developer.apple.com/documentation/apptrackingtransparency/attrackingmanager/requesttrackingauthorization(completionhandler:))

### 5.2. The Unity package `com.unity.ads.ios-support`

Official description: the package «provides support for App Tracking Transparency and SkAdNetwork API newly introduced in Apple iOS 14», including an example of a customizable "warm-up" screen before the tracking permission request. [GitHub — Unity-Technologies/com.unity.ads.ios-support](https://github.com/Unity-Technologies/com.unity.ads.ios-support)

Methods available via `ATTrackingStatusBinding` (namespace `Unity.Advertisement.IosSupport`):

```csharp
public static void RequestAuthorizationTracking()
public static AuthorizationTrackingStatus GetAuthorizationTrackingStatus()
public static void SkAdNetworkUpdateConversionValue(int conversionValue)
```

[GitHub — Unity-Technologies/com.unity.ads.ios-support](https://github.com/Unity-Technologies/com.unity.ads.ios-support)

Official usage example from Unity's documentation (docs.unity.com):

```csharp
using UnityEngine;
#if UNITY_IOS
// Include the IosSupport namespace if running on iOS:
using Unity.Advertisement.IosSupport;
#endif

public class AttPermissionRequest : MonoBehaviour {
  void Awake() {
#if UNITY_IOS
  // Check the user's consent status.
  // If the status is undetermined, display the request:
  if(ATTrackingStatusBinding.GetAuthorizationTrackingStatus() == ATTrackingStatusBinding.AuthorizationTrackingStatus.NOT_DETERMINED) {
    ATTrackingStatusBinding.RequestAuthorizationTracking();
  }
#endif
  }
}
```

[Unity — ATT Compliance guide](https://docs.unity.com/grow/en-us/ads/ios-sdk/ios14/att-compliance)

Automatically writing `NSUserTrackingUsageDescription` into Info.plist via `PostProcessBuild` (official Unity example):

```csharp
#if UNITY_IOS
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using System.IO;

public class PostBuildStep {
  const string k_TrackingDescription = "Your data will be used to provide you a better and personalized ad experience.";

  [PostProcessBuild(0)]
  public static void OnPostProcessBuild(BuildTarget buildTarget, string pathToXcode) {
    if (buildTarget == BuildTarget.iOS) {
      AddPListValues(pathToXcode);
    }
  }

  static void AddPListValues(string pathToXcode) {
    string plistPath = pathToXcode + "/Info.plist";
    PlistDocument plistObj = new PlistDocument();
    plistObj.ReadFromString(File.ReadAllText(plistPath));
    PlistElementDict plistRoot = plistObj.root;
    plistRoot.SetString("NSUserTrackingUsageDescription", k_TrackingDescription);
    File.WriteAllText(plistPath, plistObj.WriteToString());
  }
}
#endif
```

[Unity — ATT Compliance guide](https://docs.unity.com/grow/en-us/ads/ios-sdk/ios14/att-compliance)

### 5.3. Request order

Unity's official recommendation: the ATT request should run **before** initializing any SDKs that need IDFA access, since Apple allows this dialog to be shown only once per install, and the user can change the decision manually in Settings at any time. Recommended order: 1) set `NSUserTrackingUsageDescription` in Info.plist (mandatory); 2) optionally show a custom explanation screen before the system dialog ("ATT context screen"); 3) check `GetAuthorizationTrackingStatus()`, and if the status is `NOT_DETERMINED`, show the system request via `RequestAuthorizationTracking()`. [Unity — ATT Compliance guide](https://docs.unity.com/grow/en-us/ads/ios-sdk/ios14/att-compliance)

For our game (assuming it has no ads/SDKs of its own requiring IDFA), ATT may not be needed at all — requesting `NSUserTrackingUsageDescription`/`requestTrackingAuthorization` only makes sense if the app actually tracks the user across apps/sites (for example, via an ad SDK with IDFA). This is a general conclusion drawn from Apple/Unity documentation, not a separate explicit quote.

## Sources

- [Apple — UNUserNotificationCenter.add(_:withCompletionHandler:)](https://developer.apple.com/documentation/usernotifications/unusernotificationcenter/add(_:withcompletionhandler:))
- [Apple Developer Forums — Does UNNotificationRequest have a 64-notification scheduling limit?](https://developer.apple.com/forums/thread/811171)
- [Apple — UNAuthorizationOptions](https://developer.apple.com/documentation/usernotifications/unauthorizationoptions)
- [Apple — UNAuthorizationOptions.provisional](https://developer.apple.com/documentation/usernotifications/unauthorizationoptions/provisional)
- [Apple — ATTrackingManager.requestTrackingAuthorization(completionHandler:)](https://developer.apple.com/documentation/apptrackingtransparency/attrackingmanager/requesttrackingauthorization(completionhandler:))
- [Use Your Loaf — Provisional Authorization of User Notificatons](https://useyourloaf.com/blog/provisional-authorization-of-user-notificatons/)
- [Unity — Mobile Notifications changelog 2.4](https://docs.unity3d.com/Packages/com.unity.mobile.notifications@2.4/changelog/CHANGELOG.html)
- [Unity — Mobile Notifications manual (overview)](https://docs.unity3d.com/Packages/com.unity.mobile.notifications@2.4/manual/index.html)
- [Unity — iOS notifications manual](https://docs.unity3d.com/Packages/com.unity.mobile.notifications@2.4/manual/iOS.html)
- [Unity — iOSNotificationCalendarTrigger API (2.1)](https://docs.unity3d.com/Packages/com.unity.mobile.notifications@2.1/api/Unity.Notifications.iOS.iOSNotificationCalendarTrigger.html)
- [GitHub — Unity-Technologies/com.unity.ads.ios-support](https://github.com/Unity-Technologies/com.unity.ads.ios-support)
- [Unity — ATT Compliance guide (docs.unity.com)](https://docs.unity.com/grow/en-us/ads/ios-sdk/ios14/att-compliance)
- [vmobify — Push Notification Strategy 2026](https://vmobify.com/blog/push-notification-strategy)
- [Pushwoosh — How to Increase Your Push Notification Opt-In Rate](https://www.pushwoosh.com/blog/increase-push-notifications-opt-in/)
- [SEM Nexus — Push Notification Timing: What the Data Says About Opt-In Rates](https://semnexus.com/push-notification-timing-data-opt-in-rates)
