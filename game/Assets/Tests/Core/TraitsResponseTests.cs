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

        [Test]
        public void AThirdSpotIsDroppedNotTheAnswer()
        {
            // CatTraits.MaxSpots is two. A model that names three no longer
            // costs the player the whole cat — the first two valid marks are
            // kept and the rest is quietly pruned, same as CatTraits.cs:186-191.
            var body = Body.Replace(
                "[{\"place\":\"paw_left\",\"shade\":\"light\"}]",
                "[{\"place\":\"paw_left\",\"shade\":\"light\"}," +
                "{\"place\":\"paw_right\",\"shade\":\"dark\"}," +
                "{\"place\":\"flank\",\"shade\":\"light\"}]");

            var traits = TraitsResponse.Read(body);

            Assert.That(traits, Is.Not.Null);
            Assert.That(traits.Spots.Count, Is.EqualTo(2));
            Assert.That(traits.Spots[0].Place, Is.EqualTo("paw_left"));
            Assert.That(traits.Spots[1].Place, Is.EqualTo("paw_right"));
        }

        [Test]
        public void ARepeatedPlaceDropsTheSecondSpotNotTheAnswer()
        {
            // Two marks in the same place is one mark described twice
            // (CatTraits' own constructor says as much); the first report wins
            // and the duplicate is dropped rather than the whole reply.
            var body = Body.Replace(
                "[{\"place\":\"paw_left\",\"shade\":\"light\"}]",
                "[{\"place\":\"paw_left\",\"shade\":\"light\"}," +
                "{\"place\":\"paw_left\",\"shade\":\"dark\"}]");

            var traits = TraitsResponse.Read(body);

            Assert.That(traits, Is.Not.Null);
            Assert.That(traits.Spots.Count, Is.EqualTo(1));
            Assert.That(traits.Spots[0].Shade, Is.EqualTo("light"));
        }

        [Test]
        public void AnUnknownPlaceDropsTheSpotNotTheAnswer()
        {
            // "tail" is not in CatTraits.Allowed["spot_place"] (the real value
            // is "tail_tip") — a schema drift the Worker didn't catch. The
            // spot goes, the four class traits the model got right do not.
            var body = Body.Replace(
                "[{\"place\":\"paw_left\",\"shade\":\"light\"}]",
                "[{\"place\":\"tail\",\"shade\":\"light\"}]");

            var traits = TraitsResponse.Read(body);

            Assert.That(traits, Is.Not.Null);
            Assert.That(traits.BaseColor, Is.EqualTo("ginger"));
            Assert.That(traits.Spots, Is.Empty);
        }

        [Test]
        public void ANullRequiredFieldIsAbsentNotTheNextKeysName()
        {
            // Before the fix, String() hunted for the next quote after the
            // colon and, finding none of its own, read the following key's
            // NAME ("pattern") as if it were base_color's value — which then
            // failed Allowed's check for the wrong reason. null must read as
            // simply absent, and a required field that is absent still means
            // no cat rather than a guess (see AMissingFieldGivesNull above).
            var body = Body.Replace("\"base_color\":\"ginger\"", "\"base_color\":null");

            Assert.That(TraitsResponse.Read(body), Is.Null);
        }

        [Test]
        public void ANullOptionalListDoesNotSwallowTheNextField()
        {
            // Before the fix, Strings() hunted forward for the next '[' and,
            // finding none of its own after `null`, took the "spots" array
            // that follows white_markings in the wire format instead. null
            // must mean no markings, and it must not eat the marks that come
            // after it.
            var body = Body.Replace(
                "\"white_markings\":[\"chest\",\"paws\"]", "\"white_markings\":null");

            var traits = TraitsResponse.Read(body);

            Assert.That(traits, Is.Not.Null);
            Assert.That(traits.WhiteMarkings, Is.Empty);
            Assert.That(traits.Spots.Count, Is.EqualTo(1));
            Assert.That(traits.Spots[0].Place, Is.EqualTo("paw_left"));
        }
    }
}
