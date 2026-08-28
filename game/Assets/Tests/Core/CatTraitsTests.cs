using System;
using System.Collections.Generic;
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
            // Fail, not Assert.Ignore. This used to skip itself when the path
            // did not resolve, which means the one check standing between the
            // game's vocabulary and the Worker's could quietly stop running
            // while the suite still reported green — the same shape of defect
            // as the coverage gate nobody invoked (tasks/AUDIT-2026-08-27.md,
            // item 4). A missing schema is a finding, not a reason to pass.
            Assert.That(File.Exists(schemaPath), Is.True,
                $"tools/traits/schema.json not found at {schemaPath} — either the "
                + "repository layout moved or this walk-up is wrong, and this "
                + "cross-language check is not running.");

            var json = File.ReadAllText(schemaPath);
            foreach (var pair in CatTraits.Allowed)
            {
                foreach (var value in pair.Value)
                    Assert.That(json, Does.Contain($"\"{value}\""),
                        $"{pair.Key}: '{value}' is missing from schema.json");
            }
        }

        // --- a cat of one's own ------------------------------------------
        //
        // The owner asked for a different kitten per player so that a shared
        // picture says something. These pin the two properties that makes
        // depend on: the same player always gets the same cat, and every cat
        // rolled is one the rest of the game can actually draw.

        [Test]
        public void Roll_IsDeterministic()
        {
            var a = CatTraits.Roll(12345);
            var b = CatTraits.Roll(12345);
            Assert.That(a.BaseColor, Is.EqualTo(b.BaseColor));
            Assert.That(a.Pattern, Is.EqualTo(b.Pattern));
            Assert.That(a.FurLength, Is.EqualTo(b.FurLength));
            Assert.That(a.EyeColor, Is.EqualTo(b.EyeColor));
            Assert.That(a.WhiteMarkings, Is.EquivalentTo(b.WhiteMarkings));
        }

        [Test]
        public void Roll_OnlyEverProducesAllowedValues()
        {
            for (int seed = 0; seed < 500; seed++)
            {
                var c = CatTraits.Roll(seed);
                Assert.That(CatTraits.Allowed["base_color"], Contains.Item(c.BaseColor));
                Assert.That(CatTraits.Allowed["pattern"], Contains.Item(c.Pattern));
                Assert.That(CatTraits.Allowed["fur_length"], Contains.Item(c.FurLength));
                Assert.That(CatTraits.Allowed["eye_color"], Contains.Item(c.EyeColor));
                foreach (var m in c.WhiteMarkings)
                    Assert.That(CatTraits.Allowed["white_markings"], Contains.Item(m));
            }
        }

        [Test]
        public void Roll_ActuallyVaries()
        {
            // The point of the feature. If a thousand players saw four cats
            // between them the feature would be pointless, and a bug that
            // collapsed the roll would otherwise pass every test above.
            var seen = new HashSet<string>();
            for (int seed = 0; seed < 1000; seed++)
            {
                var c = CatTraits.Roll(seed);
                seen.Add($"{c.BaseColor}/{c.Pattern}/{c.FurLength}/{c.EyeColor}/" +
                         string.Join(",", c.WhiteMarkings));
            }
            Assert.That(seen.Count, Is.GreaterThan(200),
                        $"only {seen.Count} distinct cats in 1000 rolls");
        }

        [Test]
        public void Roll_SurvivesTheSaveFormat()
        {
            // A rolled cat is written to disk on first launch and read back on
            // every launch after. A trait that does not survive that round trip
            // would give the player a different cat every time they opened the
            // game.
            for (int seed = 0; seed < 50; seed++)
            {
                var rolled = CatTraits.Roll(seed);
                var cat = new Cat("Kitty", rolled);
                var back = CatSave.Read(CatSave.Write(cat));
                Assert.That(back, Is.Not.Null, $"seed {seed} did not round-trip");
                Assert.That(back.Traits.BaseColor, Is.EqualTo(rolled.BaseColor));
                Assert.That(back.Traits.Pattern, Is.EqualTo(rolled.Pattern));
                Assert.That(back.Traits.FurLength, Is.EqualTo(rolled.FurLength));
                Assert.That(back.Traits.EyeColor, Is.EqualTo(rolled.EyeColor));
                Assert.That(back.Traits.WhiteMarkings,
                            Is.EquivalentTo(rolled.WhiteMarkings), $"seed {seed}");
            }
        }
    }
}
