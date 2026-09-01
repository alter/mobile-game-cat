using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using CatShelter.Core;
using Newtonsoft.Json.Linq;

namespace CatShelter.Tools
{
    /// <summary>
    /// Score <see cref="CoatReader"/> against the labelled photographs.
    ///
    /// The accuracy figures in a report have to come from somewhere a person
    /// can re-run, and "I looked at the pictures" is not that. This reads the
    /// mask dumps <c>tools/coat-probe</c> writes, runs the shipped reader over
    /// them, and prints one line per cat and a scoreboard.
    ///
    /// Pattern is scored PER CLASS on purpose. Twelve of the twenty reference
    /// cats are tabbies, so a reader that answers "tabby" to everything scores
    /// 60% and a single overall percentage hides it.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// What Shell/CatColour.EstimateHere would answer for the same crop:
        /// the arithmetic mean of the central half of the frame, background
        /// and all, matched to the same six anchors. The baseline every figure
        /// in the report is measured against.
        /// </summary>
        private static string Shipped(byte[] rgb, int width, int height)
        {
            int left = width / 4, right = width - width / 4;
            int bottom = height / 4, top = height - height / 4;
            long r = 0, g = 0, b = 0, n = 0;
            for (var y = bottom; y < top; y++)
            for (var x = left; x < right; x++)
            {
                var p = (y * width + x) * 3;
                r += rgb[p]; g += rgb[p + 1]; b += rgb[p + 2]; n++;
            }
            if (n == 0) return null;
            var scale = 255.0 * n;
            return CoatPalette.Nearest(r / scale, g / scale, b / scale);
        }

        // ── Experiment: compare chroma and lightness separately ──────────
        //
        // CoatPalette.NearestIndex is plain squared RGB distance, in which
        // lightness and hue carry equal weight and lightness has the larger
        // range. In Lab the six anchors are:
        //
        //   ginger L65.1 a 9.6 b28.7 C30.2      white  L85.8 a-0.0 b 2.8 C 2.8
        //   grey   L46.6 a 1.0 b 3.5 C 3.7      cream  L75.2 a 0.6 b17.5 C17.5
        //   black  L23.7 a 5.2 b 3.7 C 6.4      brown  L47.6 a 4.9 b 9.7 C10.8
        //
        // black and brown share a hue almost exactly (a 5.2 against 4.9) and
        // are separated by 24 points of L*. So a dark brown tabby sorts to
        // black on lightness alone. Weighting dL down should fix it — and
        // should also collapse black into grey, which differ by 23 of L* and
        // 4 of a*. Hence the chroma gate.

        private static (double L, double A, double B) ToLab(double r, double g, double b)
        {
            double Lin(double c) => c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
            double R = Lin(r), G = Lin(g), Bl = Lin(b);
            var X = (0.4124 * R + 0.3576 * G + 0.1805 * Bl) / 0.95047;
            var Y = 0.2126 * R + 0.7152 * G + 0.0722 * Bl;
            var Z = (0.0193 * R + 0.1192 * G + 0.9505 * Bl) / 1.08883;
            double F(double t) => t > 216.0 / 24389.0
                ? Math.Pow(t, 1.0 / 3.0) : (24389.0 / 27.0 * t + 16.0) / 116.0;
            double fx = F(X), fy = F(Y), fz = F(Z);
            return (116 * fy - 16, 500 * (fx - fy), 200 * (fy - fz));
        }

        private static readonly (string Name, double L, double A, double B)[] LabPalette =
            CoatPalette.Entries.Select(e =>
            {
                var lab = ToLab(e.R, e.G, e.B);
                return (e.Name, lab.L, lab.A, lab.B);
            }).ToArray();

        /// <summary>
        /// Nearest anchor with lightness weighted below hue, and the weight
        /// itself gated on how chromatic the sample is: a near-grey sample is
        /// judged on lightness as before, because black, grey and white are
        /// DEFINED by lightness and nothing else tells them apart.
        /// </summary>
        private static string LabNearest(double r, double g, double b,
                                         double wLow, double cLow, double cHigh)
        {
            var s = ToLab(r, g, b);
            var chroma = Math.Sqrt(s.A * s.A + s.B * s.B);
            var t = chroma <= cLow ? 0.0
                  : chroma >= cHigh ? 1.0
                  : (chroma - cLow) / (cHigh - cLow);
            var wL = 1.0 + t * (wLow - 1.0);

            string best = null;
            var bestScore = double.MaxValue;
            foreach (var e in LabPalette)
            {
                var dL = s.L - e.L;
                var dA = s.A - e.A;
                var dB = s.B - e.B;
                var score = wL * dL * dL + dA * dA + dB * dB;
                if (score >= bestScore) continue;
                bestScore = score;
                best = e.Name;
            }
            return best;
        }

        // ── Experiment: where the reference for the white balance comes from ─
        //
        // The correction that shipped is grey-world over the pixels the mask
        // says are NOT the cat (Core/CoatReader.ReadIlluminant). Four other
        // references were measured against the same twenty-four photographs
        // first, and all four were worse or useless:
        //
        //   White patch — the brightest near-neutral background pixels, on the
        //   theory that a white surface under the lamp IS the lamp. On the
        //   photograph this whole task is about it estimates (0.98,0.99,0.98)
        //   and asks for no correction at all, because the brightest neutral
        //   thing in that room is the WINDOW. The scene is lit twice, daylight
        //   at the window and a warm lamp on the cat, and a white-patch
        //   estimate finds the brighter light and misses the one falling on the
        //   animal. Rejected: it does nothing on the case it was built for.
        //
        //   Grey-world over near-neutral background pixels only (saturation
        //   under 0.2). Same objection, milder: it is dominated by the same
        //   already-neutral surfaces and changes no answer on the set.
        //
        //   Minkowski p=6 over the background ("shades of grey"). Scores one
        //   better than grey-world overall — it moves the outdoor white cat
        //   3-2 from cream to white — but leaves the owner's indoor white cat
        //   on ginger, which is the defect. A point on the scoreboard bought by
        //   not fixing the bug is not a fix.
        //
        //   A local band of background hugging the cat, on the theory that in a
        //   room lit twice, what is BESIDE her is lit the way she is. It is a
        //   good theory and the numbers say it makes no difference: the gains
        //   agree with the whole-background ones to two decimals on every one
        //   of the twenty-four (0.85/1.01/1.19 against 0.88/1.01/1.14 on the
        //   white cat), and where they differ they are worse — her corrected
        //   median lands 0.159 from cream against 0.162 from ginger, a two per
        //   cent margin, where the whole background gives 0.164 against 0.176.
        //   Rejected as complexity that buys nothing.
        //
        // Clamping an out-of-range estimate instead of refusing it was also
        // measured and lost a cat: see CoatReader.MaxIlluminantGain.
        //
        // ── Experiment: read the colour off the HAIRIER half of the mask ─────
        //
        // Rejected, and it is worth writing down because the idea is a good one
        // and the numbers still say no. Fur is grainy at the pixel and an
        // armchair is not, so when a subject mask swallows the furniture — see
        // tools/coat-probe/README.md, which is exactly what ML Kit does to the
        // owner's white cat — the furniture ought to be the smooth part of the
        // mask, and dropping the smoothest half of her pixels ought to leave
        // the cat.
        //
        // It does the opposite on the one photograph it was built for. Taking
        // the grainiest half moves her from cream to GINGER on the Mac mask and
        // from brown to ginger on the device mask, because the grainiest pixels
        // on a long-haired white cat are not her coat — they are the shaded
        // gaps between backlit hairs and the fringe where single hairs meet the
        // room, and both are warmer and more saturated than she is. Measured at
        // three cut-offs over both mask sets, forty-eight masks in all: at the
        // top fifth it scores one better on Mac masks (10/18) by breaking one
        // cat and fixing two, and no better at all on device masks. A rule that
        // helps by a coin-flip and hurts the case it exists for is not a rule.
        //
        // ── Experiment: read the colour off the ERODED CORE of the mask ──────
        //
        // Rejected. The reasoning was general and good — an animal fills the
        // middle of her own cutout, and whatever the segmenter wrongly swept in
        // is attached at the EDGE, so eroding by a radius scaled to the mask's
        // own size should shed the furniture and barely touch a cat that was
        // cut out correctly. Swept at ten strengths from 0.01 to 0.20 of the
        // mask's short side, over both mask sets, scoring colour and pattern.
        //
        // There is no plateau. The gentlest erosion that does anything at all,
        // 0.01, already breaks cat_15 (brown -> ginger) on BOTH mask sets. On
        // device masks 0.03 to 0.08 fixes 1-2 (brown -> grey) while cat_15
        // stays broken, so the total sits at 9/18 and never rises above it.
        // Past 0.12 the pattern reader collapses as well — 9 tabbies of eleven
        // down to 3 on Mac masks — because a stripe measured over a fifth of
        // the animal is no longer a stripe. Every setting is a trade and not
        // one of them is a gain.
        //
        // And the premise is false for the case that motivated it. On the
        // device mask of the owner's white cat the answer is brown at EVERY
        // strength down to 19% of her remaining, and black after that. The
        // armchair is not attached at her edge — she is sitting IN it, so the
        // chair is the deepest interior of that mask and her own thin legs and
        // fluffy outline are the first thing an erosion throws away. Eroding
        // towards the core walks towards the chair. cat_09 (a hand in frame)
        // and cat_20 (two kittens), the other two masks that legitimately hold
        // something that is not one cat, do not improve at any strength either.

        private sealed class Label
        {
            public string File, BaseColor, Pattern, FurLength, Confidence, Note;
            public string Key;
        }

        private static readonly Dictionary<string, string> ShippedAnswer =
            new Dictionary<string, string>();

        public static int Main(string[] args)
        {
            var root = FindRoot();
            var dumps = args.Length > 0 ? args[0] : Path.Combine(root, "tmp", "coat-dumps");
            var labelPath = Path.Combine(root, "fixtures", "reference-photos", "traits-labels.json");

            if (!File.Exists(labelPath))
            {
                Console.Error.WriteLine($"no labels at {labelPath}");
                return 2;
            }
            if (!Directory.Exists(dumps))
            {
                Console.Error.WriteLine(
                    $"no dumps at {dumps} — run tools/coat-probe first (see its README)");
                return 2;
            }

            var labels = ReadLabels(labelPath);
            var rows = new List<(Label label, CoatReading reading)>();
            var missing = new List<string>();

            foreach (var label in labels)
            {
                var path = Path.Combine(dumps, label.Key + ".coat");
                if (!File.Exists(path)) { missing.Add(label.Key); continue; }
                rows.Add((label, ReadDump(path)));
            }

            Console.WriteLine();
            Console.WriteLine("cat        conf  label                    read                     " +
                              "body  L*    contr  band   hf    tex   ratio  light            " +
                              "as shot -> balanced   p65    p80");
            Console.WriteLine(new string('-', 140));
            foreach (var (label, r) in rows)
            {
                var want = $"{label.BaseColor}/{label.Pattern}/{label.FurLength}";
                var got = $"{Mark(r.BaseColor, label.BaseColor)}/" +
                          $"{Mark(r.Pattern, label.Pattern)}/" +
                          $"{Mark(r.FurLength, label.FurLength)}";
                Console.WriteLine(
                    $"{label.Key,-10} {label.Confidence,-5} {want,-24} {got,-24} " +
                    $"{r.BodyShare,4:P0} {r.BodyLightness,5:F1} {r.Contrast,6:F1} " +
                    $"{r.Banding,6:F3} {r.HighFrequency,5:F2} {r.Texture,5:F2} " +
                    $"{r.TextureRatio,5:F2} " +
                    $"{r.GainR:F2}/{r.GainG:F2}/{r.GainB:F2} bg {r.BackgroundShare,4:P0} " +
                    $"{r.ColourByMedian ?? "-",-6} -> {r.ColourBalanced ?? "-",-8} " +
                    $"{r.ColourAtP65 ?? "-",-6} {r.ColourAtP80 ?? "-",-6}");
            }

            if (missing.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine($"no dump for: {string.Join(", ", missing)}");
            }

            Timing(dumps);
            Report("HIGH CONFIDENCE", rows.Where(x => x.label.Confidence == "high").ToList());
            Report("LOW CONFIDENCE (reported, not scored)",
                   rows.Where(x => x.label.Confidence != "high").ToList());
            return 0;
        }

        /// <summary>
        /// What CoatReader costs at the 512 crop the pipeline hands it. Ten
        /// runs over every dump, the first discarded as warm-up, reported as
        /// the median rather than the mean so one scheduling hiccup does not
        /// set the number.
        /// </summary>
        private static void Timing(string dumps)
        {
            var files = Directory.GetFiles(dumps, "*.coat");
            var times = new List<double>();
            foreach (var file in files)
            {
                var bytes = File.ReadAllBytes(file);
                var width = BitConverter.ToInt32(bytes, 4);
                var height = BitConverter.ToInt32(bytes, 8);
                var rgb = new byte[width * height * 3];
                Buffer.BlockCopy(bytes, 16, rgb, 0, rgb.Length);
                var mask = new byte[width * height];
                Buffer.BlockCopy(bytes, 16 + rgb.Length, mask, 0, mask.Length);

                CoatReader.Read(rgb, width, height, mask, width, height);
                var clock = Stopwatch.StartNew();
                for (var i = 0; i < 10; i++)
                    CoatReader.Read(rgb, width, height, mask, width, height);
                times.Add(clock.Elapsed.TotalMilliseconds / 10.0);
            }
            times.Sort();
            Console.WriteLine();
            Console.WriteLine($"── COST — CoatReader.Read at 512x512, {files.Length} photographs, "
                + "10 runs each " + new string('─', 10));
            Console.WriteLine($"median {times[times.Count / 2]:F1} ms   "
                + $"fastest {times[0]:F1} ms   slowest {times[times.Count - 1]:F1} ms");
        }

        /// <summary>The coordinator's proposal: the ground colour between the
        /// bands on a banded cat, the median on everything else.</summary>
        private static string Branch((Label label, CoatReading reading) x) =>
            x.reading.Pattern == "tabby"
                ? x.reading.ColourByLightHalf
                : x.reading.ColourByMedian;

        private static string Grid(List<(Label label, CoatReading reading)> rows,
                                   double w, double cLow, double cHigh) =>
            Score(rows, x => LabNearest(x.reading.MedianR, x.reading.MedianG,
                                        x.reading.MedianB, w, cLow, cHigh));

        private static string GridOf(List<(Label label, CoatReading reading)> rows,
                                     string colour, double w, double cLow, double cHigh) =>
            Grid(rows.Where(x => x.label.BaseColor == colour).ToList(), w, cLow, cHigh);

        private static string ScoreOf(List<(Label label, CoatReading reading)> rows,
                                      string colour,
                                      Func<(Label label, CoatReading reading), string> pick) =>
            Score(rows.Where(x => x.label.BaseColor == colour).ToList(), pick);

        private static string Score(List<(Label label, CoatReading reading)> rows,
                                    Func<(Label label, CoatReading reading), string> pick) =>
            $"{rows.Count(x => pick(x) == x.label.BaseColor)}/{rows.Count}";

        private static string Mark(string got, string want) =>
            got == null ? "—" : got == want ? got : got + "!";

        private static void Report(string title, List<(Label label, CoatReading reading)> rows)
        {
            Console.WriteLine();
            Console.WriteLine($"── {title} — {rows.Count} cats " + new string('─', 40));
            if (rows.Count == 0) return;

            // Split by what the READER decided the pattern was, not by the
            // label: a branch on pattern sees its own verdict, not the truth.
            var banded = rows.Where(x => x.reading.Pattern == "tabby").ToList();
            var plain = rows.Where(x => x.reading.Pattern != "tabby").ToList();
            foreach (var pair in new[] { Tuple.Create("all", rows),
                                         Tuple.Create("read tabby", banded),
                                         Tuple.Create("read plain", plain) })
            {
                if (pair.Item2.Count == 0) continue;
                Console.WriteLine($"colour {pair.Item1,-11} " +
                    $"shipped {Score(pair.Item2, x => ShippedAnswer[x.label.Key])}  " +
                    $"median {Score(pair.Item2, x => x.reading.ColourByMedian)}  " +
                    $"BALANCED {Score(pair.Item2, x => x.reading.ColourBalanced)}  " +
                    $"lighthalf {Score(pair.Item2, x => x.reading.ColourByLightHalf)}  " +
                    $"p65 {Score(pair.Item2, x => x.reading.ColourAtP65)}  " +
                    $"p80 {Score(pair.Item2, x => x.reading.ColourAtP80)}  " +
                    $"BRANCH {Score(pair.Item2, Branch)}");
            }

            // The two cats that decide the metric question, swept across the
            // lightness weight. cat_07 is the one a weighted metric fixes; 2-2
            // is the owner's own cat and the miss this whole task began with.
            // If no weight fixes the first while keeping the second, the metric
            // change is not shippable at any setting — which is the finding.
            foreach (var x in rows.Where(x => x.label.Key == "2-2" || x.label.Key == "cat_07"))
                Console.WriteLine($"  sweep {x.label.Key} (label {x.label.BaseColor}) " +
                    string.Join(" ", new[] { 1.0, 0.8, 0.6, 0.5, 0.4, 0.3, 0.2, 0.1 }
                        .Select(w => $"w{w:F1}:{LabNearest(x.reading.MedianR, x.reading.MedianG, x.reading.MedianB, w, 4, 14)}")));
            foreach (var w in new[] { 1.0, 0.5, 0.3, 0.2, 0.1, 0.05 })
            {
                Console.Write($"metric wL={w,-5:F2} gate 4..14  all {Grid(rows, w, 4, 14)}" +
                              $"  tabby {Grid(banded, w, 4, 14)}  plain {Grid(plain, w, 4, 14)}");
                Console.WriteLine($"  black {GridOf(rows, "black", w, 4, 14)}" +
                                  $"  white {GridOf(rows, "white", w, 4, 14)}" +
                                  $"  grey {GridOf(rows, "grey", w, 4, 14)}");
            }
            Console.WriteLine($"metric RGB (shipped rule) all {Score(rows, x => x.reading.ColourByMedian)}" +
                $"  tabby {Score(banded, x => x.reading.ColourByMedian)}" +
                $"  plain {Score(plain, x => x.reading.ColourByMedian)}" +
                $"  black {ScoreOf(rows, "black", x => x.reading.ColourByMedian)}" +
                $"  white {ScoreOf(rows, "white", x => x.reading.ColourByMedian)}" +
                $"  grey {ScoreOf(rows, "grey", x => x.reading.ColourByMedian)}");

            // The comparison that matters: what the shipped code would say.
            // It hard-codes solid and short, so its pattern score is the count
            // of cats that really are solid.
            foreach (var group in new[] { "solid", "tabby" })
            {
                var of = rows.Where(x => x.label.Pattern == group).ToList();
                if (of.Count == 0) continue;
                var right = of.Count(x => x.reading.Pattern == group);
                var silent = of.Count(x => x.reading.Pattern == null);
                Console.WriteLine($"pattern    {group,-6} {right}/{of.Count} right, " +
                                  $"{silent} said nothing, " +
                                  $"{of.Count - right - silent} wrong");
            }
            var other = rows.Where(x => x.label.Pattern != "solid" && x.label.Pattern != "tabby")
                            .ToList();
            foreach (var x in other)
                Console.WriteLine($"pattern    {x.label.Key} is labelled " +
                                  $"{x.label.Pattern}, outside solid/tabby; read " +
                                  $"{x.reading.Pattern ?? "nothing"}");

            // What discounting the light actually did, cat by cat. A correction
            // is only worth having if this list is short and every line on it
            // is an improvement — a long list is a metric being reshuffled.
            var moved = rows.Where(x => x.reading.ColourBalanced != x.reading.ColourByMedian)
                            .ToList();
            var applied = rows.Count(x => Math.Abs(x.reading.GainR - 1.0) > 0.005 ||
                                          Math.Abs(x.reading.GainG - 1.0) > 0.005 ||
                                          Math.Abs(x.reading.GainB - 1.0) > 0.005);
            Console.WriteLine($"light      estimated on {applied} of {rows.Count}, " +
                              $"changed the answer on {moved.Count}" +
                              (moved.Count == 0 ? "" : ": " + string.Join(", ", moved.Select(x =>
                                  $"{x.label.Key} {x.reading.ColourByMedian}->" +
                                  $"{x.reading.ColourBalanced} (label {x.label.BaseColor})"))));

            var furSaid = rows.Count(x => x.reading.FurLength != null);
            var longs = rows.Where(x => x.label.FurLength == "long").ToList();
            Console.WriteLine($"fur        {furSaid} answered of {rows.Count}; " +
                              $"{longs.Count} long-haired in this group " +
                              $"({string.Join(",", longs.Select(x => x.label.Key))})");
            Console.WriteLine($"           soft band: short {Range(rows, "short")}  " +
                              $"long {Range(rows, "long")}");
            Console.WriteLine($"           roughness: short {RangeR(rows, "short")}  " +
                              $"long {RangeR(rows, "long")}");
        }

        private static string Range(List<(Label label, CoatReading reading)> rows, string fur)
        {
            var values = rows.Where(x => x.label.FurLength == fur)
                             .Select(x => x.reading.SoftBand).ToList();
            return values.Count == 0 ? "—"
                : $"{values.Min():F2}..{values.Max():F2}";
        }

        private static string RangeR(List<(Label label, CoatReading reading)> rows, string fur)
        {
            var values = rows.Where(x => x.label.FurLength == fur)
                             .Select(x => x.reading.EdgeRoughness).ToList();
            return values.Count == 0 ? "—"
                : $"{values.Min():F2}..{values.Max():F2}";
        }

        private static List<Label> ReadLabels(string path)
        {
            var json = JObject.Parse(File.ReadAllText(path));
            var labels = new List<Label>();
            foreach (var key in new[] { "cats", "owner_cats" })
            {
                foreach (var entry in (JArray)json[key])
                {
                    var file = (string)entry["file"];
                    labels.Add(new Label
                    {
                        File = file,
                        Key = Path.GetFileNameWithoutExtension(file),
                        BaseColor = (string)entry["base_color"],
                        Pattern = (string)entry["pattern"],
                        FurLength = (string)entry["fur_length"],
                        Confidence = (string)entry["confidence"],
                        Note = (string)entry["note"],
                    });
                }
            }
            return labels;
        }

        /// <summary>
        /// The dump <c>tools/coat-probe/coat-dump.swift</c> writes:
        /// "COAT", int32 width, int32 height, int32 hasMask, then w*h*3 bytes
        /// of sRGB and w*h bytes of mask. Little-endian.
        /// </summary>
        private static CoatReading ReadDump(string path)
        {
            var bytes = File.ReadAllBytes(path);
            if (bytes.Length < 16 || bytes[0] != 'C' || bytes[1] != 'O' ||
                bytes[2] != 'A' || bytes[3] != 'T')
                throw new InvalidDataException($"{path} is not a COAT dump");

            var width = BitConverter.ToInt32(bytes, 4);
            var height = BitConverter.ToInt32(bytes, 8);
            var hasMask = BitConverter.ToInt32(bytes, 12) != 0;
            var rgb = new byte[width * height * 3];
            Buffer.BlockCopy(bytes, 16, rgb, 0, rgb.Length);

            byte[] mask = null;
            if (hasMask)
            {
                mask = new byte[width * height];
                Buffer.BlockCopy(bytes, 16 + rgb.Length, mask, 0, mask.Length);
            }
            ShippedAnswer[Path.GetFileNameWithoutExtension(path)] =
                Shipped(rgb, width, height);
            return CoatReader.Read(rgb, width, height, mask,
                                   hasMask ? width : 0, hasMask ? height : 0);
        }

        private static string FindRoot()
        {
            var here = new DirectoryInfo(AppContext.BaseDirectory);
            while (here != null &&
                   !Directory.Exists(Path.Combine(here.FullName, "fixtures")))
                here = here.Parent;
            return here?.FullName ?? Directory.GetCurrentDirectory();
        }
    }
}
