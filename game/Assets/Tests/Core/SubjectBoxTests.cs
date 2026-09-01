using CatShelter.Core;
using NUnit.Framework;

namespace CatShelter.Core.Tests
{
    /// <summary>
    /// The mask is not the photograph, and the box has to say which one it is
    /// measured in.
    ///
    /// Every case here is built from the real geometry: the segmenter's mask is
    /// capped at 512 on its long side (<c>CatVision.DefaultMaskSide</c>), so a
    /// 3000x4000 photograph is described by a 384x512 grid and every mask cell
    /// stands for roughly 7.8 pixels. The bug this fixture exists for shipped
    /// with no conversion at all, which is invisible on the small photographs a
    /// messenger delivers and catastrophic on the ones a camera makes.
    /// </summary>
    [TestFixture]
    public class SubjectBoxTests
    {
        /// <summary>A mask with one rectangle of "her" in it, everything else 0.</summary>
        private static byte[] MaskWith(int maskWidth, int maskHeight,
                                       int left, int top, int right, int bottom)
        {
            var mask = new byte[maskWidth * maskHeight];
            for (int y = top; y <= bottom; y++)
                for (int x = left; x <= right; x++)
                    mask[y * maskWidth + x] = 255;
            return mask;
        }

        [Test]
        public void BoxIsInPhotographPixelsNotMaskCells()
        {
            // The owner's own frame, 2026-09-01: a 3000x4000 photograph, a
            // 384x512 mask, and the cat filling a 277x470 block of it. Shipped,
            // this came out as a box of 277x470 PIXELS and cropped the corner.
            var mask = MaskWith(384, 512, 40, 20, 40 + 276, 20 + 469);

            var box = SubjectBox.Of(mask, 384, 512, 3000, 4000);

            // 3000/384 = 7.8125 per cell across, 4000/512 = 7.8125 down.
            Assert.That(box.x, Is.EqualTo(312));       // floor(40 * 7.8125)
            Assert.That(box.y, Is.EqualTo(156));       // floor(20 * 7.8125)
            Assert.That(box.width, Is.EqualTo(2165));  // ceil(317 * 7.8125) - 312
            Assert.That(box.height, Is.EqualTo(3673)); // ceil(490 * 7.8125) - 156

            // The point of the whole fixture, stated as the thing that was
            // wrong: a box measured in mask cells would be 277x470 here, which
            // is under a tenth of the frame and in the wrong corner of it.
            Assert.That(box.width, Is.GreaterThan(277 * 5));
        }

        [Test]
        public void SmallPhotographIsWrongByLessWhichIsWhyThisHidSoLong()
        {
            // The same cat, the same mask, a 960x1280 frame — what a messenger
            // delivers. 960/384 = 2.5, so the uncorrected box was out by 2.5x
            // rather than 7.8x, and still overlapped her. That is exactly why
            // weeks of testing on forwarded copies found nothing.
            var mask = MaskWith(384, 512, 40, 20, 40 + 276, 20 + 469);

            var box = SubjectBox.Of(mask, 384, 512, 960, 1280);

            Assert.That(box.x, Is.EqualTo(100));
            Assert.That(box.y, Is.EqualTo(50));
            Assert.That(box.width, Is.EqualTo(693));
            Assert.That(box.height, Is.EqualTo(1175));
        }

        [Test]
        public void SquareMaskOverSquareImageIsTheIdentity()
        {
            var mask = MaskWith(4, 4, 1, 1, 2, 2);

            var box = SubjectBox.Of(mask, 4, 4, 4, 4);

            Assert.That(box.x, Is.EqualTo(1));
            Assert.That(box.y, Is.EqualTo(1));
            Assert.That(box.width, Is.EqualTo(2));
            Assert.That(box.height, Is.EqualTo(2));
        }

        [Test]
        public void TheBoxNeverLeavesThePhotograph()
        {
            // Her mask runs to the very last cell on both sides. Rounding
            // outward must not walk off the edge — CatPhoto would clamp it, but
            // a box that has to be clamped is a box that lied.
            var mask = MaskWith(384, 512, 0, 0, 383, 300);

            var box = SubjectBox.Of(mask, 384, 512, 3000, 4000);

            Assert.That(box.x, Is.EqualTo(0));
            Assert.That(box.x + box.width, Is.LessThanOrEqualTo(3000));
            Assert.That(box.y + box.height, Is.LessThanOrEqualTo(4000));
        }

        [Test]
        public void AMaskOverEverythingHasLocatedNothing()
        {
            // The "cat merged with the armchair" case. A box around the whole
            // frame is the crop we were already taking, so say so.
            var mask = MaskWith(384, 512, 0, 0, 383, 511);

            Assert.That(SubjectBox.Of(mask, 384, 512, 3000, 4000).width,
                        Is.EqualTo(0));
        }

        [Test]
        public void AnEmptyMaskIsNoBox()
        {
            var mask = new byte[384 * 512];

            Assert.That(SubjectBox.Of(mask, 384, 512, 3000, 4000).width,
                        Is.EqualTo(0));
        }

        [Test]
        public void HalfConfidenceIsTheEdgeOfHer()
        {
            // 128 is inside, 127 is not, and the coat reader thresholds the
            // same mask at the same value. The two must not drift apart.
            var mask = new byte[16];
            mask[5] = 127;
            Assert.That(SubjectBox.Of(mask, 4, 4, 4, 4).width, Is.EqualTo(0));

            mask[5] = 128;
            Assert.That(SubjectBox.Of(mask, 4, 4, 4, 4).width, Is.EqualTo(1));
        }

        [Test]
        public void NonsenseInputIsNoBoxRatherThanACrash()
        {
            Assert.That(SubjectBox.Of(null, 384, 512, 3000, 4000).width, Is.EqualTo(0));
            Assert.That(SubjectBox.Of(new byte[4], 0, 0, 3000, 4000).width, Is.EqualTo(0));
            // A mask shorter than its own declared size: the packet arrived
            // truncated, and guessing at the missing rows is worse than nothing.
            Assert.That(SubjectBox.Of(new byte[10], 384, 512, 3000, 4000).width,
                        Is.EqualTo(0));
            // No frame size means nothing to convert INTO, which is the state
            // the shipped bug was effectively in.
            var mask = MaskWith(4, 4, 1, 1, 2, 2);
            Assert.That(SubjectBox.Of(mask, 4, 4, 0, 0).width, Is.EqualTo(0));
        }

        [Test]
        public void ALocatedBoxIsNamedButNotClaimed()
        {
            var mask = MaskWith(4, 4, 1, 1, 2, 2);

            var box = SubjectBox.Of(mask, 4, 4, 400, 400);

            // Named so PhotoJudge and AnimalBox.IsCat agree with the rest of
            // the pipeline; confidence left at zero because the mask located
            // her and identified nothing.
            Assert.That(box.IsCat, Is.True);
            Assert.That(box.confidence, Is.EqualTo(0f));
        }
    }
}
