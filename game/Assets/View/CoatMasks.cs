using System.Collections.Generic;
using UnityEngine;

namespace CatShelter.View
{
    /// <summary>
    /// The masks of 40-art/04, computed from the silhouette instead of drawn.
    ///
    /// That task asks for 54 hand-traced files and forbids generating them,
    /// for a good reason: a model asked for "the same cat but striped" draws a
    /// different cat, and a mask that is one pixel off its base is worse than
    /// no mask. Deriving them from the base has neither problem — the base IS
    /// the input, so alignment is exact by construction.
    ///
    /// What that buys and what it costs:
    ///
    ///   eyes      found, not drawn: a pair of dark blobs at the same height
    ///             on the head. Two of the three cats have their eyes shut, so
    ///             the mask is empty for them, which is correct — there is
    ///             nothing to colour.
    ///   pointed   distance from the body's centre. Ears, muzzle, paws and
    ///             tail are the far parts; that IS what pointed means.
    ///   paws      the bottom of the figure.
    ///   face      an ellipse around the muzzle, placed from the eyes.
    ///   chest     an ellipse below the head.
    ///   tuxedo    chest plus paws, which is what a tuxedo is.
    ///   bicolor   two or three large zones of value noise.
    ///   calico    smaller patches of the same.
    ///   tabby     near-vertical wavy stripes, denser along the back.
    ///
    /// The weak one is tabby: real tabby stripes follow the animal, and these
    /// follow the frame. It reads as a striped cat and not as a photograph of
    /// one. If it ever matters, three hand-drawn stripe masks replace it —
    /// <see cref="CoatBuilder"/> prefers a file over a computed mask wherever
    /// one exists, so drawing them is an improvement, never a rewrite.
    /// </summary>
    public static class CoatMasks
    {
        public const string Eyes = "eyes";
        public const string MarkChest = "mark_chest";
        public const string MarkPaws = "mark_paws";
        public const string MarkFace = "mark_face";
        public const string PatternTabby = "pattern_tabby";
        public const string PatternPointed = "pattern_pointed";
        public const string PatternBicolor = "pattern_bicolor";
        public const string PatternCalico = "pattern_calico";
        public const string PatternTuxedo = "pattern_tuxedo";

        /// <summary>Every mask for one silhouette, as coverage 0..1 per pixel.</summary>
        public static Dictionary<string, float[]> Build(Color32[] px, int w, int h, int seed)
        {
            var body = new bool[px.Length];
            var lum = new float[px.Length];
            int top = h, bottom = -1, left = w, right = -1;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    body[i] = px[i].a > 200;
                    lum[i] = (px[i].r + px[i].g + px[i].b) / 3f;
                    if (!body[i]) continue;
                    if (y < top) top = y;
                    if (y > bottom) bottom = y;
                    if (x < left) left = x;
                    if (x > right) right = x;
                }

            var masks = new Dictionary<string, float[]>();
            if (bottom < 0) return masks;

            // Rows run bottom-up in a texture, so "top of the cat" is the high
            // row index. Everything below is written in that space.
            int height = bottom - top, width = right - left;

            var eyes = FindEyes(body, lum, w, h, top, bottom);
            masks[Eyes] = eyes;

            FindEyeCentre(eyes, w, out float eyeY, out float eyeX, out bool haveEyes);
            // Head box: the upper 42% of the figure.
            int headBottom = bottom - Mathf.RoundToInt(height * 0.42f);
            if (!haveEyes)
            {
                eyeY = bottom - height * 0.20f;
                eyeX = (left + right) * 0.5f;
            }

            masks[MarkFace] = Blur(Ellipse(w, h, eyeY - height * 0.10f, eyeX,
                                           height * 0.11f, width * 0.11f), w, h, 10, body);
            masks[MarkChest] = Blur(Ellipse(w, h, headBottom - height * 0.10f, eyeX,
                                            height * 0.13f, width * 0.10f), w, h, 12, body);

            var paws = new float[px.Length];
            int pawTop = top + Mathf.RoundToInt(height * 0.14f);
            for (int i = 0; i < px.Length; i++)
                if (body[i] && i / w < pawTop) paws[i] = 1f;
            masks[MarkPaws] = Blur(paws, w, h, 4, body);

            masks[PatternPointed] = Pointed(body, w, h, top, bottom, left, right);
            masks[PatternBicolor] = Blur(Patches(body, w, h, 4, 0.52f, 10, seed), w, h, 8, body);
            masks[PatternCalico] = Blur(Patches(body, w, h, 7, 0.55f, 6, seed + 1), w, h, 5, body);
            masks[PatternTabby] = Tabby(body, w, h, top, bottom, left, right, seed + 2);

            var tuxedo = new float[px.Length];
            for (int i = 0; i < px.Length; i++)
                tuxedo[i] = Mathf.Clamp01(masks[MarkChest][i] + masks[MarkPaws][i]);
            masks[PatternTuxedo] = tuxedo;

            return masks;
        }

        // ---------------------------------------------------------------
        // Eyes: a pair of dark blobs at the same height on the head
        // ---------------------------------------------------------------

        private static float[] FindEyes(bool[] body, float[] lum, int w, int h,
                                        int top, int bottom)
        {
            var result = new float[body.Length];
            int height = bottom - top;

            // Threshold at the 6th percentile of the coat's own lightness, so
            // it adapts to a light cat and a dark one alike.
            var values = new List<float>();
            for (int i = 0; i < body.Length; i++) if (body[i]) values.Add(lum[i]);
            if (values.Count == 0) return result;
            values.Sort();
            float threshold = values[Mathf.Clamp((int)(values.Count * 0.06f), 0, values.Count - 1)];

            // Head band only: the upper 45%.
            int bandBottom = bottom - Mathf.RoundToInt(height * 0.45f);
            var dark = new bool[body.Length];
            for (int i = 0; i < body.Length; i++)
                dark[i] = body[i] && lum[i] < threshold && i / w > bandBottom;

            var found = Blobs(dark, w, h, 150);
            if (found.Count < 2) return result;   // eyes shut: nothing to colour

            // The pair at the same height and of comparable size. Without this
            // the ears win — they are darker inside than a half-closed eye,
            // which is exactly what the first attempt selected.
            List<int> bestA = null, bestB = null; int bestScore = 0;
            for (int i = 0; i < found.Count; i++)
                for (int j = i + 1; j < found.Count; j++)
                {
                    float ya = MeanRow(found[i], w), yb = MeanRow(found[j], w);
                    int sa = found[i].Count, sb = found[j].Count;
                    if (Mathf.Abs(ya - yb) > height * 0.06f) continue;
                    if (Mathf.Max(sa, sb) / (float)Mathf.Min(sa, sb) > 2.2f) continue;
                    if (sa + sb <= bestScore) continue;
                    bestScore = sa + sb; bestA = found[i]; bestB = found[j];
                }

            if (bestA == null) return result;
            foreach (var i in bestA) result[i] = 1f;
            foreach (var i in bestB) result[i] = 1f;
            return result;
        }

        private static float MeanRow(List<int> blob, int w)
        {
            long sum = 0;
            foreach (var i in blob) sum += i / w;
            return (float)sum / blob.Count;
        }

        private static void FindEyeCentre(float[] eyes, int w,
                                          out float y, out float x, out bool any)
        {
            double sy = 0, sx = 0; int n = 0;
            for (int i = 0; i < eyes.Length; i++)
                if (eyes[i] > 0.5f) { sy += i / w; sx += i % w; n++; }
            any = n > 0;
            y = any ? (float)(sy / n) : 0f;
            x = any ? (float)(sx / n) : 0f;
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

        // ---------------------------------------------------------------
        // Shapes
        // ---------------------------------------------------------------

        private static float[] Ellipse(int w, int h, float cy, float cx, float ry, float rx)
        {
            var m = new float[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float dy = (y - cy) / Mathf.Max(ry, 1f), dx = (x - cx) / Mathf.Max(rx, 1f);
                    if (dy * dy + dx * dx < 1f) m[y * w + x] = 1f;
                }
            return m;
        }

        /// <summary>The far parts of the body: ears, muzzle, paws, tail.</summary>
        private static float[] Pointed(bool[] body, int w, int h,
                                       int top, int bottom, int left, int right)
        {
            double sy = 0, sx = 0; int n = 0;
            for (int i = 0; i < body.Length; i++)
                if (body[i]) { sy += i / w; sx += i % w; n++; }
            if (n == 0) return new float[body.Length];
            float cy = (float)(sy / n), cx = (float)(sx / n);
            float height = Mathf.Max(1, bottom - top), width = Mathf.Max(1, right - left);

            var dist = new float[body.Length];
            var sorted = new List<float>();
            for (int i = 0; i < body.Length; i++)
            {
                if (!body[i]) continue;
                float dy = ((i / w) - cy) / height, dx = ((i % w) - cx) / width;
                dist[i] = Mathf.Sqrt(dy * dy + dx * dx);
                sorted.Add(dist[i]);
            }
            sorted.Sort();
            float cut = sorted[Mathf.Clamp((int)(sorted.Count * 0.66f), 0, sorted.Count - 1)];

            var m = new float[body.Length];
            for (int i = 0; i < body.Length; i++)
                if (body[i] && dist[i] > cut) m[i] = 1f;
            return Blur(m, w, h, 10, body);
        }

        /// <summary>
        /// Patches of value noise. <paramref name="cells"/> is how many patches
        /// fit across the frame — fewer means larger. Bicolor is a handful of
        /// big zones, calico is more and smaller; getting this backwards turns
        /// both into freckles, which is what the first attempt produced.
        /// </summary>
        private static float[] Patches(bool[] body, int w, int h,
                                       int cells, float threshold, int softness, int seed)
        {
            var rng = new System.Random(seed);
            var grid = new float[cells * cells];
            for (int i = 0; i < grid.Length; i++) grid[i] = (float)rng.NextDouble();

            var m = new float[body.Length];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    if (!body[i]) continue;
                    float gx = x * (cells - 1f) / w, gy = y * (cells - 1f) / h;
                    if (Bilinear(grid, cells, gx, gy) > threshold) m[i] = 1f;
                }
            return Blur(m, w, h, softness, body);
        }

        private static float[] Tabby(bool[] body, int w, int h,
                                     int top, int bottom, int left, int right, int seed)
        {
            var rng = new System.Random(seed);
            const int cells = 8;
            var wob = new float[cells * cells];
            for (int i = 0; i < wob.Length; i++) wob[i] = (float)rng.NextDouble() - 0.5f;

            float width = Mathf.Max(1, right - left), height = Mathf.Max(1, bottom - top);
            float period = width * 0.075f;

            var m = new float[body.Length];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    if (!body[i]) continue;
                    float wobble = Bilinear(wob, cells, x * (cells - 1f) / w, y * (cells - 1f) / h);
                    float phase = (x + wobble * width * 0.05f) / period;
                    if (Mathf.Sin(phase * Mathf.PI) <= 0.45f) continue;
                    // Denser along the back, fading towards the belly.
                    float fromTop = (bottom - y) / height;
                    m[i] = Mathf.Clamp01(1f - (fromTop - 0.10f) / 0.85f);
                }
            return Blur(m, w, h, 2, body);
        }

        private static float Bilinear(float[] grid, int cells, float gx, float gy)
        {
            int x0 = Mathf.Clamp(Mathf.FloorToInt(gx), 0, cells - 1);
            int y0 = Mathf.Clamp(Mathf.FloorToInt(gy), 0, cells - 1);
            int x1 = Mathf.Min(x0 + 1, cells - 1), y1 = Mathf.Min(y0 + 1, cells - 1);
            float fx = gx - x0, fy = gy - y0;
            float a = Mathf.Lerp(grid[y0 * cells + x0], grid[y0 * cells + x1], fx);
            float b = Mathf.Lerp(grid[y1 * cells + x0], grid[y1 * cells + x1], fx);
            return Mathf.Lerp(a, b, fy);
        }

        /// <summary>
        /// Three box passes, which is close enough to a gaussian and far
        /// cheaper. Clipped to the body afterwards: a mask that bleeds past the
        /// silhouette paints the background, and 40-art/04's own check is that
        /// no mask extends beyond the base's alpha.
        /// </summary>
        private static float[] Blur(float[] src, int w, int h, int radius, bool[] body)
        {
            var a = src; var b = new float[src.Length];
            for (int pass = 0; pass < 3; pass++)
            {
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        float sum = 0; int n = 0;
                        for (int d = -radius; d <= radius; d++)
                        {
                            int sx = x + d;
                            if (sx < 0 || sx >= w) continue;
                            sum += a[y * w + sx]; n++;
                        }
                        b[y * w + x] = n > 0 ? sum / n : 0f;
                    }
                for (int x = 0; x < w; x++)
                    for (int y = 0; y < h; y++)
                    {
                        float sum = 0; int n = 0;
                        for (int d = -radius; d <= radius; d++)
                        {
                            int sy = y + d;
                            if (sy < 0 || sy >= h) continue;
                            sum += b[sy * w + x]; n++;
                        }
                        a[y * w + x] = n > 0 ? sum / n : 0f;
                    }
            }
            for (int i = 0; i < a.Length; i++) if (!body[i]) a[i] = 0f;
            return a;
        }
    }
}
