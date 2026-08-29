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

        /// <summary>
        /// Where the face must not be flattened — as distinct from
        /// <see cref="Eyes"/>, which is where it may be RECOLOURED.
        ///
        /// Two jobs were sharing one mask and they want opposite things. Tinting
        /// an eye green demands precision: a mask that strays onto an eyelid
        /// paints a green streak across the cat's face, so <see cref="Eyes"/> is
        /// deliberately strict and stays empty rather than guess. Protecting an
        /// eye from the debander demands the opposite: over-reaching costs a
        /// little texture on a cheek, which nobody will ever notice, while
        /// under-reaching costs the cat her eyes, which is the one thing every
        /// player looks at first.
        ///
        /// Holding both to the strict standard is what produced the regression
        /// this exists to close. On 2026-08-29 the eye-anchored protection in
        /// CoatBuilder.Deband was written on the belief that the detector had
        /// been fixed to fire at 256; it had not. Measured afterwards, across
        /// every one of the three shipped poses and at both 256 and 512,
        /// <see cref="FindEyes"/> returns an EMPTY mask — it has never once
        /// fired on the delivered art. So the eye protection was dead code, and
        /// every pose fell through to a fallback band placed at a fixed fraction
        /// of the figure's height. That band happens to land on the eyes of the
        /// standing cat and above the eyes of the sitting one, which is the
        /// whole of why state 1 lost her eyes and state 2 did not.
        /// </summary>
        public const string EyeGuard = "eye_guard";
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
            masks[EyeGuard] = FindEyeGuard(body, lum, w, h, top, bottom);

            FindEyeCentre(eyes, w, out float eyeY, out float eyeX, out bool haveEyes);
            // Head box: the upper 42% of the figure.
            int headBottom = bottom - Mathf.RoundToInt(height * 0.42f);
            if (!haveEyes)
            {
                // The middle of the HEAD, not of the figure.
                //
                // `(left + right) * 0.5f` stood here and it is the centre of the
                // whole silhouette — tail included. The cat's head is at one end
                // and her tail at the other, so that point lands on her flank,
                // and every marking placed from it went there too: on
                // 2026-08-29 a black tuxedo cat came out with her white bib on
                // her side and her white muzzle on her cheek. Invisible until
                // the stripes came off, and wrong the whole time.
                //
                // The head is the top 30% of the figure, and its own horizontal
                // extent is the only honest place to centre a face on.
                int headBand = bottom - Mathf.RoundToInt(height * 0.30f);
                int hl = w, hr = -1;
                for (int y = headBand; y <= bottom; y++)
                    for (int x = 0; x < w; x++)
                        if (body[y * w + x])
                        {
                            if (x < hl) hl = x;
                            if (x > hr) hr = x;
                        }

                eyeY = bottom - height * 0.20f;
                eyeX = hr > hl ? (hl + hr) * 0.5f : (left + right) * 0.5f;
            }

            // Blur halved on 2026-08-29. It was chosen when the drawn stripes
            // ran over everything and a marking only had to suggest itself
            // through them; with the stripes coming off a non-tabby cat
            // (CoatBuilder.Deband) the same softness reads as a smudge floating
            // on the shoulder rather than as a white bib. A marking on a real
            // cat has an edge — a soft one, not none.
            masks[MarkFace] = Blur(Ellipse(w, h, eyeY - height * 0.10f, eyeX,
                                           height * 0.11f, width * 0.11f), w, h, 5, body);
            masks[MarkChest] = Blur(Ellipse(w, h, headBottom - height * 0.10f, eyeX,
                                            height * 0.13f, width * 0.10f), w, h, 6, body);

            var paws = new float[px.Length];
            int pawTop = top + Mathf.RoundToInt(height * 0.14f);
            for (int i = 0; i < px.Length; i++)
                if (body[i] && i / w < pawTop) paws[i] = 1f;
            masks[MarkPaws] = Blur(paws, w, h, 4, body);

            masks[PatternPointed] = Pointed(body, w, h, top, bottom, left, right);
            masks[PatternBicolor] = Blur(Patches(body, w, h, 4, 0.52f, 10, seed), w, h, 8, body);
            masks[PatternCalico] = Blur(Patches(body, w, h, 7, 0.55f, 6, seed + 1), w, h, 5, body);
            // Not computed any more. The 28.08 cat delivery draws real tabby
            // markings into the silhouette — rings round the legs and tail, an M
            // on the forehead, stripes leaving the spine and curving over the
            // ribs, none on the chest or belly — and the delivery note says in
            // as many words that the computed longitudinal hatching can now be
            // removed.
            //
            // It had to go regardless. The owner's verdict on seeing it running
            // was that no cat has straight vertical bands from ear tip to paw
            // and that it looked painted on with a brush. He was right: these
            // stripes followed the frame, not the animal. Leaving them would now
            // double the drawn ones.
            //
            // An empty mask, not a missing key: `MaskOf` still asks for it, and
            // a hand-drawn `Art/cat_N_pattern_tabby` file still overrides it the
            // moment one exists.
            masks[PatternTabby] = new float[w * h];

            var tuxedo = new float[px.Length];
            for (int i = 0; i < px.Length; i++)
                tuxedo[i] = Mathf.Clamp01(masks[MarkChest][i] + masks[MarkPaws][i]);
            masks[PatternTuxedo] = tuxedo;

            return masks;
        }

        // ---------------------------------------------------------------
        // Distinctive marks
        // ---------------------------------------------------------------

        /// <summary>
        /// A shapeless patch at one named place on the cat.
        ///
        /// The one thing in this file that is about a particular cat rather than
        /// about cats. Everything else here answers "what kind of cat is this";
        /// this answers "which cat".
        ///
        /// Shapeless on purpose. Naming shapes — heart, star, ring — would ask
        /// the model to see something it mostly cannot and hand the player back
        /// an outline she never had. What the eye reads on a real cat is the
        /// PLACE: a white sock on one foot, a smudge on the chin, a patch over
        /// one eye. So the outline is an irregular closed curve with no meaning
        /// of its own, and the place carries everything.
        ///
        /// Irregular rather than round: a circle reads as a sticker. The radius
        /// is bent by three harmonics at a phase drawn from the seed, so two
        /// cats with a patch in the same place do not have the same patch.
        /// </summary>
        public static float[] Spot(bool[] body, int w, int h, float cy, float cx,
                                   float radius, int seed)
        {
            var m = new float[w * h];
            if (radius <= 1f) return m;

            var rng = new System.Random(seed);
            float p1 = (float)rng.NextDouble() * 6.2832f;
            float p2 = (float)rng.NextDouble() * 6.2832f;
            float p3 = (float)rng.NextDouble() * 6.2832f;

            int y0 = Mathf.Max(0, Mathf.FloorToInt(cy - radius * 1.6f));
            int y1 = Mathf.Min(h - 1, Mathf.CeilToInt(cy + radius * 1.6f));
            int x0 = Mathf.Max(0, Mathf.FloorToInt(cx - radius * 1.6f));
            int x1 = Mathf.Min(w - 1, Mathf.CeilToInt(cx + radius * 1.6f));

            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    int i = y * w + x;
                    if (!body[i]) continue;   // never off the cat

                    float dy = y - cy, dx = x - cx;
                    float d = Mathf.Sqrt(dy * dy + dx * dx);
                    float a = Mathf.Atan2(dy, dx);
                    float r = radius * (1f
                        + 0.20f * Mathf.Sin(a * 2f + p1)
                        + 0.13f * Mathf.Sin(a * 3f + p2)
                        + 0.07f * Mathf.Sin(a * 5f + p3));
                    if (d <= r) m[i] = 1f;
                }

            // A marking has an edge, but fur does not draw a hard one.
            return Blur(m, w, h, Mathf.Max(2, Mathf.RoundToInt(radius * 0.18f)), body);
        }

        /// <summary>
        /// Where each named place is on this silhouette, and how big a patch
        /// there should be — in pixels, for this picture.
        ///
        /// Everything is derived from the figure itself, so it holds for all
        /// three poses and for whatever is drawn next. Rows run bottom-up, so
        /// the head is the HIGH end and going down the cat is decreasing y.
        ///
        /// Returns false for a place this silhouette cannot offer — a tail the
        /// pose tucks away, say — and the caller simply does not draw it. A
        /// patch invented in the wrong place is worse than a patch missing.
        /// </summary>
        public static bool PlaceOf(string place, bool[] body, int w, int h,
                                   float[] eyes,
                                   out float cy, out float cx, out float radius)
        {
            cy = cx = radius = 0f;

            int top = h, bottom = -1, left = w, right = -1;
            for (int i = 0; i < body.Length; i++)
                if (body[i])
                {
                    int y = i / w, x = i % w;
                    if (y < top) top = y;
                    if (y > bottom) bottom = y;
                    if (x < left) left = x;
                    if (x > right) right = x;
                }
            if (bottom < 0) return false;

            float height = bottom - top, width = right - left;

            // The head's own horizontal extent, from the topmost band — her ears
            // are the highest thing on her whatever the pose, which a band of
            // rows across the whole figure is not.
            int earsFrom = bottom - Mathf.RoundToInt(height * 0.12f);
            int hx0 = w, hx1 = -1;
            for (int y = earsFrom; y <= bottom; y++)
                for (int x = 0; x < w; x++)
                    if (body[y * w + x]) { if (x < hx0) hx0 = x; if (x > hx1) hx1 = x; }
            float headX = hx1 > hx0 ? (hx0 + hx1) * 0.5f : (left + right) * 0.5f;
            float headW = hx1 > hx0 ? hx1 - hx0 : width * 0.5f;

            // The eyes when they were found: they place the face exactly.
            float eyeY = bottom - height * 0.18f, eyeL = headX - headW * 0.22f,
                  eyeR = headX + headW * 0.22f;
            FindEyeCentre(eyes, w, out float fy, out float fx, out bool haveEyes);
            if (haveEyes)
            {
                eyeY = fy;
                // Split the eye pixels at the centre to get one eye each side,
                // rather than assuming a symmetric face.
                float sumL = 0, nL = 0, sumR = 0, nR = 0;
                for (int i = 0; i < eyes.Length; i++)
                    if (eyes[i] > 0.5f)
                    {
                        float x = i % w;
                        if (x < fx) { sumL += x; nL++; } else { sumR += x; nR++; }
                    }
                if (nL > 0) eyeL = sumL / nL;
                if (nR > 0) eyeR = sumR / nR;
            }

            float small = height * 0.055f, medium = height * 0.085f;

            switch (place)
            {
                case "forehead": cy = eyeY + height * 0.07f; cx = headX; radius = small; break;
                case "eye_left": cy = eyeY; cx = eyeL; radius = small; break;
                case "eye_right": cy = eyeY; cx = eyeR; radius = small; break;
                case "muzzle": cy = eyeY - height * 0.075f; cx = headX; radius = small; break;
                case "chin": cy = eyeY - height * 0.135f; cx = headX; radius = small * 0.8f; break;
                case "chest": cy = bottom - height * 0.50f; cx = headX; radius = medium; break;
                case "flank": cy = bottom - height * 0.60f; cx = (left + right) * 0.5f; radius = medium; break;

                case "paw_left":
                case "paw_right":
                {
                    // The paws are the bottom of the figure; take the two
                    // extremes of that band and pick a side.
                    int pawTop = top + Mathf.RoundToInt(height * 0.10f);
                    int px0 = w, px1 = -1;
                    for (int y = top; y <= pawTop; y++)
                        for (int x = 0; x < w; x++)
                            if (body[y * w + x]) { if (x < px0) px0 = x; if (x > px1) px1 = x; }
                    if (px1 <= px0) return false;
                    cy = top + height * 0.05f;
                    cx = place == "paw_left" ? px0 + (px1 - px0) * 0.18f
                                             : px1 - (px1 - px0) * 0.18f;
                    radius = small * 0.85f;
                    break;
                }

                case "tail_tip":
                {
                    // The body pixel furthest from the head, in the lower half.
                    // On these silhouettes that is the tail, and when the pose
                    // hides it the answer is simply somewhere unremarkable — so
                    // it is rejected unless it is genuinely far out.
                    float bestD = 0f; int bestI = -1;
                    for (int i = 0; i < body.Length; i++)
                    {
                        if (!body[i]) continue;
                        int y = i / w, x = i % w;
                        if (y > bottom - height * 0.35f) continue;
                        float dy = y - bottom, dx = x - headX;
                        float d = dy * dy + dx * dx;
                        if (d > bestD) { bestD = d; bestI = i; }
                    }
                    if (bestI < 0) return false;
                    cy = bestI / w; cx = bestI % w; radius = small * 0.8f;
                    if (Mathf.Abs(cx - headX) < width * 0.20f) return false;
                    break;
                }

                default: return false;
            }
            return radius > 1f;
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

            // A share of the picture, not 150 pixels flat.
            //
            // 150 stood here until 2026-08-28 and made this whole pass depend on
            // the size it happened to be run at. At the board's 256 the eye blobs
            // come out under 150 pixels, so no pair was ever found and no eye was
            // ever coloured — the cats kept the dark eyes they were drawn with,
            // and that is every screenshot this project has taken. The moment the
            // cat card asked for 512 the same eyes were four times the area,
            // cleared 150 easily, and turned into flat green blobs on the one
            // screen a player would post. A rule about anatomy must not change
            // its answer because the texture got bigger.
            //
            // 0.23% of the picture is what 150 was at 256, so the game looks
            // exactly as it did — at every size.
            // 0.0023 of the picture until 2026-08-29, which was 150 pixels at
            // 256 — and the eyes on the shipped silhouettes come out just under
            // that at the size the board builds at. So the pair was found at 512
            // and not at 256, and anything anchored to the eyes worked on the
            // cat card and silently did nothing on the board. That cost a solid
            // cat her whole face when the debander asked where not to touch.
            //
            // 0.0011 is about 72 pixels at 256 and 288 at 512: below both eyes
            // at both sizes, and still far above the speckle the threshold is
            // there to reject. The pair test below — same height, comparable
            // size — is what actually keeps ears and stripes out, not this.
            var found = Blobs(dark, w, h, Mathf.Max(16, Mathf.RoundToInt(w * h * 0.0011f)));
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

        /// <summary>
        /// The dark features on the head that the debander must leave alone.
        ///
        /// The same search as <see cref="FindEyes"/> with both of its acceptance
        /// rules relaxed, because it is answering an easier question. FindEyes
        /// must be sure enough to hand Tint a set of pixels to paint green; this
        /// only has to be right about roughly where the face is.
        ///
        /// Both rules had to go, and the measurements are worth keeping because
        /// they say what the shipped art actually looks like. Taken at 256, the
        /// eye blobs are 48px and 36px on the sitting cat, 80px and 65px on the
        /// standing one and 37px and 36px on the lying one, while the largest
        /// thing that is not an eye is 10px. FindEyes' size floor is 0.0011 of
        /// the picture — 72px at 256 — so it rejected FIVE of those six blobs,
        /// and at 512 it rejects five of six again. That one constant is why the
        /// detector has never fired on this artwork at any size.
        ///
        /// The pairing rule fails separately and only on the sitting cat. Her
        /// head is tucked down and turned away, so the near eye is a wide almond
        /// and the far one a narrow slit thirteen rows higher — 6.6% of her
        /// height apart, against a tolerance of 6%. A rule about anatomy that
        /// assumes both eyes are at the same height assumes the cat is facing
        /// the camera, and one of the three shipped poses is not.
        ///
        /// 0.0004 of the picture is 26px at 256 and 105px at 512: below every
        /// eye at both sizes and comfortably above the speckle. Grouping by the
        /// largest blob's own row, rather than testing a pair for symmetry,
        /// drops an ear or a nose far above or below without demanding that the
        /// two eyes be level.
        /// </summary>
        private static float[] FindEyeGuard(bool[] body, float[] lum, int w, int h,
                                            int top, int bottom)
        {
            var result = new float[body.Length];
            int height = bottom - top;
            if (height <= 0) return result;

            var values = new List<float>();
            for (int i = 0; i < body.Length; i++) if (body[i]) values.Add(lum[i]);
            if (values.Count == 0) return result;
            values.Sort();
            float threshold = values[Mathf.Clamp((int)(values.Count * 0.06f), 0, values.Count - 1)];

            int bandBottom = bottom - Mathf.RoundToInt(height * 0.45f);
            var dark = new bool[body.Length];
            for (int i = 0; i < body.Length; i++)
                dark[i] = body[i] && lum[i] < threshold && i / w > bandBottom;

            var found = Blobs(dark, w, h, Mathf.Max(8, Mathf.RoundToInt(w * h * 0.0004f)));
            if (found.Count == 0) return result;

            // The largest dark thing on the head, and everything level with it.
            int biggest = 0;
            for (int i = 1; i < found.Count; i++)
                if (found[i].Count > found[biggest].Count) biggest = i;
            float row = MeanRow(found[biggest], w);

            foreach (var blob in found)
                if (Mathf.Abs(MeanRow(blob, w) - row) <= height * 0.12f)
                    foreach (var i in blob) result[i] = 1f;
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
