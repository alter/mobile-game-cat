# Level JSON format — shared contract between the Python solver and C# Core

Matches `game/Assets/Core/Level.cs` / `PileEntry` one-to-one.

```json
{
  "number": 1,
  "room_id": "room_01",
  "moves_limit": 33,
  "pile": [
    {"id": 1, "kind": "vase", "blocked_by": [2]},
    {"id": 2, "kind": "book", "blocked_by": []}
  ]
}
```

Rules (validated by both `tools/solver/schema.py:validate` and, at load time,
the game side):

1. `number` >= 1; `room_id` non-empty; `moves_limit` > 0.
2. `id` — unique integers across the pile.
3. `blocked_by` — ids of items lying directly on top of this one; every id must exist; no self-reference.
4. `blocked_by` graph must be acyclic (a cycle would make items permanently unreachable).
5. Every kind's count is a multiple of 3 (otherwise the pile cannot fully clear).

Semantics mirrored in `tools/solver/rules.py` from `Board.cs`: see its docstring.
