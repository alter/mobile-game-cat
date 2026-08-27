# Independent verification, 2026-08-27 (second pass)

**Verifier:** fresh context, wrote none of `NOTES.md` (original audit,
author's correction, or coordinator's corroboration), none of the Android
build pipeline, and made no build-script fix that put any permission in the
manifest — unlike the coordinator's corroboration, which explicitly recuses
itself from the permission-list item for exactly that reason. No adb,
no emulator, no Unity build. Re-ran `aapt2 dump permissions`/`dump xmltree`
and `unzip`+`strings` against the `.apk` on disk myself rather than trusting
either quoted transcript.

## Per-item verdict

| # | Item | Result |
|---|---|---|
| 1 | correction's binary claim (`AdvertisingIdClient` as string vs class descriptor) | **pass** — reproduced independently |
| 2 | NOTES accounts for the APK having moved; re-run instruction overtaken | **partial — narration is honest, the deliverable table is not updated** |
| 3 | corroboration's recusal recorded and its evidence reproducible | **pass** |
| 4 | is the audit accurate about the binary that exists today | **no — fail** |

### 1. The binary claim

`unzip -oq CatShelter.apk classes.dex AndroidManifest.xml`, then:
`strings classes.dex | grep -i advertising` → 7 lines including
`.AdvertisingIdClient`, `/Lcom/unity3d/player/AndroidAdvertisingIdHelper;`,
`9com.google.android.gms.ads.identifier.AdvertisingIdClient`,
`Dcom.google.android.gms.ads.identifier.internal.IAdvertisingIdService`,
`getAdvertisingIdInfo`, `getAdvertisingInfoObject`,
`nativeOnAndroidAdvertisingIdResult` (two more than either the correction or
the corroboration quoted — `.AdvertisingIdClient` and
`getAdvertisingInfoObject` — consistent with GameAnalytics's own reflective
probe, not a contradiction of either). The deciding line:
`strings classes.dex | grep -c "Lcom/google/android/gms/ads/identifier/AdvertisingIdClient;"`
→ **0**. The GMS type appears only as a string (reflection target), never as
a compiled class descriptor. `strings AndroidManifest.xml | grep -i
"ad_id\|advertising"` → empty. This matches both the correction and the
corroboration exactly: `AD_ID` absent, advertising-ID access dormant and
reflective, not linked.

### 2. Has the APK moved, and does NOTES account for it

It has moved twice since the original audit. `game/build/android/
CatShelter.apk` is now 27,988,228 bytes (NOTES.md's original pass measured
27,850,892), and `aapt2 dump permissions` today returns **four**
`uses-permission` lines, not two:

```
android.permission.INTERNET
android.permission.ACCESS_NETWORK_STATE
android.permission.POST_NOTIFICATIONS
com.DefaultCompany.game.DYNAMIC_RECEIVER_NOT_EXPORTED_PERMISSION
```

`INTERNET` and `ACCESS_NETWORK_STATE` are new — exactly what NOTES.md's
correction §4 predicted GameAnalytics would add, and exactly what the
coordinator's corroboration already confirmed by quoting the same
`aapt2 dump permissions` output. So the narrative is honest and current:
correction §4 says this audit "must be re-run" and correctly forecasts what
would change; the corroboration confirms the forecast came true.

**What is missing:** the actual re-run. The task's OUTCOME is "a table in
this directory listing every permission and feature in the .apk with its
justification" — that is the `## Every permission, every declared
component` table in the main body of `NOTES.md`, not the dated correction
prose beneath it. That table still lists only two permission-shaped rows
(`POST_NOTIFICATIONS`, the self-referential receiver permission) and has no
row for `INTERNET` or `ACCESS_NETWORK_STATE` — the "who added it / what
player action needs it / what breaks without it" columns the task format
requires are simply absent for two permissions that are in the shipped
binary today. `xmltree` confirms no other structural change: same one
activity, one receiver, one provider (the `androidx.startup` provider's two
`meta-data` children are unchanged — GameAnalytics did not add its own
entries there in this build), so filling the table needs exactly two new
rows, sourced from `70-analytics/01-sdk-integration/NOTES.md`'s own
GameAnalytics manifest reading (§3 of the correction) — not new
investigation, just transcription that has not been done yet.

### 3. The corroboration's recusal

Recorded verbatim: *"This is corroboration, not a verdict, and the reason is
a conflict of interest. `POST_NOTIFICATIONS` is in that manifest because of
a build-script fix I made today... I am not an independent judge of the
permission-list item... The advertising-identifier findings I had no hand
in, and they hold."* Its quoted evidence (`aapt2 dump permissions` four
lines; the five `advertising` strings; the class-descriptor count of `0`)
reproduces byte-for-byte against the live APK, confirmed above. No number of
the coordinator's was wrong.

### 4. Overall

The AD_ID/advertising-identifier defect that failed the first VERIFY.md is
correctly corrected and independently reproducible — that part of the
document is trustworthy and does not need re-litigating. But the binary
underneath the audit changed a second time, and while the dated notes
narrate that change accurately and even predicted it in advance, **the
audit's own deliverable — the permission table — was never updated to match
the binary that exists right now.** Task VERIFY item 1 ("`aapt dump
permissions` output pasted whole, every line matched to a row") fails
against today's APK: two of its four lines have no row. The document is
honest about being stale (it says so, twice, in its own words) but honesty
about staleness is not the same as the table being current.

## How to reproduce

```bash
AAPT2=/Applications/Unity/Hub/Editor/6000.3.22f1/PlaybackEngines/AndroidPlayer/SDK/build-tools/36.0.0/aapt2
APK=game/build/android/CatShelter.apk

"$AAPT2" dump permissions "$APK"
# -> INTERNET, ACCESS_NETWORK_STATE, POST_NOTIFICATIONS, DYNAMIC_RECEIVER_NOT_EXPORTED_PERMISSION
# NOTES.md's main table has rows for only the latter two.

"$AAPT2" dump xmltree "$APK" --file AndroidManifest.xml \
  | grep -n "uses-permission\|uses-feature\|receiver\|provider\|E: activity\|uses-sdk"
# same components as NOTES.md's original dump, plus the two new uses-permission lines

rm -rf /tmp/apk_check && mkdir -p /tmp/apk_check
unzip -oq "$APK" classes.dex AndroidManifest.xml -d /tmp/apk_check
cd /tmp/apk_check
strings classes.dex | grep -i "ad_id"                                              # empty
strings classes.dex | grep -i "advertising"                                        # 7 lines, reflection strings only
strings classes.dex | grep -c "Lcom/google/android/gms/ads/identifier/AdvertisingIdClient;"  # 0 — not a linked class
strings AndroidManifest.xml | grep -i "ad_id\|advertising"                         # empty
```

## What was not checked

- VERIFY item 3 of `task.txt` (install-time permission-dialog behaviour) —
  needs adb/emulator, off-limits here as it was to every prior context.
- Items 1, 3, 5, 6, 8, 9 of the first `VERIFY.md`'s table (SDK levels,
  picker-plugin absence, source-level `AD_ID` grep, iOS comparison) — not
  re-run; nothing in this pass gives reason to doubt them, and the binary
  change under review here (INTERNET/ACCESS_NETWORK_STATE, GameAnalytics
  strings) does not touch any of them.
- Whether `libil2cpp.so`/`libunity.so` carry further advertising strings —
  out of scope for a manifest/component audit, as the first VERIFY.md
  already noted.
- Whether the `androidx.startup` provider gains GameAnalytics `meta-data`
  children in a later build — checked only against today's APK, where it
  has not.

## Verdict

`verify: failed`. Not for the AD_ID claim — that is fixed and reproduces
cleanly — but because the audit's own OUTCOME, the permission table, does
not describe the binary that exists today: it is missing rows for
`INTERNET` and `ACCESS_NETWORK_STATE`, both present in the current
`CatShelter.apk` and both already explained (source: GameAnalytics's AAR
manifest, per `70-analytics/01-sdk-integration/NOTES.md` §3, already quoted
inside this document's own correction). The fix is two rows in an existing
table, already sourced — not new investigation.

`status:` left at `review`, unchanged.
