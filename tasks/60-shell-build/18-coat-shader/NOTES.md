# Half of it works, 2026-08-27

`game/Assets/View/CoatBuilder.cs` builds a coloured cat from one greyscale
silhouette. `CoatGridView` renders every coat colour against every state; drop
a `coat.txt` next to the save to see it. The grid was photographed on the
Android emulator and is the evidence for what follows.

## What ships

| step | what it does |
|---|---|
| `Reshape` | narrows the body 15% for state 1, widens 5% for state 3, head untouched |
| `Weather` | dulls the coat, coarsens it, dirties the underside — full for state 1, a trace for state 2 |
| `Tufts` | strands grown along the contour normal, in clumps |
| `Tint` | multiplies the coat colour into the greyscale, so the modelling survives |
| `Outline` | the canon rim the cats were delivered without |

Three states out of three files, six colours, no masks.

## Three things learned the hard way, each from a screenshot

**The textures are not readable and should stay that way.** `GetPixels32` threw
`texture data is either not readable`. Marking them Read/Write keeps a second
copy in memory for the whole run to serve one pass at load, so the pixels come
through a GPU blit into a temporary RenderTexture instead. Works on any
texture whatever its import settings, including files delivered later.

**Do not tint the shadow.** The cats are drawn with a soft semi-transparent
shadow beneath them. Tinting everything with alpha above zero turned it into a
coloured puddle — a yellow smear under a cream cat. Only pixels at alpha > 200
are coat; the rest keeps its own neutral colour, and the outline skips them too
so the shadow does not get a dark ring.

**Lightness cannot find eyes.** Eye colour was applied by tinting the darkest
pixels, and the first grid came back with amber smears under every sleeping
cat: the deep shadow beneath a curled body is darker than an eye. The tint is
removed. Every player's cat has the dark eyes the silhouette was drawn with
until `40-art/04` supplies an eyes mask — which is what that mask is for.

## Not done, and the task is not closed

- **`fur_length` is ignored.** The task requires it to pick the silhouette and
  to fall back to short-haired with one logged line when the long-haired file
  is missing. Nothing reads the field yet.
- **Mask mode is not written.** Only the no-masks path exists. The task asks
  for both, chosen at runtime by whether the mask texture loads.
- **`pattern` is not applied.** Every cat is solid. Procedural stripes over a
  body this size read as noise, and a wrong pattern is worse than none when the
  point is "that looks like my cat". This waits for the masks rather than being
  faked.
- **`white_markings` is not applied**, same reason.

So a player whose cat is a long-haired ginger tabby with white paws currently
gets a short-haired solid ginger. The colour is right and the rest is missing.
