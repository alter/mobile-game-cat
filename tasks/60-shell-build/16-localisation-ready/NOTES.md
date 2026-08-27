# Why this is P2 and last, not P0 and now

Raised by the owner on 2026-08-26, while the notification copy was being
written: are the strings written so a translation can be dropped in, or are
they hardcoded?

They are hardcoded. `const string Title` / `const string Body` in
`EveningReminder`, and card titles and bodies inline in `DebugGameView`
("Room clean!", "Shelf jammed", "Would you keep playing if this were the real
game?").

That is deliberate for now and should stay deliberate:

- The MVP ships in English only, to an English-speaking test audience, on about
  a hundred paid installs. A second language before gate 3 would be work spent
  on an audience that does not exist yet.
- The copy is still moving. `12-copy-english` has not run, and half these
  strings will be rewritten by it. Extracting text that is about to change
  twice means doing the extraction three times.
- The count is small — roughly thirty strings — so the cost of extracting later
  is hours, not days.

**What makes it worth writing down rather than forgetting:** the cost of
extraction grows with every new screen, and the photo phase (`50-photo`) adds
the four capture-outcome messages, the meet-your-cat screen and the skip path.
If this is still undone when those land, it stops being an afternoon.

The one string that has a claim to being extracted early is the notification
body: it is what someone sees before ever opening the app, and it is the only
text that reaches a player who has stopped playing.

---

## 2026-08-27 — the VERIFY finding, fixed

`VERIFY.md`, filed the same day, found OUTCOME false in one live path:
`CaptureScreen.cs` was substituting a raw, untabled reason string into the
tabled `"capture.failed"` template on any picker failure other than
cancellation — reasons that came partly from `CatPicker.cs` and partly from
native `CatPicker.swift`, one of which (`error.localizedDescription`) can
itself arrive in the device's system language rather than English. Both
were invisible to `test_copy_table.py`: `CatPicker.cs` was in `EXEMPT`, and
Swift files were outside `UI_DIRS` entirely.

**Fixed.** `CatPicker.swift` now sends fixed lowercase reason codes
(`"read_failed"`, `"save_failed"`, `"no_window"`) instead of sentences, and
no longer forwards `error.localizedDescription` across the boundary at all
(logged locally via `NSLog` for device-console debugging only).
`CatPicker.cs` does the same for its own two English-literal reasons
(`"unsupported"` for both platform-unsupported cases) and no longer builds
a sentence in `OnPickUnavailable`. `CaptureScreen.cs` no longer formats any
reason into a template — every non-cancelled failure now shows
`Copy.Of("photo.our_fault")`, the same honest, already-existing message the
crop-failure path used. `Copy.cs`'s now-unused `"capture.failed"` key was
removed, with a comment explaining why, matching how the retired `D4`
booster strings were handled. `DebugGame.uxml`'s two literal button
defaults (`"One more shelf"` — the exact string `DECISIONS.md` D4 says was
removed from the lose screen — and `"Continue"`) were changed to `text=""`,
matching every other default in that file; both were already unconditionally
overwritten by `DebugGameView.ShowCard` before display, so this closes a
latent gap rather than a live one.

**The Swift change is not compiled.** No Unity/Xcode build was run as part
of this fix — that is for whoever runs the next iOS build to confirm.

**`tools/tests/test_copy_table.py` widened**, so this class of gap can't
recur silently: `CatPicker.cs` was removed from `EXEMPT` (its stated reason
no longer holds — and no longer needs to, since the file now contains no
player-visible sentence literal at all) and a matching `SWIFT_EXEMPT` set
plus a new `test_no_player_visible_english_in_swift` test now scan
`game/Assets/Plugins/**/*.swift` the same way `*.cs` was already scanned;
`test_the_copy_is_english` (the Cyrillic check) now covers Swift too. Full
suite: `.venv/bin/python -m pytest tools/ -q` → 155 passed (the copy-table
file alone: 27, up from 21). `dotnet test build/core-tests/core-tests.csproj`
stays at 152/152 — nothing in `Core/` was touched.

### The `Cat.DefaultName` question — left alone, recommendation on record

Per the task brief for this fix, `Core/Cat.cs`'s `DefaultName = "Kitty"` was
**not** touched. It is not a live violation of this task's OUTCOME today —
no `View`/`Shell` file reads it, because `50-photo/09-meet-your-cat`
(`status:todo`) doesn't exist yet — but it is a real structural gap this
project should decide on before that screen is built, and the reasoning
should survive whoever picks this up next.

`tools/tests/test_copy_table.py` only scans `View`/`Shell`, and that
boundary is the right one: widening it to all of `Core` would flag
unrelated domain strings (error type names, internal identifiers) that are
not player copy, for no real gain. The sharper problem, already identified
in `tasks/50-photo/10-skip-default-cat/VERIFY.md` item 4, is that the
*likely* way `09` gets built is a bare symbol reference —
`nameField.value = Cat.DefaultName` — with no string literal anywhere in
`View`/`Shell` at all. A literal-regex test can never catch that, no matter
which directories it scans; scope is not the axis the fix lives on.

**Recommendation, not implemented (would touch `Core/`, out of scope for
this fix):** one of —

1. A symbol-usage tripwire alongside the existing tests — assert
   `grep -rn "Cat\.DefaultName" game/Assets/View game/Assets/Shell` is
   empty — cheap, and it fails loudly the day `09` is built carelessly; or
2. Make `DefaultName` non-public and expose only `Cat.IsDefaultName(string)`
   (or an `IsDefault` flag on `Cat` itself) from `Core`, so `View`/`Shell`
   structurally cannot read the raw literal at all — the display-time
   choice ("show `Copy.Of("cat.default_name")` instead of the stored name")
   becomes the only thing left to write when `09` is built, rather than an
   easy-to-miss discipline.

Either closes the gap for real; a plan written down with no test or
compiler behind it, as this file already had, is not the same thing as a
closed one.

### The Swift change compiles — 2026-08-27

The fix turned the native picker's failure reasons into codes, and that change
was uncompiled when it was written. Checked twice, both quoted:

```
xcrun swiftc -parse -sdk <iphonesimulator> -target arm64-apple-ios15.0-simulator \
      game/Assets/Plugins/iOS/CatPicker.swift        -> exit 0

BuildScript.BuildIOSSimulatorProject                 -> exit 0
xcodebuild ... -sdk iphonesimulator -arch arm64      -> ** BUILD SUCCEEDED **
```

The second is the one that counts: `swiftc -parse` only checks syntax, while
the full simulator build compiles the plugin as the app's own target with the
Unity headers in scope. The app bundle is at
`game/build/ios-sim/CatShelter/DerivedData/Build/Products/Debug-iphonesimulator/`.

Not checked: that the picker still *works* — the simulator has no camera, and
`50-photo/05-vision-plugin` records that the real Vision plugin fails on the
simulator anyway. This closes "does it compile", not "does it behave".
