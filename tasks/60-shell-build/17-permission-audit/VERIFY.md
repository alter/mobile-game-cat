# Independent verification, 2026-08-27

**Verifier:** a fresh agent context, invoked specifically to check this
document, immediately after independently verifying (and failing)
`tasks/90-android/10-permission-audit`. I wrote none of `NOTES.md`, none of
`SwiftPluginPostProcess.cs`, none of the Swift plugins under
`game/Assets/Plugins/iOS/`, and none of the Android audit this one is
compared against in spirit. I did **not** run `adb`, the Android emulator, a
Unity build, or `dotnet` — all off-limits per instructions — and I did not
run `xcodebuild` or otherwise compile the generated Xcode project, so I have
not observed the actual linked-symbol table of a compiled binary, only its
source and project files. That gap is listed under "What was not checked."

## Per-item verdict

| # | Claim checked | Verdict | Evidence |
|---|---|---|---|
| 1 | "`plutil -p` … 48 keys, one of them a permission" | **FAIL** | Re-read `game/build/ios/CatShelter/Info.plist` myself: `plutil -p`, `/usr/libexec/PlistBuddy -c Print`, and a raw `cat` all agree on **28 top-level keys** (33 including the 5 nested keys inside `UIApplicationSceneManifest`; 85 raw lines). No count I can construct from this file reaches 48. See reproduction below. |
| 2 | "one of them a permission: `NSCameraUsageDescription`" | **Pass** | `grep -n "UsageDescription"` on the plist returns exactly one hit, at line 31, with the exact quoted string. No `NSPhotoLibraryUsageDescription`, no `UIBackgroundModes` anywhere in the file. |
| 3 | Which build the claim is about, and whether it's stale | **The file is current, not stale — but it isn't what "48" was ever counted from.** | `game/build/` is entirely gitignored (`game/.gitignore:4`), so there is no history for this file; its mtime is 21:15 today, same day as the audit. Cross-checked against `game/ProjectSettings/ProjectSettings.asset`: `cameraUsageDescription` matches the plist string verbatim; `locationUsageDescription`, `microphoneUsageDescription`, `bluetoothUsageDescription` are all empty; `iOSBackgroundModes: 0`. Every Player Setting that could add a key says "add nothing," which matches a 28-key plist, not a 48-key one — there is no configuration in this project that was ever going to produce 20 more keys. I did **not** find a second, larger Info.plist anywhere under `game/build/ios` that the "48" could instead describe (`UnityFramework/Info.plist` has 10 keys, no permissions; the test-target plist is not a shipped artefact). |
| 4 | Two packages (`com.unity.purchasing`, `com.unity.analytics`, `com.unity.modules.unityanalytics`) were removed and stay removed | **Pass** | `game/Packages/manifest.json` — none of the three appear. |
| 5 | Privacy manifest: `PrivacyInfo.xcprivacy` declares exactly system boot time, disk space, user defaults, file timestamp | **Pass** | `plutil -p` on `game/build/ios/CatShelter/UnityFramework/PrivacyInfo.xcprivacy` lists exactly those four `NSPrivacyAccessedAPICategory*` entries, no more. |
| 6 | VERIFY 3: no `RequestTrackingAuthorization` call site across Assets/Packages (D9) | **Pass** | `grep -rn "RequestTrackingAuthorization" game/Assets game/Packages` — empty. Also checked `AppTrackingTransparency`, `ATTrackingManager`, `ASIdentifierManager`, `EnableAdvertisingIdTracking` across `game/Assets` — all empty. The last one matters: D9's *required* mitigation call is also absent, for the same reason it's absent on Android — GameAnalytics isn't wired into either platform yet, so there is nothing yet to mitigate. |
| 7 | **The sharpest question: does iOS have Android's dormant-advertising-id situation?** | **Present in source, but compiled out — genuinely different from Android, in iOS's favour.** | `game/build/ios/CatShelter/Classes/Unity/DeviceSettings.mm` (Unity's own generated trampoline, not project code) contains `QueryASIdentifierManager()` and `QueryAttTrackingAuthorization()`, dynamically loading `AdSupport.framework`/`AppTrackingTransparency.framework` and calling `ASIdentifierManager.advertisingIdentifier` / `ATTrackingManager.trackingAuthorizationStatus` — the Obj-C analogue of Android's `AndroidAdvertisingIdHelper`. But every one of those three blocks is wrapped in `#if UNITY_USES_IAD`, and `Classes/Preprocessor.h:206` reads `#define UNITY_USES_IAD 0` — Unity's own comment above it says these flags are "adjusted... whenever the project is built" by detecting API usage in the C# scripts, and nothing here calls the legacy iAd API. With the flag at 0, this code does not compile into the app at all (unlike Android's `classes.dex`, where the equivalent bytecode is unconditionally present, dormant only for lack of a permission and a linked library). I did not compile the project to confirm the resulting symbol table is actually empty — see "not checked" — but the source-level gate is unambiguous and is Unity's default, not a project-specific choice anyone here made. No `AdSupport.framework`/`AppTrackingTransparency.framework` appear in the linked-frameworks list in `Unity-iPhone.xcodeproj/project.pbxproj` either. |
| 8 | Does the audit account for Unity's own boilerplate plist keys, not just this project's asked-for ones? | **Substance yes, headline no.** | All 28 real keys were read individually: 27 are Unity/Xcode boilerplate (`CFBundle*`, `UI*`, `CADisable*`, the scene manifest, `Unity_LoadingActivityIndicatorStyle`) and 1 is the camera permission NOTES already covers. None of the 27 is a `Ns*UsageDescription`, `UIBackgroundModes` entry, entitlement, or capability — so the document's *substantive* conclusion (nothing beyond the one permission needs a row) holds up against the real file. But NOTES never lists or counts the 27 individually, and its stated total (48) is simply wrong, so that conclusion was not actually demonstrated by an inventory — it happens to be true when checked independently. |

## Overall verdict: **verify:failed**

Item 1 is a specific, sourced, quoted number in the document that does not
reproduce against the artefact it claims to describe — the same failure
shape as `90-android/10-permission-audit`'s "returns nothing" claim, and
exactly what `tasks/README.md`'s verify rule exists to catch. The
substantive conclusions (one permission, no ATT call site, iAd code
compiled out, privacy manifest correct) all hold up independently and are
arguably the more important half of this document — but "48 keys" is
wrong by any count I can construct, and a wrong headline number in an audit
whose entire job is counting keys is not a rounding error to wave through.

**Plain answer to the sharpest question:** iOS does **not** have the same
advertising-id situation Android does. Both engines ship dormant,
reflection/dynamic-load-based code capable of reaching the platform
advertising identifier, present in every default Unity export regardless of
this project's own choices. On Android that code is unconditionally
compiled into `classes.dex` as live bytecode, inert today only because no
permission is declared and no GMS ads-identifier library is linked — a
future dependency could activate it with no source change. On iOS, the
equivalent code sits behind `#if UNITY_USES_IAD`, which Unity's own export
step sets to `0` because nothing in this project's C# uses the legacy iAd
API — so, on the evidence available without compiling, the code does not
end up in the binary at all, not merely unused inside it. iOS is the safer
platform on this specific axis today, and NOTES.md's own audit never went
looking for this at all — the finding is new, not confirmed-with-caveats.

`status:` is left at `done`, unchanged — Flow decouples the implementer's
status from the verifier's `verify:`, and the deliverable (a table
distinguishing the one real permission from everything else) does exist and
is substantively right; what's wrong is a specific number inside it.
`verify:` is set to `failed` in `labels.txt`.

## How to reproduce

All commands run from a clean checkout against the already-generated Xcode
project, no build, no simulator, no variables exported by hand.

```bash
# item 1 — three independent counts of the same file
plutil -p game/build/ios/CatShelter/Info.plist
/usr/libexec/PlistBuddy -c "Print" game/build/ios/CatShelter/Info.plist
grep -c "<key>" game/build/ios/CatShelter/Info.plist   # 33, includes nested
wc -l game/build/ios/CatShelter/Info.plist              # 85 raw lines

# item 2
grep -n "UsageDescription\|UIBackgroundModes" game/build/ios/CatShelter/Info.plist

# item 3 — cross-check against the settings that generate the plist
grep -n -i "camera\|location\|microphone\|bluetooth\|iOSBackgroundModes" game/ProjectSettings/ProjectSettings.asset
find game/build/ios -iname "Info.plist"
cat game/build/ios/CatShelter/UnityFramework/Info.plist

# item 4
grep -n "com.unity.purchasing\|com.unity.analytics\|com.unity.modules.unityanalytics" game/Packages/manifest.json

# item 5
plutil -p game/build/ios/CatShelter/UnityFramework/PrivacyInfo.xcprivacy

# item 6
grep -rn "RequestTrackingAuthorization\|AppTrackingTransparency\|ATTrackingManager\|ASIdentifierManager\|EnableAdvertisingIdTracking" game/Assets game/Packages

# item 7 — the compiled-out advertising-id code
grep -n "ASIdentifierManager\|AppTrackingTransparency\|ATTrackingManager\|advertisingIdentifier\|AdSupport" game/build/ios/CatShelter/Classes/Unity/DeviceSettings.mm
grep -n "UNITY_USES_IAD" game/build/ios/CatShelter/Classes/Preprocessor.h
grep -n "AdSupport.framework\|AppTrackingTransparency.framework" game/build/ios/CatShelter/Unity-iPhone.xcodeproj/project.pbxproj
```

## What was not checked

- **VERIFY item 2 of `task.txt`** ("a launch on the simulator raises no
  permission dialog the player has not asked for") — requires running the
  simulator, off-limits under this verification's constraints (no Unity
  build). Still genuinely unchecked; NOTES.md itself never demonstrates this
  either (its own VERIFY-list self-assessment marks it "Met" on the basis of
  "checked on an erased simulator after `09-notification` fixed the package
  default," referring to a check made *before* today's audit was written up,
  for a different bug — not re-verified against today's plist as part of
  this document).
- **Whether the compiled binary actually omits the `UNITY_USES_IAD`-gated
  symbols.** I read the preprocessor guard and the flag's value; I did not
  run `xcodebuild` or inspect a linked binary's symbol table, which would be
  the only way to observe the compiled result directly rather than infer it
  from source. Given the constraint against building, this is the ceiling of
  what a static check can confirm.
- **The Android side of this same question was already checked** in
  `tasks/90-android/10-permission-audit/VERIFY.md` and is not re-derived
  here beyond citing its conclusion for comparison.
- **`Unity-iPhone Tests-Info.plist`** (the unit-test target's plist) — not a
  shipped artefact, out of this task's SCOPE, not examined beyond confirming
  it exists.
- Code-signing, provisioning profile, and entitlements-file presence were
  checked only for existence (`find … -iname "*.entitlements"` — none found,
  no `CODE_SIGN_ENTITLEMENTS` in the pbxproj) and not examined further, since
  SCOPE's "entitlement" concern is satisfied by there being none at all.

## Re-verification of the correction, 2026-08-27

**Verifier: a fresh context, not the one that wrote the `verify:failed`
verdict above and not the coordinator, who wrote the correction and cannot
rule on it.** Wrote none of `NOTES.md`'s correction, `DeviceSettings.mm`,
`Preprocessor.h`, or `90-android/10-permission-audit/NOTES.md`. No Unity
build, no `xcodebuild`, no adb, no simulator.

| # | Question | Verdict | Evidence |
|---|---|---|---|
| 1 | Is the corrected count (28) right? | **Yes** | `plutil -p ... \| grep -cE '^  "'` → 28; `PlistBuddy -c Print` top-level count → 28 independently. Both agree with each other and with the correction; neither reaches 48 by any grouping. |
| 2 | Did the correction fix the reasoning, or only the number? | **Only the number — the missing inventory is still missing** | The correction states 28 keys, 1 permission, and asserts "27 non-permission keys... were never inventoried, so the conclusion was reached rather than demonstrated" — naming its own gap precisely — but then does not close it: `NOTES.md`'s corrected text still gives only the aggregate split (28/1), with no row or list classifying the 27 individually. The itemized read-every-key inventory exists only in the earlier `verify:failed` document's own item 8, not in the corrected `NOTES.md`. The correction repeats the original's shape (a conclusion asserted, not demonstrated) at a now-accurate number. |
| 3 | Do all four parts of the `DeviceSettings.mm`/`UNITY_USES_IAD` claim hold, checked broadly? | **Yes, including under a wider search than the claim itself used** | `grep -rl "ASIdentifierManager"` / `"ATTrackingManager"` over the **entire** `game/build/ios/CatShelter/` tree, and `grep -rlaI` (binary-aware) to rule out a `.a`/`.framework` silently skipped by text grep — both return only `DeviceSettings.mm`. Its three `#if UNITY_USES_IAD ... #endif` blocks (lines 9-67, 73-89, 125-127) contain every `ASIdentifierManager`/`ATTrackingManager` reference in the file. `Preprocessor.h:206`: `#define UNITY_USES_IAD 0`, sole definition, no pbxproj override. All four parts hold. **Adjacent finding, not a refutation:** `strings` on Unity's own prebuilt `Libraries/libiPhone-lib.a` (not gated by `UNITY_USES_IAD`, a separate mechanism) shows `Application_CUSTOM_RequestAdvertisingIdentifierAsync` / `UnityEngine.Application::RequestAdvertisingIdentifierAsync` unconditionally present — a different Unity API than the one NOTES checked. No project C# calls it (`grep -rn "RequestAdvertisingIdentifierAsync" game/Assets game/Packages` → empty), so it stays unreachable too, but NOTES's specific claim never looked at this path — it is narrowly true, not the whole story of "can this binary reach an identifier at all." |
| 4 | Is the Android comparison fair? | **Fair in substance; overstated in confidence** | The underlying difference is real and correctly characterised: Android's `AndroidAdvertisingIdHelper` is unconditionally compiled Java bytecode in `classes.dex` (confirmed there by `strings` on the actual built APK in `90-android/10-permission-audit/NOTES.md`'s own correction), dormant only pending a linked GMS artifact; iOS's equivalent is excluded by the C preprocessor before compilation, which is a categorical, deterministic exclusion, not a probabilistic one — a fair asymmetry to draw. But `NOTES.md`'s iOS correction states "the advertising-identifier path is **compiled out**" as settled fact, without the hedge the earlier `verify:failed` document carried ("I did not compile the project to confirm... this is the ceiling of what a static check can confirm"). The Android correction verified its claim against a compiled artifact (`classes.dex` from a built APK); the iOS correction verifies its claim from source and a `#define` alone, never compiling. Given how `#if 0` exclusion works, this is very likely still correct — but it is asserted with more certainty than its own evidence, gathered the same way, technically established. |

**Verdict: `verify:passed`.** The number that failed the audit is now right,
confirmed independently by two methods. The new claim about the
advertising-identifier path is correct on every part checked, including
under a search broader than the one the claim itself used. The Android
comparison's direction is sound. Two caveats are recorded, not fail
conditions: the 27-key inventory the original failure implicitly asked for
still doesn't exist as a list, only as a number; and the iOS "compiled out"
line is stated more confidently than an uncompiled, source-only check
strictly proves. `status:` stays `done` — unchanged, correctly.

### How to reproduce

```sh
plutil -p game/build/ios/CatShelter/Info.plist | grep -cE '^  "'
/usr/libexec/PlistBuddy -c "Print" game/build/ios/CatShelter/Info.plist | grep -cE '^    [A-Za-z]'
grep -rl "ASIdentifierManager\|ATTrackingManager" game/build/ios/CatShelter/
grep -rlaI "ASIdentifierManager\|ATTrackingManager" game/build/ios/CatShelter/
grep -n "#if\|#endif" game/build/ios/CatShelter/Classes/Unity/DeviceSettings.mm
grep -n "UNITY_USES_IAD" game/build/ios/CatShelter/Classes/Preprocessor.h
strings game/build/ios/CatShelter/Libraries/libiPhone-lib.a | grep -i advertisingidentifier
grep -rn "RequestAdvertisingIdentifierAsync" game/Assets game/Packages
```

### What was not checked (this pass)

- No compiled iOS binary was inspected (constraint) — item 4's confidence
  caveat follows directly from this.
- Did not re-derive the Android `classes.dex` finding first-hand; took
  `90-android/10-permission-audit/NOTES.md`'s own reproduction as read.
- Did not attempt to classify all 27 non-permission plist keys myself to
  fill the gap noted in item 2 — flagging the gap is this pass's job, not
  closing it.
