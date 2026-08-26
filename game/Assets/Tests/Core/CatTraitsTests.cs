using System;
using System.IO;
using System.Linq;
using CatShelter.Core;
using NUnit.Framework;

namespace CatShelter.Core.Tests
{
    /// <summary>
    /// Tasks 50-photo/10 and /11: the two cats a player can end up with when
    /// the photograph path does not complete, and the guarantee that neither
    /// is a broken object.
    /// </summary>
    [TestFixture]
    public class CatTraitsTests
    {
        [Test]
        public void TheDefaultCatIsTheSameEveryTime()
        {
            // Two players who skipped must be able to talk about the same cat,
            // and a player who skips twice must not get two different ones.
            var first = CatTraits.Default;
            var second = CatTraits.Default;

            Assert.That(second.BaseColor, Is.EqualTo(first.BaseColor));
            Assert.That(second.Pattern, Is.EqualTo(first.Pattern));
            Assert.That(second.FurLength, Is.EqualTo(first.FurLength));
            Assert.That(second.EyeColor, Is.EqualTo(first.EyeColor));
            Assert.That(second.WhiteMarkings, Is.EqualTo(first.WhiteMarkings));
        }

        [Test]
        public void TheDefaultCatIsComplete_AndKnowsItWasSkipped()
        {
            var cat = CatTraits.Default;
            Assert.That(cat.Origin, Is.EqualTo(TraitsOrigin.Skipped));
            foreach (var value in new[] { cat.BaseColor, cat.Pattern, cat.FurLength, cat.EyeColor })
                Assert.That(value, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void TheOfflineCatKeepsHerRealColourAndDefaultsTheRest()
        {
            var cat = CatTraits.FromColourOnly("ginger");

            Assert.That(cat.BaseColor, Is.EqualTo("ginger"));
            // No on-device API reads a coat pattern, so it is forced rather
            // than guessed.
            Assert.That(cat.Pattern, Is.EqualTo("solid"));
            Assert.That(cat.Origin, Is.EqualTo(TraitsOrigin.OfflineColourOnly));
        }

        [Test]
        public void EveryPaletteColourCanBecomeAnOfflineCat()
        {
            foreach (var colour in CatTraits.Allowed["base_color"])
                Assert.DoesNotThrow(() => CatTraits.FromColourOnly(colour), colour);
        }

        [TestCase("orange")]
        [TestCase("")]
        [TestCase(null)]
        public void AColourOutsideThePaletteIsRefused(string colour)
        {
            Assert.Throws<ArgumentException>(() => CatTraits.FromColourOnly(colour));
        }

        [Test]
        public void RepeatedMarkingsAreRefused()
        {
            Assert.Throws<ArgumentException>(() => new CatTraits(
                "grey", "tabby", "short", "green", new[] { "chest", "chest" }));
        }

        [Test]
        public void TheAllowedValuesMatchTheWorkerSchema()
        {
            // tools/traits/schema.json is the single definition shared with the
            // Worker. If the two drift, the game draws a cat the Worker cannot
            // describe, or refuses one it can.
            var schemaPath = Path.GetFullPath(Path.Combine(
                TestContext.CurrentContext.TestDirectory,
                "..", "..", "..", "..", "..", "tools", "traits", "schema.json"));
            if (!File.Exists(schemaPath))
                Assert.Ignore($"schema not reachable from here: {schemaPath}");

            var json = File.ReadAllText(schemaPath);
            foreach (var pair in CatTraits.Allowed)
            {
                foreach (var value in pair.Value)
                    Assert.That(json, Does.Contain($"\"{value}\""),
                        $"{pair.Key}: '{value}' is missing from schema.json");
            }
        }
    }
}
