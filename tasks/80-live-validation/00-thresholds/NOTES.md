Source: cat-shelter-tasks.md lines 992-1051; primary data in
knowledge/analytics/02-benchmarks-and-attribution.md.

## Why this task exists

As originally written, "day-1 retention > 35%" is not a floor separating a
viable game from a dead one - it is roughly double the genre median. Two
independently checked primary sources:

- GameAnalytics, "2025 Mobile Gaming Benchmarks" (11,600 games, 2024 data):
  puzzle median day-1 retention 19.66-20.74%.
- Adjust, "The gaming app insights report: 2025 edition" (2024 data):
  all-games average day-1 retention 27%.

35% sits between "the best casual sub-genres at launch" (hybrid/hyper
casual, 27-28% per Adjust) and "the top of the market" (40-50% per Adjust's
own framing). A game returning 25% - comfortably above the genre median -
would be shut down by the 35% rule as originally written. The frequently
repeated "puzzle day-1 is ~32%" figure did not survive a check against the
GameAnalytics primary source and is not used here.

## The three stances (pick one, write it down)

1. Keep 35%. Only interested in a breakout, not a merely viable game. Honest
   given "three prototypes in three months, three weeks each."
2. Lower to ~25%. Comfortably above the genre median, still evidence the
   hook works.
3. Bands instead of one line: below 20% stop, 20-30% iterate once, above
   30% push. Costs nothing, avoids a binary verdict on a noisy sample.

## Sample size caution

A hundred installs puts a day-1 retention reading within several percentage
points either way. Treat 24% and 27% as the same number - this is itself an
argument for the banded stance.

## Metric four: two numbers, not one

"Tapped 'one more shelf', >15%" was written against all players, but the
button only appears on the lose screen, and the difficulty curve (pile sizes
36/48/60, measured win rates ~98% / ~87% / ~66%) determines how many players
ever see it. A low combined figure is ambiguous between "the levels were too
easy" and "nobody would pay" - the two numbers (share who ever lost; share
of those who tapped) resolve that ambiguity. Set both here, in advance.

## Publishers as a source for the fourth threshold

SayGames, Homa, Kwalee, CrazyLabs, Rollic (the five in
04-publisher-submission) are worth asking directly what they consider a
passing monetisation signal, rather than guessing at 15% alone.

---

## Metric four lost its instrument, 2026-08-27

The section above ("Metric four: two numbers, not one") assumes the "one more
shelf" button exists. It does not. **D4 was revised on 27.08.2026** and the
button and its two strings were removed from the lose screen, after the owner
hit the jam in play and asked why the game offers something and then refuses
it. `Analytics.BoosterTap` and `Board.AddShelfSlots` both remain; only the
offer is gone.

The reasoning is worth repeating because it settles what to do next: the tap
was **free**, and a tap on a costless offer to not lose is not evidence about
willingness to pay. Metric four asks whether anyone would pay. So the number
that button produced was never going to decide anything.

That leaves gate 3 with three real metrics and one hole. Pick one of these,
in writing, before `01-spend`:

**(a) Drop metric four from this run.** Say plainly that this $400 answers
"do they arrive, and do they come back" and not "would they pay". Costs
nothing, and the honesty matters: a missing number must not later be read as
a failed one. What it forfeits is the monetisation signal a publisher will
ask for.

**(b) Put the offer back with a real price.** One StoreKit product, three
slots, once per level, priced. This is the only version that produces evidence
about paying. It needs the App Store Connect record, a configured in-app
purchase and review — days, not hours — and it pulls monetisation work forward
that GOAL.md defers until after gate 3.

**(c) Ask the five publishers instead of measuring.** `04-publisher-submission`
already contacts SayGames, Homa, Kwalee, CrazyLabs and Rollic. What they call
a passing monetisation signal for a prototype is free to ask and worth more
than a number from a hundred installs. This is not a substitute for (a) or
(b) — it is how the threshold gets chosen if (b) is taken.

**The denominator problem is now measured, and it is the reason to lean to
(a).** Re-measured 27.08 on the shipped levels
(`30-levels-solver/10-remeasure-curve-partial-info/NOTES.md`): a realistic
player wins **92.7%** of level attempts, so roughly one attempt in fourteen
ends in a jam. At a hundred installs, the number who ever reach a lose screen
is small, and the number who then buy anything is smaller still. Option (b)
would spend days building a purchase to measure it on a handful of people.

**Correction to the section above.** It quotes win rates "~98% / ~87% / ~66%".
Those are the retired figures for a player who only watches the shelf. The
current measurement, same script, same seed, is 98.0% / 83.8% / 71.5% for that
player and **99.2% / 94.2% / 90.0%** for a realistic one. The realistic row is
the one that governs how often a lose screen is ever seen.
