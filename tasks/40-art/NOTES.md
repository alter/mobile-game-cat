## Divergence from the old flat list

The old M4 table (cat-shelter-tasks.md lines 630-640) has 9 rows: 4.1-4.9.
This tree does not map 1:1 to those rows. Differences, and why:

- **4.1-4.3 merged into `01-props`.** Prop list, batch generation and
  curation are one continuous pass in art-prompts.md section 3 - splitting
  them into three directories would scatter one acceptance criterion (the
  52px distinguishability check) across three task.txt files.
- **4.6, coat compositing shader, dropped from this tree.** It is not an art
  asset - it is VIEW code that consumes the layers `04-cat-layers` produces.
  art-brief.md section 5 ("Full list of work"), which this tree is built
  to match, lists only asset groups and does not include it. It belongs in a
  shell/view task once that phase is converted; leaving it here would give
  ART a task it cannot execute (no shader access - see ROLES.md).
- **Four assets added that the old list never mentions**, because
  art-brief.md section 5 requires them and the old source predates that
  brief:
  - `prop_unknown` (buried-item blank), in `02-prop-blockers` - needed by
    30-levels-solver 3.9 (hidden kinds).
  - `prop_locked` (lock overlay), in `02-prop-blockers` - needed by
    30-levels-solver 3.11 (complications).
  - the house map cells (`map_background` + 3 states x 12 rooms), in
    `06-house-map` - old source's 6.2.1, no art task ever existed for it.
  - the share-card frame, in `09-share-frame` - old source's 6.14, same gap.

## Pilot dependency is a forward reference

`00-validate-demand/` has not been converted to this tree format yet (only
the phase directory exists at the time of writing). The pilot - art-brief.md
section 11, three items: 3 props, 1 cat in 2 states, 1 room pair - is source
tasks 0.3, 0.3.1, 0.3.2, milestone M0. Subtask DEPENDS below point at
`00-validate-demand/03-art-prompt-props`, `00-validate-demand/04-cat-two-states`, and
`00-validate-demand/05-room-dirty-clean` as descriptive best-guess paths; the
exact directory names will be settled when M0 itself is converted. Whoever
converts M0 should either match these names or fix these DEPENDS lines to
match theirs - do not leave both trees pointing at different names for the
same task.

## Why the epic depends on the go/no-go gate, not just the pilot

GOAL.md is explicit: "Until it returns 'go', nothing from the shell, the
art, the photo capture or the store gets built." The pilot slices feed the
M0 creatives and can run before the gate; the other ~90% of this milestone's
work (all of 01-props, 03-05, 06-09 beyond their pilot slice) is real spend
against art-brief.md's full asset list and does not start until
`00-validate-demand/09-go-no-go` records "go".
