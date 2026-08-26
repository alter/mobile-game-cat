# Built, 2026-08-26

`View/CaptureScreen.cs` owns the pipeline; `Shell/CatPicker.cs` +
`Plugins/iOS/CatPicker.swift` are the two ways a photo gets in.

## One permission, not two

Gallery uses **`PHPickerViewController`**, which runs outside the app's process
and needs **no photo-library permission at all** as long as the `PHAsset` is
never requested. Camera uses `UIImagePickerController`, which does require
`NSCameraUsageDescription` — there is no way around that one, and its text is
written for a person: *"To photograph your cat, so the kitten in the game can
take her colours."*

Confirmed on the built app: `plutil -p` finds `NSCameraUsageDescription` and
**no** `NSPhotoLibraryUsageDescription`. That is one fewer prompt between a
player and the screen the whole concept rests on.

## All four messages, driven through a real iOS build

Vision cannot run in the simulator (`05-vision-plugin`), so only the "no cat"
branch would ever be reachable. A second line in the debug flag file stubs the
Vision answer, and the four branches then come out as:

```
fake none 0    -> "No cat in this one. Try a photo where she fills more of the frame."
fake Dog 0.73  -> "That looks like a dog. Lovely, but this shelter is for cats."
fake Cat 0.45  -> "A cat, but too blurry to copy her colours. One more, holding still?"
fake Cat 0.80  -> "Got her."  + accepted a 73096-byte photo
```

The last line matters twice: the accepted branch really did run the crop inside
the iOS build, and produced 73 KB — well under the 200 KB cap. `CatPhoto` works
on device where `CatVision` cannot.

## Against the VERIFY list

- **3 — each of the four messages renders when its result is injected: met**,
  by the run above, in a real build rather than a PlayMode test.
- **1 and 2 — camera and gallery paths reach the Worker call: partly.** Both
  paths run to the same `Handle`, and `Handle` runs end to end. What does not
  exist is the Worker (`02-traits-worker`, blocked on the spend cap), so the
  pipeline ends at `OnAccepted` with the prepared JPEG — the exact point the
  request will be made from. And neither picker can be opened without a finger:
  `simctl` cannot tap, so the pickers themselves are unexercised.

## Two things a device has to settle

- The camera button is shown when `isSourceTypeAvailable(.camera)` says there
  is a camera. On this simulator it says yes, so the button appears; whether
  that is the simulator's virtual camera or a wrong answer is **not
  established**. On hardware it is moot, and on an iPad without a camera it
  wants checking.
- Nothing here has been tapped by a person. The screen renders and the pipeline
  runs; how it feels to use is `14-testflight`.

## Fixed while building

- `CatPicker_hasCamera` came back as garbage: Swift's `Bool` is one byte and
  the marshaller was reading four. `[return: MarshalAs(UnmanagedType.I1)]`.
- The screen drew dark text on an unpainted root, which the panel showed as
  black. It now paints its own background and colours its own text.
