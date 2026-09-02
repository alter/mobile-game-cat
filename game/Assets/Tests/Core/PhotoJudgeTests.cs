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

        /// <summary>
        /// The same reference set through ML Kit on Android, plus the owner's
        /// four photographs — measured 2026-09-01/02 on emulator-5554 with the
        /// shipped plugin, after the frame-size fix (`Decode.ANALYSIS_MAX_SIDE`
        /// dropped to 1280). Task 90-android/07.
        ///
        /// It sits beside the iOS table on purpose. `PhotoJudge` is ONE rule
        /// for both platforms, and the only way to know it fits both is to run
        /// both sets of numbers through it in one file: a threshold edited for
        /// one platform now breaks the other's test in the same run.
        ///
        /// What the two tables say read together — the whole answer to "does
        /// Android need its own threshold":
        ///
        /// <code>
        ///                   iOS (Vision)         Android (ML Kit)
        ///   cats named      18 of 20             20 of 20
        ///   cat confidence  0.60 - 0.81          0.88 - 1.00
        ///   dogs            5 of 5, 0.69-0.79    5 of 5, 0.93-0.99
        ///   empty rooms     nothing at all       Dog 0.11 - 0.30
        ///   blurry cats     4 of 5               5 of 5
        /// </code>
        ///
        /// On iOS the threshold sits ON the floor of the cat range: 0.60 IS
        /// the lowest genuine cat, and one step up starts refusing real ones.
        /// On Android there is a gulf — worst cat 0.88, best empty room 0.30 —
        /// so 0.60 lands mid-gap and nothing is marginal. One constant serves
        /// both, and no second one is warranted. It is also load-bearing on
        /// Android in a way it never was on iOS: without it, an empty kitchen
        /// labelled `Dog 0.30` would be named a dog to the player.
        ///
        /// The one category where the platforms genuinely DISAGREE: a
        /// photograph of a cat on a screen. iOS calls it a confident cat
        /// (0.62, 0.64); Android does not (0.43, 0.17, 0.46). Since the gate
        /// was deleted on 2026-09-01 both still make a cat out of it, so the
        /// disagreement costs a different sentence and nothing more.
        /// </summary>
        private static readonly (string File, string Id, float Confidence)[] MeasuredAndroid =
        {
            ("blurry_01", "Cat", 0.96f), ("blurry_02", "Cat", 0.99f), ("blurry_03", "Cat", 1.00f),
            ("blurry_04", "Cat", 1.00f), ("blurry_05", "Cat", 1.00f),
            ("cat_01", "Cat", 1.00f), ("cat_02", "Cat", 0.99f), ("cat_03", "Cat", 1.00f),
            ("cat_04", "Cat", 0.97f), ("cat_05", "Cat", 1.00f), ("cat_06", "Cat", 0.98f),
            ("cat_07", "Cat", 1.00f), ("cat_08", "Cat", 0.99f), ("cat_09", "Cat", 0.99f),
            ("cat_10", "Cat", 1.00f), ("cat_11", "Cat", 0.99f), ("cat_12", "Cat", 1.00f),
            ("cat_13", "Cat", 1.00f), ("cat_14", "Cat", 1.00f), ("cat_15", "Cat", 1.00f),
            ("cat_16", "Cat", 0.99f), ("cat_17", "Cat", 0.99f), ("cat_18", "Cat", 1.00f),
            ("cat_19", "Cat", 1.00f), ("cat_20", "Cat", 1.00f),
            ("dog_01", "Dog", 0.99f), ("dog_02", "Dog", 0.99f), ("dog_03", "Dog", 0.99f),
            ("dog_04", "Dog", 0.99f), ("dog_05", "Dog", 0.93f),
            // Empty rooms come back NAMED on Android, unlike iOS, and it is
            // the threshold that turns them into NoAnimal.
            ("empty_01", "Dog", 0.26f), ("empty_02", "", 0.00f), ("empty_03", "Dog", 0.15f),
            ("empty_04", "Dog", 0.11f), ("empty_05", "Dog", 0.30f),
            ("multi_01", "Cat", 0.92f), ("multi_02", "Cat", 0.95f), ("multi_03", "Cat", 0.99f),
            ("ofphoto_01", "Dog", 0.43f), ("ofphoto_02", "Dog", 0.17f), ("ofphoto_03", "Cat", 0.46f),
            // The owner's own four — the ones that spent a week coming back as
            // "похоже на собаку" until the frame reaching ML Kit was capped.
            ("photo_1", "Cat", 0.99f), ("photo_2", "Cat", 0.97f),
            ("photo_3", "Cat", 0.88f), ("photo_4", "Cat", 0.97f),
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

        /// <summary>
        /// Was `EveryDogIsRejectedAsADog` until 2026-09-01. Nothing is rejected
        /// any more — a confident dog gets the kind sentence about somebody's
        /// dog AND a cat — so the name was the only part that had to change.
        /// The assertion is untouched and still matters: it is what keeps the
        /// dog copy attached to actual dogs, which was itself a live bug
        /// yesterday (`Dog 0.06` wearing the same words as `dog_03` at 0.79).
        /// </summary>
        [Test]
        public void EveryDogIsNamedADog()
        {
            foreach (var (file, _, _) in Measured.Where(m => m.File.StartsWith("dog")))
                Assert.That(Judge(file), Is.EqualTo(PhotoOutcome.Dog), file);
        }

        /// <summary>
        /// Was `EveryEmptyFrameIsRejectedAsNoAnimal`; renamed with the gate.
        /// `NoAnimal` is now the outcome that says "crop the whole frame,
        /// because there is no box here we believe in" — see
        /// <see cref="PhotoJudge.LocatedAnAnimal"/> — and an empty room is
        /// exactly the photograph that should get that treatment. It still
        /// becomes a cat.
        /// </summary>
        [Test]
        public void EveryEmptyFrameIsNamedNoAnimal()
        {
            foreach (var (file, _, _) in Measured.Where(m => m.File.StartsWith("empty")))
                Assert.That(Judge(file), Is.EqualTo(PhotoOutcome.NoAnimal), file);
        }

        /// <summary>
        /// Was `EighteenOfTwentyCatsAreAccepted_TheOtherTwoAreNotSeenAtAll`,
        /// and the old name recorded the exact damage the gate did: two of
        /// twenty photographs of cats did not become cats. VERIFY 2 asked for
        /// 20 of 20 and this test was the standing admission that we shipped
        /// 18, with a comment explaining why 18 was the honest number.
        ///
        /// It is 20 of 20 now, and it always was — cat_10 (259x270, the
        /// smallest in the set) and cat_20 (two kittens filling the frame) are
        /// photographs of cats whatever the recogniser managed to say about
        /// them. What the two of them cost us is no longer a cat; it is a crop
        /// of the whole frame instead of a crop aimed at her, which is a
        /// slightly worse coat reading and nothing more.
        ///
        /// So the counts stay, because they are still the truth about the
        /// recogniser and they are the number to watch if it ever gets better
        /// or worse. They just no longer describe who gets a cat.
        /// </summary>
        [Test]
        public void EighteenOfTwentyCatsAreLocated_TheOtherTwoGetTheWholeFrame()
        {
            var cats = Measured.Where(m => m.File.StartsWith("cat_")).ToList();
            var located = cats.Count(m => PhotoJudge.LocatedAnAnimal(Judge(m.File)));
            var wholeFrame = cats.Count(m => !PhotoJudge.LocatedAnAnimal(Judge(m.File)));

            Assert.That(located, Is.EqualTo(18));
            Assert.That(wholeFrame, Is.EqualTo(2));
            Assert.That(located + wholeFrame, Is.EqualTo(cats.Count),
                "and all twenty become cats either way");
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
        // Was `("Dog", 0.10f, Dog)` until 2026-09-01, which is the bug written
        // down as a requirement: the dog branch skipped the confidence gate
        // entirely, so a 0.10 guess wore the same word as a photograph of an
        // actual dog. See AFaintDogIsNotADog for what that looked like to a
        // player.
        [TestCase("Dog", 0.10f, PhotoOutcome.NoAnimal)]
        [TestCase("Dog", 0.60f, PhotoOutcome.Dog)]
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

        /// <summary>
        /// WHAT THIS TEST USED TO REQUIRE, AND WHY IT DOES NOT ANY MORE.
        ///
        /// It was called `OnlyTheAcceptedBranchGoesOnToTheCrop`, and it asserted
        /// `PhotoJudge.Accepts(Cat) == true` with the other three false — which
        /// was a true description of a gate that stopped three photographs in
        /// four from ever becoming a cat. The gate was deleted on 2026-09-01
        /// (see the class comment on <see cref="PhotoJudge"/> for the full
        /// account); `Accepts` went with it, because a method with that name on
        /// a screen that accepts everything is an invitation to put the
        /// `yield break` back.
        ///
        /// The shape of the assertion is unchanged — one outcome true, three
        /// false — and that is not laziness: `SawACat` really is the same
        /// partition of the four. What changed is the sentence it stands for.
        /// It used to mean "only this one is allowed through". It now means
        /// "only this one is a cat we are sure of", and nothing downstream is
        /// allowed to read it as permission.
        /// </summary>
        [Test]
        public void OnlyTheCatBranchIsACatWeAreSureOf()
        {
            Assert.That(PhotoJudge.SawACat(PhotoOutcome.Cat), Is.True);
            foreach (var other in new[] { PhotoOutcome.NoAnimal, PhotoOutcome.Dog,
                                          PhotoOutcome.UnclearCat })
                Assert.That(PhotoJudge.SawACat(other), Is.False, other.ToString());
        }

        /// <summary>
        /// The replacement for the gate, and the only question the outcome
        /// still answers for the pipeline: is the box beside this label worth
        /// cropping to, or do we keep the whole frame?
        ///
        /// The dog case is the one worth staring at. A confident dog is
        /// LOCATED — we crop to her — because we are making a cat out of this
        /// photograph regardless and a dog's own fur is a better thing to read
        /// a coat off than the room behind her. Under the old gate this
        /// combination could not exist: a dog never reached the crop at all.
        /// </summary>
        [TestCase(PhotoOutcome.Cat, true)]
        [TestCase(PhotoOutcome.UnclearCat, true)]
        [TestCase(PhotoOutcome.Dog, true)]
        [TestCase(PhotoOutcome.NoAnimal, false)]
        public void OnlyAnAnimalWeCanNameHasABoxWorthCroppingTo(
            PhotoOutcome outcome, bool located)
        {
            Assert.That(PhotoJudge.LocatedAnAnimal(outcome), Is.EqualTo(located));
        }

        /// <summary>
        /// The whole reference set, as the thing it now measures.
        ///
        /// There used to be no test at this level because there was nothing to
        /// say: the judge's verdict WAS the outcome for the player, and
        /// `EighteenOfTwentyCatsAreAccepted` below counted how many of them got
        /// a cat. That number is now 41 out of 41, every time, and the judge
        /// has no say in it — which is exactly the property that had to become
        /// impossible to break by accident.
        ///
        /// It is asserted here, in Core, rather than against `CaptureScreen`,
        /// because `CaptureScreen` needs the engine and `dotnet test` cannot
        /// load it. So this pins the half that Core owns: no outcome is a
        /// refusal, and there is no method left on `PhotoJudge` that could be
        /// mistaken for one. The other half — that `Handle` has no `yield
        /// break` before `OnCatReady` on any path — is checked on the device,
        /// which is where the last four causes of this bug were found anyway.
        /// </summary>
        [Test]
        public void NoOutcomeRefusesToMakeACat()
        {
            var refusals = typeof(PhotoJudge).GetMethods(
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.Static)
                .Select(m => m.Name)
                .Where(n => n == "Accepts" || n == "Rejects" || n == "Allows")
                .ToList();
            Assert.That(refusals, Is.Empty,
                "the photo gate was deleted on 2026-09-01 and must not grow back " +
                "under a name that reads as permission — see PhotoJudge's class comment");

            foreach (var (file, id, confidence) in Measured)
            {
                var outcome = PhotoJudge.Judge(id, confidence);
                // Every one of the 41 produces a cat. The outcome chooses her
                // sentence and whether we crop to a box; it never chooses
                // whether she gets one.
                Assert.That(Enum.IsDefined(typeof(PhotoOutcome), outcome), file);
                Assert.That(PhotoMessageKey.For(outcome), Is.Not.Empty, file);
            }
        }

        // --- Task 90-android/07: the same rule over the Android numbers -----

        [Test]
        public void EveryAndroidMeasurementAlsoLandsInExactlyOneBranch()
        {
            var branches = Enum.GetValues(typeof(PhotoOutcome)).Cast<PhotoOutcome>().ToList();
            foreach (var (file, id, confidence) in MeasuredAndroid)
            {
                var outcome = PhotoJudge.Judge(id, confidence);
                Assert.That(branches, Contains.Item(outcome), file);
                Assert.That(PhotoMessageKey.For(outcome), Is.Not.Empty, file);
            }
            Assert.That(MeasuredAndroid.Length, Is.EqualTo(45),
                "41 reference photographs plus the owner's four");
        }

        [Test]
        public void AndroidNamesEveryCatAndEveryDogCorrectly()
        {
            foreach (var (file, _, _) in MeasuredAndroid.Where(m => m.File.StartsWith("cat")))
                Assert.That(AndroidJudge(file), Is.EqualTo(PhotoOutcome.Cat), file);
            foreach (var (file, _, _) in MeasuredAndroid.Where(m => m.File.StartsWith("dog")))
                Assert.That(AndroidJudge(file), Is.EqualTo(PhotoOutcome.Dog), file);
        }

        /// <summary>
        /// The threshold is what stands between the player and being told her
        /// empty kitchen is a dog. On iOS this case cannot arise — Vision
        /// returns nothing for an empty room — so this assertion exists only
        /// because Android measured differently, and it is the reason the
        /// constant may not be lowered towards ML Kit's own 0.05 floor.
        /// </summary>
        [Test]
        public void AndroidEmptyRoomsAreNoAnimalDespiteBeingNamedDog()
        {
            var named = MeasuredAndroid
                .Where(m => m.File.StartsWith("empty") && m.Id == "Dog")
                .ToList();
            Assert.That(named, Is.Not.Empty,
                "the point of this test is that ML Kit DOES name empty rooms");

            foreach (var (file, _, _) in MeasuredAndroid.Where(m => m.File.StartsWith("empty")))
                Assert.That(AndroidJudge(file), Is.EqualTo(PhotoOutcome.NoAnimal), file);
        }

        /// <summary>
        /// One constant, not two — asserted rather than asserted-in-prose.
        /// Every Android cat clears the threshold with room to spare, and
        /// every Android empty room falls short of it with room to spare, so
        /// there is no measurement here that would justify a second named
        /// constant for the platform.
        /// </summary>
        [Test]
        public void OneThresholdServesBothPlatformsWithRoomToSpare()
        {
            var worstCat = MeasuredAndroid
                .Where(m => m.File.StartsWith("cat")).Min(m => m.Confidence);
            var bestEmpty = MeasuredAndroid
                .Where(m => m.File.StartsWith("empty")).Max(m => m.Confidence);

            Assert.That(worstCat, Is.GreaterThan(PhotoJudge.MinimumConfidence + 0.2f),
                "Android's worst cat should clear the shared threshold comfortably");
            Assert.That(bestEmpty, Is.LessThan(PhotoJudge.MinimumConfidence - 0.2f),
                "Android's loudest empty room should fall short of it comfortably");

            // iOS is the tight side, and that is what fixes the number: its
            // lowest genuine cat IS the threshold.
            var worstIosCat = Measured
                .Where(m => m.File.StartsWith("cat") && m.Id == "Cat").Min(m => m.Confidence);
            Assert.That(worstIosCat, Is.EqualTo(PhotoJudge.MinimumConfidence).Within(0.001f));
        }

        private static PhotoOutcome AndroidJudge(string file) =>
            MeasuredAndroid.Where(m => m.File == file)
                           .Select(m => PhotoJudge.Judge(m.Id, m.Confidence))
                           .Single();
    }
}
