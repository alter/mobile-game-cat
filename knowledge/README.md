# Technology stack knowledge catalog

Compiled 2026-08-24 for the "Rescued Kitten" project
(`cat-shelter-mvp.md`, `cat-shelter-tech.md`, `cat-shelter-tasks.md`).

Purpose: give the agent that will write the code precise information on the
exact versions chosen in the concept — instead of it relying on memory of
outdated releases and inventing calls that don't exist. This is exactly what
section 0 of `cat-shelter-tech.md` talks about.

---

## Rules of this catalog

1. **Every fact with a source.** A claim without a link to an open page is
   an error, not a minor detail.
2. **Where nothing was found, it says so.** The notes "no reliable source
   found" and "not verified" are placed deliberately. They are more useful
   than a plausible invention.
3. **Prose in Russian, code verbatim.** API names, command-line keys,
   parameters and code snippets are given as in the original source and are
   not translated.
4. **This is a snapshot as of August 24, 2026, not eternal truth.** Before
   relying on a version number or a store requirement, open the linked
   source.

---

## What surfaced during collection — read before starting work

Three findings touch not the code, but the concept's decisions themselves.

### 1. The "day-1 retention > 35%" threshold is twice the market rate

Personally verified against the primary source: according to
[GameAnalytics, 2025 Mobile Gaming Benchmarks](https://gameanalytics.com/reports/2025-mobile-gaming-benchmarks/)
(11,600 games, 2024 data), the median day-1 retention for puzzle games is
**19.66–20.74%**. The overall median across all games is about 17–18.5%.

In `cat-shelter-mvp.md` the threshold is set at "> 35%," and failing it is
interpreted as "no reason to come back." In fact 35% is **not a viability
threshold, but a level noticeably above the genre average**. A game could be
perfectly viable at 25% and would be shut down under this rule.

Separately: the figure "puzzle games have D1 around 32%," common on blogs,
was not confirmed when checked against the primary source. Details and other
benchmarks are in
[`analytics/02-benchmarks-and-attribution.md`](analytics/02-benchmarks-and-attribution.md).

What to do about this is your call, but the decision needs to be made
**before** spending 400 dollars, not after.

### 2. Apple's requirement is confirmed, but the date is more precise than in the concept

Personally verified against
[Apple's announcement](https://developer.apple.com/news/?id=ueeok6yw), verbatim:

> Starting April 28, 2026, apps and games uploaded to App Store Connect need to
> meet the following minimum requirements: iOS and iPadOS apps must be built with
> the iOS 26 & iPadOS 26 SDK or later...

The wording "starting April 2026" is correct; the exact date is **April 28,
2026**. Important clarification: the requirement concerns the build tool
(in effect, Xcode 26 or newer is needed), not the minimum iOS version the
game runs on — the developer sets that separately. These are two different
numbers that are easy to confuse. Details are in
[`ios/01-appstore-requirements-2026.md`](ios/01-appstore-requirements-2026.md).

### 3. A first-party Unity MCP server exists, but it isn't free under the terms

Section 2 of `cat-shelter-tech.md` lists "Unity MCP, its own, first-party" as
a given. It does indeed exist — in the `com.unity.ai.assistant` package, in
pre-release state (2.0.0-pre.1), and Claude Code is explicitly named among
supported clients. But it requires a Unity Cloud project and an active
subscription to Unity AI tools. The third-party solution
[CoplayDev/unity-mcp](https://github.com/CoplayDev/unity-mcp) (13,619 stars,
verified via the GitHub API) sets no such conditions. Both are examined in
[`agents/01-unity-mcp.md`](agents/01-unity-mcp.md).

---

### 4. Verify on a real device before building measurements on this

The GameAnalytics package's privacy manifest has `NSPrivacyTracking = true`
and declares the domain `tracking.gameanalytics.com`. Apple's rule is that
domains from `NSPrivacyTrackingDomains` **get blocked by the system if the
player hasn't given ATT permission**. The plan to "not ask for ATT" rests on
the assumption that events go to a different domain, and this one applies
only when the advertising identifier is enabled. This could not be confirmed
from open sources.

The check takes half an hour: build a test build, don't call
`RequestTrackingAuthorization`, send an event, confirm it reaches the web
dashboard. If it doesn't arrive, you'll have to either show the ATT dialog or
take a different tool. The failure here is silent: events simply won't show
up, and this will be discovered after spending 400 dollars on advertising.

---

## Catalog structure

### Foundation

- [`00-versions.md`](00-versions.md) — summary of verified versions and what
  in the concept is already outdated. **Start here.**
- [`00-vendor-lock-in.md`](00-vendor-lock-in.md) — **the exit cost of every
  free service**: whether you can take your data with you, how much export
  costs, signs of freemium bait-and-switch, confirmed cases of free-tier
  cutbacks.

### Unity

- [`unity/01-unity-6.3-lts.md`](unity/01-unity-6.3-lts.md) — releases and
  support timelines, what's new in 6.3, the new 2D physics API on Box2D v3,
  known regressions.
- [`unity/02-2d-urp-mobile.md`](unity/02-2d-urp-mobile.md) — setting up URP
  2D for mobile, Sprite Atlas, texture compression, draw order, the cost of
  2D lighting.
- [`unity/03-ui-toolkit-runtime.md`](unity/03-ui-toolkit-runtime.md) — UI
  Toolkit at runtime: architecture, UXML/USS, drag-and-drop, safe area,
  performance, and an honest assessment of "whether to take it at all."
- [`unity/04-test-framework.md`](unity/04-test-framework.md) — tests for the
  rules core, an assembly definition without `UnityEngine`, NUnit, coverage.
- [`unity/05-headless-build-ci.md`](unity/05-headless-build-ci.md) — building
  from the command line, BuildScript, licensing in CI, log analysis.

### iOS

- [`ios/01-appstore-requirements-2026.md`](ios/01-appstore-requirements-2026.md) —
  store requirements, privacy manifest, age rating, ATT, TestFlight, rules
  on random reward drops.
- [`ios/02-unity-ios-build-pipeline.md`](ios/02-unity-ios-build-pipeline.md) —
  Unity → Xcode, signing, PostProcessBuild, reducing build size.
- [`ios/03-vision-animal-recognition.md`](ios/03-vision-animal-recognition.md) —
  on-device cat recognition via Apple Vision.
- [`ios/04-unity-swift-native-plugin.md`](ios/04-unity-swift-native-plugin.md) —
  C# ↔ Swift bridging, camera access, cropping a photo.
- [`ios/05-notifications-permissions.md`](ios/05-notifications-permissions.md) —
  local notifications, when to request permission, ATT.
- [`ios/06-on-device-coat-traits.md`](ios/06-on-device-coat-traits.md) —
  **whether coat pattern can be determined without the cloud and without a
  server**. Answer: pattern — no. Apple's classifier taxonomy has 1303
  categories and only five cat-related words, not a single breed or a single
  coat color.

### Cloud model

- [`vision-model/01-traits-strict-json.md`](vision-model/01-traits-strict-json.md) —
  coat traits as strict JSON: schema, structured outputs, **the cost
  calculation for parsing a photo** (about 0.10 cents), image constraints.

### Python and backend

- [`python/01-fastapi-service.md`](python/01-fastapi-service.md) — the
  intermediary node.
- [`python/02-pytest-testing.md`](python/02-pytest-testing.md) — tests for
  the service and a run against a reference set of photos.
- [`python/03-ratelimit-and-signing.md`](python/03-ratelimit-and-signing.md) —
  rate limiting and request signing.
- [`python/04-free-hosting-options.md`](python/04-free-hosting-options.md) —
  **where to host the intermediary node for free**: free tiers, whether a
  card is needed, whether the app sleeps.
- [`python/05-cloudflare-worker-proxy.md`](python/05-cloudflare-worker-proxy.md) —
  **how to write and deploy the node itself** on Cloudflare Workers:
  wrangler commands, secrets, a complete sample `/traits` handler, limits,
  rate limiting.

### Levels

- [`solver/01-tile-match-solver.md`](solver/01-tile-match-solver.md) —
  problem statement, computational complexity, search algorithms, level
  generation by working backward.
- [`solver/02-level-format-and-property-tests.md`](solver/02-level-format-and-property-tests.md) —
  level description in JSON, property-based tests, keeping rules consistent
  between C# and Python.

### Art

- [`artgen/01-art-pipeline.md`](artgen/01-art-pipeline.md) — batch
  generation, keeping a consistent look, background removal, assembling a
  cat from layers.

### Analytics

- [`analytics/01-own-event-collection.md`](analytics/01-own-event-collection.md) —
  building your own event collection, on-device queue, device fingerprint,
  computing the four metrics.
- [`analytics/02-benchmarks-and-attribution.md`](analytics/02-benchmarks-and-attribution.md) —
  industry benchmarks for retention and install cost, attribution on iOS.
- [`analytics/03-free-analytics-options.md`](analytics/03-free-analytics-options.md) —
  **ready-made free measurement tools**: what's free without a card and
  which of the four metrics are covered without a single line of your own
  code.
- [`analytics/04-gameanalytics-unity-usage.md`](analytics/04-gameanalytics-unity-usage.md) —
  **how to use GameAnalytics**: installation, configuration, an "our event →
  call" table for all nine, naming restrictions, privacy manifest, how to
  get by without the ATT dialog.

### Working with agents

- [`agents/01-unity-mcp.md`](agents/01-unity-mcp.md) — MCP servers for Unity.
- [`agents/02-unity-repo-hygiene.md`](agents/02-unity-repo-hygiene.md) — Unity
  file formats and agents, .meta and GUID, git, CI checks.

### Fallback path

- [`godot/01-godot-4.6-fallback.md`](godot/01-godot-4.6-fallback.md) — Godot
  as a fallback path if self-publishing.

---

## Caution

The review of third-party MCP servers mentions a repository that, in its
operation, uses the `--dangerously-skip-permissions` key. This disables
confirmation of the agent's actions. It should not be adopted for work; it's
mentioned so you'll recognize it if you encounter it, not so you'll use it.
