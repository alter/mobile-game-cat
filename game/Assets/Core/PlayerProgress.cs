using System;
using System.Collections.Generic;
using System.Linq;

namespace CatShelter.Core
{
    /// <summary>
    /// Player progress across rooms and piles (tasks 6.2, 6.2.1).
    /// A room holds several piles; clearing the last pile of a room completes
    /// it, which is what advances the cat state and pays the big reward.
    /// </summary>
    public sealed class PlayerProgress
    {
        /// <summary>1-based room numbers fully cleared.</summary>
        public IReadOnlyList<int> RoomsDone => _roomsDone;
        private readonly List<int> _roomsDone = new();

        public int CurrentRoom { get; private set; } = 1;
        public int CurrentPile { get; private set; }

        /// <summary>Piles in each room, 1-based index → count.</summary>
        public IReadOnlyList<int> PilesPerRoom { get; }

        public PlayerProgress(IReadOnlyList<int> pilesPerRoom)
        {
            if (pilesPerRoom is null || pilesPerRoom.Count == 0)
                throw new ArgumentException("need at least one room", nameof(pilesPerRoom));
            if (pilesPerRoom.Any(c => c < 1))
                throw new ArgumentException("every room needs at least one pile",
                                            nameof(pilesPerRoom));
            PilesPerRoom = pilesPerRoom;
        }

        public bool IsRoomDone(int room) => _roomsDone.Contains(room);

        /// <summary>
        /// How many piles of room <paramref name="room"/> (1-based) are
        /// cleared right now. Derived from the cursor and RoomsDone — never a
        /// second counter, so it cannot drift from CompletePile's own view of
        /// the world. Rooms are played in order, so a room ahead of the
        /// cursor reads 0 and a room behind it (or IsRoomDone) reads full,
        /// with no room left ambiguous in between.
        ///
        /// Task 60-shell-build/03 (house map): the view turns this into a
        /// dirty/partial/clean cell instead of counting piles itself.
        /// </summary>
        public int PilesClearedIn(int room)
        {
            if (room < 1 || room > PilesPerRoom.Count) return 0;
            if (IsRoomDone(room)) return PilesPerRoom[room - 1];
            if (room == CurrentRoom) return CurrentPile;
            return 0;
        }

        /// <summary>Dirty (untouched), partial (started), or clean (closed) —
        /// the house map's three cell states, read off <see cref="PilesClearedIn"/>
        /// against the room's own pile count so the view never keeps a fourth
        /// copy of this.</summary>
        public RoomCellState CellStateFor(int room)
        {
            var total = room >= 1 && room <= PilesPerRoom.Count
                ? PilesPerRoom[room - 1] : 0;
            var cleared = PilesClearedIn(room);
            if (total <= 0 || cleared <= 0) return RoomCellState.Dirty;
            return cleared >= total ? RoomCellState.Clean : RoomCellState.Partial;
        }

        /// <summary>
        /// Done, open, or locked — what the house map has to say about a room
        /// before it says anything else.
        ///
        /// Deliberately separate from <see cref="CellStateFor"/>. That one says
        /// how dirty a room is, which only becomes interesting once you know
        /// you can go there at all. Twelve rooms drawn as twelve thumbnails
        /// told a first-time viewer neither thing: the owner's reaction on
        /// seeing it running was that the icons were grey and nothing was
        /// clear. Where you may go is the first question a map answers.
        ///
        /// Rooms are played in order, so the rule is the cursor's: everything
        /// behind it is finished, the cursor itself is the room to play, and
        /// everything ahead is shut. A room out of range reads Locked rather
        /// than throwing — the map draws whatever the level files handed it.
        /// </summary>
        public RoomAccess AccessFor(int room)
        {
            if (room < 1 || room > PilesPerRoom.Count) return RoomAccess.Locked;
            if (IsRoomDone(room) || room < CurrentRoom) return RoomAccess.Done;
            return room == CurrentRoom ? RoomAccess.Open : RoomAccess.Locked;
        }

        /// <summary>
        /// Rebuild a cursor from a saved position (Core.GameSave) instead of
        /// replaying every CompletePile call the player ever made — the save
        /// already carries the cursor and the finished-rooms list, and
        /// replaying would be a second implementation of the same rule.
        /// </summary>
        public static PlayerProgress Restore(IReadOnlyList<int> pilesPerRoom,
                                             int cursorRoom, int cursorPile,
                                             IReadOnlyList<int> roomsDone)
        {
            var progress = new PlayerProgress(pilesPerRoom);
            if (cursorRoom < 1 || cursorRoom > pilesPerRoom.Count)
                throw new ArgumentOutOfRangeException(nameof(cursorRoom));
            if (cursorPile < 0 || cursorPile >= pilesPerRoom[cursorRoom - 1])
                throw new ArgumentOutOfRangeException(nameof(cursorPile));

            progress.CurrentRoom = cursorRoom;
            progress.CurrentPile = cursorPile;
            if (roomsDone != null)
            {
                foreach (var room in roomsDone)
                    if (!progress._roomsDone.Contains(room))
                        progress._roomsDone.Add(room);
            }
            return progress;
        }

        /// <summary>Record that pile <paramref name="pileIndex"/> (0-based) of the
        /// current room is cleared and advance the cursor.</summary>
        public void CompletePile(int pileIndex)
        {
            if (pileIndex != CurrentPile)
                throw new InvalidOperationException(
                    $"expected pile {CurrentPile}, got {pileIndex}");
            if (pileIndex + 1 >= PilesPerRoom[CurrentRoom - 1])
            {
                // last pile of the room: close it and move on
                if (!_roomsDone.Contains(CurrentRoom))
                    _roomsDone.Add(CurrentRoom);
                if (CurrentRoom >= PilesPerRoom.Count)
                    return; // whole house done
                CurrentRoom++;
                CurrentPile = 0;
            }
            else
            {
                CurrentPile++;
            }
        }

        /// <summary>Cat states change after the 4th and 8th completed room
        /// (cat-shelter-tasks.md: "Cat states anchor to rooms").</summary>
        public static int CatStateFor(int completedRooms)
        {
            if (completedRooms >= 8) return 3;
            if (completedRooms >= 4) return 2;
            return 1;
        }

        public int CatState => CatStateFor(_roomsDone.Count);
    }

    /// <summary>The house map's three room states (60-shell-build/03,
    /// art-brief.md section 9). Not started, started, or closed out — always
    /// read off <see cref="PlayerProgress.PilesClearedIn"/>, never stored.</summary>
    public enum RoomCellState
    {
        Dirty,
        Partial,
        Clean
    }

    /// <summary>
    /// Whether a room can be entered — the house map's first question, and a
    /// different one from <see cref="RoomCellState"/>. Read off
    /// <see cref="PlayerProgress.AccessFor"/>, never stored.
    /// </summary>
    public enum RoomAccess
    {
        /// <summary>Cleared. Behind the cursor.</summary>
        Done,

        /// <summary>The room being played. Exactly one room is ever Open.</summary>
        Open,

        /// <summary>Ahead of the cursor, or not a room at all.</summary>
        Locked
    }
}
