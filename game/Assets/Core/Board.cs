using System;
using System.Collections.Generic;
using System.Linq;

namespace CatShelter.Core
{
    /// <summary>How a game ended.</summary>
    public enum GameOutcome
    {
        /// <summary>The pile is empty — the room is cleared, the player won.</summary>
        Win,
        /// <summary>The shelf is full with no match available — the player is stuck.</summary>
        ShelfJammed
    }

    /// <summary>
    /// The playing field: a pile of overlapped items plus the shelf. Decides
    /// win/lose. There is no move limit: winning means taking every item, so
    /// each move spends exactly one take and a limit either blocks the level or
    /// can never be reached (reviews/2026-08-24-refactor-difficulty.md).
    /// </summary>
    public sealed class Board
    {
        private readonly Dictionary<int, PileEntry> _entries;
        private readonly HashSet<int> _taken;

        public Level Level { get; }
        public Shelf Shelf { get; }
        public bool IsOver { get; private set; }
        public GameOutcome? Outcome { get; private set; }

        public Board(Level level) : this(level, Shelf.SlotsPerRow * Shelf.RowCount)
        {
        }

        public Board(Level level, int shelfCapacity)
        {
            Level = level ?? throw new ArgumentNullException(nameof(level));
            Shelf = new Shelf(shelfCapacity);
            // Every kind must appear in triples: otherwise the pile empties
            // while items remain stranded on the shelf and the win condition
            // fires on an unfinished board.
            foreach (var group in level.Pile.GroupBy(e => e.Item.Kind.Id))
            {
                if (group.Count() % 3 != 0)
                    throw new ArgumentException(
                        $"kind '{group.Key}' appears {group.Count()} times, " +
                        "not a multiple of three", nameof(level));
            }
            _entries = level.Pile.ToDictionary(e => e.Item.Id);
            // Reject duplicate ids up front: occlusion bookkeeping relies on
            // them being unique.
            if (_entries.Count != level.Pile.Count)
                throw new ArgumentException("Duplicate item ids in the pile", nameof(level));
            _taken = new HashSet<int>();
        }

        /// <summary>
        /// Items currently reachable: those still in the pile with nothing on top
        /// of them (nothing they are blocked by is still in the pile).
        /// </summary>
        public IReadOnlyList<Item> GetAvailable()
        {
            return _entries.Values
                .Where(e => !_taken.Contains(e.Item.Id))
                .Where(e => e.BlockedBy.All(id => _taken.Contains(id)))
                .Select(e => e.Item)
                .ToList();
        }

        /// <summary>Take an item from the pile and put it on the shelf.</summary>
        /// <remarks>
        /// Win is checked before the jam: an empty pile is a win even when the
        /// final placement happens to fill the shelf. A full unmatched shelf is
        /// the only loss — that is what the "+1 slot" booster answers.
        /// </remarks>
        public bool TakeItem(int itemId)
        {
            if (IsOver)
                return false;
            if (!_entries.TryGetValue(itemId, out var entry) || _taken.Contains(itemId))
                return false;
            if (!entry.BlockedBy.All(id => _taken.Contains(id)))
                return false;

            _taken.Add(itemId);

            if (_taken.Count == _entries.Count)
            {
                Finish(GameOutcome.Win);
                return true;
            }

            if (!Shelf.TryPlace(entry.Item, out _) || Shelf.IsFull)
            {
                Finish(GameOutcome.ShelfJammed);
                return true;
            }

            return true;
        }

        private void Finish(GameOutcome outcome)
        {
            IsOver = true;
            Outcome = outcome;
        }
    }
}
