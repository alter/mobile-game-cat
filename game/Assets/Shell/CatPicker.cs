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
            Instance.Fail("gallery is iOS-only");
#endif
        }

        /// <summary>Open the camera. Requires NSCameraUsageDescription.</summary>
        public static void CaptureWithCamera(Action<byte[]> onPicked, Action<string> onFailed)
        {
            Instance.Begin(onPicked, onFailed);
#if UNITY_IOS && !UNITY_EDITOR
            CatPicker_openCamera();
#else
            Instance.Fail("camera is iOS-only");
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
                Fail($"could not read the picked photo: {e.Message}");
            }
        }

        private void OnPickCancelled(string _) => Fail("cancelled");

        private void OnPickFailed(string reason) => Fail(reason);

        private void OnPickUnavailable(string what) => Fail($"{what} is not available");
    }
}
