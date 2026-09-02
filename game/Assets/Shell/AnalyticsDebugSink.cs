using System;
using UnityEngine;

namespace CatShelter.Shell
{
    /// <summary>
    /// Task 70-analytics/02 VERIFY: the nine events are wired and pinned by
    /// static call-site tests, but nobody has ever watched one actually leave
    /// the device — <see cref="GameAnalyticsSink.TryConfigure"/> returns
    /// (null, null) without <c>analytics-keys.txt</c> (the owner's own step,
    /// 01-sdk-integration), so every <c>Analytics.*</c> call today reaches
    /// <c>Configure</c>'s sink and stops there. There is nothing to observe.
    ///
    /// Drop an `analytics.txt` file beside the save — presence-only, content
    /// ignored, same convention as `board.txt`/`coat.txt` — and Shell wraps
    /// whatever sink <see cref="GameAnalyticsSink"/> produced (a real one, or
    /// the no-op pair) with one that also writes every call to the device log
    /// and to `analytics-log.txt` beside the save: event name, value or level
    /// number, and seconds since launch, one line per call. No file: today's
    /// behaviour, unchanged — <see cref="Wrap"/> hands back <c>inner</c> as
    /// given.
    /// </summary>
    public static class AnalyticsDebugSink
    {
        private const string FlagFileName = "analytics.txt";
        private const string LogFileName = "analytics-log.txt";

        public static (Action<string, double, string> design,
                       Action<string, int, string> progression)
            Wrap(GameObject host,
                 (Action<string, double, string> design,
                  Action<string, int, string> progression) inner)
        {
            if (!Requested()) return inner;

            // Fresh each run: a log left over from a previous playthrough
            // would make this run's "seconds since launch" column a lie.
            try { System.IO.File.WriteAllText(LogPath(), ""); }
            catch (Exception) { }

            var innerDesign = inner.design;
            var innerProgression = inner.progression;

            // The real sink (if any) still runs first: this wraps it rather
            // than replacing it, so a device that somehow has both
            // analytics-keys.txt and analytics.txt still sends real events.
            Action<string, double, string> design = (name, value, extra) =>
            {
                innerDesign?.Invoke(name, value, extra);
                Line($"design {name} value={value} extra={extra}");
            };
            Action<string, int, string> progression = (name, levelNumber, extra) =>
            {
                innerProgression?.Invoke(name, levelNumber, extra);
                Line($"progression {name} level={levelNumber}");
            };
            return (design, progression);
        }

        private static bool Requested()
        {
            try
            {
                return System.IO.File.Exists(System.IO.Path.Combine(
                    Application.persistentDataPath, FlagFileName));
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static string LogPath() =>
            System.IO.Path.Combine(Application.persistentDataPath, LogFileName);

        private static void Line(string body)
        {
            // realtimeSinceStartup, not a stopwatch started in Wrap: it reads
            // as "time since the app launched" regardless of how late
            // Configure ran, which is what the task asks the table to show.
            var line = $"t={Time.realtimeSinceStartup:0.000}s {body}";
            Debug.Log($"[Analytics] {line}");
            try
            {
                System.IO.File.AppendAllText(LogPath(), line + "\n");
            }
            catch (Exception)
            {
            }
        }
    }
}
