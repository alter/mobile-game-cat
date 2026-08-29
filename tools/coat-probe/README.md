# coat-probe — the masks the coat reader is scored against

`Core/CoatReader` reads a coat from two things: the pixels of the 512×512 crop
and the subject mask over them. On a phone the mask comes from ML Kit or from
Vision. On a Mac it comes from here, so that the reader can be scored against
real photographs without a device build and so that the accuracy figures in any
report are re-derivable by anyone with a Mac in two commands.

## The two commands

```sh
xcrun swiftc -O game/Assets/Plugins/iOS/CatPhoto.swift \
             tools/coat-probe/coat-dump.swift -o /tmp/coat-dump

/tmp/coat-dump tmp/coat-dumps \
  tmp/test_cat_photo/{1,2,3}-2.jpg fixtures/reference-photos/cat_*.jpg

dotnet run --project tools/coat-score
```

The first writes one `.coat` dump and one `.preview.png` per photograph into
`tmp/coat-dumps` — the preview tints everything outside the mask magenta, so a
person can see at a glance whether the segmenter found the cat or the cushion.
The second runs the shipped reader over the dumps and scores it against
`fixtures/reference-photos/traits-labels.json`.

Dumps are not committed. They are a megabyte each and they are a function of
photographs that are already in the repository.

## Why it mirrors the pipeline rather than the photograph

`CaptureScreen` recognises the animal on the ORIGINAL photo, crops to the box
with `CatPhoto.Prepare`, and re-runs recognition on the 512×512 crop to pick
which foreground instance is the cat. This tool does the same, in the same
order, and re-decodes the JPEG rather than reusing the in-memory image — so the
JPEG's own artefacts are in what the reader measures, exactly as on a phone.
`CatPhoto.prepare` is called directly; the four mask helpers are copied from
`Plugins/iOS/CatMarks.swift` because they are `private` there.

## The dump format

Little-endian throughout.

| offset | bytes | meaning |
|---|---|---|
| 0 | 4 | ASCII `COAT` |
| 4 | 4 | int32 width (512) |
| 8 | 4 | int32 height (512) |
| 12 | 4 | int32 hasMask, 1 or 0 |
| 16 | w·h·3 | sRGB, row-major, origin **top-left** |
| 16 + w·h·3 | w·h | mask confidence 0…255, same grid, same origin |

Origin top-left is the same convention `CatSilhouette.mask` uses and the
opposite of `Texture2D.GetPixels32`; `Shell/CatCoat.cs` is where the flip
happens on the device, and getting it wrong measures a cat upside down without
throwing.
