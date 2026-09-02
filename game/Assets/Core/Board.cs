using System;
using System.Collections.Generic;
using System.Linq;

namespace CatShelter.Core
{
    /// <summary>How a game ended.</summary>
    public enum GameOutcome
    {
        /// <summary>The pile is empty — the room corner is cleared, the player won.</summary>
        Win,
        /// <summary>
        /// The player is stuck: either the shelf is full with no match available,
        /// or nothing in the pile can be taken because every remaining item is
        /// locked by a complication. Both are one outcome because they are one
        /// thing to the player — no move exists — and the booster answers both.
        /// </summary>
        ShelfJammed
    }

    /// <summary>
    /// The playing field: a pile of overlapped items plus the shelf. Decides
    /// win/lose. There is no move limit: winning means taking every item, so
    /// each move spends exactly one take and a limit either blocks the level or
    /// can never be reached (reviews/2026-08-24-refactor-difficulty.md).
    ///
    /// Partial information (task 3.9): a buried item's kind is hidden until it
    /// becomes reachable. Locked items (task 3.11) are unreachable until the
    /// player has completed enough triples, regardless of occlusion.
    /// </summary>
    public sealed class Board
    {
        private readonly Dictionary<int, PileEntry> _entries;
        private readonly HashSet<int> _taken;
        private readonly List<int> _takenOrder = new();

        /// <summary>Completed-triple count; unlocks locked items (task 3.11).</summary>
        public int TriplesCompleted { get; private set; }

        /// <summary>Ids already taken, in take order (task 6.7 serialisation).</summary>
        public IReadOnlyList<int> TakenOrder => _takenOrder;

        public Level Level { get; }
        public Shelf Shelf { get; }

        public bool IsOver { get; private set; }
        public GameOutcome? Outcome { get; private set; }

        public Board(Level level)
            : this(level, Shelf.SlotsPerRow * Shelf.RowCount)
        {
        }

        public Board(Level level, int shelfCapacity)
        {
            Level = level ?? throw new ArgumentNullException(nameof(level));
            Shelf = new Shelf(shelfCapacity);
            // Kinds-in-triples and unique ids are enforced by Level itself, so
            // a Board cannot be handed a pile that breaks either.
            _entries = level.Pile.ToDictionary(e => e.Item.Id);
            _taken = new HashSet<int>();

            // Task 07 (2026-09-02): the jam check at the bottom of TakeItem
            // only ever runs after a successful take, so a level where every
            // top-of-pile item is locked (LockedAfterTriples > 0, and no
            // triple has been completed yet) never reached it — IsOver stayed
            // false and Outcome null forever, a dead screen with no move and
            // no way out. Catching it here, right after GetAvailable() first
            // becomes computable, closes that gap at its only entry point:
            // both public constructors funnel through this one, so this also
            // covers BoardSave.Restore's fresh rebuild before it replays the
            // saved takes (see the constructor's own note below).
            if (_entries.Count > 0 && GetAvailable().Count == 0)
                Finish(GameOutcome.ShelfJammed);
        }

        /// <summary>
        /// Items currently reachable: still in the pile, nothing covering them,
        /// and not locked (or the lock has been satisfied).
        /// </summary>
        public IReadOnlyList<Item> GetAvailable()
        {
            return _entries.Values
                .Where(e => !_taken.Contains(e.Item.Id))
                .Where(e => e.BlockedBy.All(id => _taken.Contains(id)))
                .Where(e => !IsLockedByComplication(e.Item))
                .Select(e => e.Item)
                .ToList();
        }

        /// <summary>Whether this item has already left the pile.</summary>
        public bool IsTaken(int itemId) => _taken.Contains(itemId);

        /// <summary>
        /// Task 3.9: an item shows its kind once nothing covers it. Being
        /// locked does NOT hide it — see D15 and the comment in the body.
        ///
        /// This summary said "and no complication locks it" until 2026-08-27,
        /// which D15 reversed the day before and which the body three lines
        /// down already contradicted. Corrected after a verification of
        /// 09-hidden-kinds pointed at it. A doc comment that disagrees with
        /// the method under it is worse than none: the body can be read, and
        /// the comment is what gets quoted into a decision.
        /// </summary>
        public bool IsRevealed(Item item)
        {
            if (_taken.Contains(item.Id))
                return false;
            var entry = _entries[item.Id];
            // Locked is NOT hidden. Burial (D3) and the complication lock
            // (3.11) are two different things and used to collapse into one
            // here: a locked item reported itself unrevealed, so the view drew
            // it as a buried tile and the lock never reached the screen -
            // visible in 0 of the 16 levels that carry one. The player has to
            // see WHICH kind is being withheld, or the lock is not a
            // complication, just a tile that will not respond. See D15.
            return entry.BlockedBy.All(id => _taken.Contains(id));
        }

        /// <summary>Take an item from the pile and put it on the shelf.</summary>
        /// <remarks>
        /// Win is checked before the jam: an empty pile is a win even when the
        /// final placement happens to fill the shelf. A full unmatched shelf is
        /// the only loss — that is what the "+1 slot" booster answers.
        /// </remarks>
        public bool TakeItem(int itemId)
        {
            if (IsOver)
                return false;
            if (!_entries.TryGetValue(itemId, out var entry) || _taken.Contains(itemId))
                return false;
            if (!entry.BlockedBy.All(id => _taken.Contains(id)))
                return false;
            if (IsLockedByComplication(entry.Item))
                return false;

            _taken.Add(itemId);
            _takenOrder.Add(itemId);

            if (!Shelf.TryPlace(entry.Item, out var matchedKind))
            {
                // Unreachable under the present rules, and kept deliberately.
                // A placement is only refused by a full shelf, and the game
                // ends the moment a shelf fills (line 137) or the pile empties
                // (line 143), so no take is ever attempted against a full one —
                // 40 000 random games never reached this branch, see
                // tasks/20-rules-core/04-outcomes/VERIFY.md.
                //
                // It stays because the item is already in _taken by this point:
                // falling through instead would leave it neither in the pile
                // nor on the shelf, losing it silently. An outcome here is the
                // safe failure. The task's OUTCOME used to claim this branch
                // protected the win/jam ordering — it does not and cannot; the
                // reachable ordering is at 137-147, pinned by
                // FinalPlacementTakesTheLastSlotAndMatches_IsAWin_NotAJam.
                if (_taken.Count == _entries.Count)
                {
                    Finish(GameOutcome.Win);
                    return true;
                }
                Finish(GameOutcome.ShelfJammed);
                return true;
            }

            if (matchedKind is not null)
                TriplesCompleted++;

            // full-but-matched shelf with a pile remaining: nowhere for the
            // next item — jam (the booster answers exactly this)
            if (Shelf.IsFull && _taken.Count != _entries.Count)
            {
                Finish(GameOutcome.ShelfJammed);
                return true;
            }

            if (_taken.Count == _entries.Count)
            {
                Finish(GameOutcome.Win);
                return true;
            }

            // Pile not empty, shelf not full, and yet nothing can be taken:
            // every remaining item is locked and there are not enough triples
            // to open them. Without this the board hangs — no outcome, no
            // moves, and on a phone no way out.
            if (GetAvailable().Count == 0)
                Finish(GameOutcome.ShelfJammed);

            return true;
        }

        /// <summary>
        /// Grow the shelf by <paramref name="extra"/> slots and, if that ended a
        /// jam, resume play (the "one more shelf" booster, DECISIONS.md D4).
        /// The MVP never calls this — the booster is a fake door — but the rules
        /// mirror in tools/solver/rules.py has always resumed, and the two must
        /// agree. Stays jammed when the extra room changes nothing.
        /// </summary>
        public void AddShelfSlots(int extra)
        {
            Shelf.AddSlots(extra);
            if (!IsOver || Outcome != GameOutcome.ShelfJammed)
                return;
            if (Shelf.IsFull || GetAvailable().Count == 0)
                return;
            IsOver = false;
            Outcome = null;
        }

        /// <summary>
        /// Task 3.11: an item carrying LockedAfterTriples &gt; 0 stays locked until
        /// that many triples have been completed. Locked items are neither
        /// available nor revealed.
        /// </summary>
        public bool IsLockedByComplication(Item item)
        {
            return item.LockedAfterTriples > 0 && TriplesCompleted < item.LockedAfterTriples;
        }

        private void Finish(GameOutcome outcome)
        {
            IsOver = true;
            Outcome = outcome;
        }
    }
}
