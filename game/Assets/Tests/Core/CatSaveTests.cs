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
    
        // --- the individuating trait ------------------------------------

        [Test]
        public void HerMarksSurviveTheRoundTrip()
        {
            // The one thing in a save that says WHICH cat rather than what
            // kind. It was dropped silently until 2026-08-29: the five class
            // traits came back unchanged, so nothing looked broken, and the
            // player's own cat quietly turned into a generic one on the next
            // launch.
            var traits = new CatTraits("black", "solid", "short", "green",
                new[] { "chest" }, TraitsOrigin.Photo,
                new[] { new CatSpot("paw_left", "light"), new CatSpot("chin", "dark") });

            var back = CatSave.Read(CatSave.Write(new Cat("Mishka", traits)));

            Assert.That(back, Is.Not.Null);
            Assert.That(back.Traits.Spots.Count, Is.EqualTo(2));
            Assert.That(back.Traits.Spots[0].Place, Is.EqualTo("paw_left"));
            Assert.That(back.Traits.Spots[0].Shade, Is.EqualTo("light"));
            Assert.That(back.Traits.Spots[1].Place, Is.EqualTo("chin"));
            Assert.That(back.Traits.Spots[1].Shade, Is.EqualTo("dark"));
        }

        [Test]
        public void ACatWithNoMarksWritesNoSuchLine()
        {
            // Most cats have none. An empty line in a format this small is
            // noise, and its absence has to read back as "none" rather than as
            // a missing field.
            var traits = new CatTraits("grey", "tabby", "short", "green",
                Array.Empty<string>());
            var text = CatSave.Write(new Cat("Kitty", traits));

            Assert.That(text, Does.Not.Contain("spots"));
            Assert.That(CatSave.Read(text).Traits.Spots, Is.Empty);
        }

        [Test]
        public void ASaveWrittenBeforeMarksExistedStillReads()
        {
            // Backward compatibility is the reason marks went on a line of
            // their own: yesterday's file has no such line and must load as a
            // cat without marks, not as a corrupt save.
            var old = "catshelter-cat-v1\nname Barsik\n" +
                      "traits ginger tabby short amber chest,paws Photo\n";

            var back = CatSave.Read(old);

            Assert.That(back, Is.Not.Null);
            Assert.That(back.Name, Is.EqualTo("Barsik"));
            Assert.That(back.Traits.Spots, Is.Empty);
        }

        [Test]
        public void AMarkInAPlaceThatDoesNotExistIsACorruptSave()
        {
            var bad = "catshelter-cat-v1\nname Barsik\n" +
                      "traits ginger tabby short amber  Photo\n" +
                      "spots whisker:light\n";

            Assert.That(CatSave.Read(bad), Is.Null);
        }
}
}
