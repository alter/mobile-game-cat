
## P1 → P0, 2026-08-28

Raised because two P0 tasks are built around these images and cannot be
finished without them: `60-shell-build/02-room-piles`, which swaps a background
it does not have, and `60-shell-build/06-win-screen`, whose entire subject is
the transformation this pair *is*. A P1 sitting under two P0 dependants is a
mislabel rather than a judgement.

The stronger reason is outside the shell. `cat-shelter-mvp.md` calls the
before/after pair the game's actual pitch and the eight-second reel, and gate 1
buys installs with creatives that would be made from exactly that. Of everything
still missing, this is the group whose absence reaches furthest.

Flagged rather than buried: the owner can argue it back down, and the argument
would be that rooms are expensive — twelve pairs at 1536×3072 — while the shell
can be finished around placeholders. That is true and it is why this is a
priority question and not a blocking one.

See `tasks/40-art/MISSING.md` for what else is missing and in what order.

## Delivery check, 2026-08-28 — measured against this task's own OUTCOME/VERIFY

**File list: complete.** All 24 files present, exactly `room_<nn>_clean.png` /
`room_<nn>_dirty.png` for `nn` 01..12 — the same names `Resources.Load` will
use once the room-piles code loads them. No extras, nothing missing. Live
under `game/Assets/Resources/Art/` (the `Resources` subfolder is required for
`Resources.Load` to find them at all — not a deviation from "under
`game/Assets/Art`", it is what makes the load work).

**Measured, not trusted:** `identify` on every file gives 1856×3328, 8-bit
sRGB, no alpha channel, for all 24 files with no exception. That matches
`MISSING.md`'s recorded 1856×3328/opaque exactly — nothing differs from what
was measured on arrival. 1856×3328 clears the 1536×3072 spec (itself already
above the 1320×2868 worst case), same direction MISSING.md called "right,
nothing to fix."

**Mean lightness, dirty vs clean, all 12 pairs** (greyscale mean, 0-100):

| room | dirty | clean | spread |
|---|---|---|---|
| 01 | 31.97 | 62.72 | 30.75 |
| 02 | 31.06 | 57.43 | 26.37 |
| 03 | 35.62 | 67.98 | 32.36 |
| 04 | 32.74 | 67.51 | 34.77 |
| 05 | 35.33 | 68.50 | 33.17 |
| 06 | 33.22 | 64.21 | 30.99 |
| 07 | 28.68 | 65.19 | 36.51 |
| 08 | 31.16 | 56.28 | **25.11** (weakest) |
| 09 | 31.86 | 63.29 | 31.43 |
| 10 | 35.13 | 62.13 | 27.01 |
| 11 | 29.59 | 60.82 | 31.23 |
| 12 | 30.79 | 57.63 | 26.84 |

Every pair clears 25 points of mean lightness — nowhere near the "couple of
points" that would predict a half-second-test failure. Room 08 is the
weakest pair (25.11) and room 07 the strongest (36.51); worth having a
person look at 08 first if only one pair gets scrutiny. This is a proxy —
mean lightness across the whole frame, not what a viewer's eye actually
lands on — so it does not replace the real test, only makes its outcome
predictable in advance.

**What a machine can't clear:**
- VERIFY 2 (furniture/window position matches by eye, same room not a
  similar one) — needs a person.
- VERIFY 3 (200×400, half a second, instant naming, recorded per-room in
  VERIFY.md) — needs a person. To make that sitting possible in one pass
  instead of twelve, `qa/room-pairs-200x400.png` renders all 12 pairs
  (dirty | clean, each 200×400, labelled) as one sheet.

Everything a machine can check is satisfied. `labels.txt` moved to
`status:review`; `verify:` left as `pending` for the human items above.
