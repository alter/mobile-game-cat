# The set, 2026-08-26 — 38 of 40 built, 2 left to shoot

`python tools/photoset/build_reference_set.py` writes
`fixtures/reference-photos/` and its `manifest.json`. Rebuilt from scratch it
produces the same 38 files, because every image is addressed by (dataset,
split, row index) rather than by a URL.

| category | asked | built | source |
|---|---|---|---|
| cat | 20 | 20 | `microsoft/cats_vs_dogs` |
| dog | 5 | 5 | `microsoft/cats_vs_dogs` |
| blurry | 5 | 5 | the blurriest of 60 downloaded cats |
| empty | 5 | 5 | `rafaelpadilla/coco2017` val, no animal annotated |
| multi | 3 | 3 | same, two separated cat boxes |
| **ofphoto** | **2** | **0** | **nothing public contains these — shoot them** |

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

## What is left for a person

Two photographs **of a photograph** — a cat on a screen and a cat on a print.
They exist in this task precisely because no public set contains them: Vision's
behaviour on photos-of-photos is undocumented (`knowledge/ios/
03-vision-animal-recognition.md`), which is what these two images are for. Five
minutes with a phone.

Worth adding beyond the forty: **three to five of your own cat photos**, shot
on the phone this game will run on. If the pipeline works on shelter photos and
falls over on your own camera roll, that is better learned now.

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
