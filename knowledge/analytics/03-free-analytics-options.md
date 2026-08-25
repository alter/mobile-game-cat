# Free analytics tools for testing an idea (Unity 6.3, iOS)

Date: 2026-08-24

Task: about 500 installs, 9 simple events in the game, zero cloud budget. Checked against the official pricing pages of each tool (see the "Sources" section).

## Summary

- **GameAnalytics** — genuinely free for this volume: no MAU cap, card not required, has custom events, funnels and retention. Best default option.
- **Firebase Analytics (Google Analytics for Firebase)** — also genuinely free: the Spark tier does not require a card and does not require enabling the paid Blaze tier (Blaze is needed only for other Google Cloud services, not for the analytics itself).
- The "500 events" cap on Firebase/GA4 is real, but it is 500 distinct event **types** **per app instance** (in practice, per device), not an overall ceiling for the whole project — with 9 events it is unreachable.
- **Unity Gaming Services Analytics** is paid per-user starting once you exceed the free cap, but the cap itself is generous: 50,000 monthly active users and 500 custom events per user free, no card needed while you stay within the cap; when it is exceeded, the service does not bill silently but first asks you to attach a card.
- **AppsFlyer, Singular, Tenjin** — all three have a free tier with no card, but they are ad-attribution tools (they count "conversions" — installs from paid advertising), not systems for collecting arbitrary in-game events. For an organic test with no ad budget they are not needed and do not solve the task.
- **Adjust** — the official pricing page could not be opened (the site returns bot protection on every attempt); no open data on a free tier was found.
- **App Store Connect Analytics** already gives, free, without a single line of code, page views, installs, view-to-install conversion, and **retention by day (Day 1, Day 7, Day 30)** — that is, it fully and freely covers measure #3 (first-day return). It also lets you compare different page versions via Custom Product Pages and Product Page Optimization.
- App Store Connect **cannot see** events inside the game itself (the cat-photo screen, uploading a snapshot, tapping "+5 moves") — only an in-app SDK (GameAnalytics or Firebase) can collect that.
- Among open self-hosted solutions, the only truly free option is **self-hosting** (Countly Lite, Matomo On-Premise/Community) — but that means your own server, not zero-hassle cloud. The cloud versions of PostHog, Countly (Flex) and Matomo are paid or free-with-time-limits.
- **PostHog Cloud** formally gives 1 million events a month free with no card, but it is a general-purpose product analytics tool, not game analytics; it has no out-of-the-box support for retention in a mobile-game context or convenient game dashboards.

## Summary table

| Tool | Free cap | Card required | What it provides |
|---|---|---|---|
| GameAnalytics | No MAU cap | No | custom events, funnels, retention, cohorts, dashboards |
| Firebase Analytics | Effectively unlimited (500 event types per app instance) | No (Spark tier) | custom events, console reports, export to BigQuery — needs separate verification |
| Unity Gaming Services Analytics | 50,000 MAU/month, 500 events per user | No, while within the cap | custom events, Remote Config and A/B testing always free |
| AppsFlyer | 12,000 conversions in the first 12 months (Zero plan) | Not stated on the page | ad attribution, not game events |
| Singular | 15,000 paid conversions (Free plan) | No | ad attribution, monetization, anti-fraud |
| Tenjin | 2000 conversions/month free forever, organic unlimited | No | attribution, ad LTV, cohort retention reports |
| Adjust | no data found (pricing page unreachable) | no data found | no data found |
| App Store Connect Analytics | Unlimited, part of the Apple Developer Program | No (only the $99 annual developer program fee is required) | impressions, installs, conversion, Day 1/7/30 retention, page A/B tests |
| PostHog Cloud | 1M events/month | No | general product analytics, not game-specific |
| Countly (Flex cloud) | 14-day trial, then from $175/month | Not stated on the page | full set of game/product analytics |
| Countly Lite | Unlimited (self-hosted) | No | same functionality, but requires your own hosting |
| Matomo Cloud | Trial only | No, for the trial | web/product analytics |
| Matomo On-Premise (Community) | Unlimited (self-hosted) | No | same functionality, requires your own hosting |

## GameAnalytics

Source: official pricing page gameanalytics.com/pricing/.

**Free plan:**
- MAU cap: **no cap** — the pricing page literally states "No MAU cap".
- Credit card: **not required** — "No credit card required".
- What happens if you exceed it: there is nothing to exceed, there is no volume cap on the free plan; the pricing page does not describe any cutoff scenario.
- Features on the free plan: custom event tracking, funnel analysis, retention, cohort grouping, real-time monitoring, ready-made dashboards and KPIs, offline event sync, live event debugging (last 50 events), a limited AI agent (3 messages a day per user, or 6 per organization).
- Unity support: the SDK is distributed as a Unity Package Manager package. The official SDK installation docs do not explicitly name Unity 6.3 as separately supported or unsupported — no specific engine-version restrictions were found on the pages checked.

**Paid tiers (all are add-ons on top of the free plan — the analytics itself stays free):**
- AnalyticsIQ Pro — $49/month (deeper player-behavior analytics, extended AI agent).
- SegmentIQ Pro — price on request ("Let's talk"), no-SQL user-segment analysis.
- PipelineIQ — from $499/month, data warehouse, advanced pipelines and API.
- MarketIQ — $499/month (10 seats), real-time ad-performance analytics.

**Conclusion for the task:** for 500 installs and 9 events, GameAnalytics fully covers the task with no hidden conditions whatsoever — the verified official pricing page shows neither a volume cap nor a card requirement.

## Firebase Analytics (Google Analytics for Firebase)

Sources: firebase.google.com/pricing, firebase.google.com/products/analytics, support.google.com/analytics/answer/9267744 (GA4 event-collection limits).

**Spark tier (free):**
- "Generous no-cost usage limits" and literally "No payment method needed" — no credit card required.
- Google Analytics for Firebase is on the list of products marked "No-cost" and is fully available on the Spark tier.
- The product page separately states: "There's no cost to using Google Analytics" — the analytics itself is free regardless of the project's tier.

**Is the Blaze plan required (and, with it, a GCP billing account)?**
No. The analytics works on the free Spark tier without upgrading to Blaze. Blaze is a paid, pay-as-you-go tier, and a linked Google Cloud billing account is required specifically for it, but not for using Firebase Analytics as such. Important for the project owner: merely creating a Firebase project does not obligate you to enable a GCP billing account, as long as you only use Analytics (and other products on the "No-cost" list).

**The 500-event-types cap — confirmed, but with a clarification.**
The official Google Analytics limits page (support.google.com/analytics/answer/9267744) gives the exact wording: "There is not limit on the number of distinctly named events for web data streams. 500 per app user (for app data streams)." That is, the cap of 500 distinct event names applies **per app instance (essentially per device/user)**, not as an overall ceiling on the number of event types across the whole project — "You might see more than 500 distinctly named events if users on different app instances trigger different events." With 9 events in the game, this cap is irrelevant.

**Other official collection limits (same page):**
- Up to 25 parameters per event, up to 40 characters in a parameter name, up to 100 characters in a value.
- Up to 25 user properties, up to 24 characters in the name, up to 36 in the value.
- Up to 100,000 events per user per day, up to 2000 sessions per user per day.
- Automatically collected events do not count toward these limits.

**Caveat:** the product page does not directly clarify whether the Blaze plan is required to export raw data to BigQuery — if such an export is needed in the future, this should be verified separately before relying on it. Built-in reports in the Firebase console (by events, by audiences) are included in the free tier.

**Conclusion for the task:** Firebase Analytics is genuinely free for 500 installs and 9 events, no card is needed, and linking a Google Cloud billing account is not required as long as only the analytics itself is used.

## Unity Analytics / Unity Gaming Services

Sources: unity.com/gaming-services, unity.com/products/gaming-services/pricing, support.unity.com — the "Billing FAQ" article.

The general Unity Editor page (unity.com/pricing) describes only licenses for the engine itself (Personal/Pro/Enterprise) and does not mention Analytics — it is a separate product within Unity Gaming Services (UGS), with its own pricing page: unity.com/products/gaming-services/pricing.

**The "Analytics & Player Engagement" section on the official UGS pricing page (data current as of 2026, per the page footer):**
- Active Users / Month (Analytics): **50,000 monthly active users free**, **500 custom events per active user** free, 0.05 queries-per-second per user free.
- Above 50,000 MAU — pay-as-you-go: $0.00360 per user, above 150,000 MAU — $0.00315, above 500,000 MAU — $0.00293, above 1,000,000 MAU — $0.00225.
- Remote Config — free with no limits stated on the page.
- A/B Testing (via Game Overrides) — free with no limits stated on the page.

**Is a credit card needed, and what happens on exceeding the limit.**
The official Billing FAQ article (support.unity.com) answers directly: "you do not need to add a payment method if you remain in these free tiers" — no card needed while you stay within the free caps. On exceeding it: "Once you exceed the free tier limitations you will then be asked to add a payment method in order to continue using the services" — the service does not bill automatically and unnoticed, but first asks you to attach a card, so there is no surprise charge.

**Is a Unity Pro subscription needed.**
On the UGS pricing page, a requirement for a Unity Pro/Personal subscription is explicitly stated only for one line — "Cloud Diagnostics: Included with Unity Personal, Pro, and Enterprise." There is no such caveat for the Analytics line: Analytics pricing and its free cap are not tied to the engine license tier.

**Conclusion for the task:** 500 installs and 9 events are two orders of magnitude below the free cap of 50,000 MAU and 500 events per user. Unity Gaming Services Analytics covers the task for free, no card required, no Unity Pro subscription required.

## AppsFlyer, Adjust, Singular, Tenjin

An important caveat before the numbers: all four services are mobile measurement partner (MMP) platforms — their core job is to track which ad campaign an install came from and to calculate ad ROI. Their unit of measure for free caps is not an "event" or a "user" but a **conversion** (usually an install credited as coming from a paid ad link). Organic installs (via a direct link, word of mouth, testers) generally do not count toward this cap and are often separately marked as unlimited. For testing an idea on 500 installs with no ad budget, the cap itself will almost never be hit, but the usefulness of such a tool for three of the four needed measures (share reaching the screen, share uploading a snapshot, tapping "+5 moves") is limited — MMPs are not built for a flexible in-game event funnel the way game or product analytics are.

**AppsFlyer** (source: appsflyer.com/pricing/) — the **Zero** plan: 12,000 free conversions during the first 12 months, aimed at apps using only their own promotion channels (no paid advertising), includes a 30-day trial of paid features. A credit-card requirement is not explicitly stated on the pricing page. What happens once the limit is used up — for the Zero plan the page describes moving to pay-as-you-go ($0.07 per conversion on the Growth plan) or switching to the paid Growth plan.

**Adjust** — the official pricing page (adjust.com/pricing/) could not be opened: the site returns bot protection on every access attempt (including via intermediary services). No open data on Adjust's free tier was found — this needs manual verification, in a browser bypassing the protection, or directly with the vendor.

**Singular** (source: singular.net/pricing/) — the **Free** plan: "$0 / conversion", includes 15,000 paid conversions, a credit card is explicitly stated as not required ("No credit card required"). The free plan includes the full set: mobile attribution, ad-monetization analytics, ROI calculation, cost aggregation, fraud protection, email/SMS/social-channel tracking, deep linking, SKAdNetwork attribution, API access.

**Tenjin** (source: tenjin.com/pricing/) — the **All-Inclusive Free** plan: 2000 free conversions a month "forever" (Free Forever), no card required ("no credit card required"), beyond the limit — $0.04 per extra conversion on monthly postpaid billing, with no annual commitment. The plan comparison table separately states "Unlimited Organic Installs" — organic installs are not counted as conversions and are unlimited even on the free plan. The free plan also includes: cost aggregation, ad-value analytics (LTV), cohort and retention reports, the full SKAdNetwork set — free for all Tenjin customers regardless of plan.

## App Store Connect Analytics (with zero lines of code)

Source: developer.apple.com/app-store-connect/analytics/.

App Store Connect Analytics is part of the Apple Developer Program (the $99 annual fee — this fee is mandatory for publishing any app to the App Store at all, it is not an extra charge for the analytics) and provides the following metrics for free, with no SDK integration whatsoever:

**User Acquisition:**
- Total Downloads — total number of downloads (first-time + redownloads).
- Unique Impressions — unique views of the app's App Store page.
- Conversion Rate — the ratio of downloads to impressions.
- Download Sources — sources of downloads (App Store search, link referral, referrer, etc.).
- Campaign Performance — data for tagged marketing links.

**User Engagement:**
- Active Devices, Sessions.
- **Retention Rate — the percentage of devices on which the app has been used on subsequent days after download**, officially: "the percentage of devices on which your app or game has been used in the days following a download." The report is broken down by day (Day 1, Day 7, Day 30 and other points).

**Monetization:** revenue (Proceeds), number of paying users, sales, revenue per paying user.

**Comparing app page variants.** A built-in **Product Page Optimization** tool is available — testing different versions of the page (icon, screenshots, video preview) with effectiveness scoring. **Custom Product Pages** are separately available — alternate versions of the product page (for example, for different ad creatives), with each version showing its own conversion stats, letting you compare creatives against each other.

**Limits:** no explicit data-volume limits are described on the page — the service is free for all developer-program members. Two caveats: the data depends on users consenting to share diagnostics; some breakdowns (referrers, campaigns) are hidden at low data volumes for privacy reasons — this may be noticeable on a sample of around 500 installs.

**Conclusion for the task.** App Store Connect Analytics requires no coding and no integration at all — it already provides the **Day 1 Retention** metric for free and without limits. This is exactly measure #3 of the four required in the task. The other three measures — share reaching the cat-photo screen, share uploading a snapshot, share tapping "+5 moves" — are events inside the game itself, which Apple cannot see; for those you need an SDK such as GameAnalytics or Firebase Analytics.

## Open self-hosted solutions (PostHog, Countly, Matomo)

**PostHog** (source: posthog.com/pricing) — the cloud (hosted) option gives **1 million events a month** free, no credit card required ("No credit card required"). Session replay is separately capped — 5000 recordings a month, of which 2500 for mobile apps. On exceeding the limits, the platform moves to pay-as-you-go, but per the official wording "You still keep the same monthly free volume, even after upgrading" — that is, even after adding a card, the free volume does not disappear, and you can set your own spend cap ("set a billing limit for each product") to avoid an unexpected bill. There is no explicit description of retention features in a mobile-game context on the pricing page — PostHog is primarily general-purpose product analytics, not game analytics. For a 9-event task, this option is excessive in setup complexity.

**Countly.** Cloud and self-hosting need to be looked at separately:
- *Countly Flex* (private cloud, source: countly.com/pricing) — there is no permanent free tier, only a 14-day trial, paid plans start from $175/month. A card requirement for the trial is not explicitly described on the page.
- *Countly Lite* (source: countly.com/community-edition) — open-source software, self-hosted, officially described as "free forever". Requires your own server — that is, hosting costs fall on the project owner separately from Countly itself. Retention features and Unity SDK support are not separately detailed on the page reviewed; for exact confirmation, the SDK documentation needs to be checked.

**Matomo.** Also two different offerings:
- *Matomo Cloud* (source: matomo.org/pricing/) — there is no permanent free tier, only a trial with no card required ("a free trial is available... You don't need a credit card"), paid plans — from €29/month at 50,000 hits a month.
- *Matomo On-Premise (Community Edition)* — confirmed as free and open-source forever, with no limit on the number of users or hits. As with Countly, it requires your own server; the pricing page gives no details on mobile SDKs or retention specifically for games.

**Section conclusion:** none of the three solutions has a free cloud (ready-made, no-server-of-your-own) tier that is both unlimited and free of hidden conditions — it is either a trial period that expires (Countly Flex, Matomo Cloud), or a volume that is conditionally sufficient for general product analytics with no game-specific features (PostHog). The fully free option here is only self-hosting (Countly Lite, Matomo Community), which means your own server and its upkeep, not "zero hassle."

## Which of the four measures are covered by ready-made tools, and which are not

The project's four measures: (1) share reaching the cat-photo screen inside the game, (2) share uploading a snapshot, (3) first-day return, (4) share tapping the "+5 moves" button.

| # | Measure | Type | What covers it for free |
|---|---|---|---|
| 1 | Share reaching the cat-photo screen | in-game event | **Not covered by App Store Connect on its own.** An SDK sending a custom event is needed — GameAnalytics or Firebase Analytics, both free with no card at this volume. |
| 2 | Share uploading a snapshot | in-game event | Same as measure #1 — an SDK (GameAnalytics/Firebase) is needed; the event itself and the "reached screen → uploaded snapshot" funnel are built with both services' built-in funnel features. |
| 3 | First-day return | retention | **Fully covered for free and with no code — App Store Connect Analytics, the Retention Rate report, Day 1.** Duplicating it via GameAnalytics/Firebase is also possible and gives more flexible segmentation, but is not required if Apple's data is enough. |
| 4 | Share tapping "+5 moves" | in-game event | Same as measures #1 and #2 — an SDK sending a custom event on the button tap is needed. |

**Bottom line:** three of the four measures (1, 2, 4) are events inside the game, for which integrating a third-party SDK is mandatory; there are no ready-made free tools for them without installing an SDK. Measure #3 (retention) is unique in that it can be obtained fully for free and without a single line of code via App Store Connect's built-in analytics — this should be used first, and an SDK should be added only for measures 1, 2, and 4, not for retention.

## What to choose on a zero budget

**Direct recommendation: GameAnalytics for the four in-game markers, plus App Store Connect's built-in Retention Rate for retention. Firebase Analytics is an equally good fallback if a longer list of event parameters or integration with other Google services is needed.**

Reasoning:
1. GameAnalytics and Firebase Analytics are the only two verified tools whose official pricing page explicitly states "no card required" **and** whose free cap is knowingly orders of magnitude above the task's volume (500 installs, 9 events); GameAnalytics has no MAU ceiling at all, and Firebase's ceiling is so high (500 event types per device, not per whole project) that it is irrelevant to the task.
2. GameAnalytics is a specialized game tool: funnels and cohorts are already built into the dashboard, no need to configure reports yourself the way you would in Firebase or, even more so, in PostHog.
3. App Store Connect covers retention (measure #3) for free and with no code — no need to spend time setting up retention inside a third-party SDK if Apple's day-based accuracy is enough.
4. Unity Gaming Services Analytics is an equally generous free option (50,000 MAU, 500 events per user, no card needed), but its integration and dashboard are less specifically geared toward quick answers to "did the idea work" than GameAnalytics's; it can be considered a second fallback if the project is already deeply tied into other Unity Gaming Services.
5. AppsFlyer, Singular, Tenjin, Adjust do not solve the task: their free caps are counted in paid-ad conversions, not in custom in-game events; for an organic idea test with no ad money they are overkill and do not directly give a funnel for the needed four markers.
6. Self-hosted solutions (Countly Lite, Matomo Community) are technically free but require your own server and its upkeep — extra work and risk that can be avoided by taking a ready-made free cloud tier from GameAnalytics or Firebase.

**What to verify before starting:** confirm personally in the GameAnalytics and Firebase consoles at sign-up whether a payment method is actually requested (the pricing pages say "not required," but sign-up UX sometimes changes faster than the documentation) — this is the only risk of a mismatch between documented and actual behavior that cannot be closed by reading the pricing page alone.

## Sources

- GameAnalytics — pricing page: https://gameanalytics.com/pricing/
- GameAnalytics — Unity SDK integration docs: https://gameanalytics.com/docs/item/unity-sdk
- Firebase — pricing page (Spark/Blaze tiers): https://firebase.google.com/pricing
- Firebase — Google Analytics for Firebase product page: https://firebase.google.com/products/analytics
- Google Analytics 4 — official event-collection limits: https://support.google.com/analytics/answer/9267744
- Unity Gaming Services — product overview: https://unity.com/gaming-services
- Unity Gaming Services — official pricing page: https://unity.com/products/gaming-services/pricing
- Unity — Billing FAQ (when a card is needed and what happens on exceeding the limit): https://support.unity.com/hc/en-us/articles/6821475035412-Billing-FAQ
- AppsFlyer — pricing page: https://www.appsflyer.com/pricing/
- Singular — pricing page: https://www.singular.net/pricing/
- Tenjin — pricing page: https://www.tenjin.com/pricing/
- Adjust — pricing page: https://www.adjust.com/pricing/ (did not load — the site blocks automated access; data not confirmed)
- Apple — App Store Connect Analytics: https://developer.apple.com/app-store-connect/analytics/
- PostHog — pricing page: https://posthog.com/pricing
- Countly — pricing page (Flex/Enterprise cloud): https://countly.com/pricing
- Countly — Community Edition / Countly Lite page: https://countly.com/community-edition
- Matomo — pricing page (Cloud and On-Premise): https://matomo.org/pricing/

