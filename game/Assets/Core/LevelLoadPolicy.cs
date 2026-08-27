using System;
using System.Collections.Generic;
using System.Linq;

namespace CatShelter.Core
{
    /// <summary>
    /// Task 30-levels-solver/06: deciding what to do when a shipped level file
    /// fails to parse is a rule, so it lives here — the same split
    /// <see cref="SaveResume"/> uses for a corrupt save. The view only supplies
    /// whatever it managed to parse; this decides what is safe to hand to
    /// <see cref="RoomPlan"/> and whether there is anything to play at all.
    ///
    /// <see cref="RoomPlan"/> requires a gapless run of pile indices
    /// (0..count-1) within every room it is given. Dropping only the one bad
    /// pile out of the middle of a room would leave a gap there, and
    /// RoomPlan would refuse the whole set — trading the crash this class
    /// exists to avoid for the same crash one call deeper. So a bad file
    /// costs its whole room, not the whole game: every OTHER room is
    /// untouched. Losing a room out of twelve is a setback; a launch crash
    /// loses the player — SaveResume's own reasoning, applied here.
    ///
    /// Only when nothing survives at all is there truly nothing to show, and
    /// only then does this refuse outright.
    /// </summary>
    public static class LevelLoadPolicy
    {
        public readonly struct Result
        {
            /// <summary>Safe to hand to <see cref="RoomPlan"/>: zero or more
            /// complete rooms, each a gapless run from pile 0.</summary>
            public IReadOnlyList<Level> Levels { get; }

            /// <summary>Room ids dropped because a file in them was missing or
            /// failed to parse — for a log line, not for the player.</summary>
            public IReadOnlyList<string> IncompleteRooms { get; }

            /// <summary>False only when every room came back incomplete —
            /// nothing survived to build a house from.</summary>
            public bool CanStart => Levels.Count > 0;

            public Result(IReadOnlyList<Level> levels, IReadOnlyList<string> incompleteRooms)
            {
                Levels = levels;
                IncompleteRooms = incompleteRooms;
            }
        }

        /// <summary>
        /// <paramref name="parsed"/> is whatever the caller managed to parse —
        /// possibly every shipped level, possibly missing some because a file
        /// was unreadable, malformed, or failed one of <see cref="Level"/>'s
        /// validity checks. <paramref name="expectedPilesPerRoom"/> says how
        /// many piles each room is supposed to have (room id -&gt; count), so
        /// a room missing its LAST file — which would otherwise look like a
        /// legitimately shorter, still-gapless room — is caught too. Never
        /// throws: a missing or malformed shipped file is a data bug, not a
        /// reason to crash the player's launch.
        /// </summary>
        public static Result Resolve(IReadOnlyList<Level> parsed,
                                     IReadOnlyDictionary<string, int> expectedPilesPerRoom)
        {
            if (parsed is null) throw new ArgumentNullException(nameof(parsed));
            if (expectedPilesPerRoom is null)
                throw new ArgumentNullException(nameof(expectedPilesPerRoom));

            var byRoom = parsed.GroupBy(l => l.RoomId)
                                .ToDictionary(g => g.Key, g => (IReadOnlyList<Level>)g.ToList());

            var kept = new List<Level>();
            var incomplete = new List<string>();

            foreach (var roomId in expectedPilesPerRoom.Keys.OrderBy(id => id, StringComparer.Ordinal))
            {
                var expected = expectedPilesPerRoom[roomId];
                if (!byRoom.TryGetValue(roomId, out var levels))
                    levels = Array.Empty<Level>();

                var indices = levels.Select(l => l.PileIndex).OrderBy(i => i).ToList();
                if (indices.SequenceEqual(Enumerable.Range(0, expected)))
                    kept.AddRange(levels);
                else
                    incomplete.Add(roomId);
            }

            return new Result(kept.OrderBy(l => l.Number).ToList(), incomplete);
        }
    }
}
