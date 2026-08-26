using System;
using System.Collections.Generic;
using System.Linq;
using CatShelter.Core;
using CatShelter.Tests;
using NUnit.Framework;

namespace CatShelter.Core.Tests
{
    /// <summary>
    /// Task 60-shell-build/02, the half without art: rooms, piles, and how far
    /// through a room a level sits. Checked against the 37 levels the game
    /// actually ships, not against a fixture.
    /// </summary>
    [TestFixture]
    public class RoomPlanTests
    {
        private static RoomPlan Shipped() => new RoomPlan(LevelLoader.LoadAllFromAssets());

        [Test]
        public void TheHouseIsTwelveRoomsOfOneToFourPiles()
        {
            var plan = Shipped();
            Assert.That(plan.RoomCount, Is.EqualTo(12));
            Assert.That(plan.PilesPerRoomInOrder(),
                Is.EqualTo(new[] { 1, 2, 3, 3, 3, 3, 3, 3, 4, 4, 4, 4 }));
        }

        [Test]
        public void EveryRoomEndsOnExactlyOneLastPile()
        {
            var plan = Shipped();
            foreach (var room in LevelLoader.LoadAllFromAssets().GroupBy(l => l.RoomId))
            {
                var lasts = room.Count(plan.IsLastPileOfRoom);
                Assert.That(lasts, Is.EqualTo(1), room.Key);
            }
        }

        [Test]
        public void ClearedFractionRunsFromAFractionToExactlyOne()
        {
            var plan = Shipped();
            foreach (var room in LevelLoader.LoadAllFromAssets().GroupBy(l => l.RoomId))
            {
                var fractions = room.OrderBy(l => l.PileIndex)
                                    .Select(plan.ClearedFractionAfter).ToList();
                Assert.That(fractions.Last(), Is.EqualTo(1f).Within(0.001f),
                    $"{room.Key}: the last pile must leave the room clean");
                Assert.That(fractions, Is.Ordered.Ascending, room.Key);
                Assert.That(fractions.First(), Is.GreaterThan(0f), room.Key);
            }
        }

        [Test]
        public void TheOneRoomWithASinglePileIsCleanAfterIt()
        {
            var plan = Shipped();
            var first = LevelLoader.LoadAllFromAssets().Single(l => l.Number == 1);
            Assert.That(plan.PilesIn(first.RoomId), Is.EqualTo(1));
            Assert.That(plan.ClearedFractionAfter(first), Is.EqualTo(1f).Within(0.001f));
            Assert.That(plan.IsLastPileOfRoom(first), Is.True);
        }

        [Test]
        public void NextWalksThePlanInOrderAndStopsAtTheEnd()
        {
            var levels = LevelLoader.LoadAllFromAssets().OrderBy(l => l.Number).ToList();
            var plan = new RoomPlan(levels);

            Assert.That(plan.Next(levels[0]).Number, Is.EqualTo(2));
            Assert.That(plan.Next(levels[levels.Count - 1]), Is.Null,
                "the house ends rather than wrapping");
        }

        [Test]
        public void AGapInPileIndicesIsRejected()
        {
            // A missing index means a corner of the room no level ever clears,
            // and a "pile 2 of 3" that is really the last one.
            var entries = Enumerable.Range(1, 3)
                .Select(i => new PileEntry(new Item(i, new ItemKind("a", "a")), Array.Empty<int>()))
                .ToList();
            var levels = new[]
            {
                new Level(1, "room_01", 0, entries),
                new Level(2, "room_01", 2, entries),   // 1 is missing
            };

            Assert.Throws<ArgumentException>(() => new RoomPlan(levels));
        }

        [TestCase("room_07", 7)]
        [TestCase("room_12", 12)]
        [TestCase("room_01", 1)]
        [TestCase("", 0)]
        [TestCase(null, 0)]
        public void RoomNumberIsReadFromTheId(string roomId, int expected)
        {
            Assert.That(RoomPlan.RoomNumber(roomId), Is.EqualTo(expected));
        }

        [Test]
        public void ProgressWalksTheWholeHouseWithoutFallingOffTheEnd()
        {
            // PlayerProgress has existed since 20-rules-core and has never had
            // a caller. This is the walk the shell will do: clear every pile of
            // every room in order.
            var plan = Shipped();
            var progress = new PlayerProgress(plan.PilesPerRoomInOrder());

            foreach (var level in LevelLoader.LoadAllFromAssets().OrderBy(l => l.Number))
                progress.CompletePile(level.PileIndex);

            Assert.That(progress.RoomsDone.Count, Is.EqualTo(12));
            Assert.That(progress.CatState, Is.EqualTo(3), "third state after eight rooms");
        }

        [Test]
        public void TheCatChangesTwice_AfterTheFourthAndEighthRoom()
        {
            var plan = Shipped();
            var progress = new PlayerProgress(plan.PilesPerRoomInOrder());
            var states = new List<int> { progress.CatState };

            foreach (var level in LevelLoader.LoadAllFromAssets().OrderBy(l => l.Number))
            {
                progress.CompletePile(level.PileIndex);
                states.Add(progress.CatState);
            }

            // Exactly two transitions, however many levels those rooms took.
            var changes = states.Zip(states.Skip(1), (a, b) => a != b).Count(changed => changed);
            Assert.That(changes, Is.EqualTo(2));
        }
    }
}
