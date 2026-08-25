# Retention/CPI benchmarks and attribution on iOS after ATT

Date collected: 2026-08-24

Context: the project has a threshold of "day-1 retention > 35%" — need to understand how realistic this is for a casual puzzle game, and how to even test the cost per install for a trailer before the game launches, on a budget of about 500 test installs.

## Summary

- Per the Adjust report "The gaming app insights report: 2025 edition" (2024 data), the average D1 retention across all mobile game genres is **27%** (down from 28% in 2023); for the leading casual genres (hybrid casual, hyper casual) it's 27–28%, but their retention collapses by day 30 to ~2% [Adjust, "The gaming app insights report: 2025 edition", 2025].
- Per the GameAnalytics report "2025 Mobile Gaming Benchmarks" (2024 data, 11,600 games, 1.48 billion MAU), the median D1 retention for the "puzzle" genre is **19.66–20.74%**, D7 is 4.27–4.79%, D28 is 1.09–1.26% [GameAnalytics, "2025 Mobile Gaming Benchmarks", 2025].
- The "> 35%" threshold is noticeably higher than both the industry average (27%) and the median puzzle-genre figure from two independent primary reports (≈20–21%). This is not a modest but an ambitious goal: in Adjust's own wording, such figures (40–50%) are seen only among "top games in the market" — meaning this is the top tier of the genre, not a typical MVP outcome [Adjust, "The gaming app insights report: 2025 edition", 2025].
- On CPI: the median CPI across all game genres in 2024 was **$0.36** (down from $0.38 in 2023), CPI in North America rose to **$1.20**, and in the US to **$1.22** [Adjust, "The gaming app insights report: 2025 edition", 2025]. Per a separate Liftoff and Singular report, "2025 Casual Gaming Apps Report" (data from February 2024 to February 2025), CPI for casual games on iOS is **$1.41**, on Android **$0.14** [Liftoff, "2025 Casual Gaming Apps Report", 2025].
- No direct, current figure for "puzzle CPI in the US" and "puzzle CPI in low-cost countries" separately was found in openly available, personally verified reports — only figures for the casual genre as a whole (Liftoff) and for all games broken down by region (Adjust). Specific numbers found in aggregator blogs, such as "puzzle iOS $3, Android $2" or "puzzle CPI on iOS is 5x higher than on Android," are not confirmed by any of the primary-verified reports and, judging by their wording, appear to be confused with casino-genre data — such figures were deliberately not included in this file.
- For testing a trailer before an iOS game's full release, Apple Search Ads (Basic, no strict minimum budget, CPI-based payment, monthly cap up to $10,000 per app) and Custom Product Pages/Product Page Optimization in App Store Connect are realistic options for comparing page versions — but both tools require an app already published on the App Store, i.e., they don't work as a "fake page" before a real listing exists [Apple Developer — Custom Product Pages](https://developer.apple.com/app-store/custom-product-pages/); [Apple Developer — Product Page Optimization](https://developer.apple.com/app-store/product-page-optimization/).
- Honest answer on attribution: for a sample of 500 test installs, neither SKAdNetwork nor AdAttributionKit is really needed — both frameworks are designed for aggregated attribution of large ad campaigns with anonymity thresholds, and at 500 installs there will be few postbacks and little use from them; for your own event pipeline, it's enough to link the `app_open` event to a source via a simple deep-link/UTM parameter at your own server level, without involving SKAdNetwork/AdAttributionKit at all.
- Custom Product Pages and Product Page Optimization in App Store Connect are suitable for comparing trailers/screenshots against each other **after** the app is published (even in a limited region), but not before — this is a tool for optimizing a live page, not an A/B test before the app itself appears in the App Store.

## 1. Industry benchmarks for retention and CPI for casual games/puzzles (2025–2026)

### Day 1 retention

The report **Adjust, "The gaming app insights report: 2025 edition"** (methodology: a blend of the top 5,000 apps and the full dataset Adjust tracks, 45 countries in detailed breakdown plus about 250 countries per the ISO 3166-1 standard, data period — January 2023 to March 2025, all amounts in USD) gives, across all game genres globally:

> "Day 1 retention rates for gaming apps globally decreased from 28% to 27% in 2024. Board and card games held steady at 22%, casino climbed from 16% to 19%, and strategy (17%) and trivia (16%) declined. Hybrid and hyper casual games maintained their lead at 28% and 27%—but despite strong early engagement from these genres, by day 30, both dropped to just 2% (vs. the overall games average of 5%)."

— [Adjust, "The gaming app insights report: 2025 edition", 2025, ebook/PDF, document mirror: investgame.net](https://investgame.net/wp-content/uploads/2025/05/gamingreport2025_ebook_en.pdf)

A more recent report in the same family, **"The gaming app insights report: 2026 edition"** (2025 data), confirms the same figure per a secondary rendering of its content — "D1 Retention across all genres was 27% in 2025" — with the caveat that top games in the market regularly exceed 40–50% D1 retention, and that comparing to the market average is "somewhat misleading," meaning Adjust itself warns against confusing the average figure with a benchmark for a successful product [retelling of the Adjust 2026 report in GameDev Reports, 2026]. It was specifically the text of the 2025 edition report (2024 data) that was opened and read directly as a PDF — the 2026 edition file itself could not be opened via WebFetch (the adjust.com site consistently returned a rate limit on every attempt during this research), so the "27% in 2025" figure is flagged as coming from a secondary retelling, not from a personally read original.

The genre-specific, personally verified benchmark for puzzle games is the report **GameAnalytics, "2025 Mobile Gaming Benchmarks"** (dataset: 11,600 game apps, 9 regions, iOS and Android, 16 genres, sample's total MAU exceeds 1.48 billion, data for calendar year 2024, retention is classic/calendar-day, not rolling):

> "Puzzle games maintained median D1 retention between 19.66% and 20.74% throughout 2024" (data extracted directly from the report page) — the same source gives D7 at 4.27–4.79%, D28 at 1.09–1.26%, which is noticeably higher than the overall market median D28 (75% of projects don't exceed 3%).

— [GameAnalytics, "2025 Mobile Gaming Benchmarks", 2025](https://www.gameanalytics.com/reports/2025-mobile-gaming-benchmarks)

**An important discrepancy worth stating explicitly.** A large number of secondary blogs and aggregators (Segwise, Business of Apps citing "Mistplay," various SEO articles about "2026 benchmarks") repeat the figure "puzzle D1 retention ≈ 31.85%," sometimes attributed to GameAnalytics. On directly opening the GameAnalytics report page, this figure **was not confirmed** — the actual value in the primary source is almost half that (19.66–20.74%). Judging by the wording of secondary sources, the 31.85% figure comes from a separate source (attribution to "Mistplay Mobile Game Retention Benchmarks" is mentioned), not from the GameAnalytics report, but the Mistplay page itself could not be opened and verified within this research (the URL was not found and was not personally read) — so the 31.85% figure is not included in this file as unverified, and only the personally read GameAnalytics report (19.66–20.74%) is used as the benchmark for puzzle games. The difference is nearly twofold, and this matters for assessing the realism of the project's internal threshold.

**Answer to the project's direct question.** The "D1 retention > 35%" threshold should be compared against two independently and personally verified primary figures: the overall average across all games — 27% (Adjust, 2024 data), the median for puzzle games — 19.66–20.74% (GameAnalytics, 2024 data). 35% falls between "the average for the best casual genres at the moment of install" (hybrid/hyper casual — 27–28% per Adjust) and "the market's top games" (40–50% per the direct Adjust quote). In other words, the 35% threshold is realistic not as a typical MVP result but as the result of a successful, polished product — no reliable data was found that such a level is typical specifically for puzzle games at launch; if anything, both personally verified reports show the genre median is below this threshold, often noticeably so.

### CPI (cost per install)

The same Adjust report gives the median CPI across all game genres:

> "In 2024, the median cost per install (CPI) for gaming apps decreased from $0.38 to $0.36. Casino apps climbed from $1.17 to $1.5 [...]. Hyper casual games climbed from $0.33 to $0.4, while hybrid casual nearly doubled, up from $0.54 to $0.95." — the regional breakdown in the same place: "North America ($1.03 to $1.2) and the U.S. ($1.04 to $1.22) saw notable increases in CPI".

— [Adjust, "The gaming app insights report: 2025 edition", 2025, PDF mirror investgame.net](https://investgame.net/wp-content/uploads/2025/05/gamingreport2025_ebook_en.pdf)

Separately, for CPM (cost per 1,000 impressions), the same report gives a specific figure for puzzle games: "Idle RPG and puzzle games also saw increases, reaching $6.06 and $3.75, respectively" — meaning puzzle CPM in 2024 was **$3.75** (a global median figure, not broken down by iOS/Android separately in the report text).

The specialized report **Liftoff and Singular, "2025 Casual Gaming Apps Report"** (data from February 2024 to February 2025, 1.4 trillion ad impressions, 63 billion clicks, 2.5 billion installs, $11.9 billion in ad spend; puzzle games fall under the broader "casual" category in this report) gives a platform breakdown:

> "The cost per install (CPI) of casual gaming apps via iOS amounted to 1.41 U.S. dollars, compared to an overall average of 14 cents for Android" (period: 02/01/2024–02/28/2025).

— [Liftoff, "2025 Casual Gaming Apps Report", 2025](https://liftoff.ai/2025-casual-gaming-apps-report/); duplicated in [Statista, citing the same report, 2025](https://www.statista.com/statistics/1241651/global-cpi-gaming-apps-genre-platform/)

No personally verified figure specifically for "puzzle CPI in the US" and "puzzle CPI in low-cost countries" (separate from the general casual category and from the overall figure for all games) could be found in open reports from Adjust, AppsFlyer, Sensor Tower, Liftoff, GameAnalytics, or AppMagic. An attempt to open the relevant Business of Apps pages (`businessofapps.com/data/mobile-game-retention-rates/`, `businessofapps.com/marketplace/mobile-game-marketing/research/mobile-game-marketing-costs/`) via WebFetch was made several times — the site consistently returned HTTP 403 and did not yield a single page for direct reading, so figures from this site were not included in the file, even though it is frequently cited by other aggregators. The report **AppsFlyer, "State of Gaming for Marketers 2026"** (2025 data; the report uses data from 9.6 thousand game apps, 24.8 billion installs, of which 14.1 billion were paid) was opened via WebFetch, but its landing page does not provide granular figures without registering/downloading the full report — only overall aggregates could be confirmed (global UA spend in gaming in 2025 — $25 billion, per the report's retelling), but not a CPI breakdown by genre or country [AppsFlyer, "State of Gaming for Marketers 2026", landing page, 2026]. Bottom line: **no reliable data was found on separate CPI for puzzle games "US vs. low-cost countries"** — one can only rely on the overall regional CPI for all games (Adjust: US $1.22, North America $1.20 in 2024) and the overall CPI for the casual genre by platform (Liftoff: iOS $1.41, Android $0.14 for the February 2024 – February 2025 period).

## 2. How to measure CPI when testing trailers before a game's launch

### Apple Search Ads

Apple Search Ads Basic campaigns operate on a cost-per-install (CPI) payment model: the advertiser sets a monthly budget and either accepts Apple's suggested maximum cost per install or sets their own — Apple, for its part, does not publish on its official page a specific minimum monthly budget amount for Basic campaigns (the page describes only the mechanism of setting a maximum CPI and Apple's suggestion based on the app's competitive environment), while the typical cap for Basic campaigns per app is up to $10,000/month according to third-party agency breakdowns of the service [ApptWeak, "The ultimate guide to Apple Ads in 2026", 2026]. Apple's official page confirms the model itself (CPI, self-set or Apple-recommended maximum) and contains a promotional offer, "Try Apple Ads for free with a 100 USD credit," for new advertisers [Apple Ads — Basic, 2026]. Limitation of the method: Apple Search Ads only shows the ad in App Store search, meaning it measures not "do people like the trailer in their feed" but conversion from search intent — this is not a full test of creative for a cold audience, more a test of app page conversion.

### Custom Product Pages and Product Page Optimization

Both App Store Connect tools require the app to already be published on the App Store (even if in a limited region/with "available" status rather than "draft") — this is not a way to test a trailer before a real app listing exists.

**Custom Product Pages (CPP)** — additional versions of the app card (up to 70 per app) with their own screenshots, promo text, video preview, and unique link:

> "Developers can publish up to 70 additional versions of their product page on the App Store for iPhone and iPad" [...] "Developers see a 2.5 percentage point increase on average when referring people to a custom product page. This is a 156% increase compared to the 1.6% average conversion rate on default product pages."

— [Apple Developer — Custom Product Pages, 2026](https://developer.apple.com/app-store/custom-product-pages/)

CPPs link directly to Apple Search Ads ad variations — this pairing is exactly what gives a measurable comparison of conversion between different trailers/screenshots under the same search traffic.

**Product Page Optimization (PPO)** — a built-in A/B test of the app card itself on organic traffic (not tied to a specific ad channel): up to three alternative versions ("treatments") with an icon, screenshots, and preview are shown to a randomly assigned share of page visitors, and App Analytics counts impressions, conversion, percentage improvement, and confidence level for the result:

> "Compare different app icons, screenshots, and app previews on your App Store product page to find out which resonate with people most" [...] "If you allocate 40% of your traffic to your test and have two treatments, each treatment receives 20% of your total traffic and your original product page receives the remaining 60%."

— [Apple Developer — Product Page Optimization, 2026](https://developer.apple.com/app-store/product-page-optimization/)

The PPO test is designed for an app with already existing organic or paid traffic (tests run up to 90 days or until manually stopped, and the estimated duration is based on existing historical conversion figures) — meaning it is a tool for "after the page is already live," not for pre-research on a trailer with no listing at all.

### Meta and TikTok

Both channels allow directing ads to an app pre-order page (Apple supports pre-orders via the App Store) or directly to the app page if a test build has already passed review and been published (for example, in a limited-region soft launch). Exact official figures for minimum daily/weekly campaign budgets in Meta Ads Manager and TikTok Ads Manager could not be personally verified through open help pages for the purposes of this document: several attempts to open the official Meta Business Help Center and TikTok for Business help pages via WebFetch returned either pages with no substantive text (only a title) or a 404 page for TikTok — so a specific minimum budget figure in USD/day cannot be included in this document (risk of an outdated or incorrect number). What can be said without risk of error: both services allow running app-install-optimized campaigns with small daily budgets (campaigns of this kind are typically tested over several consecutive days so the delivery algorithm accumulates enough data to stabilize the bid — this is general optimization mechanics, not a specific number), and both accept a pre-order/app-page link as the landing page for an install campaign. Before budgeting this into the MVP plan, minimum budgets need to be checked with fresh eyes directly in the Ads Manager interface at launch time — no exact official data was found as of this file's collection date.

### The "store page stub" as a separate technique

The technique named in the task, "store page stub" (dummy/fake door store page), is not directly supported as an official tool by either Apple or Google — the App Store does not allow publishing a listing without a real build that has passed review. What is practically referred to by this term in the industry is either (a) an App Store pre-order page with a minimal working build that has passed review, or (b) a separate web page imitating the app card, to which ads lead, with a click on the "Install" button measured as a proxy conversion instead of a real install. No general reliable statistics on the accuracy of this proxy method (how well CTR on a web stub predicts future CPI in the real App Store) were found in the personally verified sources of this research — this is described as a common practice among developers, but not as a method measured and published with precise numbers by a major source.

## 3. Attribution on iOS after ATT: SKAdNetwork and AdAttributionKit

**SKAdNetwork (SKAN)** — a private, aggregated attribution mechanism introduced by Apple even before ATT: the ad network receives not data about a specific user, but an aggregated postback with a limited "conversion value" per campaign, with delays and crowd-anonymity thresholds that don't reveal precise data for small-volume campaigns. **AdAttributionKit (AAK)** — the SKAN successor introduced at WWDC 2024, which keeps full backward compatibility with SKAN and adds several capabilities absent from SKAN: measuring re-engagement via Universal Links, support for alternative third-party app stores (which became relevant due to EU antitrust regulation, DMA), a "Developer Mode" with a heavily reduced postback delay for testing purposes (around 5–10 minutes instead of the usual 24–48 hours), mandatory cryptographic signing of impressions (JSON Web Signature), and counting an ad impression only when viewed for longer than two seconds [Tenjin — "AdAttributionKit vs. SKAdNetwork: What's the Difference?", 2026]. Both frameworks coexist: "Apple has not announced any deprecation timeline for SKAdNetwork" — meaning switching to AAK right now isn't mandatory unless there's a specific reason (e.g., working with alternative app stores in the EU or a need to measure re-engagement) [Tenjin — "AdAttributionKit vs SKAdNetwork: What's the Difference?", 2026]; [Singular — "AdAttributionKit: the new SKAdNetwork?", 2026].

**Honest answer to the question "is this needed at 500 test installs": no, it isn't.** Both frameworks are designed for attributing purchased ads via third-party ad networks at a scale where anonymity thresholds and campaign-level aggregation function properly — at a sample of 500 installs there will be physically few postbacks, and the mechanisms themselves (postback delays, truncated conversion values, aggregation) are built to protect privacy at large traffic volumes, not to measure precisely on a small test cohort. A direct quote on this: "For a pre-launch test of this scale, attribution frameworks carry minimal practical importance. At 500 installs, you'll likely see limited postback data due to Apple's aggregation and privacy protections designed for larger campaigns" [Singular — "AdAttributionKit: the new SKAdNetwork?", 2026]; another source independently reaches the same conclusion: "At this scale, neither framework meaningfully impacts results. Focus on basic conversion tracking instead" [Tenjin — "AdAttributionKit vs SKAdNetwork: What's the Difference?", 2026]. For a project that is already building its own event collection (file `01-own-event-collection.md`), this means: linking `app_open` to a traffic source is sufficient via your own parameter in a deep link/deferred deep link at your own server level, without hooking up either SKAdNetwork or AdAttributionKit — they're not needed either technically (the volume is too small) or organizationally (they complicate the pipeline for the sake of capabilities that don't unlock at this scale).

## 4. Custom Product Page and Product Page Optimization for comparing trailers

Both mechanisms (described in more detail in section 2) are in principle suitable for comparing trailers against each other — but with caveats regarding applicability to the task of "testing a trailer before the game launches":

- **Product Page Optimization** compares several versions of the card (including video previews) against each other on a single stream of incoming page traffic, randomly splitting visitors across variants, and gives statistics on conversion and confidence level — this is a valid A/B tool for comparing trailers against each other, but only once traffic is already flowing to the page (organic or paid) and the app is already published;
- **Custom Product Pages**, linked to Apple Search Ads ad variations, allow directing different ad traffic to different page versions with different videos and comparing conversion for each version — this is also a comparison of trailers against each other, but again requires a published app and configured Apple Search Ads campaigns;
- neither of the two tools compares trailers against each other *before* the app is published on the App Store — for that stage, only external platforms (Meta, TikTok, YouTube) apply, landing on a pre-order/web stub as described in section 2, or a soft-launch stage in one small region, after which both CPP and PPO become available.

## Sources

- [Adjust — "The gaming app insights report: 2025 edition" (PDF, investgame.net mirror)](https://investgame.net/wp-content/uploads/2025/05/gamingreport2025_ebook_en.pdf)
- [GameDev Reports — retelling of "Adjust: Gaming App Insights Report 2026"](https://gamedevreports.substack.com/p/adjust-gaming-app-insights-report)
- [GameAnalytics — "2025 Mobile Gaming Benchmarks"](https://www.gameanalytics.com/reports/2025-mobile-gaming-benchmarks)
- [Segwise — "Mobile Game Retention Benchmarks 2026"](https://segwise.ai/blog/mobile-gaming-app-user-retention-strategies)
- [Liftoff — "2025 Casual Gaming Apps Report"](https://liftoff.ai/2025-casual-gaming-apps-report/)
- [Liftoff — "Must-Know Highlights From the 2025 Casual Gaming Apps Report"](https://liftoff.ai/blog/highlights-2025-casual-gaming-apps-report/)
- [Statista — "Global CPI gaming apps by genre and platform"](https://www.statista.com/statistics/1241651/global-cpi-gaming-apps-genre-platform/)
- [AppsFlyer — "State of Gaming for Marketers 2026" (report page)](https://www.appsflyer.com/resources/reports/gaming-app-marketing/)
- [Apple Developer — Custom Product Pages](https://developer.apple.com/app-store/custom-product-pages/)
- [Apple Developer — Product Page Optimization](https://developer.apple.com/app-store/product-page-optimization/)
- [ApptWeak — "The ultimate guide to Apple Ads in 2026"](https://www.apptweak.com/en/aso-blog/guide-to-apple-search-ads)
- [Tenjin — "AdAttributionKit vs. SKAdNetwork: What's the Difference?"](https://tenjin.com/blog/adattributionkit-vs-skadnetwork-whats-the-difference/)
- [Singular — "AdAttributionKit: the new SKAdNetwork?"](https://www.singular.net/blog/adattributionkit-the-new-skadnetwork/)

### Pages that could not be opened (mentioned for transparency, figures from them not used)

- businessofapps.com/data/mobile-game-retention-rates/ — HTTP 403 on every attempt via WebFetch
- businessofapps.com/marketplace/mobile-game-marketing/research/mobile-game-marketing-costs/ — HTTP 403
- adjust.com/blog/gaming-app-insights-2026/, adjust.com/resources/ebooks/ and adjust.com/blog/adattributionkit/ — consistent HTTP 429 (rate limiting) on every attempt
- facebook.com/business/help/... and ads.tiktok.com/help/... — opened with no substantive text (only the page title) or as a 404 page
