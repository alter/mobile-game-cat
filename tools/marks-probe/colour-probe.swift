// Why the coat colour comes out wrong, in numbers.
//
// The owner put three photographs of his own cats through the game on
// 2026-08-29 and got back: a grey tabby rendered as a black shape with no
// features, a brown-and-cream exotic rendered ginger, and a white long-haired
// cat rendered grey. Three for three.
//
// `Shell/CatColour.EstimateHere` takes the ARITHMETIC MEAN of the central half
// of the crop — background included — and picks the nearest palette entry by
// squared RGB distance. Three faults in one line:
//
//   1. the background is in the average. A white cat against a summer forest
//      averages towards grey; a tabby on a blue blanket averages towards blue.
//   2. a mean is the wrong statistic for a coat. A tabby is dark stripes on
//      light fur and its mean is neither — it can name a colour that is
//      nowhere on the animal.
//   3. nearest-in-RGB weighs lightness and hue alike, so any mixed cat drifts
//      to grey.
//
// Fault 1 is now fixable for free: both platforms produce a subject mask
// (Vision here, ML Kit on Android), and the game already asks for one. This
// program measures what that is worth — the same palette decision made over
// the whole centre, and over the cat's own pixels, on the same photograph.
//
//   xcrun swiftc -O tools/marks-probe/colour-probe.swift -o /tmp/colour-probe
//   /tmp/colour-probe tmp/test_cat_photo/1-2.jpg ...

import Foundation
import Vision
import CoreGraphics
import ImageIO

/// The game's palette, from `CoatBuilder.Coats`.
let palette: [(String, Double, Double, Double)] = [
    ("ginger", 186 / 255.0, 108 / 255.0, 52 / 255.0),
    ("grey", 0.60, 0.62, 0.65),
    ("black", 0.18, 0.17, 0.17),
    ("white", 0.96, 0.94, 0.90),
    ("cream", 0.78, 0.71, 0.57),
    ("brown", 0.55, 0.40, 0.28),
]

func nearest(_ r: Double, _ g: Double, _ b: Double) -> String {
    var best = "", bestD = Double.greatestFiniteMagnitude
    for (name, pr, pg, pb) in palette {
        let d = (r - pr) * (r - pr) + (g - pg) * (g - pg) + (b - pb) * (b - pb)
        if d < bestD { bestD = d; best = name }
    }
    return best
}

for path in CommandLine.arguments.dropFirst() {
    guard let src = CGImageSourceCreateWithURL(URL(fileURLWithPath: path) as CFURL, nil),
          let image = CGImageSourceCreateImageAtIndex(src, 0, nil) else { continue }

    let w = image.width, h = image.height
    var rgba = [UInt8](repeating: 0, count: w * h * 4)
    guard let ctx = CGContext(data: &rgba, width: w, height: h, bitsPerComponent: 8,
                              bytesPerRow: w * 4,
                              space: CGColorSpaceCreateDeviceRGB(),
                              bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)
    else { continue }
    ctx.draw(image, in: CGRect(x: 0, y: 0, width: w, height: h))

    // What the game does today: mean over the central half.
    var r = 0.0, g = 0.0, b = 0.0, n = 0.0
    for y in (h / 4)..<(h - h / 4) {
        for x in (w / 4)..<(w - w / 4) {
            let i = (y * w + x) * 4
            r += Double(rgba[i]); g += Double(rgba[i + 1]); b += Double(rgba[i + 2]); n += 1
        }
    }
    let now = n > 0 ? nearest(r / n / 255, g / n / 255, b / n / 255) : "—"

    // The cat's own pixels, from the subject mask.
    var mask = [Bool](repeating: false, count: w * h)
    var haveMask = false
    let handler = VNImageRequestHandler(cgImage: image, options: [:])
    let request = VNGenerateForegroundInstanceMaskRequest()
    if (try? handler.perform([request])) != nil,
       let result = request.results?.first,
       let buffer = try? result.generateScaledMaskForImage(
            forInstances: result.allInstances, from: handler) {
        CVPixelBufferLockBaseAddress(buffer, .readOnly)
        if let base = CVPixelBufferGetBaseAddress(buffer) {
            let bw = CVPixelBufferGetWidth(buffer), bh = CVPixelBufferGetHeight(buffer)
            let stride = CVPixelBufferGetBytesPerRow(buffer)
            let floats = base.assumingMemoryBound(to: Float32.self)
            for y in 0..<h {
                let by = y * bh / h
                for x in 0..<w where floats[(by * stride / 4) + (x * bw / w)] > 0.5 {
                    mask[y * w + x] = true; haveMask = true
                }
            }
        }
        CVPixelBufferUnlockBaseAddress(buffer, .readOnly)
    }

    var onCat = "no mask", median = "no mask"
    if haveMask {
        var mr = 0.0, mg = 0.0, mb = 0.0, mn = 0.0
        // Every channel kept, so a median can be taken as well as a mean: on a
        // tabby the two disagree, and which one is right is the question.
        var rs: [Double] = [], gs: [Double] = [], bs: [Double] = []
        for i in 0..<(w * h) where mask[i] {
            let p = i * 4
            let pr = Double(rgba[p]), pg = Double(rgba[p + 1]), pb = Double(rgba[p + 2])
            mr += pr; mg += pg; mb += pb; mn += 1
            rs.append(pr); gs.append(pg); bs.append(pb)
        }
        if mn > 0 {
            onCat = nearest(mr / mn / 255, mg / mn / 255, mb / mn / 255)
            rs.sort(); gs.sort(); bs.sort()
            let mid = rs.count / 2
            median = nearest(rs[mid] / 255, gs[mid] / 255, bs[mid] / 255)
        }
    }

    let name = (path as NSString).lastPathComponent
    print("\(name)\tцентр кадра: \(now)\tпо маске (среднее): \(onCat)\tпо маске (медиана): \(median)")
}
