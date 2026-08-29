using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CatShelter.Core;
using Newtonsoft.Json.Linq;
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

            var schema = JObject.Parse(File.ReadAllText(schemaPath));

            // Where each of CatTraits.Allowed's keys actually lives in the
            // schema, spelled out explicitly rather than assumed. The five
            // scalar fields carry their enum directly on the property;
            // white_markings is an array, so its enum sits on `items`;
            // spot_place and spot_shade are nested two levels deeper still,
            // inside `spots.items.properties`, because a spot is an object
            // with two fields rather than one bare string. A naive
            // Does.Contain(value) scan of the raw text used to stand in for
            // this and never actually walked the tree — it would have passed
            // even if `spot_place`'s values were only ever a substring of
            // some other, unrelated array.
            var schemaPaths = new Dictionary<string, string>
            {
                ["base_color"] = "properties.base_color.enum",
                ["pattern"] = "properties.pattern.enum",
                ["fur_length"] = "properties.fur_length.enum",
                ["eye_color"] = "properties.eye_color.enum",
                ["white_markings"] = "properties.white_markings.items.enum",
                ["spot_place"] = "properties.spots.items.properties.place.enum",
                ["spot_shade"] = "properties.spots.items.properties.shade.enum",
            };

            foreach (var pair in CatTraits.Allowed)
            {
                Assert.That(schemaPaths.ContainsKey(pair.Key), Is.True,
                    $"{pair.Key}: this test does not know where schema.json "
                    + "keeps it — add it to schemaPaths above.");

                var path = schemaPaths[pair.Key];
                var node = schema.SelectToken(path);
                Assert.That(node, Is.Not.Null,
                    $"{pair.Key}: schema.json has no node at '{path}' — its "
                    + "shape moved and this test's path needs to follow.");

                var schemaValues = node.Select(t => (string)t).ToArray();

                // CatTraits.Allowed must not accept anything the Worker
                // cannot describe...
                foreach (var value in pair.Value)
                    Assert.That(schemaValues, Contains.Item(value),
                        $"{pair.Key}: '{value}' is in CatTraits.Allowed but "
                        + "missing from schema.json");
                // ...and must not refuse anything the Worker can send.
                foreach (var value in schemaValues)
                    Assert.That(pair.Value, Contains.Item(value),
                        $"{pair.Key}: schema.json allows '{value}' but "
                        + $"CatTraits.Allowed[\"{pair.Key}\"] does not");
            }
        }

        // --- spots: her distinctive marks ---------------------------------
        //
        // See CatSpot for why these exist: every other field is a class
        // characteristic shared with hundreds of other cats, and a mark in an
        // asymmetric place is the one thing that actually identifies her.
        // CatTraits enforces at most two and no two in the same place;
        // CatSpot enforces that place and shade are each one of the allowed
        // values. These tests pin both.

        [Test]
        public void NoSpotsPassed_LeavesTheListEmpty()
        {
            var cat = new CatTraits(
                "grey", "tabby", "short", "green", Array.Empty<string>());
            Assert.That(cat.Spots, Is.Empty);
        }

        [Test]
        public void TheDefaultCatHasNoSpots()
        {
            Assert.That(CatTraits.Default.Spots, Is.Empty);
        }

        [Test]
        public void OneSpot_IsAcceptedAndRoundTrips()
        {
            var spot = new CatSpot("chest", "dark");
            var cat = new CatTraits("grey", "tabby", "short", "green",
                Array.Empty<string>(), TraitsOrigin.Photo, new[] { spot });

            Assert.That(cat.Spots.Count, Is.EqualTo(1));
            Assert.That(cat.Spots[0].Place, Is.EqualTo("chest"));
            Assert.That(cat.Spots[0].Shade, Is.EqualTo("dark"));
        }

        [Test]
        public void TwoSpots_AreAcceptedAndRoundTrip()
        {
            var spots = new[]
            {
                new CatSpot("chest", "dark"),
                new CatSpot("tail_tip", "light"),
            };
            var cat = new CatTraits("grey", "tabby", "short", "green",
                Array.Empty<string>(), TraitsOrigin.Photo, spots);

            Assert.That(cat.Spots.Count, Is.EqualTo(2));
            Assert.That(cat.Spots[0], Is.EqualTo(spots[0]));
            Assert.That(cat.Spots[1], Is.EqualTo(spots[1]));
        }

        [Test]
        public void ThreeSpots_AreRefused()
        {
            var spots = new[]
            {
                new CatSpot("chest", "dark"),
                new CatSpot("tail_tip", "light"),
                new CatSpot("chin", "dark"),
            };
            Assert.Throws<ArgumentException>(() => new CatTraits(
                "grey", "tabby", "short", "green", Array.Empty<string>(),
                TraitsOrigin.Photo, spots));
        }

        [Test]
        public void TwoSpotsInTheSamePlace_AreRefused_EvenWithDifferentShades()
        {
            var spots = new[]
            {
                new CatSpot("chest", "dark"),
                new CatSpot("chest", "light"),
            };
            Assert.Throws<ArgumentException>(() => new CatTraits(
                "grey", "tabby", "short", "green", Array.Empty<string>(),
                TraitsOrigin.Photo, spots));
        }

        [Test]
        public void TwoSpotsInDifferentPlaces_AreFine_EvenWithTheSameShade()
        {
            var spots = new[]
            {
                new CatSpot("chest", "dark"),
                new CatSpot("tail_tip", "dark"),
            };
            Assert.DoesNotThrow(() => new CatTraits(
                "grey", "tabby", "short", "green", Array.Empty<string>(),
                TraitsOrigin.Photo, spots));
        }

        [TestCase("nose")]
        [TestCase("")]
        [TestCase(null)]
        public void AnUnknownOrMissingSpotPlace_IsRefused(string place)
        {
            Assert.Throws<ArgumentException>(() => new CatSpot(place, "light"));
        }

        [TestCase("bright")]
        [TestCase("")]
        [TestCase(null)]
        public void AnUnknownOrMissingSpotShade_IsRefused(string shade)
        {
            Assert.Throws<ArgumentException>(() => new CatSpot("chest", shade));
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
