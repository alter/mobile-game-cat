import Foundation
#if os(iOS)
import UIKit

// Task 60-shell-build/15: the iOS half of Shell/Share.cs.
//
// UIActivityViewController is the system share sheet — the one the player
// already knows, with whatever she has installed in it. We do not enumerate
// targets and we do not draw our own list: Apple's own guidance and Android's
// alike say the sheet is the system's, and a hand-rolled row of logos goes
// stale the day she installs something new.
//
// Two things about this class are not obvious and both are load-bearing:
//
//  1. iPad. From the UIActivityViewController reference: "When presenting the
//     view controller, you must do so using the appropriate means for the
//     current device. On iPad, you must present the view controller in a
//     popover. On iPhone and iPod touch, you must present it modally."
//     UIKit does the first half itself — on iPad the sheet arrives already in
//     .popover style — but it does NOT anchor it, and an unanchored popover
//     raises at presentation time ("UIPopoverPresentationController should
//     have a non-nil sourceView or barButtonItem set before the presentation
//     occurs"). That is an uncaught exception, i.e. a crash, on every iPad and
//     on no iPhone, which is exactly the shape of bug that ships. The anchor
//     below is set unconditionally: popoverPresentationController is nil on
//     iPhone, so the `if let` costs nothing there and there is no device check
//     to get wrong.
//
//  2. No answer goes back to Unity. CatPicker.swift has UnitySendMessage
//     because the game waits for a photo; nothing waits for this. The sheet
//     belongs to the system, the player may dismiss it, and iOS does not
//     report which target took the image in any form worth a callback.
//
// This file carries no player-visible words: the caption is composed in the
// player's language on the C# side (Copy.cs) and arrives as an argument. Every
// string below is either a path or an NSLog line for whoever reads the device
// console.
//
// NOT COMPILED as part of this change — no iOS build was run. Verify on
// device, and specifically on an iPad, which is the one that crashes.

private func present(_ controller: UIViewController) {
    // Same window lookup CatPicker.swift uses, deliberately: whatever is true
    // about UIApplication.windows in this project's toolchain is already true
    // for the picker, and two different ways of finding the root view
    // controller in one app is one more thing to keep in step.
    guard let root = UIApplication.shared.windows.first(where: { $0.isKeyWindow })?
        .rootViewController else {
        NSLog("[CatShare] no_window")
        return
    }

    if let popover = controller.popoverPresentationController {
        // Anchored to the centre of the presenting view with a zero-size rect
        // and no arrow: the share button is drawn by UI Toolkit inside the
        // Unity view, so there is no UIView and no UIBarButtonItem to point
        // at. A centred, arrowless popover is the honest answer to "anchor
        // this to nothing in particular".
        popover.sourceView = root.view
        popover.sourceRect = CGRect(x: root.view.bounds.midX,
                                    y: root.view.bounds.midY,
                                    width: 0, height: 0)
        popover.permittedArrowDirections = []
    }

    root.present(controller, animated: true)
}

/// Open the share sheet on the PNG at `path`, with `text` alongside it.
/// Called from Shell/Share.cs; the C# side wrote the file and this side owns
/// it from here, deletion included.
@_cdecl("CatShare_image")
public func CatShare_image(_ pathC: UnsafePointer<CChar>,
                           _ textC: UnsafePointer<CChar>) {
    let path = String(cString: pathC)
    let text = String(cString: textC)

    DispatchQueue.main.async {
        guard let image = UIImage(contentsOfFile: path) else {
            NSLog("[CatShare] no_image at %@", path)
            return
        }
        // The bytes are in the UIImage now; the file has done its job as a way
        // across the C boundary and nothing else will clean it up.
        try? FileManager.default.removeItem(atPath: path)

        // A UIImage rather than the file URL. Both work, and the URL would
        // hand AirDrop and Files a real .png with a name — but a UIImage is
        // what every target treats as a picture without having to guess from
        // an extension, and Photos, Messages, Mail and the third-party apps
        // the owner named all take it directly. The cost is one re-encode
        // inside UIKit, which the player cannot see.
        var items: [Any] = [image]
        if !text.isEmpty {
            // Second item, not merged into the image: the sheet offers both
            // and each target keeps what it can use.
            items.append(text)
        }

        let sheet = UIActivityViewController(activityItems: items,
                                             applicationActivities: nil)
        present(sheet)
    }
}
#endif
