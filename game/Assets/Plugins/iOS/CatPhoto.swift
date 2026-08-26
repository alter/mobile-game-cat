import Foundation
import CoreGraphics
import ImageIO
import UniformTypeIdentifiers

// Task 50-photo/07: shrink an accepted photo to what the model needs and no
// more. Image cost is ceil(w/28)*ceil(h/28) visual tokens, so 512x512 is 361
// tokens; accuracy falls off below about 200 px on a side
// (knowledge/vision-model/01-traits-strict-json.md). Everything here follows
// from those two numbers.
//
// Orientation is NOT handled here. The image arriving is the one Vision already
// looked at, oriented (see CatVision.swift); re-applying orientation would turn
// the crop on its side.

public enum CatPhoto {
    public static let side = 512
    public static let minCropSide = 200
    public static let maxBytes = 200 * 1024

    /// Crop to `box` (pixels, origin top-left), widened around its own centre
    /// when it is too small to survive the resize, then square off and scale to
    /// `side`, then JPEG-encode under `maxBytes`.
    public static func prepare(image: CGImage, box: CGRect?) -> Data? {
        let full = CGRect(x: 0, y: 0, width: image.width, height: image.height)
        var rect = box ?? full

        // A cat filling a corner of a large photo can be reported as a 90 px
        // box. Blowing that up to 512 invents detail the model then reads as
        // real, so widen the crop instead and let the cat be smaller in frame.
        if rect.width < CGFloat(minCropSide) || rect.height < CGFloat(minCropSide) {
            rect = expand(rect, toAtLeast: CGFloat(minCropSide), within: full)
        }

        // Square around the centre of what we have, clamped to the image: the
        // model gets a square either way, and cropping is better than padding.
        rect = square(rect, within: full)

        guard let cropped = image.cropping(to: rect.integral) else { return nil }
        guard let scaled = resize(cropped, to: side) else { return nil }
        return encodeJPEG(scaled, underBytes: maxBytes)
    }

    static func expand(_ rect: CGRect, toAtLeast minimum: CGFloat,
                       within bounds: CGRect) -> CGRect {
        let width = max(rect.width, minimum)
        let height = max(rect.height, minimum)
        var expanded = CGRect(x: rect.midX - width / 2, y: rect.midY - height / 2,
                              width: width, height: height)
        // Slide back inside the image rather than clipping: an off-centre cat
        // should stay whole.
        expanded.origin.x = min(max(expanded.origin.x, bounds.minX),
                                max(bounds.maxX - expanded.width, bounds.minX))
        expanded.origin.y = min(max(expanded.origin.y, bounds.minY),
                                max(bounds.maxY - expanded.height, bounds.minY))
        return expanded.intersection(bounds)
    }

    static func square(_ rect: CGRect, within bounds: CGRect) -> CGRect {
        let size = min(max(rect.width, rect.height), min(bounds.width, bounds.height))
        var squared = CGRect(x: rect.midX - size / 2, y: rect.midY - size / 2,
                             width: size, height: size)
        squared.origin.x = min(max(squared.origin.x, bounds.minX),
                               max(bounds.maxX - size, bounds.minX))
        squared.origin.y = min(max(squared.origin.y, bounds.minY),
                               max(bounds.maxY - size, bounds.minY))
        return squared
    }

    static func resize(_ image: CGImage, to side: Int) -> CGImage? {
        let colourSpace = CGColorSpaceCreateDeviceRGB()
        guard let context = CGContext(
            data: nil, width: side, height: side, bitsPerComponent: 8,
            bytesPerRow: 0, space: colourSpace,
            bitmapInfo: CGImageAlphaInfo.noneSkipLast.rawValue) else { return nil }
        context.interpolationQuality = .high
        context.draw(image, in: CGRect(x: 0, y: 0, width: side, height: side))
        return context.makeImage()
    }

    /// Encode, dropping quality until it fits. Quality is stepped rather than
    /// fixed because a busy photo at 0.9 can exceed the cap while a plain one
    /// never will, and re-encoding is cheaper than sending too much.
    static func encodeJPEG(_ image: CGImage, underBytes limit: Int) -> Data? {
        for quality in [0.9, 0.8, 0.7, 0.6, 0.5, 0.4] {
            let data = NSMutableData()
            guard let destination = CGImageDestinationCreateWithData(
                data, UTType.jpeg.identifier as CFString, 1, nil) else { return nil }
            CGImageDestinationAddImage(destination, image, [
                kCGImageDestinationLossyCompressionQuality: quality
            ] as CFDictionary)
            guard CGImageDestinationFinalize(destination) else { return nil }
            if data.length <= limit || quality == 0.4 {
                return data as Data
            }
        }
        return nil
    }
}

#if os(iOS)
/// Crop and shrink a photo. Returns a malloc'd JPEG buffer; the caller frees it
/// with CatPhoto_free. Length comes back through `outLength`.
/// A box of all zeroes means "no box" — use the whole image.
@_cdecl("CatPhoto_prepare")
public func CatPhoto_prepare(_ bytes: UnsafePointer<UInt8>?, _ length: Int32,
                             _ boxX: Int32, _ boxY: Int32,
                             _ boxWidth: Int32, _ boxHeight: Int32,
                             _ outLength: UnsafeMutablePointer<Int32>?) -> UnsafeMutablePointer<UInt8>? {
    outLength?.pointee = 0
    guard let bytes = bytes, length > 0 else { return nil }
    let data = Data(bytes: bytes, count: Int(length))
    guard let source = CGImageSourceCreateWithData(data as CFData, nil),
          let image = CGImageSourceCreateImageAtIndex(source, 0, nil) else { return nil }

    let box: CGRect? = (boxWidth > 0 && boxHeight > 0)
        ? CGRect(x: Int(boxX), y: Int(boxY), width: Int(boxWidth), height: Int(boxHeight))
        : nil
    guard let jpeg = CatPhoto.prepare(image: image, box: box) else { return nil }

    let buffer = malloc(jpeg.count)?.assumingMemoryBound(to: UInt8.self)
    guard let buffer = buffer else { return nil }
    jpeg.copyBytes(to: buffer, count: jpeg.count)
    outLength?.pointee = Int32(jpeg.count)
    return buffer
}

@_cdecl("CatPhoto_free")
public func CatPhoto_free(_ pointer: UnsafeMutablePointer<UInt8>?) {
    free(pointer)
}
#endif
