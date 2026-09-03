using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;

namespace CatShelter.Core
{
    /// <summary>
    /// Task 60-shell-build/08: mid-level save written every move.
    ///
    /// Serialises the full position — taken items (order matters: replaying
    /// rebuilds occlusion and locks), shelf contents, shelf capacity (mutable,
    /// see the booster decision), current room and pile — to a plain text
    /// format with zero dependencies, so the exact same code runs in Unity and
    /// under dotnet tests.
    ///
    /// Deliberately NOT System.Text.Json (IL2CPP-forbidden) and not
    /// Newtonsoft-inside-Core (Core stays dependency-free). If the Shell later
    /// prefers JsonUtility it can serialise the same fields; this format is the
    /// lossless ground truth.
    /// </summary>
    public static class GameSave
    {
        public const string Header = "catshelter-save-v1";

        /// <summary>
        /// The line that says the first lesson has been played
        /// (60-shell-build/28). Written only when it has; an older file simply
        /// has no such line and reads back as "not played", which is the same
        /// shape as the `cap` fallback below.
        /// </summary>
        private const string LessonLine = "lesson 1";

        /// <summary>Capture the live board plus progress cursor.</summary>
        /// <param name="lessonSeen">Whether the first lesson has been played
        /// through. Carried in the save because it must outlive the process and
        /// there is nowhere else in this game that a player-lifetime fact
        /// lives.</param>
        public static string Write(Board board, PlayerProgress progress,
                                   bool lessonSeen = false)
        {
            if (board is null) throw new ArgumentNullException(nameof(board));
            var snap = BoardSave.Capture(board);
            return Write(snap, progress, lessonSeen);
        }

        public static string Write(BoardSnapshot snap, PlayerProgress progress,
                                   bool lessonSeen = false)
        {
            if (snap is null) throw new ArgumentNullException(nameof(snap));
            var lines = new List<string>
            {
                Header,
                // level identity
                $"level {snap.LevelNumber} {snap.RoomId} {snap.PileIndex}",
                // capacity on its own line, ahead of the shelf contents: a
                // real kind name can start with "cap" (e.g. "prop_capstan"),
                // and folding capacity into the shelf line as a trailing
                // "capN" token made that name indistinguishable from the
                // marker — int.Parse on its non-numeric tail threw and lost
                // the whole file. A dedicated line has nothing to collide
                // with.
                $"cap {snap.Shelf.Count}",
                // shelf: kind ids, '_' for empty
                "shelf " + string.Join(" ", snap.Shelf.Select(s => s ?? "_")),
                $"triples {snap.TriplesCompleted}",
                // taken ids in take order (replay drives occlusion + locks)
                "taken " + string.Join(" ", snap.Taken),
            };
            if (lessonSeen) lines.Add(LessonLine);
            if (progress != null)
            {
                lines.Add($"cursor {progress.CurrentRoom} {progress.CurrentPile}");
                lines.Add("roomsdone " + string.Join(" ", progress.RoomsDone));
            }
            return string.Join("\n", lines) + "\n";
        }

        /// <summary>
        /// Parse a save. Returns null on anything malformed — callers fall
        /// back to a fresh board, never crash (task VERIFY 1).
        /// </summary>
        public static SavedGame Read(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            try
            {
                var lines = text.Replace("\r\n", "\n")
                                .Split('\n', StringSplitOptions.RemoveEmptyEntries);
                if (lines[0].Trim() != Header) return null;

                int levelNumber = 0; string roomId = null; int pileIndex = 0;
                List<string> shelf = null; int triples = -1;
                List<int> taken = null;
                int cursorRoom = 1, cursorPile = 0; List<int> roomsDone = null;
                bool lessonSeen = false;
                // Set only by a "cap" line, written ahead of "shelf" since
                // 08-save-hardening. Old files never have one; see the
                // "shelf" case below for the two read paths this drives.
                int? capLine = null;

                foreach (var raw in lines.Skip(1))
                {
                    var parts = raw.Trim().Split(' ');
                    switch (parts[0])
                    {
                        case "level":
                            levelNumber = int.Parse(parts[1], CultureInfo.InvariantCulture);
                            roomId = parts[2];
                            pileIndex = int.Parse(parts[3], CultureInfo.InvariantCulture);
                            break;
                        case "cap":
                            capLine = int.Parse(parts[1], CultureInfo.InvariantCulture);
                            break;
                        case "shelf":
                            shelf = new List<string>();
                            int capacity;
                            if (capLine.HasValue)
                            {
                                // Current format: capacity already came off its
                                // own "cap" line, so every token here is a plain
                                // item — including one that happens to start
                                // with "cap" (e.g. "prop_capstan").
                                capacity = capLine.Value;
                                foreach (var tok in parts.Skip(1))
                                    shelf.Add(tok == "_" ? null : tok);
                            }
                            else
                            {
                                // Pre-08-save-hardening format: capacity was a
                                // trailing "capN" token on the shelf line
                                // itself. Kept so saves written before this fix
                                // still resume instead of vanishing.
                                capacity = 9;
                                foreach (var tok in parts.Skip(1))
                                {
                                    if (tok.StartsWith("cap", StringComparison.Ordinal))
                                        capacity = int.Parse(tok.Substring(3), CultureInfo.InvariantCulture);
                                    else
                                        shelf.Add(tok == "_" ? null : tok);
                                }
                            }
                            // A shelf shorter than its own capacity is padded; one
                            // longer, or a nonsense capacity, is a broken file —
                            // reject it instead of resuming a distorted position.
                            if (capacity < 1 || capacity < shelf.Count)
                                return null;
                            while (shelf.Count < capacity) shelf.Add(null);
                            break;
                        case "triples":
                            triples = int.Parse(parts[1], CultureInfo.InvariantCulture);
                            break;
                        case "taken":
                            taken = parts.Skip(1)
                                .Select(t => int.Parse(t, CultureInfo.InvariantCulture))
                                .ToList();
                            break;
                        case "cursor":
                            cursorRoom = int.Parse(parts[1], CultureInfo.InvariantCulture);
                            cursorPile = int.Parse(parts[2], CultureInfo.InvariantCulture);
                            break;
                        case "lesson":
                            // Present or absent, never "lesson 0": a file that
                            // does not carry the line is a file from before the
                            // lesson existed, and both cases mean the same
                            // thing. Anything other than 1 is treated as absent
                            // rather than rejected — a stray token here must not
                            // cost the player the position it is attached to.
                            lessonSeen = parts.Length > 1 && parts[1] == "1";
                            break;
                        case "roomsdone":
                            roomsDone = parts.Skip(1)
                                .Select(t => int.Parse(t, CultureInfo.InvariantCulture))
                                .ToList();
                            break;
                    }
                }

                if (roomId == null || shelf == null || triples < 0 || taken == null)
                    return null;
                if (levelNumber < 1 || pileIndex < 0 || cursorRoom < 1 || cursorPile < 0)
                    return null;
                if (taken.Any(id => id < 0))
                    return null;
                // Same rule as cursorRoom just above: a room number is
                // 1-based, and this level has no view of the shipped room
                // count to bound it further (PlayerProgress.Restore checks
                // the upper bound once it does). A repeat entry is not
                // garbage in the same sense, but it is not a real position
                // either — RoomsDone is a set, so reject rather than let a
                // duplicate through unnoticed.
                if (roomsDone != null && (roomsDone.Any(r => r < 1) ||
                    roomsDone.Distinct().Count() != roomsDone.Count))
                    return null;

                return new SavedGame(levelNumber, roomId, pileIndex,
                    taken, shelf, triples, cursorRoom, cursorPile,
                    roomsDone ?? new List<int>(), lessonSeen);
            }
            catch (FormatException)
            {
                return null;
            }
            catch (IndexOutOfRangeException)
            {
                return null;
            }
            // A number too large for int is malformed like any other garbage;
            // without this the promise above ("never crash") was not kept.
            catch (OverflowException)
            {
                return null;
            }
        }

        /// <summary>
        /// Has the first lesson been played? Answered WITHOUT parsing a
        /// position, and that is the whole reason it exists.
        ///
        /// <see cref="Read"/> returns null for any save whose board is not
        /// resumable — truncated, from a level that no longer ships, already
        /// over. All of those are ordinary and all of them start a fresh board,
        /// which is right. What is not right is showing a player the tutorial
        /// again because the position they were in stopped being readable: the
        /// lesson is a fact about the person, not about the pile.
        /// </summary>
        public static bool LessonSeenIn(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            var lines = text.Replace("\r\n", "\n")
                            .Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (lines[0].Trim() != Header) return false;
            foreach (var raw in lines)
                if (raw.Trim() == LessonLine) return true;
            return false;
        }

        /// <summary>
        /// What has to survive deleting the save, or null when nothing does.
        ///
        /// The board file is deleted on a loss and when a save names a level
        /// past the end (DebugGameView.Finish, Shell.SaveFile.Clear). Losing the
        /// first pile after being taught how to play would otherwise teach the
        /// player again on the replay — the one thing SCOPE rules out ("the
        /// lesson runs once in the life of an install"). So a clear leaves
        /// behind a header and the lesson line and nothing else.
        ///
        /// Deliberately NOT a resumable save: <see cref="Read"/> rejects this
        /// text — no level, no shelf, no taken list — so every caller that asks
        /// for a position gets exactly what it got before, a fresh board.
        /// </summary>
        public static string Residue(string text) =>
            LessonSeenIn(text) ? Header + "\n" + LessonLine + "\n" : null;
    }

    /// <summary>Parsed save payload — everything needed to resume exactly.</summary>
    public sealed class SavedGame
    {
        public int LevelNumber { get; }
        public string RoomId { get; }
        public int PileIndex { get; }
        public IReadOnlyList<int> TakenOrder { get; }
        public IReadOnlyList<string> ShelfKinds { get; }
        public int ShelfCapacity => ShelfKinds.Count;
        public int TriplesCompleted { get; }
        public int CursorRoom { get; }
        public int CursorPile { get; }
        public IReadOnlyList<int> RoomsDone { get; }

        /// <summary>Whether the first lesson has already been played through
        /// (60-shell-build/28). False for every file written before it
        /// existed.</summary>
        public bool LessonSeen { get; }

        public SavedGame(int levelNumber, string roomId, int pileIndex,
                         IReadOnlyList<int> takenOrder,
                         IReadOnlyList<string> shelfKinds,
                         int triplesCompleted,
                         int cursorRoom, int cursorPile,
                         IReadOnlyList<int> roomsDone,
                         bool lessonSeen = false)
        {
            LessonSeen = lessonSeen;
            LevelNumber = levelNumber;
            RoomId = roomId;
            PileIndex = pileIndex;
            TakenOrder = takenOrder;
            ShelfKinds = shelfKinds;
            TriplesCompleted = triplesCompleted;
            CursorRoom = cursorRoom;
            CursorPile = cursorPile;
            RoomsDone = roomsDone;
        }
    }
}
