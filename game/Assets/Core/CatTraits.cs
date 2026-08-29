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

        /// <summary>
        /// Her distinctive marks — the only fields here that are not also true
        /// of thousands of other cats. See <see cref="CatSpot"/> for why they
        /// were added and what the other five cannot do.
        ///
        /// At most two. A cat with a list of marks is a cat nobody looked at
        /// properly: the model is asked for what stands out, and three things
        /// standing out means nothing does.
        /// </summary>
        public IReadOnlyList<CatSpot> Spots { get; }

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
                // Places a mark can be, and every one of them is somewhere the
                // silhouette can be asked about: the eyes are found, the paws
                // are the bottom of the figure, the head is the top, the tail
                // is the far end of the body from the head. Left and right are
                // separate on purpose — a patch on one paw is the whole point,
                // and a rule that could only say "paws" would throw away the
                // asymmetry that does the identifying.
                ["spot_place"] = new[]
                {
                    "muzzle", "forehead", "eye_left", "eye_right", "chin",
                    "chest", "paw_left", "paw_right", "flank", "tail_tip",
                },
                ["spot_shade"] = new[] { "light", "dark" },
            };

        public CatTraits(string baseColor, string pattern, string furLength,
                         string eyeColor, IReadOnlyList<string> whiteMarkings,
                         TraitsOrigin origin = TraitsOrigin.Photo,
                         IReadOnlyList<CatSpot> spots = null)
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

            var marks = spots ?? Array.Empty<CatSpot>();
            if (marks.Count > MaxSpots)
                throw new ArgumentException(
                    $"at most {MaxSpots} spots, got {marks.Count}", nameof(spots));
            // Two marks in the same place is one mark described twice, and it
            // would paint the same patch over itself.
            if (marks.Select(s => s.Place).Distinct().Count() != marks.Count)
                throw new ArgumentException("two spots in the same place", nameof(spots));
            Spots = marks.ToArray();

            Origin = origin;
        }

        /// <summary>At most this many marks; see <see cref="Spots"/>.</summary>
        public const int MaxSpots = 2;

        /// <summary>Public so <see cref="CatSpot"/> can use the one table.</summary>
        internal static string CheckValue(string field, string value) => Check(field, value);

        /// <summary>
        /// The same cat with her distinctive marks filled in.
        ///
        /// Separate from the constructor because the two halves arrive from
        /// different places and at different times: the class traits come from
        /// the model or from the phone's own colour estimate, and the marks are
        /// measured on the device from the same photograph. Neither waits for
        /// the other, and this is where they meet.
        /// </summary>
        public CatTraits WithSpots(IReadOnlyList<CatSpot> spots) =>
            new CatTraits(BaseColor, Pattern, FurLength, EyeColor, WhiteMarkings,
                          Origin, spots);

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
        /// <summary>
        /// A cat of this player's own, rolled once and kept.
        ///
        /// The owner's reasoning, and it is a product argument rather than a
        /// technical one: every player meeting the same grey tabby has nothing
        /// to show anyone. If the kitten differs — fat or thin, ginger or
        /// olive, striped or plain, long-haired or short — then a shared
        /// picture says something, and seeing someone else's says that cats
        /// vary. That is the whole reason the coat is assembled from traits
        /// instead of being three shipped pictures.
        ///
        /// Deterministic from <paramref name="seed"/> so the same player gets
        /// the same cat on every launch, and so a test can pin one.
        ///
        /// White markings are rolled at one in three each, independently: a cat
        /// with none is the commonest single outcome and a cat with all three
        /// is rare, which is roughly how markings fall in life.
        ///
        /// The origin is <see cref="TraitsOrigin.Skipped"/> and not something
        /// new, because it is still true — nobody has given the game a
        /// photograph. This cat is a placeholder with a face, not a claim about
        /// anyone's actual pet, and the moment a photo arrives it is replaced.
        /// </summary>
        public static CatTraits Roll(int seed)
        {
            var rng = new Random(seed);
            string Pick(string key)
            {
                var options = Allowed[key];
                return options[rng.Next(options.Length)];
            }

            var markings = new List<string>();
            foreach (var m in Allowed["white_markings"])
                if (rng.Next(3) == 0) markings.Add(m);

            return new CatTraits(Pick("base_color"), Pick("pattern"),
                                 Pick("fur_length"), Pick("eye_color"),
                                 markings, TraitsOrigin.Skipped);
        }

        public static CatTraits Default => new CatTraits(
            "grey", "tabby", "short", "green", Array.Empty<string>(),
            TraitsOrigin.Skipped);

        /// <summary>
        /// A cat built without the Worker, from what the device could read off
        /// the photograph itself.
        ///
        /// <para>It used to be the base colour and nothing else, on the
        /// grounds that "no on-device API reads a coat pattern"
        /// (knowledge/ios/06-on-device-coat-traits.md). That is still true of
        /// Apple's classifier and still true of ML Kit's labeller, and it was
        /// the wrong conclusion: no API NAMES a pattern, but both platforms
        /// hand over a subject mask, and banding is a measurement over the
        /// cat's own pixels rather than a classification.
        /// <see cref="CoatReader"/> makes it.</para>
        ///
        /// <para><paramref name="pattern"/> and <paramref name="furLength"/>
        /// are null when the measurement would not commit, and null keeps the
        /// value the game has always used. That is deliberate and it is the
        /// rule the marks system already follows: a wrong pattern is worse
        /// than no pattern, because the plain cat is the one nobody has ever
        /// been surprised by.</para>
        /// </summary>
        public static CatTraits FromColourOnly(string baseColor,
                                               string pattern = null,
                                               string furLength = null) =>
            new CatTraits(baseColor, pattern ?? "solid", furLength ?? "short",
                          "green", Array.Empty<string>(),
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
