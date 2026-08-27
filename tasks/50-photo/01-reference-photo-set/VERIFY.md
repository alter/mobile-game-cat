# Independent verification, 2026-08-27

**Verifier:** fresh context, wrote none of `tools/photoset/build_reference_set.py`,
`manifest.json`, or the images. No build/adb/emulator. Did not download the
set — checked only what's on disk, the manifest, the builder, and whether the
named sources resolve live.

## Per-item verdict

| # | Item | Result |
|---|---|---|
| VERIFY 1 | exactly 41 files | **pass** — 41 `.jpg` + `manifest.json` |
| VERIFY 2 | 20+5+5+5+3+3=41 by filename | **pass**, matches `by_category` and `missing: {}` |
| VERIFY 3 | manual spot-check | **pass** — viewed `empty_01` (mirror reflection, no cat/dog), `dog_01` (dog, no cat), `ofphoto_02`/`ofphoto_03` (both show a clear screen bezel/edge) |
| Reproducibility | builder + manifest sufficient, sources resolve | **pass, and integrity-checked**: all 41 on-disk files' sha256 match the manifest exactly; both HF dataset parquet indices (`microsoft/cats_vs_dogs`, `rafaelpadilla/coco2017`) resolve live with the exact splits the builder reads |
| `05-vision-plugin` denominators | 18/20 cats, 5/5 dogs | **match** — manifest `by_category` is cat:20, dog:5, identical to the table |
| Licences | do they permit this use | **not established — real gap** |

## Findings

- **Licensing is unresolved, not confirmed.** HF's own card for
  `microsoft/cats_vs_dogs` lists **"License: unknown."** `rafaelpadilla/coco2017`'s
  card licenses only the *annotations* (CC-BY-4.0); the images are COCO's
  original Flickr-sourced photos, each under its own uploader's licence, not
  covered by that grant. Project's own text (`.gitignore`, `NOTES.md`) says
  "under their own licences" but never names or checks one. Mitigated in
  practice — gitignored, never committed, never shipped, used only for
  local accuracy measurement — but "the licences let these images live here"
  is asserted nowhere, and now checked, not just assumed.
- **`NOTES.md` contradicts its own title**: header says "41 images," body
  says a rebuild "produces the same **38** files" — leftover from before the
  count widened. The builder's own module docstring still says "shoot **two**
  by hand" for `ofphoto`, while `WANTED["ofphoto"] = 3` a few lines below.

## How to reproduce

```bash
ls fixtures/reference-photos/*.jpg | wc -l   # 41
python3 -c "import json;d=json.load(open('fixtures/reference-photos/manifest.json'));print(d['by_category'],d['missing'])"
python3 -c "
import json,hashlib,os
d=json.load(open('fixtures/reference-photos/manifest.json'))
bad=[f['file'] for f in d['files'] if hashlib.sha256(open(f'fixtures/reference-photos/{f[\"file\"]}','rb').read()).hexdigest()!=f['sha256']]
print('hash mismatches:', bad)"
curl -s https://huggingface.co/api/datasets/microsoft/cats_vs_dogs/parquet | head -c 100
curl -s https://huggingface.co/api/datasets/rafaelpadilla/coco2017/parquet | grep -o '"val"'
grep -n "38 files\|41 images" tasks/50-photo/01-reference-photo-set/NOTES.md
grep -n "Shoot two by hand" tools/photoset/build_reference_set.py
```

## What was not checked

- Did not download/rebuild the set (constraint); resolution checked at the
  parquet-index level only.
- Did not view all 41 images, only a spot sample (matches task's own VERIFY 3
  scope, not exhaustive).
- Whether COCO's individual Flickr image licences would actually forbid this
  specific use — not researched per-image; flagged as unresolved, not ruled
  either way.

## Verdict

`verify:failed`. The mechanical VERIFY items (count, composition,
reproducibility, downstream-consumer parity) all genuinely pass, checked, not
assumed. Failing on the licensing gap and the self-contradicting count in
`NOTES.md` — small to fix, but this task's whole point is provenance, and an
unresolved "License: unknown" plus a document that disagrees with itself
about the headline number are exactly the kind of thing that shouldn't ship
as `status:done`.
