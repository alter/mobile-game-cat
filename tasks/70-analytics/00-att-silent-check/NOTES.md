# ATT silent-failure check — documentation pass, 2026-08-27

Answers what documentation can settle before anyone touches a device. Does
not change `status:` or `verify:` — this is a finding, not a completion; the
task's own VERIFY (a live device/dashboard check) is still outstanding.

Read first: `tasks/70-analytics/01-sdk-integration/NOTES.md` (today, confirms
GameAnalytics needs no card, no player cap — D17 doesn't block this),
`tasks/70-analytics/00-att-silent-check/task.txt`, `tasks/DECISIONS.md` D9
(line 317).

## 1. What Apple's rules actually say, and who's saying it

**Primary Apple source**, fetched directly, 2026-08-27:
developer.apple.com/documentation/bundleresources/privacy-manifest-files
(page footer: "Copyright © 2026 Apple Inc.", i.e. the current live page, not
an archived one). Verbatim:

> `NSPrivacyTrackingDomains` — An array of strings that lists the internet
> domains your app or third-party SDK connects to that engage in tracking.
> **If the user has not granted tracking permission through the App
> Tracking Transparency framework, network requests to these domains fail
> and your app receives an error.**

This is Apple's own documentation, not a developer's summary of it — it
states plainly that requests to a *declared tracking domain* fail, and that
the failure surfaces to the app as an error (a failed request the calling
code can see), not a request that silently succeeds with empty data. Whether
GameAnalytics's SDK code *logs or surfaces* that error anywhere a developer
would notice is a separate, SDK-level question — see §2.

**Developer reports, distinguished as such, corroborating but not
authoritative:** a live Apple Developer Forums thread
(developer.apple.com/forums/thread/744659, retrieved 2026-08-27) has a
developer describing exactly this in the wild: "I've run an Instruments
network capture of our iOS app and the Points of Interest track lists
faults due to undisclosed tracking domains" — i.e. the block is real and
observable via Instruments, not just a documentation claim. Several
marketing/SDK-vendor blog posts (Branch, AppsFlyer, Kochava, Singular,
bitrise.io — all dated 2024, retrieved 2026-08-27) restate the same
mechanism in their own words ("iOS system will block outgoing network
connections," "network requests... will fail until the user gives ATT
consent"). These corroborate Apple's own page; they are not independent
evidence beyond it.

**Not settled by documentation: whether this is enforced identically in
the iOS Simulator.** No Apple page or developer report found addresses this
either way — see §5.

## 2. What GameAnalytics declares, and whether it can be configured out of it

**Read directly from the shipped package**, already done today by this
project's own research
(`knowledge/analytics/04-gameanalytics-unity-usage.md` §7.1, current SDK
8.1.0, `CHANGELOG.md` dated 2026-08-21): the file was pulled from the actual
repository tree and decoded from binary plist (`plutil -convert xml1`), not
taken from marketing copy. Verbatim from that decode:

```xml
<key>NSPrivacyTracking</key><true/>
<key>NSPrivacyTrackingDomains</key>
<array><string>tracking.gameanalytics.com</string></array>
```

**One domain only: `tracking.gameanalytics.com`.** This is the fact D9
already had. What D9 didn't check, and this pass did: **is that the same
domain events actually get sent to?** GameAnalytics's own Collection API
documentation (docs.gameanalytics.com/event-tracking-and-integrations/sdks-and-collection-api/api/setup,
retrieved 2026-08-27) states plainly:

> A Collector is a GameAnalytics server... that receive and collect game
> events being submitted using the HTTPS protocol. **All official
> GameAnalytics SDK's are using this same API.**
> API endpoint for production: **`api.gameanalytics.com`**
> API endpoint for sandbox: **`sandbox-api.gameanalytics.com`**
> Routes: `POST /v2/<game_key>/init`, `POST /v2/<game_key>/events`

**These are different domains.** The privacy manifest declares
`tracking.gameanalytics.com` as tracking; the actual init/events traffic —
what fires when `GameAnalytics.NewDesignEvent()` / `NewProgressionEvent()`
are called, i.e. every event this project's nine-event funnel depends on —
goes to `api.gameanalytics.com`, which is **not** listed in
`NSPrivacyTrackingDomains`. Apple's blocking mechanism (§1) operates on
requests to the *declared* domains specifically; a domain not declared
there isn't subject to it by the documented mechanism.

**This was not traced through the SDK's own source code in this pass** — I
confirmed the two domains from GameAnalytics's own documentation pages, not
by decompiling the binary and finding a literal reference to
`tracking.gameanalytics.com` inside it (the way the Android AAR was opened
directly in `01-sdk-integration/NOTES.md` §5). So this is strong
documentation-level evidence that ordinary event submission is unaffected,
not a source-level proof that the SDK never contacts the tracking domain
for anything (some ancillary feature — attribution matching, an
install-tracking ping — could still exist and would fail per §1 if it
exists, without affecting the event pipeline). That residual gap is exactly
what a live check settles and documentation can't — see §5.

**Does GameAnalytics offer a configuration where it doesn't declare itself
as tracking at all?** No such option was found in the SDK's configuration
docs or the Unity wrapper's public API
(`GameAnalytics.cs`, per the knowledge doc's source-level read). The
manifest is a static file shipped inside the compiled `.xcframework`; there
is no runtime call that edits it. See §3.

## 3. Does `EnableAdvertisingIdTracking(false)` change any of this?

**No — it changes runtime behavior only, not the shipped manifest.** Per
the knowledge doc's own source-level reading (`GameAnalytics.cs:1141`) and
GameAnalytics's Unity configuration docs (docs.gameanalytics.com/.../unity/configuration,
retrieved 2026-08-27): the call stops the SDK from reading/sending the IDFA
and switches the user identifier to a fully random one instead of IDFV. It
is a Unity/C#-level runtime flag evaluated when `Initialize()` runs — it
cannot rewrite `PrivacyInfo.xcprivacy`, a static resource baked into the
`.framework` bundle at Apple's build-time processing step, long before any
of the app's own code executes. So:

- The manifest keeps declaring `NSPrivacyTracking = true` and
  `tracking.gameanalytics.com` regardless of this call — App Store Connect's
  App Privacy questionnaire still needs to account for that domain's
  presence (already flagged in the knowledge doc §7.2), independent of what
  the call does at runtime.
- What the call *does* do, per D9 and confirmed above: it stops the SDK
  from actively pursuing IDFA-based flows, which is the behavior most
  likely to be the thing that would ever touch a tracking-flavored
  endpoint. It reduces exposure; it does not remove the declaration.

## 4. The Android side — is there an equivalent silent-drop risk?

**No OS-level equivalent exists on Android.** There is no Android
counterpart to iOS's privacy-manifest-driven, ATT-gated network block —
Android has no "declared tracking domain" concept enforced by the OS
itself; permissions gate *device APIs* (e.g. reading the advertising ID),
not arbitrary outbound HTTPS requests to a named host. Nothing in Android's
platform documentation blocks a plain HTTPS POST based on a manifest
declaration the way `NSPrivacyTrackingDomains` does on iOS.

Cross-checked against today's other finding
(`01-sdk-integration/NOTES.md` §5, this project, same day, from
downloading and inspecting the real `gameanalytics.aar` directly): the
Android SDK declares only `INTERNET` and `ACCESS_NETWORK_STATE`, no
`AD_ID`; it reaches for the advertising ID via reflection and a
`bindService` call to Google Play services **only if the app already
declares the permission elsewhere**, per GameAnalytics's own Configuration
page ("If the app has the required permissions, GameAnalytics will track
the advertising IDs..."), and `EnableAdvertisingIdTracking(false)` stops
even that attempt. None of this touches whether `init`/`events` calls to
`api.gameanalytics.com` succeed — that's a plain HTTPS request gated by
nothing Android-specific. **Bottom line: Android has no mechanism that
would silently drop these events the way D9 worried iOS might.**

## 5. The honest bottom line — what documentation can't settle, and whether a simulator suffices

**What documentation settles:** ordinary event submission
(`AppOpen`/`LevelStart`/etc., everything this project's four metrics need)
targets `api.gameanalytics.com`, a domain GameAnalytics does not declare as
tracking, so it is not subject to the ATT-gated block Apple documents for
domains that *are* declared (`tracking.gameanalytics.com`). D9's fear —
"whether events still arrive could not be confirmed from any published
source" — is now substantially de-risked by GameAnalytics's own API
documentation, which was the missing piece. `EnableAdvertisingIdTracking(false)`
doesn't change the manifest but does keep the SDK away from the one kind of
behavior (IDFA pursuit) most likely to involve a tracking-flavored call.

**What documentation cannot settle:**
- Whether `tracking.gameanalytics.com` is contacted by the SDK for anything
  at all in practice (an attribution ping, an install postback) — not
  confirmed or ruled out at the source-code level in this pass, only
  inferred from API docs describing the *event* pipeline specifically.
- Whether the OS-level block Apple documents is enforced identically in the
  **iOS Simulator**. No Apple page or developer report found addresses
  simulator behavior for `NSPrivacyTrackingDomains` either way — this is a
  genuine documentation gap, not an oversight in this pass.
- Whether GameAnalytics's SDK, if a request to any endpoint *did* fail,
  would surface that failure anywhere visible short of the dashboard simply
  staying empty (it queues and retries on reconnect per the knowledge doc
  §"pitfalls," which is about connectivity loss, not about a
  permanently-blocked domain — an indefinitely-retried, indefinitely-failing
  request is exactly the "no error raised" shape D9 warned about).

**Would a simulator run answer it?** Very likely yes, **for the actual
question this task's VERIFY line cares about** — "does a test event reach
the dashboard from a build that never calls ATT" — because that traffic
goes to `api.gameanalytics.com`, which isn't gated by ATT/tracking-domain
enforcement on any platform, simulator or device; the other agent's
`01-sdk-integration/NOTES.md` also notes GameAnalytics's SDK doesn't
distinguish simulator from device for sending events, and this project has
already shipped simulator-only iOS builds with zero Apple account
(`60-shell-build/08-mid-level-save/NOTES.md`, cited there). It would
**not** definitively answer the narrower, lower-stakes question of whether
`tracking.gameanalytics.com` specifically gets blocked the same way on
simulator as on a real device, since that particular enforcement detail
isn't documented for either environment.

**The half hour that documentation can't replace:** a build with
`EnableAdvertisingIdTracking(false)` called and `RequestTrackingAuthorization`
never called, run once — on simulator first, since nothing found here
requires a physical device for the event-delivery question — watching the
Xcode console with GameAnalytics's "Info Log Build" / "Verbose Log Build"
enabled (per the knowledge doc §1) for any network error lines, and then
checking the GameAnalytics dashboard (Realtime view, ~30 seconds) for the
test event. If it shows up on simulator, the P0 is answered without waiting
on a device or an Apple account at all. If it doesn't, or if the console
shows unexplained network faults, that's the point to retest on a real
device to separate "GameAnalytics genuinely doesn't work without ATT" from
"the simulator doesn't reproduce the block/unblock behavior faithfully."

## Sources

- developer.apple.com/documentation/bundleresources/privacy-manifest-files — fetched 2026-08-27 (current page, footer "Copyright © 2026 Apple Inc.")
- developer.apple.com/forums/thread/744659 — fetched 2026-08-27 (developer report, Instruments network-fault observation)
- docs.gameanalytics.com/event-tracking-and-integrations/sdks-and-collection-api/api/setup — fetched 2026-08-27 (Collection API endpoints: `api.gameanalytics.com`, `sandbox-api.gameanalytics.com`)
- docs.gameanalytics.com/event-tracking-and-integrations/sdks-and-collection-api/game-engine-sdks/unity/configuration — fetched 2026-08-27 (`EnableAdvertisingIdTracking` behavior)
- `knowledge/analytics/04-gameanalytics-unity-usage.md` §7.1–7.3 (this project, compiled 2026-08-24/updated since — decoded `PrivacyInfo.xcprivacy` from the actual 8.1.0 package, source-level read of `GameAnalytics.cs:1141`)
- `tasks/70-analytics/01-sdk-integration/NOTES.md` (this project, 2026-08-27 — D17 not a blocker, GameAnalytics free-tier confirmed live, Android AAR inspected directly)
- `tasks/DECISIONS.md` D9 (line 317)
- Secondary/corroborating, all fetched 2026-08-27, all dated 2024 in-page: dataprotectionreport.com, bitrise.io, singular.net, branch.io/help, appsflyer.com/glossary, kochava.com — restating Apple's own mechanism, not adding independent evidence
- Not found / open gap: any Apple or GameAnalytics documentation addressing iOS Simulator enforcement of `NSPrivacyTrackingDomains` specifically
