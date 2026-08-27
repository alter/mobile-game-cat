# Re-verification, 2026-08-27 — ruling on fixes to the failing VERIFY.md above

**Verifier:** fresh context, wrote none of the fixes. No build/adb/emulator.
Read `CatPicker.swift` directly (not the C# mirror), ran the real pytest
suite, and ran two mutation probes on copies outside the repo.

## Ruling by item

**1 — closed, confirmed in Swift itself.** Every `send("OnPickFailed"/...)`
call site now passes a fixed code (`"read_failed"`, `"save_failed"`,
`"no_window"`, `"camera"`); `error.localizedDescription` goes only to
`NSLog`, never crosses `UnitySendMessage`. `CatPicker.cs`/`CaptureScreen.cs`
map every non-`cancelled` code to `Copy.Of("photo.our_fault")`, no raw
string. `capture.failed` removed from `Copy.cs`. No raw sentence reaches a
player from this path today.

**2 — closed.** `CatPicker.cs` is out of `EXEMPT`; `SWIFT_EXEMPT` is empty;
`test_no_player_visible_english_in_swift` scans `Plugins/**/*.swift`. The
two remaining exemptions still hold, checked by reading, not assumed:
`VisionSelfTest.cs` is genuinely dormant-unless-debug-folder, no player copy;
`SaveFile.cs` is pure file I/O, no strings at all. 28/28 in the copy-table
file, 156/156 overall, run myself.

**3 — mutation confirms both a catch and a real blind spot.** A plain
English sentence dropped into `CatPicker.swift` on a copy outside the repo
is caught immediately. The harder case — split so **no single fragment**
opens with a capital letter (`"Could" + " not read the picked image..."`)
— **passes silently**: the regex needs `"[A-Z]` to open the literal, and a
continuation fragment starting mid-sentence never does. This is a real,
demonstrated gap in the scanner, though nothing in the shipped code
exploits it today.

**4 — re-ruled: honest now, one named exception.** The live violation that
justified "materially overstated" is gone. `DebugGame.uxml`'s dead
`"One more shelf"`/`"Continue"` are also fixed (`text=""`, verified). What
concretely remains: `Core/Cat.DefaultName = "Kitty"` is still un-tabled and
has no `View`/`Shell` caller yet — not a violation today, but the likely
`09-meet-your-cat` implementation (`nameField.value = Cat.DefaultName`) would
be invisible to this mechanism *in principle*, regex or not. `NOTES.md`
names this and proposes two real fixes, neither implemented. Honest framing
for a reader: ready for a language file today; will silently stop being
ready the day `09` ships without one of those two fixes.

## How to reproduce

```sh
sed -n '49,65p;98,105p;119,133p' game/Assets/Plugins/iOS/CatPicker.swift
.venv/bin/python -m pytest tools/tests/test_copy_table.py -q   # 28 passed
sed -n '1,16p' game/Assets/View/DebugGame.uxml   # text="" everywhere
grep -rn "Cat\.DefaultName" game/Assets/View game/Assets/Shell   # empty
```
Mutation (outside the repo): copy `tools/`, `Copy.cs`, `CatPicker.swift`;
mutate `send("OnPickFailed","read_failed")` to a plain sentence → scan fails;
mutate to `"Could" + " not read the picked image, please try again"` → scan
passes despite the concatenated result being a full player-visible sentence.

## What was not checked

Same device-behaviour gap `NOTES.md` already names: the Swift change
compiles (confirmed there via a real simulator build) but was not run on
device. Not re-derived here.

## Verdict

`verify:passed`. The disqualifying live violation is closed and independently
confirmed at the Swift source, not the C# mirror. Item 3's `Cat.DefaultName`
risk and the scanner's concatenation blind spot are both real and both
named — carried forward as documented, non-blocking risks, not reasons to
fail, since neither is a violation of this task's stated OUTCOME as the code
stands today. `status:` moved `in_progress → done`.
