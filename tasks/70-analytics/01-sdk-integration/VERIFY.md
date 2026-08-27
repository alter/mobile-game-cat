# Independent verification, 2026-08-27

**Verifier:** a fresh agent context, invoked specifically to check this
work. I wrote none of `game/Assets/Shell/GameAnalyticsSink.cs`, none of the
`GameBoot.cs` changes, and did not add the `com.gameanalytics.sdk`
dependency or scoped registry to `manifest.json`. I did **not** run a Unity
build (the owner said they were running one concurrently — a second would
collide) and did **not** run `adb`. I did run `dotnet test
build/core-tests/core-tests.csproj -v q --nologo` and
`build/check-core-purity.sh` myself (both permitted), and I read the actual
GameAnalytics Unity SDK source from `github.com/GameAnalytics/GA-SDK-UNITY`
(`master`, matching the pinned `8.1.0`) directly, rather than trusting
`NOTES.md`'s own reading of it — that primary-source read is where the
central finding below comes from.

## Per-item verdict

| # | Claim checked | Verdict | Evidence |
|---|---|---|---|
| 1 | No key can ever be committed | **Does not fully hold — one open path, already flagged, still unmitigated** | See "The central finding" below. The code path built (`analytics-keys.txt` → `GameAnalyticsSink`) is safe: `grep -rniE "gamekey\|secretkey\|game_key\|secret_key"` and a search for any hex/base64-looking constant across `game/Assets` finds only parsing code, no value. But the SDK's own `Assets/Resources/GameAnalytics/Settings.asset` — confirmed by reading `GameAnalytics.cs`'s `InitAPI()` directly — auto-creates (empty) the moment anything touches `GameAnalyticsSDK.GameAnalytics.SettingsGA` **inside the Unity Editor**, which `GameAnalyticsSink.Configure`'s very first line does. That asset is a normal serialized `ScriptableObject` (`[SerializeField] private List<string> gameKey/secretKey` in `Setup/Settings.cs`) with a first-class Editor Inspector tab ("I want to fill in my game keys manually", `GA_SettingsInspector.cs`) that writes typed keys straight into those fields. `game/.gitignore` has no rule for `Resources/GameAnalytics/` or `*.asset` — nothing stops that file, once it exists, from being swept into a commit. |
| 2 | The no-key path is a true no-op | **Holds, for every state checked** | Traced `TryConfigure` by hand: file absent → `File.Exists` false → `(null, null)`, no `Debug` call at all. File present, 0 lines (empty file) → `lines.Length < 2` → `(null, null)`. Exactly 1 line → same. 2 lines, one or both blank/whitespace-only → `.Trim()` then `Length == 0` check → `(null, null)`. All of these return before any GameAnalytics API is touched, so no network call and no repeated log line — matches "exactly as before." One adjacent case, **outside item 2's literal scope but worth flagging**: if keys ARE present and well-formed but `SettingsGA` is null (package Settings asset not yet created), `Configure` logs one `Debug.LogWarning` **on every launch** until that asset exists — not a no-op in that specific state, though it is not the "no key" state the item asks about. |
| 3 | D9 compliance | **Holds, confirmed from SDK source, not just the report** | `grep -rn "RequestTrackingAuthorization" game/Assets` → two hits, both inside comments (lines 109/112 of `GameAnalyticsSink.cs`); no call site anywhere. `grep -rn "EnableAdvertisingIdTracking"` → the call at line 114, and it precedes `Initialize()` at line 131 (`AddComponent` at 129 runs first, which is fine — it only attaches the component, `Initialize()` is the call that matters and is last). Read `GameAnalytics.cs` from the actual SDK: `RequestTrackingAuthorization` exists as a public method gated `#if UNITY_IOS \|\| UNITY_TVOS` but is **never called by the SDK itself** — it is a pure opt-in surface, so not calling it is sufficient; the SDK does not request ATT on its own. One nuance not in the report: `EnableAdvertisingIdTracking` is implemented as `#if UNITY_ANDROID && (!UNITY_EDITOR) GA_Wrapper.enableGAIDTracking(flag) #endif` — **it does nothing on iOS**. That's not a defect: on iOS, IDFA is gated by the OS itself behind ATT authorization, which is never requested, so there is nothing for this flag to disable there; on Android it is the actual kill switch for the reflection-based AD_ID lookup this project's own `90-android/10-permission-audit` traced through the AAR's `classes.jar`. |
| 4 | Core stays engine-free and analytics-ignorant | **Holds** | `git diff --stat -- game/Assets/Core/` against the current commit is empty — nothing under `Assets/Core` changed. `build/check-core-purity.sh` → `Core is engine-free: OK`, re-run myself. `GameAnalyticsSink.cs` lives in `Assets/Shell`; `Core.Analytics.Configure` still only takes `Action<string,double,string>`/`Action<string,int,string>` (unchanged signature). |
| 5 | The scoped registry | **Accurately described, with one thing worth naming plainly** | `manifest.json` adds `package.openupm.com` scoped to `["com.gameanalytics"]` and pins `"com.gameanalytics.sdk": "8.1.0"` — re-verified live against the registry itself (`curl .../com.gameanalytics.sdk` → `dist-tags.latest: "8.1.0"`, 80 versions listed) and against GitHub's own release page (`8.1.0 Latest Aug 21, 2026`), both matching `NOTES.md`. Unity's `manifest.json` version strings are always exact resolutions, not semver ranges, so `"8.1.0"` **is** a hard pin, consistent with every other entry already in this file (e.g. `com.unity.mobile.notifications: 2.4.3`) — not a deviation. What this means plainly: anyone building this project now resolves one package from **OpenUPM**, a third-party community registry/proxy, not from Unity's own registry or npmjs — a real, if standard-in-the-Unity-ecosystem, addition to the project's supply-chain trust surface, worth naming out loud rather than treating as equivalent to the first-party packages already in the file. |

## The central finding, stated plainly

**A key cannot reach a commit through the code this task wrote.** It **can**
reach one through a path this task did not close: opening the project in
the Unity Editor with `GameAnalyticsSink` wired in will, on its own,
materialize `Assets/Resources/GameAnalytics/Settings.asset` on disk (empty,
via `AssetDatabase.CreateAsset` + `SaveAssets()` inside `InitAPI()` —
confirmed by reading that method directly, not inferred). That file sits
under `Assets/`, which git tracks, and `game/.gitignore` has no rule for it.
The SDK ships its own Inspector UI for typing a Game Key/Secret Key directly
into that same asset ("I want to fill in my game keys manually") — a normal,
documented, first-party way to configure GameAnalytics that has nothing to
do with this project's `analytics-keys.txt` convention and isn't prevented
by it. The commit that shipped this work (`88f80a1`) already names this
exact gap in its own message — *"No settings asset appeared either... though
a first Editor open is a different question and stays open"* — so this is a
known-open risk, not a missed one; I am confirming its mechanics precisely
and it remains unmitigated as of this check. **Recommended, not applied
(outside this touch scope): add `game/Assets/Resources/GameAnalytics/` — or
just `Settings.asset` within it — to `game/.gitignore`.**

## How to reproduce

```bash
# item 1 — no value anywhere, and the .gitignore gap
grep -rniE "gamekey|secretkey|game_key|secret_key" game/Assets --include="*.cs"
find game/Assets -ipath "*GameAnalytics*Settings*"
grep -n "Resources\|GameAnalytics\|Settings" game/.gitignore   # empty output — no rule exists

# item 2 — trace by reading
sed -n '39,65p' game/Assets/Shell/GameAnalyticsSink.cs

# item 3 — grep independently
grep -rn "RequestTrackingAuthorization" game/Assets
grep -rn "EnableAdvertisingIdTracking" game/Assets
grep -n "EnableAdvertisingIdTracking\|AddComponent<GameAnalyticsSDK.GameAnalytics>\|GameAnalytics.Initialize()" game/Assets/Shell/GameAnalyticsSink.cs

# item 4
git diff --stat -- game/Assets/Core/
bash build/check-core-purity.sh
dotnet test build/core-tests/core-tests.csproj -v q --nologo

# item 5
curl -s "https://package.openupm.com/com.gameanalytics.sdk" | python3 -c "import json,sys; d=json.load(sys.stdin); print(d['dist-tags'])"
grep -A2 "com.gameanalytics.sdk" game/Packages/manifest.json

# the central finding — primary-source confirmation
# (network reads of github.com/GameAnalytics/GA-SDK-UNITY, master branch:
#  Runtime/Scripts/GameAnalytics.cs InitAPI(); Runtime/Scripts/Setup/Settings.cs
#  gameKey/secretKey fields; Editor/GA_SettingsInspector.cs manual-entry tab)
```

## What was not checked

- **Compiling `GameAnalyticsSink.cs` against the resolved package.** Not run
  by me — the owner was running a Unity build concurrently and asked that I
  not run a second one. A compile check already exists in commit `88f80a1`'s
  own message ("Zero error CS, exit 0, package resolved"), and the owner may
  add a further result from the build in progress; either way that number is
  not mine to report as something I personally verified.
- **VERIFY 1 and 2 of `task.txt`** (debug log line on device launch, no ATT
  dialog on first launch) — both need a real device/simulator run, out of
  scope for a static, no-build pass.
- **Whether a batch/CI Android or iOS build (not an interactive Editor
  session) also triggers `InitAPI()`'s asset-creation branch.** That branch
  is `#if UNITY_EDITOR`-gated, and commit `88f80a1` reports no settings asset
  appeared after an Android build — consistent with batch builds not
  entering the Editor code path — but I did not independently run a build to
  confirm this, per the constraint above.
- **The GameAnalytics account/dashboard side** (whether events actually
  arrive) — needs the human sign-up step `NOTES.md` already defers, and is
  `00-att-silent-check`'s job, not this task's.
- **Whether OpenUPM's own registry infrastructure has had any past
  supply-chain incident** — not researched; flagged as a trust-surface fact
  in item 5, not audited further.

## Verdict

`verify:failed` on this task as written — not because the code is unsafe in
what it does, but because the OUTCOME's "no key can ever be committed"
premise (task's own framing, and the reason this review names it explicitly)
has a real, confirmed, currently-open gap that isn't closed by anything in
this diff, and the fix (one `.gitignore` line) is outside this verification's
touch scope to apply. `status:` left at `review` — the fix is small and this
is close, not a rewrite.
