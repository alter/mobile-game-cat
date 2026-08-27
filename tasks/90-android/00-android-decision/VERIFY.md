# Independent verification, 2026-08-27

**Verifier:** a fresh context. I wrote none of `decision.md`, `DECISIONS.md`
D17, nor `90-android/NOTES.md`. This is a `role:HUMAN` task; per
`tasks/README.md`'s independence rule such tasks cannot be re-performed or
simulated by an agent — nothing here re-does the owner's decision. What is
checked is whether the filed record honestly represents what happened and
meets its own task's written criteria, which is a document audit, not a
substitution for the decision itself. No adb, no emulator, no Unity build.
Added one dated correction note to `decision.md` (authorized separately from
this verdict) pointing at D17; did not touch its `go` or its reasoning.

## 1. Is OUTCOME met?

**No, on the clause that mattered most.** OUTCOME: "a dated decision file...
go or no-go... **citing gate-3 numbers by value**, with the day estimate it
is based on." Three of four hold: the file exists, is dated, says go, and
carries a summed per-task day estimate (VERIFY 2, met — 15 rows, agent/owner
split, "roughly six to seven days"). The fourth does not: `decision.md`
states outright, "**There are none.** Gate 3 has not run... none of the four
metrics has a measurement" — VERIFY 1 ("names at least three gate-3 numbers
by value, each with its dashboard source") cannot be satisfied because the
numbers it asks for do not exist, and the file says so rather than
inventing them.

**What matters is not the missing numbers by themselves — it's that a
different kind of decision was substituted for the one specified.** This
task's GOAL was "put the second platform behind a decision **instead of an
assumption**," gated on evidence, precisely to stop `GOAL.md`'s named
anti-pattern ("building second-wave features before gate 3"). What was
filed is, by its own description, a judgement call made *because* the
evidence doesn't exist yet — honestly labelled as such, but structurally
the very thing the gate existed to prevent, not a narrower version of it.
The absence of numbers is the symptom; the substitution of decision-mode is
the finding.

## 2. Is `decision.md` honest, today?

**At the time it was written, yes — about its own limits.** As a standalone
document today, **no, and that is the finding the coordinator asked for.**
`decision.md` reads as self-contained: its `go` rests on "the owner's
reasoning, recorded as given" (idle capacity while iOS is blocked). Nothing
in the file, before this pass, told a reader that this reasoning has since
been superseded. `DECISIONS.md` D17 (same date) records the owner deciding
Android is a target on entirely different grounds, and `90-android/NOTES.md`
already states plainly: *"its `done` rests on D17 now, not on the reasoning
inside it."* A reader of `decision.md` alone — which is what this
directory's `OUTCOME` promises — had no way to know that. Fixed by the dated
correction note added above, kept explicitly separate from this verdict.

## 3. Does anything downstream depend on the reasoning?

**No — checked, not assumed.**

```
$ grep -rln "00-android-decision" tasks/90-android/ | grep -v "^tasks/90-android/00-android-decision"
tasks/90-android/task.txt
tasks/90-android/NOTES.md
tasks/90-android/02-build-pipeline/task.txt
tasks/90-android/12-play-console/task.txt
tasks/90-android/02-build-pipeline/labels.txt
tasks/90-android/12-play-console/labels.txt
tasks/90-android/01-mlkit-capability-probe/labels.txt
tasks/90-android/01-mlkit-capability-probe/task.txt
```

Every task-level hit is a bare `DEPENDS: 90-android/00-android-decision`
line or, in the phase overview, "read `00-android-decision` before starting
anything else." None quotes the idle-capacity argument, the cost table, or
the "what would have to be true for this to be wrong" section. All fifteen
downstream tasks are gated on the *outcome* (does it say go) not the
*argument* for it — and `90-android/NOTES.md` already says the phase's
`done` rests on D17, independently confirming this from the other side. The
honesty gap in §2 is real but does not propagate.

## 4. `done`, or a task whose question was overtaken?

**Argued for: `status:done` stays, because no other label in this project's
vocabulary (`todo | in_progress | review | done | blocked`) says it better,
and changing it would mislead in a different direction.** `blocked` is false
— the phase is not blocked, D17 unblocked it. `todo`/`in_progress` would
imply someone should still go find gate-3 numbers and write them into this
file, which `90-android/NOTES.md` explicitly forecloses: "It is not reopened
here." `done` correctly describes the *phase-gate's* state — an artefact
exists, downstream work is unblocked, and per §3 nothing downstream is
misled about why. What `done` cannot honestly claim is that *this task's own
brief* was fulfilled, and that is what `verify:` is for, not `status:` —
exactly the separation `tasks/README.md`'s own status-done rule draws
("a separate rule from `verify:` because in practice `done` is what people
read"). The correction note makes the split legible to a reader who opens
only `decision.md`; this document makes it legible to whoever reads task
records.

## How to reproduce

```bash
cat tasks/90-android/00-android-decision/decision.md   # "There are none."
grep -n "D17" tasks/DECISIONS.md
grep -n "superseded in substance" tasks/90-android/NOTES.md
grep -rln "00-android-decision" tasks/90-android/ | grep -v "^tasks/90-android/00-android-decision"
```

## What was not checked

- Whether D17's own justification is sound — out of scope, a separate
  decision with its own standing.
- Whether the day-cost table's per-task estimates are accurate — several
  of those tasks have since been verified independently this session
  (`01-mlkit-capability-probe`, `02-build-pipeline`, etc.); their actual
  time cost was not reconciled against this table.
- Any task outside `tasks/90-android/` that might reference this decision —
  searched only within the phase directory.

## Verdict

**`verify:failed`.** VERIFY 1 is unmet, admittedly and by design of the
document itself — not a defect to fix, but a criterion this task cannot
meet given gate 3 has not run, which is a real, disclosed gap between what
was asked for and what was filed. `decision.md` was honest about its own
limits from the start; it was not, until this pass, honest about having
been superseded by D17 as the standing justification — corrected above,
separately from this verdict. `status:` is not changed by this document;
the argument for leaving it at `done` is made in §4 for whoever owns that
label.
