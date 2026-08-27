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
#else
                return false;
#endif
            }
        }

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

        /// <summary>Open the system gallery. Needs no photo-library permission:
        /// PHPicker runs out of process and hands over only what was picked.</summary>
        public static void PickFromGallery(Action<byte[]> onPicked, Action<string> onFailed)
        {
            Instance.Begin(onPicked, onFailed);
#if UNITY_IOS && !UNITY_EDITOR
            CatPicker_openGallery();
#else
            Instance.Fail("unsupported");
#endif
        }

        /// <summary>Open the camera. Requires NSCameraUsageDescription.</summary>
        public static void CaptureWithCamera(Action<byte[]> onPicked, Action<string> onFailed)
        {
            Instance.Begin(onPicked, onFailed);
#if UNITY_IOS && !UNITY_EDITOR
            CatPicker_openCamera();
#else
            Instance.Fail("unsupported");
#endif
        }

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

        // --- called from Swift by name; do not rename ----------------------

        private void OnPicked(string path)
        {
            var callback = _onPicked;
            _onPicked = null;
            _onFailed = null;
            try
            {
                var bytes = File.ReadAllBytes(path);
                // The picker wrote this to the temporary directory; nothing
                // else will clean it up, and a camera roll's worth of stale
                // JPEGs is not something to leave behind.
                File.Delete(path);
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
        // "save_failed", "no_window") — never a sentence; see the class
        // summary above.
        private void OnPickFailed(string reason) => Fail(reason);

        // what is "camera" today and only ever a bare code, not prose; the
        // caller maps every non-"cancelled" reason to one fixed message
        // regardless of its exact value, so no sentence is built from it.
        private void OnPickUnavailable(string what) => Fail("unavailable");
    }
}
