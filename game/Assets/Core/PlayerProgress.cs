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
}
