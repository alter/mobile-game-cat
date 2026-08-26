// Throwaway conformance runner: replays solution scripts through Core and
// writes results. Lives under /build, never referenced by game code.
// Usage: dotnet run --project build/solver-bridge -- levels.json scripts.json results.json
//
// Script protocol: each case is a list of moves. A move is either a number
// (take item id) or an array [itemId, "booster", extra] where the optional
// booster element grows the shelf before the take.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

var levelsPath = args.Length > 0 ? args[0] : "levels.json";
var scriptsPath = args.Length > 1 ? args[1] : "scripts.json";
var resultsPath = args.Length > 2 ? args[2] : "results.json";

using var levelsDoc = JsonDocument.Parse(File.ReadAllText(levelsPath));
using var scriptDoc = JsonDocument.Parse(File.ReadAllText(scriptsPath));

var results = new List<object>();

foreach (var levelEl in levelsDoc.RootElement.EnumerateArray())
{
    int number = levelEl.GetProperty("number").GetInt32();
    string roomId = levelEl.GetProperty("room_id").GetString()!;
    int pileIndex = levelEl.TryGetProperty("pile_index", out var pi)
        ? pi.GetInt32() : 0;
    int? shelfCapacity = levelEl.TryGetProperty("shelf_capacity", out var sc)
        ? sc.GetInt32() : null;

    var entries = new List<CatShelter.Core.PileEntry>();
    foreach (var e in levelEl.GetProperty("pile").EnumerateArray())
    {
        var blocked = e.GetProperty("blocked_by").EnumerateArray()
            .Select(x => x.GetInt32()).ToList();
        int locked = e.TryGetProperty("locked_after_triples", out var lt)
            ? lt.GetInt32() : 0;
        entries.Add(new CatShelter.Core.PileEntry(
            new CatShelter.Core.Item(e.GetProperty("id").GetInt32(),
                new CatShelter.Core.ItemKind(e.GetProperty("kind").GetString()!,
                                             e.GetProperty("kind").GetString()!),
                locked),
            blocked));
    }
    var level = new CatShelter.Core.Level(number, roomId, pileIndex, entries);
    var board = shelfCapacity.HasValue
        ? new CatShelter.Core.Board(level, shelfCapacity.Value)
        : new CatShelter.Core.Board(level);

    string key = number.ToString();
    bool legalSequence = true;
    string error = "";
    foreach (var move in scriptDoc.RootElement.GetProperty(key).EnumerateArray())
    {
        // a move may be a bare id or [id, "booster", extra]
        int itemId;
        if (move.ValueKind == JsonValueKind.Number)
        {
            itemId = move.GetInt32();
        }
        else if (move.ValueKind == JsonValueKind.Array && move.GetArrayLength() >= 1)
        {
            itemId = move[0].GetInt32();
            // The booster is applied before the IsOver check on purpose: its
            // whole point is to resume a jammed game, and breaking out first
            // meant recovery was never exercised on the C# side.
            if (move.GetArrayLength() >= 3 && move[2].GetInt32() > 0)
                board.AddShelfSlots(move[2].GetInt32());
        }
        else
        {
            continue;
        }

        if (board.IsOver) break;

        if (!board.TakeItem(itemId))
        {
            legalSequence = false;
            error = $"illegal move {itemId}";
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
        ["capacity"] = board.Shelf.Capacity,
        // Outcome alone hid a real divergence: the two engines disagreed on
        // whether the last item is placed before the win is declared, which
        // left Python holding two items on every win and one triple short.
        ["occupied"] = board.Shelf.Occupied,
        ["triples"] = board.TriplesCompleted,
        ["taken"] = board.TakenOrder.Count
    });
}

File.WriteAllText(resultsPath,
    JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"wrote {results.Count} results to {resultsPath}");
