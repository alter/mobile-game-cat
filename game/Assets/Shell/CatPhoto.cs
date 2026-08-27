using System;
using System.Runtime.InteropServices;
using CatShelter.Core;
using UnityEngine;

namespace CatShelter.Shell
{
    /// <summary>
    /// Task 50-photo/07: shrink an accepted photo to what the model needs and
    /// no more. Wraps Assets/Plugins/iOS/CatPhoto.swift.
    ///
    /// 512×512 because image cost is ceil(w/28)·ceil(h/28) visual tokens — 361
    /// at this size — while accuracy falls off below about 200 px on a side.
    /// Under 200 KB before base64, which inflates by roughly a third.
    /// </summary>
    public static class CatPhoto
    {
        public const int Side = 512;
        public const int MaxBytes = 200 * 1024;

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
#else
            return null;
#endif
        }
    }
}
