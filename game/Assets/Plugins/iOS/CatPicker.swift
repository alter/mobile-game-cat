import Foundation
#if os(iOS)
import UIKit
import PhotosUI

// Task 50-photo/08: the two ways a photo gets in.
//
// Gallery uses PHPickerViewController, which runs OUTSIDE the app's process:
// the app receives only what the player picked and needs no photo-library
// permission at all, as long as it never asks for the PHAsset. That is one
// fewer permission prompt between a player and the only screen that matters.
// Camera uses UIImagePickerController, which does require
// NSCameraUsageDescription — there is no way around that one.
//
// The picked image comes back as a file path rather than bytes:
// UnitySendMessage carries a string, and a JPEG written to the temporary
// directory avoids inventing a second callback channel for binary data. Unity
// reads it and deletes it.
//
// Task 60-shell-build/16 VERIFY finding: OnPickFailed/OnPickUnavailable used
// to carry English sentences ("could not save the picked image: <system
// error>"), which the C# side folded straight into a player-visible message.
// That is prose crossing a native boundary into a project that keeps its
// words in one table (Shell/Copy.cs) - untranslatable by construction, and
// on a device set to another system language, `error.localizedDescription`
// could hand back text in that language, not English. Every reason below is
// now a fixed lowercase code, not a sentence; CatPicker.cs maps codes to
// copy, never displays one verbatim. NOT COMPILED as part of this change -
// verify with an iOS build.

@_silgen_name("UnitySendMessage")
private func UnitySendMessage(_ object: UnsafePointer<CChar>,
                              _ method: UnsafePointer<CChar>,
                              _ message: UnsafePointer<CChar>)

private func send(_ method: String, _ payload: String) {
    // The listener is a fixed GameObject created by the shell before any pick
    // starts; a missing one is a silent no-op on Unity's side, which is why
    // the shell also times out rather than waiting forever.
    "CatPickerListener".withCString { object in
        method.withCString { selector in
            payload.withCString { text in
                UnitySendMessage(object, selector, text)
            }
        }
    }
}

private func deliver(_ image: UIImage?) {
    guard let image = image, let jpeg = image.jpegData(compressionQuality: 0.95) else {
        send("OnPickFailed", "read_failed")
        return
    }
    let path = NSTemporaryDirectory() + "\(filePrefix)\(UUID().uuidString)\(fileSuffix)"
    do {
        try jpeg.write(to: URL(fileURLWithPath: path))
        send("OnPicked", path)
    } catch {
        // error.localizedDescription is deliberately not sent: it follows
        // the device's system language, not the game's, and would land
        // straight in a player-visible message. NSLog keeps it for whoever
        // reads device console output; it never crosses the Unity boundary.
        NSLog("[CatPicker] save_failed: \(error.localizedDescription)")
        send("OnPickFailed", "save_failed")
    }
}

private final class PickerDelegate: NSObject, PHPickerViewControllerDelegate,
                                    UIImagePickerControllerDelegate,
                                    UINavigationControllerDelegate {
    static let shared = PickerDelegate()

    func picker(_ picker: PHPickerViewController,
                didFinishPicking results: [PHPickerResult]) {
        picker.dismiss(animated: true)
        guard let provider = results.first?.itemProvider,
              provider.canLoadObject(ofClass: UIImage.self) else {
            send("OnPickCancelled", "")
            return
        }
        provider.loadObject(ofClass: UIImage.self) { object, _ in
            DispatchQueue.main.async { deliver(object as? UIImage) }
        }
    }

    func imagePickerController(_ picker: UIImagePickerController,
                               didFinishPickingMediaWithInfo info: [UIImagePickerController.InfoKey: Any]) {
        picker.dismiss(animated: true)
        deliver(info[.originalImage] as? UIImage)
    }

    func imagePickerControllerDidCancel(_ picker: UIImagePickerController) {
        picker.dismiss(animated: true)
        send("OnPickCancelled", "")
    }
}

/// The prefix and suffix every file `deliver(_:)` writes, so purging only
/// ever touches this plugin's own leftovers in a directory the whole app
/// shares.
private let filePrefix = "catpick-"
private let fileSuffix = ".jpg"

/// Delete leftover picked photographs from the temporary directory.
///
/// Task 50-photo/13: the iOS counterpart of `CatPicker.purge()` /
/// `CatPickActivity.onCreate`, which empties Android's cache directory before
/// every pick "because the photograph must not outlive the run". iOS wrote to
/// `NSTemporaryDirectory()` with a fresh UUID name per pick and never revisited
/// it, so a process killed between `deliver(_:)` writing the JPEG and Unity
/// reading the path left a stranger's cat on disk indefinitely — the OS
/// reclaims `NSTemporaryDirectory()` on its own schedule, not on the next
/// launch. Called at the start of every pick, same as Android: a dead process
/// cannot clean up after itself, but the next one it starts can.
private func purge() {
    let directory = NSTemporaryDirectory()
    guard let names = try? FileManager.default.contentsOfDirectory(atPath: directory) else { return }
    for name in names where name.hasPrefix(filePrefix) && name.hasSuffix(fileSuffix) {
        try? FileManager.default.removeItem(atPath: directory + name)
    }
}

private func present(_ controller: UIViewController) {
    guard let root = UIApplication.shared.windows.first(where: { $0.isKeyWindow })?
        .rootViewController else {
        send("OnPickFailed", "no_window")
        return
    }
    root.present(controller, animated: true)
}

@_cdecl("CatPicker_openGallery")
public func CatPicker_openGallery() {
    DispatchQueue.main.async {
        purge()
        var configuration = PHPickerConfiguration()
        configuration.filter = .images
        configuration.selectionLimit = 1
        let picker = PHPickerViewController(configuration: configuration)
        picker.delegate = PickerDelegate.shared
        present(picker)
    }
}

@_cdecl("CatPicker_openCamera")
public func CatPicker_openCamera() {
    DispatchQueue.main.async {
        purge()
        guard UIImagePickerController.isSourceTypeAvailable(.camera) else {
            // The simulator has no camera, and neither do some iPads. The
            // shell shows the gallery path instead of a dead button.
            send("OnPickUnavailable", "camera")
            return
        }
        let picker = UIImagePickerController()
        picker.sourceType = .camera
        picker.delegate = PickerDelegate.shared
        present(picker)
    }
}

/// Whether a camera exists at all, so the shell can hide the button rather
/// than let the player press it and get nothing.
@_cdecl("CatPicker_hasCamera")
public func CatPicker_hasCamera() -> Bool {
    UIImagePickerController.isSourceTypeAvailable(.camera)
}
#endif
