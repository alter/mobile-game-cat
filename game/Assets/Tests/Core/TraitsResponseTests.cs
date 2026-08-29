using System;
using CatShelter.Core;
using NUnit.Framework;

namespace CatShelter.Core.Tests
{
    /// <summary>
    /// The Worker's reply, turned into traits. Nothing read that reply until
    /// 2026-08-29: the request had a builder, the Worker had a schema and
    /// tests, and the answer went nowhere.
    /// </summary>
    public sealed class TraitsResponseTests
    {
        private const string Body =
            "{\"base_color\":\"ginger\",\"pattern\":\"tabby\",\"fur_length\":\"short\"," +
            "\"eye_color\":\"amber\",\"white_markings\":[\"chest\",\"paws\"]," +
            "\"spots\":[{\"place\":\"paw_left\",\"shade\":\"light\"}]}";

        [Test]
        public void ItReadsTheClassTraits()
        {
            var traits = TraitsResponse.Read(Body);

            Assert.That(traits, Is.Not.Null);
            Assert.That(traits.BaseColor, Is.EqualTo("ginger"));
            Assert.That(traits.Pattern, Is.EqualTo("tabby"));
            Assert.That(traits.FurLength, Is.EqualTo("short"));
            Assert.That(traits.EyeColor, Is.EqualTo("amber"));
            Assert.That(traits.WhiteMarkings, Is.EquivalentTo(new[] { "chest", "paws" }));
        }

        [Test]
        public void ItReadsTheOneTraitThatIdentifiesHer()
        {
            var traits = TraitsResponse.Read(Body);

            Assert.That(traits.Spots.Count, Is.EqualTo(1));
            Assert.That(traits.Spots[0].Place, Is.EqualTo("paw_left"));
            Assert.That(traits.Spots[0].Shade, Is.EqualTo("light"));
        }

        [Test]
        public void NoSpotsIsAnOrdinaryAnswer()
        {
            var body = Body.Replace(
                "[{\"place\":\"paw_left\",\"shade\":\"light\"}]", "[]");

            Assert.That(TraitsResponse.Read(body).Spots, Is.Empty);
        }

        [Test]
        public void EmptyWhiteMarkingsAreFine()
        {
            var body = Body.Replace("[\"chest\",\"paws\"]", "[]");

            Assert.That(TraitsResponse.Read(body).WhiteMarkings, Is.Empty);
        }

        [Test]
        public void AValueOutsideThePaletteGivesNullRatherThanThrowing()
        {
            // The Worker validates the same schema, so this means the two
            // drifted — and a drifted pair is exactly when a player must still
            // get a cat instead of an error screen.
            var body = Body.Replace("\"ginger\"", "\"orange\"");

            Assert.That(TraitsResponse.Read(body), Is.Null);
        }

        [Test]
        public void AMissingFieldGivesNull()
        {
            var body = Body.Replace("\"eye_color\":\"amber\",", "");

            Assert.That(TraitsResponse.Read(body), Is.Null);
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase("not json at all")]
        [TestCase("{")]
        public void RubbishGivesNull(string body)
        {
            Assert.That(TraitsResponse.Read(body), Is.Null);
        }

        [Test]
        public void AKeyNameInsideAValueIsNotMistakenForTheField()
        {
            // The reader matches a key with its quotes; a cat whose name-like
            // value contains the word `pattern` must not confuse it.
            var body = Body.Replace("\"tabby\"", "\"solid\"");

            Assert.That(TraitsResponse.Read(body).Pattern, Is.EqualTo("solid"));
        }
    }
}
