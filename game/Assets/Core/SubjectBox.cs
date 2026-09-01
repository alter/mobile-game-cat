using System;

namespace CatShelter.Core
{
    /// <summary>
    /// Where the segmenter's mask says the animal is, as a box in PHOTOGRAPH
    /// PIXELS.
    ///
    /// This is the fallback that runs when the labeller named nothing: no label
    /// means no box from the labeller, and cropping the whole room is how a coat
    /// ends up read off the wallpaper. The mask still knows where she is even
    /// when nothing knows what she is.
    ///
    /// WHY IT IS IN CORE. It was eleven lines inside `CaptureScreen.SubjectBox`,
    /// and it shipped with the conversion missing entirely: the scan walks the
    /// MASK, so it produced mask indices — 0..511 at the most, because
    /// <c>CatVision.DefaultMaskSide</c> is 512 — and handed them to
    /// <c>CatPhoto.Prepare</c>, which reads a box as pixels of the file. The
    /// error is the mask's own shrink factor, so it grows with the photograph
    /// and is invisible on the small ones:
    ///
    /// <code>
    ///   960x1280  -> mask 384x512, factor 2.5   -> a near-miss
    ///   3000x4000 -> mask 384x512, factor 7.8   -> the top-left corner
    /// </code>
    ///
    /// Measured on the owner's own photograph, 2026-09-01: "cropping to the
    /// subject mask 277x470" on a 3000x4000 frame, cropped as a 470 px square
    /// out of the corner, and the coat came back `cream` for a brown cat
    /// because that square was wall.
    ///
    /// A rule that is wrong by a factor that depends on the input is exactly the
    /// kind that a test catches and an eye does not, and there was nowhere to
    /// put that test: `CaptureScreen` needs the engine, and the Core suite is
    /// the one that runs on every change. So the rule moved here, which is the
    /// same move <see cref="PhotoJudge"/> and <see cref="VisionAnswer"/> already
    /// made, for the same reason.
    /// </summary>
    public static class SubjectBox
    {
        /// <summary>
        /// Half confidence, the same threshold the coat reader uses, so the two
        /// agree about what "inside her" means.
        /// </summary>
        public const byte Inside = 128;

        /// <summary>
        /// A box covering essentially the whole frame has located NOTHING —
        /// that is the "cat merged with the room" case, and a box around
        /// everything is the crop we were already taking. Saying so honestly
        /// beats pretending to have narrowed it.
        /// </summary>
        public const float TooMuch = 0.95f;

        /// <summary>
        /// The extent of the mask, in the photograph's own pixels. A default
        /// <see cref="AnimalBox"/> means "nothing worth cropping to" and every
        /// caller already reads it as "use the whole image".
        /// </summary>
        public static AnimalBox Of(byte[] mask, int maskWidth, int maskHeight,
                                   int imageWidth, int imageHeight)
        {
            if (mask == null || maskWidth <= 0 || maskHeight <= 0) return default;
            if (mask.Length < maskWidth * maskHeight) return default;
            if (imageWidth <= 0 || imageHeight <= 0) return default;

            int left = maskWidth, right = -1, top = maskHeight, bottom = -1;
            for (int y = 0; y < maskHeight; y++)
            {
                int row = y * maskWidth;
                for (int x = 0; x < maskWidth; x++)
                {
                    if (mask[row + x] < Inside) continue;
                    if (x < left) left = x;
                    if (x > right) right = x;
                    if (y < top) top = y;
                    if (y > bottom) bottom = y;
                }
            }
            if (right < left || bottom < top) return default;

            float share = (right - left + 1f) * (bottom - top + 1f)
                          / ((float)maskWidth * maskHeight);
            if (share > TooMuch) return default;

            // Mask cells to pixels. A cell is a whole block of pixels here, so
            // the box is rounded OUTWARD: half a cell of wall costs less than
            // half a cell of cat.
            double across = imageWidth / (double)maskWidth;
            double down = imageHeight / (double)maskHeight;

            int x0 = Clamp((int)Math.Floor(left * across), 0, imageWidth - 1);
            int y0 = Clamp((int)Math.Floor(top * down), 0, imageHeight - 1);
            int x1 = Clamp((int)Math.Ceiling((right + 1) * across), x0 + 1, imageWidth);
            int y1 = Clamp((int)Math.Ceiling((bottom + 1) * down), y0 + 1, imageHeight);

            return new AnimalBox
            {
                identifier = "Cat",
                // Located, not identified, and the difference matters: nothing
                // reads this number, and a number here would invite something
                // to start.
                confidence = 0f,
                x = x0,
                y = y0,
                width = x1 - x0,
                height = y1 - y0,
            };
        }

        private static int Clamp(int value, int low, int high) =>
            value < low ? low : (value > high ? high : value);
    }
}
