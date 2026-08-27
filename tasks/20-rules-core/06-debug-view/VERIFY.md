# VERIFY — 20-rules-core/06-debug-view

Result: **pending** — not passed, not failed. Both VERIFY items are human
observations, neither has been performed, and this verifier is barred from
standing in for them.

Verifier: an independent agent context, 2026-08-26, against `dev` at commit
`27f9904`. It did **not** write `game/Assets/View/DebugGameView.cs`,
`DebugGame.uxml` or `DebugGame.uss`, and changed none of them. It did **not**
play the game, did **not** run a build on a phone, did **not** show ten tiles to
anybody, and did **not** ask anyone to name them. Its only writes were this file
and `labels.txt`.

## Why the label was moved back to `pending`

`labels.txt` in this directory carried `verify:passed` as an uncommitted change
(`git diff` shows `-verify:pending` / `+verify:passed`) with no `VERIFY.md`
beside it. `tasks/README.md`, "The verify:passed rule", forbids that.

More to the point, this task is `ROLE: VIEW, HUMAN` with `VERIFY (HUMAN)`, and
`tasks/README.md`, "The independence rule", says of such tasks: "substitution is
impossible in principle: an agent almost always reports that the result looks
good. Such tasks are not performed and not simulated. They are handed to a
person with a precise statement of what is needed."

So `passed` would be a fabrication and `failed` would be equally false — nobody
has tried and reported a problem. `pending` is the accurate state, and it is
what the committed tree already said.

## What is needed from a person

1. **One level played through on a phone.** Not the editor, not the macOS
   player, not a screenshot: a build on a handset, one level from first tile to
   the win or jam screen. Report the device, and whether anything was unreadable
   or untappable at real size.
2. **Ten kinds named as ten different things, by someone who did not build it.**
   Show the ten tiles side by side at tile size to a person who has not seen the
   code, and write down the ten names they give. Ten distinct names is a pass;
   any two called the same thing is a fail, and the pair should be recorded.

## Supporting facts, which are not a substitute for either item

- The shipped levels use exactly ten kinds, `prop_00` … `prop_09`, across 37
  level files (counted from `game/Assets/Resources/Levels/l*.json`; command in
  "How to reproduce").
- `DebugGameView.HueFor` (`game/Assets/View/DebugGameView.cs:28-38`) spaces hues
  by the golden angle, 137.508°, at fixed saturation 0.38 and value 0.62. For
  those ten kind ids the resulting hues are 0.00, 20.06, 52.52, 105.05, 137.51,
  157.57, 190.03, 242.56, 275.02 and 327.54 degrees; the smallest gap is 20.06°,
  between `prop_00`/`prop_08` and again between `prop_01`/`prop_09`.
  Whether a 20° hue gap at S=0.38 V=0.62 is *told apart at tile size on a
  phone*, by a person, is exactly what item 2 asks and this number does not
  answer.
- `game/Assets/View/DebugGameView.cs` is uncommitted work in progress right now
  (`git status --porcelain game/Assets/View/` shows ` M`), so the artefact a
  human would be asked to judge is still moving. That is a second reason not to
  close this now.

## How to reproduce

From a clean state — fresh clone, nothing exported by hand. These commands
reproduce the supporting facts above; they do not verify the task, which needs a
person and a handset.

```bash
git clone <repo-url> /tmp/verify-06 && cd /tmp/verify-06
git rev-parse --short HEAD          # expect 27f9904 for the numbers above

# the label state this file is about
git log -1 --format=%h -- tasks/20-rules-core/06-debug-view/labels.txt
cat tasks/20-rules-core/06-debug-view/labels.txt

# ten kinds across 37 level files, and the hues the debug view gives them
python3 - <<'EOF'
import json, glob
files = sorted(glob.glob("game/Assets/Resources/Levels/l*.json"))
kinds = sorted({e["kind"] for f in files for e in json.load(open(f))["pile"]})
print("level files:", len(files), "distinct kinds:", len(kinds), kinds)
def hue(k):
    d = "".join(c for c in k if c.isdigit())
    n = int(d) if d else 0
    return (n * 137.508) % 360.0
hs = sorted((hue(k), k) for k in kinds)
for h, k in hs: print(f"{k:9s} hue={h:7.2f}")
gaps = sorted(((hs[(i+1) % len(hs)][0] - hs[i][0]) % 360, hs[i][1], hs[(i+1) % len(hs)][1])
              for i in range(len(hs)))
print("smallest hue gaps:", [(round(g, 2), a, b) for g, a, b in gaps[:3]])
EOF
```

## Update, 2026-08-27: the colour argument above is obsolete

The art arrived, and the tiles are no longer coloured rectangles. Two of the
supporting facts above no longer describe the build:

- Kind ids are the prop names now — `prop_vase`, `prop_book` and so on, thirty
  of them, ten drawn per level — not `prop_00` … `prop_09`. The levels were
  regenerated (`tools/solver/generate.py`, `PROPS`), so a level file names the
  sprite the view loads and there is no table in between.
- `HueFor` is no longer what a player sees. `DebugGameView.MakeTile` paints the
  tile with `Resources.Load<Texture2D>($"Art/{kind}")`; the hue function is kept
  only as the fallback when a kind has no file, so a missing sprite is visible
  rather than invisible. The golden-angle hue arithmetic above therefore
  answers nothing about the current build, including the colour-vision concern
  under "What was not checked" — thirty drawn objects are told apart by shape.

Both human items still stand, and item 2 is now the easier question: ten
drawings of household objects, not ten shades of the same colour.

## What was not checked

- Item 1 in full: nothing was played, on any device or in any editor. No build
  was produced by this verifier.
- Item 2 in full: no tile was rendered and no person was asked to name anything.
  The hue arithmetic above was computed from the source, not sampled from
  pixels, and does not account for the tile size, the phone's display, ambient
  light, or colour-vision deficiency — under which a golden-angle hue ramp at
  fixed saturation is the least robust choice.
- Whether `DebugGameView` renders what `HueFor` returns. Only the colour
  function was read; `game/Assets/View/DebugGameView.cs:194` assigns it to
  `tile.style.backgroundColor`, but the layout, the blanks for hidden items, the
  shelf and the pile were not inspected, and the file is mid-edit.
- Whether the reachable-item and hidden-item rules are honoured on screen. The
  Core side is verified in `02-pile-occlusion/VERIFY.md`; the view side is not.
- `build/playtest/index.html`, the HTML prototype named in this task's CONTEXT,
  was not opened or compared against the Unity view.
