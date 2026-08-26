# Written, tested, not deployed — 2026-08-27

`worker/` holds the whole thing: `src/index.ts`, `wrangler.jsonc`, 13 tests.
What is missing is the account, and only the account.

## Why it was written before the account exists

This task depends on `10-accounts/01-spend-cap`, which is the owner's to set,
and everything downstream — the schema in use, the rate limit, the capture
screen's last step — waits behind it. None of that waiting needs to include
writing the code, and the four VERIFY status codes can be checked against the
handler directly, with the model endpoint stubbed. So they are.

`npx vitest run` in `worker/`: 13 passing, no account, no key, no network.

## The reason this component exists at all

A key shipped inside the app is extractable, so the model key never leaves the
Worker (`wrangler secret put ANTHROPIC_API_KEY`). Everything else follows from
that: the payload limits, the rate limit, and the fact that the photo is never
stored, logged or forwarded anywhere but to the model. The premise is "it is
*her* cat", and a proxy keeping a copy of every player's cat would be a
different product with a different privacy story.

## Decisions worth stating

- **400 KB body ceiling.** The crop is 512×512 under 200 KB (`07`), which base64
  inflates to about 270 KB. 400 KB leaves room for the envelope and refuses
  anything that is not our own crop. Checked twice: on `content-length` before
  the body is read, and on the decoded string, because a chunked request has no
  length to check.
- **The limit is keyed by device id, never by IP.** Mobile carriers share
  addresses; an IP limit cuts off strangers along with an abuser. A client that
  sends no id lands in one shared `anonymous` bucket — throttled, not exempt.
  6 per 60 s: the binding accepts only 10 or 60 for `period`, and a player
  photographs her cat once.
- **The model's error text is never passed through.** It can carry account
  details, and the game has nothing to do with it; a 502 is enough for the game
  to fall back to the on-device colour estimate (`11`).
- **An unparseable model answer is a 502, not a pass-through.** The game should
  never receive something it has to guess at.
- **`output_config`, not `output_format`** — the parameter moved and the old
  name raises a `TypeError`. There is a test for it, because that is exactly
  the sort of thing written from memory.
- **The schema is generated, not copied.** `worker/src/schema.ts` is produced by
  `python worker/sync-schema.py` from `tools/traits/schema.json`, the single
  definition shared with the game. A hand-kept second copy drifts, and the
  first sign would be the Worker accepting a value the game refuses.

## Against the VERIFY list

- **1 — the four cases locally: met**, against the handler rather than through
  `wrangler dev`. 200 for a well-formed request, 400 for malformed JSON, 400
  for an empty image, 413 for oversize (both by header and by content), plus
  405/404 on the wrong method or path and 429 past the limit.
- **2 — the same four against a deployed URL: not met.** There is no
  deployment. It needs a Cloudflare login and the spend cap first.
- **3 — no key in any committed file: met.** The only occurrences of
  `ANTHROPIC_API_KEY` are the binding's name in `wrangler.jsonc`'s comment, the
  `Env` type, the header line, and a literal `"test-key-not-a-real-one"` in the
  tests.

## What deploying will take, once the cap is set

```sh
cd worker
npx wrangler login
npx wrangler secret put ANTHROPIC_API_KEY
npx wrangler deploy
```

Then point `CaptureScreen.AskWorker` at the returned `workers.dev` URL — the
hook is already there and currently null, which is why the offline fallback
(`11`) is the path that runs today.

**Model choice stays open.** Haiku 4.5 is set as the default because it is the
cheapest that supports structured outputs, but `cat-shelter-tech.md` says the
choice is settled by comparing colourings by eye on the reference set. That
comparison needs the endpoint and costs money. The set is built and waiting.
