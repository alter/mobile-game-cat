# Four branches, and the threshold picked from data — 2026-08-26

`Core/PhotoJudge` decides; `Shell/PhotoMessages` words it. The split is
deliberate: which branch a photo falls into is a rule and is tested, while the
wording is copy that `12-copy-english` will rewrite and
`16-localisation-ready` will move into a table.

| outcome | when | what the player is told |
|---|---|---|
| `NoAnimal` | nothing detected, or an identifier Vision should never return | "No cat in this one. Try a photo where she fills more of the frame." |
| `Dog` | identifier `Dog` | "That looks like a dog. Lovely, but this shelter is for cats." |
| `UnclearCat` | `Cat` below the threshold | "A cat, but too blurry to copy her colours. One more, holding still?" |
| `Cat` | `Cat` at or above it | "Got her." → crop, then the model call |

Nothing can miss all four: the identifier is either empty, `Dog`, `Cat`, or
something Vision does not produce — and that last case is routed to `NoAnimal`
rather than guessed, because a species we do not recognise must never be
accepted as a cat.

## The threshold is 0.60, and it is measured

Apple publishes no recommended value, so this one comes from the 41 photographs
in `05-vision-plugin/NOTES.md`. Confidence on genuine cats runs **0.60 to
0.81**, and the four lowest — 0.60, 0.60, 0.60, 0.61 — are ordinary photographs
of ordinary cats. Anything above 0.60 starts rejecting cats that plainly are
cats, so the floor sits exactly at the bottom of the observed range. A test
asserts that equality, so re-measuring on new data forces the constant to move
with it.

**What the threshold cannot do**, and does not pretend to: separate a live cat
from a photograph of one on a screen. Those measured 0.62 and 0.64 — inside the
range, above four genuine cats. It is in the tests as a fact about the product,
not as a defect.

## Against the VERIFY list

Run over the measured results for all 41 images (inlined into the test, since
the photographs themselves are third-party and gitignored):

- **5 of 5 dogs** → `Dog`. **5 of 5 empty frames** → `NoAnimal`. Both exact.
- **Blurry and multi-cat: all handled**, none unclassified — 4 blurry and all
  3 multi accepted as `Cat`, one blurry falls to `NoAnimal`.
- **Cats: 18 accepted, not the 20 VERIFY 2 asks for.** The other two were never
  detected by Vision at all — `cat_10` is the smallest image in the set
  (259×270) and `cat_20` has two kittens filling the frame — so they arrive
  here as "nothing found" and the judge cannot accept what was not seen. This
  is a limit of stage one, not of the branching, and it is pinned by a test
  rather than rounded up.

99 C# tests (84 → 99).
