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

        [Test]
        public void Restore_RejectsRoomsDoneOutsideRoomCount()
        {
            // A save naming a room this plan does not have (task
            // 08-save-hardening) must not resume silently — same rejection
            // as an out-of-range cursor, one room too many for this curve.
            Assert.Throws<ArgumentOutOfRangeException>(
                () => PlayerProgress.Restore(Curve, 1, 0, new List<int> { Curve.Length + 1 }));
        }

        // --- house map: done / open / locked ----------------------------
        //
        // The map's first question is where the player may go, which is not
        // the same question as how dirty a room is. These pin the rule that
        // exactly one room is open until the house is finished and none after,
        // because a map offering two playable rooms — or none, mid-game —
        // misleads worse than one saying nothing. The distinction matters: an
        // independent verifier found three places claiming the stronger "always
        // exactly one", which this suite does not assert and the code does not
        // do.

        [Test]
        public void AccessFor_FreshGame_OnlyRoomOneIsOpen()
        {
            var p = Make();
            Assert.That(p.AccessFor(1), Is.EqualTo(RoomAccess.Open));
            for (int room = 2; room <= Curve.Length; room++)
                Assert.That(p.AccessFor(room), Is.EqualTo(RoomAccess.Locked),
                            $"room {room} should be shut on a fresh game");
        }

        [Test]
        public void AccessFor_FinishedRoomReadsDone_AndTheCursorIsOpen()
        {
            var p = Make();
            p.CompletePile(0); // room 1 is one pile, so this closes it

            Assert.That(p.AccessFor(1), Is.EqualTo(RoomAccess.Done));
            Assert.That(p.AccessFor(2), Is.EqualTo(RoomAccess.Open));
            Assert.That(p.AccessFor(3), Is.EqualTo(RoomAccess.Locked));
        }

        [Test]
        public void AccessFor_HalfClearedRoom_IsStillOpenNotDone()
        {
            var p = Make();
            p.CompletePile(0); // room 1 done, cursor on room 2 (two piles)
            p.CompletePile(0); // the first of room 2's two piles

            Assert.That(p.AccessFor(2), Is.EqualTo(RoomAccess.Open));
            Assert.That(p.CellStateFor(2), Is.EqualTo(RoomCellState.Partial),
                        "a started room is partial, and being partial does not close it");
        }

        [Test]
        public void AccessFor_ExactlyOneRoomIsOpen_AtEveryPointInTheGame()
        {
            var p = Make();
            int piles = Curve.Sum();
            for (int step = 0; step <= piles; step++)
            {
                var open = Enumerable.Range(1, Curve.Length)
                                     .Count(r => p.AccessFor(r) == RoomAccess.Open);
                var finished = p.RoomsDone.Count == Curve.Length;
                Assert.That(open, Is.EqualTo(finished ? 0 : 1),
                            $"after {step} piles, {open} rooms were open");
                // CompletePile wants the pile's index inside its room, not a
                // running count — the cursor already knows which one is next.
                if (step < piles) p.CompletePile(p.CurrentPile);
            }
        }

        [Test]
        public void AccessFor_OutOfRange_IsLockedRatherThanThrowing()
        {
            var p = Make();
            Assert.That(p.AccessFor(0), Is.EqualTo(RoomAccess.Locked));
            Assert.That(p.AccessFor(-3), Is.EqualTo(RoomAccess.Locked));
            Assert.That(p.AccessFor(Curve.Length + 1), Is.EqualTo(RoomAccess.Locked));
        }

        [Test]
        public void AccessFor_SurvivesRestore()
        {
            var p = PlayerProgress.Restore(Curve, cursorRoom: 4, cursorPile: 1,
                                           roomsDone: new List<int> { 1, 2, 3 });
            Assert.That(p.AccessFor(2), Is.EqualTo(RoomAccess.Done));
            Assert.That(p.AccessFor(4), Is.EqualTo(RoomAccess.Open));
            Assert.That(p.AccessFor(5), Is.EqualTo(RoomAccess.Locked));
        }
    }
}
