# The table in this task is retired — 2026-08-26

Superseded by `10-remeasure-curve-partial-info/NOTES.md`, which carries the
replacement and a script that reproduces it (`python -m tools.solver.measure`).

The 98/87/66 numbers are **not wrong**: this task's own policy — "prefers kinds
already 2-of-3 on the shelf" — reproduces them at 98.0 / 87.0 / 69.5. They are
retired because that policy is a weaker player than the one who will play. A
player who also notices which kinds have most copies open in front of her wins
99.0 / 96.5 / 89.8 on the same levels.

VERIFY 2 of this task asks that the numbers be "reproducible by re-running the
measurement script referenced there". No such script was ever committed; the
numbers lived only in `reviews/2026-08-24-refactor-difficulty.md`. There is one
now, and it is `tools/solver/measure.py`.

VERIFY 3 — "curve is monotonically decreasing band-to-band" — still holds, and
still says less than it appears to. It is a claim about bands, while a player
meets levels one after another. Measured on the 37 shipped levels, 200 games
each (`python -m tools.solver.measure --shipped`):

| band | shelf-only | reachable-aware |
|---|---|---|
| 36 items | 97.2% (90.0–100) | 98.4% (92.0–100) |
| 48 items | 83.4% (56.5–97.0) | 95.1% (84.0–100) |
| 60 items | 64.4% (31.5–86.0) | 90.8% (67.5–99.5) |

The band averages track the retired table closely; the spread inside a band
does not. `l24_room09_pile2` is the hardest level in the game by a wide margin
(31.5% / 67.5%) and sits a few levels before `l27`, among the easiest of the
same band. Nothing measures or evens that out, because difficulty runs on one
knob — pile size — which is constant within a band.
