"""Task 40-art/04, section 4: tools/coat-split/split.py.

`tools/coat-split` has a hyphen in its name (the task specified that path),
so it cannot be a dotted `tools.coat_split` import like the other tool
packages here — it is loaded straight from its file path instead. The CLI
itself is exercised through a subprocess, the same way `git` is invoked
elsewhere in this suite, so the argparse wiring is actually tested and not
just the library functions behind it.

Real-data checks reuse the three shipped, unstriped cats
(`game/Assets/Resources/Art/cat_{1,2,3}_short_base.png`) for the negative
check the task asks for (no stripes in, no invented markings out) and
synthesize a striped cat from `cat_1` for the positive one, because the true
"markings" and the true unstriped base are then known exactly and every
number below is measured against that ground truth, not eyeballed.
"""
import importlib.util
import subprocess
import sys
from pathlib import Path

import numpy as np
import pytest
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
SPLIT_PY = ROOT / "tools/coat-split/split.py"
ART = ROOT / "game/Assets/Resources/Art"

_spec = importlib.util.spec_from_file_location("coat_split", SPLIT_PY)
coat_split = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(coat_split)


# ---------------------------------------------------------------------------
# helpers
# ---------------------------------------------------------------------------

def _synthetic_rgba(w=200, h=200, seed=0):
    """A small, self-contained RGBA test image: a soft grey blob with a
    ragged, partially-transparent silhouette edge (not just a hard circle),
    so alpha-preservation tests actually exercise partial-alpha pixels."""
    rng = np.random.default_rng(seed)
    yy, xx = np.mgrid[0:h, 0:w]
    cy, cx = h / 2, w / 2
    r = np.sqrt(((yy - cy) / (h * 0.42)) ** 2 + ((xx - cx) / (w * 0.42)) ** 2)
    alpha = np.clip((1.2 - r) * 255, 0, 255).astype(np.uint8)
    grey = (120 + 40 * np.sin(xx / 15.0) + 20 * np.cos(yy / 23.0)
            + rng.normal(0, 2, size=(h, w)))
    grey = np.clip(grey, 0, 255).astype(np.uint8)
    out = np.zeros((h, w, 4), dtype=np.uint8)
    out[..., 0] = grey
    out[..., 1] = grey
    out[..., 2] = grey
    out[..., 3] = alpha
    return out


def _make_synthetic_stripes(grey, alpha, strength=55.0, period=42.0, curl=3.5):
    """Impose plausible curved (not vertical-band) tabby-style markings onto
    a real, already-shaded cat, and return the striped image plus the exact
    per-pixel darkening amount that was applied, so recovery is measurable
    against ground truth rather than judged by eye.
    """
    h, w = grey.shape
    body = alpha > 10
    ys, xs = np.mgrid[0:h, 0:w]
    ys = ys.astype(np.float32)
    xs = xs.astype(np.float32)
    cy = ys[body].mean()
    cx = xs[body].mean()
    dy = ys - cy
    dx = xs - cx
    # elliptical-spiral coordinate: bands curve around the body's centre
    # instead of running straight top-to-bottom, unlike CoatMasks.Tabby.
    r = np.sqrt((dx / (w * 0.5)) ** 2 + (dy / (h * 0.5)) ** 2)
    theta = np.arctan2(dy, dx)
    phase = (r * (w * 0.5) / period) + theta * curl
    wave = np.sin(phase * 2 * np.pi)
    band = np.clip((wave - 0.45) / 0.15, 0, 1)
    band = band * band * (3 - 2 * band)         # smoothstep: soft band edges
    delta = band * strength
    delta[~body] = 0
    striped = np.clip(grey - delta, 0, 255)
    return striped, delta, body


# ---------------------------------------------------------------------------
# alpha / format contract
# ---------------------------------------------------------------------------

def test_alpha_is_preserved_exactly_in_both_outputs():
    rgba = _synthetic_rgba()
    grey = rgba[..., :3].astype(np.float32).mean(axis=2)
    alpha = rgba[..., 3]

    base_grey, mask_grey = coat_split.split(grey, alpha, radius=5, gain=5.0)

    # split() itself must not touch alpha -- the function only returns grey
    # channels, but callers reuse the same `alpha` array for both outputs,
    # which is the alignment guarantee the task is built around.
    assert alpha.shape == grey.shape

    # round-trip through the actual PNG writer and re-read, byte for byte
    import tempfile
    with tempfile.TemporaryDirectory() as tmp:
        tmp = Path(tmp)
        coat_split.save_grey_alpha(tmp / "base.png", base_grey, alpha)
        coat_split.save_grey_alpha(tmp / "mask.png", mask_grey, alpha)
        base_arr = np.array(Image.open(tmp / "base.png").convert("RGBA"))
        mask_arr = np.array(Image.open(tmp / "mask.png").convert("RGBA"))

    assert np.array_equal(base_arr[..., 3], alpha)
    assert np.array_equal(mask_arr[..., 3], alpha)


def test_mask_alpha_never_leaks_outside_the_silhouette():
    rgba = _synthetic_rgba()
    grey = rgba[..., :3].astype(np.float32).mean(axis=2)
    alpha = rgba[..., 3]
    base_grey, mask_grey = coat_split.split(grey, alpha, radius=5, gain=5.0)

    outside = alpha == 0
    assert np.array_equal(mask_grey[outside], np.zeros(outside.sum(), dtype=mask_grey.dtype))
    assert np.array_equal(base_grey[outside], np.zeros(outside.sum(), dtype=base_grey.dtype))


def test_outputs_are_greyscale_r_equals_g_equals_b():
    rgba = _synthetic_rgba()
    grey = rgba[..., :3].astype(np.float32).mean(axis=2)
    alpha = rgba[..., 3]
    base_grey, mask_grey = coat_split.split(grey, alpha, radius=5, gain=5.0)

    import tempfile
    with tempfile.TemporaryDirectory() as tmp:
        tmp = Path(tmp)
        coat_split.save_grey_alpha(tmp / "base.png", base_grey, alpha)
        coat_split.save_grey_alpha(tmp / "mask.png", mask_grey, alpha)
        for name in ("base.png", "mask.png"):
            arr = np.array(Image.open(tmp / name).convert("RGBA"))
            assert np.array_equal(arr[..., 0], arr[..., 1])
            assert np.array_equal(arr[..., 1], arr[..., 2])


# ---------------------------------------------------------------------------
# CLI
# ---------------------------------------------------------------------------

def test_cli_writes_base_and_mask_with_expected_names(tmp_path):
    rgba = _synthetic_rgba()
    src = tmp_path / "some_cat.png"
    Image.fromarray(rgba, mode="RGBA").save(src)

    out_dir = tmp_path / "out"
    result = subprocess.run(
        [sys.executable, str(SPLIT_PY), str(src), "--out-dir", str(out_dir),
         "--radius", "5"],
        capture_output=True, text=True, cwd=ROOT,
    )
    assert result.returncode == 0, result.stderr
    assert (out_dir / "some_cat_base.png").exists()
    assert (out_dir / "some_cat_mask.png").exists()


def test_cli_defaults_output_dir_to_the_input_directory(tmp_path):
    rgba = _synthetic_rgba()
    src = tmp_path / "another_cat.png"
    Image.fromarray(rgba, mode="RGBA").save(src)

    result = subprocess.run(
        [sys.executable, str(SPLIT_PY), str(src)],
        capture_output=True, text=True, cwd=ROOT,
    )
    assert result.returncode == 0, result.stderr
    assert (tmp_path / "another_cat_base.png").exists()
    assert (tmp_path / "another_cat_mask.png").exists()


# ---------------------------------------------------------------------------
# real data: negative check (no stripes in, none invented)
# ---------------------------------------------------------------------------

@pytest.mark.parametrize("n", [1, 2, 3])
def test_real_shipped_cats_have_no_stripes_and_the_mask_stays_near_empty(n):
    """cat_1/2/3_short_base.png carry no drawn markings -- the game paints
    stripes in code today. A correct extractor must not invent any: the
    mask's mean value within the silhouette should stay low. The bound here
    (60/255) is well above what was actually measured for all three cats
    (19.6, 18.6, 21.3 -- see tools/coat-split/README.md), left loose so the
    test isn't tuned to the exact figure and only fails on real regressions.
    """
    path = ART / f"cat_{n}_short_base.png"
    grey, alpha = coat_split.load_grey_alpha(path)
    base_grey, mask_grey = coat_split.split(grey, alpha)
    body = alpha > 10
    mean_mask = float(mask_grey[body].mean())
    assert mean_mask < 60.0, f"cat_{n}: mean mask value {mean_mask:.1f}/255 -- too high for an unstriped cat"


# ---------------------------------------------------------------------------
# real data: positive check (stripes in, recovered)
# ---------------------------------------------------------------------------

def test_synthetic_stripes_are_recovered_and_the_base_gets_closer_to_truth():
    """Draw curved markings onto cat_1 (known exactly), run the real split(),
    and check against that known ground truth -- both that the mask lights
    up where the marking was, and that the base moves measurably closer to
    the true unstriped cat than doing nothing at all would.
    """
    grey, alpha = coat_split.load_grey_alpha(ART / "cat_1_short_base.png")
    striped, delta, body = _make_synthetic_stripes(grey, alpha)
    true_stripe = (delta > 15) & body

    base_grey, mask_grey = coat_split.split(striped, alpha)

    recall = float((mask_grey > 128)[true_stripe].sum()) / float(true_stripe.sum())
    # measured 0.813; bounded well below that so the test isn't chasing the
    # exact figure, only guarding against the method breaking outright.
    assert recall > 0.6, f"recovered only {recall:.2%} of the synthetic markings"

    base_rmse = float(np.sqrt(np.mean((base_grey[body] - grey[body]) ** 2)))
    noop_rmse = float(np.sqrt(np.mean((striped[body] - grey[body]) ** 2)))
    # measured ~19.8 vs ~30.4 (base ~35% closer to truth than the untouched
    # striped image); require at least a 15% improvement.
    assert base_rmse < noop_rmse * 0.85, (
        f"base ({base_rmse:.1f}) is not meaningfully closer to the true "
        f"unstriped cat than the untouched striped image ({noop_rmse:.1f})")
