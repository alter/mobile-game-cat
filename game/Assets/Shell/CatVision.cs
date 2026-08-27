using System;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;

namespace CatShelter.Shell
{
    /// <summary>Where an animal sits, in pixels, origin top-left.</summary>
    [Serializable]
    public struct AnimalBox
    {
        public string identifier;   // "Cat" or "Dog" — Vision knows no other animal
        public float confidence;
        public int x, y, width, height;

        public bool IsCat => string.Equals(identifier, "Cat", StringComparison.OrdinalIgnoreCase);
    }

    [Serializable]
    public struct VisionAnswer
    {
        public bool ok;
        public string error;
        public int imageWidth, imageHeight;
        public AnimalBox[] detections;

        public bool FoundAnimal => ok && detections != null && detections.Length > 0;

        // 50-photo/05 VERIFY item: "found nothing" and "could not look" used
        // to be the same thing to every caller — both left FoundAnimal false,
        // and nothing read `ok` or `error`. They are different: this is not a
        // judgement about the photo, and the player should not be told her
        // cat wasn't recognised when the truth is the device couldn't run
        // Vision at all (decode failure, empty bytes, not iOS, or
        // handler.perform threw).
        public bool Failed => !ok;

        // 50-photo/06 VERIFY item 2: this used to be detections[0] on the
        // strength of a comment ("plugin sorts by confidence") — true in
        // Plugins/iOS/CatVision.swift today, checked nowhere. Picking the max
        // here means correctness no longer depends on the Swift ordering, or
        // on remembering to re-check it if that file ever changes.
        public AnimalBox Best => detections.OrderByDescending(d => d.confidence).First();
    }

    /// <summary>
    /// Task 50-photo/05: stage one of the photo pipeline, on device and free.
    /// Wraps Assets/Plugins/iOS/CatVision.swift.
    ///
    /// Outside iOS — the editor, a desktop build — there is no Vision, so every
    /// call answers "not available" rather than throwing. The capture screen
    /// has to work in the editor while it is being built.
    /// </summary>
    public static class CatVision
    {
        public static bool Available =>
            Application.platform == RuntimePlatform.IPhonePlayer;

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern IntPtr CatVision_recognise(byte[] bytes, int length, int orientation);

        [DllImport("__Internal")]
        private static extern void CatVision_free(IntPtr text);
#endif

        /// <summary>
        /// Recognise animals in an encoded image.
        /// </summary>
        /// <param name="orientation">
        /// A CGImagePropertyOrientation value; 0 reads it from the file's own
        /// metadata. Vision keeps no orientation of its own and mis-detects
        /// silently when it is wrong, which is why this is not optional in the
        /// native call.
        /// </param>
        public static VisionAnswer Recognise(byte[] imageBytes, int orientation = 0)
        {
            if (imageBytes == null || imageBytes.Length == 0)
                return new VisionAnswer { ok = false, error = "empty image data" };

#if UNITY_IOS && !UNITY_EDITOR
            var pointer = CatVision_recognise(imageBytes, imageBytes.Length, orientation);
            if (pointer == IntPtr.Zero)
                return new VisionAnswer { ok = false, error = "plugin returned nothing" };
            try
            {
                var json = Marshal.PtrToStringAnsi(pointer);
                return JsonUtility.FromJson<VisionAnswer>(json);
            }
            finally
            {
                // Swift allocated this with strdup; the marshaller will not.
                CatVision_free(pointer);
            }
#else
            return new VisionAnswer { ok = false, error = "vision is iOS-only" };
#endif
        }
    }
}
