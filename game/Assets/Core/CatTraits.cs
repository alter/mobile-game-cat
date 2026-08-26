using System;
using System.Collections.Generic;
using System.Linq;

namespace CatShelter.Core
{
    /// <summary>
    /// What the game knows about a cat's coat. Five fields, all enumerable,
    /// because the sprite is assembled from them:
    /// silhouette(fur_length) + fill(base_color) + pattern mask + markings +
    /// eyes (cat-shelter-tech.md section 3).
    ///
    /// Values are plain strings rather than enums so that one list —
    /// tools/traits/schema.json — stays the single definition shared with the
    /// Worker; <see cref="Allowed"/> mirrors it and a test compares the two.
    /// </summary>
    public sealed class CatTraits
    {
        public string BaseColor { get; }
        public string Pattern { get; }
        public string FurLength { get; }
        public string EyeColor { get; }
        public IReadOnlyList<string> WhiteMarkings { get; }

        /// <summary>How this cat's traits were arrived at. Not part of the coat,
        /// but the difference matters to what the player is told.</summary>
        public TraitsOrigin Origin { get; }

        public static readonly IReadOnlyDictionary<string, string[]> Allowed =
            new Dictionary<string, string[]>
            {
                ["base_color"] = new[] { "ginger", "grey", "black", "white", "cream", "brown" },
                ["pattern"] = new[] { "solid", "tabby", "bicolor", "calico", "tuxedo", "pointed" },
                ["fur_length"] = new[] { "short", "long" },
                ["eye_color"] = new[] { "green", "amber", "blue" },
                ["white_markings"] = new[] { "chest", "paws", "face" },
            };

        public CatTraits(string baseColor, string pattern, string furLength,
                         string eyeColor, IReadOnlyList<string> whiteMarkings,
                         TraitsOrigin origin = TraitsOrigin.Photo)
        {
            BaseColor = Check("base_color", baseColor);
            Pattern = Check("pattern", pattern);
            FurLength = Check("fur_length", furLength);
            EyeColor = Check("eye_color", eyeColor);

            var markings = whiteMarkings ?? Array.Empty<string>();
            foreach (var marking in markings) Check("white_markings", marking);
            if (markings.Distinct().Count() != markings.Count)
                throw new ArgumentException("repeated white markings", nameof(whiteMarkings));
            WhiteMarkings = markings.ToArray();
            Origin = origin;
        }

        private static string Check(string field, string value)
        {
            if (value == null || !Allowed[field].Contains(value))
                throw new ArgumentException(
                    $"{field}: '{value}' is not one of {string.Join(", ", Allowed[field])}");
            return value;
        }

        /// <summary>
        /// The cat a player gets when she skips the photo (task 6.10).
        ///
        /// Deliberately a plain grey short-haired tabby with green eyes: the
        /// most ordinary cat there is, so nobody feels they were given a
        /// consolation prize, and so the skipped path is visibly the same game
        /// rather than a lesser one. Fixed, never random — two players who skip
        /// must be able to talk about the same cat.
        /// </summary>
        public static CatTraits Default => new CatTraits(
            "grey", "tabby", "short", "green", Array.Empty<string>(),
            TraitsOrigin.Skipped);

        /// <summary>
        /// A cat built when the Worker could not be reached (task 6.11).
        /// Only the base colour is real — read from the photo on device — and
        /// everything else takes the default, because no on-device API reads a
        /// coat pattern (knowledge/ios/06-on-device-coat-traits.md).
        /// </summary>
        public static CatTraits FromColourOnly(string baseColor) => new CatTraits(
            baseColor, "solid", "short", "green", Array.Empty<string>(),
            TraitsOrigin.OfflineColourOnly);

        public override string ToString() =>
            $"{FurLength} {BaseColor} {Pattern}, {EyeColor} eyes" +
            (WhiteMarkings.Count > 0 ? $", white {string.Join("/", WhiteMarkings)}" : "");
    }

    public enum TraitsOrigin
    {
        /// <summary>Read from the player's photograph by the model.</summary>
        Photo,
        /// <summary>The player skipped the photo.</summary>
        Skipped,
        /// <summary>The Worker was unreachable; base colour read on device.</summary>
        OfflineColourOnly,
    }
}
