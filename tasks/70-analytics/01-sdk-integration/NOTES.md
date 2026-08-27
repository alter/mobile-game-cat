# Is this phase actually blocked by D17? — research pass, 2026-08-27

**Not blocked.** D17 defers two accounts (the model-provider spend cap, the
Apple Developer Program); neither is GameAnalytics. `00-att-silent-check` and
`01-sdk-integration` need a GameAnalytics account, which D9 already
established needs no card and no player cap, and which is checked live below
and still holds today. The one place D17 brushes this phase is that
`00`'s task wording says "on device," and no Apple account of any kind exists
yet — but the same simulator method `60-shell-build/08` already used without
a developer team applies here too, so that isn't a real blocker either. This
phase was swept up with the other two by association, not by an actual
dependency.

No adb, no build, no `Assets` edits were used to reach this — reading,
`grep`, and live web sources only.

## 1. What D9, D17, and the two tasks actually say

**D9** (`tasks/DECISIONS.md:317`, decided 2026-08-24): "GameAnalytics has no
player cap and needs no card." ATT is deliberately never requested;
`EnableAdvertisingIdTracking(false)` is called instead. The "silent failure"
risk it names is exactly what `00-att-silent-check` exists to close.

**D17** (`tasks/DECISIONS.md:565`, decided 2026-08-27) defers exactly two
accounts: the model-provider spend cap (blocks the traits Worker / metric 2)
and the Apple Developer Program (blocks Gate 1's ad account + store page, and
Gate 3's TestFlight/Play listing). It does not mention GameAnalytics, and its
own "What is left" section calls out Gate 2 as "the only gate that can be run
at all" — but Gate 2 is `30-levels-solver`, unrelated to analytics. D17 never
states that `70-analytics` is blocked; the phase inherited P0/P1 priority and
sat untouched because the two accounts it names dominate the conversation,
not because anyone checked GameAnalytics's own requirements.

**`00-att-silent-check/task.txt`** (P0, `status:todo`, `depends:
60-shell-build`): "Prove that GameAnalytics events reach the dashboard from a
build that never calls ATT... Minimal SDK init on a real device build, one
test event sent... VERIFY: GameAnalytics dashboard shows at least one event
after the test build runs on device." Its only listed dependency is
`60-shell-build`, which is done — not any account task.

**`01-sdk-integration/task.txt`** (P1, `status:todo`, `depends:
70-analytics/00-att-silent-check`): "Initialise the GameAnalytics SDK with
iOS keys, without ever requesting ATT... SDK package added, iOS game key and
secret key configured... OUTCOME: SDK initialises on launch." Depends only on
`00`, not on any of the D17 accounts.

**`knowledge/analytics/04-gameanalytics-unity-usage.md`** (compiled
2026-08-24, primary sources `docs.gameanalytics.com` and the
`GA-SDK-UNITY`/`GA-SDK-ANDROID` repos) already answers most of the mechanical
questions below in detail — key setup via an editor Settings object, manual
`Initialize()`, `EnableAdvertisingIdTracking(false)` confirmed in source at
`GameAnalytics.cs:1141`, the `PrivacyInfo.xcprivacy` contents verbatim
(`NSPrivacyTracking=true`, tracking domain `tracking.gameanalytics.com`). This
pass adds the live-web check that document didn't need to do (D9's claim
holding today) and the Android AD_ID question, which that iOS-scoped document
doesn't cover.

## 2. Live check: does GameAnalytics still offer a free tier with no card, no player cap?

Checked live, 2026-08-27, not from memory:

- `https://www.gameanalytics.com/pricing` (Google-cached snippet, fetched
  today): **"Build dashboards, run LiveOps, explore market insights, and chat
  with your data using the AI Agent. No MAU cap. No credit card required."**
  — verbatim, on the pricing page itself, describing the base/free plan.
- The same page, fetched directly: **"Start free with no MAU limits, then add
  advanced capabilities whenever you're ready to scale"** — the plans shown
  above the free tier are $49/mo (Growth), "from $499/mo" (a segmentation
  tier), and $499/mo with 10 seats (a data-pipeline tier); the base plan
  carries no listed price because it is the free one the marketing copy
  describes.
- `https://www.gameanalytics.com/trust/terms` (fetched 2026-08-27, page
  itself dated "Oct 29, 2025" per its own metadata): sections 5.5.2–5.5.3 say
  a **credit card may be required for the 14-day "Free Trial"** of a paid
  tier, which auto-converts to a paid subscription if not cancelled. **This
  is a different thing from the always-free base plan** — the trial gives
  time-limited access to premium features and needs a card because it bills
  automatically; the free/Core plan the game would actually use does not
  appear in that clause and is the one the pricing page states needs no card.

**D9 holds, checked today, not assumed.** The one nuance D9 didn't spell out —
and worth recording so nobody signs up for the wrong thing — is to use the
account/plan flow that lands on the free plan, not the "start your free
trial" button on a paid tier, which does ask for a card.

## 3. What wiring the SDK involves on top of what exists

Read `game/Assets/Core/Analytics.cs` and `game/Assets/Shell/GameBoot.cs`
directly.

`Core/Analytics` is already fully built and is the **entire** call surface
the rest of the game uses — nine event names in `AnalyticsEventNames`, a
`Configure(designSink, progressionSink)` entry point that takes two
`Action<...>` delegates, name validation (`EnsureValid`) run once at
`Configure` and again per call, and nine named helper methods
(`AppOpen()`, `LevelStart(int)`, etc.) that the presentation layer already
calls. None of this is GameAnalytics-shaped — the sinks are generic
delegates, which is exactly what lets `Core` stay engine-free (comment at
`Analytics.cs:33-35`).

`Shell/GameBoot.cs:17-19` calls `Core.Analytics.Configure(null, null)` at
launch, with a comment: *"Analytics sink: no-op until the GameAnalytics SDK
task wires the real one. Configure(null, null) keeps calls valid and silent."*
Every `Design`/`Progression` call already in the codebase runs today — it
just invokes a null delegate (`_designSink?.Invoke(...)`, a no-op) and goes
nowhere.

**What `01-sdk-integration` actually has to add, precisely:**

1. Add `com.gameanalytics.sdk` to `game/Packages/manifest.json` (scoped
   registry `package.openupm.com`, per the knowledge doc — a Unity package
   install, no account needed).
2. Create the GameAnalytics Settings ScriptableObject
   (`Window → GameAnalytics → Select Settings`), add the iOS platform, and
   fill in the Game Key/Secret Key — from a real GameAnalytics account (see
   §4).
3. Add exactly one `GameAnalytics` GameObject to the startup scene
   (`Window → GameAnalytics → Create GameAnalytics object`).
4. In `GameBoot.cs`, replace `Core.Analytics.Configure(null, null)` with two
   real delegates that call `GameAnalytics.NewDesignEvent(name)` /
   `GameAnalytics.NewProgressionEvent(status, progression01, score)` — a
   small adapter translating `Core`'s generic `(string, double, string)` /
   `(string, int, string)` shapes into the SDK's specific call signatures
   (the mapping table for this already exists in
   `knowledge/analytics/04-gameanalytics-unity-usage.md` §3.8).
5. Call `GameAnalytics.EnableAdvertisingIdTracking(false)` before
   `GameAnalytics.Initialize()`, and never call
   `RequestTrackingAuthorization()` — both per D9, and both a one-line code
   change, not new architecture.
6. Enable "Info Log Build" / "Verbose Log Build" checkboxes in the Settings
   inspector for local verification (SCOPE line 3), which is an editor
   checkbox, not code.

Nothing here touches `Core` — the whole point of the existing split is that
this task only ever edits `Shell` plus editor-only Unity assets.

## 4. Human step vs. agent step, and what compiles without keys

**Human step, and it cannot be an agent action:** creating the GameAnalytics
account itself and the "game" entry inside it, to obtain a real Game
Key/Secret Key pair. This happens through a login on gameanalytics.com (the
editor's "Login" button in the Settings inspector does this same thing, or
keys can be typed in manually once obtained from the web dashboard — either
way, an account with an email and a password, or an OAuth login, has to
exist first). This is genuinely no different in kind from creating any other
free SaaS account — it needs a human with an email address, once, and no
payment method, per §2.

**Everything else is agent-doable without that account existing yet:**

- Editing `game/Packages/manifest.json` to add the dependency and scoped
  registry — plain JSON, no login required to declare the dependency (Unity
  resolves it from the public OpenUPM registry at package-restore time, which
  needs network access but no GameAnalytics credentials).
- Writing the `GameBoot.cs` adapter code described in §3, item 4 — it only
  needs the SDK's public C# API surface (`GameAnalytics.NewDesignEvent`,
  etc.), which is part of the package once fetched, not tied to any specific
  key.
- Writing `EnableAdvertisingIdTracking(false)` before `Initialize()`.

**Does it compile without keys?** Yes, for the C# side — the Settings
ScriptableObject asset can exist with empty Game Key/Secret Key fields and
the project still compiles; nothing in the public API requires a key at
compile time, only at runtime (an empty/wrong key would fail to authenticate
against the collector, which surfaces as a runtime log line, not a build
error). The one piece that is a genuine editor action rather than a script
edit is creating the Settings asset and the `GameAnalytics` GameObject via
the `Window → GameAnalytics` menu — this is normally done inside the Unity
Editor GUI; whether it is scriptable via a batch-mode `-executeMethod` call
(as `BuildScript` already does for the platform switch, per
`90-android/02-build-pipeline`) was **not checked in this pass** — worth
a five-minute look before `01` starts, since it changes whether the whole
task is agent-doable or needs one interactive editor session.

**Bottom line on the human step:** it is exactly one thing — sign up, create
a game, copy two strings — and it is cheap and reversible, unlike the two
accounts D17 actually deferred. There is no reason it needs to wait for
those.

## 5. `00-att-silent-check`: runnable without an Apple account? Android's own AD_ID risk?

**Runnable without any Apple account, on the same evidence this project
already produced.** `00`'s task wording says "a real device build" / "runs
on device," and D17's context (quoted in `60-shell-build/08-mid-level-save/NOTES.md`,
"Done on the simulator, not on a device — there is no developer team yet")
shows this project has already built and run iOS Simulator builds from Xcode
**with zero Apple account of any kind** — no developer team, paid or free.
GameAnalytics's own SDK does not distinguish device vs. simulator for
sending events (it opens a normal network connection out), and ATT itself is
not being requested either way, so there is nothing in `00`'s actual
mechanism that needs a physical device or a paid Apple Developer Program
membership — the literal word "device" in the task's VERIFY line is stricter
than the check needs to be. **This is a task-wording gap worth fixing, not a
real blocker**, and I have not edited `00`'s files (out of scope for this
pass, and the task said touch only `70-analytics/01` for the write-up).

**Android's own AD_ID risk — checked by downloading and inspecting the real
GameAnalytics Android AAR, not by guessing.** The permission audit
(`90-android/10-permission-audit/NOTES.md`, same day) flagged, as a forward
prediction, that "any package that transitively depends on
`com.google.android.gms:play-services-ads-identifier`... would put `AD_ID`
back" — written before this check. Verifying that prediction against the
actual artifact:

```
$ curl -sL -o gameanalytics.aar \
    https://raw.githubusercontent.com/GameAnalytics/GA-SDK-ANDROID/master/GA/aar/gameanalytics.aar
$ unzip -o -q gameanalytics.aar AndroidManifest.xml
$ cat AndroidManifest.xml
```

```xml
<?xml version="1.0" encoding="utf-8"?>
<manifest xmlns:android="http://schemas.android.com/apk/res/android"
    package="com.gameanalytics.sdk.android" >
    <uses-sdk android:minSdkVersion="21" />
    <uses-permission android:name="android.permission.INTERNET" />
    <uses-permission android:name="android.permission.ACCESS_NETWORK_STATE" />
    <application>
        <service
            android:name="com.gameanalytics.sdk.errorreporter.GameAnalyticsExceptionReportService"
            android:permission="android.permission.BIND_JOB_SERVICE"
            android:process=":gameAnalyticsExceptionReporter" />
    </application>
</manifest>
```

**Correction to the prediction in `10-permission-audit`: bare GameAnalytics
does not declare or merge `AD_ID` at all** — only `INTERNET` and
`ACCESS_NETWORK_STATE`. Digging into `classes.jar` inside the same AAR shows
how it actually gets the advertising ID when available:

```
$ unzip -o -q classes.jar -d classes_extracted
$ grep -a -o "com\.google\.android\.gms[a-zA-Z0-9\.]*\|IAdvertisingIdService\|getAdvertisingIdInfo" \
    classes_extracted/com/gameanalytics/sdk/GooglePlayServicesClient*.class \
    classes_extracted/com/gameanalytics/sdk/device/GADevice.class \
    classes_extracted/com/gameanalytics/sdk/utilities/Reflection.class
```

Found: `com.google.android.gms.ads.identifier.service.START` (an `Intent`
action string, used to `bindService` against Play services directly — not a
compiled library dependency), `com.google.android.gms.ads.identifier.internal.IAdvertisingIdService`,
and `getAdvertisingIdInfo` inside a class literally named `Reflection.class`.
That is: the SDK **reaches for** the advertising ID via runtime reflection
and an `Intent`/`bindService` call to Google Play services, rather than
declaring `com.google.android.gms:play-services-ads-identifier` as a Gradle
dependency the way an ads SDK would. It does not itself carry or merge the
`AD_ID` permission into a consuming app's manifest — confirmed directly from
`docs.gameanalytics.com`'s own Configuration page (fetched 2026-08-27):
*"If the app has the required permissions, GameAnalytics will track the
advertising IDs on certain platforms such as Android and iOS"* — the
conditional "if" is the point: it uses AD_ID **only if something else in the
app already declared the permission**, and `EnableAdvertisingIdTracking(false)`
(the same D9-mandated call) additionally stops it from even attempting this.

**So: adding bare GameAnalytics to this project would not, by itself, put
`AD_ID` in the Android manifest.** The real risk `10-permission-audit`
correctly flagged in principle — that *some* future dependency reintroduces
it — is still real, but the culprit would have to be a genuinely
ads/attribution-flavored package (an AdMob/UnityAds/attribution SDK), not
GameAnalytics itself. Worth updating that file's phrasing the next time it is
touched, since this pass is scoped to `70-analytics` only and does not edit
it here.

## Sources

- `tasks/DECISIONS.md` D9 (line 317), D17 (line 565)
- `tasks/70-analytics/00-att-silent-check/task.txt`, `tasks/70-analytics/01-sdk-integration/task.txt`
- `knowledge/analytics/04-gameanalytics-unity-usage.md` (compiled 2026-08-24)
- `game/Assets/Core/Analytics.cs`, `game/Assets/Shell/GameBoot.cs`, `game/Packages/manifest.json`
- `tasks/60-shell-build/08-mid-level-save/NOTES.md` ("Done on the simulator... no developer team yet")
- `tasks/90-android/10-permission-audit/NOTES.md` (this project, same day — the AD_ID prediction corrected above)
- https://www.gameanalytics.com/pricing — fetched 2026-08-27 ("No MAU cap. No credit card required.")
- https://www.gameanalytics.com/trust/terms — fetched 2026-08-27, page dated 2025-10-29 (§5.5.2–5.5.3, free-trial card clause)
- https://docs.gameanalytics.com/event-tracking-and-integrations/sdks-and-collection-api/game-engine-sdks/unity/configuration — fetched 2026-08-27 (advertising-id tracking wording)
- https://github.com/GameAnalytics/GA-SDK-ANDROID — `GA/aar/gameanalytics.aar`, downloaded and inspected directly 2026-08-27 (manifest + `classes.jar` contents, quoted above)

---

# Built: everything that needs no account, 2026-08-27

Follows straight from the research above — the only human step is a signup,
so everything up to that step is built now. No Unity build was run (another
agent may need the toolchain); what that leaves unvalidated is called out
below rather than guessed at.

## 1. Package added, version checked against the live registry

`game/Packages/manifest.json` now carries the scoped registry and the
dependency:

```json
"scopedRegistries": [
  { "name": "package.openupm.com", "url": "https://package.openupm.com",
    "scopes": ["com.gameanalytics"] }
],
"dependencies": {
  "com.gameanalytics.sdk": "8.1.0",
  ...
```

`8.1.0` is not carried over from the knowledge doc by assumption — checked
live, today, against the actual registry Unity's Package Manager talks to for
this scoped registry (not just GitHub's release page):

```
$ curl -s "https://package.openupm.com/com.gameanalytics.sdk" | python3 -c "
import json,sys
d=json.load(sys.stdin)
print('dist-tags:', d.get('dist-tags'))
print('has 8.1.0:', '8.1.0' in d['versions'])
"
dist-tags: {'latest': '8.1.0', 'patch@7.3.8': '7.3.8'}
has 8.1.0: True
```

Cross-checked against `https://api.github.com/repos/GameAnalytics/GA-SDK-UNITY/releases/latest`
→ `"tag_name": "8.1.0"`, `"published_at": "2026-08-21T09:06:31Z"` — same
version, still current five days later.

## 2. The real sink, wired behind Core/Analytics without Core learning what GameAnalytics is

New file: `game/Assets/Shell/GameAnalyticsSink.cs`. `Core/Analytics.cs` is
untouched — it still only knows `Action<string, double, string>` /
`Action<string, int, string>`, exactly as `Analytics.Configure`'s signature
already required. `GameBoot.cs` now reads:

```csharp
var (designSink, progressionSink) =
    GameAnalyticsSink.TryConfigure(gameObject);
Core.Analytics.Configure(designSink, progressionSink);
```

replacing the old `Configure(null, null)`. `check-core-purity.sh` still
passes because `GameAnalyticsSink` lives in `Assets/Shell`, not `Assets/Core`
— confirmed below, not asserted.

**What `GameAnalyticsSink.TryConfigure` does**, reading the real
`GA-SDK-UNITY` source (`GameAnalytics.cs`, `Setup/Settings.cs`, both
downloaded and read directly, not guessed from the docs):

- Reads `analytics-keys.txt` beside the save via `Application.persistentDataPath`
  — game key on line 1, secret key on line 2 — the same try/catch-and-silently-
  return-nothing idiom `GameBoot.CaptureRequested`/`CaptureStub` use for
  `capture.txt` and `EveningReminder` uses for `notify-in-seconds.txt`. Missing
  file, short file, or a blank line → returns `(null, null)`, identical to
  today's `Configure(null, null)`. No exception path logs anything — the
  common case (no file yet) touches no `Debug.Log` call at all, so nothing
  repeats every launch.
- If both keys are present: finds or adds the iOS platform entry on
  `GameAnalyticsSDK.GameAnalytics.SettingsGA` (`Settings.AddPlatform` and
  `Settings.UpdateKeys` are plain public/static methods, not editor-gated —
  confirmed by reading `Setup/Settings.cs` directly), injects the two keys
  into the **in-memory** settings singleton via `Settings.UpdateKeys(index,
  gameKey, secretKey)` — this never writes to the `.asset` file on disk, so
  no key is ever serialised anywhere in the repo — sets `InfoLogBuild = true`
  (SCOPE's "debug logging enabled," already the SDK's own default, set
  explicitly so intent doesn't ride on a default that could change), calls
  `GameAnalyticsSDK.GameAnalytics.EnableAdvertisingIdTracking(false)`, adds
  exactly one `GameAnalyticsSDK.GameAnalytics` component to the `GameBoot`
  GameObject if one isn't already there, then calls
  `GameAnalyticsSDK.GameAnalytics.Initialize()`.
- **No call to `RequestTrackingAuthorization` anywhere in this file, or
  anywhere else in the project** — grepped after writing it:

```
$ grep -rn "RequestTrackingAuthorization" game/Assets
game/Assets/Shell/GameAnalyticsSink.cs:109:                // RequestTrackingAuthorization; call
game/Assets/Shell/GameAnalyticsSink.cs:112:                // file contains no call to RequestTrackingAuthorization
```

Both hits are inside comments (the two lines quoted in §3 below) — there is
no line anywhere in the project that actually calls
`RequestTrackingAuthorization(...)`, confirmed by reading both matches.

  The comment above the `EnableAdvertisingIdTracking(false)` call quotes D9
  directly: *"ATT is deliberately not requested... The dialog costs installs
  and we use nothing behind it."*

- Translates Core's raw progression names (`"level_start"`/`"level_win"`/
  `"level_fail"`, from `AnalyticsEventNames`) into
  `GameAnalyticsSDK.GAProgressionStatus.Start/Complete/Fail` — that mapping
  has to live here, in Shell, because it is GameAnalytics-shaped and Core
  must not know it exists. Design events pass straight through
  (`NewDesignEvent(name)` / `NewDesignEvent(name, value)`) since Core's
  design names are already the exact colon-hierarchical strings GameAnalytics
  expects (`"app:open"`, etc.).
- If `SettingsGA` turns out to be null (the package's own `Settings.asset`
  scaffold isn't there — see §4 below) even though keys were supplied, it
  logs one warning and returns `(null, null)` rather than guessing at how to
  create that asset from a build.

## 3. `EnableAdvertisingIdTracking(false)` before `Initialize()`, never `RequestTrackingAuthorization`

Both requirements hold in the code as written — `EnableAdvertisingIdTracking(false)`
is the line immediately before the `AddComponent`/`Initialize()` sequence,
and the grep above is the check that the forbidden call is absent project-wide,
not just in the new file.

## 4. The keys are the human step — what I could not do without one, and what I could not validate without Unity

**What genuinely needs the owner, and cannot be automated:** creating the
GameAnalytics account and a game inside it, to get the Game Key and Secret
Key. Nothing above invents or stores either string. `grep -rn` for the words
"gamekey\|secretkey\|game_key\|secret_key" with an actual value attached
finds nothing in this diff — only the parsing code, never a value.

**One thing this pass could not build, and says so rather than guessing:**
GameAnalytics's own `InitAPI()` (`GameAnalytics.cs`) does
`Resources.Load("GameAnalytics/Settings", ...)` in a real build, and only
auto-creates the asset via `#if UNITY_EDITOR` — i.e. **only inside the Unity
Editor**, the first time something touches `GameAnalytics.SettingsGA`. There
is currently no `Assets/Resources/GameAnalytics/Settings.asset` in this
repo (checked: `find game/Assets -ipath "*GameAnalytics*Settings*"` → nothing),
because creating one correctly means either running the Unity Editor once
(out of scope for this pass — no Unity build was run) or hand-writing a
Unity `ScriptableObject` `.asset` YAML file referencing a `MonoScript` GUID
that only exists once the package has actually been resolved into
`Library/PackageCache` — which also needs Unity to run. Hand-writing that
GUID without having seen it would be exactly the kind of guessing the task
said not to do, so it was not attempted.

**Consequence, stated plainly:** the very first time anyone opens this
project in the Unity Editor after this package resolves, `Settings.asset`
gets created empty (no keys, per §1's read of `InitAPI`) automatically — that
is expected, not a bug, and needs no manual step beyond opening the project.
Until that first Editor open happens, `GameAnalyticsSink.TryConfigure` would
find `SettingsGA == null` even if `analytics-keys.txt` existed, log the one
warning described in §2, and stay in no-op mode — so the "no key, no Unity
Editor run yet" state is safe, but so is "key present, no Editor run yet" —
neither throws, neither spams a log.

**Also not validated, for the same reason:** whether
`game/Assets/Shell/GameAnalyticsSink.cs` actually compiles once Unity
resolves `com.gameanalytics.sdk` — every API surface used
(`GameAnalytics.SettingsGA`, `Settings.AddPlatform`, `Settings.UpdateKeys`,
`Settings.InfoLogBuild`, `GameAnalytics.EnableAdvertisingIdTracking`,
`GameAnalytics.Initialize`, `GameAnalytics.NewDesignEvent`,
`GameAnalytics.NewProgressionEvent`, `GAProgressionStatus`) was checked
against the actual current SDK source (`GameAnalytics.cs`,
`Setup/Settings.cs`, both downloaded from
`github.com/GameAnalytics/GA-SDK-UNITY` at `master` and read directly, not
guessed from the docs), so the signatures should match — but nothing here
substitutes for an actual Editor compile, which this pass was told not to
run.

## What was checked, green

```
$ bash build/check-core-purity.sh
Core is engine-free: OK

$ dotnet test build/core-tests/core-tests.csproj -v q --nologo
...
Пройден!   : не пройдено     0, пройдено   152, пропущено     0, всего   152, длительность 267 ms. - core-tests.dll (net8.0)
```

152 passing, same count the rule names — unaffected, since nothing under
`Assets/Core` changed.

## The owner's one-minute instruction

1. Go to `https://www.gameanalytics.com/`, click sign up, and create a free
   account (no card — checked live today, §2 of the research section above).
2. Create one game entry for CatShelter (the dashboard asks for a name and a
   genre; anything reasonable is fine, it's editable later).
3. Open that game's **Settings** in the dashboard. Copy the **Game Key** and
   the **Secret Key** — two short hex-looking strings, both on the same
   settings page.
4. On the test device (or the simulator's container), create a file named
   `analytics-keys.txt` next to the save file
   (`Application.persistentDataPath` — the same folder `capture.txt` and
   `notify-in-seconds.txt` already go in), two lines:
   ```
   <game key>
   <secret key>
   ```
5. Relaunch the app. That's the whole instruction — nothing else changes,
   nothing to rebuild, no code to touch. The next launch after that file
   exists is what `00-att-silent-check` needs to look at the GameAnalytics
   dashboard for.

## Status

Everything buildable without an account is done: package declared at a
version checked live, the sink wired behind `Core/Analytics` with `Core`
unchanged, `EnableAdvertisingIdTracking(false)` in place with D9's reasoning
in the comment, `RequestTrackingAuthorization` absent (grepped), no key of
any kind committed, the no-key behaviour identical to today's
`Configure(null, null)` (checked by reading the early-return paths, not
assumed), tests green, purity check green. `status:` moves to `review`.
`verify:` untouched — this pass did not run a device build, and VERIFY 1 and
2 in `task.txt` both need one.

### It compiles, and the APK confirms the AAR prediction — 2026-08-27

The one gap this task's write-up admitted — that `GameAnalyticsSink.cs` had
never been compiled against the resolved package — is closed. A full Unity
Android build, run for this purpose:

```
Unity -batchmode -quit -nographics -executeMethod BuildScript.BuildAndroidPlayer
UNITY_EXIT=0
grep -cE "error CS" build/ga-compile.log  ->  0
game/build/android/CatShelter.apk        ->  27,988,228 bytes (was 27,850,892)
```

**And the manifest changed exactly as the AAR inspection predicted.** Before
the package, the APK declared `POST_NOTIFICATIONS` and Unity's own
`DYNAMIC_RECEIVER_NOT_EXPORTED_PERMISSION`. After:

```
uses-permission: android.permission.INTERNET
uses-permission: android.permission.ACCESS_NETWORK_STATE
uses-permission: android.permission.POST_NOTIFICATIONS
```

`INTERNET` and `ACCESS_NETWORK_STATE` are the two the AAR's own manifest
declares, and **`AD_ID` is still absent** — which is what was predicted from
reading the AAR rather than guessing, and it now has a built artefact behind
it rather than an inference.

**No settings asset appeared.** `find game/Assets -iname "*GameAnalytics*"`
returns only `GameAnalyticsSink.cs` and its meta. A batch build did not create
the SDK's settings asset, so nothing capable of capturing a key into a
committed file exists today. That does not settle what a first *Editor* open
would do — this project builds headless and nobody has opened the editor since
the package landed — so the risk is narrowed, not closed, and it is on the
verifier's list.

`90-android/10-permission-audit` must be re-run against this build: it examined
the pre-package APK and its permission table is now one build out of date.
