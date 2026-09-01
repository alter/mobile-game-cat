# Seven non-Latin languages, 2026-08-29

`game/Assets/Shell/Copy.Scripts.cs` — Chinese Simplified, Chinese Traditional,
Japanese, Korean, Thai, Arabic, Hindi. Plus the legacy `SystemLanguage.Chinese`,
which Unity still reports on some devices, mapped to the Simplified table.

Companion to `NOTES.md`, which records the English and Russian decisions. None
of those decisions is reversed here. Where one of them is a length limit, it is
a **different** limit in these scripts, and the arithmetic is redone at every
key rather than inherited.

---

## 1. The glyph list — read this first

`glyphs.txt` in this directory. **681 distinct characters** across the seven
tables, written by a script from the parsed string values, not by hand.

| Language | Distinct characters | Was, 2026-08-29 |
|---|---|---|
| Chinese Simplified | 191 | 189 |
| Chinese Traditional | 192 | 190 |
| Japanese | 160 | 159 |
| Korean | 186 | 178 |
| Thai | 60 | 60 |
| Arabic | 58 | 58 |
| Hindi | 67 | 69 |
| **Combined** | **681** | **673** |

**Regenerated 2026-09-01**, after the four `photo.*` outcome strings were
rewritten in all seven tables — the photo screen stopped refusing, so none of
them instructs a retry any more. Thai and Arabic did not move at all: the new
wording reuses letters those tables already had. Hindi went DOWN by two,
because `ऐ` and `ठ` lived only in the old ऐसी and बैठी and nothing else in the
table wants them. Korean moved most, by eight syllables, which is what a
syllabic script does when a sentence is reworded.

Twenty-five characters are new to the combined line and seventeen dropped out
of it. **The new ones are ordinary, and every one of them is in `glyphs.txt`:**

| Table | Added | Dropped |
|---|---|---|
| Chinese Simplified | 做准它楚清照片猜身 | 动坐容得所试趁 |
| Chinese Traditional | 做它楚清準照片猜身 | 動坐容得所試趁 |
| Japanese | ね作確近 | 取大枚 |
| Korean | 내또렷신었올와작잘져짐확 | 겠읽장히 |
| Thai | — | — |
| Arabic | — | — |
| Hindi | — | ऐठ |

A font subset built before this date does not cover the added column, and a
missing glyph is an empty box on the device — rebuild from the file rather than
assuming the old subset still fits. Nothing needs to be removed: the dropped
column is a font that is merely larger than it has to be, which costs nothing
but bytes.

The file carries three things: one line per language, a combined line, and a
codepoint table (`U+XXXX`, the character, its Unicode name) so a font tool can
be fed it directly.

**It contains things that are easy to leave out of a font and impossible to
notice missing until a player sees a hole:**

- `U+0020` space — the Thai tables depend on it (§4) and the Korean and Hindi
  ones are full of it;
- the ASCII letters of `Sootpaw`, which appears untranslated in all seven
  tables (`card.game_name`, and inside two share captions);
- `{`, `}`, `0` — `string.Format` leaves these on screen verbatim when a call
  site passes no argument, which is a documented live hazard in this codebase
  (`EveningReminder.cs:52`);
- `U+2026` ellipsis, `U+00B7` middle dot (the separator in `map.legend`),
  `U+2014` em dash;
- **every combining mark**: Thai vowels and tone marks, Arabic shadda and
  hamza, Devanagari matras and `U+093C` nukta. A font missing `U+093C` does not
  show an empty box — it shows Hindi words like साफ़ and नक़्शा subtly wrong,
  which will pass a screenshot review.

**Regenerating it.** The list is derived from the file, so it goes stale the
moment anyone edits a string. The script is 90 lines and lives in this task's
scratchpad, not in the repo — if it should be permanent, it belongs in `tools/`
as a test that fails when `glyphs.txt` disagrees with `Copy.Scripts.cs`. That
would be the right fix and it is **not done**: today the list is correct and
nothing enforces that it stays correct. The 2026-09-01 pass had to write the
script again from this description, because the scratchpad it lived in was
gone — which is the argument for `tools/` making itself.

**How the rewritten script was checked, since it is the only thing standing
between an edit and a box on the device.** It was run first against the
`Copy.Scripts.cs` of the previous commit and its output diffed against the
`glyphs.txt` committed alongside it: byte for byte identical, all eight counts.
Only then was it run against the edited file. Two traps it has to avoid and a
naive version does not: values must be unescaped by hand (Python's
`unicode_escape` codec round-trips through latin-1 and turns every CJK
character into mojibake, which silently produced 96 "characters" on the first
attempt), and `\n` — `house.complete.body` has two — is a paragraph break, not
a glyph any font has to draw.

---

## 2. Register, per language

The brief is section 2 of `cat-shelter-mvp.md`: women 30–55, ten to twenty
minutes between chores, and a game that never shouts, congratulates, rushes or
competes. **No table has an exclamation mark in it.** Beyond that, four of the
seven forced a real choice:

**Japanese — ですます, level and plain.** Two wrong registers were available and
both are what a mobile game actually ships: the loud one
(「クリア！」「おめでとうございます！」, katakana and exclamation marks, which
congratulates a player for a tap) and the cute one (「〜だよ」「〜しちゃおう」,
plain form with sentence-final particles, written for a teenager). Not honorific
throughout either — 尊敬語 would make the kitten's house sound like a department
store. **One deliberate exception:** `photo.accepted` is 「お預かりしました」,
humble form, because that is the moment the game takes custody of somebody's
real cat and that is exactly what 謙譲語 is for. Cards are intransitive —
「部屋がきれいになりました」, not 「部屋をきれいにしました」 — so the game reports
what is true instead of crediting the tap, which is the same call the English
pass made when it deleted "Room clean!".

**Korean — 해요체.** Rejected: 합니다체 (correct, and the register of a news
reader — cold over a photograph of a kitten), 반말 (what casual games use; it
addresses a woman of forty-five as a schoolfriend), and the aegyo register
(「축하해요!」, 「냥이」, cat-speak endings — the exact Korean equivalent of the
pink-with-glitter section 2 rules out). 당신 appears nowhere: it is textbook
Korean rather than spoken Korean. `map.title` is 우리 집, not 당신의 집 — the
English "Your house" is doing warmth, not ownership, and 우리 집 is where that
warmth lives in Korean.

**Hindi — आप, never तुम or तू.** तुम is what a mobile game uses and what an
adult uses to a child; on a woman of forty-five it is a small rudeness repeated
on every screen. Verbs follow: दिखाइए, चुनिए, लीजिए. The kitten is
**नन्ही बिल्ली**, not the dictionary's बिल्ली का बच्चा — that phrase is three
words, grammatically masculine, and reads like a school textbook, whereas
नन्ही बिल्ली is feminine and so carries the English's deliberate "she"
(12-copy-english change 7) for free.

**Arabic — MSA, and deliberately genderless.** Arabic imperatives are gendered
(اختر / اختاري) and the game does not know who is holding the phone. So buttons
use the verbal noun (التقاط صورة, اختيار صورة), which is what Arabic iOS and
Android already print on their own buttons; the player's own lines use the first
person (أريد), which is genderless; instructions use impersonal forms (يُرجى,
يُفضَّل); and possessive suffixes such as بيتك and بانتظارك are written
identically for both genders undiacritized, which they are.

**Chinese (both) — plain declarative, 你 not 您.** 您 would make the game sound
like a bank, and most strings address nobody at all, which is quieter still.
Avoided: 恭喜／太棒了／完成啦.

**Thai — no ครับ/ค่ะ anywhere.** Those are the normal source of politeness in
spoken Thai and both are gendered **by speaker** — using either would force the
game to decide, on every line, whether it is a man or a woman talking. Softness
comes from word choice (ไม่ต้องรีบ, เมื่อไรก็ได้) and from คุณ, the neutral
polite second person. กรุณา appears once, on the one string that is genuinely an
instruction.

---

## 3. How Traditional Chinese was produced — say this plainly

**It is the Simplified table converted character by character, plus ten
deliberate lexical substitutions. It is not a Taiwan or Hong Kong
localisation.**

**Ten, not eleven, since 2026-09-01.** 試試看 for the mainland's 試試 was the
eleventh and it lived in exactly one string, `photo.no_animal`, which no longer
asks the player to try anything — the photo screen stopped refusing, so none of
the four outcome lines instructs a retry. The substitution was not withdrawn;
the sentence that needed it was.

The substitutions, all of them cases where the two markets use different
*words* rather than different glyphs:

| Simplified | Traditional | Key |
|---|---|---|
| 相册 | 相簿 | `capture.gallery` |
| 加载 | 載入 | `map.no_levels`, `levels.unavailable.body` |
| 消息 | 訊息 | `notification.channel_description` |
| 这儿 | 這裡 | `photo.accepted` |
| 太糊了 | 太模糊了 | `photo.unclear` |
| 占满 | 佔滿 | `lose.body`, `capture.hint` |
| 着急 | 著急 | `capture.cancelled` |
| 里 | 裡 | five keys |

**What this is not.** No Taiwanese or Hong Kong reader has seen it. Hong Kong
wording is not addressed at all — Cantonese-influenced usage diverges from
Taiwan's again, and Unity reports both through the one
`SystemLanguage.ChineseTraditional` value, so one of the two markets is being
served wording written for the other. Treat this table as "readable and not
wrong" rather than as localised, and hand it to a real reviewer first if the
game ever sells in Taipei.

---

## 4. Width, and what had to be shortened

Read off `game/Assets/View/DebugGame.uss` and the four view files, not guessed.
Panel is 390×844 units, `m_Match: 1`, so 390 is the narrow end.

| Element | Size | Wraps? | Budget |
|---|---|---|---|
| `capture.title`, `meet.title` | 26 | **no** | 342u (390 − 48 padding) |
| `.game__card-title` | 22 | **no** | 318u (366 overlay − 48 card padding) |
| `.game__card-body` | 15 | yes | 240u `max-width` |
| `.game__ba-label` | 12 | yes | 116u pane |
| `.game__card-button` | 16 | no | 200u `min-width`, grows |
| `map.legend` | 10 | yes | 92% of width |

**A CJK character is one em wide.** That is the number that changed everything:
`capture.title` gets about **thirteen** Chinese, Japanese or Korean characters
where a Latin line gets twenty-four. Measured against that budget, every value
in all seven tables fits; the tightest is Japanese `capture.title` at 312u of
342.

What was actually shortened or dropped:

- **`capture.title`, Japanese.** 「あなたの猫を見せてください」 is thirteen
  characters — exactly on the ceiling, nothing to spare — and was dropped for
  「猫の写真を見せてください」 (twelve, 312u).
- **`capture.title`, Arabic.** Not shortened, *restructured*: أرِنا / أرينا
  would pick a gender for the player on the first screen she sees, so it became
  نودّ رؤية قطّتك ("we would like to see your cat"), which asks the same thing
  of nobody in particular.
- **`house.complete.title`, Japanese.** 「どの部屋もきれいになりました」 is
  fourteen, on the ceiling; cut to 「どの部屋もきれいです」 (ten).
- **`levels.unavailable.title`, Japanese.** 「見つからないものがあります」 (13)
  → 「何かが足りません」 (8).
- **`house.complete.body`, all seven.** Two sentences where the English has one
  clause and a comma — the same call the Russian table made, and for the same
  reason: the relative clause these languages would otherwise need is a line
  longer than a card that already carries two photographs.
- **`win.before` / `win.after`.** Bare 前/後 was rejected in both Chinese
  (ambiguous over a photograph) and Japanese (reads as "front/back"), and bare
  전/후 in Korean (reads as a timestamp). They became 之前/之后, 片づけ前/片づけ後,
  정리 전/정리 후 — all inside the 116u pane at fontSize 12, the widest being
  Japanese at 48u.

**Thai: the spaces are load-bearing.** Thai has no spaces between words; a space
in Thai separates clauses, the way a comma does in English. Unity's standard
text generator wraps CJK between any two characters with no spaces needed, but
Thai needs dictionary lookup — which needs the Advanced Text Generator and an
ICU data asset, and `Shell/PanelSettings.asset` has
`m_ICUDataAsset: {fileID: 0}`. A normally-written Thai sentence is therefore
**one unbreakable token** and the 240u card body cannot wrap it. So every long
Thai value is broken at a clause boundary with a real space, which is where
written Thai puts its spaces anyway and costs the reader nothing. Measured: the
longest unbreakable Thai run is **206u** against the 240u budget — it moved from
198u on 2026-09-01, when `photo.our_fault` gained the clause
แต่เราทำลูกแมวจากรูปของคุณแล้ว (twenty-five spacing characters; the vowels and
tone marks stack and add no width). **Anyone "tidying up" those spaces turns a
wrapping paragraph into a line that runs off the card.**

**CJK wrapping is functional but unpolished.** `PanelSettings.asset` also has
`textSettings: {fileID: 0}`, so the kinsoku leading/following character lists
are empty and a 。 or 、 can be pushed to the start of a wrapped line. Ugly, not
broken, and a PanelSettings fix rather than a copy one.

---

## 5. Arabic will probably not render — the thing to check on the device

**State this before the build, so the result means something.**

Arabic is right-to-left and its letters change shape according to what they join
to. Both are the text engine's job. This project has not given it the means:

```
game/Assets/Shell/PanelSettings.asset
  m_ICUDataAsset: {fileID: 0}
  textSettings:   {fileID: 0}
```

The Advanced Text Generator — the one that does bidirectional reordering and
Arabic shaping — is not configured. **The predicted symptom is a line of
isolated, unjoined Arabic letters running left to right**, which is readable to
nobody.

Three outcomes and what each means:

1. **Empty boxes.** Not a bidi problem — the default font has no Arabic at all.
   Fix is the font built from `glyphs.txt`.
2. **Disconnected letters, left to right.** The predicted result. Fix is the
   text engine in `PanelSettings.asset`, not this table.
3. **It renders correctly.** Then Unity 6.3 turns the shaper on by default in a
   way the asset file does not show, and the note above is too pessimistic —
   say so and delete it.

**A free extra signal:** `notification.title` and `notification.body` are drawn
by iOS and Android, both of which shape and reorder Arabic properly. If the lock
screen is right and the game's own cards are wrong, that is decisive evidence
that the fault is UI Toolkit's text engine and not the Arabic in this file.

**No code here works around it, and none should.** The table is written in
logical order with no manual reversal, no pre-shaped presentation forms and no
zero-width joiners. Faking right-to-left in the strings would look better in one
screenshot and be broken beyond repair the day a shaper is switched on.

`card.caption` and `map.room_failed`/`map.map_failed` are the worst cases even
if shaping works: each mixes a left-to-right run (the name `Sootpaw`, or a
system error message arriving in the system language) into a right-to-left line,
which is precisely what the bidi algorithm exists to resolve.

---

## 6. Checks run, and what was not run

Run, by me, on the actual file:

- All seven tables parse with the same regexes `tools/tests/test_copy_table.py`
  uses. Seven tables, **49 keys each**, matching English exactly — no missing
  key, no extra key. (This line said 48 until 2026-09-01. It was not wrong when
  written; `cat.default_name` landed afterwards and nothing came back to the
  count. Re-measured today rather than believed.)
- Placeholders counted per index against English: identical everywhere, and
  `0..n` in every value that has one. `{0}` survives in all four keys that carry
  it (`card.caption`, `house.complete.caption`, `map.room_failed`,
  `map.map_failed`), positioned where each grammar wants it — first in Japanese,
  Korean and Hindi, last in Chinese, Thai and Arabic.
- No value equals its English counterpart except `card.game_name`, which is
  `Sootpaw` in all seven.
- Every table is reachable from a `SystemLanguage`, including the legacy
  `SystemLanguage.Chinese` → Simplified.
- No Cyrillic in the file, so `test_no_cyrillic_outside_the_copy_table` is safe
  when `Assets/Shell` is next scanned.
- Width arithmetic above, computed from the USS numbers.

**Not run, and not claimable:**

- **Nothing has been rendered.** No Unity, no simulator, no device — not run by
  instruction. Every width figure is arithmetic from the USS, using one em per
  CJK character and about 0.55 em for Thai, Arabic and Devanagari. That
  approximation is good enough to catch a string that is twice too long and not
  good enough to certify one sitting at 95% of its budget; Japanese
  `capture.title` at 312u of 342 is the one to look at in the first screenshot.
- **`tools/tests/test_copy_table.py` does not read this file yet.** It reads
  only `Copy.cs`. Another worker is wiring up the companion files; until that
  lands, the parity guarantees above rest on my script, not on the suite.
- **No native reader has checked any of the seven.** Register, idiom and tone
  are argued from the brief and stated per language so that a reviewer can
  disagree with a specific sentence rather than with a whole table.
- **Korean `photo.unclear` and Japanese `photo.unclear`** are the two longest
  strings in their tables and land in a wrapping label whose width I read from
  `CaptureScreen`, not from the USS — they are the likeliest place for a
  three-line paragraph to become four.
- **Hindi `map.no_levels`** keeps स्तर for "levels" — engine vocabulary the
  English notes complained about, kept because the string is an error message
  and the alternative was longer. If the map ever shows this to a real player it
  should be rewritten.

---

## 7. The four outcome strings, rewritten 2026-09-01

The photo screen used to be able to REFUSE. Four outcomes — no cat, a dog, too
blurry, our fault — each ended the run and put the player back on the buttons
with nothing, so all four were written as instructions to retry, because a retry
was the only move. **Nothing refuses now.** Every photograph makes a kitten, and
the line is read WHILE it is being made, over a progress bar that says "copying
the colours" in the same table. So "try another photo" no longer merely read
stale — it contradicted the screen underneath it.

All four in all seven tables now say what we saw, then what we did, then offer a
better photograph as a **choice**. Concretely, per language, the grammar that
does the offering without instructing:

| Language | "we did it anyway" | the offer |
|---|---|---|
| Chinese (both) | 还是／還是 | 会更准／會更準 — a comparative, not 试试 |
| Japanese | 〜が + 作りました | 〜なら…なります, a condition, not 〜てください |
| Korean | 그래도 | plain 해요체 statement; the 〜ㄹ까요? that asked is gone |
| Thai | แต่เรา…แล้ว | จะ + comparative, not ลอง |
| Arabic | على أيّ حال | a verbless comparison; still no imperative, so still no gender on the player |
| Hindi | फिर भी | the counterfactual आता／होता, not the imperative लीजिए |

**What was removed, and it was removed on purpose in all seven:** the second
clause of `photo.dog` was the refusal itself — 不过这里是猫的收容所,
ここは猫のための家です, 여기는 고양이 보호소예요, แต่ที่นี่เป็นบ้านพักของแมว,
لكنّ هذا الملجأ للقطط, पर यह आश्रय बिल्लियों के लिए है. The compliment to the dog
stays in every one of them: it existed so that being turned away was not a
rebuke, and it costs nothing now that nobody is turned away. The dog's own coat
colours are what the kitten takes, which is what the second clause now says.

`photo.our_fault` also lost its `capture.skipped` tail in all seven — "the
kitten is waiting for you either way". That sentence pointed at the skip button
because being sent back to the buttons was what happened next, and nothing does
now; the middle clause reports that the kitten was made from her photograph even
on this path.

### Flagged — what a native reader should look at first

Nothing below is a known error. These are the places where the writer is not
confident a native speaker would find the line natural, listed so a reviewer can
disagree with a sentence instead of with a table.

- **Arabic, all four.** This table was already medium confidence and stays
  there. `صورة أوضح تجعل الألوان أدقّ` (`photo.unclear`) and
  `ستأخذ الهرّة هذه الألوان` (`photo.dog`) are grammatical and neither agrees
  with the photographed cat, which was the constraint; whether they read as warm
  MSA or as a manual is exactly the question `meet.title` and `meet.confirm` are
  already flagged for. **Same caveat, not a new one.**
- **Thai `photo.unclear`** — `สีขนจึงเป็นการเดา`, "the coat colour is therefore
  a guess". จึง is correct and slightly bookish; a Thai writer might reach for
  เลย. Not changed, because เลย pulls toward the casual register this table
  avoids. The rest of the Thai four reuse phrasing the table already had.
- **Hindi `photo.no_animal` and `photo.unclear`** — the counterfactuals
  `रंग और सटीक आता` and `और पक्का होता`. Hindi drops होता in speech and the bare
  form can read as clipped on the page. It is the construction that offers
  without instructing, which is why it is here, but a native reader may want
  `आ सकता था` / `हो सकता था`.
- **Japanese `photo.dog`** — 「かわいいですね」. ね is the one sentence-final
  particle anywhere in this table, and the class note rules out the 〜だよ
  register it belongs to. It is kept because a compliment with no particle at
  all reads as a report rather than as warmth, and ね in ですます is level adult
  speech, not the cute register. **Still, it is the one place this table bends
  its own rule, and a reader should confirm it does not tip.**
- **Korean `photo.unclear`** — `털색은 짐작이에요`. 짐작 is the right word and the
  noun-predicate is compact; whether 해요체 wants 짐작이에요 or the more spoken
  `짐작으로 했어요` is a judgement no dictionary settles.
- **Chinese (both), `photo.no_animal`** — `小猫还是照它做的`. 照它 is "going by
  it", meaning the photograph, and Chinese would more often name the thing than
  pronominalise it. 这张 is right there in the first clause, so the reference is
  clear, but a native writer might prefer `照着这张做的`.

Not flagged, and deliberately: the Chinese `photo.dog` 这身毛色 / 這身毛色, where
身 is the ordinary measure word for a coat; the Thai `photo.dog`; and all four
`photo.our_fault`, which are the plainest sentences of the twenty-eight.
