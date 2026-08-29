// Can the ten places be found from the SILHOUETTE alone, with no skeleton?
//
// This decides how much of the feature Android can have. Apple gives 25 animal
// joints; Android gives a subject mask and nothing else — ML Kit has no animal
// pose at all, and a custom keypoint model is a separate project with its own
// megabytes. So the question is not "can Android do this" but "how much does it
// lose", and that is measurable today.
//
// The trick is that we already solved this problem once. `View/CoatMasks.cs`
// finds the same ten places on OUR OWN drawings, which have no skeleton either:
// the head is the top of the figure (her ears are the highest thing on her in
// every pose), the paws are the bottom band split left and right, the tail is
// the point farthest from the head. A segmentation mask of a real cat is the
// same kind of input — a binary blob — so the same geometry applies.
//
// This program runs BOTH ways over the reference photographs: Apple's joints,
// and our own geometry over Apple's mask. Then it prints how far apart they
// land, in fractions of the cat's own size. That number is what Android would
// lose, and it is the number the decision should rest on.
//
//   xcrun swiftc -O tools/marks-probe/geometry-probe.swift -o /tmp/geometry-probe
//   /tmp/geometry-probe fixtures/reference-photos

import Foundation
import Vision
import CoreGraphics
import ImageIO

struct Point { var x: Double; var y: Double }

/// Where the cat's head is, found from the shape of the silhouette instead of
/// from "the top of the picture".
///
/// The top-band rule is transcribed from `CoatMasks`, where it is correct: our
/// three drawings are all of an upright kitten, so her ears really are the
/// highest thing in the frame. A photograph is not so obliging — a cat lying on
/// her side has her head at one END, not at the top — and the first measurement
/// showed the cost: the muzzle landed 18.6% of her own size away from where
/// Vision put her nose.
///
/// A cat is a long blob with a blunt end and a thin one. So: the principal axis
/// of the mask, then the width profile along it. The tail end stays thin for a
/// long run; the head end is wide. Whichever end is thinner over its last fifth
/// is the tail, and the head is the other one. No model, no data, no megabytes
/// — arithmetic over the same mask Android already gets.
func headEndFromShape(_ mask: [Bool], _ w: Int, _ h: Int) -> (head: Point, tail: Point)? {
    var n = 0.0, sx = 0.0, sy = 0.0
    for y in 0..<h { for x in 0..<w where mask[y * w + x] {
        n += 1; sx += Double(x); sy += Double(y) } }
    guard n > 20 else { return nil }
    let cx = sx / n, cy = sy / n

    // Principal axis by the covariance of the filled pixels.
    var xx = 0.0, yy = 0.0, xy = 0.0
    for y in 0..<h { for x in 0..<w where mask[y * w + x] {
        let dx = Double(x) - cx, dy = Double(y) - cy
        xx += dx * dx; yy += dy * dy; xy += dx * dy } }
    xx /= n; yy /= n; xy /= n
    let theta = 0.5 * atan2(2 * xy, xx - yy)
    let ax = cos(theta), ay = sin(theta)

    // Project every pixel onto the axis, and record how far it sits off it.
    var lo = Double.greatestFiniteMagnitude, hi = -Double.greatestFiniteMagnitude
    var samples: [(t: Double, off: Double)] = []
    for y in 0..<h { for x in 0..<w where mask[y * w + x] {
        let dx = Double(x) - cx, dy = Double(y) - cy
        let t = dx * ax + dy * ay
        samples.append((t, abs(-dx * ay + dy * ax)))
        if t < lo { lo = t }
        if t > hi { hi = t } } }
    guard hi > lo else { return nil }

    // Mean thickness over the outer fifth at each end.
    let fifth = (hi - lo) * 0.2
    var loSum = 0.0, loN = 0.0, hiSum = 0.0, hiN = 0.0
    for s in samples {
        if s.t < lo + fifth { loSum += s.off; loN += 1 }
        if s.t > hi - fifth { hiSum += s.off; hiN += 1 }
    }
    guard loN > 0, hiN > 0 else { return nil }
    let loThick = loSum / loN, hiThick = hiSum / hiN

    // The blunt end is the head.
    let headT = loThick > hiThick ? lo : hi
    let tailT = loThick > hiThick ? hi : lo
    return (Point(x: cx + ax * headT, y: cy + ay * headT),
            Point(x: cx + ax * tailT, y: cy + ay * tailT))
}

/// The same rules as `CoatMasks.PlaceOf`, over a mask instead of a drawing.
/// Deliberately a transcription rather than an improvement: if it is allowed to
/// diverge, the comparison stops meaning anything.
func placesFromMask(_ mask: [Bool], _ w: Int, _ h: Int) -> [String: Point] {
    var top = h, bottom = -1, left = w, right = -1
    for y in 0..<h {
        for x in 0..<w where mask[y * w + x] {
            if y < top { top = y }
            if y > bottom { bottom = y }
            if x < left { left = x }
            if x > right { right = x }
        }
    }
    guard bottom >= 0 else { return [:] }

    // Image rows run top-down here, unlike the texture in CoatMasks where they
    // run bottom-up. So "the top of the cat" is the LOW row index, and going
    // down her body is increasing y. Everything below is written in that space
    // and the fractions are the same numbers.
    let height = Double(bottom - top), width = Double(right - left)

    // Her ears are the highest thing on her whatever the pose, so the topmost
    // band gives the head's horizontal extent — which a band across the whole
    // figure does not.
    let earsTo = top + Int(height * 0.12)
    var hx0 = w, hx1 = -1
    for y in top...max(top, earsTo) {
        for x in 0..<w where mask[y * w + x] {
            if x < hx0 { hx0 = x }
            if x > hx1 { hx1 = x }
        }
    }
    let headX = hx1 > hx0 ? Double(hx0 + hx1) * 0.5 : Double(left + right) * 0.5
    let headW = hx1 > hx0 ? Double(hx1 - hx0) : width * 0.5

    // The paws are the bottom band; its two extremes are the two sides.
    let pawsFrom = bottom - Int(height * 0.10)
    var px0 = w, px1 = -1
    for y in max(top, pawsFrom)...bottom {
        for x in 0..<w where mask[y * w + x] {
            if x < px0 { px0 = x }
            if x > px1 { px1 = x }
        }
    }

    let eyeY = Double(top) + height * 0.18

    var out: [String: Point] = [
        "forehead": Point(x: headX, y: Double(top) + height * 0.11),
        "eye_left": Point(x: headX + headW * 0.22, y: eyeY),
        "eye_right": Point(x: headX - headW * 0.22, y: eyeY),
        "muzzle": Point(x: headX, y: eyeY + height * 0.075),
        "chin": Point(x: headX, y: eyeY + height * 0.135),
        "chest": Point(x: headX, y: Double(top) + height * 0.50),
        "flank": Point(x: Double(left + right) * 0.5, y: Double(top) + height * 0.60),
    ]
    if px1 > px0 {
        let span = Double(px1 - px0)
        out["paw_left"] = Point(x: Double(px1) - span * 0.18, y: Double(bottom) - height * 0.05)
        out["paw_right"] = Point(x: Double(px0) + span * 0.18, y: Double(bottom) - height * 0.05)
    }

    // The tail: the point farthest from the head, in the lower half.
    var bestD = 0.0, best: Point? = nil
    for y in 0..<h where Double(y) > Double(top) + height * 0.35 {
        for x in 0..<w where mask[y * w + x] {
            let dy = Double(y - bottom), dx = Double(x) - headX
            let d = dy * dy + dx * dx
            if d > bestD { bestD = d; best = Point(x: Double(x), y: Double(y)) }
        }
    }
    if let tip = best, abs(tip.x - headX) > width * 0.20 { out["tail_tip"] = tip }
    return out
}

// --- driver ------------------------------------------------------------------

let arguments = CommandLine.arguments
guard arguments.count >= 2 else {
    FileHandle.standardError.write("usage: geometry-probe <folder>\n".data(using: .utf8)!)
    exit(2)
}
let folder = URL(fileURLWithPath: arguments[1])
let names = ((try? FileManager.default.contentsOfDirectory(atPath: folder.path)) ?? [])
    .filter { $0.lowercased().hasSuffix(".jpg") }.sorted()

/// Apple's joints, by the names this project's ten places rest on.
let jointFor: [String: VNAnimalBodyPoseObservation.JointName] = [
    "muzzle": .nose,
    "eye_left": .leftEye,
    "eye_right": .rightEye,
    "paw_left": .leftFrontPaw,
    "paw_right": .rightFrontPaw,
]

for name in names {
    guard let source = CGImageSourceCreateWithURL(
            folder.appendingPathComponent(name) as CFURL, nil),
          let image = CGImageSourceCreateImageAtIndex(source, 0, nil) else { continue }

    let w = image.width, h = image.height
    let handler = VNImageRequestHandler(cgImage: image, options: [:])

    // The mask.
    var mask = [Bool](repeating: false, count: w * h)
    var haveMask = false
    let maskRequest = VNGenerateForegroundInstanceMaskRequest()
    if (try? handler.perform([maskRequest])) != nil,
       let result = maskRequest.results?.first,
       let buffer = try? result.generateScaledMaskForImage(
            forInstances: result.allInstances, from: handler) {
        CVPixelBufferLockBaseAddress(buffer, .readOnly)
        if let base = CVPixelBufferGetBaseAddress(buffer) {
            let bw = CVPixelBufferGetWidth(buffer), bh = CVPixelBufferGetHeight(buffer)
            let stride = CVPixelBufferGetBytesPerRow(buffer)
            let floats = base.assumingMemoryBound(to: Float32.self)
            for y in 0..<h {
                let by = y * bh / h
                for x in 0..<w {
                    let bx = x * bw / w
                    let v = floats[(by * stride / 4) + bx]
                    if v > 0.5 { mask[y * w + x] = true; haveMask = true }
                }
            }
        }
        CVPixelBufferUnlockBaseAddress(buffer, .readOnly)
    }

    // The joints.
    var joints: [String: Point] = [:]
    let poseRequest = VNDetectAnimalBodyPoseRequest()
    if (try? handler.perform([poseRequest])) != nil,
       let pose = poseRequest.results?.first,
       let points = try? pose.recognizedPoints(.all) {
        for (place, joint) in jointFor {
            if let p = points[joint], p.confidence > 0.3 {
                // Vision's origin is bottom-left and normalised.
                joints[place] = Point(x: p.location.x * Double(w),
                                      y: (1 - p.location.y) * Double(h))
            }
        }
    }

    guard haveMask, !joints.isEmpty else {
        print("{\"file\":\"\(name)\",\"comparable\":false}")
        continue
    }

    let guessed = placesFromMask(mask, w, h)
    let shaped = headEndFromShape(mask, w, h)
    var diffs: [String] = []
    // Distance as a fraction of the cat's own diagonal, so a big photograph and
    // a small one are on the same scale.
    var mx0 = w, mx1 = -1, my0 = h, my1 = -1
    for y in 0..<h { for x in 0..<w where mask[y * w + x] {
        if x < mx0 { mx0 = x }; if x > mx1 { mx1 = x }
        if y < my0 { my0 = y }; if y > my1 { my1 = y } } }
    let size = (Double((mx1 - mx0) * (mx1 - mx0) + (my1 - my0) * (my1 - my0))).squareRoot()

    for (place, truth) in joints {
        guard let mine = guessed[place], size > 1 else { continue }
        let d = ((mine.x - truth.x) * (mine.x - truth.x)
                 + (mine.y - truth.y) * (mine.y - truth.y)).squareRoot() / size
        diffs.append("\"\(place)\":\(String(format: "%.3f", d))")
    }

    // The shape-based head, against the same nose. Reported beside the
    // top-band answer rather than instead of it: the comparison is the point.
    if let truth = joints["muzzle"], let head = shaped?.head, size > 1 {
        let d = ((head.x - truth.x) * (head.x - truth.x)
                 + (head.y - truth.y) * (head.y - truth.y)).squareRoot() / size
        diffs.append("\"muzzle_by_shape\":\(String(format: "%.3f", d))")
    }
    print("{\"file\":\"\(name)\",\"comparable\":true,\"off\":{\(diffs.joined(separator: ","))}}")
}
