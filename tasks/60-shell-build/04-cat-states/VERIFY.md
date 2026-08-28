# VERIFY — 60-shell-build/04-cat-states

Verifier: an independent agent context, 2026-08-28. It wrote none of
`View/CoatBuilder.cs`, none of `View/DebugGameView.cs`, none of
`Shell/GameBoot.cs`, none of `Shell/DeviceLog.cs`, none of the Core tests in
`Tests/Core/PlayerProgressTests.cs` or `Tests/Core/RoomPlanTests.cs`, none of the
three cat `.meta` files, and none of `NOTES.md`. It did not run Unity, did not
open the iOS simulator or an Android emulator, did not take either screenshot in
this directory, and did not touch any file outside this `VERIFY.md`. It read
files and ran the two test suites and the purity check.

## Verdict

`verify: pending` — not `passed`.

The iOS blank-screen claim holds up on every piece of evidence available without
running the engine (sections 1–4 below). But this task's own acceptance is
`VERIFY (QA)` and names two PlayMode checks:

1. state changes the instant the 4th and the 8th room close;
2. changing a room's `pile_count` does not shift when the transition fires.

Neither has been performed by anyone. The **rule** behind both is well tested in
Core (see "What passed", point 5). The **View** half — that `RenderCat()`
repaints at that moment — is not covered by any automated test, and neither
screenshot in this directory shows a transition: both show cat state 1. The
task's OUTCOME is "a cat whose appearance and behaviour change *exactly twice*",
and no artefact in this repository shows her changing.

## What passed

**1. `isReadable: 1`, and compression is off on every platform block.**
All three files are byte-identical in these fields. From
`game/Assets/Resources/Art/cat_1_short_base.png.meta`:

```
  isReadable: 1                       # line 24
```

and the five `platformSettings` entries, lines 69–134:

```
  - serializedVersion: 4
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 2048
    textureFormat: -1
    textureCompression: 0
    crunchedCompression: 0
    overridden: 0
```

repeated with `buildTarget: Standalone`, `Android`, `WebGL` and `iOS` — every one
carrying `textureCompression: 0` and `crunchedCompression: 0`. Verified identical
in `cat_2_short_base.png.meta` and `cat_3_short_base.png.meta` by grepping all
three. `textureCompression: 0` is `TextureImporterCompression.Uncompressed`, so
`GetPixels32` has a format it can read. Note also `overridden: 0` on all five:
the per-platform overrides are not enabled, so every platform inherits the
Default block — which is itself uncompressed, so the answer is the same either
way. The claim "uncompressed on every platform, not just the default one" is
true, and true twice over.

**2. The `isReadable` path is the one that runs.**
`game/Assets/View/CoatBuilder.cs:274-283`:

```csharp
if (source.isReadable)
{
    try
    {
        var direct = source.GetPixels32();
        LastReadWasBlit = false;
        return direct;
    }
```

With the meta above, the three silhouettes take this branch and never reach the
`RenderTexture.GetTemporary` / `Graphics.Blit` / `ReadPixels` block at `:301-322`
that the notes blame for the blank screen.

**3. `RenderCat`'s ownership logic is right.**
`game/Assets/View/DebugGameView.cs:276-287`:

```csharp
var baseArt = CoatBuilder.LoadBase(CatStateTraits, state);
if (baseArt == null) return; // art not shipped yet; portrait stays blank

var built = CoatBuilder.TryBuild(baseArt, CatStateTraits, state);
if (_catTexture != null) UnityEngine.Object.Destroy(_catTexture);
// Null when the coat could not be built: own nothing, and paint the
// untinted silhouette. `baseArt` is the Resources asset itself, so
// destroying it on the next state change would take the art out of
// the game for the rest of the run.
_catTexture = built;
_catTextureState = state;
Paint(_catPortrait, built != null ? built : baseArt);
```

`_catTexture` is only ever assigned `built`, and `built` comes from
`CoatBuilder.Build`, which returns a `new Texture2D(...)` it never caches
(`CoatBuilder.cs:130-138`). `baseArt` — the `Resources.Load` asset — is never
assigned to `_catTexture`, so the `Destroy` on the next state change cannot reach
it. When `TryBuild` returns null the field is set to null, the view owns nothing,
and the untinted `baseArt` is painted without being adopted. The claim in the
comment and in `CoatBuilder.cs:161-166` is correct as implemented.

The `_catTextureState == state` early return at `:274` records the state last
*attempted*, so a coat that fails does not retry a whole-silhouette pass on every
tap. That is also correct, and it is a change from a `_catTexture != null` guard
that would have.

**4. The screenshots show what `NOTES.md` claims.**
`ios-board-blank-before-fix.png` (1206×2622) is a uniform dark grey screen with
nothing on it but the Dynamic Island cutout — no tiles, no header, no shelf.
`ios-board-cat.png` (same dimensions) is the full board: cream background, header
"Room 3 of 12 · pile 3 of 3", "Items left: 36", 36 prop tiles in six rows, the
nine empty shelf slots at the bottom, and a small grey cat with green eyes in the
top-right corner. Before/after is exactly as described.

**5. The rule under the View is tested, and tested against the shipped data.**
`Tests/Core/PlayerProgressTests.cs:52-60` asserts 1 → 2 → 3 at four and eight
completed rooms. `Tests/Core/RoomPlanTests.cs:119-135` walks every shipped level
in order and asserts the number of state changes is exactly 2 — which is the
task's SCOPE claim ("regardless of how many levels those rooms took") checked
against the real 37-level curve rather than a hand-written one.
`Core/PlayerProgress.cs:150` is `CatState => CatStateFor(_roomsDone.Count)`, so
the state is a function of completed rooms and cannot be a function of a level
number.

**6. Suites, run from this checkout on 2026-08-28.**

- `dotnet test build/core-tests/core-tests.csproj -v q --nologo` →
  `Пройден!   : не пройдено     0, пройдено   195, пропущено     0, всего   195,
  длительность 307 ms.` — **195 passed, 0 failed**.
- `.venv/bin/python -m pytest tools/tests -q` → `170 passed in 13.43s`
  (`tools/` gives the same 170).
- `build/check-core-purity.sh` → `Core is engine-free: OK`.

`NOTES.md` quotes 189 and 160. Both suites have grown since it was written;
nothing fails. Stale, not wrong at the time.

## What failed — the 12 MB figure is understated

`CoatBuilder.cs:263-265`:

```
/// Reading a readable texture costs 4 MB of resident memory per cat
/// silhouette — 12 MB for the three — which the earlier version of this
/// comment argued was not worth "one pass at load".
```

The base arithmetic is right and the conclusion is right, but the number leaves
out two multipliers.

`sips -g pixelWidth -g pixelHeight` on all three files gives **1024×1024** each.
1024 is already a power of two, so `nPOTScale: 1` is a no-op and
`maxTextureSize: 2048` does not downscale. Uncompressed RGBA32 is
1024 × 1024 × 4 = **4,194,304 bytes = 4 MiB** per texture, 12 MiB for three. So
far the comment is exact.

What it omits:

- `enableMipMap: 1` (line 9 of every one of the three `.meta` files). A full mip
  chain adds one third: 4 MiB → **5.59 MiB** per texture, **16.8 MiB** for three.
- `isReadable: 1` makes Unity keep a CPU-addressable duplicate of the texture
  data in addition to the GPU copy. That is the documented cost of Read/Write,
  and it is the whole reason the flag was set. It roughly doubles the figure
  again, to on the order of **32–34 MiB**.

On top of that, `Build` allocates a further 1024×1024 RGBA32 with
`mipChain: false` (`CoatBuilder.cs:130`) — **4 MiB** — held live in
`_catTexture`, though only one at a time and it is destroyed on the next state
change.

So "12 MB" counts one copy of one texture level of three files. The real resident
cost is at least 16.8 MiB and plausibly about twice that. The decision the
sentence defends is not in question — a screen that does not draw costs more than
any of these numbers — but the number itself should say 16.8 MiB minimum, and
mipmaps on a texture drawn into a 56×56 portrait (`View/DebugGame.uss:210-215`)
are worth a second look on their own. Not changed here; this context verifies and
does not fix.

## What failed — the fallback can still be reached silently on a shipping build

Asked directly, the answer is **yes**.

`ReadPixels` (`CoatBuilder.cs:274-323`) has two ways into the blit, and neither
logs anything:

```csharp
    catch (Exception e)
    {
        LastReadNote = $"{source.name}: {e.GetType().Name}";
    }
}
else
{
    LastReadNote = $"{source.name}: not readable";
}

LastReadWasBlit = true;
```

No `Debug.LogWarning`, no `Debug.LogError`, no exception. `LastReadNote` and
`LastReadWasBlit` are static properties, and their only consumer is
`Shell/GameBoot.cs:191-192`, inside a `GeometryChangedEvent` callback that writes
one line to `boot-state.txt` in `Application.persistentDataPath`. That file is
not surfaced anywhere in the game and nobody reads it on a user's device.
`Shell/DeviceLog.cs:94-96` filters everything below error:

```csharp
private static void OnLog(string message, string stackTrace, LogType type)
{
    if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)
```

so the fallback never reaches `errors.txt` either. The mitigation the notes
describe — "`LastReadWasBlit` is now reported ... a path this consequential
should not be invisible" — makes it visible to a developer who goes looking in
`persistentDataPath` after the fact. It does not make it visible on a shipping
build, and it does not fail loudly.

**And there is a live second caller.** `MaskOf` (`CoatBuilder.cs:634-647`):

```csharp
var drawn = Resources.Load<Texture2D>($"Art/{baseName}_{maskName}");
if (drawn != null)
{
    var px = ReadPixels(drawn);
```

This is the runtime hook for the 27 hand-drawn masks of `40-art/04`, designed so
they can land one at a time with no code change. No such file exists today —
`ls game/Assets/Resources/Art/cat_*` returns only the three `_short_base.png`
files and their metas — so the path is currently dead. But Unity's default import
is `isReadable: 0`, and a mask dropped into `Resources/Art` by an artist will
have exactly that unless someone remembers. The first such file re-enables the
blit inside `OnEnable`, on the iOS simulator, silently, and reinstates the blank
screen this task spent a day finding. The whole point of `MaskOf` is that it
takes no code change to activate, which is also what makes it the trap.

A `Debug.LogError` on the blit branch, or a `.meta` check in the build, would
close this. Neither exists. Not added here.

## How to reproduce

From a clean state — fresh clone, nothing exported by hand:

```sh
git clone git@github.com:alter/mobile-game-cat.git
cd mobile-game-cat
git checkout dev

# 1. isReadable and compression on all three, every platform block
for f in game/Assets/Resources/Art/cat_*_short_base.png.meta; do
  echo "== $f"
  grep -n 'isReadable\|enableMipMap\|buildTarget\|textureCompression\|crunchedCompression\|overridden\|maxTextureSize:\|nPOTScale' "$f"
done
# -> isReadable: 1, enableMipMap: 1, and textureCompression: 0 under each of
#    DefaultTexturePlatform, Standalone, Android, WebGL, iOS

# 2. The dimensions the memory figure has to come from
for f in game/Assets/Resources/Art/cat_*_short_base.png; do
  sips -g pixelWidth -g pixelHeight "$f"
done
# -> 1024 x 1024 each. 1024*1024*4 = 4 MiB; x1.333 for mips = 5.59 MiB; x3 = 16.8 MiB

# 3. The silent fallback: two entries, no log on either
sed -n '274,325p' game/Assets/View/CoatBuilder.cs
sed -n '94,97p'   game/Assets/Shell/DeviceLog.cs
grep -rn 'LastReadWasBlit\|LastReadNote' game/Assets --include='*.cs'
# -> only consumer is Shell/GameBoot.cs:191-192, writing boot-state.txt

# 4. The second, currently-dead caller that would re-enable the blit
sed -n '634,647p' game/Assets/View/CoatBuilder.cs
ls game/Assets/Resources/Art/cat_*
# -> only the three *_short_base.png and their metas; no mask file exists yet

# 5. Ownership: _catTexture never holds the Resources asset
sed -n '266,289p' game/Assets/View/DebugGameView.cs

# 6. The rule the View reads, and its tests
sed -n '140,152p' game/Assets/Core/PlayerProgress.cs
sed -n '51,73p'   game/Assets/Tests/Core/PlayerProgressTests.cs
sed -n '118,136p' game/Assets/Tests/Core/RoomPlanTests.cs

# 7. Suites
dotnet test build/core-tests/core-tests.csproj -v q --nologo   # 195 passed, 0 failed
python3 -m venv .venv && .venv/bin/pip install -r tools/requirements.txt
.venv/bin/python -m pytest tools/tests -q                       # 170 passed
./build/check-core-purity.sh                                    # Core is engine-free: OK
```

The two QA checks in `task.txt` remain and need a run of the game:

- close the 4th room and the 8th and confirm the portrait changes at that
  instant, on the win card for that room's last pile, without pressing Next;
- change a room's pile count in the level data and confirm the transition still
  fires on the 4th and 8th *room*, not at a shifted level number.

`NOTES.md` for `06-win-screen` describes the save-crafting trick that reaches an
end-of-level screen without playing 36 taps by hand; the same trick reaches a
room-4 and room-8 close cheaply.

## What was not checked

- **The two QA checks in `task.txt`.** Not performed. This is the task's own
  acceptance and it is the reason for `pending`.
- **Unity was not run** — no compile, no PlayMode, no `-runTests` (forbidden,
  `AGENT-BRIEF.md:196`). That the code compiles is inferred from
  `ios-board-cat.png` existing, not from a build performed here.
- **The View is not covered by `dotnet test` at all.** The 195 Core tests cover
  `PlayerProgress.CatStateFor` and `RoomPlan`; nothing covers `RenderCat`,
  `CoatBuilder`, `ReadPixels`, or the `Finish()` repaint that makes the change
  visible in the room rather than only after Next.
- **Neither screenshot shows a state change.** `ios-board-cat.png` is "Room 3 of
  12", i.e. state 1. States 2 and 3 have never been photographed, and no artefact
  in this directory shows the cat differing between them. That the three
  silhouettes differ from each other was not checked at all — only that three
  files exist at 1024×1024.
- **Android was not checked for this task.** Both screenshots here are iOS.
  `MEMORY.md`'s standing rule is a screenshot from each platform per screen, and
  one exists only for `06-win-screen`. Whether the cat portrait renders on
  Android is inferred from the sibling task's Android win-screen shot, in which a
  cat is visible top-right — not from a board screenshot taken for this task.
- **The memory figures are arithmetic, not measurement.** 16.8 MiB and the
  ~2× read/write duplicate are derived from the file dimensions, the format
  implied by `textureCompression: 0`, and `enableMipMap: 1`. No Unity Memory
  Profiler capture was taken, and the exact resident total is **not
  established**.
- **Whether a real iPhone shares the simulator's blit fault** — unknown, as
  `NOTES.md` itself says. Nothing here narrows it.
- **`LoadBase`'s long-hair fallback** (`CoatBuilder.cs:99-104`) was read but not
  exercised; `CatTraits.Default` is short-haired, so it never fires on this path.
- **The `Skipped` / `nocat.txt` flag** was read, not exercised. It calls
  `File.Exists` on every `TryBuild`; the cost of that on a per-state-change path
  was not measured.
