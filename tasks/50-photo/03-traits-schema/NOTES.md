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

---

# The note conceded more than it had to — 2026-08-27

Same shape as the analytics phase this morning: "waits on a deployed
endpoint" turned out to describe only part of the task. The schema is one
definition, but it has more copies than this document named, and the one
copy that actually gates a real HTTP response — the TypeScript Worker — had
no enforcement in it at all. Fixed below, without deploying anything or
spending a cent.

Also worth flagging directly: line 61 above says `validate.py` is "the same
one the Worker will import" — a Cloudflare Worker is JavaScript/TypeScript;
it cannot import a Python module. That sentence is where the gap in §2/§3
below actually originates.

## 1. Every copy of the schema, named

| copy | kind | guarded how |
|---|---|---|
| `tools/traits/schema.json` | the source | — (this is the definition, not a copy) |
| `tools/traits/validate.py` | **not a copy** | loads `schema.json` at import time (`SCHEMA = json.loads(SCHEMA_PATH.read_text())`) — same object, not a duplicate |
| `worker/src/schema.ts` | **generated** | `worker/sync-schema.py` regenerates it from `schema.json`; re-ran it today, `git diff` on the output was empty — no drift. Nothing forces this regeneration to happen, though — no test fails if `schema.ts` goes stale and nobody reruns the script. That is a real gap, smaller than the one below, noted so it isn't mistaken for closed |
| `worker/src/index.ts` | consumer, not a copy | imports `TRAITS_SCHEMA` from `schema.ts` for the *request* (`output_config.format.schema`) — but until today did nothing with it on the *response* side; see §2 |
| `game/Assets/Core/CatTraits.cs`'s `Allowed` dictionary | **hand-copy** | `CatTraitsTests.TheAllowedValuesMatchTheWorkerSchema` reads `schema.json` off disk and asserts, for every value in `Allowed`, that the raw JSON text contains it. Real and it runs (it fails loudly rather than skipping if the path is wrong, per its own comment referencing `AUDIT-2026-08-27.md` item 4). But it is **one-directional and substring-based**: it cannot catch `schema.json` gaining a value `Allowed` does not have, only `Allowed` naming something `schema.json` has lost. A guard, not a mirror |
| `game/Assets/View/CoatBuilder.cs`'s `Coats` (6 `base_color` values) and `Eyes` (3 `eye_color` values) dictionaries | **hand-copy, unguarded** | No test in the project references `CoatBuilder` at all (`grep -rln "CoatBuilder" game/Assets/Tests` — nothing). Worse than silent: a miss falls back to `Color.white` (`Coats.TryGetValue(...) ?? Color.white`), so a vocabulary drift here would not throw, not fail a test, and not even look obviously wrong — it would just quietly paint the wrong colour. Out of this pass's touchable directories (`game/Assets/View` is off-limits), named here rather than fixed |
| `game/Assets/View/CoatMasks.cs`'s `PatternTabby`/`PatternPointed`/`PatternBicolor`/`PatternCalico`/`PatternTuxedo` constants | **hand-copy, unguarded** | Five of the six `pattern` values (`solid` has no mask, it is the absence of one) as mask-name strings. Same file family as `CoatBuilder`, same lack of any test tying it back to `schema.json`. Also out of scope to touch here |

Six real copies total (the source does not count itself): one generated
(`schema.ts`), one two-way-tested-in-name-but-one-directional-in-practice
(`CatTraits.Allowed`), and two genuinely unguarded (`CoatBuilder`,
`CoatMasks`) that this pass could name but not fix.

## 2. The SCOPE's three claims, checked against the files — and the one that was wrong

**"Values are constrained by the schema, not the prompt" — true.**
`schema.json` carries `enum` on all five fields and `additionalProperties:
false` at the object level (`tools/traits/schema.json:29`). The prompt in
`worker/src/index.ts` (`PROMPT` constant) asks for "the closest allowed
value," but the enforcement is the schema object sent as
`output_config.format.schema`, not the wording.

**"`maxItems` is unsupported, so `white_markings` is trimmed in the
handler" — half right, and the load-bearing half was missing.** `maxItems`
is confirmed absent from `schema.json`'s `white_markings.items` (also
asserted by `test_white_markings_cap_is_enforced_in_code` in
`tools/tests/test_traits_schema.py`). But "trimmed" overstates what the
established, tested behaviour actually is: `tools/traits/validate.py`
**rejects** an over-cap or duplicate list — raises `TraitsError`, does not
truncate it — matching this project's own stated rule two paragraphs above
in this file ("Out-of-enum means reject, never repair"). Silently trimming
`["chest","chest","chest","chest"]` down to three items would keep a
duplicate and still be a repair, not a rejection, so "trimmed" was never
what should happen even in Python.

**More importantly: "in the handler" was false.** `worker/src/index.ts` is
the actual handler — the code Cloudflare runs for a real `/traits` request —
and until today it read:

```ts
try {
    // Parsed here so a malformed answer becomes a 502 rather than
    // reaching the game as something it has to guess at.
    return json(JSON.parse(text), 200);
} catch {
    return json({ error: "model returned unparseable JSON" }, 502);
}
```

That is a **JSON syntax check, not a schema check.** A model returning
`{"base_color":"orange","pattern":"tabby","fur_length":"short","eye_color":"green","white_markings":["chest","chest","chest","chest"]}`
— syntactically valid JSON, semantically outside the schema on three counts —
would have parsed cleanly and gone straight to the game as a 200. `tools/traits/validate.py`
enforces the cap and the enums, correctly, but it is Python; the Worker is
TypeScript running on Cloudflare, a different runtime that never imports it
and could not. The cap being "enforced in the Worker's own code" was written
as if one enforcement covered both languages. It did not — only one side of
a two-sided contract was built.

**"the trimming is tested" — was true for Python (19 passing cases in
`tools/tests/test_traits_schema.py`, confirmed by re-running:
`python3.11 -m pytest tools/tests/test_traits_schema.py -q` → `19 passed`)
and false for TypeScript, where zero tests in `worker/test/traits.test.ts`
exercised an out-of-enum, extra-field, over-cap, or duplicate response
before today.**

## 3. Fixed — new file, one wired call, six new tests, no endpoint touched

Added `worker/src/validate.ts`: a direct TypeScript port of
`tools/traits/validate.py`'s `validate()` — same rules, same reject-not-repair
behaviour, same cap derived from the schema's own `white_markings.items.enum.length`
rather than hardcoded, so the number cannot drift between the two languages
independently of the schema either. Exports `validateTraits()` and a
`TraitsError` class.

Wired into `worker/src/index.ts`: the model's parsed JSON now goes through
`validateTraits()` before being returned; a `TraitsError` becomes a 502 with
a generic message (`"model returned traits outside the schema"`) — the
offending value stays server-side, matching how the existing code already
keeps the model's own error text out of the response one branch above.

Six new tests in `worker/test/traits.test.ts`, all stubbing the model
exactly as the existing suite already does (no account, no network, no
endpoint): an out-of-enum `base_color` → 502, value withheld from the
response body; an extra field → 502; `white_markings` one over the cap → 502;
a duplicate within the cap → 502; the full three-value set → 200 (cap is
inclusive); an empty list → 200 (no white on a cat is not an error).

```
$ npx vitest run   (in worker/)
 ✓ test/traits.test.ts (20 tests) 13ms
 Test Files  1 passed (1)
      Tests  20 passed (20)

$ npx tsc --noEmit   (in worker/)
test/traits.test.ts(197,31): error TS2769: ... (pre-existing, confirmed via
git stash — present before this change too, in the URL/readFileSync typing
of an unrelated existing test; nothing in validate.ts or index.ts introduces
a new error)

$ python3.11 -m pytest tools/tests/test_traits_schema.py -q
19 passed in 0.01s

$ dotnet test build/core-tests/core-tests.csproj -v q --nologo
Пройден!   : не пройдено 0, пройдено 152, пропущено 0, всего 152 — untouched, Core wasn't edited

$ python3 worker/sync-schema.py && git diff --stat worker/src/schema.ts
wrote worker/src/schema.ts
(no output from git diff — byte-identical, no drift)
```

## 4. What genuinely needs the deployed endpoint, and what did not

**Still needs it, correctly:** VERIFY 1, 2 and 4 as literally worded — "run
all 40 reference-set images through the deployed endpoint," "parse rate:
100%," "`white_markings` never exceeds its capped length **on any
response**." Those are claims about what a real model actually returns, and
nothing in a stub can stand in for that. Same for the model-choice
comparison (Haiku vs Sonnet, "compared by eye on a reference set") — that
needs real images through a real model.

**Did not need it, and was the thing actually missing:** the code path that
*would* enforce the schema on whatever a real endpoint returns. That is
pure logic — parse a JSON object, check it against rules already written
once in Python — and it is exactly what the rest of `worker/test/traits.test.ts`
already proves can be tested with a stubbed `fetch`, no account, no key, no
network (its own file header says as much). This is the same shape of gap
`70-analytics/01-sdk-integration` had this morning: "blocked on an account"
was true for the part that needs the account and quietly covered a second
part that didn't. Built now, tested now, verified today by the commands
above.

## Status

`schema.json`, `validate.py`, `sync-schema.py`, `schema.ts`, and
`CatTraits.Allowed` — the parts this document originally claimed done — hold
up, with two precise caveats now on record: `schema.ts`'s sync is unguarded
by a test (manual regeneration, verified by hand today) and
`CatTraits.Allowed`'s guard is one-directional. The part that did not hold —
the Worker's own response-side enforcement — is fixed, tested, and green
above. `status:` moves to `review`. What remains genuinely open is exactly
the deployed-endpoint VERIFY items and the model choice, both correctly
still blocked on `10-accounts/01-spend-cap` via `02-traits-worker` — so
`verify:` is left untouched.
