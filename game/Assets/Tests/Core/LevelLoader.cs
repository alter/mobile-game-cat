// JSON level loading. Deliberately OUTSIDE Core: parsing is plumbing, and
// Core stays dependency-free (ARCH rule).
//
// Two hosts, one API:
// - In Unity (UNITY_2D... defined automatically) levels are TextAssets under
//   Assets/Levels — loaded via Resources-compatible direct file reads at edit
//   time, or passed in by the caller from a TextAsset.
// - In dotnet test runs, levels come from the filesystem.
using System;
using System.Collections.Generic;
using System.Linq;
using CatShelter.Core;

namespace CatShelter.Tests
{
    public static class LevelLoader
    {
        /// <summary>Parse one level definition.</summary>
        public static Level FromJson(string json)
        {
            var root = Newtonsoft.Json.Linq.JObject.Parse(json);
            int number = (int)root["number"]!;
            string roomId = (string)root["room_id"]!;
            int pileIndex = (int?)root["pile_index"] ?? 0;

            var entries = new List<PileEntry>();
            foreach (var e in (Newtonsoft.Json.Linq.JArray)root["pile"]!)
            {
                var item = new Item((int)e["id"]!,
                    new ItemKind((string)e["kind"]!, (string)e["kind"]!),
                    (int?)e["locked_after_triples"] ?? 0);
                var blocked = e["blocked_by"]!.Select(t => (int)t).ToList();
                entries.Add(new PileEntry(item, blocked));
            }

            return new Level(number, roomId, pileIndex, entries);
        }

#if UNITY_EDITOR || UNITY_STANDALONE
        /// <summary>
        /// Unity path: read every shipped level through UnityEditor asset
        /// database. Editor-only by design — runtime loads via TextAssets.
        /// </summary>
        public static System.Collections.Generic.IReadOnlyList<Level> LoadAllFromAssets()
        {
            var guids = UnityEditor.AssetDatabase.FindAssets("t:TextAsset",
                new[] { "Assets/Levels" });
            var levels = new List<Level>();
            foreach (var guid in guids)
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<
                    UnityEngine.TextAsset>(path);
                if (asset != null)
                    levels.Add(FromJson(asset.text));
            }
            return levels;
        }
#else
        /// <summary>
        /// dotnet-test path: read the same files straight from disk. The repo
        /// root is found relative to the test assembly location.
        /// </summary>
        public static System.Collections.Generic.IReadOnlyList<Level> LoadAllFromAssets()
        {
            // walk up until game/Assets/Levels exists (depth varies by host)
            var dir = AppContext.BaseDirectory;
            string levelsDir = null;
            for (int i = 0; i < 12 && dir != null; i++)
            {
                dir = System.IO.Path.GetDirectoryName(dir);
                var candidate = System.IO.Path.Combine(dir, "game", "Assets", "Levels");
                if (System.IO.Directory.Exists(candidate))
                {
                    levelsDir = candidate;
                    break;
                }
            }
            if (levelsDir == null)
                throw new System.IO.DirectoryNotFoundException(
                    "game/Assets/Levels not found above " + AppContext.BaseDirectory);
            var levels = new List<Level>();
            foreach (var file in System.IO.Directory.GetFiles(levelsDir, "l*.json"))
                levels.Add(FromJson(System.IO.File.ReadAllText(file)));
            return levels;
        }
#endif
    }
}
