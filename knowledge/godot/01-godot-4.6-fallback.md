# Godot as a fallback path for self-publishing

Date material collected: 2026-08-24.

## In brief

- Godot 4.6-stable was released on January 26, 2026, and the branch reached 4.6.3-stable (May 20, 2026). But branch 4.7 has already shipped: 4.7-stable — June 18, 2026, current patch as of the collection date — 4.7.2-stable (August 18, 2026). [1][2]
- Under the project's policy, a stable branch is actively supported only until the first patch of its successor; 4.7.1 shipped July 14, 2026 — meaning the 4.6 branch has formally already moved into "partial" support rather than full support. Planning development around 4.6.3 as "the current stable" is inaccurate: the current stable branch as of the collection date is 4.7.x. [3]
- Exporting for iOS requires macOS with Xcode installed; the exact macOS/Xcode versions are not named explicitly in the official documentation. [4]
- The official documentation still calls C# project export to iOS "experimental, with limitations", although a number of 2026 overview articles describe it as a working channel. [4][5]
- No measured size for an empty Godot 4.6 iOS build was found in open sources.
- Live plugins for iOS: AdMob — `godot-sdk-integrations/godot-admob` (112 stars, updated May 27, 2026); StoreKit 2 — `godot-sdk-integrations/godot-storekit2` (19 stars, updated April 27, 2026; the authors state directly that the API is unstable). The old official plugin built on StoreKit 1 is considered deprecated and vulnerable. [6][7][8]
- There are many MCP servers for Godot on GitHub and they are actively maintained; the largest by star count is `Coding-Solo/godot-mcp` (5348 stars), the most recently committed to as of the collection date is `hi-godot/godot-ai` (1890 stars, a commit on the collection date). [9]
- GDScript in 4.6 got bytecode optimizations, but for compute-heavy tasks C# remains faster; the gap narrowed but did not close. [5]
- `.tscn`/`.tres` is a text format made of five sections; manual editing breaks it most often through a mismatch of `id`/`uid` in `ExtResource(...)`/`SubResource(...)` references and in `parent="..."` paths.
- Moving from Unity to Godot for self-publishing is justified mainly by savings on licensing and a lighter runtime; the cost is rewriting C#/Unity-specific code, a less mature IAP/Ads plugin ecosystem for iOS, and probably weaker language-model knowledge of GDScript compared with the C#/Unity API.

## Versions and release dates

The official 4.6 release page is called "It's all about your flow" and lists as the main changes the new Modern editor theme, a unified docking system, Jolt Physics by default for new 3D projects, a new IK framework, and rewritten Screen Space Reflections. [1]

Release dates for the 4.6.x and 4.7.x branches, obtained directly from the GitHub API (`gh api repos/godotengine/godot/releases`, verified 2026-08-24):

```
4.6-stable      2026-01-26T14:05:33Z
4.6.1-stable    2026-02-16T20:26:38Z
4.6.2-stable    2026-04-01T19:12:33Z
4.6.3-stable    2026-05-20T20:49:16Z
4.7-stable      2026-06-18T12:06:17Z
4.7.1-stable    2026-07-14T18:03:10Z
4.7.2-stable    2026-08-18T16:12:28Z
```
[2]

Godot 4.6.3 is an ordinary maintenance release: "41 contributors submitted 86 fixes for this release... no known incompatibilities with the previous Godot 4.6.2 release." [10]

### State of 4.7

Godot 4.7 officially shipped on June 18, 2026 under the codename "Lights, Camera, Action!". Key additions: HDR output support on desktop (iOS and web are not supported), an `AreaLight3D` node, `DrawableTexture2D`, independent transform offsets for Control nodes, an updated Asset Store replacing the old Asset Library, a built-in virtual joystick for touchscreens, and standalone build-and-publish tools for Android. As of the collection date, two maintenance releases (4.7.1, 4.7.2) have already shipped. [11][12]

Practical conclusion: if a project is starting now, it makes more sense to target the current 4.7.x branch rather than "freezing" on 4.6.3 — unless, of course, there is a specific reason to stay on 4.6 (for example, a dependency on a plugin not yet ported to 4.7).

## Version support policy

The official release documentation states: "Stable branches are supported at minimum until the next stable branch is released and has received its first patch update" — meaning the minimum support period for the 4.6 branch ended on July 14, 2026, with the release of 4.7.1. Past that point the branch receives not priority fixes but only "best effort" fixes, "for as long as they have active users who need maintenance updates." [3]

Long-Term Support (LTS) status in Godot is assigned to the previous stable branch at the moment a new *major* version ships (for example, the 3.x branch became LTS when 4.0 shipped) — "the team does their best to provide fixes for issues encountered by users of that branch who cannot port complex projects to the new major version." There is no separate official LTS status for minor branches within the 4.x line (something like "4.6 LTS") — it is an ordinary stable branch with an ordinary support period. [3]

Within a maintenance cycle, the criteria for a fix to land in a stable branch's patch release are strict: "no new features (unless necessary to enable platform support), and no risky bugfixes unless absolutely critical," with security fixes and new platform-policy requirements considered first. [13]

## Exporting for iOS from Godot 4.6/4.7

### Requirements and steps

The official documentation states a hard requirement: "You must export for iOS from a computer running macOS with Xcode installed" — exact macOS/Xcode versions are not named in the guide's text. [4]

Steps per the documentation:
1. Editor → Manage Export Templates — download the export templates.
2. Project → Export — open the export window, add an iOS preset.
3. Fill in the required App Store Team ID and Bundle Identifier parameters — "Leaving them blank will cause the exporter to throw an error."
4. Export the project — Godot creates an Xcode project (`.xcodeproj`), which is then built and signed with standard Xcode tools. [4]

A common problem is `xcode-select` pointing to the wrong place: "Godot is trying to find the Platforms folder containing the iPhone SDK inside the /Library/Developer/CommandLineTools/ folder, but the Platforms folder with the iPhone SDK is actually located under /Applications/Xcode.app/Contents/Developer" — fixed with the `xcode-select` command pointing at the correct path. [4]

### Empty build size

No measured data on the size of an empty Godot 4.6 iOS build (neither in the official documentation nor in the independent sources found) was found.

### C# on iOS

The official iOS export page (the current stable version of the documentation as of the collection date) states directly: "Projects written in C# can be exported to iOS as of Godot 4.2, but support is experimental and some limitations apply." [4] 2026 overview material puts it more mildly — "C# exports work for Android and iOS," naming the web (HTML5) as the only platform officially unreachable for C#, with console exports via W4 Games in beta status. Both statements do not contradict each other: technically the export works, but the official status is experimental with limitations, and relying on it for a production release is worth padding with extra time to work around possible issues. [5]

## In-app purchase and ad plugins on iOS

### AdMob

The current, maintained option is `godot-sdk-integrations/godot-admob`: "A Godot plugin that provides a unified GDScript interface for integrating Google Mobile Ads SDK on Android and iOS," supporting banner, interstitial, rewarded, rewarded-interstitial, app-open, and native formats, mediation with up to 15 additional ad networks, a built-in UMP consent flow (GDPR), and handling of iOS App Tracking Transparency. Verified via the GitHub API on 2026-08-24: 112 stars, last push 2026-05-27, not archived. [6]

An alternative is the `poingstudios/godot-admob-plugin` monorepo ("Complete AdMob... Supports GDScript and C#"); their earlier separate iOS repository `cengiz-pz/godot-ios-admob-plugin` is archived (last push 2025-08-05, `archived: true` per the GitHub API), confirming the migration to the monorepo. [14]

### StoreKit / in-app purchases

The current modern plugin is `godot-sdk-integrations/godot-storekit2`: "iOS plugin for Godot integrating the StoreKit 2 API." Verified via the GitHub API on 2026-08-24: 19 stars, last push 2026-04-27. The developers themselves warn: "this plugin is still in ongoing development so the API isn't stable and there might be bugs." [7]

The old official plugin `inappstore` (part of `godot-sdk-integrations/godot-ios-plugins`) uses the deprecated StoreKit 1: the issue tracker points directly to a vulnerability — "unlike the Android Billing plugin, there is no way to query_purchases() and find out what the user has purchased/subscribed to when the app starts up" — and to the fact that "Storekit 1 is deprecated" (announced at WWDC 24). For a new project it makes more sense to plan for StoreKit 2 from the start via one of the modern plugins. [8]

A Swift-wrapper alternative is `atlasapplications/godot-store-kit` (version 1.5, compatible with Godot 4.5.1, SwiftGodot 0.74.0, iOS 17+); the authors note that part of the API (subscriptions in particular) is not fully implemented. [15]

## The `.tscn`/`.tres` scene format

A `.tscn` file — "text scene" — is a text representation of a scene tree, made of five sections: file descriptor, external resources, internal resources (sub-resources), nodes, connections. [16]

Example of the header (file descriptor), which must come first in the file:
```
[gd_scene format=3 uid="uid://cecaux1sm7mo0"]
```
An external resource and a reference to it:
```
[ext_resource type="Material" uid="uid://c4cp0al3ljsjv" path="material.tres" id="1_7bt6s"]
...
material = ExtResource("1_7bt6s")
```
An internal resource:
```
[sub_resource type="CapsuleShape" id=2]
radius = 0.5
height = 3.0
```
Properties equal to their default value are not written to the file: "properties equal to the default value are not stored in scene/resource files." Godot 4 introduced string UIDs in place of incrementing integer identifiers — these are exactly what let the engine keep a file reference intact when the file is moved within the filesystem. [16]

Why this format is convenient for an agent to edit: it is ordinary human-readable text (unlike the binary `.scn`), line-structured, with readable git diffs. Dangerous spots an agent can break through careless editing:
- `ExtResource("id")` / `SubResource("id")` references — a typo or an `id` mismatch when removing/adding a resource breaks the link without an explicit parse error;
- the `parent="..."` attribute on `[node ...]` — sets the node's place in the tree via a path; a wrong path breaks the hierarchy;
- `NodePath(...)` inside property values — also a path through the scene tree, not checked while editing the text;
- `uid://...` in the header and in `ext_resource` — must match the project's real UID index; a manual uid edit without syncing with `.godot/uid_cache.bin` can leave the Godot editor unable to find the file.

## MCP servers for Godot

Star counts and last-push dates were obtained directly via the GitHub API (`gh api repos/<owner>/<repo>`, verified 2026-08-24):

| Repository | Stars | Last push | Archived |
|---|---|---|---|
| [Coding-Solo/godot-mcp](https://github.com/Coding-Solo/godot-mcp) | 5348 | 2026-04-16 | no |
| [hi-godot/godot-ai](https://github.com/hi-godot/godot-ai) | 1890 | 2026-08-24 | no |
| [IvanMurzak/Godot-MCP](https://github.com/IvanMurzak/Godot-MCP) | 223 | 2026-08-16 | no |
| [n24q02m/better-godot-mcp](https://github.com/n24q02m/better-godot-mcp) | 34 | 2026-08-23 | no |
| [mkdevkit/godot-mcp](https://github.com/mkdevkit/godot-mcp) | 11 | 2026-06-09 | no |
| [hybridindie/godot-mcp](https://github.com/hybridindie/godot-mcp) | 1 | 2026-08-09 | no |

`Coding-Solo/godot-mcp` is the most popular one; it "provides tools for launching the editor, running projects, and capturing debug output," using direct commands for simple operations and an embedded GDScript file for complex ones (creating scenes, adding nodes). [9]

`hi-godot/godot-ai` — "Production-grade MCP server and AI tools for the Godot engine," giving, per its README, "120+ operations through ~43 MCP tools" for scenes, nodes, signals, materials, and animations; requires Godot 4.5+ and `uv` for the server's Python part; installed from source, a ZIP release, or via the Asset Library/Asset Store. [17]

`IvanMurzak/Godot-MCP` is written in C#, an "AI-powered game development assistant for the Godot Editor," with 42 built-in tools in 12 groups, with an optional cloud connection to ai-game.dev, Apache-2.0 licensed. [9]

## GDScript versus C#

Performance: Godot 4.6 got "bytecode and method-call optimisations" for GDScript, with "gains most pronounced for typed GDScript." At the same time, "C# still holds a performance edge... the gap narrowed but did not close, and C# remains measurably faster than GDScript for compute-heavy work." [5]

iOS: GDScript has no platform restrictions ("GDScript has none of these platform restrictions... exports everywhere including the web"); C# is officially experimental on iOS per the official documentation (see the export section above). An important separate limitation of C#: it cannot call GDExtension directly — "you cannot call GDExtensions directly from C#, and if that's an immediate must-have for your project, you should not use C#." [4][5]

Model knowledge of the language: no direct measurements were found in open sources. It can be indirectly assumed that LLM answer quality on GDScript is lower than on C#/Unity API, simply due to the substantially smaller volume of training material and the engine's lower popularity compared with Unity — this is a judgment call, not a confirmed figure.

## Honest summary: when moving from Unity to Godot is justified

The move makes sense if several conditions coincide:
- the game is self-published, without a publisher that already has requirements for a specific engine/SDK;
- the team is willing to accept the risk of a less mature IAP/Ads plugin ecosystem on iOS (see the sections above — the plugins are alive but small by star count, and the developers themselves warn about API instability, unlike the official Unity IAP/Ads packages);
- the project is simple enough on the 2D side to not run into the experimental status of C# on iOS — i.e., the logic is written in GDScript;
- an open license and the absence of engine royalties/subscription matter (Godot has none of these in principle — this does not require a price check, since it is an architectural fact, not a commercial one).

The cost of the move consists of: rewriting game logic from C#/Unity API to GDScript (or accepting the risks of the experimental C# export), rebuilding the shader and animation pipeline for the Godot system, re-integrating purchases and ads through less battle-tested plugins, and probably slower work with AI agents due to the more modest volume of training data on GDScript compared with C#. No numeric estimate of the cost in hours/money was found in open sources — any such figure would be made up.

## Sources

1. [Godot 4.6 Release: It's all about your flow — godotengine.org](https://godotengine.org/releases/4.6/)
2. [godotengine/godot — Releases (GitHub API)](https://github.com/godotengine/godot/releases)
3. [Godot release policy — docs.godotengine.org (stable)](https://docs.godotengine.org/en/stable/about/release_policy.html)
4. [Exporting for iOS — docs.godotengine.org (stable)](https://docs.godotengine.org/en/stable/tutorials/export/exporting_for_ios.html)
5. [GDScript vs C# in Godot 2026: Choosing Your Scripting Language — StraySpark](https://www.strayspark.studio/blog/gdscript-vs-csharp-godot-2026-choosing-scripting-language)
6. [godot-sdk-integrations/godot-admob — GitHub](https://github.com/godot-sdk-integrations/godot-admob)
7. [godot-sdk-integrations/godot-storekit2 — GitHub](https://github.com/godot-sdk-integrations/godot-storekit2)
8. [Storekit 1 is deprecated · Issue #68 — godot-sdk-integrations/godot-ios-plugins](https://github.com/godot-sdk-integrations/godot-ios-plugins/issues/68)
9. [Godot MCP server GitHub search results / repositories](https://github.com/Coding-Solo/godot-mcp)
10. [Maintenance release: Godot 4.6.3 — godotengine.org](https://godotengine.org/article/maintenance-release-godot-4-6-3/)
11. [Godot 4.7 Release — godotengine.org](https://godotengine.org/releases/4.7/)
12. [What's New in Godot 4.7? — Vagon](https://vagon.io/blog/what-s-new-in-godot-4-7)
13. [Maintenance release process — contributing.godotengine.org](https://contributing.godotengine.org/en/latest/other/release_management/maintenance_releases.html)
14. [cengiz-pz/godot-ios-admob-plugin — GitHub (archived)](https://github.com/cengiz-pz/godot-ios-admob-plugin)
15. [atlasapplications/godot-store-kit — GitHub](https://github.com/atlasapplications/godot-store-kit)
16. [TSCN file format — docs.godotengine.org (stable)](https://docs.godotengine.org/en/stable/engine_details/file_formats/tscn.html)
17. [hi-godot/godot-ai — GitHub](https://github.com/hi-godot/godot-ai)
18. [endoflife.date/godot](https://endoflife.date/godot)
