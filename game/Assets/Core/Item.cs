using System;

namespace CatShelter.Core
{
    /// <summary>A kind of prop that appears in a pile ("vase", "book", ...).</summary>
    public sealed class ItemKind
    {
        public string Id { get; }
        public string SpriteId { get; }

        public ItemKind(string id, string spriteId)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            SpriteId = spriteId ?? throw new ArgumentNullException(nameof(spriteId));
        }

        public override string ToString() => $"ItemKind({Id})";
    }

    /// <summary>
    /// One physical item sitting at a position in the pile.
    /// LockedAfterTriples &gt; 0 marks a complication (task 3.11): the item stays
    /// locked until that many triples have been completed.
    /// </summary>
    public sealed class Item
    {
        public int Id { get; }
        public ItemKind Kind { get; }
        public int LockedAfterTriples { get; }

        public Item(int id, ItemKind kind, int lockedAfterTriples = 0)
        {
            if (lockedAfterTriples < 0)
                throw new ArgumentOutOfRangeException(nameof(lockedAfterTriples));
            Id = id;
            Kind = kind ?? throw new ArgumentNullException(nameof(kind));
            LockedAfterTriples = lockedAfterTriples;
        }

        public override string ToString() => $"Item({Id}, {Kind.Id})";
    }
}
