using System;
using System.Collections.Generic;
using System.Linq;
using CatShelter.Core;
using NUnit.Framework;

namespace CatShelter.Core.Tests
{
    /// <summary>
    /// Tasks 6.2/6.2.1 data model: rooms of several piles, progress cursor,
    /// cat states anchored to completed rooms (1,2,3,3,3,3,3,3,4,4,4,4 curve).
    /// </summary>
    [TestFixture]
    public class PlayerProgressTests
    {
        private static readonly int[] Curve = { 1, 2, 3, 3, 3, 3, 3, 3, 4, 4, 4, 4 };

        private PlayerProgress Make() => new(Curve);

        [Test]
        public void StartsAtRoomOnePileZero()
        {
            var p = Make();
            Assert.That(p.CurrentRoom, Is.EqualTo(1));
            Assert.That(p.CurrentPile, Is.EqualTo(0));
        }

        [Test]
        public void CompletingPilesAdvancesWithinRoom()
        {
            var p = Make();
            p.CompletePile(0);          // room 1 has one pile → room completes
            Assert.That(p.IsRoomDone(1), Is.True);
            Assert.That(p.CurrentRoom, Is.EqualTo(2));
            Assert.That(p.CurrentPile, Is.EqualTo(0));

            p.CompletePile(0);          // room 2 pile 0
            Assert.That(p.CurrentRoom, Is.EqualTo(2));   // still room 2
            p.CompletePile(1);          // last pile of room 2
            Assert.That(p.IsRoomDone(2), Is.True);
            Assert.That(p.CurrentRoom, Is.EqualTo(3));
        }

        [Test]
        public void WrongPileIndex_Rejected()
        {
            var p = Make();
            Assert.Throws<InvalidOperationException>(() => p.CompletePile(1));
        }

        [Test]
        public void CatState_AnchorsToRooms_FourAndEight()
        {
            var p = Make();
            Assert.That(p.CatState, Is.EqualTo(1));
            for (int i = 0; i < 4; i++) CompleteRoom(p, i + 1);
            Assert.That(p.CatState, Is.EqualTo(2));
            for (int i = 4; i < 8; i++) CompleteRoom(p, i + 1);
            Assert.That(p.CatState, Is.EqualTo(3));
        }

        [Test]
        public void WholeHouseCompletes_WithoutCrash()
        {
            var p = Make();
            // complete all 37 piles
            for (int room = 1; room <= 12; room++)
                for (int pile = 0; pile < Curve[room - 1]; pile++)
                    p.CompletePile(p.CurrentRoom == room ? p.CurrentPile : 0);
            Assert.That(p.RoomsDone.Count, Is.EqualTo(12));
            Assert.That(p.CatState, Is.EqualTo(3));
        }

        private static void CompleteRoom(PlayerProgress p, int room)
        {
            while (p.CurrentRoom == room && !p.IsRoomDone(room))
                p.CompletePile(p.CurrentPile);
        }

        // --- 60-shell-build/03: house map derivation --------------------

        [Test]
        public void PilesClearedIn_UntouchedRoom_IsZero()
        {
            var p = Make();
            Assert.That(p.PilesClearedIn(5), Is.EqualTo(0));
            Assert.That(p.PilesClearedIn(12), Is.EqualTo(0));
        }

        [Test]
        public void PilesClearedIn_CurrentRoom_TracksCursor()
        {
            var p = Make();          // room 3 has 3 piles
            CompleteRoom(p, 1);
            CompleteRoom(p, 2);
            Assert.That(p.CurrentRoom, Is.EqualTo(3));
            Assert.That(p.PilesClearedIn(3), Is.EqualTo(0));
            p.CompletePile(0);
            Assert.That(p.PilesClearedIn(3), Is.EqualTo(1));
            p.CompletePile(1);
            Assert.That(p.PilesClearedIn(3), Is.EqualTo(2));
        }

        [Test]
        public void PilesClearedIn_DoneRoom_IsFull()
        {
            var p = Make();
            CompleteRoom(p, 1);
            Assert.That(p.PilesClearedIn(1), Is.EqualTo(Curve[0]));
        }

        [Test]
        public void PilesClearedIn_OutOfRange_IsZero()
        {
            var p = Make();
            Assert.That(p.PilesClearedIn(0), Is.EqualTo(0));
            Assert.That(p.PilesClearedIn(13), Is.EqualTo(0));
        }

        [Test]
        public void CellStateFor_UntouchedRoom_IsDirty()
        {
            var p = Make();
            Assert.That(p.CellStateFor(9), Is.EqualTo(RoomCellState.Dirty));
        }

        [Test]
        public void CellStateFor_PartlyCleared_IsPartial()
        {
            var p = Make();          // room 9 has 4 piles
            for (int room = 1; room < 9; room++) CompleteRoom(p, room);
            p.CompletePile(0);
            p.CompletePile(1);
            Assert.That(p.CellStateFor(9), Is.EqualTo(RoomCellState.Partial));
        }

        [Test]
        public void CellStateFor_FinishedRoom_IsClean()
        {
            var p = Make();
            CompleteRoom(p, 1);
            Assert.That(p.CellStateFor(1), Is.EqualTo(RoomCellState.Clean));
        }

        [Test]
        public void CellStateFor_SinglePileRoom_SkipsPartial()
        {
            // Room 1 holds exactly one pile: it can only be dirty or clean,
            // never partial - clearing its only pile finishes it.
            var p = Make();
            Assert.That(p.CellStateFor(1), Is.EqualTo(RoomCellState.Dirty));
            p.CompletePile(0);
            Assert.That(p.CellStateFor(1), Is.EqualTo(RoomCellState.Clean));
        }

        [Test]
        public void Restore_MatchesReplayedState()
        {
            var replayed = Make();
            CompleteRoom(replayed, 1);
            CompleteRoom(replayed, 2);
            replayed.CompletePile(0); // room 3, pile 0 of 3

            var restored = PlayerProgress.Restore(Curve, replayed.CurrentRoom,
                replayed.CurrentPile, replayed.RoomsDone);

            Assert.That(restored.CurrentRoom, Is.EqualTo(replayed.CurrentRoom));
            Assert.That(restored.CurrentPile, Is.EqualTo(replayed.CurrentPile));
            Assert.That(restored.RoomsDone, Is.EquivalentTo(replayed.RoomsDone));
            for (int room = 1; room <= Curve.Length; room++)
                Assert.That(restored.CellStateFor(room),
                    Is.EqualTo(replayed.CellStateFor(room)), $"room {room}");
        }

        [Test]
        public void Restore_FreshGame_IsAllDirty()
        {
            var restored = PlayerProgress.Restore(Curve, 1, 0, new List<int>());
            for (int room = 1; room <= Curve.Length; room++)
                Assert.That(restored.CellStateFor(room), Is.EqualTo(RoomCellState.Dirty));
        }

        [Test]
        public void Restore_RejectsCursorOutsideRoomCount()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => PlayerProgress.Restore(Curve, 13, 0, new List<int>()));
        }

        [Test]
        public void Restore_RejectsPileOutsideRoomSize()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => PlayerProgress.Restore(Curve, 1, 1, new List<int>())); // room 1 has 1 pile
        }
    }
}
