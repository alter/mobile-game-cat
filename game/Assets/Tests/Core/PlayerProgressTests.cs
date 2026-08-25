using System;
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
    }
}
