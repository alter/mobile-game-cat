# VERIFY — 30-levels-solver/08-measure-playtime

Verifier: an independent agent context, 2026-08-28, against `dev` at `c5bf5cc`.
I wrote neither `NOTES.md` nor `task.txt` in this directory, nor
`tools/solver/pacing.py`, nor the shipped level files. I did **not** play the
game — not on a phone, not on the simulator, not on the emulator, not in the
editor. I did **not** run Unity or `Unity -runTests`. I did **not** count a
single tap. I did **not** speak to the owner. I changed no file in the
repository.

That list is the whole point of this document, so it goes first rather than at
the end.

Written because the task carried `verify:passed` with no `VERIFY.md`, which
`tasks/README.md` makes a precondition of that label.

## What this task claims, and what kind of claim it is

OUTCOME: *"DONE 2026-08-24. 576 taps, 14 minutes brisk to 24 minutes unhurried,
playing all twelve levels on the refactored 36/48/60 pile-size curve."*

`labels.txt` says `role:HUMAN`. `tasks/README.md` is explicit about what that
means for verification:

> For `role:HUMAN` tasks substitution is impossible in principle: an agent almost
> always reports that the result looks good. Such tasks are not performed and not
> simulated.

So there is no honest path by which I confirm 576 taps or 14-24 minutes. A
person sat down on 24 August and played; either that happened or it did not, and
no command I can run distinguishes the two. **I do not verify the measurement.
I verify the record, and the parts of it that the tree can still be asked
about.**

## Item 1 — the number is recorded — record present, measurement unverifiable

`NOTES.md` in this directory, under "## The result":

> The owner played all twelve levels on the refactored 36/48/60 pile-size curve:
> **576 taps, 14 minutes brisk, 24 minutes unhurried.**

Both halves the item asks for — tap count and time — are written down, with the
build they were taken on. The record exists. Whether it is true is outside my
reach.

**One thing I can do is check it for internal consistency**, and it survives
that. The old build was twelve levels, one pile per room, on the 36/48/60 curve.
Re-derived from `tools/solver/generate.py` `items_for_room` (36 for rooms ≤ 4,
48 for rooms ≤ 8, 60 thereafter):

```
4 rooms x 36 + 4 rooms x 48 + 4 rooms x 60 = 144 + 192 + 240 = 576 items
```

**576 items, 576 taps** — exactly one tap per item, which is what the mechanic
implies and what an invented round number would be unlikely to land on. That is
corroboration, not proof: a fabricated figure derived from the same arithmetic
would look identical. But a number inconsistent with the curve would have been
visible here, and this one is not.

**Recorded, consistent, not independently verified.**

## Item 2 — a verdict on feel, not just a duration

`NOTES.md`, same paragraph:

> Verdict on feel: mildly enjoyable, doubtful it lasts.

Present, and unflattering — which is the useful kind. It is also traceable
downstream: `NOTES.md` names the two decisions it triggered, and both exist.
`tasks/DECISIONS.md:32` is *"D2. Difficulty is pile size; pacing is piles per
room — 2026-08-25"*, and `:42` records the restructuring by value: *"Piles per
room: 1, 2, 3, 3, 3, 3, 3, 3, 4, 4, 4, 4 — twelve rooms, **37 levels**."*

**PASS**, to the extent an agent can pass it: the verdict is recorded, and the
work it caused is on disk.

## Re-deriving what the note says about *today's* build

The 2026-08-28 audit's warning is about claims that were true when written and
were never re-read. This task has one: `NOTES.md` closes with a forward estimate
for the 37-level build. Checked today rather than taken on trust.

The note says:

> At 37 levels across the same pacing curve the estimated total is roughly
> **44-74 minutes** by tap count - a floor, since hidden kinds and complications
> both slow play further.

Counted from the level files as they sit on disk right now:

```
shipped level files: 37
total items across all shipped levels: 1860
pile sizes: {36: 9, 48: 12, 60: 16}
piles per room: [1, 2, 3, 3, 3, 3, 3, 3, 4, 4, 4, 4]
```

The piles-per-room row matches `tools/solver/pacing.py:9` exactly, and
`TOTAL_LEVELS = sum(PILES_PER_ROOM)` is 37. So the structural half of the note
still describes the shipped content.

The arithmetic is slightly off, in the direction that matters least:

| basis | ratio to the old build | 14-24 min scaled |
|---|---|---|
| tap count — 1860 / 576 | 3.229 | **45.2 - 77.5 min** |
| level count — 37 / 12 | 3.083 | 43.2 - 74.0 min |

The note says "roughly 44-74 minutes **by tap count**", but 44-74 is the
level-count scaling; by tap count it is 45-78. The gap is about four minutes at
the top end and the note already calls the figure a floor, so nothing downstream
breaks. It is recorded here because it is precisely the shape of error the audit
asked to be re-derived rather than trusted, and because the label on the wrong
basis is the sort of thing that gets quoted forward.

## What has moved underneath this task since it was written

Everything the measurement was taken on is gone:

- **Twelve levels became 37.** `ls game/Assets/Resources/Levels/*.json | wc -l`
  → `37`.
- **One pile per room became one to four.** `pacing.py:9`.
- **Hidden kinds and locked kinds arrived** after this was measured (09, 11), and
  both slow play.

The task's own SCOPE says so, in writing, before any of it happened:

> - This measurement was taken on the OLD twelve-level, one-pile-per-room
>   build. It does not carry forward to the 37-level build in 05 without
>   re-running.

That disclosure is why this task is a true record rather than a stale claim. It
is the only one of the six tasks I examined that anticipated its own
obsolescence in the file, and it is the reason its `status:done` still stands
while the number it holds no longer describes the game.

## How to reproduce

Everything in this document except the measurement itself:

```sh
git clone --depth 1 --branch dev <repo-url> clean && cd clean

# the record and the verdict
grep -n "576 taps" tasks/30-levels-solver/08-measure-playtime/NOTES.md
grep -n "Verdict on feel" tasks/30-levels-solver/08-measure-playtime/NOTES.md

# the decision it triggered, by value
sed -n '32p;42p' tasks/DECISIONS.md

# the old build's item count, from the curve that still ships
python3 -c "print(4*36 + 4*48 + 4*60)"          # -> 576, one tap per item

# today's shipped content, counted rather than assumed
python3 - <<'PY'
import json, pathlib, collections
d = pathlib.Path("game/Assets/Resources/Levels")
files = sorted(d.glob("*.json")); tot = 0
sizes, rooms = collections.Counter(), collections.Counter()
for f in files:
    j = json.loads(f.read_text()); n = len(j["pile"])
    tot += n; sizes[n] += 1; rooms[j.get("room_id")] += 1
print(len(files), tot, dict(sorted(sizes.items())), [rooms[k] for k in sorted(rooms)])
PY
# -> 37 1860 {36: 9, 48: 12, 60: 16} [1, 2, 3, 3, 3, 3, 3, 3, 4, 4, 4, 4]
```

The measurement itself reproduces only one way: a person plays the 37 shipped
levels back to back and records taps and time.

## What was not checked

- **The measurement.** 576 taps and 14-24 minutes are not verified and cannot be
  verified by me. I checked that 576 is consistent with the curve; that is
  corroboration and nothing more.
- **The verdict on feel.** "Mildly enjoyable, doubtful it lasts" is one person's
  judgement of a build that no longer exists. I did not play, and would not
  report an opinion if I had — that is the substitution the independence rule
  forbids.
- **How long today's 37 levels actually take.** Unmeasured. 1860 taps is a count
  of items, not a duration; the 45-78 minute figure above is arithmetic from a
  superseded session, not an observation. Hidden kinds (09) and locked kinds (11)
  both slow play by an amount nobody has timed.
- **Whether the content-volume problem this task exists to detect is solved.**
  The task's purpose was to keep metric 3 from measuring content volume while
  appearing to measure desire. Whether 37 levels is enough for that is a question
  for `80-live-validation`, and nothing here answers it.
- **Unity, any device, any build.** None run.

## Verdict

**True as a record. Unverifiable as a measurement. Superseded as a description
of the game.**

The two VERIFY items are satisfied in the only sense available to a non-human
verifier: the number is written down with its build, the verdict on feel is
written down beside it, and both are traceable into `DECISIONS.md` D2 and the
37-level restructuring that followed. The one figure I could re-derive — 576
taps against 576 items on the 36/48/60 curve — is internally consistent. The one
forward-looking figure in `NOTES.md` is four minutes off at the top end and
labelled with the wrong basis.

**On `verify: passed` I split the answer, and the split is the finding.**

If the label means *this measurement was independently confirmed*, it is not
warranted and never can be by an agent — `tasks/README.md` says `role:HUMAN`
tasks "are not performed and not simulated", and no `VERIFY.md`, this one
included, changes that. If it means *the record required by the task exists,
with its verdict, and its limits are disclosed in the task itself*, then it is
warranted and this file is the missing paperwork.

I would leave the label at `verify: passed` and add one line to `labels.txt` or
`NOTES.md` making the reading explicit — that what passed is the record, not a
second person's replay — so nobody downstream reads it as a confirmed
measurement of the shipped game. That is the same disclosure `60-shell-build/01`
carries on its `verify:` line. Re-running on the 37-level build is a separate
task and belongs to a person, not here.

I have changed no label.
