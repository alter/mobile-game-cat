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
        send("OnPickFailed", "could not read the picked image")
        return
    }
    let path = NSTemporaryDirectory() + "catpick-\(UUID().uuidString).jpg"
    do {
        try jpeg.write(to: URL(fileURLWithPath: path))
        send("OnPicked", path)
    } catch {
        send("OnPickFailed", "could not save the picked image: \(error.localizedDescription)")
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

private func present(_ controller: UIViewController) {
    guard let root = UIApplication.shared.windows.first(where: { $0.isKeyWindow })?
        .rootViewController else {
        send("OnPickFailed", "no window to present from")
        return
    }
    root.present(controller, animated: true)
}

@_cdecl("CatPicker_openGallery")
public func CatPicker_openGallery() {
    DispatchQueue.main.async {
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
