# Independent verification, 2026-08-27

**Verifier:** a fresh agent context, invoked specifically to check this
document. I wrote none of `NOTES.md`, none of the Android build pipeline
(`90-android/02-build-pipeline`), none of the picker plugin (which does not
exist), and none of `60-shell-build/17-permission-audit` (the iOS document
this one compares against). I did **not** run `adb`, did not touch the
Android emulator, did not run a Unity build, and did not install the APK on
a device — all off-limits per the instructions I was given, and VERIFY item
3 (install-time dialog behaviour) is explicitly left unchecked below for
that reason. I re-ran `aapt2` and `strings`/`unzip` myself against the
already-built `.apk` rather than trusting the quoted output in `NOTES.md`.

## Per-item verdict

| # | Claim checked | Verdict | Evidence |
|---|---|---|---|
| 1 | `aapt2 dump permissions` output, as quoted | **Pass** | Re-ran verbatim; byte-for-byte identical 4-line output (below). |
| 2 | `aapt2 dump xmltree` output, as quoted | **Pass, with a fidelity caveat** | Content (permissions, features, components, `exported` flags, SDK levels) matches exactly. The quoted version reformats each Unity `meta-data` element onto one line (`name=... value=...`); the raw tool emits `name` and `value` as two separate `A:` attribute lines. No fact is changed or hidden, but it is not a literal paste of the command's output as VERIFY 1's wording ("pasted whole") implies — see reproduction command below to see the difference yourself. |
| 3 | min/target/compile SDK = 25/36/36, matching `02-build-pipeline/NOTES.md`'s `aapt dump badging` reading | **Pass** | `uses-sdk` element confirms 25/36; `02-build-pipeline/NOTES.md:23` reads `targetSdkVersion: 36`, `sdkVersion: 25` — no drift. |
| 4 | Permission/component table: every dump line justified, `exported` flags correct | **Pass** | All 8 distinct items (2 permission lines, 3 `uses-feature`, activity, receiver, provider) map to a table row with correct `exported`/`required` values. The claim "accounts for every line in both dumps" is a small overstatement — `meta-data`, `layout`, `intent-filter`, `supports-screens` and `uses-sdk` entries (Unity boilerplate, no privacy/permission weight) are not individually itemized — but nothing privacy-relevant is missing, and VERIFY 1 as literally worded (the *permissions* dump) is fully matched. |
| 5 | `CAMERA`/`READ_EXTERNAL_STORAGE`/`READ_MEDIA_IMAGES` absent because `04-picker-plugin` is unbuilt | **Pass** | `tasks/90-android/04-picker-plugin/labels.txt` → `status:todo`. `find game/Assets/Plugins -iname "*android*"` → no output; `ls game/Assets/Plugins` shows only `iOS`/`iOS.meta`. Claim is accurate and appropriately conservative. |
| 6 | `AD_ID` / advertising-id absent from `Assets` and the merged manifest (VERIFY 2, literal scope) | **Pass** | `grep -rn "AD_ID"`, `grep -rin "advertisingId\|AdvertisingId"`, `grep -rn "EnableAdvertisingIdTracking"` over `game/Assets` — all empty. `strings` over the extracted `AndroidManifest.xml` for `AD_ID`/`advertising` — empty. |
| 7 | **"`strings` over the built APK for AD_ID/advertising returns nothing"** — the NOTES' own broader claim, beyond VERIFY 2's scope | **FAIL** | False as stated. `strings` on `classes.dex` (unzipped from the same `.apk`) matches `advertising` four times: `Lcom/unity3d/player/AndroidAdvertisingIdHelper;`, the string literal `com.google.android.gms.ads.identifier.AdvertisingIdClient`, `getAdvertisingIdInfo`, `nativeOnAndroidAdvertisingIdResult`. See reproduction below. `AD_ID` itself (the permission string) is genuinely absent everywhere I looked, including `classes.dex` — that half of the claim holds. |
| 8 | Judgement on item 7's finding: does it change the "no dependency puts it back" conclusion? | **Qualified pass, noted as a gap** | The four strings are Unity's own `AndroidAdvertisingIdHelper`, part of the base Android player Unity ships in *every* build (present regardless of GameAnalytics), which reflectively probes for the GMS `AdvertisingIdClient` class at runtime (`Class.forName`-style string, not a compiled class reference — I confirmed `Lcom/google/android/gms/ads/identifier/AdvertisingIdClient;`, the compiled-class form, is **absent** from `classes.dex`). With no GMS ads-identifier artifact linked and no `AD_ID` permission declared, this call would fail closed (caught `ClassNotFoundException`) today. So the *practical* conclusion — no advertising ID is collected today — still holds, but NOTES' explanation ("not because the Android side skipped D9's rule, but because GameAnalytics is not integrated") is incomplete: dormant, GMS-capable code already ships from the engine itself, independent of GameAnalytics, and the audit's own "check that no dependency puts it back" (task SCOPE) did not look inside the compiled binary for this. |
| 9 | Comparison table vs `60-shell-build/17-permission-audit/NOTES.md`, and the "sharpest claim" (clean manifest = incomplete feature, not platform virtue) | **Pass, fair** | iOS NOTES: 48 plist keys, one permission (`NSCameraUsageDescription`, from `50-photo/08`). Android NOTES: one permission (`POST_NOTIFICATIONS`, from `90-android/09-notifications`, itself `status:done, verify:passed`). The document's own conclusion — that the apparent Android/iOS parity is an artefact of `04-picker-plugin` being `status:todo`, not a real platform difference — is stated plainly and is correct per the check above. It does not overclaim credit for Android; if anything it undercuts its own headline number, which is the right instinct. One accuracy note: the iOS document itself is still `verify:pending` (`60-shell-build/17-permission-audit/labels.txt`), so the comparison is against an unverified baseline — worth knowing, not a defect in this document. |

## Overall verdict: **verify:failed**

Items 1–6 and 9 hold up under independent re-checking. Item 7 is a specific,
sourced, false factual claim in the document ("returns nothing" when it does
not), and item 8 shows the SCOPE requirement it was meant to satisfy ("a
check that no dependency puts it back") was not actually exercised against
the compiled APK, only against source and the manifest. This is the exact
failure shape `tasks/README.md`'s verify rule exists for — a specific quoted
result that does not reproduce. The fix is cheap (correct the sentence,
either narrowing it to "Assets and the manifest" to match VERIFY 2's literal
wording, or acknowledging the `AndroidAdvertisingIdHelper` string and
explaining why it is dormant) but it has not been made, so I am not signing
this off as passed. `status:` is left at `review` — not moved to `done`.

## How to reproduce

All commands below run from a clean checkout with nothing exported by hand,
against the already-built `game/build/android/CatShelter.apk` (no build,
no adb, no emulator required).

```bash
AAPT2=/Applications/Unity/Hub/Editor/6000.3.22f1/PlaybackEngines/AndroidPlayer/SDK/build-tools/36.0.0/aapt2
APK=game/build/android/CatShelter.apk

# item 1 — short permissions dump
"$AAPT2" dump permissions "$APK"

# item 2 — full manifest tree, literal tool output (compare to NOTES.md's reformatting)
"$AAPT2" dump xmltree "$APK" --file AndroidManifest.xml

# items 5–6 — source-level and manifest-level AD_ID/advertising check
grep -rn "AD_ID" game/Assets
grep -rin "advertisingId\|AdvertisingId" game/Assets
grep -rn "EnableAdvertisingIdTracking" game/Assets
find game/Assets/Plugins -iname "*android*"
cat tasks/90-android/04-picker-plugin/labels.txt

# items 7–8 — the binary-level check NOTES.md's "strings over the APK" claim needed
mkdir -p /tmp/apk_extract && unzip -oq "$APK" -d /tmp/apk_extract
strings /tmp/apk_extract/classes.dex | grep -i "ad_id"          # empty — AD_ID itself is absent
strings /tmp/apk_extract/classes.dex | grep -i "advertising"    # NOT empty — contradicts NOTES.md
strings /tmp/apk_extract/classes.dex | grep "Lcom/google/android/gms/ads/identifier"  # empty — no compiled GMS class, reflection-only
strings /tmp/apk_extract/AndroidManifest.xml | grep -i "ad_id\|advertising"  # empty — the manifest itself is clean

# item 3 — SDK-level cross-check
grep -n "sdkVersion\|targetSdkVersion" tasks/90-android/02-build-pipeline/NOTES.md
```

## What was not checked

- **VERIFY item 3 of `task.txt`** ("a clean install raises no permission
  dialog until the player presses the button that needs one") — requires an
  install and a tap-through on a device or emulator. Off-limits under this
  verification's own constraints (no adb, no emulator) exactly as it was
  off-limits to the original author. Still open; needs the NATIVE context
  that owns the emulator.
- Whether `04-picker-plugin`, once built, actually avoids `CAMERA`/storage
  permissions in practice — can't be checked before it exists.
- The `manifest.json` / Unity package manifest package list (analogous to
  the two packages the iOS audit found and removed) — NOTES.md correctly
  marks this out of this task's stated SCOPE, and I did not pull it in
  either; flagged in both documents as a follow-up, not a gap in this
  verification.
- Whether the `libil2cpp.so` / `libunity.so` native libraries (44 MB / 17 MB
  of the APK) contain further advertising-related strings — I checked
  `classes.dex`, `resources.arsc` and `AndroidManifest.xml` only, the three
  places a manifest-permission audit would plausibly look; the native
  libraries are Unity engine binaries out of scope for a permission/manifest
  audit and were not scanned.
- Signing/certificate details of the APK — irrelevant to this task's SCOPE
  and not examined.
