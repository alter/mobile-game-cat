using System;
using System.Linq;
using CatShelter.Core;
using NUnit.Framework;

namespace CatShelter.Core.Tests
{
    /// <summary>
    /// Task 6.7: the board survives an interruption. Capture after every move;
    /// Restore rebuilds the identical position.
    /// </summary>
    [TestFixture]
    public class BoardSaveTests
    {
        private static PileEntry E(int id, string kind, params int[] blockedBy) =>
            new(new Item(id, new ItemKind(kind, kind)), blockedBy.ToList());

        private static Level L(params PileEntry[] pile)
        {
            var list = pile.ToList();
            int nextId = 100;
            foreach (var group in pile.GroupBy(e => e.Item.Kind.Id))
            {
                int deficit = (3 - group.Count() % 3) % 3;
                for (int i = 0; i < deficit; i++)
                    list.Add(E(nextId++, group.Key));
            }
            return new Level(7, "room_03", 1, list);
        }

        [Test]
        public void Restore_RebuildsIdenticalPosition()
        {
            var level = L(
                E(1, "a", 2), E(2, "b"), E(3, "c"),
                E(4, "a"), E(5, "b"), E(6, "c"),
                E(7, "a"), E(8, "b"), E(9, "c"));
            var board = new Board(level);
            board.TakeItem(2);
            board.TakeItem(5);
            board.TakeItem(8);   // 'b' triple completes

            var snapshot = BoardSave.Capture(board);
            var restored = BoardSave.Restore(level, snapshot);

            Assert.That(restored.TakenOrder, Is.EqualTo(board.TakenOrder));
            Assert.That(restored.TriplesCompleted, Is.EqualTo(board.TriplesCompleted));
            Assert.That(
                restored.GetAvailable().Select(i => i.Id),
                Is.EqualTo(board.GetAvailable().Select(i => i.Id)));
        }

        [Test]
        public void RestoredBoard_ContinuesToTheSameOutcome()
        {
            var level = L(E(1, "a"), E(2, "a"), E(3, "a"));
            var original = new Board(level);
            original.TakeItem(1);

            var restored = BoardSave.Restore(level, BoardSave.Capture(original));
            restored.TakeItem(2);
            restored.TakeItem(3);

            Assert.That(restored.IsOver, Is.True);
            Assert.That(restored.Outcome, Is.EqualTo(GameOutcome.Win));
        }

        [Test]
        public void CorruptedSnapshot_TakenIdUnknown_FailsLoudly()
        {
            var level = L(E(1, "a"), E(2, "a"), E(3, "a"));
            var snapshot = new BoardSnapshot(7, "room_03", 1,
                new[] { 99 }, new string?[] { null, null, null, null, null, null, null, null, null }, 0);
            Assert.Throws<InvalidOperationException>(
                () => BoardSave.Restore(level, snapshot));
        }

        [Test]
        public void CorruptedSnapshot_ShelfMismatch_FailsLoudly()
        {
            var level = L(E(1, "a"), E(2, "a"), E(3, "a"));
            var board = new Board(level);
            board.TakeItem(1);
            var snapshot = BoardSave.Capture(board);
            // tamper: claim slot 0 holds a different kind
            var shelf = snapshot.Shelf.ToArray();
            shelf[0] = "zzz";
            var tampered = new BoardSnapshot(7, "room_03", 1,
                snapshot.Taken, shelf, snapshot.TriplesCompleted);

            Assert.Throws<InvalidOperationException>(
                () => BoardSave.Restore(level, tampered));
        }

        [Test]
        public void Snapshot_AfterEveryMove_MatchesLiveState()
        {
            var level = L(E(1, "a", 2), E(2, "b"), E(3, "c"),
                          E(4, "a"), E(5, "b"), E(6, "c"),
                          E(7, "a"), E(8, "b"), E(9, "c"));
            var live = new Board(level);
            int[] moves = { 2, 5, 8, 1 };
            foreach (var move in moves)
            {
                live.TakeItem(move);
                var restored = BoardSave.Restore(level, BoardSave.Capture(live));
                Assert.That(restored.TakenOrder, Is.EqualTo(live.TakenOrder),
                    $"after move {move}");
            }
        }
    }
}
