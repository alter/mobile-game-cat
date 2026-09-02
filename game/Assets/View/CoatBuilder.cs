using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CatShelter.Core;
using UnityEngine;

namespace CatShelter.View
{
    /// <summary>
    /// Task 60-shell-build/18: turn one greyscale silhouette plus a
    /// <see cref="CatTraits"/> into the player's own cat.
    ///
    /// The art ships without colour on purpose (40-art/03), and three files
    /// have to cover an arc the brief describes with six. What separates the
    /// three states is applied here rather than drawn:
    ///
    ///   state 1  body narrowed, coat dulled and dirtied, tufts along the edge
    ///   state 2  a trace of the same
    ///   state 3  clean, body very slightly rounder
    ///
    /// This was the owner's call and it is the right one — a cat that reads as
    /// merely fed rather than rescued was the finding from the playtest of the
    /// three delivered files, and the difference between the states is coat and
    /// frame, not expression.
    ///
    /// Done as a one-off CPU pass at load, not a pixel shader. The tufts are
    /// strands grown along the contour normal, which needs the whole alpha
    /// channel and a random walk per strand; in a fragment shader that is both
    /// expensive and inexact, and there are three textures to build, once.
    /// </summary>
    public static class CoatBuilder
    {
        // Canon outline: one thickness for the whole set, dark walnut
        // (art-prompts.md section 1). The props carry it and the cats do not,
        // so it is added here rather than sent back to be redrawn.
        private static readonly Color32 Ink = new(0x4A, 0x3B, 0x28, 0xFF);

        /// <summary>
        /// The lightness the coat colour is pinned to. Measured off the shipped
        /// silhouette rather than chosen: the body of `cat_2_short_base`
        /// averages 0.459 of full light. A pixel at this value comes out the
        /// palette colour exactly.
        /// </summary>
        private const float Midtone = 0.459f;

        /// <summary>
        /// How far light and shadow move away from that colour.
        ///
        /// Was 1.6, chosen to carry the drawing's own range onto whatever colour
        /// the cat is: the deepest shadow (v = 0.02) landing at 0.30 of the coat
        /// colour and the brightest highlight (v = 1.0) at 1.89. The right
        /// instinct and the wrong number, for a reason the drawing itself
        /// supplies. These are renders of silver tabbies with photographic
        /// contrast, and dividing by the mid-tone multiplies whatever range it
        /// is handed by about three — so it was not carrying the drawing's range
        /// across, it was trebling it. What clipped at the top was the
        /// countershading; what crushed at the bottom was the stripes.
        ///
        /// Lowered to 1.35 on 2026-08-29 against build/coat-harness. It is the
        /// point where a solid cat stops showing bands and a tabby is still
        /// plainly a tabby: measured on the lying cat at 256, the tabby's
        /// high-frequency spread falls only from 0.210 to 0.186 while the share
        /// of her clipped to pure white falls from 19% to 5% and her crushed
        /// area from 7.4% to 3.5%. Below about 1.2 the tabby goes flat too,
        /// which trades one wrong cat for another.
        ///
        /// <see cref="Fit"/> is the other half of this and was added at the same
        /// time: the range that remains is now rolled off at both ends instead
        /// of being cut off.
        /// </summary>
        private const float Contrast = 1.35f;

        /// <summary>Coat colours, from the six base_color values. Multiplied
        /// into the greyscale base, so its light and shadow survive.</summary>
        private static readonly Dictionary<string, Color> Coats = new()
        {
            // Ginger darkened about a quarter on 2026-08-28, on the art
            // delivery's own measurement rather than by eye: the house palette
            // is warm and light, and a ginger cat dissolves into it — lightness
            // difference 3 of 100 at the old 0.87,0.55,0.29. 186,108,52 raises
            // it to 13 and the cat reads.
            ["ginger"] = new Color(186f / 255f, 108f / 255f, 52f / 255f),
            ["grey"] = new Color(0.60f, 0.62f, 0.65f),
            // Darkened 2026-08-29, and the reason is the shading change above:
            // the palette was tuned against the old multiply, where every value
            // came out roughly half of what was written. Pinned to the mid-tone
            // instead, 0.28 rendered a black cat at 0.25 of full light — a
            // grey-brown tabby. The black cats in the reference photographs
            // measure 0.08, 0.18 and 0.23 (cat_11, cat_04, cat_08), so 0.17
            // puts her in the middle of what a black cat actually looks like
            // and keeps the drawn light and shadow.
            ["black"] = new Color(0.18f, 0.17f, 0.17f),
            ["white"] = new Color(0.96f, 0.94f, 0.90f),
            // Cream is the other coat that warning names. Darkened by the same
            // proportion rather than to a measured value, because the delivery
            // measured ginger and not this one — worth re-checking against a
            // real room rather than trusting the arithmetic.
            ["cream"] = new Color(0.78f, 0.71f, 0.57f),
            ["brown"] = new Color(0.55f, 0.40f, 0.28f),
        };

        /// <summary>Eye colours. Declared and not yet used: applying them
        /// needs the eyes mask from 40-art/04 — see Tint for what happened
        /// without one.</summary>
        private static readonly Dictionary<string, Color> Eyes = new()
        {
            ["green"] = new Color(0.55f, 0.72f, 0.42f),
            ["amber"] = new Color(0.90f, 0.68f, 0.25f),
            ["blue"] = new Color(0.55f, 0.72f, 0.86f),
        };

        /// <summary>How neglected each state looks. Index 0 is unused; states
        /// are 1..3 to match RoomPlan and the art's own file names.</summary>
        private static readonly float[] Neglect = { 0f, 1.0f, 0.30f, 0f };

        /// <summary>Body width per state: state 1 is drawn thin, state 3 a
        /// little fuller. Negative narrows.</summary>
        private static readonly float[] Waist = { 0f, -0.15f, 0f, +0.05f };

        /// <summary>Logged once, not once per frame: a fallback nobody sees
        /// is a fallback nobody fixes, and one repeated every frame is noise
        /// nobody reads.</summary>
        private static readonly HashSet<string> _warned = new();

        /// <summary>
        /// A window into the middle of <see cref="Deband"/>: called with each
        /// intermediate greyscale field by name, when anything is listening.
        /// Null in the game, so it costs a null check per stage and nothing else.
        ///
        /// This exists because the striped "solid" cat could not be diagnosed
        /// from the outside. Deband reported `lifted 201104px, value scaled back
        /// x0.83` on the device while the stripes stayed plainly visible, and
        /// that one line is compatible with half a dozen different failures —
        /// the closing not reaching across a stripe, the relief gate putting the
        /// line back, the protection box covering the flank, the lift being
        /// undone by the rescale. Telling them apart means looking at the fields
        /// themselves, and there was no way to look at them.
        ///
        /// build/coat-harness writes each one out as a picture.
        /// </summary>
        public static Action<string, float[], int, int> Stages;

        /// <summary>
        /// How the last <see cref="Deband"/> divided the body up. Reported
        /// beside the pixel count because that count on its own is what hid this
        /// pass's failure for two days: `lifted 201104px` reads like work being
        /// done, and it was true while the output was identical to the input.
        /// What tells the difference is how much of her was never a candidate —
        /// a fifth of the cat, as it turned out, including her whole forehead.
        /// </summary>
        private static int _protectedPx, _unmovedPx;

        /// <summary>
        /// The silhouette for these traits: <c>cat_&lt;state&gt;_&lt;fur&gt;_base</c>.
        ///
        /// Only the short-haired column was drawn (40-art/03 delivered three of
        /// six), so a long-haired cat falls back to the short-haired art and
        /// says so. Visible, not silent — the player's cat being the wrong coat
        /// length is a real difference to them, and it should read as missing
        /// art rather than as a decision.
        /// </summary>
        public static Texture2D LoadBase(CatTraits traits, int state)
        {
            if (traits == null) throw new ArgumentNullException(nameof(traits));
            state = Mathf.Clamp(state, 1, 3);

            var wanted = $"Art/cat_{state}_{traits.FurLength}_base";
            var art = Resources.Load<Texture2D>(wanted);
            if (art != null) return art;

            var fallback = $"Art/cat_{state}_short_base";
            if (_warned.Add(wanted))
                Debug.LogWarning($"[CoatBuilder] no {wanted}, using {fallback} — " +
                                 "long-haired cats render short until 40-art/03 " +
                                 "delivers the other three silhouettes");
            return Resources.Load<Texture2D>(fallback);
        }

        /// <summary>
        /// Build the cat. <paramref name="state"/> is 1..3.
        /// Returns a new readable texture; the caller owns it.
        /// </summary>
        public static Texture2D Build(Texture2D baseCoat, CatTraits traits, int state)
        {
            // The synchronous path is the frame-sliced one, drained in a single
            // frame. Written this way on purpose (60-shell-build/19): the first
            // attempt kept two copies of the pass order — one here, one in the
            // coroutine — and two copies of a nine-stage pipeline is a cat that
            // renders differently depending on which screen asked for it. There
            // is one sequence, and the only difference is who spins the crank.
            Texture2D result = null;
            var steps = Steps(baseCoat, traits, state, t => result = t);
            while (steps.MoveNext()) { }
            return result;
        }

        /// <summary>
        /// The pass order, with a frame boundary between every expensive stage.
        ///
        /// Each `yield return null` is a place the work can be put down and
        /// picked up next frame. They sit where they do because that is where
        /// the pixel arrays are handed on whole — every stage takes the finished
        /// output of the one before and nothing is half-written across the seam.
        ///
        /// Not a worker thread, and that is not laziness. <see cref="ReadPixels"/>
        /// and the texture at the end are `Texture2D`, which Unity permits only
        /// on the main thread; the arithmetic in between could move, but then
        /// the two ends would have to marshal back and the seams would be in the
        /// same places anyway. Frames buy the same thing — a screen that keeps
        /// drawing — at a fraction of the risk.
        /// </summary>
        private static IEnumerator Steps(Texture2D baseCoat, CatTraits traits,
                                         int state, Action<Texture2D> onBuilt)
        {
            if (baseCoat == null) throw new ArgumentNullException(nameof(baseCoat));
            if (traits == null) throw new ArgumentNullException(nameof(traits));
            state = Mathf.Clamp(state, 1, 3);

            int w = baseCoat.width, h = baseCoat.height;

            // The longest single stage, and its name. Wall-clock time across a
            // frame-sliced build says nothing about the main thread — most of it
            // is waiting for the screen — so this is the number that answers
            // "did anything here hold a frame". Reported by the callers'
            // `[Perf] coat` lines.
            // Restart, work, Mark: the clock is started immediately before a
            // pass and read immediately after, so the frame the build spent
            // waiting in between never lands on anybody's stage. Left running
            // across the yield the first time, it charged CoatMasks with the
            // board's own first render — 247 ms for a pass that takes 12.
            var stage = new System.Diagnostics.Stopwatch();
            LongestStageMs = 0; LongestStage = "-";
            void Mark(string name)
            {
                var ms = stage.ElapsedMilliseconds;
                if (ms > LongestStageMs) { LongestStageMs = ms; LongestStage = name; }
                stage.Reset();
            }

            stage.Start();
            var px = ReadPixels(baseCoat);
            Mark("read");
            yield return null;

            // Masks come from the silhouette itself (CoatMasks), so they line
            // up exactly, and a hand-drawn file replaces any of them the moment
            // one exists — see MaskOf.
            // Driven stage by stage rather than called in one piece: CoatMasks
            // is a run of independent searches over the same body, and once
            // Outline had been cut into bands this became the longest thing left
            // — 190 ms for one 512 silhouette on emulator-5554, measured rather
            // than guessed. Its own enumerator hands a frame back between them.
            Dictionary<string, float[]> masks = null;
            var maskSteps = CoatMasks.BuildOverFrames(
                px, w, h, seed: baseCoat.name.GetHashCode(), m => masks = m);
            while (true)
            {
                stage.Start();
                bool more = maskSteps.MoveNext();
                Mark("masks");
                if (!more) break;
                yield return null;
            }
            yield return null;

            stage.Start();
            px = Reshape(px, w, h, Waist[state]);

            // The drawing's own silhouette, kept before any fur is grown on it.
            //
            // `Outline` needs it. The rim is a dilation of whatever it is given,
            // and given the tufted alpha it dilates around every strand as well
            // as around the cat — a strand sticking out 23 pixels grows an
            // 8-pixel ink blob at its root, and nine clumps of them turn the
            // lower edge into a scalloped crust. Reported off an iOS playthrough
            // on 2026-08-29 as "an aliased outline with stray hairs escaping the
            // mask", and it is worst in state 1, where Neglect is 1.0 and the
            // tufts are at full strength.
            //
            // The rim belongs to the drawing, not to the fur standing off it.
            var drawn = new bool[px.Length];
            for (int i = 0; i < px.Length; i++) drawn[i] = px[i].a > 200;
            Mark("reshape");
            yield return null;

            stage.Start();
            px = Weather(px, w, h, Neglect[state], seed: 5);
            Mark("weather");
            yield return null;

            // The stripes come off for every cat that is not a tabby — but only
            // while the drawing HAS stripes, and since 2026-08-29 it does not.
            //
            // The whole pass exists to subtract a tabby that the artwork drew
            // in. Three silhouettes with a plain, even coat arrived that
            // evening, and against them subtraction has nothing to take and
            // everything to lose: run on the plain art it wiped the standing
            // cat's eyes to two faint rings and left a black wedge in her ear,
            // while the eye check passed at 86% — on an even coat a faint ring
            // still scores contrast against a uniform cheek. That measurement
            // could not see the damage, and the picture could.
            //
            // Sniffing the drawing at runtime was tried and rejected: the share
            // of the body a closing changes is 52–62% on the plain art against
            // 72–81% on the striped, and nine points is not a margin to hang a
            // switch on. The closing moves the body's own modelling too, so the
            // number measures shading as much as banding.
            //
            // So it is a stated fact about the project, not a guess: the bases
            // are plain. Whoever brings striped art back turns this on and
            // reads the paragraph above first.
            //
            // Kept rather than deleted — with all of Deband, FormKeep and
            // LineKeep behind it — because a hand-drawn `pattern_tabby` mask is
            // still an open task (40-art/04-cat-layers), and the day a drawing
            // with a coat pattern returns, this is the code that takes it off.
            const bool basesHaveDrawnStripes = false;
            if (basesHaveDrawnStripes && traits.Pattern != "tabby")
            {
                stage.Start();
                px = Deband(px, w, h, masks, baseCoat.name);
                Mark("deband");
                yield return null;
            }

            stage.Start();
            px = Tint(px, traits, masks, baseCoat.name);
            Mark("tint");
            yield return null;

            stage.Start();
            px = Marks(px, w, h, traits, masks, baseCoat.name);
            Mark("marks");
            yield return null;

            // The dear one, and the only pass cut open rather than merely
            // fenced off. Outline scans a square window per pixel — 33×33 at
            // 1024 — so a frame boundary on each side of it still leaves one
            // step far longer than a frame, which is the thing this whole
            // exercise is against. Bands of rows instead: each band reads the
            // untouched source and writes only its own rows, so the picture is
            // the same whatever the band size (proved against the coat harness,
            // 26 of 26 coats byte-identical).
            //
            // 32 rows: at 256 that is eight bands and at 512 sixteen, which
            // keeps the band's cost roughly level as the source grows instead of
            // letting it climb with the picture.
            int rim = Mathf.RoundToInt(w * 0.016f);
            if (rim > 0)
            {
                var outlined = new Color32[px.Length];
                Array.Copy(px, outlined, px.Length);
                const int Band = 32;
                for (int y0 = 0; y0 < h; y0 += Band)
                {
                    stage.Start();
                    OutlineRows(px, outlined, w, h, rim, drawn, y0, Mathf.Min(y0 + Band, h));
                    Mark($"outline@{y0}");
                    yield return null;
                }
                px = outlined;
            }

            stage.Start();
            var result = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: false)
            {
                name = $"{baseCoat.name}_{traits.BaseColor}_{state}",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            result.SetPixels32(px);
            result.Apply(updateMipmaps: false);
            Mark("upload");
            onBuilt?.Invoke(result);
        }

        /// <summary>How long the dearest single stage of the last build took,
        /// and which one it was. See <see cref="Steps"/>.</summary>
        public static long LongestStageMs { get; private set; }

        /// <inheritdoc cref="LongestStageMs"/>
        public static string LongestStage { get; private set; } = "-";

        /// <summary>
        /// What stopped the last <see cref="TryBuild"/>, or null if none has
        /// failed. The coat harness shows it, so a checker reads the reason on
        /// the screen instead of guessing at an empty square.
        /// </summary>
        public static string LastFailure { get; private set; }

        /// <summary>
        /// True when the last pixel read went through the GPU rather than
        /// straight from memory. Reported in `boot-state.txt`, because the blit
        /// path is the one that blanks the iOS simulator and the first attempt
        /// at avoiding it failed silently — the direct read threw on a
        /// compressed texture and the fallback quietly did the harmful thing
        /// again. A path this consequential should not be invisible.
        /// </summary>
        public static bool LastReadWasBlit { get; private set; }

        /// <summary>Why the direct read was not used, when it was not.</summary>
        public static string LastReadNote { get; private set; }

        /// <summary>
        /// <see cref="Build"/> that cannot take a screen down with it. Returns
        /// null when the coat could not be built; the caller then paints the
        /// untinted silhouette, which it must **not** destroy — that texture is
        /// the Resources asset itself, not a copy.
        ///
        /// This exists because of a real failure, not as a precaution. On 28.08
        /// the board and meet-your-cat both came up as a black screen on the
        /// iOS simulator, while the house map — the one screen that builds no
        /// coat — rendered correctly on the same run. An exception inside Build
        /// aborted the calling Build(parent, …) before its root was ever added
        /// to the panel, so one image failing erased twelve tiles, a progress
        /// bar and a name field. A cat that will not tint should cost a tint.
        ///
        /// The reason is written to `coat-failure.txt` beside the save as well
        /// as logged, so a run with no console attached still leaves the
        /// reason behind. The log itself does reach both platforms —
        /// `simctl launch --console` on iOS, `adb logcat -s Unity` on Android;
        /// the claim that it did not is what turned this bug into a day.
        /// </summary>
        /// <summary>
        /// Skip the coat entirely: drop a `nocat.txt` beside the save.
        ///
        /// A diagnostic switch, in the same style as `housemap.txt` and
        /// `coat.txt`, and it earned its place. On 28.08 the board and
        /// meet-your-cat drew nothing on the iOS simulator while the house map
        /// drew correctly, with no exception, a fully laid-out tree, a cream
        /// background on the right element and a 52×52 tile carrying its
        /// texture — every measurement said the screen was fine and the screen
        /// was blank. The one thing the two blank screens share and the working
        /// one does not is this class, which reads its pixels back through a
        /// temporary RenderTexture. This flag is what tells the difference
        /// between "the coat is the cause" and "the coat is a coincidence"
        /// without rebuilding twice to find out.
        /// </summary>
        public static bool Skipped
        {
            get
            {
                try
                {
                    return System.IO.File.Exists(System.IO.Path.Combine(
                        Application.persistentDataPath, "nocat.txt"));
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// A small copy of a silhouette, cached by name and size.
        ///
        /// Measured on the iOS simulator, 28.08: building one coat from the
        /// shipped 1024×1024 silhouette took **21.8 seconds**, and that was the
        /// whole of the board's opening delay — level loading was 91ms and
        /// everything else 4ms. The owner asked why opening a room was slow and
        /// guessed the pile was being generated; it is not, it comes from a
        /// file. This was the answer.
        ///
        /// The cost is superlinear, not linear. `Outline` dilates by 1.6% of the
        /// width — 16 pixels at 1024 — and does it by scanning a square window
        /// per pixel: a million pixels against a 33×33 window is over a billion
        /// reads. Halving the source quarters the pixels *and* halves the
        /// radius, so the work falls by roughly sixteen times each time.
        ///
        /// The cat is drawn at about 52 points. 256 is already more than that
        /// can show.
        /// </summary>
        public static Texture2D Downscale(Texture2D src, int size)
        {
            if (src == null || src.width <= size) return src;
            var key = $"{src.name}@{size}";
            if (_downscaled.TryGetValue(key, out var cached) && cached != null) return cached;
            if (!src.isReadable) return src;   // Build's own path will complain

            Color32[] px;
            try { px = src.GetPixels32(); }
            catch (Exception) { return src; }

            int w = src.width, h = src.height;
            int oh = Mathf.Max(1, h * size / w);
            var outPx = new Color32[size * oh];
            int bx = Mathf.Max(1, w / size), by = Mathf.Max(1, h / oh);
            for (int y = 0; y < oh; y++)
                for (int x = 0; x < size; x++)
                {
                    int r = 0, g = 0, b = 0, a = 0, n = 0;
                    for (int sy = y * by; sy < (y + 1) * by && sy < h; sy++)
                        for (int sx = x * bx; sx < (x + 1) * bx && sx < w; sx++)
                        {
                            var c = px[sy * w + sx];
                            r += c.r; g += c.g; b += c.b; a += c.a; n++;
                        }
                    if (n == 0) n = 1;
                    outPx[y * size + x] =
                        new Color32((byte)(r / n), (byte)(g / n), (byte)(b / n), (byte)(a / n));
                }

            var tex = new Texture2D(size, oh, TextureFormat.RGBA32, mipChain: false)
            {
                name = src.name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            tex.SetPixels32(outPx);
            tex.Apply(updateMipmaps: false);
            _downscaled[key] = tex;
            return tex;
        }

        private static readonly Dictionary<string, Texture2D> _downscaled = new();

        /// <summary>
        /// A built coat, kept so re-entering a room does not rebuild it. Keyed
        /// by everything that changes the result.
        ///
        /// **This dictionary owns what is in it.** No caller may destroy a
        /// texture handed back by <see cref="TryBuildFor"/>, and the rule is not
        /// bookkeeping: two of the three things stored here are not ours to
        /// destroy in the first place. A default cat's coat is the baked
        /// `Art/coat_default_N` asset straight out of Resources — destroying it
        /// takes the art out of the game for the rest of the run — and every
        /// entry is shared by whoever asks for the same cat at the same size,
        /// so one screen tidying up on its way out empties another's portrait.
        /// The board did exactly that until 2026-09-02 (`DebugGameView.RenderCat`).
        ///
        /// The cache outlives every screen on purpose. It is static, it survives
        /// the board being destroyed on the way back to the house map, and that
        /// is what makes returning to a room free.
        /// </summary>
        private static readonly Dictionary<string, Texture2D> _builtCache = new();

        /// <summary>
        /// The cat nobody chose. Compared by value, not by reference:
        /// `CatTraits.Default` is a fresh instance every time it is read.
        /// </summary>
        private static bool IsDefault(CatTraits t)
        {
            var d = CatTraits.Default;
            return t.BaseColor == d.BaseColor && t.Pattern == d.Pattern
                && t.FurLength == d.FurLength && t.EyeColor == d.EyeColor
                && string.Join(",", t.WhiteMarkings) == string.Join(",", d.WhiteMarkings);
        }

        /// <summary>Where a built coat is kept between launches.</summary>
        /// <summary>
        /// Bumped whenever this file changes what a coat looks like.
        ///
        /// The cache on disk had no version until 2026-08-29, and that is worse
        /// than it sounds: a coat is written once, from a photograph, and read
        /// on every launch thereafter. Every improvement to the builder — the
        /// stripes coming off a solid cat, the palette, the markings finding the
        /// chest instead of the flank — reached only players who had never built
        /// a cat. Everyone else kept the cat the old code drew, for good. It was
        /// caught the way anything like this is caught: the coat harness went on
        /// showing striped "solid" cats after the stripes had demonstrably been
        /// removed.
        ///
        /// One number, and the old files are simply never named again.
        /// </summary>
        /// <summary>
        /// 4 since 2026-08-29: the eye guard changes what every non-tabby cat
        /// looks like, and anyone who ran the intervening build has a cat with
        /// her eyes rubbed out sitting in the cache under version 3.
        /// </summary>
        private const int CoatVersion = 4;

        private static string CachePath(string key)
        {
            var safe = key.Replace('/', '_').Replace('@', '_').Replace(',', '-');
            return System.IO.Path.Combine(Application.persistentDataPath,
                                          $"coat_v{CoatVersion}_{safe}.png");
        }

        private static Texture2D LoadCached(string key, int size)
        {
            try
            {
                var path = CachePath(key);
                if (!System.IO.File.Exists(path)) return null;
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
                if (!tex.LoadImage(System.IO.File.ReadAllBytes(path))) return null;
                tex.filterMode = FilterMode.Bilinear;
                tex.wrapMode = TextureWrapMode.Clamp;
                return tex;
            }
            catch (Exception)
            {
                return null;   // a broken cache is not a broken game
            }
        }

        private static void SaveCached(string key, Texture2D tex)
        {
            try
            {
                System.IO.File.WriteAllBytes(CachePath(key), tex.EncodeToPNG());
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// <see cref="TryBuild"/> at a size worth drawing, cached. This is what
        /// callers should use; the full-resolution path exists for anything
        /// that genuinely needs a big texture, and nothing does yet.
        /// </summary>
        public static Texture2D TryBuildFor(CatTraits traits, int state, int size)
        {
            if (traits == null) return null;
            var key = KeyFor(traits, state, size);
            var ready = Remembered(key, traits, state, size);
            if (ready != null) return ready;

            var art = LoadBase(traits, state);
            if (art == null) return null;
            var built = TryBuild(Downscale(art, size), traits, state);
            Keep(key, built);
            return built;
        }

        /// <summary>
        /// <see cref="TryBuildFor"/> spread over frames — what a caller that can
        /// run a coroutine should use. <paramref name="onDone"/> is called with
        /// the coat (or null), possibly on the very first pass when the answer
        /// was already remembered.
        ///
        /// The three cheap answers — memory, the baked default, the file on
        /// disk — are still given without yielding, because they are the
        /// ordinary case: a player re-opening a room pays nothing and should not
        /// be made to wait a frame for the privilege.
        /// </summary>
        public static IEnumerator TryBuildForOverFrames(CatTraits traits, int state,
                                                        int size, Action<Texture2D> onDone)
        {
            if (traits == null) { onDone?.Invoke(null); yield break; }

            var key = KeyFor(traits, state, size);
            var ready = Remembered(key, traits, state, size);
            if (ready != null) { onDone?.Invoke(ready); yield break; }

            var art = LoadBase(traits, state);
            if (art == null) { onDone?.Invoke(null); yield break; }

            // Downscale gets its own frame: it reads back a whole 1024×1024 and
            // box-filters it, which is one of the larger single stages here and
            // is not part of Steps.
            var small = Downscale(art, size);
            yield return null;

            Texture2D built = null;
            yield return TryBuildOverFrames(small, traits, state, t => built = t);
            Keep(key, built);
            onDone?.Invoke(built);
        }

        /// <summary>
        /// Everything that changes what the coat looks like, in one string.
        ///
        /// Spots are part of it. Without them two cats alike in every class
        /// trait and different in the one thing that identifies them — a sock on
        /// one paw — share a cached coat, and the second player gets the first
        /// player's cat.
        /// </summary>
        private static string KeyFor(CatTraits traits, int state, int size)
        {
            var marks = string.Join(",", traits.Spots.Select(m => $"{m.Place}:{m.Shade}"));
            return $"{traits.BaseColor}/{traits.Pattern}/{traits.FurLength}/" +
                   $"{traits.EyeColor}/{string.Join(",", traits.WhiteMarkings)}/" +
                   $"{marks}/{state}@{size}";
        }

        /// <summary>
        /// The coat we already have, or null. Memory, then the baked default,
        /// then the file beside the save — cheapest first.
        /// </summary>
        private static Texture2D Remembered(string key, CatTraits traits, int state, int size)
        {
            if (_builtCache.TryGetValue(key, out var hit) && hit != null) return hit;

            // Shipped first. The default cat — what a player sees before she has
            // given the game a photograph — is baked into Resources at build
            // time by Assets/Editor/BakeDefaultCoats.cs. Nothing is computed for
            // her at all, on any launch, ever.
            if (IsDefault(traits) && size <= 512)
            {
                // Two are baked: 256 for the board's portrait and 512 for the
                // cat card, where she is nearly the width of the screen. Asking
                // for anything above 256 gets the larger one.
                var baked = Resources.Load<Texture2D>(
                    size > 256 ? $"Art/coat_card_{state}" : $"Art/coat_default_{state}");
                if (baked != null)
                {
                    _builtCache[key] = baked;
                    return baked;
                }
            }

            // Disk, before doing the work again. A coat survives an app
            // restart: the traits that produced it come from a photograph the
            // player took once, and rebuilding it on every launch is paying
            // twice for the same picture.
            var onDisk = LoadCached(key, size);
            if (onDisk != null) { _builtCache[key] = onDisk; return onDisk; }
            return null;
        }

        private static void Keep(string key, Texture2D built)
        {
            if (built == null) return;
            _builtCache[key] = built;
            SaveCached(key, built);
        }

        public static Texture2D TryBuild(Texture2D baseCoat, CatTraits traits, int state)
        {
            if (Skipped)
            {
                LastFailure = "skipped by nocat.txt";
                return null;
            }

            try
            {
                return Build(baseCoat, traits, state);
            }
            catch (Exception e)
            {
                Failed(e);
                return null;
            }
        }

        /// <summary>
        /// The record a failed build leaves behind. Lifted out of
        /// <see cref="TryBuild"/>'s catch so the frame-sliced path can leave
        /// exactly the same one: C# forbids `yield` inside a try that has a
        /// catch, so that path catches around a single `MoveNext` instead, and
        /// without this it would have grown its own second version of the
        /// warning, the file and the once-only guard.
        /// </summary>
        private static void Failed(Exception e)
        {
            LastFailure = $"{e.GetType().Name}: {e.Message}";
            if (!_warned.Add("coat-build-failure")) return;

            Debug.LogWarning($"[CoatBuilder] coat not built ({LastFailure}); " +
                             "painting the untinted silhouette instead");
            try
            {
                System.IO.File.AppendAllText(
                    System.IO.Path.Combine(Application.persistentDataPath,
                                           "coat-failure.txt"),
                    $"{LastFailure}\n{e.StackTrace}\n");
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// <see cref="TryBuild"/> spread over frames. Drive it with
        /// <c>StartCoroutine</c>; <paramref name="onDone"/> is handed the coat,
        /// or null if it could not be built — the same two answers the
        /// synchronous call gives, arriving a few frames later.
        /// </summary>
        public static IEnumerator TryBuildOverFrames(Texture2D baseCoat, CatTraits traits,
                                                     int state, Action<Texture2D> onDone)
        {
            if (Skipped)
            {
                LastFailure = "skipped by nocat.txt";
                onDone?.Invoke(null);
                yield break;
            }

            Texture2D built = null;
            var steps = Steps(baseCoat, traits, state, t => built = t);
            while (true)
            {
                bool more = false, broke = false;
                // One stage per iteration, each inside its own catch. The
                // guarantee TryBuild makes — a coat that will not build costs a
                // coat and nothing else — has to survive being cut up, and a
                // half-finished pipeline must not leave the caller waiting.
                try { more = steps.MoveNext(); }
                catch (Exception e) { Failed(e); broke = true; }

                if (broke) { onDone?.Invoke(null); yield break; }
                if (!more) break;
                yield return null;
            }
            onDone?.Invoke(built);
        }

        /// <summary>
        /// Pixels of a texture, straight from memory when it was imported
        /// Read/Write enabled, and through the GPU when it was not.
        ///
        /// The three cat silhouettes are now imported readable, and the reason
        /// is not tidiness. The blit path below **stops the iOS simulator from
        /// drawing anything at all** for the rest of the run: on 28.08 the
        /// board and meet-your-cat rendered as a blank screen there while the
        /// house map — the one screen that builds no coat — rendered
        /// correctly. Nothing threw; the tree was laid out, the background
        /// colour resolved cream and a 52×52 tile carried its texture. Every
        /// measurement said the screen was fine and the screen was blank.
        /// Skipping this one function through `nocat.txt` brought the whole
        /// board back, which is the controlled experiment that settled it:
        /// binding a temporary RenderTexture during OnEnable leaves the
        /// simulator's Metal target somewhere the camera never recovers from.
        ///
        /// Reading a readable texture costs 4 MB of resident memory per cat
        /// silhouette — 12 MB for the three — which the earlier version of this
        /// comment argued was not worth "one pass at load". It is worth it. A
        /// screen that does not draw costs everything.
        ///
        /// The blit stays as the fallback because it works on any texture
        /// whatever its import settings, including art delivered later that
        /// nobody remembered to mark, and because it is fine everywhere except
        /// this one simulator. Anything reaching it should expect a blank
        /// screen there and nowhere else.
        /// </summary>
        private static Color32[] ReadPixels(Texture2D source)
        {
            if (source.isReadable)
            {
                try
                {
                    var direct = source.GetPixels32();
                    LastReadWasBlit = false;
                    return direct;
                }
                catch (Exception e)
                {
                    // Readable is not the whole story: GetPixels32 throws on a
                    // compressed format, which is what every texture in this
                    // project is by default and what sent this straight back to
                    // the blit on the first attempt at the fix — same blank
                    // screen, and silently, because the fallback swallowed it.
                    // The three cat silhouettes are imported uncompressed for
                    // exactly this reason (textureCompression: 0 in their meta).
                    LastReadNote = $"{source.name}: {e.GetType().Name}";
                }
            }
            else
            {
                LastReadNote = $"{source.name}: not readable";
            }

            LastReadWasBlit = true;
            // Loud, once per texture. This path blanks the iOS simulator for the
            // rest of the run, and a verifier pointed out it could still be
            // entered in silence: MaskOf reads any `Art/{base}_{mask}` file, and
            // a mask delivered by 40-art/04 will import with isReadable: 0 by
            // default and walk straight back into it. LogError so DeviceLog
            // records it too — that filter only keeps Error and above.
            if (_warned.Add($"blit:{source.name}"))
                Debug.LogError($"[CoatBuilder] reading {source.name} through the GPU " +
                               $"({LastReadNote}). This is the path that blanks the " +
                               "iOS simulator — import it readable and uncompressed.");
            var rt = RenderTexture.GetTemporary(
                source.width, source.height, 0,
                RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var previous = RenderTexture.active;
            try
            {
                Graphics.Blit(source, rt);
                RenderTexture.active = rt;
                var readable = new Texture2D(source.width, source.height,
                                             TextureFormat.RGBA32, mipChain: false);
                readable.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
                readable.Apply(updateMipmaps: false);
                var px = readable.GetPixels32();
                UnityEngine.Object.Destroy(readable);
                return px;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        // ---------------------------------------------------------------
        // 1. Body width
        // ---------------------------------------------------------------

        /// <summary>
        /// Squeeze or widen the body horizontally, leaving the head alone.
        /// The scale ramps in below <see cref="HeadKeep"/> of the height, so
        /// the face — which is what makes it the same cat — is untouched.
        ///
        /// This is the coarse half of "thin": real gauntness would change the
        /// pose, the ribs and the belly line, and only a redraw gives that.
        /// Width alone still reads, and it costs no files.
        /// </summary>
        private const float HeadKeep = 0.42f;

        private static Color32[] Reshape(Color32[] src, int w, int h, float amount)
        {
            if (Mathf.Approximately(amount, 0f)) return src;

            // Centre of the body, measured from the lower half only — the head
            // is often off to one side and would drag the axis with it.
            double sum = 0; int n = 0;
            for (int y = (int)(h * HeadKeep); y < h; y++)
                for (int x = 0; x < w; x++)
                    if (src[y * w + x].a > 128) { sum += x; n++; }
            float cx = n > 0 ? (float)(sum / n) : w * 0.5f;

            var dst = new Color32[src.Length];
            for (int y = 0; y < h; y++)
            {
                // Texture rows run bottom-up; the head is at the top.
                float fromTop = 1f - (float)y / (h - 1);
                float t = (fromTop - HeadKeep) / (1f - HeadKeep);
                float k = t <= 0f ? 1f : 1f + amount * Mathf.Min(1f, t * 1.6f);

                for (int x = 0; x < w; x++)
                {
                    float sx = cx + (x - cx) / k;
                    int x0 = Mathf.FloorToInt(sx);
                    if (x0 < 0 || x0 >= w - 1) continue;
                    float f = sx - x0;
                    dst[y * w + x] = Color32.Lerp(src[y * w + x0], src[y * w + x0 + 1], f);
                }
            }
            return dst;
        }

        // ---------------------------------------------------------------
        // 2. Weathering: dull, dirty, matted, tufted
        // ---------------------------------------------------------------

        /// <summary>
        /// Drop the fur texture and keep only the value shifts: a `flatcoat.txt`
        /// beside the save.
        ///
        /// A checking switch, and it is asking a real question. The artist's
        /// finding, tested three times in a day, is that **any fur texture
        /// throws the cat out of this game's flat style** and breaks her
        /// kinship with the thirty-two props. Weather does three things and
        /// only one of them is texture: it dulls the coat towards its own mean,
        /// it lays a grime gradient up from the paws, and it lays coarse value
        /// noise meant to read as matted fur. The first two are value, not
        /// texture, and the artist's objection does not reach them.
        ///
        /// With this flag the noise term drops and the other two stay, so the
        /// two can be compared on screen instead of argued about.
        /// </summary>
        public static bool FlatCoat
        {
            get
            {
                try
                {
                    return System.IO.File.Exists(System.IO.Path.Combine(
                        Application.persistentDataPath, "flatcoat.txt"));
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        private static Color32[] Weather(Color32[] px, int w, int h, float s, int seed)
        {
            if (s <= 0f) return px;
            var rng = new System.Random(seed);
            var flat = FlatCoat;

            // Mean lightness of the coat, to pull contrast towards.
            float mean = 0f; int n = 0;
            foreach (var p in px)
                if (p.a > 128) { mean += (p.r + p.g + p.b) / 3f; n++; }
            mean = n > 0 ? mean / n : 128f;

            // Value noise, stretched horizontally so it reads as matted fur
            // rather than as film grain.
            // Coarse on purpose: at w/4 the noise came out as sand rather
            // than as matted fur, and at cell size it read as image noise.
            int nw = Mathf.Max(2, w / 12), nh = Mathf.Max(2, h / 12);
            var noise = new float[nw * nh];
            for (int i = 0; i < noise.Length; i++) noise[i] = (float)rng.NextDouble();

            for (int y = 0; y < h; y++)
            {
                float fromBottom = (float)y / (h - 1);
                float dirt = Mathf.Pow(Mathf.Clamp01((0.42f - fromBottom) / 0.42f), 1.5f);

                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    if (px[i].a < 8) continue;

                    float nv = Sample(noise, nw, nh, x * nw / (float)w, y * nh / (float)h);
                    // Was `(nv - 0.5f) * 2f * 13f * s` — coarse value noise meant
                    // to read as matted fur. Measured out on 28.08: rendering
                    // the whole coat grid with and without it changes the
                    // picture by **0.9–1.3 of 255 on average**, which is
                    // nothing. It cost a full pass over every pixel and bought
                    // an effect no one can see.
                    //
                    // Left at its original strength rather than tuned to a
                    // smaller number, because a constant chosen to make an
                    // invisible thing more invisible is exactly the sort of
                    // unmeasured tweak this file has been burned by. The switch
                    // stays so the artist can look at both; if a future
                    // silhouette wants texture, this is where it goes, and the
                    // measurement above is the bar it must clear.
                    float tuft = flat ? 0f : (nv - 0.5f) * 2f * 13f * s;
                    float grime = dirt * 24f * s;

                    px[i] = Shift(px[i], mean, 0.28f * s, tuft - grime);
                }
            }

            return Tufts(px, w, h, s, rng);
        }

        private static float Sample(float[] a, int w, int h, float x, float y)
        {
            int x0 = Mathf.Clamp(Mathf.FloorToInt(x), 0, w - 1);
            int y0 = Mathf.Clamp(Mathf.FloorToInt(y), 0, h - 1);
            int x1 = Mathf.Min(x0 + 1, w - 1), y1 = Mathf.Min(y0 + 1, h - 1);
            float fx = x - x0, fy = y - y0;
            float a0 = Mathf.Lerp(a[y0 * w + x0], a[y0 * w + x1], fx);
            float a1 = Mathf.Lerp(a[y1 * w + x0], a[y1 * w + x1], fx);
            return Mathf.Lerp(a0, a1, fy);
        }

        /// <summary>Pull towards the mean (dulling) and add an offset.</summary>
        private static Color32 Shift(Color32 c, float mean, float pull, float offset)
        {
            return new Color32(
                Chan(c.r, mean, pull, offset),
                Chan(c.g, mean, pull, offset),
                Chan(c.b, mean, pull, offset),
                c.a);
        }

        private static byte Chan(byte v, float mean, float pull, float offset)
        {
            float f = mean + (v - mean) * (1f - pull) + offset;
            return (byte)Mathf.Clamp(f, 0f, 255f);
        }

        // ---------------------------------------------------------------
        // 3. Tufts along the edge
        // ---------------------------------------------------------------

        /// <summary>
        /// Strands growing outward from the silhouette, in clumps.
        ///
        /// Clumps, not an even fringe: an evenly tufted outline reads as frost
        /// or as a rendering artefact, which is what the first two attempts
        /// looked like. Matted fur sticks out in a few places.
        /// </summary>
        private static Color32[] Tufts(Color32[] px, int w, int h, float s, System.Random rng)
        {
            int clumps = Mathf.RoundToInt(9 * s);
            if (clumps <= 0) return px;

            // Edge pixels: opaque, with a transparent neighbour within 3px.
            var edge = new List<int>();
            for (int y = 3; y < h - 3; y++)
                for (int x = 3; x < w - 3; x++)
                {
                    int i = y * w + x;
                    if (px[i].a < 128) continue;
                    if (px[i - 3].a < 128 || px[i + 3].a < 128 ||
                        px[i - 3 * w].a < 128 || px[i + 3 * w].a < 128)
                        edge.Add(i);
                }
            if (edge.Count == 0) return px;

            // Strand colour: the coat's own, a shade darker.
            long r = 0, g = 0, b = 0; int n = 0;
            foreach (var p in px)
                if (p.a > 200) { r += p.r; g += p.g; b += p.b; n++; }
            if (n == 0) return px;
            var hair = new Color32(
                (byte)(r / n * 0.84f), (byte)(g / n * 0.84f), (byte)(b / n * 0.84f), 255);

            float radius = w * 0.07f;
            int length = Mathf.RoundToInt(w * 0.045f);

            for (int c = 0; c < clumps; c++)
            {
                int centre = edge[rng.Next(edge.Count)];
                int cy = centre / w, cx = centre % w;

                for (int k = 0; k < 30; k++)
                {
                    int pick = edge[rng.Next(edge.Count)];
                    int py = pick / w, pxx = pick % w;
                    if (Mathf.Abs(py - cy) > radius || Mathf.Abs(pxx - cx) > radius) continue;

                    Normal(px, w, h, pxx, py, out float nx, out float ny);
                    float ang = ((float)rng.NextDouble() - 0.5f) * 0.76f;
                    float cs = Mathf.Cos(ang), sn = Mathf.Sin(ang);
                    float dx = nx * cs - ny * sn, dy = nx * sn + ny * cs;

                    int steps = Mathf.Max(2, Mathf.RoundToInt(length * (0.4f + (float)rng.NextDouble() * 0.9f) * s));
                    for (int t = 0; t < steps; t++)
                    {
                        float f = t / (float)(steps - 1);
                        int sxi = Mathf.RoundToInt(pxx + dx * t), syi = Mathf.RoundToInt(py + dy * t);
                        if (sxi < 0 || sxi >= w || syi < 0 || syi >= h) break;
                        int di = syi * w + sxi;
                        byte a = (byte)(240 * Mathf.Pow(1f - f, 0.6f));
                        if (px[di].a >= a) continue;
                        px[di] = new Color32(hair.r, hair.g, hair.b, a);
                    }
                }
            }
            return px;
        }

        /// <summary>Outward normal at an edge pixel, from the alpha gradient.</summary>
        private static void Normal(Color32[] px, int w, int h, int x, int y,
                                   out float nx, out float ny)
        {
            const int R = 4;
            float gx = 0f, gy = 0f;
            for (int dy = -R; dy <= R; dy++)
                for (int dx = -R; dx <= R; dx++)
                {
                    int sxi = Mathf.Clamp(x + dx, 0, w - 1), syi = Mathf.Clamp(y + dy, 0, h - 1);
                    float a = px[syi * w + sxi].a / 255f;
                    gx += dx * a; gy += dy * a;
                }
            float len = Mathf.Sqrt(gx * gx + gy * gy);
            if (len < 1e-4f) { nx = 1f; ny = 0f; return; }
            // Gradient points into the shape, so the outward normal is negated.
            nx = -gx / len; ny = -gy / len;
        }

        // ---------------------------------------------------------------
        // 4. Colour
        // ---------------------------------------------------------------

        /// <summary>
        /// Multiply the coat colour into the greyscale base, so its modelling
        /// survives — replacing the colour outright gives a flat cat.
        ///
        /// Pattern is not applied here. The masks of 40-art/04 do not exist and
        /// may never; procedural stripes over a body this small read as noise,
        /// and a wrong pattern is worse than none when the point is "that looks
        /// like my cat". Solid coats only until the masks arrive, and the
        /// caller is told so once, not silently.
        ///
        /// eye_color is not applied either, for a reason worth recording. It
        /// was, by tinting the darkest pixels — and the first grid came out
        /// with amber smears under every sleeping cat, because the deep shadow
        /// beneath a curled body is darker than the eyes. Lightness alone
        /// cannot find eyes. This needs 40-art/04's eyes mask; until then every
        /// player's cat has the dark eyes the silhouette was drawn with.
        /// </summary>
        private static Color32[] Tint(Color32[] px, CatTraits traits,
                                      Dictionary<string, float[]> masks, string baseName)
        {
            var coat = Coats.TryGetValue(traits.BaseColor, out var c) ? c : Color.white;
            var eye = Eyes.TryGetValue(traits.EyeColor, out var e) ? e : Color.white;

            // The pattern is a darker shade of the same coat, not a second
            // colour: a ginger tabby is ginger with darker ginger stripes.
            var pattern = traits.Pattern == "solid"
                ? null
                : MaskOf(masks, baseName, $"pattern_{traits.Pattern}");

            var markings = new List<float[]>();
            foreach (var marking in traits.WhiteMarkings)
            {
                var m = MaskOf(masks, baseName, $"mark_{marking}");
                if (m != null) markings.Add(m);
            }
            // Eye colour stays off, and the plain silhouettes are the reason it
            // now has to be said out loud rather than left to happen.
            //
            // On the striped art `CoatMasks.Eyes` never once fired — its size
            // floor rejected five of the six blobs — so this branch was dead and
            // the decision of 2026-08-29 ("do not paint eyes until a real pupil
            // mask exists") cost nothing to keep. The plain art changed that:
            // with no stripes competing, the mask finds the eyes on state 1, and
            // the result is worse than doing nothing. These cats are drawn with
            // a light iris and a dark pupil, and multiplying one colour through
            // the whole blob flattens both into a green almond — beside the
            // untouched states 2 and 3, which keep a dark pupil and a highlight,
            // it is plainly the poorer eye.
            //
            // Worse than poorer: it fires on ONE state of three. The same cat
            // would have green eyes sitting and dark eyes standing, and a cat
            // whose eyes change when she stands up is not her cat.
            //
            // Turn this on when a pupil mask exists (40-art/04-cat-layers) and
            // when it fires on all three states — not before, and not because
            // the mask happened to start working.
            const bool paintEyeColour = false;
            var eyeMask = paintEyeColour
                ? MaskOf(masks, baseName, CoatMasks.Eyes)
                : null;

            for (int i = 0; i < px.Length; i++)
            {
                if (px[i].a < 8) continue;

                // The drop shadow under the cat is drawn as semi-transparent
                // grey. Tinting it turns it into a coloured puddle — a yellow
                // smear under a cream cat, which is what the first grid
                // showed. Anything not solidly part of the body keeps its own
                // neutral colour.
                if (px[i].a < 200) continue;

                float v = (px[i].r + px[i].g + px[i].b) / 765f;   // 0..1 lightness
                var target = coat;

                if (pattern != null && pattern[i] > 0.01f)
                    target = Color.Lerp(coat, coat * 0.55f, Mathf.Clamp01(pattern[i]));

                float white = 0f;
                foreach (var m in markings)
                    white = Mathf.Max(white, Mathf.Clamp01(m[i]));
                if (white > 0.01f)
                    target = Color.Lerp(target, White, white * 0.92f);

                // Eyes keep their modelling. Flat colour stood here until
                // 2026-08-28 — every pixel of the blob set to the same green,
                // pupil and highlight alike — and it was never seen, because the
                // mask that feeds it never fired at the size the game ran at.
                // The first time it did fire, at 512 on the cat card, she had two
                // flat green discs for eyes.
                //
                // These cats are drawn with one dark eye mass and a specular dot,
                // not with a separable iris, so the colour is multiplied through
                // the drawing's own light instead of replacing it: the dark of
                // the eye stays dark, the highlight stays a highlight, and what
                // changes is the hue. The lift is there because the blob was
                // chosen for being the darkest sixth of a percent of the cat —
                // multiplied raw, a green eye would come out black.
                if (eyeMask != null && eyeMask[i] > 0.5f)
                {
                    float lit = 0.25f + v * 1.9f;
                    px[i] = new Color32(
                        Fit(eye.r * lit), Fit(eye.g * lit), Fit(eye.b * lit),
                        px[i].a);
                    continue;
                }

                // The drawing's light is used as MODELLING around the coat
                // colour, not as a multiplier up from black.
                //
                // Multiplying was the first version and it put a ceiling on the
                // whole palette. The silhouette's own body averages 0.459 of
                // full light, so `v * 1.35` averages 0.62, and a white cat —
                // whose palette entry is 0.96 — came out at 0.51 measured over
                // the body: mid-grey. Every colour was squeezed into 0.17..0.51
                // and no cat could read as light-coloured at all. A white cat
                // was not merely wrong, it was unreachable by construction.
                //
                // Centring on the drawing's mid-tone fixes that without losing
                // the modelling: a pixel at the mid-tone comes out the coat
                // colour exactly, darker pixels fall towards black and lighter
                // ones rise towards white, in proportion. White lands near
                // white, black stays black, and the light and shadow the artist
                // drew survive in both.
                float shade = 1f + Contrast * (v - Midtone) / Midtone;

                // White markings keep the old treatment, compressed rather than
                // applied at full depth: they are already the lightest thing on
                // the cat and want less range, not more.
                shade = Mathf.Lerp(shade, 0.62f + v * 0.48f, white);

                px[i] = new Color32(
                    Fit(target.r * shade),
                    Fit(target.g * shade),
                    Fit(target.b * shade),
                    px[i].a);
            }
            return px;
        }

        /// <summary>
        /// One channel of the tinted coat, in 0..1, brought into a byte without
        /// throwing away what is at the ends.
        ///
        /// This used to be a plain clamp, and the clamp is the reason a white cat
        /// and a black cat both came out as featureless shapes.
        ///
        /// Work it through for white. The palette entry is 0.96, the mid-tone the
        /// shading is centred on is 0.459, and the contrast is 1.6 — so a pixel
        /// clips as soon as `shade` passes 1/0.96, which is a lightness of 0.471,
        /// four thousandths above the mid-tone. Half a white cat is above her own
        /// mid-tone by construction. Measured on the lying cat: 39% of her body at
        /// exactly 255,255,255, all of it joined up across the chest and the front
        /// legs, which is the "blot" in the owner's report seen from the other
        /// side — not a mark, a region where the picture ran out of numbers.
        /// Black has the same failure mirrored, at the bottom, where the deepest
        /// shadow lands below zero and a quarter of the modelling under her chin
        /// and between her legs becomes one flat black.
        ///
        /// A clamp is a cliff. What is wanted at both ends is a ramp: keep the
        /// middle of the range exactly as computed, because that is where almost
        /// every pixel is and it is where the palette was measured, and bend the
        /// last fifth so that it approaches the limit without ever reaching it.
        /// An exponential does that with no parameter to tune beyond where it
        /// starts, it is smooth at the join, and it is monotonic — a lighter pixel
        /// stays lighter than a darker one, which is the whole of what "keeps its
        /// modelling" means.
        ///
        /// The cost is that a coat can no longer reach pure white or pure black.
        /// Neither should: these are drawings of animals in a warm room, and the
        /// props they sit beside have no pure black or pure white in them either.
        /// </summary>
        private static byte Fit(float v)
        {
            const float Knee = 0.80f;   // the top fifth is bent
            const float Toe = 0.06f;    // and the bottom sixteenth

            if (v > Knee)
            {
                float over = (v - Knee) / (1f - Knee);
                v = Knee + (1f - Knee) * (1f - Mathf.Exp(-over));
            }
            else if (v < Toe)
            {
                // Mirror of the shoulder. Zero still maps to zero, so a fully
                // transparent or fully unlit pixel is unchanged.
                v = v <= 0f ? 0f : Toe * (1f - Mathf.Exp(-v / Toe));
            }

            return (byte)Mathf.Clamp(v * 255f, 0f, 255f);
        }

        private static readonly Color White = new(0.97f, 0.96f, 0.93f);

        /// <summary>
        /// A mask by name: the drawn file if one was ever added to
        /// Resources/Art, otherwise the one computed from the silhouette.
        ///
        /// This is the runtime choice task 18 asks for. It means the 27 masks
        /// of 40-art/04 can be drawn one at a time, whenever any of them is
        /// worth drawing, and each one improves the game the moment it lands —
        /// no code changes, no all-or-nothing milestone.
        /// </summary>
        /// <summary>
        /// Her distinctive marks, painted last.
        ///
        /// Last on purpose: the coat colour, the pattern and the white markings
        /// all describe a KIND of cat, and this describes THIS cat. It goes over
        /// them for the same reason a scar goes over skin.
        ///
        /// Light or dark against her own coat rather than a colour of its own.
        /// A white sock on a black cat and a black sock on a white one are the
        /// same fact from two sides, and asking the model for a colour would
        /// have it guess at one under whatever light the photograph was taken
        /// in.
        ///
        /// A place the silhouette cannot offer is skipped in silence — a patch
        /// invented where the tail is not is worse than a patch missing.
        /// </summary>
        private static Color32[] Marks(Color32[] px, int w, int h, CatTraits traits,
                                       Dictionary<string, float[]> masks, string baseName)
        {
            if (traits.Spots == null || traits.Spots.Count == 0) return px;

            var body = new bool[px.Length];
            for (int i = 0; i < px.Length; i++) body[i] = px[i].a > 200;

            var eyes = MaskOf(masks, baseName, CoatMasks.Eyes);
            var dst = px;

            foreach (var spot in traits.Spots)
            {
                if (!CoatMasks.PlaceOf(spot.Place, body, w, h, eyes,
                                       out float cy, out float cx, out float radius))
                {
                    Debug.Log($"[CoatBuilder] no {spot.Place} on this silhouette, " +
                              "spot not drawn");
                    continue;
                }

                // Seeded from the place and the drawing, so the same cat has the
                // same patch every time it is built, and two cats marked in the
                // same place do not share an outline.
                var mask = CoatMasks.Spot(body, w, h, cy, cx, radius,
                                          (baseName + spot.Place).GetHashCode());
                int painted = 0;

                bool light = spot.Shade == "light";
                dst = (Color32[])dst.Clone();
                for (int i = 0; i < dst.Length; i++)
                {
                    float a = mask[i];
                    if (a <= 0.01f) continue;
                    painted++;

                    // 0.86 rather than 1: even a white sock keeps a little of
                    // the fur under it, and a patch painted flat reads as a
                    // sticker. The shading of the coat shows through.
                    float k = light ? 1f + 1.35f * a : 1f - 0.62f * a;
                    dst[i] = new Color32(
                        (byte)Mathf.Clamp(dst[i].r * k, 0, 255),
                        (byte)Mathf.Clamp(dst[i].g * k, 0, 255),
                        (byte)Mathf.Clamp(dst[i].b * k, 0, 255),
                        dst[i].a);
                }
                Debug.Log($"[Spot] {spot.Place} at {cx:F0},{cy:F0} r={radius:F0} " +
                          $"painted {painted}px");
            }
            return dst;
        }

        /// <summary>
        /// Half the window the stripes are closed over, as a share of the
        /// width. A stripe on the shipped silhouette is about 2% across, and
        /// the narrowest thing that must survive — the gap between the legs —
        /// is nearer 10%.
        /// </summary>
        private const float DebandWindow = 0.05f;

        /// <summary>
        /// Half the window a CONTOUR is closed over: the width of the darkest
        /// lines in the drawing rather than of its bands.
        ///
        /// The two together are what let a stripe go and a tail stay. Measured
        /// on 2026-08-29: on a solid cream cat in the sitting pose the tail's
        /// own outline survived on 17% of its length against 67% on the same
        /// cat as a tabby, and sweeping <see cref="DebandWindow"/> from 5% down
        /// to 1.5% did not move that number above 24% — it only brought the
        /// stripes back (high-frequency spread 0.107 to 0.123 against the
        /// tabby's 0.157). A single closing cannot tell the two apart, because
        /// the tail's contour is NARROWER than a stripe, and a closing removes
        /// the narrow first. Any radius that erases a 20-pixel ring has already
        /// erased the 5-pixel line beside it.
        /// </summary>
        private const float ContourWindow = 0.005f;

        /// <summary>
        /// How much of the drawing's own fine lines is put back after the bands
        /// have been lifted off. One number for the whole cat, deliberately.
        ///
        /// This replaced a per-pixel gate on 2026-08-29, and the reason is worth
        /// keeping, because the gate was a good idea that this artwork defeats.
        ///
        /// It measured the local relief of the form under each line, on the
        /// argument that a contour is where one part of the cat passes in front
        /// of another — so the form changes across it — while a stripe is paint
        /// on fur whose form does not change. The argument is sound. What is not
        /// available is a measurement of it. Taken from the closing it is a
        /// feedback loop: a closing leaves a plateau where it filled a band, the
        /// plateau's edges lie on the band's edges, and the gate opens widest
        /// exactly where the stripes are — measured on the lying cat at 256,
        /// p50 0.0165 and p90 0.0482 against a gate saturating at 0.035, which
        /// put 68% of the median stripe straight back and 100% of the deepest
        /// tenth. Taken from a blur instead, which was the obvious repair, it
        /// gets worse rather than better — p50 0.0237 — because the local spread
        /// of a blurred cat is dominated by her own modelling, the roll of the
        /// ribs and the shading down a leg, and that is large everywhere.
        ///
        /// Underneath both attempts is a fact about the delivered art that no
        /// filter gets around: on the standing cat (state 2) the flank stripes
        /// are drawn as thin curved lines, the same width and the same depth as
        /// the contour round her tail. The file already recorded this once —
        /// "no window and no threshold on depth separates those" — and a second
        /// heuristic was stacked on top instead of the conclusion being taken.
        /// The conclusion is that they cannot be told apart locally, and a pass
        /// that pretends otherwise will always be putting back some stripes and
        /// erasing some contours; the only choice is which way it errs.
        ///
        /// For a cat the player is meant to recognise as HERS, it must err
        /// towards flat. A solid cat with a faint toe line lost is a cat; a
        /// solid cat with tabby stripes is somebody else's cat, which is the
        /// complaint that started this.
        ///
        /// Swept on the harness against the same cat rendered as a tabby, which
        /// is the only scale that means anything here — the tabby is what the
        /// drawing looks like untouched. On the lying cat at 256 the finished
        /// picture's high-frequency spread runs 0.041 at 0, 0.049 at 0.15 and
        /// 0.057 at 0.35, against the tabby's 0.186. 0.15 sits where the drawn
        /// lines are still there to be seen up close — the split between the
        /// toes, the line under the chin, the edge where the tail crosses the
        /// flank — and where nothing on her flank reads as a band at the size
        /// she is actually drawn. Above that the flank starts to stripe again
        /// for no gain in the face, which has its own protection and does not
        /// depend on this number.
        /// </summary>
        private const float LineKeep = 0.15f;

        /// <summary>
        /// How much of the drawing's broad modelling a non-tabby cat keeps.
        ///
        /// This is the second complaint, and unlike the stripes it is not a bug
        /// in a filter — it is a property of the artwork that no filter can be
        /// asked to fix. All three delivered silhouettes are pictures of silver
        /// tabbies, and a silver tabby is drawn with a chest, belly and paws far
        /// lighter than her back. That gradient is broader than any window, so
        /// neither a closing nor an opening touches it; it is the form.
        ///
        /// Measured on the lying cat at 256: after every band has been removed
        /// in both directions the form still spans 0.51 to 0.88, and Tint then
        /// multiplies whatever range it is given by about three. A white cat
        /// came out with 39% of her body at pure white, in one connected patch
        /// over the chest and the front legs. That is what the owner saw and
        /// described, of his brown-and-cream cat, as a patch covering half the
        /// animal.
        ///
        /// 0.45 keeps enough that the light still falls where it was drawn — the
        /// back lighter than the flank, shadow under the chin and between the
        /// legs — and not so much that a solid cat is born wearing a bib. It
        /// applies only where Deband runs, so a tabby, whose countershading is
        /// correct for her, keeps all of it.
        ///
        /// This is a repair and not a cure, and it should be said plainly: the
        /// cure is a silhouette drawn without stripes and with a neutral value,
        /// which would make both this constant and the whole debanding pass
        /// unnecessary.
        /// </summary>
        private const float FormKeep = 0.45f;

        /// <summary>
        /// Takes the drawn tabby markings off, and leaves everything else.
        ///
        /// The stripes are the one thing on this drawing that is high-frequency
        /// and dark: rings round the legs and tail, bands off the spine, the M
        /// on the forehead. Blur the cat's own light over a radius wider than a
        /// stripe and narrower than her body, and what is left is the form —
        /// belly lighter than back, legs shaded, ears modelled — with the bands
        /// gone. Lifting every pixel that is darker than that blur towards it
        /// erases the bands and touches nothing else, because only the bands are
        /// darker than their own neighbourhood.
        ///
        /// Highlights used to be left alone, on the argument that a cat whose
        /// highlights were flattened too would come out plastic. That was
        /// wrong, and it is half of the second complaint the owner made.
        ///
        /// The delivered silhouettes are not neutral drawings with stripes added
        /// on top; they are pictures of TABBIES, and a tabby's countershading is
        /// drawn into them at photographic strength — a chest, belly and paws
        /// far lighter than the back, over a third of the animal. That is a
        /// highlight by this pass's old definition, so it was preserved intact,
        /// and on a solid cat it reads as a pale blot rather than as modelling.
        /// The owner's words for it, on his own cat, were that it was not
        /// stripes but a patch taking up half the animal.
        ///
        /// So the lift now runs BOTH ways: a pixel is moved to the form whether
        /// it sits below it or above it. A bright band a stripe's width across
        /// is no more part of a solid cat than a dark one is.
        ///
        /// What that alone does not fix is the part of the countershading that
        /// is broader than any window — the whole underside being lighter than
        /// the whole back. That is not texture, it is the form itself, and
        /// removing it would flatten the cat into a paper cut-out. It is
        /// compressed instead; see <see cref="FormKeep"/>.
        ///
        /// Two things are protected, and they are protected because they are
        /// also dark and also small — the same description as a stripe. The eyes
        /// (mask from CoatMasks) and the muzzle (the same ellipse `mark_face`
        /// uses, so the nose and mouth line survive). Without them a solid cat
        /// loses her face, which is a worse trade than keeping a forehead M.
        /// </summary>
        private static Color32[] Deband(Color32[] px, int w, int h,
                                        Dictionary<string, float[]> masks,
                                        string baseName)
        {
            int radius = Mathf.Max(2, Mathf.RoundToInt(w * DebandWindow));

            var body = new bool[px.Length];
            var light = new float[px.Length];
            for (int i = 0; i < px.Length; i++)
            {
                body[i] = px[i].a > 200;
                if (body[i]) light[i] = (px[i].r + px[i].g + px[i].b) / 765f;
            }

            // A morphological CLOSING, not a blur.
            //
            // Blurring was the first attempt and it does not work: a blur
            // averages the stripes together with the fur, so the value it
            // offers is itself dragged down by the very bands being removed,
            // and they come back softer instead of going. Closing — take the
            // local maximum, then the local minimum over the same window —
            // deletes any dark feature narrower than the window outright and
            // leaves everything broader untouched. Removing thin dark marks
            // from a drawing is exactly what the operator is for.
            var closed = MinFilter(MaxFilter(light, body, w, h, radius),
                                   body, w, h, radius);
            // The closing removes what is DARKER than its window. An OPENING —
            // local minimum then local maximum — removes what is lighter, and a
            // solid cat needs both.
            //
            // Added 2026-08-29. Without it the drawn countershading survives
            // whole: the chest, belly and front paws of every one of these
            // silhouettes are drawn far lighter than the back, because they are
            // drawings of tabbies, and every solid cat inherited a pale bib she
            // had no trait for. Running the opening after the closing means the
            // pass no longer has an opinion about which direction a band goes in,
            // only about how wide it is, which is the only thing it can actually
            // measure.
            var opened = MaxFilter(MinFilter(closed, body, w, h, radius),
                                   body, w, h, radius);
            // One gentle blur afterwards, over a third of the radius: closing
            // leaves small flat plateaus where the bands were, and the fur
            // around them is not flat.
            var smooth = BoxBlur(BoxBlur(opened, body, w, h, Mathf.Max(1, radius / 3), horizontal: true),
                                 body, w, h, Mathf.Max(1, radius / 3), horizontal: false);

            // And now flatten what is left of the drawing's own modelling.
            //
            // `smooth` at this point is the form with every band gone in either
            // direction, and it still spans 0.51 to 0.88 on the lying cat: her
            // underside is drawn two-thirds again as light as her back. That is
            // the countershading of a silver tabby, and Tint below then stretches
            // whatever range it is handed by another factor of three. A white cat
            // came out with 39% of her body clipped to pure white, all of it in
            // one connected patch across the chest and front legs — the blot.
            //
            // Some modelling has to survive or she is a paper cut-out, so the
            // spread is scaled towards the body's own mean rather than removed.
            // Everything above and below moves in, the mean does not move, and
            // the light still falls the way the artist drew it — less far.
            float formMean = 0f; int formCount = 0;
            for (int i = 0; i < px.Length; i++)
                if (body[i]) { formMean += smooth[i]; formCount++; }
            if (formCount > 0)
            {
                formMean /= formCount;
                for (int i = 0; i < px.Length; i++)
                    if (body[i])
                        smooth[i] = formMean + (smooth[i] - formMean) * FormKeep;
            }

            // The same operator again, over a window the width of a LINE.
            //
            // This is what keeps the cat's drawn edges when her bands go. A
            // closing fills any dark feature narrower than its window, so the
            // difference between a pixel and the fine closing is exactly "how
            // much darker than a line's width this pixel is" — nonzero on the
            // contour round the tail, on the split between two toes, on the
            // line under the chin, and ZERO in the middle of a stripe, which is
            // far too wide for this window to bridge.
            //
            // Subtracting that difference back from the coarse target lifts the
            // bands to the fur around them and leaves the drawing's own lines
            // sitting at the same depth below it. One extra pass at a sixth of
            // the coarse radius; the cost is a few per cent of a build that is
            // already cached to disk.
            int fineRadius = Mathf.Max(2, Mathf.RoundToInt(w * ContourWindow));
            var fine = MinFilter(MaxFilter(light, body, w, h, fineRadius),
                                 body, w, h, fineRadius);

            // And put back a fixed share of it — see <see cref="LineKeep"/> for
            // the two measured attempts at deciding this per pixel and why
            // neither can work on the delivered art.
            //
            // The subtraction itself is the honest half of this pass and it is
            // worth stating plainly, because it is the part that does the work.
            // Write `deep = smooth - light` for how far a pixel sits below the
            // form, and `narrow = fine - light` for how far it sits below its
            // own neighbourhood at a line's width. Then `deep - narrow` is
            // exactly the BAND component: what is dark over a stripe's width but
            // not over a line's. Lifting by that and leaving `narrow` alone is
            // the decomposition, and `smooth - (fine - light)` is it written out.
            // In the core of a stripe `narrow` is near zero and the whole band
            // goes; on a drawn line `narrow` is the line's own depth and all of
            // it would stay, which is where LineKeep chooses to keep only some.

            if (Stages != null)
            {
                Stages("light", light, w, h);
                Stages("closed", closed, w, h);
                Stages("smooth", smooth, w, h);
                Stages("fine", fine, w, h);
                var em = MaskOf(masks, baseName, CoatMasks.Eyes);
                if (em != null) Stages("eyesmask", em, w, h);
                // Both, and separately. `eyesmask` is empty on all three shipped
                // poses and `eyeguard` is not, and a picture of the two side by
                // side is the shortest way to see that — which is the diagnosis
                // that took two days and a rubbed-out face to arrive at.
                var eg = MaskOf(masks, baseName, CoatMasks.EyeGuard);
                if (eg != null) Stages("eyeguard", eg, w, h);
            }

            // What to protect: her face.
            //
            // Anchored to the eyes that were actually found, because everything
            // else tried here failed. `mark_face` is placed from the mean of the
            // eye blobs and walks onto a cheek. A band of top rows works for a
            // sitting cat and cuts a lying one across the back, and leaves a
            // hard seam where it ends. The darkest 2% of the drawing is not the
            // face at all — measured, it is the stripes, which are as dark as an
            // eye and cover far more of her.
            //
            // The eyes' own bounding box has none of those failures — but it has
            // to come from a detector that actually fires, and until 2026-08-29
            // this read CoatMasks.Eyes, which never has.
            //
            // That claim was checked rather than assumed, after the sitting cat
            // came back from this pass with her eyes rubbed out. Across all three
            // shipped poses, at 256 and at 512, `CoatMasks.Eyes` is EMPTY: its
            // size floor rejects five of the six eye blobs on this artwork, and
            // on the sitting cat its pairing rule rejects the sixth as well
            // because her head is turned down and her eyes are not level. So
            // every pose has always fallen through to the muzzle band below, and
            // the eye-anchored box was code that had never once executed.
            //
            // The band is placed at a fixed fraction of the figure's height, and
            // that is the whole of why the poses differ: measured over the eye
            // pixels of each, the protection it grants is 1.00 on the standing
            // cat, 0.58 on the sitting one and 0.33 on the lying one. State 2 is
            // fine by luck. State 1 keeps only the far eye's lid, which happens
            // to fall inside the band, and loses the near eye entirely.
            //
            // CoatMasks.EyeGuard is the same search with its two acceptance rules
            // relaxed to what this job actually needs; see the note there. The
            // band stays underneath as a fallback, because a cat whose eyes are
            // shut is a pose this art could still deliver, and a face is not
            // something to lose to a heuristic that missed.
            var eyes = MaskOf(masks, baseName, CoatMasks.EyeGuard);
            int fx0 = int.MaxValue, fx1 = int.MinValue, fy0 = int.MaxValue, fy1 = int.MinValue;
            if (eyes != null)
                for (int i = 0; i < eyes.Length; i++)
                    if (eyes[i] > 0.5f)
                    {
                        int y = i / w, x = i % w;
                        if (x < fx0) fx0 = x; if (x > fx1) fx1 = x;
                        if (y < fy0) fy0 = y; if (y > fy1) fy1 = y;
                    }

            bool haveFace = fx1 > fx0;
            float span = haveFace ? fx1 - fx0 : 0f;
            // A third of an eye-span out to the sides and up, three-quarters of
            // one down for the muzzle. Rows run bottom-up, so down the face is
            // decreasing y.
            //
            // Was 0.45/0.45/1.05 until 2026-08-29, and that box, together with
            // the much larger fallback below it, protected 3109 of the lying
            // cat's 15005 body pixels — a fifth of her, at full drawn contrast,
            // including her whole forehead and the M drawn on it. Whatever the
            // rest of this pass achieved, a fifth of the animal was never
            // debanded at all, and it was the fifth a player looks at.
            //
            // The box can be this much smaller now because it is no longer
            // carrying the whole job. It was sized when the only alternative to
            // protection was total flattening; LineKeep now returns a share of
            // every fine dark line everywhere on the cat, so the nose, the mouth
            // line and the split of the lip survive on their own merits, as
            // narrow marks, wherever they happen to be. What the box still does
            // is hold the immediate area of the eyes and muzzle at full strength,
            // because that is the one place where losing a line costs the cat her
            // expression rather than a little texture.
            // Asymmetric, and deliberately mean UPWARD.
            //
            // Below the eyes is the muzzle — nose, mouth line, the split of the
            // lip — which is what a player reads as the cat's expression and
            // what no narrow-line rule recovers. Above the eyes is the forehead,
            // and on these silhouettes the forehead carries the tabby M, the
            // single most legible tabby marking there is. The box wants to reach
            // generously into the first and barely into the second.
            //
            // Measured on the harness when the eye guard first started firing:
            // at the old 0.25 up with a 0.7-span feather the protection reached
            // 0.95 of an eye-span above the eyes, which on the sitting cat is her
            // entire forehead, and a solid cream cat's high-frequency spread went
            // from 0.043 to 0.086 against a tabby's 0.166 — half the stripes
            // back, bought for nothing, since the eyes are below that reach.
            float padX = span * 0.24f, padUp = span * 0.22f, padDown = span * 0.55f;

            int top = h, bottom = -1;
            for (int i = 0; i < px.Length; i++)
                if (body[i])
                {
                    int y = i / w;
                    if (y < top) top = y;
                    if (y > bottom) bottom = y;
                }
            if (bottom < 0) return px;

            // The fallback head box, for when no dark feature is found on the
            // head at all — a cat drawn with her eyes shut, which is a pose this
            // art could still deliver.
            //
            // It used to be the path every cat took, and that is worth leaving
            // written down, because it was quietly carrying the whole job while
            // the eye-anchored code above it read as though it were. Being a
            // band at a fixed fraction of the figure's height, it cannot follow
            // a head: measured over the eye pixels of each shipped pose it
            // grants 1.00 of protection to the standing cat, 0.58 to the sitting
            // one and 0.33 to the lying one. Nothing chose those numbers; they
            // are where the constant happened to fall on three drawings, and two
            // of the three cats paid for it with an eye.
            //
            // A band of top rows alone is not enough: it protects the whole
            // width, so a LYING cat keeps the stripes on her back and gets a
            // straight seam across her middle. Her ears are still the highest
            // thing on her, though, whatever the pose — so the top of the figure
            // gives the head's horizontal extent, and the box is the two
            // together.
            int height = bottom - top;
            int earsFrom = bottom - Mathf.RoundToInt(height * 0.12f);
            int hx0 = w, hx1 = -1;
            for (int y = earsFrom; y <= bottom; y++)
                for (int x = 0; x < w; x++)
                    if (body[y * w + x])
                    {
                        if (x < hx0) hx0 = x;
                        if (x > hx1) hx1 = x;
                    }
            // A quarter again to each side: the cheeks are wider than the ears.
            // Was half again, alongside a box running down 42% of the figure,
            // and the two together are what protected a fifth of the cat.
            float earPad = hx1 > hx0 ? (hx1 - hx0) * 0.25f : 0f;

            // The muzzle, not the head.
            //
            // This box used to run from the top of the figure down through 42%
            // of her height — the whole head and the shoulders under it — on the
            // reasoning that a face is not something to lose to a heuristic that
            // missed. But protecting the head protects the forehead, and the
            // forehead is where these silhouettes carry the tabby M. A solid cat
            // kept a drawn M between her ears in every render this project has
            // ever produced, and it is the single most legible tabby marking a
            // cat has.
            //
            // What genuinely cannot be recovered from a narrow-line rule is the
            // small dark mass of the nose and the mouth line under it, so that is
            // what the fallback holds now: a band around a fifth of the way down
            // from the top of the head, a ninth of her height deep. Everything
            // above it — forehead, ears, the M — is debanded like the rest of her.
            // A quarter of the figure's height down from the top of the head,
            // seven hundredths deep. That is where these three poses put the
            // nose: CoatMasks.PlaceOf independently estimates the eyes at
            // `bottom - height * 0.18` and the muzzle a further 0.075 below, and
            // this band is the same place arrived at from the other direction.
            //
            // The band was two hundredths higher and half again as deep until it
            // was looked at on the harness, and at that size its feathered upper
            // edge reached the forehead — so the M drawn between these kittens'
            // ears was still half protected, on the very cat whose whole purpose
            // is not to be a tabby. It is the most recognisable tabby marking
            // there is and it was the last one left.
            float muzzleCy = bottom - height * 0.25f;
            float muzzleHalfY = height * 0.07f;

            // Feathered over a twelfth of her height. A hard edge to the
            // protection reads as a seam across the shoulders — it did, and it
            // was the first thing visible on the harness.
            float feather = Mathf.Max(1f, height * 0.045f);

            // The protection is a BOX WITH SOFT SIDES, and every side is soft.
            //
            // Both boxes below used to be all-or-nothing on three of their four
            // sides: inside, `continue`; one pixel outside, the full lift. Only
            // the fallback box's lower edge was ever feathered. A straight edge
            // in the amount of lift is a straight edge in the picture, and this
            // drawing has none of its own, so it is unmistakable — an iOS
            // playthrough on 2026-08-29 found a razor-straight seam down the
            // back of the lying cat (state 3), which is the right-hand side of
            // the eye-anchored face box crossing her shoulder, with the box's
            // top edge meeting it in a right angle.
            //
            // Feathering the box on all four sides costs nothing: the same
            // pixels are protected, the boundary is simply no longer visible.
            // The ramp is smoothstepped rather than linear so that the join at
            // each end has no crease of its own.
            float boxCx, boxCy, boxHalfX, boxHalfY, margin;
            if (haveFace)
            {
                boxCx = (fx0 - padX + fx1 + padX) * 0.5f;
                boxHalfX = (fx1 + padX - (fx0 - padX)) * 0.5f;
                boxCy = (fy0 - padDown + fy1 + padUp) * 0.5f;
                boxHalfY = (fy1 + padUp - (fy0 - padDown)) * 0.5f;
                margin = Mathf.Max(1f, span * 0.25f);
            }
            else
            {
                float lx = hx1 > hx0 ? hx0 - earPad : 0f;
                float rx = hx1 > hx0 ? hx1 + earPad : w - 1;
                boxCx = (lx + rx) * 0.5f;
                boxHalfX = (rx - lx) * 0.5f;
                boxCy = muzzleCy;
                boxHalfY = muzzleHalfY;
                margin = feather;
            }

            var dst = new Color32[px.Length];
            Array.Copy(px, dst, px.Length);
            _protectedPx = _unmovedPx = 0;
            var keptField = Stages != null ? new float[px.Length] : null;
            var wantField = Stages != null ? new float[px.Length] : null;
            int lifted = 0;
            for (int i = 0; i < px.Length; i++)
            {
                if (!body[i]) continue;
                if (eyes != null && eyes[i] > 0.4f) continue;

                int py = i / w, pxx = i % w;
                float outX = (Mathf.Abs(pxx - boxCx) - boxHalfX) / margin;
                float outY = (Mathf.Abs(py - boxCy) - boxHalfY) / margin;
                float t = Mathf.Clamp01(Mathf.Max(outX, outY));
                float keep = 1f - t * t * (3f - 2f * t);   // smoothstep, inverted
                if (keptField != null) keptField[i] = keep;
                if (wantField != null) wantField[i] = light[i];
                if (keep >= 0.999f) { _protectedPx++; continue; }

                // Lift to the fur around the bands, then put back the share of
                // the drawing's own fine lines that LineKeep asks for.
                float here = light[i];
                float want = smooth[i] - LineKeep * (fine[i] - here);
                // No `if (want <= here) continue` any more. That single line was
                // what let the drawn countershading through untouched: it made
                // the pass one-directional, so a band lighter than its
                // surroundings — which is what the chest and belly of every one
                // of these tabby silhouettes is — was never a candidate for
                // removal at all. See the note on this method.
                if (Mathf.Abs(want - here) <= 0.004f) { _unmovedPx++; continue; }
                if (keep > 0f) want = Mathf.Lerp(want, here, keep);
                if (wantField != null) wantField[i] = want;

                // Scaled, not replaced: the hue of the fur is in the ratio
                // between the channels, and setting a lightness would grey it.
                //
                // The ceiling of 3.5 bounds how far the darkest 5% of the
                // drawing can be lifted, and on these silhouettes the darkest 5%
                // is her eyes.
                //
                // It was described here as "the only thing standing between a
                // solid cat and a blank face", and that was true when it was
                // written and is the reason it was not enough. A ceiling is a
                // limit on the RATIO, so what it saves is a pixel that is very
                // much darker than the fur it is being lifted towards — a black
                // pupil — and what it does not save is an eye drawn in mid-tone.
                // The sitting cat's near eye is a wide grey almond whose lift
                // never reaches 3.5, so it was raised exactly onto the value of
                // the cheek around it and vanished, leaving the thin dark line
                // of its rim behind: the "faint wireframe where the eye was"
                // in the owner's report. The lying cat lost one eye the same way
                // and kept the other, which is why the pose read as fine.
                //
                // What actually protects an eye is knowing where it is, which is
                // now CoatMasks.EyeGuard's job. This ceiling stays as the second
                // line — it costs nothing and it is the thing that still holds
                // if the guard ever misses — but it is not the defence.
                //
                // The price is that a mark deeper than 3.5x its target keeps
                // some of its depth, and the tabby M between these kittens' ears
                // is exactly that deep. A solid cat still carries a faint M. It
                // is the last drawn marking left on her, and the cure for it is
                // art without an M in it rather than another constant here.
                float k = Mathf.Min(want / Mathf.Max(here, 0.02f), 3.5f);
                dst[i] = new Color32(
                    (byte)Mathf.Clamp(px[i].r * k, 0, 255),
                    (byte)Mathf.Clamp(px[i].g * k, 0, 255),
                    (byte)Mathf.Clamp(px[i].b * k, 0, 255),
                    px[i].a);
                lifted++;
            }
            // The bands are gone; now put the coat's own lightness back.
            //
            // Every pixel this pass touches gets LIGHTER, so the cat as a whole
            // comes out lighter than she was drawn — and on a pale coat that
            // clips. Measured on a cream cat in the sitting pose: 6% of her body
            // at pure white, her chest a flat blank and her tail smeared into
            // it, while the same cream cat as a tabby was fine. Found by
            // uploading a photograph and looking at the screen it leads to.
            //
            // Removing a stripe is a statement about texture, not about how
            // light she is. So the body's mean is measured before and after and
            // scaled back — the bands stay gone, the coat keeps its value, and
            // nothing clips.
            float before = 0f, after = 0f; int n = 0;
            for (int i = 0; i < px.Length; i++)
            {
                if (!body[i]) continue;
                before += light[i];
                after += (dst[i].r + dst[i].g + dst[i].b) / 765f;
                n++;
            }
            if (n > 0 && after > 0.001f)
            {
                float back = (before / n) / (after / n);
                for (int i = 0; i < px.Length; i++)
                {
                    if (!body[i]) continue;
                    dst[i] = new Color32(
                        (byte)Mathf.Clamp(dst[i].r * back, 0, 255),
                        (byte)Mathf.Clamp(dst[i].g * back, 0, 255),
                        (byte)Mathf.Clamp(dst[i].b * back, 0, 255),
                        dst[i].a);
                }
                Debug.Log($"[Deband] lifted {lifted}px, value scaled back x{back:F2}");
                Debug.Log($"[Deband] body {n}px  protected {_protectedPx}  " +
                          $"unmoved {_unmovedPx}  lifted {lifted}");
            }
            if (Stages != null)
            {
                Stages("keep", keptField, w, h);
                Stages("want", wantField, w, h);
                var outLight = new float[px.Length];
                for (int i = 0; i < px.Length; i++)
                    if (body[i]) outLight[i] = (dst[i].r + dst[i].g + dst[i].b) / 765f;
                Stages("debanded", outLight, w, h);
            }
            return dst;
        }

        /// <summary>
        /// The lightest value within <paramref name="radius"/>, per axis, over
        /// the body only. Run twice — once each way — this is a square window.
        ///
        /// Naive rather than deque-based: at the 512 a photograph's cat is built
        /// at, this is about 26 million comparisons per pass and the coat is
        /// built once and then cached, on disk and in memory. The clever version
        /// is worth writing the day that stops being true.
        /// </summary>
        private static float[] MaxFilter(float[] src, bool[] body, int w, int h, int radius)
            => Extremum(src, body, w, h, radius, wantMax: true);

        /// <summary>The darkest value within the same window; the other half of
        /// the closing.</summary>
        private static float[] MinFilter(float[] src, bool[] body, int w, int h, int radius)
            => Extremum(src, body, w, h, radius, wantMax: false);

        private static float[] Extremum(float[] src, bool[] body, int w, int h,
                                        int radius, bool wantMax)
        {
            var pass = new float[src.Length];
            var dst = new float[src.Length];

            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    if (!body[i]) continue;
                    float best = src[i];
                    for (int k = Mathf.Max(0, x - radius); k <= Mathf.Min(w - 1, x + radius); k++)
                    {
                        int j = y * w + k;
                        if (!body[j]) continue;
                        if (wantMax ? src[j] > best : src[j] < best) best = src[j];
                    }
                    pass[i] = best;
                }

            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    if (!body[i]) continue;
                    float best = pass[i];
                    for (int k = Mathf.Max(0, y - radius); k <= Mathf.Min(h - 1, y + radius); k++)
                    {
                        int j = k * w + x;
                        if (!body[j]) continue;
                        if (wantMax ? pass[j] > best : pass[j] < best) best = pass[j];
                    }
                    dst[i] = best;
                }
            return dst;
        }

        /// <summary>
        /// One axis of a box blur over the body only, by running sum — O(1) per
        /// pixel rather than O(radius), because this runs at 512 on a phone for
        /// a cat built from a photograph.
        ///
        /// Averaged over the body pixels in the window rather than all of them,
        /// so the blur does not drag the background in and wash out the edge.
        /// </summary>
        private static float[] BoxBlur(float[] src, bool[] body, int w, int h,
                                       int radius, bool horizontal)
        {
            var dst = new float[src.Length];
            int outer = horizontal ? h : w, inner = horizontal ? w : h;

            for (int o = 0; o < outer; o++)
            {
                float sum = 0f;
                int count = 0;

                int Index(int k) => horizontal ? o * w + k : k * w + o;

                for (int k = 0; k <= radius && k < inner; k++)
                {
                    int i = Index(k);
                    if (body[i]) { sum += src[i]; count++; }
                }

                for (int k = 0; k < inner; k++)
                {
                    int at = Index(k);
                    dst[at] = count > 0 ? sum / count : src[at];

                    int add = k + radius + 1, drop = k - radius;
                    if (add < inner)
                    {
                        int i = Index(add);
                        if (body[i]) { sum += src[i]; count++; }
                    }
                    if (drop >= 0)
                    {
                        int i = Index(drop);
                        if (body[i]) { sum -= src[i]; count--; }
                    }
                }
            }
            return dst;
        }

        /// <summary>
        /// A mask by name: the drawn file if one exists, otherwise the one
        /// computed from the silhouette.
        ///
        /// The drawn file is brought to the working size, and that line is the
        /// whole reason this method is not four lines long. Masks are drawn at
        /// 1024, the size of the silhouette on disk, but the coat is built from
        /// a `Downscale`d copy — 256 for the board, 512 for the card. Without
        /// the resize a drawn mask returns a 1024×1024 array that the caller
        /// then walks with a 256×256 index: the first 65 536 entries of a
        /// 1024-wide picture are its top 64 rows, which on every one of these
        /// files is empty margin above the cat's ears. The mask applied
        /// perfectly and to nothing.
        ///
        /// Found on 2026-08-29, the first day a drawn mask existed. The hook
        /// itself is older than that and was described in `40-art/04-cat-layers`
        /// as "CoatBuilder picks each one up with no code change" — true of the
        /// lookup, false of the arithmetic, and untestable until there was a
        /// file to pick up. Any of the masks that task would have produced
        /// would have failed the same silent way.
        ///
        /// The working size is taken from the computed masks rather than passed
        /// in: they are built at it by construction, and threading a width and
        /// height through seven call sites to re-state a fact already in the
        /// dictionary would be the more fragile change.
        /// </summary>
        private static float[] MaskOf(Dictionary<string, float[]> computed,
                                      string baseName, string maskName)
        {
            var drawn = Resources.Load<Texture2D>($"Art/{baseName}_{maskName}");
            if (drawn != null)
            {
                int want = 0;
                foreach (var any in computed.Values) { want = any.Length; break; }

                if (want > 0 && drawn.width * drawn.height != want)
                {
                    int side = Mathf.RoundToInt(Mathf.Sqrt(want));
                    if (side * side == want)
                    {
                        var scaled = Downscale(drawn, side);
                        if (scaled != null) drawn = scaled;
                    }
                    else
                    {
                        // Non-square working size is not something this project
                        // produces, and guessing at one would put the mask half
                        // a cat out of place. Silence beats a wrong marking.
                        Debug.LogWarning($"[CoatBuilder] drawn mask {maskName} " +
                                         $"ignored: cannot fit {drawn.width}×{drawn.height} " +
                                         $"to {want} px");
                        return computed.TryGetValue(maskName, out var fallback)
                            ? fallback : null;
                    }
                }

                var px = ReadPixels(drawn);
                var m = new float[px.Length];
                for (int i = 0; i < px.Length; i++)
                    m[i] = px[i].r / 255f;      // white where the mask applies
                return m;
            }
            return computed.TryGetValue(maskName, out var c) ? c : null;
        }

        // ---------------------------------------------------------------
        // 5. Outline
        // ---------------------------------------------------------------

        /// <summary>
        /// Grow a dark rim outside the silhouette. The props carry this outline
        /// and the cats were delivered without it — measured as rim-versus-
        /// interior lightness, 59–67 for props against 2–11 for the cats — so
        /// beside a prop the cat read as a different game.
        ///
        /// Built from the alpha edge, so it applies to every silhouette
        /// delivered later without anyone having to draw it.
        /// </summary>
        /// Done a horizontal band at a time — rows <paramref name="y0"/> up to
        /// <paramref name="y1"/> — because <see cref="Steps"/> puts this pass
        /// down between bands. Every row reads `px` and `drawn` and writes only
        /// its own rows of `dst`, so where the bands fall makes no difference to
        /// the result: the dilation window reads the *source*, which no band
        /// ever touches. The seam had to go inside this pass rather than merely
        /// around it — it scans a square window per pixel and is the single most
        /// expensive stage of the build, so a frame boundary on either side of
        /// it would still have left one step far longer than a frame.
        /// </summary>
        /// <param name="drawn">The silhouette the artist delivered, before the
        /// tufts were grown on it. The rim is a dilation of this and not of the
        /// finished alpha, so a strand of matted fur no longer drags an ink blob
        /// out with it — see <see cref="Build"/>.</param>
        private static void OutlineRows(Color32[] px, Color32[] dst, int w, int h,
                                        int width, bool[] drawn, int y0, int y1)
        {
            for (int y = y0; y < y1; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    if (px[i].a > 200) continue;          // inside, leave alone

                    // The shadow is part of the drawing, not a hole in it:
                    // outlining it draws a dark ring around the cat's shadow.
                    // So half-transparent pixels are normally left alone —
                    // EXCEPT the ones that touch her body.
                    //
                    // Those are not background at all: they are the silhouette's
                    // own soft edge, one pixel of pale anti-aliasing lying
                    // between the ink outside and the fur inside. Skipping them
                    // left a light hairline all the way around her. On the board
                    // she is 120 points wide and it is invisible; on the cat card
                    // she is nearly the full width of the screen, the source is
                    // 256, and at that magnification the hairline reads as a
                    // dashed line — as if she had been cut out with scissors.
                    // Seen on Android on 2026-08-28.
                    //
                    // A shadow pixel touches nothing solid, so it still keeps out
                    // of this and the shadow stays unringed.
                    if (px[i].a > 40 && !TouchesBody(drawn, w, h, x, y)) continue;

                    // Drawn pixel within `width`? Then this is rim.
                    bool near = false;
                    for (int dy = -width; dy <= width && !near; dy++)
                    {
                        int sy = y + dy;
                        if (sy < 0 || sy >= h) continue;
                        for (int dx = -width; dx <= width; dx++)
                        {
                            int sx = x + dx;
                            if (sx < 0 || sx >= w) continue;
                            if (dx * dx + dy * dy > width * width) continue;
                            if (drawn[sy * w + sx]) { near = true; break; }
                        }
                    }
                    if (!near) continue;

                    // Over the tufts, not under them: a strand that pokes
                    // through the rim looks like a mistake.
                    byte a = (byte)Mathf.Max(px[i].a, (byte)235);
                    dst[i] = new Color32(Ink.r, Ink.g, Ink.b, a);
                }
        }

        /// <summary>
        /// Does this pixel sit right against something solid? Eight neighbours,
        /// not four: a diagonal step is where a hairline shows first, and it is
        /// exactly the diagonals that made the gap look dashed rather than
        /// merely thin.
        /// </summary>
        private static bool TouchesBody(bool[] drawn, int w, int h, int x, int y)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                int sy = y + dy;
                if (sy < 0 || sy >= h) continue;
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int sx = x + dx;
                    if (sx < 0 || sx >= w) continue;
                    if (drawn[sy * w + sx]) return true;
                }
            }
            return false;
        }
    }
}
