using System;

namespace CatShelter.Core
{
    /// <summary>
    /// Read a coat off a photograph and a subject mask, on the device, for
    /// free.
    ///
    /// <para><b>Why this is managed code and not two native copies.</b> Both
    /// platforms already make a subject mask — ML Kit's subject segmentation on
    /// Android, <c>VNGenerateForegroundInstanceMaskRequest</c> on iOS — and
    /// both can hand the bytes across. Everything after that is arithmetic over
    /// two byte arrays, so it lives here, once, where <c>dotnet test</c> can
    /// run it on a fixture and where a threshold can be re-fitted without a
    /// device build. The alternative was a Swift copy and a Java copy of three
    /// statistics with nothing able to compare them, which is precisely the
    /// complaint <c>Shell/CatColour.cs</c> already makes about the six palette
    /// anchors.</para>
    ///
    /// <para><b>Silence beats a wrong answer.</b> Every trait comes back null
    /// unless the measurement clears a margin, and null means the caller keeps
    /// what it has. A cat wrongly called a tabby is worse than a cat called
    /// plain, because the plain answer is the one the game has always given and
    /// nobody has ever been surprised by it.</para>
    /// </summary>
    public static class CoatReader
    {
        /// <summary>
        /// Mask values at or above this are her. 128 of 255, the same "more cat
        /// than not" cut <c>CatMarks.swift</c>'s <c>binarise</c> makes at 0.5.
        /// </summary>
        public const byte MaskCut = 128;

        /// <summary>
        /// A mask covering less of the frame than this is not a cat we can
        /// measure — a sliver of ear, or a segmenter that latched onto a
        /// cushion. The crop already centred and squared her, so a real cat
        /// fills a large part of it.
        /// </summary>
        public const double MinBodyShare = 0.06;

        // ── Pattern ─────────────────────────────────────────────────────────
        //
        // Two numbers have to agree before a cat is called a tabby, and the
        // thresholds are set from the twenty-three labelled photographs in
        // fixtures/reference-photos/traits-labels.json. See
        // Tests/Core/CoatReaderFixtureTests.cs, which is where they are checked.

        /// <summary>
        /// Stripe-scale texture, in CIE L* points, above which a coat is
        /// banded. Fitted on the seventeen high-confidence cats of
        /// <c>fixtures/reference-photos/traits-labels.json</c>: the five plain
        /// coats measure 2.88 to 4.21 and the eleven tabbies 2.31 to 7.17, and
        /// 4.5 sits in the gap between the highest plain cat and the lowest
        /// tabby this catches. It takes nine of the eleven tabbies and calls no
        /// plain cat striped.
        ///
        /// The two tabbies it misses are the two smallest photographs in the
        /// set — 220x300 and 500x407 — blown up to the 512 crop, where the
        /// bands are softer than the threshold. They come back with no pattern,
        /// which is the plain cat the game has always drawn.
        /// </summary>
        public const double TabbyTexture = 4.5;

        /// <summary>…and her L* has to spread at least this far as well, so a
        /// flat coat cannot be banded by noise alone. Every tabby in the
        /// labelled set clears 36; this is a floor against the pathological
        /// case, not a discriminator.</summary>
        public const double TabbyContrast = 20.0;

        // ── Fur ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Set deliberately out of reach. See <see cref="ReadFur"/>: the
        /// labelled set holds one plausible long-hair in twenty and the
        /// statistic cannot be validated on it, so fur length is measured,
        /// reported and never asserted.
        /// </summary>
        /// <remarks>A <c>static readonly</c> and not a <c>const</c>, so that
        /// the measurement below it stays live code the compiler checks rather
        /// than a block it folds away unreachable. The day there are enough
        /// long-haired cats to fit a threshold on, this is the one line that
        /// changes.</remarks>
        public static readonly bool FurLengthEnabled = false;

        public const double LongFurSoftBand = 3.0;
        public const double LongFurRoughness = 1.20;

        /// <summary>
        /// Measure a coat.
        /// </summary>
        /// <param name="rgb">Tightly packed 8-bit sRGB, three bytes per pixel,
        /// row-major, origin top-left.</param>
        /// <param name="mask">One byte per pixel over the WHOLE image, 0
        /// outside her and up to 255 inside — exactly what
        /// <c>CatSilhouette.mask</c> holds. May be a different size from the
        /// image; it is sampled with the mapping <c>CatSilhouette</c>
        /// documents. Null or empty is not an error.</param>
        /// <returns>Never null, and never throws for a photograph it cannot
        /// read: the capture screen must always produce a cat.</returns>
        public static CoatReading Read(byte[] rgb, int width, int height,
                                       byte[] mask, int maskWidth, int maskHeight)
        {
            var reading = new CoatReading();
            if (rgb == null || width <= 0 || height <= 0 ||
                rgb.Length < (long)width * height * 3)
            {
                reading.Note = "no pixels";
                return reading;
            }
            if (mask == null || maskWidth <= 0 || maskHeight <= 0 ||
                mask.Length < (long)maskWidth * maskHeight)
            {
                reading.Note = "no mask, so nothing here can tell her coat from the sofa";
                return reading;
            }

            var pixels = width * height;
            var inside = new bool[pixels];
            var soft = 0;
            int minX = width, minY = height, maxX = -1, maxY = -1, body = 0;

            for (var y = 0; y < height; y++)
            {
                var maskRow = (y * maskHeight / height) * maskWidth;
                var row = y * width;
                for (var x = 0; x < width; x++)
                {
                    var value = mask[maskRow + x * maskWidth / width];
                    // The half-confident band is fur the segmenter could not
                    // commit to. Counted before binarising, because binarising
                    // is what throws it away.
                    if (value >= 32 && value <= 223) soft++;
                    if (value < MaskCut) continue;
                    inside[row + x] = true;
                    body++;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }

            reading.BodyPixels = body;
            reading.BodyShare = (double)body / pixels;
            if (reading.BodyShare < MinBodyShare)
            {
                reading.Note = $"mask covers only {reading.BodyShare:P1} of the frame";
                return reading;
            }

            var lightness = Lightness(rgb, inside, pixels);
            ReadColour(rgb, inside, body, lightness, reading);
            ReadPattern(lightness, inside, width, height,
                        minX, minY, maxX, maxY, body, reading);
            ReadFur(inside, width, height, minX, minY, maxX, maxY, soft, reading);
            return reading;
        }

        // ── Colour ──────────────────────────────────────────────────────────

        /// <summary>
        /// Her colour, over her own pixels, by two statistics.
        ///
        /// <para>Not a mean. The shipped estimate takes the arithmetic mean of
        /// the central half of the frame with the background in it, and a mean
        /// is the wrong statistic twice over: it averages the carpet in, and on
        /// a tabby — dark bands on light fur — it lands between the two, on a
        /// colour that is nowhere on the animal.</para>
        ///
        /// <para>The median of each channel is a colour she actually is, and
        /// the vote is the palette name most of her pixels individually chose.
        /// They agree on most cats; where they disagree, the median wins,
        /// because a vote is decided by the largest region and on a
        /// black-and-white cat that is whichever half is bigger.</para>
        /// </summary>
        private static void ReadColour(byte[] rgb, bool[] inside, int body,
                                       byte[] lightness, CoatReading reading)
        {
            var lit = new int[256];
            for (var i = 0; i < inside.Length; i++)
                if (inside[i]) lit[lightness[i]]++;
            var half = Quantile(lit, body, 0.5);
            var lightRed = new int[256];
            var lightGreen = new int[256];
            var lightBlue = new int[256];
            var lightBody = 0;

            var red = new int[256];
            var green = new int[256];
            var blue = new int[256];
            var votes = new int[CoatPalette.Entries.Length];

            for (var i = 0; i < inside.Length; i++)
            {
                if (!inside[i]) continue;
                var p = i * 3;
                var r = rgb[p];
                var g = rgb[p + 1];
                var b = rgb[p + 2];
                red[r]++;
                green[g]++;
                blue[b]++;
                var choice = CoatPalette.NearestIndex(r / 255.0, g / 255.0, b / 255.0);
                if (choice >= 0) votes[choice]++;
                if (lightness[i] < half) continue;
                lightRed[r]++;
                lightGreen[g]++;
                lightBlue[b]++;
                lightBody++;
            }

            reading.MedianR = Quantile(red, body, 0.5) / 255.0;
            reading.MedianG = Quantile(green, body, 0.5) / 255.0;
            reading.MedianB = Quantile(blue, body, 0.5) / 255.0;
            reading.ColourByMedian = CoatPalette.Nearest(
                reading.MedianR, reading.MedianG, reading.MedianB);

            reading.ColourByLightHalf = lightBody > 0
                ? At(lightRed, lightGreen, lightBlue, lightBody, 0.5) : null;
            reading.ColourAtP65 = At(red, green, blue, body, 0.65);
            reading.ColourAtP80 = At(red, green, blue, body, 0.80);

            var winner = 0;
            for (var i = 1; i < votes.Length; i++)
                if (votes[i] > votes[winner]) winner = i;
            reading.VoteShare = body > 0 ? (double)votes[winner] / body : 0.0;
            reading.ColourByVote = CoatPalette.Entries[winner].Name;

            reading.BaseColor = reading.ColourByMedian;
            if (reading.BaseColor == null)
                reading.Note += "colour: the median landed on no palette name. ";
        }

        private static string At(int[] red, int[] green, int[] blue,
                                 int body, double fraction) =>
            CoatPalette.Nearest(Quantile(red, body, fraction) / 255.0,
                                Quantile(green, body, fraction) / 255.0,
                                Quantile(blue, body, fraction) / 255.0);

        // ── Pattern ─────────────────────────────────────────────────────────

        /// <summary>
        /// CIE L*, 0..100, stored to a byte per pixel — 0.39 L* a step, finer
        /// than anything below looks at, and the same 256-bin resolution
        /// <c>CatMarks.swift</c> settled on for the marks.
        /// </summary>
        private static byte[] Lightness(byte[] rgb, bool[] inside, int pixels)
        {
            var table = LinearTable;
            var result = new byte[pixels];
            for (var i = 0; i < pixels; i++)
            {
                if (!inside[i]) continue;
                var p = i * 3;
                var y = 0.2126 * table[rgb[p]]
                      + 0.7152 * table[rgb[p + 1]]
                      + 0.0722 * table[rgb[p + 2]];
                var l = y > 216.0 / 24389.0
                    ? 116.0 * Math.Pow(y, 1.0 / 3.0) - 16.0
                    : y * (24389.0 / 27.0);
                var bin = (int)Math.Round(l / 100.0 * 255.0);
                result[i] = (byte)(bin < 0 ? 0 : bin > 255 ? 255 : bin);
            }
            return result;
        }

        private static readonly double[] LinearTable = BuildLinearTable();

        private static double[] BuildLinearTable()
        {
            var table = new double[256];
            for (var i = 0; i < 256; i++)
            {
                var v = i / 255.0;
                table[i] = v <= 0.04045 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
            }
            return table;
        }

        /// <summary>
        /// Solid or tabby, from the shape of the dark half of her coat.
        ///
        /// <para>The dark half is thresholded on a BLURRED lightness field, not
        /// the raw one. Fur is noisy at the pixel — a plain black cat has
        /// thousands of pixels either side of her own median — and morphology
        /// on that measures the noise, not the animal. A 3×3 box first, and a
        /// dead band scaled to her own interquartile spread, leave only
        /// structure.</para>
        ///
        /// <para>Then a closing at stripe scale: dilate the dark set and erode
        /// it back. What the closing ADDS is the light gaps thin enough to
        /// swallow — which is what a tabby's banding is and what a plain cat's
        /// single soft shadow is not.</para>
        /// </summary>
        private static void ReadPattern(byte[] lightness, bool[] inside,
                                        int width, int height,
                                        int minX, int minY, int maxX, int maxY,
                                        int body, CoatReading reading)
        {
            var histogram = new int[256];
            for (var i = 0; i < inside.Length; i++)
                if (inside[i]) histogram[lightness[i]]++;

            const double toL = 100.0 / 255.0;
            var median = Quantile(histogram, body, 0.5);
            reading.BodyLightness = median * toL;
            var p10 = Quantile(histogram, body, 0.10);
            var p90 = Quantile(histogram, body, 0.90);
            var p25 = Quantile(histogram, body, 0.25);
            var p75 = Quantile(histogram, body, 0.75);
            reading.Contrast = (p90 - p10) * toL;

            var blurred = Blur3(lightness, inside, width, height);

            var sum = 0.0;
            for (var i = 0; i < inside.Length; i++)
                if (inside[i]) sum += Math.Abs(lightness[i] - blurred[i]) * toL;
            reading.HighFrequency = body > 0 ? sum / body : 0.0;

            // The structuring element scales with the cat, not with the image:
            // her stripes are a fixed fraction of her, and the crop can hold her
            // tight or loose.
            var span = Math.Min(maxX - minX + 1, maxY - minY + 1);

            // Energy at STRIPE scale, and it is the number that decides.
            //
            // The first version judged banding on the closing alone and called
            // eleven tabbies out of eleven — and four plain cats out of five as
            // well, which is the "answers tabby to everything" reader the
            // ground truth warns about. Two black cats scored 0.19 and 0.21
            // banding with no band on them anywhere: on a very dark coat the L*
            // histogram is compressed, Otsu splits sheen from shadow, and the
            // "gaps" a closing fills are sensor noise.
            //
            // A 3x3 residual alone is no better, because it is deaf to nothing:
            // grain, ringing and a small photograph blown up to 512 all move it.
            // The difference between the two blurs is what is left — energy
            // coarser than grain and finer than the animal — and dividing by
            // the fine residual makes it scale-free, so cat_07 at 220x300 and
            // cat_19 at 300x281 are judged the same way.
            var wide = (int)Math.Round(0.03 * span);
            if (wide < 3) wide = 3;
            if (wide > 24) wide = 24;
            var smooth = BlurBox(blurred, inside, width, height, wide);
            var coarse = 0.0;
            for (var i = 0; i < inside.Length; i++)
                if (inside[i]) coarse += Math.Abs(blurred[i] - smooth[i]) * toL;
            reading.Texture = body > 0 ? coarse / body : 0.0;
            reading.TextureRatio = reading.HighFrequency > 0.01
                ? reading.Texture / reading.HighFrequency : 0.0;

            // A quarter of her own interquartile spread, floored so that a
            // uniformly lit plain cat cannot produce a band of nothing, and
            // capped so that a high-contrast tabby's stripes are not all thrown
            // away with the shading.
            // Where to cut her coat into a dark half and a light one.
            //
            // **Not her median, and the first version's failure is the
            // argument.** "Median minus a dead band" reads well and is wrong
            // for exactly the animal this measurement exists for: on a cat
            // whose coat really is two tones, the median falls INSIDE the
            // darker of them, and anything subtracted from it lands below the
            // whole coat. The dark set came back empty and a textbook tabby
            // scored zero banding (CoatReaderTests.ABandedCoatIsATabby, first
            // run).
            //
            // Otsu's threshold instead — the cut that best separates two
            // groups, which is the question actually being asked. On a banded
            // cat it lands between the bands; on a plain one it lands between
            // her lit side and her shadow, and those are two large smooth
            // regions with no thin gaps for a closing to fill, which is the
            // answer we want there too.
            var cut = Otsu(histogram);
            reading.DarkCut = cut * toL;

            var dark = new bool[inside.Length];
            var darkCount = 0;
            for (var i = 0; i < inside.Length; i++)
            {
                if (!inside[i] || blurred[i] >= cut) continue;
                dark[i] = true;
                darkCount++;
            }

            // A cut that put almost everything on one side did not find two
            // groups; it found one, and the "gaps" it would report are the
            // grain of the fur. Say nothing rather than measure noise.
            var darkShare = (double)darkCount / body;
            reading.DarkShare = darkShare;
            if (darkShare < 0.04 || darkShare > 0.80)
            {
                reading.Banding = 0.0;
                return;
            }

            // Kept as a diagnostic and no longer used to decide. It was the
            // first attempt and it failed in the direction that matters: on the
            // labelled set it called eleven tabbies out of eleven AND four of
            // the five plain cats striped, because on a dark coat Otsu splits
            // sheen from shadow and a closing fills the noise between them.
            // `Texture` above is what replaced it. The numbers stay because
            // re-fitting a threshold needs the statistic that lost as well as
            // the one that won.
            //
            // Three scales, and the strongest wins. One scale is not enough:
            // a closing only fills a gap it can span, so a single radius reads
            // a mackerel tabby's fine stripes and a spotted cat's wide ones as
            // different animals. Both bracket a real coat, and the widest here
            // — a twelfth of her short side — is still far under the size of
            // the single soft shadow a plain cat carries, which is the thing
            // that must NOT fill.
            reading.Banding = 0.0;
            foreach (var fraction in new[] { 0.012, 0.024, 0.040 })
            {
                var radius = (int)Math.Round(fraction * span);
                if (radius < 2) radius = 2;
                if (radius > 20) radius = 20;

                var closed = Erode(Dilate(dark, width, height, radius), width, height, radius);
                var added = 0;
                for (var i = 0; i < inside.Length; i++)
                    if (inside[i] && closed[i] && !dark[i]) added++;
                var share = body > 0 ? (double)added / body : 0.0;
                if (share > reading.Banding) reading.Banding = share;
            }

            // **Only "tabby" is ever claimed, and never "solid".**
            //
            // Not timidity — the two answers are the same answer. A null
            // pattern makes CatTraits.FromColourOnly keep `solid`, which is
            // what the game has drawn since the offline path existed, so
            // saying "solid" out loud and saying nothing produce the identical
            // kitten. Given that, there is no reason to take on the risk of the
            // positive claim, and there is a reason not to: on this set the
            // plain cats and the two softest tabbies overlap, so any threshold
            // that called plain cats plain would also have called two real
            // tabbies plain — a wrong answer bought for no gain at all.
            //
            // So the reader answers one question, "is she banded", and the
            // silence is already the other answer.
            if (reading.Texture >= TabbyTexture && reading.Contrast >= TabbyContrast)
                reading.Pattern = "tabby";
            else
                reading.Note += $"pattern: texture {reading.Texture:F2} is under " +
                                $"{TabbyTexture}, so no banding is claimed and the " +
                                "plain coat the game already draws stands. ";
        }

        // ── Fur ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Measured, reported, and deliberately never asserted.
        ///
        /// <para>A long-haired cat's outline is ragged and her mask has a wide
        /// half-confident band around it; a short-haired one's is clean. Both
        /// numbers are computed here and both are in the reading. What is not
        /// here is a verdict, and the reason is the ground truth rather than
        /// the arithmetic: the labelled set holds ONE plausible long-hair among
        /// twenty reference cats, and its own note calls it medium. A threshold
        /// fitted to one positive example is a threshold fitted to one
        /// photograph. So <see cref="FurLengthEnabled"/> is false, the caller
        /// keeps <c>short</c>, and the day there are twenty long-haired cats to
        /// score against, the constants above are where to start.</para>
        /// </summary>
        private static void ReadFur(bool[] inside, int width, int height,
                                    int minX, int minY, int maxX, int maxY,
                                    int softPixels, CoatReading reading)
        {
            var perimeter = Perimeter(inside, width, height);
            if (perimeter <= 0)
            {
                reading.Note += "fur: no outline to measure. ";
                return;
            }

            var span = Math.Min(maxX - minX + 1, maxY - minY + 1);
            var radius = (int)Math.Round(0.02 * span);
            if (radius < 2) radius = 2;
            if (radius > 12) radius = 12;

            var smoothed = Dilate(Erode(
                Erode(Dilate(inside, width, height, radius), width, height, radius),
                width, height, radius), width, height, radius);
            var smoothPerimeter = Perimeter(smoothed, width, height);

            reading.EdgeRoughness = smoothPerimeter > 0
                ? (double)perimeter / smoothPerimeter : 0.0;
            reading.SoftBand = (double)softPixels / perimeter;

            if (!FurLengthEnabled)
            {
                reading.Note += "fur: measured, not claimed — one long-hair in the " +
                                "labelled set is not enough to fit a threshold on. ";
                return;
            }
            if (reading.SoftBand >= LongFurSoftBand && reading.EdgeRoughness >= LongFurRoughness)
                reading.FurLength = "long";
            else if (reading.SoftBand < LongFurSoftBand * 0.6)
                reading.FurLength = "short";
        }

        // ── Arithmetic ──────────────────────────────────────────────────────

        /// <summary>
        /// Otsu's threshold over a 256-bin histogram: the bin that maximises
        /// the variance BETWEEN the two groups it makes, which is the same as
        /// minimising the variance inside them. One pass, no parameters, and
        /// the standard answer to "cut this into two" since 1979.
        /// </summary>
        private static double Otsu(int[] histogram)
        {
            long total = 0, weighted = 0;
            for (var i = 0; i < histogram.Length; i++)
            {
                total += histogram[i];
                weighted += (long)i * histogram[i];
            }
            if (total == 0) return 0;

            long belowCount = 0, belowSum = 0;
            var bestScore = -1.0;
            var cut = (double)weighted / total;
            for (var i = 0; i < histogram.Length; i++)
            {
                belowCount += histogram[i];
                if (belowCount == 0) continue;
                var aboveCount = total - belowCount;
                if (aboveCount == 0) break;
                belowSum += (long)i * histogram[i];

                var belowMean = (double)belowSum / belowCount;
                var aboveMean = (double)(weighted - belowSum) / aboveCount;
                var gap = belowMean - aboveMean;
                var score = (double)belowCount * aboveCount * gap * gap;
                if (score <= bestScore) continue;
                bestScore = score;

                // Halfway between the two group MEANS, not the bin index that
                // scored best. On a coat with two clean tones every bin in the
                // empty valley between them scores identically, the loop keeps
                // the first, and that first bin is the top of the dark mode
                // itself — so a cut there puts the entire dark half on the
                // light side and the dark set comes back empty. It did, and
                // the tabby in CoatReaderTests scored zero banding twice
                // before this line was written.
                cut = (belowMean + aboveMean) / 2.0;
            }
            return cut;
        }

        /// <summary>The value at <paramref name="fraction"/> of a 256-bin
        /// histogram holding <paramref name="count"/> samples.</summary>
        private static int Quantile(int[] histogram, int count, double fraction)
        {
            if (count <= 0) return 0;
            var target = (int)(count * fraction);
            var seen = 0;
            for (var i = 0; i < histogram.Length; i++)
            {
                seen += histogram[i];
                if (seen > target) return i;
            }
            return histogram.Length - 1;
        }

        /// <summary>3×3 box blur of the lightness field, inside the mask only.
        /// Separable, so two passes rather than nine reads a pixel.</summary>
        private static byte[] Blur3(byte[] source, bool[] inside, int width, int height)
        {
            var pass = new int[source.Length];
            var counts = new int[source.Length];
            for (var y = 0; y < height; y++)
            {
                var row = y * width;
                for (var x = 0; x < width; x++)
                {
                    var sum = 0;
                    var n = 0;
                    for (var dx = -1; dx <= 1; dx++)
                    {
                        var sx = x + dx;
                        if (sx < 0 || sx >= width) continue;
                        if (!inside[row + sx]) continue;
                        sum += source[row + sx];
                        n++;
                    }
                    pass[row + x] = sum;
                    counts[row + x] = n;
                }
            }

            var result = new byte[source.Length];
            for (var y = 0; y < height; y++)
            {
                var row = y * width;
                for (var x = 0; x < width; x++)
                {
                    if (!inside[row + x]) continue;
                    var sum = 0;
                    var n = 0;
                    for (var dy = -1; dy <= 1; dy++)
                    {
                        var sy = y + dy;
                        if (sy < 0 || sy >= height) continue;
                        sum += pass[sy * width + x];
                        n += counts[sy * width + x];
                    }
                    result[row + x] = n > 0 ? (byte)(sum / n) : source[row + x];
                }
            }
            return result;
        }

        /// <summary>
        /// Box blur of any radius over the masked pixels, separable and with a
        /// running sum, so the window costs one add and one subtract however
        /// wide it is. Pixels outside the mask are left out of both the sum and
        /// the count, so the blur never mixes the sofa into her coat.
        /// </summary>
        private static byte[] BlurBox(byte[] source, bool[] inside,
                                      int width, int height, int radius)
        {
            var sums = new int[source.Length];
            var counts = new int[source.Length];
            for (var y = 0; y < height; y++)
            {
                var row = y * width;
                int sum = 0, n = 0;
                for (var x = 0; x < width + radius; x++)
                {
                    if (x < width && inside[row + x]) { sum += source[row + x]; n++; }
                    var leaving = x - 2 * radius - 1;
                    if (leaving >= 0 && inside[row + leaving])
                    {
                        sum -= source[row + leaving];
                        n--;
                    }
                    var centre = x - radius;
                    if (centre < 0 || centre >= width) continue;
                    sums[row + centre] = sum;
                    counts[row + centre] = n;
                }
            }

            var result = new byte[source.Length];
            for (var x = 0; x < width; x++)
            {
                int sum = 0, n = 0;
                for (var y = 0; y < height + radius; y++)
                {
                    if (y < height) { sum += sums[y * width + x]; n += counts[y * width + x]; }
                    var leaving = y - 2 * radius - 1;
                    if (leaving >= 0)
                    {
                        sum -= sums[leaving * width + x];
                        n -= counts[leaving * width + x];
                    }
                    var centre = y - radius;
                    if (centre < 0 || centre >= height) continue;
                    var i = centre * width + x;
                    if (!inside[i]) continue;
                    result[i] = n > 0 ? (byte)(sum / n) : source[i];
                }
            }
            return result;
        }

        /// <summary>Square-window dilation, separable, O(1) a pixel: a running
        /// count of true in the window rather than a scan of it.</summary>
        private static bool[] Dilate(bool[] source, int width, int height, int radius) =>
            Sweep(source, width, height, radius, true);

        private static bool[] Erode(bool[] source, int width, int height, int radius) =>
            Sweep(source, width, height, radius, false);

        /// <summary>
        /// One separable morphological pass. Dilation is "any true in the
        /// window"; erosion is "no false in the window", where everything past
        /// the edge of the image counts as false — so a cat touching the frame
        /// erodes at that edge, which is the honest reading of a cat we can
        /// only see part of.
        /// </summary>
        private static bool[] Sweep(bool[] source, int width, int height,
                                    int radius, bool dilate)
        {
            var span = 2 * radius + 1;
            var pass = new bool[source.Length];
            for (var y = 0; y < height; y++)
            {
                var row = y * width;
                // `hits` counts the pixels of the window that decide the
                // answer: the true ones for a dilation, the false ones for an
                // erosion. Kept as a running total, so the window costs one add
                // and one subtract however wide it is.
                var hits = 0;
                for (var x = 0; x < width + radius; x++)
                {
                    if (x < width && source[row + x] == dilate) hits++;
                    var leaving = x - span;
                    if (leaving >= 0 && source[row + leaving] == dilate) hits--;
                    var centre = x - radius;
                    if (centre < 0 || centre >= width) continue;
                    pass[row + centre] = dilate
                        ? hits > 0
                        : hits == 0 && centre >= radius && centre + radius < width;
                }
            }

            var result = new bool[source.Length];
            for (var x = 0; x < width; x++)
            {
                var hits = 0;
                for (var y = 0; y < height + radius; y++)
                {
                    if (y < height && pass[y * width + x] == dilate) hits++;
                    var leaving = y - span;
                    if (leaving >= 0 && pass[leaving * width + x] == dilate) hits--;
                    var centre = y - radius;
                    if (centre < 0 || centre >= height) continue;
                    result[centre * width + x] = dilate
                        ? hits > 0
                        : hits == 0 && centre >= radius && centre + radius < height;
                }
            }
            return result;
        }

        /// <summary>Pixels inside with a 4-neighbour outside, the image edge
        /// counting as outside.</summary>
        private static int Perimeter(bool[] set, int width, int height)
        {
            var total = 0;
            for (var y = 0; y < height; y++)
            {
                var row = y * width;
                for (var x = 0; x < width; x++)
                {
                    if (!set[row + x]) continue;
                    if (x == 0 || x == width - 1 || y == 0 || y == height - 1 ||
                        !set[row + x - 1] || !set[row + x + 1] ||
                        !set[row - width + x] || !set[row + width + x]) total++;
                }
            }
            return total;
        }
    }
}
