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

---

# Delivered 2026-08-28 — what arrived and what is still open

105 files. Checked by measurement before being placed, not by reading the
delivery note.

| group | files | size | alpha | placed |
|---|---|---|---|---|
| rooms | 24 (12 pairs) | 1856×3328 | opaque, correct for a background | `Resources/Art/` |
| house map | 37 (background + 12×3) | 928×1664 / 256×256 | opaque | `Resources/Art/` |
| icons | 5 | 1328×1328 | opaque | `Assets/Art/icons/` — a player setting, not a runtime load |
| share frame | 1 | 1328×1328 | opaque | `Assets/Art/` — P2, D8 |
| cats | 3 | 1024×1024 | yes | **already in the repo, byte-identical** |
| props | 32 | 256×256 | yes | **already in the repo, byte-identical** |
| rewards | 2 | 1328×1328 | **no** | held in `Assets/Art/rewards-pending/`, see its README |

**Rooms came in larger than specified and that is the right direction.**
`art-brief.md` §5 worked out 1536×3072 as the next power of two above the worst
case (1320×2868 on an iPhone 16 Pro Max). 1856×3328 clears it with room to
spare. Nothing to fix.

**The map names matched the code exactly** — `map_background` and
`map_room_<nn>_<state>` — so `View/HouseMapView`, written hours earlier against
nothing, loads the real art with no change at all. That is what the fallback
was for.

**The two rewards are the one defect** and it is a cut-out problem rather than a
drawing one: fully opaque, so they would paste a white square over a room. Held
out of `Resources/` on purpose. `game/Assets/Art/rewards-pending/README.md`
carries the measurement and what would finish them.

## What the delivery settled about the cat, which was the open question

The delivery note asks the same question the project had been circling: are the
coat masks needed? **No.** `View/CoatMasks` derives all nine from the silhouette
at load time, and the note's own alternative — "by hand or by code over the
finished base" — is the second one, already built and already looked at.

Three of the note's requirements turned out to be already satisfied, arrived at
independently:

| the delivery asks | what the code does |
|---|---|
| outline not baked, applied over the tinted coat | `CoatBuilder.Outline`, 1.6% of width |
| long fur by growing the silhouette, not texture | `CoatBuilder.Tufts`, strands along the contour normal |
| ginger darkened about a quarter | **was not done** — corrected 28.08 to 186,108,52 from the delivery's measurement |

And it settles the long-haired set: the note says three attempts failed and
suggests that a shader approach makes the second silhouette set unnecessary.
That approach is what is running. **Six silhouettes are not needed; three are
enough.** `40-art/03`'s outstanding count of "3 of 6" should be read as complete
unless someone decides tufts are not good enough on a device.
