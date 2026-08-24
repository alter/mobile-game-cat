# Unity + нативный плагин на Swift для iOS

Дата сбора: 2026-08-24. Стек: Unity 6.3 LTS (генерирует Xcode-проект с таргетами `UnityFramework` и `Unity-iPhone`), Xcode, Swift.

## Кратко

- Файлы плагина (`.swift`, `.m`, `.mm`, `.c`, `.cpp`, `.h`, `.a`) кладутся в `Assets/Plugins/iOS` — Unity автоматически копирует их в сгенерированный Xcode-проект и ограничивает платформой iOS. [Unity — Automated plug-in integration](https://docs.unity3d.com/6000.3/Documentation/Manual/ios-native-plugin-automated-integration.html)
- Swift нельзя вызвать из C# напрямую через `[DllImport]`, потому что мы можем экспортировать только функции с C-совместимой сигнатурой; для этого в Swift есть атрибут `@_cdecl`, который экспортирует функцию с C linkage без мангла имени. [Unity — Create a native plug-in for iOS](https://docs.unity3d.com/Manual/ios-native-plugin-create.html)
- Обратный вызов из нативного кода в C# — два способа: `UnitySendMessage("GameObjectName", "MethodName", "строка")` (просто, но асинхронно, с задержкой в один кадр, и только `void MethodName(string)`) или делегат, зарегистрированный через `[DllImport]` и статический метод с атрибутом `[MonoPInvokeCallback]`. [Unity — Create callbacks from native code](https://docs.unity3d.com/6000.3/Documentation/Manual/ios-native-plugin-call-back.html)
- Строки, возвращаемые из нативного кода в Unity, должны быть в UTF-8 и выделены в куче — Mono сам освобождает такую память; для передачи структур/массивов строк в обратном направлении нужна ручная работа с `Marshal.AllocHGlobal`/`Marshal.FreeHGlobal`. [Unity — Create a native plug-in for iOS](https://docs.unity3d.com/Manual/ios-native-plugin-create.html)
- Настройки Xcode для Swift-плагина (`SWIFT_VERSION`, `SWIFT_OBJC_BRIDGING_HEADER`, `ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES`) можно и нужно проставлять из `[PostProcessBuild]`-скрипта через `PBXProject`, отдельно для таргетов `UnityFramework` и главного таргета приложения. [Unity — Scripting API: PBXProject](https://docs.unity3d.com/ScriptReference/iOS.Xcode.PBXProject.html)
- `PHPickerViewController` — современная замена `UIImagePickerController` для выбора из галереи, работает вне процесса приложения и **не требует** `NSPhotoLibraryUsageDescription`, пока не запрашивается сам `PHAsset`. [Apple — PHPickerViewController](https://developer.apple.com/documentation/photosui/phpickerviewcontroller)
- Для съёмки с камеры `NSCameraUsageDescription` обязателен всегда, если приложение обращается к камере. [Apple — NSCameraUsageDescription](https://developer.apple.com/documentation/bundleresources/information-property-list/nscamerausagedescription)
- Известная проблема — «Always Embed Swift Standard Libraries» должен быть `NO` и на главном таргете, и на `UnityFramework`, иначе сборка либо не проходит валидацию App Store («disallowed file 'Frameworks'»), либо не собирается («'UnityFramework/UnityFramework.h' file not found»). [GitHub — yasirkula/UnityNativeGallery issue #234](https://github.com/yasirkula/UnityNativeGallery/issues/234)

## 1. Где размещать код и почему нужен мост Objective-C/`@_cdecl`

Unity поддерживает автоматическую интеграцию плагинов: файлы с расширениями `.a, .m, .mm, .c, .cpp, .h, .swift`, помещённые в `Assets/Plugins/iOS`, копируются в сгенерированный Xcode-проект, а Unity ограничивает их использование платформой iOS. Важная деталь — после копирования файлы **больше не связаны** с оригиналами в Unity-проекте: если поменять их прямо в Xcode, изменения нужно вручную переносить обратно в Unity, иначе следующая сборка их перезапишет. [Unity — Automated plug-in integration](https://docs.unity3d.com/6000.3/Documentation/Manual/ios-native-plugin-automated-integration.html)

Причина, по которой Swift нельзя дёрнуть из C# напрямую через `[DllImport("__Internal")]`, — компилятор Swift по умолчанию использует мангл имён (name mangling), а `DllImport` ищет символ по точному строковому имени с C linkage. Для функций на C++/Objective-C++ решение — обернуть объявление в `extern "C" { ... }`; функции на чистом C и Objective-C уже используют C linkage и обёртки не требуют. Для Swift Unity рекомендует атрибут `@_cdecl`:

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

`"__Internal"` используется для статически слинкованного кода (то есть для кода, который Unity встраивает прямо в `UnityFramework`); для отдельных `.dylib` вместо `"__Internal"` указывается имя библиотеки. [Unity — Create a native plug-in for iOS](https://docs.unity3d.com/Manual/ios-native-plugin-create.html)

На практике, если проекту нужен более сложный обмен данными (объекты, JSON, коллбэки), между Swift и Unity иногда всё равно ставят тонкую прослойку Objective-C(++), поскольку исторически именно так в первую очередь документированы взаимодействия с Unity (см. официальный пример Unity выше с `extern "C"` для C++/Objective-C++), а `@_cdecl` в Swift — более новый и минималистичный путь, который сама Unity явно поддерживает и рекомендует напрямую, без обязательной Objective-C-прослойки. [Unity — Create a native plug-in for iOS](https://docs.unity3d.com/Manual/ios-native-plugin-create.html)

Ещё одно официальное предупреждение Unity: управляемо-неуправляемые вызовы (managed↔unmanaged) на iOS довольно затратны по процессору, поэтому не стоит дёргать много нативных методов за кадр, и нативные методы стоит оборачивать дополнительным слоем на C#, который в редакторе возвращает заглушки (так как нативный плагин на iOS работает только на реальном устройстве, не в Editor). [Unity — Automated plug-in integration](https://docs.unity3d.com/6000.3/Documentation/Manual/ios-native-plugin-automated-integration.html)

## 2. Полный сквозной пример: C# → Objective-C/`@_cdecl` → Swift, и обратно

### 2.1. Прямой вызов: C# → Swift через `@_cdecl`

Swift-код с логикой (например, запуск нашего Vision-запроса из файла 03):

```swift
// AnimalDetector.swift, Assets/Plugins/iOS/AnimalDetector.swift
import Foundation

@_cdecl("AnimalDetector_isPhotoCat")
func AnimalDetector_isPhotoCat(_ jpegPathCString: UnsafePointer<CChar>) -> Bool {
    let path = String(cString: jpegPathCString)
    // ... запуск VNRecognizeAnimalsRequest / RecognizeAnimalsRequest на изображении по path ...
    return true // заглушка
}
```

C#-обёртка:

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
        return false; // заглушка для редактора и других платформ
#endif
    }
}
```

Схема `@_cdecl` → `[DllImport("__Internal")]` подтверждена в официальной документации Unity как рекомендованный способ для Swift-плагинов. [Unity — Create a native plug-in for iOS](https://docs.unity3d.com/Manual/ios-native-plugin-create.html)

### 2.2. Обратный вызов, способ 1 — `UnitySendMessage`

Используется, когда обратный вызов нужен один раз/редко и не критична задержка в кадр — например, «фото обработано, вот результат». Официальное описание Unity:

```
UnitySendMessage("GameObjectName1", "MethodName1", "Message to send");
```

«From native code, you can only call script methods that correspond to the following signature: `void MethodName(string message)`.» Ограничения: вызов асинхронный и выполняется с задержкой в один кадр; если несколько GameObject имеют одинаковое имя, возможны конфликты. [Unity — Create callbacks from native code](https://docs.unity3d.com/6000.3/Documentation/Manual/ios-native-plugin-call-back.html)

Официальная документация Unity показывает вызов `UnitySendMessage` только как C-функцию, вызываемую из «native code» (то есть однозначно поддерживается вызов из C/Objective-C, слинкованного с `UnityFramework`). Прямого официального примера вызова `UnitySendMessage` именно из Swift в открытых источниках не найдено — «надёжных источников не найдено». Ниже — конструкция, которую мы составили сами, опираясь на то, что `@_silgen_name` — реальный (но не задокументированный официально) атрибут Swift для привязки к существующему C-символу; на практике эту связку стоит проверять на реальном билде, а не считать гарантированно рабочей «из коробки»:

```swift
import Foundation

// UnityFramework экспортирует UnitySendMessage как C-функцию.
// @_silgen_name — недокументированный атрибут Swift для привязки к существующему
// символу; использовать с осторожностью и проверять на реальной сборке.
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

Более безопасный с точки зрения документированности вариант — вызывать `UnitySendMessage` не из Swift, а из тонкой Objective-C(++)-прослойки (как показано в официальном C-примере Unity выше), а Swift-функцию оформить через `@_cdecl` и звать её именно из этой прослойки.

C#-приёмник:

```csharp
using UnityEngine;

public class AnimalDetectorReceiver : MonoBehaviour
{
    // Метод обязан быть public и принимать один параметр string.
    public void OnAnimalDetected(string message)
    {
        bool isCat = bool.Parse(message);
        Debug.Log("Received message from native plug-in: " + message);
    }
}
```

Требование к сигнатуре метода-приёмника — из официальной документации Unity. [Unity — Create callbacks from native code](https://docs.unity3d.com/6000.3/Documentation/Manual/ios-native-plugin-call-back.html)

### 2.3. Обратный вызов, способ 2 — делегат + `MonoPInvokeCallback`

Используется, когда нужны частые/синхронные обратные вызовы без задержки в кадр (например, коллбэк во время обработки, а не в конце). Официальный пример Unity:

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

Метод, помеченный `[MonoPInvokeCallback]`, обязан быть **статическим** — это ключевое требование, а не просто рекомендация: если нативный код держит сырой указатель на функцию, а C#-делегат не статический (либо является замыканием), Mono/IL2CPP может собрать такой делегат сборщиком мусора, пока на него ещё ссылается нативная сторона («callbackOnCollectedDelegate»). [Unity — Create callbacks from native code](https://docs.unity3d.com/6000.3/Documentation/Manual/ios-native-plugin-call-back.html)

Со стороны Swift такой C-совместимый указатель на функцию можно принять как `@convention(c)`-замыкание:

```swift
@_cdecl("AnimalDetector_registerCallback")
func AnimalDetector_registerCallback(_ callback: @escaping @convention(c) (Bool) -> Void) {
    // сохраняем callback и вызываем его после завершения анализа Vision
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

**Когда какой способ использовать:** `UnitySendMessage` — проще в реализации, не требует совпадения сигнатур делегата между Swift/C#, но асинхронна (задержка в кадр) и требует уникальных имён GameObject; делегат + `MonoPInvokeCallback` — синхронный вызов «в моменте», подходит для частых или чувствительных к задержке обратных вызовов, но требует аккуратной работы с временем жизни делегата (обязательная статичность метода). Оба варианта — из официальной документации Unity. [Unity — Create callbacks from native code](https://docs.unity3d.com/6000.3/Documentation/Manual/ios-native-plugin-call-back.html)

## 3. Передача данных: строки, byte[], изображения

### 3.1. Строки

Официальное правило Unity: «Ensure string values returned from a native method are UTF–8 encoded and allocated on the heap» — Mono/IL2CPP сам освобождает такую память при возврате строки из нативного метода в управляемый код. [Unity — Create a native plug-in for iOS](https://docs.unity3d.com/Manual/ios-native-plugin-create.html)

Типовая ошибка — вернуть строку не в UTF-8 (например, в кодировке по умолчанию для `NSString` без явного `.utf8`) или вернуть указатель на память в стеке/автоматически освобождаемую ARC-память вместо кучи — тогда к моменту чтения строки на стороне C# память уже может быть невалидна.

Направление C# → нативный код (передача строки как параметра `[DllImport]`) Unity обычно маршалит автоматически (P/Invoke сам создаёт временную C-строку на время вызова). Если же нужно вручную собрать структуру, содержащую строки, или массив строк, придётся использовать `Marshal.AllocHGlobal`/`Marshal.StringToHGlobalAnsi` и не забыть вызвать `Marshal.FreeHGlobal`, когда буфер больше не нужен — `AllocHGlobal` выделяет неуправляемую память, о которой сборщик мусора ничего не знает и которую сам никогда не освободит.

### 3.2. `byte[]`

Для передачи бинарных данных (например, JPEG уже обрезанного фото) в Unity типичный путь — передать нативный указатель (`IntPtr`) и длину буфера отдельным параметром, а на стороне C# скопировать данные через `Marshal.Copy` в управляемый `byte[]`. Официального единого API Unity именно под этот сценарий (аналога `UnitySendMessage` для бинарных данных) не найдено — этот паттерн общий для P/Invoke, а не специфичный для Unity API.

### 3.3. Изображения

Прямая передача `UIImage`/`CGImage` в Unity невозможна — типы объектов Objective-C/Swift не пересекают границу C ABI. Стандартный путь — сериализовать изображение на нативной стороне (JPEG/PNG в `byte[]` или временный файл на диске) и передать в Unity либо путь к файлу (строка), либо буфер байт с длиной (см. 3.2), после чего в C# создать `Texture2D` через `LoadImage(byte[])` (`Texture2D` — тип Unity Engine API, не проверялся отдельно в рамках этого исследования).

## 4. Настройки Xcode-проекта для Swift в Unity

Ключевые build settings, которые должны быть выставлены на сгенерированном Xcode-проекте, чтобы Swift-плагин собирался и проходил валидацию App Store:

- `SWIFT_VERSION` — версия языка Swift для таргета.
- `SWIFT_OBJC_BRIDGING_HEADER` — путь к bridging header, если нужен доступ к Objective-C-коду из Swift (актуально, если у плагина есть смешанный Objective-C/Swift код в `Assets/Plugins/iOS`).
- `ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES` — должен быть **`YES` только на главном таргете приложения** и **`NO` на таргете `UnityFramework`**; в противном случае либо возникает ошибка сборки «`'UnityFramework/UnityFramework.h' file not found`», либо архив не проходит валидацию App Store с ошибкой о недопустимом файле `Frameworks`. [GitHub — yasirkula/UnityNativeGallery issue #234](https://github.com/yasirkula/UnityNativeGallery/issues/234)

Настроить это можно (и для командной/CI-сборки нужно) автоматически из `[PostProcessBuild]`-скрипта через `UnityEditor.iOS.Xcode.PBXProject`. Ключевые методы этого класса — с официальных страниц Unity Scripting API:

```csharp
public void SetBuildProperty(string targetGuid, string name, string value);
public void SetBuildProperty(IEnumerable<string> targetGuids, string name, string value);
public string GetUnityFrameworkTargetGuid(); // GUID таргета UnityFramework (код, плагины, линковка)
public string GetUnityMainTargetGuid();      // GUID главного таргета приложения
public void ReadFromFile(string path);
public void WriteToFile(string path);
public static string GetPBXProjectPath(string buildPath);
```

[Unity — Scripting API: PBXProject](https://docs.unity3d.com/ScriptReference/iOS.Xcode.PBXProject.html), [Unity — Scripting API: PBXProject.SetBuildProperty](https://docs.unity3d.com/ScriptReference/iOS.Xcode.PBXProject.SetBuildProperty.html)

Официальный пример Unity для `SetBuildProperty` (структура скрипта, значение свойства в примере — `ENABLE_BITCODE`, но паттерн идентичен для `SWIFT_VERSION`/`ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES`):

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

По аналогии для нашей задачи (Swift-версия и раздельная настройка `ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES` для двух таргетов):

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

Комбинация методов (`GetUnityFrameworkTargetGuid`/`GetUnityMainTargetGuid`/`SetBuildProperty`) подтверждена официальной документацией Unity по отдельности; сведение их в один скрипт под конкретные ключи (`SWIFT_VERSION`, `ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES`) — наша компоновка по задокументированному паттерну, а не дословная цитата единого примера Apple/Unity.

## 5. Доступ к камере и фотоплёнке

### 5.1. `PHPickerViewController` против `UIImagePickerController`

`UIImagePickerController` официально не деприкейчен и остаётся текущим API для **захвата фото с камеры** (source type `.camera`) — доступен с iOS 2.0. Для более гибкого управления камерой (собственный UI поверх превью) Apple рекомендует `AVFoundation`, а для «полного» выбора из галереи с расширенными возможностями — `PhotoKit`. [Apple — UIImagePickerController](https://developer.apple.com/documentation/uikit/uiimagepickercontroller)

`PHPickerViewController` (фреймворк PhotosUI, с iOS 14.0) — современная замена `UIImagePickerController` именно для **выбора существующих фото/видео из галереи**. Ключевое отличие — картинка/видео выбирается пользователем в системном UI, который выполняется **вне процесса приложения** («out-of-process»), поэтому приложение получает только то, что выбрал пользователь, без доступа ко всей библиотеке. Официальная формулировка: «PHPickerViewController is an alternative to UIImagePickerController that improves stability and reliability», с поддержкой отложенной загрузки изображений, надёжной работой с RAW/панорамами, более строгой валидацией. [Apple — PHPickerViewController](https://developer.apple.com/documentation/photosui/phpickerviewcontroller)

**Для нашей задачи** (игрок либо снимает кота на камеру, либо выбирает готовое фото из галереи) актуальны оба API на разных участках: `UIImagePickerController` с `sourceType = .camera` (или собственный AVFoundation-контроллер) — для съёмки, `PHPickerViewController` — если разрешён выбор уже существующего фото из галереи.

### 5.2. Обязательные ключи Info.plist

- `NSCameraUsageDescription` — обязателен, если приложение использует API доступа к камере. Требуется на iOS 7.0+. [Apple — NSCameraUsageDescription](https://developer.apple.com/documentation/bundleresources/information-property-list/nscamerausagedescription)
- `NSPhotoLibraryUsageDescription` — обязателен, если приложение читает или пишет в фотобиблиотеку через PhotoKit (`PHAsset` и т. п.). Требуется на iOS 6.0+. Если приложение **только добавляет** ассеты (не читает существующие), вместо этого достаточно `NSPhotoLibraryAddUsageDescription`. [Apple — NSPhotoLibraryUsageDescription](https://developer.apple.com/documentation/bundleresources/information-property-list/nsphotolibraryusagedescription)

### 5.3. Когда разрешение на фотоплёнку не требуется вовсе

И `UIImagePickerController` (при выборе из галереи), и `PHPickerViewController` работают как отдельные, «песочные» пикеры, которые не запрашивают `NSPhotoLibraryUsageDescription`, пока приложение не пытается напрямую получить `PHAsset` через PhotoKit. Официальная документация `PHPickerViewController` это подтверждает явно: «Unlike UIImagePickerController, PHPickerViewController can provide `PHLivePhoto` objects without requiring photo library permissions» — то есть базовый сценарий «пользователь выбрал фото → приложение получило копию» не требует permission вообще, разрешение появляется только если код идёт дальше и запрашивает исходный `PHAsset` из библиотеки. [Apple — PHPickerViewController](https://developer.apple.com/documentation/photosui/phpickerviewcontroller)

Для съёмки на камеру `NSCameraUsageDescription` нужен всегда — здесь исключений по типу «песочницы» нет, поскольку сама камера — это чувствительный сенсор, а не выбор уже существующего файла. [Apple — NSCameraUsageDescription](https://developer.apple.com/documentation/bundleresources/information-property-list/nscamerausagedescription)

## 6. Обрезка/уменьшение изображения до 512×512 и кодирование в JPEG/base64

Пример на официальных API Apple (`UIGraphicsImageRenderer`, `UIImage.jpegData(compressionQuality:)`), собранный нами для конкретной задачи (уменьшение и центрированная обрезка до квадрата 512×512 перед отправкой на сервер):

```swift
import UIKit

extension UIImage {
    /// Уменьшает и обрезает изображение до квадрата targetSize x targetSize
    /// (сначала aspect-fill уменьшение, затем центрированная обрезка).
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
          // compressionQuality: 0.0 - максимальное сжатие (низкое качество),
          // 1.0 - минимальное сжатие (лучшее качество).
          let jpegData = squareImage.jpegData(compressionQuality: 0.8) else {
        return nil
    }
    return jpegData.base64EncodedString()
}
```

Сигнатуры, использованные в примере, — из документации Apple:

```swift
init(size: CGSize) // UIGraphicsImageRenderer
func image(actions: (UIGraphicsImageRendererContext) -> Void) -> UIImage
func jpegData(compressionQuality: CGFloat) -> Data?
```

[Apple — UIGraphicsImageRenderer](https://developer.apple.com/documentation/uikit/uigraphicsimagerenderer), [Apple — UIImage.jpegData(compressionQuality:)](https://developer.apple.com/documentation/uikit/uiimage/jpegdata(compressionquality:))

**Про размер полезной нагрузки:** base64 увеличивает размер данных примерно на треть по сравнению с исходным бинарным буфером (кодирование 3 байт в 4 символа) — это общее свойство base64, а не специфика Apple/Unity; отдельного источника с точным процентом в рамках этого исследования не открывалось, но принцип «3 байта → 4 символа» (то есть рост ~33%) следует из самой природы base64. Если сервер может принимать `multipart/form-data` или сырое тело запроса, передача JPEG-байтов без base64-обёртки будет заметно компактнее, чем строка Base64.

## 7. Подводные камни из отчётов разработчиков

- **`ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES` на двух таргетах.** Если не выставить `NO` на `UnityFramework`, сборка падает с ошибкой `'UnityFramework/UnityFramework.h' file not found`; если оставить `YES` там же (или не согласовать с главным таргетом), архив не проходит валидацию App Store Connect с ошибкой о недопустимом файле `Frameworks`. Обсуждается как системная проблема, а не единичный баг конкретного плагина. [GitHub — yasirkula/UnityNativeGallery issue #234](https://github.com/yasirkula/UnityNativeGallery/issues/234)
- **`GetUnityFrameworkTargetGuid` недоступен на старых Unity.** Зафиксированная ошибка компиляции: «'PBXProject' does not contain a definition for 'GetUnityFrameworkTargetGuid' and no accessible extension method 'GetUnityFrameworkTargetGuid' accepting a first argument of type 'PBXProject' could be found» — возникает, когда сторонний плагин вызывает этот метод, а установленная версия Unity его ещё не содержит (метод есть не во всех версиях редактора). [GitHub — gree/unity-webview issue #468](https://github.com/gree/unity-webview/issues/468)
- **Изменения в файлах плагина в самом Xcode-проекте теряются.** Так как Unity копирует (не симлинкует, согласно текущей документации) `.swift`/`.m`/`.mm`/`.h`-файлы в сгенерированный Xcode-проект при каждой сборке, любые правки, сделанные прямо в Xcode, будут перезаписаны при следующей генерации проекта из Unity — исправления нужно переносить обратно в `Assets/Plugins/iOS`. [Unity — Automated plug-in integration](https://docs.unity3d.com/6000.3/Documentation/Manual/ios-native-plugin-automated-integration.html)
- **Managed↔unmanaged вызовы дороги на iOS.** Официальное предупреждение Unity — не дёргать много нативных методов за кадр из-за процессорной стоимости перехода между управляемым и неуправляемым кодом. [Unity — Automated plug-in integration](https://docs.unity3d.com/6000.3/Documentation/Manual/ios-native-plugin-automated-integration.html)
- **Нативный плагин не работает в Editor.** Код, вызываемый через `[DllImport("__Internal")]`, реально существует только в собранном iOS-приложении — вызовы такого рода нужно оборачивать `#if UNITY_IOS && !UNITY_EDITOR` и подставлять заглушки для редактора и других платформ (это отражено и в наших примерах в разделе 2).
- **Обязательная статичность метода с `[MonoPInvokeCallback]`.** Технический разбор проблемы «callbackOnCollectedDelegate»: когда нативный код держит указатель на функцию, соответствующую управляемому делегату, этот делегат может быть собран сборщиком мусора, и нативный код обратится к уже невалидной памяти. `MonoPInvokeCallback` «tells Mono's AOT compiler to generate this stub statically» — то есть указывает AOT-компилятору Mono сгенерировать заглушку заранее и статически, так что она не будет собрана сборщиком мусора (в отличие от JIT-сценария, где заглушки создаются на лету и уничтожаются вместе с делегатом). Это особенно важно на iOS, где используется AOT-компиляция и динамическая генерация заглушек невозможна. [GitHub dotnet/runtime — Is there equivalent to MonoPInvokeCallback in dotnet?](https://github.com/dotnet/runtime/discussions/65296)
- **Практические сложности с `[AOT.MonoPInvokeCallback]` в реальных проектах.** В отдельном обсуждении на форуме Unity разработчики отмечали, что атрибут нужно указывать с полным путём `[AOT.MonoPInvokeCallback(typeof(...))]` (а не просто `[MonoPInvokeCallback(...)]`, если нет соответствующего `using`), и что добавление `[UnmanagedFunctionPointer(CallingConvention.Cdecl)]` поверх делегата в одном случае приводило к сбою кросс-компиляции — атрибут пришлось убрать. Прямых участников, подтвердивших работу этой связки на Android в этом обсуждении, не нашлось — вопрос остался открытым. [Unity Discussions — MonoPInvokeCallback in unity?](https://discussions.unity.com/t/monopinvokecallback-in-unity/473887)
- **PHPicker/UIImagePickerController и `PHAsset`.** Если картинка выбрана через пикер, но код затем пытается получить `PHAsset` этой же фотографии напрямую (а не просто использовать полученную копию), это может внезапно потребовать полноценного разрешения на фотобиблиотеку — Apple в этом случае рекомендует по возможности не запрашивать доступ к библиотеке вовсе и оставаться на уровне того, что вернул сам пикер.

## Источники

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
