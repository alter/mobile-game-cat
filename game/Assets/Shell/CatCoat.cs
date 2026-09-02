using System;
using System.Collections;
using System.Diagnostics;
using CatShelter.Core;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace CatShelter.Shell
{
    /// <summary>
    /// Task 60-coat/01: read the coat off the player's photograph, on the
    /// device, free, with no network and no model.
    ///
    /// <para>This file is the plumbing and nothing else — decode the crop, ask
    /// <see cref="CatVision.Silhouette"/> which pixels are the cat, hand both
    /// to <see cref="CoatReader"/>. Every judgement lives in
    /// <c>Core/CoatReader.cs</c>, where <c>dotnet test</c> can reach it and
    /// where a threshold can move without a device build. Compare
    /// <see cref="CatMarks"/>, which draws the same line in the same place for
    /// the same reason.</para>
    ///
    /// <para><b>What it replaces.</b> <see cref="CatColour"/> takes the
    /// arithmetic mean of the central half of the frame, background included,
    /// and names the nearest of six colours. That is one crude number and it
    /// was the only trait the offline path ever read: pattern was always
    /// <c>solid</c> and fur was always <c>short</c>, so three cats
    /// photographed in one room came out as three identical kittens.
    /// <see cref="CatColour"/> is still here and still called — it is the
    /// answer when there is no mask, which is a real case on an Android phone
    /// whose segmentation module has not downloaded yet.</para>
    ///
    /// <para><b>Never an error screen.</b> Every path returns a reading, an
    /// empty reading is legal, and a null trait means the caller keeps what it
    /// had. Nothing in here throws.</para>
    /// </summary>
    public static class CatCoat
    {
        /// <summary>
        /// Read the coat from the 512×512 JPEG <see cref="CatPhoto.Prepare"/>
        /// makes, a frame at a time, with the two long steps on threads of
        /// their own. This is the form the capture screen uses; drive it with
        /// <c>yield return CatCoat.ReadOverFrames(jpeg, r => coat = r)</c>.
        ///
        /// <para>The whole of 60-shell-build/19 is in the shape of this method.
        /// Step one is <see cref="CatVision.Silhouette"/>, which on Android is
        /// a JNI call with a 30-second ceiling on the far side of it; step
        /// three is <see cref="CoatReader"/> over a quarter of a million
        /// pixels. Neither belongs on the main thread and neither may leave it:
        /// step two decodes the crop, and <see cref="Texture2D"/> exists only
        /// on the main thread. So the work is cut where the engine forces it to
        /// be cut rather than where it would read most tidily — off, on for one
        /// frame, off again.</para>
        ///
        /// <para>Frames go on being drawn between the steps, which is the
        /// point: the capture screen's bar rides the panel's own scheduler and
        /// moves whenever a frame is drawn. Before this it did not move at all
        /// between "Copying the colours…" appearing and the cat arriving,
        /// because the mask call alone held the main thread for 232 ms on an
        /// emulator and for up to the plug-in's 30-second ceiling on a phone
        /// whose Play services module is still coming down.</para>
        /// </summary>
        /// <param name="orientation">A CGImagePropertyOrientation value; 0
        /// reads it from the file's own metadata. A prepared crop is already
        /// upright, so 0 is right for the live path.</param>
        /// <param name="then">Called exactly once, with the reading.</param>
        public static IEnumerator ReadOverFrames(byte[] croppedJpeg,
                                                 Action<CoatReading> then,
                                                 int orientation = 0)
        {
            if (croppedJpeg == null || croppedJpeg.Length == 0)
            {
                then(new CoatReading { Note = "no photo" });
                yield break;
            }

            var clock = Stopwatch.StartNew();
            var look = OffMain.Run(() => Look(croppedJpeg, orientation), "coat mask");
            while (!look.Done) yield return null;
            var maskMs = clock.ElapsedMilliseconds;

            if (!Decode(croppedJpeg, look.Value, out var pixels, out var early))
            {
                then(early);
                yield break;
            }

            var read = OffMain.Run(
                () => Measure(pixels, look.Value, maskMs, clock), "coat");
            while (!read.Done) yield return null;
            then(read.Value);
        }

        /// <summary>
        /// The same three steps in a row, blocking, on whatever thread calls.
        /// For anything that is not a coroutine — the editor, a tool. The
        /// shipping photo path uses <see cref="ReadOverFrames"/>; this is kept
        /// because the steps only mean anything in this order, and a caller
        /// with no frames to spend should not have to work that out again.
        /// </summary>
        /// <returns>Never null.</returns>
        public static CoatReading Read(byte[] croppedJpeg, int orientation = 0)
        {
            if (croppedJpeg == null || croppedJpeg.Length == 0)
                return new CoatReading { Note = "no photo" };

            var clock = Stopwatch.StartNew();
            var silhouette = Look(croppedJpeg, orientation);
            var maskMs = clock.ElapsedMilliseconds;
            return Decode(croppedJpeg, silhouette, out var pixels, out var early)
                ? Measure(pixels, silhouette, maskMs, clock)
                : early;
        }

        /// <summary>
        /// Step one, off the main thread: ask the plug-in which pixels are the
        /// cat. Nothing in here touches the engine — a JNI call on Android, a
        /// <c>DllImport</c> on iOS — and <see cref="OffMain"/> has attached the
        /// thread to the VM before it runs.
        /// </summary>
        private static CatSilhouette Look(byte[] croppedJpeg, int orientation)
        {
            try
            {
                return CatVision.Silhouette(croppedJpeg, orientation);
            }
            catch (Exception e)
            {
                // The type only, never the message: a native error string
                // could name a file. The same rule CatVision.AndroidCall
                // follows.
                Debug.LogWarning($"[CatCoat] silhouette failed: {e.GetType().Name}");
                return new CatSilhouette
                {
                    answer = new VisionAnswer { ok = false, error = Threw },
                };
            }
        }

        /// <summary>
        /// The marker <see cref="Look"/> leaves on an answer it never got, so
        /// <see cref="Decode"/> can tell "the plug-in fell over" from "the
        /// plug-in has no mask for this photograph". The two readings say
        /// different things to whoever reads the log and used to be told apart
        /// by a <c>catch</c> that a worker thread cannot hand back.
        /// </summary>
        private const string Threw = "silhouette threw";

        /// <summary>
        /// Step two, main thread, one frame: the crop's own pixels. All of it
        /// is <see cref="Texture2D"/>, which is why the step exists — decoding
        /// a 512×512 JPEG cannot be moved off the main thread at all, so this
        /// is the one place on this path where the main thread does real work.
        /// Measured at 39 ms on the emulator, against 232 ms for the mask call
        /// that now runs beside it.
        /// </summary>
        /// <returns>
        /// False when there is nothing to measure, with the reading to hand
        /// back in <paramref name="early"/>.
        /// </returns>
        private static bool Decode(byte[] croppedJpeg, CatSilhouette silhouette,
                                   out CoatPixels pixels, out CoatReading early)
        {
            pixels = default;
            early = null;

            // Read here and carried, not read where it is used: Application's
            // properties, persistentDataPath among them, are main-thread only,
            // and Dump runs in step three on a worker.
            pixels.savePath = Application.persistentDataPath;

            if (silhouette.answer.error == Threw)
            {
                early = new CoatReading { Note = "the mask could not be made" };
                return false;
            }

            // Which subject the mask came from, and how many there were to
            // choose between. Cheap, and it is the difference between "the
            // segmenter merged the cat with the armchair" and "the segmenter
            // offered us the cat and we took the armchair" — two faults that
            // look identical in a coat reading and need opposite fixes.
            var found = silhouette.answer.detections?.Length ?? 0;
            Debug.Log($"[CatCoat] mask from {silhouette.maskSource ?? "none"} " +
                      $"(rung {silhouette.rung ?? "none"}), {found} subject(s) offered, " +
                      $"coverage {silhouette.maskCoverage:P0}");

            if (!silhouette.HasMask)
            {
                Debug.Log($"[CatCoat] no mask (rung {silhouette.rung ?? "none"}), " +
                          "so the coat is not read; the colour estimate stands");
                early = new CoatReading
                {
                    Note = "no mask, so nothing here can tell her coat from the sofa",
                };
                return false;
            }

            var decodeClock = Stopwatch.StartNew();
            Texture2D texture = null;
            try
            {
                // linear:false — the bytes are sRGB and must stay that way, and
                // this project renders in Gamma, so GetPixels32 hands back the
                // stored bytes with no conversion. CoatPalette's anchors are
                // sRGB, so the comparison is like for like. Same argument as
                // CatColour.EstimateHere, which is the estimate this extends.
                texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
                if (!texture.LoadImage(croppedJpeg, markNonReadable: false))
                {
                    Debug.LogWarning("[CatCoat] could not decode the crop");
                    early = new CoatReading { Note = "the crop would not decode" };
                    return false;
                }

                var width = texture.width;
                var height = texture.height;
                if (width <= 0 || height <= 0)
                {
                    early = new CoatReading { Note = "the crop is empty" };
                    return false;
                }

                var raw = texture.GetPixels32();
                var rgb = new byte[width * height * 3];
                for (var y = 0; y < height; y++)
                {
                    // GetPixels32 is row-major from the BOTTOM, and the mask
                    // the plug-ins produce is row-major from the TOP. Getting
                    // this wrong does not throw and does not look broken — it
                    // measures the coat of a cat flipped head to tail, which on
                    // a photograph of an animal filling the frame is a plausible
                    // wrong answer, and those are the expensive kind.
                    var from = (height - 1 - y) * width;
                    var to = y * width * 3;
                    for (var x = 0; x < width; x++)
                    {
                        var pixel = raw[from + x];
                        rgb[to + x * 3] = pixel.r;
                        rgb[to + x * 3 + 1] = pixel.g;
                        rgb[to + x * 3 + 2] = pixel.b;
                    }
                }

                pixels.rgb = rgb;
                pixels.width = width;
                pixels.height = height;
                pixels.decodeMs = decodeClock.ElapsedMilliseconds;
                return true;
            }
            catch (Exception e)
            {
                // A coat that cannot be read is not an error screen: the caller
                // keeps the traits it has and the photograph itself was fine.
                Debug.LogWarning($"[CatCoat] decode failed: {e.GetType().Name}");
                early = new CoatReading { Note = "the coat could not be read" };
                return false;
            }
            finally
            {
                if (texture != null)
                {
                    if (Application.isPlaying) UnityEngine.Object.Destroy(texture);
                    else UnityEngine.Object.DestroyImmediate(texture);
                }
            }
        }

        /// <summary>
        /// Step three, off the main thread: the arithmetic.
        /// <see cref="CoatReader"/> over a quarter of a million pixels, several
        /// passes, and not one engine call in any of it — which is what makes
        /// the move legal. <see cref="Dump"/> writes from here too, which is
        /// why it is handed a path rather than asking for one.
        /// </summary>
        private static CoatReading Measure(CoatPixels pixels, CatSilhouette silhouette,
                                           long maskMs, Stopwatch clock)
        {
            try
            {
                Dump(pixels, silhouette);
                var reading = CoatReader.Read(pixels.rgb, pixels.width, pixels.height,
                                              silhouette.mask,
                                              silhouette.maskWidth, silhouette.maskHeight);
                // The three numbers this task is measured by: what the mask
                // cost, what the main thread had to keep, and the whole of it.
                Debug.Log($"[CatCoat] {reading} " +
                          $"(mask {maskMs} ms, decode {pixels.decodeMs} ms on the main " +
                          $"thread, all {clock.ElapsedMilliseconds} ms) {reading.Note}");
                return reading;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CatCoat] read failed: {e.GetType().Name}");
                return new CoatReading { Note = "the coat could not be read" };
            }
        }

        /// <summary>
        /// The crop's own pixels on the way from step two to step three, plus
        /// the one piece of engine state step three needs and cannot ask for
        /// itself. See <see cref="Decode"/> for why the path travels.
        /// </summary>
        private struct CoatPixels
        {
            public byte[] rgb;
            public int width, height;
            public long decodeMs;
            public string savePath;
        }

        /// <summary>
        /// Write the crop and the mask the DEVICE made, in the format
        /// <c>tools/coat-probe</c> writes and <c>tools/coat-score</c> reads, so
        /// that the two can be compared instead of argued about.
        ///
        /// <para>Dormant unless a file named <c>coatdump.txt</c> sits next to
        /// the save — the same idiom <c>GameBoot</c>'s <c>capture.txt</c> and
        /// <c>VisionSelfTest</c>'s <c>visiontest</c> folder already use, and for
        /// the same reason: a build a player can install must not be writing a
        /// megabyte of their cat to disk.</para>
        ///
        /// <para><b>Why it had to exist.</b> The same photograph of the same cat
        /// measures a 33% body on macOS Vision and a 52% body through ML Kit,
        /// and the coat that comes out differs with it. Without a dump there
        /// was no way to tell a reader that mis-measures a coat from a
        /// segmenter that handed it the armchair, and every fix would have been
        /// a guess. The Mac probe's format is used verbatim so that a device
        /// mask drops straight into <c>tmp/coat-dumps</c> and is scored by the
        /// tool that already scores the twenty-four photographs.</para>
        /// </summary>
        private static void Dump(CoatPixels pixels, CatSilhouette silhouette)
        {
            try
            {
                // pixels.savePath, not Application.persistentDataPath: this
                // runs on a worker thread since 60-shell-build/19, and reading
                // an Application property there is a UnityException, not a
                // slow answer. Decode read it for us on the main thread.
                if (string.IsNullOrEmpty(pixels.savePath)) return;
                byte[] rgb = pixels.rgb;
                int width = pixels.width, height = pixels.height;
                var flag = System.IO.Path.Combine(pixels.savePath, "coatdump.txt");
                if (!System.IO.File.Exists(flag)) return;

                var mask = silhouette.mask ?? new byte[0];
                var header = new byte[16];
                header[0] = (byte)'C'; header[1] = (byte)'O';
                header[2] = (byte)'A'; header[3] = (byte)'T';
                Buffer.BlockCopy(BitConverter.GetBytes(width), 0, header, 4, 4);
                Buffer.BlockCopy(BitConverter.GetBytes(height), 0, header, 8, 4);
                Buffer.BlockCopy(BitConverter.GetBytes(mask.Length > 0 ? 1 : 0),
                                 0, header, 12, 4);

                // The mask may be coarser than the image, exactly as
                // CoatReader.Read allows; resample it to the image grid on the
                // way out so the dump is self-describing and the offline tool
                // needs no second pair of dimensions.
                var square = new byte[width * height];
                if (mask.Length > 0 && silhouette.maskWidth > 0 && silhouette.maskHeight > 0)
                    for (var y = 0; y < height; y++)
                    {
                        var from = (y * silhouette.maskHeight / height) * silhouette.maskWidth;
                        var to = y * width;
                        for (var x = 0; x < width; x++)
                            square[to + x] = mask[from + x * silhouette.maskWidth / width];
                    }

                var path = System.IO.Path.Combine(pixels.savePath, "device.coat");
                using (var file = System.IO.File.Create(path))
                {
                    file.Write(header, 0, header.Length);
                    file.Write(rgb, 0, rgb.Length);
                    file.Write(square, 0, square.Length);
                }
                Debug.Log($"[CatCoat] wrote {path} ({16 + rgb.Length + square.Length} bytes)");
            }
            catch (Exception e)
            {
                // A dump that fails must never cost the player a cat.
                Debug.LogWarning($"[CatCoat] dump failed: {e.GetType().Name}");
            }
        }
    }
}
