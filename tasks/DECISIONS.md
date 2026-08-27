# Decisions

Cross-cutting decisions that belong to no single task. Recorded so they stop
being re-argued, and so that reopening one is a deliberate act with a reason
rather than a drift.

Each entry: what was decided, when, why, and what was rejected. Where a number
appears it carries its source.

---

## D1. No move counter — 2026-08-24

**Decided.** The game has two outcomes: win and jam. There is no move limit.

**Why.** A move limit is incompatible with the win condition by construction.
Winning means the pile is empty, each move takes exactly one item, so a level
needs exactly as many moves as it has items. A limit below that makes the level
unwinnable; a limit above it is never reached. There is no meaningful value.

**Evidence.** All twelve shipped levels had 36 items and limits of 44 down to 38.
`OutOfMoves` was unreachable in every one of them, and the "difficulty curve" of
slack 8 down to 2 therefore did nothing at all.

**Corroboration.** The genre titles the MVP itself cites — Sheep a Sheep, Triple
Match 3D, Zen Match — carry no move counter either. The only loss is a jam.

**Rejected.** Adjusting the numbers. The parameter had no correct value.

---

## D2. Difficulty is pile size; pacing is piles per room — 2026-08-25

**Decided.** Three knobs, each governing one thing:

| Knob | Governs | Range |
|---|---|---|
| piles per room | pacing — how often a large reward lands | 1 → 4 |
| pile size | difficulty | 36 → 60 items |
| complications | difficulty and variety | three, one per band |

Piles per room: 1, 2, 3, 3, 3, 3, 3, 3, 4, 4, 4, 4 — twelve rooms, **37 levels**.

**Why.** A level was tied one-to-one with a room, which is the only reason there
were twelve of them. Nothing justified the coupling. Levels are free — the
generator makes a hundred per run — while rooms are the expensive half at twelve
dirty/clean art pairs.

**Evidence.** Owner played all twelve levels of the earlier build: 576 taps,
14–24 minutes. The whole MVP consumed in one sitting, which would make day-1
retention measure content exhaustion rather than desire.

Measured win rate for a sensible player at nine shelf slots: 98% at 36 items,
87% at 48, 66% at 60.

**Retired 2026-08-26 — replaced, not corrected.** Those numbers reproduce
exactly (98.0 / 87.0 / 69.5 from `python -m tools.solver.measure`), so nothing
about them was miscalculated. They describe a player who only watches the
shelf. A player who also notices which kinds have most copies open in front of
her — a habit anyone acquires within a room or two — wins **99.0 / 96.5 /
89.8**. Difficulty is set far more by how the player plays than by pile size,
and one run in ten ending in a jam has consequences for metric 4; see
`10-remeasure-curve-partial-info/NOTES.md`. The pacing structure this decision
sets out is unaffected — the numbers attached to it are.

**Rejected.** Linear growth to twelve piles per room. It yields 78 levels and
94–156 minutes, tempting, but the final room would run twelve levels between
large rewards — two or three whole sittings with nothing completing, arriving
exactly where players stop.

**Rejected.** Growing all three knobs together. They multiply: a final room of
six piles, sixty items each, under three complications.

---

## D3. Buried items hide their kind — 2026-08-25

**Decided.** An item shows its kind only once nothing covers it. Buried items
render blank.

**Why.** With the whole pile visible, a level solves at a glance and the tension
the genre runs on — not knowing what lies underneath — is absent. This is the
likeliest reason the prototype wore thin after twenty minutes.

**Consequence that must not be forgotten.** The solver assumes perfect
information. It remains a feasibility oracle ("a solution exists") but stops
being a difficulty oracle. The 98/87/66 table above was measured with full
visibility and **does not survive hiding**. Order: hide first, re-measure second,
tune pile size third.

**Measured, 2026-08-26 — hiding changed nothing.** The re-measurement is done
(`10-remeasure-curve-partial-info/NOTES.md`, from
`python -m tools.solver.measure`). One policy, played with the buried pile
hidden and with it visible, differs by **0.0 ± 1.2 percentage points**. The
reason is structural and should have been foreseen: a move is chosen among
*reachable* items, and a reachable item always shows its kind, so hiding
removes only the planning past them — and 17 to 28 items are open at once, a
quarter to a half of the pile. The level solved at a glance because the front
is wide, not because the pile was visible; this decision aimed at the wrong
half. Hiding stays — it is free, the genre does it, and whether it changes how
the game *feels* is a question for the playtest, not for a simulation — but it
must not be credited with difficulty it does not deliver.

---

## D4. The booster is a fake door in the MVP — 2026-08-25

**Decided.** The lose screen offers "one more shelf". The tap is counted, a
"soon" stub is shown, **the level stays lost**, the player replays. `AddSlots` is
not called in the MVP.

**Why.** Metric four measures intent to pay, not usefulness of the purchase. An
offer appearing and a tap being counted answers the question; what happens
afterwards does not. Granting it free takes the win rate from 72% to 95% and
undoes the work of D2 and D3.

Losing is not punishment here. Punishment is lost progress or a lockout;
replaying a two-minute level is not. A 28% failure rate supplies the stakes the
prototype was missing.

**What the measurement was for.** Not whether to grant it, but what to offer, so
the offer is credible. Measured over 400 games across all 37 levels with a greedy
player making 12% mistakes, scored on whether the run survived to the end:

| Booster | Run survived the jam | Games won |
|---|---|---|
| none | — | 72% |
| one slot | 33% | 81% |
| three slots | 81% | 95% |
| return three items to the pile | 51% | 86% |

**Rejected.** One slot. The worst jam is nine distinct kinds on the shelf; one
extra place cannot help, because completing a triple means digging two more of
some kind out of the pile. Three slots change the jam threshold itself — five
distinct kinds jam nine slots, six are needed for twelve — and that keeps paying
for the rest of the level.

**Rejected.** Returning three items to the pile, the genre-standard booster. It
relieves the moment without changing capacity: 51% against 81%.

**Naming.** The shelf is three rows of three, so three slots is one more row.
"Put up another shelf" is what a person would want at that moment; "+3 slots" is
spreadsheet language.

**Post-MVP, when it becomes real:** three slots, once per level. Repeatable takes
the win rate to 100% and there is nothing left to sell.

**The mechanism now exists in Core and is still not called — 2026-08-26.**
`Board.AddShelfSlots` grows the shelf and resumes a jammed game; `Shelf.AddSlots`
alone never could, because the board stayed over. The Python mirror had always
resumed, so the two implementations disagreed on what the booster does, and the
conformance test hid it by applying the booster one move early — before the jam
it was supposed to undo. Both sides now resume a jam, leave a win alone, and stay
jammed when the extra room opens no move. The MVP still grants nothing: the fake
door is unchanged.

**Still unverified.** The 72% base rate comes from a modelled player. If the five
outsiders in the playtest gate jam every other run, this reopens — decided on
people, not on simulation.

---

## D5. No energy or lives gate — 2026-08-25

**Decided.** No gate in the MVP. Open question for the product, to be settled on
gate-3 data rather than on genre habit.

**Why not in the MVP.** Day-1 return is one of four numbers deciding the project.
Behind a gate, some share of returns means "the lock expired" rather than "she
wanted to come back", and at a hundred installs the two cannot be separated. The
gate would also *raise* the number, so the decision to continue would rest on an
inflated reading.

It would also hide the problem D2 exists to fix: a gate stops players finishing
in one sitting, not because there is more game but because they were locked out.

**Why probably not ever, for this game.** The audience plays 10–20 minutes in
gaps between other things. The whole point is that she has fifteen free minutes
*now*; a gate refuses her at exactly the moment paid acquisition bought. It is
the same injury as losing progress mid-level, and it contradicts the project rule
that the kitten never falls ill.

**A distinction worth keeping**, from Deconstructor of Fun, June 2026: "Match-3
stops you with a loss, Merge stops you when you run out of energy." Our mechanic
stops the player with a jam, so we are in the match family, where the
genre-consistent gate is lives-on-failure, not energy-to-play. Royal Match and
Homescapes use lives; Merge Mansion and Travel Town use energy. All four sit in
the MVP's own reference list, so "the references do it" cuts both ways.

**Honest cost of refusing.** An economy on cosmetics and rarity needs higher
retention than one on gates: few players pay, and only after attachment forms.
That is a real bet.

---

## D6. Cosmetics: additive yes, substitutive no — 2026-08-25

**Decided.** The rule is not "cosmetics yes/no" but **additive versus
substitutive**.

- **Additive — sell freely.** Frames, neon outlines, glow, backgrounds, poses,
  collars, beds, bowls, toys, the room. Each decorates *her* cat and makes the
  photo-derived coat more visible.
- **Substitutive — do not sell.** Anything that repaints or replaces the coat.

**Why.** The coat comes from her photograph, and that is the entire premise: "it
is her cat". A skin that overwrites the coat sells her the removal of the one
thing that made her attach.

**A word that lands on both sides.** "Unique colouring" is the breed-rarity
driver when it is a rare coat on a **rescued kitten** — a different creature she
collects — and a hook-destroyer when painted over **her own cat**. Same phrase,
opposite decisions.

**Keep the two economies apart.** Care objects — bowl, blanket, cushion, basket —
are earned by playing and are evidence the kitten is looked after. Handing the
same objects out as referral loot dilutes that. Referral rewards should be
decoration around the cat.

---

## D7. Referral: per-partner yes and free, per-user no — 2026-08-25

**Decided.** Creator links through Apple's Custom Product Pages. No per-user
attribution, no invite ladder in the MVP.

**Two different problems, only one expensive.**

*Per-user attribution* — knowing that Masha specifically brought Olya. Needs
deferred deep linking. Firebase Dynamic Links shut down 25 August 2025 and Branch
removed its free tier, now from $499/month. Out, on the no-paid-services rule.

*Per-partner attribution* — knowing that a given creator's link produced N
installs and how they retained. **Free, native, already inside the $99.** Apple
allows up to **70** custom product pages, each with its own URL, and App
Analytics reports impressions, downloads, redownloads, conversion, **retention**
and proceeds per page. Seventy partners is far more than this project will have.

**Why the audience argument holds.** Cat-content creators reach precisely women
30–55, and the hook films itself: the creator photographs their own cat and it
walks into the game. That is not new work — it is creative concept 0.4.2 already
on the M0 list.

**Constraint.** Custom Product Pages require the app to be live on the App Store,
so this channel opens after launch, not during M0.

**The invite ladder (1/5/10/30/100) is post-MVP.** Its shape is sound and its top
rewards are additive, but four of the five rungs are unreachable at a hundred
total installs. Two free substitutes exist for later: manual invite codes, which
give real attribution for the minority who type them, and click counting on the
Cloudflare Worker already in the stack, which is free and gameable.

**Tone caution.** The audience is defined as avoiding competition and pressure. A
ladder reading "recruit thirty friends" is transactional in a game about care.
Framed as "show your cat", where the reward is a nicer frame for *her cat*, the
mechanic survives and the tone problem goes.

---

## D8. The share card exists, at P2 — 2026-08-25

**Decided.** An in-app screen rendering a **before/after** pair to an image and
handing it to the iOS share sheet, with the link pointing at a custom product
page. Not a portrait.

**Why before/after.** The MVP already knows this: the "was — became" pair is the
eight-second reel. A scruffy cat standing still is not something anyone posts.
Offer it at two moments only — after a room completes, and at the cat's state
transitions after rooms 4 and 8.

**Why it is cheap.** No server, no storage, no moderation queue, no privacy
exposure: the photo never left the device, only traits did, so what she shares is
game art.

**Against the MVP's own text.** Section 14 lists social sharing under "later or
never" as near-zero yield. That judgement is right for scores and probably wrong
here, because the artifact is her cat. This is reasoning, not data — a cheap bet,
not a certainty.

**Content caution.** The name she typed must not appear on the shared image, or
it becomes a public artifact next to the app's branding.

---

## D9. Analytics bought, not built — 2026-08-24

**Decided.** GameAnalytics for in-game events, App Store Connect for day-1
retention. The custom collection service, store, offline queue, de-duplication
scheme and report tool are deleted.

**Why.** All of that existed to produce four percentages that two ready-made
tools hand over for nothing. GameAnalytics has no player cap and needs no card;
App Store Connect reports day-1 retention with no code at all.

**ATT is deliberately not requested.** Never call `RequestTrackingAuthorization`;
call `EnableAdvertisingIdTracking(false)` before `Initialize()`. The dialog costs
installs and we use nothing behind it.

**The silent failure this creates**, and the reason a P0 check exists before
anything else in that milestone: GameAnalytics declares `NSPrivacyTracking = true`
and the tracking domain `tracking.gameanalytics.com`, and Apple blocks such
domains without ATT permission. Whether events still arrive could not be
confirmed from any published source. Half an hour on a device settles it; a
dropped event raises no error.

**What we give up.** Raw player-level export is a paid product from $499/month,
and stored data expires after twelve months either way. Acceptable for a
three-week probe whose instrumentation gets rebuilt against a publisher's SDK
anyway. Not acceptable for a product — revisit the moment the game survives
gate 3. If a server is available, send events to our own Postgres as a second
sink so the raw data stays ours.

**One caveat on the free retention number.** App Store Connect hides any slice
covering fewer than five users. At a hundred installs expect the headline figure
but no breakdown.

---

## D10. Infrastructure: managed on the critical path, self-hosted elsewhere — 2026-08-25

**Decided.**

| What | Where | Why |
|---|---|---|
| traits endpoint `/traits` | Cloudflare Workers | downtime is not survivable: the capture screen feeds the metric that decides the project |
| raw event archive | own Postgres | downtime harmless, and the data stays ours |
| invite codes, counters | own Postgres | post-MVP, downtime harmless |
| four-metric reporting | GameAnalytics | already exists |

**Why a Worker and not the FastAPI service originally planned.** The old plan
built a real service — gunicorn, systemd, nginx — for a few hundred calls. One
Worker does the same for nothing: 100,000 requests a day on the free tier against
our few hundred in total, no card, no cold start. Verified against Cloudflare's
own limits page: "Waiting on network requests … does not count toward CPU time",
so a multi-second model call does not consume the 10 ms CPU budget.

**Postgres, no Redis.** Redis would exist for rate limiting, but at these volumes
one Postgres table does it. A second service to run and patch for load that does
not exist is pure cost.

**Any endpoint called from the app needs a domain and a valid TLS certificate** —
iOS App Transport Security will not permit plain HTTP.

---

## D11. Request signing deleted, spend cap instead — 2026-08-24

**Decided.** No HMAC request signing. The ceiling on damage is a hard spend limit
on the model provider account, set before the first call.

**Why.** A secret shipped inside the app is extractable, so it buys nothing. The
spend cap cannot be defeated by decompiling the app, takes five minutes, and is
the only measure here that actually bounds the loss.

A courtesy rate limit stays in the Worker against a stuck client, at P1. Note the
binding's `period` accepts only 10 or 60 seconds, so a genuine per-device daily
cap would need a counter on top — build that only if the spend cap shows it is
needed.

---

## D12. Mid-level save, written every move — 2026-08-25

**Decided.** Serialise the whole board — taken items, shelf contents, current
room and pile, shelf capacity — and write it on **every move**. `Board` must be
reconstructable from that state.

**Why.** The previous spec stored only which level you were on, so leaving
mid-level cost the room. The audience plays "in gaps between other things":
interruption is their normal case, not an edge case. The metro stop arrives, the
app closes, a half-cleared room evaporates. That is a punishment, and the project
rule forbids punishments.

**Not on `OnApplicationPause`.** iOS kills backgrounded apps without warning; the
pause callback is not a reliable last chance.

---

## D13. Engine and language pins — 2026-08-25

**Unity 6.3 LTS, `6000.3.22f1`.** Install from the release archive: the download
page offers 6.5 by default. 6.5 is a real, stable release — `6000.5.9f1` of
19 August 2026, not a beta as an earlier note wrongly claimed — but it is an
**Update release**, supported only "until the next release is published". 6.3 LTS
runs to 4 December 2027, extended to 2028.

**Newtonsoft for level definitions; the save file is written by hand, in Core.**
`System.Text.Json` was in the plan and is a mistake: Unity does not ship it and
under IL2CPP it needs `Reflection.Emit`, which iOS does not have.

**Corrected 2026-08-26.** This decision used to name `JsonUtility` for the save
file, and that could not be built: `JsonUtility` lives in `UnityEngine`, the save
lives in `Core`, and `Core` carrying no engine reference is a condition of the
project, enforced by `build/check-core-purity.sh`. Newtonsoft inside `Core` would
be the same leak in a different coat. So `Core/GameSave.cs` writes its own line
format — plain ASCII, no dependencies, identical under Unity and under
`dotnet test`. The code did this from the start and explained itself only in a
comment, which is exactly the "silent departure from the documents" GOAL.md warns
about. If the Shell ever wants `JsonUtility` it may serialise the same fields;
this format stays the lossless ground truth.

**Xcode 26+ with the iOS 26 SDK.** Apple: "Starting April 28, 2026, apps and
games uploaded to App Store Connect need to meet the following minimum
requirements: iOS and iPadOS apps must be built with the iOS 26 & iPadOS 26 SDK
or later." That constrains the build tool, not the deployment target; iOS 15+
remains the runtime floor.

**No DOTween, Zenject, Odin or mechanic kits.** They repay themselves on a long
project; on a three-week prototype they add version drift and code the agent
knows less well than bare Unity. Ready-made *utility* packages are a different
matter and are taken freely: Newtonsoft, GameAnalytics.

---

## D14. Unity MCP: CoplayDev, after the project exists — 2026-08-25

**Decided.** `CoplayDev/unity-mcp` (MIT, 13,643 stars, verified 2026-08-25),
connected **after** the Unity project is created, not before. Fallback:
`IvanMurzak/Unity-MCP`.

**Official Unity MCP rejected.** `com.unity.ai.assistant` exists and names Claude
Code as supported, but requires a Unity Cloud project and a paid AI subscription.

**Not for speed.** Batch mode was measured on this machine: creating an empty
project 6 seconds, reopening 2–3. The usual argument does not survive. The real
benefit is reading the Unity console — compile and runtime errors arriving
parseable instead of fished out of `Editor.log` — plus scenes and play mode.

**Constraint.** MCP lives inside an editor with a window open, so the agent stops
being self-sufficient. Builds and CI stay on batch mode; the two coexist.

**Do not let it undo the architecture.** Data in JSON and ScriptableObjects, UI in
UXML/USS, scenes built by code — that shape exists precisely so an agent never has
to touch scene YAML.

</content>

## D15. The lock is invisible today — a question for the owner, 2026-08-27

**Not decided.** This entry records a contradiction found while wiring the art
in, with the numbers behind it. Whoever owns the design decides; nothing in the
rules was changed.

**The contradiction.** `Board.IsRevealed` (`game/Assets/Core/Board.cs:85-91`)
returns false for a locked item — being locked makes an item *hidden*, on top of
D3's rule that a buried item hides its kind. The test
`PartialInformationTests.LockedItem_IsNotRevealed` pins that behaviour, so it is
deliberate, not an accident.

The consequence is that complication 3.11 never appears on screen. A locked item
is drawn exactly like a buried one — the same drape — so a player meets a tile
that will not be taken and is given no reason why. Task `40-art/02-prop-blockers`
asks for the opposite in as many words: "prop_locked composited over any one of
the 30 props: prop underneath still identifiable through the overlay." One of
the two documents is wrong.

**Measured, not guessed.** Across the 37 shipped levels: 16 carry a locked kind.
Playing each greedily to the end, the lock is visible **0 times out of 16** as
the rules stand. Removing the `&& !IsLockedByComplication(item)` clause from
`IsRevealed` — changing nothing else — it becomes visible in **16 of 16**, first
appearing on move 0 or 1. So the mechanic is currently either fully invisible or
fully visible; there is no middle setting to tune.

**What is already built either way.** `DebugGameView.MakeTile` draws
`prop_locked` as a child element over the prop's own sprite (class
`game__tile-lock`), so the moment the clause goes, the art is correct with no
further work. Until then that branch is unreachable and the drawing ships unused.

**The choice.**
- Keep the rule: 3.11 is a difficulty knob the player never sees as one, and
  `40-art/02` should drop its overlay requirement.
- Drop the clause: the player sees which kind is being withheld and can plan
  around it. `LockedItem_IsNotRevealed` and D3's wording need revising, and the
  0.0 +/- 1.2 pp win-rate finding from `30-levels-solver/10` would want a rerun,
  since it measured a rule nobody could see.
