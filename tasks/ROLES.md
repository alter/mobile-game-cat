# Roles

Each role is a separate agent run with its own scope, its own subtree and its own
write permission. **Overlap is forbidden:** two agents editing one file is the
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
| **QA** | acceptance against criteria written before work started | `/game/Tests/Integration` | C#, Python |
| **HUMAN** | everything a machine cannot verify | — | — |

## ARCH

Writes no code. Its only job, after every completed task, is to check that
`Core` has not acquired an engine reference, that `View` contains no rules and
that `Shell` has not reached into `Core`. One leak turns a one-day Godot
migration into a one-month one.

Enforced by a grep step in the build, not by good intentions:
`build/check-core-purity.sh`.

## QA

Never accepts work from the agent that produced it. Acceptance criteria are
written into `task.txt` **before** work starts and are not fitted to the result.

## HUMAN — and why this is not a formality

Tasks with this label are not performed by an agent and not simulated. Not
because an agent cannot do them, but because it errs systematically in one
direction: it reports that the result looks good.

Three places where an outsider must judge:

- the six cat silhouettes — "one cat, or different cats?";
- ten props at 52 px — are they distinguishable;
- a room pair at thumbnail size — which is clean, in half a second.

And a fourth, the most important: **five outsiders play**
(`30-levels-solver/07-outsiders-playtest`). An author cannot judge whether their
own game is fun.
</content>
