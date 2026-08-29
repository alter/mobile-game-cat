# Seventeen languages on Android — what was looked at and what is broken

**Device:** Android emulator `emulator-5554`, `sdk_gphone64_arm64`, Android 15,
1080x2340, density 420. Panel `389.5385 x 843.9999` units, scale 2.7726.
**Build:** `game/build/android/CatShelter.apk`, already installed, **not rebuilt**.
**Date:** 2026-08-29. **Language switch:** `lang.txt` beside the save, per launch.

170 language x screen pairs were reached and photographed: 17 languages x 10
screens. Eight screens are the ones asked for; two more were added because the
first eight cannot show them (see *Two extra screens*). Every screenshot is a
real device capture, not a mock-up; 187 audit files were pulled.

Montages are in `shots/`, all named `android-*`. Six close-up sheets carry the
defects: `android-defect-1..5-*.png` and `android-method-check-squeeze.png`.

---

## 1. The table

`pass` = reached, photographed, audit ran and found nothing, nothing wrong to
the eye. Letters are the defects below.

| language | 1 map | 2 board | 3 cat card | 4 room clean | 5 shelf jammed | 6 every room | 7 capture | 8 meet | 9 placeholder | 10 dog |
|---|---|---|---|---|---|---|---|---|---|---|
| English | pass | pass | pass | pass | pass | pass | pass* | D4 | pass* | pass* |
| Russian | pass | pass | pass | pass | pass | **D2** | pass* | D4 | pass* | pass* |
| Spanish | pass | pass | pass | **D3** | pass | D2 | pass* | D4 | pass* | pass* |
| Portuguese | pass | pass | pass | pass | pass | D2 | pass* | D4 | pass* | pass* |
| French | pass | pass | pass | pass | pass | **D2** | pass* | D4 | pass* | pass* |
| German | pass | pass | pass | D3 | pass | **D2** | pass* | D4 | pass* | pass* |
| Italian | pass | pass | pass | pass | pass | D2 | pass* | D4 | pass* | pass* |
| Turkish | pass | pass | pass | pass | pass | pass | pass* | D4 | pass* | pass* |
| Indonesian | pass | pass | pass | D3 | pass | **D2** | pass* | D4 | pass* | pass* |
| Vietnamese | pass | pass | pass | pass | pass | pass | pass* | D4 | pass* | pass* |
| ChineseSimplified | pass | pass | pass | pass | **D1** | pass | pass* | D4 | pass* | pass* |
| ChineseTraditional | pass | pass | pass | pass | **D1** | pass | pass* | D4 | pass* | pass* |
| Japanese | pass | pass | pass | D3 | pass | pass | pass* | D4 | pass* | pass* |
| Korean | pass | pass | pass | pass | pass | pass | pass* | D4 | pass* | pass* |
| Thai | pass | pass | pass | pass | pass | pass | pass* | D4 | pass* | pass* |
| Arabic | pass | pass | pass | pass | pass | pass | pass* | D4 | **D5** | pass* |
| Hindi | pass | pass | pass | pass | pass | pass | pass* | D4 | pass* | pass* |

`*` on screens 7, 9 and 10 — the text is right, but the **buttons and the name
field on those two screens are Unity's default grey rectangles in all seventeen
languages**. Not a translation fault; see *D6*.

Bold = worst instance of that defect.

---

## 2. The defects

### D1 — a line that begins with a full stop. ChineseSimplified, ChineseTraditional, "Shelf jammed"

`shots/android-defect-1-cjk-orphan-stop.png`, also
`shots/android-5-lose-2.png`.

The card body wraps so that the ideographic full stop `。` is pushed to the
**start** of the second line:

```
格子都占满了，也没有三个一样的
。这堆会恢复原样。            (Simplified)

格子都佔滿了，也沒有三個一樣的
。這堆會恢復原樣。            (Traditional)
```

This is the missing kinsoku rule. `Copy.Scripts.cs:36-43` predicted it in
writing ("The symptom is an orphaned full stop at the left edge of a wrapped
card body… It is a PanelSettings fix rather than a copy one"). This run is the
first time it has been seen on a screen. Only the lose card shows it; the win
and ending card bodies in both Chinese tables happen to break elsewhere.

The layout audit cannot see this — every label still fits its box.

### D2 — the ending card has no maximum width. Indonesian, Russian, French, German, Portuguese, Italian, Spanish

`shots/android-defect-2-ending-card-width.png`, also
`shots/android-6-ending-1.png` and `-2.png`.

`house.complete.title` does not wrap, so the card grows to fit it. Measured from
the screenshots (cream card edge to cream card edge, screen is 1080px):

| language | card width | margin each side |
|---|---|---|
| English | 830 | 125 |
| Turkish | 821 | 129 |
| ChineseSimplified / Traditional | 791 | 144 |
| Japanese | 809 | 135 |
| Korean | 884 | 98 |
| Arabic | 886 | 97 |
| Spanish | 951 | 64 |
| Italian | 968 | 56 |
| Portuguese | 972 | 54 |
| German | 1031 | 24 |
| French | 1041 | 19 |
| Russian | 1044 | 18 |
| **Indonesian** | **1062** | **9** |

Nothing is clipped and nothing is off screen. But at 9px of margin the card has
stopped being a card — it is a full-bleed band with rounded corners touching the
edge of the phone, against 125px in English. A title one word longer, or a
narrower phone, and it goes off the edge.

### D3 — the win card does the same. Spanish, Japanese, German, Indonesian

`shots/android-defect-3-win-card-width.png`, also `shots/android-4-win-1.png`.

`win.room_clean.title`, same mechanism:

| language | card width | margin |
|---|---|---|
| English, French, Italian, Vietnamese, Thai, Arabic, CJK-T/S | 798–802 | 139–141 |
| Portuguese | 849 | 115 |
| Indonesian | 882 | 99 |
| German | 891 | 94 |
| Japanese | 939 | 70 |
| **Spanish** | **975** | **52** |

"La habitación está limpia" pushes the card to 90% of the screen width.

### D4 — the cat's name is the English default, in all seventeen languages

`shots/android-defect-4-kitty.png`, also `shots/android-8-meet-*.png`.

Meet-your-cat pre-fills the name field with **`Kitty`** on every one of the
seventeen. The value comes from the save (`cat.save` on the device holds
`name Kitty`), which was written by the skip path from
`Core/Cat.cs:27` `public const string DefaultName = "Kitty";` — a string that is
not in any copy table and cannot be translated.

This is exactly the gap `NOTES.md` wrote down on 2026-08-27 under "The
`Cat.DefaultName` question — left alone, recommendation on record", and the one
`tasks/50-photo/10-skip-default-cat/VERIFY.md` item 4 predicted no regex test
could catch. It is now live, on a screen, in seventeen languages.

The placeholder itself (`meet.name_placeholder`) *is* translated correctly
everywhere — see screen 9. The defect is only the stored default.

### D5 — Arabic name-field placeholder. Arabic, meet-your-cat

`shots/android-defect-5-arabic-placeholder.png`.

Two things, one of them found by the audit:

```
[Layout] taller than its box: "ما اسمها؟" — by 2pt — 38 in a 44 VisualElement
[Layout] checked 3 labels, 1 new, narrowest 8pt spare, shortest -2pt spare
```

That is the only fault the audit reported in 187 audit files. Every other
language reports `shortest 6pt` to `8pt spare` on the same screen; Arabic is
`-2pt`. The advanced text generator draws a taller line box.

Second, visible rather than measured: the placeholder is pinned to the **left**
of the field (`MeetYourCatScreen.cs:113` sets
`placeholder.style.left = 6` absolutely), so in a right-to-left language the
hint starts on the wrong side of the box.

### D6 — not localisation, but found on the way: two screens use raw `new Button()`

`shots/android-7-capture-*.png`, `android-8-meet-*.png`,
`android-9-placeholder-*.png`, `android-10-dog-*.png`.

Every button on the capture screen and on meet-your-cat is a grey square-cornered
Unity default, in all seventeen languages, while the cards use the project's own
style. `CaptureScreen.cs:97,98,109` and `MeetYourCatScreen.cs:126` call
`new Button(...)` directly instead of `View/Buttons.cs`, whose class comment
(`Buttons.cs:10-16`) exists precisely to stop this: *"`new Button()` in a runtime
UI Toolkit panel picks up Unity's default runtime theme: a grey fill, a hairline
border, square corners… On a cream paper board that is a foreign object."*

---

## 3. What is right, said plainly

- **Arabic renders correctly.** `Copy.Scripts.cs:45-53` predicted "isolated,
  disconnected letters running left to right". That is not what happens.
  `GameBoot.ApplyTextDirection` loads `Resources/UI/AdvancedText.uss` and adds
  `.advanced-text` + `.rtl` at the panel root, and the letters join, the
  paragraphs run right to left, and sentence-final stops land on the left. The
  Back button reads `رجوع` correctly shaped (`shots/android-3-catcard-3.png`).
  Log line, present on every Arabic launch:
  `[GameBoot] arabic: advanced text generator, direction rtl`.
- **Nothing was clipped and nothing went off screen**, in any language, on any
  of the ten screens, at 1080x2340.
- **Every glyph drew.** No empty boxes in Thai, Devanagari, Arabic or any CJK
  table. The bundled Noto subsets in `FontFallbacks.cs:55-64` are doing their
  job on Android.
- **The board carries no copy and proves it.** Sixteen of seventeen board
  screenshots are *pixel-identical* to English (`ImageChops.difference` bbox
  `None`). Arabic differs only inside `(177, 11, 534, 228)` — the progress pill,
  a few pixels of different text metrics from the advanced generator. No layout
  flipped.
- **No save was rejected.** `grep -iE "corrupt|cannot retake|did not build|Exception"`
  over 102 launch logs: no matches. Every outcome is confirmed by its own log
  line — `[Board] win` 17/17, `[Board] lose` 17/17, `[Board] house complete`
  17/17, `branch=capture` 17/17, `branch=meet` 17/17, `cat card opened` 17/17.
- **`Sootpaw` stays Latin on the cat card in all seventeen.** That is deliberate
  — `card.game_name` is the one key `test_copy_table.py:401`
  (`SAME_IN_EVERY_LANGUAGE`) allows to be identical everywhere.
- **The house map, cat card, capture screen and the four photo-outcome messages
  are fully translated.** No English leaked into a non-English screen anywhere
  except D4.

---

## 4. Every audit line, verbatim

The audit was read for all 170 pairs. Grouped; where a group is not all 17 the
languages are named. Nothing is omitted — one fault line exists in the whole set
and it is quoted in D5.

**1 map** — 16/17
`[Layout] checked 14 labels, 0 new, narrowest 12pt spare, shortest 4pt spare`
Arabic
`[Layout] checked 14 labels, 0 new, narrowest 11pt spare, shortest 4pt spare`

**2 board** — 17/17
`[Layout] checked 4 labels, 0 new, narrowest 2pt spare, shortest 2pt spare`

**3 cat card** — 17/17
`[Layout] checked 7 labels, 0 new, narrowest 2pt spare, shortest 2pt spare`

**4 the room is clean** — 17/17
`[Layout] checked 7 labels, 0 new, narrowest 2pt spare, shortest 2pt spare`

**5 shelf jammed** — 17/17
`[Layout] checked 7 labels, 0 new, narrowest 2pt spare, shortest 2pt spare`

**6 every room is clean** — 17/17
`[Layout] checked 7 labels, 0 new, narrowest 2pt spare, shortest 2pt spare`

**7 capture screen** — the only screen whose numbers move with the language:
`[Layout] checked 4 labels, 0 new, narrowest 29pt spare, shortest 321pt spare` — English, Russian
`… narrowest 28pt spare, shortest 321pt spare` — Spanish, German, Indonesian
`… narrowest 32pt spare, shortest 321pt spare` — Portuguese
`… narrowest 27pt spare, shortest 321pt spare` — French, Italian, Vietnamese
`… narrowest 30pt spare, shortest 321pt spare` — Turkish
`… narrowest 30pt spare, shortest 312pt spare` — ChineseSimplified, ChineseTraditional, Japanese, Korean
`… narrowest 32pt spare, shortest 320pt spare` — Thai
`… narrowest 36pt spare, shortest 285pt spare` — Arabic
`… narrowest 27pt spare, shortest 317pt spare` — Hindi

**8 meet your cat** — 17/17
`[Layout] checked 3 labels, 0 new, narrowest 11pt spare, shortest 11pt spare`

**9 meet your cat, name field empty**
`[Layout] checked 3 labels, 0 new, narrowest 8pt spare, shortest 8pt spare` — English, Russian, Spanish, Portuguese, French, German, Italian, Turkish, Indonesian, Vietnamese, Hindi
`… shortest 7pt spare` — ChineseSimplified, ChineseTraditional, Japanese, Korean
`… shortest 6pt spare` — Thai
Arabic, and only Arabic:
```
[Layout] taller than its box: "ما اسمها؟" — by 2pt — 38 in a 44 VisualElement
[Layout] checked 3 labels, 1 new, narrowest 8pt spare, shortest -2pt spare
```

**10 capture screen, a dog in the photo**
`[Layout] checked 4 labels, 0 new, narrowest 28pt spare, shortest 321pt spare` — English, German, Turkish, Indonesian
`… narrowest 29pt spare, shortest 321pt spare` — Russian, Spanish
`… narrowest 27pt spare, shortest 321pt spare` — Portuguese, French, Italian
`… narrowest 31pt spare, shortest 321pt spare` — Vietnamese
`… narrowest 44pt spare, shortest 322pt spare` — ChineseSimplified, ChineseTraditional
`… narrowest 30pt spare, shortest 312pt spare` — Japanese
`… narrowest 43pt spare, shortest 322pt spare` — Korean
`… narrowest 60pt spare, shortest 320pt spare` — Thai
`… narrowest 71pt spare, shortest 300pt spare` — Arabic
`… narrowest 29pt spare, shortest 317pt spare` — Hindi

No run reported `checked 0 labels`. No run reported `wider than its box`,
`clipped` or `offscreen`.

---

## 5. Two things about the audit that a reader must not be misled by

### 5a. It runs once per launch, not on every layout pass

`LayoutAudit.Attach` registers one `GeometryChangedEvent` callback on the panel
root. On this build that fires **only at startup**. Measured, not assumed:
reaching the board by tapping a room and then opening the cat card produced
exactly two `[Layout]` lines in the whole session log —

```
08-29 05:33:22.663 I Unity : [Layout] audit on
08-29 05:33:22.889 I Unity : [Layout] checked 4 labels, 0 new, narrowest 2pt spare, shortest 2pt spare
```

— and nothing after the tap, backgrounding and re-foregrounding the app included.
**So a screen reached by tapping is not audited by simply tapping to it.**

Worked around by forcing a relayout: `adb shell wm size 1080x2338`, read the
file, `adb shell wm size reset`. The panel becomes `389.87 x 844` instead of
`389.5385 x 844` — 0.33 units wider, i.e. 0.08% *more* forgiving, so the risk is
a missed fault, never an invented one. That is how screens 2–6 were audited.

### 5b. Its headline numbers do not move with the translation

`narrowest`/`shortest spare` compare each label with its **parent**. A UI Toolkit
`Label` sizes itself to its own text, so on these screens the tightest fit is
always a fixed-size element — a room number in its circle — and the number is the
same no matter what the text says. Proof: `narrowest 12pt spare, shortest 4pt
spare` on the house map in **sixteen of seventeen languages**, character for
character, and `narrowest 2pt spare, shortest 2pt spare` on all four card screens
in **all seventeen**. Only the capture screen, whose buttons are auto-width inside
a centred column, varies (27pt to 36pt).

D2 and D3 are precisely what this blindness costs: the card grew to 1062 of
1080px and the audit reported nothing, because every label still fitted the box
that grew around it.

### 5c. But the audit is genuinely live — proved, not assumed

The German win card was left on screen and the display squeezed to `420x2340`.
The audit immediately named every label on that card:

```
[Layout] clipped: "0" — needs 8pt, the style allows 3
[Layout] offscreen: "0" — at 156,129 3x24 in a 151x844 panel
[Layout] clipped: "Das Zimmer ist sauber" — needs 264pt, the style allows 206
[Layout] offscreen: "Das Zimmer ist sauber" — at -28,233 206x35 in a 151x844 panel
[Layout] offscreen: "Vorher" — at -8,466 45x22 in a 151x844 panel
[Layout] offscreen: "Nachher" — at 107,466 56x22 in a 151x844 panel
[Layout] offscreen: "Der kleinen Katze gefällt es hier schon besser." — at -25,506 199x45 in a 151x844 panel
[Layout] offscreen: "Weiter" — at -24,573 200x40 in a 151x844 panel
[Layout] checked 7 labels, 8 new, narrowest 4pt spare, shortest 2pt spare
```

`shots/android-method-check-squeeze.png`. So `0 new` on the card screens at full
width is a real measurement of those seven labels, not a walk that measured
nothing.

---

## 6. Two extra screens, and why

- **9 — meet-your-cat with the name field empty.** The eighth screen always
  shows a *saved* name, so `meet.name_placeholder` — one of the longest strings
  in the table (French "Comment s'appelle-t-elle ?", 26 characters, in a
  220-unit field) — is never visible on it. Reached by deleting `cat.save`
  before `meet.txt`. It found D5.
- **10 — the capture screen after a photo it rejects.** The four
  `PhotoMessages` outcomes cannot be reached from screen 7 at all: the emulator
  has no camera and the gallery is empty. Reached with a two-line `capture.txt`
  naming a PNG already in the app's folder and stubbing the Vision answer
  (`fake Dog 0.73`), per `GameBoot.CaptureStub`. All seventeen `photo.dog`
  values drew correctly; `capture-state.txt` confirms each, e.g.
  `-> "犬のようです。かわいいですが、ここは猫のための家です。"`.

The capture screen shows **two** buttons, not three: the camera button is hidden
because the emulator has no camera. `capture.camera` was therefore **not seen on
a screen in any language** — the only string in the whole sweep that was not.

---

## 7. What could not be reached, and why

| not reached | why |
|---|---|
| `capture.camera` ("Take a photo") | the emulator has no camera, so the button is never built. Needs a real handset. |
| `photo.no_animal`, `photo.unclear`, `photo.accepted` | the stub in `capture.txt` takes one answer; only `photo.dog` was exercised. The other three are one line of `capture.txt` each and were not run for want of a reason to spend six more minutes on them. |
| `capture.cancelled`, `capture.skipped`, `capture.looking`, `capture.opening`, `photo.our_fault`, `capture.colours` | transient states behind a real picker. |
| `map.no_levels`, `map.room_failed`, `map.map_failed`, `levels.unavailable.*` | failure paths; would need a broken install. |
| `notification.*` | the lock screen is outside this sweep. |
| The "Pile cleared" card (`win.corner.*`) | `almost.save` ends the room, not a corner. A sibling of screen 4 through the same `ShowCard` call. |
| iOS | another worker. Nothing here says anything about iOS, and `NOTES-fonts.md` already records that iOS drew Thai as empty boxes where Android drew it. |

The `wm size` trick means screens 2–6 were audited at `1080x2338`, not
`1080x2340`. Screenshots for all ten screens are at the true `1080x2340`.

---

## 8. Where everything is

- `shots/android-<screen>-<n>.png` — 30 montages, ten screens x three sheets of
  six languages, every language labelled.
- `shots/android-defect-1..5-*.png` — the five defects, cropped close.
- `shots/android-method-check-squeeze.png` — 5c.
- Raw material (170 screenshots, 187 audit files, 102 launch logs) is in the
  session scratchpad under `l10n-android/`, not in the repository.

Nothing was fixed. Nothing under `game/Assets/` was touched. No commit was made.
The APK was not rebuilt.
