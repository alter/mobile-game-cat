# What art is missing, and exactly what stops without it — 2026-08-28

Written because the owner said images are coming and there was no single place
saying which ones matter, in what order, and what each one unblocks. Sizes and
pivots are `art-brief.md` section 5; nothing here restates a requirement it
does not already carry.

## What exists

| group | on disk | state |
|---|---|---|
| pile items | 30 files, 256×256 | done, verified — all RGBA, real alpha, no halo |
| blank tile + locked item | 2 files, 256×256 | done, verified, both composited and looked at |
| cat silhouettes | **3 of 6**, 1024×1024 | short-haired only; greyscale confirmed by measurement |

That is everything. Three groups of eleven.

## What is missing, ordered by what it blocks

### 1. Rooms — 12 dirty/clean pairs, 1536×3072 (task `40-art/07`, P1)

**The largest hole, and it is misfiled at P1.** Two P0 tasks are built around
art that does not exist:

- `60-shell-build/02-room-piles` — clearing a pile removes that corner's
  clutter from the dirty background, and the last pile swaps the whole
  background to clean. There is nothing to swap.
- `60-shell-build/06-win-screen` — shows the room's transformation at the
  moment it lands. That transformation *is* the pair of images. Without them
  the win screen can only describe what a player should be feeling.

`cat-shelter-mvp.md` calls the before/after pair the game's actual pitch and
the eight-second reel. It is the one thing a creative for gate 1 would be built
from. A P1 label under two P0 dependants is a mislabel, not a judgement — worth
the owner's eye.

`art-brief.md` section 8: same room, two lightings, no new furniture; the
clutter is the same 30 props laid over the background. So the *pair* is one
room photographed twice, not two drawings — which is what makes 12 pairs
tractable at all.

### 2. House map — background plus 12 cells in three states (task `40-art/06`, P0)

Blocks `60-shell-build/03-house-map`, which is being built now against
placeholder cells. Section 9's requirement is the load-bearing one: the three
states must differ **at a glance, not by shade**. Three tints of one colour
would pass a technical check and fail the only thing the screen is for.

### 3. The other three cat silhouettes — long-haired, 1024×1024 (task `40-art/03`)

`View/CoatBuilder.LoadBase` already falls back to the short-haired base and
logs once, so a long-haired cat renders rather than breaking. What a player
whose photo says "long" gets today is a short-haired cat. Not a crash; a quiet
lie about her own cat, which is the one thing this game promises not to tell.

### 4. App icon — 5 files, 1024×1024 (task `40-art/05`, P0)

Blocks nothing that runs, blocks everything that ships. No store submission,
no TestFlight build that looks finished, no creative.

### 5. Reward items — bowl and blanket, 256×256 (task `40-art/08`, P1)

Blocks `60-shell-build/05-rewards`. Not on any gate's path.

### 6. Cat layer masks (task `40-art/04`, now P2)

Downgraded on 2026-08-27 because `View/CoatMasks` derives all nine masks from
the silhouette at runtime and a drawn file overrides a computed one with no
code change. Read that task's NOTES before drawing anything: the masks worth
drawing are the markings on the sitting and sleeping cats, and **not** the
stripes, which were predicted to fail and do not.

### 7. Share-frame (task `40-art/09`, P2)

Post-gate-3 by D8's own placement.

## The one thing worth deciding before any of it is made

`40-art/03`'s NOTES calls the cat "the task the executor can fail outright" and
sets a rule: two attempts, then hire an artist. That rule was written for the
cat because a player looks at the cat closely. **Rooms have the opposite
property** — they are a background seen at a glance, twice, and the pair only
has to differ obviously. If generation is going to be tried anywhere, rooms are
where it is most likely to work and least likely to be noticed if it is
mediocre.

Nothing here should be generated without the owner asking. This file exists so
that when images arrive, the order is already decided and nobody spends a day
drawing the P2 set.
