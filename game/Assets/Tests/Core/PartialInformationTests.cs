using System.Linq;
using CatShelter.Core;
using NUnit.Framework;

namespace CatShelter.Core.Tests
{
    /// <summary>
    /// Task 3.9 — hidden kinds: an item shows its kind only once it is reachable.
    /// Task 3.11 — locked items open after N completed triples.
    /// </summary>
    [TestFixture]
    public class PartialInformationTests
    {
        private static PileEntry E(int id, string kind, params int[] blockedBy) =>
            new(new Item(id, new ItemKind(kind, kind)), blockedBy.ToList());

        private static PileEntry Locked(int id, string kind, int unlockAfter) =>
            new(new Item(id, new ItemKind(kind, kind), unlockAfter),
                System.Array.Empty<int>());

        private static Level L(params PileEntry[] pile)
        {
            // pad each kind group to a multiple of three (Board invariant)
            var list = pile.ToList();
            int nextId = 100;
            foreach (var group in pile.GroupBy(e => e.Item.Kind.Id))
            {
                int deficit = (3 - group.Count() % 3) % 3;
                for (int i = 0; i < deficit; i++)
                    list.Add(E(nextId++, group.Key));
            }
            return new Level(7, "room_1", 0, list);
        }

        // ---- 3.9 revealed -------------------------------------------------

        [Test]
        public void BuriedItem_KindHidden_UntilReachable()
        {
            var board = new Board(L(
                E(1, "a", 2),   // covered by 2 → hidden
                E(2, "b")));     // top → visible
            Assert.That(board.IsRevealed(_item(board, 1)), Is.False);
            Assert.That(board.IsRevealed(_item(board, 2)), Is.True);
        }

        [Test]
        public void TakingCover_RevealsBuriedItem()
        {
            var board = new Board(L(E(1, "a", 2), E(2, "b"), E(3, "c")));
            board.TakeItem(2);
            Assert.That(board.IsRevealed(_item(board, 1)), Is.True);
        }

        [Test]
        public void AvailableItems_AreAlwaysRevealed()
        {
            var board = new Board(L(E(1, "a"), E(2, "b")));
            foreach (var item in board.GetAvailable())
                Assert.That(board.IsRevealed(item), Is.True);
        }

        // ---- 3.11 locked ---------------------------------------------------

        [Test]
        public void LockedItem_NotAvailable_UntilThreshold()
        {
            var board = new Board(L(
                Locked(1, "x", 1),          // opens after one completed triple
                E(10, "a"), E(11, "a"), E(12, "a")));
            Assert.That(board.GetAvailable().Any(i => i.Id == 1), Is.False);

            board.TakeItem(10);
            board.TakeItem(11);
            board.TakeItem(12);             // first triple completes

            Assert.That(board.TriplesCompleted, Is.EqualTo(1));
            Assert.That(board.GetAvailable().Any(i => i.Id == 1), Is.True);
        }

        [Test]
        public void LockedItem_CannotBeTaken_EvenIfAsked()
        {
            var board = new Board(L(
                Locked(1, "x", 1),
                E(10, "a"), E(11, "a"), E(12, "a")));
            Assert.That(board.TakeItem(1), Is.False);   // direct attempt ignored
        }

        /// <summary>
        /// D15: the lock is a complication, so it has to be seen to be one.
        /// This test asserted the opposite until 2026-08-27 — a locked item
        /// reported itself unrevealed, the view drew it as a buried tile, and
        /// the lock reached the screen in none of the 37 shipped levels.
        /// Locked and buried are different states; only burial hides a kind.
        /// </summary>
        [Test]
        public void LockedItem_IsRevealedButNotTakeable()
        {
            // 2, not some arbitrarily large number: task 07 caps
            // LockedAfterTriples at the pile's max achievable triples (here
            // 6 items / 3 = 2), so 2 is the largest threshold Level accepts
            // and is already unreachable within this fixture, which never
            // takes anything.
            var board = new Board(L(
                Locked(1, "x", 2),          // never unlocks in this level
                E(10, "a"), E(11, "a"), E(12, "a")));
            var locked = _item(board, 1);
            Assert.That(board.IsRevealed(locked), Is.True, "the player must see which kind is withheld");
            Assert.That(board.IsLockedByComplication(locked), Is.True);
            Assert.That(board.GetAvailable().Any(i => i.Id == 1), Is.False, "seen, still not takeable");
            Assert.That(board.TakeItem(1), Is.False);
        }

        [Test]
        public void ItemsWithoutALockField_AreAllAvailable()
        {
            var board = new Board(L(
                E(1, "a"), E(2, "a"), E(3, "a")));
            Assert.That(board.GetAvailable().Count, Is.EqualTo(3));
        }

        [Test]
        public void EveryRemainingItemLocked_EndsAsJam_NotAHang()
        {
            // one free triple, then three items locked behind the pile's own
            // ceiling of two triples (task 07: Level rejects anything past
            // Pile.Count / 3) — one triple short of what completing "a"
            // supplies, so it can never be collected: the board used to sit
            // with no outcome and no legal move, which on a phone is a dead
            // screen.
            var board = new Board(L(
                E(1, "a"), E(2, "a"), E(3, "a"),
                Locked(4, "b", 2), Locked(5, "b", 2), Locked(6, "b", 2)));

            board.TakeItem(1);
            board.TakeItem(2);
            board.TakeItem(3);

            Assert.That(board.GetAvailable(), Is.Empty);
            Assert.That(board.IsOver, Is.True, "no move exists, so the game is over");
            Assert.That(board.Outcome, Is.EqualTo(GameOutcome.ShelfJammed));
        }

        /// <summary>
        /// Task 07: the test above passes through a successful first take
        /// before the jam is detected, which is exactly the gap the previous
        /// guard had — it lived at the end of TakeItem, so it never fired for
        /// a level where even the FIRST move is unavailable (every top item
        /// already locked). Here nothing is ever taken; the board must arrive
        /// pre-jammed straight out of its constructor.
        /// </summary>
        [Test]
        public void EveryItemLockedFromTheStart_EndsAsJam_BeforeFirstMove()
        {
            var board = new Board(L(
                Locked(1, "a", 1), Locked(2, "a", 1), Locked(3, "a", 1)));

            Assert.That(board.TakenOrder, Is.Empty, "no move was ever made");
            Assert.That(board.GetAvailable(), Is.Empty);
            Assert.That(board.IsOver, Is.True, "construction itself must end the game");
            Assert.That(board.Outcome, Is.EqualTo(GameOutcome.ShelfJammed));
        }

        private static Item _item(Board board, int id) =>
            board.Level.Pile.First(e => e.Item.Id == id).Item;
    }
}
