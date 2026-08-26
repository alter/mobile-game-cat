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
        /// <summary>
        /// The player is stuck: either the shelf is full with no match available,
        /// or nothing in the pile can be taken because every remaining item is
        /// locked by a complication. Both are one outcome because they are one
        /// thing to the player — no move exists — and the booster answers both.
        /// </summary>
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

        public Level Level { get; }
        public Shelf Shelf { get; }

        public bool IsOver { get; private set; }
        public GameOutcome? Outcome { get; private set; }

        public Board(Level level)
            : this(level, Shelf.SlotsPerRow * Shelf.RowCount)
        {
        }

        public Board(Level level, int shelfCapacity)
        {
            Level = level ?? throw new ArgumentNullException(nameof(level));
            Shelf = new Shelf(shelfCapacity);
            // Kinds-in-triples and unique ids are enforced by Level itself, so
            // a Board cannot be handed a pile that breaks either.
            _entries = level.Pile.ToDictionary(e => e.Item.Id);
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

        /// <summary>Whether this item has already left the pile.</summary>
        public bool IsTaken(int itemId) => _taken.Contains(itemId);

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

            // Pile not empty, shelf not full, and yet nothing can be taken:
            // every remaining item is locked and there are not enough triples
            // to open them. Without this the board hangs — no outcome, no
            // moves, and on a phone no way out.
            if (GetAvailable().Count == 0)
                Finish(GameOutcome.ShelfJammed);

            return true;
        }

        /// <summary>
        /// Grow the shelf by <paramref name="extra"/> slots and, if that ended a
        /// jam, resume play (the "one more shelf" booster, DECISIONS.md D4).
        /// The MVP never calls this — the booster is a fake door — but the rules
        /// mirror in tools/solver/rules.py has always resumed, and the two must
        /// agree. Stays jammed when the extra room changes nothing.
        /// </summary>
        public void AddShelfSlots(int extra)
        {
            Shelf.AddSlots(extra);
            if (!IsOver || Outcome != GameOutcome.ShelfJammed)
                return;
            if (Shelf.IsFull || GetAvailable().Count == 0)
                return;
            IsOver = false;
            Outcome = null;
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
