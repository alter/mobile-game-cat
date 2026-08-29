# 2026-08-29 — eight Latin-script languages

`game/Assets/Shell/Copy.Latin.cs`: Spanish, Portuguese, French, German,
Italian, Turkish, Indonesian, Vietnamese. 48 keys each, 384 strings, filled
into `Copy.Tables` through the `AddLatinScript` partial-method hook that
`Copy.cs` declares. No call site moved and `Copy.cs` was not edited — the whole
point of the split, which landed the same day for exactly this reason: two
people cannot translate into one file at once.

Written against the screens, like the Russian pass before it. Every value was
read on the card, button or label it lands on; the widths below are arithmetic
from `DebugGame.uss`, `PanelSettings.asset` and the two screens that build
their labels in C#, not guesses. **Nothing has been seen on a screen.**

## Variants Unity does not let us split

`SystemLanguage` has one `Spanish` and one `Portuguese` — no LatAm/Spain, no
Brazil/Portugal. Both tables are written for the larger mobile audience:
**Latin American Spanish** and **Brazilian Portuguese**. Where the halves
disagree, the wording chosen is at worst slightly foreign in the smaller
market rather than wrong in the larger one. Two cases were live:

- **Spanish is written in "tú", not "vos".** Argentina says "mirá" and "tenés";
  a table in vos is wrong everywhere else, and tú is read without friction in
  Buenos Aires.
- **Portuguese uses "cômodo" for a room**, the Brazilian generic. "Divisão"
  would be the Portuguese one; "quarto" is a bedroom and "sala" a living room,
  and either would be wrong eleven times out of twelve in a house of twelve
  mixed rooms.

## Form of address, one decision per language

English hides the choice; seven of these eight force it. The rule behind all of
them is the same — women 30-55, `cat-shelter-mvp.md` section 2, a game that
does not shout — but **the register that sounds like that is a different one in
each language**, and copying Russian's "вы" into all eight would have been
wrong in at least five.

| Language | Chosen | Why, in one line |
|---|---|---|
| Spanish | **tú** | "Usted" in a game about a kitten reads like a bank; Apple's and Google's own Spanish say tú. And "Su casa" is ambiguous with *his* house, "Tu casa" is not. |
| Portuguese | **você** | In Brazil this is not the informal half of a pair, it is the neutral register. "A senhora" would be addressing an elderly customer. |
| French | **vous** | The one place the Russian reasoning transfers unchanged. French keeps the tu/vous distinction alive, and "tu" from an app to an adult stranger claims what she did not grant. |
| German | **du** | The opposite of Russian, on purpose. German "Sie" is the register of a bank, an insurer and a form; Apple, Spotify, Netflix and IKEA all say "du" to this exact audience. **Most overrulable decision in the pass** — nine strings and a find-replace. |
| Italian | **tu** | "Lei" belongs to a counter and a doctor's office. Same reasoning as Spanish. |
| Turkish | **siz in sentences, bare imperative on buttons** | Turkish holds the stranger line more firmly than Spanish or Italian, so sentences take siz ("gösterin", "bir dakikanız"). But Turkish UI convention writes a button as a bare stem — "Paylaş", "Geri", "Devam" — which is read as a label, not as "sen"; "Paylaşın" on a 200px button would be both longer and stranger. |
| Indonesian | **Anda** | "Kamu" is what a friend or a younger person is called — the same claim "ты" makes. Indonesian has no colder register above "Anda" for it to be mistaken for. |
| Vietnamese | **bạn** | See below. |

### Vietnamese, which has no neutral "you"

Every Vietnamese second-person word encodes a relationship: **chị** is an older
sister, **cô** an aunt or teacher, **em** someone younger, **bà** an old woman,
**quý khách** a valued customer. Picking one means the game asserts the
player's age, her sex, and how she stands to whoever is speaking — from a
screen that knows none of the three.

**Chosen: "bạn"** — literally "friend", the word Vietnamese interfaces settled
on precisely because it commits to nothing. It is not the warmest option. For
this audience **"chị" would read better**, and it is what a shop assistant
would say to a woman of 45. It was rejected because it is *wrong when it is
wrong*: it calls a man a woman and a 25-year-old older than she is, and the
game cannot find out which. A flat-but-correct pronoun beats a warm-but-
mistaken one on a screen this quiet.

Vietnamese drops pronouns more freely than Russian, and the table takes that
option nearly everywhere: **"bạn" appears in four strings out of forty-eight.**
Where the game speaks of itself it says "mình", not the plural "chúng tôi",
which is a company writing to a customer.

## The kitten stays "she" in five of eight

`12-copy-english` change 7 made the kitten "she" everywhere on purpose, and the
Russian table had to give it up ("котёнок" is masculine and "кошечка" is
baby-talk). Most of these languages can keep it:

- **Spanish "la gatita", Portuguese "a gatinha", Italian "la gattina"** — the
  ordinary word for a young female cat in each, no baby-talk problem.
- **French: "la petite chatte"** where she is the subject, plain "chaton"
  otherwise. "Chatonne" exists and reads as a curiosity. This one is
  load-bearing: French cannot drop a subject pronoun, so "Le chaton … il
  attend" in `notification.body` would say *he*.
- **German: "die kleine Katze", not "das Kätzchen"**, wherever she is the
  subject. "Kätzchen" is neuter, so "es wartet" says *it* — the Russian trap
  from the other side. "Kätzchen" stays where the sentence does not need her sex.
- **Turkish, Indonesian, Vietnamese have no grammatical gender at all.** "Yavru
  kedi", "anak kucing", "mèo con" and their pronouns are sexless, so the
  English survives without a decision being made, and none is made.

`card.game_name` is **"Sootpaw"** in all eight, matching Russian: an app name is
not copy, and a caption naming something no store search finds sends nobody
anywhere. It is the only proper noun in the table — the game names no character
(the player names the kitten) and no place — so there was nothing else to match.

## Widths: what had to be shortened, and by how much

Three call sites have no room to spare. Read from the source, not guessed.

**`capture.title` cannot wrap.** `CaptureScreen.cs:86-89` builds it at
`fontSize 26` and never sets `whiteSpace = Normal`; the panel is 390 units
(`PanelSettings.asset`, `m_Match: 1`, so 390 is the *narrow* end) less 48 of
padding. The Russian pass measured the ceiling at about 24 characters. All
eight are inside it, and three were cut to get there:

| | Literal | Chars | Shipped | Chars |
|---|---|---|---|---|
| Portuguese | "Mostre para nós a sua gata" | 26 | **"Mostre sua gata"** | 15 |
| French | "Montrez-nous votre chatte" | 25 | **"Montrez votre chatte"** | 20 |
| Vietnamese | "Cho chúng mình xem mèo của bạn" | 30 | **"Cho xem mèo của bạn"** | 19 |

All three drop the "us", which is the same cut Russian made with "нам" —
it adds nothing any of these sentences needs. German is 20 ("Zeig uns deine
Katze"); "Zeigen Sie uns Ihre Katze" is 25 and clips, which did not decide the
register but agrees with it.

**`.game__card-body` is 240px at 15px** (`DebugGame.uss:419-426`), and on the
lose and ending cards it wraps in a narrow column under two photographs.
English `lose.body` is 76 characters. Two rewrites:

- **German `lose.body`: 104 → 82 characters, 22 cut.** The faithful sentence is
  "Alle Plätze sind belegt und es sind keine drei gleichen dabei. Der Haufen
  liegt wieder so, wie er vorher war." — five wrapped lines. "Es sind … dabei"
  and the relative clause both go and neither carried meaning.
- **Indonesian `house.complete.body`: about 24 characters cut** from the clause
  "yang tidak lagi punya tempat untuk menyembunyikan barang-barang temuannya",
  which is 103 on its own. "Temuannya" is one word for the four the literal
  needs.

Two more, smaller:

- **German `map.legend`, third clause: 20 characters cut.** "Dunkle Zimmer sind
  noch verschlossen" repeats "Zimmer" a second time in one line; "dunkle sind
  noch zu" is what a person says.
- **Italian `house.complete.share`: "Farlo vedere a qualcuno" (23) → "Mostrare
  a qualcuno" (19).** The first is the more idiomatic phrase and was dropped
  for the width alone.

Everything else came in at or under the English. Turkish `lose.body` is 69 and
Vietnamese `lose.body` is 67, both *shorter* than English — Vietnamese
diacritics look long and cost no width.

**`house.complete.share` sits beside the 44px heart** in a row inside 342
available units (`DebugGameView.BuildEndingExtras`, `Buttons.LabelSize` 17).
Shortest is Turkish at 13 ("Birine göster"), longest **Indonesian at 22
("Tunjukkan ke seseorang") — about 303 units with the glyph and the padding.**
It fits on arithmetic and on arithmetic only; the Russian at 20 is the only
comparable number anyone has, and neither has been seen.

**`win.before` / `win.after`** sit under 116px panes at 12px bold
(`.game__ba-label`). All eight pairs are 3-7 characters. German took the
idiomatic "Vorher"/"Nachher", which is shorter than the English it replaces.

## What could not be translated faithfully

1. **Vietnamese address**, above. The best word for this audience is not the
   word the game can safely use.
2. **German `notification.title` is 51 characters against the English 43** —
   the longest lock-screen title of the eight, and it is already the short
   version: "Deine" was dropped for length (as Russian dropped "Ваш" and French
   "Votre"), and "Kätzchen" would save six more but would force "es wartet" in
   the body, which says *it*. French is 58 for the same reason. **Neither has
   been seen truncated on a real lock screen** — that is the first thing to
   look at on the next build.
3. **`map.room_failed` / `map.map_failed` still interpolate `{0}`**, which is
   the system's own reason and arrives in the *system* language, not the
   table's. Unchanged from English and Russian, and named in `NOTES.md` as a
   thing to fix at the call site rather than in a table.
4. **Turkish cannot suffix "Sootpaw".** Turkish would normally attach a case
   ending to the game's name ("Sootpaw'da"), which needs vowel harmony guessed
   from the last syllable of an invented English word. `card.caption` and
   `house.complete.caption` route the suffix through the noun after it instead
   — "{0} oyunundaki", "{0} oyununda" — which is correct and slightly more
   formal than a Turkish speaker would write by hand.
5. **The tone is asserted, not tested.** Every one of these 384 strings was
   written to `cat-shelter-mvp.md` section 2 — no exclamation marks, no
   congratulation, no urgency, no diminutive that reads as baby-talk — but
   nobody who speaks these languages natively has read them. That is the honest
   status, and it is the same status the Russian table has had since yesterday.

## The test

`tools/tests/test_copy_table.py` read `Copy.cs` alone. It now reads **`Copy.cs`
plus every `Copy.*.cs` beside it**, globbed rather than listed: a companion
added and not named would be a whole script's worth of translation no check
here ever looked at, and the glob makes that impossible. `Copy.cs` stays first
because it holds the reference language.

Four things changed, and two checks are new:

- `parsed_tables()` slices tables **per file**, so a companion's last table
  stops at its own closing brace rather than swallowing the next file.
- Every copy source is exempt from `test_no_player_visible_english_outside_the_table`,
  from the same glob that parses it — otherwise 384 translated sentences read
  as loose English literals.
- `test_every_table_is_selectable_by_a_device_language` reads across all the
  files: a companion's tables are selected from that companion's own hook, and
  `Copy.cs` never names them.
- **New: `test_every_table_is_selected_by_its_own_language`.** The mistake a
  copy-paste of eight hook lines actually makes is
  `tables[SystemLanguage.Spanish] = Portuguese;` — which passes every other
  check in the file (both tables exist, both are reachable, both have the right
  keys) and ships Portuguese to Spain while Brazil gets English.
- **New: `test_each_copy_file_holds_the_script_it_is_named_for`.** A value
  pasted into the wrong file passes everything else — the key exists, the
  placeholders match, it is not English — and the split stops meaning anything
  the first time it happens silently. Checked in one direction only: "no
  Cyrillic, Greek, CJK, Arabic, Hebrew, Devanagari, Thai or Hangul in
  `Copy.Latin.cs`" is statable; "what belongs in `Copy.Scripts.cs`" is not.
  Blacklist and not whitelist, because Vietnamese alone needs most of Latin
  Extended Additional and a permitted-characters list would fail on the first
  correct string nobody anticipated.

Everything the file checked before, it still checks, now over seventeen tables
instead of two: key parity against English both ways, placeholders counted per
index, `{n}` balanced 0..n, no value left in English, and the `EveningReminder`
no-argument trap.

```
.venv/bin/python -m pytest tools/tests/test_copy_table.py -q  -> 45 passed, 2 failed
.venv/bin/python -m pytest tools/tests -q                     -> 220 passed, 2 failed
```

**Both failures are another worker's untracked `game/Assets/View/GlyphCheckView.cs`**
(`??` in `git status`, written while this pass was running); it holds literal
English and literal Russian for a font-glyph harness. Proved not to be this
change: the committed version of the test, run against this same tree, fails on
the same two —

```
git show HEAD:tools/tests/test_copy_table.py > …_BASELINE.py
.venv/bin/python -m pytest …_BASELINE.py -q
  -> 44 passed, 3 failed
     FAILED …[GlyphCheckView.cs]          (the same one)
     FAILED …test_no_cyrillic_outside_the_copy_table   (the same one)
     FAILED …[Copy.Latin.cs]              (fixed by this change)
```

## It compiles, and it builds at runtime

No Unity and no simulator — out of bounds. `Copy.cs`, `Copy.Latin.cs` and the
companion `Copy.Scripts.cs` were compiled together against a `UnityEngine` stub
(`SystemLanguage`, `Application.systemLanguage`) at the same `net8.0` /
`LangVersion 9` / `Nullable enable` the project's own `core-tests.csproj` uses:

```
dotnet build copycheck.csproj -v m --nologo
  -> Сборка успешно завершена.   Предупреждений: 2   Ошибок: 0
```

Both warnings are `CS8618` on `Copy.cs:23` and `Copy.cs:25` (`_current`,
`_tables` — nullable fields), they are the stub project's `Nullable enable` and
not Unity's setting, and neither is in this pass's file.

Then run, which the previous pass did not do, because a duplicate key inside a
table is a runtime `ArgumentException` and not a compile error:

```
dotnet run --project copycheck.csproj
  -> 17 tables, 48 keys each, no exception; card.caption formatted with
     card.game_name printed correctly in all seventeen — the {0} survives in
     whatever position each grammar puts it, including Turkish's leading one
     ("Sootpaw oyunundaki yavru kedime bakın").
```

That says the C# is valid and the tables build. It says nothing about how any
of it looks.

## Unverified, named rather than glossed

- **No string has been seen on a screen.** Every width above is arithmetic.
  Worth a screenshot on the next build, in order: German `lose.body` on the
  card, Indonesian `house.complete.share` beside the heart, German and French
  `notification.title` on a real lock screen, and all eight `capture.title`.
- **The font.** `NOTES.md` already flags that the project ships no font asset
  and every label falls through to Unity's built-in default. That risk is
  larger here than it was for Russian: Turkish needs ı/ğ/ş, Vietnamese needs
  stacked tone marks on Latin Extended Additional, and if the default font
  lacks them the symptom is boxes, not a bad translation — and the fix is a
  font asset, not a copy change. Another worker's untracked `GlyphCheckView.cs`
  appears to be aimed at exactly this.
- **No native reader.** See "what could not be translated faithfully", item 5.
