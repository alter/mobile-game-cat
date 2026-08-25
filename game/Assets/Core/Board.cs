using System;
using System.Collections.Generic;
using System.Linq;

namespace CatShelter.Core
{
    /// <summary>How a game ended.</summary>
    public enum GameOutcome
    {
        /// <summary>The pile is empty — the room corner is cleared, the player won.</summary>
        Win,
        /// <summary>The shelf is full with no match available — the player is stuck.</summary>
        ShelfJammed
    }

    /// <summary>
    /// The playing field: a pile of overlapped items plus the shelf. Decides
    /// win/lose. There is no move limit: winning means taking every item, so
    /// each move spends exactly one take and a limit either blocks the level or
    /// can never be reached (reviews/2026-08-24-refactor-difficulty.md).
    ///
    /// Partial information (task 3.9): a buried item's kind is hidden until it
    /// becomes reachable. Locked items (task 3.11) are unreachable until the
    /// player has completed enough triples, regardless of occlusion.
    /// </summary>
    public sealed class Board
    {
        private readonly Dictionary<int, PileEntry> _entries;
        private readonly HashSet<int> _taken;
        private readonly List<int> _takenOrder = new();

        /// <summary>Completed-triple count; unlocks locked items (task 3.11).</summary>
        public int TriplesCompleted { get; private set; }

        /// <summary>Ids already taken, in take order (task 6.7 serialisation).</summary>
        public IReadOnlyList<int> TakenOrder => _takenOrder;

        /// <summary>Locked items open after this many completed triples.</summary>
        public int LockThreshold { get; }

        public Level Level { get; }
        public Shelf Shelf { get; }

        public bool IsOver { get; private set; }
        public GameOutcome? Outcome { get; private set; }

        public Board(Level level)
            : this(level, Shelf.SlotsPerRow * Shelf.RowCount, lockThreshold: 0)
        {
        }

        public Board(Level level, int shelfCapacity, int lockThreshold = 0)
        {
            Level = level ?? throw new ArgumentNullException(nameof(level));
            if (lockThreshold < 0)
                throw new ArgumentOutOfRangeException(nameof(lockThreshold));
            LockThreshold = lockThreshold;
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
        /// Items currently reachable: still in the pile, nothing covering them,
        /// and not locked (or the lock has been satisfied).
        /// </summary>
        public IReadOnlyList<Item> GetAvailable()
        {
            return _entries.Values
                .Where(e => !_taken.Contains(e.Item.Id))
                .Where(e => e.BlockedBy.All(id => _taken.Contains(id)))
                .Where(e => !IsLockedByComplication(e.Item))
                .Select(e => e.Item)
                .ToList();
        }

        /// <summary>
        /// Task 3.9: an item shows its kind only once it is reachable —
        /// nothing covers it and no complication locks it.
        /// </summary>
        public bool IsRevealed(Item item)
        {
            if (_taken.Contains(item.Id))
                return false;
            var entry = _entries[item.Id];
            return entry.BlockedBy.All(id => _taken.Contains(id)) && !IsLockedByComplication(item);
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
            if (IsLockedByComplication(entry.Item))
                return false;

            _taken.Add(itemId);
            _takenOrder.Add(itemId);

            if (!Shelf.TryPlace(entry.Item, out var matchedKind))
            {
                // shelf full: the win still wins, otherwise it's a jam
                if (_taken.Count == _entries.Count)
                {
                    Finish(GameOutcome.Win);
                    return true;
                }
                Finish(GameOutcome.ShelfJammed);
                return true;
            }

            if (matchedKind is not null)
                TriplesCompleted++;

            // full-but-matched shelf with a pile remaining: nowhere for the
            // next item — jam (the booster answers exactly this)
            if (Shelf.IsFull && _taken.Count != _entries.Count)
            {
                Finish(GameOutcome.ShelfJammed);
                return true;
            }

            if (_taken.Count == _entries.Count)
            {
                Finish(GameOutcome.Win);
                return true;
            }

            return true;
        }

        /// <summary>
        /// Task 3.11: an item carrying LockedAfterTriples &gt; 0 stays locked until
        /// that many triples have been completed. Locked items are neither
        /// available nor revealed.
        /// </summary>
        public bool IsLockedByComplication(Item item)
        {
            return item.LockedAfterTriples > 0 && TriplesCompleted < item.LockedAfterTriples;
        }

        private void Finish(GameOutcome outcome)
        {
            IsOver = true;
            Outcome = outcome;
        }
    }
}
