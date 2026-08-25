# UI Toolkit at runtime: state as of Unity 6.3 LTS

Date material collected: 2026-08-24.
Stack version: Unity 6.3 LTS (Unity documentation build version — 6000.3.x); some Unity pages haven't been re-published for 6.3 yet and are quoted from versions 6000.0–6000.4 (Unity keeps nearly identical text between 6.x patch releases, discrepancies are noted separately).

Scope: runtime UI only (the game while running on a mobile device), not Unity editor windows.

---

## In brief

- Official Unity position: for runtime, Unity still recommends uGUI as the main option, with UI Toolkit as an alternative; for editor tooling, it's the reverse — UI Toolkit is the main option. This is stated directly on the systems comparison page. [Unity Manual: Comparison of UI systems in Unity](https://docs.unity3d.com/6000.3/Documentation/Manual/UI-system-compare.html)
- UI Toolkit doesn't have: serializable events (like UnityEvent in the inspector), authoring directly in the scene (elements aren't GameObjects), integration with Animation Clips and Timeline. uGUI, in turn, doesn't have: a data binding system, USS transition animations, global style management, SVG support, or RTL language support. [Comparison of UI systems](https://docs.unity3d.com/6000.3/Documentation/Manual/UI-system-compare.html)
- Layout is built on Yoga (a Flexbox subset); USS units of measurement are only `px` and `%` — there are no other CSS units (em, rem, vh, vw). [USS data types](https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-USS-PropertyTypes.html)
- Runtime data binding is a relatively new feature (documented for the Unity 6 line), letting you bind properties of a regular C# object to element properties without a manual update loop, but it has noticeable overhead (see the complaint about 664 bindings below). [Data binding](https://docs.unity3d.com/6000.1/Documentation/Manual/UIE-data-binding.html), [complaint about runtime binding performance](https://discussions.unity.com/t/ui-toolkit-runtime-bindings-performance/1593988)
- Drag-and-drop of elements at runtime has no ready-made component — it's implemented manually via `PointerDownEvent`/`PointerMoveEvent`/`PointerUpEvent`/`PointerCaptureOutEvent` in a custom `PointerManipulator`. The official example exists only for Editor windows, but uses the same API that also works at runtime. [Create a drag-and-drop UI](https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-create-drag-and-drop-ui.html)
- The safe area (notch on iPhone) has no built-in solution in UI Toolkit — `Screen.safeArea` gives a rectangle with the origin at the bottom-left (unlike UI Toolkit, where the origin is at the top-left), so coordinates must be inverted manually; there are no ready-made Unity components for this, only third-party packages. [discussion and package artstorm/ui-toolkit-safe-area](https://github.com/artstorm/ui-toolkit-safe-area)
- Performance on mobile phones is below the level of "unconditionally better than uGUI": developers have recorded FPS drops to 30 on weak Android devices (Xiaomi Redmi Note 4) because of UI Toolkit's expensive universal ("uber") shader with a lot of branching; a Unity engineer confirmed this as a known architectural limitation (optimizing for CPU-bottleneck at the cost of a more expensive GPU shader). [UIToolkit rendering is extremely slow on older Android devices](https://discussions.unity.com/t/uitoolkit-rendering-is-extremely-slow-on-older-android-devices/1561024)
- World Space UI (UI in world coordinates, not an overlay) exists only from Unity 6.2+, configured via `PanelSettings.renderMode = World Space` and `Pixels Per Unit`; integration with 2D sorting layers is explicitly not supported. [World Space UI](https://docs.unity3d.com/6000.3/Documentation/Manual/ui-systems/world-space-ui.html), [Create a World Space UI](https://docs.unity3d.com/6000.3/Documentation/Manual/ui-systems/create-world-space-ui.html)
- Unity's practices recommend USS classes (`AddToClassList`/`RemoveFromClassList`) instead of inline styles — inline styles create overhead per element and don't support pseudo-classes (`:hover` etc.). [Best practices for USS](https://docs.unity3d.com/Manual/UIE-USS-WritingStyleSheets.html)
- Practitioners' opinions diverge: some developers complain about an excessively complex class hierarchy, "hundreds of lines of USS just to remove the default runtime styles," and a useless debugger in play mode; others show through tests (not from Unity, but from a studio) a multiple-fold advantage of UI Toolkit in draw calls and memory. There's no unambiguous consensus to "switch everyone over." [UI Toolkit frustrations](https://discussions.unity.com/t/ui-toolkit-frustrations/1685389), [Angry Shark Studio: UI Toolkit vs UGUI 2025](https://www.angry-shark-studio.com/blog/unity-ui-toolkit-vs-ugui-2025-guide/)

---

## 1. Comparison of UI Toolkit and uGUI (Unity's official position)

Source — the page "Comparison of UI systems in Unity," current for 6.3. [Unity Manual](https://docs.unity3d.com/6000.3/Documentation/Manual/UI-system-compare.html)

**Usage recommendations:**
- Runtime: main system — uGUI ("easy referencing from MonoBehaviours"), alternative — UI Toolkit (when the project has many UI screens and needs a familiar workflow for artists/designers).
- Editor: main system — UI Toolkit ("better reusability and decoupling", "visual tools for authoring UI"), alternative — IMGUI.
- Development status: UI Toolkit is actively developed and gains new capabilities every release; uGUI and IMGUI are "established and production-proven UI systems that are updated infrequently."

**What UI Toolkit lacks compared to uGUI** (per the comparison page):
- serializable events (UnityEvent-like fields in the inspector);
- authoring directly in the scene (UI Toolkit elements aren't GameObjects, they can't be dragged as scene objects);
- integration with Animation Clips and Timeline.

**What uGUI lacks compared to UI Toolkit:**
- a data binding system;
- USS transition animations;
- global style management (analogous to a theme/cascade);
- SVG support;
- support for right-to-left (RTL) languages.

Practical conclusion for a mobile 2D puzzle: if the game relies heavily on Timeline/Animator animations on UI, integration with existing uGUI assets, or serializable UnityEvents in the inspector — this is a downside of UI Toolkit that will have to be compensated for with code.

---

## 2. Structure: UIDocument, PanelSettings, VisualTreeAsset, StyleSheet

Relationship diagram (per the official guide "Creating your first runtime UI"): [Unity Manual](https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-HowTo-CreateRuntimeUI.html)

1. **PanelSettings** — an asset that sets the panel's screen settings: scale mode, resolution, drawing order (sort order), and the name under which the UI will appear in the UI Toolkit Debugger. Created via `Assets > Create > UI Toolkit > Panel Settings Asset`.
2. **UIDocument** — a component on a GameObject in the scene. References the `PanelSettings` and a root `VisualTreeAsset` (a UXML file, the "Source Asset"). On entering Play Mode it automatically loads the assigned UXML.
3. **VisualTreeAsset** — how Unity represents a UXML file in C#; a regular project asset, can be loaded via `AssetDatabase.LoadAssetAtPath<VisualTreeAsset>` or referenced via a field in the inspector.
4. **StyleSheet** — how Unity represents a USS file in C#; also a regular asset.

Both types (`VisualTreeAsset`, `StyleSheet`) are "regular Unity assets" — they can be attached using standard Unity methods (dragging into the inspector, loading by path). [Load UXML and USS in C# scripts](https://docs.unity3d.com/Manual/UIE-manage-asset-reference.html)

**What to put in the scene:** one GameObject with a `UIDocument` per screen/panel (or a shared one for the whole application, if screens are switched via visibility of root `VisualElement`s). The root of the visual tree is `UIDocument.rootVisualElement` (in the editor — `EditorWindow.rootVisualElement`), from which the whole `VisualElement` hierarchy is built. [Introduction to visual elements and the visual tree](https://docs.unity3d.com/Manual/UIE-VisualTree.html)

**Example from the official guide** (structure of a UXML file with a character list):

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements" xmlns:uie="UnityEditor.UIElements" editor-extension-mode="False">
    <Style src="MainView.uss" />
    <ui:VisualElement name="background">
        <ui:VisualElement name="main-container">
            <ui:ListView focusable="true" name="character-list" />
```

Scene setup steps (verbatim from the guide): `GameObject > UI Toolkit > UI Document`, then drag `MainView.uxml` into the `Source Asset` field of the `UIDocument` component. [Create a list view runtime UI](https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-HowTo-CreateRuntimeUI.html)

**An important lifecycle detail:** when the UI reloads (e.g., when the UXML file changes on the fly), all associated `MonoBehaviour` components are "disabled before the reload, and then re-enabled after" — so UI initialization code should be kept in `OnEnable()`/`OnDisable()`, not `Start()`. [Creating a Runtime UI with UIDocument](https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-HowTo-CreateRuntimeUI.html)

Controller example from the guide:

```csharp
void OnEnable()
{
    var uiDocument = GetComponent<UIDocument>();
    var characterListController = new CharacterListController();
    characterListController.InitializeCharacterList(
        uiDocument.rootVisualElement, m_ListEntryTemplate);
}
```

Also, newer versions of Unity 6 introduced the **Panel Renderer** component — an alternative way to attach a panel to a GameObject rendering hierarchy; it initializes the visual tree when the component is created, when `PanelSettings`/`VisualTreeAsset` changes, or when the component is enabled. [Panel Renderer component](https://docs.unity3d.com/6000.6/Documentation/Manual/ui-systems/panel-renderer-component.html) (page documented for version 6000.6, not verified for 6.3 — the component may not yet exist in this build).

**Default theme:** when the first `UIDocument` is added to a project, a theme asset `Assets/UI Toolkit/UnityThemes/UnityDefaultTheme.tss` (Theme Style Sheet, TSS) is automatically generated; for the standard controls (buttons, fields, etc.) to look and work correctly, this theme file needs to be imported, after which styles can be overridden/extended on top of it. [Theme Style Sheet (TSS)](https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-tss.html) (not verified directly via WebFetch — taken from search results).

---

## 3. UXML and USS syntax

### Units of measurement

UI Toolkit supports only two length unit types: pixels (`px`, absolute) and percent (`%`, relative to the parent). If a unit isn't specified explicitly, the value is treated as pixels; the exception is `0`, for which a unit isn't required. Numeric (non-length) values are given as float or integer literals, e.g. `flex: 1.0`. [USS data types](https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-USS-PropertyTypes.html)

There are no other CSS units (`em`, `rem`, `vh`, `vw`, `pt`, etc.) in USS — this is a difference from full CSS, important when porting layout/markup from the web.

In C#, length can be specified via the `Length` struct:

```csharp
new Translate(new Length(10f, LengthUnit.Percent), new Length(50f, LengthUnit.Pixel))
// equivalent via implicit conversions:
new Translate(Length.Percent(10), 50)
```

### Layout (Flexbox / Yoga)

UI Toolkit's layout engine is built on Yoga — an implementation of a Flexbox subset from the HTML/CSS world; UI Toolkit's properties match Yoga's behavior and cover "most properties in Flexbox." By default all elements participate in layout, and a container lays out its child elements vertically. [Position element with the layout engine](https://docs.unity3d.com/Manual/UIE-LayoutEngine.html)

Main supported Flexbox properties (verified against the manual):
- `flex-direction` (Flex > Direction) — the main axis direction; `row` switches the layout to horizontal.
- `flex-grow` (Flex > Grow) — an element's growth share along the main axis relative to sibling elements; `1` on two siblings gives each 50% of the parent's available space.
- `justify-content` (Align > Justify Content) — alignment along the main axis (`flex-start`, `flex-end`, etc., values depend on `flex-direction`).
- `position: absolute` — takes an element out of the Flexbox layout flow ("invisible to the default Flexbox-based layout engine, as if it no longer takes any space"); absolutely positioned elements render on top of relatively positioned siblings.
- `display: flex | none` — the USS `display` property supports only a small subset of the CSS `display` keywords (not the full list from the web).
- `overflow: visible | hidden` — default is `visible` (content isn't clipped), `hidden` clips at the element's bounds (useful for masks).

[Introduction to UI Toolkit / layout summary via search, partially confirmed via UIE-LayoutEngine.html]

### Box model specifics

The USS box model corresponds to setting the CSS property `box-sizing: border-box` — meaning padding and border are included in the element's specified width/height, rather than added to it (unlike the classic CSS content-box model, which is the default). [USS properties reference](https://docs.unity3d.com/Manual/UIE-USS-Properties-Reference.html)

### USS properties: inheritance and animatability

Most layout and positional properties **are not inherited**. Text properties are inherited: `color`, `font-size`, `letter-spacing`, `text-shadow`, `-unity-font`, `-unity-font-style`, `-unity-text-align`, `white-space`. [USS properties reference](https://docs.unity3d.com/Manual/UIE-USS-Properties-Reference.html)

Properties fall into three categories by animatability: fully animatable (most sizes, padding/margins, color, transforms), discrete (layout direction, display mode, font, positioning mode), and non-animatable (cursor, `display`, transitions themselves, text generator settings).

Unity adds its own properties with the `-unity-` prefix, which aren't in standard CSS: `-unity-font`, `-unity-font-definition`, `-unity-material` (custom rendering materials), `-unity-slice-*` (9-slice image scaling), `-unity-text-outline`, and other text properties.

### Transforms without triggering a layout rebuild

The `translate`, `rotate`, `scale` properties are animatable and **do not** trigger a layout recalculation of neighboring elements — this is explicitly called out in the documentation as a cheaper way to animate elements. [USS transform](https://docs.unity3d.com/6000.4/Documentation/Manual/UIE-Transform.html)

Syntax examples (verbatim from the manual):

```css
/* translate */
translate: 80%;
translate: 35px;
translate: 5% 10px;
translate: 24px 0%;

/* scale */
scale: 2.5;
scale: -1 1;
scale: none;

/* rotate */
rotate: 45deg;
rotate: -100grad;
rotate: -3.14rad;
rotate: 0.75turn;
rotate: none;
```

Wording from the manual: "Applying transform to an element reduces recalculations because it doesn't change the layout of other elements in the hierarchy" — i.e., transforms are preferable for animations over directly changing `position`/`margin`.

### border-radius and percentages

A separate quirk (found via WebSearch of the documentation, not confirmed verbatim by a repeat WebFetch of the same wording, but reproduced across several manual versions): if `border-radius` is set in percent, Unity first converts the percent to pixels, then clamps the resulting radius to half of that pixel value — so for a 100×100 px element, any radius greater than 50px will be clipped to 50px.

---

## 4. Data binding in Unity 6

Data binding is something uGUI doesn't have at all (see section 1), and it's one of the main architectural differences of UI Toolkit. Unity 6 has **two binding systems**: [Data binding](https://docs.unity3d.com/6000.1/Documentation/Manual/UIE-data-binding.html)

- **Runtime data binding** — binds properties of an arbitrary C# object ("plain C# object", not necessarily a Unity object) to control properties in the UI. Works in both runtime UI and editor UI (except for serialized data).
- **SerializedObject data binding** — binding to a `SerializedObject`, works only in editor UI (provides undo/redo and multi-selection support) — not relevant for game runtime.

Official wording: "Data binding synchronizes properties of non-UI objects, such as a string property on a MonoBehaviour, with properties of UI objects, such as the value property of a TextField."

### Creating a binding in C#

General procedure: [Create a runtime binding in C# scripts](https://docs.unity3d.com/6000.1/Documentation/Manual/UIE-runtime-binding-types.html)

1. Create a `DataBinding` object.
2. Set the `dataSource` (source object) and `dataSourcePath` (path from the source to the needed property).
3. Set the binding mode and the update trigger.
4. Register the binding on the visual element via `SetBinding()`, adding type converters if needed.

Binding modes appearing in the API examples: `ToTarget` (source → UI), `ToSource` (UI → source), `TwoWay` (two-way), `ToTargetOnce` (once, from source to UI).

Example of registering a binding (verbatim from the documentation):

```csharp
vector3Field.SetBinding("value", new DataBinding
{
    dataSourcePath = new PropertyPath(nameof(ExampleObject.vector3Value))
});
```

Additional binding management methods: `GetBinding()`, `TryGetBinding()`, `HasBinding()`, `ClearBinding()`. Type conversion between the source and the UI is done via `sourceToUiConverters.AddConverter()`.

**Important limitation:** UI Toolkit doesn't track changes to `element.style` and `element.resolvedStyle` — a binding can be targeted at an element's resolved style, but changes to it can't be tracked via a binding. [Data binding manual, via WebFetch of UIE-data-binding.html and associated search]

**UXML binding declaration** (per search results, a `<Bindings>`/`<ui:DataBinding>` structure with `property`, `data-source-path`, `binding-mode` attributes — the UXML example itself wasn't opened directly via WebFetch, noting as not verified verbatim).

### Binding performance — see section 7 (which walks through a real complaint about 664 bindings in a `ScrollView`).

---

## 5. Input and event handling

### Default event system

On entering Play Mode, UI Toolkit creates its own "default event system that is not part of any scene, and provides basic support for most input devices" — meaning that in a simple scenario (UI Toolkit only, no uGUI) a separate `EventSystem` doesn't need to be added to the scene. [Runtime UI event system](https://docs.unity3d.com/6000.4/Documentation/Manual/UIE-Runtime-Event-System.html)

The `EventSystem` component becomes necessary when UI Toolkit is combined with uGUI: when the first uGUI element is added to a scene, Unity automatically adds an `EventSystem` and a `Standalone Input Module`. The input module needs to be chosen based on the project's active input system:
- **Standalone Input Module** — for the legacy Input Manager, "dispatches events to UI Toolkit elements."
- **Input System UI Input Module** — for the Input System package; together with its own `EventSystem` it "ensures that the events from both UI Toolkit and uGUI elements are properly dispatched."

The page doesn't explicitly describe touch-input specifics separately from pointer input — touch is handled within the general pointer event model.

### Pointer events

The base class for all pointer events is `PointerEventBase`. [Pointer events](https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-Pointer-Events.html)

- **PointerDownEvent** — sent when the pointer is pressed; the target is the element that captured the pointer (pointer capture), or the topmost picking-eligible element by Z-order under the cursor.
- **PointerMoveEvent** — on a change in pointer state (movement, change of pressed buttons, etc.), targeting is the same as PointerDown.
- **PointerUpEvent** — when the pointer is released inside a visual element; firing it also "removes the pointer coordinates."

Properties: `pointerId` ("returns an integer that identifies the pointer that sends the event" — critical for multi-touch, since each touch has its own id); `pressure` (touch pressure force, `1.0f` if the device doesn't report it).

All three events: "Trickles down: Yes", "Bubbles up: Yes", "Cancellable: Yes" — i.e., they follow the standard propagation model (first top-down through the tree — the capture/trickle phase, then the target phase, then bottom-up — the bubble phase). Disabled (`disabled`) elements don't receive `PointerDownEvent`.

### ClickEvent

`ClickEvent` — "occurs when the user clicks the left mouse button (or the first button on a pointing device) over a VisualElement." A click is a `PointerDownEvent` followed by a `PointerUpEvent` **on the same VisualElement**; between them the pointer may move, as long as the down and up occur over the same element. [Click events](https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-Click-Events.html)

Handler example (verbatim from the manual):

```csharp
private void OnBoxClicked(ClickEvent evt)
{
    if (evt.propagationPhase != PropagationPhase.AtTarget)
        return;

    var targetBox = evt.target as VisualElement;
    targetBox.style.backgroundColor = GetRandomColor();
}
```

`ClickEvent` is useful for catching clicks on arbitrary `VisualElement`s, not just buttons — for example, the `Toggle` implementation uses `ClickEvent` to switch state and show the checkmark.

### Drag-and-drop — critical for a game with item dragging

There's no ready-made runtime drag-and-drop component in UI Toolkit. The official example (written for Editor windows, but built on the general `PointerManipulator` API, which also works at runtime) is the `DragAndDropManipulator : PointerManipulator` class: [Create a drag-and-drop UI inside a custom Editor window](https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-create-drag-and-drop-ui.html)

Manipulator structure:
- The constructor saves the `target` and a reference to the root of the visual tree (the parent).
- `PointerDownHandler` — saves the starting position of the `target` and the pointer, captures the pointer (`target.CapturePointer(pointerId)`), marks that dragging has started.
- `PointerMoveHandler` — if dragging is active and the pointer is captured, recalculates the new `target` position within the window from the pointer's movement delta.
- `PointerUpHandler` — checks the drag and capture state, releases the pointer.
- `PointerCaptureOutHandler` — on losing capture, looks through all slots, determines intersecting ones, finds the nearest, and either snaps the object to the slot or returns it to its original position.
- `RegisterCallbacksOnTarget()` / `UnregisterCallbacksFromTarget()` — register/unregister all four callbacks on the `target`.

Callback registration (per the general documented practice for draggable elements): you need to register `PointerDownEvent`, `PointerMoveEvent`, `PointerUpEvent` (and `PointerCaptureOutEvent`, to properly handle capture interruption — e.g., if a finger slides off the screen):

```csharp
target.RegisterCallback<PointerDownEvent>(PointerDownHandler);
target.RegisterCallback<PointerMoveEvent>(PointerMoveHandler);
target.RegisterCallback<PointerUpEvent>(PointerUpHandler);
target.RegisterCallback<PointerCaptureOutEvent>(PointerCaptureOutHandler);
```

Practical conclusion for "dragging items" in a puzzle: the logic will have to be written from scratch on top of pointer events and `PointerManipulator` — Unity doesn't provide a ready-made "out of the box" solution in either uGUI or UI Toolkit (uGUI has `IBeginDragHandler`/`IDragHandler`/`IEndDragHandler`, which is somewhat closer to "out of the box," but still requires manually implementing slot logic).

### What's not covered by the official documentation

A separate FAQ page on input and events doesn't contain a touch-vs-mouse comparison, doesn't describe multi-touch gestures or limitations — it only covers key remapping, `EventSystem.current.IsPointerOverGameObject`, `panel.Pick()`, and focus navigation. [FAQ for input and event systems with UI Toolkit](https://docs.unity3d.com/Manual/UIE-faq-event-and-input-system.html) — i.e., there's no official UI Toolkit documentation on gestures (pinch-to-zoom, swipes, multi-touch recognition); at the level of general Unity input, developers solve this via third-party libraries (TouchScript, TouchKit) even outside UI Toolkit, which points to the absence of a single standard solution for complex gestures in general.


## 6. Scaling for different screens and the safe area

### PanelSettings: scale modes

`PanelSettings.scaleMode` (type `PanelScaleMode`) supports three values: [PanelScaleMode enum](https://docs.unity3d.com/2021.2/Documentation/ScriptReference/UIElements.PanelScaleMode.html), [Panel Settings properties reference](https://docs.unity3d.com/6000.0/Documentation/Manual/UIE-Runtime-Panel-Settings.html)

- **Constant Pixel Size** (`ConstantPixelSize`) — "Elements stay the same size, in pixels, regardless of screen size." Parameter — `Scale` (must be greater than 0).
- **Constant Physical Size** (`ConstantPhysicalSize`) — "Elements stay the same physical size (displayed size) regardless of screen size and resolution." Parameters — `Reference DPI` and `Fallback DPI`.
- **Scale With Screen Size** (`ScaleWithScreenSize`) — "Elements get bigger when the screen size increases, and smaller when it decreases." Key parameters — `Screen Match Mode` and `Reference Resolution`.

For `Scale With Screen Size`:
- `Screen Match Mode`: `Match Width or Height` (with interpolation via `Match Value`, where 0 = by width, 1 = by height, 0.4 = 40% interpolation), `Shrink` (crop the canvas), `Expand` (enlarge the canvas).
- `Reference Resolution` — "Set the resolution that this panel's UI is designed for."

Other `PanelSettings` properties: `Sort Order` ("Set the order that the UI System draws panels"), `Target Texture` (for rendering UI onto 3D geometry), `Theme Style Sheet`, `Text Settings`, `Target Display`.

This is essentially a direct analog of `Canvas` + `CanvasScaler` from uGUI: "The Panel Setting asset is the UI Toolkit's version of the Canvas and Canvas Scaler from the old UGUI system" (per search results, not confirmed verbatim by a repeat WebFetch).

### Known issue: resolvedStyle ignores scaleMode

A complaint recorded on Unity Discussions: `visualElement.worldBound.height`, `visualElement.resolvedStyle.height`, and `visualElement.layout.height` **ignore** the scaling set via `uiDocument.panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize`; `uiDocument.panelSettings.scale` also doesn't return the actual scale in this version of the discussion. Proposed workaround: [Get actual size of a VisualElement considering PanelSettings PanelScaleMode.ScaleWithScreenSize](https://discussions.unity.com/t/get-actual-size-of-a-visualelement-considering-panelsettings-panelscalemode-scalewithscreensize/906698)

```csharp
scale = Screen.height / uiDocument.rootVisualElement.resolvedStyle.height
```

Later replies in the same thread note that Unity subsequently added a `scale` property directly on the panel object, removing the need for the manual recalculation (the version in which this was fixed isn't named precisely in the discussion).

### The safe area (notch on iPhone)

No ready-made standard "safe area + UI Toolkit" solution was found in Unity's own documentation — there are only third-party solutions and practitioner articles. Key technical points about `Screen.safeArea`:

- `Screen.safeArea` is defined relative to the player window, not the device's physical screen: if `PlayerSettings.Android.renderOutsideSafeArea` is disabled, Unity itself fits the player window to the device's safe area, and then `Screen.safeArea` effectively equals `Rect(0, 0, Screen.width, Screen.height)`, since the player window no longer includes the unsafe zones.
- **Gotcha with the coordinate system**: the origin of `Screen.safeArea` is at the bottom-left of the screen, while the origin in UI Toolkit is at the top-left; when transferring safe-area coordinates into a UI Toolkit panel, the Y axis needs to be inverted.
- Practical technique (per practitioner articles, not officially documented by Unity): instead of uGUI's anchor approach, for UI Toolkit the safe area is wrapped in a container and given `padding` computed from `Screen.safeArea`, using `RuntimePanelUtils.ScreenToPanel(panel, screenPoint)` to convert screen coordinates into panel coordinates.

A ready-made third-party package — [`artstorm/ui-toolkit-safe-area`](https://github.com/artstorm/ui-toolkit-safe-area): provides a custom `SafeArea Container` control, which needs to be placed as the topmost element of the hierarchy so it occupies the whole screen. Package features (verbatim from the README):
- "The container margins and the safe area is collapsed by default" (with collapsing, the larger of the margin and safe area is used; with it disabled, the margin is added on top of the safe area).
- Individual edges (left/right/top/bottom) can be excluded from the safe-area calculation.
- "This option excludes the safe area values for all edges on tvOS."
- A separate flag forces polling of the safe area for correct updates on a 180° screen rotation (e.g., Landscape Left → Landscape Right) — this compensates for a known bug where the normal safe-area update doesn't fire on a fast 180° rotation.

Practical conclusion for a mobile puzzle: the safe area with UI Toolkit will have to be computed manually (via `Screen.safeArea` + `RuntimePanelUtils.ScreenToPanel` + Y inversion) or by attaching a ready-made third-party control — there is no standard UI Toolkit analog of the uGUI `Safe Area` component.

## 7. Performance on mobile devices

### What triggers a layout rebuild

The official optimization guide states: a layout recalculation ("layout rebuild / relayout") is triggered by changes to an element's size, position, or alignment — for example, resizing the panel or moving elements; frequent layout recalculations are expensive. Recommendation — use transforms (`translate`/`rotate`/`scale`, see section 3) for animations instead of directly changing positional properties, because this doesn't change the layout of neighbors. [Optimizing performance](https://docs.unity3d.com/6000.4/Documentation/Manual/best-practice-guides/ui-toolkit-for-advanced-unity-developers/optimizing-performance.html)

Style recalculation (repaint / style resolution) is triggered by changing classes or styles — for example, adding a class or changing a color. Recommendation — don't toggle classes to change style in large hierarchies during animations; instead update properties directly (inline) in such hot paths.

### Batching and the limit of 8 textures

When the limit of eight textures per batch is exceeded, the batching system is forced to split rendering into separate batches, which increases overhead; the solution is to use a dynamic texture atlas or Sprite Atlas to combine textures and preserve batching efficiency.

### The dynamic atlas

UI Toolkit automatically adds and removes textures from the dynamic atlas as visual elements reference them. Atlas settings (Dynamic Atlas Settings) are located in `PanelSettings`; there are also filters there that determine which textures go into the atlas (for example, a size filter — large textures aren't atlased). [Control textures of the dynamic atlas](https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-control-textures-of-the-dynamic-atlas.html)

The cost of dynamic atlases is fragmentation: "When textures are added or removed from the atlas, it can lead to fragmentation, creating small spaces where previous textures were, which are too small to reallocate to other textures." To reset the atlas to its initial state, there's `RuntimePanelUtils.ResetDynamicAtlas()` — recommended to call when many visual elements are removed or added at the same time.

For mobile devices with limited memory, the documentation directly advises reducing `Max Atlas Size` relative to the default — for example, `2048` pixels instead of `4096`.

### Other mobile recommendations from the official optimization guide

- Use rectangular shader-based masks instead of stencil-based masks, to avoid render-state breaks.
- Use `ListView` for scrollable content for virtualization (don't create all list elements at once — see section 9).
- Set `DisplayStyle.None` rather than `opacity = 0`, to fully remove an element from rendering (rather than just making it transparent while it still consumes rendering resources).
- Apply the `DynamicTransform` usage hint to animated elements.

### Real performance complaints on weak Android devices

A case with specific numbers recorded on Unity Discussions: on a **Xiaomi Redmi Note 4 (Android 7)** device — 60+ FPS with no background style, 55 FPS when adding only a background color, 30 FPS when adding a background color and a texture simultaneously. The user diagnosed the cause as "extensive branching in the UnityUIE.cginc shader, particularly when selecting a texture" (excessive branching in UI Toolkit's internal shader when selecting a texture). [UIToolkit rendering is extremely slow on older Android devices](https://discussions.unity.com/t/uitoolkit-rendering-is-extremely-slow-on-older-android-devices/1561024)

Reply from a Unity engineer (AlexandreT-unity), verbatim: "UI Toolkit has been heavily optimized assuming that the UI bottleneck is on CPU... the consequence is that our shader is more expensive" — i.e., architecturally UI Toolkit trades GPU shader cost for reduced CPU load, and on weak mobile GPUs this can be an unfavorable trade-off. It was also confirmed that Unity uses a Mali-T720 MP2 as the "low-end device" for performance testing. Among the proposed solutions — custom shaders for `VisualElement`, `ImmediateModeElement` with a simplified shader, and the `LargePixelCoverage` usage hint that appeared in the Unity 6.3 beta. At the same time, version 6.3 itself raised the minimum Android API level to 25, which excludes some old devices like the test Redmi Note 4 from the applicability of this improvement.

Other complaints recorded on the Unity Discussions forum:
- Poor performance of a full-screen `ScrollView` with about 20 elements (`VisualElement` with a background image) on a Samsung Galaxy A3 2016.
- Drops during application initialization on mobile: "We are currently using the UI Toolkit for a mobile project, and are running into some performance issues when the application initializes, as others have noted in other forum posts."
- Absence of official benchmarks from Unity: developers directly asked Unity about data on power consumption and CPU/GPU load on mobile, and no such specifics from Unity were found in the material gathered — only qualitative explanations of the architecture. [Performance of UI Toolkit](https://discussions.unity.com/t/performance-of-ui-toolkit/1563732)

### The cost of runtime data binding

A separately documented complaint: a user with a `ScrollView` of ~100 elements (about 20 visible at once) got **664 bindings** in total, and this caused a noticeable performance drop regardless of the binding mode — "I tested all binding modes and update triggers with no results in performance." The cause was not the value update itself, but the check for whether an update is needed: "It not updating them, but checking if they need to be updated and that's the issue here" (the `ShouldUpdateBindings` function ate up noticeable frame time even without real changes). Elements hidden via `DisplayStyle.None` or `flex: none` still continue to be processed by the binding system — turning off display doesn't disable the bindings. [UI Toolkit Runtime Bindings performance](https://discussions.unity.com/t/ui-toolkit-runtime-bindings-performance/1593988)

Reply from a Unity developer (martinpa_unity): "Runtime bindings... have an overhead compared to handcrafted code"; the recommendation is to reduce the number of simultaneous bindings via `ListView` (in this specific case, switching to `ListView` reduced the number of bindings from 664 to about 135). Among the announced future improvements: the ability to "disable" a binding when `display` isn't `flex`, using code generation to skip unneeded updates, and a pre-pass over data sources.

**Practical conclusion for a puzzle:** if the UI will include a grid of cells/items with a large number of simultaneously active elements, data bindings via `SetBinding`/`DataBinding` on each element may turn out more expensive than manually updating values in code — especially if the elements don't use `ListView`/`GridView` virtualization.

## 8. Pitfalls and developer complaints

### World Space UI: limitations

UI working in world coordinates (not a screen overlay) only appeared with Unity 6.2+ (the same manual page is confirmed for 6.3). Setup: [World Space UI](https://docs.unity3d.com/6000.3/Documentation/Manual/ui-systems/world-space-ui.html), [Create a World Space UI](https://docs.unity3d.com/6000.3/Documentation/Manual/ui-systems/create-world-space-ui.html)

- The `PanelSettings` render mode is switched to `World Space` ("Set the render mode of the Panel Settings asset to World Space to create a World Space UI").
- `Pixels Per Unit` (default 100) — how many panel pixels correspond to one world unit.
- Panel Input Configuration setup — via `GameObject > UI Toolkit > Panel Input Configuration` or a button in the inspector, if `PanelSettings` is already in World Space mode.
- The container size is set via the USS `size`/`position` properties; the `UIDocument` inspector has a `World-Space Dimensions` mode: `Dynamic` (by content) or `Fixed` (manual), plus a pivot point selector (9 positions) and `Pivot Reference Size` (`Bounding Box` — includes all elements, or `Layout` — by the layout system).
- **An explicit limitation from the documentation**: "Integration with 2D sorting layers isn't currently supported" — the sort order of root documents depends on distance along Z to the camera, not on 2D sorting layers, which matters for a 2D game where all sorting is usually done via sorting layers/order in layer.

For a mobile 2D puzzle this means: if UI attached to a game object in world coordinates is needed (e.g., a tooltip above an item), its sorting won't slot directly into the existing 2D sorting layers of the sprites — a workaround via Z will be needed, or such elements will need an overlay panel with manual recalculation of the object's screen coordinates instead of World Space UI.

### Direct complaints about runtime UI Toolkit collected from Unity Discussions

**Complexity of the class system.** Developer Tom163: "every little bit of UI has at least two, often three different classes that do essentially the same thing" — it's not obvious which class to override for the needed behavior. [UI Toolkit frustrations](https://discussions.unity.com/t/ui-toolkit-frustrations/1685389)

**Default styles get in the way.** The same author: "My stylesheet is literally hundreds of lines by now that only remove the runtime default styling" — meaning a significant part of custom USS goes not into one's own design, but into neutralizing the default theme's styles. Also noted is confusion with pseudo-classes: "Styling :hover is pointless, you need to style :hover:enabled in almost all use cases."

**Useless debugger in play mode.** "the debugger is mostly useless," because input capture by the game view prevents pausing the UI for inspection.

**Weak data binding for MVVM/MVC.** Another participant (aberroarman): "I can't even bind button to an action" — meaning the binding doesn't allow directly linking a button press to a method, as expected in a full MVVM setup; existing third-party MVVM add-ons require that "both View and ViewModel classes... has to be inherited from a base class," which reduces applicability in real projects.

**Fragility of references in UXML.** References to types in UXML don't update automatically when types are moved between assemblies/namespaces, unlike regular references in C# code.

**Instability of runtime UI recreation.** In a separate thread — a warning "UI was recreated and no companion MonoBehaviour found, some UI functionality may have been lost" with minimal diagnostic information; one participant: "How can I debug this to actually see where the issue is coming from?" and further "Can someone from Unity who built this crap stuff explain how to debug it?" The problem occurred in a specific project even on a fresh empty scene with a new UI Document, not only in old scenes. The thread author's final assessment: "I thought prefabs were the most sensitive things in Unity, but I guess we have a new champion UI Toolkit," and "I've already lost two days on this warning and can't move forward because of it." [What's wrong with UI Toolkit?](https://discussions.unity.com/t/whats-wrong-with-ui-toolkit/1693143)

These complaints (except those explicitly marked as mobile/performance-related in section 7) aren't specific solely to mobile platforms, but they directly concern runtime development, not just editor tooling.

## 9. Practical techniques

### List/grid via ListView

`ListView` — the "most commonly used list-based control in UI Toolkit," providing virtualization: only visible elements are instantiated and rendered, which is critical for performance with large data sets. [Create a list view runtime UI](https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-HowTo-CreateRuntimeUI.html), [ListView UXML element](https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-uxml-element-ListView.html)

`ListView` UXML attributes (verbatim from the manual): `item-template` — "A UXML template that constructs each recycled and rebound element within the list"; `fixed-item-height` (float) — "The height of a single item in the list, in pixels"; `reorderable` (bool) — "Gets or sets a value that indicates whether the user can drag list items to reorder them"; `selection-type`; `virtualization-method` (`FixedHeight` or `DynamicHeight`).

UXML example:

```xml
<UXML xmlns="UnityEngine.UIElements">
    <ListView class="the-uxml-listview" fixed-item-height="20" />
</UXML>
```

Populated from C# via a pair of functions `makeItem`/`bindItem` (a standard virtualization pattern — `makeItem` creates visual elements "as needed when the ListView needs more items to render," `bindItem` binds a recycled element to data by index):

```csharp
Func<VisualElement> makeItem = () => new Label();
Action<VisualElement, int> bindItem = (e, i) => ((Label)e).text = items[i];

var listView = container.Q<ListView>();
listView.makeItem = makeItem;
listView.bindItem = bindItem;
listView.itemsSource = items;
```

An important architectural detail: `ListView`'s visual elements aren't `GameObject`s, so a `MonoBehaviour` component can't be attached to them directly; data is bound via the element's `userData` and a separate controller class (`CharacterListEntryController` in the official example), which has `SetVisualElement` to obtain a reference to the template's elements and a `Set` method to update the displayed data when an element is recycled.

For a puzzle's grid of items (multiple columns), no separate `GridView` control is described in the official documentation in the sources found — the practical route is: `ListView` with a horizontal row layout via USS (`flex-direction: row`, `flex-wrap: wrap` on the row container) on top of the same virtualization, or a manual implementation based on `ScrollView` with a custom element pool if two-dimensional virtualization is needed (not verified by an official source — an extrapolation from the documented Flexbox properties in section 3).

### Changing appearance from code: classes instead of inline styles

Official recommendation ("Best practices for USS"): "Use USS files instead of inline styles when you can for more efficient memory usage" — inline styles are stored on each element separately and quickly increase memory consumption when scaling to many elements. Additionally, pseudo-classes (`:hover` and similar) can't be set via an inline style. [Best practices for USS](https://docs.unity3d.com/Manual/UIE-USS-WritingStyleSheets.html)

The recommended way to change an element's state from code is to toggle a class rather than directly assigning `element.style.*`:

```csharp
element.RemoveFromClassList("common");
element.AddToClassList("legendary");
```

Classes are recommended to be added in the custom element's constructor via `AddToClassList()`, including classes for child elements that constructor instantiates.

**Naming convention — BEM** (Block Element Modifier): a block is a standalone entity (`menu`, `button`); a block element uses a double underscore (`menu__item`); a modifier uses a double hyphen (`menu--disabled`). Example from the manual:

```xml
<VisualElement class="menu">
    <Label class="menu__item" text="Banana" />
    <Label class="menu__item menu__item--disabled" text="Orange" />
</VisualElement>
```

**Runtime cost of selectors.** All USS selectors are applied at runtime, so the class architecture affects initialization performance; the complexity is estimated roughly as N1 × N2, where N1 is the number of classes on an element and N2 is the number of applicable USS files. This usually isn't a problem, since each USS file is turned into a lookup table, but a specific risk is called out: "Avoid using `:hover` pseudo-class in selectors on elements with many descendants, such as `.yellow:hover > * > Button`," because mouse movement then invalidates the entire linked hierarchy. It's recommended to prefer child selectors (`>`) over descendant selectors when only a partial match is needed.

Inline styles in code/UXML are appropriate for one-off or experimental cases; they can later be moved into a USS class if needed.

## 10. UI Toolkit or uGUI for a 2D game: arguments for and against

No direct threads on Reddit r/Unity3D with a developed opinion could be found (searches for `site:reddit.com` and keyword phrases turned up no relevant results) — no reliable Reddit-specific sources were found. Below are arguments from official Unity documentation, the Unity Discussions forum, and a published practitioner breakdown (Angry Shark Studio).

### For UI Toolkit

- Officially positioned by Unity as a system that's actively developed and gains new capabilities every release, unlike "updated infrequently" uGUI/IMGUI. [Comparison of UI systems](https://docs.unity3d.com/6000.3/Documentation/Manual/UI-system-compare.html)
- Has built-in data binding, USS transitions, global style management, SVG and RTL support — none of which uGUI has at all.
- Architecturally, elements aren't GameObjects — practitioners claim this reduces overhead compared to "every UI element creates a GameObject" in uGUI. An independent (not from Unity) test by the Angry Shark studio for Unity 2022.3.10f1 LTS showed in their scenario: a 9x reduction in draw calls (5 vs 45), 3x faster CPU frame time (4.2 ms vs 12.5 ms), 2.6x less memory (48 MB vs 125 MB), "Smooth at 10,000+" elements in a scroll versus "Stutters at 500+" for uGUI, 5.7x faster instantiation of 100 elements (15 ms vs 85 ms). **Important:** these are figures from one third-party studio in one test scenario, not an official Unity benchmark — useful as a guideline, but not a guarantee for any given project. [Angry Shark Studio: UI Toolkit vs UGUI 2025](https://www.angry-shark-studio.com/blog/unity-ui-toolkit-vs-ugui-2025-guide/)
- Automatic batching and built-in list virtualization (`ListView`) without third-party plugins.
- Flexbox-based layout is better suited to interfaces adapting to different screens than uGUI's anchor system, if built from scratch.

### Against UI Toolkit (arguments for uGUI)

- Unity itself officially recommends uGUI as the main option specifically for runtime, with UI Toolkit only as an alternative. [Comparison of UI systems](https://docs.unity3d.com/6000.3/Documentation/Manual/UI-system-compare.html)
- No serializable events (UnityEvent in the inspector) and no integration with Animation Clips/Timeline — if the project's UI animation is already tied to Timeline/Animator, this is a direct loss of functionality.
- Real complaints about performance specifically on weak/old Android devices — FPS drops caused by the expensive "uber" shader, confirmed by a Unity engineer as an architectural trade-off (cheaper CPU, more expensive GPU). [UIToolkit rendering is extremely slow on older Android devices](https://discussions.unity.com/t/uitoolkit-rendering-is-extremely-slow-on-older-android-devices/1561024)
- Runtime data binding has a documented, practically observed overhead with a large number of simultaneous bindings (664 bindings → a noticeable drop), which the Unity developer themself acknowledged. [UI Toolkit Runtime Bindings performance](https://discussions.unity.com/t/ui-toolkit-runtime-bindings-performance/1593988)
- Drag-and-drop, safe area, complex gestures — everywhere the logic has to be written by hand on top of low-level pointer events; UI Toolkit provides no ready-made "out of the box" components for these scenarios (uGUI has a similar situation with gestures/safe area, but drag-and-drop via `IDragHandler` is somewhat more standardized).
- World Space UI only appeared in Unity 6.2+ and doesn't integrate with 2D sorting layers — for a 2D game where all sprite sorting is normally built on sorting layers, this is a significant limitation if world (non-overlay) UI is needed.
- Developers complain about architectural redundancy of classes, aggressive default styles (which have to be "turned off" with hundreds of lines of USS), and a weak runtime debugger. [UI Toolkit frustrations](https://discussions.unity.com/t/ui-toolkit-frustrations/1685389)
- uGUI has "Full Animator support," "Thousands of Asset Store packages," visual editing directly in the Scene view; UI Toolkit has "No Timeline support," "No mask component" (at least in standard form, only shader-based rectangular masks exist), "Limited shader effects," plus a separate learning curve (a different model than GameObject-based uGUI). [Angry Shark Studio: UI Toolkit vs UGUI 2025](https://www.angry-shark-studio.com/blog/unity-ui-toolkit-vs-ugui-2025-guide/)

### Conclusion for the project (without a ready answer on the author's behalf)

There's no single consensus in the sources on "what's better for a 2D mobile puzzle" — Unity's own position ("uGUI is the main option for runtime") diverges from practical tests by individual studios where UI Toolkit wins on metrics. For this specific project (text-based UXML/USS edited by an agent, instead of binary scenes), the deciding factor wasn't performance but the convenience of text-based editing — this is stated in the task brief as the original reason for choosing UI Toolkit, not a conclusion from this research.

---

## Sources

- [Unity Manual: Comparison of UI systems in Unity (6000.3)](https://docs.unity3d.com/6000.3/Documentation/Manual/UI-system-compare.html)
- [Unity Manual: Creating your first runtime UI / Create a list view runtime UI (6000.3)](https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-HowTo-CreateRuntimeUI.html)
- [Unity Manual: Load UXML and USS in C# scripts](https://docs.unity3d.com/Manual/UIE-manage-asset-reference.html)
- [Unity Manual: Introduction to visual elements and the visual tree](https://docs.unity3d.com/Manual/UIE-VisualTree.html)
- [Unity Manual: Panel Renderer component (6000.6)](https://docs.unity3d.com/6000.6/Documentation/Manual/ui-systems/panel-renderer-component.html)
- [Unity Manual: Theme Style Sheet (TSS) (6000.3)](https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-tss.html)
- [Unity Manual: Panel Settings properties reference (6000.0)](https://docs.unity3d.com/6000.0/Documentation/Manual/UIE-Runtime-Panel-Settings.html)
- [Unity Scripting API: PanelScaleMode (2021.2)](https://docs.unity3d.com/2021.2/Documentation/ScriptReference/UIElements.PanelScaleMode.html)
- [Unity Scripting API: PanelSettings (6000.3)](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/UIElements.PanelSettings.html)
- [Unity Manual: USS data types (6000.3)](https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-USS-PropertyTypes.html)
- [Unity Manual: USS properties reference](https://docs.unity3d.com/Manual/UIE-USS-Properties-Reference.html)
- [Unity Manual: Position element with the layout engine](https://docs.unity3d.com/Manual/UIE-LayoutEngine.html)
- [Unity Manual: USS transform (6000.4)](https://docs.unity3d.com/6000.4/Documentation/Manual/UIE-Transform.html)
- [Unity Manual: Best practices for USS](https://docs.unity3d.com/Manual/UIE-USS-WritingStyleSheets.html)
- [Unity Manual: Data binding (6000.1)](https://docs.unity3d.com/6000.1/Documentation/Manual/UIE-data-binding.html)
- [Unity Manual: Create a runtime binding in C# scripts (6000.1)](https://docs.unity3d.com/6000.1/Documentation/Manual/UIE-runtime-binding-types.html)
- [Unity Manual: Pointer events (6000.3)](https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-Pointer-Events.html)
- [Unity Manual: Click events (6000.3)](https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-Click-Events.html)
- [Unity Manual: Create a drag-and-drop UI inside a custom Editor window (6000.3)](https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-create-drag-and-drop-ui.html)
- [Unity Manual: Runtime UI event system and input handling (6000.4)](https://docs.unity3d.com/6000.4/Documentation/Manual/UIE-Runtime-Event-System.html)
- [Unity Manual: FAQ for input and event systems with UI Toolkit](https://docs.unity3d.com/Manual/UIE-faq-event-and-input-system.html)
- [Unity Manual: World Space UI (6000.3)](https://docs.unity3d.com/6000.3/Documentation/Manual/ui-systems/world-space-ui.html)
- [Unity Manual: Create a World Space UI (6000.3)](https://docs.unity3d.com/6000.3/Documentation/Manual/ui-systems/create-world-space-ui.html)
- [Unity Manual: ListView UXML element (6000.3)](https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-uxml-element-ListView.html)
- [Unity Manual: Control textures of the dynamic atlas (6000.3)](https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-control-textures-of-the-dynamic-atlas.html)
- [Unity Manual: Optimizing performance — best practice guide (6000.4)](https://docs.unity3d.com/6000.4/Documentation/Manual/best-practice-guides/ui-toolkit-for-advanced-unity-developers/optimizing-performance.html)
- [GitHub: artstorm/ui-toolkit-safe-area](https://github.com/artstorm/ui-toolkit-safe-area)
- [Unity Discussions: Get actual size of a VisualElement considering PanelSettings PanelScaleMode.ScaleWithScreenSize](https://discussions.unity.com/t/get-actual-size-of-a-visualelement-considering-panelsettings-panelscalemode-scalewithscreensize/906698)
- [Unity Discussions: UI Toolkit frustrations](https://discussions.unity.com/t/ui-toolkit-frustrations/1685389)
- [Unity Discussions: Performance of UI Toolkit](https://discussions.unity.com/t/performance-of-ui-toolkit/1563732)
- [Unity Discussions: UIToolkit rendering is extremely slow on older Android devices](https://discussions.unity.com/t/uitoolkit-rendering-is-extremely-slow-on-older-android-devices/1561024)
- [Unity Discussions: What's wrong with UI Toolkit?](https://discussions.unity.com/t/whats-wrong-with-ui-toolkit/1693143)
- [Unity Discussions: UI Toolkit Runtime Bindings performance](https://discussions.unity.com/t/ui-toolkit-runtime-bindings-performance/1593988)
- [Angry Shark Studio: Unity UI Toolkit vs UGUI: 2025 Developer Guide](https://www.angry-shark-studio.com/blog/unity-ui-toolkit-vs-ugui-2025-guide/)

Not verified / no reliable source found:
- Direct Reddit r/Unity3D threads with a developed "for/against" opinion on UI Toolkit for mobile 2D games — the search turned up no relevant results.
- The official Unity article "UI Toolkit at runtime: Get the breakdown" — found via search, but a WebFetch on the URL returned HTTP 403 (access blocked), so its content is not included in this file as a primary source; only independently quoted fragments are used, where the wording is confirmed by other openly accessible pages.
- The Medium article "Unity UI Toolkit: Safe Area" (idimus) with the full code of a custom `SafeArea : VisualElement` — WebFetch returned HTTP 403, content not confirmed directly; section 6 only uses wording confirmed by the `artstorm/ui-toolkit-safe-area` package README and general facts about `Screen.safeArea`.
- The exact Unity version in which the `LargePixelCoverage` usage hint appeared and exactly when the slow-rendering bug on the Redmi Note 4 was fixed — per the thread this is "Unity 6.3 beta" and later versions, but independent confirmation in the release notes wasn't checked.
