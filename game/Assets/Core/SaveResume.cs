using System;
using System.Collections.Generic;

namespace CatShelter.Core
{
    /// <summary>
    /// Task 60-shell-build/08: deciding whether a save can be resumed is a rule,
    /// so it lives here rather than in the view. The view only supplies the text
    /// it read from disk and the levels it has loaded.
    ///
    /// Every failure resolves to "start fresh" and none of them throws: a save
    /// can be missing, truncated, from a level that no longer ships, or describe
    /// a position the rules reject. Losing a pile is a setback; a crash on
    /// launch loses the player.
    /// </summary>
    public static class SaveResume
    {
        /// <summary>
        /// The board to resume into, or null when there is nothing usable.
        /// <paramref name="reason"/> says why nothing came back — it is meant
        /// for a log line, not for the player.
        /// </summary>
        public static Board TryResume(string savedText, IReadOnlyList<Level> levels,
                                      out string reason)
        {
            if (levels is null) throw new ArgumentNullException(nameof(levels));

            var saved = GameSave.Read(savedText);
            if (saved is null)
            {
                reason = "no readable save";
                return null;
            }

            Level level = null;
            foreach (var candidate in levels)
            {
                if (candidate.Number != saved.LevelNumber) continue;
                level = candidate;
                break;
            }
            if (level is null)
            {
                reason = $"level {saved.LevelNumber} is not among the shipped levels";
                return null;
            }

            try
            {
                var board = BoardSave.Restore(level, new BoardSnapshot(
                    saved.LevelNumber, saved.RoomId, saved.PileIndex,
                    saved.TakenOrder, saved.ShelfKinds, saved.TriplesCompleted));

                // A finished position is not worth resuming into: the outcome
                // card is gone with the process, so the player would face a
                // board that refuses every tap.
                if (board.IsOver)
                {
                    reason = "saved position is already over";
                    return null;
                }

                reason = null;
                return board;
            }
            catch (InvalidOperationException e)
            {
                reason = e.Message;
                return null;
            }
        }

        /// <summary>Index of that level in <paramref name="levels"/>, or -1.</summary>
        public static int IndexOf(IReadOnlyList<Level> levels, Board board)
        {
            for (int i = 0; i < levels.Count; i++)
                if (ReferenceEquals(levels[i], board.Level))
                    return i;
            return -1;
        }
    }
}
