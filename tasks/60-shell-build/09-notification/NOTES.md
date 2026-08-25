# Notes - why this is P0, and a debunked number to not repeat

Source: cat-shelter-tasks.md, lines 883-897; knowledge/ios/05-notifications-
permissions.md section 4.

## Why P0, not P1

This task was raised from P1 to P0 because the original priorities
contradicted each other. Metric 3 (return on day 1) is one of four numbers
that decide the project, and the evening notification is the only mechanism
in the entire MVP designed to cause that return. Leaving the mechanism
optional while the metric it drives is a go/no-go threshold means a slipped
P1 task would quietly guarantee a bad reading on a P0 measurement. Either the
notification ships, or 80-live-validation/00-thresholds has to lower the
day-1 threshold to account for its absence - and shipping the notification is
cheaper than renegotiating the gate.

## Ask after level 2, not on the first screen - but don't cite a number for it

Keep the practice, drop the justification that used to travel with it. The
claim that asking for permission on first launch "doubles the refusal rate"
was traced back through the sources that repeat it and does not hold up: a
marketing blog (vmobify) cites Pushwoosh for "55-70% opt-in vs 30-40%"; the
actual Pushwoosh post contains no such figures, only a qualitative
recommendation to ask at a moment of high intent. A second source
(semnexus.com) makes the same qualitative claim with no numbers either.

The reasoning that survives is plain, not measured: a permission request
means more once the player already knows what the notification is for.
Treat "ask after level 2" as judgement, not as a measured fact, and do not
repeat "doubles the refusal rate" or any specific percentage to a publisher
or in analytics reporting - see
knowledge/ios/05-notifications-permissions.md section 4 for the full citation
chain.
