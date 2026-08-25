"""Assemble ready-to-paste generation prompts from art-prompts.md.

Nobody should be gluing three fragments together by hand fifty-four times: that
is where a Cyrillic placeholder, a missing negative block or a dropped frame
line gets into a prompt and quietly ruins a batch.

Single source of truth is `art-prompts.md`. The per-asset middles are parsed out
of its tables; the shared blocks live here because they never vary.

Usage:
    python tools/artgen/build_prompts.py            # writes tools/artgen/out/
    python tools/artgen/build_prompts.py --check    # verifies, writes nothing
"""

from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "art-prompts.md"
OUT = Path(__file__).resolve().parent / "out"

# --- shared blocks, section 2 of art-prompts.md -----------------------------

BASE = (
    "soft 3D cartoon render, three-quarter top-down view at 30 degrees, "
    "rounded shapes with no sharp corners, thick soft dark-walnut outline, "
    "volume from soft diffused light coming from the upper left, "
    "soft shadow falling to the lower right at 25% opacity, "
    "warm muted palette of cream, peach, mint and light oak, "
    "matte surfaces, gentle ambient occlusion where forms meet, "
    "cozy children-book illustration feel but not childish, "
    "clean flat single-colour background, subject centred in frame"
)

NEGATIVE = (
    "pixel art, voxel, low poly, flat vector, line art, sketch, watercolour, "
    "photorealistic, photograph, 3D studio render with reflections, "
    "neon, glow, bloom, lens flare, rim light, hard specular highlights, "
    "high contrast, dark background, black background, dramatic lighting, "
    "saturated colours, acid colours, pink glitter, sparkles, stars, "
    "kawaii, chibi, anime, big shiny anime eyes, human facial expression, "
    "text, letters, numbers, watermark, signature, logo, UI elements, "
    "frame, border, vignette, drop shadow box, gradient background, "
    "multiple objects, cropped subject, subject touching frame edge, "
    "busy background, clutter behind subject, cast shadow on background wall"
)

FRAME_PROP = (
    "single object, centred, occupying 80% of the frame, "
    "10% empty margin on every side, plain #F4EAD8 background"
)

FRAME_ROOM = (
    "interior view, vertical composition, camera at standing height, "
    "bottom third of the frame left empty and uncluttered"
)

# Extra negatives that apply only to a family, section 4 and 5.
EXTRA_NEGATIVE = {
    "cat": (
        "collar, clothing, accessories, human hands, background objects, "
        "multiple cats, kitten and adult cat together, cat food, bowl"
    ),
    "room": (
        "people, cat, animals, modern electronics, television, computer, "
        "open flame, mould, insects, rubbish bags, broken glass, decay, rot, "
        "text on walls, posters with writing"
    ),
}

# name prefix -> (frame, extra-negative key)
FAMILIES = (
    ("prop_", (FRAME_PROP, None)),
    ("cat_", (FRAME_PROP, "cat")),
    ("room_", (FRAME_ROOM, "room")),
)

ROW = re.compile(r"^\|\s*`([a-z0-9_]+)`\s*\|\s*`(.+?)`\s*\|\s*$")


def parse(text: str) -> dict[str, str]:
    """Pull `name` | `middle` rows out of the markdown tables."""
    found: dict[str, str] = {}
    for line in text.splitlines():
        m = ROW.match(line.strip())
        if not m:
            continue
        name, middle = m.group(1), m.group(2).strip()
        if not any(name.startswith(p) for p, _ in FAMILIES):
            continue
        if name in found:
            raise SystemExit(f"duplicate asset in {SOURCE.name}: {name}")
        found[name] = middle
    return found


def family_of(name: str) -> tuple[str, str | None]:
    for prefix, spec in FAMILIES:
        if name.startswith(prefix):
            return spec
    raise SystemExit(f"no family for {name}")


def assemble(name: str, middle: str) -> str:
    frame, extra = family_of(name)
    negative = NEGATIVE if extra is None else f"{NEGATIVE}, {EXTRA_NEGATIVE[extra]}"
    return (
        f"POSITIVE\n{BASE}, {middle}, {frame}\n\n"
        f"NEGATIVE\n{negative}\n"
    )


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true",
                    help="parse and report, write nothing")
    args = ap.parse_args()

    if not SOURCE.exists():
        raise SystemExit(f"missing {SOURCE}")

    assets = parse(SOURCE.read_text(encoding="utf-8"))
    if not assets:
        raise SystemExit("no asset rows parsed — has the table format changed?")

    bad = [n for n, m in assets.items() if re.search(r"[Ѐ-ӿ]", m)]
    if bad:
        raise SystemExit("Cyrillic inside a prompt, models will not understand it: "
                         + ", ".join(bad))

    counts: dict[str, int] = {}
    for name in assets:
        key = name.split("_")[0]
        counts[key] = counts.get(key, 0) + 1

    if args.check:
        print(f"{len(assets)} prompts parsed, no Cyrillic:",
              ", ".join(f"{k}={v}" for k, v in sorted(counts.items())))
        return 0

    OUT.mkdir(exist_ok=True)
    for old in OUT.glob("*.txt"):
        old.unlink()
    for name, middle in sorted(assets.items()):
        (OUT / f"{name}.txt").write_text(assemble(name, middle), encoding="utf-8")

    print(f"wrote {len(assets)} prompts to {OUT.relative_to(ROOT)}:",
          ", ".join(f"{k}={v}" for k, v in sorted(counts.items())))
    return 0


if __name__ == "__main__":
    sys.exit(main())
