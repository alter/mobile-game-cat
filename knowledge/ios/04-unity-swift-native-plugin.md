# Unity + a native Swift plugin for iOS

Data collected: 2026-08-24. Stack: Unity 6.3 LTS (generates an Xcode project with the `UnityFramework` and `Unity-iPhone` targets), Xcode, Swift.

## In brief

- Plugin files (`.swift`, `.m`, `.mm`, `.c`, `.cpp`, `.h`, `.a`) go into `Assets/Plugins/iOS` — Unity automatically copies them into the generated Xcode project and restricts them to the iOS platform. [Unity — Automated plug-in integration](https://docs.unity3d.com/6000.3/Documentation/Manual/ios-native-plugin-automated-integration.html)
- Swift cannot be called from C# directly through `[DllImport]`, because we can only export functions with a C-compatible signature; for this, Swift has the `@_cdecl` attribute, which exports a function with C linkage without name mangling. [Unity — Create a native plug-in for iOS](https://docs.unity3d.com/Manual/ios-native-plugin-create.html)
- A callback from native code into C# — two ways: `UnitySendMessage("GameObjectName", "MethodName", "string")` (simple, but asynchronous, with a one-frame delay, and only `void MethodName(string)`) or a delegate registered via `[DllImport]` and a static method with the `[MonoPInvokeCallback]` attribute. [Unity — Create callbacks from native code](https://docs.unity3d.com/6000.3/Documentation/Manual/ios-native-plugin-call-back.html)
- Strings returned from native code into Unity must be UTF-8 and heap-allocated — Mono frees such memory itself; passing structs/arrays of strings in the reverse direction requires manual work with `Marshal.AllocHGlobal`/`Marshal.FreeHGlobal`. [Unity — Create a native plug-in for iOS](https://docs.unity3d.com/Manual/ios-native-plugin-create.html)
- Xcode settings for a Swift plugin (`SWIFT_VERSION`, `SWIFT_OBJC_BRIDGING_HEADER`, `ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES`) can and should be set from a `[PostProcessBuild]` script via `PBXProject`, separately for the `UnityFramework` target and the main application target. [Unity — Scripting API: PBXProject](https://docs.unity3d.com/ScriptReference/iOS.Xcode.PBXProject.html)
- `PHPickerViewController` — the modern replacement for `UIImagePickerController` for picking from the gallery, runs outside the app's process and **does not require** `NSPhotoLibraryUsageDescription`, as long as the `PHAsset` itself isn't requested. [Apple — PHPickerViewController](https://developer.apple.com/documentation/photosui/phpickerviewcontroller)
- For capturing photos with the camera, `NSCameraUsageDescription` is always required if the app accesses the camera. [Apple — NSCameraUsageDescription](https://developer.apple.com/documentation/bundleresources/information-property-list/nscamerausagedescription)
- Known issue — "Always Embed Swift Standard Libraries" must be `NO` on both the main target and `UnityFramework`, otherwise the build either fails App Store validation ("disallowed file 'Frameworks'") or fails to build ("'UnityFramework/UnityFramework.h' file not found"). [GitHub — yasirkula/UnityNativeGallery issue #234](https://github.com/yasirkula/UnityNativeGallery/issues/234)

## 1. Where to place code and why an Objective-C/`@_cdecl` bridge is needed

Unity supports automated plugin integration: files with the extensions `.a, .m, .mm, .c, .cpp, .h, .swift`, placed in `Assets/Plugins/iOS`, are copied into the generated Xcode project, and Unity restricts their use to the iOS platform. An important detail — after copying, the files are **no longer linked** to the originals in the Unity project: if you change them directly in Xcode, the changes must be manually carried back into Unity, otherwise the next build will overwrite them. [Unity — Automated plug-in integration](https://docs.unity3d.com/6000.3/Documentation/Manual/ios-native-plugin-automated-integration.html)

The reason Swift can't be pulled from C# directly through `[DllImport("__Internal")]` is that the Swift compiler uses name mangling by default, while `DllImport` looks up a symbol by its exact string name with C linkage. For C++/Objective-C++ functions, the solution is to wrap the declaration in `extern "C" { ... }`; functions in plain C and Objective-C already use C linkage and need no wrapper. For Swift, Unity recommends the `@_cdecl` attribute:

```swift
@_cdecl("FooPluginFunction")
func AnythingFooPluginFunction() -> Float {
    return 3.14
}
```

```csharp
[DllImport ("__Internal")]
private static extern float FooPluginFunction();
```

`"__Internal"` is used for statically linked code (that is, code that Unity embeds directly into `UnityFramework`); for a separate `.dylib`, the library name is given instead of `"__Internal"`. [Unity — Create a native plug-in for iOS](https://docs.unity3d.com/Manual/ios-native-plugin-create.html)

In practice, if a project needs more complex data exchange (objects, JSON, callbacks), a thin Objective-C(++) layer is sometimes still placed between Swift and Unity, since historically this is how interaction with Unity was first documented (see Unity's official example above with `extern "C"` for C++/Objective-C++), whereas `@_cdecl` in Swift is a newer, more minimal path that Unity itself explicitly supports and recommends directly, without a mandatory Objective-C layer. [Unity — Create a native plug-in for iOS](https://docs.unity3d.com/Manual/ios-native-plugin-create.html)

Another official Unity warning: managed-unmanaged calls (managed↔unmanaged) on iOS are fairly CPU-expensive, so many native method calls per frame should be avoided, and native methods should be wrapped with an additional C# layer that returns stubs in the editor (since the native plugin on iOS only works on a real device, not in the Editor). [Unity — Automated plug-in integration](https://docs.unity3d.com/6000.3/Documentation/Manual/ios-native-plugin-automated-integration.html)

## 2. A full end-to-end example: C# → Objective-C/`@_cdecl` → Swift, and back

### 2.1. Direct call: C# → Swift via `@_cdecl`

Swift code with the logic (for example, running our Vision request from file 03):

```swift
// AnimalDetector.swift, Assets/Plugins/iOS/AnimalDetector.swift
import Foundation

@_cdecl("AnimalDetector_isPhotoCat")
func AnimalDetector_isPhotoCat(_ jpegPathCString: UnsafePointer<CChar>) -> Bool {
    let path = String(cString: jpegPathCString)
    // ... run VNRecognizeAnimalsRequest / RecognizeAnimalsRequest on the image at path ...
    return true // stub
}
```

C# wrapper:

```csharp
using System.Runtime.InteropServices;

public static class AnimalDetectorBridge
{
#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern bool AnimalDetector_isPhotoCat(string jpegPath);
#endif

    public static bool IsPhotoCat(string jpegPath)
    {
#if UNITY_IOS && !UNITY_EDITOR
        return AnimalDetector_isPhotoCat(jpegPath);
#else
        return false; // stub for the editor and other platforms
#endif
    }
}
```

The `@_cdecl` → `[DllImport("__Internal")]` scheme is confirmed in the official Unity documentation as the recommended approach for Swift plugins. [Unity — Create a native plug-in for iOS](https://docs.unity3d.com/Manual/ios-native-plugin-create.html)

### 2.2. Callback, method 1 — `UnitySendMessage`

Used when the callback is needed once/rarely and a one-frame delay isn't critical — for example, "photo processed, here's the result." Official Unity description:

```
UnitySendMessage("GameObjectName1", "MethodName1", "Message to send");
```

"From native code, you can only call script methods that correspond to the following signature: `void MethodName(string message)`." Limitations: the call is asynchronous and executes with a one-frame delay; if several GameObjects have the same name, conflicts are possible. [Unity — Create callbacks from native code](https://docs.unity3d.com/6000.3/Documentation/Manual/ios-native-plugin-call-back.html)

The official Unity documentation shows the `UnitySendMessage` call only as a C function called from "native code" (that is, calling it from C/Objective-C linked into `UnityFramework` is unambiguously supported). No direct official example of calling `UnitySendMessage` specifically from Swift was found in open sources — "no reliable source found." Below is a construction we put together ourselves, based on the fact that `@_silgen_name` is a real (but not officially documented) Swift attribute for binding to an existing C symbol; in practice this combination should be verified on a real build rather than assumed to work "out of the box":

```swift
import Foundation

// UnityFramework exports UnitySendMessage as a C function.
// @_silgen_name is an undocumented Swift attribute for binding to an existing
// symbol; use with caution and verify on a real build.
@_silgen_name("UnitySendMessage")
func UnitySendMessage(_ gameObject: UnsafePointer<CChar>, _ method: UnsafePointer<CChar>, _ message: UnsafePointer<CChar>)

func notifyUnity(isCat: Bool) {
    "AnimalDetectorReceiver".withCString { go in
        "OnAnimalDetected".withCString { method in
            "\(isCat)".withCString { msg in
                UnitySendMessage(go, method, msg)
            }
        }
    }
}
```

A safer option in terms of documentation is to call `UnitySendMessage` not from Swift, but from a thin Objective-C(++) layer (as shown in Unity's official C example above), with the Swift function exposed via `@_cdecl` and called specifically from that layer.

C# receiver:

```csharp
using UnityEngine;

public class AnimalDetectorReceiver : MonoBehaviour
{
    // The method must be public and accept a single string parameter.
    public void OnAnimalDetected(string message)
    {
        bool isCat = bool.Parse(message);
        Debug.Log("Received message from native plug-in: " + message);
    }
}
```

The requirement for the receiver method's signature is from the official Unity documentation. [Unity — Create callbacks from native code](https://docs.unity3d.com/6000.3/Documentation/Manual/ios-native-plugin-call-back.html)

### 2.3. Callback, method 2 — delegate + `MonoPInvokeCallback`

Used when frequent/synchronous callbacks are needed without a one-frame delay (for example, a callback during processing, not at the end). Official Unity example:

```csharp
delegate void MyFuncType();

[AOT.MonoPInvokeCallback(typeof(MyFuncType))]
static void MyFunction() { }

[DllImport ("__Internal")]
static extern void RegisterCallback(MyFuncType func);
```

```c
typedef void (*MyFuncType)();

void RegisterCallback(MyFuncType func) {}
```

A method marked with `[MonoPInvokeCallback]` must be **static** — this is a key requirement, not just a recommendation: if native code holds a raw pointer to a function and the C# delegate is not static (or is a closure), Mono/IL2CPP may garbage-collect that delegate while the native side still references it ("callbackOnCollectedDelegate"). [Unity — Create callbacks from native code](https://docs.unity3d.com/6000.3/Documentation/Manual/ios-native-plugin-call-back.html)

On the Swift side, such a C-compatible function pointer can be accepted as an `@convention(c)` closure:

```swift
@_cdecl("AnimalDetector_registerCallback")
func AnimalDetector_registerCallback(_ callback: @escaping @convention(c) (Bool) -> Void) {
    // store the callback and call it after Vision analysis finishes
    savedCallback = callback
}

var savedCallback: (@convention(c) (Bool) -> Void)?

func finishDetection(isCat: Bool) {
    savedCallback?(isCat)
}
```

```csharp
public class AnimalDetectorBridge
{
    delegate void AnimalDetectedCallback(bool isCat);

    [AOT.MonoPInvokeCallback(typeof(AnimalDetectedCallback))]
    static void OnAnimalDetected(bool isCat)
    {
        UnityEngine.Debug.Log("isCat: " + isCat);
    }

    [DllImport("__Internal")]
    private static extern void AnimalDetector_registerCallback(AnimalDetectedCallback callback);

    public static void RegisterCallback()
    {
        AnimalDetector_registerCallback(OnAnimalDetected);
    }
}
```

**When to use which method:** `UnitySendMessage` is simpler to implement, doesn't require matching delegate signatures between Swift/C#, but is asynchronous (one-frame delay) and requires unique GameObject names; a delegate + `MonoPInvokeCallback` gives a synchronous "in the moment" call, suited for frequent or latency-sensitive callbacks, but requires careful handling of the delegate's lifetime (the method must be static). Both options are from the official Unity documentation. [Unity — Create callbacks from native code](https://docs.unity3d.com/6000.3/Documentation/Manual/ios-native-plugin-call-back.html)

## 3. Passing data: strings, byte[], images

### 3.1. Strings

Official Unity rule: "Ensure string values returned from a native method are UTF–8 encoded and allocated on the heap" — Mono/IL2CPP itself frees such memory when a string is returned from a native method into managed code. [Unity — Create a native plug-in for iOS](https://docs.unity3d.com/Manual/ios-native-plugin-create.html)

A typical mistake is returning a string not in UTF-8 (for example, in `NSString`'s default encoding without an explicit `.utf8`) or returning a pointer to stack memory or automatically-freed ARC memory instead of the heap — by the time the string is read on the C# side, the memory may already be invalid.

In the C# → native code direction (passing a string as a `[DllImport]` parameter), Unity usually marshals it automatically (P/Invoke itself creates a temporary C string for the duration of the call). If you need to manually assemble a struct containing strings, or an array of strings, you'll have to use `Marshal.AllocHGlobal`/`Marshal.StringToHGlobalAnsi` and remember to call `Marshal.FreeHGlobal` once the buffer is no longer needed — `AllocHGlobal` allocates unmanaged memory that the garbage collector knows nothing about and will never free on its own.

### 3.2. `byte[]`

For passing binary data (for example, JPEG of an already-cropped photo) into Unity, the typical approach is to pass a native pointer (`IntPtr`) and the buffer length as a separate parameter, then copy the data on the C# side via `Marshal.Copy` into a managed `byte[]`. No single official Unity API for this exact scenario (an analogue of `UnitySendMessage` for binary data) was found — this pattern is generic to P/Invoke, not specific to the Unity API.

### 3.3. Images

Passing a `UIImage`/`CGImage` directly into Unity is impossible — Objective-C/Swift object types don't cross the C ABI boundary. The standard approach is to serialize the image on the native side (JPEG/PNG into a `byte[]` or a temporary file on disk) and pass either a file path (a string) or a byte buffer with a length (see 3.2) into Unity, after which a `Texture2D` is created in C# via `LoadImage(byte[])` (`Texture2D` is a Unity Engine API type, not separately verified within the scope of this research).

## 4. Xcode project settings for Swift in Unity

Key build settings that must be set on the generated Xcode project so that the Swift plugin builds and passes App Store validation:

- `SWIFT_VERSION` — the Swift language version for the target.
- `SWIFT_OBJC_BRIDGING_HEADER` — the path to the bridging header, if access to Objective-C code from Swift is needed (relevant if the plugin has mixed Objective-C/Swift code in `Assets/Plugins/iOS`).
- `ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES` — must be **`YES` only on the main application target** and **`NO` on the `UnityFramework` target**; otherwise either a build error occurs ("`'UnityFramework/UnityFramework.h' file not found`"), or the archive fails App Store validation with an error about a disallowed `Frameworks` file. [GitHub — yasirkula/UnityNativeGallery issue #234](https://github.com/yasirkula/UnityNativeGallery/issues/234)

This can (and, for a team/CI build, should) be configured automatically from a `[PostProcessBuild]` script via `UnityEditor.iOS.Xcode.PBXProject`. The key methods of this class — from the official Unity Scripting API pages:

```csharp
public void SetBuildProperty(string targetGuid, string name, string value);
public void SetBuildProperty(IEnumerable<string> targetGuids, string name, string value);
public string GetUnityFrameworkTargetGuid(); // GUID of the UnityFramework target (code, plugins, linking)
public string GetUnityMainTargetGuid();      // GUID of the main application target
public void ReadFromFile(string path);
public void WriteToFile(string path);
public static string GetPBXProjectPath(string buildPath);
```

[Unity — Scripting API: PBXProject](https://docs.unity3d.com/ScriptReference/iOS.Xcode.PBXProject.html), [Unity — Scripting API: PBXProject.SetBuildProperty](https://docs.unity3d.com/ScriptReference/iOS.Xcode.PBXProject.SetBuildProperty.html)

Official Unity example for `SetBuildProperty` (script structure; the property value in the example is `ENABLE_BITCODE`, but the pattern is identical for `SWIFT_VERSION`/`ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES`):

```csharp
using UnityEditor;
using System.IO;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

public class Sample_SetBuildProperty
{
    [PostProcessBuild]
    public static void OnPostprocessBuild(BuildTarget buildTarget, string pathToBuiltProject)
    {
        if (buildTarget != BuildTarget.iOS)
            return;

        string projectPath = PBXProject.GetPBXProjectPath(pathToBuiltProject);
        PBXProject pbxProject = new PBXProject();
        pbxProject.ReadFromFile(projectPath);

        string unityFrameworkTargetGuid = pbxProject.GetUnityFrameworkTargetGuid();
        pbxProject.SetBuildProperty(unityFrameworkTargetGuid, "ENABLE_BITCODE", "NO");
        pbxProject.WriteToFile(projectPath);
    }
}
```

[Unity — Scripting API: PBXProject.SetBuildProperty](https://docs.unity3d.com/ScriptReference/iOS.Xcode.PBXProject.SetBuildProperty.html)

By analogy, for our task (Swift version and separate `ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES` settings for the two targets):

```csharp
[PostProcessBuild]
public static void OnPostprocessBuild(BuildTarget buildTarget, string pathToBuiltProject)
{
    if (buildTarget != BuildTarget.iOS) return;

    string projectPath = PBXProject.GetPBXProjectPath(pathToBuiltProject);
    var pbxProject = new PBXProject();
    pbxProject.ReadFromFile(projectPath);

    string frameworkTarget = pbxProject.GetUnityFrameworkTargetGuid();
    string mainTarget = pbxProject.GetUnityMainTargetGuid();

    pbxProject.SetBuildProperty(frameworkTarget, "SWIFT_VERSION", "5.0");
    pbxProject.SetBuildProperty(frameworkTarget, "ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES", "NO");
    pbxProject.SetBuildProperty(mainTarget, "ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES", "YES");

    pbxProject.WriteToFile(projectPath);
}
```

The combination of methods (`GetUnityFrameworkTargetGuid`/`GetUnityMainTargetGuid`/`SetBuildProperty`) is individually confirmed by the official Unity documentation; combining them into a single script for these specific keys (`SWIFT_VERSION`, `ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES`) is our own composition following the documented pattern, not a verbatim quote of a single Apple/Unity example.

## 5. Camera and photo library access

### 5.1. `PHPickerViewController` vs. `UIImagePickerController`

`UIImagePickerController` is not officially deprecated and remains the current API for **capturing photos with the camera** (source type `.camera`) — available since iOS 2.0. For more flexible camera control (a custom UI over the preview), Apple recommends `AVFoundation`, and for "full" gallery selection with extended capabilities, `PhotoKit`. [Apple — UIImagePickerController](https://developer.apple.com/documentation/uikit/uiimagepickercontroller)

`PHPickerViewController` (the PhotosUI framework, since iOS 14.0) — the modern replacement for `UIImagePickerController` specifically for **picking existing photos/videos from the gallery**. The key difference is that the picture/video is selected by the user in a system UI that runs **outside the app's process** ("out-of-process"), so the app only receives what the user selected, without access to the entire library. Official wording: "PHPickerViewController is an alternative to UIImagePickerController that improves stability and reliability," with support for deferred image loading, reliable handling of RAW/panoramas, and stricter validation. [Apple — PHPickerViewController](https://developer.apple.com/documentation/photosui/phpickerviewcontroller)

**For our task** (the player either photographs a cat with the camera or picks an existing photo from the gallery), both APIs are relevant in different places: `UIImagePickerController` with `sourceType = .camera` (or a custom AVFoundation controller) — for capture, `PHPickerViewController` — if picking an existing photo from the gallery is allowed.

### 5.2. Required Info.plist keys

- `NSCameraUsageDescription` — required if the app uses camera access APIs. Required on iOS 7.0+. [Apple — NSCameraUsageDescription](https://developer.apple.com/documentation/bundleresources/information-property-list/nscamerausagedescription)
- `NSPhotoLibraryUsageDescription` — required if the app reads or writes to the photo library via PhotoKit (`PHAsset`, etc.). Required on iOS 6.0+. If the app **only adds** assets (doesn't read existing ones), `NSPhotoLibraryAddUsageDescription` is sufficient instead. [Apple — NSPhotoLibraryUsageDescription](https://developer.apple.com/documentation/bundleresources/information-property-list/nsphotolibraryusagedescription)

### 5.3. When photo library permission isn't required at all

Both `UIImagePickerController` (when picking from the gallery) and `PHPickerViewController` work as separate, "sandboxed" pickers that don't request `NSPhotoLibraryUsageDescription` unless the app tries to directly obtain a `PHAsset` via PhotoKit. The official `PHPickerViewController` documentation confirms this explicitly: "Unlike UIImagePickerController, PHPickerViewController can provide `PHLivePhoto` objects without requiring photo library permissions" — meaning the basic scenario "user picked a photo → app got a copy" requires no permission at all; permission only comes into play if the code goes further and requests the original `PHAsset` from the library. [Apple — PHPickerViewController](https://developer.apple.com/documentation/photosui/phpickerviewcontroller)

For capturing with the camera, `NSCameraUsageDescription` is always required — there are no "sandbox"-type exceptions here, since the camera itself is a sensitive sensor, not a selection of an already-existing file. [Apple — NSCameraUsageDescription](https://developer.apple.com/documentation/bundleresources/information-property-list/nscamerausagedescription)

## 6. Cropping/resizing an image to 512×512 and encoding as JPEG/base64

An example using official Apple APIs (`UIGraphicsImageRenderer`, `UIImage.jpegData(compressionQuality:)`), assembled by us for this specific task (resizing and center-cropping to a 512×512 square before sending to the server):

```swift
import UIKit

extension UIImage {
    /// Resizes and crops the image to a targetSize x targetSize square
    /// (first an aspect-fill resize, then a centered crop).
    func resizedAndCroppedSquare(to targetSize: CGFloat) -> UIImage? {
        let originalSize = self.size
        let scale = max(targetSize / originalSize.width, targetSize / originalSize.height)
        let scaledSize = CGSize(width: originalSize.width * scale, height: originalSize.height * scale)

        let renderer = UIGraphicsImageRenderer(size: CGSize(width: targetSize, height: targetSize))
        return renderer.image { _ in
            let origin = CGPoint(
                x: (targetSize - scaledSize.width) / 2,
                y: (targetSize - scaledSize.height) / 2
            )
            self.draw(in: CGRect(origin: origin, size: scaledSize))
        }
    }
}

func encodeForUpload(_ image: UIImage) -> String? {
    guard let squareImage = image.resizedAndCroppedSquare(to: 512),
          // compressionQuality: 0.0 - maximum compression (low quality),
          // 1.0 - minimum compression (best quality).
          let jpegData = squareImage.jpegData(compressionQuality: 0.8) else {
        return nil
    }
    return jpegData.base64EncodedString()
}
```

The signatures used in the example are from Apple's documentation:

```swift
init(size: CGSize) // UIGraphicsImageRenderer
func image(actions: (UIGraphicsImageRendererContext) -> Void) -> UIImage
func jpegData(compressionQuality: CGFloat) -> Data?
```

[Apple — UIGraphicsImageRenderer](https://developer.apple.com/documentation/uikit/uigraphicsimagerenderer), [Apple — UIImage.jpegData(compressionQuality:)](https://developer.apple.com/documentation/uikit/uiimage/jpegdata(compressionquality:))

**On payload size:** base64 increases the data size by roughly a third compared to the original binary buffer (encoding 3 bytes into 4 characters) — this is a general property of base64, not something specific to Apple/Unity; no separate source with an exact percentage was found within the scope of this research, but the "3 bytes → 4 characters" principle (that is, a ~33% increase) follows from the very nature of base64. If the server can accept `multipart/form-data` or a raw request body, sending JPEG bytes without a base64 wrapper will be noticeably more compact than a base64 string.

## 7. Pitfalls from developer reports

- **`ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES` on two targets.** If it's not set to `NO` on `UnityFramework`, the build fails with the error `'UnityFramework/UnityFramework.h' file not found`; if it's left `YES` there (or not reconciled with the main target), the archive fails App Store Connect validation with an error about a disallowed `Frameworks` file. This is discussed as a systemic issue, not a one-off bug in a specific plugin. [GitHub — yasirkula/UnityNativeGallery issue #234](https://github.com/yasirkula/UnityNativeGallery/issues/234)
- **`GetUnityFrameworkTargetGuid` unavailable on older Unity versions.** A documented compile error: "'PBXProject' does not contain a definition for 'GetUnityFrameworkTargetGuid' and no accessible extension method 'GetUnityFrameworkTargetGuid' accepting a first argument of type 'PBXProject' could be found" — occurs when a third-party plugin calls this method but the installed Unity version doesn't yet contain it (the method isn't present in every editor version). [GitHub — gree/unity-webview issue #468](https://github.com/gree/unity-webview/issues/468)
- **Changes to plugin files made directly in the Xcode project are lost.** Since Unity copies (not symlinks, per the current documentation) `.swift`/`.m`/`.mm`/`.h` files into the generated Xcode project on every build, any edits made directly in Xcode will be overwritten the next time the project is regenerated from Unity — fixes need to be carried back into `Assets/Plugins/iOS`. [Unity — Automated plug-in integration](https://docs.unity3d.com/6000.3/Documentation/Manual/ios-native-plugin-automated-integration.html)
- **Managed↔unmanaged calls are expensive on iOS.** Official Unity warning — avoid calling many native methods per frame due to the CPU cost of transitioning between managed and unmanaged code. [Unity — Automated plug-in integration](https://docs.unity3d.com/6000.3/Documentation/Manual/ios-native-plugin-automated-integration.html)
- **The native plugin doesn't work in the Editor.** Code called through `[DllImport("__Internal")]` only actually exists in the built iOS app — calls of this kind need to be wrapped in `#if UNITY_IOS && !UNITY_EDITOR` with stubs substituted for the editor and other platforms (this is also reflected in our examples in section 2).
- **The mandatory static requirement for a method with `[MonoPInvokeCallback]`.** A technical breakdown of the "callbackOnCollectedDelegate" problem: when native code holds a function pointer corresponding to a managed delegate, that delegate can be garbage-collected, and the native code will then access already-invalid memory. `MonoPInvokeCallback` "tells Mono's AOT compiler to generate this stub statically" — that is, it tells Mono's AOT compiler to generate the stub ahead of time and statically, so it won't be garbage-collected (unlike a JIT scenario, where stubs are created on the fly and destroyed along with the delegate). This is especially important on iOS, where AOT compilation is used and dynamic stub generation is impossible. [GitHub dotnet/runtime — Is there equivalent to MonoPInvokeCallback in dotnet?](https://github.com/dotnet/runtime/discussions/65296)
- **Practical difficulties with `[AOT.MonoPInvokeCallback]` in real projects.** In a separate discussion on the Unity forum, developers noted that the attribute needs to be specified with the full path `[AOT.MonoPInvokeCallback(typeof(...))]` (rather than just `[MonoPInvokeCallback(...)]`, if there's no corresponding `using`), and that adding `[UnmanagedFunctionPointer(CallingConvention.Cdecl)]` on top of the delegate caused cross-compilation to fail in one case — the attribute had to be removed. No direct participants confirming this combination works on Android were found in that discussion — the question remained open. [Unity Discussions — MonoPInvokeCallback in unity?](https://discussions.unity.com/t/monopinvokecallback-in-unity/473887)
- **PHPicker/UIImagePickerController and `PHAsset`.** If an image is selected via a picker, but the code then tries to obtain the `PHAsset` for that same photo directly (rather than just using the copy it received), this may suddenly require full photo library permission — Apple recommends, in this case, avoiding requesting library access altogether where possible and staying at the level of what the picker itself returned.

## Sources

- [Unity — Create a native plug-in for iOS](https://docs.unity3d.com/Manual/ios-native-plugin-create.html)
- [Unity — Create callbacks from native code](https://docs.unity3d.com/6000.3/Documentation/Manual/ios-native-plugin-call-back.html)
- [Unity — Automated plug-in integration](https://docs.unity3d.com/6000.3/Documentation/Manual/ios-native-plugin-automated-integration.html)
- [Unity — Scripting API: PBXProject](https://docs.unity3d.com/ScriptReference/iOS.Xcode.PBXProject.html)
- [Unity — Scripting API: PBXProject.SetBuildProperty](https://docs.unity3d.com/ScriptReference/iOS.Xcode.PBXProject.SetBuildProperty.html)
- [Apple — UIImagePickerController](https://developer.apple.com/documentation/uikit/uiimagepickercontroller)
- [Apple — PHPickerViewController](https://developer.apple.com/documentation/photosui/phpickerviewcontroller)
- [Apple — NSCameraUsageDescription](https://developer.apple.com/documentation/bundleresources/information-property-list/nscamerausagedescription)
- [Apple — NSPhotoLibraryUsageDescription](https://developer.apple.com/documentation/bundleresources/information-property-list/nsphotolibraryusagedescription)
- [Apple — UIGraphicsImageRenderer](https://developer.apple.com/documentation/uikit/uigraphicsimagerenderer)
- [Apple — UIImage.jpegData(compressionQuality:)](https://developer.apple.com/documentation/uikit/uiimage/jpegdata(compressionquality:))
- [GitHub — yasirkula/UnityNativeGallery, issue #234](https://github.com/yasirkula/UnityNativeGallery/issues/234)
- [GitHub — gree/unity-webview, issue #468](https://github.com/gree/unity-webview/issues/468)
- [Unity Discussions — MonoPInvokeCallback in unity?](https://discussions.unity.com/t/monopinvokecallback-in-unity/473887)
- [GitHub dotnet/runtime — Is there equivalent to MonoPInvokeCallback in dotnet? (discussion #65296)](https://github.com/dotnet/runtime/discussions/65296)




