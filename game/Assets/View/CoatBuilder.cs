using System;
using System.Collections.Generic;
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

        /// <summary>Coat colours, from the six base_color values. Multiplied
        /// into the greyscale base, so its light and shadow survive.</summary>
        private static readonly Dictionary<string, Color> Coats = new()
        {
            ["ginger"] = new Color(0.87f, 0.55f, 0.29f),
            ["grey"] = new Color(0.60f, 0.62f, 0.65f),
            ["black"] = new Color(0.28f, 0.26f, 0.26f),
            ["white"] = new Color(0.96f, 0.94f, 0.90f),
            ["cream"] = new Color(0.90f, 0.82f, 0.66f),
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

            px = Reshape(px, w, h, Waist[state]);
            px = Weather(px, w, h, Neglect[state], seed: 5);
            px = Tint(px, traits);
            px = Outline(px, w, h, Mathf.RoundToInt(w * 0.016f));

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
        /// Pixels of a texture that was imported without Read/Write enabled —
        /// which every texture in this project is, and should stay: marking
        /// them readable keeps a second copy in memory for the whole run, for
        /// the sake of one pass at load.
        ///
        /// The blit goes through the GPU, so it works on any texture whatever
        /// the import settings, including anything delivered later.
        /// </summary>
        private static Color32[] ReadPixels(Texture2D source)
        {
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

        private static Color32[] Weather(Color32[] px, int w, int h, float s, int seed)
        {
            if (s <= 0f) return px;
            var rng = new System.Random(seed);

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
                    float tuft = (nv - 0.5f) * 2f * 13f * s;
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
        private static Color32[] Tint(Color32[] px, CatTraits traits)
        {
            var coat = Coats.TryGetValue(traits.BaseColor, out var c) ? c : Color.white;

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

                px[i] = new Color32(
                    (byte)Mathf.Clamp(coat.r * 255f * v * 1.35f, 0, 255),
                    (byte)Mathf.Clamp(coat.g * 255f * v * 1.35f, 0, 255),
                    (byte)Mathf.Clamp(coat.b * 255f * v * 1.35f, 0, 255),
                    px[i].a);
            }
            return px;
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
        private static Color32[] Outline(Color32[] px, int w, int h, int width)
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
                    if (px[i].a > 40) continue;

                    // Opaque pixel within `width`? Then this is rim.
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
                            if (px[sy * w + sx].a > 200) { near = true; break; }
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
    }
}
