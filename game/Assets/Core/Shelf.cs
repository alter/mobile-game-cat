using System;
using System.Collections.Generic;
using System.Linq;

namespace CatShelter.Core
{
    /// <summary>
    /// The shelf the player places items on. Default layout is three rows of
    /// three slots ("three shelves of three", section 3 of cat-shelter-mvp.md);
    /// rows are presentation only — matching looks across all slots.
    /// Capacity is mutable: the lose-screen booster "+1 slot" grows it.
    /// </summary>
    public sealed class Shelf
    {
        public const int SlotsPerRow = 3;
        public const int RowCount = 3;

        private Item?[] _slots;

        public Shelf(int capacity = SlotsPerRow * RowCount)
        {
            if (capacity < 1)
                throw new ArgumentOutOfRangeException(nameof(capacity));
            _slots = new Item?[capacity];
        }

        /// <summary>
        /// Grow the shelf by <paramref name="extra"/> slots. Call
        /// <see cref="Board.AddShelfSlots"/> instead when a game is in progress:
        /// this one only widens the shelf and leaves a jammed board over.
        /// </summary>
        public void AddSlots(int extra)
        {
            if (extra < 0)
                throw new ArgumentOutOfRangeException(nameof(extra));
            var grown = new Item?[_slots.Length + extra];
            Array.Copy(_slots, grown, _slots.Length);
            _slots = grown;
        }

        public int Capacity => _slots.Length;

        /// <summary>Number of currently occupied slots.</summary>
        public int Occupied => _slots.Count(s => s is not null);

        public bool IsFull => Occupied == Capacity;

        public IReadOnlyList<Item?> Slots => _slots;

        /// <summary>
        /// Place an item into the leftmost free slot — which, after a triple has
        /// been cleared, is a gap in the middle rather than the end of the row.
        /// Deliberate; see TryMatch and DECISIONS.md D16.
        /// Returns false when the shelf is already full — the item does not fit.
        /// Matching is attempted after placement; matched triples are removed and
        /// reported through <paramref name="matchedKind"/> (null when nothing matched).
        /// </summary>
        public bool TryPlace(Item item, out ItemKind? matchedKind)
        {
            if (item is null) throw new ArgumentNullException(nameof(item));
            matchedKind = null;
            int free = Array.IndexOf(_slots, null);
            if (free < 0)
                return false;

            _slots[free] = item;
            TryMatch(out matchedKind);
            return true;
        }

        /// <summary>
        /// If some kind occupies three slots, remove those items and report the kind.
        /// A single placement adds a single item, so at most one triple can complete
        /// per call; the method removes that triple and returns.
        /// </summary>
        /// <remarks>
        /// The three slots are emptied where they stand and nothing shifts along:
        /// the shelf neither compacts nor sorts (DECISIONS.md D16, decided
        /// 2026-08-27 after the owner saw the gaps in play and asked). This is
        /// not an unfinished implementation, and the genre's habit of sliding
        /// items left and grouping like with like was turned down on purpose —
        /// grouping would hand the player the work of spotting a pair, which is
        /// part of what the game asks of them.
        /// </remarks>
        public bool TryMatch(out ItemKind? matchedKind)
        {
            foreach (var group in _slots.OfType<Item>().GroupBy(i => i.Kind.Id))
            {
                if (group.Count() >= 3)
                {
                    var kind = group.Key;
                    int removed = 0;
                    for (int i = 0; i < _slots.Length && removed < 3; i++)
                    {
                        if (_slots[i]?.Kind.Id == kind)
                        {
                            _slots[i] = null;
                            removed++;
                        }
                    }
                    matchedKind = group.First().Kind;
                    return true;
                }
            }
            matchedKind = null;
            return false;
        }
    }
}
