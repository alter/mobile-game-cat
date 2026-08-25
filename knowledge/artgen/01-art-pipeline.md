# Pipeline for generating 2D item art outside the engine

Date material collected: 2026-08-24.

## In brief

- Google Imagen 4 is already discontinued: the official documentation states "This model is deprecated and will be shut down on August 17, 2026" — as of the collection date (August 24, 2026) the model is most likely unavailable; Google names the Nano Banana family via the Gemini API as the replacement. [1]
- Current image-generation models via the Gemini API as of August 24, 2026: Gemini 2.5 Flash Image (Nano Banana), Gemini 3.1 Flash Image (Nano Banana 2), Gemini 3.1 Flash Lite Image (Nano Banana 2 Lite), Gemini 3 Pro Image (Nano Banana Pro) — pricing and the batch rate are confirmed for all of them. [2]
- OpenAI has the `gpt-image-2`, `gpt-image-1.5`, `gpt-image-1-mini`, `gpt-image-1` line, priced per token (not directly per image), with a `background: "transparent"` parameter for a transparent background and no seed parameter. [3][4]
- Black Forest Labs (FLUX) has confirmed official pricing for every model in the FLUX.2/FLUX.1/Kontext line, per megapixel or a flat price per image; FLUX.2 supports reference images (up to 8 via the API), with no explicit confirmation of transparent background or seed found on the open pages. [5][6]
- No deterministic `seed` parameter is confirmed as a publicly documented capability for any of the verified models — where this could not be verified, it is honestly marked "no data found."
- A single, consistent style across a set is kept not by one setting but by a combination of techniques: a fixed prompt template + a reference image + (where possible) a LoRA trained on 20–30 reference sprites; a plain seed closes no more than "~80%" of the problem, per practitioner experience. [7]
- `rembg` (version 2.0.81 on PyPI, 24,410 stars on GitHub, updated August 18, 2026) and `backgroundremover` (0.4.5, 8020 stars, updated July 10, 2026) remain working background-removal tools in Python. [8][9]
- Pillow provides a ready set of tools for auto-cropping and alignment: `Image.getbbox(alpha_only=True)`, `Image.crop(box)`, `Image.thumbnail(size)`, `Image.paste(im, box, mask)`. [10]
- For Unity, what matters: power-of-two texture size for the platform, a single consistent PPU, 2–4 px padding in the atlas, ASTC/ETC2 compression on mobile, Sprite Atlas to reduce the number of draw calls. [11][12]
- Recoloring a layered 2D cat is done via color masks and a single shader (Replace Color / Color Mask in Shader Graph, or a hand-written HLSL with RGB masks), not via a separate texture for each coat variant. [13][14][15]

## Image-generation models via API (August 2026)

### OpenAI: the gpt-image line

Per the official guide (developers.openai.com, verified 2026-08-24), the current models are `gpt-image-2`, `gpt-image-1.5`, `gpt-image-1`, `gpt-image-1-mini`; for the Responses API, the call goes through a model like `gpt-5.6`, which calls the image-generation tool. [3]

Supported sizes (`size`): `1024x1024`, `1536x1024`, `1024x1536`, `2048x2048`, `2048x1152`, `2160x3840`, `3840x2160`, `auto`. Limits: "max edge size ≤ 3840px," "both edges a multiple of 16px," "long-to-short side ratio ≤ 3:1." [3]

Transparent background is officially confirmed: set `background: "transparent"`; works with the `png` and `webp` formats, but not `jpeg`. [3]

No batch mode as a separate API for images was found in the guide; the `n` parameter only "generates multiple images at once in a single request" — this is not the same as an actual batch job. A separate `seed` parameter is not mentioned in the documentation; moreover, the documentation itself acknowledges a consistency limitation: "the model may sometimes struggle to maintain consistency" for recurring characters. [3]

Other parameters: `quality` — `"low"`, `"medium"`, `"high"`, `"auto"`; `format` — `"png"` (default), `"jpeg"`, `"webp"`; `output_compression` — 0–100% for JPEG/WebP; `moderation` — `"auto"` or `"low"`. [3]

Official pricing (developers.openai.com/api/docs/pricing, verified 2026-08-24) is given per 1M tokens, not directly per image:

```
gpt-image-2:      input $8.00 / output $30.00 per 1M tokens
gpt-image-1.5:    input $8.00 / output $32.00 per 1M tokens
gpt-image-1-mini: input $2.50 / output $8.00  per 1M tokens
gpt-image-1:      input $10.00 / output $40.00 per 1M tokens
```
Batch mode (for text/token calls, not to be confused with the "image-generation batch" mentioned above) is priced at half. [4]

Official table of token cost per image by `quality`/`size` (for models in the line up to `gpt-image-2`):

| Quality | Square (1024×1024) | Portrait (1024×1536) | Landscape (1536×1024) |
|---|---|---|---|
| Low | 272 tokens | 408 tokens | 400 tokens |
| Medium | 1056 tokens | 1584 tokens | 1568 tokens |
| High | 4160 tokens | 6240 tokens | 6208 tokens |

For `gpt-image-2`, no separate token-table breakdown was found in the open documentation — the calculation is offered via a calculator embedded in the guide. [3]

### Google: Imagen (being shut down) and the Nano Banana family

The official Gemini API documentation states plainly about Imagen: "This model is deprecated and will be shut down on August 17, 2026; migrate to Nano Banana for image generation." The material-collection date is August 24, 2026 — meaning the deadline has already passed: Imagen 4 cannot be relied on for a new project. [1]

While the model was active, the official pricing tiers were: Fast — $0.02, Standard — $0.04, Ultra — $0.06 per image; supported sizes were 1K and 2K (2K — Standard/Ultra only), aspect ratios 1:1, 3:4, 4:3, 9:16, 16:9, configured via `numberOfImages` (1–4), `imageSize`, `aspectRatio`, `personGeneration`. No seed, reference images, transparent background, or batch mode is described for Imagen in the documentation. [1]

The official replacement is the Nano Banana line via the Gemini API. Official pricing (ai.google.dev/gemini-api/docs/pricing, verified 2026-08-24):

```
Gemini 2.5 Flash Image (Nano Banana):
  input $0.30 / 1M tokens
  output "$0.039 per image"
  batch: $0.15 / 1M input, $0.0195 per image output

Gemini 3.1 Flash Image (Nano Banana 2):
  input $0.50 / 1M tokens
  output $60 / 1M tokens ($0.045–0.151 per image, depending on resolution)
  batch: $0.25 / 1M input, $30 / 1M output

Gemini 3.1 Flash Lite Image (Nano Banana 2 Lite):
  input $0.25 / 1M tokens
  output $1.50 / 1M tokens ($0.0336 per image)
  batch: $0.125 / 1M input, $0.75 / 1M output

Gemini 3 Pro Image (Nano Banana Pro):
  input $2.00 / 1M tokens
  output $120 / 1M tokens ($0.134–0.24 per image)
  batch: $1.00 / 1M input, $6.00 / 1M output
```
[2]

Sizes: Nano Banana 2 Lite — 0.5K (512px) and 1K; Nano Banana 2 and Nano Banana Pro — 1K, 2K and 4K. [2]

Reference images are officially confirmed: the Nano Banana models support "up to 14 reference images" to preserve consistency of characters and objects — the limit varies by model tier. [2]

Batch API is confirmed separately: "All of the image generation capabilities described on this page can also be run as batch jobs using the Batch API," with the caveat "higher rate limits in exchange for a turnaround of up to 24 hours." [2]

Transparent background and a `seed` parameter are not mentioned in the verified documentation — no data found.

### Black Forest Labs: the FLUX line

The official pricing page bfl.ai/pricing (verified 2026-08-24, data extracted from the JSON markup embedded in the page) gives exact pricing per model. Some models are priced per megapixel (the first megapixel costs more than subsequent ones), others at a flat price per image:

```
FLUX.2 [max]:          $0.07 for the first Mp, $0.03 for each following Mp
                       reference images: $0.03 / Mp
FLUX.2 [pro]:          $0.03 for the first Mp, $0.015 for each following Mp
                       reference images: $0.015 / Mp
FLUX.2 [klein] 9B:     $0.015 for the first Mp, $0.002 for each following Mp
                       reference images: $0.002 / Mp
FLUX.2 [klein] 4B:     $0.014 for the first Mp, $0.001 for each following Mp
                       reference images: $0.001 / Mp
FLUX.2 [flex]:         flat price $0.05 / Mp

FLUX.1 Kontext [max]:  $0.08 per image
FLUX.1 Kontext [pro]:  $0.04 per image
FLUX 1.1 [pro] Ultra:  $0.06 per image
FLUX 1.1 [pro]:        $0.04 per image
FLUX.1 [pro]:          $0.05 per image
FLUX.1 [dev]:          $0.025 per image
FLUX.1 Fill [pro]:     $0.05 per image
```
The megapixel-counting rule is given as text on the page itself: "for pricing, resolution is always rounded up to the next megapixel, separately for each reference image and for the generated image" and "1 megapixel is counted as 1024x1024 pixels." [5]

Reference images (for style/character consistency) are officially confirmed by the FLUX.2 documentation (docs.bfl.ai/flux_2, verified 2026-08-24): the limit depends on the model — "[klein]: Recommended max 6," "[max] / [pro] / [flex]: Up to 8 (API), 10 (playground)." [6]

Transparent output background, a `seed` parameter, and a separate discounted batch endpoint are not described in the verified official sources — no data found. The pricing page only mentions general "volume discounts... for high-throughput workloads" under individual terms, which is not the same as a documented batch API. [5]

## Techniques for keeping a consistent style across a set

Practitioners agree that a consistent style is not one setting but a combination of techniques applied together, not separately.

**A fixed prompt template + seed do not close the whole gap.** One practitioner puts it this way: "Seed control and prompt templates only get you 80% of the way. Here's what closes the gap: Use a LoRA fine-tuned on your target art style" — and adds that "Even a small LoRA (4-8 rank) trained on 20-30 reference sprites dramatically improves consistency." [7]

**The cause of style desynchronization is called "style drift"**: "generating the same character twice can yield two completely different art styles," treated with "a combination of fixed seeds, detailed style prompts, and a reusable prompt template." [7]

**Reference-image scheme**: first, one reference image of the character/style is generated, then it is passed as a reference into every subsequent call together with the same seed and the same prompt template — "using the same seed value, same style settings, and same prompt structure across all generations for maximum consistency." [7]

**An "art bible" before the first API call**: one source advises explicitly fixing in text, before generating a set, the camera, palette, light direction, scale, tile size, and material rules — "lock the camera, palette, light direction, scale, tile size, and material rules before generating batches" — because "if AI changes the camera by 5-10 degrees between generations, the set feels broken." [7]

**Generating as a sprite sheet instead of one item at a time**: modern multimodal models understand explicit frame-grid instructions in a single prompt, for example "Create a sprite sheet of the character running, 8 frames in 2 rows on grey background, side view, consistent proportions" — this produces one wide sheet that is then sliced up in the engine, instead of N independent calls with the risk of style drifting apart between frames. [7]

**Batching and picking the best**: "generate 4 images per pose with consecutive seeds and pick the best" — faster than regenerating one image at a time. [7]

**ControlNet** (for Stable Diffusion-based models) is called "the king of consistency and control" — it lets you hold a pose/structure from a reference image, depth map, or pose map while generating sheets and turnarounds. [7]

A practical reminder from the same source: a generated PNG is not yet a game asset, "a generated PNG is not a game asset, just a picture of one"; slicing the sheet into frames, the pivot point, collision, and the animator are a separate mandatory step after generation. [7]

## Background removal and transparency preparation in Python

### rembg

Current version on PyPI is 2.0.81 (verified 2026-08-24, `pip index versions rembg`); on GitHub — 24,410 stars, last push 2026-08-18 (data from the GitHub API). License — MIT. [8]

Supported models include `u2net`, `u2netp`, `isnet-general-use`, `isnet-anime`, the `birefnet-general` family and its variants, plus the cloud default model `bria-rmbg` (~1.02 GB, for high quality). For hair in portraits the documentation separately recommends `birefnet-portrait` with the color-decontamination flag or alpha matting. Python version requirement: `>=3.11, <3.14`. The cloud API variant has a 20 MB upload limit. [8]

Usage example from the README:
```python
from rembg import remove
from PIL import Image

input_img = Image.open('input.png')
output_img = remove(input_img)
output_img.save('output.png')
```
[8]

### backgroundremover

Version on PyPI — 0.4.5 (verified 2026-08-24); on GitHub — 8020 stars, last push 2026-07-10, repository not archived. [9]

### Pillow: content-aware cropping, uniform sizing, alignment

The official Pillow documentation (verified 2026-08-24) gives exact signatures:

```python
Image.getbbox(*, alpha_only: bool = True) -> tuple[int, int, int, int] | None
```
Computes the bounding box of non-zero (non-transparent, if `alpha_only=True`) areas — i.e., the main tool for auto-cropping a sprite to its content. Returns `None` if the image is empty. [10]

```python
Image.crop(box: tuple[float, float, float, float] | None = None) -> Image
```
Crops to the `(left, upper, right, lower)` rectangle in pixels. As of Pillow 3.4.0 the operation is no longer lazy. [10]

```python
im.thumbnail(size)  # e.g., size = (128, 128)
im.save(file + ".thumbnail", "JPEG")
```
Shrinks the image to `size` while preserving proportions — suitable for bringing a set to a uniform "preview" size before atlasing. [10]

```python
Image.paste(
    im: Image | str | float | tuple,
    box: Image | tuple[int, int, int, int] | tuple[int, int] | None = None,
    mask: Image | None = None
) -> None
```
Pastes an image or a color fill onto a canvas of the desired final size; `mask` controls the transparency of the pasted area — used to align cropped sprites centered on a canvas of a fixed size. [10]

A typical chain for one item: `getbbox()` → `crop(bbox)` → create an empty canvas of a fixed size (`Image.new("RGBA", size, (0,0,0,0))`) → `paste(cropped, offset, cropped)`, where `offset` is computed to center the cropped content.

## Preparing sprites for Unity

The official Unity blog and documentation agree on several rules. Textures should ideally be brought to a power of two on a side: "ideally be powers of two on each side, as this ensures hardware can efficiently compress images" — width and height need not match. The `Max Size` setting can and should be set separately per platform ("Import Settings allow you to define a Max Size and other compression settings per platform"), with ASTC compression (best quality/size balance on modern GPUs) or ETC2 (broader compatibility) on mobile. [11]

For sheets sliced automatically, padding is needed between sprites: "Unity's texture sampling does need... proper padding is needed specifically to avoid visual glitches," unlike some other engines. [11]

Pixels Per Unit (PPU) must be consistent across the whole project: "Consistent Pixels Per Unit (PPU) across all related assets is paramount for ensuring uniform scaling and avoiding visual inconsistencies," with typical values of 16/32/64 depending on the art's scale; the default value is 100. [11]

Sprite Atlas is Unity's official mechanism for packing multiple sprites into a shared texture to reduce the number of draw calls on mobile devices. The official Unity recommendation: "ideally all or most sprites that are active in the Scene should belong to the same Atlas," and it's also worth "split[ting] Sprite Textures into multiple smaller Atlases according to their common usage." Empty space between packed textures reduces the resulting atlas size and is checked via the Pack Preview panel in the inspector. If `Max Texture Size` in the platform-specific overrides is smaller than the atlas's current dimensions, Unity shrinks the packed texture automatically. [12]

One independent (not official Unity) mobile-device performance measurement: a scene with ~120 unique sprite textures on a mid-range Android device held 38 fps at 6.2 ms of CPU render time; after packing into a single 2048×2048 atlas, the same scene held a stable 60 fps at 1.4 ms of CPU time, with the same art, shaders, and scene graph. These are figures from a third-party source, not official Unity documentation — given with the source noted. [16]

Final checklist for a 2D mobile pipeline: texture type — `Sprite (2D and UI)`; for pixel art — `Point (No Filter)` filtering; a single consistent PPU project-wide; 2–4 px padding between sprites in atlases; `Max Size` — a power of two for the target platform; ASTC/ETC2 compression on mobile; grouping sprites active in the scene into shared Sprite Atlases; per-platform overrides to reduce texture size on mobile devices. [11][12]

## Assembling the cat from layers: recoloring via masks and a shader

The principle all the practical sources found agree on: instead of a separate texture for each cat coat, keep one desaturated (or grayscale) base texture and one or more black-and-white masks, and let a shader assemble the final color at render time.

### Via Shader Graph nodes (Cyanilux)

The simplest option is tinting via `Multiply`: a grayscale texture is multiplied by the material's color property and fed into Base Color/Albedo — "one of the simplest forms of adjusting colour is a tint using a Multiply node between a greyscale input texture and a given colour." [13]

More flexible nodes are `Replace Color` and `Color Mask`. Their corresponding HLSL functions (verbatim from the writeup):
```hlsl
float3 ReplaceColor(float3 In, float3 From, float3 To,
                    float Range, float Fuzziness){
    float Distance = distance(From, In);
    return lerp(To, In, saturate((Distance - Range) /
           max(Fuzziness, 1e-5)));
}

float3 ColorMask(float3 In, float3 MaskColor,
                 float Range, float Fuzziness){
    float Distance = distance(MaskColor, In);
    return saturate((Distance - Range) / max(Fuzziness, 1e-5));
}
```
An important caveat about mobile performance: methods that change UV coordinates at the fragment-shader stage create a "dependent texture read," which "can prevent GPU texture pre-fetches and increase latency" — i.e., it is more expensive precisely on mobile GPUs, and this is worth profiling on the target device rather than assuming theoretically. [13]

### Via color masks over a desaturated texture (4experience.co)

Step by step: (1) remove color from the albedo — either with a saturation node or (preferable for performance) a pre-prepared black-and-white texture with no color information; (2) prepare masks in advance for each recolorable area (for example, in Blender or any graphics editor); (3) for each mask apply a `Lerp` node, where the mask itself serves as the alpha value determining where the new color lands; (4) repeat for all additional masks and combine the results; (5) add configurable parameters for optional details and shared masks (logos/patterns) that require a separate UV mapping. [14]

### Via an explicit RGB mask channel in a custom shader (staraban.com)

A verbatim example of a ShaderLab shader with three independent colors, each tied to its own mask-texture channel (R/G/B):
```glsl
Shader "Particles/ColorTint" {
Properties {
_MainTex ("Particle Texture", 2D) = "white" {}
_TintColorRed ("Tint Color Red", Color) = (0.5,0.5,0.5,0.5)
_TintColorGreen ("Tint Color Green", Color) = (0.5,0.5,0.5,0.5)
_TintColorBlue ("Tint Color Blue", Color) = (0.5,0.5,0.5,0.5)
}
```
Fragment shader:
```glsl
fixed4 frag (v2f i) : COLOR
{
    float4 baseColor = tex2D(_MainTex, i.texcoord);
    float alpha = baseColor.a;
    baseColor = alpha * (baseColor.r * _TintColorRed +
                        baseColor.g * _TintColorGreen +
                        baseColor.b * _TintColorBlue);
    baseColor.a = 1.0f - step(alpha, 0.1);
    return baseColor;
}
```
And the controlling C# script that assigns colors to the material:
```csharp
public class Player : MonoBehaviour {
    public Transform head;
    public Transform body;
    public Color HairColor, EyeColor, SkinColor, BodyColor;

    void ColorTint() {
        if(head != null) {
            Material tempMaterial = new Material(
                head.GetComponent<Renderer>().sharedMaterial);
            tempMaterial.SetColor("_TintColorRed", SkinColor);
            tempMaterial.SetColor("_TintColorBlue", HairColor);
            tempMaterial.SetColor("_TintColorGreen", EyeColor);
            head.GetComponent<Renderer>().material = tempMaterial;
        }
    }
}
```
Applied to the cat: mask channel R — the base fur color, G — the color of pattern spots/stripes, B — for example, the color of the ears/paws; so one cat sprite and one mask give an arbitrary number of coats without growing the number of textures. [15]

A ready-made asset-level solution for pixel art is Mana Seed Shaders: recoloring happens by simply assigning a material to the sprite, "eliminating the need for extra palette swapped sheets," with preloaded palettes. [13]

## Batch runs in Python: organization

An official example from OpenAI (`openai-cookbook/examples/api_request_parallel_processor.py`) is described as a solution specifically for parallelizing API requests while respecting rate limits: "parallelizes requests to the OpenAI API while throttling to stay under rate limits, streaming requests from a file to avoid running out of memory for giant jobs, making requests concurrently to maximize throughput, throttling request and token usage, retrying failed requests up to a configurable number of times, and logging errors." Example invocation (for a different endpoint, but the structure carries over to image generation by swapping `request_url` and the request body):
```
python examples/api_request_parallel_processor.py \
  --requests_filepath ... \
  --request_url https://api.openai.com/v1/embeddings \
  --max_requests_per_minute 1500 \
  --max_tokens_per_minute 6250000 \
  --max_attempts 5
```
[17]

The officially recommended OpenAI retry pattern is the `tenacity.retry` decorator with `wait_random_exponential` (a randomized exponential delay, so retries from different jobs don't hit the API at the same moment). Verbatim example from the documentation:
```python
from openai import OpenAI
from tenacity import (
    retry,
    stop_after_attempt,
    wait_random_exponential,
)  # for exponential backoff

client = OpenAI()


@retry(wait=wait_random_exponential(min=1, max=60), stop=stop_after_attempt(6))
def completion_with_backoff(**kwargs):
    return client.completions.create(**kwargs)


completion_with_backoff(
    model="gpt-3.5-turbo-instruct",
    prompt="Once upon a time,",
)
```
The documentation separately notes: "Tenacity is a third-party tool" — OpenAI gives no guarantees about its reliability; it's a ready-made but third-party component. [18]

A practical organization for a batch run generating a set of game items, assembled from the official examples listed above and general Python concurrency recommendations:
1. The list of prompts is built ahead of time as a data structure (for example, a list of dicts: `{"id": "item_042_sword", "prompt": "...", "size": "1024x1024"}`), not built on the fly — the same requirement as "streaming requests from a file," just in reverse (results can be written out as soon as they are ready).
2. Concurrent calls are bounded by a semaphore or a pool (`asyncio.Semaphore`, or a per-minute request counter) — the same as in the OpenAI example, where throttling runs on `max_requests_per_minute`/`max_tokens_per_minute`.
3. Retries — via `tenacity` with exponential backoff and jitter, as in the official example above; failed requests are logged separately with the job's `id`, so they can be retried individually.
4. Saving — under a meaningful name that includes the item's identifier, not the API call's sequence number (`item_042_sword_v1.png`), so the result is easy to match against the original prompt when only the failed items are rerun.
5. For a "need 500+ images and it's not time-critical" scenario, instead of a synchronous request pool it's worth looking at the official Batch API (see the OpenAI and Google sections above) — per the official pages it gives a lower price in exchange for deferred (up to 24 hours at Google) execution, not for client-side parallelism.

## Sources

1. [Imagen — ai.google.dev/gemini-api/docs/imagen](https://ai.google.dev/gemini-api/docs/imagen)
2. [Gemini API pricing — ai.google.dev/gemini-api/docs/pricing](https://ai.google.dev/gemini-api/docs/pricing)
3. [Image generation guide — developers.openai.com/api/docs/guides/image-generation](https://developers.openai.com/api/docs/guides/image-generation)
4. [API pricing — developers.openai.com/api/docs/pricing](https://developers.openai.com/api/docs/pricing)
5. [FLUX API Pricing — bfl.ai/pricing](https://bfl.ai/pricing)
6. [FLUX.2 documentation — docs.bfl.ai/flux_2](https://docs.bfl.ai/flux_2)
7. [AI Game Asset Generation: How to Use AI to Build 2D Game Art Faster — Spritesheets.ai](https://www.spritesheets.ai/blog/ai-game-asset-generation-guide)
8. [danielgatis/rembg — GitHub](https://github.com/danielgatis/rembg)
9. [nadermx/backgroundremover — GitHub](https://github.com/nadermx/backgroundremover)
10. [Image module — Pillow documentation](https://pillow.readthedocs.io/en/stable/reference/Image.html)
11. [A Mobile Artist's Guide to Unity: Import Settings & Best Practices — Medium](https://medium.com/@chetan.balhara/a-mobile-artists-guide-to-unity-import-settings-best-practices-dcfdfa6c81a7)
12. [Optimize Sprite Atlas usage and size for improved performance — Unity Manual](https://docs.unity3d.com/6000.1/Documentation/Manual/sprite/atlas/workflow/optimize-sprite-atlas-usage-size-improved-performance.html)
13. [Swapping Colours — Cyanilux Shader Tutorials](https://www.cyanilux.com/tutorials/color-swap/)
14. [Tutorial: Asset color customization with shader graph and color masks — 4experience.co](https://4experience.co/asset-color-customization-with-shader-graph-and-color-masks/)
15. [2D Game Tutorial. Part 1. Character creating and tinting — staraban.com](https://staraban.com/en/2d-game-tutorial-part-1-simple-characters-with-cusomization-using-tint-shader/)
16. [Why Sprite Atlases Matter for Unity Mobile Games — I Love Sprites Blog](https://ilovesprites.com/blog/unity-sprite-atlas-mobile-games)
17. [api_request_parallel_processor.py — openai/openai-cookbook](https://github.com/openai/openai-cookbook/blob/main/examples/api_request_parallel_processor.py)
18. [Rate limits guide (retry with tenacity) — developers.openai.com/api/docs/guides/rate-limits](https://developers.openai.com/api/docs/guides/rate-limits)