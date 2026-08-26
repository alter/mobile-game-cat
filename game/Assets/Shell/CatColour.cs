using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace CatShelter.Shell
{
    /// <summary>
    /// Task 50-photo/11: the one trait a phone can read without the Worker.
    /// Wraps Assets/Plugins/iOS/CatColour.swift.
    ///
    /// Base colour only. Nothing on device reads a coat pattern — Apple's
    /// classifier has 1303 categories and not one is a tabby
    /// (knowledge/ios/06-on-device-coat-traits.md) — so the caller forces
    /// pattern=solid rather than guessing at one.
    /// </summary>
    public static class CatColour
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern IntPtr CatColour_estimate(byte[] bytes, int length);

        [DllImport("__Internal")]
        private static extern void CatColour_free(IntPtr text);
#endif

        /// <summary>The base colour, or null when it cannot be read.</summary>
        public static string Estimate(byte[] croppedJpeg)
        {
            if (croppedJpeg == null || croppedJpeg.Length == 0) return null;
#if UNITY_IOS && !UNITY_EDITOR
            var pointer = CatColour_estimate(croppedJpeg, croppedJpeg.Length);
            if (pointer == IntPtr.Zero) return null;
            try { return Marshal.PtrToStringAnsi(pointer); }
            finally { CatColour_free(pointer); }
#else
            return null;
#endif
        }
    }
}
