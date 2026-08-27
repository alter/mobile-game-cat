# Notes - why this task exists at all

Source: cat-shelter-tasks.md, "6.2.1 was missing, and it serves a motivator
the MVP names explicitly" (lines 740-753).

There was no screen showing the whole house. 6.2 binds a room to a level and
6.10 (11-post-level-12) shows an end card, but nothing in between shows how
much is left. The audience analysis lists, as motivator #3, "completeness — an unfinished
set is nagging" (an unfinished set nags). A house with four clean
rooms and eight dirty ones makes that feeling visible, for the cost of one
screen. Leaving it out would discard a retention driver the design already
identified.

It also fixes something subtler: progress in this game is meant to be *seen*,
not counted - a room brightens, the cat improves, objects appear - but all of
that is visible only one room at a time without this screen. The map turns
twelve separate improvements into one accumulating thing, which is what makes
an unfinished set nag.

## Placeholder art, and what the real art must preserve

40-art/06-house-map has not delivered art yet (still `status:todo`), so
`View/HouseMapView.cs` falls back to a painted placeholder for the background
and all 36 cells, the same way `CoatBuilder.LoadBase` falls back when a coat
texture is missing - one warning, not a hole in the screen.

The placeholder tells the three states apart by silhouette, not shade:
- dirty - a plain square, flat dark fill, no icon.
- partial - a split tile: two halves across a hard vertical edge at 50%, no
  gradient between them.
- clean - a full circle (a different outline from the other two, not a
  lighter square) with a small mark.

art-prompts.md already commits the real art to lightness-only, hue-fixed
tiles ("dark, halfway, light"), which is a narrower rule than the
placeholder's shape trick and is not something this task is overriding. What
the placeholder's choices point at, and what the real art has to keep, is
two things art-prompts.md already says but that are easy to lose while
drawing thirty-six small files one at a time:
1. "partial" needs a **crisp boundary**, not a blend - at 256x256 (and
   smaller once laid into the map), a soft gradient between lit and unlit
   reads as one smudged tile, not "half done". The placeholder's hard edge
   at 50% is standing in for that requirement, not inventing a new one.
2. The three states of every one of the twelve rooms must stay orderable
   dark-to-light with colour removed (40-art/06's own QA check 2) *and* the
   full set of twelve must still read as "mostly unfinished" or "mostly
   done" in one glance once laid out together (QA check 3) - the thing this
   whole task exists to make visible.

## The screen was unreachable until it was wired — 2026-08-28

Built, verified, and impossible to open. `HouseMapView.Requested` reads a
`housemap.txt` flag exactly as `CoatGridView.Requested` does, and its author
concluded no wiring was needed because the pattern was already "present in
GameBoot". It is present there in the sense that **GameBoot is what asks**:
`CoatGridView` is reached because `OnEnable` tests its `Requested` property and
adds the component. Nothing tested this one, so the property was a door with no
corridor leading to it.

Found by running the build rather than by reading it: the flag was on the
device, the screen never appeared, and the board came up instead. Two builds
were spent before the cause was looked for in the right place.

**The coordination error was mine.** The author was told not to touch
`GameBoot.cs` because another agent held it, and was invited to report any line
it needed instead — which it did, saying none was needed. A file boundary drawn
to prevent a collision also prevented the one line that made the work usable,
and neither side noticed because the screen looked complete from every angle
except running it.

One branch added to `GameBoot.OnEnable`, beside the other three.

## Run with the real art, and what three builds fixed — 2026-08-28

`android-house-map-real-art.png` is the screen on the emulator at 1080×2340
with the delivered art. Twelve rooms inside the house, numbered, all in the
dirty state because the save is fresh, legend wrapping, nothing off-screen.
`android-before-layout-fix.png` is the first run, kept for the comparison.

Three defects, none of which any amount of reading would have found:

**The screen could not be opened at all.** Recorded above — no branch in
`GameBoot` asked for it.

**The layout was written in points.** 480×420 for the house and 440 for the
grid were chosen against placeholder cells. With the real 928×1664 background
the house drew over the grid and the outer columns ran off both edges. Now
everything is a percentage of the panel, so it fits whatever screen it lands
on rather than the one it was written on.

**Percentage heights collapsed the cells to thumbnails.** A child's percentage
height resolves against its parent, and the grid had no height of its own, so
twelve rooms rendered a few pixels tall. Giving the grid an explicit height
fixed it. This is the failure that looks most like it worked: the screen drew,
the cells were there, and they were the wrong size by two orders of magnitude.

**And one thing that was measured rather than guessed:** the delivered
`map_background.png` is opaque with a white surround — 255,255,254 at three
corners. The dark page it was drawn on framed the house in a white rectangle,
so the page now matches the image instead of fighting it.

## What still looks unfinished, said plainly

- **Two columns, not three.** The grid is narrower than three cells plus their
  margins, so twelve rooms stack as 2×6. In a house this tall that reads fine,
  and it was not worth a fourth build to change — but it is an accident rather
  than a decision, and whoever gives this screen a real navigation entry should
  decide it on purpose.
- **Rooms 1 and 2 sit slightly over the roofline.** Cosmetic.
- **The white surround is still visible** as a panel behind the house. Matching
  the page to it hid the contrast, not the rectangle. The real fix is art with
  transparency or a background sized to the house itself.

None of the three blocks the screen from doing its job, which is showing twelve
rooms in three states derived from one source.
