# The cost of exiting free services (2026-08-24)

The task isn't "how much does this cost," but "what will it cost to leave" —
if a free service turns out to be a freemium trap. For each service: can you
take your own data with you, does export fall under the free tier, how tied
are the code and data to this particular service, what is known about past
changes to the terms.

## In brief

1. **GameAnalytics**: dashboards and aggregated reports — free and without a
   card, but **raw per-player data can only be exported for a fee** (Data
   Export / PipelineIQ, officially from $499/month). This is the lock-in
   itself: the metrics are visible, but your own raw events are not.
2. GameAnalytics doesn't store data indefinitely: full filters — 1 month,
   degradation after that, after 12 months the data is anonymized or deleted
   — regardless of plan.
3. **Cloudflare Workers** — the handler is written on standard Web APIs
   (`fetch`, `Request`, `Response`); this is confirmed by Cloudflare itself
   as WinterCG compliance; moving to Deno/Node/Bun is a small edit.
4. Cloudflare **D1** is exported by an official command as a SQL dump (not
   as a `.sqlite` file); Cloudflare **KV** has no ready-made full-export
   command — only the combination `kv key list` + `kv bulk get` (a feature
   request from 2021 has not been implemented).
5. **App Store Connect**: both manual CSV export and the Analytics Reports
   API are free, with no paid tier for your own data.
6. Critical at ~100 installs: Apple has an official **privacy threshold —
   data is hidden below 5 users/devices** per slice, and displayed values
   contain noise. This doesn't affect the overall install count, but it
   kills any segmentation (by country, device, campaign).
7. **Anthropic API**: the call is a single POST request via a plain
   `fetch()`, contained in one Cloudflare Worker function; moving to a
   different provider means rewriting this function, not the architecture.
8. The Anthropic API has an official **hard spending ceiling**, configurable
   in the console (Console → Billing → Spend limits): on reaching it, the
   API rejects requests instead of silently charging money.
9. Confirmed cases of a sharp cutback in free terms for developer services
   exist without any need to invent them: Heroku (2022), Twitter/X API
   (2023), Unity Runtime Fee (2023, later canceled under pressure).

## Summary table

| Service | Can you take your data | Is export free | Cost of moving | Verdict |
|---|---|---|---|---|
| GameAnalytics | Partially: aggregates and reports — yes; raw per-player events — no | No: manual CSV from reports — yes, free; raw export (Data Export/PipelineIQ) — paid add-on from $499/month | Low for aggregates; high for raw data (either pay or lose history older than 12 months) | Tolerable |
| Cloudflare Workers (code + KV + D1) | Yes, fully | Yes: D1 — via `wrangler d1 export` command to SQL; KV — manually (`kv key list` + `kv bulk get`) | Low: code on standard `Request`/`Response`/`fetch`, only the KV/D1 bindings are specific | Safe |
| App Store Connect Analytics | Yes | Yes: manual CSV and Analytics Reports API — free | Low, but with a caveat about the privacy threshold (<5 users/devices per slice) | Safe (with a caveat) |
| Anthropic API (vision-capable model) | Yes; photos aren't stored on the provider's side — nothing to take | Not applicable: no accumulated data on the provider's side | Low-to-medium: one POST in one function; hard spending ceiling is configurable | Tolerable |

## 1. GameAnalytics

### Can the data be taken, and in what form

The free tier gives access to the AnalyticsIQ, SegmentIQ, MarketIQ
dashboards: ready-made and customizable panels, funnels, retention, an AI
agent (limit — "3 per user / 6 per Org" messages per day). Manual export of
individual tables to CSV is provided by the **Explore** tool ("Table Export:
View results in a tabular format and download as CSV (comma, semicolon,
colon, or tab–delimited)") and **Scheduled Reports** (scheduled CSV
attachments) — the documentation shows no sign that this feature is paid.

The situation is different with **raw, player-level events**. This is a
separate product within the DataSuite package — **Data Export** (aka Raw
Export, PipelineIQ): "facilitates the automated export of your game event
data," in JSON and Parquet formats, updated every 15 minutes or more often,
delivered to your own AWS/GCS. The free tier does not provide exactly this
data.

### Does export fall under the free tier

No. The pricing page (gameanalytics.com/pricing) lists Data Export and
PipelineIQ as an add-on on top of paid plans: **PipelineIQ — from
$499/month** plus separate payment for data storage ($6.25 per TiB).
According to a developer's testimony on Reddit (January 2024, before the
current repricing model), direct export of raw data cost about $100/month —
meaning the minimum price of accessing your own raw data has risen over time
(no official changelog with a date and reason for the change was found —
this is a comparison of two data points, not a confirmed history).

### How tied are the data and code

There is no code dependency — GameAnalytics is used via an SDK, and calls
like `addDesignEvent` create no architectural lock-in. The lock-in is in the
**data**: without a paid subscription to Data Export, the only channel out
is manual CSV from ready-made reports, meaning **aggregated**, not raw,
events for a specific player.

Retention period (data-retention-and-deletion-policy, official page): full
metrics and filters are available for **up to 1 month**; 1–3 months — part
of the data is unavailable (e.g., error stack traces); 3–12 months —
filtering is noticeably restricted; after **12 months** the data is
anonymized or deleted irreversibly — "Retained for 12 months on a rolling
basis, then permanently anonymized or deleted." This rule does not
distinguish between paid and free plans for basic analytics (AnalyticsIQ);
separately, the PipelineIQ Data Warehouse retains no more than 30 days and
only starts filling after PipelineIQ is purchased.

Deletion of the account and data: no explicit self-service "delete account"
button was found in the documentation. The Developer Policy phrases this as
an obligation on request: "GameAnalytics will securely delete or return all
personal data in its possession, unless retention is necessary for legal and
regulatory compliance or technical constraints" — meaning deletion happens
on request (privacy@gameanalytics.com), not with a single button in the
console. Inactive accounts (no login for 12 months) are deleted automatically
after a 30-day email warning.

### What is known about changes to the terms

GameAnalytics switched from a paid model to fully free in 2013–2014
("GameAnalytics goes 100% free"). Since then the direction has reversed:
paid AnalyticsIQ Pro ($49/month), MarketIQ Pro ($499/month), SegmentIQ
(custom pricing) have appeared, and — most important for the topic of
exit — access to raw data specifically became a separate paid add-on
(PipelineIQ/Data Export), which did not originally exist in this form. This
is not a single loud incident, but a gradual, documented shift: what could
once be obtained within the free perimeter now requires a separate
subscription.
## 2. Cloudflare Workers

### How tied is the code

The handler is written as an export of an object with a `fetch(request, env,
ctx)` method, where `request` is the web platform's standard `Request`, and
the response is built with the ordinary `Response` constructor. Cloudflare
itself officially confirms this: in the blog post "The road to a more
standards-compliant Workers API" (November 14, 2022), it states that the
Workers runtime has "compliant or nearly compliant implementations of every
one of the WinterCG Minimum Common API" — meaning Workers, Deno, Node (via
an adapter), and Bun rely on the same standard set of web APIs (`fetch`,
`Request`, `Response`, `Headers`, `ReadableStream`, etc.).

The practical conclusion for the project: if the `/traits` handler doesn't
touch `env.KV`/`env.DB`/`ctx.waitUntil`, and only accepts a `Request`, parses
JSON, and makes an outgoing `fetch()` to Anthropic — such code moves to
another environment with a minimal edit (replacing the entry point and the
way secrets are read). What's specific to Cloudflare is precisely the
`env` accesses (secrets, KV bindings, D1 bindings) and `ctx.waitUntil` —
these are the only load-bearing walls.

### Storage: can it be fully exported

**D1 (SQLite-compatible database).** The official export is the command
`wrangler d1 export <database> --remote --output=./database.sql`, with
`--table`, `--no-data`, `--no-schema` flags. The result is **a file of SQL
statements**, not a `.sqlite` file: "You cannot download your SQLite
database as is. All you can do is dump an SQL file" (confirmed both by a
Hacker News discussion and by Cloudflare's own documentation — the export
produces a `.sql` file, which is imported back with the `d1 execute --file`
command). Export limitations: incompatible with virtual tables (including
FTS5), blocks other access to the database during execution, numbers are
limited to JavaScript's 52-bit precision. This is not data loss — the SQL
dump opens in any SQLite and transfers to Postgres with a minimal syntax
edit — but it's also not "click a button, download a file."

**Workers KV.** There is no ready-made command for a full export. The
official commands are only for single-item and batch operations:
`wrangler kv key list`, `wrangler kv bulk get`, `wrangler kv bulk put`. An
open feature request for a full export function has been sitting in the
`cloudflare/wrangler` repository since 2021 (issue #1957: "Currently, I have
300k+ keys in a project and I see no way to get this data out of Workers
again. Wrangler has a bulk set, but not a bulk read") and remains
unimplemented. In practice, export is possible — get the list of keys, then
run it through `bulk get` — but it requires a small script, not a single
command.

### What is known about past changes to the free tier

No directly confirmed case was found of **execution limits** being cut back
for Workers Free (requests per day, CPU time, KV/D1 limits) — the available
official sources, on the contrary, record expansion: the free tier for
Workers KV was introduced on November 16, 2020 as the opening up of a
previously paid feature ("Workers KV - free to try, with increased
limits!"). Current official Free limits: 100,000 requests per day, 10ms CPU
per invocation, KV — 100,000 reads and 1,000 writes per day, 1GB storage;
D1 — 5 million rows read and 100,000 rows written per day, 5GB storage.

One confirmed but adjacent case of a cutback was found — not to Workers
itself, but to DNS zones: according to a third-party blog (no official
announcement of the reason was found), the DNS record limit for free zones
was lowered from 3,000 to 1,000 for zones created after September 2024. This
doesn't directly apply to Workers execution, but it shows that Cloudflare
does selectively shrink the free tier's perimeter where it sees abuse.

For the project's expected load (a photo up to 200KB, hundreds of requests
total), all the listed limits have a multiple-fold margin; the project won't
come close to any of them.
## 3. App Store Connect Analytics

### Can the metrics be exported

Yes, in two ways, and both are free. The first — manual CSV export directly
from the Analytics dashboard (the export button is present in the interface
— confirmed by Apple's official video "Measure and improve acquisition with
App Analytics" and by independent interface breakdowns; separately, the
legacy Sales and Trends section has its own "Download and view reports"
page with a step-by-step description of downloading CSV). The second, more
complete path — the **Analytics Reports API**: "The Analytics Reports API
lets you export App Store Connect Analytics data in bulk, enabling you to
perform offline analysis." The export format is compressed
tab-separated-value files (`.txt.gz`). The first request requires an Admin
role in App Store Connect; subsequent downloads only need an API key with
the Sales and Reports or Finance role. No official page mentions a paid
tier or a monetary limit — exporting your own data is free regardless of
scale.

### The privacy threshold — the main risk at ~100 installs

Apple's official page "Protecting user privacy in report data" directly
sets out two mechanisms for reports marked "Detailed":

> Thresholding — Omits values with data from fewer than 5 users or 5 unique
> devices.
>
> Noise — Adds a small random number to metrics.

For "detailed" reports the noise precision is also stated: "approximately
68.2% of values are expected to be within +/- 2 of the true values," 95% —
within ±4, 99.7% — within ±6. Separately, for App Usage reports the
threshold is stricter — the entire report simply isn't generated if there's
data from fewer than 5 users: "If fewer than five users contribute to a
usage metric for a specific day, week, or month, then the relevant report
does not generate." For "detailed" reports, additionally, an individual row
is not populated if it covers fewer than 5 devices.

**What this means for ~100 installs.** The final, non-segmented count
("total installs for the period") will almost certainly not hit this
threshold — a hundred installs is more than five. But **any segmentation**
— by country, device model, referral source, A/B test page variant — easily
produces groups smaller than 5 users, and such rows simply won't appear in
the report, rather than showing "0" or "<5." Even the visible values carry
random noise of a few units. Conclusion: at this volume you can only count
on rough, non-segmented totals; any "broken down by" on a sample of a
hundred installs is unreliable or simply won't be shown at all.

### What is known about past changes

Apple's official news from March 25, 2026 (also confirmed by
macstories.net, daringfireball.net, macrumors.com on the same day)
announces a large-scale Analytics update — more than 100 new metrics — and
at the same time **retires the legacy Sales and Trends section**:
"Dashboards in Trends will be deprecated starting in mid-2026. App Store
Connect will stop generating new Trends reports in 2027." This is not a cut
to free access — the new analytics remains free and more comprehensive —
but it requires migrating to the new dashboard/API if the analysis is built
on old Sales and Trends reports.
## 4. Anthropic API as the vision-model provider

### How tied is the code

Per the project's own documentation (`python/05-cloudflare-worker-proxy.md`),
the call to Anthropic is made without an SDK — a plain `fetch()` inside a
Cloudflare Worker: POST to `https://api.anthropic.com/v1/messages`, headers
`x-api-key` and `anthropic-version`, body — JSON with the image (as message
content) and `output_config.format` (a JSON Schema guaranteeing the response
structure — `additionalProperties: false`, `enum` for enumerable fields).
The entire integration is one function inside one `/traits` handler, with no
agent loop, no Files API, no direct use of the Anthropic SDK.

Moving to a different vision provider would require rewriting exactly this
function: the endpoint address, the form of the authorization header, the
form of the image block (details of encoding and nesting differ between
providers, though all accept the image as base64 inside a JSON request),
and — most importantly — the name and shape of the structured-output
parameter: for Anthropic this is `output_config.format.schema`; alternative
providers implement JSON Schema structured output their own way (different
parameter nesting, their own restrictions on `enum`/`additionalProperties`).
The **idea** itself — a photo plus a JSON Schema on output — is portable;
what would need porting is not the architecture but one function that
translates the request/response between formats.

### Is there a way to cap spending

Yes, confirmed by official documentation (platform.claude.com, Rate limits
section) — and it is indeed a hard ceiling, not just a notification:

> Spend limits set a maximum monthly cost an organization can incur for API
> usage.

It works in two layers. The first is the plan-level ceiling that Anthropic
itself enables by default: the starter plan (Start) has **$500/month**,
Build — $1,000, Scale — $200,000. On hitting the ceiling, the API stops
responding to requests (HTTP 429, `error_code: enforced_spend_limit_reached`)
until 00:00 UTC on the first day of the next month. The second is your own,
lower limit that you can set yourself: Console → Settings → Billing →
Spend limits → "Adjust limit" (or "Set limit," if no limit has been set
yet). On hitting your own limit, requests are rejected with HTTP 400
(`invalid_request_error`) instead of going through and silently spending
money.

For the scale of this task (per the calculation in
`vision-model/01-traits-strict-json.md` — 0.10–0.20 cents per photo parse on
Haiku/Sonnet 5, roughly 20–40 cents for the entire test run), even the
minimal plan-level ceiling of $500 is an excessive margin; the practical
advice is to set your own limit at a few dollars through the same console,
so that a bug in the code (e.g., an infinite retry loop) can't turn into an
unexpected bill.
## Signs of a trap

From the services examined above and general practice among developer
services — a stable set of signs by which a freemium trap can be recognized
in advance, before you need to leave:

1. **Export only on the paid tier** — metrics are visible for free, but the
   source, raw data cannot be taken without a subscription (example
   examined above — GameAnalytics Data Export/PipelineIQ from $499/month).
2. **Storage is limited by a time period**, after which data is anonymized
   or deleted irreversibly, regardless of willingness to pay retroactively
   (GameAnalytics — degradation after 1 month, end of history after 12).
3. **Its own, nonstandard formats** instead of common ones — complicates
   migration even where export formally exists (D1 gives back a SQL dump,
   not a `.sqlite` file — a tolerable but real example of such friction).
4. **No ready-made command for a full export** where export is technically
   possible piecemeal — Cloudflare KV: a feature request from 2021
   (issue #1957) remains unimplemented, even though piecemeal export exists.
5. **Mandatory card binding "just for identity verification"** even on the
   free plan — a typical first step toward being charged when a limit is
   exceeded, which is easy to miss. None of the four services examined here
   was found to have such a requirement on the free tier.
6. **A sharp change of terms announced "retroactively."** The confirmed case
   most relevant to game development is the **Unity Runtime Fee**
   (September 2023): a fee of up to $0.20 per install above a threshold,
   applied even to already-released games ("retroactive"), which triggered
   mass protest from developers and a public apology from Unity (Axios,
   September 22, 2023: "Unity will no longer require game creators to pay
   retroactive fees"); the fee itself was canceled only a year later, in
   September 2024.
7. **Complete shutdown of the free tier with a short evacuation window** —
   **Heroku**: announced in late August 2022, took effect November 28,
   2022 — free Dynos, Postgres, and Redis were shut off, data from free
   databases deleted ("Removal of Heroku Free Product Plans," official
   Heroku FAQ; devcenter.heroku.com/changelog-items/2461).
8. **Cutting a free API tier down to a state that no longer solves the
   original task**, while keeping up the sign that says "there's still a
   free plan" — **Twitter/X API** (announced February 2, 2023, took effect
   February 9, 2023): the free tier lost read access to data, leaving only
   the ability to post tweets.
9. **A targeted cutback to specific limits without a broad announcement** —
   per a third-party source (no official announcement of the reason was
   found), Cloudflare lowered the DNS record limit for free zones from
   3,000 to 1,000 for zones created after September 2024.
## Verdict per service

**GameAnalytics — tolerable.** You can leave without loss as long as it's
about aggregated metrics and manual CSV — that's free and without a card.
But precisely **raw events for a specific player**, that is, what's usually
called "your own data," cannot be taken without a paid subscription to Data
Export/PipelineIQ (from $499/month). Plus the data degrades and disappears
after 12 months regardless of paying or not. For a one-off test at ~100
installs this isn't critical: the volume is manageable for manual export,
and raw events can also be obtained through your own logging, without
relying on GameAnalytics.

**Cloudflare Workers (code, KV, D1) — safe.** The code is written on
standard `Request`/`Response`/`fetch`, environment lock-in is minimal and
concentrated in a couple of places (`env` bindings). D1 is exported by an
official command into a SQL dump without data loss. KV has no ready-made
command for a full export, but a script-based export (list + bulk get)
solves the problem without loss — just without a button. The free tier's
limits for the expected load have an enormous margin.

**App Store Connect Analytics — safe, with one caveat.** Export (both
manual and via API) is free with no restrictions and no signs of a paid
tier. The caveat isn't about dependency on Apple, but about the usability of
the data at this scale: the privacy threshold (fewer than 5 users/devices
per slice) and noise in the metrics mean that at ~100 installs only rough
totals are reliable, and any segmentation may simply not be shown at all.

**Anthropic API — tolerable.** The integration is a single POST request via
plain `fetch()` in one function; data (photos) isn't stored on Anthropic's
side — nothing to take, and in this sense there's no data lock-in at all.
Moving to a different provider means rewriting one function (endpoint,
authorization, the shape of the JSON schema), not the project's
architecture. A hard spending ceiling exists and is configurable in the
console — the promise "you won't be silently overcharged" is backed by
documentation, not a declaration. The rating is "tolerable" rather than
"safe" because switching providers would mean re-tuning the prompt and the
exact shape of the structured output from scratch: this isn't architectural
work, but it isn't zero either.

## Sources

- GameAnalytics: [Pricing](https://www.gameanalytics.com/pricing),
  [Data Export — Overview and Use Cases](https://docs.gameanalytics.com/products-and-features/pipeline-iq/data-export/overview-and-use-cases/),
  [Raw Game Data, Delivered Your Way](https://www.gameanalytics.com/pipelineiq/data-export),
  [Data Retention and Deletion Policy](https://www.gameanalytics.com/trust/data-retention-and-deletion-policy),
  [Data Retention Practices](https://docs.gameanalytics.com/event-tracking-and-integrations/data-retention-and-limits/data-retention-practices/),
  [Developer Policy / Privacy FAQ](https://www.gameanalytics.com/trust/privacy-faq),
  [Explore](https://docs.gameanalytics.com/products-and-features/analytics-iq/explore/),
  [Scheduled Reports](https://docs.gameanalytics.com/products-and-features/analytics-iq/scheduled-reports/),
  testimony on the price of Raw Export ($100/month, January 2024) — [discussion on Reddit](https://www.reddit.com/r/gamedev/comments/192kozr/suggestions_for_free_or_cheap_analytics_raw_data/).
- Cloudflare Workers: [The road to a more standards-compliant Workers API](https://blog.cloudflare.com/standards-compliant-workers-api/),
  [Import and export data · D1](https://developers.cloudflare.com/d1/best-practices/import-export-data/),
  [Workers Pricing](https://developers.cloudflare.com/workers/platform/pricing/),
  [Workers KV — free to try, with increased limits!](https://blog.cloudflare.com/workers-kv-free-tier/),
  project analysis [`python/05-cloudflare-worker-proxy.md`](python/05-cloudflare-worker-proxy.md),
  full-export request for KV — [issue #1957, cloudflare/wrangler](https://github.com/cloudflare/wrangler/issues/1957),
  DNS limit for free zones — [third-party review](https://eastondev.com/blog/en/posts/dev/20260526-cloudflare-free-limits/).
- App Store Connect: [Protecting user privacy in report data](https://developer.apple.com/documentation/analytics-reports/privacy),
  [Analytics reports API — overview](https://developer.apple.com/help/app-store-connect-analytics/overview/analytics-reports-api/),
  [Analytics dashboard — overview](https://developer.apple.com/help/app-store-connect-analytics/overview/analytics-dashboard/),
  announcement of the update and Trends retirement — [macstories.net](https://www.macstories.net/news/apple-overhauls-app-store-connect/), [daringfireball.net](https://daringfireball.net/linked/2026/03/25/improved-analytics-in-app-store-connect), [macrumors.com](https://www.macrumors.com/2026/03/25/app-store-connect-receives-new-metrics/).
- Anthropic API: [Rate limits — Spend limits](https://platform.claude.com/docs/en/api/rate-limits),
  [Manage usage credits for paid Claude plans](https://support.claude.com/en/articles/12429409-manage-usage-credits-for-paid-claude-plans),
  project analysis [`vision-model/01-traits-strict-json.md`](vision-model/01-traits-strict-json.md) and [`python/05-cloudflare-worker-proxy.md`](python/05-cloudflare-worker-proxy.md).
- Known cases of cutbacks: [Removal of Heroku Free Product Plans FAQ](https://help.heroku.com/RSBRUH58/removal-of-heroku-free-product-plans-faq),
  [Heroku changelog #2461](https://devcenter.heroku.com/changelog-items/2461),
  [Announcing new access tiers for the Twitter API](https://devcommunity.x.com/t/announcing-new-access-tiers-for-the-twitter-api/188728),
  [Unity is Canceling the Runtime Fee](https://unity.com/blog/unity-is-canceling-the-runtime-fee),
  [Unity apologizes, makes controversial new game... (Axios)](https://www.axios.com/2023/09/22/unity-apologizes-runtime-fees).
