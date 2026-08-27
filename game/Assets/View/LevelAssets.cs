using System;
using System.Collections.Generic;
using System.Linq;
using CatShelter.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CatShelter.View
{
    /// <summary>
    /// Runtime level loading from Assets/Resources/Levels as TextAssets.
    /// Editor-side loading (asset database, for tests) stays in the test
    /// assembly; this one survives into the player. JToken instead of dynamic:
    /// dynamic needs Microsoft.CSharp which IL2CPP strips.
    ///
    /// A missing or malformed file no longer throws out of <see cref="LoadAll"/>:
    /// it used to, uncaught, straight out of DebugGameView.OnEnable — before
    /// _plan/_progress ever existed — which left the player on a blank screen.
    /// The decision of what to do with what's left is CatShelter.Core.LevelLoadPolicy
    /// (30-levels-solver/06), the same split SaveResume uses for a corrupt save;
    /// this class only tries each file and hands the results over.
    /// </summary>
    public static class LevelAssets
    {
        // Mirrors tools/solver/pacing.py — the 37-level (room, pile) plan.
        private static readonly int[] PilesPerRoom =
            { 1, 2, 3, 3, 3, 3, 3, 3, 4, 4, 4, 4 };

        public static LevelLoadPolicy.Result LoadAll()
        {
            var parsed = new List<Level>();
            var expected = new Dictionary<string, int>();
            int seq = 1;
            for (int room = 1; room <= 12; room++)
            {
                var roomId = string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "room_{0:00}", room);
                expected[roomId] = PilesPerRoom[room - 1];

                for (int pile = 0; pile < PilesPerRoom[room - 1]; pile++)
                {
                    var name = string.Format(
                        System.Globalization.CultureInfo.InvariantCulture,
                        "l{0:00}_room{1:00}_pile{2}", seq, room, pile);
                    seq++;

                    var asset = Resources.Load<TextAsset>($"Levels/{name}");
                    if (asset == null)
                    {
                        Debug.LogError($"[LevelAssets] missing level asset {name}");
                        continue;
                    }
                    // Every way Parse can fail on shipped data, found by
                    // mutation-testing nine corruptions of a real level file
                    // (30-levels-solver/06 VERIFY): malformed JSON syntax
                    // (JsonReaderException), a field absent where Parse
                    // indexes into it without a null check
                    // (NullReferenceException), and everything Level's own
                    // constructor rejects — wrong kind counts, a duplicate
                    // id, a self-block, a dangling blocker, a cycle — which
                    // all derive from ArgumentException. A shipped level
                    // that fails to parse is a data bug, not a reason to
                    // crash the player's launch: log it and move on to the
                    // next file: CatShelter.Core.LevelLoadPolicy decides
                    // afterwards what the gap costs.
                    try
                    {
                        parsed.Add(Parse(asset.text));
                    }
                    catch (JsonReaderException e)
                    {
                        Debug.LogError($"[LevelAssets] {name}: malformed JSON — {e.Message}");
                    }
                    catch (NullReferenceException e)
                    {
                        Debug.LogError($"[LevelAssets] {name}: missing required field — {e.Message}");
                    }
                    catch (ArgumentException e)
                    {
                        Debug.LogError($"[LevelAssets] {name}: invalid level data — {e.Message}");
                    }
                }
            }

            var result = LevelLoadPolicy.Resolve(parsed, expected);
            foreach (var roomId in result.IncompleteRooms)
                Debug.LogError($"[LevelAssets] {roomId} dropped: incomplete after a bad file");
            return result;
        }

        public static Level Parse(string json)
        {
            var root = JObject.Parse(json);
            int number = root.Value<int>("number");
            string roomId = root.Value<string>("room_id");
            int pileIndex = root.Value<int?>("pile_index") ?? 0;

            var entries = new List<PileEntry>();
            foreach (var e in (JArray)root["pile"])
            {
                int id = e.Value<int>("id");
                string kind = e.Value<string>("kind");
                int locked = e.Value<int?>("locked_after_triples") ?? 0;
                var blocked = e["blocked_by"]
                    .Select(b => b.Value<int>()).ToList();
                entries.Add(new PileEntry(
                    new Item(id, new ItemKind(kind, kind), locked),
                    blocked));
            }
            return new Level(number, roomId, pileIndex, entries);
        }
    }
}
