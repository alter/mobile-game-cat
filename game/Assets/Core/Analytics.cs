using System;
using System.Runtime.CompilerServices;

// 26-room12-reveal: Unity compiles Assets/Core and Assets/Tests/Core into two
// separate asmdef assemblies (CatShelter.Core / CatShelter.Core.Tests), so
// ResetForTests()'s `internal` below was invisible to the test assembly and
// every Unity Editor build failed to compile — "Core and Tests compile into
// one assembly" is only true of build/core-tests/core-tests.csproj, which
// folds both folders into a single dotnet-test assembly and so never caught
// this. Found while building an APK for this task's VERIFY.
[assembly: InternalsVisibleTo("CatShelter.Core.Tests")]

namespace CatShelter.Core
{
    /// <summary>Where an analytics event came from in the flow.</summary>
    public static class AnalyticsEventNames
    {
        // Design events (exact colon-hierarchical names, task 70-analytics/02).
        public const string AppOpen = "app:open";
        public const string PhotoScreenShown = "photo:screen_shown";
        public const string PhotoUploaded = "photo:uploaded";
        public const string PhotoRejected = "photo:rejected";
        public const string BoosterTap = "booster:tap";
        public const string NotificationAllowed = "notification:allowed";

        // Progression events (keyed by levelId; map to Start/Complete/Fail).
        public const string LevelStart = "level_start";
        public const string LevelWin = "level_win";
        public const string LevelFail = "level_fail";

        /// <summary>All nine — used by tests to pin the surface.</summary>
        public static readonly string[] All =
        {
            AppOpen, PhotoScreenShown, PhotoUploaded, PhotoRejected,
            BoosterTap, NotificationAllowed,
            LevelStart, LevelWin, LevelFail,
        };
    }

    /// <summary>
    /// The only way Shell/View code reports analytics. Core raises nothing on
    /// its own: the presentation layer calls these at the right moments. The
    /// sink is injected at composition time (GameAnalytics in the player, a
    /// list-capturing fake in tests) so Core stays engine-free and no event
    /// name ever appears twice.
    ///
    /// GameAnalytics drops silently on bad names (knowledge/analytics/
    /// 04-gameanalytics-unity-usage.md), so names are validated here at the
    /// single point of definition instead of trusted everywhere.
    /// </summary>
    public static class Analytics
    {
        private static Action<string, double, string> _designSink;
        private static Action<string, int, string> _progressionSink;
        private static bool _validated;

        /// <summary>
        /// Diagnostic sink for recoverable Analytics misuse (Configure not
        /// called yet, level number outside 1..999). Core has no UnityEngine
        /// reference so it cannot call Debug.LogWarning itself; wiring this
        /// to one — same as the design/progression sinks above — is the
        /// Shell's job. Null is a valid, silent no-op: telemetry must never
        /// end a level (task 60-shell-build/21) even when nobody is
        /// listening for the warning.
        /// </summary>
        public static Action<string> WarnSink { get; set; }

        private static void Warn(string message) => WarnSink?.Invoke(message);

        /// <summary>Wire once at startup. Nulls are allowed (no-op mode).</summary>
        public static void Configure(
            Action<string, double, string> designSink,
            Action<string, int, string> progressionSink)
        {
            _designSink = designSink;
            _progressionSink = progressionSink;
            ValidateSurface();
            _validated = true;
        }

        private static void ValidateSurface()
        {
            foreach (var name in AnalyticsEventNames.All)
                EnsureValid(name);
        }

        public static void Design(string name, double value = 0, string extra = null)
        {
            // An event firing before Configure is a wiring bug, but it must
            // lose the event, not the level (task 60-shell-build/21).
            if (!_validated)
            {
                Warn($"Analytics.Design('{name}') before Configure — event dropped");
                return;
            }
            EnsureValid(name);
            _designSink?.Invoke(name, value, extra);
        }

        /// <summary>Progression event with 1-based level number as score.</summary>
        public static void Progression(string name, int levelNumber)
        {
            // Same rule as Design: "level_start" must never be able to end
            // the level it is reporting on (task 60-shell-build/21).
            if (!_validated)
            {
                Warn($"Analytics.Progression('{name}') before Configure — event dropped");
                return;
            }
            EnsureValid(name);
            if (levelNumber < 1 || levelNumber > 999)
            {
                // Out-of-range level numbers are dropped, not clamped: a
                // clamped 1000 -> 999 would silently misreport which level
                // the player is on, which is worse than losing the sample.
                Warn($"Analytics.Progression('{name}') levelNumber={levelNumber} " +
                    "outside 1..999 — event dropped");
                return;
            }
            _progressionSink?.Invoke(name, levelNumber, levelNumber.ToString());
        }

        // -- named helpers: call sites read as intent -----------------------

        public static void AppOpen() => Design(AnalyticsEventNames.AppOpen);

        public static void PhotoScreenShown() =>
            Design(AnalyticsEventNames.PhotoScreenShown);

        public static void PhotoUploaded() =>
            Design(AnalyticsEventNames.PhotoUploaded);

        public static void PhotoRejected() =>
            Design(AnalyticsEventNames.PhotoRejected);

        public static void BoosterTap() => Design(AnalyticsEventNames.BoosterTap);

        public static void NotificationAllowed() =>
            Design(AnalyticsEventNames.NotificationAllowed);

        public static void LevelStart(int levelNumber) =>
            Progression(AnalyticsEventNames.LevelStart, levelNumber);

        public static void LevelWin(int levelNumber) =>
            Progression(AnalyticsEventNames.LevelWin, levelNumber);

        public static void LevelFail(int levelNumber) =>
            Progression(AnalyticsEventNames.LevelFail, levelNumber);

        // GameAnalytics rules (knowledge/analytics/04): 1–64 chars for design
        // event names; allowed: [a-zA-Z0-9:_-.] — we stay stricter: colon +
        // word chars only, so every current and future name passes.
        public static void EnsureValid(string name)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("event name empty");
            if (name.Length > 64)
                throw new ArgumentException($"event name too long: {name}");
            foreach (var c in name)
            {
                var ok = char.IsLetterOrDigit(c) || c == ':' || c == '_';
                if (!ok)
                    throw new ArgumentException(
                        $"event name has character '{c}': {name}");
            }
        }

        // internal, not public, kept usable from the Tests asmdef only via
        // the InternalsVisibleTo at the top of this file — see its comment.
        internal static void ResetForTests() => _validated = false;
    }
}
