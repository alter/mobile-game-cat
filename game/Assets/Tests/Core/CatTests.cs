using System;
using CatShelter.Core;
using NUnit.Framework;

namespace CatShelter.Core.Tests
{
    /// <summary>
    /// Task 50-photo/10: the naming half of the skip OUTCOME. A default cat
    /// must be "named", including when nobody ever types a name — a blank
    /// or absent name is not a broken cat, it is DefaultName.
    /// </summary>
    [TestFixture]
    public class CatTests
    {
        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void ABlankOrMissingName_BecomesTheDefault(string typed)
        {
            var cat = new Cat(typed, CatTraits.Default);
            Assert.That(cat.Name, Is.EqualTo(Cat.DefaultName));
        }

        [Test]
        public void ATypedName_IsKept_AndTrimmed()
        {
            var cat = new Cat("  Marmalade  ", CatTraits.Default);
            Assert.That(cat.Name, Is.EqualTo("Marmalade"));
        }

        [Test]
        public void ANullTraits_IsRefused()
        {
            Assert.Throws<ArgumentNullException>(() => new Cat("Marmalade", null));
        }

        [Test]
        public void SkippedIsNamedAndComplete()
        {
            var cat = Cat.Skipped;
            Assert.That(cat.Name, Is.EqualTo(Cat.DefaultName));
            Assert.That(cat.Traits, Is.Not.Null);
            Assert.That(cat.Traits.Origin, Is.EqualTo(TraitsOrigin.Skipped));
        }

        [Test]
        public void SkippedIsTheSameCatEveryTime()
        {
            // The same guarantee CatTraits.Default already makes about the
            // coat (CatTraitsTests.TheDefaultCatIsTheSameEveryTime), extended
            // to the name: two players who skip must be able to talk about
            // the same named kitten.
            var first = Cat.Skipped;
            var second = Cat.Skipped;

            Assert.That(second.Name, Is.EqualTo(first.Name));
            Assert.That(second.Traits.BaseColor, Is.EqualTo(first.Traits.BaseColor));
            Assert.That(second.Traits.Pattern, Is.EqualTo(first.Traits.Pattern));
            Assert.That(second.Traits.FurLength, Is.EqualTo(first.Traits.FurLength));
            Assert.That(second.Traits.EyeColor, Is.EqualTo(first.Traits.EyeColor));
        }
    }
}
