# The set, 2026-08-26 — complete, 41 images

`python tools/photoset/build_reference_set.py` writes
`fixtures/reference-photos/` and its `manifest.json`. Rebuilt from scratch it
produces the same 41 files, because every image is addressed by (dataset,
split, row index) rather than by a URL.

**Corrected 2026-08-27.** This sentence said "38" — a leftover from before
the set widened (task.txt's amendment: 40 files and two `ofphoto` shots
became 41 and three). 41 is the number actually on disk: counted directly
(`ls fixtures/reference-photos/*.jpg`), matched against `manifest.json`'s own
`images` field and `by_category` sum, and confirmed by an independent
`VERIFY.md` that hashed all 41 files against the manifest with zero
mismatches.

| category | asked | built | source |
|---|---|---|---|
| cat | 20 | 20 | `microsoft/cats_vs_dogs` |
| dog | 5 | 5 | `microsoft/cats_vs_dogs` |
| blurry | 5 | 5 | the blurriest of 60 downloaded cats |
| empty | 5 | 5 | `rafaelpadilla/coco2017` val, no animal annotated |
| multi | 3 | 3 | same, two separated cat boxes |
| ofphoto | 3 | 3 | shot by the owner — see below |

## Sources and why these

`microsoft/cats_vs_dogs` is the Asirra set: photographs supplied by Petfinder
shelters. Phone cameras, bad light, noise, animals in cages and on laps — the
closest public stand-in for what a player will actually upload. Flickr-derived
sets were avoided for the opposite reason: they are *photographs*, taken with
intent and a real camera, which is not what arrives from a phone.

`rafaelpadilla/coco2017` has per-object boxes, so "two or more cats" and "no
animal" are selected by annotation rather than by eye.

Both predate 2022 and state their origin, per the no-synthetic rule.

## Blur is a number here, not a judgement

There is no dataset of blurry cats and looking for one would be a waste. Every
downloaded cat is scored by the variance of its Laplacian and the five lowest
become the blurry category. The split is wide: blurry images score 15–42,
the median sharp cat scores **946** — a factor of twenty, not a borderline
call. Each score is in the manifest, so "how blurry" is answerable later.

## Two things the annotation could not decide, decided by looking

- **"No animal" first meant "no cat and no dog"**, which let through a horse on
  a hillside and sheep under a tree. Fixed to exclude every COCO animal class.
- **Two cat boxes are not two cats.** A cat facing a mirror is annotated twice,
  with well-separated boxes, and no rule over the annotation distinguishes that
  from a second animal. Three such rows are listed in `MULTI_REJECT` with the
  reason for each, so the rejection is reproducible instead of hand-picked.

Both were caught by rendering the set as a contact sheet and looking at it. A
fixture that nobody has looked at is not a fixture.

## The three photographs of a photograph, and why there are three

Supplied by the owner, the same cat off a screen, one file per capture mode:

| file | mode | what it adds |
|---|---|---|
| `ofphoto_01.jpg` | ordinary photo | the plain case; no screen edge in frame |
| `ofphoto_02.jpg` | portrait mode | the phone blurs the background *before* Vision sees anything |
| `ofphoto_03.jpg` | frame lifted from video | compressed, lower resolution, 960×1280 |

The task originally asked for two, and the count was widened to three rather
than picking two of them, because these are three different inputs and not
three copies of one. Portrait mode is the interesting case: depth-of-field is
applied on device, so the pipeline receives an image that has already been
altered by something we do not control. A frame from video is the other end —
compression artefacts and less detail. Discarding either would leave that mode
untested, and the whole point of this fixture is to find out what Vision does
with cases nobody documented.

`ofphoto_01` shows no screen edge, so VERIFY item 3 is only satisfied by the
other two. That is deliberate: an upload with no visible frame is exactly the
hard case for "is this a live animal or a picture of one".

No photograph of a *print* yet. Paper produces different artefacts from a
screen — no moiré, no backlight, but paper texture and reflections. Worth one
more shot if a printer is at hand; not blocking.

Still worth adding beyond the set: **three to five ordinary photos of your own
cat**, shot on the phone this game will run on, as `own_*.jpg`. If the pipeline
works on shelter photos and falls over on your own camera roll, that is better
learned now. Those files are picked up automatically and versioned in git,
since no script can rebuild them.

## Not committed, deliberately

The images come from third-party datasets under their own licences, so
`fixtures/reference-photos/*.jpg` is gitignored; the manifest and the builder
are versioned. `python tools/photoset/build_reference_set.py` restores the
folder — 2.4 MB, about a minute, no API keys.

The Hugging Face rows API was the obvious way to fetch these and is unusable:
it answers 429 to nearly every anonymous call regardless of pacing. The builder
reads the datasets' parquet files over HTTP range requests instead, pulling
only the row groups it needs — a few megabytes out of 330, and no request
budget at all.

## The licence question, 2026-08-27

"Under their own licences," above, was never actually checked. An
independent `VERIFY.md` in this directory did: `microsoft/cats_vs_dogs`'s
Hugging Face card lists "License: unknown," and `rafaelpadilla/coco2017`
licenses only its annotations, not the underlying Flickr-sourced images.
Full sourcing, what that does and doesn't permit, what depends on it, and
why it blocks nothing today: `tasks/00-validate-demand/01-market-scan/legal-risk.md`
§5. Not fixed here because it isn't a code question — it's what a person
decides if and when an image from this set is ever shown outside the
project.
