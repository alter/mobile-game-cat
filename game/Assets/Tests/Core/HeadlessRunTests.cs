using System;
using System.Linq;
using CatShelter.Core;
using CatShelter.Tests;
using NUnit.Framework;

namespace CatShelter.Core.Tests
{
    /// <summary>
    /// Acceptance 3.6: the game reads the shipped definitions; all 37 run
    /// through Core headlessly and end in a Win when played with sensible play.
    /// Levels load from Assets/Levels via the asset database (editor/test time).
    /// </summary>
    [TestFixture]
    public class HeadlessRunTests
    {
        [Test]
        public void AllShippedLevelFilesExistAndParse()
        {
            var levels = LevelLoader.LoadAllFromAssets();
            Assert.That(levels.Count, Is.EqualTo(37), "expected 37 level files");
        }

        [Test]
        public void RoomAndPileIndices_CoverThePacingCurve()
        {
            var levels = LevelLoader.LoadAllFromAssets();
            var perRoom = levels.GroupBy(l => l.RoomId)
                .ToDictionary(g => g.Key, g => g.Count());
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
        public void LockedKinds_AreValid()
        {
            // late rooms carry exactly one locked kind, in triples
            foreach (var level in LevelLoader.LoadAllFromAssets())
            {
                var lockedKinds = level.Pile
                    .Where(e => e.Item.LockedAfterTriples > 0)
                    .GroupBy(e => e.Item.Kind.Id).ToList();
                foreach (var g in lockedKinds)
                {
                    Assert.That(g.Count() % 3, Is.EqualTo(0),
                        $"{level.RoomId}/{level.PileIndex}: locked kind {g.Key}");
                    Assert.That(g.Select(e => e.Item.LockedAfterTriples).Distinct().Count(),
                        Is.EqualTo(1), "all copies share one threshold");
                }
            }
        }

        [Test]
        public void AllThirtySevenLevelsPlayThroughToWin_Headless()
        {
            foreach (var level in LevelLoader.LoadAllFromAssets())
            {
                var board = new Board(level);

                // Greedy sensible play: prefer kinds closest to completing,
                // same policy as the Python solver's heuristic.
                while (!board.IsOver)
                {
                    var avail = board.GetAvailable();
                    Assert.That(avail, Is.Not.Empty,
                        $"{level.RoomId}/{level.PileIndex}: stuck");
                    var shelfCounts = board.Shelf.Slots
                        .OfType<Item>().GroupBy(i => i.Kind.Id)
                        .ToDictionary(g => g.Key, g => g.Count());
                    var pick = avail.OrderByDescending(
                        i => shelfCounts.TryGetValue(i.Kind.Id, out var c) ? c : 0).First();
                    Assert.That(board.TakeItem(pick.Id), Is.True,
                        $"{level.RoomId}/{level.PileIndex}");
                }

                Assert.That(board.Outcome, Is.EqualTo(GameOutcome.Win),
                    $"{level.RoomId}/{level.PileIndex} did not end in a win");
            }
        }
    }
}
