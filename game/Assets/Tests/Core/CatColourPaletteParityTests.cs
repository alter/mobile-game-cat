using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CatShelter.Core;
using NUnit.Framework;

namespace CatShelter.Core.Tests
{
    /// <summary>
    /// Task 50-photo/11: <c>CatColour.swift</c> names the base colour it read
    /// off a photo, and <see cref="CatTraits.Allowed"/>["base_color"] decides
    /// which names are legal — two copies of one set of strings, the same
    /// shape as <c>CatTraitsTests.TheAllowedValuesMatchTheWorkerSchema</c>
    /// guards for the Worker. Nothing kept them in sync: a name Swift returns
    /// that <see cref="CatTraits"/> does not recognise throws inside
    /// <c>CatTraits.FromColourOnly</c>, which used to be able to escape
    /// <c>CaptureScreen.Handle</c> uncaught. The catch there is now the last
    /// line of defence; this test is the first — it should never need to fire.
    /// </summary>
    [TestFixture]
    public class CatColourPaletteParityTests
    {
        [Test]
        public void SwiftPaletteMatchesCatTraitsAllowed()
        {
            var swiftPath = Path.GetFullPath(Path.Combine(
                TestContext.CurrentContext.TestDirectory,
                "..", "..", "..", "..", "..",
                "game", "Assets", "Plugins", "iOS", "CatColour.swift"));

            // Fail, not Assert.Ignore: a missing file must not let this check
            // quietly stop running while the suite still reports green — the
            // same defect the coverage gate had (tasks/AUDIT-2026-08-27.md,
            // item 4) and CatTraitsTests already fails loudly for, not skips.
            Assert.That(File.Exists(swiftPath), Is.True,
                $"CatColour.swift not found at {swiftPath} — either the "
                + "repository layout moved or this walk-up is wrong, and this "
                + "cross-language check is not running.");

            var source = File.ReadAllText(swiftPath);

            var start = source.IndexOf("static let palette", StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0),
                "CatColour.swift no longer declares `static let palette` — " +
                "this test's anchor moved with it.");
            var arrayStart = source.IndexOf('[', source.IndexOf('=', start));
            var arrayEnd = source.IndexOf(']', arrayStart);
            var block = source.Substring(arrayStart, arrayEnd - arrayStart);

            var swiftNames = Regex.Matches(block, "\\(\"(\\w+)\"")
                .Select(m => m.Groups[1].Value)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToArray();
            var coreNames = CatTraits.Allowed["base_color"]
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToArray();

            Assert.That(swiftNames, Is.Not.Empty,
                "no palette entries parsed out of CatColour.swift — the " +
                "tuple shape changed and this regex no longer matches it.");
            Assert.That(swiftNames, Is.EqualTo(coreNames),
                "CatColour.swift's palette and CatTraits.Allowed[\"base_color\"] "
                + "have drifted: a name only one side knows about is exactly "
                + "what makes CatTraits.FromColourOnly throw.");

            // And the managed copy the estimate actually matches against.
            Assert.That(CoatPalette.Names.OrderBy(n => n, StringComparer.Ordinal).ToArray(),
                Is.EqualTo(coreNames),
                "CoatPalette and CatTraits.Allowed[\"base_color\"] have drifted.");
        }

        /// <summary>
        /// The six ANCHORS, and not just the six names.
        ///
        /// <c>CatColour.swift</c>'s own comment says why this check did not
        /// exist: "what is still duplicated is the six ANCHORS … and nothing
        /// can check those: they are numbers, not names, and a wrong one is a
        /// worse guess rather than an exception." That was true while the
        /// managed copy was a private array inside <c>Shell/CatColour.cs</c>,
        /// which <c>core-tests</c> does not compile. It stopped being true when
        /// the numbers moved to <see cref="CoatPalette"/> in Core. A drifted
        /// anchor now fails the suite instead of quietly renaming cats on one
        /// platform — which is a defect whose only symptom would have been two
        /// phones disagreeing about the same animal.
        /// </summary>
        [Test]
        public void SwiftPaletteAnchorsMatchCoatPalette()
        {
            var block = PaletteBlock();
            var matches = Regex.Matches(
                block,
                "\\(\"(\\w+)\"\\s*,\\s*([0-9.]+)\\s*,\\s*([0-9.]+)\\s*,\\s*([0-9.]+)");

            Assert.That(matches.Count, Is.EqualTo(CoatPalette.Entries.Length),
                $"parsed {matches.Count} anchors out of CatColour.swift and "
                + $"CoatPalette has {CoatPalette.Entries.Length} — either one "
                + "side gained an entry or the tuple shape changed under this "
                + "regex, and in the second case the check is not running.");

            foreach (Match match in matches)
            {
                var name = match.Groups[1].Value;
                var entry = CoatPalette.Entries.FirstOrDefault(e => e.Name == name);
                Assert.That(entry.Name, Is.EqualTo(name),
                    $"CatColour.swift has an anchor '{name}' that CoatPalette "
                    + "does not.");

                var swift = new[]
                {
                    double.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture),
                    double.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture),
                    double.Parse(match.Groups[4].Value, CultureInfo.InvariantCulture),
                };
                Assert.That(swift, Is.EqualTo(new[] { entry.R, entry.G, entry.B }).Within(1e-9),
                    $"the '{name}' anchor differs between CatColour.swift and "
                    + "CoatPalette. An iPhone and an Android phone would name "
                    + "the same cat differently, and nothing else would ever "
                    + "say so.");
            }
        }

        /// <summary>The palette literal out of <c>CatColour.swift</c>.</summary>
        private static string PaletteBlock()
        {
            var swiftPath = Path.GetFullPath(Path.Combine(
                TestContext.CurrentContext.TestDirectory,
                "..", "..", "..", "..", "..",
                "game", "Assets", "Plugins", "iOS", "CatColour.swift"));

            Assert.That(File.Exists(swiftPath), Is.True,
                $"CatColour.swift not found at {swiftPath} — either the "
                + "repository layout moved or this walk-up is wrong, and this "
                + "cross-language check is not running.");

            var source = File.ReadAllText(swiftPath);
            var start = source.IndexOf("static let palette", StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0),
                "CatColour.swift no longer declares `static let palette`.");
            var arrayStart = source.IndexOf('[', source.IndexOf('=', start));
            var arrayEnd = source.IndexOf(']', arrayStart);
            return source.Substring(arrayStart, arrayEnd - arrayStart);
        }
    }
}
