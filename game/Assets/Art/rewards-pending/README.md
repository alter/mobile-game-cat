# The two reward items are held here, not in Resources — 2026-08-28

`reward_bowl.png` and `reward_blanket.png` arrived in the art delivery of
27.08.2026 and are **not wired into the game**, deliberately. Two things about
them do not match what a reward item has to be, and both were measured rather
than eyeballed:

**No transparency.** Both are fully opaque — the alpha channel's extrema are
identical, so there is no channel at all in effect. A reward sits *in a room*,
on a floor, next to a cat. An opaque square would paste a white rectangle over
the room behind it. Every other object in this game that lands on a background
— all 32 props, all three cats — carries real alpha, and these are the
exception.

**Wrong size.** They are 1328×1328. `art-brief.md` section 5 asks for 256×256,
and the delivery's own README asks for 256×256 in its table. Both documents
agree with each other and disagree with the files. Not fatal on its own — the
engine can downscale — but taken with the missing alpha it suggests these two
came out of a different pass than the props, which are 256×256 with clean
alpha.

## What they look like

Correct in every other respect: a sage bowl with a paw mark, a folded apricot
blanket, both in the house style and the right palette. This is a cut-out
problem, not a drawing problem. Re-exporting from the same source with a
transparent background at 256×256 would finish them.

## What they block

`60-shell-build/05-rewards` (P1) and `40-art/08-rewards` (P1). Neither is on any
gate's path, so nothing urgent waits on this — which is exactly why it is worth
fixing properly rather than working around with a background-coloured mask that
would break the moment a reward is shown over a room instead of over the menu.
