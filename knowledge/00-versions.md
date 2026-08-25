# Version summary: personally verified

Date verified: 2026-08-24
Purpose: a single point of truth for version numbers. Everything else in
`knowledge/` relies on these figures. Recheck against the source before
starting work — entries go stale.

---

## Verified against primary sources

| Component | Version | Status as of 2026-08-24 | Source |
|---|---|---|---|
| Unity LTS | **6.3 LTS (6000.3.x)** | released December 2025, first LTS after 6.0; two years of support, until December 2027 | [unity.com/blog](https://unity.com/blog/unity-6-3-lts-is-now-available), [docs 6000.3](https://docs.unity3d.com/6000.3/Documentation/Manual/UnityManual.html) |
| Unity, 6.3 patch branch | **6000.3.22f1** (August 13, 2026) | latest f-release of the branch; support until 04.12.2027, extended to 04.12.2028 | [endoflife.date/unity](https://endoflife.date/unity) |
| Unity 6.5 | **6000.5.9f1** (August 19, 2026) | Update release, released June 15, 2026; **this is what Unity offers by default** | [endoflife.date/unity](https://endoflife.date/unity) |
| Unity 6.0 LTS | 6000.0.82f1 (August 19, 2026) | support ends **October 16, 2026** — in a month and a half | [endoflife.date/unity](https://endoflife.date/unity) |
| Godot 4.6 | 4.6.3 stable | 4.6 released January 26, 2026, 4.6.3 — May 20, 2026 | [godotengine.org/releases/4.6](https://godotengine.org/releases/4.6/), [GitHub 4.6.3](https://github.com/godotengine/godot/releases/tag/4.6.3-stable) |
| Godot 4.7 | **4.7.2** (August 16, 2026) | 4.7 released June 18, 2026, already two patch releases | [endoflife.date/godot](https://endoflife.date/godot) |

### What this confirms in the concept

Section 1 of `cat-shelter-tech.md` was written according to the actual state
of affairs: Unity 6.3 LTS exists, support until December 2027 — as recorded;
releases 6.4 and 6.5 are indeed newer and without two-year support; Godot
4.6.3 stable exists and does indeed have three patch releases behind it.

The argument "don't take 6.4/6.5" is confirmed, but **not for the reasoning
that stood here before.**

The previous edition claimed 6000.5 was in beta. This is outdated: 6.5
was released June 15, 2026, and the latest patch, `6000.5.9f1`, from August
19, is an ordinary stable `f` release. It is exactly this version that Unity
offers by default to anyone downloading the editor today.

The argument still holds, but for a different reason: **6.5 is an Update
release.** Such releases live "until the next release (update or LTS) is
published," meaning support will end when the next version comes out,
possibly in a couple of months. 6.3 LTS has support until December 4, 2027,
extended to 2028.

### How to install 6.3 LTS if Unity offers 6.5

There is a choice, it's just not in plain sight. Two paths:

1. **Release archive** — [unity.com/releases/editor/archive](https://unity.com/releases/editor/archive).
   Find `6000.3.22f1`, click "Unity Hub" — this opens a link of the form
   `unityhub://`, and Hub will install exactly that version.
2. **From Hub itself** — Installs → Install Editor, there's also a link to
   the archive there ([Hub documentation](https://docs.unity.com/hub/add-editor)).

Not "6.3 is unavailable," but "the download page shows the recommended one."

### If you end up having to work on 6.5 anyway

It won't be a disaster: what we use — URP 2D, UI Toolkit, Test Framework,
`JsonUtility` — hasn't changed between these versions. The real cost lies
elsewhere: the entire `knowledge/unity/` catalog was compiled from
`docs.unity3d.com/6000.3/…` pages, and version-tied claims would have to be
rechecked. Plus the main argument of section 0 of the tech document — the
agent is more accurate on the version that's more represented in its
training data, and 6.5 was released in June 2026.

### What in the concept is outdated

**The argument about Godot 4.7 no longer holds.** `cat-shelter-tech.md`
states: "Not 4.7, even though it was released June 18, 2026: 4.6.3 already
has three patch releases, 4.7 doesn't have any yet." As of August 24, 2026,
4.7 has **two patches** — 4.7.1 from July 14 and 4.7.2 from August 16.
Godot's support rule states: "Stable branches are supported at minimum until
the next stable branch is released and has received its first patch
update." This means 4.6's guaranteed support period has already expired, and
it is being kept alive by the maintainers' goodwill.

The conclusion is not "urgently switch to 4.7" — Godot remains a fallback
path, and there's no need to touch it before the publishers reject the game.
The conclusion is different: **the argument used to justify the choice went
stale in two months.** If it comes to Godot, the version choice will need to
be made again, not pulled from this document.

---

## Cloud model prices

Verified against the [pricing page](https://platform.claude.com/docs/en/about-claude/pricing)
as of 2026-08-24. All prices per million tokens.

| Model | Input | Output |
|---|---|---|
| Claude Haiku 4.5 | $1 | $5 |
| Claude Sonnet 5 | $2 | $10 |
| Claude Opus 5 | $5 | $25 |
| Claude Fable 5 | $10 | $50 |

Separately: the discounted price of Sonnet 5 ($2/$10) has been announced as
permanent; the increase to $3/$15 on September 1, 2026 **will not happen**.

The cost calculation for parsing a photo is in
[`vision-model/01-traits-strict-json.md`](vision-model/01-traits-strict-json.md).
Bottom line: about 0.10 cents on Haiku 4.5, about 0.20 on Sonnet 5. The
estimate of "0.1–0.3 cents" from section 3 of `cat-shelter-tech.md` is
confirmed by the calculation.

---

## Needs verification before starting work

These figures are taken from `cat-shelter-tech.md` and `cat-shelter-mvp.md`
and must be checked against the primary source separately — their
verification is carried out in the corresponding `knowledge/` files:

- the App Store's iOS SDK requirement and the date it takes effect
  (in the concept: "starting April 2026, iOS 26 SDK only") — see `ios/01-appstore-requirements-2026.md`;
- the existence of a first-party Unity MCP server — see `agents/01-unity-mcp.md`;
- the current name of the Apple Vision API for animal recognition — see `ios/03-vision-animal-recognition.md`;
- versions of Python, FastAPI, Pydantic, pytest — see `python/01-fastapi-service.md`;
- the state of Godot 4.7 — see `godot/01-godot-4.6-fallback.md`.

---

## Rule for handling this catalog

The entries in `knowledge/` are a snapshot of the state of affairs as of
2026-08-24, not eternal truth. Before relying on a version number, a
command-line key, or an API name from these files, it's worth opening the
linked source. This is especially true for everything related to Apple:
store deadlines and requirements change with an announcement on the website,
without warning.
