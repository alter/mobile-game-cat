# coat-split

Task 40-art/04, section 4. Splits a delivered cat PNG — drawn **with** its
tabby markings already on it — into two files that align pixel-for-pixel by
construction, because both come from the same input image:

- `<name>_base.png` — the cat with the markings smoothed away, volume and
  shading kept.
- `<name>_mask.png` — a greyscale coverage map, white where a marking was,
  black where it wasn't, meant to be tinted separately in code (the same way
  `CoatBuilder.Tint` already tints the computed `CoatMasks` patterns).

Both outputs are greyscale (R=G=B) with the source's alpha channel copied
into them byte-for-byte, so the mask can never show colour or transparency
the original silhouette didn't have.

## Run it

```
.venv/bin/python tools/coat-split/split.py CAT.png --out-dir tools/coat-split/out
```

Options: `--radius` (median filter radius, default 17) and `--gain` (mask
contrast multiplier, default 5.0).

## Method, and the evidence for it

Three ways to get a "smoothed, marking-free" base were tried, measured
against a synthetic striped cat built by drawing curved (not the game's
vertical-band) markings onto the real `cat_1_short_base.png` — synthetic
because the true unstriped image is then known exactly, so recovery can be
measured instead of eyeballed:

| method | what happened |
|---|---|
| Gaussian/box blur | Attenuates the stripes but never removes them — a threshold that catches real stripes also fires on ~93% of all body pixels. It can't tell a marking from ordinary shading because it smooths both by the same amount. |
| Bilateral-style (edge-preserving, keeps big intensity jumps, blurs small ones) | The opposite failure. A drawn stripe against fur *is* a big intensity jump, so a filter built to preserve edges preserves the stripes too. Measured recall 2.5–12% depending on radius — it mostly returns the input unchanged. |
| **Large-radius median filter (chosen)** | Stripes are a local minority inside a window bigger than the stripe period, so the median naturally returns the surrounding fur tone. Measured below. |

Median filter won on measurement, not on which sounds better.

### Positive check: synthetic stripes recovered

Reproduce with `.venv/bin/python tools/coat-split/split.py` on a striped
cat built the same way as `tools/tests/test_coat_split.py`'s
`_make_synthetic_stripes` (curved spiral bands, contrast up to 55 grey
levels, imposed on `cat_1_short_base.png`):

- **Recall**: 81.3% of true stripe pixels (darkened by ≥15 levels) end up
  with mask value > 128/255.
- **Base RMSE** against the true, unstriped `cat_1`: 19.8, versus 30.4 for
  doing nothing (leaving the striped image as its own "base") — a 34.9%
  reduction.

### Negative check: the three real, unstriped cats stay near-empty

`cat_1/2/3_short_base.png` carry no drawn markings — the game paints stripes
in code today — so a correct extractor should invent close to nothing:

| cat | mean mask value (0–255) | fraction of body px with mask > 128 |
|---|---|---|
| cat_1 | 19.6 | 3.75% |
| cat_2 | 18.6 | 3.58% |
| cat_3 | 21.3 | 3.51% |

Mostly black, as it should be — but not perfectly.

## Where it is weak

**A median filter has no notion of "marking" versus "linework."** It treats
any small-scale departure from the local majority the same way, whether that
departure is a periodic tabby stripe or a one-off dark line (an ear's inner
shadow, a shut eye's seam, the nose). On the three real, unstriped cats
above, ~3.5–3.8% of the body's pixels still register a mask value above 128
even though nothing was drawn there — visible in
`tools/coat-split/out/cat_1_short_base_mask.png` as bright flecks at the
eyes and inner ear, not as noise spread evenly over the coat. On a genuinely
striped delivery this is lost in the real signal; used uncorrected on an
unstriped cat it would show as faint false marks. Not fixed here — a
silhouette-relative face/ear exclusion zone is the obvious next step, but
tuning it without a real striped delivery to validate against would be
guessing, which the task asked not to do.

**High-curvature regions under-resolve.** The synthetic test's stripes
spiral tightly near their centre; where the true stripe period there gets
smaller than roughly twice the filter radius, the median filter can't fully
separate stripe from gap and the base keeps a faint ghost of the pattern
(visible in `tools/coat-split/out/synthetic_striped_cat1_base.png`). Real
tabby stripes on a cat's body don't curl that tightly, so this is more a
property of the synthetic test than an expected real-delivery failure, but
it hasn't been checked against real art.

**Un-premultiplying the alpha-blended edge ring was tried and made things
worse, not better.** The source PNGs' RGB darkens toward the silhouette edge
roughly in proportion to alpha (measured on `cat_2`: mean |diff| 21.7 in the
partial-alpha ring vs 3.6 deep inside the body). Dividing by a floored alpha
before filtering was tried as a fix; it amplified noise more than it removed
the fringe (ring mean |diff| rose to 38.7) and was dropped. The ring
contributes to, but is not the dominant source of, the false-positive
figures above — the interior linework false positives are larger and were
the reason the fix wasn't pursued further.

**Only two real methods were actually compared quantitatively at scale**
(median vs Gaussian); the bilateral-style filter was only checked at two
radius/sigma settings before its edge-preservation problem was judged
fundamental rather than a tuning issue — a wider sweep was not run because
the failure mode (preserving the exact edges we want removed) does not go
away with different parameters.

## Reproduce every number above

```
.venv/bin/python -m pytest tools/tests/test_coat_split.py -q
```

`test_real_shipped_cats_have_no_stripes_and_the_mask_stays_near_empty` and
`test_synthetic_stripes_are_recovered_and_the_base_gets_closer_to_truth` run
the exact computation behind the tables above (with looser bounds than the
measured figures, so the tests guard against regressions rather than
re-asserting the exact numbers).

---

## Калибровка по ширине полос — измерено 28.08 при проверке

Заявленный в первой редакции возврат **81,3 %** получен на своих узких полосах и
**не переносится** на любую ширину. Радиус срединного фильтра должен превышать
полуширину полосы, иначе полоса перестаёт быть меньшинством в окне и фильтр её
сохраняет — то есть считает частью основы.

Проверка: на `cat_1_short_base.png` рисуются изогнутые дуги известной ширины,
затем картинка разделяется и сравнивается с истиной.

| полуширина полосы | радиус 17 | радиус 35 |
|---|---|---|
| 5 px | **95,7 %** возврата, 9,7 % ложных | 96,6 %, 24,8 % ложных |
| 11 px | 16,4 % — не работает | **91,6 %**, 32,8 % ложных |
| 22 px | 10,0 % — не работает | 28,5 %, 65,9 % ложных |

«Ложные» — доля пикселей тела вне полос, попавших в маску.

**Как этим пользоваться.** Померить ширину полос на присланной кошке (на глаз по
снимку достаточно) и взять радиус примерно вдвое больше полуширины. Проверить
глазами: если маска светится по всему телу — радиус велик; если полосы в ней
почти не видны — мал.

**Где предел.** При полуширине от 20 пикселей приём ломается при любом радиусе:
широкая полоса неотличима от собственной светотени тела. На кошке 1024×1024 это
полосы шире примерно 40 пикселей. Если художник нарисует такие, разделение
придётся делать иначе — например, просить отдельный слой, — и это надо выяснить
на **первой** присланной кошке, а не после трёх.

Воспроизвести:

```bash
.venv/bin/python tools/coat-split/split.py КОШКА.png --radius 17 --out-dir out
```
