import Foundation
#if os(iOS)
import UIKit

// Task 60-shell-build/10: the small per-move feedback that makes ten minutes of
// tidying feel good rather than mechanical.
//
// UIFeedbackGenerator, not Core Haptics: the taps here are the two stock
// patterns the system already tunes per device, and Core Haptics would mean
// authoring waveforms and handling engine restarts for no gain. Generators are
// kept alive and prepared, because a generator created at the moment of the tap
// arrives late enough to feel disconnected from it.
//
// Every call is a no-op on a device without a Taptic Engine, and iOS ignores it
// silently — nothing here needs a capability check.

private enum Haptics {
    static let light = UIImpactFeedbackGenerator(style: .light)
    static let success = UINotificationFeedbackGenerator()
}

/// A light tap: one item placed on the shelf.
@_cdecl("CatHaptics_place")
public func CatHaptics_place() {
    DispatchQueue.main.async {
        Haptics.light.impactOccurred()
        Haptics.light.prepare()
    }
}

/// A distinct, stronger cue: three of a kind matched and left the shelf.
@_cdecl("CatHaptics_match")
public func CatHaptics_match() {
    DispatchQueue.main.async {
        Haptics.success.notificationOccurred(.success)
        Haptics.success.prepare()
    }
}

/// Warm the generators up. Without this the first tap of a session lags by
/// enough to feel unrelated to the tap that caused it.
@_cdecl("CatHaptics_prepare")
public func CatHaptics_prepare() {
    DispatchQueue.main.async {
        Haptics.light.prepare()
        Haptics.success.prepare()
    }
}
#endif
