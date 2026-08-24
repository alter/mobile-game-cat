# Cat Shelter — Tasks, Roles, Verification

Companion to `cat-shelter-mvp.md` and `cat-shelter-tech.md`.
Evidence for every changed decision below lives in `knowledge/` — start at
`knowledge/README.md`.

Priority: **P0** — no MVP without it. **P1** — required for measurement.
**P2** — only if everything else is done.

---

## Revision, 2026-08-24 — what changed and why

Research pass over the whole stack. Ten tasks were deleted, four added, six
rewritten. Nothing here is a preference; each item traces to a source.

**Deleted — buy instead of build.** The entire custom analytics stack (old 7.1,
7.3, 7.4) is gone. GameAnalytics is free with no player-count cap and no card,
and App Store Connect already reports day-1 retention with no code at all. The
old plan built a service, a store, an offline queue, a de-duplication scheme and
a reporting tool to obtain four percentages that two ready-made tools hand over
for nothing. See `knowledge/analytics/03-free-analytics-options.md`.

**Deleted — security theatre.** Request signing with a shared secret (old 5.3)
is gone. A secret shipped inside the app is extractable, so it buys nothing.
What actually caps the downside is a hard spend limit on the model provider
account, which is now task 5.3.

**Changed — the proxy is not a Python service.** It is a single Cloudflare
Worker: free, no card, no cold start, and network wait does not count against
the CPU limit (verified against Cloudflare's own limits page). A FastAPI app
behind nginx and systemd was a real service built for a few hundred calls. See
`knowledge/python/05-cloudflare-worker-proxy.md`.

**Kept, and now justified.** The cloud vision call survives an explicit attempt
to kill it. Apple's on-device classifier has 1303 categories and exactly five
cat words — no breeds, no coat patterns, while it carries thirty-odd dog
breeds. Pattern cannot be read on device. See `knowledge/ios/06-on-device-coat-traits.md`.

**Added — a spend cap, a silent-failure check, and a threshold review.** Tasks
1.6, 7.0 and 8.0. Each guards against a failure that produces no error message
and is only discovered after the money is gone.

**Unchanged on purpose.** No DOTween, Zenject or Odin. Those repay themselves on
a long project; on a three-week prototype they add version drift and code the
agent knows less well than bare Unity.

### Second pass — does this list actually produce the planned MVP?

It builds the game described in `cat-shelter-mvp.md`. It did not reliably reach
the goal behind it, for six reasons, now addressed:

1. **Nothing tested whether the game is fun** between the two money gates. Added
   3.7 — five outsiders on the debug build, at the cheapest possible moment.
2. **The worst art risk sat after the money gate.** The M0 creatives need a cat
   that improves and a room that cleans up, but the cat was deferred to 4.4,
   the task most likely to fail outright. Added 0.3.1 and 0.3.2.
3. **Day-1 retention may measure content exhaustion**, not desire. Added 3.8 to
   measure total playtime before the threshold is set.
4. **The mechanism driving day-1 return was optional.** 6.8 raised to P0.
5. **Metric 4 conflated willingness to pay with difficulty tuning.** Redefined in
   8.0 as two separate numbers.
6. **Two deliverables existed only in prose.** The 40-image reference set is now
   5.0; the "re-theme the shell in a week" claim is called out in Part I as
   either needing a task or needing deletion.

One correction in the other direction: **1.2 was never a blocker.** Free apps
need no Paid Apps Agreement, so the tax and banking work is off the critical path
entirely.

---

## Monetisation: decided, so it stops being re-argued

**None of this ships in the MVP.** There are no payments by design; the lose
screen carries one stub button and a counter. Recorded here so the question is
not reopened every week.

| Surface | Verdict | Why |
|---|---|---|
| Energy / lives gate | **Not in the MVP. Open for the product.** | See below — it would corrupt metric 3 |
| Booster on failure ("+1 slot") | **Chosen** | Sells recovery, not admission; already task 6.6 |
| Hints | **Good fit, post-MVP** | Becomes genuinely valuable once 3.9 hides kinds |
| Items for the cat and the room | **Good fit, post-MVP** | Bed, bowl, toys — already the second wave in MVP §14 |
| Frames, glow, backgrounds, poses around the cat | **Good fit, post-MVP** | Additive — they decorate her cat without replacing it |
| Skins that repaint the cat's coat | **No — this one breaks the hook** | See below |
| Rare coats on *rescued* kittens | **Yes** | A different animal she collects; this is the breed-rarity driver |
| Breed rarity of the rescued kitten | **Already the named driver** | MVP §14 |
| Shareable cat card | **Yes, but P2 in the MVP** | Cheap; see below |
| Referral attribution | **No — the free options died** | Branch is now $499/mo; see below |

### Why no energy gate before M8

Metric 3 — return on day 1 — decides the project. Behind a gate, some share of
returns means "the lock expired", not "she wanted to come back", and at roughly a
hundred installs the two cannot be separated. The gate would also *raise* the
number, so the decision to continue would rest on an inflated reading.

It would also hide the problem 3.8 just found. Twelve levels finish in 14–24
minutes; a gate stops players finishing in one sitting — not because there is
more game, but because they were locked out. The content problem would remain,
merely invisible.

A distinction worth keeping, from Deconstructor of Fun (June 2026): "Match-3
stops you with a loss, Merge stops you when you run out of energy." Our mechanic
stops the player with a jam, so structurally we are in the match family, where
the genre-consistent gate is lives-on-failure, not energy-to-play. Royal Match
and Homescapes use lives; Merge Mansion and Travel Town use energy. All four are
in the MVP's own reference list, so "the references do it" cuts both ways.

Against any gate at all, for this game specifically: the audience plays "10–20
минут в паузах между делами". The whole point is that she has fifteen free
minutes *now*. A gate refuses her at precisely the moment paid acquisition
bought. It is the same injury as losing progress on the metro (see 6.7), and it
contradicts "котёнок не болеет".

Honest cost of refusing: an economy built on cosmetics and rarity needs higher
retention than one built on gates — few players pay, and they pay only after
attachment forms. That is a real bet, not a free lunch. Revisit it on M8 data,
not on instinct.

### Why coat skins are the one item to refuse

Selling items *for* the cat is sound. Selling a skin that **repaints the cat** is
not, and the reason is the whole premise: the coat comes from her photograph.
"Отличие от всего, что есть на рынке: это её кот." A skin that overwrites the
coat sells her the removal of the one thing that made her attach.

The workable rule is **additive versus substitutive**, not "cosmetics yes/no":

- **Additive — sell freely.** Frames, neon outlines, glow, backgrounds, poses,
  collars, beds, bowls, toys, the room. Every one of these decorates *her* cat and
  makes the photo-derived coat more visible, not less.
- **Substitutive — do not sell.** Anything that repaints or replaces the coat.

"Уникальный раскрас" sits on both sides depending on the animal. A rare coat on a
**rescued kitten** — a different creature she collects — is exactly the breed
rarity already chosen as the revenue driver. A rare coat painted over **her own
cat** erases the hook. Same word, opposite decisions.

### The shareable cat card: yes to the sharing, no to the referral

Two separate things, with very different price tags.

**The card is cheap and should exist.** An in-app screen showing her cat, framed,
rendered to an image, handed to the iOS share sheet with a plain App Store link.
No server, no storage, no moderation queue, no privacy exposure — the photo never
left the device to begin with, only the traits did, so what she shares is game
art. A day of work at most.

Note that the MVP currently dismisses this: section 14 lists "обмен снимками в
сетях (даёт почти нулевой приток при заметной работе)" under "later or never".
That judgement is right for the general case — share buttons on scores are dead
weight — and **probably wrong for this game**, because the artifact is her cat
rather than a number. Pet owners share pet pictures unprompted. This is an
argument from reasoning, not from data; treat it as a cheap bet, not a certainty.

**Referral attribution is the expensive half, and it just got more expensive.**
Crediting an install to the person who shared needs deferred deep linking. The
free option is gone: Firebase Dynamic Links shut down on 25 August 2025, and
Branch has removed its free tier, starting at $499/month. That collides head-on
with two standing decisions — no paid services, and no ATT prompt.

So for the MVP: share the card, link to the plain App Store page, and **do not
try to attribute**. Two free signals are still available:

- count taps on the share button as an in-app event (intent, not conversion);
- watch the App Store Connect referrer breakdown for web traffic — crude, but it
  is already included in the $99 and needs no code.

If sharing turns out to matter, buy attribution later with revenue. Buying it now
would cost more per month than the entire retention test.

**One content caution.** If the card carries the name she typed for her cat, that
name becomes a public artifact next to your app's branding. Either leave the name
off the shared image or filter it. Cheap to decide now, embarrassing to discover
later.

### The referral reward ladder: sound shape, wrong milestone

Proposed: 1 invite → small bonus, 5 → triple bonus, 10 → simple skin, 30 → unique
skin, 100 → a "blogger" item (blanket, cushion, basket, bow). Recorded as
post-MVP design. **Not in the MVP**, for four reasons, in descending order of how
hard they are to argue with.

**1. The arithmetic kills it before the engineering does.** The retention test
buys roughly a hundred installs *in total*. A ladder whose rungs are 5, 10, 30
and 100 invites has four tiers that literally nobody can reach at that scale. You
would build five rewards to observe one.

**2. It needs the counter we cannot afford.** Crediting "five people came through
your link" is deferred deep linking, and the free options are gone — Firebase
Dynamic Links closed 25 August 2025, Branch starts at $499/month. Two cheap
substitutes exist and both should be understood before choosing:

- **Manual invite code.** Sharer gets a code, the new player types it during
  onboarding. Free, no SDK, no ATT, real attribution — for the minority who
  bother to type it. This is what small teams actually ship.
- **Click counting on our own Worker.** A link like `…/r/ABC123` records the
  click in D1 and redirects to the App Store. Free, uses infrastructure already
  chosen, no ATT. But it counts *clicks*, not installs, and is trivially gamed by
  clicking your own link. Tolerable if rewards are cosmetic; not if they are
  currency.

The workable combination later: reward on verified code entries, show click count
as progress feedback.

**3. Two rewards on the ladder do not exist and one should not.** "Extra energy"
presupposes an energy gate, which is refused above. Hints are post-MVP. That
leaves the booster as the only tier-1 reward that exists today.

**4. The top rewards collide with the game's own care milestones.** The MVP gives
a bowl at level 4 and a blanket at level 8 as evidence that the kitten is being
looked after — "Полезности не дают и давать не должны". Handing out a blanket, a
cushion or a basket as referral loot spends the same objects as recruitment
prizes and dilutes what earning them through care is supposed to mean. Keep the
two economies apart: care objects come from playing, referral rewards should be
decoration around the cat — frames, glow, backgrounds — which we already decided
are additive and safe.

**And a tone check worth taking seriously.** The audience is defined as avoiding
"соперничества" and pressure. A ladder that reads "recruit thirty friends" is
transactional in a game whose whole proposition is care. The same mechanic framed
as "покажи своего кота" — where the reward is that *her cat* gets a nicer frame,
not that she hit a recruitment target — keeps the mechanic and drops the tone
problem. A 100-invite tier is influencer territory; if it exists at all it should
be an explicit creator programme, not a rung ordinary players stare at.

---

## Part I. Agent roles

Each role is a separate agent run with its own scope, its own subtree, and its
own write permission. Overlap is forbidden: two agents editing one file is the
source of half of all breakage.

| Role | Owns | Writes to | Language |
|---|---|---|---|
| **ARCH** | layer boundaries, review of other roles' output, leak prevention | nothing, review only | — |
| **CORE** | rules engine, unit tests | `/game/Assets/Core`, `/game/Tests` | C# |
| **TOOLS** | solver, level generation, traits worker | `/tools` (Python), `/worker` (TypeScript) | Python 3, TypeScript |
| **VIEW** | presentation, scenes, layout, animation | `/game/Assets/View`, `/Shell` | C#, UXML |
| **NATIVE** | Vision plugin, build pipeline, App Store Connect | `/game/Plugins/iOS`, `/build` | Swift, shell |
| **ART** | prompts, batch generation, curation, background cleanup | `/game/Assets/Art` | Python + curation |
| **GROWTH** | creatives, store page, paid acquisition, metric analysis | `/growth` | — |
| **QA** | acceptance against the criteria written below | `/game/Tests/Integration` | C#, Python |
| **HUMAN** | everything a machine cannot verify | — | — |

**ARCH rule:** ARCH writes no code. Its only job is to check, after every
completed task, that `Core` has not acquired an engine reference, that `View`
contains no rules, and that `Shell` has not reached into `Core`. One leak turns
a one-day Godot migration into a one-month one.

**One claim in the MVP has no task and no verification.** Section 7 of
`cat-shelter-mvp.md` says the shell must be configurable enough to re-theme the
same core onto a different subject — a spaceship, a café — inside a week, and
calls this "the real advantage: not one game, but a machine that turns out
prototypes". Nothing in this list builds that, and nothing tests it. The ARCH
layer checks are necessary for it but nowhere near sufficient: keeping `Core`
free of `UnityEngine` does not make the shell swappable.

Two honest options, and drifting between them is the bad one. Either add a task —
after M6, re-theme the shell to any second subject and time it, with the claim
confirmed only if it takes under a week — or strike the sentence from the MVP and
stop counting a reusable machine among the project's assets. As written it is an
untested belief doing the work of a strategy.

**QA rule:** QA never accepts work from the agent that produced it. Acceptance
criteria are written into the task **before** work starts, never fitted to the
result afterwards.

---

## Part II. Verification levels

| Level | Catches | Tooling | Run by |
|---|---|---|---|
| 1. Unit | rules bugs | NUnit, no engine | CORE, on every change |
| 2. Property-based | unsolvable levels | solver as oracle | TOOLS |
| 3. Headless end-to-end | broken game flow | 12 levels through Core, no engine | QA |
| 4. PlayMode | broken scenes and input | Unity Test Framework | QA |
| 5. Service | traits worker | `wrangler dev` + reference photo set, driven by a script | TOOLS |
| 6. Human eye | visual coherence, game feel | phone in hand | HUMAN |
| 7. Live users | the project goal | four metrics from paid installs | GROWTH |

**The distinction that is easy to lose.** Levels 1–5 verify that a task was
**done**. Levels 6–7 verify that the task was **worth doing**. An agent can do
the first on its own. It cannot do the second, ever.

Of some sixty tasks, only two milestones verify the goal: M0 (does the concept
buy installs) and M8 (do users return and tap to pay). Everything else is
scaffolding. "Nearly every task green" says nothing about viability — a flawless
game nobody installs is a possible outcome. This is the core trap of agent-driven
development: throughput creates the feeling of progress toward the goal without
producing any.

The revision above cut work rather than adding it. That is the shape to keep
looking for: the fastest task is the one deleted because something free already
does it.

**Reference photo set** (assemble before 5.2, 40 images): 20 different cats,
5 dogs, 5 with no animal, 5 blurry, 3 with multiple cats, 2 cats in a picture
rather than live. Every change to 5.2, 5.4, and 5.5 is measured against it.

---

## M0. Validate before writing code

**The only milestone that tests the goal before effort is spent.**

| # | Task (role) | P | Done when | Verified by |
|---|---|---|---|---|
| 0.1 | Market scan: does "photograph your own cat" already exist (GROWTH) | P0 | list of 10 comparable titles with revenue estimates | cross-check AppMagic and store search |
| 0.2 | Assemble style reference (GROWTH) | P0 | 30–40 screenshots from 5 titles in one folder | human eye |
| 0.3 | Art generation prompt (ART) | P0 | 3 props that read as one set | HUMAN views them side by side: one set or a mismatch |
| 0.3.1 | **One cat, two states, for the creatives** (ART) | **P0** | same cat readably worse and better | **outside person: same cat, or two cats?** |
| 0.3.2 | **One room, dirty and clean** (ART) | **P0** | pair reads at thumbnail size | difference visible in half a second |
| 0.4 | 6–8 creatives for a game that does not exist (GROWTH) | P0 | 8 files, 8–15 seconds each | — |
| ↳ 0.4.1 | Concept: room before/after | P0 | | |
| ↳ 0.4.2 | Concept: photograph your cat → it appears in game | P0 | | |
| ↳ 0.4.3 | Concept: rescue from a box in the pile | P0 | | |
| ↳ 0.4.4 | Concept: clean mechanic showcase | P0 | | |
| 0.5 | Store placeholder page (NATIVE) | P0 | accepts traffic | test click produces an entry |
| 0.6 | Spend $300, $40 per creative (GROWTH) | P0 | CPI per creative recorded | ad platform dashboard |
| 0.7 | Go / no-go decision (HUMAN) | P0 | written decision | best creative achieves CPI < $5 |

### The biggest art risk used to sit on the wrong side of the money gate

Concepts 0.4.1 and 0.4.2 cannot be filmed without a cat that visibly improves and
a room that visibly cleans up. The original plan asked only for three props here
(0.3) and deferred the cat to 4.4 — a task its own note calls "the single largest
project risk", where an agent "may fail outright" and the fallback is hiring an
artist. That order means committing $300 and the whole build on the strength of a
creative, then discovering whether the art it promised can be made at all.

Hence 0.3.1 and 0.3.2. They are a small slice of 4.4 and 4.7 pulled forward: one
cat in two states, one room in two states. If that slice fails, you have learned
the project's worst news for zero dollars instead of $300 and three weeks.

There is a second reason, sharper than scheduling. **A creative that promises art
the game cannot deliver makes M0's cost-per-install meaningless as a predictor.**
You would be measuring demand for a game that does not exist and cannot be built.
Whatever appears in the M0 creatives must be producible by the same pipeline that
makes the real game — same prompt, same style, no hand-finishing.

**The CPI < $5 gate is looser than it sounds.** For context: median CPI across
casual games on iOS was $1.41 (Liftoff/Singular, 2025 Casual Gaming Apps Report,
Feb 2024–Feb 2025); Adjust's 2025 report puts the median across all games at
$0.36, and $1.22 in North America. Those are live games with tuned campaigns, so
they are not a like-for-like comparison with a placeholder page — but a bar set
at $5 is unlikely to reject anything, which makes it a weak gate. If the point of
0.7 is to filter, consider $2–3. If the point is only to catch a catastrophe,
leave it and say so. Sources: `knowledge/analytics/02-benchmarks-and-attribution.md`.

**Blocker:** nothing after M0 starts until 0.7 returns "go".
**Moves toward the goal:** directly. This *is* the demand test.

---

## M1. Accounts and hardware

| # | Task (role) | P | Done when | Verified by |
|---|---|---|---|---|
| 1.1 | Apple Developer Program (HUMAN) | P0 | account active | dashboard login |
| 1.2 | Tax forms and payout bank details (HUMAN) | **P2** | payouts section shows Active | dashboard |
| 1.3 | **Xcode 26+, iOS 26 SDK** (NATIVE) | P0 | empty build installs on device | `xcodebuild` exits clean |
| 1.4 | Unity 6.3 LTS, 6000.3.22f1 + iOS Build Support (NATIVE) | P0 | empty project builds for iOS | command-line build |
| 1.5 | App record in App Store Connect (NATIVE) | P1 | TestFlight accepts a build | upload a stub |
| 1.6 | **Hard spend limit on the model provider account** (HUMAN) | P0 | limit set below the reserve, email alert on | Console → Billing → Spend limits shows the cap |

**1.3 is stricter than it used to read.** Apple's own wording: "Starting April 28,
2026, apps and games uploaded to App Store Connect need to meet the following
minimum requirements: iOS and iPadOS apps must be built with the iOS 26 &
iPadOS 26 SDK or later." That date has passed. Xcode 16 no longer produces an
uploadable build. Note this constrains the *build tool*, not the deployment
target — iOS 15+ as a minimum runtime is a separate, still-valid choice.
Source: `knowledge/ios/01-appstore-requirements-2026.md`.

**1.6 replaces the deleted request-signing task** as the actual protection
against a runaway bill. It costs five minutes and is the only measure here that
cannot be defeated by decompiling the app.

**1.2 was wrong, and dropping it removes a stall point.** It was marked P0 with
"longest lead time, blocks the end of M6". It blocks nothing here. Apple: "By
joining the Apple Developer Program, you can distribute free apps on the App
Store under the Apple Developer Program License Agreement", while "To sell your
apps on the App Store or offer In-App Purchases, the Account Holder must sign the
Paid Apps Agreement." This MVP is free and takes no money — the "+5 moves" button
is a stub by design. So no Paid Apps Agreement, no tax forms, no bank details.

Start it anyway if idle time allows, since it is slow and will be needed the day
real monetisation appears. But it is off the critical path, and the schedule
should stop pretending otherwise.

**Moves toward the goal:** no, it is admission. But the goal is unreachable
without it.

---

## M2. Rules engine

| # | Task (role) | P | Done when | Verified by |
|---|---|---|---|---|
| 2.1 | Entities Item, Shelf, Level, Board (CORE) | P0 | types defined, project compiles | build + ARCH review |
| 2.2 | Pile with occlusion, available items (CORE) | P0 | `Board.GetAvailable()` returns top layer | unit tests: empty pile, one layer, three layers, circular block |
| 2.3 | Shelf: place, match, free slots (CORE) | P0 | `Shelf.Place()`, `Shelf.TryMatch()` | unit tests: match at slot boundary, full shelf, match after free |
| 2.4 | Move counter, win, lose (CORE) | P0 | three outcomes distinguishable | unit test per outcome |
| 2.5 | Core coverage (CORE) | P0 | all termination branches covered | coverage report, 90% threshold on `Core` |
| 2.6 | Debug view with plain rectangles (VIEW) | P0 | a level is playable by hand | **HUMAN plays one level on a phone** |

**ARCH review after every task:** zero occurrences of `using UnityEngine` under
`Core`. Enforced by a grep step in the build, not by good intentions.
**Moves toward the goal:** no. Scaffolding.

---

## M3. Levels and solver

| # | Task (role) | P | Done when | Verified by |
|---|---|---|---|---|
| 3.1 | Rules reachable from solver (TOOLS) | P0 | Python calls the same rules engine | same level yields same outcome in C# and Python |
| 3.2 | Solver: solvable, in how many moves (TOOLS) | P0 | answer under 2 s per level | 5 known-solvable and 5 known-dead-end levels |
| 3.3 | Level generation (TOOLS) | P0 | batch of 100 per run | all 100 parse in the loader |
| 3.4 | Difficulty curve (TOOLS) | P0 | pile size 36/48/60 across levels 1–12 | measured win rate: sensible play wins ~98% / ~87% / ~66% per band |
| 3.5 | Ship 12 levels (TOOLS) | P0 | 12 definitions in `/Levels` | each solver-verified, zero dead ends |
| 3.6 | JSON level loading in game (CORE) | P0 | game reads definitions | headless run of all 12 through Core |
| 3.7 | **Five outsiders play the rectangle build** (HUMAN) | **P0** | five written answers | **at least 3 of 5 say they would keep playing** |
| 3.8 | ~~Measure total playtime~~ **DONE 2026-08-24** | P0 | **576 taps, 14–24 min** | owner played all 12 on the refactored curve |
| 3.9 | **Hidden kinds: an item shows its kind only once it is reachable** (CORE) | **P0** | buried items render blank | unit tests; blocks 3.10 |
| 3.10 | **Re-measure the curve under partial information** (TOOLS) | **P0** | new win-rate table | greedy policy that sees only reachable kinds |
| 3.11 | **One complication introduced around level 7** (CORE) | P1 | locked items that open after N matches | unit tests; level 7 differs from level 6 |

**Key property test:** for every generated level, the solver finds a solution.
One unsolvable level in the output means the milestone is rejected.

### 3.8 came back with a number, and the number is a problem

The owner played all twelve levels on the refactored 36/48/60 curve: **576 taps,
14 minutes brisk, 24 unhurried.** That is the entire content of the MVP, consumed
in one sitting. Verdict on feel: mildly enjoyable, doubtful it lasts.

This is exactly the failure 3.8 existed to catch. If everything finishes on day
zero, metric 3 measures content exhaustion rather than desire — the player did
not return because there was nothing to return to. The threshold in 8.0 cannot be
set honestly until this is addressed.

### 3.9 is the cheapest fix, and it repairs the mechanic as well as the metric

The whole pile is visible today: tiles in a flat grid, blocked ones merely dimmed
to 35% opacity. Nothing is concealed, so a level can be solved at a glance. The
tension this genre runs on — not knowing what lies underneath — is absent, and
that is the likeliest reason it wears thin.

Show a buried item as a blank tile and reveal its kind once nothing covers it.
One field on the item, one condition in the renderer. It buys discovery, slows
play, and turns sorting back into a puzzle. Sheep a Sheep, named as a reference
in the MVP, works exactly this way.

### 3.10 exists because 3.9 invalidates the numbers we have

The measured table — 98% / 87% / 66% at 36 / 48 / 60 items — came from a policy
that could see every kind in the pile. Under hiding a player cannot plan ahead,
and those rates will fall, possibly a long way.

The solver remains useful as a feasibility oracle ("does a solution exist") but
stops being a difficulty oracle. Order matters: hide first, measure second, tune
pile size third. Tuning against the old numbers would be tuning against a game
nobody will play.

### 3.11 tests a pattern, not a feature

Games of this kind that run for hundreds of rounds do not scale by enlarging the
board. They introduce a new complication every thirty to fifty rounds — blockers,
reordering, a different way items arrive. Twelve levels of "the same thing, more
of it" is the shape that bores, and that is what we currently have.

One complication, introduced once, is enough here: it proves the rhythm works and
makes at least one room memorable. The full ladder is post-MVP design and belongs
in `cat-shelter-mvp.md` section 14, not in this list.

### 3.7 fills the project's largest measurement hole

Between M0 and M8 there is no check on whether the game is any good. M0 tests
whether a promise sells; M8 tests whether the finished thing retains. In between
sit three weeks of building on the assumption that the core loop is enjoyable —
an assumption nothing challenges until the last $400 has been spent.

At the end of M3 a playable game exists in debug rectangles. That is the cheapest
moment in the whole project to find out the loop is dull: no art, no shell, no
store account required. Five people, ten minutes each, one question.

Note 2.6 does not cover this. It has HUMAN — you — play one level. Nobody is a
reliable judge of whether their own game is fun. The answer has to come from
people with nothing invested.

If 3 of 5 say no, the correct response is to change the mechanic or stop, and
either is far cheaper here than after M6.

### 3.8 exists because retention needs something to retain

Metric 3 asks whether players return on day 1. If 12 levels take 40 minutes and
the target player sits down for 10–20 minutes, a keen player finishes the entire
game before day 1 arrives — and then "did not return" means "had nothing left to
play", not "did not want to come back". The metric would be measuring content
volume while appearing to measure desire.

Get the number before M8. If total playtime is under roughly an hour, either add
levels or accept that metric 3 has a ceiling and say so in 8.0 when setting the
threshold. This is cheap to learn and expensive to discover afterwards.

**Moves toward the goal:** 3.7 and 3.8 yes, directly — they are the only checks
between the two money gates. The rest removes the "game is unplayable" risk.

---

## M4. Art

| # | Task (role) | P | Done when | Verified by |
|---|---|---|---|---|
| 4.1 | Prop list, ~30 items (ART) | P0 | list approved | — |
| 4.2 | Batch generation (ART) | P0 | 30 files, one run, one prompt | — |
| 4.3 | Curation and background cleanup (ART) | P0 | 30 PNGs, alpha, uniform size | automated: size and alpha; human: coherence |
| 4.4 | **6 cat silhouettes** (ART) | P0 | 3 states × 2 fur lengths | **show the 6 images to an outside person: same cat or different cats?** |
| 4.5 | Cat layers (ART) | P0 | base fill, pattern mask, white markings, eyes as separate layers | 6 coat variants composite without drift |
| 4.6 | Coat compositing shader (VIEW) | P0 | coat applied at runtime | PlayMode test: 6 trait sets → 6 distinct cats |
| 4.7 | Rooms, dirty and clean (ART) | P1 | 12 pairs | **difference readable on a 200×400 thumbnail in half a second** |
| 4.8 | Bowl and blanket in room (ART) | P1 | 2 props place correctly | human eye |
| 4.9 | App icon, 5 variants (ART) | P0 | 5 files at 1024×1024 | **poll: 10 people pick which they would tap** |

**Blocker and the single largest project risk:** 4.4. The acceptance criterion
is deliberately delegated to an outside human — an agent will report "they look
consistent" almost every time. Two attempts, then hire an artist for a one-off.
Do not drag this out.
**Moves toward the goal:** 4.9 directly (the icon decides more than the 12
levels do), 4.7 through creative strength. The rest is scaffolding.

---

## M5. Cat photo capture

| # | Task (role) | P | Done when | Verified by |
|---|---|---|---|---|
| 5.0 | **Assemble the reference photo set** (HUMAN) | **P0** | 40 images in one folder | count and composition match the list in Part II |
| 5.1 | **Cloudflare Worker, POST /traits** (TOOLS) | P0 | deployed, responds to a test request | `wrangler dev` then live call: valid, malformed, empty, oversized payload |
| 5.2 | **Traits schema via structured outputs** (TOOLS) | P0 | every response parses | **reference set: 100% parse rate, zero out-of-enum values** |
| 5.3 | Rate limit in the Worker (TOOLS) | P1 | burst from one device is refused | live call loop past the limit |
| 5.4 | Vision plugin, native (NATIVE) | P0 | returns bounding box and confidence | on device, against reference set |
| 5.5 | Four-outcome handling (VIEW) | P0 | 4 outcomes → 4 distinct messages | **reference set: 20 cats accepted, 5 dogs and 5 empty rejected with correct copy** |
| 5.6 | Crop and downscale to 512 (NATIVE) | P0 | payload under 200 KB | automated |
| 5.7 | Capture screen (VIEW) | P0 | camera and gallery both work | PlayMode test |
| 5.8 | Meet-your-cat screen, name entry (VIEW) | P0 | cat renders with traits from the photo | **HUMAN photographs their own cat and recognises it** |
| 5.9 | Skip → default cat (VIEW) | P0 | path without a photo completes | PlayMode test |
| 5.10 | Offline fallback, base colour only (VIEW/NATIVE) | P2 | cat still appears with no network | run with the Worker unreachable |

**5.0 was described but never scheduled.** Part II names the reference set and
makes 5.2, 5.4 and 5.5 measurable against it, yet no task created it. Somebody
has to find 20 different cats, 5 dogs, 5 empty frames, 5 blurry shots, 3 with
several cats and 2 photographs of pictures rather than live animals. That is an
afternoon of human work with no code in it, and three P0 tasks are blocked until
it exists.

**5.1 is a Worker, not a service.** One file, standard `fetch` handler, key held
in `wrangler secret`. Free tier covers this workload with room to spare: 100,000
requests a day against our few hundred total, and — verified against Cloudflare's
limits page — "Waiting on network requests … does not count toward CPU time", so
the multi-second model call does not consume the 10 ms CPU budget. Only the
base64 decode and JSON handling do. Full recipe and a working handler:
`knowledge/python/05-cloudflare-worker-proxy.md`.

**5.2 changed method, not acceptance.** Ask for strict JSON in the prompt and you
get JSON that *usually* parses; the acceptance bar here is 100%. Use
`output_config.format` with a `json_schema` carrying `enum` for every field and
`additionalProperties: false`. Values outside the enum then become impossible
rather than unlikely. One schema limit to design around: `maxItems` is not
supported, so cap the length of `white_markings` in the Worker. See
`knowledge/vision-model/01-traits-strict-json.md`.

**5.2 is still not verified for accuracy, only for parseability.** A ginger cat
classified as cream is not a defect — the player will not notice. A defect is a
response that fails to parse or contains a value outside the enum.

**5.3 dropped from P0 to P1, and lost its second half.** Request signing with a
shared secret is deleted outright: the secret ships inside the app and comes out
of it. The spend cap (1.6) is the real ceiling on damage. What remains is a
courtesy limit against a stuck client. Note the binding's `period` accepts only
10 or 60 seconds, so a genuine per-device daily cap needs a KV or D1 counter on
top — build that only if 1.6 shows it is needed.

**5.10 is capped by physics, not by effort.** An attempt to move the whole traits
read on device failed on one specific fact: Apple's image classifier exposes 1303
categories, of which exactly five are feline (`cat`, `adult_cat`, `kitten`,
`bobcat`, `feline`) and none describe a coat pattern — while the same list
carries over thirty dog breeds. Base colour is obtainable offline via Core Image;
pattern is not obtainable at all without training a model we have no data for.
So the offline cat is a plausible cat, not the player's cat. Evidence:
`knowledge/ios/06-on-device-coat-traits.md`.

**Sequencing note worth knowing.** Metric 2 — "uploaded a photo" — is recorded at
the moment of upload, before the player ever sees the generated cat. So a late or
flaky traits pipeline does not invalidate the metric that decides the photo hook.
It does affect day-1 retention through 5.8. Useful if the Worker slips.

**Moves toward the goal:** 5.8 yes — this is where attachment forms, and the
drop-off reaching it is measured in M7.

---

## M6. Shell and build

| # | Task (role) | P | Done when | Verified by |
|---|---|---|---|---|
| 6.1 | Presentation, input, placement animation (VIEW) | P0 | level playable with real art | PlayMode: full level run |
| 6.2 | Room bound to level number (VIEW) | P0 | 12 rooms cycle | PlayMode |
| 6.3 | Three cat states (VIEW) | P0 | transitions at levels 5 and 9 | PlayMode at both boundaries |
| 6.4 | Rewards at levels 4 and 8 (VIEW) | P1 | props appear in room | PlayMode |
| 6.5 | Win screen, before/after (VIEW) | P0 | both frames shown | **HUMAN: difference readable in half a second** |
| 6.6 | Lose screen, "+1 slot" button (VIEW) | P0 | tap recorded, stub shown; booster grows shelf by one slot in Core | PlayMode + event in analytics |
| 6.7 | **Mid-level save, written every move** (CORE) | P0 | quitting mid-level and reopening resumes the same board | unit tests: write, read, corrupted file; **on device: kill the app mid-level, reopen, same position** |
| 6.8 | Notification, permission after level 2 (NATIVE) | **P0** | fires after 24 h | on device |
| 6.9 | Click and haptics on placement (VIEW) | P1 | present | **HUMAN plays 5 minutes straight** |
| 6.10 | Post-level-12 screen (VIEW) | P1 | "to be continued" | PlayMode |
| 6.11 | Copy in English (VIEW) | P0 | zero non-English strings | grep over asset tree |
| 6.12 | Headless build (NATIVE) | P0 | one command produces an ipa | run from a clean checkout |
| 6.13 | TestFlight distribution (NATIVE) | P0 | installs from invite | 3 people installed |
| 6.14 | **Shareable cat card** (VIEW) | P2 | card renders to an image, iOS share sheet opens with a plain App Store link | on device: share to Notes, image arrives intact; `share_tap` event recorded |

### 6.7 was under-specified, and the gap punishes exactly our player

It read "close and reopen preserves progress", and the `Player` entity in the MVP
holds `levels_done, current_level` — level granularity. Nothing stores which
items have been taken or what sits on the shelf, and `Board._taken` is private
with no way out. So today: leave mid-level, lose the room.

The audience is defined as playing "10–20 минут в паузах между делами".
Interruption is their normal case, not an edge case. Riding the metro, the stop
arrives, the app closes — and a half-cleared room evaporates. That is a
punishment, and the MVP's own rule forbids punishments: "котёнок не болеет".

Three things this task now requires:

1. Serialise the board, not the level number: taken items, shelf contents,
   current level, shelf capacity (the booster can change it).
2. Write on **every move**, not on `OnApplicationPause`. iOS kills backgrounded
   apps without warning; the pause callback is not a reliable last chance. This
   is already documented in `knowledge/analytics/01-own-event-collection.md`.
3. Make `Board` reconstructable from that state — today it can only be built
   fresh from a `Level`.

Cheap to build, and it removes the single most common way this audience will lose
work.

**6.8 was raised from P1 to P0, because the priorities contradicted each other.**
Metric 3 — return on day 1 — is one of four numbers that decide the project, and
the evening notification is the only mechanism in the entire MVP designed to
cause that return. Leaving the mechanism optional while the metric it drives is a
go/no-go threshold means a slipped P1 task quietly guarantees a bad reading on a
P0 measurement. Either the notification ships, or 8.0 has to lower the day-1
threshold to account for its absence. Shipping it is cheaper.

**6.8 — ask for notification permission after level 2, not on the first screen.**
Keep the practice; drop the justification. The claim that asking first "doubles
the refusal rate" was traced back through the sources that repeat it and does not
hold up — the primary source does not support the figure. The reasoning that does
survive is plain: a permission request means more once the player knows what the
notification is for. Treat this as judgement, not as a measured fact, and do not
repeat the number to a publisher. Detail: `knowledge/ios/05-notifications-permissions.md`.

**Moves toward the goal:** 6.5 and 6.9 indirectly, through retention. The rest
is scaffolding.

---

## M7. Analytics — bought, not built

The old milestone built a collection service, a store, an offline queue, a
de-duplication scheme and a report. All of it existed to produce four
percentages. GameAnalytics produces three of them for free with no player cap
and no card; App Store Connect produces the fourth with no code at all.

| # | Task (role) | P | Done when | Verified by |
|---|---|---|---|---|
| 7.0 | **Prove events arrive with no ATT prompt** (NATIVE) | **P0** | one event visible in the dashboard from a build that never calls ATT | on device, real build, dashboard read |
| 7.1 | GameAnalytics SDK integrated (VIEW) | P1 | SDK initialises, iOS keys set | debug log shows init, no ATT dialog appears |
| 7.2 | Nine events wired (VIEW) | P1 | all nine visible | **manual playthrough, dashboard checked against expected sequence** |
| 7.3 | Confirm no ATT dialog anywhere (NATIVE) | P1 | dialog never appears | full playthrough on device |
| 7.4 | Funnel + retention readable (GROWTH) | P1 | three funnel steps in GameAnalytics, day-1 retention in App Store Connect | numbers reconcile with 10 manual sessions |

### 7.0 is the one that can silently void the whole milestone

The GameAnalytics privacy manifest declares `NSPrivacyTracking = true` and lists
`tracking.gameanalytics.com` as a tracking domain. Apple blocks domains in that
list when ATT permission has not been granted. The plan is to never ask for ATT
— so the question is whether events still reach the dashboard. The manifest also
marks every individual data type `Tracking = false`, which suggests the tracking
domain is only used when the advertising identifier is enabled, but this could
not be confirmed from any published source.

Half an hour of work: build, do not call `RequestTrackingAuthorization`, send one
event, look at the dashboard. If it does not arrive, either the ATT dialog comes
back or the tool changes. **Do this before wiring the other eight events, and
long before spending the $400 in M8.** A dropped event raises no error.

### Skipping ATT is a deliberate choice, not an omission

GameAnalytics never shows the ATT dialog on its own — only an explicit
`RequestTrackingAuthorization()` call does. Leave it out and additionally call
`EnableAdvertisingIdTracking(false)` before `Initialize()`. The SDK then uses the
vendor identifier instead of the advertising identifier. We lose nothing we were
going to use, and avoid a dialog that costs installs at the door.

### Event mapping

Progression events are a distinct type in this SDK, not a naming convention.
Getting this wrong means the levels do not appear in progression reports.

| Our event | GameAnalytics type | Call |
|---|---|---|
| `app_open` | Design | `GameAnalytics.NewDesignEvent("app:open");` |
| `photo_screen_shown` | Design | `GameAnalytics.NewDesignEvent("photo:screen_shown");` |
| `photo_uploaded` | Design | `GameAnalytics.NewDesignEvent("photo:uploaded");` |
| `photo_rejected` | Design | `GameAnalytics.NewDesignEvent("photo:rejected");` |
| `level_start` | Progression | `GameAnalytics.NewProgressionEvent(GAProgressionStatus.Start, levelId);` |
| `level_win` | Progression | `GameAnalytics.NewProgressionEvent(GAProgressionStatus.Complete, levelId);` |
| `level_fail` | Progression | `GameAnalytics.NewProgressionEvent(GAProgressionStatus.Fail, levelId);` |
| `booster_tap` | Design | `GameAnalytics.NewDesignEvent("booster:tap");` |
| `notification_allowed` | Design | `GameAnalytics.NewDesignEvent("notification:allowed");` |

Event names have character and length rules; a name that breaks them is dropped
in silence rather than rejected loudly. The rules, the debug-logging switches and
the known SDK issues are in `knowledge/analytics/04-gameanalytics-unity-usage.md`.

### What we accept by choosing this

Raw player-level export is not part of the free tier — it sits in a paid product
starting at $499 a month — and stored data expires after twelve months either
way. So we get percentages, not our own event history. For a three-week probe
whose instrumentation gets rebuilt against a publisher's SDK anyway, that is an
acceptable trade. For a product meant to live, it would not be. Revisit this the
moment the game survives M8. Detail: `knowledge/00-vendor-lock-in.md`.

Second caveat, on 7.4: App Store Connect hides any slice covering fewer than five
users. At roughly a hundred installs the headline retention figure should still
appear, but no breakdown will. If it does not appear at all, fall back to
measuring day-1 return through GameAnalytics.

**Moves toward the goal:** it is the instrument that measures the goal. Without
it M8 is blind.

---

## M8. Live validation

| # | Task (role) | P | Done when | Verified by |
|---|---|---|---|---|
| 8.0 | **Re-decide the thresholds against the market** (HUMAN) | **P0** | four numbers written down and dated | done *before* 8.1, never after seeing results |
| 8.1 | Spend $400 (GROWTH) | P0 | 100+ installs | dashboard |
| 8.2 | Collect four metrics (GROWTH) | P0 | table filled | 7.4 |
| 8.3 | Decision (HUMAN) | P0 | written | thresholds from 8.0 |
| 8.4 | Submit to publishers with numbers (GROWTH) | P0 | 5 submissions sent | SayGames, Homa, Kwalee, CrazyLabs, Rollic |

### 8.0 exists because one threshold is roughly double the market

**Thresholds as originally written, with what the market actually does:**

| Metric | Threshold | Market reference | Verdict |
|---|---|---|---|
| reached capture screen | > 90% | no external benchmark; internal funnel step | reasonable |
| uploaded a photo | > 40% | none exists — the mechanic is novel | a guess, and unavoidably so |
| returned on day 1 | > 35% | **puzzle median 19.66–20.74%** (GameAnalytics 2025, 11,600 games, 2024 data); all games averaged 27% (Adjust 2025) | **~1.8× the genre median** |
| tapped "+1 slot" | > 15% of those who reached the lose screen | no comparable public figure found | a guess; denominator recorded separately |

The day-1 number is the problem. As written, "> 35%" is not a floor separating a
viable game from a dead one — it is a level well above typical for the genre. A
game returning 25% would be shut down by this rule while sitting above the
median. Note also that the widely repeated "puzzle day-1 is about 32%" figure did
not survive a check against the primary source; the real number is close to half
that.

Decide deliberately, and write the decision down before any money moves. Three
defensible stances:

- **Keep 35%.** You are only interested in a breakout, not a viable game. Honest,
  and consistent with "three prototypes in three months".
- **Lower to ~25%.** Comfortably above the genre median, still evidence the hook
  works.
- **Two lines instead of one.** Below 20% stop; 20–30% iterate once; above 30%
  push. Costs nothing and avoids a binary verdict on a noisy sample.

Whichever you choose, choose it now. The failure mode this task prevents is
picking the threshold after seeing the result, which turns the whole $700 test
into theatre. Sources: `knowledge/analytics/02-benchmarks-and-attribution.md`.

### Metric 4 measures two things at once, and one of them is level tuning

"Tapped the booster, at least once, > 15%" is written against all players. But
the button only appears on the lose screen, and 3.4 tunes the difficulty curve
by pile size (36/48/60). If the curve is generous, few players ever lose, few
ever see the button, and a low reading means "the levels were easy", not "nobody
would pay". The measured win rates (~98% / ~87% / ~66%) mean roughly a third of
level-9+ attempts jam — the denominator is real, but it must be recorded.

The offer itself changed with the rules: the button is now "+1 shelf slot",
not "+5 moves" — a move limit no longer exists, so extra moves could fix
nothing. The event is `booster_tap`, neutral to what is offered.

Fix the definition rather than the game: **measure taps as a share of players who
reached the lose screen**, and record how many players that was. Two numbers
instead of one, both meaningful:

- share of players who ever lost a level — tells you whether the difficulty curve
  works at all;
- share of those who tapped — tells you about willingness to pay, which is the
  thing you actually wanted to know.

Set both in 8.0. Recording only the combined figure produces a number that cannot
be acted on, because a failure gives no clue which half broke.

**One more caution about the sample.** A hundred installs puts a day-1 retention
reading in a range of several percentage points either way. Treat 24% and 27% as
the same number. This argues for the banded stance above.

**Moves toward the goal:** this *is* the goal test. The second and last time in
the project.

---

## Sequencing

```
M0 ──────────► 0.7 go / no-go          ← gate 1: does the promise sell
       │
       └─► M1 (accounts; 1.2 is NOT on the critical path)

after "go":
  M2 ──► M3 ──► 3.7 fun gate ──┐       ← gate 2: is the loop worth building
  M4 ──────────────────────────┼──► M6 ──► M7 ──► 8.0 ──► M8
  M5 ──────────────────────────┘                   │
                                                   └─ gate 3: thresholds fixed
                                                      before any money moves
```

Three gates now, not two. Gate 2 is new and costs nothing but pride.

M2, M4, M5 run concurrently under different roles and do not block each other.
M6 is the integration point — where it becomes clear what does not fit.

## Five places this stalls

1. **4.4, cat silhouettes.** The one task an agent may fail outright. Acceptance
   is deliberately delegated to an outside human. Partly de-risked by pulling
   0.3.1 forward, so the failure now surfaces before the money.
2. **3.7, five outsiders on the rectangle build.** The second gate, and the one
   most likely to be skipped because it is uncomfortable and requires other
   people. Skipping it means the first real verdict on whether the game is fun
   arrives at M8, with the budget already spent.
3. **0.7, the go/no-go after creatives.** The urge to proceed anyway is the
   project's main trap.
4. **7.0, events blocked without ATT.** New, and the nastiest of the five
   because it fails silently. Everything downstream of it looks green while
   measuring nothing. Half an hour of work, done early, removes it.
5. **The gap between green tasks and a live game.** Agents will close most tasks
   with green tests. Until M8 that means nothing. The only remedy is that HUMAN
   picks up a phone and plays after every milestone, starting at 2.6.

---

## What this costs to run

Infrastructure is now zero. The money in this project is advertising and Apple.

| Item | Cost | Note |
|---|---|---|
| Apple Developer Program | $99 / year | unavoidable, longest lead time (1.1, 1.2) |
| Creative test (M0) | $300 | the actual experiment |
| Retention test (M8) | $400 | the actual experiment |
| Traits calls, whole MVP | **~$0.20–0.40** | ~200 parses of a 512×512 image; ~0.10¢ each on Haiku 4.5 |
| Worker hosting | **$0** | Cloudflare free tier, no card |
| Analytics | **$0** | GameAnalytics free tier, no card, no player cap |
| Retention reporting | **$0** | already included in the Apple fee |
| Art generation | small, one-off | ~30 props, one batch |

Two rules that keep it that way. Set the spend cap first (1.6) — an uncapped bill
is the only way this budget breaks. And do not sign up for anything that demands
a card "for verification": none of the tools chosen here require one, so a card
prompt means you have wandered onto a different product.
