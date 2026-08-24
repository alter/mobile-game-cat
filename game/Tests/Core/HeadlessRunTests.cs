using System;
using System.IO;
using System.Linq;
using CatShelter.Core;
using CatShelter.Tests;
using NUnit.Framework;

namespace CatShelter.Core.Tests
{
    /// <summary>
    /// Acceptance 3.6: the game reads the shipped definitions; all 37 run
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
        public void AllShippedLevelFilesExistAndParse()
        {
            var files = Directory.GetFiles(LevelsDir(), "l*.json");
            Assert.That(files.Length, Is.EqualTo(37), "expected 37 level files");
            Assert.DoesNotThrow(() =>
            {
                foreach (var f in files)
                    _ = LevelLoader.FromJson(File.ReadAllText(f));
            });
        }

        [Test]
        public void RoomAndPileIndices_CoverThePacingCurve()
        {
            var files = Directory.GetFiles(LevelsDir(), "l*.json");
            var perRoom = files
                .Select(f => LevelLoader.FromJson(File.ReadAllText(f)))
                .GroupBy(l => l.RoomId)
                .ToDictionary(g => g.Key, g => g.Count());
            // rooms hold 1..4 piles; every room is present exactly once
            Assert.That(perRoom.Count, Is.EqualTo(12));
            Assert.That(perRoom.Values.Max(), Is.EqualTo(4));
            Assert.That(perRoom["room_01"], Is.EqualTo(1));
            Assert.That(perRoom["room_09"], Is.EqualTo(4));
        }

        [Test]
        public void CorruptedJsonFailsLoudly()
        {
            Assert.Throws<Newtonsoft.Json.JsonReaderException>(
                () => LevelLoader.FromJson("{ not json "));
        }

        [Test]
        public void AllThirtySevenLevelsPlayThroughToWin_Headless()
        {
            var levelsDir = LevelsDir();
            foreach (var file in Directory.GetFiles(levelsDir, "l*.json"))
            {
                var level = LevelLoader.FromJson(File.ReadAllText(file));
                var board = new Board(level);

                // Greedy sensible play: prefer kinds closest to completing,
                // same policy as the Python solver's heuristic.
                while (!board.IsOver)
                {
                    var avail = board.GetAvailable();
                    Assert.That(avail, Is.Not.Empty, $"{file}: stuck");
                    var shelfCounts = board.Shelf.Slots
                        .OfType<Item>().GroupBy(i => i.Kind.Id)
                        .ToDictionary(g => g.Key, g => g.Count());
                    var pick = avail.OrderByDescending(
                        i => shelfCounts.TryGetValue(i.Kind.Id, out var c) ? c : 0).First();
                    Assert.That(board.TakeItem(pick.Id), Is.True, file);
                }

                Assert.That(board.Outcome, Is.EqualTo(GameOutcome.Win),
                    $"{file} did not end in a win");
            }
        }
    }
}
