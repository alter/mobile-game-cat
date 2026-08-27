# Legal/policy risk: photographing the player's own cat

Date: 2026-08-27. Follow-up to NOTES.md's mechanic check, requested because
the Secret Cat Forest fan-group thread cited there looked like a precedent
against the project's own premise ("it's her cat, from her photograph") and
needed to be tested before anyone relies on it.

All figures and quotes below carry a URL and a retrieval date. Where no
source was found, that is stated as a finding, not filled in.

## 1. How strong is the Secret Cat Forest precedent

**Weak — a single unverified fan comment, not a studio statement.**

The claim in NOTES.md traces to one sentence, from one person, in one
Facebook thread: a group *moderator* (not a developer, not IDEASAM staff)
wrote that the studio "did try to do it worldwide a long time ago but ran
into potential issues with trademarking"
(facebook.com/groups/secretcatforest/posts/1305537703686770, thread dated
2023-08-29, retrieved 2026-08-27). No date is given for the attempt itself,
no source is linked, no patch note or announcement is named.

Searched for a primary source and found none:
- "Secret Cat Forest trademark issue worldwide cat submission official
  statement IDEASAM" — no hit naming trademark or a rolled-back feature.
- "\"Secret Cat Forest\" developer statement trademark cats worldwide
  submissions" — no hit.
- IDEASAM's own site, ideasam.net/cats (retrieved 2026-08-27) — describes
  the studio and the game, nothing about a submission feature or
  trademarking.
- The community-run Fandom wiki's "Cat Requirements" page
  (secret-cat-forest.fandom.com/wiki/Cat_Requirements, retrieved 2026-08-27)
  — lists named cats (Oreo, Fluffy, Curry, etc.) as fixed content with
  care/furniture requirements, consistent with cats being added by the
  studio at its own discretion, not with there having been, and having been
  withdrawn, a player-facing photo-upload feature.

What the game visibly does today (Google Play listing, retrieved
2026-08-27): cats are a fixed developer-curated roster, periodically
expanded through the studio's own "design contest" / social-media events on
its Korean Naver Cafe — a pipeline the moderator's own comment also
describes ("they have added cats through events on their Korean Social
Media Page Cafe NAVER"). That part checks out. The trademark claim about a
withdrawn worldwide feature does not have anything behind it beyond the one
sentence.

**Verdict: does not hold up as a precedent.** It should be read as an
anecdote worth being aware of, not as evidence that letting a player upload
her own cat's photo runs into trademark trouble. Nothing found here
contradicts the project's premise.

## 2. What Apple and Google's rules actually require

Checked against what this project already does, per `tasks/50-photo/task.txt`
and DECISIONS.md D8: the photo never leaves the device except as a cropped
image sent to a model for trait extraction; only ~100 bytes of enum traits
come back; nothing is stored by the app; the optional share card (D8, P2)
renders in-game art, not the photo itself, and the player's typed name is
forbidden from appearing on anything shared.

**The UGC moderation rules likely do not apply — this is the myth to
retire.**

- Apple's Guideline 1.2 (developer.apple.com/app-store/review/guidelines,
  retrieved 2026-08-27) requires filtering, reporting, blocking, and
  published contact info for "apps with user-generated content or social
  networking services" — its examples and enforcement language are about
  content other users can see and interact with.
- Google Play's User Generated Content policy
  (support.google.com/googleplay/android-developer/answer/9876937, retrieved
  2026-08-27) defines UGC explicitly as "content that users contribute to an
  app, and which is **visible to or accessible by at least a subset of the
  app's users**," and its required controls (reporting, blocking) are all
  about one user's content reaching another user.

Because the cat photo is never shown to any other player — the design
already keeps it off-device except for an ephemeral processing call — it
does not meet either platform's own definition of UGC that needs
moderation. Nothing here requires building a report/block/moderation
system for the photo feature.

**What genuinely does apply, regardless of UGC status — real, unglamorous
paperwork:**

- **Privacy policy link**, in App Store Connect metadata and in-app —
  Apple Guideline 5.1.1(i), same page as above.
- **Purpose strings** for camera/photo access (`NSCameraUsageDescription` /
  photo-picker equivalent), required by Apple's privacy Human Interface
  Guidelines (developer.apple.com/design/human-interface-guidelines/privacy,
  retrieved 2026-08-27) and enforced at review; the string must "clearly and
  completely describe the app's use" of the photo.
- **App Store "nutrition label" (App Privacy) and Google Play's Data Safety
  section** must declare Photos as a data type collected, its purpose (App
  Functionality), and that it is shared with a third-party processor
  (Anthropic) for the trait call — even though the app itself never stores
  it. `tasks/60-shell-build/14-testflight/task.txt` already has this on its
  scope list ("Complete the App Privacy … declaration for photo collection —
  required regardless of the fact the photo is not stored server-side"), and
  `tasks/90-android/10-permission-audit/task.txt` plans the matching Play
  Data Safety answers. **Both of those tasks currently phrase the premise as
  "the photo is not stored server-side" — §3 below shows that's not quite
  right, and their declarations need to say what actually happens, not that
  phrase.**
- **Google Play's Photo and Video Permissions policy** (in force since
  2025-01-22, per support.google.com/googleplay/android-developer/answer/16935362
  and corroborating coverage, retrieved 2026-08-27) restricts the broad
  `READ_MEDIA_IMAGES` / `READ_MEDIA_VIDEO` permissions to apps whose *core
  functionality* needs gallery-wide access, and requires a declaration form
  to justify it otherwise. CatShelter only ever needs one photo per capture,
  not the gallery — `tasks/90-android/04-picker-plugin` is already named as
  "the picker choice that avoids storage permissions entirely" in the
  permission-audit task's own context list, i.e. using the system Photo
  Picker (no runtime permission, mirrors iOS's `PHPickerViewController`)
  sidesteps this restriction entirely. Real constraint, already designed
  around correctly.

**The three named risks, checked against what's already built:**

- *(a) Photo contains something other than a cat* — covered by the on-device
  Vision gate (cat-shelter-tech.md §3), which rejects anything not
  classified as a cat/dog above threshold, and per that same section
  "doubles as the indecency filter." Adequate for the animal-detection case;
  see the open question at the end for its limit.
- *(b) Resulting image shared publicly* — covered by D8: the share card
  renders game art, not the photo, and forbids the player's typed name on
  any shared image.
- *(c) Storing biometric or personal data* — this is where the project's own
  documentation needs a correction. See §3.

## 3. The provider quote — checked against Anthropic's current terms, and it doesn't hold

`cat-shelter-tech.md` §3 states: *"The photo isn't stored either by us or by
the vendor: 'Image uploads are ephemeral and not stored beyond the duration
of the API request.'"*

**A verbatim web search for that exact quoted sentence returned zero
results** — it does not appear on any current Anthropic page found. It is
either outdated, paraphrased from a page that has since changed, or
conflates a specific opt-in arrangement with the default. Either way, it
should not be quoted as Anthropic's policy without a live citation, and
right now there isn't one.

**What Anthropic's current official pages say instead** (both retrieved
2026-08-27):

- privacy.claude.com/en/articles/7996866-how-long-do-you-store-my-organization-s-data
  (the article's own "last updated" listing shows 2026-07-01): *"For
  Anthropic API users, we automatically delete inputs and outputs on our
  backend within 30 days of receipt or generation."* This is the standard
  default for a self-serve API account — the kind this project uses per
  DECISIONS.md D11 (spend cap on a vendor account, no request signing, no
  enterprise arrangement mentioned).
- platform.claude.com/docs/en/manage-claude/api-and-data-retention: true
  zero-retention exists, but only as "Zero Data Retention (ZDR)," and *"ZDR
  is enabled per organization … contact the Anthropic sales team"* — an
  opt-in enterprise arrangement, not the default, and not something a small
  self-serve project has by simply calling the API.
- Same page and the privacy article both state retained data is *"never
  used for model training without your express permission"* — so the
  training-data risk specifically is not a live concern under standard
  terms — but retention itself, up to 30 days, is real and is not
  "ephemeral."
- One caveat found but not confirmed at the primary source: a third-party
  summary (anarlog.so/blog/anthropic-data-retention-policy, retrieved
  2026-08-27, dated 2026-03-30) claims Anthropic shortened API log retention
  from 30 to 7 days as of 2025-09-14. Anthropic's own privacy-center article
  above still says 30 days as of its 2026-07-01 update, so this scan treats
  30 days as the sourced figure and flags the 7-day claim as unconfirmed at
  the primary source — worth a direct check before either number goes in a
  privacy policy.

**Correction needed:** the cropped cat photo sent to the API is retained by
Anthropic for up to 30 days under this project's standard account tier, not
"not stored beyond the duration of the API request." It is not used for
training by default. Both `cat-shelter-tech.md` §3 and the "not stored
server-side" phrasing already sitting in
`tasks/60-shell-build/14-testflight/task.txt` and (by implication)
`tasks/90-android/10-permission-audit/task.txt` should be corrected to
match this before either privacy declaration is filled in — an inaccurate
nutrition-label answer is itself a review-rejection and platform-policy
risk, separate from anything about the cat photo itself.

## 4. Plain list

**Real constraints:**
- The cropped photo is retained by Anthropic for up to 30 days by default,
  not "ephemeral" — `cat-shelter-tech.md` §3's quote and the "not stored
  server-side" language in two other tasks need correcting, and the App
  Store nutrition label / Play Data Safety form must say "shared with a
  third party (Anthropic), retained up to 30 days, not used for training,"
  not "not stored."
- Apple 5.1.1 and Google Play's Data Safety section both require a privacy
  policy and disclosure of the third-party photo processor — paperwork, due
  before submission, already scoped into `60-shell-build/14-testflight` and
  `90-android/10-permission-audit` but needs the corrected wording from
  above.
- Purpose strings for camera/photo access are required on both platforms and
  must state the actual use.
- Google Play's Photo and Video Permissions policy restricts broad gallery
  permissions; the project's existing single-photo picker plugin choice
  already avoids needing them — confirm it stays that way.

**Myths / not real constraints:**
- Apple 1.2 and Google Play's UGC moderation/reporting/blocking rules do not
  apply: the photo is never shown to other players under either platform's
  own definition of UGC, by the project's existing design (D8).
- The Secret Cat Forest "trademark" story: unverified, single-source, not
  found anywhere primary. Not evidence against the premise.
- Training-data risk on the photo crop: not applicable under Anthropic's
  standard API terms (no training without express permission, which this
  project wouldn't grant).

**What a person has to decide before launch:**
- Whether "up to 30 days at the processor, not used for training" is
  acceptable to ship as-is, or whether it's worth pursuing Anthropic's ZDR
  enterprise arrangement to make a true "not stored" claim possible — that
  requires a sales conversation, likely inconsistent with the project's
  no-paid-cloud-spend constraint (MEMORY.md) unless ZDR turns out to be free
  at this scale, which was not checked here.
- Whether the on-device Vision "is this a cat" gate is treated as sufficient
  content-safety coverage for Apple 1.1.2 (no realistic depiction of animals
  being harmed) — it only checks *that* a cat is present, not what else is
  in frame beyond the crop box; worth a second look specifically for review
  risk, separate from the UGC question above.
- Who owns correcting `cat-shelter-tech.md` §3's quote and updating the two
  downstream tasks that currently repeat "not stored server-side."
- Whether the reference photo set's dataset licences need resolving before
  any accuracy number built on it is ever shown outside the project. See §5.

---

## 5. The reference photo set's licences — an open question, not a code fix

`50-photo/01-reference-photo-set` builds its 41 images from two Hugging Face
datasets. Its own `NOTES.md` and the root `.gitignore` both say the images
come "under their own licences" and neither names one. Checked live,
2026-08-27:

- `huggingface.co/datasets/microsoft/cats_vs_dogs` (retrieved 2026-08-27):
  the dataset card states **"License: unknown."**
- `huggingface.co/datasets/rafaelpadilla/coco2017` (retrieved 2026-08-27):
  "Licensing Information — the annotations in this dataset belong to the
  COCO Consortium and are licensed under a Creative Commons Attribution 4.0
  License." That licenses the **bounding boxes**, not the photographs. COCO's
  images are individually Flickr-sourced, each under its original uploader's
  own licence — this dataset card grants nothing over the pixels.

**What this does and does not permit.** Neither page grants a documented
right to redistribute or publish the underlying photographs. Nothing here
says the images cannot be used this way either — "unknown" and "annotations
only" are gaps, not prohibitions, and this project has not researched
per-image Flickr licences to close them either way.

**What actually depends on it.** The set is never committed
(`fixtures/reference-photos/*.jpg` is gitignored) and never ships inside the
app — it exists on a dev machine to tune one confidence threshold and produce
accuracy tables like `05-vision-plugin/NOTES.md`'s "18 of 20 cats, 5 of 5
dogs, 0.70 mean confidence." Nobody outside the project has seen an image
from it. So this is a question about **evidence, not distribution**: the
numbers themselves — 18/20, 0.70, the reference-set counts — are free to
quote anywhere, because a number carries no licence. The images that
produced them are a different matter. The trigger is specific: the day one
of these photographs, or a crop/screenshot containing one, appears in
anything shown outside this project — an investor deck, a publisher pitch,
a blog post, an App Store screenshot, a support ticket — the licence
question stops being theoretical and needs a real answer, per image if
necessary, or the affected image swapped for one with clean provenance.

**Blocks nothing today.** No image has left the project; the numbers built
on the set can be quoted freely. This is a gate on *future* external use of
the images themselves, recorded now so it isn't rediscovered under time
pressure later.

Cross-referenced from `50-photo/01-reference-photo-set/NOTES.md`.
