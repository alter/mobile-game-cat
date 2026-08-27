# Built and measured, 2026-08-26

`Assets/Plugins/iOS/CatPhoto.swift` plus the C# wrapper `Assets/Shell/CatPhoto.cs`.

## The two numbers everything follows from

Image cost is `ceil(w/28)·ceil(h/28)` visual tokens, so 512×512 is 361 tokens;
accuracy falls off below roughly 200 px on a side
(`knowledge/vision-model/01-traits-strict-json.md`). Hence 512 as the output
side, 200 px as the floor a crop may not go under, and 200 KB before base64,
which inflates by about a third.

## Against the VERIFY list, on real data

Run over every image the Vision stage accepted — 27 of the 41, cats plus the
blurry, multi and screen-shot cats it also recognised:

| check | result |
|---|---|
| every output exactly 512×512 | **27/27** |
| every output under 200 KB pre-base64 | **27/27** — 37 KB min, 85 KB median, 138 KB max |
| the under-200 px guard triggers and widens | **12 of 27** |

The guard is not a corner case. Twelve of the twenty-seven boxes Vision
returned were under 200 px on a side — as small as 75 px (`multi_02`) and 90 px
(`multi_01`) — because a cat sitting in a corner of a large photo is a small
box. Upscaling those to 512 would invent detail the model then reads as real,
so the crop widens around its own centre instead and the cat simply sits
smaller in frame. `crop-check.jpg` shows the results.

## Two decisions worth stating

**Orientation is not touched here.** The image arriving has already been
through Vision, which was given the orientation explicitly; re-applying it
would turn the crop on its side. Orientation belongs to `05-vision-plugin` and
stays there.

**Quality is stepped, not fixed.** A busy photo at 0.9 can exceed 200 KB where
a plain one never will, so the encoder walks 0.9 → 0.4 and stops at the first
size that fits. Nothing in this set needed to go below 0.9 — the 138 KB
maximum is a first-try encode — but a photo of a patterned rug would.

## How this was checked without a device

The same Swift source compiles for macOS, so the probe under
`scratchpad/vision/cropprobe.swift` links `CatPhoto.swift` directly and runs it
over the reference set with the real boxes measured in `05-vision-plugin`. That
is the same code path the plugin exports, minus the `@_cdecl` bridge — which
`05` already exercised 41 times.

Not verified on hardware: `14-testflight`.

---

## status:done → in_progress, 2026-08-27

The OUTCOME artefact this task names is not there. What is missing, what does
exist, and why it matters: `tasks/AUDIT-2026-08-27.md`.
