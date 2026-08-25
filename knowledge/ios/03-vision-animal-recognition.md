# Apple Vision: animal recognition (cat/dog)

Collection date: 2026-08-24. Stack: Unity 6.3 LTS, iOS (target version for the new API — iOS 18+, deployment — up to iOS 26), Swift, Vision.framework.

## Summary

- Apple has two parallel animal-recognition APIs: the old Objective-C-compatible `VNRecognizeAnimalsRequest` (since iOS 13) and the new Swift-only `RecognizeAnimalsRequest` (since iOS 18). Neither is currently marked deprecated. [Apple — VNRecognizeAnimalsRequest](https://developer.apple.com/documentation/vision/vnrecognizeanimalsrequest), [Apple — RecognizeAnimalsRequest](https://developer.apple.com/documentation/vision/recognizeanimalsrequest)
- Starting with iOS 18.0, Vision provides a new Swift-only API built on structs (`struct`) with `async/await`; the old `VN`-prefixed API has been moved by Apple into the "Legacy API" section of the framework's documentation. [Apple — Vision framework](https://developer.apple.com/documentation/vision)
- Only two species are recognized: cat and dog — confirmed in both the old (`VNAnimalIdentifier.cat`, `.dog`) and the new (`RecognizeAnimalsRequest.Animal.cat`, `.dog`) API. [Apple — VNAnimalIdentifier](https://developer.apple.com/documentation/vision/vnanimalidentifier), [Apple — RecognizeAnimalsRequest.Animal](https://developer.apple.com/documentation/vision/recognizeanimalsrequest/animal)
- The result is an array of `VNRecognizedObjectObservation` (old API) or `RecognizedObjectObservation` (new API); each one has a `boundingBox` and a `labels` array with identifier and confidence. [Apple — VNRecognizedObjectObservation](https://developer.apple.com/documentation/vision/vnrecognizedobjectobservation), [Apple — RecognizedObjectObservation](https://developer.apple.com/documentation/vision/recognizedobjectobservation)
- `boundingBox` — in normalized coordinates (0…1), with the origin at the image's lower-left corner; there is a function for converting to pixels, `VNImageRectForNormalizedRect`. [Apple — VNDetectedObjectObservation.boundingBox](https://developer.apple.com/documentation/vision/vndetectedobjectobservation/boundingbox), [Apple — VNImageRectForNormalizedRect](https://developer.apple.com/documentation/vision/vnimagerectfornormalizedrect(_:_:_:))
- Vision does not store image orientation itself — it must be passed explicitly via `CGImagePropertyOrientation` when creating `VNImageRequestHandler`/calling `perform(on:orientation:)`; this is a typical reason why recognition "doesn't work." [Apple — VNImageRequestHandler](https://developer.apple.com/documentation/vision/vnimagerequesthandler), [Apple — CGImagePropertyOrientation](https://developer.apple.com/documentation/imageio/cgimagepropertyorientation)
- No exact official figures on the confidence threshold for `VNRecognizeAnimalsRequest`/`RecognizeAnimalsRequest` were found — Apple does not publish a recommended threshold value; the community picks a threshold empirically. No exact data.
- No reliable source was found stating that Vision confuses on-screen drawings/photos with a live cat — "no reliable source found."
- No official latency benchmarks specifically for `VNRecognizeAnimalsRequest`/`RecognizeAnimalsRequest` were found; the general mechanism is that Vision and Core ML models dispatch to the Neural Engine automatically, falling back to the GPU and then the CPU (per an independent, non-Apple source — see section 8). [Blake Crosley — Apple Vision Framework: On-Device CV Most Devs Skip](https://blakecrosley.com/blog/vision-framework-built-in)
- For a future Android version there is an analog — Google's ML Kit Object Detection & Tracking and ML Kit Image Labeling, both running on-device. [Google — ML Kit Object Detection](https://developers.google.com/ml-kit/vision/object-detection), [Google — ML Kit Image Labeling](https://developers.google.com/ml-kit/vision/image-labeling)

## 1. Two APIs: the old `VNRecognizeAnimalsRequest` and the new `RecognizeAnimalsRequest`

### 1.1. The old (Objective-C-compatible) API

`VNRecognizeAnimalsRequest` is a class, a subclass of `VNImageBasedRequest`. Available since iOS 13.0, iPadOS 13.0, macOS 10.15, Mac Catalyst 13.1, tvOS 13.0, visionOS 1.0. As of the data collection date (2026-08-24), the class is not marked deprecated. [Apple — VNRecognizeAnimalsRequest](https://developer.apple.com/documentation/vision/vnrecognizeanimalsrequest)

Declaration:

```swift
class VNRecognizeAnimalsRequest
```

Key members:

```swift
var results: [VNRecognizedObjectObservation]? { get }
func supportedIdentifiers() throws -> [VNAnimalIdentifier]
class func knownAnimalIdentifiers(forRevision requestRevision: Int) -> [VNAnimalIdentifier] // deprecated method
```

There are revision constants `VNRecognizeAnimalsRequestRevision1` and `VNRecognizeAnimalsRequestRevision2`. The `knownAnimalIdentifiers(forRevision:)` method is marked deprecated — `supportedIdentifiers()` should be used instead, on a created request instance. [Apple — VNRecognizeAnimalsRequest](https://developer.apple.com/documentation/vision/vnrecognizeanimalsrequest)

`VNAnimalIdentifier` is a structure wrapping a string:

```swift
struct VNAnimalIdentifier

static let cat: VNAnimalIdentifier   // An animal identifier for cats.
static let dog: VNAnimalIdentifier   // An animal identifier for dogs.

init(rawValue: String)
```

[Apple — VNAnimalIdentifier](https://developer.apple.com/documentation/vision/vnanimalidentifier)

### 1.2. The new Swift-only API (iOS 18+)

At WWDC24 (the session "Discover Swift enhancements in the Vision framework"), Apple introduced a reworked Vision API built on Swift structs with Swift Concurrency support: requests are now named without the `VN` prefix (for example, `RecognizeAnimalsRequest`, `ClassifyImageRequest`, `DetectFaceRectanglesRequest`). The session presenter was Megan Williams, Vision team. [Apple — WWDC24 10163](https://developer.apple.com/videos/play/wwdc2024/10163/)

`RecognizeAnimalsRequest` is a struct, available since iOS 18.0, iPadOS 18.0, Mac Catalyst 18.0, macOS 15.0, tvOS 18.0, visionOS 2.0, watchOS 27.0 (beta status on watchOS). [Apple — RecognizeAnimalsRequest](https://developer.apple.com/documentation/vision/recognizeanimalsrequest)

```swift
struct RecognizeAnimalsRequest

init(_ revision: RecognizeAnimalsRequest.Revision? = nil)

func perform(on image: CGImage, orientation: CGImagePropertyOrientation?) async throws -> [RecognizedObjectObservation]
func perform(on pixelBuffer: CVPixelBuffer, orientation: CGImagePropertyOrientation?) async throws -> [RecognizedObjectObservation]
// perform(on:) overloads also accept URL, Data, CIImage, CMSampleBuffer

var supportedAnimals: [RecognizeAnimalsRequest.Animal] { get }
```

According to Apple's documentation: «This request generates a collection of `RecognizedObjectObservation` objects that describe the animals the request detects.» [Apple — RecognizeAnimalsRequest](https://developer.apple.com/documentation/vision/recognizeanimalsrequest)

`RecognizeAnimalsRequest.Animal` is an enum:

```swift
enum Animal
case cat  // An animal identifier for cats.
case dog  // An animal identifier for dogs.
```

Availability here too: iOS 18.0+ and correspondingly on other platforms. There is also a separate `RecognizeAnimalsRequest.Identifier` type, marked as a beta API — no details on it were found in open sources. [Apple — RecognizeAnimalsRequest.Animal](https://developer.apple.com/documentation/vision/recognizeanimalsrequest/animal)

In the Vision framework overview, `RecognizeAnimalsRequest` belongs to the "Image classification and recognition" section of the new Swift API, while `VNRecognizeAnimalsRequest` is in the "Legacy API" section. Apple's official wording: «Starting in iOS 18.0, the Vision framework provides a new Swift-only API.» [Apple — Vision framework](https://developer.apple.com/documentation/vision)

### 1.3. What's relevant for iOS 26 and what Apple recommends

As of the data collection date, Apple has not published an explicit deprecation statement for `VNRecognizeAnimalsRequest` — both APIs are present in the Vision documentation at the same time, with the old one moved into the "Legacy API" section and the new one in the main sections. This leads to a practical conclusion (not a confirmed direct recommendation from Apple, but the logic of the documentation's structure): for a new project on Unity 6.3 targeting iOS 18+, it makes sense to rely on `RecognizeAnimalsRequest`, but if support for older iOS versions (13–17) is needed, `VNRecognizeAnimalsRequest` is required. No direct Apple quote of the form "use only the new API" was found — "no reliable source found."

## 2. Full working example: the old API (`VNRecognizeAnimalsRequest`)

Below is an example assembled from Apple's declarations (the class, properties, `VNImageRequestHandler` initializers) and the typical pattern for using Vision requests. It shows the path UIImage → CGImage → `VNImageRequestHandler` (with orientation specified) → `VNRecognizeAnimalsRequest` → reading `VNRecognizedObjectObservation`.

```swift
import UIKit
import Vision

func recognizeAnimal(in image: UIImage, completion: @escaping ([VNRecognizedObjectObservation]) -> Void) {
    guard let cgImage = image.cgImage else {
        completion([])
        return
    }

    // Orientation must be passed explicitly: CGImage/CVPixelBuffer don't store it.
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

// Converting UIImage.Orientation -> CGImagePropertyOrientation.
// The mapping is taken from Apple's CGImagePropertyOrientation documentation
// (case up = 1 ... left = 8) and the standard set of UIImage.Orientation cases.
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

Reading labels and boundingBox (signatures from Apple's documentation for `VNRecognizedObjectObservation` and `VNDetectedObjectObservation.boundingBox`):

```swift
for observation in observations {
    // labels are sorted by descending confidence; confidence within labels
    // sums to 1.0 — the final confidence = label.confidence * observation.confidence.
    guard let topLabel = observation.labels.first else { continue }

    let identifier = topLabel.identifier          // "Cat" or "Dog"
    let finalConfidence = topLabel.confidence * observation.confidence

    // boundingBox — normalized coordinates, origin at the bottom-left.
    let normalizedBox = observation.boundingBox

    print("\(identifier): \(finalConfidence)")
}
```

The source for the final-confidence formula and the sorting of `labels` is the `VNRecognizedObjectObservation` documentation: «The confidence values of all classifications in the array sum up to `1.0`»; the final confidence of a given classification is obtained by multiplying `classification.confidence` by `observation.confidence`. [Apple — VNRecognizedObjectObservation](https://developer.apple.com/documentation/vision/vnrecognizedobjectobservation)

## 3. Full working example: the new API (`RecognizeAnimalsRequest`, iOS 18+)

An `async/await` example, based on the official `perform(on:orientation:)` signature and on the pattern from WWDC24: «on iOS 18.0+, using the new Swift-featured API, you create `let request = RecognizeAnimalsRequest()`, then in a Task, call `try await request.perform(on: fileURL)`». [Apple — WWDC24 10163](https://developer.apple.com/videos/play/wwdc2024/10163/)

```swift
import Vision
import CoreGraphics

func recognizeAnimalsModern(cgImage: CGImage, orientation: CGImagePropertyOrientation) async throws -> [RecognizedObjectObservation] {
    let request = RecognizeAnimalsRequest()
    let observations = try await request.perform(on: cgImage, orientation: orientation)
    return observations
}
```

Reading the result (signatures from the `RecognizedObjectObservation` documentation):

```swift
struct RecognizedObjectObservation {
    let labels: [ClassificationObservation]
    // boundingBox — via the BoundingBoxProviding protocol
}
```

The final confidence is computed the same way as in the old API: «Multiply the classification confidence with the confidence of this observation to get the actual confidence for each label.» [Apple — RecognizedObjectObservation](https://developer.apple.com/documentation/vision/recognizedobjectobservation)

```swift
for observation in observations {
    guard let topLabel = observation.labels.first else { continue }
    let finalConfidence = topLabel.confidence * observation.confidence
    print("\(topLabel.identifier): \(finalConfidence)")
}
```

For reference — an analogous but not animal-specific real example from Apple's article on image classification (demonstrates the official style of working with the new API, including precision filtering via `hasMinimumPrecision`/`hasMinimumRecall`):

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

## 4. The `boundingBox` coordinate system and converting to image coordinates

`boundingBox` is declared in `VNDetectedObjectObservation` (the parent of `VNRecognizedObjectObservation`) as:

```swift
var boundingBox: CGRect { get }
```

Apple's official wording: «The system normalizes the coordinates to the dimensions of the processed image, with the origin at the lower-left corner of the image.» That is, the coordinates are normalized to the 0…1 range, and the origin is the **lower-left corner**, not the upper-left as in UIKit. [Apple — VNDetectedObjectObservation.boundingBox](https://developer.apple.com/documentation/vision/vndetectedobjectobservation/boundingbox)

To convert a normalized rectangle to pixel coordinates of the image, Apple provides a function:

```swift
func VNImageRectForNormalizedRect(
    _ normalizedRect: CGRect,
    _ imageWidth: Int,
    _ imageHeight: Int
) -> CGRect
```

Abstract: «Projects a rectangle from normalized coordinates into image coordinates.» Parameters: `normalizedRect` — the source rectangle in normalized coordinates; `imageWidth`, `imageHeight` — the width and height of the image whose coordinates we're projecting into. Returns a `CGRect` in the image's pixel coordinates. Available since iOS 11.0+. [Apple — VNImageRectForNormalizedRect](https://developer.apple.com/documentation/vision/vnimagerectfornormalizedrect(_:_:_:))

The exact conversion formula (the projection without the function, for doing it manually — the same result that `VNImageRectForNormalizedRect` gives):

```
pixelX = normalizedRect.origin.x * imageWidth
pixelY = (1 - normalizedRect.origin.y - normalizedRect.height) * imageHeight   // Y-axis inversion
pixelWidth = normalizedRect.width * imageWidth
pixelHeight = normalizedRect.height * imageHeight
```

A practical example of using the function (the wrapper was written by us based on the official signature from section 4 — the idea of "multiplying normalized coordinates by width and height" itself is described in an independent breakdown of Vision coordinates): «just multiply the coordinates by the width and height of the full image», and Vision provides a ready-made function for this, `VNImageRectForNormalizedRect()`. [Machine, Think! — How to display Vision bounding boxes](https://machinethink.net/blog/bounding-boxes/)

```swift
extension CGRect {
    func rect(in image: UIImage) -> CGRect {
        VNImageRectForNormalizedRect(self, Int(image.size.width), Int(image.size.height))
    }
}

let boxInPixels = observation.boundingBox.rect(in: sourceImage)
```

Additionally, displaying on screen (`UIImageView`) requires one more step — accounting for `contentMode` (`aspectFit`/`aspectFill`): «you have to take its `contentMode` into account… need to apply the same rules to your bounding boxes» — that is, the image's pixel coordinates and the view's coordinates generally don't match directly, and on top of `VNImageRectForNormalizedRect` you need one more transform (scale + offset) that the function itself doesn't do. [Machine, Think! — How to display Vision bounding boxes](https://machinethink.net/blog/bounding-boxes/)

## 5. Image orientation (`CGImagePropertyOrientation`)

`VNImageRequestHandler` assumes the image is supplied in the correct ("upright") orientation, but `CGImage`, `CIImage`, and `CVPixelBuffer` themselves don't store orientation information — which is why every `VNImageRequestHandler` initializer has an `orientation: CGImagePropertyOrientation` parameter:

```swift
init(cgImage: CGImage, orientation: CGImagePropertyOrientation, options: [VNImageOption : Any])
init(ciImage: CIImage, orientation: CGImagePropertyOrientation, options: [VNImageOption : Any])
init(cvPixelBuffer: CVPixelBuffer, orientation: CGImagePropertyOrientation, options: [VNImageOption : Any])
init(cmSampleBuffer: CMSampleBuffer, orientation: CGImagePropertyOrientation, options: [VNImageOption : Any])
init(data: Data, orientation: CGImagePropertyOrientation, options: [VNImageOption : Any])
init(url: URL, orientation: CGImagePropertyOrientation, options: [VNImageOption : Any])
```

[Apple — VNImageRequestHandler](https://developer.apple.com/documentation/vision/vnimagerequesthandler)

`CGImagePropertyOrientation` is an enum with eight values (available since iOS 4.0):

```swift
@frozen enum CGImagePropertyOrientation
case up = 1              // Data matches the intended display orientation.
case upMirrored = 2      // Mirrored horizontally.
case down = 3            // Rotated 180°.
case downMirrored = 4    // Mirrored vertically.
case leftMirrored = 5    // Mirrored horizontally and rotated 90° counterclockwise.
case right = 6           // Rotated 90° counterclockwise.
case rightMirrored = 7   // Mirrored horizontally and rotated 90° clockwise.
case left = 8            // Rotated 90° clockwise.
```

Apple's official explanation: «For example, the pixel data for an image captured by an iOS device camera is encoded in the camera sensor's native landscape orientation. When the user captures a photo while holding the device in portrait orientation, iOS writes an orientation value of `.right` in the resulting image file.» [Apple — CGImagePropertyOrientation](https://developer.apple.com/documentation/imageio/cgimagepropertyorientation)

A typical cause of recognition failure is an incorrectly passed or entirely omitted orientation. Explanation from an independent breakdown: «The camera sensor on the iPhone is mounted in landscape orientation… When the device is in portrait mode, images coming from the camera are seen by the Core ML model as rotated 90 degrees to the right», and the fix is to pass `orientation: .right` to `VNImageRequestHandler`, «so Vision already fixes the image's rotation before passing it to Core ML». For apps supporting both portrait and landscape orientation, it's recommended to keep `AVCaptureConnection` always in landscape orientation, adjusting only the preview layer, and to pass Vision the corresponding device orientation. [Machine, Think! — How to display Vision bounding boxes](https://machinethink.net/blog/bounding-boxes/)

## 6. Confidence threshold

`confidence` in Vision is a `Float` value from `0.0` to `1.0`. For `VNRecognizedObjectObservation`/`RecognizedObjectObservation` the rule holds: the total confidence of all classifications within `labels` equals `1.0`, and the final ("real") confidence of a given label is the product `label.confidence * observation.confidence`. [Apple — VNRecognizedObjectObservation](https://developer.apple.com/documentation/vision/vnrecognizedobjectobservation)

Apple does not publish a recommended numeric confidence threshold specifically for `VNRecognizeAnimalsRequest`/`RecognizeAnimalsRequest`. In the new Swift API, for a similar task (`ClassifyImageRequest`), Apple offers not a hard threshold but the methods `hasMinimumPrecision(_:forRecall:)` and `hasMinimumRecall(_:forPrecision:)`, which filter results by a precision/recall ratio rather than a raw confidence number — meaning Apple itself recommends not picking a "magic number" by hand but describing the desired precision/recall trade-off. [Apple — Classifying images for categorization and search](https://developer.apple.com/documentation/vision/classifying-images-for-categorization-and-search)

Community practice: developers take the `identifier` (`"Cat"`/`"Dog"`) and `confidence` from `labels.first` and compare it against a threshold picked empirically for their task — there is no hard-coded "standard" value (e.g., 0.7 or 0.9) in the sources found. The specific numbers that appear in general (not Apple-specific) materials about cat/dog classifiers are thresholds for particular third-party models, not values recommended by Apple for Vision. There is no exact data on a reference threshold for Vision — "no exact data."

## 7. Limitations

- **Only cat and dog.** Both the old (`VNAnimalIdentifier.cat`/`.dog`) and the new (`RecognizeAnimalsRequest.Animal.cat`/`.dog`) API recognize exclusively two animal species — no other identifiers are described in the documentation. [Apple — VNAnimalIdentifier](https://developer.apple.com/documentation/vision/vnanimalidentifier), [Apple — RecognizeAnimalsRequest.Animal](https://developer.apple.com/documentation/vision/recognizeanimalsrequest/animal)
- **Reliability of distinguishing cat from dog.** No official Apple data on the error rate (cat mistaken for dog and vice versa) was found. "No reliable source found."
- **Drawings/images on a screen.** No specific data or official Apple statements about the behavior of `VNRecognizeAnimalsRequest` on cartoons, drawings, or a photo of a cat shown on another device's screen were found. "No reliable source found."
- **Multiple animals in frame.** `results`/`perform(on:)` return an **array** of observations (`[VNRecognizedObjectObservation]` / `[RecognizedObjectObservation]`) — meaning the API is designed to work with multiple objects in a single frame, each with its own `boundingBox` and its own `labels`. No direct Apple text stating a declared maximum number of simultaneously recognized animals in one frame was found. [Apple — VNRecognizeAnimalsRequest](https://developer.apple.com/documentation/vision/vnrecognizeanimalsrequest)

## 8. Performance and resource use (Neural Engine)

No official numeric latency benchmarks specifically for `VNRecognizeAnimalsRequest`/`RecognizeAnimalsRequest` from Apple were found. The general Vision/Core ML dispatch mechanism is described in independent sources as: «Vision (and the Core ML models it runs) dispatches automatically to the Neural Engine when available, falls back to the GPU when not, and to the CPU as a last resort» — meaning the developer doesn't choose the executor directly, the system does. [Blake Crosley — Apple Vision Framework](https://blakecrosley.com/blog/vision-framework-built-in)

The same source gives latency estimates for **other** Vision requests (not animal recognition): text recognition (OCR) — 150–300 ms per receipt page; face detection — 5–15 ms per frame; body pose at 60 fps — under 16 ms per frame; image embeddings — 20–40 ms. These figures are not from Apple and don't relate specifically to `VNRecognizeAnimalsRequest`; they're given only as an order-of-magnitude reference for on-device Vision requests. [Blake Crosley — Apple Vision Framework](https://blakecrosley.com/blog/vision-framework-built-in)

There is no separate explicit flag in Vision for accessing the Neural Engine — it happens automatically inside Core ML, which Vision requests rely on. A developer cannot directly check or force a given request to run specifically on the Neural Engine — it's a "hint" controlled by the system. [Blake Crosley — Apple Vision Framework](https://blakecrosley.com/blog/vision-framework-built-in)

## 9. Android alternative: ML Kit (Google)

For future porting to Android, Google has two distinct but related APIs within ML Kit:

### ML Kit Object Detection & Tracking

- Runs on-device («happens on the device»), not over the network.
- In a single pass it finds and tracks objects in the image, giving each one a position (bounding box); in a video stream, each object is assigned a unique ID for tracking across frames.
- Has a built-in "coarse" classifier with five categories: «home goods, fashion goods, food, plants, and places» — so there aren't many categories, and this classification doesn't give detailed information about the type of object (e.g., "cat" separately from "dog").
- Positioned as «optimized for mobile devices and intended for use in real-time applications, even on lower-end devices».

[Google — Object detection and tracking](https://developers.google.com/ml-kit/vision/object-detection)

### ML Kit Image Labeling

- The base model recognizes «more than 400 categories» — people, things, places, activities, including animal species and products.
- Intended for classifying the **whole image** («image classification models that describe the full image»), not for locating and drawing boxes around specific objects in a photo — for that task Google explicitly recommends Object Detection & Tracking: «for classifying one or more objects in an image, such as shoes or pieces of furniture, the Object Detection & Tracking API may be a better fit».
- Supports both the built-in base model and custom TensorFlow Lite/LiteRT models.

[Google — Image labeling](https://developers.google.com/ml-kit/vision/image-labeling)

**Conclusion for porting:** neither Object Detection & Tracking nor Image Labeling gives an out-of-the-box, equally targeted "cat/dog" identifier pair with a bounding box, the way the `VNRecognizeAnimalsRequest` + Vision combination does. The closest in spirit is Image Labeling (labels like "cat" may appear among its 400+ categories), but the official ML Kit Image Labeling label list wasn't opened within the scope of this research — the exact list needs to be checked separately against the `label-map` in Google's documentation.

## Sources

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
- [Apple — Vision framework (overview)](https://developer.apple.com/documentation/vision)
- [Apple — Classifying images for categorization and search](https://developer.apple.com/documentation/vision/classifying-images-for-categorization-and-search)
- [Apple — WWDC24: Discover Swift enhancements in the Vision framework](https://developer.apple.com/videos/play/wwdc2024/10163/)
- [Machine, Think! — How to display Vision bounding boxes](https://machinethink.net/blog/bounding-boxes/)
- [Blake Crosley — Apple Vision Framework: On-Device CV Most Devs Skip](https://blakecrosley.com/blog/vision-framework-built-in)
- [Google — Object detection and tracking (ML Kit)](https://developers.google.com/ml-kit/vision/object-detection)
- [Google — Image labeling (ML Kit)](https://developers.google.com/ml-kit/vision/image-labeling)
