// tools/coat-probe: exports, for a list of photographs, exactly what the
// on-device pipeline sees — the 512x512 crop `CatPhoto.swift` produces, and
// the subject mask `CatMarks.swift` measures on top of it — as raw bytes a
// C# process can read without going through Vision or a phone at all.
//
// This is not a reimplementation. `CatPhoto.prepare` is called directly
// (it is public); `upright`, `rgbaBytes`, `readMask` and `chooseInstance` are
// copied byte-for-byte from `CatMarks.swift` because Swift's `private` makes
// them unreachable across files even inside the same module — copying is the
// only way to reuse them without editing the shipped plugin.
//
// The real call order, confirmed in `View/CaptureScreen.cs`:
//   Recognise(photo) -> best animal box (Shell/CatVision.swift)
//   Crop(photo, box) = CatPhoto.Prepare  -> 512x512 JPEG
//   CatMarks.Measure(prepared)           -> re-decodes that JPEG, re-runs
//                                            VNRecognizeAnimalsRequest on the
//                                            CROP (not the original photo) to
//                                            pick the mask instance.
// This tool reproduces exactly that order.
//
//   xcrun swiftc -O \
//     game/Assets/Plugins/iOS/CatPhoto.swift tools/coat-probe/coat-dump.swift \
//     -o /tmp/coat-dump
//   /tmp/coat-dump <outdir> <image1> <image2> ...

import Foundation
import Vision
import ImageIO
import CoreGraphics
import CoreVideo
import UniformTypeIdentifiers

// MARK: - Copied verbatim from Plugins/iOS/CatMarks.swift (private there,
// unreachable from here any other way).

/// Redraw upright so that Vision's coordinate space and the mask's pixel grid
/// are the same grid. Copied from CatMarks.swift's `upright(_:_:)`.
func upright(_ image: CGImage, _ orientation: CGImagePropertyOrientation) -> CGImage? {
    if orientation == .up { return image }
    let w = CGFloat(image.width), h = CGFloat(image.height)
    let sideways = [.left, .right, .leftMirrored, .rightMirrored].contains(orientation)
    let outW = Int(sideways ? h : w), outH = Int(sideways ? w : h)

    guard let context = CGContext(
        data: nil, width: outW, height: outH,
        bitsPerComponent: 8, bytesPerRow: 0,
        space: CGColorSpaceCreateDeviceRGB(),
        bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue) else { return nil }

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

/// The image as tightly packed RGBA8, top-left row-major. Copied from
/// CatMarks.swift's `rgbaBytes(_:)`.
func rgbaBytes(_ image: CGImage) -> (bytes: [UInt8], width: Int, height: Int)? {
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

/// Read a one-component mask buffer, whatever depth Vision handed back, as
/// raw values — floats 0...1 for OneComponent32Float, bytes 0...255 passed
/// through unchanged for OneComponent8. Copied from CatMarks.swift's
/// `readMask(_:)`.
func readMask(_ buffer: CVPixelBuffer) -> (values: [Float], width: Int, height: Int)? {
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

/// Pick the foreground instance that is the cat, by overlap with the animal
/// box. Copied from CatMarks.swift's `chooseInstance(_:animalBox:)`.
func chooseInstance(_ observation: VNInstanceMaskObservation,
                    animalBox: CGRect?) -> Int? {
    guard let mask = readMask(observation.instanceMask) else { return nil }
    var inside = [Int: Int](), total = [Int: Int]()

    for y in 0..<mask.height {
        for x in 0..<mask.width {
            let index = Int(mask.values[y * mask.width + x].rounded())
            guard index > 0 else { continue }
            total[index, default: 0] += 1
            if let box = animalBox {
                let nx = (Double(x) + 0.5) / Double(mask.width)
                let ny = 1.0 - (Double(y) + 0.5) / Double(mask.height)
                if box.contains(CGPoint(x: nx, y: ny)) { inside[index, default: 0] += 1 }
            }
        }
    }
    guard !total.isEmpty else { return nil }

    if animalBox != nil, !inside.isEmpty {
        return inside.max { a, b in
            let fa = Double(a.value) / Double(total[a.key] ?? 1)
            let fb = Double(b.value) / Double(total[b.key] ?? 1)
            return fa < fb
        }?.key
    }
    return total.max { $0.value < $1.value }?.key
}

// MARK: - This tool's own code

func bestAnimal(_ image: CGImage) throws -> (box: CGRect?, identifier: String, confidence: Double) {
    let request = VNRecognizeAnimalsRequest()
    try VNImageRequestHandler(cgImage: image, orientation: .up, options: [:]).perform([request])
    guard let best = (request.results ?? []).max(by: {
        ($0.labels.first?.confidence ?? 0) < ($1.labels.first?.confidence ?? 0)
    }), let label = best.labels.first else {
        return (nil, "", 0)
    }
    return (best.boundingBox, label.identifier, Double(label.confidence))
}

/// Vision's normalised, bottom-left box converted to pixels, origin top-left —
/// exactly the conversion `Shell/CatVision.swift` does, and exactly what
/// `CatPhoto.prepare(image:box:)` expects.
func pixelBox(_ normalised: CGRect, imageWidth: Int, imageHeight: Int) -> CGRect {
    let rect = VNImageRectForNormalizedRect(normalised, imageWidth, imageHeight)
    return CGRect(x: rect.origin.x,
                  y: CGFloat(imageHeight) - rect.origin.y - rect.height,
                  width: rect.width, height: rect.height)
}

func appendInt32LE(_ v: Int32, to data: inout Data) {
    var le = v.littleEndian
    withUnsafeBytes(of: &le) { data.append(contentsOf: $0) }
}

func writePreviewPNG(rgb: [UInt8], width: Int, height: Int, maskBytes: [UInt8]?, to url: URL) {
    var pixels = [UInt8](repeating: 255, count: width * height * 4)
    for i in 0..<(width * height) {
        let on = (maskBytes?[i] ?? 0) >= 128
        let p = i * 4
        if on {
            pixels[p] = rgb[i * 3]
            pixels[p + 1] = rgb[i * 3 + 1]
            pixels[p + 2] = rgb[i * 3 + 2]
        } else {
            pixels[p] = 255; pixels[p + 1] = 0; pixels[p + 2] = 255
        }
        pixels[p + 3] = 255
    }
    guard let context = CGContext(
        data: &pixels, width: width, height: height, bitsPerComponent: 8,
        bytesPerRow: width * 4, space: CGColorSpaceCreateDeviceRGB(),
        bitmapInfo: CGImageAlphaInfo.noneSkipLast.rawValue),
        let image = context.makeImage(),
        let destination = CGImageDestinationCreateWithURL(
            url as CFURL, UTType.png.identifier as CFString, 1, nil) else { return }
    CGImageDestinationAddImage(destination, image, nil)
    CGImageDestinationFinalize(destination)
}

func fail(_ name: String, _ reason: String) {
    print("FAILED \(name) \(reason)")
}

@main
struct CoatDump {
    static func main() {
        let arguments = CommandLine.arguments
        guard arguments.count >= 3 else {
            FileHandle.standardError.write(
                "usage: coat-dump <outdir> <image> [image ...]\n".data(using: .utf8)!)
            exit(2)
        }
        let outDir = URL(fileURLWithPath: arguments[1])
        try? FileManager.default.createDirectory(at: outDir, withIntermediateDirectories: true)
        let paths = Array(arguments.dropFirst(2))

        for path in paths {
            let name = (path as NSString).lastPathComponent
            let base = (name as NSString).deletingPathExtension

            guard let data = try? Data(contentsOf: URL(fileURLWithPath: path)) else {
                fail(name, "cannot read file"); continue
            }
            guard let source = CGImageSourceCreateWithData(data as CFData, nil),
                  let decoded = CGImageSourceCreateImageAtIndex(source, 0, nil) else {
                fail(name, "not a decodable image"); continue
            }

            var orientation = CGImagePropertyOrientation.up
            if let properties = CGImageSourceCopyPropertiesAtIndex(source, 0, nil) as? [CFString: Any],
               let exif = properties[kCGImagePropertyOrientation] as? UInt32 {
                orientation = CGImagePropertyOrientation(rawValue: exif) ?? .up
            }
            guard let image = upright(decoded, orientation) else {
                fail(name, "could not orient the image"); continue
            }

            // Step 2: the box VNRecognizeAnimalsRequest draws on the ORIGINAL
            // (uprighted) photo — same call CaptureScreen makes as `Recognise`.
            var animalIdentifier = "", animalConfidence = 0.0
            var normalisedBox: CGRect?
            do {
                let found = try bestAnimal(image)
                normalisedBox = found.box
                animalIdentifier = found.identifier
                animalConfidence = found.confidence
            } catch {
                // No box: CatPhoto.prepare(box: nil) uses the whole image,
                // exactly as CaptureScreen does when Recognise fails to find
                // anything — carry on rather than aborting the run.
            }
            let cropBox = normalisedBox.map {
                pixelBox($0, imageWidth: image.width, imageHeight: image.height)
            }

            // Step 3: crop + square + scale to 512x512, EXACTLY as
            // CatPhoto.swift does — this calls its own public function, not a
            // reimplementation.
            guard let jpeg = CatPhoto.prepare(image: image, box: cropBox) else {
                fail(name, "CatPhoto.prepare failed"); continue
            }
            // CatMarks.Measure receives this same JPEG and re-decodes it — so
            // do we, to see exactly the pixels it sees (JPEG loss included).
            guard let cropSource = CGImageSourceCreateWithData(jpeg as CFData, nil),
                  let cropImage = CGImageSourceCreateImageAtIndex(cropSource, 0, nil) else {
                fail(name, "could not re-decode the crop"); continue
            }
            guard let raster = rgbaBytes(cropImage) else {
                fail(name, "could not rasterise the crop"); continue
            }
            let width = raster.width, height = raster.height

            // Step 4: the mask, on the CROP, choosing the instance against the
            // animal box RE-RUN on the crop — exactly what CatMarks_measure
            // does internally on `prepared`.
            var maskBytes: [UInt8]?
            let maskClockStart = DispatchTime.now()
            do {
                let cropAnimal = try? bestAnimal(cropImage)
                let maskHandler = VNImageRequestHandler(cgImage: cropImage, orientation: .up, options: [:])
                let masking = VNGenerateForegroundInstanceMaskRequest()
                try maskHandler.perform([masking])
                if let observation = masking.results?.first {
                    var instances = observation.allInstances
                    if let chosen = chooseInstance(observation, animalBox: cropAnimal?.box) {
                        instances = IndexSet(integer: chosen)
                    }
                    if !instances.isEmpty {
                        let buffer = try observation.generateScaledMaskForImage(
                            forInstances: instances, from: maskHandler)
                        if let read = readMask(buffer), read.width == width, read.height == height {
                            let format = CVPixelBufferGetPixelFormatType(buffer)
                            var bytes = [UInt8](repeating: 0, count: width * height)
                            if format == kCVPixelFormatType_OneComponent32Float {
                                for i in 0..<bytes.count {
                                    let v = min(1.0, max(0.0, read.values[i])) * 255.0
                                    bytes[i] = UInt8(v.rounded())
                                }
                            } else {
                                for i in 0..<bytes.count {
                                    bytes[i] = UInt8(min(255.0, max(0.0, read.values[i].rounded())))
                                }
                            }
                            maskBytes = bytes
                        }
                    }
                }
            } catch {
                // Mask stays nil; recorded as hasMask=0 below.
            }
            let maskMs = Double(DispatchTime.now().uptimeNanoseconds
                - maskClockStart.uptimeNanoseconds) / 1_000_000.0

            // Write the dump.
            var dump = Data()
            dump.append(contentsOf: [0x43, 0x4F, 0x41, 0x54]) // "COAT"
            appendInt32LE(Int32(width), to: &dump)
            appendInt32LE(Int32(height), to: &dump)
            appendInt32LE(maskBytes != nil ? 1 : 0, to: &dump)
            var rgb = [UInt8](); rgb.reserveCapacity(width * height * 3)
            for i in 0..<(width * height) {
                let p = i * 4
                rgb.append(raster.bytes[p]); rgb.append(raster.bytes[p + 1]); rgb.append(raster.bytes[p + 2])
            }
            dump.append(contentsOf: rgb)
            if let m = maskBytes { dump.append(contentsOf: m) }

            let dumpURL = outDir.appendingPathComponent("\(base).coat")
            do {
                try dump.write(to: dumpURL)
            } catch {
                fail(name, "could not write dump: \(error.localizedDescription)"); continue
            }

            writePreviewPNG(rgb: rgb, width: width, height: height, maskBytes: maskBytes,
                            to: outDir.appendingPathComponent("\(name).preview.png"))

            let maskPixelsAt128 = maskBytes?.reduce(0) { $0 + ($1 >= 128 ? 1 : 0) } ?? 0
            print("\(name)\t\(maskBytes != nil ? 1 : 0)\t\(maskPixelsAt128)\t"
                + "\(animalIdentifier)\t\(animalConfidence)\t\(String(format: "%.2f", maskMs))")
        }
    }
}
