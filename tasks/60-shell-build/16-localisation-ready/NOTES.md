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

### The concatenation blind spot — found, then fixed after the `verify:passed` above

**This landed after the re-verification that passed the task, so that
verdict did not cover it.** The re-verification's own mutation testing found
that `test_copy_table.py`'s scanner requires a literal to open with
`"[A-Z]`, so `"Could" + " not read the picked image, please try again"`
passes silently — no fragment alone looks like a sentence. Nothing shipped
used this shape; it was a property of the checker, not a live leak.

**Fixed.** `_sentence_literals` now also joins chains of two or more plain
`"..." + "..."` literals and tests the *joined* text against the same
`SENTENCE` bar as a single literal always had to clear — not a looser bar,
the same one, applied to one more shape.

Deliberately excludes `$"..."` interpolated strings (C#): most diagnostic
`Debug.Log`/`NSLog` lines in this codebase (`VisionSelfTest.cs`,
`EveningReminder.cs`, `CoatBuilder.cs`) are built that way, and scanning
them would flag routine log assembly as if it were copy — the kind of false
alarm that gets a check switched off, which this project has now watched
happen conceptually twice today.

Proved both ways, on copies outside the repo:

```
baseline (real CatPicker.swift):      1 passed, 9 deselected
mutated ("Could" + " not read..."):   FAILED — CatPicker.swift: this looks
  like prose crossing the native boundary -> ['"Could" + " not read the
  picked image, please try again"']
```

And the real tree, unaffected — no genuine `"lit" + "lit"` concatenation
exists anywhere in the scanned files today, confirmed by grep before
writing the fix, not assumed:

```
.venv/bin/python -m pytest tools/tests/test_copy_table.py -q   -> 28 passed
.venv/bin/python -m pytest tools/ -q                           -> 156 passed
dotnet test build/core-tests/core-tests.csproj -v q --nologo    -> 169 passed
```

**The same shape, checked elsewhere, in one sentence each:**

- `test_analytics_call_sites.py` does not have it and could not — it matches
  `Analytics\.Helper\s*\(` against call-site *syntax*, not a string literal,
  and a method call cannot be split across a `+` the way a string can.
- `test_no_secrets.py` does have it, and it plausibly matters more there:
  its `SOURCE_LITERAL` pattern stops at the first closing quote, so
  `gameKey = "abcdefghij" + "klmnopqrst"` would only ever see `"abcdefghij"`
  (10 characters, under `MIN_KEY_LENGTH`) and never the 20-character whole —
  a credential split in two evades it today. Not fixed here (out of this
  task's touch-scope); reported as a fourth blind spot alongside the three
  `test_no_secrets.py` already names in its own docstring.

**The honest limit, named rather than chased further.** This closes plain
`+`-concatenation, the shape actually demonstrated. It does not, and by
construction cannot, close string interpolation (`$"...{x}..."` in C#,
`"...\(x)..."` in Swift) built to read as a sentence, or a sentence
assembled across separate statements/variables. Closing those is not a
regex-over-source problem anymore — it needs either a linter with real
syntax understanding or an architectural rule that all player-visible text
is built by exactly one call (`Copy.Of`), so there is only ever one place to
check. Naming that boundary is the stopping point for this pass, not a gap
to keep patching with more regex.

---

## 2026-08-28 — the second language arrives, and it is Russian

The file above spent 2026-08-26 arguing that a second language was work for
an audience that did not exist. That was true of the *store* audience and
false of the only audience the game actually has today: the owner and his
first players read Russian, and until this change a Russian player finished
the house and posted **"Look at the kitten I have in Sootpaw"** to a
Russian-speaking feed.

`Copy.cs` was already shaped for this — that is what the task built — so the
change is one table and four lines of selection, and no call site moved.

### How a language is chosen

`Copy.Current` still lazy-initialises, for the reason recorded above it (a
static field assigned from a table declared below it reads null and took the
game down on launch once). It now initialises from
`Copy.For(Application.systemLanguage)`, which looks the device's language up
in a `Tables` map and returns `English` for anything not in it.

`Tables` is itself a lazy property and not a static field, for exactly the
same reason: a field built from `English` and `Russian` would read whichever
of them is declared after it as null, which is the same crash wearing a
different hat.

**A third language is: one `["..."]` block, one line in `Tables`.** Nothing
else. `test_every_table_is_selectable_by_a_device_language` fails if the
second of those is forgotten, and the key-parity test fails if the first is
incomplete — so the two halves cannot drift apart silently.

Why English is the fallback and not a nearest-match rule: there is no
nearest match to compute with two tables, and a rule that hands Ukrainian to
a Polish phone because the letters look similar is a guess the game cannot
check. `Copy.Current` is settable, so a language switch inside the game
later is a setter call and not a redesign.

### Three decisions that hold across the whole Russian table

These are in the table's own doc-comment too; the argument is here.

**"вы", not "ты".** The audience is women 30–55 (`cat-shelter-mvp.md`
section 2). "Ты" from an app to a woman of 45 is a familiarity she did not
grant; "вы" is the neutral register and also the quieter one, which is the
tone rule. Most strings address nobody at all — Russian lets a button be an
infinitive ("Сфотографировать") and a status be a plain verb ("Открываем…")
— and that option is taken wherever it reads naturally, so the choice only
actually shows in six strings. **This is the one decision in the pass the
owner may simply overrule on taste; it is six values and a find-replace.**

**"котёнок", and no pronoun for her.** `12-copy-english` change 7 made the
kitten "she" everywhere on purpose, and Russian has no way to keep that.
"Котёнок" is grammatically masculine, so "she is waiting" becomes "он ждёт"
— which says *he*. The feminine words are "кошечка", which a Russian ear
hears as baby-talk (section 2 rules that out with the pink and the glitter),
and "кошка", which is a grown cat and not this animal. Russian drops subject
pronouns freely, so the two places the English says "she" of the game's
kitten drop the pronoun instead of picking a sex:
`notification.body` is "Ждёт, чтобы показать. Когда у вас будет минутка." —
the subject carries over from the title above it on the lock screen.

**"она" *is* used for the player's own cat** (`capture.*`, `photo.*`). That
is a real animal with a real sex, "кошка" is the word a Russian cat owner
uses, and the English says "she" there for the same reason.

### The strings where Russian could not simply be longer

Russian runs about a fifth longer than English for the same sentence, and
three call sites have no room for that. Read against the code, not guessed:

- **`capture.title` cannot wrap.** `CaptureScreen.cs:86-89` builds it at
  `fontSize 26` and never sets `whiteSpace = Normal`; the panel is 390 units
  wide (`Shell/PanelSettings.asset`, and `m_Match: 1` means 390 is the
  *narrow* end — a shorter phone gets more units, not fewer) less 48 of
  padding. "Покажите свою кошку" is 19 characters. "Покажите нам свою
  кошку" is 23 and was dropped for that alone; "нам" adds nothing a Russian
  sentence needs. `meet.title` has the same shape
  (`MeetYourCatScreen.cs:78-81`) and "Вот она" has room to spare.
- **`.game__card-body` is 240px at 15px** (`DebugGame.uss:419-426`), so the
  lose and ending cards wrap inside a narrow column with two photographs
  above them. `lose.body` is 73 characters against the English 76 and wraps
  the same. `house.complete.body` is split into two sentences where the
  English has one clause and a comma: the relative clause Russian would
  otherwise need — "котёнок, которому больше некуда прятать находки" — costs
  a line the card does not have, and `12-copy-english` change 4 had already
  spent a pass buying two lines back on that exact card.
- **`house.complete.share`** sits in a row beside the 44px heart
  (`DebugGameView.BuildEndingExtras`). "Показать кому-нибудь" is 20
  characters at `Buttons.LabelSize` 17 — about 286 units with the glyph, the
  padding and the heart, inside 342 available. It fits on arithmetic. **It
  has not been seen on a screen** — see the unverified list below.

`win.before` / `win.after` became **"Было" / "Стало"**, which is how a
Russian before-and-after is captioned and is shorter than the English under
a 116px pane. `card.game_name` stays **"Sootpaw"** in both tables: it is the
name the game is listed under, an app name is not copy, and a caption naming
something no store search finds sends nobody anywhere. That is the one key
`test_no_value_was_left_untranslated` allows to be identical.

### What the test now checks

`tools/tests/test_copy_table.py` read `Copy.cs` as one flat table. It now
parses it as a set of them — sliced on the `public static readonly
IReadOnlyDictionary<string, string> <Name> =` declaration line, with an
entry regex that joins `"..." + "..."` values across lines, since three keys
are written that way. Everything it checked before, it still checks; five
checks are new, all of them failures that only a second language makes
possible.

| Check | What it catches |
|---|---|
| `test_every_language_has_exactly_the_reference_keys` | a key in one table and not another, **both directions** — a missing key renders `[win.next]` on that player's button, an extra one is translation work nothing can reach |
| `test_placeholders_are_identical_in_every_language` | a `{n}` count that differs from English, **counted per index** so `"{0} in {0}"` and `"{0} in {1}"` are not treated as equal |
| `test_keys_read_without_arguments_have_no_placeholders` | the `EveningReminder` trap, in every language at once — see below |
| `test_no_value_was_left_untranslated` | a value copied from English to make the parity check green, with `card.game_name` the one allowed exception |
| `test_every_table_is_selectable_by_a_device_language` | a table no `SystemLanguage` maps to — a language that ships and never reaches anybody, while every other check passes |

Two existing checks changed rather than being added to.
`test_format_placeholders_are_balanced` now runs over every table, not the
file as one blob: English being 0..n says nothing about a translation that
renumbered them. And `test_the_copy_is_english` — which asserted *no
Cyrillic anywhere, `Copy.cs` included* — is split. The half that was
actually load-bearing is kept unchanged as
`test_no_cyrillic_outside_the_copy_table`: a Cyrillic string in a View file
is a string no other language can ever override. The other half becomes
`test_the_english_table_is_english`, because a Russian value pasted into the
English table is invisible to every other check here — the key exists, the
placeholders match, it is used — and shows Cyrillic to an English player.

**Proved by mutation, on copies outside the repo, not asserted.** Nine
mutations, each reverted before the next:

```
baseline                                            44 passed
1. Russian loses "win.next"                    FAILED test_every_language_has_exactly_the_reference_keys
2. Russian gains "win.extra"                   FAILED test_every_language_has_exactly_the_reference_keys
3. Russian card.caption drops {0}              FAILED test_placeholders_are_identical_in_every_language
4. Russian notification.title gains {0}        FAILED test_placeholders_… + test_keys_read_without_arguments_…
5. Russian lose.replay left as "Replay"        FAILED test_no_value_was_left_untranslated
6. [SystemLanguage.Russian] removed            FAILED test_every_table_is_selectable_by_a_device_language
7. Cyrillic in the English table               FAILED test_the_english_table_is_english (+ untranslated)
8. Russian caption renumbered {0}->{1}         FAILED test_placeholders_… + test_format_placeholders_are_balanced
9. {0} added to notification.title in BOTH     FAILED test_keys_read_without_arguments_have_no_placeholders
10. the whole Russian table reverted           FAILED test_every_language_… + test_every_table_is_selectable_…
```

Mutation 9 is the one worth keeping: with both languages changed together
the parity check is satisfied and the trap is still caught, which is the
whole reason that check is written against the *call site* and not against
English.

```
.venv/bin/python -m pytest tools/tests/test_copy_table.py -q   -> 44 passed
                                    (HEAD's test against HEAD's Copy.cs: 38)
.venv/bin/python -m pytest tools/ -q                           -> 218 passed, 1 failed
```

The one failure is `test_sound.py::test_match_is_three_events_and_the_others_are_one`.
It is untracked work in progress by another worker (`tools/sound/` and
`tools/tests/test_sound.py` are both `??` in `git status`), it reads `.wav`
files and never opens `Copy.cs`, and nothing in this pass touches it.

### The `EveningReminder` trap — closed, in code

`EveningReminder.cs:52` is `Copy.Of("notification.title")`, the overload that
takes no arguments and calls no `string.Format`. A `{0}` in that value — put
there in English or in a translation, by someone reasonably wanting the
kitten's typed name in it, which is what `12-copy-english` recommends and
`cat-shelter-mvp.md` section 4's own worked example ("Murzik found something
behind the couch") assumes — is delivered to a lock screen as the four
characters `{0}`.

`12-copy-english/NOTES.md` recorded this as a warning to a future reader. It
is now a failing test instead: `test_keys_read_without_arguments_have_no_placeholders`
finds every `Copy.Of("key")` with nothing after the key, and asserts that
key holds no `{` in any table. The key and its call site still have to change
in one commit — the test simply refuses the half of that change that can be
made from this file alone.

### `HouseMapView.cs` — six English literals, still outside the table

**The map is the game's first screen, so a Russian player meets English
there before anything else.** All six are hard-coded literals; the file is
out of bounds for this pass, so they are listed for whoever holds it. Line
numbers re-checked against the file as it stands today (they have not moved
since `12-copy-english` listed them):

| Line | Literal | Who reads it |
|---|---|---|
| `HouseMapView.cs:78` | `"house map: 12 rooms"` | **every player, first screen** |
| `HouseMapView.cs:87` | `"no levels loaded — nothing to map"` | a broken install |
| `HouseMapView.cs:180-182` | `"tap the lit number to play it   ·   ticked rooms are done   " + "·   dim rooms are still locked"` | **every player, first screen** |
| `HouseMapView.cs:787-788` | `"the board's layout is missing — " + "DebugGame.uxml is not assigned to the UIDocument"` | a broken build |
| `HouseMapView.cs:812` | `$"could not open the room: {e.Message}"` | a crash on entering a room |
| `HouseMapView.cs:975` | `$"could not open the map: {e.Message}"` | a crash on leaving one |

Three things to carry into that change rather than discover during it:

1. **The key and the call site must land in one commit.**
   `test_every_declared_key_is_used` fails on a key nothing references, so
   the `map.*` keys cannot be added here in advance. Same constraint that
   left `map.returning` unwritten — `ShowOpening` is called with `null` on
   the way back from the board (`HouseMapView.cs:657` passes
   `Copy.Of("map.opening")` on the way *in*), and the suggested value when
   somebody holds both files is still **"Back to the house"** /
   **"Обратно в дом"**.
2. **The last two are not a `{0}` job.** `e.Message` is a .NET exception
   message, and on a localised device the framework hands it back in the
   *system* language. Substituting it into a tabled template is precisely
   the leak this task's own VERIFY found and fixed at the Swift boundary in
   August — the fix there was to send a code and map it to a key on the C#
   side, and the same shape applies here. Log `e` and show a tabled
   sentence; do not format the exception into the copy.
3. **The green test is not evidence about this file.** All six pass
   `test_no_player_visible_english_outside_the_table` only because
   `SENTENCE` requires a capitalised first word and every one of these is
   lowercase. That hole is still open and is not this pass's to close.

The copy proposal from `12-copy-english` — a title that names the place
rather than the screen, and a legend cut to the one actionable instruction —
stands, and is a product call. If it is taken, the Russian is
**"Здесь живёт котёнок"** over the house and **"Нажмите на светлую
комнату"** for the legend; both are written here so the translation is not
invented in a hurry inside a C# file.

### Unverified, and named rather than glossed

- **Nothing was run.** No Unity, no simulator, no device — those were out of
  bounds. `Copy.cs` compiles: it was built with `dotnet build` against a
  four-line `UnityEngine` stub (`SystemLanguage`, `Application.systemLanguage`)
  at the same `LangVersion 9` the project's own `core-tests.csproj` uses —
  0 warnings, 0 errors. That says the C# is valid and says nothing about how
  any of it looks.
- **No Russian string has been seen on a screen.** Every width claim above
  is arithmetic from the USS and the panel's reference resolution. The three
  worth a screenshot on the next build, in order:
  `capture.title` (cannot wrap), `house.complete.share` beside the heart,
  and `lose.body` on the card.
- **Whether the font has Cyrillic at all — the one risk that could make this
  whole change invisible.** The project ships no font: there is no `.ttf` or
  `.otf` under `game/Assets`, no `-unity-font` in any USS, and
  `Shell/PanelSettings.asset` has `textSettings: {fileID: 0}`, so every
  label falls through to Unity's built-in default font asset. Whether that
  asset resolves Cyrillic glyphs on a *player's* device — as opposed to in
  the editor, where the editor font is available — is not answerable from
  the source tree, and it is the first thing to look at in the next build.
  If the answer is no, the symptom is not a wrong translation but empty
  boxes, and the fix is a font asset with Cyrillic coverage, not a copy
  change.
- **"Sootpaw" in a Russian store listing.** The table keeps it in Latin in
  both languages on the argument that an app's name is not copy. If the game
  is to be listed in the Russian store under a Russian name, that is a
  product decision this pass had no way to make, and `card.game_name` is the
  single key to change when it is made.
