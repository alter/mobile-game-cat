using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace CatShelter.Shell
{
    /// <summary>
    /// Task 50-photo/08: the C# half of the photo picker.
    ///
    /// The native side answers through <c>UnitySendMessage</c>, which needs a
    /// GameObject by name, so this component creates and owns that object. It
    /// survives scene loads because the picker runs in another process and the
    /// answer can arrive after anything on our side has been torn down.
    ///
    /// Every path ends in exactly one callback — picked, cancelled, failed or
    /// unavailable — so the screen never sits in a loading state with nothing
    /// coming.
    ///
    /// <c>onFailed</c> carries a fixed lowercase reason code
    /// ("unsupported"/"read_failed"/"save_failed"/"no_window"/"unavailable"),
    /// never a sentence: task 60-shell-build/16's VERIFY found this class
    /// exempted from <c>test_copy_table.py</c> on the strength of "failure
    /// reasons handed to Copy.Of" — true, but the reasons themselves used to
    /// be raw English (some from native Swift, one carrying a
    /// system-language OS error string), which defeats a copy table on
    /// arrival. The caller maps every code to one fixed, already-tabled
    /// message; no reason string is ever displayed verbatim.
    /// </summary>
    public sealed class CatPicker : MonoBehaviour
    {
        private const string ListenerName = "CatPickerListener";

        private static CatPicker _instance;
        private Action<byte[]> _onPicked;
        private Action<string> _onFailed;

        public static bool CameraAvailable
        {
            get
            {
#if UNITY_IOS && !UNITY_EDITOR
                return CatPicker_hasCamera();
#elif UNITY_ANDROID && !UNITY_EDITOR
                try
                {
                    using var plugin = new AndroidJavaClass(AndroidPlugin);
                    // Two questions on the Java side, and both have to answer
                    // yes: is there a camera in this device, and is there an
                    // app willing to drive it. See CatPicker.java.
                    return plugin.CallStatic<bool>("hasCamera");
                }
                catch (Exception e)
                {
                    // A question that cannot be answered is answered "no". A
                    // hidden button costs the player one way in; a dead one
                    // costs her the screen.
                    Debug.LogWarning($"[CatPicker] hasCamera failed: {e.Message}");
                    return false;
                }
#else
                return false;
#endif
            }
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        /// <summary>
        /// Assets/Plugins/Android/CatPicker.androidlib. Java rather than a
        /// DllImport, and the same four callbacks arrive by the same route:
        /// UnityPlayer.UnitySendMessage to the GameObject below.
        /// </summary>
        private const string AndroidPlugin = "com.catshelter.picker.CatPicker";
#endif

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void CatPicker_openGallery();
        [DllImport("__Internal")] private static extern void CatPicker_openCamera();
        // Swift's Bool is one byte; without MarshalAs the marshaller reads
        // four and the answer is whatever happened to sit next to it — which
        // showed a camera button on a simulator that has no camera.
        [DllImport("__Internal")]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool CatPicker_hasCamera();
#endif

        private static CatPicker Instance
        {
            get
            {
                if (_instance != null) return _instance;
                var host = new GameObject(ListenerName);
                DontDestroyOnLoad(host);
                _instance = host.AddComponent<CatPicker>();
                return _instance;
            }
        }

        /// <summary>Open the system gallery. Needs no photo-library permission
        /// on either platform, and that is not a coincidence: PHPicker on iOS
        /// and <c>MediaStore.ACTION_PICK_IMAGES</c> on Android both run out of
        /// process and hand over only what was picked. Two platforms, one
        /// permission audit that stays empty
        /// (60-shell-build/17-permission-audit).</summary>
        public static void PickFromGallery(Action<byte[]> onPicked, Action<string> onFailed)
        {
            Instance.Begin(onPicked, onFailed);
#if UNITY_IOS && !UNITY_EDITOR
            CatPicker_openGallery();
#elif UNITY_ANDROID && !UNITY_EDITOR
            Instance.CallAndroid("openGallery");
#else
            Instance.Fail("unsupported");
#endif
        }

        /// <summary>Open the camera. Requires NSCameraUsageDescription on iOS.
        /// On Android it requires no permission at all, because the plug-in
        /// delegates to the camera app rather than opening the camera itself —
        /// and would require one the moment it declared
        /// android.permission.CAMERA, which is why it does not.</summary>
        public static void CaptureWithCamera(Action<byte[]> onPicked, Action<string> onFailed)
        {
            Instance.Begin(onPicked, onFailed);
#if UNITY_IOS && !UNITY_EDITOR
            CatPicker_openCamera();
#elif UNITY_ANDROID && !UNITY_EDITOR
            Instance.CallAndroid("openCamera");
#else
            Instance.Fail("unsupported");
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        /// <summary>
        /// Fire-and-forget into the Java plug-in. A throw here means the
        /// plug-in is not in the build at all — a stripped class, a missing
        /// .androidlib — and that is "unavailable", the same code iOS sends
        /// for a device that cannot do this. The callback still fires exactly
        /// once, which is the whole contract.
        /// </summary>
        private void CallAndroid(string method)
        {
            try
            {
                using var plugin = new AndroidJavaClass(AndroidPlugin);
                plugin.CallStatic(method);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CatPicker] {method} failed: {e.Message}");
                Fail("unavailable");
            }
        }
#endif

        private void Begin(Action<byte[]> onPicked, Action<string> onFailed)
        {
            _onPicked = onPicked;
            _onFailed = onFailed;
        }

        private void Fail(string reason)
        {
            var callback = _onFailed;
            _onPicked = null;
            _onFailed = null;
            callback?.Invoke(reason);
        }

        // --- called from Swift and from Java by name; do not rename ---------

        private void OnPicked(string path)
        {
            var callback = _onPicked;
            _onPicked = null;
            _onFailed = null;
            try
            {
                var bytes = File.ReadAllBytes(path);
                // The picker wrote this to the temporary directory (Android:
                // getCacheDir()/catpick); nothing else will clean it up, and a
                // camera roll's worth of stale JPEGs is not something to leave
                // behind. The Android plug-in also empties that directory at
                // the start of every pick, for the case where this line never
                // runs because the process died first.
                File.Delete(path);
                Debug.Log($"[CatPicker] read {bytes.Length} bytes from the picker");
                callback?.Invoke(bytes);
            }
            catch (Exception e)
            {
                // e.Message is diagnostic only and must never reach the
                // player: it is a .NET/OS message, not copy, and not
                // English on every device.
                Debug.LogWarning($"[CatPicker] read_failed: {e.Message}");
                Fail("read_failed");
            }
        }

        private void OnPickCancelled(string _) => Fail("cancelled");

        // reason is already a fixed code from CatPicker.swift ("read_failed",
        // "save_failed", "no_window") or from CatPickActivity.java
        // ("read_failed", "no_window", "unavailable") — never a sentence; see
        // the class summary above.
        private void OnPickFailed(string reason) => Fail(reason);

        // what is "camera" today and only ever a bare code, not prose; the
        // caller maps every non-"cancelled" reason to one fixed message
        // regardless of its exact value, so no sentence is built from it.
        private void OnPickUnavailable(string what) => Fail("unavailable");
    }
}
