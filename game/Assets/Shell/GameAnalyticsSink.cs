using System;
using UnityEngine;

namespace CatShelter.Shell
{
    /// <summary>
    /// Task 70-analytics/01: the GameAnalytics adapter behind
    /// <see cref="Core.Analytics"/>. This is the only file in the project that
    /// is allowed to know GameAnalytics exists — <c>Core/Analytics</c> only
    /// ever sees the generic <c>Action&lt;string, double, string&gt;</c> /
    /// <c>Action&lt;string, int, string&gt;</c> shape (see
    /// <c>build/check-core-purity.sh</c>, which forbids any
    /// <c>UnityEngine</c> reference under <c>Assets/Core</c> — the SDK is a
    /// UnityEngine-referencing package, so it cannot live there either).
    ///
    /// The keys are the one human step this task cannot do (70-analytics/01
    /// NOTES.md has the one-minute instruction). They are never committed —
    /// read from a plain text file beside the save, the same out-of-band
    /// idiom <c>GameBoot.CaptureRequested</c> uses for <c>capture.txt</c> and
    /// <c>EveningReminder</c> uses for <c>notify-in-seconds.txt</c>: absent
    /// file means "do nothing," silently, every launch — not an error, not a
    /// log line repeated forever.
    /// </summary>
    public static class GameAnalyticsSink
    {
        private const string KeysFileName = "analytics-keys.txt";

        /// <summary>
        /// Reads <c>analytics-keys.txt</c> next to the save (game key on line
        /// 1, secret key on line 2), and if both are present, configures and
        /// initialises the GameAnalytics SDK, returning the two delegates
        /// <c>Core.Analytics.Configure</c> wants. Returns (null, null) — the
        /// exact no-op <c>Configure(null, null)</c> already used — for every
        /// other case: no file, a malformed file, or the SDK not being ready
        /// to configure. Never throws.
        /// </summary>
        public static (Action<string, double, string> design,
                       Action<string, int, string> progression)
            TryConfigure(GameObject host)
        {
            string gameKey, secretKey;
            try
            {
                var path = System.IO.Path.Combine(
                    Application.persistentDataPath, KeysFileName);
                if (!System.IO.File.Exists(path))
                    return (null, null);

                var lines = System.IO.File.ReadAllLines(path);
                if (lines.Length < 2)
                    return (null, null);
                gameKey = lines[0].Trim();
                secretKey = lines[1].Trim();
                if (gameKey.Length == 0 || secretKey.Length == 0)
                    return (null, null);
            }
            catch (Exception)
            {
                // Same rule as every other out-of-band switch in this file:
                // a broken read behaves exactly like an absent one.
                return (null, null);
            }

            return Configure(host, gameKey, secretKey);
        }

        private static (Action<string, double, string> design,
                        Action<string, int, string> progression)
            Configure(GameObject host, string gameKey, string secretKey)
        {
            try
            {
                var settings = GameAnalyticsSDK.GameAnalytics.SettingsGA;
                if (settings == null)
                {
                    // The package's own Settings.asset (created once, empty,
                    // the first time the project opens in the Editor after
                    // the package is added — see 01-sdk-integration NOTES.md)
                    // is not present in this build. Keys exist but there is
                    // nowhere to put them: stay in no-op mode rather than
                    // guess at creating the asset from a build.
                    Debug.LogWarning(
                        "[GameAnalyticsSink] analytics-keys.txt present but " +
                        "no GameAnalytics Settings asset — staying no-op.");
                    return (null, null);
                }

                int index = settings.Platforms.IndexOf(RuntimePlatform.IPhonePlayer);
                if (index < 0)
                {
                    settings.AddPlatform(RuntimePlatform.IPhonePlayer);
                    index = settings.Platforms.Count - 1;
                }
                // Runtime key injection, never written to the asset on disk:
                // Settings.UpdateKeys mutates the in-memory singleton
                // (GameAnalytics.SettingsGA), which is what Initialize()
                // reads a few lines down. The .asset file itself keeps
                // whatever it was checked in with — no key, real or
                // placeholder, is ever serialised into it by this code path.
                GameAnalyticsSDK.Setup.Settings.UpdateKeys(index, gameKey, secretKey);

                // SCOPE: "Debug logging enabled for local verification."
                // True is already GameAnalytics's own default for this field
                // (Settings.cs); set explicitly so the intent doesn't depend
                // on an SDK default that could change under us.
                settings.InfoLogBuild = true;

                // D9: "ATT is deliberately not requested. Never call
                // RequestTrackingAuthorization; call
                // EnableAdvertisingIdTracking(false) before Initialize(). The
                // dialog costs installs and we use nothing behind it." This
                // file contains no call to RequestTrackingAuthorization
                // anywhere, on either platform, on purpose.
                GameAnalyticsSDK.GameAnalytics.EnableAdvertisingIdTracking(false);

                // The SDK needs exactly one GameAnalytics component alive
                // (normally placed via the Editor's "Create GameAnalytics
                // object" menu item). GameBoot already assembles the whole
                // scene from code ("an agent never has to touch scene YAML"
                // — GameBoot.cs's own doc comment), so this follows the same
                // pattern instead of requiring a one-off Editor action: added
                // and initialised in the same call, so there is no ambiguity
                // about Awake() ordering between two separately-placed
                // objects, which is the reason the SDK's own docs say to wait
                // for Start() — AddComponent runs this component's Awake()
                // synchronously, before Initialize() is called on the next
                // line.
                if (host.GetComponent<GameAnalyticsSDK.GameAnalytics>() == null)
                    host.AddComponent<GameAnalyticsSDK.GameAnalytics>();

                GameAnalyticsSDK.GameAnalytics.Initialize();

                return (Design, Progression);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GameAnalyticsSink] init failed, staying no-op: {e.Message}");
                return (null, null);
            }
        }

        // -- the two sinks Core.Analytics.Configure wants -------------------

        private static void Design(string name, double value, string extra)
        {
            if (value != 0d)
                GameAnalyticsSDK.GameAnalytics.NewDesignEvent(name, (float)value);
            else
                GameAnalyticsSDK.GameAnalytics.NewDesignEvent(name);
        }

        /// <summary>
        /// Core's three progression names ("level_start"/"level_win"/
        /// "level_fail", <c>Core/Analytics.cs</c>) are plain strings, not
        /// GameAnalytics's <see cref="GameAnalyticsSDK.GAProgressionStatus"/>
        /// — that translation belongs here, not in Core, which does not know
        /// GameAnalytics exists.
        /// </summary>
        private static void Progression(string name, int levelNumber, string extra)
        {
            GameAnalyticsSDK.GAProgressionStatus status;
            switch (name)
            {
                case "level_start": status = GameAnalyticsSDK.GAProgressionStatus.Start; break;
                case "level_win": status = GameAnalyticsSDK.GAProgressionStatus.Complete; break;
                case "level_fail": status = GameAnalyticsSDK.GAProgressionStatus.Fail; break;
                default: return; // unrecognised name: drop rather than mis-tag a status
            }
            GameAnalyticsSDK.GameAnalytics.NewProgressionEvent(status, levelNumber.ToString());
        }
    }
}
