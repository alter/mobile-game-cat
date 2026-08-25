# Where to host the proxy/worker for a mobile game for free (checked against official pages, 2026-08-24)

Verification date: 2026-08-24. All figures below are taken from official pricing/documentation pages via a direct request to the site (WebFetch/curl), with quotes. Where the official page could not be retrieved or a figure isn't stated directly, "no data found" is written — instead of a guess.

Task context: one HTTP POST handler, accepts a cat snapshot in base64 (up to 512×512, up to 200 KB), calls Anthropic Claude with vision (the key must live on the proxy/worker, not the device), returns ~100 bytes of JSON, stores nothing. Load — a few hundred calls over the entire verification period, peak — tens per day. A second, similar handler is possible for receiving game events with a write to simple storage. Budget — zero, GCP excluded.

## Summary

1. **Cloudflare Workers** — the best-fitting option: 100,000 requests/day for free, no card required, the worker doesn't "go to sleep" (it's not a container but a V8 isolate, starts up within milliseconds), outbound HTTPS requests to api.anthropic.com are allowed out of the box via `fetch()`.
2. But **Cloudflare's Python Workers are an open beta** (`python_workers` — a compatibility flag), production readiness is not officially claimed. For reliability, the handler itself is better written in JavaScript/TypeScript, even if the rest of the project is in Python.
3. Cloudflare has free storage: **KV** (100,000 reads/day, 1,000 writes/day, 1 GB) and **D1** (5M rows read/day, 100,000 rows written/day, 5 GB) — suits a second handler for game events.
4. **Fly.io has lost its free tier** — a card is mandatory for any organization, the smallest working machine costs ~$2/month.
5. **Render** is free without a card, but the web service goes to sleep after 15 minutes of idleness and "wakes up" in about a minute — for a player waiting for a result on the photo screen, this is bad on the first call after a pause. Render's free Postgres database expires after 30 days.
6. **Railway** — not a permanently free plan but a 30-day trial with $5 of credit (no card needed); after that — paid.
7. **Hugging Face Spaces**: free hardware (CPU Basic) exists, but since 2026 creating a Docker/Gradio Space for a personal account requires the paid PRO plan ($9/month) — only static sites and up to 2 Gradio apps on ZeroGPU are free (aimed at GPU inference, not proxying to an external API).
8. **Oracle Cloud Always Free** truly gives 2 ARM OCPUs + 12 GB RAM + 200 GB disk forever, but requires a card for identity verification at signup, and accounts idle for 30+ days may officially be deemed abandoned and suspended.
9. **PythonAnywhere**: the free (Beginner) account doesn't "sleep," and `api.anthropic.com` is already on its whitelist of allowed external addresses — meaning calling Anthropic is possible. The limit is 100 seconds of CPU time per day; for our volumes (tens of light requests per day) this may be enough, but background/always-on tasks are unavailable on the free plan.
10. For comparison: the cheapest "regular" VPS — Netcup VPS 500 G12 at **€5.91/month** (4 GB RAM, 128 GB NVMe); Hetzner's cheap "Cost-Optimized" line is currently unavailable to order, the current minimum is CPX12 from **€11.49/month** (1 vCPU, per data from Hetzner's public pricing JSON).

## Summary table

| Service | Free limit | Card required | Sleeps | Fits us |
|---|---|---|---|---|
| Cloudflare Workers | 100,000 requests/day, 10ms CPU/request | No | No (edge isolate) | Yes — main candidate (write in JS/TS) |
| Cloudflare KV / D1 | KV: 100k reads + 1k writes/day, 1 GB; D1: 5M reads + 100k writes/day, 5 GB | No | — | Yes, for the second handler (events) |
| Deno Deploy | 1M requests/month, 15h CPU/month, 20 GB traffic/month, 1 GiB KV | No data found | No data found (probably not, these are edge functions) | Possible as a backup option (JS/TS only) |
| Fly.io | No free tier | Yes, mandatory | Not a "sleeping" service — a machine must be explicitly stopped | No — not free from the first hour |
| Render | 750 instance-hours/month | No (without a card — simply disabled on overage) | Yes, after 15 min idle, wake-up ~1 min | Partially — bad for a player's "cold" first request |
| Railway | $5 credit for 30 days (trial) | No | No data found | No — not permanently free |
| Koyeb | Documentation states a free instance of 512 MB/0.1 vCPU/2 GB SSD | No data found | No data found | Uncertain — conflicting data on the site |
| Vercel (Hobby) | 1M function invocations/month, 1M edge requests/month | No data found | Serverless functions, not a container — no "sleep" like Render | No — the plan is explicitly restricted to non-commercial personal use |
| Hugging Face Spaces | CPU Basic is free, but a Docker/Gradio Space requires PRO $9/month for a personal account | No data found | Yes, "goes to sleep" when idle (exact time not stated) | No for a plain FastAPI handler without a paid plan |
| Oracle Cloud Always Free | 2 OCPU + 12 GB (ARM Ampere), 200 GB disk — forever | Yes, for identity verification | No, but the account may be deemed abandoned after 30+ days idle | Yes, but heavyweight for such a simple task (your own VPS, your own web server) |
| PythonAnywhere | 100 sec CPU/day, access to external sites only via whitelist (api.anthropic.com is on it) | No data found | No (not a container, the web app is always "there," but there's a daily CPU-second limit) | Yes, as a pure-Python option |
| Hetzner Cloud (for comparison, not free) | — | — | — | From €11.49/month (CPX12, 1 vCPU) |
| Netcup (for comparison, not free) | — | — | — | From €5.91/month (VPS 500 G12, 4 GB RAM, 128 GB NVMe) |

## 1. Cloudflare Workers

Source: `developers.cloudflare.com/workers/platform/pricing/`, `.../workers/platform/limits/`, `.../workers/languages/python/`, `.../workers/runtime-apis/fetch/`, `.../kv/platform/limits/`, `.../d1/platform/limits/`, `.../d1/platform/pricing/`, `cloudflare.com/plans/`.

- **Requests**: "100,000 per day" on the free plan.
- **CPU time**: "10 milliseconds of CPU time per invocation".
- **Exceeding the limit**: "If you exceed any one of these limits, further operations of that type will fail with an error" — the request is simply rejected with an error, no auto-charges result on the free plan.
- **Card**: the cloudflare.com/plans marketing page states directly: "Start building for free — no credit card required".
- **Python Workers**: the documentation page states directly "Python Workers are in beta", requiring the `python_workers` compatibility flag. There is no explicit confirmation of production readiness in the documentation — this is an open beta, not GA. Support for FastAPI, Pydantic, and access to KV/D1/R2/Workers AI via bindings is claimed, but for a reliability-critical production handler it's safer to take a JavaScript/TypeScript Worker (no maturity limitations there).
- **Request body size**: up to 100 MB (depends on the account's plan, not the Workers plan) — a huge margin for a 200 KB base64 cat snapshot.
- **Worker size itself**: 3 MB after gzip, 64 MB uncompressed.
- **Subrequests**: 50 outbound `fetch()` calls per request — more than enough for a single call to Anthropic.
- **Memory**: 128 MB per isolate.
- **Outbound HTTPS requests to a third-party API**: confirmed — `fetch()` in the Workers Runtime API is explicitly intended for "asynchronously fetching resources via HTTP requests inside of a Worker", there is no restriction specifically on the destination domain in the documentation.
- **KV** (free tier): 100,000 reads/day, 1,000 writes/day to different keys (the same key — no more than once per second), 1 GB storage per account and per namespace, value size up to 25 MiB.
- **D1** (free tier): 5M rows read per day, 100,000 rows written per day, up to 10 databases per account, up to 500 MB per database, 5 GB total storage, 50 subrequests per Worker invocation, 7-day Time Travel history. Limits reset at midnight UTC.

Bottom line: the best free option for a lightweight proxy handler — provided the code is written in JS/TS, not Python (which is still in beta).

## 2. Deno Deploy

Source: `deno.com/deploy/pricing`.

- Free plan: "1M" requests per month, "15h" of CPU time per month, "20GB" of outbound traffic per month, "1GiB" of KV storage.
- Card requirement at signup — no data found on the pricing page.
- Idle behavior (cold start/"sleep") — no explicit description found on the pages checked (`deploy/pricing`, `deploy/manual/regions`); this is a serverless edge platform, which usually means there's no "sleeping container" the way Render has one, but no official confirmation of first-request latency was found.
- Only works with JavaScript/TypeScript — for our Python project it only fits as a separately written thin proxy, not in Python.

## 3. Fly.io

Source: `fly.io/docs/about/pricing/`.

- No free tier in 2026: "All organizations (except for Linked Organizations) require a credit card on file".
- The smallest working machine — shared-cpu-1x with 256 MB RAM: "$0.0028/hour" (about $2.02/month in Ashburn; from $1.94 to $3.14 in other regions).
- A stopped (non-running) machine is still billed, just for storage: "$0.15/GB per month of provisioned capacity" for volumes.
- The genuinely free items on the pricing page are only the first 10 SSL certificates per host and the first 10 GB of volume snapshots per month; compute and traffic are never free.
- Bottom line: doesn't fit at zero budget — money is needed from the first hour, and a card is mandatory.

## 4. Render

Source: `render.com/docs/free`.

- The web service goes to sleep when idle: "Render spins down a Free web service that goes 15 minutes without receiving any inbound traffic".
- Wake-up delay: "This process takes about one minute. Render displays a loading page to connecting browsers while a service is spinning up" — meaning a player who takes a photo after a pause of more than 15 minutes will wait up to a minute, and during that time will see Render's loading page instead of JSON from our API. This is critical for the "player waits for a response on the photo screen" scenario.
- Free hours: "Render grants 750 Free instance hours to each workspace per calendar month".
- Card: there's no explicit card requirement for signup stated on the page. On overage of traffic/build minutes without a card attached — "Render instead suspends all of your Free services for the remainder of the month" (simply disabled, there can be no auto-charges without a card).
- The filesystem is ephemeral — "ephemeral filesystem", local changes are lost on restart/redeploy.
- Free Postgres database: "Free Render Postgres databases expire 30 days after creation" — meaning Render's free Postgres doesn't fit long-term storage of game events without an upgrade.

## 5. Railway

Source: `railway.com/pricing`, `docs.railway.com/reference/pricing`.

- This is not a permanently free plan but a trial period: "Free Trial — $5 in credits for 30 days to try Railway".
- Card: "No credit card required" for the trial period.
- What happens after the trial — not explicitly described on the pages checked; based on the general pricing structure, the paid Hobby plan follows. For a task with a limited verification period this may be enough, but it's not "free forever".
- "Sleep" behavior — no data found.

## 6. Koyeb

Source: `koyeb.com/pricing`, `koyeb.com/docs`.

- The public pricing page (`/pricing`) shows only the paid plans Pro ($29/month), Scale ($299/month), Enterprise, and mentions only a "Free 5h" for Postgres (0.25 vCPU, 1 GB).
- At the same time, the documentation page (`/docs`, the app deployment section) contains the phrase: "Start with a `free` Instance: 512MB of RAM, 0.1 vCPU, and 2GB of SSD" — meaning somewhere in the product there apparently is a free permanent instance for services.
- This contradiction could not be resolved within this check: the individual pages about free-instance limits and terms (`/docs/reference/free-instances`, `/docs/reference/plans`, `/docs/pricing-details`) return 404.
- Card requirement and "sleep" behavior (scale-to-zero) for the free instance — no data found on the available official pages.
- Bottom line: Koyeb can be neither confidently recommended nor confidently ruled out — it needs a separate check via an actual signup if taken seriously.

## 7. Vercel

Source: `vercel.com/pricing`, `vercel.com/docs/plans/hobby`.

- Hobby plan (free): 1,000,000 function invocations/month, 4 CPU-hours of active compute/month, 360 GB-hours of memory/month, up to 1,000,000 edge requests/month, 10 GB traffic/month, maximum function duration — 300 seconds.
- Card requirement — no data found on the pages checked.
- Key restriction: "the Hobby plan restricts users to non-commercial, personal use only" (per the fair use guidelines). A mobile game, even at the testing stage, usually doesn't fall under "personal non-commercial use" — this makes Vercel Hobby a legally risky choice for this task, not just a technically limited one.
- On exceeding Hobby limits: "in most cases, if you exceed your usage limits on the Hobby plan, you will have to wait until 30 days have passed before you can use the feature again" — meaning just a pause on the feature, not a bill.

## 8. Hugging Face Spaces

Source: `huggingface.co/docs/hub/spaces-overview`, `huggingface.co/pricing`.

- CPU Basic hardware (2 vCPU, 16 GB RAM, 50 GB non-persistent disk) is formally free, but with an important caveat right in the documentation: "Static Spaces are free for everyone. Gradio and Docker Spaces run on compute and require a paid plan to create: PRO for personal accounts, Team or Enterprise for organizations. Free personal accounts in good standing can still host up to 2 Gradio Spaces running on ZeroGPU".
- In other words, **a regular Docker container with FastAPI cannot be created on a free personal account** — that requires the PRO plan at "$9 /month". Only static Spaces and (in a limited quantity) Gradio apps on ZeroGPU are available for free — and ZeroGPU is built for queued GPU inference, not a simple proxy to an external HTTP API.
- "Lifecycle management": "On free hardware, your Space will "go to sleep" and stop executing after a period of time if unused" — going to sleep is confirmed, but the exact idle time before sleep isn't stated in the documentation.
- Card requirement — no data found.
- Bottom line: doesn't fit the task without paying $9/month, if done via a Docker/FastAPI Space, as originally planned.

## 9. Oracle Cloud Always Free

Source: `docs.oracle.com/en-us/iaas/Content/FreeTier/freetier_topic-Always_Free_Resources.htm`, `oracle.com/cloud/free/`.

- Compute: "All tenancies get the first 1,500 OCPU hours and 9,000 GB hours per month for free for VM instances using the VM.Standard.A1.Flex shape... For Always Free tenancies, this is equivalent to 2 OCPUs and 12 GB of memory" — you can run one instance with 2 OCPU/12 GB or two with 1 OCPU/6 GB each, and this is forever, not a trial period.
- Storage: "All tenancies receive a total of 200 GB of Block Volume storage, and five volume backups included in the Always Free resources".
- A card is mandatory, and this is explicitly explained in the FAQ: "Why do I need to provide credit or debit card information when I sign up for Oracle Cloud Free Tier? ... we need to ensure that you are who you say you are. We use your contact information and credit/debit card information for account setup and identity verification. Oracle may periodically check the validity of your card, resulting in a temporary "authorization" hold... [it does] not result in actual charges to your account".
- The risk for an idle account is also explicitly described: "Accounts left idle for 30 days or more may be deemed abandoned and become eligible for suspension or termination".
- Reliability based on user reviews could not be checked within this session (the search query limit was exhausted) — it can only be assessed from the officially documented policy above, without referencing forums.
- Bottom line: the only option on the list with a permanently free full-fledged server (2 vCPU, 12 GB RAM) — but this is already a genuine VPS, on which you'll have to stand up your own web server, TLS, systemd/docker, and watch for account idleness yourself. For a single HTTP handler serving a few hundred requests — excessively heavyweight, but useful if the project outgrows a "toy" volume.

## 10. PythonAnywhere

Source: `pythonanywhere.com/pricing/`, `pythonanywhere.com/whitelist/`.

- Free (Beginner) plan: "100 seconds" of CPU time per day, 512 MB disk, 1 web app at `<name>.pythonanywhere.com`, up to 2 consoles, no SSH, no MySQL, no scheduled tasks, and no "always-on tasks" (marked with an X in the plans table).
- Outbound network requests on the free plan are restricted to a domain whitelist: "Specific sites via HTTP(S) only". Checking the list (`pythonanywhere.com/whitelist/`) showed that **`api.anthropic.com` is present on this list** — meaning calling Anthropic from a free account is possible.
- Card requirement for signup — no data found on the pages checked.
- The web app on the free plan doesn't "sleep" the way Render does (it's not an on-demand container but a permanently mounted WSGI app behind PythonAnywhere's proxy), but the 100-second daily CPU-time limit is specifically processor time, not network wait time, so waiting for Anthropic's HTTP response most likely doesn't burn through this quota as fast as computation would. Exact data on whether network wait counts toward CPU-seconds on PythonAnywhere could not be officially found.
- Bottom line: the only checked service that simultaneously (a) is explicitly written for Python/Flask/Django, (b) has no "wake-up" problem, (c) officially allows calling `api.anthropic.com` from a free account. The main risk is staying within 100 seconds of CPU time per day under tens of peak-load requests.

## The cheapest regular VPS (a baseline)

Source: `hetzner.com/cloud/` (data obtained from the public pricing JSON file `www.hetzner.com/_resources/app/data/bench/cloud_data.json`, which the page itself uses), `netcup.com/en/server/vps`.

- **Hetzner Cloud**: the "Cost-Optimized" line (historically the cheapest) is explicitly marked on the page as "currently unavailable" — not orderable right now. The current minimum within the "Shared - Regular Performance" line is the **CPX12** plan (1 virtual AMD core) at **€11.49/month** in European data centers (Nuremberg/Falkenstein/Helsinki), according to the official pricing JSON that the hetzner.com page itself loads to render its pricing table. RAM/disk figures for this plan could not be obtained from the official page (the specs table is rendered via JavaScript, the static JSON only has price and core count).
- **Netcup**: the cheapest VPS — **VPS 500 G12** at **€5.91/month** (VAT 19% included), 4 GB DDR5 RAM (ECC), 128 GB NVMe.
- Bottom line: "not cutting corners" costs from **€5.91/month** (Netcup) — that's the price of the question, if you decide not to spend time fitting into someone else's free limit.

## Can you do without your own node entirely

Source: `developers.cloudflare.com/ai-gateway/`, `developers.cloudflare.com/ai-gateway/configuration/authentication/`.

The only way verified against official documentation to avoid writing your own handler is **Cloudflare AI Gateway**.

- What it is: "Observe and control your AI applications with analytics, caching, rate limiting, and model fallback through AI Gateway" — a proxy layer in front of AI providers.
- Anthropic support is confirmed: the documentation lists supported providers, including "OpenAI, Anthropic, Google, and more".
- The key-hiding mechanism is called **BYOK (Bring Your Own Keys)**: the actual Anthropic key is stored on Cloudflare's side ("configured with stored provider keys through Bring Your Own Keys (BYOK)"), and the device calls the Gateway with a separate Cloudflare token instead of the Anthropic key. This does genuinely mean you don't need to write and deploy your own handler code — just configure the Gateway via the dashboard/API.
- An important caveat from the same security documentation: "Any token with AI Gateway Run can send requests through every gateway in the account, including any configured with stored provider keys through BYOK, consuming those credentials" — meaning the token that will have to be baked into the app somehow (or obtained dynamically) allows, if leaked, spending the attached Anthropic key through any Gateway in the account. A risk of the same class as leaking a key from a homemade node — just one level of indirection further out. Safe use still generally requires your own minimal code that authenticates specifically your mobile device/session before handing it the Gateway token, rather than embedding a static token in the APK/IPA.
- AI Gateway cost and limits: the documentation only answers "Available on all plans" — meaning it's available on Cloudflare's free plan too, but exact quantitative limits (requests per day, etc.) aren't given on the pages checked — no data found.
- Other "provider intermediaries" (third-party aggregators like OpenRouter, etc.) could not be examined against official pages within this check — the session's search query limit was exhausted before reaching them. Claiming anything about their free limits or security would be speculation, so this topic is deliberately left unaddressed here.

Section conclusion: you cannot do entirely without some server-side intermediary (even a ready-made, third-party one) — Anthropic doesn't issue limited/one-time client keys for mobile apps, so in practice you either write your own thin Worker or configure someone else's (Cloudflare AI Gateway with BYOK) and still add your own authentication on top of it. Given that writing your own Worker on Cloudflare is literally a dozen lines of code and uses the same free limit, separately configuring AI Gateway to "save on code" makes little sense: the difference isn't whether an intermediary is needed, but who hosts its routing logic.

## What to choose at zero budget

**Direct recommendation: Cloudflare Workers, a handler in JavaScript/TypeScript (not Python — that's still in beta), plus Cloudflare D1 or KV for the second handler with game events.**

Why exactly this:
- 100,000 free requests/day covers the stated load (a few hundred calls over the entire verification period, peaks of tens per day) with a huge margin.
- No card needed either for signup or for operating within the limit.
- No "wake-up" problem — unlike Render, Koyeb (presumably), and Hugging Face Spaces, Cloudflare's worker isn't a container that sleeps: it starts up on the edge within milliseconds, which is critical for the "player waits for a response on the photo screen" scenario.
- `fetch()` to `api.anthropic.com` works with no restriction beyond the general limit of 50 subrequests per invocation — more than enough for a single call to Anthropic.
- The second handler (game events) doesn't need a separate database standing up — D1 (5M rows read and 100,000 rows written per day for free) is enough, or, for very simple storage, KV.

The only real cost of this decision is writing the handler not in Python but in JS/TS, since Python Workers remains an open beta with no stated production readiness.

**If it must be written in Python** — the second-best fit is **PythonAnywhere**: the free Beginner plan doesn't "sleep" like Render, and `api.anthropic.com` is already officially listed on the whitelist of allowed external addresses. The restriction is 100 seconds of CPU time per day; under light load (tens of short requests per day, most of whose time goes to waiting for Anthropic's network response rather than processor work), the odds of staying within it are good, but exact data on whether network waiting counts against this quota could not be found — it's worth checking empirically before relying on this option.

**If the project outgrows a volume of "a few hundred calls" and needs a full, controllable server** — the next step up is **Oracle Cloud Always Free** (2 vCPU/12 GB RAM forever, but requires a card for verification and your own administration), and if even that doesn't fit — a regular VPS from **€5.91/month** (Netcup VPS 500 G12) as a plain baseline for "the cost of not cutting corners".

**Not suitable at all for this task**: Fly.io (no free tier, card mandatory from the first hour), Railway (not a permanent plan, only a 30-day trial), Vercel Hobby (explicit ban on commercial use), Hugging Face Spaces (Docker/FastAPI Space unavailable for free since 2026 without the $9/month PRO plan).

## Sources

- Cloudflare Workers pricing — https://developers.cloudflare.com/workers/platform/pricing/
- Cloudflare Workers limits — https://developers.cloudflare.com/workers/platform/limits/
- Cloudflare Python Workers — https://developers.cloudflare.com/workers/languages/python/
- Cloudflare Workers fetch API — https://developers.cloudflare.com/workers/runtime-apis/fetch/
- Cloudflare Workers KV limits — https://developers.cloudflare.com/kv/platform/limits/
- Cloudflare D1 limits — https://developers.cloudflare.com/d1/platform/limits/
- Cloudflare D1 pricing — https://developers.cloudflare.com/d1/platform/pricing/
- Cloudflare plans (no card needed) — https://www.cloudflare.com/plans/
- Cloudflare AI Gateway — https://developers.cloudflare.com/ai-gateway/
- Cloudflare AI Gateway authentication (BYOK) — https://developers.cloudflare.com/ai-gateway/configuration/authentication/
- Deno Deploy pricing — https://deno.com/deploy/pricing
- Fly.io pricing — https://fly.io/docs/about/pricing/
- Render free plan docs — https://render.com/docs/free
- Railway pricing — https://railway.com/pricing
- Railway pricing reference — https://docs.railway.com/reference/pricing
- Koyeb pricing — https://www.koyeb.com/pricing
- Koyeb docs — https://www.koyeb.com/docs
- Vercel pricing — https://vercel.com/pricing
- Vercel Hobby plan docs — https://vercel.com/docs/plans/hobby
- Hugging Face Spaces overview — https://huggingface.co/docs/hub/spaces-overview
- Hugging Face pricing — https://huggingface.co/pricing
- Oracle Cloud Always Free resources — https://docs.oracle.com/en-us/iaas/Content/FreeTier/freetier_topic-Always_Free_Resources.htm
- Oracle Cloud Free Tier (FAQ on card, account idleness) — https://www.oracle.com/cloud/free/
- PythonAnywhere pricing — https://www.pythonanywhere.com/pricing/
- PythonAnywhere whitelist — https://www.pythonanywhere.com/whitelist/
- Hetzner Cloud — https://www.hetzner.com/cloud/ (prices checked against the public JSON https://www.hetzner.com/_resources/app/data/bench/cloud_data.json, which this page itself loads)
- Netcup VPS — https://www.netcup.com/en/server/vps


