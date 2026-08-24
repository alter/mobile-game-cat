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
        /// <summary>Moves ran out with items still in the pile.</summary>
        OutOfMoves,
        /// <summary>The shelf is full with no match available.</summary>
        ShelfJammed
    }

    /// <summary>
    /// The playing field: a pile of overlapped items plus the shelf. Owns the move
    /// counter and decides win/lose. This is the whole rules engine.
    /// </summary>
    public sealed class Board
    {
        private readonly Dictionary<int, PileEntry> _entries;
        private readonly HashSet<int> _taken;

        public Level Level { get; }
        public Shelf Shelf { get; } = new();
        public int MovesLeft { get; private set; }
        public bool IsOver { get; private set; }
        public GameOutcome? Outcome { get; private set; }

        public Board(Level level)
        {
            Level = level ?? throw new ArgumentNullException(nameof(level));
            MovesLeft = level.MovesLimit;
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
        /// A jam (shelf full) or running out of moves ends the game immediately;
        /// an item already taken from the pile stays on the shelf in both cases,
        /// matching how such games behave visually.
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

            if (!Shelf.TryPlace(entry.Item, out _))
            {
                Finish(GameOutcome.ShelfJammed);
                return true;
            }

            // A completely full shelf with nothing matched is also a jam: the
            // player has nowhere to put the next item.
            if (Shelf.IsFull)
            {
                Finish(GameOutcome.ShelfJammed);
                return true;
            }

            if (_taken.Count == _entries.Count)
            {
                Finish(GameOutcome.Win);
                return true;
            }

            MovesLeft--;
            if (MovesLeft <= 0)
                Finish(GameOutcome.OutOfMoves);
            return true;
        }

        private void Finish(GameOutcome outcome)
        {
            IsOver = true;
            Outcome = outcome;
        }
    }
}
