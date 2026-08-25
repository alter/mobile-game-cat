# Cloudflare Workers — the proxy worker for calling Anthropic (2026-08-24)

A working knowledge base on using Cloudflare Workers for one small handler: receiving a cat photo from an iOS game, calling the Anthropic Messages API, returning a short JSON. Source — the official documentation at developers.cloudflare.com/workers/ (verified with verbatim quotes), plus developer discussions and open GitHub issues where the documentation does not give a direct answer.

## In brief

- The configuration file — **`wrangler.jsonc`**: the documentation explicitly names it the format "for new projects" and notes that some new Wrangler features will only be available in JSON configuration. The current version of `wrangler` on npm as of the check date is **4.125.0**.
- The key finding on CPU time: the official Cloudflare documentation states directly that **waiting for a network response (`fetch()`, a KV read, a database query) does not count toward the CPU-time limit**. This means the free tier with its 10 ms CPU-per-request limit does not, in principle, get in the way of a one-second wait for the vision model's response — only the time the Worker actually occupies the processor (parsing JSON, decoding base64, etc.) is counted.
- The Anthropic key is set via the command `wrangler secret put ANTHROPIC_API_KEY` — the secret is not stored in the configuration file and does not end up in the repository; unlike ordinary `vars`, the secret's value is hidden even in the Cloudflare dashboard after it is created.
- A subdomain of the form `name.subdomain.workers.dev` is issued for free immediately on the first deployment and is technically fine for calls from the app, but Cloudflare explicitly does not recommend it for "business-critical" workloads — for a few hundred calls total this is not an obstacle.
- Rate limiting rules in the Cloudflare dashboard (WAF Rate limiting rules) work at the zone level (your own domain connected to Cloudflare); the programmatic way to limit rate directly in the Worker's code is the Rate Limiting binding, configured in `wrangler.jsonc` without an explicit dependency on the plan.
- D1 and Workers KV on the free tier are more than enough for a second handler for game events at a volume of "hundreds of calls": D1 — 5 GB per account and 50 subrequests per Worker invocation; KV — 100,000 read operations and 1000 write operations on distinct keys per day.
- `wrangler tail` gives a real-time log for free on any plan; Workers Logs in the dashboard on the free tier retains records for 3 days and accepts up to 200,000 events per day.
- The request body size is limited primarily by the Cloudflare account's own plan (100 MB on Free), not by Workers — a photo up to 200 KB in base64 (about 270 KB after encoding) fits with a huge margin.
- Bottom line on the applicability of the free tier: for our task — it works. The only risk is not network waiting but actual processor work (base64 decoding, assembling/parsing JSON) within the 10 ms limit; at hundreds of requests it is worth checking the actual usage once via `wrangler tail`, and if it falls short — switching to the paid Workers plan ($5/month, 30 seconds of CPU by default) without changing the code.

## 1. From zero to a deployed handler

Sources: developers.cloudflare.com/workers/get-started/guide/, developers.cloudflare.com/workers/wrangler/configuration/, developers.cloudflare.com/workers/wrangler/commands/.

The exact sequence of commands:

```sh
# 1. Create the project (installs wrangler locally into the project via npm create)
npm create cloudflare@latest -- my-first-worker

# 2. Go into the project folder
cd my-first-worker

# 3. Local check (on first run this opens a browser to log into the Cloudflare account)
npx wrangler dev

# 4. Deploy to Cloudflare
npx wrangler deploy
```

A separate explicit account login (if needed ahead of time, rather than during the first `wrangler dev`):

```sh
wrangler login
```

Per the documentation: "Authorize Wrangler with your Cloudflare account using OAuth." There is also `wrangler logout` — "Remove Wrangler's authorization for accessing your account. This command will invalidate your current OAuth token and delete the stored credentials." — and `wrangler whoami` to check the current login.

**Configuration file.** After `npm create cloudflare@latest`, the generator (C3) creates `wrangler.jsonc`: "C3 will have generated the following: `wrangler.jsonc`: Your Wrangler configuration file." The configuration documentation directly recommends this exact format: "Cloudflare recommends using `wrangler.jsonc` for new projects, and some newer Wrangler features will only be available to projects using a JSON config file." The old `wrangler.toml` is still supported (starting with Wrangler 3.91.0 both formats work in parallel), but for a new project there is no reason to pick TOML.

Minimal configuration in `wrangler.jsonc`:

```jsonc
{
	"name": "cat-traits-proxy",
	"main": "src/index.js",
	"compatibility_date": "2026-08-24"
}
```

The current version of `wrangler` on npm as of the check: `4.125.0` (the `latest` tag).
## 2. The anatomy of a handler

Sources: developers.cloudflare.com/workers/runtime-apis/handlers/fetch/, developers.cloudflare.com/workers/runtime-apis/request/, developers.cloudflare.com/workers/runtime-apis/response/.

The modern shape of a Worker module is a default export of an object with a `fetch` method taking three arguments: the request, the environment bindings (`env`), and the execution context (`ctx`):

```js
export default {
	async fetch(request, env, ctx) {
		// request — the standard Web-platform Request object
		// env — access to secrets, variables and bindings (KV, D1, Rate Limiting, etc.)
		// ctx — e.g., ctx.waitUntil(promise) for background work after the response
		return new Response("ok");
	},
};
```

Breaking down the task for our `/traits` handler fits into this sequence inside `fetch`:

1. Check the method: `if (request.method !== "POST") return new Response(null, { status: 405 });`
2. Check the path: `new URL(request.url).pathname === "/traits"`.
3. Read the body as JSON: `const body = await request.json();` — on a parse error (a non-JSON body) `request.json()` throws an exception that must be caught and result in a `400` response.
4. Check the input size before passing it further (we set our own limit — 200 KB per the task's terms, well below the system limits, see section 5) — since the client's `Content-Length` cannot be trusted, it's safer to check the length of the already-read base64 string.
5. Call Anthropic via `fetch()` (section 4).
6. Have the Worker return compact JSON of the required shape and the correct status code (`200` — success, `400` — invalid input, `413` — size limit exceeded, `502` — error calling Anthropic, `429` — rate limiting triggered).

The response is built via the standard `Response` constructor:

```js
return new Response(JSON.stringify({ ok: true, traits }), {
	status: 200,
	headers: { "content-type": "application/json" },
});
```

A complete working sample for the task — receiving a base64 snapshot and calling Anthropic — is given as a separate section below ("Full sample of the `/traits` handler").
## 3. Secrets

Source: developers.cloudflare.com/workers/configuration/secrets/.

The command for adding a secret (creates a new version of the Worker and deploys it immediately):

```sh
npx wrangler secret put ANTHROPIC_API_KEY
```

Wrangler will prompt for the value interactively (the value does not stay in shell history). Access to the secret in code is via the `env` parameter:

```js
export default {
	async fetch(request, env, ctx) {
		const apiKey = env.ANTHROPIC_API_KEY;
		// ...
	},
};
```

**Why the key must not be written into the configuration file's `vars`.** The documentation warns directly: "Do not use `vars` to store sensitive information in your Worker's Wrangler configuration file. Use secrets instead." Ordinary `vars` are stored in plain text right in `wrangler.jsonc` — meaning if this file is committed, the key would go into git. The difference between a secret and a variable is stated verbatim in the documentation: "The difference is secret values are not visible within Wrangler or Cloudflare dashboard after you define them" — meaning the secret is not only absent from the configuration file, it is also not shown back even in the dashboard once it has been entered.

For local development (`wrangler dev`), secrets are placed in a `.dev.vars` file (or `.env`) next to the configuration file — but this file must always be excluded from version control: "The `.dev.vars` and `.env` files should not be committed to git. Add `.dev.vars*` and `.env*` to your project's `.gitignore` file."

Bottom line: the Anthropic key must never appear in `wrangler.jsonc` or in the Worker's own code — only via `wrangler secret put` in production and `.dev.vars` (in `.gitignore`) locally. With this arrangement the key never reaches the player's device at all — it exists only on Cloudflare's side.
## 4. Calling a third-party API — the key question about CPU time

Sources: developers.cloudflare.com/workers/platform/limits/, developers.cloudflare.com/workers/runtime-apis/fetch/.

The call is made with an ordinary `fetch()` from within the handler body:

```js
const anthropicResponse = await fetch("https://api.anthropic.com/v1/messages", {
	method: "POST",
	headers: {
		"content-type": "application/json",
		"x-api-key": env.ANTHROPIC_API_KEY,
		"anthropic-version": "2023-06-01",
	},
	body: JSON.stringify(payload),
});
```

**Waiting for a network response does NOT count toward the CPU-time limit.** The Cloudflare documentation states this in plain text: time spent waiting for a network response (including `fetch()` calls, KV reads, database queries) does not count toward CPU time — "Waiting on network requests (such as `fetch()` calls, KV reads, or database queries) does not count toward CPU time." This is a distinction between two different quantities:

- **CPU time** — only the processor's active work done by the Worker itself is counted: parsing JSON, serialization, decoding base64, any computation in JS. This is exactly the quantity subject to the 10 ms limit on the free tier.
- **Wall time (clock time)** — the total elapsed time from the start to the end of processing a request, including waiting on the network, I/O, and other asynchronous operations: "Wall time (also called wall-clock time) is the total elapsed time from the start to end of an invocation, including time spent waiting on network requests, I/O, and other asynchronous operations." For HTTP handlers, the documentation separately notes the absence of a hard limit on wall time: "There is no hard limit on duration for HTTP-triggered Workers. As long as the client remains connected, the Worker can continue processing…"

This directly answers the task's question: **a one-second (or longer) wait for the vision model's response from Anthropic does not consume the 10 ms CPU-time budget of the Workers Free tier.** The only thing that does consume this budget is the Worker's own work before and after the wait: reading and parsing the incoming JSON with the base64 string (up to ~270 KB after encoding), assembling the request body to Anthropic, parsing the response, and assembling the ~100-byte response to the game. This is substantially less than, say, password hashing (in real developer discussions it is precisely computationally heavy operations like scrypt/bcrypt that regularly hit the 10 ms limit on Free — see the "Pitfalls" section), but decoding hundreds of kilobytes of base64 is also not a free operation, and after the first deployment it is worth checking the actual CPU usage once via `wrangler tail` (the `cpuTime` field in the structured output).

**Limits on outgoing calls:**

- Simultaneous open outgoing connections — **6** (`developers.cloudflare.com/workers/platform/limits/`) — not an obstacle for our task: one incoming request requires one outgoing call to `api.anthropic.com`.
- Subrequests per Worker invocation: **50 on Workers Free**, **up to 10,000 on Workers Paid** (up to 10,000,000 with a separate configuration). One `fetch()` to Anthropic is one subrequest, so the margin on the Free tier is more than sufficient.
- Script startup time — up to **1 second** on both the Free and Paid tiers; this is a limit on module initialization (imports, top-level code) separate from CPU time — not on request processing.

Thus the direct answer to the task's question: **waiting for a network response does NOT count toward CPU time**, and this is confirmed verbatim by the official documentation. The free tier is sufficient for the described node.
## 5. Limits

Source: developers.cloudflare.com/workers/platform/limits/.

| Metric | Workers Free | Workers Paid |
|---|---|---|
| Request body size (depends on the Cloudflare account plan, not the Workers plan) | 100 MB | 100 MB (Pro), 200 MB (Business), 500 MB (Enterprise by default) |
| Worker script size, compressed | 3 MB | 10 MB |
| Worker script size, uncompressed | 64 MB | 64 MB |
| CPU time per HTTP request | 10 ms | 30 seconds by default, up to 5 minutes configurable |
| Wall time (duration) per HTTP request | no hard limit while the client is connected | no hard limit while the client is connected |
| Script startup time | 1 second | 1 second |
| Simultaneous open outgoing connections | 6 | 6 |
| Subrequests per invocation | 50 | 10,000 (up to 10,000,000 configurable) |
| Number of Worker scripts per account | 100 | 500 |
| Memory per isolate | 128 MB | 128 MB |
| Daily request limit per Worker | 100,000 per day | unlimited |

For our task (a snapshot up to 200 KB in base64, a response of about 100 bytes, hundreds of calls total), none of these limits is a bottleneck — even the daily limit of 100,000 requests on Free comfortably exceeds the expected load.

## 6. Rate limiting

Sources: developers.cloudflare.com/workers/runtime-apis/bindings/rate-limit/, developers.cloudflare.com/waf/rate-limiting-rules/.

**Method 1 — Rate Limiting binding (programmatic, inside the Worker's code).** Configured in `wrangler.jsonc`:

```jsonc
{
	"name": "cat-traits-proxy",
	"main": "src/index.js",
	"compatibility_date": "2026-08-24",
	"ratelimits": [
		{
			"name": "MY_RATE_LIMITER",
			"namespace_id": "1001",
			"simple": {
				"limit": 100,
				"period": 60
			}
		}
	]
}
```

Usage in the handler:

```js
export default {
	async fetch(request, env) {
		const { pathname } = new URL(request.url);
		const { success } = await env.MY_RATE_LIMITER.limit({ key: pathname });
		if (!success) {
			return new Response("429 Rate limit exceeded", { status: 429 });
		}
		return new Response("Success!");
	},
};
```

Tooling version requirement: "You must use version 4.36.0 or later of the Wrangler CLI." The documentation strictly constrains the `period` parameter: "Must be either 10 or 60" (only 10 or 60 seconds — not an arbitrary period). The documentation contains no explicit statement of a plan restriction on this binding itself — there is no mention of the word "Free" or "Paid" in connection with using the Rate Limiting binding as such on that page, so on plan availability here — no data found, only the Wrangler version restriction.

To get the required "no more than N calls per device per day," it's worth using a stable device identifier as the key (`key` in `limit({ key })`) — e.g., a value the game sends in the request body or header — rather than IP, since mobile carrier IP addresses are often shared across many users. Since `period` is strictly limited to 10 or 60 seconds rather than a day, a daily limit cannot be assembled directly on this mechanism alone — the Rate Limiting binding does not support a persistent 86,400-second window; a daily limit needs either a counter on top of KV/D1 with its own windowing logic, or accepting a per-minute limit instead (100 calls per minute from one identifier practically rules out abuse given the expected load of hundreds of calls total).

**Method 2 — rules in the Cloudflare dashboard (WAF Rate limiting rules).** The documentation describes them as a setting "for a zone" — that is, the rule is created for a zone registered with Cloudflare (your own domain). On Cloudflare's free tier (not the Workers plan, but the account/zone plan itself), only 1 rule is available with narrow conditions: the counting field — only by IP address, the counting period is fixed at 10 seconds, the block period is up to 10 seconds, the expression condition — only by Path or "Verified Bot." Counting by headers, arbitrary expressions, and longer periods require paid Cloudflare plans (Business and above). Since these rules are configured at the zone level, their applicability to calls against a bare `*.workers.dev` (without your own domain connected to Cloudflare as a zone) is not directly confirmed by the documentation — no data found; for our task with a small call volume, it is more practical to use the Rate Limiting binding directly in the code.
## 7. Storage for a second handler

Sources: developers.cloudflare.com/d1/, developers.cloudflare.com/d1/platform/limits/, developers.cloudflare.com/d1/best-practices/import-export-data/, developers.cloudflare.com/kv/, developers.cloudflare.com/kv/platform/limits/, developers.cloudflare.com/kv/reference/kv-commands/.

### Cloudflare D1 (SQLite)

Creating the database and binding it:

```sh
npx wrangler d1 create [NAME]
```

Parameters: `[NAME]` (required) — the database name; `--location` — a hint about geographic location (`weur`, `eeur`, `apac`, `oc`, `wnam`, `enam`); `--binding` — the binding name in the Worker.

Executing SQL:

```sh
npx wrangler d1 execute [DATABASE] --command "INSERT INTO events (name) VALUES ('spawn')"
npx wrangler d1 execute [DATABASE] --file ./schema.sql --remote
```

Either `--command` or `--file` is required; `--local` runs the query against the local copy, `--remote` — against the actual remote D1 database.

Writing from the Worker's code (after the `d1_databases` binding is set up in `wrangler.jsonc`):

```js
export default {
	async fetch(request, env) {
		await env.DB.prepare("INSERT INTO events (name, ts) VALUES (?, ?)")
			.bind("spawn", Date.now())
			.run();
		return new Response("ok");
	},
};
```

D1 limits: Free — 10 databases, up to 500 MB per database, 5 GB storage per account, 50 subrequests per Worker invocation; Paid — 50,000 databases, up to 10 GB per database, 1 TB per account, 1000 subrequests per invocation. Limits common to both tiers: a row/BLOB — up to 2,000,000 bytes (2 MB), a single SQL query — up to 100,000 bytes (100 KB). The limits documentation gives no exact daily limits on the number of read/write queries — no data found.

Exporting data — the `d1 export` command:

```sh
npx wrangler d1 export [NAME] --remote --output backup.sql
```

The flags `--table` (specific tables), `--no-data` (schema only), `--no-schema` (data only) are supported.

### Workers KV

Creating a namespace and binding it in `wrangler.jsonc`:

```sh
npx wrangler kv namespace create [NAMESPACE]
```

```jsonc
"kv_namespaces": [
	{ "binding": "KV", "id": "<YOUR_BINDING_ID>" }
]
```

Writing and reading from code:

```js
await env.KV.put("KEY", "VALUE");
const value = await env.KV.get("KEY");
await env.KV.delete("KEY");
```

KV limits: Free — 100,000 read operations per day, 1000 write operations on distinct keys per day (writing the same key — no more than once per second on both tiers), 1 GB storage per account and per namespace; Paid — unlimited read/write operations, unlimited storage. Common limits: a key — up to 512 bytes, a value — up to 25 MiB, key metadata — up to 1024 bytes, up to 1000 namespaces per account, the number of keys in a namespace — unlimited.

There is no built-in command for a full export of KV data — only point operations and batch ones:

```sh
npx wrangler kv key list --namespace-id=<ID>
npx wrangler kv bulk get [FILENAME]
npx wrangler kv bulk put [FILENAME]
```

`kv bulk get` reads a list of keys from a file and returns key-value pairs; there is no "export everything in one call" among the official commands — this is also confirmed by an open GitHub issue (`cloudflare/wrangler`, #1957: "Currently, I have 300k+ keys in a project and I see no way to get this data out of Workers again. Wrangler has a bulk set, but not a bulk read" — as of the issue's date). In practice, a full export requires first getting the list of keys via `kv key list`, then running it through `kv bulk get`.

For the second handler (receiving game events, not accumulating sensitive data), either option works: KV — if a simple key-value pair is enough and instant consistency doesn't matter (KV has distributed, not strict, consistency), D1 — if queries, aggregates, and SQL over accumulated events are needed.
## 8. Logs and observability

Sources: developers.cloudflare.com/workers/observability/logs/workers-logs/, developers.cloudflare.com/workers/wrangler/commands/ (the Workers commands section).

**`wrangler tail`** — a real-time log right in the terminal:

```sh
npx wrangler tail [WORKER]
```

Main options: `--format` (`json` or `pretty`), `--status` (`ok` | `error` | `canceled`), `--header`, `--method`, `--sampling-rate`, `--search`, `--ip`, `--version-id`. This is a developer tool — available regardless of plan, showing the stream of requests as they are processed (including `console.log` and errors), but it saves nothing — the session ends when the terminal closes.

**Workers Logs** in the Cloudflare dashboard — persistent log storage with filters and analysis. Available on both tiers: Free and Paid. Free-tier limits: **200,000 logical events per day**, record retention — **3 days**. Paid tier: 20 million events per month included, retention — 7 days, beyond the limit — an extra charge of $0.60 per million events. When the overall limit of 5 billion records per account per day is exceeded, the system switches to 1% sampled recording until the end of the day.

For our load (hundreds of calls total), both tools are more than sufficient with a large margin: `wrangler tail` — for in-the-moment debugging (including a one-time measurement of actual CPU-time usage, see section 4), Workers Logs — to review the error history over the last 3 days without extra setup.

## 9. Your own name or the assigned one

Source: developers.cloudflare.com/workers/configuration/routing/workers-dev/.

A subdomain of the form `<worker_name>.<account_subdomain>.workers.dev` is issued **for free** immediately on the first deployment (`wrangler deploy`), without any extra configuration and without needing a custom domain. Technically it is fully suitable for calls from the mobile app — it's an ordinary HTTPS address.

At the same time, the documentation explicitly warns about the appropriateness of such an address: "It's recommended to run production Workers on a Workers route or custom domain, rather than on your workers.dev subdomain" — and describes workers.dev itself as intended "for personal or hobby projects that aren't business-critical." Technical restrictions on the name itself: up to 63 characters, letters, digits and hyphens only, cannot start or end with a hyphen.

For the described node (a few hundred calls total, an auxiliary service not critical to downtime), starting with the free `workers.dev` address is reasonable and sufficient; moving to a custom domain makes sense only if the project grows to a load the developer themselves considers "business-critical," or if finer zone-level configuration is needed (in particular, from section 6 — rate limiting rules in the dashboard, which are tied specifically to the zone/custom domain).
## 10. Pitfalls from developer discussions and GitHub issues

- **"Exceeded CPU time limit" on the Free tier almost always comes from computation, not network waiting.** A real example — the `better-auth` library: "Cloudflare's CPU time isn't sufficient to hash and verify passwords, so email/password login doesn't work well in a worker environment" (issue `better-auth/better-auth#969`), and a later case: "Sign-up fails intermittently with Worker exceeded CPU time limit. The pure JS scrypt from @noble/hashes is right on the edge of Workers' CPU [limit]" (issue `better-auth/better-auth#8860`). Our task has no direct hashing, but the general conclusion holds: before relying on the free tier, it's worth measuring the actual CPU usage for base64 decoding and JSON assembly/parsing once via `wrangler tail`, rather than relying solely on "the network doesn't count."
- **A limit separate from request CPU time — script startup time.** StackOverflow and issue `cloudflare/workers-sdk#2152` ("BUG: Startup script exceeded CPU time limit," error code `10021`) describe the confusion: top-level module code (imports of heavy libraries, load-time initialization) is also CPU-limited, and this is not the same as the limit on processing the request itself. For our small handler with no heavy dependencies this shouldn't be a problem, but it's worth keeping in mind when adding large npm packages (e.g., an SDK).
- **Local Rate Limiting binding counters in `wrangler dev` are unreliable.** Issue `cloudflare/workers-sdk#14962`: "In `wrangler dev`, hit your Worker, pause for ~15 seconds to look at something, hit it again, and your limit has silently reset" — local Rate Limiting binding counters in development mode can reset spontaneously. Real rate-limiting behavior must be verified on the deployed Worker, not only locally.
- **`wrangler dev --remote` can silently inherit someone else's rate-limiting rules.** Issue `cloudflare/workers-sdk#9880` describes how, during remote development (`--remote`), a Worker can implicitly fall under rate-limiting rules configured for the zone by a different developer — worth checking the source of unexpected `429`s when debugging as a team.
- **Secrets for `wrangler dev` must be placed separately — the configuration file does not hold them.** The official page confirms this explicitly (section 3), but the community regularly sees confusion from old articles that suggested putting secrets directly in `wrangler.toml` (example — a discussion on Cloudflare Community, "Confusing advice about secrets and Wrangler"); the current and only correct path is `wrangler secret put` for production and `.dev.vars` (mandatorily in `.gitignore`) for local development.
- **There is no full export command for Workers KV.** As noted in section 7, issue `cloudflare/wrangler#1957` and the subsequent discussion on Cloudflare Community ("Quick(ish) Reliable bulk export of all KV data from a namespace") confirm that no standard way to export an entire namespace in one call has existed for several years — if the second handler (events) later needs a bulk export, it is more sensible to choose D1 from the start (it has a full `d1 export` in SQL) instead of KV.
- **Dashboard rate-limiting rules depend on the zone.** As noted in section 6, the documentation describes creating a rule specifically "for a zone" — for a bare `*.workers.dev` without a connected custom domain, the applicability of these rules is not officially confirmed; for a minimal node it is safer to rely on the built-in Rate Limiting binding in code rather than dashboard rules.
## Full sample of the `/traits` handler

Configuration file `wrangler.jsonc` (the Anthropic key does not go in here — it is set separately via `wrangler secret put ANTHROPIC_API_KEY`, see section 3; the rate-limiting binding is an optional part, see section 6):

```jsonc
{
	"name": "cat-traits-proxy",
	"main": "src/index.js",
	"compatibility_date": "2026-08-24",
	"ratelimits": [
		{
			"name": "TRAITS_RATE_LIMITER",
			"namespace_id": "1001",
			"simple": { "limit": 30, "period": 60 }
		}
	]
}
```

Handler code (`src/index.js`). Constraints from the task: a snapshot up to 512×512, up to 200 KB before base64 encoding (roughly up to 273,000 characters after encoding — the base64 factor is 4/3), a vision-capable model, the key only on the Worker side, the response to the game — compact JSON:

```js
const MAX_BASE64_LENGTH = 280_000; // with margin over 200 KB * 4/3
const ANTHROPIC_MODEL = "claude-haiku-4-5"; // an inexpensive model with vision support
const ANTHROPIC_VERSION = "2023-06-01";

export default {
	async fetch(request, env, ctx) {
		const url = new URL(request.url);

		if (url.pathname !== "/traits") {
			return new Response(null, { status: 404 });
		}
		if (request.method !== "POST") {
			return new Response(null, { status: 405 });
		}

		// optional rate limiting — keyed on the device identifier
		// from a header set by the game itself
		if (env.TRAITS_RATE_LIMITER) {
			const deviceId = request.headers.get("x-device-id") ?? "unknown";
			const { success } = await env.TRAITS_RATE_LIMITER.limit({ key: deviceId });
			if (!success) {
				return new Response(JSON.stringify({ error: "rate_limited" }), {
					status: 429,
					headers: { "content-type": "application/json" },
				});
			}
		}

		let body;
		try {
			body = await request.json();
		} catch {
			return new Response(JSON.stringify({ error: "invalid_json" }), {
				status: 400,
				headers: { "content-type": "application/json" },
			});
		}

		const imageBase64 = body?.image_base64;
		if (typeof imageBase64 !== "string" || imageBase64.length === 0) {
			return new Response(JSON.stringify({ error: "missing_image_base64" }), {
				status: 400,
				headers: { "content-type": "application/json" },
			});
		}
		if (imageBase64.length > MAX_BASE64_LENGTH) {
			return new Response(JSON.stringify({ error: "image_too_large" }), {
				status: 413,
				headers: { "content-type": "application/json" },
			});
		}

		const mediaType = body?.media_type ?? "image/jpeg";

		const anthropicPayload = {
			model: ANTHROPIC_MODEL,
			max_tokens: 300,
			system:
				"You determine the cat's coloring traits from the photo. Respond with ONLY json, no explanations, " +
				"strictly following the schema: {\"color\":string,\"pattern\":string,\"eyeColor\":string}.",
			messages: [
				{
					role: "user",
					content: [
						{
							type: "image",
							source: { type: "base64", media_type: mediaType, data: imageBase64 },
						},
						{ type: "text", text: "Determine the cat's coloring traits from the photo." },
					],
				},
			],
		};

		let anthropicResponse;
		try {
			anthropicResponse = await fetch("https://api.anthropic.com/v1/messages", {
				method: "POST",
				headers: {
					"content-type": "application/json",
					"x-api-key": env.ANTHROPIC_API_KEY,
					"anthropic-version": ANTHROPIC_VERSION,
				},
				body: JSON.stringify(anthropicPayload),
			});
		} catch (err) {
			return new Response(JSON.stringify({ error: "upstream_unreachable" }), {
				status: 502,
				headers: { "content-type": "application/json" },
			});
		}

		if (!anthropicResponse.ok) {
			return new Response(JSON.stringify({ error: "upstream_error" }), {
				status: 502,
				headers: { "content-type": "application/json" },
			});
		}

		const anthropicJson = await anthropicResponse.json();
		const rawText = anthropicJson?.content?.[0]?.text ?? "";

		let traits;
		try {
			traits = JSON.parse(rawText);
		} catch {
			return new Response(JSON.stringify({ error: "bad_model_output" }), {
				status: 502,
				headers: { "content-type": "application/json" },
			});
		}

		// we save nothing — immediately return the compact response to the game (about 100 bytes)
		return new Response(JSON.stringify(traits), {
			status: 200,
			headers: { "content-type": "application/json" },
		});
	},
};
```

Deployment: `npx wrangler secret put ANTHROPIC_API_KEY`, then `npx wrangler deploy`. Local check: `npx wrangler dev` and `curl -X POST http://localhost:8787/traits -H "content-type: application/json" -d '{"image_base64":"..."}'`.
## Sources

- https://developers.cloudflare.com/workers/get-started/guide/
- https://developers.cloudflare.com/workers/wrangler/configuration/
- https://developers.cloudflare.com/workers/wrangler/commands/
- https://developers.cloudflare.com/workers/wrangler/commands/workers/
- https://developers.cloudflare.com/workers/wrangler/commands/d1/
- https://developers.cloudflare.com/workers/wrangler/commands/kv/
- https://developers.cloudflare.com/workers/wrangler/commands/general/
- https://developers.cloudflare.com/workers/configuration/secrets/
- https://developers.cloudflare.com/workers/runtime-apis/handlers/fetch/
- https://developers.cloudflare.com/workers/runtime-apis/fetch/
- https://developers.cloudflare.com/workers/platform/limits/
- https://developers.cloudflare.com/workers/runtime-apis/bindings/rate-limit/
- https://developers.cloudflare.com/waf/rate-limiting-rules/
- https://developers.cloudflare.com/d1/
- https://developers.cloudflare.com/d1/platform/limits/
- https://developers.cloudflare.com/d1/best-practices/import-export-data/
- https://developers.cloudflare.com/d1/wrangler-commands/
- https://developers.cloudflare.com/kv/
- https://developers.cloudflare.com/kv/platform/limits/
- https://developers.cloudflare.com/kv/reference/kv-commands/
- https://developers.cloudflare.com/workers/observability/logs/workers-logs/
- https://developers.cloudflare.com/workers/configuration/routing/workers-dev/
- npm registry: `npm view wrangler version` (verified 2026-08-24, `4.125.0`)
- GitHub issue `better-auth/better-auth#969` — https://github.com/better-auth/better-auth/issues/969
- GitHub issue `better-auth/better-auth#8860` — https://github.com/better-auth/better-auth/issues/8860
- GitHub issue `cloudflare/workers-sdk#2152` — https://github.com/cloudflare/workers-sdk/issues/2152
- GitHub issue `cloudflare/workers-sdk#14962` — https://github.com/cloudflare/workers-sdk/issues/14962
- GitHub issue `cloudflare/workers-sdk#9880` — https://github.com/cloudflare/workers-sdk/issues/9880
- GitHub issue `cloudflare/wrangler#1957` — https://github.com/cloudflare/wrangler/issues/1957
- Anthropic Messages API — request/header structure confirmed by the internal `claude-api` reference (`curl/examples.md`, current Anthropic model identifiers, cache date 2026-06-24).
