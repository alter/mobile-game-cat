# VERIFY — 50-photo/07-crop-downscale

Verifier: an independent context that wrote none of `game/Assets/Core/TraitsRequest.cs`,
`game/Assets/Tests/Core/TraitsRequestTests.cs`, `game/Assets/Shell/CatPhoto.cs`,
`game/Assets/Plugins/iOS/CatPhoto.swift`, `game/Assets/View/CaptureScreen.cs`, or
`worker/src/index.ts`, and made no edits to any of them. Did NOT run the app in
Unity, in the iOS Simulator, or on a device; did NOT exercise the `@_cdecl`
P/Invoke bridge (`CatPhoto_prepare`/`CatPhoto_free`) or `Shell/CatPhoto.cs`
itself — both require Unity/iOS and are outside what can be checked from a
shell. Instead verified the underlying Swift crop/resize/encode algorithm
directly, by compiling `CatPhoto.swift` for macOS (its `prepare` function has
no `#if os(iOS)` guard) against a harness added to this task directory. Did
not re-run Apple's Vision framework to obtain real per-photo bounding boxes
(that belongs to `50-photo/05-vision-plugin`); used `box: nil` (whole image)
for the bulk check and a synthetic tiny box for the guard check instead.

## Per-item verdict

| # | Item | Verdict | Evidence |
|---|---|---|---|
| task.txt VERIFY 1 | crop/downscale run over the reference set | PASS (superset) | `verify-crop-check.main.swift` run over all 41 files in `fixtures/reference-photos/` (not just the ~20 "accepted" ones filtered by Vision — a strict superset), see reproduce log below |
| task.txt VERIFY 2 | every output exactly 512×512 and under 200 KB pre-base64 | PASS | harness output: `41/41 exactly 512x512, 41/41 under 200KB`, size range 37529–165628 bytes, all `< CatPhoto.maxBytes` (`game/Assets/Plugins/iOS/CatPhoto.swift:19`, `200*1024`) |
| task.txt VERIFY 3 | under-200px guard triggers on a tiny crop and widens rather than upscales | PASS | harness fed a synthetic 40×40 box (`CatPhoto.minCropSide` = 200, `CatPhoto.swift:18`); `CatPhoto.expand()` returned a 200×200 rect (not 40×40 upscaled), and `CatPhoto.prepare()` with that box still produced a 512×512 JPEG — see reproduce log |
| Base64 field present and correctly formed | PASS | `TraitsRequest.BuildJson` (`game/Assets/Core/TraitsRequest.cs:72,82`) — `Convert.ToBase64String`, round-tripped by `ARoundTrip_BytesInBase64OutDecodesBackToTheSameBytes` |
| Field names match what the Worker reads | PASS, checked first-hand | `worker/src/index.ts:77` `payload.image_base64`, `:86` `payload.media_type`, `:97` `payload.device_id` — all three literally present at those lines, matching `TraitsRequest.cs`'s emitted keys `image_base64`/`media_type`/`device_id` |
| Media type matches | PASS, checked first-hand | `worker/src/index.ts:32` `ALLOWED_MEDIA = new Set(["image/jpeg", "image/png"])` contains `"image/jpeg"`, which is `TraitsRequest.MediaType` (`TraitsRequest.cs:50`) and the only type `CatPhoto.swift`'s `UTType.jpeg` ever emits |
| "anonymous" device-id default matches | PASS, checked first-hand | `worker/src/index.ts:97-99` falls back to `"anonymous"` when `device_id` is missing/empty/non-string; `TraitsRequest.cs:78` (`AnonymousDeviceId = "anonymous"`) does the same |
| 200 KB pre-encode / 400 KB post-encode ceilings match | PASS, checked first-hand | `worker/src/index.ts:30` `const MAX_BODY_BYTES = 400 * 1024;` matches `TraitsRequest.MaxEncodedBytes` (`TraitsRequest.cs:47`); `TraitsRequest.MaxPreEncodeBytes` (200 KB, `TraitsRequest.cs:38`) matches `Shell/CatPhoto.cs:18` `MaxBytes` and `Plugins/iOS/CatPhoto.swift:19` `maxBytes` |
| `dotnet test` run, zero skipped | PASS | see raw output below: `137` total, `0` пропущено (skipped) |
| `AHostileDeviceIdCannotBreakTheJson` actually exercises escaping and would fail without it | PASS, mutation-tested | see mutation test below: baseline (unmutated copy) passes, mutated copy (control-char escaping removed) fails on exactly the `\u0007` assertion |
| Wired into production code, not only tests | PASS | `game/Assets/View/CaptureScreen.cs:196` calls `TraitsRequest.BuildJson(prepared, DeviceId)` inside `Handle()`, right after `Crop()` succeeds |
| OUTCOME met end-to-end on a device / in the shipping app | **NOT PROVEN** | see "What was not checked" |

## OUTCOME judgement

The OUTCOME — "a 512×512 JPEG, base64-encoded, under 200 KB before encoding,
ready to POST to /traits" — is genuinely met **as code**, on both halves,
each independently checked here rather than taken on the strength of the
other:

- the pixel/size half (`CatPhoto.swift`) was re-run by me, directly, on 41
  real photos plus a synthetic tiny-box case, not merely re-read from NOTES.md;
- the base64/envelope half (`TraitsRequest.cs`) was re-run by me via
  `dotnet test`, and its cross-language claims against `worker/src/index.ts`
  were checked by reading the cited lines myself, not taken from the C#
  comments' word;
- the two are wired together in `CaptureScreen.Handle()`, in shipping code,
  not just in a test fixture.

What is **not** proven, by me or by anything in the repository: this pipeline
running through the actual `@_cdecl` bridge, inside a compiled iOS build, on
a device or simulator. `Shell/CatPhoto.cs` returns `null` for every build
target except `UNITY_IOS && !UNITY_EDITOR` (`CatPhoto.cs:54-55`), so in the
Unity Editor `Crop()` always fails and `TraitsRequest.BuildJson` is never
reached at all — the only place the full pipeline has ever run is this
verification's macOS-compiled copy of the Swift algorithm and the C# unit
tests, run separately. That gap is the same one flagged for every other
NATIVE task in `tasks/AUDIT-2026-08-27.md` (items 9–11): it resolves at
`60-shell-build/14-testflight`, not here. The task's own VERIFY items (1–3)
do not ask for a device run, and I judge them met on that basis — but the
device gap is real and unclosed, and I am not passing it on the strength of
the parts that are.

## How to reproduce

From a clean checkout of this repository, with .NET 8 SDK and Xcode command
line tools (`swiftc`) installed, nothing exported by hand:

```sh
# 1. C# tests — the base64 envelope, the cross-language checks against
#    worker/src/index.ts, and the escaping test.
dotnet test build/core-tests/core-tests.csproj -v q --nologo
# Expected tail line:
# Пройден!   : не пройдено     0, пройдено   137, пропущено     0, всего   137, ...

# 2. Swift crop/resize/encode algorithm — compiled directly from the repo's
#    own game/Assets/Plugins/iOS/CatPhoto.swift, unmodified, against the
#    harness saved in this task directory. (swiftc requires the driver file
#    to be literally named main.swift.)
cp tasks/50-photo/07-crop-downscale/verify-crop-check.main.swift /tmp/main.swift
swiftc /tmp/main.swift game/Assets/Plugins/iOS/CatPhoto.swift -o /tmp/cropcheck
/tmp/cropcheck fixtures/reference-photos
# Expected:
# whole-image pass: 41 files, 41/41 exactly 512x512, 41/41 under 200KB
# size range: min=37529 median=82513 max=165628
# guard: tiny box (10.0, 10.0, 40.0, 40.0) -> expand() (0.0, 0.0, 200.0, 200.0) — widened to >= 200px: true
# guard: prepare() with tiny box still outputs 512x512, 28009 bytes
# VERDICT guard triggers and widens: true
```

Raw output actually captured during this verification:

```
Тестовый запуск для .../core-tests.dll (.NETCoreApp,Version=v8.0)
Общее количество тестовых файлов (1), соответствующих указанному шаблону.

Пройден!   : не пройдено     0, пройдено   137, пропущено     0, всего   137, длительность 270 ms. - core-tests.dll (net8.0)
```

```
whole-image pass: 41 files, 41/41 exactly 512x512, 41/41 under 200KB
size range: min=37529 median=82513 max=165628
guard: tiny box (10.0, 10.0, 40.0, 40.0) -> expand() (0.0, 0.0, 200.0, 200.0) — widened to >= 200px: true
guard: prepare() with tiny box still outputs 512x512, 28009 bytes
VERDICT guard triggers and widens: true
```

### Mutation test (not scripted as a single command — done once, by hand, outside the repository)

To check that `AHostileDeviceIdCannotBreakTheJson` actually depends on the
escaping it claims to test, `game/Assets/Core/TraitsRequest.cs` and
`game/Assets/Tests/Core/TraitsRequestTests.cs` were copied to a scratch
directory **outside this repository** (never modified in place). In the copy,
`JsonString`'s `default:` branch in the copy of `TraitsRequest.cs` was changed
from escaping control characters (`if (c < ' ') sb.Append("\\u...")`) to
`sb.Append(c)` unconditionally — the escaping removed. A minimal throwaway
`.csproj` (NUnit + Test SDK, mirroring `build/core-tests/core-tests.csproj`)
was created next to the copies and run with
`dotnet test --filter AHostileDeviceIdCannotBreakTheJson`:

- **Baseline** (unmutated copy): `не пройдено 0, пройдено 1` — passes.
- **Mutated** (escaping removed): `не пройдено 1, пройдено 0` — fails, on
  exactly the assertion that a bare control character must appear as
  `\u0007`:
  ```
  a bare control character is not escaped, so the body is not JSON
  Assert.That(json, Does.Contain("\u0007"))
  Expected: String containing "\u0007"
  But was:  "{"image_base64":"...","media_type":"image/jpeg","device_id":"d\"1\\2\n3\t45"}"
  ```
  (the raw U+0007 byte is present unescaped in the actual body, confirmed by
  the failing assertion's own message).

`git status --short` on `game/Assets/Core/TraitsRequest.cs` and
`game/Assets/Tests/Core/TraitsRequestTests.cs` was empty throughout —
the repository's own files were never touched.

## What was not checked

- No Unity Editor, iOS Simulator, or physical device run. `Shell/CatPhoto.cs`
  and the `@_cdecl` P/Invoke bridge (`CatPhoto_prepare`/`CatPhoto_free`) were
  never executed — only the pure-Swift `CatPhoto.prepare` algorithm underneath
  them, compiled for macOS. That algorithm has no `#if os(iOS)` guard, but the
  marshaling code that calls it from C# does, and that marshaling code has
  never run in this or any prior verification of this task.
- No real Vision-measured bounding boxes were used. Task VERIFY item 1 asks
  for "the 20 accepted-cat images" with their Vision boxes; I ran `box: nil`
  (whole image) over all 41 reference photos instead, plus one synthetic
  40×40 box for the guard case. This exercises the same crop/resize/encode
  code paths (including `square()`, since none of the source photos are
  already square) but is not a literal re-run of `05-vision-plugin`'s output.
- `CaptureScreen.AskWorker` is still an unassigned delegate (confirmed:
  `grep -n "AskWorker =" game/Assets/View/CaptureScreen.cs` finds no
  assignment) — no HTTP call to the Worker has ever happened from the game.
  That is correctly out of this task's OUTCOME (it belongs to
  `50-photo/08-capture-screen`, `status:in_progress`), not a gap in this
  verification.
- `worker/test/traits.test.ts` itself was not run; only `worker/src/index.ts`
  was read directly to cross-check field names, media type, defaults and
  ceilings.
- No performance/timing measurement of the JPEG quality-stepping loop
  (`encodeJPEG`, up to 6 re-encodes) was made; only correctness of the final
  output.
