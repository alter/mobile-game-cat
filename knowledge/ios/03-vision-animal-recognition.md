# Apple Vision: распознавание животных (кот/собака)

Дата сбора: 2026-08-24. Стек: Unity 6.3 LTS, iOS (целевая версия для нового API — iOS 18+, эксплуатация — вплоть до iOS 26), Swift, Vision.framework.

## Кратко

- У Apple есть два параллельных API распознавания животных: старый Objective-C-совместимый `VNRecognizeAnimalsRequest` (с iOS 13) и новый Swift-only `RecognizeAnimalsRequest` (с iOS 18). Оба сейчас не помечены как deprecated. [Apple — VNRecognizeAnimalsRequest](https://developer.apple.com/documentation/vision/vnrecognizeanimalsrequest), [Apple — RecognizeAnimalsRequest](https://developer.apple.com/documentation/vision/recognizeanimalsrequest)
- Начиная с iOS 18.0 Vision предоставляет новый Swift-only API на структурах (`struct`) с `async/await`; старый `VN`-префиксный API вынесен Apple в раздел «Legacy API» документации фреймворка. [Apple — Vision framework](https://developer.apple.com/documentation/vision)
- Распознаются только два вида: кот и собака — это подтверждено и в старом (`VNAnimalIdentifier.cat`, `.dog`), и в новом (`RecognizeAnimalsRequest.Animal.cat`, `.dog`) API. [Apple — VNAnimalIdentifier](https://developer.apple.com/documentation/vision/vnanimalidentifier), [Apple — RecognizeAnimalsRequest.Animal](https://developer.apple.com/documentation/vision/recognizeanimalsrequest/animal)
- Результат — массив `VNRecognizedObjectObservation` (старый API) или `RecognizedObjectObservation` (новый API), у каждого — `boundingBox` и массив `labels` с identifier и confidence. [Apple — VNRecognizedObjectObservation](https://developer.apple.com/documentation/vision/vnrecognizedobjectobservation), [Apple — RecognizedObjectObservation](https://developer.apple.com/documentation/vision/recognizedobjectobservation)
- `boundingBox` — в нормализованных координатах (0…1), начало координат — в левом нижнем углу изображения; для перевода в пиксели есть функция `VNImageRectForNormalizedRect`. [Apple — VNDetectedObjectObservation.boundingBox](https://developer.apple.com/documentation/vision/vndetectedobjectobservation/boundingbox), [Apple — VNImageRectForNormalizedRect](https://developer.apple.com/documentation/vision/vnimagerectfornormalizedrect(_:_:_:))
- Vision не хранит ориентацию изображения сама — её обязательно передавать через `CGImagePropertyOrientation` при создании `VNImageRequestHandler`/при вызове `perform(on:orientation:)`; это типовая причина, почему распознавание «не работает». [Apple — VNImageRequestHandler](https://developer.apple.com/documentation/vision/vnimagerequesthandler), [Apple — CGImagePropertyOrientation](https://developer.apple.com/documentation/imageio/cgimagepropertyorientation)
- Точных официальных цифр по порогу уверенности (confidence) для `VNRecognizeAnimalsRequest`/`RecognizeAnimalsRequest` не найдено — Apple не публикует рекомендованное пороговое значение; сообщество подбирает порог эмпирически. Точных данных нет.
- Надёжных источников о том, что Vision путает рисунки/фото на экране с живым котом, не найдено — «надёжных источников не найдено».
- Официальных бенчмарков задержки именно для `VNRecognizeAnimalsRequest`/`RecognizeAnimalsRequest` не найдено; общее устройство таково, что Vision и модели Core ML диспетчеризуются на Neural Engine автоматически, с откатом на GPU и затем CPU (по независимому источнику, не от Apple — см. раздел 8). [Blake Crosley — Apple Vision Framework: On-Device CV Most Devs Skip](https://blakecrosley.com/blog/vision-framework-built-in)
- Для Android-версии в будущем есть аналог — ML Kit Object Detection & Tracking и ML Kit Image Labeling от Google, оба работают на устройстве. [Google — ML Kit Object Detection](https://developers.google.com/ml-kit/vision/object-detection), [Google — ML Kit Image Labeling](https://developers.google.com/ml-kit/vision/image-labeling)

## 1. Два API: старый `VNRecognizeAnimalsRequest` и новый `RecognizeAnimalsRequest`

### 1.1. Старый (Objective-C-совместимый) API

`VNRecognizeAnimalsRequest` — класс, наследник `VNImageBasedRequest`. Доступен с iOS 13.0, iPadOS 13.0, macOS 10.15, Mac Catalyst 13.1, tvOS 13.0, visionOS 1.0. На момент сбора данных (2026-08-24) класс не помечен как deprecated. [Apple — VNRecognizeAnimalsRequest](https://developer.apple.com/documentation/vision/vnrecognizeanimalsrequest)

Объявление:

```swift
class VNRecognizeAnimalsRequest
```

Ключевые члены:

```swift
var results: [VNRecognizedObjectObservation]? { get }
func supportedIdentifiers() throws -> [VNAnimalIdentifier]
class func knownAnimalIdentifiers(forRevision requestRevision: Int) -> [VNAnimalIdentifier] // deprecated метод
```

Есть константы ревизий `VNRecognizeAnimalsRequestRevision1` и `VNRecognizeAnimalsRequestRevision2`. Метод `knownAnimalIdentifiers(forRevision:)` помечен устаревшим — вместо него следует использовать `supportedIdentifiers()` на созданном экземпляре запроса. [Apple — VNRecognizeAnimalsRequest](https://developer.apple.com/documentation/vision/vnrecognizeanimalsrequest)

`VNAnimalIdentifier` — структура-обёртка над строкой:

```swift
struct VNAnimalIdentifier

static let cat: VNAnimalIdentifier   // An animal identifier for cats.
static let dog: VNAnimalIdentifier   // An animal identifier for dogs.

init(rawValue: String)
```

[Apple — VNAnimalIdentifier](https://developer.apple.com/documentation/vision/vnanimalidentifier)

### 1.2. Новый Swift-only API (iOS 18+)

На WWDC24 (сессия «Discover Swift enhancements in the Vision framework») Apple представила переработанный Vision API на структурах Swift с поддержкой Swift Concurrency: запросы теперь называются без префикса `VN` (например, `RecognizeAnimalsRequest`, `ClassifyImageRequest`, `DetectFaceRectanglesRequest`). Ведущая сессии — Megan Williams, команда Vision. [Apple — WWDC24 10163](https://developer.apple.com/videos/play/wwdc2024/10163/)

`RecognizeAnimalsRequest` — структура, доступна с iOS 18.0, iPadOS 18.0, Mac Catalyst 18.0, macOS 15.0, tvOS 18.0, visionOS 2.0, watchOS 27.0 (в бета-статусе на watchOS). [Apple — RecognizeAnimalsRequest](https://developer.apple.com/documentation/vision/recognizeanimalsrequest)

```swift
struct RecognizeAnimalsRequest

init(_ revision: RecognizeAnimalsRequest.Revision? = nil)

func perform(on image: CGImage, orientation: CGImagePropertyOrientation?) async throws -> [RecognizedObjectObservation]
func perform(on pixelBuffer: CVPixelBuffer, orientation: CGImagePropertyOrientation?) async throws -> [RecognizedObjectObservation]
// перегрузки perform(on:) также принимают URL, Data, CIImage, CMSampleBuffer

var supportedAnimals: [RecognizeAnimalsRequest.Animal] { get }
```

Согласно документации Apple: «This request generates a collection of `RecognizedObjectObservation` objects that describe the animals the request detects.» [Apple — RecognizeAnimalsRequest](https://developer.apple.com/documentation/vision/recognizeanimalsrequest)

`RecognizeAnimalsRequest.Animal` — перечисление:

```swift
enum Animal
case cat  // An animal identifier for cats.
case dog  // An animal identifier for dogs.
```

Доступность и здесь: iOS 18.0+ и аналогично на других платформах. Есть также отдельный тип `RecognizeAnimalsRequest.Identifier`, помеченный как бета-API — по нему подробностей в открытых источниках не найдено. [Apple — RecognizeAnimalsRequest.Animal](https://developer.apple.com/documentation/vision/recognizeanimalsrequest/animal)

В обзоре фреймворка Vision `RecognizeAnimalsRequest` относится к разделу «Image classification and recognition» нового Swift API, тогда как `VNRecognizeAnimalsRequest` находится в разделе «Legacy API». Официальная формулировка Apple: «Starting in iOS 18.0, the Vision framework provides a new Swift-only API.» [Apple — Vision framework](https://developer.apple.com/documentation/vision)

### 1.3. Что актуально для iOS 26 и что рекомендует Apple

На момент сбора данных Apple не публиковала явного заявления о депрекации `VNRecognizeAnimalsRequest` — оба API присутствуют в документации Vision одновременно, старый вынесен в раздел «Legacy API», новый — в основные разделы. Из этого следует practical-вывод (не подтверждённая Apple прямая рекомендация, а логика структуры документации): для нового проекта на Unity 6.3 с целевым iOS 18+ разумно ориентироваться на `RecognizeAnimalsRequest`, но при необходимости поддержки более старых iOS (13–17) нужен `VNRecognizeAnimalsRequest`. Прямой цитаты Apple вида «используйте только новый API» не найдено — «надёжных источников не найдено».

## 2. Полный рабочий пример: старый API (`VNRecognizeAnimalsRequest`)

Ниже — пример, собранный из деклараций Apple (класс, свойства, инициализаторы `VNImageRequestHandler`) и типового паттерна использования Vision-запросов. Показывает путь UIImage → CGImage → `VNImageRequestHandler` (с указанием ориентации) → `VNRecognizeAnimalsRequest` → чтение `VNRecognizedObjectObservation`.

```swift
import UIKit
import Vision

func recognizeAnimal(in image: UIImage, completion: @escaping ([VNRecognizedObjectObservation]) -> Void) {
    guard let cgImage = image.cgImage else {
        completion([])
        return
    }

    // Ориентацию нужно передать явно: CGImage/CVPixelBuffer её не хранят.
    let orientation = CGImagePropertyOrientation(image.imageOrientation)

    let handler = VNImageRequestHandler(cgImage: cgImage, orientation: orientation, options: [:])
    let request = VNRecognizeAnimalsRequest { request, error in
        guard error == nil,
              let observations = request.results as? [VNRecognizedObjectObservation] else {
            completion([])
            return
        }
        completion(observations)
    }

    DispatchQueue.global(qos: .userInitiated).async {
        do {
            try handler.perform([request])
        } catch {
            DispatchQueue.main.async { completion([]) }
        }
    }
}

// Преобразование UIImage.Orientation -> CGImagePropertyOrientation.
// Соответствие взято из документации Apple по CGImagePropertyOrientation
// (case up = 1 ... left = 8) и стандартного набора случаев UIImage.Orientation.
extension CGImagePropertyOrientation {
    init(_ uiOrientation: UIImage.Orientation) {
        switch uiOrientation {
        case .up: self = .up
        case .upMirrored: self = .upMirrored
        case .down: self = .down
        case .downMirrored: self = .downMirrored
        case .left: self = .left
        case .leftMirrored: self = .leftMirrored
        case .right: self = .right
        case .rightMirrored: self = .rightMirrored
        @unknown default: self = .up
        }
    }
}
```

Чтение labels и boundingBox (сигнатуры — из документации Apple по `VNRecognizedObjectObservation` и `VNDetectedObjectObservation.boundingBox`):

```swift
for observation in observations {
    // labels отсортированы по убыванию confidence; confidence внутри labels
    // в сумме дают 1.0 — итоговая уверенность = label.confidence * observation.confidence.
    guard let topLabel = observation.labels.first else { continue }

    let identifier = topLabel.identifier          // "Cat" или "Dog"
    let finalConfidence = topLabel.confidence * observation.confidence

    // boundingBox — нормализованные координаты, начало координат внизу слева.
    let normalizedBox = observation.boundingBox

    print("\(identifier): \(finalConfidence)")
}
```

Источник по формуле итоговой уверенности и сортировке `labels` — документация `VNRecognizedObjectObservation`: «The confidence values of all classifications in the array sum up to `1.0`», итоговая уверенность конкретной классификации получается умножением `classification.confidence` на `observation.confidence`. [Apple — VNRecognizedObjectObservation](https://developer.apple.com/documentation/vision/vnrecognizedobjectobservation)

## 3. Полный рабочий пример: новый API (`RecognizeAnimalsRequest`, iOS 18+)

Пример на `async/await`, основанный на официальной сигнатуре `perform(on:orientation:)` и на паттерне из WWDC24: «on iOS 18.0+, using the new Swift-featured API, you create `let request = RecognizeAnimalsRequest()`, then in a Task, call `try await request.perform(on: fileURL)`». [Apple — WWDC24 10163](https://developer.apple.com/videos/play/wwdc2024/10163/)

```swift
import Vision
import CoreGraphics

func recognizeAnimalsModern(cgImage: CGImage, orientation: CGImagePropertyOrientation) async throws -> [RecognizedObjectObservation] {
    let request = RecognizeAnimalsRequest()
    let observations = try await request.perform(on: cgImage, orientation: orientation)
    return observations
}
```

Чтение результата (сигнатуры — из документации `RecognizedObjectObservation`):

```swift
struct RecognizedObjectObservation {
    let labels: [ClassificationObservation]
    // boundingBox — через протокол BoundingBoxProviding
}
```

Итоговая уверенность считается так же, как в старом API: «Multiply the classification confidence with the confidence of this observation to get the actual confidence for each label.» [Apple — RecognizedObjectObservation](https://developer.apple.com/documentation/vision/recognizedobjectobservation)

```swift
for observation in observations {
    guard let topLabel = observation.labels.first else { continue }
    let finalConfidence = topLabel.confidence * observation.confidence
    print("\(topLabel.identifier): \(finalConfidence)")
}
```

Для справки — аналогичный, но не привязанный к животным, реальный пример из статьи Apple о классификации изображений (демонстрирует официальный стиль работы с новым API, включая фильтрацию по точности через `hasMinimumPrecision`/`hasMinimumRecall`):

```swift
// Returns an `ImageFile` object based on the `ClassifyImageRequest` results.
func classifyImage(url: URL) async throws -> ImageFile {
    var image = ImageFile(url: url)

    // Vision request to classify an image.
    let request = ClassifyImageRequest()

    // Perform the request on the image, and return an array of `ClassificationObservation` objects.
    let results = try await request.perform(on: url)
        // Use `hasMinimumPrecision` for a high-recall filter.
        .filter { $0.hasMinimumPrecision(0.1, forRecall: 0.8) }
        // Use `hasMinimumRecall` for a high-precision filter.
        // .filter { $0.hasMinimumRecall(0.01, forPrecision: 0.9) }

    // Add each classification identifier and its respective confidence level into the observations dictionary.
    for classification in results {
        image.observations[classification.identifier] = classification.confidence
    }

    return image
}
```

[Apple — Classifying images for categorization and search](https://developer.apple.com/documentation/vision/classifying-images-for-categorization-and-search)

## 4. Система координат `boundingBox` и перевод в координаты изображения

`boundingBox` объявлен в `VNDetectedObjectObservation` (родитель `VNRecognizedObjectObservation`) как:

```swift
var boundingBox: CGRect { get }
```

Официальная формулировка Apple: «The system normalizes the coordinates to the dimensions of the processed image, with the origin at the lower-left corner of the image.» То есть координаты нормализованы в диапазон 0…1, а начало координат — **левый нижний угол**, а не левый верхний, как в UIKit. [Apple — VNDetectedObjectObservation.boundingBox](https://developer.apple.com/documentation/vision/vndetectedobjectobservation/boundingbox)

Для перевода нормализованного прямоугольника в пиксельные координаты изображения Apple предоставляет функцию:

```swift
func VNImageRectForNormalizedRect(
    _ normalizedRect: CGRect,
    _ imageWidth: Int,
    _ imageHeight: Int
) -> CGRect
```

Абстракт: «Projects a rectangle from normalized coordinates into image coordinates.» Параметры: `normalizedRect` — исходный прямоугольник в нормализованных координатах; `imageWidth`, `imageHeight` — ширина и высота изображения, в координаты которого проецируем. Возвращает `CGRect` в пиксельных координатах изображения. Доступна с iOS 11.0+. [Apple — VNImageRectForNormalizedRect](https://developer.apple.com/documentation/vision/vnimagerectfornormalizedrect(_:_:_:))

Точная формула перевода (проекция без функции, если нужно сделать вручную — тот же результат, что даёт `VNImageRectForNormalizedRect`):

```
pixelX = normalizedRect.origin.x * imageWidth
pixelY = (1 - normalizedRect.origin.y - normalizedRect.height) * imageHeight   // инверсия оси Y
pixelWidth = normalizedRect.width * imageWidth
pixelHeight = normalizedRect.height * imageHeight
```

Практический пример использования функции (обёртка написана нами по официальной сигнатуре из раздела 4 — сама идея «умножить нормализованные координаты на ширину и высоту» описана в независимом разборе Vision-координат): «just multiply the coordinates by the width and height of the full image», и Vision даёт для этого готовую функцию `VNImageRectForNormalizedRect()`. [Machine, Think! — How to display Vision bounding boxes](https://machinethink.net/blog/bounding-boxes/)

```swift
extension CGRect {
    func rect(in image: UIImage) -> CGRect {
        VNImageRectForNormalizedRect(self, Int(image.size.width), Int(image.size.height))
    }
}

let boxInPixels = observation.boundingBox.rect(in: sourceImage)
```

Дополнительно для отображения на экране (`UIImageView`) нужен ещё один шаг — учёт `contentMode` (`aspectFit`/`aspectFill`): «you have to take its `contentMode` into account… need to apply the same rules to your bounding boxes» — то есть пиксельные координаты изображения и координаты вью, как правило, не совпадают напрямую, и поверх `VNImageRectForNormalizedRect` нужно ещё одно преобразование (масштаб + сдвиг), которое сама функция не делает. [Machine, Think! — How to display Vision bounding boxes](https://machinethink.net/blog/bounding-boxes/)

## 5. Ориентация изображения (`CGImagePropertyOrientation`)

`VNImageRequestHandler` предполагает, что изображение подано в правильной («upright») ориентации, но `CGImage`, `CIImage` и `CVPixelBuffer` сами по себе не хранят информацию об ориентации — поэтому во всех инициализаторах `VNImageRequestHandler` есть параметр `orientation: CGImagePropertyOrientation`:

```swift
init(cgImage: CGImage, orientation: CGImagePropertyOrientation, options: [VNImageOption : Any])
init(ciImage: CIImage, orientation: CGImagePropertyOrientation, options: [VNImageOption : Any])
init(cvPixelBuffer: CVPixelBuffer, orientation: CGImagePropertyOrientation, options: [VNImageOption : Any])
init(cmSampleBuffer: CMSampleBuffer, orientation: CGImagePropertyOrientation, options: [VNImageOption : Any])
init(data: Data, orientation: CGImagePropertyOrientation, options: [VNImageOption : Any])
init(url: URL, orientation: CGImagePropertyOrientation, options: [VNImageOption : Any])
```

[Apple — VNImageRequestHandler](https://developer.apple.com/documentation/vision/vnimagerequesthandler)

`CGImagePropertyOrientation` — перечисление с восемью значениями (доступно с iOS 4.0):

```swift
@frozen enum CGImagePropertyOrientation
case up = 1              // Данные соответствуют предполагаемой ориентации показа.
case upMirrored = 2      // Отражены по горизонтали.
case down = 3            // Повёрнуты на 180°.
case downMirrored = 4    // Отражены по вертикали.
case leftMirrored = 5    // Отражены по горизонтали и повёрнуты на 90° против часовой стрелки.
case right = 6           // Повёрнуты на 90° против часовой стрелки.
case rightMirrored = 7   // Отражены по горизонтали и повёрнуты на 90° по часовой стрелке.
case left = 8            // Повёрнуты на 90° по часовой стрелке.
```

Официальное пояснение Apple: «For example, the pixel data for an image captured by an iOS device camera is encoded in the camera sensor's native landscape orientation. When the user captures a photo while holding the device in portrait orientation, iOS writes an orientation value of `.right` in the resulting image file.» [Apple — CGImagePropertyOrientation](https://developer.apple.com/documentation/imageio/cgimagepropertyorientation)

Типовая причина сбоя распознавания — неверно переданная или вовсе не переданная ориентация. Пояснение из независимого разбора: «The camera sensor on the iPhone is mounted in landscape orientation… When the device is in portrait mode, images coming from the camera are seen by the Core ML model as rotated 90 degrees to the right», и решение — передавать `orientation: .right` в `VNImageRequestHandler`, «so Vision already fixes the image's rotation before passing it to Core ML». Для приложений, поддерживающих и портретную, и ландшафтную ориентацию, рекомендуется держать `AVCaptureConnection` всегда в ландшафтной ориентации, регулируя только preview-слой, и передавать в Vision соответствующую ориентацию устройства. [Machine, Think! — How to display Vision bounding boxes](https://machinethink.net/blog/bounding-boxes/)

## 6. Порог уверенности (confidence)

`confidence` в Vision — значение `Float` от `0.0` до `1.0`. Для `VNRecognizedObjectObservation`/`RecognizedObjectObservation` действует правило: суммарная уверенность всех классификаций внутри `labels` равна `1.0`, а итоговая («настоящая») уверенность конкретной метки — произведение `label.confidence * observation.confidence`. [Apple — VNRecognizedObjectObservation](https://developer.apple.com/documentation/vision/vnrecognizedobjectobservation)

Apple не публикует рекомендованное числовое пороговое значение confidence именно для `VNRecognizeAnimalsRequest`/`RecognizeAnimalsRequest`. В новом Swift API для похожей задачи (`ClassifyImageRequest`) Apple предлагает не жёсткий порог, а методы `hasMinimumPrecision(_:forRecall:)` и `hasMinimumRecall(_:forPrecision:)`, которые фильтруют результаты по соотношению точности/полноты, а не по сырому числу confidence — то есть сама Apple рекомендует не подбирать «магическое число» вручную, а описывать желаемый компромисс между precision и recall. [Apple — Classifying images for categorization and search](https://developer.apple.com/documentation/vision/classifying-images-for-categorization-and-search)

Практика сообщества: разработчики берут `identifier` (`"Cat"`/`"Dog"`) и `confidence` из `labels.first` и сравнивают с порогом, подобранным опытным путём под свою задачу — жёстко прописанного «стандартного» значения (например, 0.7 или 0.9) в найденных источниках нет. Конкретные числа, которые встречаются в общих (не Apple-специфичных) материалах про классификаторы кот/собака — это пороги конкретных сторонних моделей, а не значения, рекомендованные Apple для Vision. Точных данных по эталонному порогу для Vision нет — «точных данных нет».

## 7. Ограничения

- **Только кот и собака.** И старый (`VNAnimalIdentifier.cat`/`.dog`), и новый (`RecognizeAnimalsRequest.Animal.cat`/`.dog`) API распознают исключительно два вида животных — других идентификаторов в документации не описано. [Apple — VNAnimalIdentifier](https://developer.apple.com/documentation/vision/vnanimalidentifier), [Apple — RecognizeAnimalsRequest.Animal](https://developer.apple.com/documentation/vision/recognizeanimalsrequest/animal)
- **Надёжность различения кота и собаки.** Официальных данных Apple о проценте ошибок (кот принят за собаку и наоборот) не найдено. «Надёжных источников не найдено».
- **Рисунки/изображения на экране.** Специальных данных или официальных заявлений Apple о поведении `VNRecognizeAnimalsRequest` на мультфильмах, рисунках или фотографии кота, показанной на экране другого устройства, не найдено. «Надёжных источников не найдено».
- **Несколько животных в кадре.** `results`/`perform(on:)` возвращают **массив** наблюдений (`[VNRecognizedObjectObservation]` / `[RecognizedObjectObservation]`) — то есть API спроектирован для работы с несколькими объектами в одном кадре, у каждого — собственный `boundingBox` и собственные `labels`. Прямого текста Apple о заявленном максимуме одновременно распознаваемых животных в одном кадре не найдено. [Apple — VNRecognizeAnimalsRequest](https://developer.apple.com/documentation/vision/vnrecognizeanimalsrequest)

## 8. Быстродействие и потребление (Neural Engine)

Официальных численных бенчмарков задержки конкретно для `VNRecognizeAnimalsRequest`/`RecognizeAnimalsRequest` от Apple не найдено. Общий принцип диспетчеризации Vision/Core ML описан в независимых источниках так: «Vision (and the Core ML models it runs) dispatches automatically to the Neural Engine when available, falls back to the GPU when not, and to the CPU as a last resort» — то есть разработчик не выбирает исполнитель напрямую, это делает система. [Blake Crosley — Apple Vision Framework](https://blakecrosley.com/blog/vision-framework-built-in)

Там же приводятся оценки задержки для **других** запросов Vision (не для распознавания животных): распознавание текста (OCR) — 150–300 мс на страницу чека; определение лиц — 5–15 мс на кадр; поза тела при 60 fps — менее 16 мс на кадр; эмбеддинги изображений — 20–40 мс. Эти цифры не от Apple и не относятся именно к `VNRecognizeAnimalsRequest`, приводятся только как ориентир порядка величины для Vision-запросов на устройстве. [Blake Crosley — Apple Vision Framework](https://blakecrosley.com/blog/vision-framework-built-in)

Для доступа к Neural Engine нет отдельного явного флага в Vision — это происходит автоматически внутри Core ML, на который опираются запросы Vision. Разработчик не может напрямую проверить или заставить конкретный запрос выполниться именно на Neural Engine — это «hint», которым управляет система. [Blake Crosley — Apple Vision Framework](https://blakecrosley.com/blog/vision-framework-built-in)

## 9. Альтернатива для Android: ML Kit (Google)

Для будущего портирования на Android у Google есть два разных, но связанных API в составе ML Kit:

### ML Kit Object Detection & Tracking

- Работает на устройстве («happens on the device»), не по сети.
- За один проход в изображении находит и отслеживает объекты, для каждого — положение (bounding box); в видеопотоке каждому объекту присваивается уникальный ID для трекинга между кадрами.
- Есть встроенный «грубый» (coarse) классификатор с пятью категориями: «home goods, fashion goods, food, plants, and places» — то есть категорий немного, детальной информации о виде объекта (например, «кошка» отдельно от «собаки») эта классификация не даёт.
- Позиционируется как «optimized for mobile devices and intended for use in real-time applications, even on lower-end devices».

[Google — Object detection and tracking](https://developers.google.com/ml-kit/vision/object-detection)

### ML Kit Image Labeling

- Базовая модель распознаёт «more than 400 categories» — люди, вещи, места, активности, в том числе виды животных, товары.
- Предназначена для классификации **всего изображения целиком** («image classification models that describe the full image»), а не для нахождения и обводки конкретных объектов на фото — для этой задачи Google явно рекомендует Object Detection & Tracking: «for classifying one or more objects in an image, such as shoes or pieces of furniture, the Object Detection & Tracking API may be a better fit».
- Поддерживает как встроенную базовую модель, так и собственные модели TensorFlow Lite/LiteRT.

[Google — Image labeling](https://developers.google.com/ml-kit/vision/image-labeling)

**Вывод для портирования:** ни Object Detection & Tracking, ни Image Labeling не дают «из коробки» такой же прицельной пары идентификаторов «кот/собака» с bounding box, как связка `VNRecognizeAnimalsRequest` + Vision. Ближе всего по духу — Image Labeling (там литералы вроде «cat» могут встречаться среди 400+ категорий), но официального списка меток ML Kit Image Labeling в рамках этого исследования не открывался — точный список нужно сверять по `label-map` в документации Google отдельно.

## Источники

- [Apple — VNRecognizeAnimalsRequest](https://developer.apple.com/documentation/vision/vnrecognizeanimalsrequest)
- [Apple — RecognizeAnimalsRequest](https://developer.apple.com/documentation/vision/recognizeanimalsrequest)
- [Apple — RecognizeAnimalsRequest.Animal](https://developer.apple.com/documentation/vision/recognizeanimalsrequest/animal)
- [Apple — RecognizeAnimalsRequest.supportedAnimals](https://developer.apple.com/documentation/vision/recognizeanimalsrequest/supportedanimals)
- [Apple — VNAnimalIdentifier](https://developer.apple.com/documentation/vision/vnanimalidentifier)
- [Apple — VNRecognizedObjectObservation](https://developer.apple.com/documentation/vision/vnrecognizedobjectobservation)
- [Apple — RecognizedObjectObservation](https://developer.apple.com/documentation/vision/recognizedobjectobservation)
- [Apple — VNDetectedObjectObservation.boundingBox](https://developer.apple.com/documentation/vision/vndetectedobjectobservation/boundingbox)
- [Apple — VNImageRectForNormalizedRect(_:_:_:)](https://developer.apple.com/documentation/vision/vnimagerectfornormalizedrect(_:_:_:))
- [Apple — VNImageRequestHandler](https://developer.apple.com/documentation/vision/vnimagerequesthandler)
- [Apple — CGImagePropertyOrientation](https://developer.apple.com/documentation/imageio/cgimagepropertyorientation)
- [Apple — VNDetectAnimalBodyPoseRequest](https://developer.apple.com/documentation/vision/vndetectanimalbodyposerequest)
- [Apple — Detecting animal body poses with Vision](https://developer.apple.com/documentation/vision/detecting-animal-body-poses-with-vision)
- [Apple — Vision framework (обзор)](https://developer.apple.com/documentation/vision)
- [Apple — Classifying images for categorization and search](https://developer.apple.com/documentation/vision/classifying-images-for-categorization-and-search)
- [Apple — WWDC24: Discover Swift enhancements in the Vision framework](https://developer.apple.com/videos/play/wwdc2024/10163/)
- [Machine, Think! — How to display Vision bounding boxes](https://machinethink.net/blog/bounding-boxes/)
- [Blake Crosley — Apple Vision Framework: On-Device CV Most Devs Skip](https://blakecrosley.com/blog/vision-framework-built-in)
- [Google — Object detection and tracking (ML Kit)](https://developers.google.com/ml-kit/vision/object-detection)
- [Google — Image labeling (ML Kit)](https://developers.google.com/ml-kit/vision/image-labeling)
