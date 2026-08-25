using System;
using System.Linq;
using CatShelter.Core;
using NUnit.Framework;

namespace CatShelter.Core.Tests
{
    /// <summary>
    /// Task 60-shell-build/08: the save survives a kill mid-level.
    /// Write on every move; read must reproduce the identical board or fall
    /// back to fresh without throwing.
    /// </summary>
    [TestFixture]
    public class GameSaveTests
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

        private static PlayerProgress Progress()
        {
            // 12-room curve, matching tools/solver/pacing.py
            return new PlayerProgress(new[]
                { 1, 2, 3, 3, 3, 3, 3, 3, 4, 4, 4, 4 });
        }

        [Test]
        public void RoundTrip_ReproducesIdenticalBoard()
        {
            var level = L(
                E(1, "a", 2), E(2, "b"), E(3, "c"),
                E(4, "a"), E(5, "b"), E(6, "c"),
                E(7, "a"), E(8, "b"), E(9, "c"));
            var board = new Board(level);
            var progress = Progress();
            board.TakeItem(2);
            board.TakeItem(5);
            progress.CompletePile(0);   // room 1 done, cursor at room 2
            board.TakeItem(8);          // 'b' triple completes

            var text = GameSave.Write(board, progress);
            var saved = GameSave.Read(text);
            Assert.That(saved, Is.Not.Null);

            // rebuild and compare move by move
            var restored = new Board(level);
            foreach (var id in saved.TakenOrder)
                Assert.That(restored.TakeItem(id), Is.True);

            Assert.That(restored.TakenOrder, Is.EqualTo(board.TakenOrder));
            Assert.That(restored.TriplesCompleted,
                Is.EqualTo(board.TriplesCompleted));
            Assert.That(saved.RoomsDone, Does.Contain(1));
            Assert.That(saved.CursorRoom, Is.EqualTo(2));

            // shelf contents match slot by slot
            for (int i = 0; i < board.Shelf.Capacity; i++)
            {
                var live = board.Shelf.Slots[i]?.Kind.Id;
                Assert.That(saved.ShelfKinds[i], Is.EqualTo(live),
                    $"shelf slot {i}");
            }
        }

        [Test]
        public void SaveAfterEveryMove_MidLevelResumeWorks()
        {
            var level = L(E(1, "x"), E(2, "y"), E(3, "z"),
                          E(4, "x"), E(5, "y"), E(6, "z"),
                          E(7, "x"), E(8, "y"), E(9, "z"));
            var board = new Board(level);

            Board lastGood = null;
            string lastText = null;
            foreach (var id in new[] { 1, 4, 7 })   // x-triple in progress
            {
                board.TakeItem(id);
                lastText = GameSave.Write(board, Progress());
            }

            // app is killed; reopened:
            var saved = GameSave.Read(lastText);
            var resumed = new Board(level);
            foreach (var resumeId in saved.TakenOrder)
                resumed.TakeItem(resumeId);

            Assert.That(resumed.TakenOrder, Is.EqualTo(board.TakenOrder));
            Assert.That(resumed.IsOver, Is.False);
            lastGood = resumed;
            Assert.That(lastGood, Is.Not.Null);
        }

        [Test]
        public void CorruptedFile_ReturnsNull_NoThrow()
        {
            Assert.That(GameSave.Read(""), Is.Null);
            Assert.That(GameSave.Read(null), Is.Null);
            Assert.That(GameSave.Read("garbage"), Is.Null);
            Assert.That(GameSave.Read("catshelter-save-v1\nlevel notanumber r 0"),
                Is.Null);
            Assert.That(GameSave.Read("catshelter-save-v9\nshelf"), Is.Null);
        }

        [Test]
        public void TruncatedSave_FallsBackCleanly()
        {
            var level = L(E(1, "a"), E(2, "b"));
            var board = new Board(level);
            board.TakeItem(1);
            var text = GameSave.Write(board, Progress());
            var truncated = text.Substring(0, text.Length / 2);
            // may or may not parse — but must never throw
            Assert.DoesNotThrow(() => GameSave.Read(truncated));
        }

        [Test]
        public void SaveIsPlainAscii_ReadableForDebugging()
        {
            var level = L(E(1, "a"), E(2, "b"));
            var board = new Board(level);
            board.TakeItem(1);
            var text = GameSave.Write(board, Progress());
            Assert.That(text, Does.StartWith(GameSave.Header));
            Assert.That(text.All(c => c < 128), Is.True,
                "save stays ASCII so it can be inspected in a crash log");
        }
    }
}
