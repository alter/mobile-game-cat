// JSON level loading. Deliberately OUTSIDE Core: parsing is plumbing, and
// Core stays dependency-free (ARCH rule). In Unity this file moves to a View/
// infrastructure assembly and switches to com.unity.nuget.newtonsoft-json;
// the API surface below is written so that swap is mechanical.
using System.Collections.Generic;
using System.Linq;
using CatShelter.Core;

namespace CatShelter.Tests
{
    public static class LevelLoader
    {
        public static Level FromJson(string json)
        {
            var root = Newtonsoft.Json.Linq.JObject.Parse(json);
            int number = (int)root["number"]!;
            string roomId = (string)root["room_id"]!;

            var entries = new List<PileEntry>();
            foreach (var e in (Newtonsoft.Json.Linq.JArray)root["pile"]!)
            {
                var item = new Item((int)e["id"]!,
                    new ItemKind((string)e["kind"]!, (string)e["kind"]!));
                var blocked = e["blocked_by"]!.Select(t => (int)t).ToList();
                entries.Add(new PileEntry(item, blocked));
            }

            return new Level(number, roomId, entries);
        }
    }
}
