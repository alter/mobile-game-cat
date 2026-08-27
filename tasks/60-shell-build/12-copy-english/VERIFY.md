Verifier: independent QA context, wrote none of `game/Assets/Shell/Copy.cs`,
`tools/tests/test_copy_table.py`, or any file under `game/Assets/View` /
`game/Assets/Shell`, and wrote neither `task.txt` for this task nor for
`16-localisation-ready`. Ran the pytest suite and read every source file
directly rather than trusting either task's prose. Did **not** run a Unity
build, PlayMode test, the Android emulator, or adb — out of scope per the
brief given for this pass. This task and `16-localisation-ready` were
checked together since they cover the same code; this file records only
`12`'s own verdict.

## Verdict by item

| # | Question | Verdict | Evidence |
|---|---|---|---|
| 1 | Does the test's `EXEMPT` set hold up — do the four exempted files still deserve their stated reason? | **3 of 4 hold cleanly; `CatPicker.cs`'s reason is true but incomplete** | `Copy.cs` — the table itself, trivially exempt. `VisionSelfTest.cs` — read in full: dormant unless a `visiontest` folder exists next to the save (`RunIfRequested`, line 30), output goes only to `Debug.Log` and a JSON file; never drawn to any UI. Holds. `SaveFile.cs` — read in full: every string is a `Debug.LogWarning`/`Debug.Log` argument (lines 38, 53, 65); no UI reference anywhere in the file. Holds. `CatPicker.cs` — its stated reason, "failure reasons handed to `Copy.Of("capture.failed")`", is literally true: `game/Assets/View/CaptureScreen.cs:144-146` does exactly that (`_message.text = reason == "cancelled" ? Shell.Copy.Of("capture.cancelled") : Shell.Copy.Of("capture.failed", reason)`). But the `reason` substituted into `"capture.failed"` (`"That did not work: {0}"`) is itself raw, un-tabled English, not merely a key — see item 4 below and this task's sibling `16-localisation-ready/VERIFY.md` for the consequence. For **this** task's narrower claim (everything the player sees is English, not that it's all tabled) the exemption does not cause a violation, since every such reason string is English. |
| 2 | Are `notification.channel` / `notification.channel_description` English and in the required tone? | **Pass** | Declared in `Copy.cs:132-134`; used at `game/Assets/Shell/EveningReminder.cs:221-222` (`Name = Copy.Of("notification.channel")`, `Description = Copy.Of("notification.channel_description")`), a file the test scans (not exempt). `.venv/bin/python -m pytest tools/tests/test_copy_table.py -q` → `21 passed` covers both keys three ways: `test_every_key_the_code_asks_for_exists`, `test_every_declared_key_is_used`, `test_the_copy_is_english`. Tone checked against `cat-shelter-mvp.md` §4, "Soft wording, no guilt": "Evening reminder" / "One quiet message in the evening, on days you have not played." states a fact with no blame, no counting of days missed, no urgency — consistent with the same section's notification example and with D9/`EveningReminder.cs`'s own comment (lines 46-51) restating the no-guilt rule. |
| 3 | `Core/Cat.cs`'s `DefaultName = "Kitty"` bypasses the test (scans `View`/`Shell` only) — defect or defensible, for *this* task's claim? | **Not a violation of 12's OUTCOME today** | 12's OUTCOME is "Zero non-English strings anywhere a player can see them" — `"Kitty"` is English, so even though it sits outside `Copy.cs`, it does not violate this task's specific claim. `grep -rn "Cat.DefaultName\|DefaultName" game/Assets --include=*.cs` outside `Tests/` finds only the definition in `Cat.cs` — no `View`/`Shell` file reads it, because `50-photo/09-meet-your-cat` (`status:todo`, confirmed in its own `labels.txt`) doesn't exist yet. This is a live, correctly-identified risk for **`16-localisation-ready`**'s broader claim ("no literal outside the table") — see that task's `VERIFY.md` for the full analysis and an independent recommendation, since it is about translatability, not English-only compliance. |
| 4 | Player-visible text the test would miss entirely (UXML/USS, string concatenation, `Plugins/iOS/*.swift`) — clean, or found? | **Found: not an English-compliance defect, but recorded because it directly feeds item 1** | Swept `game/Assets/View/DebugGame.uxml` (the project's only `.uxml`) and `DebugGame.uss` (its only `.uss`) — two literal button texts (`"One more shelf"`, `"Continue"`, lines 12-13) sit outside the table and outside the test's `*.cs`-only glob, but `DebugGameView.cs:388-415` (`ShowCard`) unconditionally overwrites or hides both before any card is shown, and all four current call sites (lines 138-140, 350-352, 359-365, 377-384) pass `secondaryText: null`, so the button carrying `"One more shelf"` is always hidden and `"Continue"` is never the string actually rendered. All five `Plugins/iOS/*.swift` files were read; `CatPicker.swift:40,48,85,109` sends raw English strings via `UnitySendMessage` (`"could not read the picked image"`, `"could not save the picked image: \(error.localizedDescription)"`, `"no window to present from"`, `"camera"`) that reach the player through the same `capture.failed` template as item 1 — English, so not a defect against *this* task, but the mechanism is identical to item 1's finding. `CatVision.swift:76`'s `"vision failed: \(error.localizedDescription)"` was traced to its only consumer, `VisionSelfTest.cs` (exempt, dormant) — the production path (`CaptureScreen.Handle`, `game/Assets/View/CaptureScreen.cs:161-169`) never reads `VisionAnswer.error`, only `PhotoJudge`/`PhotoMessages.For(outcome)`, both fully tabled. `CatColour.swift`, `CatHaptics.swift`, `CatPhoto.swift` — no `UnitySendMessage` calls, no player-facing text. `grep -rlP "[а-яА-ЯёЁ]" game/Assets/Plugins/iOS/*.swift game/Assets/View/DebugGame.uxml game/Assets/View/DebugGame.uss` → no matches, confirming no non-Latin text leaked through a channel the pytest suite can't see either. |

**Overall verdict for `12-copy-english`: `verify:passed`.** Every player-visible string found in this pass — across `.cs`, `.uxml`, `.uss`, and `Plugins/iOS/*.swift` — is English. The gaps found in items 1, 3 and 4 are real, but they are translatability/localisation-readiness gaps, which is `16-localisation-ready`'s claim, not this task's. `status:` stays `done`.

## How to reproduce

From a clean checkout, no exported variables:

```sh
git rev-parse HEAD   # 4b8d96521a66af16ee0a3c6f00f058bd6dc4f8a1 at time of this check
.venv/bin/python -m pytest tools/tests/test_copy_table.py -q
# -> 21 passed
grep -rlP "[а-яА-ЯёЁ]" game/Assets/Plugins/iOS/*.swift game/Assets/View/DebugGame.uxml game/Assets/View/DebugGame.uss
# -> no output (no matches, exit 1)
sed -n '1,17p' game/Assets/View/DebugGame.uxml   # the two UXML literals discussed in item 4
sed -n '25,50p' game/Assets/Plugins/iOS/CatPicker.swift   # the raw English reason strings discussed in items 1 and 4
sed -n '138,150p' game/Assets/View/CaptureScreen.cs       # where a CatPicker reason reaches Copy.Of("capture.failed", reason)
```

## What was not checked

- No Unity build, PlayMode test, Android emulator, or adb — out of scope for
  this pass; the notification copy's actual on-screen/in-Settings rendering
  was not visually confirmed on a device or simulator.
- `CatColour.swift`, `CatHaptics.swift`, `CatPhoto.swift` were read for
  `UnitySendMessage`/text-bearing calls only, not audited line-by-line for
  every possible logic issue.
- Files outside `game/Assets/View`, `game/Assets/Shell`, `game/Assets/Plugins/iOS`,
  `game/Assets/Core/Cat.cs` and `Copy.cs`/`test_copy_table.py` were not
  reviewed — e.g. `Core/CatTraits.cs`, `Core/CatSave.cs` were not re-read in
  this pass (they were independently verified in
  `tasks/50-photo/10-skip-default-cat/VERIFY.md`, which this pass cites
  rather than repeats).
- Tone was checked by reading `cat-shelter-mvp.md` §4 and comparing wording
  by eye; no second human or outside reviewer confirmed the tone judgment.
- Whether `error.localizedDescription` in Swift can itself return
  system-language (non-English) text on a device set to a non-English locale
  was reasoned about, not tested on a real device set to another language —
  flagged as a live question in `16-localisation-ready/VERIFY.md`, not
  resolved here.
