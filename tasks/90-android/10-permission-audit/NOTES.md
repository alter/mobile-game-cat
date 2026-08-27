# The audit, 2026-08-27

Run against `game/build/android/CatShelter.apk` (27,850,892 bytes, built
22:08 today — after the `BuildScript.UseTarget` fix recorded in
`90-android/02-build-pipeline/NOTES.md`, so this is the first Android manifest
in the project that actually carries its package-injected entries).

```
AAPT2=/Applications/Unity/Hub/Editor/6000.3.22f1/PlaybackEngines/AndroidPlayer/SDK/build-tools/36.0.0/aapt2
$AAPT2 dump permissions game/build/android/CatShelter.apk
```

```
package: com.DefaultCompany.game
uses-permission: name='android.permission.POST_NOTIFICATIONS'
permission: com.DefaultCompany.game.DYNAMIC_RECEIVER_NOT_EXPORTED_PERMISSION
uses-permission: name='com.DefaultCompany.game.DYNAMIC_RECEIVER_NOT_EXPORTED_PERMISSION'
```

`$AAPT2 dump xmltree game/build/android/CatShelter.apk --file AndroidManifest.xml`,
in full:

```
N: android=http://schemas.android.com/apk/res/android (line=2)
  E: manifest (line=2)
    A: http://schemas.android.com/apk/res/android:versionCode(0x0101021b)=1
    A: http://schemas.android.com/apk/res/android:versionName(0x0101021c)="1.0" (Raw: "1.0")
    A: http://schemas.android.com/apk/res/android:installLocation(0x010102b7)=2
    A: http://schemas.android.com/apk/res/android:compileSdkVersion(0x01010572)=36
    A: http://schemas.android.com/apk/res/android:compileSdkVersionCodename(0x01010573)="16" (Raw: "16")
    A: package="com.DefaultCompany.game" (Raw: "com.DefaultCompany.game")
    A: platformBuildVersionCode=36
    A: platformBuildVersionName=16
      E: uses-sdk (line=8)
        A: http://schemas.android.com/apk/res/android:minSdkVersion(0x0101020c)=25
        A: http://schemas.android.com/apk/res/android:targetSdkVersion(0x01010270)=36
      E: supports-screens (line=12)
        A: http://schemas.android.com/apk/res/android:anyDensity(0x0101026c)=true
        A: http://schemas.android.com/apk/res/android:smallScreens(0x01010284)=true
        A: http://schemas.android.com/apk/res/android:normalScreens(0x01010285)=true
        A: http://schemas.android.com/apk/res/android:largeScreens(0x01010286)=true
        A: http://schemas.android.com/apk/res/android:xlargeScreens(0x010102bf)=true
      E: uses-feature (line=19)
        A: http://schemas.android.com/apk/res/android:glEsVersion(0x01010281)=0x00030000
      E: uses-feature (line=20)
        A: http://schemas.android.com/apk/res/android:name(0x01010003)="android.hardware.vulkan.version" (Raw: "android.hardware.vulkan.version")
        A: http://schemas.android.com/apk/res/android:required(0x0101028e)=false
      E: uses-feature (line=23)
        A: http://schemas.android.com/apk/res/android:name(0x01010003)="android.hardware.touchscreen" (Raw: "android.hardware.touchscreen")
        A: http://schemas.android.com/apk/res/android:required(0x0101028e)=false
      E: uses-permission (line=27)
        A: http://schemas.android.com/apk/res/android:name(0x01010003)="android.permission.POST_NOTIFICATIONS" (Raw: "android.permission.POST_NOTIFICATIONS")
      E: permission (line=29)
        A: http://schemas.android.com/apk/res/android:name(0x01010003)="com.DefaultCompany.game.DYNAMIC_RECEIVER_NOT_EXPORTED_PERMISSION" (Raw: "com.DefaultCompany.game.DYNAMIC_RECEIVER_NOT_EXPORTED_PERMISSION")
        A: http://schemas.android.com/apk/res/android:protectionLevel(0x01010009)=0x00000002
      E: uses-permission (line=33)
        A: http://schemas.android.com/apk/res/android:name(0x01010003)="com.DefaultCompany.game.DYNAMIC_RECEIVER_NOT_EXPORTED_PERMISSION" (Raw: "com.DefaultCompany.game.DYNAMIC_RECEIVER_NOT_EXPORTED_PERMISSION")
      E: application (line=35)
        A: http://schemas.android.com/apk/res/android:label(0x01010001)=@0x7f0d0021
        A: http://schemas.android.com/apk/res/android:icon(0x01010002)=@0x7f0c0000
        A: http://schemas.android.com/apk/res/android:extractNativeLibs(0x010104ea)=true
        A: http://schemas.android.com/apk/res/android:appCategory(0x01010545)=0
        A: http://schemas.android.com/apk/res/android:appComponentFactory(0x0101057a)="androidx.core.app.CoreComponentFactory" (Raw: "androidx.core.app.CoreComponentFactory")
        A: http://schemas.android.com/apk/res/android:enableOnBackInvokedCallback(0x0101066c)=true
          E: meta-data (line=42) name="unity.splash-mode" value=0
          E: meta-data (line=45) name="unity.splash-enable" value=true
          E: meta-data (line=48) name="unity.launch-fullscreen" value=true
          E: meta-data (line=51) name="unity.render-outside-safearea" value=true
          E: meta-data (line=54) name="notch.config" value="portrait|landscape"
          E: meta-data (line=57) name="unity.auto-report-fully-drawn" value=true
          E: meta-data (line=60) name="unity.strip-engine-code" value=true
          E: meta-data (line=63) name="unity.run-without-focus" value=false
          E: meta-data (line=66) name="unity.auto-set-game-state" value=true
          E: activity (line=70)
            A: android:theme=@0x7f0e00a1
            A: android:name="com.unity3d.player.UnityPlayerGameActivity"
            A: android:enabled=true
            A: android:exported=true
            A: android:launchMode=2
            A: android:screenOrientation=13
            A: android:configChanges=0x40003fff
            A: android:hardwareAccelerated=false
            A: android:resizeableActivity=true
              E: intent-filter (line=80)
                  E: category name="android.intent.category.LAUNCHER"
                  E: action name="android.intent.action.MAIN"
              E: meta-data name="unityplayer.UnityActivity" value=true
              E: meta-data name="android.app.lib_name" value="game"
              E: meta-data name="WindowManagerPreference:FreeformWindowSize" value=@0x7f0d0002
              E: meta-data name="WindowManagerPreference:FreeformWindowOrientation" value=@0x7f0d0000
              E: meta-data name="notch_support" value=true
              E: layout (line=102) minWidth=400.000000px minHeight=300.000000px
          E: receiver (line=107)
            A: android:name="com.unity.androidnotifications.UnityNotificationManager"
            A: android:exported=false
          E: meta-data (line=111) name="com.unity.androidnotifications.exact_scheduling" value=0
          E: provider (line=115)
            A: android:name="androidx.startup.InitializationProvider"
            A: android:exported=false
            A: android:authorities="com.DefaultCompany.game.androidx-startup"
              E: meta-data name="androidx.emoji2.text.EmojiCompatInitializer" value="androidx.startup"
              E: meta-data name="androidx.lifecycle.ProcessLifecycleInitializer" value="androidx.startup"
```

## SDK levels, from the manifest itself

`minSdkVersion=25`, `targetSdkVersion=36`, `compileSdkVersion=36`. Matches what
`90-android/02-build-pipeline/NOTES.md` reported from `aapt dump badging`
(`sdkVersion: 25`, `targetSdkVersion: 36`) — no drift between the two dumps.

## Every permission, every declared component

| item | who added it | what player action needs it | what breaks without it |
|---|---|---|---|
| `android.permission.POST_NOTIFICATIONS` | `com.unity.mobile.notifications` package, wired for the evening reminder in `90-android/09-notifications` (`Shell/EveningReminder.cs`) | On API 33+, the system prompt fires when the reminder is scheduled after level 2 (per `09`'s own verify log: no dialog before that point) | No reminder can be posted on API 33+; below 33 the permission is granted automatically and this line is moot |
| `com.DefaultCompany.game.DYNAMIC_RECEIVER_NOT_EXPORTED_PERMISSION` (self-declared `<permission>` + matching `<uses-permission>`) | AndroidX Core / Unity manifest merger, not written by us | Nothing a player does — this is a signature-level permission the app defines on itself so its own dynamically-registered (`registerReceiver`), non-exported broadcast receiver is provably owned by this app on the pre-Tiramisu compat path. Standard boilerplate wherever `androidx.core` registers a receiver at runtime | The receiver registration this backs would be less strict about who can deliver to it pre-API 33; it raises no user-visible prompt either way |
| `<uses-feature glEsVersion=0x00030000>` | Unity engine, from the Graphics API setting (OpenGL ES 3.0) | Every player, implicitly — engine minimum | App would not render; not filterable by `required`, glEsVersion is always load-bearing |
| `<uses-feature android.hardware.vulkan.version required=false>` | Unity engine default | None — explicitly optional | Nothing; kept `false` so Play does not hide the app from devices without Vulkan |
| `<uses-feature android.hardware.touchscreen required=false>` | Unity engine default | None — explicitly optional | Nothing; kept `false` so Chromebooks / non-touch devices are not filtered out by Play |
| `activity com.unity3d.player.UnityPlayerGameActivity` | Unity engine, the app's only entry point | Every launch | The app; this is the LAUNCHER activity |
| `receiver com.unity.androidnotifications.UnityNotificationManager` (`exported=false`) | `com.unity.mobile.notifications`, same feature as the permission above | Fires the scheduled `AlarmManager` broadcast that posts the reminder | The reminder notification never appears even if permission was granted |
| `provider androidx.startup.InitializationProvider` (`exported=false`) → `EmojiCompatInitializer`, `ProcessLifecycleInitializer` | `androidx.startup`, pulled in transitively by the notifications package (`androidx.emoji2` for notification text rendering, `androidx.lifecycle` for the process-lifecycle awareness the alarm/receiver plumbing uses) | None directly — internal plumbing, non-exported, requests nothing at runtime | Notification text could lose emoji rendering support and the lifecycle-aware scheduling path used by `09` would need another mechanism |

That accounts for every line in both dumps. Nothing is left unexplained.

## What is conspicuously absent, and why — checked, not assumed

**`CAMERA`, `READ_EXTERNAL_STORAGE`, `READ_MEDIA_IMAGES` — all absent.**
`90-android/04-picker-plugin` — the task that would add the camera capture
path and request `CAMERA` — is still `status:todo` (its own `labels.txt`
confirms this; `find game/Assets/Plugins/Android` returns nothing, no Android
picker plugin exists yet). So today's clean manifest is not evidence the
picker was built permission-minimally; it is evidence the picker has not been
built at all on this platform yet. **This audit runs ahead of its own stated
dependency** (`10-permission-audit`'s `labels.txt` lists `depends:
90-android/04-picker-plugin`, which has not shipped). Re-run this table once
`04` lands, and confirm then — not now — that the storage permissions stay
absent per that task's SCOPE (Photo Picker / `ACTION_IMAGE_CAPTURE`, no
`READ_MEDIA_IMAGES`).

**`com.google.android.gms.permission.AD_ID` — absent.** `grep -rn "AD_ID"`
and `grep -rn "advertisingId\|AdvertisingId"` over `game/Assets` return
nothing; `strings` over the built APK for `AD_ID`/`advertising` returns
nothing. `grep -rn "EnableAdvertisingIdTracking"` also returns nothing — not
because the Android side skipped D9's rule, but because **GameAnalytics is
not integrated on either platform yet**: `game/Assets/Core/Analytics.cs` and
`Shell/GameBoot.cs` both currently wire the sink as a no-op, with a comment
that it stays that way "until the GameAnalytics SDK task wires the sink in."
There is no package in the project yet that could pull in
`play-services-ads-identifier` and merge `AD_ID` back into the manifest.
**What would put it there**: adding GameAnalytics's Android SDK (or any
package that transitively depends on `com.google.android.gms:play-services-ads-identifier`)
without also excluding it — the fix, matching D9's `EnableAdvertisingIdTracking(false)`
call, is `<uses-permission android:name="com.google.android.gms.permission.AD_ID" tools:node="remove"/>`
in a manifest merge, plus the `EnableAdvertisingIdTracking(false)` call before
`Initialize()` that D9 already mandates. This mirrors the iOS NOTES.md
instruction to re-audit after `70-analytics/01`; the same instruction applies
here.

**No `SCHEDULE_EXACT_ALARM`.** `90-android/09-notifications/NOTES.md` records
this as a deliberate choice (`exact_scheduling` meta-data value is `0` above,
confirming it): an evening reminder does not need exact delivery.

## Comparison against iOS (`60-shell-build/17-permission-audit/NOTES.md`)

| | iOS | Android | verdict |
|---|---|---|---|
| Notification permission | requested (implicit via `UNUserNotificationCenter`, no plist key needed pre-authorization) | `POST_NOTIFICATIONS` | same feature, platform-correct mechanism on each side — justified |
| Camera | `NSCameraUsageDescription` present (`50-photo/08`, `status:in_progress`) | absent — `90-android/04-picker-plugin` is `status:todo`, nothing built yet | **not a platform difference — a completeness gap.** iOS is further along on the same feature; Android's manifest will grow a `CAMERA` line once `04` ships. Not accidental, not yet applicable — just early |
| Photo library | no permission — `PHPickerViewController` runs out-of-process | (not yet applicable — no Android picker exists) | can't compare yet; `04`'s SCOPE explicitly commits to the Android Photo Picker for the same no-permission parity |
| Self-referential broadcast-receiver permission (`DYNAMIC_RECEIVER_NOT_EXPORTED_PERMISSION`) | no iOS equivalent | present | **platform-required**, not accidental — Android's exported-component security model has no iOS analogue; iOS delivers local notifications without a receiver at all |
| Tracking / advertising ID | ATT never requested (D9) | `AD_ID` absent (checked above) | same policy, same reason on both platforms: no analytics SDK wired in yet |
| Purchasing / analytics packages removed as dead weight | `com.unity.purchasing`, `com.unity.analytics`, `com.unity.modules.unityanalytics` removed, `17`'s finding | not checked here — out of this task's SCOPE, which is manifest permissions/components/features, not package list pruning | not directly comparable; worth a follow-up if the Android package manifest (`manifest.json`) carries the same three |

**Sharpest difference:** it isn't a real platform asymmetry at all — it's that
this audit landed while its own dependency (`04-picker-plugin`) is still
`todo`. The current Android manifest (one permission, `POST_NOTIFICATIONS`)
looks cleaner than iOS's (one permission, `NSCameraUsageDescription`) only
because Android hasn't built the camera/gallery feature yet, not because it
needs less from the OS to do the same job. Everything else — the
self-referential receiver permission, the two `required=false` features — is
genuinely Android-only plumbing with no iOS counterpart, and each is
accounted for above.

## Against the VERIFY list

1. **Met** — `aapt2 dump permissions` output pasted whole above; every line
   matched to a row in the table.
2. **Met** — no `AD_ID` or advertising-id string anywhere in `Assets` or in
   the built APK's strings table.
3. **Not checked here** — requires an install and a tap-through on a device
   or emulator, which is out of scope for this static, no-adb pass per this
   task's instructions. Left for the NATIVE context that owns the emulator.

## What to do next

Re-run this audit after `90-android/04-picker-plugin` ships (it will add
`CAMERA`, and should add nothing else), and again after `70-analytics/01`
wires GameAnalytics in (watch specifically for `AD_ID` reappearing and for
`INTERNET`/`ACCESS_NETWORK_STATE` arriving with the SDK — neither is in this
manifest today).
