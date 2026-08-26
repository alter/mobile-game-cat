using System;
using System.Collections.Generic;
using System.Linq;
using CatShelter.Core;
using NUnit.Framework;

namespace CatShelter.Core.Tests
{
    /// <summary>
    /// Task 20-rules-core/01, VERIFY 3: a level whose kind count is not a
    /// multiple of three is rejected at construction. It used to be rejected
    /// only when a Board was built from it, so an unwinnable level could exist
    /// and travel — through a save file, a generator run, a JSON asset — until
    /// something tried to play it.
    /// </summary>
    [TestFixture]
    public class LevelTests
    {
        private static PileEntry E(int id, string kind, params int[] blockedBy) =>
            new(new Item(id, new ItemKind(kind, kind)), blockedBy.ToList());

        private static IReadOnlyList<PileEntry> Pile(params PileEntry[] entries) => entries;

        [Test]
        public void KindAppearingFourTimes_IsRejected()
        {
            var ex = Assert.Throws<ArgumentException>(() =>
                new Level(1, "room_01", 0,
                    Pile(E(1, "a"), E(2, "a"), E(3, "a"), E(4, "a"))));

            Assert.That(ex.Message, Does.Contain("appears 4 times"));
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(4)]
        [TestCase(5)]
        public void AnyCountNotDivisibleByThree_IsRejected(int copies)
        {
            var entries = Enumerable.Range(1, copies).Select(i => E(i, "a")).ToArray();

            Assert.Throws<ArgumentException>(
                () => new Level(1, "room_01", 0, Pile(entries)));
        }

        [Test]
        public void TriplesAreAccepted()
        {
            Assert.DoesNotThrow(() => new Level(1, "room_01", 0,
                Pile(E(1, "a"), E(2, "a"), E(3, "a"),
                     E(4, "b"), E(5, "b"), E(6, "b"))));
        }

        [Test]
        public void EmptyPile_IsAccepted()
        {
            // Zero copies of zero kinds is trivially in triples, and the empty
            // level is used by tests as a degenerate case.
            Assert.DoesNotThrow(
                () => new Level(1, "room_01", 0, Array.Empty<PileEntry>()));
        }

        [Test]
        public void DuplicateItemIds_AreRejected()
        {
            // Occlusion bookkeeping is keyed by id: two items sharing one id
            // means taking either takes both.
            var ex = Assert.Throws<ArgumentException>(() =>
                new Level(1, "room_01", 0,
                    Pile(E(7, "a"), E(7, "a"), E(7, "a"))));

            Assert.That(ex.Message, Does.Contain("duplicate item id 7"));
        }

        [Test]
        public void RejectionHappensBeforeABoardExists()
        {
            // The point of moving the check: the bad level cannot be built, so
            // it cannot be written to a save or shipped as an asset either.
            Assert.Throws<ArgumentException>(() =>
                new Level(1, "room_01", 0, Pile(E(1, "a"), E(2, "a"))));
        }
    }
}
