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

        [Test]
        public void LockedItem_IsNotRevealed()
        {
            var board = new Board(L(
                Locked(1, "x", 5),          // far off: stays hidden all game
                E(10, "a"), E(11, "a"), E(12, "a")));
            Assert.That(board.IsRevealed(_item(board, 1)), Is.False);
        }

        [Test]
        public void ZeroLockThreshold_MeansNoLocks()
        {
            // default constructor: nothing locked even if items carry a lock field
            var board = new Board(L(
                E(1, "a"), E(2, "a"), E(3, "a")));
            Assert.That(board.GetAvailable().Count, Is.EqualTo(3));
        }

        private static Item _item(Board board, int id) =>
            board.Level.Pile.First(e => e.Item.Id == id).Item;
    }
}
