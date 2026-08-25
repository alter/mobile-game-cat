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

        /// <summary>Capture the live board plus progress cursor.</summary>
        public static string Write(Board board, PlayerProgress progress)
        {
            if (board is null) throw new ArgumentNullException(nameof(board));
            var snap = BoardSave.Capture(board);
            return Write(snap, progress);
        }

        public static string Write(BoardSnapshot snap, PlayerProgress progress)
        {
            if (snap is null) throw new ArgumentNullException(nameof(snap));
            var lines = new List<string>
            {
                Header,
                // level identity
                $"level {snap.LevelNumber} {snap.RoomId} {snap.PileIndex}",
                // shelf: kind ids, '_' for empty, then capacity
                "shelf " + string.Join(" ", snap.Shelf.Select(s => s ?? "_"))
                        + $" cap{snap.Shelf.Count}",
                $"triples {snap.TriplesCompleted}",
                // taken ids in take order (replay drives occlusion + locks)
                "taken " + string.Join(" ", snap.Taken),
            };
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
                        case "shelf":
                            shelf = new List<string>();
                            int capacity = 9;
                            foreach (var tok in parts.Skip(1))
                            {
                                if (tok.StartsWith("cap", StringComparison.Ordinal))
                                    capacity = int.Parse(tok.Substring(3), CultureInfo.InvariantCulture);
                                else
                                    shelf.Add(tok == "_" ? null : tok);
                            }
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
                        case "roomsdone":
                            roomsDone = parts.Skip(1)
                                .Select(t => int.Parse(t, CultureInfo.InvariantCulture))
                                .ToList();
                            break;
                    }
                }

                if (roomId == null || shelf == null || triples < 0 || taken == null)
                    return null;

                return new SavedGame(levelNumber, roomId, pileIndex,
                    taken, shelf, triples, cursorRoom, cursorPile,
                    roomsDone ?? new List<int>());
            }
            catch (FormatException)
            {
                return null;
            }
            catch (IndexOutOfRangeException)
            {
                return null;
            }
        }
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

        public SavedGame(int levelNumber, string roomId, int pileIndex,
                         IReadOnlyList<int> takenOrder,
                         IReadOnlyList<string> shelfKinds,
                         int triplesCompleted,
                         int cursorRoom, int cursorPile,
                         IReadOnlyList<int> roomsDone)
        {
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
