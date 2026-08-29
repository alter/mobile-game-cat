using System;
using System.Collections.Generic;
using System.Linq;

namespace CatShelter.Core
{
    /// <summary>
    /// The six base colours the game can draw, as sRGB anchors, in the one
    /// place both the colour estimate and the coat reader can see them.
    ///
    /// <para>They used to live in <c>Shell/CatColour.cs</c> as a private array
    /// and in <c>Plugins/iOS/CatColour.swift</c> as a second one, and that
    /// file's own comment says what was wrong with it: the six NAMES were
    /// guarded by <c>CatColourPaletteParityTests</c> and the six NUMBERS were
    /// guarded by nothing, "because they are numbers, not names, and a wrong
    /// one is a worse guess rather than an exception". Moving them here does
    /// not fix that by itself — Swift still holds a copy — but it puts the
    /// managed copy somewhere a test compiled by <c>core-tests</c> can read,
    /// so the parity test now compares the values too and a drifted anchor
    /// fails the suite instead of quietly misnaming cats.</para>
    ///
    /// <para>The values are unchanged, and the reasoning behind them is in
    /// <c>CatColour.swift</c>: measured rather than chosen, deliberately dull,
    /// scored against a labelled set. <c>white</c> and <c>cream</c> are
    /// physical rather than measured because the set held one white cat and no
    /// cream one.</para>
    /// </summary>
    public static class CoatPalette
    {
        public readonly struct Anchor
        {
            public readonly string Name;
            public readonly double R, G, B;

            public Anchor(string name, double r, double g, double b)
            {
                Name = name;
                R = r;
                G = g;
                B = b;
            }
        }

        public static readonly Anchor[] Entries =
        {
            new Anchor("ginger", 0.75, 0.59, 0.42),
            new Anchor("grey", 0.45, 0.43, 0.41),
            new Anchor("black", 0.26, 0.21, 0.20),
            new Anchor("white", 0.85, 0.84, 0.82),
            new Anchor("cream", 0.78, 0.72, 0.60),
            new Anchor("brown", 0.50, 0.43, 0.38),
        };

        /// <summary>
        /// Index of the nearest anchor by plain squared distance. Weighting
        /// lightness was tried at 1x, 2x and 4x when this lived in
        /// <c>CatColour</c> and made it worse every time, so it is not here
        /// either.
        /// </summary>
        public static int NearestIndex(double r, double g, double b)
        {
            var best = -1;
            var bestScore = double.MaxValue;
            for (var i = 0; i < Entries.Length; i++)
            {
                var e = Entries[i];
                var score = (r - e.R) * (r - e.R)
                          + (g - e.G) * (g - e.G)
                          + (b - e.B) * (b - e.B);
                if (score >= bestScore) continue;
                bestScore = score;
                best = i;
            }
            return best;
        }

        /// <summary>
        /// The nearest anchor's name, or null when it is not one
        /// <see cref="CatTraits"/> would accept.
        ///
        /// The null is the same refusal <c>CatColour.Nearest</c> made: a name
        /// only the palette knows about makes <c>CatTraits.FromColourOnly</c>
        /// throw, and the two lists sit in one assembly here, so the check is
        /// real rather than textual.
        /// </summary>
        public static string Nearest(double r, double g, double b)
        {
            var index = NearestIndex(r, g, b);
            if (index < 0) return null;
            var name = Entries[index].Name;
            return CatTraits.Allowed["base_color"].Contains(name) ? name : null;
        }

        /// <summary>The six names, in palette order.</summary>
        public static IReadOnlyList<string> Names =>
            Entries.Select(e => e.Name).ToArray();
    }
}
