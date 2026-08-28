using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace CatShelter.Shell
{
    /// <summary>
    /// Task 60-shell-build/15: hand one picture and one caption to the phone's
    /// own share sheet, and get out of the way.
    ///
    /// One entry point, <see cref="Image"/>. Everything above it (which card,
    /// which room, which words) is the View's business; everything below it is
    /// two native halves that do nothing but present the system sheet:
    ///
    ///   iOS      Plugins/iOS/CatShare.swift      - UIActivityViewController
    ///   Android  Plugins/Android/CatShare.androidlib - ACTION_SEND + FileProvider
    ///
    /// Deliberately fire-and-forget: no callback, no listener GameObject, no
    /// "did she actually post it" answer. CatPicker needs UnitySendMessage
    /// because the game is blocked until a photo comes back; nothing here is
    /// waiting. The sheet is the system's, the player may dismiss it, and
    /// neither store reports which target was chosen — a completion channel
    /// would carry "the sheet closed", which is not worth a second boundary.
    /// The analytics hook the old task asks for (`share_tap`) therefore fires
    /// at the tap, on the C# side, not on a native answer.
    ///
    /// On the editor and on any platform without a plugin this logs the size
    /// of what it was given and returns. It never throws: a share is the least
    /// important thing on the screen it is drawn on, and it must not be able
    /// to take the screen down with it.
    /// </summary>
    public static class Share
    {
        /// <summary>
        /// Fixed rather than unique-per-share on purpose. Both plugins write
        /// into a cache directory nothing else cleans up, and a card is only
        /// ever shared one at a time — a UUID per tap would leave one
        /// megabyte-and-a-bit behind per tap forever. Overwriting means the
        /// worst case is a single stale file.
        /// </summary>
        private const string FileName = "kitten-card.png";

        private const string AndroidPlugin = "com.catshelter.share.CatShare";

#if UNITY_IOS && !UNITY_EDITOR
        // Path rather than bytes, the same reason CatPicker.swift sends a path
        // back: a DllImport string marshals for free and a byte[] does not.
        [DllImport("__Internal")]
        private static extern void CatShare_image(string path, string text);
#endif

        /// <summary>
        /// Whether a share sheet exists to open. False in the editor and on
        /// desktop, so a caller can hide the button instead of drawing one
        /// that does nothing — the same honesty CatPicker.CameraAvailable
        /// gives the camera button.
        /// </summary>
        public static bool Available
        {
            get
            {
#if UNITY_EDITOR
                return false;
#elif UNITY_IOS || UNITY_ANDROID
                return true;
#else
                return false;
#endif
            }
        }

        /// <summary>
        /// Open the system share sheet on <paramref name="png"/>, with
        /// <paramref name="text"/> offered alongside as the caption. Composing
        /// the image is the caller's job; this class never draws anything.
        ///
        /// The caption is a suggestion, not a guarantee: every target decides
        /// for itself what it takes. Instagram's feed composer keeps the
        /// picture and drops the words; Telegram, WhatsApp and Mail keep both.
        /// Nothing here can change that, and nothing here should try.
        /// </summary>
        /// <param name="png">Encoded PNG bytes. 1080x1080 — see this task's
        /// NOTES.md for why that number.</param>
        /// <param name="text">Caption, already in the player's language.
        /// May be empty; never null-checked into a sentence here, because no
        /// player-visible words are built in this file.</param>
        public static void Image(byte[] png, string text)
        {
            if (png == null || png.Length == 0)
            {
                Debug.LogWarning("[Share] nothing to share: empty png");
                return;
            }

            text ??= string.Empty;

#if UNITY_IOS && !UNITY_EDITOR
            string path;
            try
            {
                path = Path.Combine(Application.temporaryCachePath, FileName);
                File.WriteAllBytes(path, png);
            }
            catch (Exception e)
            {
                // Diagnostic only. e.Message is an OS string in the device's
                // language, not the game's, and must not reach the player —
                // the same rule CatPicker.cs holds to. A failed share shows
                // nothing; the card stays open and she can tap again.
                Debug.LogWarning($"[Share] write_failed: {e.Message}");
                return;
            }
            // Swift deletes the file once it has the image, so nothing on this
            // side has to guess when the sheet is done with it.
            CatShare_image(path, text);

#elif UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var plugin = new AndroidJavaClass(AndroidPlugin);
                // The explicit object[] is load-bearing. CallStatic's signature
                // is CallStatic(string, params object[]); handing it a byte[]
                // directly makes the marshaller splay the array into the params
                // list — one Java argument per byte — and the call fails to
                // resolve against image(byte[], String).
                plugin.CallStatic("image", new object[] { png, text });
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Share] android_failed: {e.Message}");
            }

#else
            Debug.Log($"[Share] no plugin here; dropped {png.Length} bytes and a caption of {text.Length}");
#endif
        }
    }
}
