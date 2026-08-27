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

---

# Correction — 2026-08-27, following `VERIFY.md`'s `verify:failed`

An independent verification context ran `strings` over `classes.dex` — this
document only ran it over `AndroidManifest.xml` and grepped source — and
found what line 143 above said wasn't there. The claim was wrong, and the
fix belongs here, not in `VERIFY.md`, which is left as written.

## 1. Reproduced myself, not taken on trust

```
$ APK=game/build/android/CatShelter.apk
$ unzip -oq "$APK" classes.dex AndroidManifest.xml -d /tmp/apk_reproduce
$ cd /tmp/apk_reproduce

$ strings classes.dex | grep -i "ad_id"
                                                    # empty — no output

$ strings classes.dex | grep -i "advertising"
/Lcom/unity3d/player/AndroidAdvertisingIdHelper;
9com.google.android.gms.ads.identifier.AdvertisingIdClient
getAdvertisingIdInfo
"nativeOnAndroidAdvertisingIdResult

$ strings classes.dex | grep "Lcom/google/android/gms/ads/identifier"
                                                    # empty — no compiled class reference

$ strings AndroidManifest.xml | grep -i "ad_id\|advertising"
                                                    # empty — the manifest itself is clean
```

Same four strings the verifier quoted, same absence of `AD_ID` and of a
compiled GMS class reference. The correction below rests on this, not on
`VERIFY.md`'s word for it.

## 2. What line 141–150 above got wrong, and what is actually true

**Wrong:** "`strings` over the built APK for `AD_ID`/`advertising` returns
nothing." It returns nothing for `AD_ID`. It does not return nothing for
`advertising` — the four strings above are in every build of this project,
today, regardless of GameAnalytics.

**What they are.** `Lcom/unity3d/player/AndroidAdvertisingIdHelper;` is a
class in Unity's own Android player — shipped by the engine itself, not by
any package this project chose to add, and not removable by not adding
GameAnalytics. It contains string literals for
`com.google.android.gms.ads.identifier.AdvertisingIdClient`,
`getAdvertisingIdInfo`, and a native callback name
`nativeOnAndroidAdvertisingIdResult` — this is Unity's built-in advertising-ID
helper, present so that `UnityEngine.iOS`/Android advertising ID APIs and any
Unity Ads/Analytics-family package can retrieve it if asked.

**Why it is dormant.** The class reference is a *string*, not a compiled
type — `strings classes.dex | grep "Lcom/google/android/gms/ads/identifier"`
finds nothing, confirming Unity's helper reaches for the GMS
`AdvertisingIdClient` reflectively (`Class.forName`-shaped lookup), the same
pattern established for the GameAnalytics AAR in `70-analytics/01-sdk-integration/NOTES.md`'s
research section. With no `play-services-ads-identifier` artifact actually
linked into this APK and no `AD_ID` permission declared, that reflective
lookup throws `ClassNotFoundException`, caught, and goes nowhere. Present,
compiled in by the engine, unconditionally — but inert without a GMS
artifact behind it. "No advertising ID is collected today" is still true;
"nothing in the binary reaches for one" was not, and that is the correction.

**Wake condition.** Any dependency that actually links
`com.google.android.gms:play-services-ads-identifier` (a real ads/attribution
SDK — AdMob, an attribution package, anything in that family) would give
Unity's already-present helper a class to find, at which point the reflective
call would succeed and `AD_ID` would also need to be declared in the manifest
for the ID itself to be non-zeroed (per Play's own policy, confirmed live
during the `70-analytics/01` research pass). GameAnalytics specifically does
not do this — see the next correction.

## 3. The GameAnalytics prediction, corrected against the real AAR

Line 151–152 above guessed: "adding GameAnalytics's Android SDK (or any
package that transitively depends on
`com.google.android.gms:play-services-ads-identifier`)... would put `AD_ID`
back." **This was already corrected once, in
`tasks/70-analytics/01-sdk-integration/NOTES.md`** (§5 of its research
section, 2026-08-27), by downloading and inspecting the actual
`gameanalytics.aar` from `github.com/GameAnalytics/GA-SDK-ANDROID`:

- Its own `AndroidManifest.xml` declares only `INTERNET` and
  `ACCESS_NETWORK_STATE` — no `AD_ID`, no GMS dependency merged in.
- Its `classes.jar` reaches the advertising ID the same way Unity's own
  helper does — reflectively, via
  `com.google.android.gms.ads.identifier.internal.IAdvertisingIdService`
  and a class literally named `Reflection.class` — not a compiled Gradle
  dependency.
- GameAnalytics's own docs (`docs.gameanalytics.com`, Unity Configuration
  page, fetched 2026-08-27): *"If the app has the required permissions,
  GameAnalytics will track the advertising IDs"* — conditional on the
  permission already existing, not something it adds.

So the two dormant reflective probes in this project — Unity's own
`AndroidAdvertisingIdHelper` (§2 above) and GameAnalytics's — are the **same
shape of risk for the same reason**: neither compiles in nor declares the
permission; both would need a *third*, genuinely ads-flavored dependency
(AdMob, an attribution SDK) to have anything to find. Adding bare
GameAnalytics does not, by itself, wake either one.

## 4. What changes now that the SDK is actually landing

As of `70-analytics/01-sdk-integration`, `com.gameanalytics.sdk: 8.1.0` is
now declared in `game/Packages/manifest.json` — added today, after this
audit and its `VERIFY.md` were both written. **Neither examined a manifest
or a `classes.dex` that had this package resolved into it** — no Unity build
has run since that dependency was added (the 01 task was explicitly told not
to run one). This audit's every finding above, and the verifier's, describes
the APK as it was *before* GameAnalytics existed in the project at all.

**This audit must be re-run after the first Android build with the package
resolved**, and specifically should look for:

- `INTERNET` / `ACCESS_NETWORK_STATE` — GameAnalytics's own manifest declares
  both (§3 above); expect them to appear for the first time.
- Whether `AD_ID` appears — per §3, it should not, from GameAnalytics alone.
  If it does, something else changed (the package version drifted from what
  `01`'s NOTES inspected, or a second dependency was added alongside it).
- `strings classes.dex | grep -i advertising` again — expect the same four
  Unity-native strings, *plus* now GameAnalytics's own
  `IAdvertisingIdService`/reflection-based lookup class, still reflective,
  still dormant absent `AD_ID`.
- The `androidx.startup.InitializationProvider` entry already in this
  manifest may grow more `meta-data` children (GameAnalytics uses
  `androidx.startup` conventions in some SDK versions) — not a red flag by
  itself, just worth reconciling against the existing table.
- Whether `EnableAdvertisingIdTracking(false)` (now called from
  `Shell/GameAnalyticsSink.cs`, per `01`'s NOTES) shows up as expected in the
  binary and whether `RequestTrackingAuthorization` — still absent from
  source, grepped in `01`'s NOTES — stays absent.

## 5. iOS gained the matching finding — what the platform difference means for the two stores' declarations

`tasks/60-shell-build/17-permission-audit/NOTES.md` (also 2026-08-27) found
the Objective-C equivalent — `DeviceSettings.mm`'s three `ASIdentifierManager`/
`ATTrackingManager` uses sit inside `#if UNITY_USES_IAD`, and
`Preprocessor.h:206` sets `UNITY_USES_IAD 0` because Unity detects no iAd use
in the scripts. Its own words: *"the advertising-identifier path is compiled
out of the iOS build,"* versus Android where — confirmed here, independently,
twice now — *"the same helper is unconditionally present."*

**That difference is real and it is not cosmetic for privacy declarations.**
"The binary cannot reach the identifier" (iOS, compiled out) and "the binary
can reach it but does not, today, because nothing supplies it a class to find"
(Android, dormant) are two different facts, and the two stores ask the
question separately: Apple's App Privacy "nutrition label" and Google Play's
Data Safety form are both filled out per-binary, not per-project, so an
honest answer to "does this app access the advertising identifier" is **"no,
the code cannot"** on iOS and **"no, not currently, but the capability is
compiled in and would activate if a linked dependency supplied the class"**
on Android — the same practical outcome today, a different declaration if
either platform's dependency set changes, and Android is the one to re-check
first since it is the one gaining a new dependency (§4).

**`UNITY_USES_IAD` is also not a constant** — the iOS NOTES already say so —
so this needs re-checking on iOS too the next time any package is added
there, for the same reason as here: what Unity compiles in depends on what it
finds in the project, not on what this document assumed.
