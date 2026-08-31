using CatShelter.Core;
using NUnit.Framework;

namespace CatShelter.Core.Tests
{
    /// <summary>
    /// 50-photo/06 VERIFY: <see cref="AnimalBox"/> and <see cref="VisionAnswer"/>
    /// had zero test coverage in either language because they lived in
    /// Shell, which dotnet test does not compile. Moved to Core (no engine
    /// reference was ever in them — the missing test was the only reason,
    /// same as <see cref="TraitsRequest"/> and <see cref="PhotoMessageKey"/>
    /// before it); this is that coverage.
    /// </summary>
    [TestFixture]
    public class VisionAnswerTests
    {
        private static AnimalBox Box(string id, float confidence) =>
            new AnimalBox { identifier = id, confidence = confidence };

        [Test]
        public void ACatBeatsAMoreConfidentDog()
        {
            // This test asserted the opposite until 2026-09-01 — Dog 0.91 over
            // Cat 0.75 — and it was not wrong about the code, it was wrong
            // about the question. It encoded "which detection is the recogniser
            // surest of", and the game asks "is there a cat in this photograph".
            //
            // What that cost: Android cuts every subject out of the frame and
            // labels each one, so a cat on a sofa arrives as several
            // detections. Whatever else was in the owner's rooms outscored his
            // cats, and the game told him "похоже на собаку" — about cats that
            // score 0.99, 0.97 and 0.88 on their own.
            var answer = new VisionAnswer
            {
                ok = true,
                detections = new[]
                {
                    Box("Cat", 0.62f),
                    Box("Dog", 0.91f),
                    Box("Cat", 0.75f),
                },
            };

            Assert.That(answer.Best.identifier, Is.EqualTo("Cat"));
            // And the best cat of the several, not merely the first one found.
            Assert.That(answer.Best.confidence, Is.EqualTo(0.75f));
        }

        [Test]
        public void WithNoCatTheMostConfidentDetectionStillWins()
        {
            // A photograph of somebody's dog is unaffected: no cat to prefer,
            // so ordering by confidence decides exactly as it did before, and
            // PhotoJudge still has the kind message about the dog.
            var answer = new VisionAnswer
            {
                ok = true,
                detections = new[] { Box("Dog", 0.41f), Box("Dog", 0.93f) },
            };

            Assert.That(answer.Best.identifier, Is.EqualTo("Dog"));
            Assert.That(answer.Best.confidence, Is.EqualTo(0.93f));
        }

        [Test]
        public void ADoubtfulCatStillOutranksAConfidentDog()
        {
            // Deliberate, and the whole asymmetry in one line: a cat the
            // recogniser is unsure about is still the thing this shelter is
            // looking for. She gets "too blurry, try another" — advice she can
            // act on — instead of being told her cat is a dog.
            var answer = new VisionAnswer
            {
                ok = true,
                detections = new[] { Box("Cat", 0.31f), Box("Dog", 0.99f) },
            };

            Assert.That(answer.Best.identifier, Is.EqualTo("Cat"));
        }

        [Test]
        public void BestOnASingleDetectionReturnsThatDetection()
        {
            var answer = new VisionAnswer
            {
                ok = true,
                detections = new[] { Box("Cat", 0.60f) },
            };

            Assert.That(answer.Best.identifier, Is.EqualTo("Cat"));
            Assert.That(answer.Best.confidence, Is.EqualTo(0.60f));
        }

        [Test]
        public void BestOnAnEmptyListThrows()
        {
            // FoundAnimal exists precisely so a caller never reaches here —
            // every real call site checks it first (View/CaptureScreen.cs).
            // Pinned so a future caller that skips the check fails loudly,
            // not silently on a default AnimalBox.
            var answer = new VisionAnswer { ok = true, detections = new AnimalBox[0] };
            Assert.Throws<System.InvalidOperationException>(() => { var _ = answer.Best; });
        }

        [Test]
        public void RanFoundNothing_NotFailed_NotFound()
        {
            // ok=true, empty detections: Vision ran and the frame had no
            // animal in it. Not a failure — a content judgement.
            var answer = new VisionAnswer { ok = true, detections = new AnimalBox[0] };
            Assert.That(answer.Failed, Is.False);
            Assert.That(answer.FoundAnimal, Is.False);
        }

        [Test]
        public void CouldNotRun_Failed_NotFound()
        {
            // ok=false: Vision itself could not run - decode failure, not
            // iOS, or handler.perform threw. Not a judgement about the
            // photo. 50-photo/05 VERIFY: this and "found nothing" must stay
            // distinguishable, which is the whole reason Failed exists.
            var answer = new VisionAnswer { ok = false, error = "vision failed: some reason" };
            Assert.That(answer.Failed, Is.True);
            Assert.That(answer.FoundAnimal, Is.False);
        }

        [Test]
        public void FoundSomething_NotFailed_Found()
        {
            var answer = new VisionAnswer { ok = true, detections = new[] { Box("Cat", 0.7f) } };
            Assert.That(answer.Failed, Is.False);
            Assert.That(answer.FoundAnimal, Is.True);
        }

        [Test]
        public void TheThreeStatesAreMutuallyExclusive()
        {
            // Failed / found-nothing / found-something: exactly one must
            // hold, for any ok/detections combination a real caller can see.
            var failed = new VisionAnswer { ok = false };
            var foundNothing = new VisionAnswer { ok = true, detections = new AnimalBox[0] };
            var foundSomething = new VisionAnswer { ok = true, detections = new[] { Box("Cat", 0.7f) } };

            Assert.That(failed.Failed, Is.True);
            Assert.That(failed.FoundAnimal, Is.False);

            Assert.That(foundNothing.Failed, Is.False);
            Assert.That(foundNothing.FoundAnimal, Is.False);

            Assert.That(foundSomething.Failed, Is.False);
            Assert.That(foundSomething.FoundAnimal, Is.True);
        }

        [Test]
        public void IsCatIsCaseInsensitive()
        {
            Assert.That(Box("Cat", 0.5f).IsCat, Is.True);
            Assert.That(Box("cat", 0.5f).IsCat, Is.True);
            Assert.That(Box("CAT", 0.5f).IsCat, Is.True);
            Assert.That(Box("Dog", 0.5f).IsCat, Is.False);
        }
    }
}
