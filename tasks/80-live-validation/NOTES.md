Source: cat-shelter-tasks.md, milestone M8 (lines 982-1058 at time of
conversion).

## Sequencing

The flat plan's sequencing diagram places this milestone as:

    M6 -> M7 -> 8.0 -> M8 (8.1..8.4)

i.e. 00-thresholds runs right after 70-analytics finishes, and strictly
before any of 01-spend / 02-collect-metrics / 04-publisher-submission. The
DEPENDS on 70-analytics in this epic and in 00-thresholds reflects that
diagram, not a hard technical dependency (fixing a threshold needs no code) -
it is here because the source explicitly sequences it there and because
0.7's precedent (thresholds decided by a human before any tool exists to
contaminate the number) argues for doing it as early as the dashboards are
believed to work, not later.

## Cost

$400 is the actual spend for this milestone's test (paid installs). Combined
with the $300 creative test in gate 1, the two real experiments in this
project cost roughly $700 total, per the flat plan's "What this costs to
run" table.

## Why this is gate 3 and not a checklist

GOAL.md: "Thresholds are fixed by 80-live-validation/00-thresholds before
the money moves, or they will be fitted to the result afterwards." The whole
milestone exists to answer one question honestly - do they return, and would
they pay - and the ordering of its five subtasks is deliberately built to
make it hard to answer it dishonestly (fix the number, then look).
