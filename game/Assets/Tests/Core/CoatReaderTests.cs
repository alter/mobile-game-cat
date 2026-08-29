using System;
using CatShelter.Core;
using NUnit.Framework;

namespace CatShelter.Core.Tests
{
    /// <summary>
    /// The reader on shapes whose answer is known by construction, so that a
    /// broken primitive is caught here rather than showing up as a two-photo
    /// swing on the fixture set. <c>CoatReaderFixtureTests</c> is the one that
    /// scores real photographs; this one only says the arithmetic works.
    /// </summary>
    [TestFixture]
    public class CoatReaderTests
    {
        private const int Side = 256;

        /// <summary>A filled circle of the given colour on a black field, with
        /// a mask that is exactly the circle.</summary>
        private static (byte[] rgb, byte[] mask) Blob(
            byte r, byte g, byte b, Func<int, int, double> shade = null)
        {
            var rgb = new byte[Side * Side * 3];
            var mask = new byte[Side * Side];
            var centre = Side / 2.0;
            var radius = Side * 0.4;
            for (var y = 0; y < Side; y++)
            for (var x = 0; x < Side; x++)
            {
                var dx = x - centre;
                var dy = y - centre;
                if (dx * dx + dy * dy > radius * radius) continue;
                var i = y * Side + x;
                mask[i] = 255;
                var k = shade?.Invoke(x, y) ?? 1.0;
                rgb[i * 3] = (byte)Math.Min(255, r * k);
                rgb[i * 3 + 1] = (byte)Math.Min(255, g * k);
                rgb[i * 3 + 2] = (byte)Math.Min(255, b * k);
            }
            return (rgb, mask);
        }

        private static CoatReading Read((byte[] rgb, byte[] mask) blob) =>
            CoatReader.Read(blob.rgb, Side, Side, blob.mask, Side, Side);

        [Test]
        public void NoMaskSaysNothingAtAll()
        {
            var blob = Blob(115, 110, 105);
            var reading = CoatReader.Read(blob.rgb, Side, Side, null, 0, 0);
            Assert.That(reading.BaseColor, Is.Null);
            Assert.That(reading.Pattern, Is.Null);
            Assert.That(reading.FurLength, Is.Null);
            Assert.That(reading.Note, Does.Contain("no mask"));
        }

        [Test]
        public void AMaskTooSmallToBeACatSaysNothingAtAll()
        {
            var rgb = new byte[Side * Side * 3];
            var mask = new byte[Side * Side];
            for (var i = 0; i < 200; i++) mask[i] = 255;
            var reading = CoatReader.Read(rgb, Side, Side, mask, Side, Side);
            Assert.That(reading.BaseColor, Is.Null);
            Assert.That(reading.Pattern, Is.Null);
        }

        [Test]
        public void ABadImageIsNotAnException()
        {
            Assert.That(CoatReader.Read(null, 0, 0, null, 0, 0).BaseColor, Is.Null);
            Assert.That(CoatReader.Read(new byte[3], 100, 100, new byte[1], 1, 1)
                .BaseColor, Is.Null);
        }

        [Test]
        public void TheColourIsReadOffTheAnimalAndNotOffTheBackground()
        {
            // A white cat on a black field. The frame mean is mid-grey; the
            // cat is white, and only the mask can tell the two apart.
            var reading = Read(Blob(217, 214, 209));
            Assert.That(reading.BaseColor, Is.EqualTo("white"));
        }

        [Test]
        public void AFlatCoatIsNotCalledBanded()
        {
            // Null, not "solid". The reader never claims solid: a null pattern
            // already makes CatTraits.FromColourOnly keep solid, so the two
            // answers draw the same kitten and only one of them can be wrong.
            var reading = Read(Blob(115, 110, 105));
            Assert.That(reading.Pattern, Is.Null, reading.ToString());
            Assert.That(reading.Texture, Is.LessThan(CoatReader.TabbyTexture));
        }

        [Test]
        public void ASmoothlyShadedCoatIsNotCalledBanded()
        {
            // A single soft gradient across the animal — the shading of a plain
            // cat lit from one side. There is no energy at stripe scale in it,
            // however far the two ends of her are apart in L*.
            var reading = Read(Blob(210, 195, 180, (x, _) => 0.30 + 0.70 * x / Side));
            Assert.That(reading.Pattern, Is.Null, reading.ToString());
            Assert.That(reading.Contrast, Is.GreaterThan(CoatReader.TabbyContrast),
                "the gradient must be wide enough that only the stripe-scale "
                + "statistic, and not the contrast floor, is what refuses it");
        }

        [Test]
        public void ABandedCoatIsATabby()
        {
            // Dark bands about eight pixels wide on a light coat — a tabby at
            // this scale. min(bbox) here is about 205 px, so the closing runs
            // at radius 3 and the light gaps between bands are inside it.
            var reading = Read(Blob(210, 190, 160,
                (x, y) => ((x + y) / 8) % 2 == 0 ? 1.0 : 0.42));
            Assert.That(reading.Pattern, Is.EqualTo("tabby"), reading.ToString());
            Assert.That(reading.Texture, Is.GreaterThan(CoatReader.TabbyTexture));
        }

        [Test]
        public void FurLengthIsNeverClaimed()
        {
            // The guard on FurLengthEnabled, pinned: the labelled set has one
            // long-hair in twenty and cannot settle the threshold, so the
            // reader must stay silent no matter how ragged the outline is.
            Assert.That(CoatReader.FurLengthEnabled, Is.False);
            Assert.That(Read(Blob(115, 110, 105)).FurLength, Is.Null);
        }

        [Test]
        public void ACleanOutlineIsNotRough()
        {
            var reading = Read(Blob(115, 110, 105));
            Assert.That(reading.EdgeRoughness, Is.GreaterThan(0.0));
            Assert.That(reading.EdgeRoughness, Is.LessThan(1.15), reading.ToString());
        }

        [Test]
        public void AMaskCoarserThanTheImageIsSampledNotRejected()
        {
            var blob = Blob(217, 214, 209);
            var half = Side / 2;
            var small = new byte[half * half];
            for (var y = 0; y < half; y++)
            for (var x = 0; x < half; x++)
                small[y * half + x] = blob.mask[(y * 2) * Side + x * 2];

            var reading = CoatReader.Read(blob.rgb, Side, Side, small, half, half);
            Assert.That(reading.BaseColor, Is.EqualTo("white"));
            Assert.That(reading.BodyShare, Is.GreaterThan(0.4));
        }
    }
}
