# Saved Kitten — MVP description

Date: August 24, 2026
State: rules engine and solver written, shell not started

Edit from August 25, 2026, after the author played through the whole
prototype. Sections 3, 4, 8, 9, 13 changed: no move counter, item kinds
under the pile are hidden, a room contains several piles (37 levels across
12 rooms), cat states are tied to rooms, the game can be dropped mid-level.
One reason, and it's measured: twelve levels were cleared in 14-24 minutes,
meaning day-one return would measure content exhaustion, not desire to come
back.

Technical decisions — in `cat-shelter-tech.md`, tasks and acceptance — in
`cat-shelter-tasks.md`, sourced rationale — in `knowledge/`, reviews — in
`reviews/`.

---

## 1. The gist in one paragraph

A pile-clearing puzzle: the player pulls items out of a pile and arranges
them on three shelves by matching. What it's for — a kitten sits in an
abandoned house, and with each cleared room it visibly gets better. On
entry the player photographs their own real cat, and the in-game kitten
gets its coloring.

Difference from everything on the market: it's her cat. Attachment forms
before the first level, not after twenty hours of grinding.

## 2. Who and why

Women 30-55, playing 10-20 minutes between chores.

What drives them (in decreasing strength):

- order from disorder — sort it out, clean it up, bring it into shape;
- caring for someone weaker who's getting better;
- completeness — an unfinished set is nagging;
- coziness and furnishing one's own place.

What to avoid: punishment for skipping a day, rushing, competition,
humiliation on loss, pink with glitter (reads as a game for eight-year-olds,
while adults are the ones paying).

Key design rule: **the kitten doesn't get sick.** Punishing a skipped day in
a game about caring repels exactly the audience we're after, and kills
permission for notifications. Instead of illness — the kitten is bored and
didn't show today's scene. The pull to come back is the same, without
guilt.

## 3. How play works

The field: a pile of 30-60 items stacked on top of each other. Only the top
ones are accessible.

**An item's kind is visible only once you've reached it.** While an item
lies under the pile it's a blank with no recognizable outline. Take the top
one off — see what's under it. That's exactly the pull of the genre: not
sorting, but not seeing what's below. A game where the whole pile is
visible at once is solved in one glance and gets boring in twenty minutes —
tested on myself.

Shelves: three cells of 3 slots each. The player takes an item from the
pile and puts it on a shelf. Three identical items on a shelf — they
disappear, the slot frees up. A match is searched for among all nine slots,
not within one shelf; rows are a layout for the eye, not a rule.

Loss: all nine slots are full and there's no match. No move counter: a win
means "the pile is fully cleared," each move removes exactly one item, so
any limit either makes the level unwinnable or never kicks in. The only
loss is a jam; the genre's benchmarks work the same way (Sheep a Sheep,
Triple Match 3D, Zen Match).

Win: the pile is fully cleared.

**One pile is not one room.** A room contains several piles, and a level is
clearing one of them. A room clears in parts: a corner, a wall, a
windowsill. Twelve rooms stay, content becomes three times as much, no new
art is needed — the clutter is assembled from the same items as in the
puzzle.

Piles per room: 1, 2, 3, then three each, and for the last four rooms —
four. Total **37 levels**, about an hour and a bit. The first room closes
in one level: the player immediately sees the whole loop at once — cleared
it, the room got brighter, the cat feels better.

Three things grow differently, and they must not be confused:

| What grows | What it controls |
|---|---|
| piles per room (1 → 4) | **pace** — how often a big reward lands |
| pile size (36 → 60 items) | difficulty |
| complications (three total, each introduced in its own room) | difficulty and variety |

Growing all three at once is not allowed: they multiply, and the last room
turns out unplayable. And the gap between room closures must not exceed
four levels — otherwise two or three sessions in a row finish nothing,
exactly where the player quits.

A measurement with a rational player under full visibility gave about
98% / 87% / 66% win rate across three pile-size bands. **After hidden kinds
were introduced these numbers are invalid** and must be obtained again —
the solver remains valid as a beatability check, but stops being a
difficulty measure (reviews/2026-08-24-refactor-difficulty.md).

Why this move and not match-3: of 120 new match-3 games released in the
first half of 2026, only one reached 100 thousand a month even once
(AppMagic). Pile clearing gives the same satisfaction of restoring
order, but the competition there is incomparably weaker, and the
"before-and-after" spectacle films into an eight-second clip.

## 4. The shell

A house of 12 rooms, each with one to four piles. A room before clearing is
dirty, grey-brown, cluttered. After — bright and warm.

The reward is two-tiered. After each level, part of the room visibly
clears — a small payoff, every time. After the last pile, the room closes
entirely: the light changes, the kitten behaves differently, sometimes an
item appears — a big payoff, earned. The previous draft gave only the
second, i.e. all or nothing.

There's also an overview: the **house map**, showing all twelve rooms and
how much is left. The audience's third-strongest motive — "completeness, an
unfinished set is nagging" — and an uncleared house is exactly that.

The kitten goes through three visible states. The tie is to **closed
rooms**, not to level numbers: that way the arc holds even if the number of
piles is later revised.

| State | When | What it looks like |
|---|---|---|
| 1 | rooms 1-4 | thin, matted fur, ears pinned back, sits in a corner |
| 2 | rooms 5-8 | tidier, walks around the room, watches the player |
| 3 | rooms 9-12 | well-groomed, plays, sleeps on the windowsill |

Items as rewards: a bowl for the fourth room, a blanket for the eighth.
Both visibly change the room and the kitten's behavior. They give no
utility and must not — the moment the bowl starts adding something, care
turns into gear math. For the same reason the bowl, blanket, basket and
cushion are **not given out for invites**: an item you can beg for stops
being evidence of care.

**The game can be dropped mid-level.** Closed the app on the subway — came
back to the same spot, nothing lost. For an audience playing in gaps
between chores, interruption is the normal mode, not an annoying edge case;
losing a half-cleared room would be the same punishment as the kitten
getting sick, just under a different name.

Notification: one a day, in the evening. Soft wording, no guilt: "Murzik
found something behind the couch."

## 5. Photographing your own cat

This is the main feature and the main source of cheap installs. It sits on
the first screen, before the first level.

**We don't generate an image.** A vision model reads the photo and outputs
a strict set of traits:

```
{
  "is_cat": true,
  "base_color": "ginger",
  "pattern": "tabby",
  "fur_length": "short",
  "eye_color": "green",
  "white_markings": ["chest", "paws"]
}
```

The kitten is assembled from ready-made drawn parts according to these
traits. What this gives:

- a cost of a fraction of a cent instead of 2-5 cents per generation;
- a one-second response instead of 5-15;
- the game's unified look is preserved, the style doesn't drift;
- the same cat is shown in all three states for free.

Resemblance is sufficient: "that's my ginger tabby with white paws" — the
attachment has already formed.

Rejection if `is_cat` is false. **We** never store the photo: it is cropped on
the device, sent once, and only the trait set comes back into the game —
nothing about it is written to any storage of ours. That is the promise worth
making to a player, and it holds.

What does not hold is the stronger claim this paragraph used to make, and it
was corrected on 27 August 2026. The crop goes to a model provider, and on a
standard API account inputs and outputs are retained **by that provider for up
to 30 days** before automatic deletion; they are not used for training without
express permission; zero retention exists only as a separate opt-in
arrangement this project does not have. Sources and dates:
`tasks/00-validate-demand/01-market-scan/legal-risk.md`.

The difference matters in one place, and it is not the pitch. A store privacy
declaration asks separately whether data is collected, whether it is linked to
the user, and whether it is used for tracking; the answers are yes, no-today,
and no, written out with their caveats in
`tasks/60-shell-build/14-testflight/NOTES.md`. The data requirements are still
small. They are not zero, and a form filled in from the old sentence would
have been wrong.

Skipping the photo is possible — then a default cat is given. But the
share of those who skip is measured; it's one of the main verification
numbers.

## 6. Money in the MVP

No payments. On the loss screen there's a button **"add another shelf."**
The tap is counted, a "coming soon" stub is shown, **the level stays
lost** — the player replays it.

This is a fake door, and it's meant to be. The fourth metric measures
**intent to pay**, not the benefit of a purchase: it's enough that the
offer appears and the tap is counted. Giving the rescue away for free is
not an option — the win rate would climb from 72% to 95%, and we'd be
handing the game back, with our own hands, the very undemanding-ness we
just moved away from with hidden kinds and a tuned difficulty curve.

Losing here is not a punishment. Punishment is taken-away progress or a
game locked for a day; replaying a two-minute level is routine, and
twenty-eight percent of losses provide exactly the stakes without which the
game gets boring fast.

**What exactly to rescue with was worked out in advance, so the offer would
be credible.** Measured over 400 runs across all 37 levels, a greedy player
with 12% error rate, the metric being whether the run survived to the end:

| Booster | Run survived a jam | Runs won |
|---|---|---|
| none | — | 72% |
| one slot | 33% | 81% |
| three slots | **81%** | 95% |
| return three items to the pile | 51% | 86% |

One slot doesn't save you: the worst jam is nine different kinds on the
shelf, and to make a triple you'd have to dig up two more items of some
kind. Three slots change the jam threshold itself — with nine slots five
different kinds are enough, with twelve you need six.

That's where the name comes from. The shelf is three rows of three; three
slots is exactly **one more row**. Hanging a fake with the label "+1 slot"
would be dishonest twice over: we'd be measuring willingness to pay for
something that doesn't help a third of the time.

Once real economy is built: three slots, once per level.

The point: to learn willingness to pay before the economy is built. If
nobody taps the button — there's nothing to build an economy from, and
that needs to be known in week three, not month eight.

## 7. Code structure

Three layers, strictly separated.

**The rules engine.** The field, items, shelves, moves, win and loss
conditions. Pure logic with not a single engine call, covered by tests.
This is where agents work best — the task is formal and verifiable.

**The view layer.** Scenes, taps, animation, transitions. Thin, knows
about the engine, the engine doesn't know about it.

**The shell.** The kitten, the room, the texts, the items. Pulled out into
settings so that the same engine can be re-skinned for another theme in a
week — a spaceship, running a café, whatever. If the ad-clip test kills the
kitten, you don't start from zero.

That's the real advantage of an infrastructure-minded builder: not one
game, but a machine that outputs prototypes.

## 8. Entities

```
Item        id, kind, sprite, layer, position, blocked_by[], revealed
Shelf       slots[], capacity, place(item), try_match(), add_slots(n)
Level       number, room_id, pile_index, items[], complications[]
Room        id, pile_count, piles_cleared
Cat         name, traits{}, state(1..3), owned_items[]
Player      rooms_done, current_room, current_pile, cat, notifications_allowed
Board       level, taken[], shelf, outcome        ← saved in full
Session     started_at, events[]
```

What changed from the previous draft and why:

- `Level` no longer has `moves_limit` — there's no move counter at all;
- `Item.revealed` — an item under the pile doesn't show its kind;
- `Shelf.capacity` is mutable, because the button on the loss screen adds a
  slot;
- `Room` was added: a level knows which room it belongs to and which
  number pile it is within it;
- `Player` counts rooms, not levels — cat states are tied to rooms;
- **`Board` is saved in full**, not just the level number. Otherwise
  exiting mid-level costs a room.

Save data is local, a single file, **written on every move**. Not on
backgrounding: iOS kills a backgrounded app without warning, and the last
move would be lost. No server, no account login.

## 9. Levels via the solver

Levels are not code but descriptions: number, room, the pile's ordinal
number within the room, the set of items, active complications.

Workflow: an agent generates a batch of descriptions according to given
difficulty rules, the solver runs each one and answers the question of
whether it's beatable at all. Unbeatable ones are discarded.

**The solver's limits need to be known.** It assumes the whole pile is
visible. With hidden kinds the player can't do that, so the solver only
answers "a solution exists," not "how hard." Difficulty is measured
separately — by running a greedy player, who sees only the available
items, through many runs per level. The order is strict: first hiding,
then measurement, then tuning pile size. Tuning against old numbers means
tuning a game nobody will play.

Doing 37 levels by hand is more than a week. Through the solver — an hour,
and that's exactly the case it was written for: levels are free, art isn't.
That's precisely why the house stays at twelve rooms while the level count
is triple that.

## 10. Art pipeline

**The full art brief — in `art-brief.md`:** a list of all the work with
sizes and formats, the cat's layer construction, item families, work order
and acceptance for each group. **The verbatim generation prompts — in
`art-prompts.md`:** the palette in hex values, positive and negative
blocks, a prompt for each of the thirty items, for the six cat silhouettes
and for each of the twelve rooms. Below is only the gist.

One prompt, one list of ~30 items, a batch run, manual curation. Unity of
look matters more than the beauty of any single item — a mismatch of
styles is what instantly gives away a homemade game. That's why the whole
set is generated with one prompt in one pass, not as-needed.

Prompt:

> soft 3D cartoon look, top-down view at a 30-degree angle, rounded shapes
> with no sharp corners, thick soft outline, volume from soft diffuse
> light from top-left, warm muted palette — cream, peach, mint, light
> wood, no acid tones or high contrast, clean solid-color background,
> item centered in frame

The cat is assembled separately — but **not from parts.** The previous
draft said "body, head, tail, paws"; that's wrong for our case. Parts pay
off only with animation, and the MVP has no animation, and the pose
changes wholesale from state to state: huddled in a corner, walking, lying
on the windowsill. Splitting it up would give seams and triple the work.

Correct: **a whole silhouette per state** — six of them, three states
times two fur lengths — with coloring, pattern, white patches and eyes
layered on top. Verbatim prompts and the layer setup — in
`art-prompts.md`, section 4.

## 11. Style

Works: rounded outlines, thick soft edging, volume from light, warm muted
palette, light like morning in a room.

Doesn't work: pixel art (loved by men 25-40, your audience reads it as
unfinished), darkness, high contrast, sharp flat outlines, oversweetness.

Three requirements outrank style:

1. The cat must be recognizably a cat, not an invented creature.
   Proportions close to real, eyes slightly larger.
2. The "before and after" difference reads in a small thumbnail in half a
   second. Dirt — muted grey-brown, cleanliness — light and warm. That's
   half the power of the ad clip.
3. Unity of look matters more than the beauty of any single item.

Reference samples: Royal Match, Gossip Harbor, Merge Mansion, Travel Town,
Homescapes. Screenshots taken from App Store pages, 6-8 frames per game.

## 12. What we measure

Four numbers, no more needed:

| Number | Threshold | What the market does |
|---|---|---|
| reached the photo screen | > 90% | internal funnel step, nothing to compare to |
| uploaded a photo | > 40% | nothing to compare to, the mechanic is new |
| returned on day one | > 35% | **puzzle median is 19.66-20.74%** |
| tapped "add another shelf" at least once | > 15% | no comparable open data found; the share is counted from those who reached the jam screen |

**The retention threshold needs revisiting before spending money.** The
puzzle median is about 20% (GameAnalytics, 11,600 games, 2024 data), the
average across all games is 27% (Adjust). The recorded 35% is not the
boundary between a live game and a dead one, but a level noticeably above
the genre's norm. By this rule a game at 25% would be shut down even
though it's above the median. Also: the figure "puzzles run around 32%"
circulating on blogs did not check out against the primary source.

The decision is left open on purpose and pushed to task 8.0: either keep
35% and hunt only for a breakout, or lower it to 25%, or set bands instead
of one line. It must be chosen **before** spending 400 dollars, otherwise
the threshold gets fitted to the result. Also remember the sample size: on
a hundred installs, 24% and 27% are the same number.

**Collection is off-the-shelf, not our own.** The previous draft said
"our own, not someone else's" with no arguments. GameAnalytics takes the
three in-game numbers for free, with no player cap and no credit card;
App Store Connect counts day-one retention itself, with no code at all.
Writing our own was pointless. The events are few: `app_open`,
`photo_screen_shown`, `photo_uploaded`, `photo_rejected`, `level_start`,
`level_win`, `level_fail`, `booster_tap`, `notification_allowed`.

Day-seven retention is not measured in the MVP — with 12 levels there's
nothing to measure.

## 13. Three-week plan

**Week 1.** The rules engine with hidden kinds and three complications,
the solver, 37 level descriptions across twelve rooms. The game is played
in a debug view of rectangles, without a single piece of art. By the end
of the week — a full playthrough and **five outsiders who played a slice**
(a couple of levels from the start, middle and end, plus one whole room).

**Week 2.** The view layer, the item set, assembling the cat from parts,
photo parsing, the photo screen, the "is it a cat in the photo" check.

**Week 3.** Rooms with partial clearing, the house map, three cat states,
items for the fourth and eighth room, the notification, mid-level saving,
the loss screen with a tap counter, gathering measurements, building for
iOS, checking on a real device.

The estimate is honest on the condition that art runs as a pipeline, not
piece by piece. Most of the timeline is not code.

## 14. Outside the MVP

Second wave, once the numbers check out: a three-slot shelter, decorating,
breeds and rarity of the rescued cat, buying extra slots, grooming.

Later or never: clothing, sharing photos on social networks (gives almost
zero acquisition for noticeable work), treatment, clubs.

Separately: **the rarity of the rescued kitten's breed** — that's what
revenue in the second wave is built on, not cages and bowls. Cleared a
pile, found a box, and inside it someone ordinary or rare. This is the
gacha pull dressed up as rescue, without loot boxes and without guilt.

### The complication ladder — how the game grows into hundreds of rounds

Written down so it doesn't get lost. In the MVP only **one** complication
from this list is taken, task 3.11; the rest is the second wave and
beyond.

Games of this kind don't grow by enlarging the field. Every thirty to
fifty rounds they introduce a new difficulty, and it's the change, not the
size, that keeps the player. The observation is taken from "Yarn Punch,"
where thousands of rounds hold up on exactly this.

What fits our mechanic, in ascending order of cost:

- **hidden kinds** — an item shows its kind only once it becomes reachable
  (in the MVP, 3.9);
- **locked items** — unlock after N triples collected (in the MVP, 3.11);
- **a temporarily blocked slot** on the shelf, for several moves;
- **paired items** — can only be taken back-to-back, one right after the
  other;
- **a kind requiring four** matches instead of three;
- **outside supply** — items arrive into the pile as clearing proceeds,
  rather than lying there from the start;
- **order shuffle** — the pile rearranges itself after each collected
  triple.

Rollout rule: one difficulty is introduced in its own room, explained
wordlessly by the level's own construction, and only afterward combined
with previous ones. The room where something new appeared is memorable —
this also cures the sameness of the twelve rooms.

### On an energy cap

The decision and the reasoning — in `cat-shelter-tasks.md`, the
"Monetisation" section. In short: no caps at all in the MVP, because they
would distort the retention metric; in the product the question stays
open and is decided by M8 data, not by genre habit.

One exception from the "sellable" list: **skins that change the cat's
coloring.** The coloring is taken from her photo, and that's the whole
point of the concept. Selling a recolor means selling the destruction of
the very thing that created the attachment. Everything around the cat —
collars, beds, bowls, toys, the room — is for sale. The cat itself is not
merchandise.

## 15. Legal and risk

- Uploading photos in a game with a childlike look — age rating and
  indecency screening. We cover this by not storing the photo and
  checking it on the way in.
- Creatures generated by the game are not passed off as other players'
  actions. Apple's and Google's rules explicitly forbid this.
- Random drops (breeds in the second wave) require odds disclosure in the
  stores and are already being cut in Belgium, the Netherlands, Brazil.
  Build in disclosure from the start.
- Building for iOS requires a Mac with **Xcode 26+**: as of April 28,
  2026 the App Store only accepts builds made with the iOS 26 SDK. The
  date has already passed, Xcode 16 no longer works. This is a
  requirement on the build tool, not on the minimum iOS version.

## 16. Forks in the road

**Engine — decided: Unity 6.3 LTS.** The analysis below is kept because a
return to Godot is possible if every publisher passes. But the Godot
version would then need to be chosen again: the argument "4.7 has no
patches yet" is two months stale, it's already at 4.7.1 and 4.7.2.

**Where photo parsing runs — decided: a Cloudflare Workers handler.** A
direct call from the game is out, the key would end up on the device. But
"our own machine" also turned out to be excess: one handler on the free
tier gives 100,000 requests a day against our hundreds for the whole MVP,
no card needed, the app doesn't go to sleep. The cost of parsing a photo
has been calculated — about 0.10 cent, i.e. roughly 20 cents for the whole
test.

Whether the cloud could be dropped entirely was also checked. It can't: in
Apple's classifier taxonomy there are 1303 categories and only five
cat-related words, not one coloring. The pattern can't be determined on
device.

The engine fork's analysis is below.

| | Unity | Godot |
|---|---|---|
| Accepted by publishers for prototypes | only one they accept | not accepted |
| Agent work | scenes in a machine format, the agent breaks them | all plain text, the agent edits directly |
| Build speed via agents | medium | high |
| Ready-made ad and measurement layers | all | AdMob and a bit more |

Go to a publisher (Homa, SayGames, Kwalee) — Unity, no choice, their
measurement layer exists only for it. Publish yourself to your own
thousand — Godot, the speed advantage there is real.

Fork outcome: **Unity 6.3 LTS.** Godot stays a fallback path, in case
every publisher passes after the first prototype and we go to
self-publishing.

## 17. Continuation condition

Three prototypes over three months, each up to three weeks. A thousand
dollars: 300 for testing ad clips before code, 400 for testing retention
afterward, 300 held in reserve for a second attempt.

If no clip delivers an install cheaper than 5 dollars — shut it down and
know the truth about it, rather than dragging out a fourth attempt.
