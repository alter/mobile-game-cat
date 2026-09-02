# GOAL — the north star

## The goal

Find out, in three weeks and a thousand dollars, whether this game is worth
building. **Not build the game — find out.** The product here is the instrument,
not the objective.

A pile-clearing puzzle for iOS where the player photographs their own cat and the
kitten in the game takes its coat. One difference from everything on the market:
**it is her cat**, and the attachment forms before level one.

Audience: women 30–55, playing 10–20 minutes in gaps between other things.

## Three gates, and only they measure the goal

Everything else in this tree is scaffolding. Of roughly seventy tasks, three test
the goal.

**Gate 1 — `00-validate-demand/09-go-no-go`.** Does the promise sell. Eight
creatives, $300 of advertising, cost per install. Until it returns "go", nothing
from the shell, the art, the photo capture or the store gets built.
*Deferred by the owner on 2026-09-01 — D18 in DECISIONS.md: build without the
demand check. The gate's question returns the day the game approaches a store;
gates 2 and 3 stand unchanged.*

**Gate 2 — `30-levels-solver/07-outsiders-playtest`.** Is this worth building.
Five outsiders play the debug build made of plain rectangles. The cheapest gate
in the project and the most likely to be skipped, because it needs other people
and it is uncomfortable.

**Gate 3 — `80-live-validation`.** Do they return, and would they pay. $400,
a hundred-odd installs, four metrics. Thresholds are fixed by
`80-live-validation/00-thresholds` **before** the money moves, or they will be
fitted to the result afterwards.

## Four metrics, no more

| Metric | Threshold | What failure means |
|---|---|---|
| reached the capture screen | > 90% | the first screen is broken |
| uploaded a photo | > 40% | the hook missed, and the hook is the whole concept |
| returned on day 1 | re-decide in `80/00` | genre median is about 20%; the recorded 35% is roughly double |
| tapped "one more shelf" | > 15% of those who reached the lose screen | there is nothing to charge for |

The denominator of the fourth metric is recorded separately — the share of
players who ever lost — because otherwise a low number cannot be interpreted.

## Signs we are getting closer

- Each closed task reduces uncertainty about one of the three gate questions,
  rather than increasing the volume of work done.
- Numbers in the documents come from measurement and carry a source.
- Rejected alternatives are written down together with the reason.
- A human picks up a phone and plays, regularly.

## Anti-patterns — we are not getting closer

- **"Twelve tasks closed" reported as progress.** Green tests are not motion
  toward the goal. This is the central trap of agent-driven development.
- **The report running ahead of the check.** Declaring done what does not
  reproduce from a clean state.
- **Silent departure from the documents.** If you diverge from what is written,
  say so; do not leave it in the code.
- **Building economy, progression or second-wave features** before gate 3.
- **Fitting thresholds to the result already obtained.**
- **An agent judging its own work** where a human is required to accept it.

## Stopping condition

Three prototypes in three months, three weeks each. If no creative buys an
install below the agreed price — close it and know the truth, rather than drag
out a fourth attempt.
</content>
