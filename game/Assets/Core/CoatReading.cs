using System;

namespace CatShelter.Core
{
    /// <summary>
    /// What one photograph said about a coat. Every trait is nullable and null
    /// means "say nothing", not "average cat": the caller keeps whatever it
    /// already had, which today is <c>solid</c> and <c>short</c>.
    ///
    /// The numbers below are diagnostics, not traits. They exist for the same
    /// reason <c>CatMarks</c> ships deltas instead of verdicts — the thresholds
    /// in <see cref="CoatReader"/> were fitted against a labelled set of
    /// twenty-three photographs, and re-fitting them needs the statistic, not
    /// the answer.
    /// </summary>
    public sealed class CoatReading
    {
        public string BaseColor;
        public string Pattern;
        public string FurLength;

        /// <summary>Pixels the mask called cat.</summary>
        public int BodyPixels;

        /// <summary>Their share of the whole frame.</summary>
        public double BodyShare;

        /// <summary>Per-channel median of the cat's own pixels, 0..1 sRGB.</summary>
        public double MedianR, MedianG, MedianB;

        /// <summary>The palette name the median lands on.</summary>
        public string ColourByMedian;

        /// <summary>The palette name most of her pixels individually land on.</summary>
        public string ColourByVote;

        /// <summary>The same decision taken at the 65th and 80th percentile of
        /// each channel instead of the 50th. Diagnostics: a banded cat's median
        /// sits in her stripes, and how far up the coat one has to go to find
        /// her base colour is the question these answer.</summary>
        public string ColourAtP65, ColourAtP80;

        /// <summary>The median taken over her LIGHTER half only — the fur
        /// between the bands on a tabby. Diagnostic.</summary>
        public string ColourByLightHalf;

        /// <summary>Share of her pixels that voted for <see cref="ColourByVote"/>.</summary>
        public double VoteShare;

        /// <summary>What the shipped mean-of-the-centre would have said, for
        /// comparison; null when it was not computed.</summary>
        public string ColourByMean;

        /// <summary>Median CIE L* of the coat, 0..100.</summary>
        public double BodyLightness;

        /// <summary>p90 − p10 of her L*. Wide on a banded cat, narrow on a
        /// plain one — but also wide on a plain cat in hard sunlight.</summary>
        public double Contrast;

        /// <summary>
        /// Share of her pixels that a morphological closing of the dark half of
        /// her coat adds. Thin stripes have thin light gaps between them and
        /// the closing swallows them; a plain cat's shadow has no gaps to fill.
        /// </summary>
        public double Banding;

        /// <summary>Where Otsu cut her coat into a dark half and a light one,
        /// in L*, and what share of her fell on the dark side.</summary>
        public double DarkCut, DarkShare;

        /// <summary>Mean |L* − blurred L*| over her pixels, at a 3x3 box. Fur
        /// grain, sensor noise and JPEG ringing all raise it, and so does a
        /// small photograph scaled up to the crop, which is why it is a poor
        /// judge of banding on its own.</summary>
        public double HighFrequency;

        /// <summary>
        /// Energy at STRIPE scale: mean |3x3 blur − stripe-wide blur| over her
        /// pixels. This is the number that separates a banded coat from a plain
        /// one, because it is deaf to everything finer than a stripe.
        /// </summary>
        public double Texture;

        /// <summary><see cref="Texture"/> over <see cref="HighFrequency"/>.
        /// Scale-free, so a small photograph blown up to the crop and a sharp
        /// one are judged alike.</summary>
        public double TextureRatio;

        /// <summary>Mask perimeter over the perimeter of its own smoothed
        /// version. 1.0 is a clean outline.</summary>
        public double EdgeRoughness;

        /// <summary>Half-confident mask pixels per pixel of perimeter — how
        /// wide the fuzzy band around her is, in pixels.</summary>
        public double SoftBand;

        /// <summary>Why a trait was left null, in plain words. Never shown to a
        /// player; worth logging on a device build.</summary>
        public string Note = "";

        public override string ToString() =>
            $"{BaseColor ?? "—"}/{Pattern ?? "—"}/{FurLength ?? "—"} " +
            $"body {BodyPixels}px ({BodyShare:P0}) L* {BodyLightness:F1} " +
            $"contrast {Contrast:F1} cut {DarkCut:F1}/{DarkShare:P0} " +
            $"banding {Banding:F3} hf {HighFrequency:F2} tex {Texture:F2} " +
            $"ratio {TextureRatio:F2} " +
            $"edge {EdgeRoughness:F3} soft {SoftBand:F2}";
    }
}
