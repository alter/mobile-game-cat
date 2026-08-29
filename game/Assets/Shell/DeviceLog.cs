using UnityEngine;

namespace CatShelter.Shell
{
    /// <summary>
    /// Every error and exception the run produces, written to a file the phone
    /// will give back.
    ///
    /// This exists because of a day spent staring at a black screen. On 28.08
    /// the board and meet-your-cat both rendered as an empty panel on the iOS
    /// simulator while the house map rendered correctly, and it was diagnosed
    /// by inference from screenshots rather than by reading anything.
    ///
    /// **The reason given for that at the time was wrong, and it is worth
    /// knowing why.** The claim was that Unity's Debug.Log reaches no console
    /// on a device or simulator. It does:
    /// `xcrun simctl launch --console booted &lt;bundle-id&gt;` prints every line,
    /// and `adb logcat -s Unity` does the same on Android. A day went into
    /// inferring from pixels what one line of log already said. Check the
    /// premise before building an apparatus on it.
    ///
    /// What this file is still for: a run nobody is attached to. A capture
    /// script, a tester's device, a crash after the console was closed — the
    /// errors are on disk afterwards either way.
    ///
    /// <see cref="GameBoot.SafeBuild"/> covers a screen that throws where it is
    /// called. It cannot cover the more common case: Unity catches exceptions
    /// thrown inside Awake, OnEnable, Start and Update itself, logs them, and
    /// returns normally to the caller — so the component is added, the screen
    /// is empty, and nothing is thrown for anyone to catch. This hook sees
    /// those, because it sees the log rather than the call.
    ///
    /// Deliberately not a general logging facility. It records errors,
    /// assertions and exceptions, and nothing else — a run's ordinary chatter
    /// would bury the one line that matters and fill a player's device for no
    /// one's benefit.
    /// </summary>
    public static class DeviceLog
    {
        /// <summary>
        /// Where the errors go, beside the save. `adb pull` on Android,
        /// `simctl get_app_container` on iOS, and the same relative path in an
        /// iTunes file share if it ever comes to that.
        /// </summary>
        public const string FileName = "errors.txt";

        /// <summary>
        /// A stuck update loop can throw sixty times a second. Past this many
        /// entries the file stops growing: whatever went wrong is already in
        /// there several hundred times over, and a log that fills the device is
        /// its own bug.
        /// </summary>
        private const int MaxEntries = 200;

        private static bool _attached;
        private static int _written;
        private static string _path;

        /// <summary>
        /// Start recording. Safe to call more than once — the second call does
        /// nothing rather than doubling every line.
        /// </summary>
        public static void Attach()
        {
            if (_attached) return;
            _attached = true;

            try
            {
                _path = System.IO.Path.Combine(Application.persistentDataPath, FileName);
                // Each run starts clean. The interesting failure is the one
                // happening now, and a file that accumulates every run's errors
                // makes the reader work out which lines are this run's.
                if (System.IO.File.Exists(_path)) System.IO.File.Delete(_path);
            }
            catch (System.Exception)
            {
                _path = null;
                return;
            }

            Application.logMessageReceived += OnLog;
        }

        /// <summary>Stop recording. Used by the tests; nothing in the game calls it.</summary>
        public static void Detach()
        {
            if (!_attached) return;
            Application.logMessageReceived -= OnLog;
            _attached = false;
            _written = 0;
        }

        /// <summary>
        /// Every log line this game writes is tagged `[Something]` — `[Board]`,
        /// `[CaptureScreen]`, `[GameBoot]`, `[Fonts]`. Unity's own warnings are
        /// not. That one convention is what lets this file take our warnings
        /// without drowning in the engine's.
        /// </summary>
        private static bool Ours(string message) =>
            message != null && message.Length > 1 && message[0] == '[';

        private static void OnLog(string message, string stackTrace, LogType type)
        {
            // Errors, exceptions and assertions always. Warnings only when they
            // are ours.
            //
            // Warnings were dropped entirely until 2026-08-29, and a full
            // playthrough showed what that cost: on iOS the capture screen
            // failed with `Could not create inference context` — the single
            // largest failure on that screen, the one that stops a photograph
            // becoming a cat — and `errors.txt` stayed EMPTY through it, because
            // the site logs it as a warning (`CaptureScreen.cs:193`). A file
            // whose whole job is to explain a run with no console attached, silent
            // through exactly the failure it exists for.
            //
            // Raising that one call site to an error would have been the wrong
            // fix: a photograph that cannot be read is not a fault in the app,
            // and lying about severity to get into a file is how severity stops
            // meaning anything. Widening the file is the honest change — but
            // only to OUR warnings. Unity's are not diagnostics about this game:
            // the same playthrough produced a screenful of JNI signature
            // warnings from one share, and they would have filled the fifty
            // lines this file keeps.
            var keep = type == LogType.Error || type == LogType.Exception
                       || type == LogType.Assert
                       || (type == LogType.Warning && Ours(message));
            if (!keep) return;
            if (_path == null || _written >= MaxEntries) return;

            _written++;
            try
            {
                System.IO.File.AppendAllText(_path, $"{type}: {message}\n{stackTrace}\n");
            }
            catch (System.Exception)
            {
                // A log that throws while recording a throw helps nobody.
                _path = null;
            }
        }
    }
}
