import Foundation
import Vision
import ImageIO
import CoreGraphics
import CoreVideo

// Task 50-photo/05: measure the cat's INDIVIDUATING marks from her photograph,
// on the device, instead of asking a language model to describe them.
//
// Core/CatSpot.cs states the reason in forensic terms: colour, pattern, fur
// length, eye colour and white markings are all CLASS characteristics — they
// narrow a pool of 288 drawable cats and identify nobody. A white sock on ONE
// paw is what a person recognises her own cat by. This file measures that.
//
// Three Vision requests, each performed on its own handler over one decoded
// image:
//
//   VNRecognizeAnimalsRequest             (iOS 13) — is it a cat, how sure
//   VNGenerateForegroundInstanceMaskRequest (iOS 17) — which pixels are her
//   VNDetectAnimalBodyPoseRequest         (iOS 17) — 25 landmarks, cat or dog
//
// The last two are why the deployment target moved to 17.0
// (ProjectSettings.asset `iOSTargetOSVersionString: 17.0`).
//
// The measurement is deliberately not a verdict. For each place it reports how
// far that patch of coat sits from HER OWN median lightness, in CIE L* points,
// and lets C# pick the threshold — because a threshold has to be tuned against
// `fixtures/reference-photos`, and a number that crossed the boundary as
// "light"/"dark" could never be re-tuned without another device build.
//
// Nothing leaves this file and nothing is written down here. There is no
// NSLog in this file on purpose: an error string may name a Vision failure,
// never a pixel, a path or a size. The photo exists as bytes in memory for
// the length of one call, and this measurement is the only use it is put to
// here.
//
// What the game does with the accepted photo afterwards is a different layer
// and a different claim: Shell/CatPhoto.cs crops it, and Core/TraitsRequest.cs
// sends that crop to the traits Worker (worker/src/index.ts), which forwards
// it to a model. See tasks/50-photo/15-privacy-wording/NOTES.md for the whole
// path, told honestly rather than by extension from this file.

// MARK: - What crosses to C#

/// One of the 25 animal-pose joints, in pixels with the origin top-left, ready
/// to draw on the same image `CatVision.swift` reported its box against.
private struct Landmark: Encodable {
    let name: String
    let x: Int, y: Int
    let confidence: Double
}

/// One measured place. Not a verdict: `delta` is signed CIE L* points against
/// this cat's own body median, and C# decides what counts as a mark.
private struct Mark: Encodable {
    /// A `spot_place` from `CatTraits.Allowed`, or `paws` on the mask-only
    /// rung, where the two front paws cannot be told apart. `grouped` says
    /// which.
    let place: String
    /// Median L* inside the sampled disc, 0…100.
    let lightness: Double
    /// `lightness` − the body median, in L* points. Positive is lighter than
    /// her own coat, negative darker. Roughly: 1 point is the smallest
    /// difference an eye resolves; a white sock on a grey cat is tens.
    let delta: Double
    /// Pixels inside both the disc and the cat mask that the median came from.
    let samples: Int
    /// The lowest confidence of the landmarks this place was placed from, so
    /// C# can raise the bar without another build. 0 on the mask-only rung,
    /// where no landmark was involved at all.
    let confidence: Double
    /// False when the place IS a landmark (an eye, the nose, a front paw).
    /// True when it was constructed from several — see `place(for:)`.
    let derived: Bool
    /// True only for `paws` on the mask-only rung: one number for both front
    /// paws, which throws away exactly the asymmetry the feature is for.
    let grouped: Bool
}

private struct Answer: Encodable {
    let ok: Bool
    let error: String?
    let imageWidth: Int, imageHeight: Int

    // Rung 0 — the same question `CatVision.swift` already answers, repeated
    // here so one call can serve the whole measurement.
    let foundAnimal: Bool
    let identifier: String
    let confidence: Double

    /// `pose_and_mask`, `mask_only`, `pose_only` or `none`. See NOTES-marks.md.
    let rung: String
    /// Everything the plugin wanted and could not have, in plain words. Read
    /// it: a short `marks` array with an empty `notes` means a healthy cat
    /// photo, and a short one with three notes means something failed.
    let notes: [String]

    let landmarks: [Landmark]
    /// Median L* over every pixel Vision called cat, 0…100; −1 when there is
    /// no mask, in which case `marks` is empty.
    let bodyLightness: Double
    /// How many pixels that median came from.
    let bodyPixels: Int
    let marks: [Mark]
}

private func encode(_ answer: Answer) -> UnsafeMutablePointer<CChar>? {
    guard let data = try? JSONEncoder().encode(answer),
          let text = String(data: data, encoding: .utf8) else { return strdup("{\"ok\":false}") }
    return strdup(text)
}

private func fail(_ message: String) -> UnsafeMutablePointer<CChar>? {
    encode(Answer(ok: false, error: message, imageWidth: 0, imageHeight: 0,
                  foundAnimal: false, identifier: "", confidence: 0,
                  rung: "none", notes: [], landmarks: [],
                  bodyLightness: -1, bodyPixels: 0, marks: []))
}

/// A short, stable code for a Vision error — the framework's own domain and
/// numeric code, e.g. "com.apple.Vision/9" for VNErrorInvalidFormat. Never
/// error.localizedDescription: that string follows the DEVICE's system
/// language, not the game's, and `notes` crosses to C# same as `error` does —
/// see CatPicker.swift:20-29, where that rule was bought the hard way. This
/// file's own header already bans naming a pixel, a path or a size here; a
/// localised OS sentence is the same violation. Duplicated in CatVision.swift
/// rather than shared: `private` is file scope, and the two files already
/// duplicate `Detection` for the same reason (see below).
private func code(_ error: Error) -> String {
    let ns = error as NSError
    return "\(ns.domain)/\(ns.code)"
}

// MARK: - Lightness

/// CIE L* from 8-bit sRGB, 0…100. Not the raw green channel and not the mean
/// of the three: L* is perceptually uniform, so "10 points lighter" means
/// about the same visible step on a black cat as on a cream one, and the
/// threshold C# tunes is one number rather than one per coat colour.
private func lightness(r: UInt8, g: UInt8, b: UInt8) -> Double {
    func linear(_ c: UInt8) -> Double {
        let v = Double(c) / 255.0
        return v <= 0.04045 ? v / 12.92 : pow((v + 0.055) / 1.055, 2.4)
    }
    // Relative luminance, Rec. 709 primaries — the same weights sRGB is
    // defined with.
    let y = 0.2126 * linear(r) + 0.7152 * linear(g) + 0.0722 * linear(b)
    let e = 216.0 / 24389.0
    return y > e ? 116.0 * pow(y, 1.0 / 3.0) - 16.0
                 : y * (24389.0 / 27.0)
}

/// L* medians come off a 256-bin histogram rather than a sorted array: a
/// 512x512 cat is a quarter of a million samples, the bins are 0.39 L* apart,
/// and that is finer than the difference this measurement is looking for.
private struct Histogram {
    private var bins = [Int](repeating: 0, count: 256)
    private(set) var count = 0

    mutating func add(_ value: Double) {
        let index = min(255, max(0, Int((value / 100.0 * 255.0).rounded())))
        bins[index] += 1
        count += 1
    }

    /// The median, or nil when nothing was added.
    var median: Double? {
        guard count > 0 else { return nil }
        let half = count / 2
        var seen = 0
        for (index, n) in bins.enumerated() {
            seen += n
            if seen > half { return Double(index) / 255.0 * 100.0 }
        }
        return nil
    }
}

/// The image as tightly packed RGBA8, so a pixel is four bytes at a known
/// offset. `CGImage` gives no promise about its own layout — bit depth, byte
/// order and alpha position all vary with how the JPEG was encoded — so it is
/// redrawn once into a buffer this file controls.
private func rgbaBytes(_ image: CGImage) -> (bytes: [UInt8], width: Int, height: Int)? {
    let width = image.width, height = image.height
    guard width > 0, height > 0 else { return nil }
    var bytes = [UInt8](repeating: 0, count: width * height * 4)
    let ok: Bool = bytes.withUnsafeMutableBytes { raw -> Bool in
        guard let base = raw.baseAddress,
              let context = CGContext(
                data: base, width: width, height: height,
                bitsPerComponent: 8, bytesPerRow: width * 4,
                space: CGColorSpaceCreateDeviceRGB(),
                bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)
        else { return false }
        context.draw(image, in: CGRect(x: 0, y: 0, width: width, height: height))
        return true
    }
    return ok ? (bytes, width, height) : nil
}

/// Redraw upright so that Vision's coordinate space and the mask's pixel grid
/// are the same grid.
///
/// This is not tidiness. Vision reports normalised points against the ORIENTED
/// image, while `generateScaledMaskForImageForInstances` returns a buffer the
/// size of the image as handed in. On a photo taken sideways those two are
/// transposed, and sampling one with the other silently reads the wrong part
/// of the cat — the same class of bug the bottom-left/top-left flip in
/// `CatVision.swift` was written to keep out of every caller. Baking the
/// orientation once means `.up` is the only case that ever runs below.
///
/// In practice the pipeline hands this an already-upright 512x512 crop
/// (`CatPhoto.swift` crops the oriented image), so this normally returns the
/// image untouched.
private func upright(_ image: CGImage, _ orientation: CGImagePropertyOrientation) -> CGImage? {
    if orientation == .up { return image }
    let w = CGFloat(image.width), h = CGFloat(image.height)
    let sideways = [.left, .right, .leftMirrored, .rightMirrored].contains(orientation)
    let outW = Int(sideways ? h : w), outH = Int(sideways ? w : h)

    guard let context = CGContext(
        data: nil, width: outW, height: outH,
        bitsPerComponent: 8, bytesPerRow: 0,
        space: CGColorSpaceCreateDeviceRGB(),
        bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue) else { return nil }

    // CGContext draws with the origin bottom-left; each case below is the
    // transform that puts the oriented image the right way up in it.
    var transform = CGAffineTransform.identity
    switch orientation {
    case .down, .downMirrored:
        transform = transform.translatedBy(x: w, y: h).rotated(by: .pi)
    case .left, .leftMirrored:
        transform = transform.translatedBy(x: h, y: 0).rotated(by: .pi / 2)
    case .right, .rightMirrored:
        transform = transform.translatedBy(x: 0, y: w).rotated(by: -.pi / 2)
    default:
        break
    }
    switch orientation {
    case .upMirrored, .downMirrored:
        transform = transform.translatedBy(x: w, y: 0).scaledBy(x: -1, y: 1)
    case .leftMirrored, .rightMirrored:
        transform = transform.translatedBy(x: h, y: 0).scaledBy(x: -1, y: 1)
    default:
        break
    }

    context.concatenate(transform)
    context.draw(image, in: CGRect(x: 0, y: 0, width: w, height: h))
    return context.makeImage()
}

// MARK: - Where a place is

/// A joint, already converted to pixels with the origin top-left.
private struct Joint {
    let point: CGPoint
    let confidence: Double
}

/// All 25 joints `VNAnimalBodyPoseObservation` knows, with the name each one
/// crosses to C# under. Apple's five groups are: head (9), trunk (1 — the
/// neck, and nothing else), forelegs (6), hindlegs (6), tail (3).
///
/// **Written out here because the list is the argument.** There is no chin
/// joint, no forehead joint, no chest joint and no flank joint, and four of
/// the game's ten `spot_place` values are exactly those four. See `recipes`
/// for what is done about it.
private let allJoints: [(VNAnimalBodyPoseObservation.JointName, String)] = [
    (.leftEarTop, "leftEarTop"), (.rightEarTop, "rightEarTop"),
    (.leftEarMiddle, "leftEarMiddle"), (.rightEarMiddle, "rightEarMiddle"),
    (.leftEarBottom, "leftEarBottom"), (.rightEarBottom, "rightEarBottom"),
    (.leftEye, "leftEye"), (.rightEye, "rightEye"), (.nose, "nose"),
    (.neck, "neck"),
    (.leftFrontElbow, "leftFrontElbow"), (.rightFrontElbow, "rightFrontElbow"),
    (.leftFrontKnee, "leftFrontKnee"), (.rightFrontKnee, "rightFrontKnee"),
    (.leftFrontPaw, "leftFrontPaw"), (.rightFrontPaw, "rightFrontPaw"),
    (.leftBackElbow, "leftBackElbow"), (.rightBackElbow, "rightBackElbow"),
    (.leftBackKnee, "leftBackKnee"), (.rightBackKnee, "rightBackKnee"),
    (.leftBackPaw, "leftBackPaw"), (.rightBackPaw, "rightBackPaw"),
    (.tailTop, "tailTop"), (.tailMiddle, "tailMiddle"), (.tailBottom, "tailBottom"),
]

/// A recipe for one of the ten `spot_place` values.
///
/// **Only four of the ten are landmarks.** `VNAnimalBodyPoseObservation` has 25
/// joints and none of them is a chin, a forehead, a chest or a flank — the
/// trunk group is the single joint `neck`. Those four are constructed from the
/// joints that do exist, and every constructed place is flagged `derived` in
/// the JSON so C# can weigh it lower than a measured one if the reference set
/// says it should.
private struct Recipe {
    let place: String
    let derived: Bool
    /// The joints it needs. A place is skipped outright when any of them is
    /// missing or below the confidence floor — a wrong mark is worse than a
    /// missing one, and a chest placed off a guessed neck lands on the carpet.
    let needs: [VNAnimalBodyPoseObservation.JointName]
    /// Given those joints in the order of `needs`, where to sample.
    let locate: ([CGPoint]) -> CGPoint
}

private func midpoint(_ a: CGPoint, _ b: CGPoint) -> CGPoint {
    CGPoint(x: (a.x + b.x) / 2, y: (a.y + b.y) / 2)
}

/// `from` moved `t` of the way towards `to`, and past it when `t` > 1.
private func lerp(_ from: CGPoint, _ to: CGPoint, _ t: CGFloat) -> CGPoint {
    CGPoint(x: from.x + (to.x - from.x) * t, y: from.y + (to.y - from.y) * t)
}

/// The ten places, in the order `CatTraits.Allowed["spot_place"]` lists them.
///
/// The four constructions, and the reasoning behind each fraction — none of
/// these has been checked against a photograph, which is the largest untested
/// thing in this file:
///
/// - **forehead** — the eye midpoint moved half way to where the ears meet the
///   skull. Between the eyes and the ear bases is the forehead by definition,
///   and both ends are real joints.
/// - **chin** — the nose carried on past the eyes' midpoint by 0.55 of the
///   eye-to-nose distance. The muzzle axis runs eyes → nose, and the chin is
///   the same distance again beyond the nose. Weakest of the four: a cat
///   looking up hides her chin entirely and this still returns a number, from
///   whatever is behind it inside the mask.
/// - **chest** — the neck moved a third of the way to the midpoint of the
///   front elbows. Below the throat, above the legs.
/// - **flank** — the middle of the neck-to-tail-base line, pushed a third of
///   the way towards the front elbows, i.e. down off the spine onto the side
///   of the body.
private let recipes: [Recipe] = [
    Recipe(place: "muzzle", derived: false, needs: [.nose]) { $0[0] },

    Recipe(place: "forehead", derived: true,
           needs: [.leftEye, .rightEye, .leftEarBottom, .rightEarBottom]) {
        lerp(midpoint($0[0], $0[1]), midpoint($0[2], $0[3]), 0.5)
    },

    Recipe(place: "eye_left", derived: false, needs: [.leftEye]) { $0[0] },
    Recipe(place: "eye_right", derived: false, needs: [.rightEye]) { $0[0] },

    Recipe(place: "chin", derived: true, needs: [.nose, .leftEye, .rightEye]) {
        lerp(midpoint($0[1], $0[2]), $0[0], 1.55)
    },

    Recipe(place: "chest", derived: true,
           needs: [.neck, .leftFrontElbow, .rightFrontElbow]) {
        lerp($0[0], midpoint($0[1], $0[2]), 0.33)
    },

    Recipe(place: "paw_left", derived: false, needs: [.leftFrontPaw]) { $0[0] },
    Recipe(place: "paw_right", derived: false, needs: [.rightFrontPaw]) { $0[0] },

    Recipe(place: "flank", derived: true,
           needs: [.neck, .tailBottom, .leftFrontElbow, .rightFrontElbow]) {
        lerp(midpoint($0[0], $0[1]), midpoint($0[2], $0[3]), 0.33)
    },

    // tail_tip is not in this table: which of the three tail joints is the tip
    // is decided by geometry, not by its name. See tailTip(from:).
]

/// The tip of the tail is **the tail joint farthest from the neck**, not the
/// one called `tailTop`.
///
/// The names do not say which end is which — Apple documents `tailTop` as "the
/// top of the tail" and `tailBottom` as "the bottom", which settles nothing.
/// Apple's own WWDC23 sample `DetectingAnimalBodyPosesWithVision` draws the
/// skeleton neck → `tailBottom` → hind elbows and `tailBottom` → `tailMiddle`
/// → `tailTop`, so `tailBottom` is the rump and `tailTop` is the tip — the
/// reverse of the plain reading of the words, and exactly the kind of thing to
/// get backwards from the name alone.
///
/// So this measures instead of trusting either reading: whichever tail joint
/// is farthest from the neck is the tip. It agrees with the sample code and
/// costs nothing, and it stays right if a future revision renumbers the tail.
private func tailTip(from joints: [VNAnimalBodyPoseObservation.JointName: Joint],
                     floor: Double) -> (Joint, VNAnimalBodyPoseObservation.JointName)? {
    guard let neck = joints[.neck], neck.confidence >= floor else { return nil }
    let candidates: [VNAnimalBodyPoseObservation.JointName] = [.tailTop, .tailMiddle, .tailBottom]
    var best: (Joint, VNAnimalBodyPoseObservation.JointName)?
    var bestDistance: CGFloat = -1
    for name in candidates {
        guard let joint = joints[name], joint.confidence >= floor else { continue }
        let dx = joint.point.x - neck.point.x, dy = joint.point.y - neck.point.y
        let distance = dx * dx + dy * dy
        if distance > bestDistance {
            bestDistance = distance
            best = (joint, name)
        }
    }
    return best
}

/// Of the poses found, the one belonging to the cat.
///
/// Scored by the share of its confident joints that land inside the
/// recognise-animals box, in Vision's own normalised bottom-left space. A cat
/// and the dog beside her both get a pose and neither says which is which;
/// this is the only link between the two requests that exists to be made.
private func chooseBody(_ bodies: [VNAnimalBodyPoseObservation],
                        animalBox: CGRect?) -> VNAnimalBodyPoseObservation? {
    guard let box = animalBox, bodies.count > 1 else {
        return bodies.max { $0.confidence < $1.confidence }
    }
    return bodies.max { a, b in share(of: a, inside: box) < share(of: b, inside: box) }
}

private func share(of body: VNAnimalBodyPoseObservation, inside box: CGRect) -> Double {
    guard let points = try? body.recognizedPoints(.all), !points.isEmpty else { return 0 }
    var inside = 0, total = 0
    for (_, point) in points where point.confidence > 0 {
        total += 1
        if box.contains(point.location) { inside += 1 }
    }
    return total == 0 ? 0 : Double(inside) / Double(total)
}

// MARK: - Which pixels are the cat

/// A cat mask at the resolution of the photograph: `on[y * width + x]`.
private struct Mask {
    let on: [Bool]
    let width: Int, height: Int
    /// The tightest box around the "on" pixels, in image pixels, top-left
    /// origin. Empty when nothing is on.
    let bounds: CGRect
    let count: Int
}

/// Read a one-component mask buffer, whatever depth Vision handed back, as
/// values scaled to 0…1. Returns nil for a format this does not understand
/// rather than reading the bytes as if it did.
private func readMask(_ buffer: CVPixelBuffer) -> (values: [Float], width: Int, height: Int)? {
    let format = CVPixelBufferGetPixelFormatType(buffer)
    guard format == kCVPixelFormatType_OneComponent32Float
            || format == kCVPixelFormatType_OneComponent8 else { return nil }

    CVPixelBufferLockBaseAddress(buffer, .readOnly)
    defer { CVPixelBufferUnlockBaseAddress(buffer, .readOnly) }
    guard let base = CVPixelBufferGetBaseAddress(buffer) else { return nil }

    let width = CVPixelBufferGetWidth(buffer)
    let height = CVPixelBufferGetHeight(buffer)
    let stride = CVPixelBufferGetBytesPerRow(buffer)
    var values = [Float](repeating: 0, count: width * height)

    for y in 0..<height {
        let row = base.advanced(by: y * stride)
        if format == kCVPixelFormatType_OneComponent32Float {
            let floats = row.bindMemory(to: Float.self, capacity: width)
            for x in 0..<width { values[y * width + x] = floats[x] }
        } else {
            let bytes = row.bindMemory(to: UInt8.self, capacity: width)
            for x in 0..<width { values[y * width + x] = Float(bytes[x]) }
        }
    }
    return (values, width, height)
}

/// Pick the foreground instance that is the cat.
///
/// `VNGenerateForegroundInstanceMaskRequest` segments EVERY salient object it
/// finds and numbers them from 1 — the cat, and also the arm holding her and
/// the cushion she is on. It has no idea which one is an animal, and there is
/// no way to ask it for one: `regionOfInterest` exists on the request (it
/// inherits `VNImageBasedRequest`) but it crops what is analysed rather than
/// naming a subject, and a box tight to the cat would cut off whatever of her
/// falls outside it.
///
/// So the choice is made here, from the low-resolution `instanceMask` — whose
/// pixel values ARE the instance numbers — by how much of each instance falls
/// inside the box `VNRecognizeAnimalsRequest` drew around the cat. Largest
/// overlap wins; with no box, largest instance wins.
private func chooseInstance(_ observation: VNInstanceMaskObservation,
                            animalBox: CGRect?) -> Int? {
    guard let mask = readMask(observation.instanceMask) else { return nil }
    var inside = [Int: Int](), total = [Int: Int]()

    for y in 0..<mask.height {
        for x in 0..<mask.width {
            let index = Int(mask.values[y * mask.width + x].rounded())
            guard index > 0 else { continue }
            total[index, default: 0] += 1
            if let box = animalBox {
                // instanceMask is aligned to the oriented image, which — see
                // upright(_:_:) — is the image. Normalised, bottom-left, to
                // compare against Vision's own box.
                let nx = (Double(x) + 0.5) / Double(mask.width)
                let ny = 1.0 - (Double(y) + 0.5) / Double(mask.height)
                if box.contains(CGPoint(x: nx, y: ny)) { inside[index, default: 0] += 1 }
            }
        }
    }
    guard !total.isEmpty else { return nil }

    if animalBox != nil, !inside.isEmpty {
        return inside.max { a, b in
            // Fraction of the instance inside the box, not raw overlap: a
            // large cushion crossing the box would otherwise beat a small cat
            // wholly inside it.
            let fa = Double(a.value) / Double(total[a.key] ?? 1)
            let fb = Double(b.value) / Double(total[b.key] ?? 1)
            return fa < fb
        }?.key
    }
    return total.max { $0.value < $1.value }?.key
}

/// Turn a full-resolution float mask into booleans plus its bounding box.
/// 0.5 because the buffer is a soft alpha at the edges of the fur and this
/// wants the pixels that are more cat than not.
private func binarise(_ values: [Float], width: Int, height: Int) -> Mask {
    var on = [Bool](repeating: false, count: values.count)
    var count = 0
    var minX = width, minY = height, maxX = -1, maxY = -1
    for y in 0..<height {
        for x in 0..<width {
            let i = y * width + x
            guard values[i] >= 0.5 else { continue }
            on[i] = true
            count += 1
            if x < minX { minX = x }; if x > maxX { maxX = x }
            if y < minY { minY = y }; if y > maxY { maxY = y }
        }
    }
    let bounds = maxX < 0 ? .zero
        : CGRect(x: minX, y: minY, width: maxX - minX + 1, height: maxY - minY + 1)
    return Mask(on: on, width: width, height: height, bounds: bounds, count: count)
}

/// Median L* of the cat pixels inside a disc, and how many there were.
///
/// Only pixels inside the mask count. A paw against a pale floor would
/// otherwise read as a white sock because half the disc is floorboard, and
/// that is the single most likely way this measurement could lie.
private func sample(centre: CGPoint, radius: Double,
                    mask: Mask, rgba: [UInt8]) -> (median: Double, samples: Int)? {
    var histogram = Histogram()
    let r = Int(radius.rounded())
    let cx = Int(centre.x.rounded()), cy = Int(centre.y.rounded())
    let r2 = radius * radius

    // Clamped and then checked, not clamped and hoped: a derived place can
    // land outside the image — the chin of a cat photographed from below is
    // computed below the bottom edge — and an empty range is a crash, not a
    // zero. The caller reads "no samples" as "say nothing about this place".
    let y0 = max(0, cy - r), y1 = min(mask.height - 1, cy + r)
    let x0 = max(0, cx - r), x1 = min(mask.width - 1, cx + r)
    guard y0 <= y1, x0 <= x1 else { return nil }

    for y in y0...y1 {
        for x in x0...x1 {
            let dx = Double(x - cx), dy = Double(y - cy)
            guard dx * dx + dy * dy <= r2 else { continue }
            let i = y * mask.width + x
            guard mask.on[i] else { continue }
            let p = i * 4
            histogram.add(lightness(r: rgba[p], g: rgba[p + 1], b: rgba[p + 2]))
        }
    }
    guard let median = histogram.median else { return nil }
    return (median, histogram.count)
}

// MARK: - The entry point

/// Measure a cat's distinctive marks in a JPEG/PNG held in memory.
///
/// - Parameters:
///   - orientationRaw: a `CGImagePropertyOrientation` raw value, exactly as
///     `CatVision_recognise` takes it. 0 means "read it from the file's own
///     metadata". Vision keeps no orientation of its own and mis-detects
///     silently when it is wrong.
///   - minLandmarkConfidence: the floor a joint must clear before any place
///     resting on it is reported. A place whose joints are shaky is left out
///     entirely — a wrong mark is worse than a missing one. Passed in rather
///     than fixed here so it can be tuned against the reference set without
///     another native build; 0.5 is the suggested starting value and is not a
///     measurement.
///
/// Returns a malloc'd JSON C string the caller frees with `CatMarks_free`.
/// Never returns null for a bad photograph — only `ok:false` with a reason,
/// for the same reason `VisionAnswer.Failed` exists: "found nothing" and
/// "could not look" are different things to tell a player.
@_cdecl("CatMarks_measure")
public func CatMarks_measure(_ bytes: UnsafePointer<UInt8>?,
                             _ length: Int32,
                             _ orientationRaw: Int32,
                             _ minLandmarkConfidence: Double) -> UnsafeMutablePointer<CChar>? {
    guard let bytes = bytes, length > 0 else { return fail("empty image data") }

    let data = Data(bytes: bytes, count: Int(length))
    guard let source = CGImageSourceCreateWithData(data as CFData, nil),
          let decoded = CGImageSourceCreateImageAtIndex(source, 0, nil) else {
        return fail("not a decodable image")
    }

    var orientation = CGImagePropertyOrientation.up
    if orientationRaw > 0 {
        orientation = CGImagePropertyOrientation(rawValue: UInt32(orientationRaw)) ?? .up
    } else if let properties = CGImageSourceCopyPropertiesAtIndex(source, 0, nil) as? [CFString: Any],
              let exif = properties[kCGImagePropertyOrientation] as? UInt32 {
        orientation = CGImagePropertyOrientation(rawValue: exif) ?? .up
    }

    guard let image = upright(decoded, orientation) else {
        return fail("could not orient the image")
    }
    guard let raster = rgbaBytes(image) else { return fail("could not read pixels") }
    let width = raster.width, height = raster.height
    let floor = min(max(minLandmarkConfidence, 0), 1)

    var notes: [String] = []

    // --- Rung 0: is it a cat, and where ------------------------------------
    // Its own handler. Three requests through one `perform` would be cheaper,
    // but Vision throws for the whole call when any one of them fails, and the
    // point of this file is that each rung survives the loss of the one above.
    var foundAnimal = false, identifier = "", confidence = 0.0
    var animalBox: CGRect?
    let animals = VNRecognizeAnimalsRequest()
    do {
        try VNImageRequestHandler(cgImage: image, orientation: .up, options: [:])
            .perform([animals])
        if let best = (animals.results ?? []).max(by: {
            ($0.labels.first?.confidence ?? 0) < ($1.labels.first?.confidence ?? 0)
        }), let label = best.labels.first {
            foundAnimal = true
            identifier = label.identifier
            confidence = Double(label.confidence)
            animalBox = best.boundingBox
        }
    } catch {
        notes.append("recognise-animals failed: \(code(error))")
    }

    // --- Rung 1: which pixels are her --------------------------------------
    // The handler has to outlive the request: generateScaledMaskForImage reads
    // back through it.
    var mask: Mask?
    let maskHandler = VNImageRequestHandler(cgImage: image, orientation: .up, options: [:])
    let masking = VNGenerateForegroundInstanceMaskRequest()
    do {
        try maskHandler.perform([masking])
        if let observation = masking.results?.first {
            var instances = observation.allInstances
            if let chosen = chooseInstance(observation, animalBox: animalBox) {
                instances = IndexSet(integer: chosen)
                if observation.allInstances.count > 1 {
                    notes.append("\(observation.allInstances.count) foreground objects; "
                        + "measured the one overlapping the animal box")
                }
            } else if !instances.isEmpty {
                notes.append("could not read the instance mask; "
                    + "measured every foreground object as one")
            }
            let buffer = try observation.generateScaledMaskForImage(
                forInstances: instances, from: maskHandler)
            if let read = readMask(buffer), read.width == width, read.height == height {
                mask = binarise(read.values, width: read.width, height: read.height)
            } else {
                notes.append("the cat mask came back in a size or format this cannot read")
            }
        } else {
            notes.append("foreground segmentation found no object to separate")
        }
    } catch {
        notes.append("foreground mask failed: \(code(error))")
    }

    // --- Rung 2: the 25 landmarks ------------------------------------------
    var joints: [VNAnimalBodyPoseObservation.JointName: Joint] = [:]
    var landmarks: [Landmark] = []
    let pose = VNDetectAnimalBodyPoseRequest()
    do {
        try VNImageRequestHandler(cgImage: image, orientation: .up, options: [:])
            .perform([pose])
        // Which pose belongs to the cat `VNRecognizeAnimalsRequest` found is
        // not a question the API answers. `VNAnimalBodyPoseObservation`
        // descends from `VNRecognizedPointsObservation`, which descends
        // straight from `VNObservation` and NOT from
        // `VNDetectedObjectObservation` — so it has no bounding box, and it
        // carries no species either. Two observations, and nothing in either
        // says which animal it is.
        //
        // So the tie is made here, out of the only thing a pose does have: the
        // joints themselves. The pose whose joints mostly fall inside the
        // animal box is the cat's. With no box, the most confident pose wins,
        // which is a guess and is noted as one.
        let bodies = pose.results ?? []
        if let body = chooseBody(bodies, animalBox: animalBox) {
            if bodies.count > 1 {
                notes.append(animalBox == nil
                    ? "\(bodies.count) animal bodies and no box to tell them apart; "
                        + "measured the most confident"
                    : "\(bodies.count) animal bodies; measured the one inside the animal box")
            }
            // Asked joint by joint from `allJoints` rather than read out of
            // `recognizedPoints(.all)`, so the names crossing to C# are the
            // ones written down here and the order is fixed. A joint the model
            // did not place simply does not appear.
            for (name, label) in allJoints {
                guard let point = try? body.recognizedPoint(name) else { continue }
                // Vision's normalised point is bottom-left; the flip happens
                // here so C# and CatVision agree on what a y means.
                let pixel = CGPoint(x: point.location.x * CGFloat(width),
                                    y: (1 - point.location.y) * CGFloat(height))
                joints[name] = Joint(point: pixel, confidence: Double(point.confidence))
                landmarks.append(Landmark(name: label,
                                          x: Int(pixel.x.rounded()),
                                          y: Int(pixel.y.rounded()),
                                          confidence: Double(point.confidence)))
            }
            if joints.isEmpty { notes.append("an animal body was found but no joint was placed") }
        } else {
            notes.append("no animal body pose found")
        }
    } catch {
        notes.append("animal body pose failed: \(code(error))")
    }

    // --- Rung 3: the measurement -------------------------------------------
    var marks: [Mark] = []
    var bodyLightness = -1.0
    var bodyPixels = 0
    var rung = "none"

    if let mask = mask, mask.count > 0 {
        var body = Histogram()
        for i in 0..<mask.on.count where mask.on[i] {
            let p = i * 4
            body.add(lightness(r: raster.bytes[p], g: raster.bytes[p + 1], b: raster.bytes[p + 2]))
        }
        guard let median = body.median else { return fail("empty cat mask") }
        bodyLightness = median
        bodyPixels = body.count

        // The disc scales with the cat, not with the image: 4.5% of her
        // shorter side, so a paw sample stays a paw whether she fills the
        // frame or sits in a corner. Floored at 2 px, above which a median of
        // fewer than `minSamples` pixels is refused rather than reported.
        let radius = min(20.0, max(2.0,
            0.045 * Double(min(mask.bounds.width, mask.bounds.height))))
        let minSamples = 8

        func record(_ place: String, _ centre: CGPoint,
                    derived: Bool, confidence: Double, grouped: Bool = false) {
            guard let taken = sample(centre: centre, radius: radius,
                                     mask: mask, rgba: raster.bytes),
                  taken.samples >= minSamples else { return }
            marks.append(Mark(place: place,
                              lightness: taken.median,
                              delta: taken.median - median,
                              samples: taken.samples,
                              confidence: confidence,
                              derived: derived,
                              grouped: grouped))
        }

        if !joints.isEmpty {
            rung = "pose_and_mask"
            for recipe in recipes {
                let wanted = recipe.needs.compactMap { joints[$0] }
                guard wanted.count == recipe.needs.count else { continue }
                let weakest = wanted.map(\.confidence).min() ?? 0
                guard weakest >= floor else { continue }
                record(recipe.place, recipe.locate(wanted.map(\.point)),
                       derived: recipe.derived, confidence: weakest)
            }
            if let (tip, name) = tailTip(from: joints, floor: floor) {
                record("tail_tip", tip.point, derived: false,
                       confidence: min(tip.confidence, joints[.neck]?.confidence ?? 0))
                if name != .tailTop {
                    notes.append("the tail joint farthest from the neck was "
                        + "\(name.rawValue), not tailTop")
                }
            }
            // recipes holds nine; tail_tip is the tenth and is placed by
            // geometry rather than by a recipe, so the total is counted rather
            // than written down.
            let places = recipes.count + 1
            if marks.count < places {
                notes.append("\(places - marks.count) of \(places) places were "
                    + "skipped: a joint they need was missing, below the confidence "
                    + "floor, or landed off the cat")
            }
        } else {
            // Mask but no pose. Three bands down the silhouette, and they are
            // worth much less than the eleven above: nothing here knows which
            // way the cat is facing.
            rung = "mask_only"
            notes.append("no pose, so the places are bands down the silhouette. "
                + "This assumes a cat upright in frame and is wrong for one lying "
                + "on her side; `paws` is both front paws together, which is the "
                + "asymmetry the marks exist to catch, thrown away.")
            let b = mask.bounds
            record("chest", CGPoint(x: b.midX, y: b.minY + b.height * 0.45),
                   derived: true, confidence: 0)
            record("flank", CGPoint(x: b.midX, y: b.minY + b.height * 0.62),
                   derived: true, confidence: 0)
            record("paws", CGPoint(x: b.midX, y: b.minY + b.height * 0.90),
                   derived: true, confidence: 0, grouped: true)
        }
    } else if !joints.isEmpty {
        // Landmarks but no mask. No marks at all, on purpose: without a mask
        // there is no telling her coat from the sofa, so a "body median" would
        // be a median of the room and every delta measured against it would be
        // a number about the furniture.
        rung = "pose_only"
        notes.append("landmarks but no cat mask, so no lightness was measured: "
            + "with nothing to say which pixels are the cat, her median would be "
            + "the median of the room")
    } else {
        notes.append("neither a cat mask nor a pose, so nothing was measured")
    }

    return encode(Answer(ok: true, error: nil,
                         imageWidth: width, imageHeight: height,
                         foundAnimal: foundAnimal,
                         identifier: identifier,
                         confidence: confidence,
                         rung: rung,
                         notes: notes,
                         landmarks: landmarks,
                         bodyLightness: bodyLightness,
                         bodyPixels: bodyPixels,
                         marks: marks))
}

/// Free a string returned by this plugin. Swift allocated it with strdup, so
/// the C# side cannot let the marshaller reclaim it.
@_cdecl("CatMarks_free")
public func CatMarks_free(_ pointer: UnsafeMutablePointer<CChar>?) {
    free(pointer)
}

// MARK: - The mask, as pixels

// Task 60-coat/01: the same mask this file already builds, handed to C# as
// bytes instead of measured here.
//
// **Why the mask crosses the boundary rather than the answer.** Everything
// above returns numbers because a threshold has to be tunable without a device
// build. The coat reader needs the same freedom and more of it: base colour,
// banding and edge roughness are three statistics with three thresholds, and
// Android reaches them from a mask ML Kit hands over as bytes. Writing them
// here would mean a Swift copy and a Java copy of arithmetic with nothing able
// to compare the two — the complaint `Shell/CatColour.cs` already makes about
// the six palette anchors, tripled. So iOS hands over what Android already
// hands over, `Core/CoatReader.cs` is the only implementation, and
// `dotnet test` can run it on a fixture.
//
// The packet is Android's, byte for byte, from
// `Plugins/Android/.../Packet.java`, so `Shell/CatVision.Unpack` reads either
// platform without knowing which produced it:
//
//     "CVS1" | int32 big-endian json length | json UTF-8 | mask bytes
//
// Nothing is logged and nothing is written down, the same as the rest of this
// file: the mask is a picture of a player's cat.

private struct SilhouetteAnswer: Encodable {
    let ok: Bool
    let error: String?
    let imageWidth: Int, imageHeight: Int
    let detections: [Detection]
    let maskWidth: Int, maskHeight: Int
    let maskCoverage: Double
    let maskSource: String
    let rung: String
}

/// The shape `CatVision.swift` reports a box in. Repeated here rather than
/// shared because that one is `private` to its own file and this packet has to
/// encode the identical field names for `VisionAnswer` to deserialise it.
private struct Detection: Encodable {
    let identifier: String
    let confidence: Double
    let x: Int, y: Int, width: Int, height: Int
}

/// Pack a JSON string and a mask into one buffer the caller frees with
/// `CatVision_freeBuffer`.
private func packet(_ json: String, _ mask: [UInt8],
                    _ outLength: UnsafeMutablePointer<Int32>) -> UnsafeMutableRawPointer? {
    let body = Array(json.utf8)
    let total = 8 + body.count + mask.count
    guard let buffer = malloc(total)?.assumingMemoryBound(to: UInt8.self) else { return nil }
    buffer[0] = UInt8(ascii: "C"); buffer[1] = UInt8(ascii: "V")
    buffer[2] = UInt8(ascii: "S"); buffer[3] = UInt8(ascii: "1")
    let n = body.count
    buffer[4] = UInt8((n >> 24) & 0xFF); buffer[5] = UInt8((n >> 16) & 0xFF)
    buffer[6] = UInt8((n >> 8) & 0xFF);  buffer[7] = UInt8(n & 0xFF)
    body.withUnsafeBufferPointer { _ = memcpy(buffer + 8, $0.baseAddress!, n) }
    if !mask.isEmpty {
        mask.withUnsafeBufferPointer { _ = memcpy(buffer + 8 + n, $0.baseAddress!, mask.count) }
    }
    outLength.pointee = Int32(total)
    return UnsafeMutableRawPointer(buffer)
}

/// Nearest-neighbour downscale of a full-resolution boolean mask onto a grid
/// whose long side is `side`. The confidence byte is 255 or 0 rather than a
/// soft alpha, because `binarise` has already made that decision at 0.5 — see
/// `CoatReader.ReadFur`, which is the one measurement that would rather have
/// had the soft band and says so.
private func downscale(_ mask: Mask, side: Int) -> (bytes: [UInt8], width: Int, height: Int) {
    let longest = max(mask.width, mask.height)
    let scale = longest > side ? Double(side) / Double(longest) : 1.0
    let outW = max(1, Int((Double(mask.width) * scale).rounded()))
    let outH = max(1, Int((Double(mask.height) * scale).rounded()))
    var bytes = [UInt8](repeating: 0, count: outW * outH)
    for y in 0..<outH {
        let sy = min(mask.height - 1, y * mask.height / outH)
        for x in 0..<outW {
            let sx = min(mask.width - 1, x * mask.width / outW)
            bytes[y * outW + x] = mask.on[sy * mask.width + sx] ? 255 : 0
        }
    }
    return (bytes, outW, outH)
}

/// Recognise, and cut out, the animal in a JPEG/PNG held in memory.
///
/// The iOS half of `Shell/CatVision.Silhouette`. Returns a malloc'd buffer the
/// caller frees with `CatVision_freeBuffer`, and writes its length through
/// `outLength`. Never null for a bad photograph: a packet whose JSON says
/// `ok:false` and whose mask is empty is a different thing to tell a player
/// than nothing at all.
@_cdecl("CatVision_silhouette")
public func CatVision_silhouette(_ bytes: UnsafePointer<UInt8>?,
                                 _ length: Int32,
                                 _ orientationRaw: Int32,
                                 _ maskSide: Int32,
                                 _ outLength: UnsafeMutablePointer<Int32>) -> UnsafeMutableRawPointer? {
    func give(_ answer: SilhouetteAnswer, _ mask: [UInt8]) -> UnsafeMutableRawPointer? {
        guard let data = try? JSONEncoder().encode(answer),
              let text = String(data: data, encoding: .utf8) else {
            return packet("{\"ok\":false}", [], outLength)
        }
        return packet(text, mask, outLength)
    }
    func refuse(_ message: String) -> UnsafeMutableRawPointer? {
        give(SilhouetteAnswer(ok: false, error: message, imageWidth: 0, imageHeight: 0,
                              detections: [], maskWidth: 0, maskHeight: 0,
                              maskCoverage: 0, maskSource: "none", rung: "none"), [])
    }

    outLength.pointee = 0
    guard let bytes = bytes, length > 0 else { return refuse("empty image data") }

    let data = Data(bytes: bytes, count: Int(length))
    guard let source = CGImageSourceCreateWithData(data as CFData, nil),
          let decoded = CGImageSourceCreateImageAtIndex(source, 0, nil) else {
        return refuse("not a decodable image")
    }

    var orientation = CGImagePropertyOrientation.up
    if orientationRaw > 0 {
        orientation = CGImagePropertyOrientation(rawValue: UInt32(orientationRaw)) ?? .up
    } else if let properties = CGImageSourceCopyPropertiesAtIndex(source, 0, nil) as? [CFString: Any],
              let exif = properties[kCGImagePropertyOrientation] as? UInt32 {
        orientation = CGImagePropertyOrientation(rawValue: exif) ?? .up
    }
    guard let image = upright(decoded, orientation) else {
        return refuse("could not orient the image")
    }
    let width = image.width, height = image.height

    // Rung 0 — which animal, and where. The box is what picks the cat out of
    // the several foreground objects the segmenter will find.
    var detections: [Detection] = []
    var animalBox: CGRect?
    let animals = VNRecognizeAnimalsRequest()
    if (try? VNImageRequestHandler(cgImage: image, orientation: .up, options: [:])
        .perform([animals])) != nil {
        for observation in animals.results ?? [] {
            guard let label = observation.labels.first else { continue }
            let box = VNImageRectForNormalizedRect(observation.boundingBox, width, height)
            detections.append(Detection(
                identifier: label.identifier,
                confidence: Double(label.confidence),
                x: Int(box.origin.x.rounded()),
                y: Int((CGFloat(height) - box.origin.y - box.height).rounded()),
                width: Int(box.width.rounded()),
                height: Int(box.height.rounded())))
        }
        detections.sort { $0.confidence > $1.confidence }
        if let best = (animals.results ?? []).max(by: {
            ($0.labels.first?.confidence ?? 0) < ($1.labels.first?.confidence ?? 0)
        }) {
            animalBox = best.boundingBox
        }
    }

    // Rung 1 — which pixels are her.
    var mask: Mask?
    var source_ = "none"
    if maskSide > 0 {
        let maskHandler = VNImageRequestHandler(cgImage: image, orientation: .up, options: [:])
        let masking = VNGenerateForegroundInstanceMaskRequest()
        if (try? maskHandler.perform([masking])) != nil,
           let observation = masking.results?.first {
            var instances = observation.allInstances
            if let chosen = chooseInstance(observation, animalBox: animalBox) {
                instances = IndexSet(integer: chosen)
                source_ = animalBox == nil ? "subject-unlabelled" : "subject"
            } else if !instances.isEmpty {
                source_ = "subject-unlabelled"
            }
            if let buffer = try? observation.generateScaledMaskForImage(
                    forInstances: instances, from: maskHandler),
               let read = readMask(buffer), read.width == width, read.height == height {
                mask = binarise(read.values, width: read.width, height: read.height)
            } else {
                source_ = "none"
            }
        }
    }

    guard let found = mask, found.count > 0 else {
        return give(SilhouetteAnswer(
            ok: true, error: nil, imageWidth: width, imageHeight: height,
            detections: detections, maskWidth: 0, maskHeight: 0, maskCoverage: 0,
            maskSource: "none", rung: detections.isEmpty ? "none" : "label"), [])
    }

    let scaled = downscale(found, side: Int(maskSide))
    return give(SilhouetteAnswer(
        ok: true, error: nil, imageWidth: width, imageHeight: height,
        detections: detections,
        maskWidth: scaled.width, maskHeight: scaled.height,
        maskCoverage: Double(found.count) / Double(max(1, found.width * found.height)),
        maskSource: source_, rung: "subject+label"), scaled.bytes)
}

/// Free a buffer returned by `CatVision_silhouette`. malloc'd here, so the
/// marshaller must not reclaim it.
@_cdecl("CatVision_freeBuffer")
public func CatVision_freeBuffer(_ pointer: UnsafeMutableRawPointer?) {
    free(pointer)
}
