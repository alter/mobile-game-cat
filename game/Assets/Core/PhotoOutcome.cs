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
        /// <summary>A cat, but not clearly enough to be sure of the reading.</summary>
        UnclearCat,
        /// <summary>A cat, and we are sure of it.</summary>
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
    ///
    /// WHAT THIS CLASS NO LONGER DOES, AS OF 2026-09-01. It used to hold the
    /// gate: `Accepts(outcome)` answered "may this photograph become a cat",
    /// `CaptureScreen` asked it, and three of the four outcomes ended the run.
    /// The gate is gone. Recognition's job is now to LOCATE the cat so the
    /// crop can be aimed at her; it no longer decides whether the player is
    /// allowed to proceed. Every photograph makes a cat.
    ///
    /// The reasoning, recorded here because the gate will look like an
    /// obvious safety feature to whoever reads this next and wants it back.
    ///
    /// The gate bought exactly one thing: not making a cat out of a dog. That
    /// is worth close to nothing. This is a cat shelter; the player chose the
    /// photograph out of her own gallery; she knows what she photographed. It
    /// cost the game's entire promise — "we will make YOUR cat" — withdrawn,
    /// with the blame put on the player ("try a photo where the cat fills more
    /// of the frame"), for a cat that was plainly in the frame.
    ///
    /// And it could not be made to work. Three separate causes were found and
    /// fixed in a single day, all the same class of mistake: a cushion
    /// outscoring the cat (<see cref="VisionAnswer.Best"/> took the plain
    /// maximum), `Dog` at 0.06 shown as "that looks like a dog" (the branch
    /// below skipped the confidence gate for dogs), and ML Kit merging the cat
    /// with the armchair so the whole-frame look was suppressed
    /// (`CatVision.java`). All three are fixed. The owner still got "кошки
    /// здесь не видно" on cats filling the frame. A fourth cause would have
    /// arrived the next day, because a 447-label general labeller was being
    /// asked to be a gatekeeper, on photographs taken in rooms we have never
    /// seen, on a phone we do not have. Deleting the gate deletes the class.
    ///
    /// The four outcomes SURVIVE, and they still earn their place: they choose
    /// the sentence the player reads, and they say whether the box beside the
    /// label is worth cropping to. They are a description of what we saw. They
    /// are no longer a permission.
    /// </summary>
    public static class PhotoJudge
    {
        /// <summary>
        /// Measured against the reference set of 41 photographs
        /// (50-photo/05-vision-plugin/NOTES.md), not chosen by feel.
        ///
        /// Vision's confidence on genuine cats runs 0.60 to 0.81. The lowest
        /// four — 0.60, 0.60, 0.60, 0.61 — are ordinary photographs of ordinary
        /// cats, so anything above 0.60 starts refusing to NAME cats that are
        /// plainly cats. That fixes the floor at exactly the bottom of the
        /// observed range.
        ///
        /// Since the gate died this number costs a great deal less than it did:
        /// getting it wrong now means the wrong sentence and a crop of the whole
        /// frame, where it used to mean no cat at all. It is kept where the
        /// measurement put it all the same.
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
            // honest thing to say about the photograph rather than naming an
            // animal that is not there.
            //
            // The word "gate" here is now about NAMING and nothing else: since
            // 2026-09-01 no outcome stops the run, and this threshold decides
            // which sentence she reads and whether the box beside the label is
            // trusted enough to crop to. Nobody is turned away by it.
            var isDog = identifier.Equals(DogIdentifier, StringComparison.OrdinalIgnoreCase);
            var isCat = identifier.Equals(CatIdentifier, StringComparison.OrdinalIgnoreCase);

            // Vision knows two species; a third identifier would mean the API
            // changed under us. Treat it as "no cat here" rather than guessing,
            // and never as a named cat — which now means the whole frame is
            // cropped rather than a box drawn around something we cannot name.
            if (!isDog && !isCat) return PhotoOutcome.NoAnimal;

            if (confidence < MinimumConfidence)
                // A cat below the line is still a cat as far as we are
                // concerned — we say we could not see her clearly, we crop to
                // where the recogniser thought she was, and she gets her cat.
                // A dog below the line is not a dog at all.
                return isCat ? PhotoOutcome.UnclearCat : PhotoOutcome.NoAnimal;

            return isCat ? PhotoOutcome.Cat : PhotoOutcome.Dog;
        }

        /// <summary>
        /// We are sure this is a cat.
        ///
        /// This replaced `Accepts(outcome)` on 2026-09-01, and the rename is
        /// the point rather than a tidy-up. `Accepts` was a permission, and it
        /// was read as one everywhere it appeared: `if (!Accepts(outcome))` was
        /// followed by a `yield break`, twice. A method still called `Accepts`
        /// on a screen that accepts everything is an invitation to put the
        /// `yield break` back, and it would go back in one line by someone
        /// perfectly reasonably thinking they were restoring a check that had
        /// been dropped by accident. There is now no method here that answers
        /// "may she proceed", because that question has no caller and must not
        /// acquire one.
        ///
        /// What it is for now: the difference between a plain hint and an
        /// answer sitting on a ground (`CaptureScreen.Answer`). A confident cat
        /// gets the quiet "she's lovely" line; the other three are the screen
        /// saying something back about what it saw, which still deserves to
        /// look like a reply.
        /// </summary>
        public static bool SawACat(PhotoOutcome outcome) => outcome == PhotoOutcome.Cat;

        /// <summary>
        /// Whether the box that came with this outcome is worth cropping to.
        ///
        /// The whole remaining job of recognition, in one line. Cat, unclear
        /// cat and dog all mean an animal was found somewhere in the frame, and
        /// the box around it is a better subject than the room it is standing
        /// in — for a dog too, because we are about to read a coat off those
        /// pixels and a dog's own fur beats an armchair.
        ///
        /// <see cref="PhotoOutcome.NoAnimal"/> is the case where it is not.
        /// That outcome covers a below-threshold guess and an identifier we do
        /// not recognise at all, and if the LABEL is not to be believed then
        /// neither is the box drawn around it: cropping to a cushion the
        /// labeller called a horse is strictly worse than keeping the whole
        /// photograph. So the frame becomes the box, and `Shell.CatPhoto`
        /// already accepts an empty box as exactly that (a default
        /// <see cref="AnimalBox"/> means "use the whole image", on both
        /// platforms, and says so in both files).
        /// </summary>
        public static bool LocatedAnAnimal(PhotoOutcome outcome) =>
            outcome != PhotoOutcome.NoAnimal;
    }
}
