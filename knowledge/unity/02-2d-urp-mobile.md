# 2D URP on mobile (iOS): URP Asset setup, Sprite Atlas, Sorting/batching, 2D lighting, Canvas, common mistakes

Date collected: 2026-08-24
Project stack version: Unity 6.3 LTS (6000.3.x), URP 2D Renderer, building for iOS.

## In brief

- On mobile it's worth disabling: **HDR**, **MSAA** ("Anti Aliasing"), and, absent a real need, **Opaque Texture** and **Depth Texture** (both create additional textures/passes and consume bandwidth), and in 2D Renderer Data — **Depth/Stencil Buffer**, if Sprite Mask isn't used. [docs.unity3d.com — universalrp-asset](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/universalrp-asset.html), [docs.unity3d.com — 2DRendererData-overview](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/2DRendererData-overview.html)
- For iOS the recommended texture compression format is **ASTC** (for devices with an A8 chip (2014) or newer); for older devices with A7, ASTC isn't supported in hardware and will be decompressed at runtime — the fallback in that case is ETC/ETC2. [docs.unity3d.com — texture-choose-format-by-platform](http://docs.unity3d.com/6000.3/Documentation/Manual/texture-choose-format-by-platform.html)
- **Sprite Atlas** — mandatory for batching: without an atlas, each unique sprite/texture is at minimum a separate draw call; the official documentation states directly that an atlas lets Unity make one draw call instead of several. [docs.unity3d.com — atlas-introduction](https://docs.unity3d.com/6000.4/Documentation/Manual/sprite/atlas/atlas-introduction.html)
- **Sorting Layers** and **Order in Layer** control the drawing order; sprites with the same material and the same sorting settings are batched together — changing material/texture/sorting within a "stack" of sprites breaks the batch. [docs.unity3d.com — 2d-renderer-sorting](https://docs.unity3d.com/6000.3/Documentation/Manual/2d-renderer-sorting.html)
- **2D lighting (Light2D)** in URP is not a cheap feature on mobile: per independent practical developer reports, a self-made GPU-based system (analogous to Light2D) can stably consume around 1-2 ms per frame even with no active light sources, and a Light2D + custom shadows combination in one report gave only about 6 fps on a phone before optimization. The official Unity documentation does not directly give numeric performance thresholds for Light2D. [github.com/simeonradivoev/Light2D](https://github.com/simeonradivoev/Light2D) (via WebSearch, not opened directly — note below), [gamba04.itch.io devlog](https://gamba04.itch.io/my-personal-devlog/devlog/145714/2d-mobile-optimized-lighting-system) (via WebSearch)
- For UI layout across different iPhone aspect ratios, **Canvas Scaler** is used in **Scale With Screen Size** mode with **Screen Match Mode** = "Match Width Or Height"; for notches/safe zone, the **Screen.safeArea** property is used, which returns a rectangle in pixels with the origin at the bottom-left corner. [docs.unity3d.com — script-CanvasScaler](https://docs.unity3d.com/Packages/com.unity.ugui@2.6/manual/script-CanvasScaler.html), [docs.unity3d.com — Screen-safeArea](https://docs.unity3d.com/ScriptReference/Screen-safeArea.html)
- The most frequently mentioned causes of FPS drops in 2D on mobile in developer reports: **overdraw from transparent/semi-transparent sprites** (fillrate-bound on mobile GPUs), too many draw calls due to non-atlased textures/materials, a full Canvas rebuild when a single UI element moves within a shared Canvas with static elements. [divillysausages.com — performance-tips-for-unity-2d-mobile](https://divillysausages.com/2016/01/21/performance-tips-for-unity-2d-mobile/)
- For Sprite Atlas on mobile, an explicit choice of compression format is recommended (None/Low/Normal/High Quality in the general dialog; for iOS, ASTC is separately available via platform override) and a deliberate choice of **Generate Mip Maps** — for a 2D game with a fixed or barely-scalable camera, mipmaps are usually unnecessary and waste memory. [docs.unity3d.com — sprite-atlas-reference](https://docs.unity3d.com/6000.3/Documentation/Manual/sprite/atlas/sprite-atlas-reference.html)

## URP Asset: what to turn off for performance on mobile

The Unity Manual page "Universal Renderer Asset" (URP Asset) was opened directly: [docs.unity3d.com — universalrp-asset](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/universalrp-asset.html)

**HDR:**
> "Enable this to allow rendering in High Dynamic Range (HDR) by default for every camera in your scene."
Disabling HDR saves performance on weak hardware by skipping HDR computations — for a flat 2D puzzle without bloom/tone mapping, HDR is usually not needed.

**Anti Aliasing (MSAA):**
> "Use Multisample Anti-aliasing by default for every Camera in your scene while rendering."
Recommendation — set to **Disabled**, to skip MSAA computations and reduce load on the mobile GPU. For a clean 2D scene with an orthographic camera, aliasing artifacts are usually not as noticeable as in 3D, which further reduces the value of MSAA here.

**Opaque Texture:**
Creates `_CameraOpaqueTexture`, needed for refraction/transparency effects that read the color of the already-rendered scene. The **Opaque Downsampling** parameter lets you choose None, 2x Bilinear, 4x Box, or 4x Bilinear, to reduce the load on bandwidth on mobile. If the project has no shaders that need `_CameraOpaqueTexture` (e.g., distortion effects), this option is worth turning off entirely.

**Depth Texture:**
> "Enables URP to create a `_CameraDepthTexture`" for all cameras — consumes memory/bandwidth on mobile. Needed only if effects that depend on scene depth are used (e.g., some post-processing effects, soft particles). In a 2D puzzle without such effects, the depth texture can be disabled.

**Render Scale:**
> "This slider scales the render target resolution...Use this when you want to render at a smaller resolution for performance reasons."
A tool for adjusting the final render resolution — when FPS drops on weak devices, Render Scale can be lowered below 1.0.

**Post-processing:**
The **Grading Mode**, **LUT Size**, and other post-processing settings directly affect performance on mobile; a higher LUT Size (default 32) has, per the documentation's wording, "potential cost of performance and memory use" — meaning lowering it saves both.

**Soft Shadows:**
Separately noted: soft shadows have "High impact on platforms that use tile-based rendering, such as mobile platforms and untethered XR platforms" — meaning on tile-based mobile GPUs (all modern iPhones) soft shadows are especially expensive. For a 2D game, shadows are generally not needed at all (except for specific sprite shadow effects via 2D Light Shadow Caster — see the section on 2D lighting).

## 2D Renderer Data (2D URP Renderer-specific settings)

In addition to the general URP Asset, a 2D project has a separate **2D Renderer Data** asset, which controls how 2D Lights are applied to sprites. The Unity Manual page "2D Renderer asset component reference for URP" was opened directly: [docs.unity3d.com — 2DRendererData-overview](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/2DRendererData-overview.html)

**HDR Emulation Scale:**
> "Sets the multiplier that Unity uses to emulate high-intensity lights on platforms that don't support HDR."
The value should be selected based on the maximum light intensity in the scene, decreasing it if color banding appears.

**Depth/Stencil Buffer:**
> "Enables Unity rendering to the depth/stencil buffer. Disabling this property can improve performance, especially on mobile platforms."
The official recommendation is to disable this parameter if the project doesn't use features that require a depth/stencil buffer (e.g., **Sprite Mask**). For a mobile 2D puzzle without masks, or with masks that don't require stencil, this is a direct source of performance savings.

**Camera Sorting Layer Texture:**
Captures the camera's color buffer at a certain sorting-layer depth and passes it to the shader as `CameraSortingLayerTexture` — needed only by custom shaders that require the color of the already-rendered part of the scene (e.g., refraction effects across sprites). If such shaders aren't used, **Disabled** can be chosen to avoid spending an extra render pass.

**Downsampling for Camera Sorting Layer Texture** — the same four options as for Opaque Texture in the main URP Asset: **None** (full resolution), **2x Bilinear** (half resolution with bilinear filtering), **4x Box** (quarter resolution, box filtering), **4x Bilinear** (quarter resolution, bilinear filtering) — reducing this texture's resolution directly saves bandwidth on mobile.

Practical conclusion: for a 2D puzzle without Sprite Mask, without custom shaders reading `CameraSortingLayerTexture`, and without a need for HDR lighting — all the parameters (Depth/Stencil Buffer, Camera Sorting Layer Texture) should be disabled/set to Disabled by default, enabling them only when a specific visual effect specifically requires it.

## Sprite Atlas: setup, texture compression for iOS (ASTC), Max Size, mipmaps

### Why Sprite Atlas is needed

Official page "Sprite atlases" (opened via WebSearch content aggregation of docs.unity3d.com, version 6000.4 Manual — the base definition is not version-specific):
> It is stated that a sprite atlas combines multiple textures into one, and Unity creates only one draw call for all the sprites in it, which improves performance.
[docs.unity3d.com — atlas-introduction](https://docs.unity3d.com/6000.4/Documentation/Manual/sprite/atlas/atlas-introduction.html)

The "Create a sprite atlas" page for version 6000.3 was opened directly and describes the basic workflow of creating an atlas and adding sprites to it, including using Custom Outline in the Sprite Editor for tighter packing. [docs.unity3d.com — create-sprite-atlas](https://docs.unity3d.com/6000.3/Documentation/Manual/sprite/atlas/create-sprite-atlas.html)

### Sprite Atlas Inspector settings

The "Sprite Atlas Inspector window reference" page for 6000.3 was opened directly: [docs.unity3d.com — sprite-atlas-reference](https://docs.unity3d.com/6000.3/Documentation/Manual/sprite/atlas/sprite-atlas-reference.html)

**Max Texture Size:**
> "Sets the maximum dimensions of the sprite atlas in pixels."
If the total size of the sprites is smaller than the specified value, Unity uses the minimum required size. A separate warning for variant sprite atlases: don't use a Max Texture Size with a scale value less than 0.25, otherwise "Unity compresses the sprites and textures twice" — double compression degrades quality.

**Compression (compression format):**
The page explicitly lists only the general quality levels:
- "**None**: Don't compress the sprite atlas."
- "**Low Quality**: Compresses the sprite atlas using a low-quality texture format."
- "**Normal Quality**: Compresses the sprite atlas using a standard texture format."
- "**High Quality**: Compresses the sprite atlas using a high-quality texture format."
No explicit mention of ASTC specifically was found on this page (general Sprite Atlas settings) — ASTC as a specific format for iOS is configured via **platform-specific override tabs** ("These tabs let you override the settings in the **Default** tab for specific platforms", allowing you to override Max Texture Size, Format, and compressor quality for a specific platform, e.g. iOS) combined with the platform's general texture compression settings (see below).

**Generate Mip Maps:**
> Creates mip levels for the atlas texture; the relevance of this parameter grows when using anisotropic filtering together with certain filter modes.
There's no direct recommendation to "enable/disable for 2D" on the Sprite Atlas Inspector page. Practical conclusion (not a quote, the agent's conclusion): for a 2D game with an orthographic camera without significant camera zoom in/out relative to sprites, mipmaps are almost always unnecessary — they don't improve quality (a sprite doesn't scale into the distance like a 3D texture on a far surface) and they waste extra memory (roughly a third more per the independent sources below, compared to disabled mipmaps).

### ASTC as the format for iOS

The "Choose a GPU texture format by platform" page was opened directly: [docs.unity3d.com — texture-choose-format-by-platform](http://docs.unity3d.com/6000.3/Documentation/Manual/texture-choose-format-by-platform.html)
> "For Apple devices that use the A8 chip (2014) or above, ASTC is the recommended texture format for RGB and RGBA textures."
ASTC gives a flexible quality/size tradeoff — from 8 bits/pixel (4x4 blocks) to 0.89 bits/pixel (12x12 blocks). For older devices with the A7 chip, ASTC isn't supported in hardware and will be decompressed at runtime — in that case the fallback is ETC/ETC2, though they give less flexible quality control.

### General recommendation on mipmaps and memory (independent source, opened directly)

A practical breakdown of mobile 2D performance (divillysausages.com) recommends:
> "Even Half Res on mobile is mostly unnoticable, and you'll gain about 75% memory" — regarding reducing texture resolution.
> Disabling mipmaps, if the texture isn't scaled down, saves "approximately 33% memory" per the article author's estimate.
[divillysausages.com — performance-tips-for-unity-2d-mobile](https://divillysausages.com/2016/01/21/performance-tips-for-unity-2d-mobile/) — this is an individual developer's estimate, not official Unity documentation; the specific percentages are not confirmed by an official source, marked as a practitioner's opinion, not a fact from a primary Unity source.

## Pixels Per Unit, Sorting Layers, Sorting Group, draw order, and batching

### Pixels Per Unit (PPU)

The "Sprite (2D and UI) texture Import Settings window reference" page for 6000.3 was opened directly: [docs.unity3d.com — texture-type-sprite](https://docs.unity3d.com/6000.3/Documentation/Manual/texture-type-sprite.html)
> "The number of pixels of width/height in the Sprite image that correspond to one distance unit in world space."
The official page doesn't give a single "correct" recommendation for a numeric PPU value — it depends on the project's art pipeline. An important practical note (not from an official Unity source, from an independent breakdown): when using physics, the PPU value directly affects the accuracy of collider contacts due to the default contact offset, so for physically active objects, PPU is often chosen to equal the sprite's native pixel size, rather than a round number like 100.

### Sorting Layers and Order in Layer

The "Change the sorting order of 2D GameObjects" page for 6000.3 was opened directly: [docs.unity3d.com — 2d-renderer-sorting](https://docs.unity3d.com/6000.3/Documentation/Manual/2d-renderer-sorting.html)
> "All 2D GameObjects are on the **Default** sorting layer" by default; additional Sorting Layers are created via Edit > Project Settings > Tags and Layers.
> "Unity renders the layers in order from top to bottom."
> "Unity renders sublayers in numerical order, so lower values render behind higher values. For example, a sprite with an **Order in Layer** value of –1 renders behind a sprite with an **Order in Layer** value of 3."

### Sorting Group

Per WebSearch aggregation of the official Unity documentation ("Sorting group reference" pages for various versions, not separately verified via WebFetch by this agent — marked as not directly verified): Sorting Group is a component that groups multiple Renderers under a common root for combined sorting; all Renderers within one Sorting Group use a shared Sorting Layer, Order in Layer, and Distance to Camera relative to the camera. A typical use case is a composite 2D character/object made of multiple sprites that should sort as a single unit relative to the rest of the scene. The component is added via Component > Rendering > Sorting Group on the root GameObject of the hierarchy.
[docs.unity3d.com — sorting-group-reference (6000.0)](https://docs.unity3d.com/6000.0/Documentation/Manual/sprite/sorting-group/sorting-group-reference.html)

### How not to break batching

The official Unity documentation (general, not 2D-specific) describes the principle: renderers with the same material settings are sorted together for more efficient rendering, including dynamic batching — Unity tries to render several meshes as one, transforming vertices on the CPU and grouping similar vertices. For tiebreaking at the same sorting priority, the order in the Render Queue is used; 2D Renderers (Sprite Renderer, Tilemap Renderer, Sprite Shape Renderer) are mostly in the Transparent queue, with 2D materials defaulting to render queue = 3000. GameObjects without a Sorting Group render as a single layer and are still sorted by Sorting Layer/Order in Layer. (These statements were obtained via WebSearch aggregation across several versions of the official Unity Manual, including 2D Sorting pages from different years — this agent did not perform a direct WebFetch with an exact quote for specifically this paragraph; marked as partially verified.)

Practical rules for not breaking sprite batching (synthesized from directly opened sources — the Sprite Atlas and Sorting pages, and an independent practical breakdown from divillysausages.com):
- Sprites that are drawn next to each other and should batch into one draw call must use the same material/shader and come from the same Sprite Atlas — otherwise, even with adjacent Order in Layer values, they will end up in different batches due to the texture/material change.
- It's not worth packing absolutely all of a game's sprites into one Sprite Atlas indiscriminately — per a practical recommendation from an independent source (not Unity documentation, a practitioner's opinion), it's more effective to make separate atlases for what's actually used on screen at the same time (e.g., a separate atlas per scene/level), rather than one giant atlas for the whole game.
- It's not worth atlasing sprites that use different render states (e.g., one opaque, another with alpha clip, a third double-sided): such sprites will still end up in different draw calls regardless of the shared atlas, with no benefit — just more complex UV packing. [divillysausages.com — performance-tips-for-unity-2d-mobile](https://divillysausages.com/2016/01/21/performance-tips-for-unity-2d-mobile/)
- A rough guideline for the number of draw calls on mobile from a practical source (not official Unity documentation, a practitioner's opinion): "I'd say any more than 10 is probably too many, and more than 20 will give you problems." [divillysausages.com — performance-tips-for-unity-2d-mobile](https://divillysausages.com/2016/01/21/performance-tips-for-unity-2d-mobile/)

## URP 2D lighting: is it worth enabling on mobile, and its cost

Official Unity documentation (obtained via WebSearch aggregation of the "Introduction to 2D lighting in URP" page, not separately verified verbatim by this agent via a targeted WebFetch — marked as partially verified):
> 2D Lighting in URP is described as a set of "artist friendly" tools and runtime components for quickly lighting a 2D scene through Sprite Renderer and 2D Light components (analogous to 3D Light components); the system is optimized for mobile systems and for multiple platforms. Important: 2D lighting is **not physically-based**, unlike 3D lighting.
[docs.unity3d.com — Lights-2D-intro](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/Lights-2D-intro.html)

Technically, 2D light and shadow rendering happens in several passes: first, 2D shadow shapes are drawn into one or more shadow textures, then the color and shape of each 2D light are drawn into one or more light textures; a light render texture is created only if at least one 2D Light uses it (i.e., unused blend styles don't create extra textures).

Light source types: **2D Global Light** (lights all 2D GameObjects uniformly, without falloff, a maximum of one global light per blend style and sorting layer), **Spot Light**, **Sprite Light**, **Freeform Light** (Parametric Light was deprecated starting with URP 11 in favor of Freeform).

### Real cost on mobile: practical reports

Official Unity documentation does not provide numeric performance thresholds for Light2D. Independent practical sources (via WebSearch, not opened directly by this agent via a separate WebFetch — estimates from specific developers, not a fact from a primary Unity source):
- A developer who added Light2D to a mobile 2D game found no shadow-casting support "out of the box" and implemented custom shadows using colliders and shaders; the result worked well on PC but gave only **about 6 fps on a phone**. Profiling showed the problem was specifically in Light2D itself, which prompted the developer to write their own lightweight solution based on SpriteRenderer. [gamba04.itch.io devlog](https://gamba04.itch.io/my-personal-devlog/devlog/145714/2d-mobile-optimized-lighting-system)
- A third-party GPU-based 2D lighting system (not the built-in Light2D, but a community project with similar principles) creates from 6 to 10 draw calls depending on settings and consumes about **1-2 ms per frame on a Nexus 4** even with no active light sources in the frame (if the system isn't fully disabled). [github.com/simeonradivoev/Light2D](https://github.com/simeonradivoev/Light2D)

Practical conclusion: enabling URP 2D Lighting on mobile is worth it only with a real artistic need and mandatory profiling on target devices (primarily on the low/mid price segment, if the target audience includes it); for a puzzle where lighting isn't a key gameplay or artistic element, a sensible default strategy is to not use Light2D at all and simulate lighting via static shading/gradients baked into the sprites themselves, which has no runtime cost.

## Canvas and resolutions: layout for different iPhone aspect ratios, safe area

### Canvas Scaler

The "Canvas Scaler" page (com.unity.ugui package, version 2.6) was opened directly: [docs.unity3d.com — script-CanvasScaler](https://docs.unity3d.com/Packages/com.unity.ugui@2.6/manual/script-CanvasScaler.html)

Three **UI Scale Mode** modes:
- **Constant Pixel Size**: "Makes UI elements retain the same size in pixels regardless of screen size."
- **Scale With Screen Size**: "Makes UI elements bigger the bigger the screen is."
- **Constant Physical Size**: "Makes UI elements retain the same physical size regardless of screen size and resolution."

**Screen Match Mode** (for Scale With Screen Size mode), in particular "Match Width Or Height":
> "Scale the canvas area with the width as reference, the height as reference, or something in between."

Practical conclusion for layout across different iPhones (from the iPhone SE with a more "square" aspect ratio to the iPhone Pro Max with an elongated screen): use **Scale With Screen Size** + **Match Width Or Height** with a reference resolution characteristic of the main part of the audience, and a Match value chosen for what's more important to keep unchanged — the width of the game field (Match = 0, by width) or the height (Match = 1, by height); for a puzzle where the geometry of the game field matters, it more often makes sense to fix by the smaller side/width, so that the entire game field is guaranteed to fit on screen, with UI elements at the edges adapting to the extra space.

### Safe Area

The Scripting API page "Screen.safeArea" was opened directly: [docs.unity3d.com — Screen-safeArea](https://docs.unity3d.com/ScriptReference/Screen-safeArea.html)
> "Returns the safe area of the screen in pixels (Read Only)."

The property determines the part of the screen actually visible to the user, accounting for non-rectangular displays (notches, rounded corners) — i.e., directly relevant for iPhones with a notch/Dynamic Island. Technical details:
- The maximum safe area size equals the screen resolution: `Rect(0, 0, Screen.width, Screen.height)`.
- The origin is the bottom-left corner (unlike UI Toolkit, where the origin is the top-left corner).
- The safe area is specified relative to the Unity Player window, not the physical device.

Example coordinate conversion for UI Toolkit from the documentation (inverting the Y axis from a "bottom-left" coordinate system to "top-left"):
```
var safeAreaForUIToolkit = new Rect(Screen.safeArea.x, 
    Screen.height - Screen.safeArea.y, 
    Screen.safeArea.width, 
    Screen.safeArea.height);
```

Practical conclusion: critical interactive and text UI elements (buttons, score, timer) should be positioned with `Screen.safeArea` taken into account, not hard-pinned to the screen edges — otherwise on an iPhone with a notch/Dynamic Island or with rounded corners, these elements risk being partially invisible or untappable.

## Common 2D performance mistakes on mobile (per developer reports)

The main practical source for this section is an independent breakdown of mobile 2D performance, opened directly: [divillysausages.com — performance-tips-for-unity-2d-mobile](https://divillysausages.com/2016/01/21/performance-tips-for-unity-2d-mobile/). This is a practitioner's opinion, not official Unity documentation — the numbers and thresholds below should be treated as guidelines, not guaranteed values.

**Overdraw from transparent/semi-transparent sprites.** The official Unity position (a page on mobile optimization, version 560 Manual, obtained via WebSearch, not verified verbatim by this agent via a separate WebFetch — marked as partially verified) frames this as a fillrate problem: "on mobile you are essentially fillrate bound (fillrate = screen pixels × shader complexity × overdraw)", and overly complex shaders are the most frequent cause of problems; particles are separately mentioned: they should minimize overdraw and use the simplest possible shaders. Each extra layer of transparency over an already-drawn pixel is an extra write to the framebuffer that the GPU can't skip (unlike opaque geometry with early-Z).

**Too many draw calls from missing/incorrect use of Sprite Atlas.** Mixing non-atlased textures or using multiple atlases where one would do results in Unity being unable to combine the drawing of depth-adjacent sprites into a single batch.

**Full Canvas geometry rebuild on frequent UI changes.** Practical recommendation: split static and frequently-changing UI into different Canvases — "a single moving element forces entire canvas geometry rebuilding" (i.e., the movement of just one element within a Canvas triggers a geometry recalculation of the entire Canvas, including static elements, if they're in the same Canvas). Hence the practice of moving animated/frequently-updated elements (timers, counters, progress bars) into a separate Canvas from the static background UI.

**`raycastTarget` enabled on non-clickable UI elements.** Each enabled `raycastTarget` adds the object to the input raycaster's check — on elements that should never respond to a tap (decorative icons, panel backgrounds), it's recommended to explicitly disable `raycastTarget` to reduce raycaster overhead.

**Inefficient 2D physics.** Recommendation — don't move objects directly via Rigidbody2D position in a hot loop where unnecessary, and don't use many separate colliders per tile; instead pre-generate/merge collision meshes (composite collider) instead of a collider per individual tile.

**Mipmaps and excessive texture resolution.** As already noted in the Sprite Atlas section: excessive sprite resolution and unnecessarily enabled mipmaps directly increase video memory consumption (per the source's estimate — up to 75% memory savings when switching to half resolution where the quality loss is unnoticeable, and about 33% savings from disabling unused mipmaps), which on mobile devices with limited video memory shared with the system can trigger texture eviction and drops.

**Additional general recommendations from the same source** (a practitioner's opinion, not official documentation): explicitly set `Application.targetFrameRate` (e.g., to 60), enable vSync in the quality settings, use object pooling instead of Instantiate/Destroy in hot paths, cache frequently requested components instead of repeated `GetComponent` calls in Update.

## Sources

- [docs.unity3d.com — universalrp-asset](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/universalrp-asset.html) — URP Asset settings (HDR, MSAA, Opaque/Depth Texture, Render Scale, post-processing, Soft Shadows).
- [docs.unity3d.com — 2DRendererData-overview](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/2DRendererData-overview.html) — 2D Renderer Data (HDR Emulation Scale, Depth/Stencil Buffer, Camera Sorting Layer Texture).
- [docs.unity3d.com — create-sprite-atlas](https://docs.unity3d.com/6000.3/Documentation/Manual/sprite/atlas/create-sprite-atlas.html) — creating a Sprite Atlas.
- [docs.unity3d.com — sprite-atlas-reference](https://docs.unity3d.com/6000.3/Documentation/Manual/sprite/atlas/sprite-atlas-reference.html) — Sprite Atlas Inspector settings (Max Texture Size, Compression, Generate Mip Maps, platform overrides).
- [docs.unity3d.com — atlas-introduction (6000.4)](https://docs.unity3d.com/6000.4/Documentation/Manual/sprite/atlas/atlas-introduction.html) — general purpose of Sprite Atlas.
- [docs.unity3d.com — texture-choose-format-by-platform](http://docs.unity3d.com/6000.3/Documentation/Manual/texture-choose-format-by-platform.html) — ASTC recommendation for iOS (A8+), ETC/ETC2 fallback.
- [docs.unity3d.com — texture-compression-formats](http://docs.unity3d.com/6000.3/Documentation/Manual/texture-compression-formats.html) — general documentation structure on texture compression formats.
- [docs.unity3d.com — texture-type-sprite](https://docs.unity3d.com/6000.3/Documentation/Manual/texture-type-sprite.html) — Pixels Per Unit, Filter Mode, Generate Mipmap for sprites.
- [docs.unity3d.com — 2d-renderer-sorting](https://docs.unity3d.com/6000.3/Documentation/Manual/2d-renderer-sorting.html) — Sorting Layers, Order in Layer.
- [docs.unity3d.com — sorting-group-reference (6000.0)](https://docs.unity3d.com/6000.0/Documentation/Manual/sprite/sorting-group/sorting-group-reference.html) — Sorting Group (not verified via a separate WebFetch by this agent, only via WebSearch aggregation).
- [docs.unity3d.com — Lights-2D-intro (6000.0)](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/Lights-2D-intro.html) — introduction to URP 2D lighting (not verified via a separate WebFetch by this agent, only via WebSearch aggregation).
- [docs.unity3d.com — script-CanvasScaler (com.unity.ugui@2.6)](https://docs.unity3d.com/Packages/com.unity.ugui@2.6/manual/script-CanvasScaler.html) — Canvas Scaler, UI Scale Mode, Screen Match Mode.
- [docs.unity3d.com — Screen-safeArea](https://docs.unity3d.com/ScriptReference/Screen-safeArea.html) — Screen.safeArea API.
- [divillysausages.com — performance-tips-for-unity-2d-mobile](https://divillysausages.com/2016/01/21/performance-tips-for-unity-2d-mobile/) — practical tips on 2D performance on mobile (draw calls, overdraw, Canvas, physics, mipmaps); a practitioner's opinion, not official documentation.
- [gamba04.itch.io — 2D Mobile Optimized Lighting System devlog](https://gamba04.itch.io/my-personal-devlog/devlog/145714/2d-mobile-optimized-lighting-system) — a practical report on the cost of Light2D on mobile (not verified via a separate WebFetch by this agent, only via WebSearch).
- [github.com/simeonradivoev/Light2D](https://github.com/simeonradivoev/Light2D) — a third-party 2D lighting system with the stated figures for draw calls and ms/frame on a Nexus 4 (not verified via a separate WebFetch by this agent, only via WebSearch).
- unity.com/blog/games/optimize-your-mobile-game-performance-expert-tips-on-graphics-and-assets — an official Unity blog post on mobile optimization; the WebFetch attempt returned HTTP 403 (bot protection), content not included directly in the file due to the inability to open and quote it verbatim.
