// tools/marks-probe: runs the SHIPPED marks plugin over a folder of images, on
// this Mac, and prints one JSON line per image.
//
// Why this exists. `Plugins/iOS/CatMarks.swift` measures a cat's distinctive
// marks — the only trait the game has that identifies a particular cat rather
// than a kind of cat. It leans on two iOS 17 requests, foreground instance mask
// and animal body pose, and **neither runs in the iOS simulator**: Apple says
// so for the pose, and the mask fails with `Could not create inference context`
// (50-photo/05-vision-plugin/NOTES.md records the same message from the older
// plugin). The first conclusion drawn from that was "nothing can be checked
// until someone runs it on a phone", and that conclusion was lazy.
//
// Vision is a macOS framework too. Both requests are macOS 14+, this machine is
// far past that, and `CatMarks.swift` type-checks against the macOS SDK
// unchanged. So the whole measurement can be run right here, over all 41
// reference photographs, today — no device, no provisioning, no waiting.
//
// The same argument, and the same limits, as the older `tools/vision-probe`:
// this compiles the plugin's own source, so the algorithm, the ten places, the
// medians and the fallback ladder are exactly the shipped ones. What it does
// NOT exercise is the C#/IL2CPP marshalling, and Vision on macOS is not
// guaranteed to run the same model an iPhone's neural engine runs. Those two
// gaps still want a phone. Everything else does not.
//
//   xcrun swiftc -O \
//     game/Assets/Plugins/iOS/CatMarks.swift tools/marks-probe/main.swift \
//     -o /tmp/marks-probe
//   /tmp/marks-probe fixtures/reference-photos > marks.jsonl

import Foundation

let arguments = CommandLine.arguments
guard arguments.count >= 2 else {
    FileHandle.standardError.write(
        "usage: marks-probe <folder-of-images> [minLandmarkConfidence]\n"
            .data(using: .utf8)!)
    exit(2)
}

let folder = URL(fileURLWithPath: arguments[1])
// The plugin's own default. Passed through rather than fixed here, so the
// threshold can be swept over the reference set instead of guessed — which is
// the whole reason the plugin reports numbers and not verdicts.
let minConfidence = arguments.count >= 3 ? Double(arguments[2]) ?? 0.3 : 0.3

let names = ((try? FileManager.default.contentsOfDirectory(atPath: folder.path)) ?? [])
    .filter { $0.lowercased().hasSuffix(".jpg") || $0.lowercased().hasSuffix(".jpeg") }
    .sorted()

if names.isEmpty {
    FileHandle.standardError.write("no images in \(folder.path)\n".data(using: .utf8)!)
    exit(1)
}

for name in names {
    let data = (try? Data(contentsOf: folder.appendingPathComponent(name))) ?? Data()

    let json: String = data.withUnsafeBytes { raw -> String in
        guard let base = raw.bindMemory(to: UInt8.self).baseAddress else {
            return "{\"error\":\"unreadable\"}"
        }
        // 1 is CGImagePropertyOrientation.up: these are files on disk, already
        // upright, and passing the camera's orientation would be a lie.
        guard let out = CatMarks_measure(base, Int32(data.count), 1, minConfidence) else {
            return "{\"error\":\"null\"}"
        }
        defer { CatMarks_free(out) }
        return String(cString: out)
    }

    print("{\"file\":\"\(name)\",\"result\":\(json)}")
}
