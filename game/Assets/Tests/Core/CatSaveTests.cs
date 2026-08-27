using System;
using System.Linq;
using CatShelter.Core;
using NUnit.Framework;

namespace CatShelter.Core.Tests
{
    /// <summary>
    /// Task 50-photo/10: a name is player data and must survive a restart —
    /// the same round-trip/never-crash promise GameSaveTests pins for the
    /// board (DECISIONS.md D12/D13).
    /// </summary>
    [TestFixture]
    public class CatSaveTests
    {
        [Test]
        public void RoundTrip_ReproducesNameAndTraits()
        {
            var cat = new Cat("Marmalade", new CatTraits(
                "ginger", "tabby", "long", "amber", new[] { "chest", "paws" }));

            var text = CatSave.Write(cat);
            var restored = CatSave.Read(text);

            Assert.That(restored, Is.Not.Null);
            Assert.That(restored.Name, Is.EqualTo("Marmalade"));
            Assert.That(restored.Traits.BaseColor, Is.EqualTo("ginger"));
            Assert.That(restored.Traits.Pattern, Is.EqualTo("tabby"));
            Assert.That(restored.Traits.FurLength, Is.EqualTo("long"));
            Assert.That(restored.Traits.EyeColor, Is.EqualTo("amber"));
            Assert.That(restored.Traits.WhiteMarkings, Is.EqualTo(new[] { "chest", "paws" }));
            Assert.That(restored.Traits.Origin, Is.EqualTo(TraitsOrigin.Photo));
        }

        [Test]
        public void RoundTrip_TheSkippedCat_NoWhiteMarkings()
        {
            var text = CatSave.Write(Cat.Skipped);
            var restored = CatSave.Read(text);

            Assert.That(restored, Is.Not.Null);
            Assert.That(restored.Name, Is.EqualTo(Cat.DefaultName));
            Assert.That(restored.Traits.WhiteMarkings, Is.Empty);
            Assert.That(restored.Traits.Origin, Is.EqualTo(TraitsOrigin.Skipped));
        }

        [Test]
        public void RoundTrip_KeepsANameWithSpacesAndNonAsciiLetters()
        {
            // A cat's name is not restricted to the game's own ASCII
            // vocabulary the way trait ids are; a player types what she
            // likes.
            var cat = new Cat("Мурка Two", CatTraits.Default);
            var restored = CatSave.Read(CatSave.Write(cat));

            Assert.That(restored.Name, Is.EqualTo("Мурка Two"));
        }

        [Test]
        public void ANewlineInATypedName_DoesNotBreakTheFormat()
        {
            var cat = new Cat("Line1\nLine2", CatTraits.Default);
            var text = CatSave.Write(cat);
            var restored = CatSave.Read(text);

            Assert.That(restored, Is.Not.Null);
            Assert.That(restored.Name, Does.Not.Contain("\n"));
        }

        [Test]
        public void WriteRefusesANullCat()
        {
            Assert.Throws<ArgumentNullException>(() => CatSave.Write(null));
        }

        [Test]
        public void CorruptedFile_ReturnsNull_NoThrow()
        {
            Assert.That(CatSave.Read(""), Is.Null);
            Assert.That(CatSave.Read(null), Is.Null);
            Assert.That(CatSave.Read("garbage"), Is.Null);
            Assert.That(CatSave.Read("catshelter-cat-v9\nname X"), Is.Null,
                "wrong header version");
            Assert.That(CatSave.Read("catshelter-cat-v1\nname Marmalade"), Is.Null,
                "no traits line at all");
            Assert.That(CatSave.Read(
                "catshelter-cat-v1\nname Marmalade\ntraits not a real cat"),
                Is.Null, "traits line malformed");
            Assert.That(CatSave.Read(
                "catshelter-cat-v1\nname Marmalade\ntraits orange tabby short green  Photo"),
                Is.Null, "base colour outside the palette");
            Assert.That(CatSave.Read(
                "catshelter-cat-v1\nname Marmalade\ntraits grey tabby short green  NotAnOrigin"),
                Is.Null, "origin not one of the enum values");
        }

        [Test]
        public void TruncatedSave_FallsBackCleanly()
        {
            var text = CatSave.Write(new Cat("Marmalade", CatTraits.Default));
            var truncated = text.Substring(0, text.Length / 2);
            Assert.DoesNotThrow(() => CatSave.Read(truncated));
        }

        [Test]
        public void SaveStartsWithItsHeader()
        {
            var text = CatSave.Write(new Cat("Marmalade", CatTraits.Default));
            Assert.That(text, Does.StartWith(CatSave.Header));
        }
    }
}
