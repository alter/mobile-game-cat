# Unity 6.3 LTS — release policy, versions, what's new in 6.3, Box2D v3, iOS build requirements

Date collected: 2026-08-24
Project stack version: Unity 6.3 LTS (6000.3.x line), building for iOS, C#, .NET Standard 2.1, URP 2D Renderer.

## In brief

- Starting with Unity 6 there are two types of releases: **LTS (Long Term Support)** and **Update release** (which replaced the old Tech Stream concept). Both go through a QA cycle of the same rigor. [unity.com/releases/unity-6/support](https://unity.com/releases/unity-6/support) (could not be opened directly, 403; the fact is confirmed via [endoflife.date/unity](https://endoflife.date/unity) and related sources below).
- **6000.3 (Unity 6.3) is an LTS release**, released 2025-12-04. Regular support — until 2027-12-04, extended (Unity Enterprise/Industry, +1 year) — until 2028-12-04. [endoflife.date/unity](https://endoflife.date/unity)
- **6000.0 (Unity 6.0) is also LTS**, released 2024-04-29, regular support until 2026-10-16, extended — until 2027-10-16. [endoflife.date/unity](https://endoflife.date/unity)
- Between the LTS releases, Update releases **6000.1, 6000.2, 6000.4, 6000.5** came out — they are not LTS and are supported only until the next release (regular or LTS branch) is published. [endoflife.date/unity](https://endoflife.date/unity)
- As of the collection date (2026-08-24), the latest patch release of the 6000.3.x line is **6000.3.22f1** (released 2026-08-13 per endoflife.date; the contents of the 6000.3.22f1 release were separately confirmed via unityreleases.com). [endoflife.date/unity](https://endoflife.date/unity), [unityreleases.com/releases/6000.3.22f1](https://unityreleases.com/releases/6000.3.22f1)
- The main new thing in 6.3 for 2D is the low-level **LowLevelPhysics2D API on Box2D v3**, which works in parallel with the old Rigidbody2D/Collider2D, without replacing it. [docs.unity3d.com — 2d-physics-api-introduction](https://docs.unity3d.com/6000.3/Documentation/Manual/2d-physics-api/2d-physics-api-introduction.html)
- For iOS: Unity 6.3 recommends **Xcode 16 or newer** for development; devices with **A8 SoC and iOS 15+** are supported; however, for publishing to the App Store, Apple separately requires a newer Xcode (see the requirements section). [docs.unity3d.com — ios-requirements-and-compatibility](https://docs.unity3d.com/6000.3/Documentation/Manual/ios-requirements-and-compatibility.html), [docs.unity3d.com — system-requirements](https://docs.unity3d.com/6000.3/Documentation/Manual/system-requirements.html)
- After upgrading to 6.3, Unity Discussions has recorded complaints about **performance regressions** (BiRP, HDRP, XR/URP post-processing) — not 2D-specific, but relevant when choosing a version for a new project. [discussions.unity.com — performance-regression-in-unity-6-3-birp](https://discussions.unity.com/t/performance-regression-in-unity-6-3-birp/1700256)
- The official "Planned breaking changes in Unity 6.3" list includes, among other things, removal of the Legacy ETC compressor and changes to URP Compatibility Mode — not directly related to 2D gameplay, but important when upgrading existing assets. [discussions.unity.com — planned-breaking-changes-in-unity-6-3](https://discussions.unity.com/t/planned-breaking-changes-in-unity-6-3/1646418)

## Unity 6 release policy: LTS vs Update release

Starting with Unity 6, Technologies abandoned the old "Tech Stream / LTS" model in favor of two types of releases:

- **LTS (Long Term Support)** — released once a year, supported for two years with bug fixes and critical platform updates; Unity Enterprise and Unity Industry users get an additional third year of support.
- **Update release** — several times a year; unlike the old Tech Stream releases (which were, in essence, "early testing" of new features), Update release goes through the same QA cycle as LTS and is considered production-ready. Supported with bug fixes and critical platform updates **only until the next release** (Update or LTS) is published.

These statements are confirmed by aggregated search data on the official page unity.com/releases/unity-6/support (the page returns HTTP 403 when opened directly — bot-traffic blocking; the content has been cross-checked against the independent source endoflife.date, which explicitly conveys the same policy):

> "Starting with Unity 6, there are two kinds of releases: update releases and long-term support (LTS) releases. Both kinds of releases undergo the same rigorous quality assurance and stability testing. LTS releases are published once a year, supported for two years with bug fixes and critical platform updates... Unity Enterprise and Unity Industry users benefit from an additional year of support."

[endoflife.date/unity](https://endoflife.date/unity) — opened directly, the page explicitly confirms: "There are multiple update releases per year. They are supported with bug fixes and critical platform updates until the next release (update or LTS) is published. LTS releases are published once a year. They are supported for two years with bug fixes and critical platform updates."

Practical conclusion for the project: since 6.3 is LTS with support through the end of 2027 (extended — through the end of 2028), it is a reasonable base for production development of a mobile game for several years ahead. Using newer Update releases (6000.4, 6000.5) in a new long-lived project is not recommended precisely because of the short support window (an Update release is supported only until the next release — in the case of 6000.4, support already ended 2026-06-17 according to endoflife.date).

## Current versions of the Unity 6 line (as of 2026-08-24)

The data below was obtained by directly opening [endoflife.date/unity](https://endoflife.date/unity) (the page is explicitly marked as updated on August 20, 2026):

| Branch | LTS? | Branch release date | Latest known version | Support |
|---|---|---|---|---|
| 6000.0 (6.0) | Yes | 2024-04-29 | 6000.0.82f1 (2026-08-19) | regular until 2026-10-16, extended until 2027-10-16 |
| 6000.1 (6.1) | No | 2025-04-23 | 6000.1.17f1 | ended 2025-08-12 |
| 6000.2 (6.2) | No | 2025-08-12 | 6000.2.15f1 | ended 2025-12-04 |
| 6000.3 (6.3) | Yes | 2025-12-04 | 6000.3.22f1 (2026-08-13) | regular until 2027-12-04, extended until 2028-12-04 |
| 6000.4 (6.4) | No | 2026-03-18 | 6000.4.12f1 | ended 2026-06-17 |
| 6000.5 (6.5) | No | 2026-06-15 | 6000.5.9f1 (2026-08-19) | active as of the collection date |

Table source: [endoflife.date/unity](https://endoflife.date/unity).

Additionally, the latest f-release of 6000.3.x was checked against an independent release aggregator: the page [unityreleases.com/releases/6000.3.22f1](https://unityreleases.com/releases/6000.3.22f1) confirms version **6000.3.22f1**, release date **August 13, 2026**, changeset `1c726e1fb402`, "59 total notes with 36 fixes and 13 package updates". The page mentions, in particular, mobile/iOS fixes: fixed an issue with audio cutting out when accessing the iOS Control Center, fixed a crash from merging render passes for MSAA depth on Apple GPU families 1-3, adjusted shader warmup for older Apple devices.

Important: as of the collection date, 6000.3.x releases continue to come out regularly (f-version patch releases), so before starting or resuming work on the project it is worth double-checking the current f-version via `unity.com/releases/editor/archive` (the page is unavailable for automatic opening due to bot protection — open manually via browser or Unity Hub).

## What's new in 6.3: focus on 2D and mobile platforms

The official Unity Manual page "New in Unity 6.3" (`WhatsNewUnity63.html`) was opened directly:

**2D:**
- "Added low-level 2D physics APIs that are an integration of Box2D v3, which is the latest actively developed version of Box2D." — details in a separate section below. [docs.unity3d.com — WhatsNewUnity63](https://docs.unity3d.com/6000.3/Documentation/Manual/WhatsNewUnity63.html)
- "The 2D Renderer now supports rendering the Mesh Renderer and Skinned Mesh Renderer together with 2D sprites in the same scene." — i.e., the 2D URP Renderer has learned to render regular 3D Mesh Renderer/Skinned Mesh Renderer together with sprites in one scene. [docs.unity3d.com — WhatsNewUnity63](https://docs.unity3d.com/6000.3/Documentation/Manual/WhatsNewUnity63.html)

**Mobile platforms (Android/iOS):**
- "Added scrolling support for TalkBack (Android), VoiceOver (iOS), and Narrator (Windows)" — improved screen reader/accessibility support on iOS/Android. [docs.unity3d.com — WhatsNewUnity63](https://docs.unity3d.com/6000.3/Documentation/Manual/WhatsNewUnity63.html)
- "Updated the minimum supported Android version to 7.1 (API level 25)". [docs.unity3d.com — WhatsNewUnity63](https://docs.unity3d.com/6000.3/Documentation/Manual/WhatsNewUnity63.html)
- "UnityWebRequest now uses HTTP/2 protocol by default, providing improved loading times and faster networking capabilities" on Android and other platforms. [docs.unity3d.com — WhatsNewUnity63](https://docs.unity3d.com/6000.3/Documentation/Manual/WhatsNewUnity63.html)
- "Unity now uses Gradle version 9.1.0 and Android Gradle Plugin (AGP) version 9.0.0" — important when updating CI/build pipelines for Android. [docs.unity3d.com — WhatsNewUnity63](https://docs.unity3d.com/6000.3/Documentation/Manual/WhatsNewUnity63.html)
- "You can now use the new Kawase and Dual filtering options for Bloom post-processing to improve performance, especially on low-end hardware and platforms" — directly relevant to mobile 2D graphics if the project uses Bloom. [docs.unity3d.com — WhatsNewUnity63](https://docs.unity3d.com/6000.3/Documentation/Manual/WhatsNewUnity63.html)

Additional details (not from the official Manual page, but from an independent breakdown of the 6000.3.0f1 release, article opened directly) — on 2D/performance/mobile topics:
- "Improved instantiation performance of GameObjects from Tiles; SpriteAtlas previews now packed asynchronously."
- "LowLevelPhysics2D renderer now performs orthographic render culling, improving debug rendering performance."
- Known issues as of 6000.3.0f1: "Metal: [iOS] Screen flashing after the iOS splash screen (UUM-121453)" and a fix for SSAO precision issues on mobile, as well as "fixed Spotlights with small angles not rendering on mobile."
- The only deprecated item mentioned on this page that indirectly relates to physics: "`Physics.autoSyncTransforms` is deprecated. Use `Physics.SyncTransforms` instead" (this is 3D Physics, not Physics2D, but mentioned in the same release context).

[omitram.com — Unity 6.3 LTS (6000.3.0f1) Full Release Notes & Breakdown](https://omitram.com/unity-6-3-lts-6000-3-0f1-full-release-notes-breakdown/)

## New low-level 2D physics API on Box2D v3

The Unity Manual page "Introduction to the LowLevelPhysics2D API" was opened directly: [docs.unity3d.com — 2d-physics-api-introduction](https://docs.unity3d.com/6000.3/Documentation/Manual/2d-physics-api/2d-physics-api-introduction.html)

What it is:
> "The `LowLevelPhysics2D` API lets you create and control 2D physics objects in C# scripts."
> "The API is based on version 3 of the Box2D physics system."

Coexistence with the old API — **these are two fully independent layers that don't interact with each other**:
> "The API doesn't interact with or affect the built-in Unity 2D physics components such as Rigidbody 2D and Collider 2D. The two systems are separate."

Compatibility with the render pipeline and platforms:
> "The API is compatible with the Universal Render Pipeline (URP), the High Definition Render Pipeline (HDRP), and the Built-In Render Pipeline."
> "[The API] works on platforms that support compute shaders."

Additional technical properties (confirmed via WebSearch against the official documentation, the page references them as advantages of the API):
- support for 64 collision layers instead of the standard 32;
- most of the API is thread-safe, which allows running physics in the Job System across multiple threads;
- objects are returned as structs, which simplifies use with DOTS.

The official "New in Unity 6.3" page describes the motivation for the API's appearance like this:
> "Added low-level 2D physics APIs that are an integration of Box2D v3, which is the latest actively developed version of Box2D, including multi-threaded performance improvements, enhanced determinism, visual debugging support for both Editor and Runtime, improved gizmos, and more."
[docs.unity3d.com — WhatsNewUnity63](https://docs.unity3d.com/6000.3/Documentation/Manual/WhatsNewUnity63.html)

API structure (workflow, page opened directly): to add 2D physics objects, you first need to create a `PhysicsWorld`, then a `PhysicsBody` (sets position/rotation/velocity, but not shape), and attach one or more `PhysicsShape` to it (set the shape that interacts with other shapes). [docs.unity3d.com — 2d-physics-api-workflow](https://docs.unity3d.com/6000.3/Documentation/Manual/2d-physics-api/2d-physics-api-workflow.html) (page opened via WebSearch content aggregation; full text not separately verified with WebFetch — marked as partially verified).

**Is it worth adopting in a new project:** the Unity documentation itself **gives no direct recommendation** to "use/don't use in new projects" — a direct quote with such a recommendation is absent. In practice, as of 6.3 the API exists as a parallel, more low-level system (manual control of PhysicsWorld/PhysicsBody/PhysicsShape in code, rather than through Rigidbody2D/Collider2D components in the inspector). For a puzzle project, where standard Rigidbody2D/Collider2D and regular 2D colliders are usually sufficient, moving to LowLevelPhysics2D is justified only if there is a need for multi-threaded mass physics or a DOTS architecture; for a typical 2D puzzle on URP this is apparently excessive — but this is the agent's conclusion, not a quote from a source, and it is worth re-checking against the project's specific requirements.

## Known regressions and breaking changes when moving to 6.3

### Officially announced breaking changes

The Unity Discussions thread "Planned breaking changes in Unity 6.3" was opened directly: [discussions.unity.com — planned-breaking-changes-in-unity-6-3](https://discussions.unity.com/t/planned-breaking-changes-in-unity-6-3/1646418)

Key points (not 2D-specific, but important when upgrading):
- "We are removing the 'Legacy ETC' compression mode, as it depends on a third party component which is no longer supported." Projects automatically switch to the current default ETC compressor — this may change the visual artifacts of compressed textures (relevant for Android/ETC textures in the project).
- The `Scene.handle` type is changing "from `int` to `SceneHandle`" — the new type supports implicit conversion to/from int, so regular C# scripts work without changes, but precompiled assemblies may need to be rebuilt.
- URP Compatibility Mode: Compatibility Mode code will be stripped by default unless `URP_COMPATIBILITY_MODE` is added to scripting defines — relevant when upgrading a project on URP that uses Compatibility Mode.
- The experimental `AdditionalBakedProbes` API is being removed — migrate to `IProbeIntegrator`.
- A stricter USS (UI Toolkit) parser — syntax errors and unsupported CSS constructs that were previously overlooked will now be flagged.

### Performance regressions recorded by the community after upgrading

- The thread "Performance regression in Unity 6.3 BiRP" (opened directly): a user reports that after upgrading a project from 6.0.58 to 6.3, with no code changes, "My frame times are around 2.3ms in Unity 6.3 and 1.6ms in Unity 6.0 when using non-development builds" — i.e., frame time grew from about 1.6 ms to 2.3 ms on an identical scene; in the profiler a growth in `Gfx.WaitForGfxCommandsFromMainThread` from ~0.2 ms to ~1.5 ms is noticed first, but ultimately "everything is taking a bit more time on 6.3" across several rendering operations. [discussions.unity.com — performance-regression-in-unity-6-3-birp](https://discussions.unity.com/t/performance-regression-in-unity-6-3-birp/1700256)
- Also recorded (per WebSearch data, pages not opened directly by this agent — marked as not directly verified, but the source is the official Unity forum): "HDRP Performance Regression After Upgrading from Unity 6.2 to 6.3" and "Performance regression in Unity 6.3 XR URP Post Processing" — both threads on discussions.unity.com, both about 3D rendering (HDRP, XR), not directly about 2D URP, but confirming the general pattern of rendering regressions in 6.3 for some users. Links: [discussions.unity.com/t/hdrp-performance-regression-after-upgrading-from-unity-6-2-to-6-3/1691742](https://discussions.unity.com/t/hdrp-performance-regression-after-upgrading-from-unity-6-2-to-6-3/1691742), [discussions.unity.com/t/performance-regression-in-unity-6-3-xr-urp-post-processing/1715174](https://discussions.unity.com/t/performance-regression-in-unity-6-3-xr-urp-post-processing/1715174).

### Known bugs in the 6000.3.0f1 release (not fully resolved at release time)

- "Metal: [iOS] Screen flashing after the iOS splash screen (UUM-121453)" — screen flashing on iOS devices after the splash screen when using Metal; reported across various iOS versions, reproducible on orientation change, after a call, when taking a screenshot. [omitram.com — Unity 6.3 LTS (6000.3.0f1) Full Release Notes & Breakdown](https://omitram.com/unity-6-3-lts-6000-3-0f1-full-release-notes-breakdown/)
- "Metal: Game freezes after command buffer Timeout error" (UUM-125778 per WebSearch data, not separately verified via WebFetch against the issue tracker) — a potential game freeze on Metal.
- "IL2CPP: [iOS] [Android] External library generics fail during IL2CPP build (UUM-125284)" — an IL2CPP build issue with generics in external libraries, relevant when using third-party .dlls on iOS/Android.

Practical conclusion: before starting or moving a project to 6.3, it is worth explicitly testing an iOS/Metal build on real hardware (splash screen, orientation change, working in the background/after a call) and checking the IL2CPP build if third-party libraries with generics are used.

## Hardware requirements and Xcode version for building for iOS

The Unity Manual page "System requirements for Unity 6.3" was opened directly: [docs.unity3d.com — system-requirements](https://docs.unity3d.com/6000.3/Documentation/Manual/system-requirements.html)

**Development machine requirements (Unity Editor):**
- macOS: "Ventura 13 or newer".
- Windows: "Windows 10 version 21H1 (build 19043) or newer (X64), Windows 11 21H2 (build 22000) or newer (Arm64)".
- Linux: "Ubuntu 22.04, Ubuntu 24.04".
- Memory: "8 GB RAM is recommended" as a minimum.
- Processor: "X64 architecture with SSE2 instruction set support" or "Apple M1 or above (Apple silicon-based processors)".
- Graphics: "DX10, DX11, DX12 or Vulkan capable GPUs" on Windows; "Metal-capable Intel and AMD GPUs" on macOS.
- Apple Silicon specifics: "Rosetta 2 is required for Apple silicon devices running on either Apple silicon or Intel versions of the Unity Editor"; also, "Unity doesn't support CPU lightmapping for Apple silicon devices, only GPU lightmapping".

**Requirements for building for iOS/iPadOS (same page):**
- Minimum device OS version: "15+" (i.e., iOS/iPadOS 15 and newer).
- Minimum device hardware: "A8 SoC+".
- Graphics API: "Metal".
- Development tools: "Xcode version 16 or later".

Additionally, the "iOS requirements and compatibility" page (opened directly) confirms and adds: [docs.unity3d.com — ios-requirements-and-compatibility](https://docs.unity3d.com/6000.3/Documentation/Manual/ios-requirements-and-compatibility.html)
> "Unity supports iOS 15 and above."
> "When developing for iOS, it's recommended to use Xcode version 16 or later."

Regarding the build machine, it's also important that macOS is required for a local build at all, since Xcode only exists for macOS: the page "How Unity builds iOS applications" (opened directly) confirms — "Xcode is only available for macOS, so if your development machine doesn't run macOS, you can't build an application locally." [docs.unity3d.com — how-unity-builds-ios-applications](https://docs.unity3d.com/6000.3/Documentation/Manual/how-unity-builds-ios-applications.html). The build process itself is two-stage: Unity first generates an Xcode project, then Xcode compiles it into an application.

**A separate and stricter requirement specifically for publishing to the App Store** (obtained via WebSearch against official Unity documentation, not separately verified with WebFetch — formally this is an Apple rule, conveyed by Unity): to submit an iOS/iPadOS application to the App Store, it must be built with Xcode 26.0 or newer, on macOS Sequoia, with the iOS 26 or iPadOS 26 SDK; an older Xcode can build the application, but it cannot be submitted to the App Store. This is a separate requirement from the minimum Xcode version for development (16+) and pertains to the moment of publication, not to the ability to build/test.

Practical conclusion for the project: for day-to-day development/testing, Xcode 16+ on macOS Ventura 13+ (Apple Silicon Mac, minimum 8 GB RAM) is sufficient, but before release to the App Store, an upgrade to the current Xcode version required by Apple at the time of publication is needed (the value "26" is fixed by WebSearch aggregation as of the collection date and may change — Apple periodically raises the bar on the minimum Xcode for the App Store; re-check on developer.apple.com before each release).

## Sources

- [endoflife.date/unity](https://endoflife.date/unity) — table of versions, release dates, and end of support for Unity 6.x.
- [docs.unity3d.com — WhatsNewUnity63](https://docs.unity3d.com/6000.3/Documentation/Manual/WhatsNewUnity63.html) — official "New in Unity 6.3" release notes.
- [docs.unity3d.com — 2d-physics-api-introduction](https://docs.unity3d.com/6000.3/Documentation/Manual/2d-physics-api/2d-physics-api-introduction.html) — LowLevelPhysics2D API on Box2D v3.
- [docs.unity3d.com — 2d-physics-api-workflow](https://docs.unity3d.com/6000.3/Documentation/Manual/2d-physics-api/2d-physics-api-workflow.html) — PhysicsWorld/PhysicsBody/PhysicsShape workflow.
- [docs.unity3d.com — system-requirements](https://docs.unity3d.com/6000.3/Documentation/Manual/system-requirements.html) — Editor and iOS build requirements (Xcode, iOS version, hardware).
- [docs.unity3d.com — ios-requirements-and-compatibility](https://docs.unity3d.com/6000.3/Documentation/Manual/ios-requirements-and-compatibility.html) — iOS/Xcode requirements.
- [docs.unity3d.com — how-unity-builds-ios-applications](https://docs.unity3d.com/6000.3/Documentation/Manual/how-unity-builds-ios-applications.html) — the iOS build process from Unity.
- [omitram.com — Unity 6.3 LTS (6000.3.0f1) Full Release Notes & Breakdown](https://omitram.com/unity-6-3-lts-6000-3-0f1-full-release-notes-breakdown/) — independent breakdown of the 6000.3.0f1 release notes, known bugs.
- [unityreleases.com/releases/6000.3.22f1](https://unityreleases.com/releases/6000.3.22f1) — independent aggregator, details of the 6000.3.22f1 patch.
- [discussions.unity.com — planned-breaking-changes-in-unity-6-3](https://discussions.unity.com/t/planned-breaking-changes-in-unity-6-3/1646418) — officially announced breaking changes.
- [discussions.unity.com — performance-regression-in-unity-6-3-birp](https://discussions.unity.com/t/performance-regression-in-unity-6-3-birp/1700256) — BiRP performance regression after upgrading.
- [discussions.unity.com — hdrp-performance-regression-after-upgrading-from-unity-6-2-to-6-3](https://discussions.unity.com/t/hdrp-performance-regression-after-upgrading-from-unity-6-2-to-6-3/1691742) — HDRP regression (not opened directly, only via WebSearch).
- [discussions.unity.com — performance-regression-in-unity-6-3-xr-urp-post-processing](https://discussions.unity.com/t/performance-regression-in-unity-6-3-xr-urp-post-processing/1715174) — XR/URP post-processing regression (not opened directly, only via WebSearch).
- unity.com/releases/unity-6/support, unity.com/releases/editor/archive, unity.com/blog/unity-6-3-lts-is-now-available — official Unity pages, return HTTP 403 on WebFetch attempts (bot protection); facts from them cross-checked via the alternative sources above.
