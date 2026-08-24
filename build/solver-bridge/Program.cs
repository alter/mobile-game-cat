// Throwaway conformance runner: replays solution scripts through Core and
// writes results. Lives under /build, never referenced by game code.
// Usage: dotnet run --project build/solver-bridge -- levels.json solutions.json results.json
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

var levelsPath = args.Length > 0 ? args[0] : "levels.json";
var solutionsPath = args.Length > 1 ? args[1] : "solutions.json";
var resultsPath = args.Length > 2 ? args[2] : "results.json";

var opts = new JsonDocumentOptions();

using var levelsDoc = JsonDocument.Parse(File.ReadAllText(levelsPath), opts);
using var solDoc = JsonDocument.Parse(File.ReadAllText(solutionsPath), opts);

var results = new List<object>();

foreach (var levelEl in levelsDoc.RootElement.EnumerateArray())
{
    int number = levelEl.GetProperty("number").GetInt32();
    string roomId = levelEl.GetProperty("room_id").GetString()!;
    int movesLimit = levelEl.GetProperty("moves_limit").GetInt32();

    var entries = new List<CatShelter.Core.PileEntry>();
    foreach (var e in levelEl.GetProperty("pile").EnumerateArray())
    {
        var blocked = e.GetProperty("blocked_by").EnumerateArray()
            .Select(x => x.GetInt32()).ToList();
        entries.Add(new CatShelter.Core.PileEntry(
            new CatShelter.Core.Item(e.GetProperty("id").GetInt32(),
                new CatShelter.Core.ItemKind(e.GetProperty("kind").GetString()!, e.GetProperty("kind").GetString()!)),
            blocked));
    }
    var level = new CatShelter.Core.Level(number, roomId, movesLimit, entries);
    var board = new CatShelter.Core.Board(level);

    string key = number.ToString();
    var order = solDoc.RootElement.GetProperty(key).EnumerateArray()
        .Select(x => x.GetInt32()).ToList();

    bool legalSequence = true;
    string error = "";
    foreach (var id in order)
    {
        if (board.IsOver) break;
        if (!board.TakeItem(id))
        {
            legalSequence = false;
            error = $"illegal move {id}";
            break;
        }
    }

    results.Add(new Dictionary<string, object>
    {
        ["number"] = number,
        ["legal"] = legalSequence,
        ["error"] = error,
        ["over"] = board.IsOver,
        ["outcome"] = board.Outcome?.ToString() ?? "none",
        ["moves_left"] = board.MovesLeft
    });
}

File.WriteAllText(resultsPath,
    JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"wrote {results.Count} results to {resultsPath}");
