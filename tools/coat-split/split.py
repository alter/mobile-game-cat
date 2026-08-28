"""Task 40-art/04, section 4: split a delivered striped cat into base + mask.

The artist now draws stripes straight onto the cat (curved, following the
body — anatomically correct, unlike `CoatMasks.PatternTabby` in
`game/Assets/View/CoatMasks.cs`, which draws even vertical bands because it
has no anatomy to follow). What we need from each delivery is two files:

  base  - the cat with the drawn markings smoothed away, volume and shading
          kept.
  mask  - a greyscale "how strongly marked is this pixel" map, white where a
          marking was, black where it wasn't, so the game can tint markings
          in code the way it already tints `CoatMasks`' computed patterns.

They align pixel-for-pixel by construction: both are computed from the one
input image, nothing is hand-placed.

## Method, and why

Two low-pass filters were tried as the "remove the markings" step, measured
against a synthetic striped cat (see tools/tests/test_coat_split.py) where the
true unstriped image is known exactly:

  * Gaussian/box blur — smooths the stripes but never removes them; it
    attenuates everything in the image by the same amount, marking or not, so
    a threshold that catches the stripes also fires on ordinary shading
    almost everywhere. Measured: ~93% of body pixels look "marked" at a
    threshold that also fires correctly on real stripes. Useless.
  * A bilateral-style filter (edge-preserving smoothing that keeps large
    intensity jumps and blurs small ones) — the opposite failure. A drawn
    stripe against fur IS a large intensity jump, so a filter built to
    *preserve* edges preserves the stripes right along with the real ones.
    Measured recall as low as 2-12%: it mostly just returns the input.
  * A large-radius **median filter** won by measurement: at window radius 17
    (size 35) on a 1024x1024 cat, it recovers ~81% of synthetic stripe
    pixels (mask value > 128 where a stripe was drawn with contrast >= 15
    levels) while cutting the base image's RMSE against the true unstriped
    original by about a third versus doing nothing. See the exact numbers in
    the task report / test file.

That is the method here: base = per-channel median filter of the greyscale
image; mask = the (gained, clipped) absolute difference between the original
and that base.

## Known weakness (measured, not guessed)

A median filter has no notion of "this edge is fur-marking, that edge is
linework" — it treats any small-scale departure from the local majority the
same way. Run on the *undrawn* game cats (`cat_1/2/3_short_base.png`, which
have no stripes — the game draws them in code today) the mask is correctly
near-empty on average (mean mask value ~19-21 / 255), which is the sanity
check the task asks for. But it is not perfectly empty: ears, nose and a
shut eye's seam are small-scale, non-periodic dark linework that the filter
also erases as if it were a marking, and about 3.5-3.8% of the body's pixels
end up with a mask value above 128 there. On a genuinely striped cat that is
lost in the true signal; on an unstriped cat it would show up as faint false
flecks if used uncorrected. A human pass (or a silhouette-relative face/ear
exclusion zone) would be the next fix if this matters in practice — not
attempted here because the task's own positive check (three unstriped cats
staying "nearly empty") already passes at the mean level, and tuning further
without a striped delivery to validate against would be guessing.

## Usage

    .venv/bin/python tools/coat-split/split.py CAT.png --out-dir tools/coat-split/out

Writes `<out-dir>/<stem>_base.png` and `<out-dir>/<stem>_mask.png`.
"""
from __future__ import annotations

import argparse
from pathlib import Path

import numpy as np
from PIL import Image, ImageFilter

DEFAULT_RADIUS = 17     # median filter window = 2*radius + 1 = 35
DEFAULT_GAIN = 5.0      # maps a ~40-46 grey-level marking (measured) near-white


def load_grey_alpha(path: Path) -> tuple[np.ndarray, np.ndarray]:
    """Read a PNG and return (grey float32 HxW, alpha uint8 HxW).

    Grey is the plain mean of R, G, B. The source art is greyscale to begin
    with (measured: max per-pixel channel divergence is 7/255 on the shipped
    cats, almost certainly PNG quantization noise, not colour), so the mean
    loses nothing a weighted luma formula would have kept, and stays simple
    and symmetric.
    """
    im = Image.open(path).convert("RGBA")
    arr = np.array(im)
    grey = arr[..., :3].astype(np.float32).mean(axis=2)
    alpha = arr[..., 3].copy()
    return grey, alpha


def median_base(grey: np.ndarray, radius: int) -> np.ndarray:
    """Large-radius median filter — the winning method, see module docstring."""
    size = 2 * radius + 1
    im = Image.fromarray(np.clip(grey, 0, 255).astype(np.uint8), mode="L")
    filtered = im.filter(ImageFilter.MedianFilter(size=size))
    return np.array(filtered).astype(np.float32)


def split(grey: np.ndarray, alpha: np.ndarray, radius: int = DEFAULT_RADIUS,
          gain: float = DEFAULT_GAIN) -> tuple[np.ndarray, np.ndarray]:
    """Return (base_grey, mask_grey), both float32 in 0..255, same shape as grey.

    base_grey: the median-filtered image — markings smoothed away.
    mask_grey: clip(gain * |grey - base_grey|, 0, 255) — a coverage map,
    white where the original departed from the smoothed base, i.e. where a
    marking was drawn.

    Both are zeroed outside the silhouette (alpha == 0) purely for a clean,
    deterministic file; it has no effect on anything a correct alpha-aware
    consumer would see, since that area is fully transparent in the output
    regardless.
    """
    base_grey = median_base(grey, radius)
    diff = np.abs(grey - base_grey)
    mask_grey = np.clip(diff * gain, 0, 255)

    body = alpha > 0
    base_grey = np.where(body, base_grey, 0.0)
    mask_grey = np.where(body, mask_grey, 0.0)
    return base_grey, mask_grey


def save_grey_alpha(path: Path, grey: np.ndarray, alpha: np.ndarray) -> None:
    """Write a greyscale-content RGBA PNG: R=G=B=round(grey), A=alpha exactly."""
    h, w = grey.shape
    byte_grey = np.clip(np.round(grey), 0, 255).astype(np.uint8)
    out = np.empty((h, w, 4), dtype=np.uint8)
    out[..., 0] = byte_grey
    out[..., 1] = byte_grey
    out[..., 2] = byte_grey
    out[..., 3] = alpha
    Image.fromarray(out, mode="RGBA").save(path)


def split_file(input_path: Path, out_dir: Path, radius: int = DEFAULT_RADIUS,
               gain: float = DEFAULT_GAIN) -> tuple[Path, Path]:
    grey, alpha = load_grey_alpha(input_path)
    base_grey, mask_grey = split(grey, alpha, radius=radius, gain=gain)

    out_dir.mkdir(parents=True, exist_ok=True)
    stem = input_path.stem
    base_path = out_dir / f"{stem}_base.png"
    mask_path = out_dir / f"{stem}_mask.png"
    save_grey_alpha(base_path, base_grey, alpha)
    save_grey_alpha(mask_path, mask_grey, alpha)
    return base_path, mask_path


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("input", type=Path, help="source PNG, greyscale with alpha")
    parser.add_argument("--out-dir", type=Path, default=None,
                        help="output directory (default: alongside the input)")
    parser.add_argument("--radius", type=int, default=DEFAULT_RADIUS,
                        help=f"median filter radius (default {DEFAULT_RADIUS})")
    parser.add_argument("--gain", type=float, default=DEFAULT_GAIN,
                        help=f"mask contrast gain (default {DEFAULT_GAIN})")
    args = parser.parse_args(argv)

    out_dir = args.out_dir if args.out_dir is not None else args.input.resolve().parent
    base_path, mask_path = split_file(args.input, out_dir, radius=args.radius, gain=args.gain)
    print(f"wrote {base_path}")
    print(f"wrote {mask_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
