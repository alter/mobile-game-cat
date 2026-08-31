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

        // ── The light in the room ───────────────────────────────────────────

        /// <summary>
        /// A circle of <paramref name="cat"/> on a field of
        /// <paramref name="room"/>, the whole scene then lit by
        /// <paramref name="light"/> — three multipliers applied in LINEAR
        /// light, which is where a lamp multiplies.
        /// </summary>
        private static (byte[] rgb, byte[] mask) Lit(
            (int r, int g, int b) cat, (int r, int g, int b) room,
            double lr, double lg, double lb, double teeth = 0.0)
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
                var reach = radius;
                // A comb around the rim: the halo of single hairs a long-haired
                // cat leaves in a subject mask.
                if (teeth > 0) reach += teeth * (((x / 3) + (y / 3)) % 2 == 0 ? 1 : -1);
                var i = y * Side + x;
                var here = dx * dx + dy * dy <= reach * reach;
                if (here) mask[i] = 255;
                var c = here ? cat : room;
                rgb[i * 3] = Shine(c.r, lr);
                rgb[i * 3 + 1] = Shine(c.g, lg);
                rgb[i * 3 + 2] = Shine(c.b, lb);
            }
            return (rgb, mask);
        }

        private static byte Shine(int value, double gain)
        {
            var v = value / 255.0;
            var linear = (v <= 0.04045 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4)) * gain;
            var back = linear <= 0.0031308
                ? linear * 12.92
                : 1.055 * Math.Pow(linear, 1.0 / 2.4) - 0.055;
            return (byte)Math.Max(0, Math.Min(255, Math.Round(back * 255.0)));
        }

        [Test]
        public void AWarmLampIsDiscountedBeforeTheColourIsNamed()
        {
            // The whole point of the exercise, in miniature. A grey cat (115,
            // 110, 105 — the palette's own grey anchor) in a room with neutral
            // walls, under a light warm enough to matter and mild enough to be
            // believable. As photographed she is browner than she is; the walls
            // say by how much, because they are known to be neutral and are not
            // her.
            var scene = Lit((115, 110, 105), (140, 140, 140), 1.12, 1.0, 0.85);
            var reading = Read(scene);

            Assert.That(reading.ColourByMedian, Is.EqualTo("brown"),
                "the cast has to be strong enough to change the answer, or this "
                + "test proves nothing: " + reading);
            Assert.That(reading.BaseColor, Is.EqualTo("grey"), reading.ToString());
            Assert.That(reading.GainB, Is.GreaterThan(reading.GainR),
                "a warm light is short of blue, so blue is what gets gained up");
        }

        [Test]
        public void ARedWallIsNotALampAndIsRefused()
        {
            // Grey-world cannot tell a red LIGHT from a red WALL, and the two
            // want opposite treatment: remove the first, keep the second. The
            // only honest separator is size — a lamp tints a scene by a few per
            // cent and a painted wall by half. Anything past the gate is
            // refused outright rather than clamped, so the answer is exactly
            // the answer this reader gave before the correction existed.
            var scene = Lit((115, 110, 105), (190, 40, 40), 1.0, 1.0, 1.0);
            var reading = Read(scene);

            Assert.That(reading.GainR, Is.EqualTo(1.0));
            Assert.That(reading.GainG, Is.EqualTo(1.0));
            Assert.That(reading.GainB, Is.EqualTo(1.0));
            Assert.That(reading.BaseColor, Is.EqualTo(reading.ColourByMedian));
            Assert.That(reading.Note, Does.Contain("coloured wall"));
        }

        [Test]
        public void ACatFillingTheFrameLeavesTheLightAlone()
        {
            // Nothing to measure the light against. The reading is the old
            // reading, and the note says why rather than leaving a person to
            // wonder whether the correction ran and failed.
            var rgb = new byte[Side * Side * 3];
            var mask = new byte[Side * Side];
            for (var i = 0; i < Side * Side; i++)
            {
                mask[i] = 255;
                rgb[i * 3] = 115;
                rgb[i * 3 + 1] = 110;
                rgb[i * 3 + 2] = 105;
            }
            var reading = CoatReader.Read(rgb, Side, Side, mask, Side, Side);

            Assert.That(reading.BackgroundShare, Is.EqualTo(0.0));
            Assert.That(reading.GainR, Is.EqualTo(1.0));
            Assert.That(reading.BaseColor, Is.EqualTo("grey"));
            Assert.That(reading.Note, Does.Contain("too little"));
        }

        [Test]
        public void ARaggedOutlineDoesNotMakeAPlainCoatBanded()
        {
            // The shape that produced the wrong answer: a long-haired cat whose
            // mask edge is a comb of single hairs. Every one of those rim
            // pixels is compared against a blur window that is mostly outside
            // her, and the residual there is the comb rather than the coat.
            // Measured over her interior only, a flat coat stays flat.
            var scene = Lit((210, 205, 200), (60, 60, 60), 1.0, 1.0, 1.0, teeth: 6.0);
            var reading = Read(scene);

            Assert.That(reading.Pattern, Is.Null, reading.ToString());
            Assert.That(reading.TextureShare, Is.GreaterThan(0.0).And.LessThan(1.0),
                "the fringe must actually have been dropped, or the gate is "
                + "not doing anything: " + reading);
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
