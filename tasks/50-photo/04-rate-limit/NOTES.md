# What is written, what is proven, and what cannot be — 2026-08-27

The limit exists. `worker/src/index.ts:100-102` calls
`env.TRAITS_LIMITER.limit({ key: deviceId })` and answers 429 when it says no;
`worker/wrangler.jsonc` declares `TRAITS_LIMITER` at 6 requests per 60 seconds,
keyed by the device id the game sends.

`npx vitest run` in `worker/`: **14 passed**, no account, no key, no network.

## What the tests actually prove, which is less than it looks

The two limit tests stub `TRAITS_LIMITER` and assert what the **handler** is
responsible for:

- the key handed to the limiter is the device id — `expect(seen).toEqual(["the-device"])`
  — and not an IP, because mobile carriers share addresses and an IP limit cuts
  off strangers along with an abuser;
- a client that sends no device id lands in a shared `"anonymous"` bucket
  rather than escaping the limit;
- `success: false` becomes a 429.

They do **not** prove that Cloudflare's binding counts, refuses, or is
configured to anything in particular. A stub says whatever it is told to say.

## The gap that was closed today, and the one that was not

**Closed.** Nothing checked `wrangler.jsonc` against what this task asked for.
A limit of 600 an hour would have passed every test on the page. There is now a
test that reads the config and asserts 6 / 60, and it was mutated to confirm it
can fail: changing the limit to 600 produces
`AssertionError: expected 600 to be 6`, and the suite goes 14 passed → 1 failed.

**Not closed, and it is VERIFY item 3.** *"Deployed Worker re-checked, not just
`wrangler dev` — local counters are known to reset unreliably in dev mode."*
There is no deployed Worker. There is no Cloudflare account and no model key,
because `10-accounts/01-spend-cap` is deferred — see `DECISIONS.md` D17. So the
one thing that would prove the limit *works*, as opposed to being correctly
asked for, cannot be done.

The task therefore stays `in_progress`. Everything that can be built has been;
what remains is not code.

## Worth remembering when the account exists

D11 is the frame: this is a **courtesy** limit against a stuck or looping
client, not a defence. The ceiling on financial damage is the provider spend
cap, and nothing here substitutes for it. The binding's `period` accepts only
10 or 60 seconds, so a genuine per-device daily cap needs a counter over KV or
D1 — build that only if the spend cap shows it is needed, per this task's own
SCOPE.
