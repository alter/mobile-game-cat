Source: cat-shelter-tasks.md lines 1001, 1024-1051, 972-975.

## Why the fourth metric is two numbers

The "one more shelf" button only appears on the lose screen, and the
difficulty curve (pile sizes 36/48/60) determines how often players ever see
it. Measured win rates are roughly 98% / 87% / 66% across those pile sizes,
meaning roughly a third of level-9+ attempts jam - so the denominator
(players who ever lost) is real and must be recorded, not assumed. A low
combined "tapped, of all players" figure cannot be told apart from "the
levels were easy" versus "nobody would pay" - recording both numbers
resolves that.

The offer itself is "+1 shelf slot", not "+5 moves" (there is no move
counter in this ruleset). The event is `booster_tap`, neutral to what is
offered.

## App Store Connect caveat

App Store Connect hides any slice covering fewer than five users. At
roughly a hundred installs the headline day-1 retention figure should still
appear, but no breakdown will. If the headline figure itself does not
appear, fall back to measuring day-1 return through GameAnalytics instead.
