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

            // A GUESS at a dog is not a dog. The confidence gate below used to
            // apply to cats only: `Dog` returned here immediately, whatever the
            // number beside it, and the number can be very small indeed —
            // `CatVision.java` keeps any Cat or Dog label above a floor of
            // 0.05, deliberately far under this threshold, because the decision
            // was supposed to be taken HERE. It then wasn't, for one of the two
            // species.
            //
            // The owner found it on 2026-09-01: "похоже на собаку" for many of
            // his cats, and for a screenshot of a web page with text on it. A
            // page of text is not a dog, and the labeller never said it was —
            // it offered `Dog 0.06` among its guesses, we took the word and
            // dropped the number, and the game told him it saw a dog.
            //
            // It also explains the cats. Where a photograph scores `Cat 0.55`
            // and `Dog 0.58`, the native side returns the larger of the two and
            // this method turned that into a flat "that looks like a dog" —
            // for a cat, on the screen whose whole job is to accept her cat.
            //
            // So both species pass the same gate now. Below it there is no
            // animal we are willing to name, and "no cat in this one" is the
            // honest thing to say: it points at the photograph, which is what
            // the player can change, rather than at an animal that is not
            // there.
            var isDog = identifier.Equals(DogIdentifier, StringComparison.OrdinalIgnoreCase);
            var isCat = identifier.Equals(CatIdentifier, StringComparison.OrdinalIgnoreCase);

            // Vision knows two species; a third identifier would mean the API
            // changed under us. Treat it as "no cat here" rather than guessing,
            // and never as an accepted cat.
            if (!isDog && !isCat) return PhotoOutcome.NoAnimal;

            if (confidence < MinimumConfidence)
                // A cat below the line is still worth saying "too blurry to
                // read" about — she did photograph a cat, and a steadier one
                // will work. A dog below the line is not a dog at all.
                return isCat ? PhotoOutcome.UnclearCat : PhotoOutcome.NoAnimal;

            return isCat ? PhotoOutcome.Cat : PhotoOutcome.Dog;
        }

        /// <summary>Whether this outcome goes on to the crop and the model call.</summary>
        public static bool Accepts(PhotoOutcome outcome) => outcome == PhotoOutcome.Cat;
    }
}
