using System;
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
        /// makes.
        /// </summary>
        /// <param name="orientation">A CGImagePropertyOrientation value; 0
        /// reads it from the file's own metadata. A prepared crop is already
        /// upright, so 0 is right for the live path.</param>
        /// <returns>Never null.</returns>
        public static CoatReading Read(byte[] croppedJpeg, int orientation = 0)
        {
            if (croppedJpeg == null || croppedJpeg.Length == 0)
                return new CoatReading { Note = "no photo" };

            var clock = Stopwatch.StartNew();
            CatSilhouette silhouette;
            try
            {
                silhouette = CatVision.Silhouette(croppedJpeg, orientation);
            }
            catch (Exception e)
            {
                // The type only, never the message: a native error string
                // could name a file. The same rule CatVision.AndroidCall
                // follows.
                Debug.LogWarning($"[CatCoat] silhouette failed: {e.GetType().Name}");
                return new CoatReading { Note = "the mask could not be made" };
            }
            var maskMs = clock.ElapsedMilliseconds;

            if (!silhouette.HasMask)
            {
                Debug.Log($"[CatCoat] no mask (rung {silhouette.rung ?? "none"}), " +
                          "so the coat is not read; the colour estimate stands");
                return new CoatReading
                {
                    Note = "no mask, so nothing here can tell her coat from the sofa",
                };
            }

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
                    return new CoatReading { Note = "the crop would not decode" };
                }

                var width = texture.width;
                var height = texture.height;
                if (width <= 0 || height <= 0)
                    return new CoatReading { Note = "the crop is empty" };

                var pixels = texture.GetPixels32();
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
                        var pixel = pixels[from + x];
                        rgb[to + x * 3] = pixel.r;
                        rgb[to + x * 3 + 1] = pixel.g;
                        rgb[to + x * 3 + 2] = pixel.b;
                    }
                }

                var reading = CoatReader.Read(rgb, width, height, silhouette.mask,
                                              silhouette.maskWidth, silhouette.maskHeight);
                Debug.Log($"[CatCoat] {reading} " +
                          $"(mask {maskMs} ms, all {clock.ElapsedMilliseconds} ms) " +
                          $"{reading.Note}");
                return reading;
            }
            catch (Exception e)
            {
                // A coat that cannot be read is not an error screen: the caller
                // keeps the traits it has and the photograph itself was fine.
                Debug.LogWarning($"[CatCoat] read failed: {e.GetType().Name}");
                return new CoatReading { Note = "the coat could not be read" };
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
    }
}
