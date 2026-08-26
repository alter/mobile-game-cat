using System;
using System.Collections.Generic;
using System.Linq;

namespace CatShelter.Core
{
    /// <summary>
    /// Task 60-shell-build/02, the half that is not art: which pile of which
    /// room a level is, and how far through its room the player has got.
    ///
    /// Rooms hold one to four piles (D2). Clearing a pile clears a corner;
    /// clearing the last pile of a room finishes the room, which is what moves
    /// the cat on and pays the large reward. The view needs both numbers to
    /// decide how much clutter to draw and when to swap the background — but
    /// the arithmetic is a rule, so it lives here where it can be tested.
    ///
    /// Built from the shipped levels rather than from a second hand-written
    /// table: `View/LevelAssets.cs` already mirrors `pacing.py` once, and a
    /// third copy of 1,2,3,3,3,3,3,3,4,4,4,4 would be one to keep in step.
    /// </summary>
    public sealed class RoomPlan
    {
        private readonly IReadOnlyList<Level> _levels;
        private readonly Dictionary<string, int> _pilesPerRoom;

        public RoomPlan(IReadOnlyList<Level> levels)
        {
            if (levels == null) throw new ArgumentNullException(nameof(levels));
            if (levels.Count == 0) throw new ArgumentException("no levels", nameof(levels));

            _levels = levels.OrderBy(l => l.Number).ToList();
            _pilesPerRoom = _levels.GroupBy(l => l.RoomId)
                                   .ToDictionary(g => g.Key, g => g.Count());

            foreach (var group in _levels.GroupBy(l => l.RoomId))
            {
                var indices = group.Select(l => l.PileIndex).OrderBy(i => i).ToList();
                // A gap here would make "pile 2 of 3" a lie and would leave a
                // corner of the room that no level ever clears.
                if (!indices.SequenceEqual(Enumerable.Range(0, indices.Count)))
                    throw new ArgumentException(
                        $"room {group.Key}: pile indices are {string.Join(",", indices)}, " +
                        "expected a gapless run from 0", nameof(levels));
            }
        }

        public int RoomCount => _pilesPerRoom.Count;

        /// <summary>How many piles this room holds, 1 to 4.</summary>
        public int PilesIn(string roomId) =>
            _pilesPerRoom.TryGetValue(roomId, out var count) ? count : 0;

        /// <summary>Is this the pile that finishes its room.</summary>
        public bool IsLastPileOfRoom(Level level) =>
            level != null && level.PileIndex == PilesIn(level.RoomId) - 1;

        /// <summary>
        /// How clean the room looks after this pile is cleared, 0 to 1. The
        /// view turns it into "which corners still have clutter"; here it is
        /// just the fraction, so the two never disagree about what "half done"
        /// means.
        /// </summary>
        public float ClearedFractionAfter(Level level)
        {
            if (level == null) return 0f;
            var piles = PilesIn(level.RoomId);
            return piles == 0 ? 0f : (float)(level.PileIndex + 1) / piles;
        }

        /// <summary>The level that follows, or null at the end of the house.</summary>
        public Level Next(Level level)
        {
            var index = IndexOf(level);
            return index >= 0 && index + 1 < _levels.Count ? _levels[index + 1] : null;
        }

        public int IndexOf(Level level)
        {
            for (int i = 0; i < _levels.Count; i++)
                if (ReferenceEquals(_levels[i], level)) return i;
            return -1;
        }

        /// <summary>1-based room number parsed from "room_07".</summary>
        public static int RoomNumber(string roomId)
        {
            if (string.IsNullOrEmpty(roomId)) return 0;
            var digits = new string(roomId.Where(char.IsDigit).ToArray());
            return digits.Length > 0 && int.TryParse(digits, out var number) ? number : 0;
        }

        /// <summary>Piles per room in play order — what a progress tracker needs.</summary>
        public IReadOnlyList<int> PilesPerRoomInOrder() =>
            _levels.Select(l => l.RoomId).Distinct()
                   .Select(roomId => _pilesPerRoom[roomId])
                   .ToList();
    }
}
