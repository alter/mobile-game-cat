# Saved Kitten — technical part

Date: August 24, 2026
Addendum to `cat-shelter-mvp.md`. Tasks and acceptance — in
`cat-shelter-tasks.md`. The rationale behind each decision — in the
`knowledge/` directory, start with `knowledge/README.md`.

---

## Edit from August 24, 2026

A check of the whole stack against primary sources. Five decisions
changed, two confirmed, one error fixed.

| Was | Now | Why |
|---|---|---|
| A Python/FastAPI intermediary node | a single Cloudflare Workers handler | free, no card, no wake-up delay; network wait doesn't count toward the CPU-time limit |
| Our own event collection | GameAnalytics + App Store Connect analytics | free, no player cap; nothing of our own to write |
| Request signing with a shared secret | a spend cap at the vendor | the secret is extractable from the build, the cap isn't |
| Save data via `System.Text.Json` | `JsonUtility`; for levels — Newtonsoft from the Unity package | **error:** Unity doesn't ship `System.Text.Json`, and under IL2CPP it hits `Reflection.Emit` |
| Xcode 16+ | Xcode 26+ | the store requirement took effect April 28, 2026 |
| Godot 4.6.3, "no patches for 4.7 yet" | argument stale | 4.7 already has 4.7.1 and 4.7.2 |

Confirmed by checking: Unity 6.3 LTS exists with support through December
2027; a first-party Unity MCP server exists; the cost of parsing a photo
falls within the stated 0.1-0.3 cent; the coat pattern still **cannot** be
determined on device without the cloud.

---

## 0. The main principle for choosing versions

The question was framed as "trendy with MCP, or established." The answer:
**this isn't a choice, it's a layer split.**

An agent writes based on what it saw in training. On the very latest
engine version it invents calls that don't exist, you spend hours catching
that instead of minutes, and the entire speed gain gets eaten up. So:

- **The layer that ships in the game** — established, with two or three
  patch releases behind it. Novelty here gives nothing and costs a lot.
- **The layer the agent works with** (MCP servers, build tools, art
  generation) — the freshest available. It's outside the game, breaks
  without consequence, changes within an hour.

Rule: **novelty where rollback is free. Stability where rollback costs
weeks.**

## 1. Engine

### Choice

**Unity 6.3 LTS (6000.3.x)** — if we go to a publisher. Confirmed: released
December 2025, support through December 4, 2027, extended through
December 4, 2028. The latest patch as of the check date is
**6000.3.22f1** from August 13, 2026 — take that one.

**Warning: Unity does not offer it by default.** Anyone downloading the
editor today is offered 6.5 — `6000.5.9f1` from August 19, 2026, a normal
stable `f` release. This isn't a beta: the previous draft lied here, 6.5
came out June 15, 2026.

Take 6.3 LTS anyway: **6.5 is an Update release**, and such releases live
"until the next release (update or LTS) is published," meaning support
ends the moment the next version ships — possibly in a couple of months.
6.3 LTS has support through December 4, 2027, extended to 2028. Same goes
for 6000.4.

Installed through the [release archive](https://unity.com/releases/editor/archive):
find `6000.3.22f1`, click "Unity Hub," the `unityhub://` link installs the
right one. The same path exists inside the Hub: Installs → Install Editor
→ the archive link. It's not "6.3 is unavailable," it's "the download page
shows the recommended one."

What happens if you work on 6.5 anyway, and why that isn't a
catastrophe — in `knowledge/00-versions.md`.

Also: support for Unity 6.0 LTS ends **October 16, 2026**. If a project is
still on it somewhere — this is the last month.

**Godot 4.6.3 stable** — if we self-publish. **The argument this choice was
based on is stale.** The previous draft said "not 4.7, 4.6.3 already has
three patch releases, 4.7 doesn't have any yet." As of August 24, 4.7 has
two patches: 4.7.1 from July 14 and 4.7.2 from August 16. Godot's rule
states that a branch is supported "until the next stable branch is
released and has received its first patch update" — meaning 4.6's
guaranteed period expired in July.

The conclusion is not "switch urgently." Godot remains a fallback path and
there's no point touching it until publishers pass. The conclusion is
elsewhere: **if it does come down to Godot, the version needs to be chosen
again from scratch, not pulled from here.** The justification rotted in
two months, and that's a good illustration of why such notes are dated.

### Comparison on the merits

| | Unity 6.3 LTS | Godot 4.6.3 |
|---|---|---|
| Accepted by publishers for prototypes | the only one accepted | not accepted |
| Scene format | machine YAML with identifiers | plain text (.tscn/.tres) |
| Agent editing scenes | breaks identifiers, needs MCP | edits directly, MCP optional |
| MCP | first-party, but requires Unity Cloud and a subscription; or third-party | third-party, open |
| Model's knowledge of the engine | very high (C# is heavily represented in training data) | medium, GDScript appears less often |
| Ready-made layers for ads and measurement | everything on the market | AdMob (Poing Studios) and a bit more |
| iOS purchase handling | out of the box | StoreKit 2 under Foundation stewardship since 2026 |
| Empty build weight | 25-40 MB | 12-20 MB |
| Revenue cut | none | none, MIT |

### What this means in practice

Godot is faster to work with via an agent, but loses where a prototype's
fate is decided — at the publisher. Their measurement layer exists only
for Unity, and without it there's nothing to measure a prototype with,
meaning nothing to accept it on.

The model's knowledge of the engine is an underrated factor. C# and Unity
are represented in training data many times more than GDScript. An agent
on Unity makes fewer mistakes simply because it has seen more code.

**Decision: Unity 6.3 LTS.** Godot remains a fallback path if every
publisher passes after the first prototype and we move to self-publishing.

## 2. Tool set

### Inside the game (established, change only when necessary)

```
Unity           6.3 LTS, 6000.3.22f1
Language        C#, .NET Standard 2.1
Rendering       built-in, 2D Renderer (URP 2D)
UI              UI Toolkit — layout in UXML/USS, this is plain text,
                the agent edits it directly, unlike scenes
State           our own finite state machine, no third-party libraries
Save data       one JSON file via JsonUtility, the whole run's state,
                written on every move (not on app backgrounding)
Levels          JSON read via com.unity.nuget.newtonsoft-json
Tests           Unity Test Framework (NUnit) for the rules engine
Ads             none in the MVP; LevelPlay for self-publishing
Measurement     GameAnalytics (events) + App Store Connect analytics (retention)
```

**On JSON — a correction of the previous draft.** It had
`System.Text.Json`, and that's an error: Unity doesn't ship it, and under
IL2CPP it hits `Reflection.Emit`, which doesn't exist on iOS. Correct:
the save file uses the built-in `JsonUtility`, which covers a flat record
like "levels passed, current level, cat traits, flags" and requires no
package at all. Level descriptions are more complex — nested arrays of
items and overlaps — and for those Newtonsoft is taken from Unity's
official `com.unity.nuget.newtonsoft-json` package. This is exactly the
case where off-the-shelf beats homemade: there's no point writing your own
JSON parser.

**On measurement — a change of decision.** The previous draft said "our
own event collection, HTTP to our own node," with no justification.
Off-the-shelf covers the task fully and free: GameAnalytics sets no cap on
player count and requires no card, and App Store Connect counts day-one
retention itself, with zero code. Separately: the ATT dialog can be
skipped — the package doesn't trigger it on its own. What we give up in
exchange, and how to exit, is in `knowledge/00-vendor-lock-in.md`.

What we deliberately don't take: DOTween, Zenject, Odin, ready-made kits.
Every third-party library is something the agent knows worse than bare
Unity, and a source of version drift. On a three-week prototype they don't
pay off.

### Around the game (the freshest, breaks without consequence)

```
Claude Code             the primary executor
Unity MCP               see the caveat below
Git MCP + Filesystem    standard
Art generation          batched, via API, one prompt for the whole set
Level solver            our own, Python 3, outside the Unity project
Intermediary node       Cloudflare Workers, TypeScript, outside the Unity project
Build                   headless, from the command line, via CI
```

**Decision on Unity MCP: we take `CoplayDev/unity-mcp`, but connect it
after task 1.4.**

An official one exists and it's first-party — the `com.unity.ai.assistant`
package, a pre-release, and Claude Code is named directly among the
supported ones. **It's out:** it requires a project on Unity Cloud and an
active subscription to Unity AI tools, and our rule is not to pay for
services.

Live third-party ones, checked via the GitHub API on August 25, 2026:

| Server | Stars | License | Last updated |
|---|---|---|---|
| `CoplayDev/unity-mcp` | 13,643 | MIT | 07.08.2026 |
| `IvanMurzak/Unity-MCP` | 3,979 | Apache-2.0 | 24.08.2026 |
| `CoderGamester/mcp-unity` | 1,874 | MIT | 10.08.2026 |

We take the first one: MIT, most widely used, alive. The second has a more
recent update and more tools (70+ versus 47), and it can work inside a
built game — which may come in handy later for on-device testing, but is
unneeded right now.

**Why it's needed — not for speed.** Batch mode was measured on this
machine: creating an empty project takes 6 seconds, reopening it 2-3. This
is acceptable, and the "batchmode is slow" argument didn't hold up.
Caveat: the measurement is on an **empty** project — with levels, sprites
and packages it will be slower, by how much we'll find out once the
project exists.

The real benefit of MCP is elsewhere: **reading the Unity console** —
compile and runtime errors arrive already parsed, instead of being fished
out of `Editor.log` — plus working with scenes, running play mode and
inspecting what's happening.

**Limitation.** MCP lives inside an **open editor with a window**. That
means the agent stops being self-sufficient: someone has to keep Unity
running. That's fine on a work machine, but the build and CI stay on
batch mode. The two modes coexist; neither replaces the other.

**Why not now.** There's no Unity project yet — nothing to connect to.
The moment to connect is right after 1.4, once the project exists and
scene work begins. A full breakdown of the options is in
`knowledge/agents/01-unity-mcp.md`.

An important separation: **the level solver, art generation and the
intermediary node live outside the Unity project.** The solver and art
generation are in Python, outputting JSON with level descriptions and PNGs
with items. The intermediary node is a separate TypeScript handler. This
way the agent works with them as ordinary files, and they survive an
engine change.

## 3. Recognizing the cat

### Why YOLO is the wrong answer

YOLO solves "find and box it." Our task is "describe the coloring." These
are different things. YOLO26 as CoreML is extra weight of 10-40 MB for
something already built into iOS for free, and it still won't say whether
the cat is tabby or spotted, without separate fine-tuning on your own
photo set, which you don't have.

Fine-tuning a coloring classifier is weeks of collecting and labeling data
for a task where an error costs nothing (the cat comes out a slightly
wrong shade — the player won't notice).

### Two-stage parsing

**Stage 1 — checking "is there a cat in the photo." On-device, free.**

Apple Vision — a built-in animal recognizer, tells a cat from a dog,
returns a box and a confidence score. Works offline, on the neural
co-processor, the photo never leaves the device.

Two APIs, both current: `VNRecognizeAnimalsRequest` — since iOS 13,
available from Objective-C too, moved by Apple into the "Legacy API"
section, but not marked deprecated; `RecognizeAnimalsRequest` — the newer
Swift-only variant, since iOS 18. Both distinguish exactly two: `.cat` and
`.dog`, Apple gives nothing more. Apple doesn't publish the confidence
threshold — tune it on a reference set.

```
if no animal found              → "I don't see a cat, try another photo"
if a dog is found                → "That's a dog! We need a cat"
if cat, confidence < 0.6         → "Photo's unclear, try getting closer"
if cat                           → crop to the box, proceed
```

This doubles as the indecency filter: only what Vision recognized as a cat
is accepted as input.

**Stage 2 — coloring traits. Cloud, fractions of a cent.**

The photo cropped to the box goes to a vision model with a strict prompt
to answer only in strict JSON:

```json
{
  "base_color": "ginger|grey|black|white|cream|brown",
  "pattern": "solid|tabby|bicolor|calico|tuxedo|pointed",
  "fur_length": "short|long",
  "eye_color": "green|amber|blue",
  "white_markings": ["chest","paws","face"]
}
```

The values are enumerable, not free text. **They're constrained not by the
prompt, but by the schema:** `output_config.format` with `json_schema`,
where every field has an `enum`, and the object has
`additionalProperties: false`. Then a value outside the list isn't
"unlikely," it's impossible. A prompt saying "answer only in JSON" gives an
answer that usually parses, and acceptance demands one hundred percent.
One schema limitation that has to be worked around in our own code:
`maxItems` isn't supported, so the length of `white_markings` is trimmed
in the handler.

What comes back is about 100 bytes. The photo isn't stored by us — only the
trait set is kept.

**Correction, 2026-08-27.** This used to also claim the vendor doesn't store
it either, quoting "Image uploads are ephemeral and not stored beyond the
duration of the API request." That exact sentence is not on any current
Anthropic page (checked live, 2026-08-27) and the claim was wrong. What
Anthropic's privacy center actually says: standard API accounts have inputs
and outputs auto-deleted within **30 days** of receipt or generation
(privacy.claude.com/en/articles/7996866-how-long-do-you-store-my-organization-s-data,
"last updated" 2026-07-01, retrieved 2026-08-27); retained data is not used
to train models **by default**, without the customer's express permission
(privacy.claude.com/en/articles/7996868-is-my-data-used-for-model-training,
retrieved 2026-08-27); true zero-retention exists only as a separate,
opt-in "Zero Data Retention" arrangement, "subject to Anthropic's approval"
through Anthropic's sales team
(privacy.claude.com/en/articles/8956058-i-have-a-zero-data-retention-agreement-with-anthropic-what-products-does-it-apply-to,
updated 2026-06-09, retrieved 2026-08-27) — which this project, on the
standard self-serve account (DECISIONS.md D11), does not have. So: **not
stored by us; retained by the vendor for up to 30 days by default; not used
for training by default.** Full sourcing and the App Store/Play Store
consequences: `tasks/00-validate-demand/01-market-scan/legal-risk.md` §3.

**Cost — calculated, not estimated.** An image costs
`⌈width / 28⌉ × ⌈height / 28⌉` visual tokens; for 512×512 that's 361
tokens. With the prompt and response it comes to about 611 input tokens
and 80 output. At prices as of August 24, 2026: **0.10 cent on Claude
Haiku 4.5**, 0.20 on Sonnet 5, 0.51 on Opus 5. At 500 installs and 40%
uploading a photo that's 200 parses, i.e. 20 cents for the whole MVP. The
previous estimate of "0.1-0.3 cent" is confirmed.

Haiku 4.5 supports structured outputs and costs a quarter of Sonnet. But
the choice isn't decided by price, it's decided by the quality of coloring
parsing, and that can't be learned from documentation — it has to be
compared by eye on a reference set.

**A fallback path without a network — and its honest limit.** The base
color can be determined on our own: k-means over the dominant colors of
the cropped photo, matched against a palette of six colorings. White
markings on paws and face — via body pose points from
`VNDetectAnimalBodyPoseRequest`. The pattern — **no, and this isn't about
trying harder.** In Apple's classifier taxonomy there are 1303 categories,
of which five are cat-related words (`cat`, `adult_cat`, `kitten`,
`bobcat`, `feline`) and not one coloring — while dog breeds number over
thirty there. There's no ready open model for this task either. So offline
we default to `solid` and get a believable cat, but not this player's cat.
Breakdown: `knowledge/ios/06-on-device-coat-traits.md`.

### Assembling the cat from parts

Not a generated image, but layers:

```
Cat sprite = silhouette(state, fur_length)
           + fill(base_color)
           + pattern mask(pattern)
           + white patches(white_markings)
           + eyes(eye_color)
```

There are three silhouettes (by state) × two (fur length) = 6 sets.
Everything else is color and mask overlay in the shader, generated on the
fly. In total 6 sets are drawn instead of 6 × 6 × 6 × 3 combinations.

### Android later

Vision is Apple-only. For Android in the second wave: ML Kit Object
Detection for stage 1, stage 2 unchanged. Or a single YOLO26n in CoreML
and TFLite — but only once install counts run into the thousands and the
extra 15 MB pays for itself.

## 4. The intermediary node

A direct call to the cloud from the game is impossible: the key would ride
along on the device and leak within the first week. A go-between is
needed.

```
Cloudflare Workers, TypeScript, one POST /traits handler
input:    cropped photo, base64, up to 512×512
output:   JSON with traits
key:      wrangler secret put — never reaches the device
guard:    a spend cap at the vendor; rate limiting in the handler
storage:  none; the photo lives in memory for the duration of the call
```

**Why not our own machine with FastAPI.** The previous draft described a
real service — Python, gunicorn, systemd, nginx — for a few hundred calls
total. A single Cloudflare Workers handler does the same for free: 100,000
requests a day on the free tier against our hundreds for the whole MVP, no
credit card needed, the app never sleeps.

The last point matters more than it looks. The player waits for a response
right there on the photo screen, and services like Render, which sleep
after fifteen minutes idle and wake up in about a minute, would kill that
screen.

The key question that settles the whole matter was checked word for word
on Cloudflare's limits page: "Waiting on network requests (such as
`fetch()` calls, KV reads, or database queries) does not count toward CPU
time." So a second spent waiting for the model's answer doesn't eat into
the 10 ms CPU-time limit. Only the real cost is spent — base64 decoding
and JSON parsing. This will need to be measured via `wrangler tail` after
the first deploy.

The only price for this decision: the handler is written in TypeScript,
not Python. That's fifty lines and the single piece outside Python in the
whole stack. If staying on Python matters, PythonAnywhere has
`api.anthropic.com` already on its allowed-address list, but caps CPU time
at 100 seconds a day.

**Request signing with a shared secret was dropped.** A secret baked into
the app is extractable from the build — meaning it protected nothing. What
protects against ruin is a hard spend cap in the vendor's console, and it
needs to be set before the first call. Rate limiting in the handler stays,
but as a courtesy against a stuck client, not as protection. A ready
handler template, `wrangler` commands and secret handling — in
`knowledge/python/05-cloudflare-worker-proxy.md`.

### We do have our own server — what goes on it and what doesn't

It turns out a server already exists, meaning the marginal cost of hosting
on it is zero. This changes less than it seems, and here's the split.

**Rule: on the critical path — someone else's managed service, everything
else — our own.**

| What | Where | Why |
|---|---|---|
| Photo parsing `/traits` | Cloudflare Workers | downtime is unacceptable: the photo screen feeds the metric that decides the project's fate |
| Raw event archive | our own Postgres | downtime is harmless, and the data stays ours |
| Invite codes and counters | our own Postgres | second wave, downtime is harmless |
| Reports on the four metrics | GameAnalytics | already done, nothing to write |

The argument for Cloudflare on the intermediary isn't "cheap," it's
"nothing to fall over." If our own server is down for a day during the
paid test, the share of photo uploads comes out understated, and there
won't be money left to redo the test.

**Postgres, no Redis.** Redis would be needed for rate limiting, but at
hundreds of calls total, Postgres handles it with one table. A second
service to stand up and maintain, for load that doesn't exist, is pure
loss.

**Condition for any call from the game: a domain and a valid TLS
certificate.** iOS won't allow a plain-HTTP request — that's App Transport
Security, and it's not worth working around for a prototype.

**One real win from having our own server.** Send events to both places:
GameAnalytics for reports, our own handler to drop raw events into
Postgres. This removes a dependency found during the review: raw
per-player data at GameAnalytics costs from 499 dollars a month and up and
is retained for 12 months. Our own copy makes that irrelevant and costs
one table.

## 5. Building for iOS

```
macOS + Xcode 26+          mandatory, see below
Unity iOS Build Support    module in Unity Hub
target                     iOS 15+ (covers all live hardware)
signing                    Apple Developer Program, $99/year
test distribution          TestFlight
```

**The requirement was refined, and it's stricter than the previous
note.** Verbatim from Apple's site: "Starting April 28, 2026, apps and
games uploaded to App Store Connect need to meet the following minimum
requirements: iOS and iPadOS apps must be built with the iOS 26 &
iPadOS 26 SDK or later." The date has already passed. Xcode 16 no longer
produces an uploadable build — Xcode 26 or newer is required.

Don't confuse two different numbers: the requirement concerns the
**build tool**, not the minimum iOS version the game runs on. The iOS 15+
target stays in force.

### Two Xcode projects, not one

Unity generates a project against **one** SDK, fixed at generation time by
`PlayerSettings.iOS.sdkVersion`. The device project carries device-only
static libraries (`Libraries/libiPhone-lib.a`, `baselib.a`, marked
`platform 2`), and `SDKROOT = iphoneos`. It cannot be made to run in the
simulator by picking a different destination in Xcode — the simulator
needs its own generated project. Hence two entry points in
`game/Assets/Editor/BuildScript.cs` and two output folders:

```
BuildScript.BuildIOSXcodeProject     -> game/build/ios/      device SDK
BuildScript.BuildIOSSimulatorProject -> game/build/ios-sim/  simulator SDK
```

`BuildIOSSimulatorProject` switches the SDK to `SimulatorSDK` and the
simulator architecture to `ARM64` (the project default is `X86_64`, which
on Apple Silicon would run under translation), builds, and then restores
both settings — `ProjectSettings.asset` is left unchanged.

### The active build target is not the same as BuildPlayer's argument

Added 27 August 2026, after this cost a whole feature silently.

`BuildPipeline.BuildPlayer(scenes, path, BuildTarget.Android, ...)` builds for
Android. It does **not** make Android the *active* build target, and editor
code that hooks the build is compiled against the active one. Unity packages
guard their build callbacks with the platform define — `com.unity.mobile.notifications`
opens its `AndroidNotificationPostProcessor` with `#if UNITY_ANDROID` — so when
the project sits on iOS, that class does not exist, its
`OnPostGenerateGradleAndroidProject` never runs, and nothing it is responsible
for reaches the manifest.

What that produced here: an APK carrying the notification Java classes in
`classes.dex` and neither `android.permission.POST_NOTIFICATIONS` nor the
`UnityNotificationManager` receiver in its manifest. The code shipped and could
never deliver anything. The build reported `result=Succeeded`, exit code 0,
zero errors, and there was nothing in the log to notice.

So every build entry point in `game/Assets/Editor/BuildScript.cs` now calls
`UseTarget` first, which switches the active target and throws if the switch
fails. All three of them, not just Android: `build/headless-build.sh` builds
Android and then iOS in one run, so without it the iOS build would inherit an
Android editor and lose its own post-processors the same way.

**The general rule, worth more than the incident:** any package that injects
permissions, receivers, entitlements or gradle changes through an editor
callback is silently skipped whenever the active target is not the one being
built. A build that succeeds is not evidence that its manifest is right.

To check a built APK rather than trust the log:

```bash
AAPT="/Applications/Unity/Hub/Editor/6000.3.22f1/PlaybackEngines/AndroidPlayer/SDK/build-tools/36.0.0/aapt2"
"$AAPT" dump permissions game/build/android/CatShelter.apk
"$AAPT" dump xmltree game/build/android/CatShelter.apk --file AndroidManifest.xml | grep -A2 "E: receiver"
```

### Running in the simulator — verified 26 August 2026

The full path from source to a running app, no Xcode window involved:

```bash
UNITY="/Applications/Unity/Hub/Editor/6000.3.22f1/Unity.app/Contents/MacOS/Unity"
cd game

# 1. Generate the Xcode project against the simulator SDK
"$UNITY" -batchmode -quit -nographics -projectPath "$(pwd)" \
         -executeMethod BuildScript.BuildIOSSimulatorProject \
         -logFile "$(pwd)/build/ios-sim-build.log"

# 2. Compile it
cd build/ios-sim/CatShelter
xcodebuild -project Unity-iPhone.xcodeproj -scheme Unity-iPhone \
           -configuration Debug -sdk iphonesimulator -arch arm64 \
           -derivedDataPath ./DerivedData \
           CODE_SIGNING_ALLOWED=NO CODE_SIGNING_REQUIRED=NO build

# 3. Boot the simulator, install, launch
xcrun simctl boot "iPhone 17"; open -a Simulator
xcrun simctl install booted DerivedData/Build/Products/Debug-iphonesimulator/game.app
xcrun simctl launch booted com.DefaultCompany.game

# 4. Screenshot — this is how a visual claim gets proven
xcrun simctl io booted screenshot /tmp/sim.png
```

Notes that cost time if forgotten:

- No signing and no team ID are needed for the simulator; the two
  `CODE_SIGNING_*` overrides are what make that true.
- The bundle identifier is `com.DefaultCompany.game` until task 14
  changes it — `simctl launch` takes the identifier, not the app name.
- The app target builds as `game.app` (`PRODUCT_NAME_APP = game`), not
  `CatShelter.app`.
- `xcrun simctl list devices available` lists the device names accepted
  by `boot`.
- The first compile is a full IL2CPP pass over the whole project and
  takes minutes; the result is ~260 MB in the Debug simulator
  configuration.

### Running on a real device

Uses `game/build/ios/` and needs what the project does not yet have:
`appleDeveloperTeamID` is empty, `appleEnableAutomaticSigning` is off,
and the bundle identifier is still the Unity default. That belongs to
task 14-testflight, not here.

## 6. Where things live

```
/game            Unity project (Unity 6.3 LTS)
  /Assets
    /Core        rules engine, plain C#, no UnityEngine
    /View        rendering, scenes, UI Toolkit
    /Shell       shell: kitten, room, texts
    /Levels      level descriptions, JSON, generated externally
    /Art         items and cat parts, generated externally
  /Tests         engine tests
/tools           Python 3, outside Unity
  /solver        solver and level generation
  /artgen        batch art generation
/worker          intermediary node, TypeScript, Cloudflare Workers
/knowledge       gathered knowledge about the tool set, with sources
/docs            this document and the MVP description
```

`Core` with not a single `using UnityEngine` — this is a condition, not a
wish. It gives tests that run without launching the engine, lets the
solver work with the same rules code, and makes it possible to port the
engine to Godot in a day if Unity turns out to be a dead end.

## 7. Decision summary

| Question | Decision | Why |
|---|---|---|
| Engine | Unity 6.3 LTS, 6000.3.22f1 | publishers only accept this one |
| Version | long-lived, not the freshest | the agent knows it better; support through December 2027 |
| Godot | fallback path, choose the version again | if every publisher passes |
| UI | UI Toolkit (UXML/USS) | plain text, the agent edits it itself |
| Game libraries | none (no DOTween, Zenject, Odin) | don't pay off on a three-week prototype, the agent knows bare Unity better |
| Utility packages | take ready-made: Newtonsoft, GameAnalytics | nothing of our own to write, both free |
| JSON parsing | `JsonUtility` for saves, Newtonsoft for levels | `System.Text.Json` doesn't work under IL2CPP |
| "Is there a cat in the photo?" | Apple Vision, on-device | free, instant, the photo never leaves |
| Coloring traits | vision model, cloud | the pattern can't be determined on-device, period |
| Answer strictness | `output_config.format` schema, not a prompt | acceptance demands 100% parsing |
| YOLO | not taken | solves the wrong task, needs fine-tuning |
| Cat in the game | layers and masks, not an image | 6 sets instead of hundreds |
| Intermediary node | Cloudflare Workers, TypeScript | free, no card, never sleeps |
| Key protection | a spend cap, not a signature | the secret is extractable from the build, the cap isn't |
| Measurement | GameAnalytics + App Store Connect | free; retention is counted with zero code |
| Raw event archive | our own Postgres, as a second recipient | GameAnalytics doesn't hand over raw events cheaper than $499/mo |
| Links for bloggers | Custom Product Pages | up to 70 addresses, reporting per address, included in the $99 |
| Save data | run state, on every move | otherwise exiting on the subway costs a room |
| Item kinds | hidden until dug up | without this the level is solved at a glance |
| ATT | not asked | the package doesn't trigger it itself; the dialog costs installs |
| MCP and agent tooling | the freshest | outside the game, rollback is free |

**What we give up for free.** Raw per-player events at GameAnalytics
aren't included in the free tier — that's a paid package from 499 dollars
a month, and the data lives for 12 months regardless. For a three-week
test, where the collection setup will be redone for the publisher's kit
anyway, this is an acceptable tradeoff. For a long-lived product — no.
Revisit it as soon as the game survives M8. The exit cost from each
service is broken down in `knowledge/00-vendor-lock-in.md`.
