Verifier: independent QA context, wrote none of `game/Assets/Shell/Copy.cs`,
`tools/tests/test_copy_table.py`, `game/Assets/View/CaptureScreen.cs`,
`game/Assets/Shell/CatPicker.cs`, `game/Assets/Plugins/iOS/CatPicker.swift`,
or this task's `task.txt`/`NOTES.md`. Ran the pytest suite and read every
source file named below directly, tracing call sites end to end rather than
trusting either task's or `Copy.cs`'s doc comments. Did **not** run a Unity
build, PlayMode test, the Android emulator, or adb — out of scope per the
brief given for this pass, and in any case this task's own SCOPE excludes
`com.unity.localization` and any actual second language, so no build could
exercise a translated string regardless. Checked together with
`12-copy-english/VERIFY.md`, which this file cross-references rather than
repeats.

## Verdict by item

| # | Question | Verdict | Evidence |
|---|---|---|---|
| 1 | Does `test_copy_table.py` enforce what the tasks claim, and does the `CatPicker.cs` exemption hold for *this* task's stronger claim ("no literal outside the table")? | **Exemption does not hold — reproducible violation of the OUTCOME** | `game/Assets/View/CaptureScreen.cs:139-146`: on any picker failure other than cancellation, `_message.text = Shell.Copy.Of("capture.failed", reason)`, and `Copy.cs:112` is `["capture.failed"] = "That did not work: {0}"`. `reason` is a raw string that is never itself a table key: from C# (`CatPicker.cs:71,82` — `"gallery is iOS-only"`, `"camera is iOS-only"`; `CatPicker.cs:118` — `$"could not read the picked photo: {e.Message}"`) or from native Swift, unreachable by the test at all (`CatPicker.swift:40,48,85,109` — `"could not read the picked image"`, `"could not save the picked image: \(error.localizedDescription)"`, `"no window to present from"`, and `"camera"` which becomes `"{what} is not available"` via `CatPicker.cs:126`). Any of these, substituted into the template, is player-visible text that: (a) never appears as a key in `Copy.cs`, so a second-language table has nothing to translate it into — the sentence's *tail* stays English (or worse, OS-language, see below) no matter what language `Copy.Current` is set to; (b) is invisible to `test_copy_table.py` twice over — once because `CatPicker.cs` is in `EXEMPT` (`test_copy_table.py:20`), and again because Swift files are outside `UI_DIRS` entirely (`test_copy_table.py:13`, C#-only). `CatPicker.cs`'s stated exemption reason — "failure reasons handed to `Copy.Of("capture.failed")`" — is true as far as it goes, but omits that the *content* handed in is exactly the kind of literal the test exists to catch, and it reaches the player in a currently-live, non-hypothetical code path (any picker failure), not a future screen. **This directly falsifies OUTCOME: "No user-visible English string literal remains outside the table."** |
| 2 | Are `notification.channel` / `notification.channel_description` covered by the mechanism and correctly wired? | **Pass** | Same evidence as `12-copy-english/VERIFY.md` item 2: declared `Copy.cs:132-134`, used `EveningReminder.cs:221-222`, covered by `test_every_key_the_code_asks_for_exists` / `test_every_declared_key_is_used`, both passing (`21 passed`). This is the mechanism working as designed — the counterexample in item 1 is a case where a call site bypasses the mechanism, not a flaw in the mechanism itself. |
| 3 | `Core/Cat.cs`'s `DefaultName = "Kitty"` — is the test's `View`/`Shell`-only scope wrong, and is "defensible, flag as future risk" the right call? | **Scope boundary is right; the remedy already on record is incomplete — my own recommendation below** | Confirmed via `tasks/50-photo/10-skip-default-cat/VERIFY.md` item 4 (a genuinely independent prior verification, not this task's own author) that this was already found and reasoned through: no `View`/`Shell` file references `Cat.DefaultName` today (`50-photo/09-meet-your-cat` is `status:todo`), so it is not a live violation of *this* task's OUTCOME yet, and widening `UI_DIRS` to include all of `Core` would flag unrelated domain strings (error type names, internal identifiers) that are not player copy — a real cost, not a free improvement. That prior verifier also identified the sharper problem precisely: the most likely way `09` gets built is `nameField.value = Cat.DefaultName`, a bare symbol reference with **no string literal anywhere in `View`/`Shell`**, which a literal-regex test can never catch **even in principle**, regardless of which directories it scans. I agree with that diagnosis and go one step further: the fix is not "scan more directories" but a **different check** — a symbol-usage guard (e.g. `grep -rn "Cat\.DefaultName" game/Assets/View game/Assets/Shell` asserted empty, or making `DefaultName` non-public and exposing only an `IsDefault` predicate from `Cat` so `View`/`Shell` structurally cannot read the raw string at all) added *now*, before `09` exists, as a tripwire — not deferred to "whoever picks up 09 or 16." Neither `50-photo/10`'s NOTES.md nor its VERIFY.md proposes an enforced guard, only a plan ("worth deciding then"); I consider a plan with no test behind it an open item, not a closed one. I could not add this myself — `tools/tests/test_copy_table.py` is outside the two directories this pass is scoped to touch — so it is recorded here as an unaddressed recommendation, not implemented. |
| 4 | Player-visible text the test would miss (UXML/USS, concatenation, `Plugins/iOS/*.swift`) — does it undermine "ready", beyond item 1? | **One more finding, lower severity: dead-but-untabled UXML text, currently harmless by luck of every call site, not by a guarantee** | `game/Assets/View/DebugGame.uxml:12-13` hardcodes `text="One more shelf"` and `text="Continue"` on the two overlay buttons, outside the table and outside the test's `*.cs`-only glob. Traced every call site of `ShowCard` in `DebugGameView.cs` (lines 138-140, 350-352, 359-365, 377-384): all four pass `secondaryText: null`, so the button carrying `"One more shelf"` is unconditionally hidden (`ShowCard`, line 415), and `_primaryButton.text` is unconditionally overwritten from a `Copy.Of(...)` call before display (line 403) — so neither literal is seen by a player *today*. But `"One more shelf"` is exactly the string `DECISIONS.md` D4 says was removed from the lose screen on 2026-08-27 ("The button and its two strings are gone from the lose screen... the wording is kept in that decision, not here") — its continued presence in committed markup contradicts that stated intent, and nothing prevents a future `ShowCard` call site from passing a non-`Copy.Of` string as `secondaryText` and silently reviving it, since the UXML default would then partially show through the moment a null check elsewhere failed. This is the same shape of gap as item 1 — a bypass channel invisible to the test — but currently inert rather than live. |

**Overall verdict for `16-localisation-ready`: `verify:failed`.** OUTCOME's own words — "No user-visible English string literal remains outside the table" — are contradicted by a reproducible, currently-live code path (item 1: any camera/gallery picker failure other than cancellation). The table mechanism itself (item 2) works correctly for everything routed through it; the failure is that not everything player-visible is routed through it, and two of the paths that aren't are structurally invisible to the enforcing test (Swift entirely, and any future bare-symbol reference to `Cat.DefaultName`). `status:` moved `done → in_progress` per the README's status:done rule — the artefact named in OUTCOME does not fully exist as claimed, and this is fixable in-repo (translate/route the `CatPicker` reason strings through `Copy.cs` keys, e.g. `photo.picker_failed` variants; decide and enforce the `Cat.DefaultName` question per item 3) rather than blocked on anything outside the repository.

## How to reproduce

From a clean checkout, no exported variables:

```sh
git rev-parse HEAD   # 4b8d96521a66af16ee0a3c6f00f058bd6dc4f8a1 at time of this check
.venv/bin/python -m pytest tools/tests/test_copy_table.py -q
# -> 21 passed  (the mechanism itself is sound; it simply cannot see this gap)
sed -n '135,151p' game/Assets/View/CaptureScreen.cs
# -> reason (raw string) formatted into Copy.Of("capture.failed", reason)
sed -n '60,127p' game/Assets/Shell/CatPicker.cs
# -> "gallery is iOS-only", "camera is iOS-only", "could not read the picked
#    photo: {e.Message}", "{what} is not available" — none are Copy.cs keys
sed -n '38,50p' game/Assets/Plugins/iOS/CatPicker.swift
# -> "could not read the picked image", "could not save the picked image:
#    \(error.localizedDescription)" sent via UnitySendMessage, outside any
#    file test_copy_table.py's UI_DIRS (C#-only) can reach
sed -n '11,17p' game/Assets/View/DebugGame.uxml
# -> "One more shelf" / "Continue" literal, outside Copy.cs and outside *.cs
grep -rn "Cat.DefaultName" game/Assets --include=*.cs | grep -v /Tests/
# -> only the definition in Core/Cat.cs; no View/Shell caller today
```

## What was not checked

- No Unity build, PlayMode test, Android emulator, or adb — this task's own
  SCOPE excludes an actual second language and `com.unity.localization`, so
  there is nothing translated to run on a device even if one were available.
- Whether `error.localizedDescription` (Swift, `CatPicker.swift:48`) can
  itself surface in a non-English system language on a device whose OS
  locale differs from the game's — reasoned about (it plausibly can, since
  that property follows the OS, not the app), not confirmed on a real
  device.
- The severity of item 1 in practice — how often a real player actually hits
  a picker failure other than cancellation — was not measured; this pass
  establishes that the path exists and is reachable in code, not its
  real-world frequency.
- Whether `EXEMPT` in `test_copy_table.py` should be changed at all was
  considered but not acted on: removing `CatPicker.cs` from `EXEMPT` would
  not by itself catch this gap, since the offending strings are lowercase
  and the test's sentence regex requires a capitalised first word
  (`test_copy_table.py:50`) — the fix belongs in the code (route the reasons
  through `Copy.cs` keys), not in loosening or tightening the test's own
  pattern, and that code change is outside the touch-scope given for this
  verification pass.
- `Core/CatTraits.cs`, `Core/CatSave.cs` and the rest of `Core` were not
  re-read in this pass beyond what `50-photo/10-skip-default-cat/VERIFY.md`
  already established, which this file cites rather than re-derives.
