"""Task 50-photo/01: assemble the 40-image reference set from open datasets.

Sources, chosen for provenance rather than size:

* `microsoft/cats_vs_dogs` — the Asirra set, photographs supplied by Petfinder
  shelters. Phone cameras, bad light, noise: the closest public stand-in for
  what a player will actually upload. Cats and dogs come from here.
* `rafaelpadilla/coco2017` (val) — per-object boxes, so "two or more cats in
  frame" and "no animal at all" are selected by annotation, not by eye.

Both are pre-2022 with a stated origin, per the no-synthetic rule in the task.

Blurry images are not a separate source. Every downloaded cat is scored by the
variance of its Laplacian and the lowest scorers become the blurry category —
so the amount of blur is a number in the manifest, not an opinion.

What this cannot fetch: the two photographs-of-a-photograph. No public set
contains them, because they exist in the task precisely to find out what Vision
does with a case nobody has documented. Shoot two by hand — a cat on a screen,
a cat on a print.

Images are written to the output folder and listed in `manifest.json` with the
dataset, the row index, the SHA-256 and the sharpness score. The download URLs
themselves are signed and expire, so the manifest records (dataset, split, row)
— the stable coordinates a third party can re-fetch from.
"""
from __future__ import annotations

import argparse
import hashlib
import io
import json
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path

import fsspec
import numpy as np
import pyarrow.parquet as pq
from PIL import Image

PARQUET_INDEX = "https://huggingface.co/api/datasets/{}/parquet"
CATS_VS_DOGS = ("microsoft/cats_vs_dogs", "default", "train")
COCO = ("rafaelpadilla/coco2017", "default", "val")
COCO_CAT = 17
# every animal class in COCO's 91-category numbering: "no animal" has to mean
# no animal, not merely no cat and no dog — a horse on a hillside still gives
# Vision something to find.
COCO_ANIMALS = frozenset({16, 17, 18, 19, 20, 21, 22, 23, 24, 25})

# COCO rows that pass every automatic test and still are not two cats. A cat
# facing a mirror is annotated twice with well-separated boxes, and no rule
# over the annotation can tell that from a second animal — these were rejected
# by looking at them. Listed rather than hand-picked so the build stays
# reproducible.
MULTI_REJECT = frozenset({
    504,    # cat and its reflection in a round mirror
    1437,   # cat at a window, the second box is glass, not an animal
    4370,   # one cat plus a person; the second box is unreadable
})

# category -> how many images the task asks for
WANTED = {
    "cat": 20,
    "dog": 5,
    "empty": 5,
    "blurry": 5,
    "multi": 3,
    # Three, not the two the task first asked for: the same cat photographed
    # off a screen in ordinary mode, in portrait mode (which blurs the
    # background before Vision ever sees it) and as a frame lifted from video
    # (compressed, lower resolution). Three capture modes, three different
    # artefacts — dropping one leaves that mode untested.
    "ofphoto": 3,
}
PAGE = 100


def row_groups(dataset: str, config: str, split: str, columns: list[str]):
    """Yield (row_index, row) reading one parquet row group at a time.

    The rows API would be simpler, but it rate-limits anonymous callers to the
    point of uselessness — 429 on nearly every call, whatever the pacing.
    Parquet over HTTP range requests fetches only the groups actually read, so
    a 330 MB file costs a few megabytes here and no request budget at all.
    """
    with urllib.request.urlopen(
            PARQUET_INDEX.format(dataset), timeout=60) as response:
        shards = json.load(response)[config][split]
    # cats_vs_dogs is sorted by label: every cat sits in shard 0 and every dog
    # in shard 1, so stopping at the first shard finds no dogs at all.
    index = 0
    for url in shards:
        parquet = pq.ParquetFile(fsspec.open(url, "rb").open())
        for group in range(parquet.metadata.num_row_groups):
            for row in parquet.read_row_group(group, columns=columns).to_pylist():
                yield index, row
                index += 1


def sharpness(data: bytes) -> float:
    """Variance of the Laplacian — the standard blur score. Higher is sharper."""
    image = Image.open(io.BytesIO(data)).convert("L")
    image.thumbnail((512, 512))
    pixels = np.asarray(image, dtype=np.float64)
    laplacian = (
        -4 * pixels[1:-1, 1:-1]
        + pixels[:-2, 1:-1] + pixels[2:, 1:-1]
        + pixels[1:-1, :-2] + pixels[1:-1, 2:]
    )
    return float(laplacian.var())


def _candidate(data: bytes, category: str, dataset: str, split: str,
               row_idx: int) -> dict | None:
    try:
        image = Image.open(io.BytesIO(data))
        image.verify()
        width, height = Image.open(io.BytesIO(data)).size
    except Exception:
        return None                      # broken file in the source set
    if min(width, height) < 200:
        return None
    return {
        "category": category, "dataset": dataset, "split": split,
        "row": row_idx, "width": width, "height": height,
        "sha256": hashlib.sha256(data).hexdigest(),
        "sharpness": round(sharpness(data), 1),
        "_bytes": data,
    }


def collect_cats_and_dogs(cat_pool: int, dogs: int) -> tuple[list[dict], list[dict]]:
    dataset, config, split = CATS_VS_DOGS
    cats: list[dict] = []
    found: list[dict] = []
    for index, row in row_groups(dataset, config, split, ["image", "labels"]):
        is_cat = row["labels"] == 0
        if is_cat and len(cats) >= cat_pool:
            continue
        if not is_cat and len(found) >= dogs:
            continue
        item = _candidate(row["image"]["bytes"], "cat" if is_cat else "dog",
                          dataset, split, index)
        if item is None:
            continue
        (cats if is_cat else found).append(item)
        if len(cats) >= cat_pool and len(found) >= dogs:
            break
    print(f"  {len(cats)} cats, {len(found)} dogs")
    return cats, found


def _boxes_overlap(a: list[float], b: list[float]) -> float:
    """Intersection over the smaller box, for COCO xywh boxes."""
    ax, ay, aw, ah = a
    bx, by, bw, bh = b
    ix = max(0.0, min(ax + aw, bx + bw) - max(ax, bx))
    iy = max(0.0, min(ay + ah, by + bh) - max(ay, by))
    smaller = min(aw * ah, bw * bh)
    return (ix * iy) / smaller if smaller > 0 else 0.0


def collect_from_coco(multi: int, empty: int) -> tuple[list[dict], list[dict]]:
    dataset, config, split = COCO
    multis: list[dict] = []
    empties: list[dict] = []
    for index, row in row_groups(dataset, config, split, ["image", "objects"]):
        labels = row["objects"]["label"]
        boxes = [b for b, l in zip(row["objects"]["bbox"], labels) if l == COCO_CAT]
        has_animal = any(label in COCO_ANIMALS for label in labels)

        if index in MULTI_REJECT:
            continue
        if len(boxes) >= 2 and len(multis) < multi:
            # Two boxes are not two cats: a cat facing a mirror is annotated
            # twice, and half the "multi" candidates turned out to be that.
            # Require separated boxes, each big enough to read as an animal.
            areas = [w * h for _, _, w, h in boxes]
            separated = all(
                _boxes_overlap(boxes[i], boxes[j]) < 0.15
                for i in range(len(boxes)) for j in range(i + 1, len(boxes)))
            if not (separated and min(areas) > 4000):
                continue
            category = "multi"
        elif not has_animal and len(empties) < empty:
            category = "empty"
        else:
            continue

        item = _candidate(row["image"]["bytes"], category, dataset, split, index)
        if item is None:
            continue
        (multis if category == "multi" else empties).append(item)
        if len(multis) >= multi and len(empties) >= empty:
            break
    print(f"  {len(multis)} multi-cat, {len(empties)} empty")
    return multis, empties


def build(out_dir: str, cat_pool: int = 60) -> dict:
    out = Path(out_dir)
    out.mkdir(parents=True, exist_ok=True)

    print("cats_vs_dogs (Petfinder shelters via Asirra):")
    cats, dogs = collect_cats_and_dogs(cat_pool, WANTED["dog"])
    print("coco2017 val (by annotation):")
    multis, empties = collect_from_coco(WANTED["multi"], WANTED["empty"])

    # The blurriest cats become the blurry category; the sharpest fill "cat".
    cats.sort(key=lambda item: item["sharpness"])
    blurry = cats[:WANTED["blurry"]]
    for item in blurry:
        item["category"] = "blurry"
    plain = cats[WANTED["blurry"]:][-WANTED["cat"]:]

    manifest = []
    # Hand-shot files (ofphoto_*, own_*) are dropped into the folder by a
    # person and cannot be re-fetched. Pick them up so a rebuild neither
    # deletes them nor writes a manifest that pretends they are absent.
    for path in sorted(out.glob("*.jpg")):
        prefix = path.stem.rsplit("_", 1)[0]
        if prefix not in ("ofphoto", "own"):
            continue
        data = path.read_bytes()
        manifest.append({
            "file": path.name, "category": prefix, "dataset": "hand-shot",
            "split": "-", "row": -1,
            "width": Image.open(path).width, "height": Image.open(path).height,
            "sha256": hashlib.sha256(data).hexdigest(),
            "sharpness": round(sharpness(data), 1),
        })

    for group in (plain, dogs, empties, blurry, multis):
        for index, item in enumerate(group, start=1):
            name = f"{item['category']}_{index:02d}.jpg"
            (out / name).write_bytes(item.pop("_bytes"))
            manifest.append({"file": name} | item)

    missing = {c: n for c, n in WANTED.items()
               if sum(1 for m in manifest if m["category"] == c) < n}
    report = {
        "images": len(manifest),
        "by_category": {c: sum(1 for m in manifest if m["category"] == c)
                        for c in WANTED},
        "missing": missing,
        "blur_threshold": round(blurry[-1]["sharpness"], 1) if blurry else None,
        "sharp_median": round(float(np.median([m["sharpness"] for m in plain])), 1)
                        if plain else None,
        "sources": {"cats_and_dogs": "/".join(CATS_VS_DOGS),
                    "multi_and_empty": "/".join(COCO)},
        "files": manifest,
    }
    (out / "manifest.json").write_text(json.dumps(report, indent=2) + "\n")
    return report


def main() -> None:
    parser = argparse.ArgumentParser(description="Build the 40-image fixture")
    parser.add_argument("--out", default="fixtures/reference-photos")
    parser.add_argument("--cat-pool", type=int, default=60,
                        help="cats downloaded before the blurry ones are split off")
    args = parser.parse_args()
    report = build(args.out, args.cat_pool)
    print(json.dumps({k: v for k, v in report.items() if k != "files"}, indent=2))


if __name__ == "__main__":
    main()
