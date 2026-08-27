# Built, 2026-08-27 — first try, no surprises

`BuildScript.BuildAndroidPlayer` and `BuildScript.BuildAndroidBundle`, beside
the two iOS entry points.

```sh
UNITY="/Applications/Unity/Hub/Editor/6000.3.22f1/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -quit -nographics -projectPath game \
         -executeMethod BuildScript.BuildAndroidPlayer -logFile game/build/android-build.log
"$UNITY" -batchmode -quit -nographics -projectPath game \
         -executeMethod BuildScript.BuildAndroidBundle -logFile game/build/android-aab.log
```

Results, both succeeded with zero errors:

| output | size |
|---|---|
| `game/build/android/CatShelter.apk` | 26.5 MB |
| `game/build/android/CatShelter.aab` | 26.5 MB |

`aapt dump badging`: package `com.DefaultCompany.game` — the same identifier as
iOS, which `12-play-console` must reuse — `native-code: 'arm64-v8a'`,
`targetSdkVersion: 36`, `sdkVersion: 25`.

## Two settings with reasons

**ARM64 only, IL2CPP.** Play requires 64-bit; adding ARMv7 doubles build time
for devices this audience does not hold.

**Minimum API 25, not 24.** Asking for 24 does not fail the build — it logs
`Minimum supported Android API level is 25 (Android 7.1 Nougat)` and silently
uses 25 anyway. Left at 24 the source would have claimed a decision that the
engine was quietly overriding, so the constant now says what actually ships.

**Target SDK is `AndroidApiLevelAuto`** — Play rejects uploads built against an
SDK more than a year behind, and pinning a number here turns that into a
surprise at upload time.

## Nothing else was needed

No Android-specific code, no manifest of ours, no gradle template. The engine,
the rules, the levels, the save and the UI came across untouched — which is the
result `11-save-parity` then confirmed by moving a real position between runs.

## Against the VERIFY list

1. **Met** — both commands run to completion from the command line, files exist,
   sizes above.
2. **Met** — `aapt dump badging` output quoted above.
3. **Met** — no `error CS` in either log; both runs exit 0.
