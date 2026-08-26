# The contract, 2026-08-26 — written ahead of the Worker on purpose

`tools/traits/schema.json` is the exact object the model receives as
`output_config.format.schema`. It is data, not code, so the Worker, the game and
the tests read one file instead of keeping three copies that drift.

## Why this was done before 02-traits-worker, which it formally depends on

The Worker is blocked on `10-accounts/01-spend-cap`, which is the owner's to
set. But a schema is a contract, and the thing it constrains is the *response*,
not the transport. Written first, the Worker has something to conform to;
written after, it would be reverse-engineered from whatever the Worker happened
to return.

Nothing here calls the model or costs anything.

## The five fields

Straight from `cat-shelter-tech.md` section 3, and pinned by a test so a later
edit to either side breaks loudly:

| field | values |
|---|---|
| `base_color` | ginger, grey, black, white, cream, brown |
| `pattern` | solid, tabby, bicolor, calico, tuxedo, pointed |
| `fur_length` | short, long |
| `eye_color` | green, amber, blue |
| `white_markings` | array of: chest, paws, face |

They are exactly what the sprite is assembled from — silhouette(fur_length) +
fill(base_color) + pattern mask + markings + eyes — so a sixth field would have
nothing to draw with.

## Two rules the schema cannot carry, and one it must

- `additionalProperties: false` **is** in the schema, and is mandatory per
  `knowledge/vision-model/01-traits-strict-json.md`.
- **`maxItems` is not a supported keyword.** A model could return
  `["chest","chest","chest","chest"]` and satisfy the schema. The cap — three,
  which is every value in the enum — and the no-duplicates rule are enforced in
  `validate.py`, on the way out of the Worker.
- **Out-of-enum means reject, never repair.** "orange" is not quietly turned
  into "ginger": a silently corrected trait paints the wrong cat and leaves no
  trace of why. `validate()` raises with the offending value in the message.

`output_config`, not `output_format`: the parameter moved and the Python SDK
1.x raises `TypeError` on the old name. There is a test for that too, because
it is exactly the kind of thing written from memory.

## Against the VERIFY list

Items 1, 2 and 4 name a **deployed endpoint** and cannot be run until
`02-traits-worker` exists — which waits on the spend cap. What is done and
checkable now:

- 14 tests in `tools/tests/test_traits_schema.py`, covering every field
  missing, four plausible out-of-enum synonyms ("orange", "striped", "medium",
  "yellow"), an extra property, an over-long and a repeated `white_markings`,
  six unparseable bodies, and the request fragment.
- Item 3 — "zero values outside the declared enums, checked by a script" —
  the script is `validate.py` and it is the same one the Worker will import.

Left `status:in_progress`: the schema is done, the endpoint it constrains is
not.

## Model choice — deliberately not made here

SCOPE asks for "model choice recorded (Haiku 4.5 vs Sonnet 5) with a reason".
It is not recorded, because it should not be guessed: `cat-shelter-tech.md`
says the choice "isn't decided by price, it's decided by the quality of
coloring, compared by eye on a reference set". That comparison needs the
endpoint and costs money, so it belongs with `02-traits-worker` — and the
reference set it needs is already built and waiting.
