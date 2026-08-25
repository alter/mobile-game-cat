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
    /// </summary>
    public static class LevelAssets
    {
        // Mirrors tools/solver/pacing.py — the 37-level (room, pile) plan.
        private static readonly int[] PilesPerRoom =
            { 1, 2, 3, 3, 3, 3, 3, 3, 4, 4, 4, 4 };

        public static IReadOnlyList<Level> LoadAll()
        {
            var levels = new List<Level>();
            int seq = 1;
            for (int room = 1; room <= 12; room++)
                for (int pile = 0; pile < PilesPerRoom[room - 1]; pile++)
                {
                    var name = string.Format(
                        System.Globalization.CultureInfo.InvariantCulture,
                        "l{0:00}_room{1:00}_pile{2}", seq, room, pile);
                    var asset = Resources.Load<TextAsset>($"Levels/{name}");
                    if (asset == null)
                        throw new InvalidOperationException(
                            $"missing level asset {name}");
                    levels.Add(Parse(asset.text));
                    seq++;
                }
            return levels;
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
