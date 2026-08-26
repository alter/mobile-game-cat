import Foundation
import CoreImage
import CoreGraphics

// Task 50-photo/11: when the Worker cannot be reached, read the one trait a
// phone can actually read — the base colour — and default the rest.
//
// Nothing on device reads a coat PATTERN. Apple's built-in classifier has 1303
// categories and not one of them is a tabby (knowledge/ios/06); there is no
// licensed model for it either. So this returns a colour and nothing else, and
// the caller forces pattern=solid rather than guessing.
//
// CIAreaAverage over the centre of the crop, matched to the six palette names.
// The centre, not the whole frame, because the crop already put the cat in the
// middle and the edges are carpet and sofa.

public enum CatColour {
    /// The six base colours the game can draw, as sRGB anchors. Deliberately
    /// dull: a photograph of a ginger cat in a warm room is nowhere near
    /// saturated orange, and matching against vivid anchors sends every warm
    /// cat to "ginger" and every cool one to "grey".
    /// Measured, not chosen: each anchor is the mean centre-frame colour of the
    /// photographs a person labelled with that name
    /// (50-photo/11-offline-fallback/ground-truth.txt). Invented anchors —
    /// saturated orange for ginger, near-white for white — scored worse,
    /// because a photograph of a ginger cat in a warm room is nothing like
    /// saturated orange.
    ///
    /// `white` and `cream` carry the fewest examples and are the least
    /// trustworthy; they are kept so the estimator can return them at all.
    static let palette: [(name: String, r: Double, g: Double, b: Double)] = [
        ("ginger", 0.75, 0.59, 0.42),
        ("grey",   0.45, 0.43, 0.41),
        ("black",  0.26, 0.21, 0.20),
        // white and cream keep physical anchors rather than measured ones: the
        // set holds a single white cat and no cream one, and its measured
        // centre landed mid-range, where it pulled six other cats to "white".
        ("white",  0.85, 0.84, 0.82),
        ("cream",  0.78, 0.72, 0.60),
        ("brown",  0.50, 0.43, 0.38),
    ]

    public static func estimate(image: CGImage) -> String? {
        let ciImage = CIImage(cgImage: image)
        // Centre half: the crop centred the animal, and this drops most of the
        // background without needing a mask.
        let full = ciImage.extent
        let centre = full.insetBy(dx: full.width * 0.25, dy: full.height * 0.25)

        // The frame average, not k-means. Clustering was tried and scored
        // WORSE on the labelled set — 41% against 48% — because the largest
        // cluster of a tabby is its stripes, not its coat. Measured, not
        // assumed; see NOTES.md.
        guard let filter = CIFilter(name: "CIAreaAverage", parameters: [
            kCIInputImageKey: ciImage,
            kCIInputExtentKey: CIVector(cgRect: centre),
        ]), let output = filter.outputImage else { return nil }

        var pixel = [UInt8](repeating: 0, count: 4)
        let context = CIContext(options: [.workingColorSpace: NSNull()])
        context.render(output, toBitmap: &pixel, rowBytes: 4,
                       bounds: CGRect(x: 0, y: 0, width: 1, height: 1),
                       format: .RGBA8, colorSpace: CGColorSpaceCreateDeviceRGB())

        return nearest(r: Double(pixel[0]) / 255.0,
                       g: Double(pixel[1]) / 255.0,
                       b: Double(pixel[2]) / 255.0)
    }

    /// Nearest palette entry by plain squared distance. Weighting lightness
    /// was tried at 1x, 2x and 4x and made it worse every time (52%, 44%, 44%
    /// against 56%), so it is not there.
    static func nearest(r: Double, g: Double, b: Double) -> String {
        var best = palette[0].name
        var bestScore = Double.greatestFiniteMagnitude
        for entry in palette {
            let score = pow(r - entry.r, 2) + pow(g - entry.g, 2) + pow(b - entry.b, 2)
            if score < bestScore {
                bestScore = score
                best = entry.name
            }
        }
        return best
    }
}

#if os(iOS)
/// Estimate the base colour of a JPEG already cropped to the cat.
/// Returns a malloc'd C string the caller frees with CatColour_free, or null.
@_cdecl("CatColour_estimate")
public func CatColour_estimate(_ bytes: UnsafePointer<UInt8>?, _ length: Int32)
    -> UnsafeMutablePointer<CChar>? {
    guard let bytes = bytes, length > 0 else { return nil }
    let data = Data(bytes: bytes, count: Int(length))
    guard let source = CGImageSourceCreateWithData(data as CFData, nil),
          let image = CGImageSourceCreateImageAtIndex(source, 0, nil),
          let name = CatColour.estimate(image: image) else { return nil }
    return strdup(name)
}

@_cdecl("CatColour_free")
public func CatColour_free(_ pointer: UnsafeMutablePointer<CChar>?) {
    free(pointer)
}
#endif
