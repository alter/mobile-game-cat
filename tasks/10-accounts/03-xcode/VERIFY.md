# Independent verification, 2026-08-27

**Verifier:** fresh context, installed nothing, wrote no project files. No
Unity build run by me, no adb, no emulator. Checked live tool output, the
generated Xcode project's own settings, and a real built `.app`'s embedded
`Info.plist` — three independent sources, not just AGENT-BRIEF.md's word.

## Per-item verdict

| # | Item | Result |
|---|---|---|
| VERIFY 1 | `xcodebuild -version` / `xcode-select -p` match OUTCOME | **pass** |
| VERIFY 2 | an empty build installs, `xcodebuild` exits 0 | **pass — stronger evidence than asked for** |
| iOS 26 SDK requirement (D13) | build tool only, not the runtime floor | **pass, confirmed at the binary level** |
| iOS 15+ floor really set | not just claimed | **pass, confirmed at the binary level** |

## What I ran, live

```
$ xcodebuild -version
Xcode 26.3
Build version 17C529
$ xcode-select -p
/Applications/Xcode.app/Contents/Developer
$ xcrun --sdk iphoneos --show-sdk-version   # and iphonesimulator
26.2
```
Matches the task's OUTCOME and `AGENT-BRIEF.md`'s table exactly.

## Checked against a real artefact, not just the tools

`game/build/ios/CatShelter` and `-ios-sim` both regenerated today
(pbxproj mtimes 21:16 / 23:21). Every build config in both greps
`IPHONEOS_DEPLOYMENT_TARGET = 15.0` — the floor is real in the project file,
not just in prose.

Stronger: `game/build/ios-sim/CatShelter/DerivedData/Build/Products/Debug-iphonesimulator/game.app`
exists — a genuinely built, executable arm64 Mach-O binary with compiled
storyboards and an `Assets.car`, not an unbuilt project. Its own `Info.plist`:

```
DTSDKName        = iphonesimulator26.2
DTXcode          = 2630
DTXcodeBuild      = 17C529
MinimumOSVersion  = 15.0
```

Built against SDK 26.2, by Xcode 26.3 (17C529) — matches `xcodebuild -version`
exactly — with the 15.0 floor baked into the compiled app, not just asserted.
This exceeds VERIFY 2's "empty build": it's a real, working one, produced by
another agent's build I did not run, and I verified the result rather than
trusting it.

## What this cannot establish

This is a snapshot of one machine on 2026-08-27, not a guarantee. Two ways it
goes stale:

- **`SDKROOT` in the pbxproj is the bare name `iphoneos`/`iphonesimulator`,
  not a pinned version** — Unity's generator always writes it that way. The
  26.2 binding comes entirely from whichever SDK the building machine has
  installed; downgrade Xcode, or build on a different machine, and the
  App-Store-eligibility claim silently stops holding with no error from the
  project file itself.
- **Apple's SDK-version mandate is not permanent.** It was set for iOS 26,
  effective 28 April 2026; Apple has a history of raising this bar roughly
  once a year alongside the next major iOS/Xcode cycle. This claim is
  current as of today and will need re-checking the next time Apple
  announces a similar deadline for a newer SDK — nothing in this repo
  watches for that on its own.

## How to reproduce

```bash
xcodebuild -version
xcode-select -p
xcrun --sdk iphoneos --show-sdk-version
xcrun --sdk iphonesimulator --show-sdk-version
grep -c "IPHONEOS_DEPLOYMENT_TARGET = 15.0" game/build/ios/CatShelter/Unity-iPhone.xcodeproj/project.pbxproj
plutil -p "game/build/ios-sim/CatShelter/DerivedData/Build/Products/Debug-iphonesimulator/game.app/Info.plist" | grep -i "DTSDKName\|MinimumOSVersion\|DTXcodeBuild"
```

## What was not checked

- Code signing / provisioning — out of this task's SCOPE (no Apple Developer
  account exists yet, per D17).
- The device-target project (`game/build/ios/CatShelter`) was checked for
  settings only; no built `.app` exists there to cross-check against.
- Whether App Store Connect itself currently enforces the SDK requirement
  literally as stated — taken from `knowledge/ios/01-appstore-requirements-2026.md`,
  not re-verified against Apple's live developer documentation this pass.

## Verdict

`verify:passed`. Both VERIFY items hold, corroborated at three independent
levels down to a real compiled binary's own metadata. `status:` stays `done`.
