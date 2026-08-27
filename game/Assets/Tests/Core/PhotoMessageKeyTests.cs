using System;
using System.IO;
using System.Linq;
using CatShelter.Core;
using NUnit.Framework;

namespace CatShelter.Core.Tests
{
    /// <summary>
    /// 50-photo/06 VERIFY item 1: the outcome->key mapping had no test at
    /// all because it lived in Shell. Moved to Core.PhotoMessageKey; this
    /// guards totality both ways — every outcome maps to a key, and every
    /// key exists in Shell/Copy.cs.
    /// </summary>
    [TestFixture]
    public class PhotoMessageKeyTests
    {
        [Test]
        public void EveryOutcomeMapsToANonEmptyKey()
        {
            foreach (PhotoOutcome outcome in Enum.GetValues(typeof(PhotoOutcome)))
                Assert.That(PhotoMessageKey.For(outcome), Is.Not.Null.And.Not.Empty,
                    outcome.ToString());
        }

        [Test]
        public void TheFourKeysAreDistinct()
        {
            var keys = Enum.GetValues(typeof(PhotoOutcome)).Cast<PhotoOutcome>()
                .Select(PhotoMessageKey.For).ToList();
            Assert.That(keys.Distinct().Count(), Is.EqualTo(keys.Count));
        }

        [Test]
        public void AFifthOutcomeWouldThrowRatherThanFallSilent()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => PhotoMessageKey.For((PhotoOutcome)999));
        }

        [Test]
        public void EveryKeyExistsInTheCopyTable()
        {
            var copyPath = Path.GetFullPath(Path.Combine(
                TestContext.CurrentContext.TestDirectory,
                "..", "..", "..", "..", "..",
                "game", "Assets", "Shell", "Copy.cs"));
            Assert.That(File.Exists(copyPath), Is.True,
                $"Copy.cs not found at {copyPath} — this cross-language check is not running.");

            var text = File.ReadAllText(copyPath);
            foreach (PhotoOutcome outcome in Enum.GetValues(typeof(PhotoOutcome)))
            {
                var key = PhotoMessageKey.For(outcome);
                Assert.That(text, Does.Contain($"[\"{key}\"]"),
                    $"{outcome}: '{key}' missing from Copy.cs");
            }
        }
    }
}
