# Determining a cat's coat on-device on iOS without the cloud — 2026-08-24

Question: can coat, pattern, fur length, eye color and white markings of a cat be recognized by the iOS device itself — without a cloud vision model and without an intermediary node.

## Summary

1. The `VNClassifyImageRequest` / `ClassifyImageRequest` taxonomy is confirmed to contain **1303 categories** (cross-checked against three independent dumps published by developers on GitHub). There are no cat breeds or coats there at all — only five general words: `cat`, `adult_cat`, `kitten`, `bobcat`, `feline`. Neither `tabby`, nor `calico`, nor `siamese`, nor `persian`, nor `tortoiseshell` appears even once. Meanwhile the same list has over thirty dog breeds (`beagle`, `corgi`, `dachshund`, `pomeranian`, and so on). The word `tuxedo` is present in the list, but it's a piece of clothing (a tuxedo), not a cat's "tuxedo" coat — its alphabetical neighbors are `turtle`, `typewriter`, not other cat designations.
2. `hasMinimumPrecision(_:forRecall:)` and `hasMinimumRecall(_:forPrecision:)` are an official, documented Apple way to filter the classifier's output for the balance of recall and precision you need. A working example is right in the official documentation.
3. `VNDetectAnimalBodyPoseRequest` (and the new `AnimalBodyPoseRequest` without the VN prefix) officially recognizes specifically cats and dogs — confirmed by the transcript of WWDC23-10045 — and gives 25 body points: ears, eyes, nose, neck, elbows/knees/paws (separately for front and back, left and right), three tail points. There is no separate "chest" point in the list.
4. The dominant coat color can actually be obtained on your own through Core Image (`CIAreaAverage`, `CIKMeans`) — this is a working, free, but not perfectly reliable method under poor lighting and mottled fur.
5. Pattern (striped vs. solid) is not a question of an Apple API but a question of texture analysis using general image-processing tools. Such features exist (brightness variance, Fourier spectrum, local binary patterns), but their reliability specifically on photos of cat fur was not measured by anyone while preparing this analysis — there are no confirmed accuracy figures.
6. No ready-made open Core ML model specifically for cat coat or pattern was found — neither on the official Apple ML models page, nor in the large curated Awesome-CoreML-Models list. GitHub has a breed model (37 classes, judging by the number — likely the Oxford-IIIT Pet Dataset) weighing about 98 MB, but with no license file — using it in a commercial product is legally risky. There is a general SigLIP zero-shot classification model (Apache-2.0 license, about 386 MB total across two files), but its accuracy specifically on fine distinctions of cat coats has not been checked.
7. Eye color can only be obtained very roughly: Vision gives a `leftEye`/`rightEye` point from the animal's body pose, but does not isolate the iris or pupil. There is no separate API for finding animal eyes (an analogue of `VNDetectFaceLandmarksRequest` for humans) in Vision.
8. Of the five needed traits, only fur length (indirectly, via a custom heuristic) and an approximate dominant color can be obtained reliably and for free on-device. Pattern, exact eye color and exact white markings are, at best, a rough approximation via custom heuristics, not a ready-made solution from Apple.

## 1. Vision image classification: are cat coats and breeds in the taxonomy

### How to get the category list

The old Objective-C-compatible API is `VNClassifyImageRequest`. It provides a static method returning all classifications for a specific algorithm revision:

```swift
import Vision

// VNClassifyImageRequestRevision1 — the only revision that exists today.
let allClassifications = try VNClassifyImageRequest
    .knownClassifications(forRevision: VNClassifyImageRequestRevision1)

for observation in allClassifications {
    print(observation.identifier)
}
print("Total categories: \(allClassifications.count)")
```

The signature is confirmed against the official documentation page:

```
class func knownClassifications(forRevision requestRevision: Int) throws -> [VNClassificationObservation]
```

The new Swift variant is `ClassifyImageRequest` (available from iOS 18). Its category list is exposed through an instance property:

```swift
import Vision

let request = ClassifyImageRequest()
let identifiers = request.supportedIdentifiers   // [String], var supportedIdentifiers: [String] { get }
print(identifiers.count)
```

Apple's documentation itself **does not publish** the contents of this list as text — neither on the `ClassifyImageRequest` page nor on the `knownClassifications(forRevision:)` page. The list can only be obtained by calling the method on-device. This is confirmed by the content of both official pages (retrieved via the internal JSON endpoint `developer.apple.com/tutorials/data/...`): the text is limited to the phrase "Requests the collection of classifications that the Vision framework recognizes" — with no listing.

### A full list published by someone

Three independent people dumped and published the result of calling `knownClassifications(forRevision:)` as gist files on GitHub:

- `ktustanowski/56c0d7541813868fed4aceb60ab5d149` — "Contains a list of supported identifiers for VNClassifyImageRequest (VNClassifyImageRequestRevision1)", 1303 lines.
- `ozgurshn/0e19568b3f930c58491ddbbe7dbb9170` — "VNClassifyImageRequest supportedIdentifiers", the same set as a JSON array.
- `mikeparisstuff/94a31c29e2bc1e84faea39429bb3879f` — "VNClassifyImageRequest_supportedIdentifiers_dec_26_2023.csv", 1302 data lines (not counting a possible header line).

All three files were downloaded and cross-checked line by line with grep. The category count converges on **1303** in all three (revision 1). The cat entries are identical across all three lists and are limited to exactly five items:

```
adult_cat
bobcat
cat
feline
kitten
```

Neither `tabby`, nor `calico`, nor `siamese`, nor `persian`, nor `tortoiseshell`, nor `maine_coon`, nor `ragdoll`, nor `sphynx`, nor `bengal`, nor `abyssinian` was found even once in any of the three files (checked via exact substring search, including a check for false matches — `tabbouleh`, the Middle Eastern dish, is in the list, but that's not "tabby"). By contrast, the same list has many dog breeds: `australian_shepherd`, `basenji`, `beagle`, `basset`, `bichon`, `bulldog`, `chihuahua`, `collie`, `corgi`, `dachshund`, `dalmatian`, `doberman`, `german_shepherd`, `greyhound`, `husky`, `jack_russell_terrier`, `malamute`, `malinois`, `mastiff`, `newfoundland`, `pitbull`, `pomeranian`, `poodle`, `pug`, `retriever`, `ridgeback`, `rottweiler`, `saint_bernard`, `schnauzer`, `setter`, `sheepdog`, `spaniel`, `terrier`, `vizsla`, `weimaraner`, `irish_wolfhound`, `bernese_mountain`, `hound` — over thirty entries.

The word `tuxedo` is present in the list (checked), but its alphabetical neighbors are `turmeric`, `turntable`, `turtle`, `typewriter` — the whole block belongs either to kitchenware or to clothing/equipment items; the closest clothing category by meaning — `bowtie`, `gown`, `kilt`, `poncho`, `suit` — is also in the list. There is no basis for treating `tuxedo` in this taxonomy as meaning a cat's "tuxedo" coat; for this point — "not confirmed", and going by the overall context — "probably an ordinary tuxedo as a clothing item."

**Separately, about the related `VNRecognizeAnimalsRequest`** (not the one asked about, but easily confused with it): this is a separate, older request that recognizes not 1303 categories but exactly two animal species. The official documentation directly points to the `knownAnimalIdentifiers(forRevision:)` method for obtaining the list, and an independently published breakdown (a Medium article with a code sample and a link to the repository) shows that for revision 1 this list is `["Cat", "Dog"]`. This confirms that wherever Apple does give animals their own category, it stops at the species level, not coat or breed.

## 2. How to filter classifier output: hasMinimumPrecision / hasMinimumRecall

Both methods are declared on `VNClassificationObservation` (and on the new `ClassificationObservation`):

```
func hasMinimumPrecision(_ minimumPrecision: Float, forRecall recall: Float) -> Bool
func hasMinimumRecall(_ minimumRecall: Float, forPrecision precision: Float) -> Bool
```

Official definition (the `hasPrecisionRecallCurve` page, Discussion section):

> Precision refers to the percentage of your classification results that are relevant, while recall refers to the percentage of total relevant results correctly classified.

That is, precision is what fraction of the returned labels is actually correct, and recall is what fraction of the labels that are actually present was returned at all. Both methods only work when `hasPrecisionRecallCurve == true` — if `false`, the result will not be meaningful (this is separately noted in the documentation).

Apple's official code sample (the documentation article "Analyze and label images using a Vision classification request") shows exactly the scenario needed for the game — filtering by threshold with an explicit choice of strategy:

```swift
// Vision request to classify an image.
let request = ClassifyImageRequest()

// Perform the request on the image, and return an array of ClassificationObservation objects.
let results = try await request.perform(on: url)
    // High recall: let more candidates through, but higher risk of false positives.
    .filter { $0.hasMinimumPrecision(0.1, forRecall: 0.8) }
    // High precision: fewer candidates, but more reliable ones.
    // .filter { $0.hasMinimumRecall(0.01, forPrecision: 0.9) }
```

Explanation from the same Apple article: "A high-recall filter provides a much broader range of observations, but can result in more false positive results... If an app can't tolerate false positive results, the hasMinimumRecall method allows for a high-precision filter... Increasing precision decreases recall, and increasing recall decreases precision. Testing can help determine the balance point."

In other words, Apple explicitly advises against relying on a single universal `confidence > X` threshold, and instead recommends choosing between the two methods based on which is more costly — missing a correct candidate or accepting an incorrect one — and tuning the specific numbers by testing on your own data. For the task of "determining a cat's coat" this isn't critical by itself, since there are no coat categories in the taxonomy (see section 1) — but the method will be useful if you decide to recognize at least the fact "this is a cat" via `cat` / `feline` / `kitten` and avoid confusing it with noise.

## 3. Animal body points: VNDetectAnimalBodyPoseRequest

The official documentation (the `VNDetectAnimalBodyPoseRequest` page and the new `DetectAnimalBodyPoseRequest`, available from iOS 18) does not state the animal species directly in the request page's text. But the transcript of the WWDC23 talk, session 10045, which is specifically about this request, says directly:

> «The request supports cats and dogs, and detects 25 animal body landmarks that includes the tail and the ears.»

The number 25 matches the official list of points given by the `VNAnimalBodyPoseObservation.JointName` page (retrieved via the documentation's internal JSON endpoint):

- **Head (10 points):** `leftEarTop`, `leftEarMiddle`, `leftEarBottom`, `leftEye`, `neck`, `nose`, `rightEye`, `rightEarTop`, `rightEarMiddle`, `rightEarBottom`.
- **Legs (12 points):** `leftBackElbow`, `leftFrontElbow`, `rightFrontElbow`, `rightBackElbow`, `leftBackKnee`, `leftFrontKnee`, `rightBackKnee`, `rightFrontKnee`, `leftBackPaw`, `leftFrontPaw`, `rightBackPaw`, `rightFrontPaw`.
- **Tail (3 points):** `tailTop`, `tailMiddle`, `tailBottom`.

10 + 12 + 3 = 25 — matches the number from the talk, which confirms the list independently of the transcript.

There's also a separate enumeration of point groups — `VNAnimalBodyPoseObservation.JointsGroupName`: `all`, `forelegs`, `head`, `hindlegs`, `tail`, `trunk`. An important observation: the `trunk` group is present in the enumeration, but there's no separately named chest or flank point among either the head points, the leg points, or the tail points. That is, **Vision does not give a direct "chest" point**.

Practical conclusion for white markings:

- Paws: the `leftFrontPaw` / `rightFrontPaw` / `leftBackPaw` / `rightBackPaw` points give exact coordinates for where the paw is in the photo — a small area around each point can be cropped and evaluated for whether it's white or not (see section 4 on color determination).
- Muzzle: the `nose` point plus `leftEye`/`rightEye` are enough to crop the muzzle area.
- Chest: there's no dedicated point. The closest approximation is the area between the `neck` point and the front leg points (`leftFrontElbow`/`rightFrontElbow`), meaning the area has to be constructed by hand rather than taken from a ready-made point. This is less reliable than for the paws and muzzle, and such a heuristic should not be relied on as a precise signal.

Code example (current Swift API, iOS 18+):

```swift
import Vision

let request = DetectAnimalBodyPoseRequest()
let observations = try await request.perform(on: image)

if let animal = observations.first {
    let points = try animal.recognizedPoints(.all)
    if let frontLeftPaw = points[.leftFrontPaw], frontLeftPaw.confidence > 0.3 {
        let location = frontLeftPaw.location // normalized coordinates (0...1)
        // Next: convert to photo pixels and crop an area around the point
        // for color estimation (section 4).
    }
}
```

For the older but still supported API (iOS 17+) — `VNDetectAnimalBodyPoseRequest` with `VNImageRequestHandler` — the point structure is the same (`VNAnimalBodyPoseObservation.JointName`).

## 4. Determining the dominant color on your own

### CIAreaAverage — the simplest method

`CIAreaAverage` is confirmed as a real Core Image protocol (a subclass of `CIAreaReductionFilter`, i.e. in the family of filters that reduce an image area to a single value). It returns a 1×1 pixel image with the average color of the given area — sufficient for a rough estimate of the dominant color if the area is cropped beforehand (for example, using the trunk points from section 3, minus the paws and muzzle, so as not to spoil the result with white markings):

```swift
import CoreImage

func averageColor(of image: CIImage, in extent: CGRect) -> (r: UInt8, g: UInt8, b: UInt8)? {
    guard let filter = CIFilter(name: "CIAreaAverage") else { return nil }
    filter.setValue(image, forKey: kCIInputImageKey)
    filter.setValue(CIVector(cgRect: extent), forKey: kCIInputExtentKey)
    guard let outputImage = filter.outputImage else { return nil }

    var pixel = [UInt8](repeating: 0, count: 4)
    let context = CIContext(options: [.workingColorSpace: NSNull()])
    context.render(outputImage,
                    toBitmap: &pixel,
                    rowBytes: 4,
                    bounds: CGRect(x: 0, y: 0, width: 1, height: 1),
                    format: .RGBA8,
                    colorSpace: nil)
    return (pixel[0], pixel[1], pixel[2])
}
```

Drawback of the simple average: if the area includes fur, background, and shadow all together, the result will "smear" into a dirty gray. For a more honest result, the photo should first be cropped to the animal's contour (Vision provides `VNGeneratePersonSegmentationRequest` for people, but there's no Apple-provided segmentation for animals — you'd have to either use the `boundingBox` from `VNRecognizeAnimalsRequest`/`VNDetectAnimalBodyPoseRequest` as a rough approximation, or write your own segmentation).

### CIKMeans — extracting several dominant colors

`CIKMeans` is also a confirmed real Core Image protocol, with the properties `count: Int` (how many clusters/colors to find), `passes: Float` (number of iterations), `perceptual: Bool` (whether to compute in a perceptual color space), and `inputMeans: CIImage?` (initial cluster centers). It performs exactly k-means over the color of an area and returns an image with a row of cluster pixels:

```swift
import CoreImage

func dominantColors(of image: CIImage, in extent: CGRect, count: Int = 3) -> [CIColor] {
    guard let filter = CIFilter(name: "CIKMeans") else { return [] }
    filter.setValue(image, forKey: kCIInputImageKey)
    filter.setValue(CIVector(cgRect: extent), forKey: kCIInputExtentKey)
    filter.setValue(count, forKey: "inputCount")
    filter.setValue(Float(10), forKey: "inputPasses")
    filter.setValue(true, forKey: "inputPerceptual")
    guard let outputImage = filter.outputImage else { return [] }

    let context = CIContext()
    var pixels = [UInt8](repeating: 0, count: count * 4)
    context.render(outputImage,
                    toBitmap: &pixels,
                    rowBytes: count * 4,
                    bounds: CGRect(x: 0, y: 0, width: count, height: 1),
                    format: .RGBA8,
                    colorSpace: nil)

    return (0..<count).map { i in
        CIColor(red: CGFloat(pixels[i*4]) / 255,
                green: CGFloat(pixels[i*4+1]) / 255,
                blue: CGFloat(pixels[i*4+2]) / 255)
    }
}
```

k-means is more useful than a plain average because with two-colored fur (bicolor, for example) it will return the "base" and the "markings" separately rather than a blend of the two. The key property names (`count`, `inputMeans`, `passes`, `perceptual`) are confirmed against the official documentation; the specific string keys for `setValue(forKey:)` (`inputCount`, `inputPasses`, `inputPerceptual`) follow the general Core Image naming convention (`input` + the property name capitalized) and were not cross-checked against an official list of filter keys — before using this in production it's worth printing `filter.inputKeys` on a real device and verifying.

`vImage` (Accelerate) can also do histograms and pixel statistics, but the exact signature of the function needed for this task could not be confirmed within this analysis — when choosing between Core Image and vImage for a first implementation, it's more sensible to take Core Image: it's simpler to combine with the Vision/CIImage pipeline already in use.

### Mapping color to a six-coat palette

A simple and practical approach is to convert the resulting RGB color to HSV/HSB and compare by hue and brightness (Value) rather than by "raw" RGB, because HSV is more robust to changes in lighting:

```swift
enum CoatColor: String, CaseIterable {
    case ginger, grey, black, white, cream, brown
}

func classifyCoatColor(r: CGFloat, g: CGFloat, b: CGFloat) -> CoatColor {
    let color = UIColor(red: r, green: g, blue: b, alpha: 1)
    var hue: CGFloat = 0, sat: CGFloat = 0, brightness: CGFloat = 0, alpha: CGFloat = 0
    color.getHue(&hue, saturation: &sat, brightness: &brightness, alpha: &alpha)
    let hueDegrees = hue * 360

    if brightness > 0.85 && sat < 0.15 { return .white }
    if brightness < 0.25 { return .black }
    if sat < 0.2 { return .grey }
    if hueDegrees < 30 || hueDegrees > 340 {
        return brightness > 0.6 ? .cream : .brown
    }
    if hueDegrees < 45 { return .ginger }
    return .brown
}
```

The thresholds (`0.85`, `0.25`, `30°`, and so on) in this example are a starting point, not verified constants: they will have to be tuned on real photos of cats with the target coats, because "ginger" and "cream", "grey" and "brown" overlap in HSV space more than they seem to the eye.

## 5. Determining pattern (striped / solid) on your own

Apple has no ready-made API here — the question comes down entirely to the general theory of texture analysis, drawn not from Vision documentation but from common image-processing practice. Below are features that are genuinely used for distinguishing textures in general (not specifically cat fur — no specialized publications on distinguishing tabby/solid on iOS could be found):

- **Spread (variance) of brightness over an area.** The cheapest feature: for solid fur, local brightness variance is low; for striped fur it's higher, due to the alternation of light and dark stripes. Computed via `CIAreaHistogram` (a confirmed real Core Image protocol, also a subclass of `CIAreaReductionFilter`) — a brightness histogram over the area, from which variance is easy to derive.
- **Fourier spectrum.** Stripes are a periodic structure, and in the frequency domain they produce a pronounced peak at the frequency corresponding to the stripe width. For a solid fill, energy is concentrated almost entirely at zero frequency. Vision/Core Image have no FFT of their own, but `vDSP` (Accelerate) provides fast Fourier transform functions applicable to an array of pixel brightness values.
- **Local Binary Patterns (LBP).** A classic texture feature in computer vision: for each pixel, its brightness is compared to that of its neighbors and the result is encoded as a binary number, then a histogram of such numbers is built over the area. Striped and solid textures produce statistically different histograms. There's no ready-made LBP implementation in Vision/Core Image — it would have to be written by hand on top of a raw pixel buffer (`CVPixelBuffer` / `vImage_Buffer`).
- **Contrast estimation via the gray-level co-occurrence matrix (GLCM) and Haralick features.** A heavier but more informative classic texture-analysis method — also requires a hand-rolled implementation; nothing ready-made exists in iOS system frameworks.

Example of a working but crude feature based on brightness variance (requires nothing beyond Core Image):

```swift
import CoreImage

func brightnessVariance(of image: CIImage, in extent: CGRect) -> CGFloat? {
    guard let filter = CIFilter(name: "CIAreaHistogram") else { return nil }
    filter.setValue(image, forKey: kCIInputImageKey)
    filter.setValue(CIVector(cgRect: extent), forKey: kCIInputExtentKey)
    filter.setValue(64, forKey: "inputCount")   // number of histogram bins
    filter.setValue(1.0, forKey: "inputScale")
    guard let outputImage = filter.outputImage else { return nil }

    let context = CIContext()
    var bins = [UInt8](repeating: 0, count: 64 * 4)
    context.render(outputImage,
                    toBitmap: &bins,
                    rowBytes: 64 * 4,
                    bounds: CGRect(x: 0, y: 0, width: 64, height: 1),
                    format: .RGBA8,
                    colorSpace: nil)

    // Take the brightness channel (e.g. R after a prior conversion to grayscale)
    // and compute the variance of the distribution across the 64 bins — then compare to a threshold.
    let values = stride(from: 0, to: bins.count, by: 4).map { Double(bins[$0]) }
    let mean = values.reduce(0, +) / Double(values.count)
    let variance = values.map { ($0 - mean) * ($0 - mean) }.reduce(0, +) / Double(values.count)
    return CGFloat(variance)
}
```

### Honest feasibility assessment

Brightness spread can, in principle, distinguish an obviously striped cat from an obviously solid black or white one — this is plausible as a first approximation. But this approach has serious weaknesses that can't be ignored:

- Shadow, fur folds, flash glare, and simply uneven lighting create exactly the same kind of brightness spread as stripes do — the feature confuses pattern with shooting conditions.
- Bicolor and calico coats (two or three solid patches of different color) also produce high brightness variance, but that's not "stripedness" in the tabby sense — the same feature doesn't distinguish between different types of pattern, only "there is spread / there isn't."
- None of the listed features was tested here on a real set of cat photos — this is general texture-analysis theory, not a solution verified on cat fur. Claiming a specific accuracy figure (high or low) would be fabrication.

Bottom line on this point: technically implementable as a crude heuristic requiring hand-tuning and manual threshold calibration on your own photo set; there is no ready-made, reliable out-of-the-box solution, and getting one on-device without collecting and labeling your own data is unlikely to work.

## 6. Core ML and ready-made models

### The official Apple ML models page

The page `developer.apple.com/machine-learning/models/` was checked. It only lists general-purpose image models: FastViT, MobileNetV2, ResNet-50, MNIST, and similar (trained on ImageNet or comparable general datasets). **There is not a single model for animal breed, coat, or pattern on the official page.**

### The Awesome-CoreML-Models curated list

`likedan.github.io/Awesome-CoreML-Models` was checked — a large, long-running list of ready-made Core ML models from across the internet. No specialized model for cat coat, pattern, or breed was found in it.

### What was found on GitHub

- **`GitMAM/Breeds_core_ml`** — a model with 37 breed classes (judging by the class count, this resembles the classic Oxford-IIIT Pet Dataset — 12 cat breeds and 25 dog breeds, but the repository itself doesn't explicitly name the data source, so this is an assumption, not a confirmed fact). Trained with PyTorch/fast.ai on top of ResNet-50. The `model_breeds.mlmodel` file weighs **102,794,417 bytes, i.e. about 98 MB** (confirmed directly via the GitHub API, `contents` endpoint). The README claims "accuracy 99.95%" while also stating `error_rate 0.055480` — these two numbers contradict each other (an error rate of 0.05548 corresponds to an accuracy of about 94.5%, not 99.95%); this is an internal inconsistency in the README itself, not an independently verified result. **There is no license** — no `LICENSE` file, no mention of a license in the repository (confirmed via `api.github.com/repos/.../license`, response "Not Found"). Without an explicit license, a repository's text and code are by default fully protected by copyright — using it in a commercial product without the author's direct permission is legally unsafe.
- Important: even if this model were free to use, it classifies **breed**, not coat — "British Shorthair" or "Persian" doesn't tell you whether the cat is ginger or grey, striped or solid. The game's task ("ginger, grey, black..." and so on) is coat and pattern classification, not breed classification; a ready-made breed model isn't enough for the game's needs even with a license.
- **`AranFononi/Animal-Classifier-Pet-Recognition-CoreML-Model`** — a classifier of animal species (dog/cat/rabbit), not breed and not coat. The model is tiny (the `PetImageClassifier.mlmodel` file is about 13 KB), and it also has no license (same verification method, same "Not Found" result). Useless for the game's purposes — the animal species is already known (it's the player's cat).
- **SigLIP ViT-B/16, converted to Core ML** (repository `john-rocky/CoreML-Models`) — a zero-shot classification model in the style of CLIP: it takes an image and an arbitrary list of text labels ("ginger tabby cat", "solid black cat", and so on) as input, and outputs a similarity score for each label. License — **Apache-2.0** (confirmed by a direct link in the repository's table to `apache.org/licenses/LICENSE-2.0`; the original model is `google/siglip-base-patch16-224` on Hugging Face). Size — **about 386 MB total across two files** (an image encoder plus a text encoder, FP16 format). This is the only model found that is theoretically suitable for the game's task (you can just plug in six coats and six patterns as text), but: first, a size of 386 MB is a lot for a mobile game (comparable to the size of the app itself); second, no accuracy testing was done specifically on fine distinctions of cat coats (ginger vs. cream, tabby vs. calico) — CLIP-like models are known to distinguish large objects and scenes well, but noticeably worse at fine visual attributes like shade or stripe frequency.

### Section summary

There is no open, license-clean, accuracy-verified Core ML model for cat coat or pattern — not from Apple, and not in the community. There is one large general-purpose option (SigLIP) with a clean license but unverified accuracy for this specific task and a hefty size, and one narrowly specialized option (breeds) with no license that doesn't even solve the coat problem.

## 7. Eye color

There is no separate Vision request for finding animal eyes specifically. Checking the full range of Vision framework capabilities (the main documentation page, the "Pose analysis" section and the "Image classification and recognition" section) shows that only two requests are officially documented for animals:

- `DetectAnimalBodyPoseRequest` / `VNDetectAnimalBodyPoseRequest` — body pose (section 3 of this document);
- `RecognizeAnimalsRequest` / `VNRecognizeAnimalsRequest` — animal species recognition (cat/dog, section 1).

The analogue of `VNDetectFaceLandmarksRequest`, which for the human face gives Apple's precise geometry of eyes, pupils and eyelids, exists **only for humans**. There is no separate "AnimalFaceLandmarks" or "AnimalEyeRequest" in Vision.

In practice this means the only handle available is the `leftEye` and `rightEye` points from `VNAnimalBodyPoseObservation.JointName` (section 3). But this is **one point per eye**, not an outline of the iris or pupil: Vision marks the approximate location of the eye's center, not its shape or boundaries. To determine eye color, you'd have to:

1. Take the `leftEye`/`rightEye` coordinate;
2. Crop a very small area around it (a radius of single to tens of pixels, depending on the photo's resolution);
3. Average the color in that area (using the methods from section 4);
4. Filter out the sclera, flash glare, and any fur that happened to fall into the cropped area — there's no ready-made solution for this; separating the iris from everything else would require a hand-tuned, unreliable heuristic based on brightness and saturation.

Bottom line: eye color can in principle be attempted, but it's the shakiest of all five traits — at low photo resolution, an imperfect angle, or squinted eyes, the `leftEye`/`rightEye` point is either absent (low recognition confidence) or lands on the eyelid or the fur around the eye rather than the iris. There is no standard, Apple-supported way to do this — only a homemade heuristic built on top of a single point.

## Verdict

For each of the five traits — what can actually be obtained on-device for free, and what can't:

1. **Dominant color (ginger, grey, black, white, cream, brown).** Can genuinely be obtained approximately. `CIAreaAverage`/`CIKMeans` plus conversion to HSV give a working, free estimate. Reliability depends on lighting and on how cleanly the fur area is cropped (without background and without paws/muzzle with their possible white patches). This is the only one of the five traits where the proposed method is close to a "ready-made solution" rather than a raw heuristic.
2. **Pattern (solid, tabby, bicolor, calico, tuxedo, pointed).** There's no ready-made method either in Vision or as a free Core ML model. A custom heuristic based on brightness variance is technically feasible but doesn't distinguish between different types of pattern (stripes vs. patches), confuses pattern with shadows and lighting, and was never tested on real cat photos within this analysis. In plain terms: **pattern cannot be reliably obtained without the cloud** — what can be assembled on your own is, at best, a crude "there's contrast / there isn't" switch, not a full six-category classification.
3. **Fur length (short, long).** Not treated separately in the task's requirements as one of the explicitly checked points of Apple's documentation, but based on everything examined: none of the checked Vision APIs (`ClassifyImageRequest`, `VNDetectAnimalBodyPoseRequest`, `VNRecognizeAnimalsRequest`) gives such a category directly. In principle, fur length could be estimated indirectly through the silhouette (the animal's contour relative to `boundingBox`, blurriness of the fur's edges) — this is a separate segmentation and contour-analysis task, for which Vision also has no ready-made animal-specific tool; a custom heuristic is possible but was not tested.
4. **Eye color (green, amber, blue).** Can only be obtained very approximately, from a single `leftEye`/`rightEye` point from body pose, without isolating the iris. This is the least reliable of all the traits: a single point that doesn't account for eye shape, sensitive to photo resolution and angle.
5. **White markings (chest, paws, muzzle).** Paws and muzzle — can genuinely be determined acceptably: `VNDetectAnimalBodyPoseRequest` gives precise coordinates for the paws and muzzle (via `nose`/`leftEye`/`rightEye`), and color can be estimated around them. Chest — there's no dedicated point; the area would have to be constructed from the `neck` point and the front leg points, which is noticeably less reliable.

**Is it worth giving up the cloud model for this.** Of the five traits, only the dominant color comes out fully and for free on-device, and with reservations — the paw and muzzle markings too. Pattern — the key, distinguishing trait of a cat in the game — cannot be reliably determined on-device without training a custom model; no ready-made open model exists for this specific task, and the general-purpose SigLIP (Apache-2.0, ~386 MB) is unverified for fitness and too heavy for a mobile game. Eye color comes out, at best, approximate.

If the owner is willing to accept pattern and eye color as "whatever a rough heuristic produces, with possibly noticeable errors, and it will fall on game design to handle the ambiguous cases," then the intermediary node can indeed be removed, and part of the logic (dominant color, part of the markings) moved on-device. But this is not an equivalent replacement for the cloud vision model in terms of result quality — it's a deliberate reduction in accuracy to save on the cloud. If it's essential to the game that the cat's in-game pattern match the player's real cat's coat (i.e., if that's an advertised mechanic rather than "an approximately similar kitten"), abandoning the cloud model in favor of a bare Vision + Core Image + custom-heuristics stack is a real risk of widespread player complaints about a misidentified pattern, not just a minor technical shortfall. Savings on the intermediary service (four tasks and a whole service) are real and achievable only partially — it can't be fully removed either way if some external source of truth or manual player correction is kept for pattern and eye color.

## Sources

Official Apple documentation (retrieved via the internal JSON endpoint `developer.apple.com/tutorials/data/documentation/...`, since ordinary page loading returns only the title):

- `documentation/vision/vnclassifyimagerequest` — the request's description and the `knownClassifications(forRevision:)` method.
- `documentation/vision/vnclassifyimagerequest/knownclassifications(forrevision:)` — the method's exact signature.
- `documentation/vision/classifyimagerequest` and `documentation/vision/classifyimagerequest/supportedidentifiers` — the new Swift API and its property.
- `documentation/vision/classifying-images-for-categorization-and-search` — the official example article with working code for `hasMinimumPrecision`/`hasMinimumRecall`.
- `documentation/vision/vnclassificationobservation`, `.../hasminimumprecision(_:forrecall:)`, `.../hasminimumrecall(_:forprecision:)`, `.../hasprecisionrecallcurve` — the precision/recall filtering methods.
- `documentation/vision/vndetectanimalbodyposerequest`, `documentation/vision/detectanimalbodyposerequest` — the old and new animal pose requests, including checking platform and minimum OS version support (iOS 17 / iOS 18 respectively).
- `documentation/vision/vnanimalbodyposeobservation/jointname`, `.../jointsgroupname` — the full list of 25 named body points and six groups.
- `documentation/vision/vnrecognizeanimalsrequest` — the animal species recognition request.
- `documentation/vision` — the framework's overall map, "Pose analysis" and "Image classification and recognition" sections, used to check the complete list of animal-specific requests.
- `documentation/coreimage/ciareaaverage`, `.../cikmeans`, `.../ciareahistogram`, `.../cikmeans/count`, `.../cikmeans/inputmeans`, `.../cikmeans/passes`, `.../cikmeans/perceptual`, `.../ciareareductionfilter/extent` — confirmation that the Core Image filters and their properties are real.

Transcript of an official Apple talk:

- WWDC23, session 10045 (Vision framework, animal pose) — the quote on cat and dog support and on the 25 body points.

Independent publications with a dump of the classifier's full category list (used as the source of the list itself, with the matching category count and matching contents manually verified across all three files):

- Gist `ktustanowski/56c0d7541813868fed4aceb60ab5d149` — "VNClassifyImageRequest.Supportedidentifiers.txt", 1303 categories.
- Gist `ozgurshn/0e19568b3f930c58491ddbbe7dbb9170` — the same list as a JSON array.
- Gist `mikeparisstuff/94a31c29e2bc1e84faea39429bb3879f` — "VNClassifyImageRequest_supportedIdentifiers_dec_26_2023.csv".
- Article by Kamil Tustanowski, Medium, "Animals detection using the Vision framework" — confirmation that `VNRecognizeAnimalsRequest` (revision 1) recognizes exactly `["Cat", "Dog"]`.

Verification of ready-made Core ML models:

- `developer.apple.com/machine-learning/models/` — Apple's official model list (confirmed the absence of animal coat/breed models).
- `likedan.github.io/Awesome-CoreML-Models` — the community's curated list (confirmed the absence of specialized models).
- `github.com/GitMAM/Breeds_core_ml` — the 37-class breed model; file size and absence of a license confirmed via `api.github.com/repos/GitMAM/Breeds_core_ml/contents/` and `.../license`.
- `github.com/AranFononi/Animal-Classifier-Pet-Recognition-CoreML-Model` — the animal species classifier; size and absence of a license confirmed the same way.
- `github.com/john-rocky/CoreML-Models` (README.md file, "Zero-Shot Image Classification" section) — the SigLIP ViT-B/16 model, Apache-2.0 license, ~386 MB size, original is `google/siglip-base-patch16-224` on Hugging Face.
- `robots.ox.ac.uk/~vgg/data/pets/` — the official Oxford-IIIT Pet Dataset page, 37 categories (12 cat breeds, 25 dog breeds), used only to compare the class count with the `Breeds_core_ml` model — no direct connection between the dataset and that specific repository is confirmed.

