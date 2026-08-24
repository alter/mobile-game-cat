using System;
using System.Collections.Generic;
using System.Linq;

namespace CatShelter.Core
{
    /// <summary>
    /// Three shelves of three slots each (nine places total), as in section 3 of
    /// cat-shelter-mvp.md. Placing an item takes a free slot; when three items of
    /// the same kind occupy the shelf they match and disappear.
    /// </summary>
    public sealed class Shelf
    {
        public const int SlotsPerRow = 3;
        public const int RowCount = 3;
        public const int Capacity = SlotsPerRow * RowCount;

        private readonly Item?[] _slots = new Item?[Capacity];

        /// <summary>Number of currently occupied slots.</summary>
        public int Occupied => _slots.Count(s => s is not null);

        public bool IsFull => Occupied == Capacity;

        public IReadOnlyList<Item?> Slots => _slots;

        /// <summary>
        /// Place an item into the leftmost free slot.
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
        /// Only one triple can complete at once because a single placement adds a
        /// single item, but the method clears every completed kind it finds.
        /// </summary>
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
