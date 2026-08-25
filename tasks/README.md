# Task tree — Rescued Kitten

Format follows `hft/task_manager`. Replaces the flat `cat-shelter-tasks.md`,
which had grown to 1116 lines of which 962 were explanatory prose. An agent
picking up one task had to read twenty thousand tokens to find one table row.

## Key documents

- `GOAL.md` — the goal, the three gates, signs of progress and anti-patterns.
- `ROLES.md` — ARCH, CORE, TOOLS, VIEW, NATIVE, ART, GROWTH, QA, HUMAN.
- `DECISIONS.md` — cross-cutting decisions belonging to no single task:
  monetisation, energy gate, the booster, the referral ladder, the share card.

## Phases

- `00-validate-demand` — test demand before writing code: creatives, store page,
  gate 0.7.
- `10-accounts` — accounts, hardware, spend cap.
- `20-rules-core` — rules engine in engine-free C#, unit tests.
- `30-levels-solver` — solver, level generation, hidden kinds, complications,
  gate "five outsiders play".
- `40-art` — props, cat, rooms, icon. Specs live in `art-brief.md` and
  `art-prompts.md`.
- `50-photo` — photograph your cat: on-device Vision, traits worker, screens.
- `60-shell-build` — shell, house map, save, notification, build.
- `70-analytics` — GameAnalytics and App Store Connect, nine events.
- `80-live-validation` — paid test, four metrics, threshold gate and decision.

## Task format

Every task is a directory with two required files.

`task.txt`, sections in this order:

```
TASK: <short name>

GOAL
  What this produces and why it exists. Two or three lines.

CONTEXT
  Files to read for THIS task. Paths into knowledge/, art-brief.md, reviews/.
  Not "read everything".

SCOPE
  + what is included
  − what is deliberately excluded, and where it lives instead

OUTCOME
  The artefact that exists when this is done.

VERIFY (<role>)
  Numbered checks. Each must be runnable or observable by someone else.

ROLE
  Which roles do the work.

DEPENDS
  Paths to tasks that must finish first.
```

`labels.txt`, one `key:value` per line.

Optional files in the same directory: `NOTES.md` (rationale, measurements,
rejected alternatives), `VERIFY.md` (evidence of verification), and any artefact
— logs, screenshots, prompts, measurement output.

The `−` lines in SCOPE matter as much as the `+` lines. A boundary that is not
written down is a boundary that will be crossed.

## Labels

```
phase:      validate | accounts | core | levels | art | photo | shell | analytics | live
role:       ARCH | CORE | TOOLS | VIEW | NATIVE | ART | GROWTH | QA | HUMAN
type:       feature | fix | research | decision | chore
priority:   P0 | P1 | P2
status:     todo | in_progress | review | done | blocked
verify:     pending | passed | failed
depends:    <task path>
milestone:  M0 … M8
gate:       yes — set only on the three tasks that test the goal
```

## Flow

1. The implementing role works (`status: in_progress → done`).
2. A **different context**, one that wrote neither the code nor its tests, sets
   `verify: passed` or `failed`.
3. Tasks labelled `gate:yes` are confirmed by a human, and the next phase does
   not start until they pass.

## The verify:passed rule

`verify:passed` requires a `VERIFY.md` file in the task directory. Words in a
commit message are not evidence.

`VERIFY.md` must contain:

- a `Verifier:` line naming who checked and what they did **not** do — they are
  not the author of the code, nor of its tests;
- a `## How to reproduce` section with at least one command a third party can
  run **from a clean state**: fresh checkout, empty environment, no variables
  exported by hand;
- a `## What was not checked` section, non-empty. The boundary of a check always
  exists; its absence means nobody thought about it.

Every numeric claim in `VERIFY.md` needs a source — a file path, a command, a
log line.

**Why the rule is shaped this way.** Twice already in this project the report ran
ahead of the check. A commit announced "30 cases agree, 3.1 done" — and in a
clean environment the test did not run at all, because it depended on a variable
exported in the author's shell. The same thirty cases compared wins only, while
the acceptance criterion asked for agreement on every outcome. Both times the
work was formally finished and factually not. "From a clean state" and "what was
not checked" are written against those two incidents.

## The independence rule

`verify:passed` is set by a context that wrote neither the code nor the tests.
Git cannot prove this — every agent commits under one identity — so independence
is carried by the structure of `VERIFY.md`: the `Verifier:` line states what the
verifier did not do.

For `role:HUMAN` tasks substitution is impossible in principle: an agent almost
always reports that the result looks good. Such tasks are not performed and not
simulated. They are handed to a person with a precise statement of what is
needed.
</content>
