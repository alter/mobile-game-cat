using System;
using System.Collections.Generic;
using System.Linq;
using CatShelter.Core;
using NUnit.Framework;

namespace CatShelter.Core.Tests
{
    [TestFixture]
    public class BoardTests
    {
        private static ItemKind K(string id) => new(id, id);

        private static PileEntry Entry(int id, string kind, params int[] blockedBy) =>
            new(new Item(id, K(kind)), blockedBy.ToList());

        // Levels must carry kinds in triples — enforced by the Board now.
        private static Level MakeLevel(params PileEntry[] pile)
        {
            var list = pile.ToList();
            int nextId = list.Count + 1;
            foreach (var group in pile.GroupBy(e => e.Item.Kind.Id))
            {
                int deficit = (3 - group.Count() % 3) % 3;
                for (int i = 0; i < deficit; i++)
                    list.Add(Entry(nextId++, group.Key));
            }
            return new Level(1, "room_1", 0, list);
        }

        // ---- GetAvailable ----------------------------------------------------

        [Test]
        public void EmptyPile_NoAvailableItems()
        {
            var board = new Board(new Level(1, "room_1", 0, Array.Empty<PileEntry>()));
            Assert.That(board.GetAvailable(), Is.Empty);
        }

        [Test]
        public void SingleLayer_AllItemsAvailable()
        {
            var board = new Board(MakeLevel(Entry(1, "a"), Entry(2, "b")));
            var ids = board.GetAvailable().Select(i => i.Id).ToList();
            Assert.That(ids, Has.Member(1));
            Assert.That(ids, Has.Member(2));
        }

        [Test]
        public void ThreeLayers_OnlyTopOfEachStackAvailable()
        {
            var board = new Board(MakeLevel(
                Entry(1, "a", 2),
                Entry(2, "a", 3),
                Entry(3, "a"),
                Entry(4, "b"),
                Entry(5, "b", 4)));
            var ids = board.GetAvailable().Select(i => i.Id).ToList();
            Assert.That(ids, Has.Member(3));
            Assert.That(ids, Has.Member(4));
            // items 1 and 2 are buried and must not be available
            Assert.That(ids, Has.No.Member(1));
            Assert.That(ids, Has.No.Member(2));
        }

        [Test]
        public void CircularBlock_IsRefusedWhenTheLevelIsBuilt()
        {
            // This used to build the cyclic level and assert that Board simply
            // reported nothing in the cycle as available — tolerance, in depth.
            // Since 2026-08-27 `Level` refuses a cycle outright, matching
            // tools/solver/schema.py, which has rejected one since the start;
            // the asymmetry was found while verifying 05-ship-37-levels.
            //
            // So the old assertion now describes a state that cannot be
            // reached: a cyclic Level cannot be constructed, therefore no Board
            // can hold one. Rather than delete the case, it asserts the
            // stronger guarantee that replaced it. Board's own "no move exists"
            // handling is unaffected and still reachable through locked items,
            // which PartialInformationTests.EveryRemainingItemLocked_EndsAsJam_NotAHang covers.
            var ex = Assert.Throws<ArgumentException>(() => MakeLevel(
                Entry(1, "a", 2),
                Entry(2, "b", 1),
                Entry(3, "c")));
            Assert.That(ex!.Message, Does.Contain("cycle"));
        }

        [Test]
        public void AfterTakingTopItem_ItemBelowBecomesAvailable()
        {
            var board = new Board(MakeLevel(
                Entry(1, "a", 2),
                Entry(2, "b"),
                Entry(3, "c")));
            Assert.That(board.TakeItem(2), Is.True);
            var ids = board.GetAvailable().Select(i => i.Id).ToList();
            Assert.That(ids, Has.Member(1));
            Assert.That(ids, Has.Member(3));
        }

        [Test]
        public void CannotTakeBlockedItem()
        {
            var board = new Board(MakeLevel(Entry(1, "a", 2), Entry(2, "b")));
            Assert.That(board.TakeItem(1), Is.False);
        }

        [Test]
        public void CannotTakeSameItemTwice()
        {
            var board = new Board(MakeLevel(Entry(1, "a"), Entry(2, "b")));
            Assert.That(board.TakeItem(1), Is.True);
            Assert.That(board.TakeItem(1), Is.False);
        }

        [Test]
        public void UnknownItemId_Rejected()
        {
            var board = new Board(MakeLevel(Entry(1, "a")));
            Assert.That(board.TakeItem(99), Is.False);
        }

        [Test]
        public void DuplicateItemIds_ConstructorThrows()
        {
            Assert.Throws<ArgumentException>(() =>
                new Board(new Level(1, "room_1", 0,
                    new[] { Entry(1, "a"), Entry(1, "b") })));
        }

        [Test]
        public void KindNotInTriples_ConstructorThrows()
        {
            // two 'a' only — the win condition would strand items on the shelf
            var pile = new[]
            {
                Entry(1, "a"),
                Entry(2, "a"),
            };
            Assert.Throws<ArgumentException>(() =>
                new Board(new Level(1, "room_1", 0, pile)));
        }

        [Test]
        public void TakingItemAfterGameIsOver_Rejected()
        {
            var board = new Board(MakeLevel(
                Entry(1, "a"), Entry(2, "a"), Entry(3, "a")));
            board.TakeItem(1);
            board.TakeItem(2);
            Assert.That(board.IsOver, Is.False);
            board.TakeItem(3); // this is the win
            Assert.That(board.TakeItem(1), Is.False);
        }
    }

    [TestFixture]
    public class ShelfTests
    {
        private static Item Item(string kind, int id = 0) =>
            new(id, new ItemKind(kind, kind));

        [Test]
        public void Place_FillsLeftmostFreeSlot()
        {
            var shelf = new Shelf();
            shelf.TryPlace(Item("a", 1), out _);
            shelf.TryPlace(Item("b", 2), out _);
            Assert.That(shelf.Slots[0]!.Kind.Id, Is.EqualTo("a"));
            Assert.That(shelf.Slots[1]!.Kind.Id, Is.EqualTo("b"));
            Assert.That(shelf.Occupied, Is.EqualTo(2));
        }

        [Test]
        public void Place_AfterMatchFreesSlots_ReusesFreeSlot()
        {
            var shelf = new Shelf();
            for (int i = 0; i < 3; i++)
                Assert.That(shelf.TryPlace(Item("x", i), out _), Is.True);

            Assert.That(shelf.Occupied, Is.EqualTo(0));

            var next = Item("y", 10);
            Assert.That(shelf.TryPlace(next, out _), Is.True);
            Assert.That(shelf.Slots[0], Is.SameAs(next));
        }

        [Test]
        public void Match_CompletesWithinOneRow()
        {
            var shelf = new Shelf();
            shelf.TryPlace(Item("m", 1), out _);
            shelf.TryPlace(Item("m", 2), out _);
            Assert.That(shelf.Occupied, Is.EqualTo(2), "no match before the third copy");

            Assert.That(shelf.TryPlace(Item("m", 3), out var matched), Is.True);
            Assert.That(matched!.Id, Is.EqualTo("m"));
            Assert.That(shelf.Occupied, Is.EqualTo(0));
        }

        [Test]
        public void Match_CompletesAcrossRowBoundary()
        {
            // Rows are presentation only, and this is the case that proves it.
            // The test that used to carry this name placed three copies into an
            // empty shelf, so they landed in slots 0-2 — inside row 0, crossing
            // nothing. Placement always takes the leftmost free slot, so the
            // copies have to be spread by filling the slots between them.
            var shelf = new Shelf();
            shelf.TryPlace(Item("a", 1), out _);   // slot 0, row 0
            shelf.TryPlace(Item("b", 2), out _);   // slot 1
            shelf.TryPlace(Item("c", 3), out _);   // slot 2, end of row 0
            shelf.TryPlace(Item("a", 4), out _);   // slot 3, row 1
            shelf.TryPlace(Item("b", 5), out _);   // slot 4

            var occupied = shelf.Slots
                .Select((item, index) => (item, index))
                .Where(pair => pair.item?.Kind.Id == "a")
                .Select(pair => pair.index)
                .ToList();
            Assert.That(occupied, Is.EqualTo(new[] { 0, 3 }),
                "the two 'a' copies must straddle the row boundary");
            Assert.That(Shelf.SlotsPerRow, Is.EqualTo(3), "rows are three slots wide");

            Assert.That(shelf.TryPlace(Item("a", 6), out var matched), Is.True);

            Assert.That(matched!.Id, Is.EqualTo("a"));
            Assert.That(shelf.Slots.Count(s => s?.Kind.Id == "a"), Is.EqualTo(0),
                "all three 'a' left the shelf although they sat in rows 0 and 1");
            Assert.That(shelf.Occupied, Is.EqualTo(3), "two 'b' and one 'c' stay");
        }

        [Test]
        public void Match_DoesNotFireWithTwoOfAKind()
        {
            var shelf = new Shelf();
            shelf.TryPlace(Item("a", 1), out _);
            shelf.TryPlace(Item("a", 2), out _);
            Assert.That(shelf.Occupied, Is.EqualTo(2));
        }

        [Test]
        public void FullShelf_PlacementRefused()
        {
            var shelf = new Shelf();
            var kinds = Enumerable.Range(0, Shelf.SlotsPerRow * Shelf.RowCount)
                .Select(i => $"k{i}").ToList();
            foreach (var k in kinds)
                Assert.That(shelf.TryPlace(Item(k), out _), Is.True);

            Assert.That(shelf.IsFull, Is.True);
            Assert.That(shelf.TryPlace(Item("extra"), out _), Is.False);
        }

        [Test]
        public void AddSlots_GrowsCapacity_KeepsPlacedItems()
        {
            var shelf = new Shelf();
            shelf.TryPlace(Item("a", 1), out _);
            Assert.That(shelf.Capacity, Is.EqualTo(9));

            shelf.AddSlots(1);
            Assert.That(shelf.Capacity, Is.EqualTo(10));
            Assert.That(shelf.Occupied, Is.EqualTo(1));
            Assert.That(shelf.IsFull, Is.False);
            Assert.That(shelf.TryPlace(Item("extra"), out _), Is.True);
        }
    }

    [TestFixture]
    public class OutcomeTests
    {
        private static PileEntry E(int id, string kind, params int[] blockedBy) =>
            new(new Item(id, new ItemKind(kind, kind)), blockedBy.ToList());

        private static PileEntry Locked(int id, string kind, int unlockAfter) =>
            new(new Item(id, new ItemKind(kind, kind), unlockAfter),
                Array.Empty<int>());

        private static Level L(params PileEntry[] pile) =>
            new(7, "room_1", 0, pile);

        [Test]
        public void Win_PileCleared()
        {
            // 12 items in 4 kinds on a roomy shelf: every triple matches as it
            // forms and the emptied pile is a win. The name used to promise
            // "even when the last take fills the shelf" and the body never
            // filled anything — peak occupancy here is two of twelve slots.
            // See VERIFY.md in tasks/20-rules-core/04-outcomes.
            var entries = new List<PileEntry>();
            for (int kind = 0; kind < 4; kind++)
                for (int i = 0; i < 3; i++)
                    entries.Add(E(kind * 3 + i + 1, $"kind{kind}"));
            var board = new Board(L(entries.ToArray()), shelfCapacity: 12);
            foreach (var e in entries)
                board.TakeItem(e.Item.Id);

            Assert.That(board.IsOver, Is.True);
            Assert.That(board.Outcome, Is.EqualTo(GameOutcome.Win));
        }

        // ---- the boundary between the two outcomes --------------------------
        // The task's original acceptance asked for "the last item empties the
        // pile AND fills the shelf → Win". That state cannot occur: a pile
        // holds every kind in multiples of three and TryMatch removes each
        // triple as it completes, so the shelf is empty exactly when the pile
        // is. The two outcomes never compete for the same move, and the
        // ordering branch at Board.cs:120-130 is unreachable — proven by
        // 40 000 random games in tasks/20-rules-core/04-outcomes/VERIFY.md.
        //
        // What IS reachable, and what these two tests pin, is the near miss:
        // the final placement takes the shelf's last free slot and matches on
        // the way in. Move the fullness check ahead of the match and this
        // reads as a jam on a winning move.

        [Test]
        public void FinalPlacementTakesTheLastSlotAndMatches_IsAWin_NotAJam()
        {
            // capacity 3, two kinds of three: every third take lands in the
            // last free slot and completes a triple on the way in.
            var entries = new List<PileEntry>();
            for (int kind = 0; kind < 2; kind++)
                for (int i = 0; i < 3; i++)
                    entries.Add(E(kind * 3 + i + 1, $"kind{kind}"));
            var board = new Board(L(entries.ToArray()), shelfCapacity: 3);

            board.TakeItem(1);
            board.TakeItem(2);
            Assert.That(board.Shelf.Occupied, Is.EqualTo(2), "shelf one short of full");
            board.TakeItem(3);   // fills the third slot, matches, empties it
            Assert.That(board.IsOver, Is.False, "a matched shelf is not a jam");
            Assert.That(board.Shelf.Occupied, Is.EqualTo(0));

            board.TakeItem(4);
            board.TakeItem(5);
            board.TakeItem(6);   // same again, and this one empties the pile

            Assert.That(board.Outcome, Is.EqualTo(GameOutcome.Win));
        }

        [Test]
        public void AtAWin_TheShelfIsEmpty_WhichIsWhyTheOutcomesCannotCompete()
        {
            // The structural reason item 2 of the old acceptance was
            // unsatisfiable: a win leaves the shelf empty, so "empties the pile
            // AND fills the shelf" is 0 == capacity.
            var entries = new List<PileEntry>();
            for (int kind = 0; kind < 3; kind++)
                for (int i = 0; i < 3; i++)
                    entries.Add(E(kind * 3 + i + 1, $"kind{kind}"));
            var board = new Board(L(entries.ToArray()), shelfCapacity: 4);
            foreach (var e in entries)
                board.TakeItem(e.Item.Id);

            Assert.That(board.Outcome, Is.EqualTo(GameOutcome.Win));
            Assert.That(board.Shelf.Occupied, Is.EqualTo(0));
            Assert.That(board.Shelf.IsFull, Is.False);
        }

        [Test]
        public void PileHoldsEveryKindInTriples_WhichIsWhatMakesTheAboveTrue()
        {
            // The actual tripwire. The test above builds its own triple pile,
            // so it can never catch a loosened invariant — this one can. Should
            // Level ever accept a kind in non-triples, a win could strand
            // leftovers on the shelf, the two outcomes would start competing
            // for the same move, and Board.cs:120-130 would stop being dead.
            var entries = new List<PileEntry>
            {
                E(1, "kind0"), E(2, "kind0"),   // two, not three
                E(3, "kind1"), E(4, "kind1"), E(5, "kind1"),
            };

            var ex = Assert.Throws<ArgumentException>(
                () => L(entries.ToArray()));
            Assert.That(ex!.ParamName, Is.EqualTo("pile"));
        }

        [Test]
        public void ShelfJammed_UnmatchedKindsFillTheShelf()
        {
            // 5 kinds × 3 = 15 items on a nine-slot shelf. Taking one of each
            // kind, then a second of four of them, fills all nine slots with
            // nothing matched: the jam lands on the ninth take, not the tenth,
            // because the ninth is the one that fills the shelf.
            var entries = new List<PileEntry>();
            for (int kind = 0; kind < 5; kind++)
                for (int i = 0; i < 3; i++)
                    entries.Add(E(kind * 3 + i + 1, $"kind{kind}"));
            var board = new Board(L(entries.ToArray()));

            int[] order = { 1, 4, 7, 10, 13, 2, 5, 8, 11, 14 };
            GameOutcome? outcome = null;
            foreach (var id in order)
            {
                board.TakeItem(id);
                if (board.IsOver) { outcome = board.Outcome; break; }
            }

            Assert.That(outcome, Is.EqualTo(GameOutcome.ShelfJammed));
        }

        [Test]
        public void OutcomesAreTwoAndDistinct()
        {
            var values = new HashSet<GameOutcome>
            {
                GameOutcome.Win,
                GameOutcome.ShelfJammed
            };
            Assert.That(values.Count, Is.EqualTo(2));
        }

        [Test]
        public void CustomShelfCapacity_ChangesJamPoint()
        {
            var entries = new List<PileEntry>();
            for (int kind = 0; kind < 5; kind++)
                for (int i = 0; i < 3; i++)
                    entries.Add(E(kind * 3 + i + 1, $"kind{kind}"));
            // capacity 4: four distinct kinds jam on the fourth take
            var board = new Board(L(entries.ToArray()), shelfCapacity: 4);
            board.TakeItem(1);
            board.TakeItem(4);
            board.TakeItem(7);
            Assert.That(board.IsOver, Is.False);
            board.TakeItem(10);

            Assert.That(board.Outcome, Is.EqualTo(GameOutcome.ShelfJammed));
        }

        // ---- the booster (DECISIONS.md D4) -----------------------------------
        // The MVP never grants it, but the Python mirror has always resumed a
        // jammed game and C# has not; conformance hid the divergence.

        private static Board JammedBoard()
        {
            var entries = new List<PileEntry>();
            for (int kind = 0; kind < 3; kind++)
                for (int i = 0; i < 3; i++)
                    entries.Add(E(kind * 3 + i + 1, $"kind{kind}"));
            var board = new Board(L(entries.ToArray()), shelfCapacity: 3);
            board.TakeItem(1);
            board.TakeItem(4);
            board.TakeItem(7);   // three distinct kinds on three slots
            return board;
        }

        [Test]
        public void Booster_ResumesAJammedBoard()
        {
            var board = JammedBoard();
            Assert.That(board.Outcome, Is.EqualTo(GameOutcome.ShelfJammed));

            board.AddShelfSlots(3);

            Assert.That(board.Shelf.Capacity, Is.EqualTo(6));
            Assert.That(board.IsOver, Is.False);
            Assert.That(board.Outcome, Is.Null);
            Assert.That(board.TakeItem(2), Is.True, "play continues after the booster");
        }

        [Test]
        public void Booster_LeavesAWonBoardWon()
        {
            var board = new Board(L(E(1, "a"), E(2, "a"), E(3, "a")));
            board.TakeItem(1);
            board.TakeItem(2);
            board.TakeItem(3);
            Assert.That(board.Outcome, Is.EqualTo(GameOutcome.Win));

            board.AddShelfSlots(3);

            Assert.That(board.IsOver, Is.True);
            Assert.That(board.Outcome, Is.EqualTo(GameOutcome.Win));
        }

        [Test]
        public void Booster_StaysJammedWhenItOpensNoMove()
        {
            // shelf grows, but every remaining item is locked out of reach
            var board = new Board(L(
                E(1, "a"), E(2, "a"), E(3, "a"),
                Locked(4, "b", 5), Locked(5, "b", 5), Locked(6, "b", 5)));
            board.TakeItem(1);
            board.TakeItem(2);
            board.TakeItem(3);
            Assert.That(board.Outcome, Is.EqualTo(GameOutcome.ShelfJammed));

            board.AddShelfSlots(3);

            Assert.That(board.IsOver, Is.True, "extra room changes nothing here");
            Assert.That(board.Outcome, Is.EqualTo(GameOutcome.ShelfJammed));
        }
    }
}
