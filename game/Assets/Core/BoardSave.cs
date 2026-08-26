using System;
using System.Collections.Generic;
using System.Linq;

namespace CatShelter.Core
{
    /// <summary>
    /// Task 6.7: the board is serialisable, not just the level number.
    /// Taken items, shelf contents and triples completed are enough to rebuild
    /// an identical Board; written after every move by the presentation layer.
    /// </summary>
    public sealed class BoardSnapshot
    {
        public int LevelNumber { get; }
        public string RoomId { get; }
        public int PileIndex { get; }
        /// <summary>Ids already taken from the pile, in take order.</summary>
        public IReadOnlyList<int> Taken { get; }
        /// <summary>Kind id per shelf slot, null where empty. Length = capacity.</summary>
        public IReadOnlyList<string?> Shelf { get; }
        public int TriplesCompleted { get; }

        public BoardSnapshot(int levelNumber, string roomId, int pileIndex,
                             IReadOnlyList<int> taken,
                             IReadOnlyList<string?> shelf,
                             int triplesCompleted)
        {
            LevelNumber = levelNumber;
            RoomId = roomId ?? throw new ArgumentNullException(nameof(roomId));
            PileIndex = pileIndex;
            Taken = taken ?? throw new ArgumentNullException(nameof(taken));
            Shelf = shelf ?? throw new ArgumentNullException(nameof(shelf));
            TriplesCompleted = triplesCompleted;
        }
    }

    public static class BoardSave
    {
        /// <summary>Capture the current position.</summary>
        public static BoardSnapshot Capture(Board board)
        {
            if (board is null) throw new ArgumentNullException(nameof(board));
            return new BoardSnapshot(
                board.Level.Number,
                board.Level.RoomId,
                board.Level.PileIndex,
                board.TakenOrder.ToList(),
                board.Shelf.Slots.Select(s => s?.Kind.Id).ToList(),
                board.TriplesCompleted);
        }

        /// <summary>
        /// Rebuild a live Board from a snapshot: replays the recorded takes so
        /// occlusion state, reveal flags and triple count come out identical.
        /// The snapshot's shelf contents are validated against the replay.
        ///
        /// The shelf is rebuilt at its saved capacity, not the default one
        /// (DECISIONS.md D12 lists capacity among what must survive). Replaying
        /// on the grown shelf from move one reaches the same slots, because
        /// placement always takes the leftmost free one; what it skips is a jam
        /// the booster had already undone.
        /// </summary>
        public static Board Restore(Level level, BoardSnapshot snapshot)
        {
            if (level is null) throw new ArgumentNullException(nameof(level));
            if (snapshot is null) throw new ArgumentNullException(nameof(snapshot));
            if (snapshot.Shelf.Count < 1)
                throw new InvalidOperationException("snapshot corrupt: shelf capacity");

            var board = new Board(level, snapshot.Shelf.Count);
            foreach (var id in snapshot.Taken)
            {
                if (!board.TakeItem(id))
                    throw new InvalidOperationException(
                        $"snapshot corrupt: cannot retake item {id}");
            }

            // verify the rebuilt shelf matches what was saved
            for (int i = 0; i < snapshot.Shelf.Count; i++)
            {
                var expected = snapshot.Shelf[i];
                var actual = i < board.Shelf.Capacity
                    ? board.Shelf.Slots[i]?.Kind.Id : null;
                if (expected != actual)
                    throw new InvalidOperationException(
                        $"snapshot corrupt: shelf slot {i} " +
                        $"expected '{expected}', got '{actual}'");
            }
            if (snapshot.TriplesCompleted != board.TriplesCompleted)
                throw new InvalidOperationException("snapshot corrupt: triple count");

            return board;
        }
    }
}
