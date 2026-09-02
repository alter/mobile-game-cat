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
        public void RoundTrip_RealKindNames_ReproducesIdenticalBoard()
        {
            // Kind ids straight from Resources/Levels/l01_room01_pile0.json,
            // not the single-letter "a"/"b"/"c" fixtures used elsewhere in
            // this file — those cannot exercise anything about real item
            // naming, which is exactly where the shelf-format bug lived.
            var level = L(
                E(1, "prop_board", 2), E(2, "prop_plate"), E(3, "prop_frame"),
                E(4, "prop_board"), E(5, "prop_plate"), E(6, "prop_frame"),
                E(7, "prop_board"), E(8, "prop_plate"), E(9, "prop_frame"));
            var board = new Board(level);
            var progress = Progress();
            board.TakeItem(2);
            board.TakeItem(5);
            progress.CompletePile(0);
            board.TakeItem(8);

            var text = GameSave.Write(board, progress);
            var saved = GameSave.Read(text);
            Assert.That(saved, Is.Not.Null);

            var restored = new Board(level);
            foreach (var id in saved.TakenOrder)
                Assert.That(restored.TakeItem(id), Is.True);
            Assert.That(restored.TakenOrder, Is.EqualTo(board.TakenOrder));
            for (int i = 0; i < board.Shelf.Capacity; i++)
                Assert.That(saved.ShelfKinds[i],
                    Is.EqualTo(board.Shelf.Slots[i]?.Kind.Id), $"shelf slot {i}");
        }

        [Test]
        public void RoundTrip_KindNameStartsWithCap_DoesNotBreakCapacityParsing()
        {
            // The audit's own example: an item kind whose name starts with
            // "cap" (e.g. "capybara") used to be misread by the shelf
            // line's cap-token scan — int.Parse on the non-numeric tail
            // ("ybara") threw and lost the whole file. Capacity now lives
            // on its own "cap" line ahead of "shelf", so this name is just
            // another token.
            var level = L(E(1, "capybara"), E(2, "capybara"), E(3, "prop_board"));
            var board = new Board(level);
            board.TakeItem(1); // one "capybara" lands on the shelf, no triple yet

            var text = GameSave.Write(board, Progress());
            Assert.That(text, Does.Contain("capybara"));

            var saved = GameSave.Read(text);
            Assert.That(saved, Is.Not.Null);
            Assert.That(saved.ShelfKinds, Does.Contain("capybara"));
            Assert.That(saved.ShelfCapacity, Is.EqualTo(board.Shelf.Capacity));
        }

        [Test]
        public void OldFormatShelfLine_WithEmbeddedCapToken_StillReads()
        {
            // Exactly what GameSave.Write produced before 08-save-hardening:
            // a single "shelf ... capN" line, no separate "cap" line. Saves
            // already on players' devices look like this; the fallback
            // branch in the "shelf" case exists so they keep resuming.
            var text = "catshelter-save-v1\n" +
                        "level 1 room_01 0\n" +
                        "shelf prop_board _ _ cap3\n" +
                        "triples 0\n" +
                        "taken 1\n";
            var saved = GameSave.Read(text);

            Assert.That(saved, Is.Not.Null);
            Assert.That(saved.ShelfCapacity, Is.EqualTo(3));
            Assert.That(saved.ShelfKinds,
                Is.EqualTo(new[] { "prop_board", null, null }));
        }

        [Test]
        public void RoomsDone_Garbage_ReturnsNull()
        {
            // Negative entries and a duplicate for a save that otherwise
            // parses cleanly — mirrors the cursorRoom < 1 check just above
            // it in GameSave.Read, plus the no-duplicates rule Restore
            // cannot enforce on its own (it only knows the room count).
            var text = "catshelter-save-v1\n" +
                        "level 1 room_01 0\n" +
                        "cap 3\nshelf _ _ _\n" +
                        "triples 0\ntaken \n" +
                        "cursor 1 0\n" +
                        "roomsdone -4 99 99 7\n";
            Assert.That(GameSave.Read(text), Is.Null);
        }

        [Test]
        public void RoomsDone_Honest_RoundTrips()
        {
            var text = "catshelter-save-v1\n" +
                        "level 1 room_01 0\n" +
                        "cap 3\nshelf _ _ _\n" +
                        "triples 0\ntaken \n" +
                        "cursor 3 0\n" +
                        "roomsdone 1 2\n";
            var saved = GameSave.Read(text);
            Assert.That(saved, Is.Not.Null);
            Assert.That(saved.RoomsDone, Is.EquivalentTo(new[] { 1, 2 }));
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
        public void NumbersOutOfRange_ReturnNull_NoThrow()
        {
            // "never crash" was not kept: a value too large for int threw
            // OverflowException, and a negative capacity was accepted, giving a
            // resumed game a shelf the save never described.
            Assert.That(GameSave.Read(
                "catshelter-save-v1\nlevel 99999999999999999999 room_1 0\n" +
                "shelf _ cap9\ntriples 0\ntaken \n"), Is.Null);
            Assert.That(GameSave.Read(
                "catshelter-save-v1\nlevel 1 room_1 0\n" +
                "shelf a b c cap-5\ntriples 0\ntaken 1\n"), Is.Null);
            Assert.That(GameSave.Read(
                "catshelter-save-v1\nlevel 0 room_1 0\n" +
                "shelf _ cap9\ntriples 0\ntaken \n"), Is.Null,
                "levels are 1-based");
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
