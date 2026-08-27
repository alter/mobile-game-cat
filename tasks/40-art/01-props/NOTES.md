
---

## Item 1 measured, and the sheet for items 2 and 3 is ready — 2026-08-27

**VERIFY item 1, checked by reading the files rather than looking at them.**
32 PNGs under `game/Assets/Resources/Art/` — the 30 props plus `prop_unknown`
and `prop_locked` from `40-art/02`. Every one is 256×256 RGBA. Every one has a
real alpha channel, not a flat one: the channel's extrema differ in all 32.
And none carries a halo — the usual failure of a generated cut-out, bright
pixels surviving under a near-transparent alpha — measured as pixels with
alpha below 16 and any channel above 200, which is zero in all 32 files.
`game/Assets/Art/contact-sheet.png` exists, 1664×832.

**Items 2 and 3 need a person and this is not that.** Item 2 asks for the
contact sheet to be reviewed by someone other than the author; item 3 asks an
outsider whether ten props shrunk to 52px read as ten different things. Both
are `VERIFY (HUMAN)`, and `ROLES.md` is explicit that an agent neither performs
nor simulates those — an executor almost always reports its own output as
consistent.

`outsider-sheet-52px.png` in this directory is what item 3 asks to be shown:
ten props at exactly 52px, no labels, no names, on the game's own background.
They are taken at even intervals across the alphabetical set rather than the
first ten, because ten neighbours are an easier question than the game asks —
the pile mixes the whole set.

Ask one question and record the answer in the words used: **"Are these ten
different things, or do some of them read as the same?"**

*What an agent may say, and it is not the answer:* the ten are ball, bottle,
casket, comb, frame, keys, mitten, roll, scissors, tray, and a machine reading
them apart at that size is evidence about a machine. The pair most likely to
collide is the tray and the plate — both shallow ovals, separated only by
colour — and neither is on this sheet, so a reviewer who wants the hardest case
should be shown those two as well.
