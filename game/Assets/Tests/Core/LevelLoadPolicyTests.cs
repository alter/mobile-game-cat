using System;
using System.Collections.Generic;
using System.Linq;
using CatShelter.Core;
using NUnit.Framework;

namespace CatShelter.Core.Tests
{
    /// <summary>
    /// Task 30-levels-solver/06: what happens when a shipped level file is
    /// missing or fails to parse. Added after this task's own `verify:passed`
    /// — see NOTES.md's dated correction. Landing in Core, not View, because
    /// `LevelAssets` cannot be unit-tested (it touches `Resources.Load`); the
    /// decision it wraps can be.
    /// </summary>
    [TestFixture]
    public class LevelLoadPolicyTests
    {
        private static PileEntry E(int id, string kind) =>
            new(new Item(id, new ItemKind(kind, kind)), Array.Empty<int>());

        private static Level Level(int number, string roomId, int pileIndex) => new(
            number, roomId, pileIndex,
            new List<PileEntry> { E(1, "a"), E(2, "a"), E(3, "a") });

        [Test]
        public void EveryFileParsed_KeepsEverything_NoRoomsIncomplete()
        {
            var parsed = new[]
            {
                Level(1, "room_01", 0),
                Level(2, "room_02", 0), Level(3, "room_02", 1),
            };
            var expected = new Dictionary<string, int> { ["room_01"] = 1, ["room_02"] = 2 };

            var result = LevelLoadPolicy.Resolve(parsed, expected);

            Assert.That(result.CanStart, Is.True);
            Assert.That(result.Levels.Select(l => l.Number), Is.EqualTo(new[] { 1, 2, 3 }));
            Assert.That(result.IncompleteRooms, Is.Empty);
        }

        [Test]
        public void AGapInTheMiddleOfARoom_DropsTheWholeRoom_KeepsOthers()
        {
            // room_09 should have piles 0,1,2,3 - pile 1's file failed to parse
            // and never made it into `parsed`.
            var parsed = new[]
            {
                Level(1, "room_01", 0),
                Level(2, "room_09", 0), Level(3, "room_09", 2), Level(4, "room_09", 3),
            };
            var expected = new Dictionary<string, int> { ["room_01"] = 1, ["room_09"] = 4 };

            var result = LevelLoadPolicy.Resolve(parsed, expected);

            Assert.That(result.CanStart, Is.True);
            Assert.That(result.Levels.Select(l => l.Number), Is.EqualTo(new[] { 1 }));
            Assert.That(result.IncompleteRooms, Is.EqualTo(new[] { "room_09" }));
        }

        [Test]
        public void AMissingTrailingPile_StillDropsTheRoom()
        {
            // room_09 has piles 0,1,2 present and gapless from zero - but it
            // was supposed to have 4. A count-only check would wrongly accept
            // this as a complete 3-pile room.
            var parsed = new[]
            {
                Level(1, "room_01", 0),
                Level(2, "room_09", 0), Level(3, "room_09", 1), Level(4, "room_09", 2),
            };
            var expected = new Dictionary<string, int> { ["room_01"] = 1, ["room_09"] = 4 };

            var result = LevelLoadPolicy.Resolve(parsed, expected);

            Assert.That(result.Levels.Select(l => l.Number), Is.EqualTo(new[] { 1 }));
            Assert.That(result.IncompleteRooms, Is.EqualTo(new[] { "room_09" }));
        }

        [Test]
        public void ARoomEntirelyMissing_IsReportedIncomplete_NotSilentlySkipped()
        {
            var parsed = new[] { Level(1, "room_01", 0) };
            var expected = new Dictionary<string, int> { ["room_01"] = 1, ["room_02"] = 2 };

            var result = LevelLoadPolicy.Resolve(parsed, expected);

            Assert.That(result.IncompleteRooms, Is.EqualTo(new[] { "room_02" }));
        }

        [Test]
        public void NothingParsedAtAll_CannotStart()
        {
            var expected = new Dictionary<string, int> { ["room_01"] = 1, ["room_02"] = 2 };

            var result = LevelLoadPolicy.Resolve(Array.Empty<Level>(), expected);

            Assert.That(result.CanStart, Is.False);
            Assert.That(result.Levels, Is.Empty);
            Assert.That(result.IncompleteRooms, Is.EqualTo(new[] { "room_01", "room_02" }));
        }

        [Test]
        public void KeptLevels_AreOrderedByNumber_RegardlessOfInputOrder()
        {
            var parsed = new[]
            {
                Level(3, "room_02", 1), Level(1, "room_01", 0), Level(2, "room_02", 0),
            };
            var expected = new Dictionary<string, int> { ["room_01"] = 1, ["room_02"] = 2 };

            var result = LevelLoadPolicy.Resolve(parsed, expected);

            Assert.That(result.Levels.Select(l => l.Number), Is.EqualTo(new[] { 1, 2, 3 }));
        }

        [Test]
        public void NullParsedList_IsRefused()
        {
            var expected = new Dictionary<string, int>();
            Assert.Throws<ArgumentNullException>(
                () => LevelLoadPolicy.Resolve(null, expected));
        }

        [Test]
        public void NullExpectedMap_IsRefused()
        {
            Assert.Throws<ArgumentNullException>(
                () => LevelLoadPolicy.Resolve(Array.Empty<Level>(), null));
        }
    }
}
