using System;
using System.Runtime.InteropServices;
using System.Text;
using CatShelter.Core;
using UnityEngine;

namespace CatShelter.Shell
{
    /// <summary>
    /// Task 50-photo/05: stage one of the photo pipeline, on device and free.
    /// Wraps <c>Assets/Plugins/iOS/CatVision.swift</c> and
    /// <c>Assets/Plugins/Android/CatVision.androidlib</c>.
    ///
    /// <see cref="AnimalBox"/> and <see cref="VisionAnswer"/> moved to
    /// <c>Core/VisionAnswer.cs</c> (50-photo/06 VERIFY): they were plain data
    /// with no engine reference, only living here because nothing forced the
    /// question. What stays is exactly what cannot move — the
    /// <c>DllImport</c>, the pointer marshalling, <c>AndroidJavaClass</c>, and
    /// <c>Application.platform</c>.
    ///
    /// <para><b>Two platforms, one answer.</b> iOS gets species, confidence and
    /// a box from a single <c>VNRecognizeAnimalsRequest</c>. Android has no
    /// animal recogniser at all, so the plug-in there stitches two ML Kit APIs
    /// together — subject segmentation for "where", image labelling for
    /// "what". The JSON both sides emit has the same field names, so
    /// <see cref="VisionAnswer"/> deserialises either without knowing which
    /// produced it, and nothing above this file changed when Android
    /// arrived.</para>
    ///
    /// <para>Outside iOS and Android — the editor, a desktop build — there is
    /// no recogniser, so every call answers "not available" rather than
    /// throwing. The capture screen has to work in the editor while it is being
    /// built.</para>
    /// </summary>
    public static class CatVision
    {
        public static bool Available =>
            Application.platform == RuntimePlatform.IPhonePlayer ||
            Application.platform == RuntimePlatform.Android;

        /// <summary>
        /// Longest side of the mask <see cref="Silhouette"/> asks for. 512 on
        /// the long side of a 4:3 photo is a 512×384 byte array — 196 KB across
        /// the JNI boundary, and finer than anything a mark measurement needs,
        /// where the smallest disc sampled is a few pixels across.
        /// </summary>
        public const int DefaultMaskSide = 512;

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
        /// A CGImagePropertyOrientation value, which is the same 1–8 as the
        /// EXIF Orientation tag; 0 reads it from the file's own metadata.
        /// Neither Vision nor ML Kit keeps an orientation of its own and both
        /// mis-detect silently when it is wrong, which is why this is not
        /// optional in either native call.
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
#elif UNITY_ANDROID && !UNITY_EDITOR
            // maskSide 0 skips segmentation entirely, so a caller that only
            // wants the species does not wait on Google Play services.
            return AndroidCall(imageBytes, orientation, 0).answer;
#else
            return new VisionAnswer { ok = false, error = "vision is iOS and Android only" };
#endif
        }

        /// <summary>
        /// Recognise, and cut out, the animal in an encoded image.
        ///
        /// <para>The mask is what a later step measures her markings against —
        /// on Android there is no animal skeleton to lean on, so the mask
        /// carries the whole job. It covers the WHOLE image, one byte per
        /// pixel, 0 outside her and up to 255 inside, at
        /// <see cref="CatSilhouette.maskWidth"/> ×
        /// <see cref="CatSilhouette.maskHeight"/> — so pixel (x, y) of the
        /// photograph is
        /// <c>mask[y * maskHeight / imageHeight * maskWidth + x * maskWidth / imageWidth]</c>.
        /// </para>
        ///
        /// <para>A mask is not guaranteed. It comes from an optional Google
        /// Play services module, and <see cref="CatSilhouette.HasMask"/> is
        /// false when that module is absent or has not downloaded yet — the
        /// species answer in <see cref="CatSilhouette.answer"/> is still good
        /// in that case, and the player still gets her cat. Do not treat a
        /// missing mask as a rejected photograph.</para>
        /// </summary>
        public static CatSilhouette Silhouette(byte[] imageBytes, int orientation = 0,
                                               int maskSide = DefaultMaskSide)
        {
            if (imageBytes == null || imageBytes.Length == 0)
            {
                return new CatSilhouette
                {
                    answer = new VisionAnswer { ok = false, error = "empty image data" }
                };
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            return AndroidCall(imageBytes, orientation, Mathf.Max(1, maskSide));
#else
            // iOS has its own mask, from VNGenerateForegroundInstanceMaskRequest
            // through Plugins/iOS/CatMarks.swift, and it arrives already
            // measured rather than as pixels. Nothing here duplicates it.
            return new CatSilhouette { answer = Recognise(imageBytes, orientation) };
#endif
        }

        /// <summary>
        /// Ask Google Play services to fetch the subject-segmentation module
        /// now, so the first photograph a player picks does not pay for the
        /// download. Returns <c>ready</c>, <c>requested</c> or
        /// <c>unavailable</c>; off Android, always <c>unavailable</c>.
        ///
        /// <para>Cheap and safe to call from the capture screen's Start. It
        /// does not block on the download itself.</para>
        /// </summary>
        public static string Prepare()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                using var plugin = new AndroidJavaClass("com.catshelter.vision.CatVision");
                return plugin.CallStatic<string>("prepare", activity);
            }
            catch (Exception)
            {
                return "unavailable";
            }
#else
            return "unavailable";
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        /// <summary>
        /// One JNI crossing for both answers. The plug-in packs its JSON and
        /// the mask into a single byte[] rather than returning a string and
        /// then a second array, because two calls would mean the plug-in
        /// holding a photograph's mask in a static field between them — state
        /// with a player's photo in it, which this pipeline does not keep.
        ///
        /// Layout, from Plugins/Android/.../Packet.java:
        ///   "CVS1" | int32 big-endian json length | json UTF-8 | mask bytes
        /// </summary>
        private static CatSilhouette AndroidCall(byte[] imageBytes, int orientation, int maskSide)
        {
            byte[] packed;
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                using var plugin = new AndroidJavaClass("com.catshelter.vision.CatVision");
                packed = plugin.CallStatic<byte[]>(
                    "analyse", activity, imageBytes, orientation, maskSide);
            }
            catch (Exception error)
            {
                // A missing class or a JNI failure is "could not look", which
                // VisionAnswer.Failed exists to tell apart from "looked and
                // found nothing". The exception type only — never the message,
                // which could name a file.
                return new CatSilhouette
                {
                    answer = new VisionAnswer
                    {
                        ok = false,
                        error = "vision failed: " + error.GetType().Name
                    }
                };
            }

            return Unpack(packed);
        }
#endif

        /// <summary>
        /// Reads the packed reply. Internal rather than private so
        /// <c>tools/tests</c> and an edit-mode test can feed it a packet
        /// without an emulator; there is nothing platform-specific in here.
        /// </summary>
        internal static CatSilhouette Unpack(byte[] packed)
        {
            var failed = new CatSilhouette
            {
                answer = new VisionAnswer { ok = false, error = "plugin returned nothing" }
            };
            if (packed == null || packed.Length < 8)
                return failed;
            if (packed[0] != 'C' || packed[1] != 'V' || packed[2] != 'S' || packed[3] != '1')
                return failed;

            int length = (packed[4] << 24) | (packed[5] << 16) | (packed[6] << 8) | packed[7];
            if (length < 0 || 8 + length > packed.Length)
                return failed;

            var result = new CatSilhouette
            {
                answer = JsonUtility.FromJson<VisionAnswer>(
                    Encoding.UTF8.GetString(packed, 8, length))
            };

            var geometry = JsonUtility.FromJson<MaskGeometry>(
                Encoding.UTF8.GetString(packed, 8, length));
            int maskBytes = packed.Length - 8 - length;
            if (maskBytes > 0 && maskBytes == geometry.maskWidth * geometry.maskHeight)
            {
                result.mask = new byte[maskBytes];
                Buffer.BlockCopy(packed, 8 + length, result.mask, 0, maskBytes);
                result.maskWidth = geometry.maskWidth;
                result.maskHeight = geometry.maskHeight;
                result.maskCoverage = geometry.maskCoverage;
                result.maskSource = geometry.maskSource;
            }
            result.rung = geometry.rung;
            return result;
        }

        /// <summary>
        /// The mask fields of the Android answer. A second type rather than
        /// four more fields on <see cref="VisionAnswer"/> because
        /// <c>VisionAnswer</c> lives in <c>Core</c>, is what iOS also returns,
        /// and should not grow fields only one platform ever fills. If a
        /// silhouette ever reaches gameplay logic this is the type that moves
        /// to <c>Core</c> beside it.
        /// </summary>
        [Serializable]
        private struct MaskGeometry
        {
            public int maskWidth, maskHeight;
            public float maskCoverage;
            public string maskSource;
            public string rung;
        }
    }

    /// <summary>
    /// What <see cref="CatVision.Silhouette"/> gives back: the same answer
    /// <see cref="CatVision.Recognise"/> would have given, plus her shape.
    /// </summary>
    public struct CatSilhouette
    {
        public VisionAnswer answer;

        /// <summary>
        /// One byte per pixel over the WHOLE image, 0 outside her, up to 255
        /// inside. Null when no mask could be made.
        /// </summary>
        public byte[] mask;

        public int maskWidth, maskHeight;

        /// <summary>Share of mask pixels at or above half confidence.</summary>
        public float maskCoverage;

        /// <summary><c>subject</c>, <c>subject-unlabelled</c> or <c>none</c>.</summary>
        public string maskSource;

        /// <summary>
        /// Which rung answered: <c>subject+label</c> (segmentation and
        /// labelling both ran), <c>label</c> (whole-frame labelling only, no
        /// mask), or <c>none</c>. Worth recording in analytics — it is the one
        /// number that says how many players' devices actually have the
        /// segmentation module.
        /// </summary>
        public string rung;

        public bool HasMask => mask != null && maskWidth > 0 && maskHeight > 0;
    }
}
