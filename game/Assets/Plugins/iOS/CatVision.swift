import Foundation
import Vision
import ImageIO
import CoreGraphics

// Task 50-photo/05: stage one of the photo pipeline. Answers "is there an
// animal, which, where, how sure" from a JPEG already in memory. Free,
// offline, on the neural engine — the gate that decides whether a paid model
// call happens at all.
//
// VNRecognizeAnimalsRequest, not the iOS 18 Swift-only RecognizeAnimalsRequest:
// the old API reaches back to iOS 13 (our floor is 15) and is not deprecated.
// Both recognise exactly two species, cat and dog.
//
// Results cross into C# as a JSON string. A struct would be faster and would
// also pin a memory layout across the Swift/C#/IL2CPP boundary for the sake of
// one call per photo taken by hand.

private struct Detection: Encodable {
    let identifier: String
    let confidence: Double
    // Pixel coordinates, origin top-left, ready to crop with. Vision itself
    // reports a normalised box with the origin at the BOTTOM-left, which is a
    // well-known source of upside-down crops, so the flip happens here rather
    // than in every caller.
    let x: Int, y: Int, width: Int, height: Int
}

private struct Answer: Encodable {
    let ok: Bool
    let error: String?
    let imageWidth: Int, imageHeight: Int
    let detections: [Detection]
}

private func encode(_ answer: Answer) -> UnsafeMutablePointer<CChar>? {
    guard let data = try? JSONEncoder().encode(answer),
          let text = String(data: data, encoding: .utf8) else { return strdup("{\"ok\":false}") }
    return strdup(text)
}

private func fail(_ message: String) -> UnsafeMutablePointer<CChar>? {
    encode(Answer(ok: false, error: message, imageWidth: 0, imageHeight: 0, detections: []))
}

/// A short, stable code for a Vision error — the framework's own domain and
/// numeric code, e.g. "com.apple.Vision/9" for VNErrorInvalidFormat. Never
/// error.localizedDescription: that string follows the DEVICE's system
/// language, not the game's, and crosses straight into a player-visible
/// message once it reaches C# — see CatPicker.swift:20-29, where that rule was
/// bought the hard way. Compare CatVision.java's reason(), which does the same
/// job on Android with an MlKitException code or a class name.
private func code(_ error: Error) -> String {
    let ns = error as NSError
    return "\(ns.domain)/\(ns.code)"
}

/// Recognise animals in a JPEG/PNG held in memory.
/// - Parameter orientationRaw: a CGImagePropertyOrientation raw value. Vision
///   stores no orientation of its own and silently mis-detects when it is
///   wrong, so the caller must always say which way is up. 0 means "read it
///   from the file's own metadata".
@_cdecl("CatVision_recognise")
public func CatVision_recognise(_ bytes: UnsafePointer<UInt8>?,
                                _ length: Int32,
                                _ orientationRaw: Int32) -> UnsafeMutablePointer<CChar>? {
    guard let bytes = bytes, length > 0 else { return fail("empty image data") }

    let data = Data(bytes: bytes, count: Int(length))
    guard let source = CGImageSourceCreateWithData(data as CFData, nil),
          let image = CGImageSourceCreateImageAtIndex(source, 0, nil) else {
        return fail("not a decodable image")
    }

    var orientation = CGImagePropertyOrientation.up
    if orientationRaw > 0 {
        orientation = CGImagePropertyOrientation(rawValue: UInt32(orientationRaw)) ?? .up
    } else if let properties = CGImageSourceCopyPropertiesAtIndex(source, 0, nil) as? [CFString: Any],
              let exif = properties[kCGImagePropertyOrientation] as? UInt32 {
        orientation = CGImagePropertyOrientation(rawValue: exif) ?? .up
    }

    let request = VNRecognizeAnimalsRequest()
    let handler = VNImageRequestHandler(cgImage: image, orientation: orientation, options: [:])
    do {
        try handler.perform([request])
    } catch {
        return fail("vision failed: \(code(error))")
    }

    // Vision reports the box against the oriented image, so the pixel sizes
    // swap when the photo is on its side.
    let sideways = [.left, .right, .leftMirrored, .rightMirrored].contains(orientation)
    let width = sideways ? image.height : image.width
    let height = sideways ? image.width : image.height

    var detections: [Detection] = []
    for observation in request.results ?? [] {
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

    return encode(Answer(ok: true, error: nil,
                         imageWidth: width, imageHeight: height,
                         detections: detections))
}

/// Free a string returned by this plugin. Swift allocated it with strdup, so
/// the C# side cannot let the marshaller reclaim it.
@_cdecl("CatVision_free")
public func CatVision_free(_ pointer: UnsafeMutablePointer<CChar>?) {
    free(pointer)
}
