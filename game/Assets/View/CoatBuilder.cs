using System;
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
        /// How far light and shadow move away from that colour. At 1.6 the
        /// deepest shadow on the drawing (v = 0.02) lands at 0.30 of the coat
        /// colour and the brightest highlight (v = 1.0) at 1.89, clipped — the
        /// same range the drawing has, carried onto whatever colour the cat is.
        /// </summary>
        private const float Contrast = 1.6f;

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
            if (baseCoat == null) throw new ArgumentNullException(nameof(baseCoat));
            if (traits == null) throw new ArgumentNullException(nameof(traits));
            state = Mathf.Clamp(state, 1, 3);

            int w = baseCoat.width, h = baseCoat.height;
            var px = ReadPixels(baseCoat);

            // Masks come from the silhouette itself (CoatMasks), so they line
            // up exactly, and a hand-drawn file replaces any of them the moment
            // one exists — see MaskOf.
            var masks = CoatMasks.Build(px, w, h, seed: baseCoat.name.GetHashCode());

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

            px = Weather(px, w, h, Neglect[state], seed: 5);

            // The stripes come off for every cat that is not a tabby.
            //
            // This is what the striped silhouette was commissioned FOR. Stripes
            // can be taken off a drawing that has them; they cannot be put onto
            // one that does not. Leaving them on meant a black cat, a white cat
            // and a calico all came out as the same tabby in three colours —
            // the photograph, the model and five traits reduced to a colour
            // picker. `pattern` is one of the five things read off the player's
            // cat, and until 2026-08-29 it was the one that changed nothing a
            // player could see.
            if (traits.Pattern != "tabby")
                px = Deband(px, w, h, masks, baseCoat.name);
            px = Tint(px, traits, masks, baseCoat.name);
            px = Marks(px, w, h, traits, masks, baseCoat.name);
            px = Outline(px, w, h, Mathf.RoundToInt(w * 0.016f), drawn);

            var result = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: false)
            {
                name = $"{baseCoat.name}_{traits.BaseColor}_{state}",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            result.SetPixels32(px);
            result.Apply(updateMipmaps: false);
            return result;
        }

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
        private const int CoatVersion = 2;

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
            // Spots are part of the key. Without them two cats alike in every
            // class trait and different in the one thing that identifies them —
            // a sock on one paw — share a cached coat, and the second player
            // gets the first player's cat.
            var marks = string.Join(",", traits.Spots.Select(m => $"{m.Place}:{m.Shade}"));
            var key = $"{traits.BaseColor}/{traits.Pattern}/{traits.FurLength}/" +
                      $"{traits.EyeColor}/{string.Join(",", traits.WhiteMarkings)}/" +
                      $"{marks}/{state}@{size}";
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

            var art = LoadBase(traits, state);
            if (art == null) return null;
            var built = TryBuild(Downscale(art, size), traits, state);
            if (built != null)
            {
                _builtCache[key] = built;
                SaveCached(key, built);
            }
            return built;
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
                LastFailure = $"{e.GetType().Name}: {e.Message}";
                if (_warned.Add("coat-build-failure"))
                {
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
                return null;
            }
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
            var eyeMask = MaskOf(masks, baseName, CoatMasks.Eyes);

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
                        (byte)Mathf.Clamp(eye.r * 255f * lit, 0, 255),
                        (byte)Mathf.Clamp(eye.g * 255f * lit, 0, 255),
                        (byte)Mathf.Clamp(eye.b * 255f * lit, 0, 255),
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
                    (byte)Mathf.Clamp(target.r * 255f * shade, 0, 255),
                    (byte)Mathf.Clamp(target.g * 255f * shade, 0, 255),
                    (byte)Mathf.Clamp(target.b * 255f * shade, 0, 255),
                    px[i].a);
            }
            return px;
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
        /// How much relief in the debanded form counts as flat fur, and how
        /// much counts as one part of the cat passing in front of another.
        /// Below the floor nothing is put back and the bands go completely;
        /// above ReliefFull the drawing's line is kept whole. Taken from the
        /// measured spread quoted in <see cref="Deband"/>: striped flank sits
        /// at 0.003–0.006, the tail's contour at 0.041.
        /// </summary>
        private const float ReliefFloor = 0.015f;
        private const float ReliefFull = 0.035f;

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
        /// Highlights are left alone: only darker-than-neighbourhood pixels move.
        /// A cat that had her highlights flattened too would come out plastic.
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
            // One gentle blur afterwards, over a third of the radius: closing
            // leaves small flat plateaus where the bands were, and the fur
            // around them is not flat.
            var smooth = BoxBlur(BoxBlur(closed, body, w, h, Mathf.Max(1, radius / 3), horizontal: true),
                                 body, w, h, Mathf.Max(1, radius / 3), horizontal: false);

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

            // And put it back only where the FORM is turning.
            //
            // Restoring every fine line everywhere does not work, and why is
            // worth keeping. On the standing cat (state 2) the drawn stripes are
            // as narrow and as deep as the tail's contour is on the sitting one:
            // measured on the silhouettes themselves, the fine closing fills the
            // state-2 flank stripes to 0.178 at the 90th percentile against
            // 0.097 at the tail contour's median. No window and no threshold on
            // depth separates those, and the attempt that ignored this gave the
            // standing cat her stripes back — removal on her flank fell from
            // 61% to 21%.
            //
            // What does separate them is what lies UNDER the line. A contour is
            // where one part of the animal passes in front of another, so the
            // debanded form itself changes across it; a stripe is painted on fur
            // whose form does not change at all. Over the same regions the local
            // spread of `smooth` is 0.041 at the tail's contour against 0.006 on
            // the sitting cat's striped flank and 0.003 on the standing cat's.
            //
            // So the line goes back in proportion to the relief beneath it.
            int reliefRadius = Mathf.Max(2, radius / 2);
            var mean = BoxBlur(BoxBlur(smooth, body, w, h, reliefRadius, horizontal: true),
                               body, w, h, reliefRadius, horizontal: false);
            var sq = new float[px.Length];
            for (int i = 0; i < px.Length; i++) if (body[i]) sq[i] = smooth[i] * smooth[i];
            var meanSq = BoxBlur(BoxBlur(sq, body, w, h, reliefRadius, horizontal: true),
                                 body, w, h, reliefRadius, horizontal: false);
            var relief = new float[px.Length];
            for (int i = 0; i < px.Length; i++)
                if (body[i])
                    relief[i] = Mathf.Sqrt(Mathf.Max(0f, meanSq[i] - mean[i] * mean[i]));

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
            // The eyes' own bounding box has none of those failures, and since
            // the detector now finds them at 256 as well as 512
            // (CoatMasks.FindEyes), it is available wherever this runs. The band
            // stays underneath as a fallback: a cat whose eyes are shut is one
            // of the three shipped poses, and a face is not something to lose to
            // a heuristic that missed.
            var eyes = MaskOf(masks, baseName, CoatMasks.Eyes);
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
            // Half an eye-span out to the sides and up, a whole one down for the
            // muzzle. Rows run bottom-up, so down the face is decreasing y.
            float padX = span * 0.45f, padUp = span * 0.45f, padDown = span * 1.05f;

            int top = h, bottom = -1;
            for (int i = 0; i < px.Length; i++)
                if (body[i])
                {
                    int y = i / w;
                    if (y < top) top = y;
                    if (y > bottom) bottom = y;
                }
            if (bottom < 0) return px;

            // The fallback head box, for when the eyes are not found — which is
            // the common case at the 256 the board builds at, because the drawn
            // stripes fill the darkest percentiles the eye detector works in.
            //
            // A band of top rows alone is not enough: it protects the whole
            // width, so a LYING cat keeps the stripes on her back and gets a
            // straight seam across her middle. Her ears are still the highest
            // thing on her, though, whatever the pose — so the top of the figure
            // gives the head's horizontal extent, and the box is the two
            // together.
            int height = bottom - top;
            int headFrom = bottom - Mathf.RoundToInt(height * 0.42f);
            int earsFrom = bottom - Mathf.RoundToInt(height * 0.12f);
            int hx0 = w, hx1 = -1;
            for (int y = earsFrom; y <= bottom; y++)
                for (int x = 0; x < w; x++)
                    if (body[y * w + x])
                    {
                        if (x < hx0) hx0 = x;
                        if (x > hx1) hx1 = x;
                    }
            // Half again to each side: the cheeks are wider than the ears.
            float earPad = hx1 > hx0 ? (hx1 - hx0) * 0.5f : 0f;

            // Feathered over an eighth of her height. A hard edge to the
            // protection reads as a seam across the shoulders — it did, and it
            // was the first thing visible on the harness.
            float feather = Mathf.Max(1f, height * 0.12f);

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
                margin = Mathf.Max(1f, span * 0.7f);
            }
            else
            {
                float lx = hx1 > hx0 ? hx0 - earPad : 0f;
                float rx = hx1 > hx0 ? hx1 + earPad : w - 1;
                boxCx = (lx + rx) * 0.5f;
                boxHalfX = (rx - lx) * 0.5f;
                // Upwards the head runs to the top of the figure and beyond;
                // there is nothing above her to feather into.
                boxCy = (headFrom + bottom + feather) * 0.5f;
                boxHalfY = (bottom + feather - headFrom) * 0.5f;
                margin = feather;
            }

            var dst = new Color32[px.Length];
            Array.Copy(px, dst, px.Length);
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
                if (keep >= 0.999f) continue;

                // Lift to the fur around the bands, then put the line back — as
                // much of it as the relief underneath asks for.
                float here = light[i];
                float line = Mathf.Clamp01((relief[i] - ReliefFloor)
                                           / (ReliefFull - ReliefFloor));
                float want = smooth[i] - line * (fine[i] - here);
                if (want > smooth[i]) want = smooth[i];
                if (want <= here + 0.004f) continue;
                if (keep > 0f) want = Mathf.Lerp(want, here, keep);

                // Scaled, not replaced: the hue of the fur is in the ratio
                // between the channels, and setting a lightness would grey it.
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

        private static float[] MaskOf(Dictionary<string, float[]> computed,
                                      string baseName, string maskName)
        {
            var drawn = Resources.Load<Texture2D>($"Art/{baseName}_{maskName}");
            if (drawn != null)
            {
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
        /// <param name="drawn">The silhouette the artist delivered, before the
        /// tufts were grown on it. The rim is a dilation of this and not of the
        /// finished alpha, so a strand of matted fur no longer drags an ink blob
        /// out with it — see <see cref="Build"/>.</param>
        private static Color32[] Outline(Color32[] px, int w, int h, int width,
                                         bool[] drawn)
        {
            if (width <= 0) return px;
            var dst = new Color32[px.Length];
            Array.Copy(px, dst, px.Length);

            for (int y = 0; y < h; y++)
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
            return dst;
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
