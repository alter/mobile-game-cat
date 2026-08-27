Verifier: independent QA context. Wrote none of `prop_unknown.png`,
`prop_locked.png`, `DebugGameView.cs`, `DebugGame.uss`, `DECISIONS.md` D15,
or this task's own `task.txt`/`labels.txt`. Read every file directly and
measured the two images itself (Pillow, `.venv/bin/python3`) rather than
trusting `40-art/01-props/NOTES.md`'s numbers for these two files, even
though that note already covers them. Did not run a Unity build, PlayMode
test, adb, or emulator. Rendered the composites in item 3 itself, outside
Unity, and saved them into this task directory as evidence. Its only writes
are to this file, `labels.txt`, and the two new PNGs below.

## Verdict by item

| # | Question | Verdict | Evidence |
|---|---|---|---|
| 1 | Files present, 256×256, clean alpha, same family as the 30 props | **Pass** | Both files exist at `game/Assets/Resources/Art/`. Measured independently: `prop_unknown` 256×256 RGBA, alpha extrema (0,255), 0 halo pixels (alpha<16 & any channel>200), 37.8% of the canvas drawn on. `prop_locked` 256×256 RGBA, alpha extrema (0,255), 0 halo, 66.4% drawn. Sampled three of the 30 props for comparison: `prop_vase` 43.8%, `prop_book` 39.1%, `prop_ball` 58.4% drawn — the two new files sit inside that same range, not an outlier family. Independently confirms `40-art/01-props/NOTES.md`'s claim ("32 PNGs... every one 256×256 RGBA... none carries a halo") rather than repeating it on trust. |
| 2 | Was the task's text updated for D15, and is the original acceptance item satisfiable? | **Already updated, correctly — the rarer good outcome** | `task.txt`'s VERIFY item 3 already carries D15's finding verbatim, in the task's own words, not left stale: it states the original wording ("prop underneath still identifiable through the overlay") "Fails as an overlay, passes as a badge," gives the 2026-08-27 five-prop composite result, and names the exact CSS class (`.game__tile-lock`) that would need to change if a true overlay is ever drawn. This is the opposite finding from the sibling verification of `60-shell-build/07` earlier the same day, where `task.txt` was *not* updated after its underlying decision reversed — here it was. **Ruling on satisfiability, independent of the task's own framing:** the original item, read against the image that ships today (a solid rope coil, not a mesh with gaps), is **unsatisfiable as a full-tile overlay** — see item 3's render below, which shows this directly rather than describing it. It is **satisfiable, and satisfied, as a corner badge** — a different acceptance criterion than the one originally written, met by a different placement of the same image, not by a different image. |
| 3 | Composited myself, both ways — which does the evidence support? | **The corner badge, unambiguously — confirmed by direct render, not by re-reading D15's own account of it** | Rendered both over five props (`vase, book, ball, frame, scissors` — three of D15's own four examples plus two more): `verify-3a-full-tile-overlay.png` (prop_locked composited at native 256×256 directly over each prop, as the original VERIFY item 3 literally asks) and `verify-3b-corner-badge-as-shipped.png` (52px tile scaled ×4, 26px badge scaled ×4, positioned at `right:-2px; bottom:-2px` matching `DebugGame.uss` exactly, matching `DebugGameView.MakeTile`'s actual draw order). In (a), all five renders are visually identical — the rope coil fully replaces every prop; none of vase/book/ball/frame/scissors is identifiable. In (b), all five props remain fully identifiable with the rope-coil badge sitting cleanly in the corner. This matches D15's account exactly, but is now independently rendered rather than taken on the decision's own word. Measured why: `prop_locked`'s own alpha averages 62.3% across the full canvas (61.7% of the canvas is alpha>200, "opaque"), close to D15's cited 59% and confirming it by direct computation, not by re-quoting it — it is a solid object with transparency only around its own silhouette, not a sparse mesh with gaps to see through, so scaling it to fill a tile necessarily occludes whatever is under it. |
| 4 | Does `prop_unknown` actually reveal nothing? | **Pass, on direct visual inspection** | Viewed the file directly: a draped-cloth silhouette over a plain rounded mound, low detail, no visible edges, corners, handles, or other feature that would hint at a specific object among the 30 props (which include angular items — box, crate, frame, suitcase — and items with distinctive silhouettes — scissors, keys, a mitten). No face, no skull, matching the SCOPE's explicit exclusions. The shape is generic enough that it does not resemble any single one of the 30 props more than another. `prop_unknown` is also present in `game/Assets/Art/contact-sheet.png` (confirmed by viewing the sheet directly — it appears alongside the 30 props and `prop_locked`), so the artifact VERIFY item 2 needs already exists. |

**VERIFY item 2's actual acceptance ("viewer confirms 'something is under
there' without naming an object") is not ruled on here.** This is a
subjective read on a specific human's first impression, the same kind of
judgment `40-art/01-props/NOTES.md` explicitly declined to simulate for its
own items 2 and 3, citing `ROLES.md`: an agent reporting on its own
generated or reviewed art "almost always reports that the result looks
good." What this pass can and does say (item 4, above) is that nothing in
the image gives away a specific object — that is a necessary condition for
item 2 to pass, not a substitute for asking someone.

**Overall verdict: `verify:passed`.** Item 1 holds on direct, independent
measurement. Item 2 (documentation currency) is a genuine positive finding
— unlike the sibling task checked earlier the same day, this `task.txt` was
correctly updated when its underlying decision (D15) changed, and states
plainly which reading of its own original acceptance item holds and which
does not. Item 3's ruling — unsatisfiable as an overlay, satisfiable and
satisfied as a badge — is now backed by a render this pass produced itself,
not by trusting the account in `task.txt`/D15. Item 4 holds on direct
visual inspection. The one item genuinely left open, VERIFY item 2's
human read, is out of scope for an agent to close in principle, not a gap
in this task's work.

## How to reproduce

From the current tree, no exported variables:

```sh
.venv/bin/python3 -c "
from PIL import Image
for name in ('prop_unknown', 'prop_locked'):
    im = Image.open(f'game/Assets/Resources/Art/{name}.png')
    a = im.split()[-1]
    print(name, im.size, im.mode, a.getextrema())
"
# -> both: (256, 256) RGBA (0, 255)

grep -n 'game__tile-lock' -A6 game/Assets/View/DebugGame.uss
# -> width: 26px; height: 26px; right: -2px; bottom: -2px

sed -n '245,270p' game/Assets/View/DebugGameView.cs
# -> the badge, not the full tile, carries prop_locked; comment cites D15

grep -n 'Fails as an overlay, passes as a badge' tasks/40-art/02-prop-blockers/task.txt
# -> present: the task's own VERIFY item 3 already carries D15's finding
```

Composite render (produces the two evidence files in this directory):

```sh
.venv/bin/python3 << 'EOF'
from PIL import Image
ART = "game/Assets/Resources/Art"
props = ["prop_vase", "prop_book", "prop_ball", "prop_frame", "prop_scissors"]
lock = Image.open(f"{ART}/prop_locked.png").convert("RGBA")

# full-tile overlay, as the original VERIFY item 3 literally asks
canvas = Image.new("RGBA", (256*len(props), 256), (255,255,255,0))
for i, name in enumerate(props):
    prop = Image.open(f"{ART}/{name}.png").convert("RGBA")
    tile = Image.new("RGBA", (256,256), (201,169,124,255))
    tile.alpha_composite(prop); tile.alpha_composite(lock)
    canvas.paste(tile, (i*256, 0))
canvas.save("tasks/40-art/02-prop-blockers/verify-3a-full-tile-overlay.png")

# 26px corner badge, as DebugGame.uss/.game__tile-lock actually draws it
S=4; TILE=52*S; BADGE=26*S; OFF=2*S
canvas = Image.new("RGBA", ((TILE+20)*len(props), TILE+20), (255,255,255,0))
for i, name in enumerate(props):
    prop = Image.open(f"{ART}/{name}.png").convert("RGBA").resize((TILE,TILE), Image.LANCZOS)
    tile = Image.new("RGBA", (TILE,TILE), (201,169,124,255)); tile.alpha_composite(prop)
    badge = lock.resize((BADGE,BADGE), Image.LANCZOS)
    tile.alpha_composite(badge, (TILE-BADGE+OFF, TILE-BADGE+OFF))
    canvas.paste(tile, (i*(TILE+20), 0))
canvas.save("tasks/40-art/02-prop-blockers/verify-3b-corner-badge-as-shipped.png")
EOF
```

## What was not checked

- VERIFY item 2's actual human-perception acceptance — deliberately not
  performed or simulated, per the reasoning above. Needs a person to look
  at `contact-sheet.png` and answer, in their own words, whether "something
  is under there" reads without naming an object.
- No Unity build, PlayMode test, adb, or emulator — the corner-badge render
  in item 3 reproduces `DebugGame.uss`'s declared geometry and
  `DebugGameView.MakeTile`'s draw order by hand, in Pillow; it was not
  captured from a running scene.
- Whether any of the other 29 props (beyond the five composited) would
  fare differently under the full-tile overlay was not individually
  re-rendered — D15's own account composited five and found the same
  result each time (the coil fully occludes regardless of what is under
  it, since it is opaque across ~62% of a canvas the prop must share), and
  the mechanism (a solid object pasted over another) does not depend on
  which prop is underneath, so this was judged not to need all 30 to be
  conclusive.
- Colour accuracy / whether `prop_locked`'s coil colour reads as "rope" and
  not something else was not separately assessed — out of scope for what
  this VERIFY item asks.
