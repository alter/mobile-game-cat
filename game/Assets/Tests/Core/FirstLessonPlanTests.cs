using System.Linq;
using CatShelter.Core;
using CatShelter.Tests;
using NUnit.Framework;

namespace CatShelter.Core.Tests
{
    /// <summary>
    /// Task 60-shell-build/28: the first lesson walks the player through
    /// taking three identical props on level 1. This ties FirstLessonPlan's
    /// hardcoded ids to the actual shipped level file
    /// (Resources/Levels/l01_room01_pile0.json) rather than trusting them by
    /// eye: a level edit that moves item 5 under something else, or gives it
    /// a different kind, would otherwise silently break the lesson without
    /// any test noticing.
    ///
    /// Ids are read off FirstLessonPlan, not hardcoded here, so there is
    /// exactly one copy of "1, 5, 8" in the repo.
    /// </summary>
    [TestFixture]
    public class FirstLessonPlanTests
    {
        // Same convention every other Tests/Core file that reads level files
        // from disk uses (see HeadlessRunTests, LevelLoadPolicyTests): go
        // through CatShelter.Tests.LevelLoader.LoadAllFromAssets(), which
        // walks up from the test assembly to find game/Assets/Resources/Levels
        // and reads every shipped file straight off disk under dotnet test.
        // Reusing it instead of re-deriving the repo root here keeps there
        // being exactly one place that knows how to find level files.
        private static Level LoadLevelOne()
        {
            var levels = LevelLoader.LoadAllFromAssets();
            var level = levels.SingleOrDefault(l => l.RoomId == "room_01" && l.PileIndex == 0);
            Assert.That(level, Is.Not.Null,
                "l01_room01_pile0.json not found among the shipped level files");
            return level;
        }

        [Test]
        public void LevelNumber_MatchesTheShippedFile()
        {
            var level = LoadLevelOne();
            Assert.That(FirstLessonPlan.LevelNumber, Is.EqualTo(level.Number),
                "FirstLessonPlan.LevelNumber must match l01_room01_pile0.json's own `number`");
        }

        [Test]
        public void EveryLessonItem_ExistsInLevelOnesPile()
        {
            var level = LoadLevelOne();
            var idsInPile = level.Pile.Select(e => e.Item.Id).ToHashSet();

            foreach (var id in FirstLessonPlan.ItemIds)
            {
                Assert.That(idsInPile, Does.Contain(id),
                    $"FirstLessonPlan.ItemIds names item {id}, which does not " +
                    "appear anywhere in l01_room01_pile0.json's pile");
            }
        }

        [Test]
        public void AllThreeLessonItems_ShareOneKind()
        {
            var level = LoadLevelOne();
            var byId = level.Pile.ToDictionary(e => e.Item.Id, e => e.Item);

            var kinds = FirstLessonPlan.ItemIds
                .Select(id =>
                {
                    Assert.That(byId.ContainsKey(id), Is.True,
                        $"item {id} from FirstLessonPlan is not in the pile, cannot check its kind");
                    return byId[id].Kind.Id;
                })
                .Distinct()
                .ToList();

            Assert.That(kinds, Has.Count.EqualTo(1),
                "FirstLessonPlan.ItemIds must all be the same prop kind so the " +
                $"lesson demonstrates one triple; found kinds: {string.Join(", ", kinds)}");
        }

        [Test]
        public void AllThreeLessonItems_AreAvailableOnTheFirstMove()
        {
            // Board.GetAvailable() is the game's own notion of "can be taken
            // right now" (not taken, nothing covering it, not locked), so
            // asserting against it agrees with the rule the game actually
            // uses instead of re-deriving it from blocked_by by hand.
            var level = LoadLevelOne();
            var board = new Board(level);
            var availableIds = board.GetAvailable().Select(i => i.Id).ToHashSet();

            foreach (var id in FirstLessonPlan.ItemIds)
            {
                Assert.That(availableIds, Does.Contain(id),
                    $"item {id} from FirstLessonPlan is not available on the " +
                    "very first move (board.GetAvailable() does not list it) " +
                    "so the lesson could not have the player tap it first");
            }
        }
    }
}
