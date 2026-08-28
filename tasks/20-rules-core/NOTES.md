
## verify:passed → verify:pending — 2026-08-28

An independent verifier checked the phase's four claims. Three hold: the purity
script passes, Core compiles against netstandard2.1 outside the repo with zero
errors, coverage is 767/808 = 94.9% and the gate exits 0.

The fourth does not. The phase claims a human has played a level on a phone, and
`tasks/20-rules-core/06-debug-view/labels.txt` says `verify:pending` for exactly
that check. A phase cannot be verified on the strength of a sub-task's check that
the sub-task itself says has not happened.

Also noted: the coverage report the phase says is "attached" is gitignored, so
the wording is unmet even though the number is real and reproducible.
