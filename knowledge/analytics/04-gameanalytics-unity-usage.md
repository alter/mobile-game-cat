# GameAnalytics in Unity 6.3 LTS (iOS): practical application

Date compiled: 2026-08-24.

Scope: game — 2D puzzle, iOS, Unity 6.3 LTS (6000.3.x), C#. Tool chosen (GameAnalytics), the document describes its application: installation, setup, calls for nine events, limits, verifying delivery, offline behavior, Apple requirements, working in the web dashboard, and known pitfalls.

Primary sources: docs.gameanalytics.com (current Unity SDK documentation) and the GitHub repository GameAnalytics/GA-SDK-UNITY (file `Runtime/Scripts/GameAnalytics.cs`, `CHANGELOG.md`, repository issues). Where data is not confirmed by documentation or source code, it is marked "not verified".

## Summary

- The current package version is **8.1.0** (released 2026-08-21, three days before this document was compiled). The minimum supported Unity version has been **raised to 2022.3 LTS**; no separate explicit confirmation of "Unity 6.x" was found in the documentation, but 2022.3+ formally covers the 6000.x line. At the same time, there is an open compatibility issue with Unity 6.5 in the repository (issue #57) and an open crash issue on Unity 6.3 in a **Standalone** build (issue #58, not iOS).
- Installation — via Unity Package Manager through the scoped registry `package.openupm.com` (package `com.gameanalytics.sdk`), or via `.unitypackage`, or directly through the OpenUPM CLI. For iOS, starting with version 8.1.0, external native dependencies were consolidated into a single `GameAnalytics.xcframework`.
- Key setup — not through code, but through an editor settings object: `Window → GameAnalytics → Select Settings` creates a ScriptableObject settings asset in the project; the iOS platform is added separately in it ("Add Platform"), and the Game Key/Secret Key for iOS are stored separately from Android.
- Initialization — **manual**, via a call to `GameAnalytics.Initialize()`; must happen after the ATT dialog has been shown (or explicitly skipped), and after `Start()`, not `Awake()`, to guarantee initialization order.
- All nine target events fit into two GameAnalytics event types: **Progression** (for `level_start/level_win/level_fail`) and **Design** (for all six of the others). Neither Business, Resource, Ad, nor Error fit them.
- The Design event name has a strict server-side rule: up to 5 parts separated by ":", each part 1–64 characters from a restricted character set; when violated, the event **is not dropped immediately on the client**, but is rejected by the collector on the server side — outwardly this looks like "the event went missing".
- Debug logging for Unity is enabled not by a runtime call, but by **checkboxes in the settings object's inspector** (Info Log Build / Verbose Log Build) — this differs from the Android/iOS-native/JS SDK, which have `SetEnabledInfoLog`/`SetEnabledVerboseLog` as public functions.
- The package itself queues events and resends them once network connectivity is restored — you don't need to write your own offline queue (confirmed for the SDK family; for the Unity wrapper this is delegated to the native iOS SDK).
- The package contains `PrivacyInfo.xcprivacy` inside `GameAnalytics.xcframework` — required for the App Store and found directly in the repository source. The SDK is not required to show the ATT dialog itself: showing ATT is an action taken by the developer (`GameAnalytics.RequestTrackingAuthorization()` is called at the developer's discretion); if it is not called, no ATT dialog will appear, and `EnableAdvertisingIdTracking(false)` additionally disables use of the IDFA and any effects related to ad attribution.
- Server-side delay: regular dashboards — up to a day ("under 24 hours" per unofficial community sources), "live" viewing (Realtime) — events appear within about 30 seconds, but only the last 50 events.

## 1. Installation

### 1.1. Current version and Unity support

From the repository's `CHANGELOG.md` (current entry at the top of the file):

```
## [8.1.0] - 2026-08-21

### Changed
- Raised the minimum supported Unity version to 2022.3 (LTS)
- iOS/tvOS: replaced the static libraries with a single `GameAnalytics.xcframework` (updated GameAnalytics iOS SDK to 5.0.2)
- Standalone (Windows/macOS/Linux): updated the native C++ SDK to 5.4.0

### Removed
- The External Dependency Manager (EDM4U) requirement — the Android SDK is now fully self-contained
- Support for deprecated platforms: UWP/WSA, Tizen, Samsung TV and Windows Phone 8.1
```

This is confirmed by the GitHub release: tag `8.1.0`, publication date `2026-08-21T09:06:31Z` (obtained via `GET /repos/GameAnalytics/GA-SDK-UNITY/releases/latest`).

Formally the minimum version is **Unity 2022.3 LTS**, which also covers Unity 6.3 LTS (6000.3.x), since the 6000.x line was released later and is stated to be compatible (the "Get Started" page itself, until a recent change, stated "Unity 2019.4+" — a wording that is stale relative to the CHANGELOG, so the CHANGELOG was taken as the more current source). No direct statement of "tested on Unity 6.3" was found in the documentation — **not verified** explicitly by documentation, but there are practical signals:

- Issue **#57** ("Unity 6.5 Compile Errors: `EditorApplication.hierarchyWindowItemOnGUI` and `EditorUtility.InstanceIDToObject` obsolete", open, GA-SDK-UNITY repository): when upgrading to Unity 6.5 (and presumably subsequent 6000.x tech streams), the build fails with `CS0619`, because `Runtime/Scripts/GameAnalytics.cs` uses a deprecated Hierarchy editor API. At the time this document was compiled, it was not confirmed whether this manifests specifically on 6.3.
- Issue **#58** ("[Unity 6.3] Windows build crashes with gameanalytics.dll", open): a confirmed crash specifically on Unity 6.3 (`fileVersion: 6000.3.16.28451`), but in a **Standalone** (Windows) build, not iOS; the stack trace points to a native mutex in `gameanalytics.dll` (the cross-platform C++ component). It does not relate to iOS directly, but shows that 8.1.0 on the 6000.3.x line has open bug reports.

### 1.2. Installation methods

From the repository's `README.md` and the official documentation (`docs.gameanalytics.com/.../unity`) — three methods:

**1) Unity Package Manager (git/scoped registry)**

A dependency and a scoped registry are added to `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.gameanalytics.sdk": "[latest_version]"
  },
  "scopedRegistries": [
    {
      "name": "Game Package Registry by Google",
      "url": "https://unityregistry-pa.googleapis.com/",
      "scopes": ["com.google"]
    },
    {
      "name": "package.openupm.com",
      "url": "https://package.openupm.com",
      "scopes": ["com.gameanalytics"]
    }
  ]
}
```

The official instructions still require installing the **External Dependency Manager for Unity** (EDM4U) beforehand as a `.tgz` package — this is the first step on the current installation page. There is a contradiction here with the 8.1.0 CHANGELOG, which states "Removed: The External Dependency Manager (EDM4U) requirement — the Android SDK is now fully self-contained". From this it follows: the installation page is likely not updated for 8.1.0, or EDM4U remains necessary for accompanying Google packages (the `com.google` registry) rather than for the GameAnalytics core itself. **Not verified** conclusively — worth trying in practice to install without EDM4U and checking whether it's required for an iOS build; for Android the CHANGELOG explicitly says it is not required.

**2) `.unitypackage`**

```
https://download.gameanalytics.com/unity/GA_SDK_UNITY.unitypackage
```

This is exactly the method used when ILRD (Impression Level Revenue Data from ad networks) is needed — the normal UPM route doesn't work for ILRD because of dependencies on the Ad SDK, which are absent from UPM:

```
https://download.gameanalytics.com/unity/GA_ILRD_UNITY.unitypackage
```

**3) OpenUPM**

The package `com.gameanalytics.sdk` is officially registered on OpenUPM (`https://openupm.com/packages/com.gameanalytics.sdk/`, the repository is listed as `GameAnalytics/GA-SDK-UNITY`), installed via the same scoped-registry method (see item 1) or via `openupm-cli`. The exact version number from the OpenUPM page was not confirmed programmatically (the page renders via JS), but the latest GitHub release is 8.1.0.

### 1.3. What gets placed in the project

- Package code and assets — in `Library/PackageCache/com.gameanalytics.sdk@...` (if installed via UPM) with the repository structure: `Runtime/Scripts/*` (the C# wrapper, the public `GameAnalytics` API), `Runtime/Apple/GameAnalytics.xcframework/*` (native iOS/tvOS code + `PrivacyInfo.xcprivacy`, see section 7), `Editor/*` (a custom settings inspector, a login wizard, build post-processing).
- Nothing is created automatically in the **user-facing** part of the project (`Assets/...`) when the package is installed — the settings object (see section 2) is created by a separate action, `Window → GameAnalytics → Select Settings`, and the initializer GameObject is created by the action `Window → GameAnalytics → Create GameAnalytics object`, which is added to the currently open scene.
- Size in the built iOS app, per the README: **≈242 KB (armv7) / ≈259 KB (armv8)** — these are old figures for previous SDK versions, the current size for 5.0.2/xcframework has not been separately confirmed ("not verified").

## 2. Setup

### 2.1. The settings object and logging into the dashboard

Keys are entered not through code, but through a built-in editor tool:

`Window → GameAnalytics → Select Settings` — if the settings object doesn't exist yet, Unity creates it automatically (this is a ScriptableObject asset, class `GameAnalyticsSDK.Setup.Settings` — file `Runtime/Scripts/Setup/Settings.cs`). Then, in the inspector:

1. The **Login** button — sign in with your GameAnalytics account credentials.
2. Select the platform and **Add Platform** — the Game Key and Secret Key are then pulled automatically from the web dashboard for the selected studio/game; or the keys can be entered manually.

### 2.2. Separate configuration for iOS

From the source code of `Settings.cs`: the list of platforms is stored as `List<RuntimePlatform> Platforms`, and for each platform the indices `SelectedPlatformOrganization`, `SelectedPlatformStudio`, `SelectedPlatformGame` are stored separately, and the keys are read/written by platform index:

```csharp
public void AddPlatform(RuntimePlatform platform)
// ...
public string GetGameKey(int index)
public string GetSecretKey(int index)
public void UpdateGameKey(int index, string value)
public void UpdateSecretKey(int index, string value)
```

Available platforms (`AvailablePlatforms`): `Android`, `IPhonePlayer` (iOS), `LinuxPlayer`, `OSXPlayer`, `tvOS`, `WebGLPlayer`, `WindowsPlayer`. From this it follows that iOS (`RuntimePlatform.IPhonePlayer`) and Android are two **independent** entries in a single settings object, each with its own Game Key/Secret Key pair. When building for a specific platform, the SDK uses the keys matching the current `RuntimePlatform`.

### 2.3. Initialization

SDK initialization is **manual**, called explicitly from your own script:

```csharp
// SDK Key and SDK Secret are taken from the Settings object
GameAnalytics.Initialize();
```

The documentation emphasizes two ordering requirements:

- The script calling `Initialize()` must have a **Script Execution Order that comes after** the GameAnalytics object's script, if both are in the same scene — part of GameAnalytics' internal code runs in `Awake()`, and this must happen before initialization.
- Sending events must not happen earlier than `Start()` (not `Awake()`), because the order of `Awake()` calls between different `GameObject`s is not guaranteed, and the GameAnalytics object configures itself precisely in its own `Awake()`. If an event is sent before initialization, the log will show:

```
Warning/GameAnalytics: Could not add design event: Datastore not initialized
```

- In the editor (Play Mode), events **are not actually sent** — the native code is not compiled/used in the editor. Verifying real delivery requires a build for the target platform (see section 5).

### 2.4. The GameAnalytics GameObject

Exactly one (and only one) `GameObject` with a GameAnalytics component is required in the startup scene:

`Window → GameAnalytics → Create GameAnalytics object`

The object is not destroyed on scene changes (`DontDestroyOnLoad` inside the implementation), so there's no need to create it again in other scenes — moreover, having more than one such object in the game is a configuration error, and the documentation explicitly warns about this.

### 2.5. ATT and initialization — order matters

Starting with iOS 14.5, permission must be requested via App Tracking Transparency **before** initializing the SDK, if you want the ATT status to be correctly reflected in events. The official example from the documentation (a wrapper for requesting it through the SDK itself):

```csharp
using UnityEngine;
using GameAnalyticsSDK;

public class MyScript : MonoBehaviour, IGameAnalyticsATTListener
{
    void Start()
    {
        if (Application.platform == RuntimePlatform.IPhonePlayer)
        {
            GameAnalytics.RequestTrackingAuthorization(this);
        }
        else
        {
            GameAnalytics.Initialize();
        }
    }

    public void GameAnalyticsATTListenerNotDetermined()  { GameAnalytics.Initialize(); }
    public void GameAnalyticsATTListenerRestricted()      { GameAnalytics.Initialize(); }
    public void GameAnalyticsATTListenerDenied()          { GameAnalytics.Initialize(); }
    public void GameAnalyticsATTListenerAuthorized()      { GameAnalytics.Initialize(); }
}
```

The documentation explicitly requires: the SDK must be initialized **in any case**, even if the user declines the permission — GameAnalytics uses IDFV as the user identifier on iOS and only adds IDFA to events if the ATT status is "authorized". A detailed discussion — how to avoid the ATT dialog altogether — is in section 7.

### 2.6. Custom user ID (brief)

```csharp
GameAnalytics.SetCustomId("myCustomUserId");
```

Set **before** `Initialize()`, otherwise it will not take effect. Introducing this into an already-released game will recount existing users as new — do not do this after the fact.

## 3. Event types

Namespace for all calls: `using GameAnalyticsSDK;`. Below are the signatures, taken verbatim from the source file `Runtime/Scripts/GameAnalytics.cs` (current `master` branch, version 8.1.0), noting when each type applies.

### 3.1. Business Event — payments

```csharp
// Without receipt validation
GameAnalytics.NewBusinessEvent(string currency, int amount, string itemType, string itemId, string cartType)

// iOS — with a receipt
GameAnalytics.NewBusinessEventIOS(string currency, int amount, string itemType, string itemId, string cartType, string receipt)

// iOS — with automatic receipt retrieval
GameAnalytics.NewBusinessEventIOSAutoFetchReceipt(string currency, int amount, string itemType, string itemId, string cartType)

// Android (Google Play)
GameAnalytics.NewBusinessEventGooglePlay(string currency, int amount, string itemType, string itemId, string cartType, string receipt, string signature)
```

Used for real monetary purchases (IAP) with receipt-validation support on GameAnalytics' servers. In our game, none of the nine target events fall here — there are no paid purchases in the list.

### 3.2. Resource Event — virtual economy

```csharp
GameAnalytics.NewResourceEvent(GAResourceFlowType flowType, string currency, float amount, string itemType, string itemId)
```

`flowType` — `GAResourceFlowType.Source` (grant) or `GAResourceFlowType.Sink` (spend). Requires currencies and item types to be pre-registered in the dashboard (a maximum of 20 currencies and 20 item types, see section 4). Suitable for tracking in-game resources (coins, lives, moves as a resource rather than as a one-off tap). **Resource is not used in our event list** — `moves_button_tap` is the fact of a button tap (a UI action), not actually a grant/spend of the "moves" resource; if the task were to track the balance of moves, this would be a candidate for Resource, but the task is "share who tapped" (a conversion rate), for which Design is the more precise fit.

### 3.3. Progression Event — level progress

```csharp
GameAnalytics.NewProgressionEvent(GAProgressionStatus progressionStatus, string progression01)
GameAnalytics.NewProgressionEvent(GAProgressionStatus progressionStatus, string progression01, int score)
GameAnalytics.NewProgressionEvent(GAProgressionStatus progressionStatus, string progression01, string progression02)
GameAnalytics.NewProgressionEvent(GAProgressionStatus progressionStatus, string progression01, string progression02, int score)
GameAnalytics.NewProgressionEvent(GAProgressionStatus progressionStatus, string progression01, string progression02, string progression03)
GameAnalytics.NewProgressionEvent(GAProgressionStatus progressionStatus, string progression01, string progression02, string progression03, int score)
```

`GAProgressionStatus` (`Runtime/Scripts/Enums.cs`, namespace `GameAnalyticsSDK`):

```csharp
public enum GAProgressionStatus
{
    Undefined = 0,
    Start = 1,
    Complete = 2,
    Fail = 3
}
```

Example from the documentation:

```csharp
GameAnalytics.NewProgressionEvent(GAProgressionStatus.Start, "World1", "Level1");
GameAnalytics.NewProgressionEvent(GAProgressionStatus.Complete, "World1", "Level1", score);
GameAnalytics.NewProgressionEvent(GAProgressionStatus.Fail, "World1", "Level1");
```

This is a specialized type built specifically for "level/start/win/fail" — the web dashboard builds ready-made KPIs off of it (progression funnels, Complete/Start and Fail/Complete rates). Our `level_start`, `level_win`, `level_fail` map here directly.

### 3.4. Design Event — arbitrary custom events

```csharp
GameAnalytics.NewDesignEvent(string eventName)
GameAnalytics.NewDesignEvent(string eventName, float eventValue)
```

Used for everything not covered by the prescriptive types (Business/Resource/Progression/Ad): screen views, button taps, custom funnel steps. The name is hierarchical, parts separated by a colon (details — section 4). Our events don't need a value (`eventValue`), so the single-parameter overload `NewDesignEvent(string eventName)` is used.

### 3.5. Error Event

```csharp
GameAnalytics.NewErrorEvent(GAErrorSeverity severity, string message)
```

`GAErrorSeverity`: `Undefined, Debug, Info, Warning, Error, Critical`. For collecting exceptions/errors, not for business metrics. Not used by any of the nine events. The internal error auto-submission (`GA_Debug.HandleLog`, if the "Submit Errors" option is enabled in settings) is capped at `MaxErrorCount = 10` — no more than 10 automatic error events per game session/application lifetime (confirmed in `Runtime/Scripts/Events/GA_Debug.cs`).

### 3.6. Ad Event — advertising

```csharp
GameAnalytics.NewAdEvent(GAAdAction adAction, GAAdType adType, string adSdkName, string adPlacement)
GameAnalytics.NewAdEvent(GAAdAction adAction, GAAdType adType, string adSdkName, string adPlacement, long duration)
GameAnalytics.NewAdEvent(GAAdAction adAction, GAAdType adType, string adSdkName, string adPlacement, GAAdError noAdReason)
```

Only for ad impressions/clicks (rewarded, interstitial, banner). There are no ad events in our list — not used.

### 3.7. Impression Event (ILRD)

`GameAnalyticsILRD.SubscribeXxxImpressions()` — subscription to impression data from specific ad networks (AdMob, IronSource/LevelPlay, MAX, TopOn, Fyber, Aequus). Not relevant to our nine events.

### 3.8. Mapping table: our event → GameAnalytics event type → exact call

| # | Our event | GameAnalytics event type | Exact C# call |
|---|---|---|---|
| 1 | `app_open` | Design | `GameAnalytics.NewDesignEvent("app:open");` |
| 2 | `photo_screen_shown` | Design | `GameAnalytics.NewDesignEvent("photo:screen_shown");` |
| 3 | `photo_uploaded` | Design | `GameAnalytics.NewDesignEvent("photo:uploaded");` |
| 4 | `photo_rejected` | Design | `GameAnalytics.NewDesignEvent("photo:rejected");` |
| 5 | `level_start` | Progression | `GameAnalytics.NewProgressionEvent(GAProgressionStatus.Start, levelId);` |
| 6 | `level_win` | Progression | `GameAnalytics.NewProgressionEvent(GAProgressionStatus.Complete, levelId);` |
| 7 | `level_fail` | Progression | `GameAnalytics.NewProgressionEvent(GAProgressionStatus.Fail, levelId);` |
| 8 | `moves_button_tap` | Design | `GameAnalytics.NewDesignEvent("moves:button_tap");` |
| 9 | `notification_allowed` | Design | `GameAnalytics.NewDesignEvent("notification:allowed");` |

Notes on the table:

- The Design event names above are proposed options, following the recommended practice "`[category]:[subcategory]:[outcome]`" (see section 4). The exact strings must be fixed before starting integration and never changed afterward (renaming = breaking continuity in reports).
- `levelId` — a string like `"Level1"` or `"World1:Level1"` (1–3 hierarchy parts within Progression, separate from Design's 5-part hierarchy). The cardinality limit for Progression is 8000 unique combinations per day per game (section 4) — with the usual number of puzzle levels this is not a problem, but raw procedural IDs (not the same as the design-assigned level number) should not be used in `progression01/02/03`.
- For `level_win`/`level_fail`, if the game tracks a score per level, there's a separate overload with an `int score` parameter — not required for the three target metrics, but may be useful later.
- The three required metrics are not counted by separate calls, but as a ratio of the number of users who reached one Design/Progression event to the number who reached another (a funnel — see section 8): the share who reached the capture screen = `photo:screen_shown` relative to `app:open`; the share who uploaded a photo = `photo:uploaded` relative to `photo:screen_shown`; the share who tapped "+5 moves" = `moves:button_tap` relative to, for example, `level_fail` (or another denominator event the team decides on) — the SDK itself does not compute the metric, that's done by a funnel in the web dashboard on top of the submitted events.

## 4. Naming and value restrictions

These restrictions are a frequent cause of "the event isn't arriving", because a violation **does not produce a compile error and is not always explicitly logged on the client** — the GameAnalytics collector validates the event on the server side and rejects anything that doesn't pass the JSON schema. Source — the official Collection API page (`docs.gameanalytics.com/.../api/event-types/`), the documentation states: "The collector servers will validate these fields and reject any event not passing".

### 4.1. Design Event — exact server-side schema

Official JSON Schema (verbatim, including escaping):

```
{
  "description": "Schema for design event",
  "id": "design",
  "type": "object",
  "extends": "shared",
  "properties": {
    "event_id": {
      "type": "string",
      "pattern": "^[A-Za-z0-9\\s\\-*\\.\\(\\)\\!\\?]{1,64}(:[A-Za-z0-9\\s\\-_\\.\\(\\)\\!\\?]{1,64}){0,4}$",
      "required": true
    },
    "value": {
      "type": "number",
      "required": false
    },
    "category": {
      "type": "string",
      "required": true,
      "pattern": "^design$"
    }
  }
}
```

From the regular expression, it follows verbatim that:

- **Number of parts**: from 1 to 5, separated by `:` (the first part is required, up to 4 more are optional).
- **Length of each part**: 1–64 characters.
- **Allowed characters in the first part**: `A-Z a-z 0-9`, space, `-`, `*`, `.`, `(`, `)`, `!`, `?`.
- **Allowed characters in parts two through five**: the same, but with `_` instead of `*` (the underscore is allowed only outside the first part, and `*` only inside the first part; this asymmetry is exactly what's in the collector's regular expression, not a typo).
- The `,` (comma) character, despite the looser wording on the Unity SDK page ("characters `a-zA-Z, 0-9, -_.,:()!?`"), is **absent** from the actual server-side schema — the Collection API's regular expression should be taken as authoritative, not the descriptive text on the Unity page.

Practical takeaway: don't use commas, Cyrillic characters, slashes, ampersands, or other unsafe characters in event names; don't start a part with `_`.

### 4.2. Progression Event — schema

```
{
  "id": "progression",
  "properties": {
    "event_id": {
      "pattern": "^(Start|Fail|Complete):[A-Za-z0-9\\s\\-*\\.\\(\\)\\!\\?]{1,64}(:[A-Za-z0-9\\s\\-_\\.\\(\\)\\!\\?]{1,64}){0,2}$",
      "required": true
    },
    "attempt_num": { "type": "integer", "minimum": 0, "required": false },
    "score": { "type": "integer", "required": false }
  }
}
```

The resulting server-side `event_id` is `Start|Fail|Complete` + from 1 to 3 values `progression01/02/03` — that is, in Unity SDK terms, 1–3 hierarchy levels are allowed (`progression01`, optionally `progression02`, optionally `progression03`), not 5 like Design.

### 4.3. Cardinality limits (number of unique combinations per day per game)

The official "Event Tracking and Cardinality Limits" page (updated for the policy in effect since October 1, 2025):

| Metric | Threshold |
|---|---|
| Total events per active user per day | 500 |
| Unique combinations (cardinality) of Design events per day | 15,000 |
| Unique combinations of Progression events per day | 8,000 |
| Unique combinations of Resource events per day | 4,000 |

Behavior when exceeded (as of October 1, 2025): the event is not dropped entirely, but its identifier in the analytics system **is replaced with "null"**, which makes metrics for it unreliable in AnalyticsIQ (dashboards, Explore, Funnels) and in MetricsAPI; there is no distortion in Data Export/Data Warehouse (raw data). Practical consequence for the puzzle game: don't use procedural level IDs, timestamps, or coordinates inside `event_id` — only a pre-known finite set of level/screen names.

### 4.4. Other numeric limits found in the Unity SDK configuration

- Currencies for Resource Event: maximum 20, string consisting only of `[A-Za-z]`.
- Item types for Resource Event: maximum 20, alphanumeric characters only.
- `itemId` in Resource Event: string, maximum 32 characters.
- `cartType` in Business Event: maximum 10 unique values (stated on the Business Event page), maximum 32 characters per the API schema.
- Custom dimensions (`SetCustomDimension01/02/03`): maximum 3 per game, values must be declared in the dashboard beforehand — otherwise the value is silently ignored ("Any value which is not defined in the dashboard will be ignored!").
- Error auto-submission: no more than 10 Error events over the application's lifetime (see section 3.5).

For our nine events (all Design or Progression, no Resource/Business), only the rules from 4.1–4.3 apply directly.

## 5. Verifying that events arrive

### 5.1. Debug mode in the Unity SDK — not via code, but via the inspector

An important clarification to the task's wording: most other GameAnalytics SDKs (Android native, iOS native, JavaScript, standalone C#) do indeed have public runtime methods `SetEnabledInfoLog(true)` / `SetEnabledVerboseLog(true)`. But in the source code of the Unity wrapper (`Runtime/Scripts/GameAnalytics.cs`, current 8.1.0), there is **no** such public static method — verified by a direct search of the file (`grep` does not find `SetEnabledInfoLog`/`SetEnabledVerboseLog`). Instead, in Unity this is enabled as a Settings object configuration:

The official "Debugging | Unity" page:

> The SDK consists of Unity code (C# wrapper) that call code inside some native libraries (iOS / Android). **When playing in the editor the native code is not compiled/used.**

Three modes:

1. **Info Log Editor** — already works in editor Play Mode: shows that an event was added from C# code, but there is no actual sending or server-side validation in this mode ("The events are not validated yet").
2. **Info Log Build** (checkbox in the Settings inspector) — basic information in a real (native) build.
3. **Verbose Log Build** (checkbox in the Settings inspector) — outputs the full JSON of the event actually sent to the GameAnalytics server.

Internally this reads as fields of the settings object:

```csharp
if (SettingsGA.InfoLogBuild)
{
    GA_Setup.SetInfoLog(true);
}
if (SettingsGA.VerboseLogBuild)
{
    GA_Setup.SetVerboseLog(true);
}
```

From this it follows: to see detailed logging on iOS, you need to **enable the checkboxes in the Settings inspector before building**, build the project for an iOS device (real or simulator), and watch the Xcode console output — there is no full verification in the editor, only a check that the call happened from C#.

An additional important warning from the documentation: if an event is sent before the SDK is ready, you'll get:

```
Warning/GameAnalytics: Could not add design event: Datastore not initialized
```

### 5.2. Live viewing (Realtime)

The official "Realtime" page (`docs.gameanalytics.com/products-and-features/analytics-iq/realtime/overview/`):

- The **Live Events** tab shows the last **50** events, updated "every few seconds", with filters by event type, build, and User ID (wildcard patterns with `*` are supported).
- Direct quote: **"Events typically appear within 30 seconds of being sent by the client"** — that is, it usually takes on the order of 30 seconds from sending to appearing in Live Events.
- There is a "Raw JSON" viewing mode — useful for checking the exact content of an event (name, category, value).
- Realtime is intended for debugging/validating integration, not for long-term analytics — Dashboards/Explore/Data Export exist separately for that.

### 5.3. Regular report latency, and how to tell "didn't send" apart from "hasn't been processed yet"

No official exact figure for the latency of standard dashboards specifically (Dashboards/Explore) on the GameAnalytics site was found in the documentation gathered — "not verified" via the docs.gameanalytics.com primary source. Indirect data from community forums (not a GameAnalytics primary source, but repeated many times): on the order of "under 24 hours" for processing until full aggregation in standard reports (discussion on forums.solar2d.com regarding GameAnalytics: "it takes under 24 hours for data to get aggregated by our servers if there are no processing delays" — from 2015, may have changed).

A practical way to distinguish "the event didn't send" from "hasn't been processed yet":

1. Enable **Verbose Log Build** and check in the Xcode console that the event's JSON is actually formed and being sent (meaning the client isn't silent).
2. Check **Live Events** in Realtime — if the event appeared there within ~30 seconds, the collector accepted and validated it; if the event doesn't appear in Realtime at all, the likely cause is a validation schema violation (section 4) or a network problem, not processing latency.
3. If the event is in Realtime but hasn't shown up in the regular dashboard — this is likely normal aggregation latency, not a lost event; wait up to a day and check Explore/Dashboards again.
4. Use the **SDK Status** tab in Realtime — it shows which SDK versions are active and how many events each is sending; useful if the hypothesis is "the SDK isn't reaching the server at all on some devices".

## 6. Offline behavior

Confirmed by documentation (the "SDK Features" page, applicable to the GameAnalytics SDK family, including the native iOS/Android library behavior used in the Unity wrapper):

> Offline: When a device is offline the events are still added to the queue. When the device is online it will submit.

That is, when there's no network, events are not dropped but accumulate in a local on-device queue and are resent automatically once the connection is restored — you don't need to write your own deferred-sending mechanism.

Additionally, from the "Configuration | Unity" page about session and queue mechanics:

> When the session is active you will be able to track events (e.g. after the SDK has been initialized) and the event queue will be running on a low priority thread, batching events and sending them to the server every **8 seconds**.

From this it follows: even with network connectivity, events don't fly out instantly one by one — they're batched and sent once every 8 seconds from a low-priority thread. This matters for interpreting latency during live debugging (see section 5): if you don't see an event in Realtime right away, wait at least one batching cycle (8 seconds) plus network latency before considering the event lost.

No explicit statement of the local queue's size limit on iOS (how many events or how many days are stored offline before overflow) was found in the documentation gathered — **not verified**.

## 7. Apple requirements

### 7.1. Privacy manifest — present, found directly in the package

A direct search of the GitHub repository tree (`GET /repos/GameAnalytics/GA-SDK-UNITY/git/trees/master?recursive=1`) confirms the physical presence of the manifest file inside the native framework shipped with the package:

```
Runtime/Apple/GameAnalytics.xcframework/ios-arm64/GameAnalytics.framework/PrivacyInfo.xcprivacy
Runtime/Apple/GameAnalytics.xcframework/ios-arm64_x86_64-simulator/GameAnalytics.framework/PrivacyInfo.xcprivacy
Runtime/Apple/GameAnalytics.xcframework/tvos-arm64/GameAnalyticsTVOS.framework/PrivacyInfo.xcprivacy
Runtime/Apple/GameAnalytics.xcframework/tvos-arm64_x86_64-simulator/GameAnalyticsTVOS.framework/PrivacyInfo.xcprivacy
```

The file was downloaded and decoded (binary plist → XML via `plutil -convert xml1`); the content is given verbatim below — this is the actual manifest of the current version (8.1.0), not what's declared in marketing materials:

```xml
<key>NSPrivacyAccessedAPITypes</key>
<array>
  <dict>
    <key>NSPrivacyAccessedAPIType</key><string>NSPrivacyAccessedAPICategoryFileTimestamp</string>
    <key>NSPrivacyAccessedAPITypeReasons</key><array><string>C617.1</string></array>
  </dict>
  <dict>
    <key>NSPrivacyAccessedAPIType</key><string>NSPrivacyAccessedAPICategoryUserDefaults</string>
    <key>NSPrivacyAccessedAPITypeReasons</key><array><string>CA92.1</string></array>
  </dict>
</array>

<key>NSPrivacyCollectedDataTypes</key>
<array>
  <!-- Performance Data, Gameplay Content, Other Diagnostic Data, Crash Data,
       Product Interaction, Advertising Data, User ID, Device ID —
       for each of these 8 types: NSPrivacyCollectedDataTypeLinked = true,
       NSPrivacyCollectedDataTypeTracking = false,
       Purposes = [AppFunctionality, Analytics] -->
</array>

<key>NSPrivacyTracking</key><true/>
<key>NSPrivacyTrackingDomains</key>
<array><string>tracking.gameanalytics.com</string></array>
```

The verbatim list of collected data types (`NSPrivacyCollectedDataType…`), each with `Linked = true` (data tied to the user's identity) and purposes `App Functionality` + `Analytics`:

- `PerformanceData` (performance data)
- `GameplayContent` (game content — presumably in-game progress/actions)
- `OtherDiagnosticData` (other diagnostic data)
- `CrashData` (crash data)
- `ProductInteraction` (product interaction)
- `AdvertisingData` (advertising data)
- `UserID` (user identifier)
- `DeviceID` (device identifier)

An important nuance in the manifest worth knowing when filling out App Privacy: at the top level of the manifest, `NSPrivacyTracking = true` is set and one tracking domain, `tracking.gameanalytics.com`, is listed (this technically means the framework declares itself as using a domain that falls under Apple's "tracking" category), but at the same time **each individual entry** in `NSPrivacyCollectedDataTypes` has `NSPrivacyCollectedDataTypeTracking = false` (meaning none of the listed data types is itself flagged as used for cross-platform user tracking). Both facts are taken verbatim from the file itself — reconciling this with ATT is the developer's task (not solved automatically by the SDK): if the app doesn't show ATT and doesn't use IDFA (see 7.2), this doesn't negate the presence of `NSPrivacyTracking=true` and the tracking domain in the framework's own manifest, but it does mean actual data transmission for tracking purposes is not activated on your end unless you explicitly enable such use.

### 7.2. What to declare in App Privacy ("Nutrition Labels" in App Store Connect)

No official page from GameAnalytics with ready-made instructions on "what to put in App Store Connect" was found in the documentation gathered — **not verified** by a GameAnalytics primary source. Below is a conclusion drawn from the actual content of `PrivacyInfo.xcprivacy` (section 7.1), not a guess: data categories that will need to be reflected in your app's App Privacy at minimum because of using this SDK:

- Identifiers (User ID, Device ID) — for the purposes "Analytics" and "App Functionality".
- Diagnostics (Crash Data, Performance Data, Other Diagnostic Data) — for the same purposes.
- Product interaction data (Product Interaction) and game content (Gameplay Content) — that is, the events you actually send (`level_start`, `photo_uploaded`, etc.) formally fall under these categories.
- Advertising Data — present in the manifest regardless of whether you have ads enabled; with the advertising identifier collection disabled (section 7.3), actual collection of this category can be reduced to the absence of IDFA, but the SDK manifest's declaration still mentions this category.

The exact wording of the final app declaration needs to be checked against the actual behavior of the entire app (not just GameAnalytics), so the final completion of the App Privacy form is the developer's/lawyer's responsibility, not something that can be derived solely from one manifest.

### 7.3. IDFA and ATT — can the dialog be avoided

Direct answer: **yes, showing the ATT dialog can be avoided**, if you follow what's described below, and this is confirmed by official documentation and the source code.

Key facts:

1. GameAnalytics **does not show the ATT dialog by itself automatically** — showing it is triggered only by an explicit call to `GameAnalytics.RequestTrackingAuthorization(this)`, which the developer calls at their discretion (see the code example in section 2.5). If this method is not called — and the native `ATTrackingManager.requestTrackingAuthorization` is not called anywhere else in the project either — the dialog will not appear at all.
2. If ATT was not requested, the tracking authorization status remains `notDetermined`, not `authorized`. The documentation says explicitly: "The GameAnalytics Unity SDK uses IDFV (for iOS) as the user id and it will only add IDFA to events if ATT consent status is authorized" — that is, without requesting ATT, no IDFA is added to events at all, and the user identifier is the IDFV (Identifier for Vendor), for which ATT permission is not required.
3. There is additionally an explicit programmatic setting to disable the use of the advertising identifier entirely, found in `Configuration | Unity`:

```csharp
GameAnalytics.EnableAdvertisingIdTracking(false);
```

Verbatim from the documentation: "This function will also force the default generated user id to be fully random on all platforms" — meaning the call not only prohibits the use of IDFA, but also switches the user identifier to a random one (instead of IDFV) on all platforms. This is also confirmed directly in the source code — the public method `EnableAdvertisingIdTracking(bool flag)` is present in `GameAnalytics.cs` (line `1141`, current 8.1.0).
4. The SDK still needs to be initialized in any case, even if ATT was not requested and the advertising identifier is disabled — this in no way blocks the rest of the SDK's functionality (Design/Progression events work the same way).

Bottom-line recommendation for this game (since the goal is to avoid the ATT dialog): **do not call** `GameAnalytics.RequestTrackingAuthorization()`, initialize the SDK directly (`GameAnalytics.Initialize()`), and additionally call `GameAnalytics.EnableAdvertisingIdTracking(false)` before initialization — this gives an explicit, declarative signal of "we do not collect the advertising identifier", rather than merely an absence of the request.

A known practical risk when working with the ATT dialog (even if you decide to enable it someday) — issue **#23** in the repository (`iOS Crashes After "Allow" or "Ask App Not to Track" IDFA Consent Dialog`, open): the iOS app crashes right after the user picks an option in the ATT dialog, if the `NSUserTrackingUsageDescription` key was not added to `Info.plist` — the app survives the crash once, and afterward (since the choice is already saved) the dialog no longer appears and no crash occurs. Since in our case the dialog won't be shown at all, this issue is not relevant — but it's noted here as an argument in favor of the chosen approach of "don't request ATT".

## 8. Web dashboard

### 8.1. Building a funnel from our events

The official "Funnels" page (`docs.gameanalytics.com/products-and-features/analytics-iq/funnels/`):

Steps to create one:

1. **Funnels → Create**.
2. Choose the type: **Standard Funnel** (supports Design, Resource, and Progression events together — what we need, since our nine events mix Design and Progression) or **Progression Funnel** (Progression only, but additionally gives Complete/Start and Fail/Complete metrics).
3. The **Steps** button — add events as funnel steps (for example: `app:open` → `photo:screen_shown` → `photo:uploaded`, a separate funnel `level_fail`/another relevant event → `moves:button_tap`).
4. Steps can be reordered, duplicated, or deleted.
5. **Process** — build the first version of the funnel.
6. Optional filters for segmenting the result.
7. **Save**.

An important nuance of the funnel model in AnalyticsIQ: by default it is an **"Any Order"** funnel — a user is counted as having completed a step if they performed it and all preceding steps, but not necessarily in chronological order. Strict order (**Strict Order**) is available only in SegmentIQ, a separate product. For an honest calculation of "did they reach the capture screen after opening the game", this is worth keeping in mind — Any Order can slightly overstate conversion compared to the intuitive "strictly one after another".

Metrics available in funnel results (both variants): Total conversion, Total churn, Total users, Biggest drop, Step completion, Churn, Total completion; only for Progression Funnel — Complete/Start ratio and Fail/Complete ratio.

### 8.2. Retention

A separate **Retention** page (`docs.gameanalytics.com/products-and-features/analytics-iq/engagement-tools/retention`) and the **Dashboards** section (`docs.gameanalytics.com/products-and-features/analytics-iq/dashboards/overview/`), which has a ready-made "Retention (D1, 7, 30, etc.)" block. Separately, on the metrics page (`events-metrics-and-filtering/metrics`) a definition is given: "Retention reports the daily percent of users who installed on a specific day and then returned N days later", by default for D1–D7 and D14.

### 8.3. CSV export

Confirmed on the Funnels page: funnel results can be exported — "Download the data in a CSV format to analyze in other products", plus a toggle between displaying whole numbers and percentages. Besides funnels, there is a separate product, **Data Export** (PipelineIQ), for a more complete export of raw events — intended for a full export of events/fields/dimensions, not just the results of a single funnel.

## 9. Pitfalls (from community practice and the issue tracker)

All the items below are taken from real issues in the `GameAnalytics/GA-SDK-UNITY` GitHub repository (obtained via the GitHub API, sorted by last updated) — not invented, links are given in the "Sources" section.

- **#57 (open) — compilation breaks on new Unity 6000.x tech streams.** When upgrading to Unity 6.5 (and presumably future 6000.x versions), the build fails with `CS0619`, because `GameAnalytics.cs` uses the deprecated `EditorApplication.hierarchyWindowItemOnGUI` and `EditorUtility.InstanceIDToObject`, which Unity fully removes in newer versions. At the time this document was compiled, the author had reported this for 6.5; reproducibility specifically on 6.3 (our target version) has not been verified — worth testing compilation right after installation, before writing game logic.
- **#58 (open) — a crash specifically on Unity 6.3 in a Standalone build.** A crash in `gameanalytics.dll` (the native cross-platform component, attempting to lock an uninitialized/destroyed mutex). Platform — Windows, not iOS, but it shows that 8.1.0 on 6000.3.x has unresolved native-layer stability issues in general.
- **#23 (open) — iOS crashes right after picking an option in the ATT dialog**, if `NSUserTrackingUsageDescription` was not added to `Info.plist`. Not relevant given the chosen strategy of "don't show ATT" (section 7.3), but critical if that decision changes.
- **#54 (open) — with Manual Session Handling enabled, `EndSession` events are still sent automatically.** The author reports that despite manual calls to `StartSession()`/`EndSession()`, the SDK keeps closing sessions on its own — meaning manual session mode doesn't fully isolate you from the automatic behavior. We use automatic session handling (the documentation recommends not switching to manual unless absolutely necessary) — this risk doesn't affect our integration, but it's worth keeping in mind if there's ever a temptation to enable manual mode.
- **#50 (open) — `null` in a custom dimension crashes the native code on Android**, rather than simply being ignored as a developer might expect (`SDK.SetCustomDimension01(condition ? "value" : null)` — bad code). Although the bug is recorded on Android, the underlying logic (that `null` should "clear" the value rather than crash the SDK) may be unclear on iOS as well — avoid passing `null` to `SetCustomDimension0N` unless this is explicitly documented behavior for the specific platform.
- **#46 (open) — a request to make the External Dependency Manager (EDM4U) optional.** Partially closed by the 8.1.0 changes (Android became self-contained), but the UPM installation page still requires installing EDM4U as the first step — see the contradiction in section 1.2; worth checking in practice whether EDM4U is really required for a clean iOS-only integration (no Android, no AdMob).
- **#41 (open) — errors in code samples in the Chinese/localized version of the documentation** (the reporter notes inconsistencies in the examples). General lesson: don't copy code blindly from old blog posts/forums — cross-check against `GameAnalytics.cs` in the repository (as done in this document) at the slightest doubt about a signature.
- **General community practice (Stack Overflow, Unity/Solar2D/Roblox forums)** — numerous threads on "events don't show up in the dashboard" almost always come down to one of three causes: (a) testing in the editor instead of a real build (section 5.1: events don't actually go out in the editor); (b) a violation of the event name validation schema (section 4) — the event is silently rejected by the server; (c) the SDK wasn't initialized before the event was sent (section 2.3) — in that case the log has an explicit `Datastore not initialized` warning, which is easy to miss if Info Log isn't enabled.

## Sources

- GameAnalytics Unity SDK — Get Started: https://docs.gameanalytics.com/event-tracking-and-integrations/sdks-and-collection-api/game-engine-sdks/unity
- GameAnalytics Unity SDK — Configuration: https://docs.gameanalytics.com/event-tracking-and-integrations/sdks-and-collection-api/game-engine-sdks/unity/configuration
- GameAnalytics Unity SDK — Event Tracking: https://docs.gameanalytics.com/event-tracking-and-integrations/sdks-and-collection-api/game-engine-sdks/unity/event-tracking
- GameAnalytics Unity SDK — Debugging: https://docs.gameanalytics.com/event-tracking-and-integrations/sdks-and-collection-api/game-engine-sdks/unity/debug/
- Design Events (event type description): https://docs.gameanalytics.com/events-metrics-and-filtering/event-types/design-events
- Event Tracking and Cardinality Limits: https://docs.gameanalytics.com/event-tracking-and-integrations/data-retention-and-limits/event-tracking-and-cardinality-limits
- Collection API — Event Types (exact validation JSON schemas): https://docs.gameanalytics.com/event-tracking-and-integrations/sdks-and-collection-api/api/event-types/
- Realtime — Overview: https://docs.gameanalytics.com/products-and-features/analytics-iq/realtime/overview/
- Funnels: https://docs.gameanalytics.com/products-and-features/analytics-iq/funnels/
- Retention: https://docs.gameanalytics.com/products-and-features/analytics-iq/engagement-tools/retention
- Metrics (definition of Retention D1–D14): https://docs.gameanalytics.com/events-metrics-and-filtering/metrics
- Dashboards — Overview: https://docs.gameanalytics.com/products-and-features/analytics-iq/dashboards/overview/
- GA-SDK-UNITY repository (GitHub): https://github.com/GameAnalytics/GA-SDK-UNITY
  - `CHANGELOG.md`: https://raw.githubusercontent.com/GameAnalytics/GA-SDK-UNITY/master/CHANGELOG.md
  - `README.md`: https://raw.githubusercontent.com/GameAnalytics/GA-SDK-UNITY/master/README.md
  - Public API source code: `Runtime/Scripts/GameAnalytics.cs`
  - Enumerations: `Runtime/Scripts/Enums.cs`
  - Settings object: `Runtime/Scripts/Setup/Settings.cs`
  - Error handling/auto-submission: `Runtime/Scripts/Events/GA_Debug.cs`
  - Design events (client side): `Runtime/Scripts/Events/GA_Design.cs`
  - Privacy manifest (actual file content): `Runtime/Apple/GameAnalytics.xcframework/ios-arm64/GameAnalytics.framework/PrivacyInfo.xcprivacy`
  - Latest release (version/date): https://api.github.com/repos/GameAnalytics/GA-SDK-UNITY/releases/latest (tag `8.1.0`, `2026-08-21T09:06:31Z`)
  - Issues used in section 9: #23, #41, #46, #50, #54, #57, #58 — https://github.com/GameAnalytics/GA-SDK-UNITY/issues
- OpenUPM — package page: https://openupm.com/packages/com.gameanalytics.sdk/

