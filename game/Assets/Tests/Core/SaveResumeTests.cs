using System.Collections.Generic;
using System.Linq;
using CatShelter.Core;
using NUnit.Framework;

namespace CatShelter.Core.Tests
{
    /// <summary>
    /// Task 60-shell-build/08, VERIFY 1: a save round-trips into an identical
    /// board, and anything unusable falls back to a fresh one without throwing.
    /// The fallback is the half that matters — it runs on every launch, on a
    /// file the process may have been killed in the middle of writing.
    /// </summary>
    [TestFixture]
    public class SaveResumeTests
    {
        private static PileEntry E(int id, string kind, params int[] blockedBy) =>
            new(new Item(id, new ItemKind(kind, kind)), blockedBy.ToList());

        private static Level Level(int number) => new(
            number, $"room_{number:00}", 0,
            new List<PileEntry>
            {
                E(1, "a"), E(2, "b"), E(3, "c"),
                E(4, "a"), E(5, "b"), E(6, "c"),
                E(7, "a"), E(8, "b"), E(9, "c"),
            });

        private static IReadOnlyList<Level> Levels() =>
            new[] { Level(1), Level(2), Level(3) };

        private static string SaveAfter(params int[] takes)
        {
            var board = new Board(Levels()[0]);
            foreach (var id in takes) board.TakeItem(id);
            return GameSave.Write(board, null);
        }

        [Test]
        public void MidLevelSave_ResumesTheSamePosition()
        {
            var text = SaveAfter(2, 5, 8, 1);   // 'b' triple completed, then one 'a'

            var resumed = SaveResume.TryResume(text, Levels(), out var reason);

            Assert.That(reason, Is.Null);
            Assert.That(resumed, Is.Not.Null);
            Assert.That(resumed.TakenOrder, Is.EqualTo(new[] { 2, 5, 8, 1 }));
            Assert.That(resumed.TriplesCompleted, Is.EqualTo(1));
            Assert.That(resumed.Shelf.Slots.Count(s => s != null), Is.EqualTo(1));
            Assert.That(resumed.IsOver, Is.False);
        }

        [Test]
        public void ResumedBoard_KeepsPlaying()
        {
            var resumed = SaveResume.TryResume(SaveAfter(1, 4), Levels(), out _);

            Assert.That(resumed.TakeItem(7), Is.True, "the 'a' triple completes");
            Assert.That(resumed.TriplesCompleted, Is.EqualTo(1));
        }

        [Test]
        public void GrownShelf_SurvivesTheRoundTrip()
        {
            var board = new Board(Levels()[0]);
            board.AddShelfSlots(3);
            board.TakeItem(1);

            var resumed = SaveResume.TryResume(GameSave.Write(board, null), Levels(), out _);

            Assert.That(resumed.Shelf.Capacity, Is.EqualTo(12));
        }

        [TestCase(null, TestName = "no file at all")]
        [TestCase("", TestName = "empty file")]
        [TestCase("garbage", TestName = "not a save")]
        [TestCase("catshelter-save-v1", TestName = "header only")]
        [TestCase("catshelter-save-v1\nlevel 1 room_01 0\nshelf _ cap9\ntriples 0\ntaken 99",
            TestName = "a take that cannot be replayed")]
        [TestCase("catshelter-save-v1\nlevel 1 room_01 0\nshelf a a a cap9\ntriples 0\ntaken 1",
            TestName = "shelf contents contradict the replay")]
        public void UnusableSave_FallsBackWithoutThrowing(string text)
        {
            Board resumed = null;
            string reason = null;
            Assert.DoesNotThrow(() => resumed = SaveResume.TryResume(text, Levels(), out reason));
            Assert.That(resumed, Is.Null);
            Assert.That(reason, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void SaveFromALevelThatNoLongerShips_FallsBack()
        {
            var text = GameSave.Write(new Board(Level(99)), null);

            var resumed = SaveResume.TryResume(text, Levels(), out var reason);

            Assert.That(resumed, Is.Null);
            Assert.That(reason, Does.Contain("99"));
        }

        [Test]
        public void FinishedPosition_IsNotResumedInto()
        {
            // The outcome card dies with the process, so resuming into a board
            // that refuses every tap would strand the player on a dead screen.
            var board = new Board(Levels()[0]);
            foreach (var id in new[] { 1, 4, 7, 2, 5, 8, 3, 6, 9 }) board.TakeItem(id);
            Assert.That(board.Outcome, Is.EqualTo(GameOutcome.Win));

            var resumed = SaveResume.TryResume(GameSave.Write(board, null), Levels(), out var reason);

            Assert.That(resumed, Is.Null);
            Assert.That(reason, Does.Contain("over"));
        }

        [Test]
        public void IndexOf_FindsTheResumedLevel()
        {
            var levels = Levels();
            var board = SaveResume.TryResume(
                GameSave.Write(new Board(levels[2]), null), levels, out _);

            Assert.That(SaveResume.IndexOf(levels, board), Is.EqualTo(2));
        }

        [Test]
        public void IndexOf_ReturnsMinusOne_ForABoardBuiltElsewhere()
        {
            // The view uses the index to know which level comes next, so a
            // stranger board must not silently answer 0.
            Assert.That(SaveResume.IndexOf(Levels(), new Board(Level(1))),
                Is.EqualTo(-1));
        }

        [Test]
        public void TruncatedSave_FallsBack_NoThrow()
        {
            var full = SaveAfter(2, 5, 8);
            for (int cut = 1; cut < full.Length; cut += 7)
            {
                var half = full.Substring(0, cut);
                Assert.DoesNotThrow(() => SaveResume.TryResume(half, Levels(), out _),
                    $"truncated at {cut}");
            }
        }
    }
}
