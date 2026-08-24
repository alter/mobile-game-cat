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

    private static Level MakeLevel(int movesLimit, params PileEntry[] pile) =>
        new(1, "room_1", movesLimit, pile);

    // ---- GetAvailable ----------------------------------------------------

    [Test]
    public void EmptyPile_NoAvailableItems()
    {
        var board = new Board(MakeLevel(10));
        Assert.That(board.GetAvailable(), Is.Empty);
    }

    [Test]
    public void SingleLayer_AllItemsAvailable()
    {
        var board = new Board(MakeLevel(10, Entry(1, "a"), Entry(2, "b")));
        Assert.That(board.GetAvailable().Select(i => i.Id), Is.EquivalentTo(new[] { 1, 2 }));
    }

    [Test]
    public void ThreeLayers_OnlyTopOfEachStackAvailable()
    {
        // 3 sits on 2, 2 sits on 1; 4 is separate.
        var board = new Board(MakeLevel(10,
            Entry(1, "a", 2),
            Entry(2, "a", 3),
            Entry(3, "a"),
            Entry(4, "b")));
        Assert.That(board.GetAvailable().Select(i => i.Id), Is.EquivalentTo(new[] { 3, 4 }));
    }

    [Test]
    public void CircularBlock_NothingInCycleAvailable()
    {
        // 1 blocked by 2, 2 blocked by 1 — neither can ever be taken.
        var board = new Board(MakeLevel(10,
            Entry(1, "a", 2),
            Entry(2, "b", 1),
            Entry(3, "c")));
        Assert.That(board.GetAvailable().Select(i => i.Id), Is.EquivalentTo(new[] { 3 }));
    }

    [Test]
    public void AfterTakingTopItem_ItemBelowBecomesAvailable()
    {
        var board = new Board(MakeLevel(10,
            Entry(1, "a", 2),
            Entry(2, "b"),
            Entry(3, "c")));
        Assert.That(board.TakeItem(2), Is.True);
        Assert.That(board.GetAvailable().Select(i => i.Id), Is.EquivalentTo(new[] { 1, 3 }));
    }

    [Test]
    public void CannotTakeBlockedItem()
    {
        var board = new Board(MakeLevel(10, Entry(1, "a", 2), Entry(2, "b")));
        Assert.That(board.TakeItem(1), Is.False);
    }

    [Test]
    public void CannotTakeSameItemTwice()
    {
        var board = new Board(MakeLevel(10, Entry(1, "a"), Entry(2, "b")));
        Assert.That(board.TakeItem(1), Is.True);
        Assert.That(board.TakeItem(1), Is.False);
    }

    [Test]
    public void UnknownItemId_Rejected()
    {
        var board = new Board(MakeLevel(10, Entry(1, "a")));
        Assert.That(board.TakeItem(99), Is.False);
    }

    [Test]
    public void DuplicateItemIds_ConstructorThrows()
    {
        Assert.Throws<ArgumentException>(() =>
            new Board(MakeLevel(10, Entry(1, "a"), Entry(1, "b"))));
    }

    // ---- Game over states -------------------------------------------------

    [Test]
    public void TakingItemAfterGameIsOver_Rejected()
    {
        var board = new Board(MakeLevel(1, Entry(1, "a")));
        board.TakeItem(1); // this is the win
        Assert.That(board.TakeItem(1), Is.False);
    }
}

[TestFixture]
public class ShelfTests
{
    private static Item Item(string kind, int id = 0) =>
        new(id, new ItemKind(kind, kind));

    // ---- Place / free slots ----------------------------------------------

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

        // All three matched and were removed, slots are free again.
        Assert.That(shelf.Occupied, Is.EqualTo(0));

        var next = Item("y", 10);
        Assert.That(shelf.TryPlace(next, out _), Is.True);
        Assert.That(shelf.Slots[0], Is.SameAs(next));
    }

    // ---- Matching at slot boundaries --------------------------------------

    [Test]
    public void Match_CompletesAcrossRowBoundary()
    {
        // Slots per row is 3: two of a kind fill row 0, the third lands in
        // row 1 — the match must still fire across the boundary.
        Assert.That(Shelf.SlotsPerRow, Is.EqualTo(3));

        var shelf = new Shelf();
        shelf.TryPlace(Item("m", 1), out _);
        shelf.TryPlace(Item("m", 2), out _);
        Assert.That(shelf.Occupied, Is.EqualTo(2), "no match before the third copy");

        Assert.That(shelf.TryPlace(Item("m", 3), out var matched), Is.True);
        Assert.That(matched!.Id, Is.EqualTo("m"));
        Assert.That(shelf.Occupied, Is.EqualTo(0));
    }

    [Test]
    public void Match_DoesNotFireWithTwoOfAKind()
    {
        var shelf = new Shelf();
        shelf.TryPlace(Item("a", 1), out _);
        shelf.TryPlace(Item("a", 2), out _);
        Assert.That(shelf.Occupied, Is.EqualTo(2));
    }

    // ---- Full shelf -------------------------------------------------------

    [Test]
    public void FullShelf_PlacementRefused()
    {
        var shelf = new Shelf();
        // Nine items, three complete matches along the way keep clearing space,
        // so fill with nine distinct kinds instead.
        var kinds = Enumerable.Range(0, Shelf.Capacity).Select(i => $"k{i}").ToList();
        foreach (var k in kinds)
            Assert.That(shelf.TryPlace(Item(k), out _), Is.True);

        Assert.That(shelf.IsFull, Is.True);
        Assert.That(shelf.TryPlace(Item("extra"), out _), Is.False);
    }
}

[TestFixture]
public class OutcomeTests
{
    private static PileEntry E(int id, string kind, params int[] blockedBy) =>
        new(new Item(id, new ItemKind(kind, kind)), blockedBy.ToList());

    private static Level L(int moves, params PileEntry[] pile) => new(7, "room_1", moves, pile);

    [Test]
    public void Win_PileCleared()
    {
        var board = new Board(L(5, E(1, "a"), E(2, "a"), E(3, "a")));
        board.TakeItem(1);
        board.TakeItem(2);
        Assert.That(board.IsOver, Is.False);
        board.TakeItem(3); // third of a kind matches, pile empties

        Assert.That(board.IsOver, Is.True);
        Assert.That(board.Outcome, Is.EqualTo(GameOutcome.Win));
    }

    [Test]
    public void OutOfMoves_MovesExhaustedWithPileRemaining()
    {
        // Three different kinds, one move allowed: after the single take the
        // counter hits zero while items remain.
        var board = new Board(L(1, E(1, "a"), E(2, "b")));
        board.TakeItem(1);

        Assert.That(board.IsOver, Is.True);
        Assert.That(board.Outcome, Is.EqualTo(GameOutcome.OutOfMoves));
        Assert.That(board.MovesLeft, Is.EqualTo(0));
    }

    [Test]
    public void FullShelfAfterPlacement_IsAJam_EvenWithoutTenthItem()
    {
        var entries = Enumerable.Range(1, 9)
            .Select(i => E(i, $"kind{i}"))
            .ToArray();
        var board = new Board(L(50, entries));

        for (int i = 1; i <= 9 && !board.IsOver; i++)
            board.TakeItem(i);

        Assert.That(board.IsOver, Is.True);
        Assert.That(board.Outcome, Is.EqualTo(GameOutcome.ShelfJammed));
    }

    [Test]
    public void OutcomesAreThreeAndDistinct()
    {
        var values = new HashSet<GameOutcome>
        {
            GameOutcome.Win,
            GameOutcome.OutOfMoves,
            GameOutcome.ShelfJammed
        };
        Assert.That(values.Count, Is.EqualTo(3));
    }

    [Test]
    public void MoveCounter_DecrementsPerTake_NotPerWin()
    {
        var board = new Board(L(10, E(1, "a"), E(2, "a"), E(3, "a")));
        board.TakeItem(1);
        Assert.That(board.MovesLeft, Is.EqualTo(9));
        board.TakeItem(2);
        Assert.That(board.MovesLeft, Is.EqualTo(8));
        // The winning take does not consume a move: the game ends on the
        // placement itself, before the counter would be touched.
        board.TakeItem(3);
        Assert.That(board.IsOver, Is.True);
        Assert.That(board.MovesLeft, Is.EqualTo(8));
    }
}
}
