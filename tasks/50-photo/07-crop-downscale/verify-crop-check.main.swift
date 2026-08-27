import Foundation
import CoreGraphics
import ImageIO

// Independent verification harness for tasks/50-photo/07-crop-downscale.
// Compiled TOGETHER with the repo's own
// game/Assets/Plugins/iOS/CatPhoto.swift (unmodified) — this file adds no
// logic to CatPhoto itself, it only drives it and checks the results.
//
// Usage: swiftc main.swift <path-to-repo>/game/Assets/Plugins/iOS/CatPhoto.swift -o /tmp/cropcheck && /tmp/cropcheck <fixtures-dir>

guard CommandLine.arguments.count > 1 else {
    print("usage: cropcheck <fixtures-dir>")
    exit(1)
}
let dir = CommandLine.arguments[1]

func loadImage(_ path: String) -> CGImage? {
    guard let source = CGImageSourceCreateWithURL(URL(fileURLWithPath: path) as CFURL, nil) else { return nil }
    return CGImageSourceCreateImageAtIndex(source, 0, nil)
}

var checked = 0
var okDims = 0
var okSize = 0
var sizes: [Int] = []

let fm = FileManager.default
let files = (try? fm.contentsOfDirectory(atPath: dir))?.filter { $0.hasSuffix(".jpg") }.sorted() ?? []

for file in files {
    guard let img = loadImage(dir + "/" + file) else { print("SKIP (decode failed): \(file)"); continue }
    guard let out = CatPhoto.prepare(image: img, box: nil) else { print("FAIL (prepare returned nil): \(file)"); continue }
    checked += 1
    guard let outImg = CGImageSourceCreateImageAtIndex(CGImageSourceCreateWithData(out as CFData, nil)!, 0, nil) else {
        print("FAIL (output not decodable): \(file)"); continue
    }
    let dimsOk = outImg.width == CatPhoto.side && outImg.height == CatPhoto.side
    let sizeOk = out.count < CatPhoto.maxBytes
    if dimsOk { okDims += 1 } else { print("DIM MISMATCH \(file): \(outImg.width)x\(outImg.height)") }
    if sizeOk { okSize += 1 } else { print("SIZE OVER \(file): \(out.count) bytes") }
    sizes.append(out.count)
}

print("whole-image pass: \(checked) files, \(okDims)/\(checked) exactly 512x512, \(okSize)/\(checked) under 200KB")
if !sizes.isEmpty {
    print("size range: min=\(sizes.min()!) median=\(sizes.sorted()[sizes.count/2]) max=\(sizes.max()!)")
}

// --- Guard test: a deliberately tiny box must widen, not upscale a tiny region ---
if let firstFile = files.first, let img = loadImage(dir + "/" + firstFile) {
    let tinyBox = CGRect(x: 10, y: 10, width: 40, height: 40) // well under minCropSide (200)
    let full = CGRect(x: 0, y: 0, width: img.width, height: img.height)
    let widened = CatPhoto.expand(tinyBox, toAtLeast: CGFloat(CatPhoto.minCropSide), within: full)
    let widenedOk = widened.width >= CGFloat(CatPhoto.minCropSide) && widened.height >= CGFloat(CatPhoto.minCropSide)
    print("guard: tiny box \(tinyBox) -> expand() \(widened) — widened to >= \(CatPhoto.minCropSide)px: \(widenedOk)")

    guard let out = CatPhoto.prepare(image: img, box: tinyBox) else {
        print("FAIL: prepare(tinyBox) returned nil"); exit(1)
    }
    guard let outImg = CGImageSourceCreateImageAtIndex(CGImageSourceCreateWithData(out as CFData, nil)!, 0, nil) else {
        print("FAIL: tiny-box output not decodable"); exit(1)
    }
    print("guard: prepare() with tiny box still outputs \(outImg.width)x\(outImg.height), \(out.count) bytes")
    print("VERDICT guard triggers and widens: \(widenedOk)")
}
