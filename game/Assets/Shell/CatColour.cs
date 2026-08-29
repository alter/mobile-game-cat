using System;
using System.Linq;
using System.Runtime.InteropServices;
using CatShelter.Core;
using UnityEngine;

namespace CatShelter.Shell
{
    /// <summary>
    /// Task 50-photo/11: the one trait a phone can read without the Worker.
    /// Wraps Assets/Plugins/iOS/CatColour.swift on iOS; everywhere else the
    /// same estimate is made here, in managed code.
    ///
    /// Base colour only. Nothing on device reads a coat pattern — Apple's
    /// classifier has 1303 categories and not one is a tabby
    /// (knowledge/ios/06-on-device-coat-traits.md) — so the caller forces
    /// pattern=solid rather than guessing at one.
    ///
    /// ── Why there is no third copy of the palette ────────────────────────
    ///
    /// The six colour names already live in two places: Swift's
    /// <c>CatColour.palette</c> and <see cref="CatTraits.Allowed"/>
    /// ["base_color"]. <c>CatColourPaletteParityTests</c> exists precisely
    /// because a name only one side knows about makes
    /// <c>CatTraits.FromColourOnly</c> throw. An Android copy in Java would
    /// have made it three, with nothing able to check the third: a test that
    /// greps a .java file is still only grepping, and the names would remain a
    /// literal list compared against nothing at compile time.
    ///
    /// So Android gets no palette of its own — there is no CatColour.java. The
    /// estimate is made here, from the prepared 512×512 JPEG, with
    /// <see cref="ImageConversion.LoadImage"/> and a mean over the centre: the
    /// same measurement CIAreaAverage makes on iOS, over the same region,
    /// against the same anchors. The winning name is then checked against
    /// <see cref="CatTraits.Allowed"/> before it is returned, so drift cannot
    /// reach <c>FromColourOnly</c> at all — it comes back as null and she gets
    /// the default cat, which is what null already meant here.
    ///
    /// What is still duplicated is the six ANCHORS, Swift's and the ones
    /// below, and nothing can check those: they are numbers, not names, and a
    /// wrong one is a worse guess rather than an exception. See
    /// tasks/50-photo/NOTES-android-capture.md.
    /// </summary>
    public static class CatColour
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern IntPtr CatColour_estimate(byte[] bytes, int length);

        [DllImport("__Internal")]
        private static extern void CatColour_free(IntPtr text);
#endif

        /// <summary>The base colour, or null when it cannot be read.</summary>
        public static string Estimate(byte[] croppedJpeg)
        {
            if (croppedJpeg == null || croppedJpeg.Length == 0) return null;
#if UNITY_IOS && !UNITY_EDITOR
            var pointer = CatColour_estimate(croppedJpeg, croppedJpeg.Length);
            if (pointer == IntPtr.Zero) return null;
            try { return Marshal.PtrToStringAnsi(pointer); }
            finally { CatColour_free(pointer); }
#else
            return EstimateHere(croppedJpeg);
#endif
        }

        /// <summary>
        /// The six base colours the game can draw, as sRGB anchors. Copied
        /// value for value from <c>Plugins/iOS/CatColour.swift</c>, whose
        /// comment is the one that matters and is not repeated here: they are
        /// measured rather than chosen, deliberately dull, and were arrived at
        /// by scoring against a labelled set
        /// (50-photo/11-offline-fallback/ground-truth.txt). Changing one of
        /// these without changing the other side means two phones disagreeing
        /// about the same cat.
        /// </summary>
        private static readonly (string Name, double R, double G, double B)[] Palette =
        {
            ("ginger", 0.75, 0.59, 0.42),
            ("grey",   0.45, 0.43, 0.41),
            ("black",  0.26, 0.21, 0.20),
            ("white",  0.85, 0.84, 0.82),
            ("cream",  0.78, 0.72, 0.60),
            ("brown",  0.50, 0.43, 0.38),
        };

        /// <summary>
        /// Mean colour of the centre of the crop, matched to the palette.
        ///
        /// The centre, not the whole frame, because the crop already put the
        /// cat in the middle and the edges are carpet and sofa. The centre
        /// half in each dimension, which is Swift's
        /// <c>insetBy(dx: width * 0.25, dy: height * 0.25)</c>.
        ///
        /// A mean, not k-means: clustering was tried and scored WORSE on the
        /// labelled set — 41% against 48% — because the largest cluster of a
        /// tabby is its stripes, not its coat.
        ///
        /// <see cref="Texture2D.GetPixels32"/> hands back the stored bytes with
        /// no colour conversion, and this project renders in Gamma
        /// (ProjectSettings m_ActiveColorSpace: 0), so these are sRGB values —
        /// the same thing CIAreaAverage produces with a null working colour
        /// space. The anchors are sRGB, so the comparison is like for like.
        /// </summary>
        private static string EstimateHere(byte[] jpeg)
        {
            Texture2D texture = null;
            try
            {
                // linear:false — the bytes are sRGB and must stay that way;
                // mipChain:false because only one level is ever read.
                texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
                if (!texture.LoadImage(jpeg, markNonReadable: false))
                {
                    Debug.LogWarning("[CatColour] could not decode the crop");
                    return null;
                }

                int width = texture.width, height = texture.height;
                if (width <= 0 || height <= 0) return null;

                int left = width / 4, right = width - width / 4;
                int bottom = height / 4, top = height - height / 4;
                var pixels = texture.GetPixels32();

                long red = 0, green = 0, blue = 0, counted = 0;
                for (int y = bottom; y < top; y++)
                {
                    int row = y * width;
                    for (int x = left; x < right; x++)
                    {
                        var pixel = pixels[row + x];
                        red += pixel.r;
                        green += pixel.g;
                        blue += pixel.b;
                        counted++;
                    }
                }
                if (counted == 0) return null;

                double scale = 255.0 * counted;
                var name = Nearest(red / scale, green / scale, blue / scale);
                Debug.Log("[CatColour] centre mean rgb " +
                          $"({red / scale:F3}, {green / scale:F3}, {blue / scale:F3}) " +
                          $"over {counted} px of {width}×{height} -> {name ?? "nothing"}");
                return name;
            }
            catch (Exception e)
            {
                // A colour that cannot be read is not an error screen: the
                // caller falls back to the default cat, and the photograph
                // itself was fine.
                Debug.LogWarning($"[CatColour] estimate failed: {e.Message}");
                return null;
            }
            finally
            {
                if (texture != null)
                {
                    if (Application.isPlaying) UnityEngine.Object.Destroy(texture);
                    else UnityEngine.Object.DestroyImmediate(texture);
                }
            }
        }

        /// <summary>
        /// Nearest palette entry by plain squared distance. Weighting
        /// lightness was tried at 1x, 2x and 4x and made it worse every time
        /// (52%, 44%, 44% against 56%), so it is not there.
        ///
        /// Returns null rather than a name <see cref="CatTraits"/> does not
        /// know. That is the check the Swift side cannot make and a test on a
        /// third language could not make either: here the two lists sit in the
        /// same assembly, so the comparison is real rather than textual.
        /// </summary>
        private static string Nearest(double r, double g, double b)
        {
            string best = null;
            var bestScore = double.MaxValue;
            foreach (var entry in Palette)
            {
                var score = (r - entry.R) * (r - entry.R)
                          + (g - entry.G) * (g - entry.G)
                          + (b - entry.B) * (b - entry.B);
                if (score >= bestScore) continue;
                bestScore = score;
                best = entry.Name;
            }
            if (best != null && !CatTraits.Allowed["base_color"].Contains(best))
            {
                // Never seen, and it must stay that way: this is the drift
                // CatColourPaletteParityTests guards for on the Swift side,
                // caught here before it can reach CatTraits.FromColourOnly.
                Debug.LogError($"[CatColour] '{best}' is not one of " +
                               "CatTraits.Allowed[\"base_color\"] — the palette " +
                               "and the trait table have drifted apart.");
                return null;
            }
            return best;
        }
    }
}
