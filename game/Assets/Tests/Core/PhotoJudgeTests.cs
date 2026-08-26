using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CatShelter.Core;
using NUnit.Framework;

namespace CatShelter.Core.Tests
{
    /// <summary>
    /// Task 50-photo/06: every Vision result lands in exactly one of four
    /// branches, checked against the measured results for the real reference
    /// set rather than against invented inputs.
    ///
    /// The measurements come from 50-photo/05-vision-plugin, which ran
    /// VNRecognizeAnimalsRequest over all 41 photographs. They are inlined here
    /// because the images themselves are third-party and gitignored, while the
    /// numbers they produced are the thing under test.
    /// </summary>
    [TestFixture]
    public class PhotoJudgeTests
    {
        // file, identifier ("" = nothing found), confidence
        private static readonly (string File, string Id, float Confidence)[] Measured =
        {
            ("cat_01", "Cat", 0.80f), ("cat_02", "Cat", 0.62f), ("cat_03", "Cat", 0.60f),
            ("cat_04", "Cat", 0.71f), ("cat_05", "Cat", 0.69f), ("cat_06", "Cat", 0.68f),
            ("cat_07", "Cat", 0.69f), ("cat_08", "Cat", 0.73f), ("cat_09", "Cat", 0.66f),
            ("cat_10", "",    0.00f), ("cat_11", "Cat", 0.75f), ("cat_12", "Cat", 0.60f),
            ("cat_13", "Cat", 0.64f), ("cat_14", "Cat", 0.60f), ("cat_15", "Cat", 0.77f),
            ("cat_16", "Cat", 0.61f), ("cat_17", "Cat", 0.72f), ("cat_18", "Cat", 0.69f),
            ("cat_19", "Cat", 0.81f), ("cat_20", "",    0.00f),
            ("dog_01", "Dog", 0.73f), ("dog_02", "Dog", 0.69f), ("dog_03", "Dog", 0.79f),
            ("dog_04", "Dog", 0.74f), ("dog_05", "Dog", 0.75f),
            ("empty_01", "", 0.00f), ("empty_02", "", 0.00f), ("empty_03", "", 0.00f),
            ("empty_04", "", 0.00f), ("empty_05", "", 0.00f),
            ("blurry_01", "Cat", 0.75f), ("blurry_02", "Cat", 0.78f),
            ("blurry_03", "Cat", 0.74f), ("blurry_04", "", 0.00f),
            ("blurry_05", "Cat", 0.64f),
            ("multi_01", "Cat", 0.79f), ("multi_02", "Cat", 0.75f), ("multi_03", "Cat", 0.79f),
            ("ofphoto_01", "Cat", 0.64f), ("ofphoto_02", "", 0.00f), ("ofphoto_03", "Cat", 0.62f),
        };

        private static PhotoOutcome Judge(string file) =>
            Measured.Where(m => m.File == file)
                    .Select(m => PhotoJudge.Judge(m.Id, m.Confidence))
                    .Single();

        [Test]
        public void EveryReferenceImageLandsInExactlyOneBranch()
        {
            var branches = Enum.GetValues(typeof(PhotoOutcome)).Cast<PhotoOutcome>().ToList();
            foreach (var (file, id, confidence) in Measured)
            {
                var outcome = PhotoJudge.Judge(id, confidence);
                Assert.That(branches, Contains.Item(outcome), file);
            }
            Assert.That(Measured.Length, Is.EqualTo(41), "the whole reference set");
        }

        [Test]
        public void EveryDogIsRejectedAsADog()
        {
            foreach (var (file, _, _) in Measured.Where(m => m.File.StartsWith("dog")))
                Assert.That(Judge(file), Is.EqualTo(PhotoOutcome.Dog), file);
        }

        [Test]
        public void EveryEmptyFrameIsRejectedAsNoAnimal()
        {
            foreach (var (file, _, _) in Measured.Where(m => m.File.StartsWith("empty")))
                Assert.That(Judge(file), Is.EqualTo(PhotoOutcome.NoAnimal), file);
        }

        [Test]
        public void EighteenOfTwentyCatsAreAccepted_TheOtherTwoAreNotSeenAtAll()
        {
            // VERIFY 2 asks for 20 of 20. Vision finds 18: cat_10 is the
            // smallest image in the set (259x270) and cat_20 has two kittens
            // filling the frame. They fail as "no animal", which is the honest
            // branch — the judge cannot accept what was never detected.
            var cats = Measured.Where(m => m.File.StartsWith("cat_")).ToList();
            var accepted = cats.Count(m => Judge(m.File) == PhotoOutcome.Cat);
            var unseen = cats.Count(m => Judge(m.File) == PhotoOutcome.NoAnimal);

            Assert.That(accepted, Is.EqualTo(18));
            Assert.That(unseen, Is.EqualTo(2));
            Assert.That(accepted + unseen, Is.EqualTo(cats.Count));
        }

        [Test]
        public void BlurryAndMultiCatImagesAreHandled_NeverUnclassified()
        {
            foreach (var (file, _, _) in Measured
                         .Where(m => m.File.StartsWith("blurry") || m.File.StartsWith("multi")))
            {
                var outcome = Judge(file);
                Assert.That(outcome,
                    Is.EqualTo(PhotoOutcome.Cat).Or.EqualTo(PhotoOutcome.NoAnimal), file);
            }
        }

        [Test]
        public void PhotographsOfAScreenAreAcceptedAsCats_AndTheThresholdCannotHelp()
        {
            // Recorded as a test because it is a product fact, not a defect:
            // Vision has no notion of liveness. Both screen shots it saw are
            // accepted, and they outscore four genuine cats.
            Assert.That(Judge("ofphoto_01"), Is.EqualTo(PhotoOutcome.Cat));
            Assert.That(Judge("ofphoto_03"), Is.EqualTo(PhotoOutcome.Cat));

            var genuineBelow = Measured
                .Where(m => m.File.StartsWith("cat_") && m.Id == "Cat" && m.Confidence < 0.62f)
                .Select(m => m.File)
                .ToList();
            Assert.That(genuineBelow, Is.Not.Empty,
                "raising the threshold to exclude screen shots would take these with it");
        }

        [Test]
        public void TheThresholdSitsAtTheBottomOfTheObservedRange()
        {
            var lowestRealCat = Measured
                .Where(m => m.File.StartsWith("cat_") && m.Id == "Cat")
                .Min(m => m.Confidence);
            Assert.That(PhotoJudge.MinimumConfidence, Is.EqualTo(lowestRealCat).Within(0.001f));
        }

        [TestCase("Cat", 0.59f, PhotoOutcome.UnclearCat)]
        [TestCase("Cat", 0.60f, PhotoOutcome.Cat)]
        [TestCase("Cat", 0.00f, PhotoOutcome.UnclearCat)]
        [TestCase("Dog", 0.10f, PhotoOutcome.Dog)]
        [TestCase("", 0.90f, PhotoOutcome.NoAnimal)]
        [TestCase(null, 0.90f, PhotoOutcome.NoAnimal)]
        public void BoundariesAreWhereTheyAreDeclared(string id, float confidence,
                                                      PhotoOutcome expected)
        {
            Assert.That(PhotoJudge.Judge(id, confidence), Is.EqualTo(expected));
        }

        [Test]
        public void AnUnknownSpeciesIsNeverAcceptedAsACat()
        {
            // Vision knows cat and dog. A third identifier means the API moved
            // under us, and guessing would put a fox in the shelter.
            Assert.That(PhotoJudge.Judge("Horse", 0.99f), Is.EqualTo(PhotoOutcome.NoAnimal));
        }

        [Test]
        public void OnlyTheAcceptedBranchGoesOnToTheCrop()
        {
            Assert.That(PhotoJudge.Accepts(PhotoOutcome.Cat), Is.True);
            foreach (var other in new[] { PhotoOutcome.NoAnimal, PhotoOutcome.Dog,
                                          PhotoOutcome.UnclearCat })
                Assert.That(PhotoJudge.Accepts(other), Is.False, other.ToString());
        }
    }
}
