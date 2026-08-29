// Render coats to PNG files and say what is in them.
//
//   dotnet run --project build/coat-harness -- <outDir> [size] [state]
//
// Default size 256 — what CoatGridView and the board build at — and state 3,
// the clean cat. Everything else comes from the trait list below, which is
// deliberately the set of cats this project keeps getting wrong: the owner's
// three photographs read as cream/solid, brown/tabby and ginger/solid, and the
// three that must be readable at a glance are cream, black and white solid.
//
// Alongside each PNG the harness prints what is measurable about it — mean
// lightness, how much of the body clips to white or crushes to black, and a
// stripe score. The stripe score is the whole point of the numbers: it is the
// mean absolute difference between the body's lightness and a heavily blurred
// copy of itself, i.e. how much high-frequency light and shade is left. The
// same cat rendered solid and rendered tabby gives the scale — if "solid" does
// not come out well under "tabby", the stripes are still there whatever the
// picture looks like. But the picture is still what decides; the number only
// says where to look.

using System;
using System.Collections.Generic;
using System.IO;
using CatShelter.Core;
using CatShelter.View;
using UnityEngine;

namespace CatShelter.Harness
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            string repo = FindRepoRoot();
            Resources.Root = Path.Combine(repo, "game", "Assets", "Resources");

            string outDir = args.Length > 0
                ? args[0]
                : Path.Combine(repo, "build", "coat-harness", "out");
            int size = args.Length > 1 ? int.Parse(args[1]) : 256;
            int state = args.Length > 2 ? int.Parse(args[2]) : 3;

            Directory.CreateDirectory(outDir);

            // A fresh cache directory every run. CoatBuilder writes built coats
            // beside the save and reads them back on the next launch, and on the
            // device that cache once served stale cats for two days after the
            // builder was fixed. A harness that could do the same would be worse
            // than useless: it would report that a change did nothing.
            Application.persistentDataPath = Path.Combine(Path.GetTempPath(),
                "coat-harness-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(Application.persistentDataPath);

            Console.WriteLine($"resources {Resources.Root}");
            Console.WriteLine($"out       {outDir}");
            Console.WriteLine($"size      {size}   state {state}");
            Console.WriteLine();

            // `stages` renders one cat and writes every intermediate field of
            // Deband beside her, as a picture. Reading a number off a striped
            // cat says the stripes are there; only the fields say which step
            // let them through.
            bool stages = Array.IndexOf(args, "stages") >= 0;

            var eyeScores = new List<(string, double)>();
            double worstSurvival = double.MaxValue;
            string worstLabel = "none";

            foreach (var (label, traits) in Cases())
            {
                if (stages)
                {
                    string prefix = label;
                    CoatBuilder.Stages = (name, field, fw, fh) =>
                        WriteField(Path.Combine(outDir, $"{prefix}__{name}.png"),
                                   field, fw, fh);
                }

                var art = CoatBuilder.LoadBase(traits, state);
                if (art == null)
                {
                    Console.WriteLine($"{label,-22} NO ART");
                    continue;
                }

                var small = CoatBuilder.Downscale(art, size);
                var built = CoatBuilder.Build(small, traits, state);

                var file = Path.Combine(outDir, $"{label}.png");
                File.WriteAllBytes(file, built.EncodeToPNG());

                // The eyes, measured on the source and then on the result. The
                // source is the reference because it is the drawing: whatever
                // contrast the artist put on the face is 100%, and the question
                // is only how much of it the coat pass left standing.
                var srcPx = small.GetPixels32();
                var outPx = built.GetPixels32();
                var seeds = FindEyesInSource(srcPx, small.width, small.height);
                EyeMassAndRing(srcPx, small.width, small.height, seeds,
                               out var eyePixels, out var ring, out int splitX);
                string eyeNote;
                double survival = double.NaN, wasWorst = 0, nowWorst = 0;
                if (eyePixels.Count == 0 || ring.Count == 0)
                {
                    eyeNote = "eyes n/a";
                }
                else
                {
                    // Each eye against the cheek beneath it, and the worse one
                    // is the score. See EyeMassAndRing for why an average lies.
                    int wS = small.width;
                    foreach (bool leftSide in new[] { true, false })
                    {
                        var e = eyePixels.FindAll(i => (i % wS < splitX) == leftSide);
                        var g = ring.FindAll(i => (i % wS < splitX) == leftSide);
                        if (e.Count == 0 || g.Count == 0) continue;
                        double was = EyeContrast(srcPx, e, g);
                        double now = EyeContrast(outPx, e, g);
                        if (double.IsNaN(was) || was <= 1e-6) continue;
                        double s = now / was;
                        if (double.IsNaN(survival) || s < survival)
                        {
                            survival = s; wasWorst = was; nowWorst = now;
                        }
                    }
                    eyeNote = double.IsNaN(survival)
                        ? "eyes n/a"
                        : $"eyes {wasWorst:F3}->{nowWorst:F3} ({100 * survival,3:F0}%)";
                    if (!double.IsNaN(survival))
                    {
                        eyeScores.Add((label, survival));
                        if (survival < worstSurvival)
                        {
                            worstSurvival = survival;
                            worstLabel = label;
                        }
                    }
                }

                WriteFace(Path.Combine(outDir, $"{label}_face.png"),
                          outPx, built.width, built.height, seeds);

                Console.WriteLine($"{label,-22} {Describe(built)}  {eyeNote}");
                Console.WriteLine();
            }

            Console.WriteLine($"wrote {Directory.GetFiles(outDir, "*.png").Length} files to {outDir}");

            // Loud, and with a non-zero exit code, because the failure this
            // guards against is one that improves every other number in the
            // table. A quiet warning underneath a row of better scores is
            // exactly how the eyes were lost in the first place.
            Console.WriteLine();
            var failures = eyeScores.FindAll(s => s.Item2 < EyeSurvivalFloor);
            if (failures.Count > 0)
            {
                Console.WriteLine($"EYE CHECK FAILED  {failures.Count} of {eyeScores.Count} "
                                  + $"cats below {100 * EyeSurvivalFloor:F0}% eye contrast");
                foreach (var (name, s) in failures)
                    Console.WriteLine($"    {name,-22} {100 * s,3:F0}%");
                return 1;
            }
            Console.WriteLine(eyeScores.Count == 0
                ? "EYE CHECK SKIPPED  no eyes located on any source silhouette"
                : $"EYE CHECK PASSED  worst {worstLabel} {100 * worstSurvival:F0}%");
            return 0;
        }

        /// <summary>
        /// The cats worth looking at. Solid first and in every colour, because
        /// "a solid cat must be solid" is the claim under test; the tabby of the
        /// same colour sits beside each so the two can be compared, which is the
        /// only honest way to say whether the stripes came off.
        /// </summary>
        private static IEnumerable<(string, CatTraits)> Cases()
        {
            var none = Array.Empty<string>();
            foreach (var colour in new[] { "cream", "black", "white", "ginger", "grey", "brown" })
                yield return ($"{colour}_solid",
                    new CatTraits(colour, "solid", "short", "green", none));

            yield return ("cream_tabby",
                new CatTraits("cream", "tabby", "short", "green", none));
            yield return ("grey_tabby",
                new CatTraits("grey", "tabby", "short", "green", none));
            yield return ("black_tuxedo",
                new CatTraits("black", "tuxedo", "short", "amber", new[] { "chest", "paws" }));
            yield return ("white_solid_chest",
                new CatTraits("white", "solid", "short", "blue", new[] { "chest" }));

            // The owner's own three cats, as the game read them on 2026-08-29.
            // The first is the one with a device log to prove the traits were
            // right and the picture wrong: `[CatColour] centre mean rgb (0.737,
            // 0.685, 0.645) -> cream`, then `cat ready (OfflineColourOnly):
            // short cream solid, green eyes`. She came back a grey striped tabby
            // with a pale wash over her chest and paws.
            yield return ("owner1_white_longhair",
                CatTraits.FromColourOnly("cream"));
            yield return ("owner2_brown_tabby",
                new CatTraits("brown", "tabby", "short", "amber", Array.Empty<string>()));
            yield return ("owner3_brown_cream",
                new CatTraits("ginger", "solid", "short", "amber", Array.Empty<string>()));
        }

        /// <summary>
        /// Numbers that say where to look. None of them decides anything on its
        /// own — a cat can measure well and look wrong — but "solid scores as
        /// high as tabby" is proof the stripes are still there, and no amount of
        /// squinting at a thumbnail beats it.
        /// </summary>
        private static string Describe(Texture2D tex)
        {
            var px = tex.GetPixels32();
            int w = tex.width, h = tex.height;

            // The COAT, not the picture. Outline paints a dark ink rim a few
            // pixels wide right round the silhouette and gives it alpha 235, so
            // a naive `a > 200` body mask includes it — and the rim is the
            // highest-contrast, highest-frequency thing in the whole image. It
            // put a floor of 0.123 under the texture score on a cream cat, which
            // is most of what a striped cat scores, and it does not move when
            // anything in Deband moves. Two hours went into asking why the
            // debander could not get below a number that was never its to move.
            //
            // Eroding the mask by the rim's own width plus the soft edge under
            // it leaves the fur and nothing else.
            var solid = new bool[px.Length];
            for (int i = 0; i < px.Length; i++) solid[i] = px[i].a > 200;
            int erode = Math.Max(3, (int)Math.Round(w * 0.016) + 3);
            var body = Erode(solid, w, h, erode);

            var light = new float[px.Length];
            int n = 0;
            double mean = 0;
            int clipped = 0, crushed = 0;
            for (int i = 0; i < px.Length; i++)
            {
                if (!body[i]) continue;
                light[i] = (px[i].r + px[i].g + px[i].b) / 765f;
                mean += light[i];
                if (light[i] > 0.97f) clipped++;
                if (light[i] < 0.05f) crushed++;
                n++;
            }
            if (n == 0) return "empty";
            mean /= n;

            // Blur radius is a twelfth of the width: broader than any stripe on
            // these silhouettes, narrower than the body. What survives the
            // difference is texture, not form.
            int radius = Math.Max(2, w / 12);
            var blur = Blur1D(Blur1D(light, body, w, h, radius, true), body, w, h, radius, false);
            double detail = 0;
            for (int i = 0; i < px.Length; i++)
                if (body[i]) detail += Math.Abs(light[i] - blur[i]);
            detail /= n;

            // Mean colour of the body, so "is this cat actually cream" has an
            // answer that is not a matter of opinion.
            long r = 0, g = 0, b = 0;
            for (int i = 0; i < px.Length; i++)
                if (body[i]) { r += px[i].r; g += px[i].g; b += px[i].b; }

            return $"rgb({r / n,3},{g / n,3},{b / n,3})  mean {mean:F3}  " +
                   $"detail {detail:F4}  clipped {100.0 * clipped / n,5:F1}%  " +
                   $"crushed {100.0 * crushed / n,5:F1}%";
        }

        /// <summary>
        /// One of Deband's internal fields as a greyscale picture, auto-scaled
        /// so a field whose whole range is 0..0.04 — the relief, which is the
        /// one that decides whether a line goes back — is still legible. The
        /// range it was stretched from is printed, because a picture with no
        /// scale on it can be read to mean anything.
        /// </summary>
        private static void WriteField(string path, float[] field, int w, int h)
        {
            float lo = float.MaxValue, hi = float.MinValue;
            foreach (var v in field)
            {
                if (v == 0f) continue;   // outside the body
                if (v < lo) lo = v;
                if (v > hi) hi = v;
            }
            if (hi <= lo) { lo = 0f; hi = 1f; }

            // Percentiles, not just the range. A field whose maximum is 0.085
            // tells you nothing about whether the threshold at 0.035 fires on
            // most of the cat or on a hundred pixels of her tail, and that
            // difference is the whole diagnosis.
            var sorted = new List<float>();
            foreach (var v in field) if (v != 0f) sorted.Add(v);
            sorted.Sort();
            string pct = "";
            if (sorted.Count > 0)
            {
                float P(double q) => sorted[Math.Clamp((int)(sorted.Count * q), 0, sorted.Count - 1)];
                pct = $"  p10 {P(0.10):F4} p50 {P(0.50):F4} p90 {P(0.90):F4}"
                    + $"  detail {FieldDetail(field, w, h):F4}";
            }

            var px = new Color32[field.Length];
            for (int i = 0; i < field.Length; i++)
            {
                byte v = (byte)Math.Clamp((field[i] - lo) / (hi - lo) * 255f, 0f, 255f);
                px[i] = new Color32(v, v, v, (byte)255);
            }
            File.WriteAllBytes(path, Png.Encode(px, w, h));
            Console.WriteLine($"         {Path.GetFileName(path),-28} range {lo:F4}..{hi:F4}{pct}");
        }

        /// <summary>
        /// The same texture score Describe uses, on a bare field. This is what
        /// says whether a stage is flat: `smooth` should score near zero and
        /// `light` high, and any stage between them that scores like `light`
        /// is the one that let the stripes through.
        /// </summary>
        private static double FieldDetail(float[] field, int w, int h)
        {
            var body = new bool[field.Length];
            int n = 0;
            for (int i = 0; i < field.Length; i++)
                if (field[i] != 0f) { body[i] = true; n++; }
            if (n == 0) return 0;
            int radius = Math.Max(2, w / 12);
            var blur = Blur1D(Blur1D(field, body, w, h, radius, true), body, w, h, radius, false);
            double d = 0;
            for (int i = 0; i < field.Length; i++) if (body[i]) d += Math.Abs(field[i] - blur[i]);
            return d / n;
        }

        /// <summary>Shrink a mask by <paramref name="radius"/>, so a measurement
        /// taken over it cannot see the ink rim or the anti-aliased edge.</summary>
        private static bool[] Erode(bool[] mask, int w, int h, int radius)
        {
            var pass = new bool[mask.Length];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    bool all = true;
                    for (int d = -radius; d <= radius && all; d++)
                    {
                        int k = x + d;
                        all = k >= 0 && k < w && mask[y * w + k];
                    }
                    pass[y * w + x] = all;
                }
            var dst = new bool[mask.Length];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    bool all = true;
                    for (int d = -radius; d <= radius && all; d++)
                    {
                        int k = y + d;
                        all = k >= 0 && k < h && pass[k * w + x];
                    }
                    dst[y * w + x] = all;
                }
            return dst;
        }

        private static float[] Blur1D(float[] src, bool[] body, int w, int h,
                                      int radius, bool horizontal)
        {
            var dst = new float[src.Length];
            int outer = horizontal ? h : w, inner = horizontal ? w : h;
            for (int o = 0; o < outer; o++)
                for (int k = 0; k < inner; k++)
                {
                    int at = horizontal ? o * w + k : k * w + o;
                    if (!body[at]) continue;
                    float sum = 0; int count = 0;
                    for (int d = -radius; d <= radius; d++)
                    {
                        int j = k + d;
                        if (j < 0 || j >= inner) continue;
                        int i = horizontal ? o * w + j : j * w + o;
                        if (!body[i]) continue;
                        sum += src[i]; count++;
                    }
                    dst[at] = count > 0 ? sum / count : src[at];
                }
            return dst;
        }

        // ---------------------------------------------------------------
        // The eye check
        // ---------------------------------------------------------------
        //
        // Why this exists, in one sentence: on 2026-08-29 a debanding change
        // erased the cat's eyes in state 1 and the `detail` score IMPROVED,
        // because deleting a large dark feature lowers high-frequency spread.
        // The number moved the right way while the picture moved the wrong way.
        // A texture score cannot tell damage from cleaning, and nothing else
        // here was looking at the face.
        //
        // The locator below is deliberately the harness's OWN code and not a
        // call into CoatMasks.FindEyes. A check that shares the detector it is
        // checking cannot fail when the detector is what is broken — and the
        // detector being broken is exactly the defect this was written for. It
        // runs on the SOURCE silhouette, where the eyes are plainly drawn and a
        // permissive rule finds them easily; the built texture is then measured
        // over the pixels the source says are eyes.

        /// <summary>
        /// How much of the drawn eye-to-face contrast a built coat must keep.
        ///
        /// A guard rail, not a tuned value: it is set where anything below it is
        /// unarguably a cat with damaged eyes, so that it fires on a regression
        /// and stays quiet through ordinary tuning. Do not adjust it to make a
        /// run pass — that is the one change that makes this check worthless.
        /// </summary>
        private const double EyeSurvivalFloor = 0.55;

        /// <summary>
        /// The eye pixels of a source silhouette: the two largest dark blobs in
        /// the head band, at comparable heights. Permissive on purpose — it is
        /// measuring, not painting, so a few pixels of eyelid cost nothing.
        /// </summary>
        private static List<int> FindEyesInSource(Color32[] px, int w, int h)
        {
            var body = new bool[px.Length];
            var lum = new float[px.Length];
            int top = h, bottom = -1;
            for (int i = 0; i < px.Length; i++)
            {
                body[i] = px[i].a > 200;
                lum[i] = (px[i].r + px[i].g + px[i].b) / 765f;
                if (!body[i]) continue;
                int y = i / w;
                if (y < top) top = y;
                if (y > bottom) bottom = y;
            }
            var result = new List<int>();
            if (bottom < 0) return result;
            int height = bottom - top;

            var values = new List<float>();
            for (int i = 0; i < px.Length; i++) if (body[i]) values.Add(lum[i]);
            values.Sort();
            float threshold = values[Math.Clamp((int)(values.Count * 0.10), 0, values.Count - 1)];

            // Rows run bottom-up, so the head is the HIGH row index.
            int bandBottom = bottom - (int)Math.Round(height * 0.45);
            var dark = new bool[px.Length];
            for (int i = 0; i < px.Length; i++)
                dark[i] = body[i] && lum[i] < threshold && i / w > bandBottom;

            var blobs = Blobs(dark, w, h, Math.Max(8, (int)(w * (long)h * 0.0004)));
            if (blobs.Count == 0) return result;
            blobs.Sort((a, b) => b.Count.CompareTo(a.Count));

            // Everything at the largest blob's own height: that groups the pair
            // and drops an ear or a nose far above or below it.
            float y0 = MeanRow(blobs[0], w);
            foreach (var b in blobs)
                if (Math.Abs(MeanRow(b, w) - y0) <= height * 0.12f)
                {
                    result.AddRange(b);
                    if (result.Count > 0 && b != blobs[0]) break;   // two is enough
                }
            return result;
        }

        private static float MeanRow(List<int> blob, int w)
        {
            long sum = 0;
            foreach (var i in blob) sum += i / w;
            return (float)sum / blob.Count;
        }

        private static List<List<int>> Blobs(bool[] mask, int w, int h, int minSize)
        {
            var seen = new bool[mask.Length];
            var found = new List<List<int>>();
            var queue = new Queue<int>();
            for (int start = 0; start < mask.Length; start++)
            {
                if (!mask[start] || seen[start]) continue;
                seen[start] = true; queue.Clear(); queue.Enqueue(start);
                var blob = new List<int>();
                while (queue.Count > 0)
                {
                    int i = queue.Dequeue(); blob.Add(i);
                    int y = i / w, x = i % w;
                    Push(x + 1, y); Push(x - 1, y); Push(x, y + 1); Push(x, y - 1);
                    void Push(int nx, int ny)
                    {
                        if (nx < 0 || nx >= w || ny < 0 || ny >= h) return;
                        int j = ny * w + nx;
                        if (!mask[j] || seen[j]) return;
                        seen[j] = true; queue.Enqueue(j);
                    }
                }
                if (blob.Count >= minSize) found.Add(blob);
            }
            return found;
        }

        /// <summary>
        /// The whole eye MASS and the face around it, grown from the seed blobs.
        ///
        /// The seeds alone are the wrong thing to measure, and measuring them is
        /// how this check first gave a reassuring number for a cat with no eyes.
        /// A seed is the darkest tenth of the drawing, which on the sitting cat
        /// (state 1) is a sliver of the far eye's lid and a fragment of the near
        /// eye's pupil — while what the owner sees vanish is the whole dark
        /// almond, most of which is mid-tone and never seeded. The far-eye sliver
        /// happens to sit inside the protected muzzle band and survives intact,
        /// so a score taken over the seeds was dominated by the one piece of eye
        /// that was never at risk. It read 70% on a face that had plainly lost
        /// its eyes.
        ///
        /// So the seeds only locate the eyes; the region measured is the box they
        /// span, and within it every pixel darker than the surrounding face. That
        /// is the mass a player reads as an eye. The box is bounded so the growth
        /// cannot reach the forehead, where the drawn tabby M is just as dark and
        /// is not an eye.
        /// </summary>
        private static void EyeMassAndRing(Color32[] px, int w, int h, List<int> seeds,
                                           out List<int> eyes, out List<int> ring,
                                           out int splitX)
        {
            eyes = new List<int>();
            ring = new List<int>();
            splitX = 0;
            if (seeds.Count == 0) return;

            int x0 = w, x1 = -1, y0 = h, y1 = -1;
            foreach (var i in seeds)
            {
                int y = i / w, x = i % w;
                if (x < x0) x0 = x;
                if (x > x1) x1 = x;
                if (y < y0) y0 = y;
                if (y > y1) y1 = y;
            }
            int span = Math.Max(4, x1 - x0);

            // Where one eye ends and the other begins. The two eyes have to be
            // scored SEPARATELY and the worse of them reported, because the
            // failure this check exists to catch is asymmetric: on the sitting
            // cat the head is turned down and away, so the near eye is a wide
            // almond and the far one a narrow slit at a different height, and
            // the debanding erased the near eye while leaving the slit whole. An
            // average over both came out at 74% — better than the pose whose
            // eyes are fine — because one intact eye pays for one destroyed one.
            // A face with one eye is not 74% of a face.
            splitX = (x0 + x1) / 2;

            // The region measured hugs each seed blob rather than spanning the
            // whole face between the two.
            //
            // A rectangle across both eyes was the first version, and it counted
            // the cheeks as eye. On the standing cat there is a drawn tabby
            // stripe running back from under the far eye across the cheek; the
            // fix removed it, which is the entire point of the debander, and the
            // score fell from 132% to 62% and reported the improvement as
            // damage. That is the same trap as the `detail` score, arrived at
            // from the other side, and it would have made this check argue
            // against every future stripe removal near the face.
            //
            // A disc around each seed is small enough to exclude the cheek and
            // large enough to hold the whole almond, which is what a player
            // reads as the eye.
            int reach = Math.Max(3, span / 6);
            var near = new bool[px.Length];
            foreach (var s in seeds)
            {
                int sy = s / w, sx = s % w;
                for (int dy = -reach; dy <= reach; dy++)
                    for (int dx = -reach; dx <= reach; dx++)
                    {
                        if (dx * dx + dy * dy > reach * reach) continue;
                        int ny = sy + dy, nx = sx + dx;
                        if (ny < 0 || ny >= h || nx < 0 || nx >= w) continue;
                        near[ny * w + nx] = true;
                    }
            }

            int ex0 = Math.Max(0, x0 - reach);
            int ex1 = Math.Min(w - 1, x1 + reach);
            int ey0 = Math.Max(0, y0 - reach);
            int ey1 = Math.Min(h - 1, y1 + reach);

            // The face to compare against: a band of the same height just below
            // the eyes — the cheeks and the bridge of the nose. Rows run
            // bottom-up, so "below the eyes" is DECREASING y.
            int ry1 = ey0 - 1;
            int ry0 = Math.Max(0, ry1 - (int)(span * 0.55));
            for (int y = ry0; y <= ry1; y++)
                for (int x = ex0; x <= ex1; x++)
                {
                    int i = y * w + x;
                    if (i >= 0 && i < px.Length && px[i].a > 200) ring.Add(i);
                }
            if (ring.Count == 0) return;

            double face = 0;
            foreach (var i in ring) face += (px[i].r + px[i].g + px[i].b) / 765.0;
            face /= ring.Count;

            // Everything in the eye band meaningfully darker than that cheek.
            // 0.88 rather than 1.0 so ordinary shading does not count as eye.
            for (int y = ey0; y <= ey1; y++)
                for (int x = ex0; x <= ex1; x++)
                {
                    int i = y * w + x;
                    if (i < 0 || i >= px.Length || px[i].a <= 200 || !near[i]) continue;
                    if ((px[i].r + px[i].g + px[i].b) / 765.0 < face * 0.88) eyes.Add(i);
                }
        }

        /// <summary>
        /// How much darker the eyes are than the face around them, as a SHARE of
        /// that face's own lightness — Weber contrast, not a plain difference.
        ///
        /// The difference was the first version and it does not survive contact
        /// with a palette. A black cat's whole head lands near 0.17 of full
        /// light, so every contrast on her is small in absolute terms, and she
        /// scored 20% in all three poses — including the two whose eyes are
        /// plainly intact. That is the coat colour being measured, not the eyes.
        ///
        /// Dividing by the surrounding face cancels it. Tint is very nearly a
        /// multiply — `target * shade` — so a change of coat colour scales the
        /// eye and the cheek by the same factor and drops out of the ratio,
        /// while flattening the eye into the cheek moves it to zero whatever
        /// colour the cat is. That is the thing under test.
        /// </summary>
        private static double EyeContrast(Color32[] px, List<int> eyes, List<int> ring)
        {
            if (eyes.Count == 0 || ring.Count == 0) return double.NaN;
            double e = 0, g = 0;
            foreach (var i in eyes) e += (px[i].r + px[i].g + px[i].b) / 765.0;
            foreach (var i in ring) g += (px[i].r + px[i].g + px[i].b) / 765.0;
            double face = g / ring.Count;
            if (face < 1e-6) return double.NaN;
            return (face - e / eyes.Count) / face;
        }

        /// <summary>
        /// The face, magnified, over an opaque ground. The eye numbers say a
        /// contrast survived; only this says the eye still looks like an eye.
        /// </summary>
        private static void WriteFace(string path, Color32[] px, int w, int h,
                                      List<int> eyes)
        {
            int x0 = w, x1 = -1, y0 = h, y1 = -1;
            if (eyes.Count > 0)
                foreach (var i in eyes)
                {
                    int y = i / w, x = i % w;
                    if (x < x0) x0 = x;
                    if (x > x1) x1 = x;
                    if (y < y0) y0 = y;
                    if (y > y1) y1 = y;
                }
            else
            {
                for (int i = 0; i < px.Length; i++)
                    if (px[i].a > 200)
                    {
                        int y = i / w, x = i % w;
                        if (x < x0) x0 = x;
                        if (x > x1) x1 = x;
                        if (y < y0) y0 = y;
                        if (y > y1) y1 = y;
                    }
                if (x1 < x0) return;
                y0 = y1 - (int)((y1 - y0) * 0.45);   // the head: rows run bottom-up
            }

            int span = Math.Max(4, x1 - x0);
            // Rows run bottom-up, so "below the eyes" — the muzzle — is DECREASING y.
            int cx0 = Math.Max(0, x0 - (int)(span * 2.2));
            int cx1 = Math.Min(w - 1, x1 + (int)(span * 2.2));
            int cy0 = Math.Max(0, y0 - (int)(span * 2.6));
            int cy1 = Math.Min(h - 1, y1 + (int)(span * 1.6));
            int cw = cx1 - cx0 + 1, ch = cy1 - cy0 + 1;
            if (cw < 2 || ch < 2) return;

            const int K = 5;
            var bg = new Color32(0xF2, 0xEA, 0xD9, 255);
            var outPx = new Color32[cw * K * ch * K];
            for (int y = 0; y < ch * K; y++)
                for (int x = 0; x < cw * K; x++)
                {
                    var c = px[(cy0 + y / K) * w + (cx0 + x / K)];
                    float a = c.a / 255f;
                    outPx[y * cw * K + x] = new Color32(
                        (byte)(c.r * a + bg.r * (1 - a)),
                        (byte)(c.g * a + bg.g * (1 - a)),
                        (byte)(c.b * a + bg.b * (1 - a)), 255);
                }
            File.WriteAllBytes(path, Png.Encode(outPx, cw * K, ch * K));
        }

        private static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, "game", "Assets", "Resources")))
                    return dir.FullName;
                dir = dir.Parent;
            }
            throw new DirectoryNotFoundException(
                "no game/Assets/Resources above " + AppContext.BaseDirectory);
        }
    }
}
