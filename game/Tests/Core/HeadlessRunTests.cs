using System;
using System.IO;
using System.Linq;
using CatShelter.Core;
using CatShelter.Tests;
using NUnit.Framework;

namespace CatShelter.Core.Tests
{
    /// <summary>
    /// Acceptance 3.6: the game reads the shipped definitions; all 12 run
    /// through Core headlessly and end in a Win when played with sensible play.
    /// </summary>
    [TestFixture]
    public class HeadlessRunTests
    {
        private static string LevelsDir()
        {
            // Tests run from game/Tests/Core/bin/...; the repo root is six up.
            var dir = AppContext.BaseDirectory;
            for (int i = 0; i < 7; i++)
                dir = Path.GetDirectoryName(dir)!;
            return Path.Combine(dir, "game", "Assets", "Levels");
        }

        [Test]
        public void TwelveShippedLevelsExistAndParse()
        {
            var files = Enumerable.Range(1, 12)
                .Select(n => Path.Combine(LevelsDir(), $"level_{n:00}.json"))
                .ToList();
            foreach (var f in files)
                Assert.That(File.Exists(f), Is.True, f);
            Assert.DoesNotThrow(() =>
            {
                foreach (var f in files)
                    _ = LevelLoader.FromJson(File.ReadAllText(f));
            });
        }

        [Test]
        public void CorruptedJsonFailsLoudly()
        {
            Assert.Throws<Newtonsoft.Json.JsonReaderException>(
                () => LevelLoader.FromJson("{ not json "));
        }

        [Test]
        public void AllTwelveLevelsPlayThroughToWin_Headless()
        {
            var levelsDir = LevelsDir();
            for (int n = 1; n <= 12; n++)
            {
                var level = LevelLoader.FromJson(
                    File.ReadAllText(Path.Combine(levelsDir, $"level_{n:00}.json")));
                var board = new Board(level);

                // Greedy sensible play: prefer kinds closest to completing,
                // same policy as the Python solver's heuristic.
                while (!board.IsOver)
                {
                    var avail = board.GetAvailable();
                    Assert.That(avail, Is.Not.Empty, $"level {n}: stuck");
                    var shelfCounts = board.Shelf.Slots
                        .OfType<Item>().GroupBy(i => i.Kind.Id)
                        .ToDictionary(g => g.Key, g => g.Count());
                    var pick = avail.OrderByDescending(
                        i => shelfCounts.TryGetValue(i.Kind.Id, out var c) ? c : 0).First();
                    Assert.That(board.TakeItem(pick.Id), Is.True, $"level {n}");
                }

                Assert.That(board.Outcome, Is.EqualTo(GameOutcome.Win),
                    $"level {n} did not end in a win");
            }
        }
    }
}
