using System;
using System.Runtime.InteropServices;
using CatShelter.Core;
using UnityEngine;

namespace CatShelter.Shell
{
    /// <summary>
    /// Task 50-photo/07: shrink an accepted photo to what the model needs and
    /// no more. Wraps Assets/Plugins/iOS/CatPhoto.swift and, on Android,
    /// Assets/Plugins/Android/CatPicker.androidlib's CatPhoto.java — a port of
    /// the Swift one, same crop rule, same ceiling, same quality ladder.
    ///
    /// 512×512 because image cost is ceil(w/28)·ceil(h/28) visual tokens — 361
    /// at this size — while accuracy falls off below about 200 px on a side.
    /// Under 200 KB before base64, which inflates by roughly a third.
    /// </summary>
    public static class CatPhoto
    {
        public const int Side = 512;
        public const int MaxBytes = 200 * 1024;

#if UNITY_ANDROID && !UNITY_EDITOR
        private const string AndroidPlugin = "com.catshelter.picker.CatPhoto";
#endif

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern IntPtr CatPhoto_prepare(byte[] bytes, int length,
            int boxX, int boxY, int boxWidth, int boxHeight, out int outLength);

        [DllImport("__Internal")]
        private static extern void CatPhoto_free(IntPtr buffer);
#endif

        /// <summary>
        /// Crop to the animal, square off and scale to 512×512, re-encode as
        /// JPEG. Pass the box from <see cref="CatVision"/>; a default box means
        /// "use the whole image". Returns null when the photo cannot be decoded.
        /// </summary>
        public static byte[] Prepare(byte[] imageBytes, AnimalBox box = default)
        {
            if (imageBytes == null || imageBytes.Length == 0)
                return null;

#if UNITY_IOS && !UNITY_EDITOR
            var pointer = CatPhoto_prepare(imageBytes, imageBytes.Length,
                box.x, box.y, box.width, box.height, out var length);
            if (pointer == IntPtr.Zero || length <= 0)
                return null;
            try
            {
                var result = new byte[length];
                Marshal.Copy(pointer, result, 0, length);
                return result;
            }
            finally
            {
                CatPhoto_free(pointer);
            }
#elif UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                // sbyte, not byte, both ways. Java's byte is signed and
                // Unity's marshaller says so out loud: an emulator run of the
                // byte[] version logged "AndroidJNIHelper.GetSignature: using
                // Byte parameters is obsolete, use SByte parameters instead"
                // and "converting Byte array is obsolete" — four warnings with
                // stack traces per photograph, in a project whose checking is
                // done by reading the device log. The bits are identical; only
                // the sign of the C# element type differs, so Buffer.BlockCopy
                // reinterprets rather than converts.
                var input = new sbyte[imageBytes.Length];
                Buffer.BlockCopy(imageBytes, 0, input, 0, imageBytes.Length);

                using var plugin = new AndroidJavaClass(AndroidPlugin);
                // The explicit object[] is load-bearing, for the reason
                // Share.cs spells out: CallStatic's signature is
                // CallStatic(string, params object[]), and an array handed in
                // bare is splayed into the params list — one Java argument per
                // byte — so the call never resolves against
                // prepare(byte[], int, int, int, int).
                var prepared = plugin.CallStatic<sbyte[]>("prepare", new object[]
                {
                    input, box.x, box.y, box.width, box.height,
                });
                if (prepared == null || prepared.Length == 0)
                    return null;

                var result = new byte[prepared.Length];
                Buffer.BlockCopy(prepared, 0, result, 0, prepared.Length);
                Debug.Log($"[CatPhoto] prepared {result.Length} bytes " +
                          $"from {imageBytes.Length} (cap {MaxBytes})");
                return result;
            }
            catch (Exception e)
            {
                // Same answer as a decode failure, and the caller already has
                // words for it: the photo was fine, we were not.
                Debug.LogWarning($"[CatPhoto] android prepare failed: {e.Message}");
                return null;
            }
#else
            return null;
#endif
        }
    }
}
