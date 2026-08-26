using System;

namespace CatShelter.Core
{
    /// <summary>What the photo turned out to be. Exactly four, no silent fifth.</summary>
    public enum PhotoOutcome
    {
        /// <summary>Nothing animal-shaped in the frame.</summary>
        NoAnimal,
        /// <summary>A dog. Recognised confidently, just not what we asked for.</summary>
        Dog,
        /// <summary>A cat, but not clearly enough to build a coat from.</summary>
        UnclearCat,
        /// <summary>A cat. Proceed to the crop.</summary>
        Cat,
    }

    /// <summary>
    /// Task 50-photo/06: turn a Vision result into one of four outcomes.
    ///
    /// This is a rule, not presentation, so it lives here and is tested here;
    /// the wording of each message belongs to the shell.
    ///
    /// The threshold is measured, not guessed — see the note on
    /// <see cref="MinimumConfidence"/>.
    /// </summary>
    public static class PhotoJudge
    {
        /// <summary>
        /// Measured against the reference set of 41 photographs
        /// (50-photo/05-vision-plugin/NOTES.md), not chosen by feel.
        ///
        /// Vision's confidence on genuine cats runs 0.60 to 0.81. The lowest
        /// four — 0.60, 0.60, 0.60, 0.61 — are ordinary photographs of ordinary
        /// cats, so anything above 0.60 starts rejecting cats that are plainly
        /// cats. That fixes the floor at exactly the bottom of the observed
        /// range.
        ///
        /// It does NOT separate a real cat from a photograph of a cat on a
        /// screen: those measured 0.62 and 0.64, inside the range and above
        /// four genuine cats. No threshold can do that, and this one does not
        /// pretend to.
        /// </summary>
        public const float MinimumConfidence = 0.60f;

        private const string CatIdentifier = "Cat";
        private const string DogIdentifier = "Dog";

        /// <summary>
        /// Decide from the best detection Vision returned.
        /// <paramref name="identifier"/> is null or empty when it found nothing.
        /// </summary>
        public static PhotoOutcome Judge(string identifier, float confidence)
        {
            if (string.IsNullOrEmpty(identifier))
                return PhotoOutcome.NoAnimal;

            if (identifier.Equals(DogIdentifier, StringComparison.OrdinalIgnoreCase))
                return PhotoOutcome.Dog;

            if (!identifier.Equals(CatIdentifier, StringComparison.OrdinalIgnoreCase))
                // Vision knows two species; a third identifier would mean the
                // API changed under us. Treat it as "no cat here" rather than
                // guessing, and never as an accepted cat.
                return PhotoOutcome.NoAnimal;

            return confidence >= MinimumConfidence
                ? PhotoOutcome.Cat
                : PhotoOutcome.UnclearCat;
        }

        /// <summary>Whether this outcome goes on to the crop and the model call.</summary>
        public static bool Accepts(PhotoOutcome outcome) => outcome == PhotoOutcome.Cat;
    }
}
