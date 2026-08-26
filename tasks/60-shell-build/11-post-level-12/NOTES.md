
# Built, 2026-08-27 — the text half

The house already ended, but badly: winning the last pile showed the ordinary
"Room clean!" card with a **Next** button, and only pressing it revealed the
ending. VERIFY 1 asks for the ending screen instead of the regular win screen,
not after it. `Finish` now checks `RoomPlan.Next(level) == null` and shows the
ending directly.

**No button on it at all.** The previous version offered "Play again", which is
the replay-from-scratch the scope excludes. A card at the end of the house has
nowhere to go, and a button would have to invent a destination — so `ShowCard`
learned to hide its primary button rather than be given a fake one.

**No call to action of any kind** (VERIFY 2): no waitlist, no email, no
purchase, no "wishlist the full version". The MVP's own rule is to build no
second-wave feature before gate 3, and a teaser here would be one.

The copy:

> **Every room is clean**
> All twelve of them, and one kitten who no longer has anywhere to hide her
> finds.
>
> That is as far as this house goes for now.

An honest stop. It says the content ended, not that the player did something
wrong and not that something is coming.

The save is cleared at that point, so relaunching does not drop the player back
onto a finished board.

## What is still missing, and why the task stays open

SCOPE asks the screen to show **the fully-clean house map and the cat in its
final form**. Both are art: the map is 37 PNGs (`40-art/06`) and the cat is six
silhouettes plus 54 masks (`40-art/03`, `/04`). What exists today is the text
and the moment it appears at.

The route there is covered by test rather than by eye: `RoomPlanTests` asserts
`Next` returns null on the last level and that the house does not wrap.
