# Notes — 22-signing-versioncode

## Done (versionCode half only — the signing-key half needs the owner)

- `game/Assets/Editor/BuildScript.cs`: added `BumpVersionCode()`, called from
  `StampVersion()` before it writes `bundleVersion`.
  - Source: `git rev-list --count HEAD` (monotonic, reproducible — same
    commit always gives the same number).
  - Guard against non-growth: if commit count <= the versionCode already
    recorded in `PlayerSettings.Android.bundleVersionCode`, uses
    recorded+1 instead and logs a WARNING explaining why (covers "no git"
    and "branch behind / shallow history" alike — both fall back the same
    way).
  - Sets `PlayerSettings.Android.bundleVersionCode` AND
    `PlayerSettings.iOS.buildNumber` to the same number (iOS already had
    the analogous field, `buildNumber: iPhone: 0` in
    `ProjectSettings.asset:172-176` — CFBundleVersion), so the two
    platforms never disagree about which build a screenshot is from.
  - `bundleVersion` string is now `"{date} {hash} vc{code}"` — e.g.
    `09-02 21:05 ddc64d7 vc260`. No change needed in
    `View/CaptureScreen.cs`: its stamp label reads `Application.version`
    directly, which is `bundleVersion`, so the code rides along for free.
  - Same never-fail-a-build discipline as the existing hash lookup: every
    git call and every PlayerSettings write is wrapped in try/catch, falls
    back to something honest, never throws.

## Verified

- `bash build/headless-build.sh --tests-only` — green (see terminal output
  in the parent task run; Core coverage 96.7%, photo-rotation, Swift parse
  all passed).
- Two consecutive `BuildAndroidPlayer` runs (`/tmp/build-vc1.log`,
  `/tmp/build-vc2.log`) produced **different, increasing** versionCodes:
  259 then 260 (both builds ran with git HEAD count=258 at the same
  commit `ddc64d7`, since nothing was committed between the two builds —
  which exercised exactly the "branch behind / no new commits" fallback
  path: build1 log line
  `commit count 258 <= recorded versionCode 258 ... using recorded+1 = 259`,
  build2 `commit count 258 <= recorded versionCode 259 ... using
  recorded+1 = 260`).
- `aapt2 dump badging` on both APKs confirmed
  `versionCode='259' versionName='... vc259'` then
  `versionCode='260' versionName='... vc260'`.
- Installed build2 on `emulator-5554` (`adb install -r -d`, after
  `pm clear com.sootpaw.game`), launched
  `com.sootpaw.game/com.unity3d.player.UnityPlayerGameActivity` (the
  actual launcher activity — `UnityPlayerActivity` does not exist in this
  build, found via `pm dump` / `cmd package resolve-activity`), and
  confirmed via `dumpsys package` that the installed versionCode is 260.
  Screenshot of the capture screen shows the stamp
  `09-02 21:05 ddc64d7 vc260` at the bottom, matching.

## Not done — left for the owner (per task split)

- Android release signing key: creation and storage are the owner's job
  (a path file next to `analytics-keys.txt`, key kept out of git). Nothing
  touched in `AndroidKeystoreName` / `AndroidKeyaliasName`
  (`ProjectSettings.asset` — still empty). Builds are still signed with
  the Unity debug key.
- iOS `appleDeveloperTeamID` / automatic signing — blocked on a paid Apple
  Developer account (decision D17), out of scope here per task text.
- `AndroidBundleVersionCode` and `buildNumber: iPhone` in
  `ProjectSettings.asset` were bumped to 260 as a side effect of the two
  verification builds above (Unity writes them back to the asset on
  build) — this is expected/intended behavior of the new code, not a
  manual edit, and will keep moving on every future build.

## Status

`labels.txt` left as `status:in_progress` — task stays open until the
owner does the signing-key half.
