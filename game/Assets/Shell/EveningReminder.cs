using System;
using System.Collections;
using UnityEngine;
#if UNITY_IOS && !UNITY_EDITOR
using Unity.Notifications.iOS;
#endif
#if UNITY_ANDROID && !UNITY_EDITOR
using Unity.Notifications.Android;
#endif

namespace CatShelter.Shell
{
    /// <summary>
    /// Task 60-shell-build/09: one evening notification per day of inactivity.
    ///
    /// This is the only mechanism in the MVP built to cause a return, and
    /// "returned on day 1" is one of the four numbers that decide the project —
    /// which is why the task sits at P0.
    ///
    /// Permission is asked after level 2, not on first launch: at that point
    /// the player has cleared something and knows what she would be reminded
    /// about. (The number that used to justify this — "asking on launch doubles
    /// refusals" — was traced to a marketing blog citing a source that does not
    /// contain it, and is deliberately not repeated here. See NOTES.md.)
    ///
    /// The reminder is rescheduled on every launch, which is what makes it fire
    /// only on days of inactivity: opening the app pushes the next one out to
    /// the following evening.
    /// </summary>
    public static class EveningReminder
    {
        /// <summary>Level after which permission is requested, 1-based.</summary>
        public const int AskAfterLevel = 2;

        private const int EveningHour = 19;
        private const string Identifier = "catshelter-evening";
        private const string AskedKey = "catshelter.notifications.asked";

        // Android replaces a pending notification by integer id where iOS uses
        // a string identifier, so the same reminder needs both. One channel,
        // named for what it is: a channel per notification type would show the
        // player a list of switches for a game that sends one kind of message.
        private const int AndroidId = 1;
        private const string AndroidChannel = "catshelter-evening";

        // Modelled on the line in cat-shelter-mvp.md section 4 — "Murzik found
        // something behind the couch". A discovery, not a chore and not a
        // reproach: the design rule there is that the kitten never gets sick,
        // because punishing a skipped day in a game about caring drives off
        // exactly the audience this is for. No guilt, no urgency, no counting
        // of days missed, nothing owed.
        private static string Title => Copy.Of("notification.title");
        private static string Body => Copy.Of("notification.body");

        /// <summary>
        /// Both stores are targets (DECISIONS.md D17), and this is the only
        /// mechanism in the MVP built to cause a return. Firing on one platform
        /// and not the other would make metric 3 a comparison of platforms
        /// rather than of the game — see 90-android/09-notifications.
        /// </summary>
        public static bool Available =>
            Application.platform == RuntimePlatform.IPhonePlayer ||
            Application.platform == RuntimePlatform.Android;

        public static bool AlreadyAsked => PlayerPrefs.GetInt(AskedKey, 0) == 1;

        /// <summary>
        /// Called when a level is finished. Asks for permission exactly once,
        /// after level <see cref="AskAfterLevel"/>, and never again.
        /// </summary>
        public static IEnumerator OnLevelCompleted(MonoBehaviour host, int levelNumber)
        {
            if (!Available || AlreadyAsked || levelNumber < AskAfterLevel)
                yield break;

            PlayerPrefs.SetInt(AskedKey, 1);
            PlayerPrefs.Save();

#if UNITY_IOS && !UNITY_EDITOR
            using (var request = new AuthorizationRequest(
                       AuthorizationOption.Alert | AuthorizationOption.Badge, true))
            {
                while (!request.IsFinished)
                    yield return null;

                Report($"permission answered: granted={request.Granted} error={request.Error}");
                if (request.Granted)
                {
                    Core.Analytics.NotificationAllowed();
                    Schedule();
                }
            }
#elif UNITY_ANDROID && !UNITY_EDITOR
            // POST_NOTIFICATIONS is a runtime permission from API 33 (Android
            // 13). Below that the request completes immediately as Allowed, so
            // the same code covers both and there is no version branch here.
            // The package remembers a refusal itself and will not re-prompt.
            var request = new PermissionRequest();
            while (request.Status == PermissionStatus.RequestPending)
                yield return null;

            Report($"permission answered: status={request.Status}");
            if (request.Status == PermissionStatus.Allowed)
            {
                Core.Analytics.NotificationAllowed();
                Schedule();
            }
#else
            yield break;
#endif
        }

        /// <summary>
        /// Push the next reminder out to this evening. Called on launch, so a
        /// player who opens the game today is not reminded about today.
        /// </summary>
        public static void Reschedule()
        {
            if (!Available)
                return;
            if (!AlreadyAsked)
            {
                Report("launch: permission not asked yet, nothing to schedule");
                return;
            }
            Schedule();
        }

        /// <summary>
        /// Ask for permission now instead of after level 2, and only when the
        /// debug file is present. Exists because the permission moment and the
        /// delivery cannot otherwise be exercised without playing two levels by
        /// hand and waiting until evening.
        /// </summary>
        public static IEnumerator DebugRequestNow(MonoBehaviour host)
        {
            if (!Available || DebugDelaySeconds() <= 0 || AlreadyAsked)
                yield break;

            Debug.Log("[EveningReminder] debug file present, requesting permission now");
            yield return OnLevelCompleted(host, AskAfterLevel);
        }

        /// <summary>
        /// Seconds to fire in, instead of this evening, when a file called
        /// `notify-in-seconds.txt` sits next to the save. Delivery is otherwise
        /// only observable by waiting until 19:00, which is no way to check
        /// that it works. Absent in any normal run.
        /// </summary>
        private static int DebugDelaySeconds()
        {
            try
            {
                var path = System.IO.Path.Combine(
                    Application.persistentDataPath, "notify-in-seconds.txt");
                if (!System.IO.File.Exists(path)) return 0;
                return int.TryParse(System.IO.File.ReadAllText(path).Trim(), out var seconds)
                    ? Mathf.Clamp(seconds, 0, 3600) : 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        private static void Schedule()
        {
#if UNITY_IOS && !UNITY_EDITOR
            // Replacing by identifier rather than cancelling everything: the
            // player may have other notifications from this app later, and
            // clearing the lot would take them with it.
            iOSNotificationCenter.RemoveScheduledNotification(Identifier);

            iOSNotificationTrigger trigger;
            var debugDelay = DebugDelaySeconds();
            if (debugDelay > 0)
            {
                trigger = new iOSNotificationTimeIntervalTrigger
                {
                    TimeInterval = TimeSpan.FromSeconds(debugDelay),
                    Repeats = false,
                };
                Debug.Log($"[EveningReminder] debug delay {debugDelay}s instead of {EveningHour}:00");
            }
            else
            {
                trigger = new iOSNotificationCalendarTrigger
                {
                    // Year, Month and Day left unset on purpose: the system
                    // then picks the next occurrence of this hour, and Repeats
                    // makes it daily from there.
                    Hour = EveningHour,
                    Minute = 0,
                    Repeats = true,
                };
            }

            iOSNotificationCenter.ScheduleNotification(new iOSNotification
            {
                Identifier = Identifier,
                Title = Title,
                Body = Body,
                ShowInForeground = false,
                Trigger = trigger,
            });

            // Written to a file as well as logged: a capture run has no
            // console attached. (The log itself does reach both platforms —
            // `simctl launch --console`, `adb logcat -s Unity`; an earlier
            // comment here said otherwise and was wrong.) So what
            // happened here is written next to the save instead. This is the
            // only way the scheduling is observable from outside the app.
            Report($"scheduled '{Identifier}', pending={iOSNotificationCenter.GetScheduledNotifications().Length}, " +
                   $"authorization={iOSNotificationCenter.GetNotificationSettings().AuthorizationStatus}, " +
                   $"debugDelay={debugDelay}");
#elif UNITY_ANDROID && !UNITY_EDITOR
            // The channel has to exist before anything is posted to it, and
            // registering the same id twice is a no-op, so this is done on
            // every schedule rather than tracked with a flag.
            AndroidNotificationCenter.RegisterNotificationChannel(
                new AndroidNotificationChannel
                {
                    Id = AndroidChannel,
                    Name = Copy.Of("notification.channel"),
                    Description = Copy.Of("notification.channel_description"),
                    // Default, not High: this is a quiet evening nudge, not an
                    // alarm. High would give it heads-up display and sound,
                    // which is the urgency the tone rule forbids.
                    Importance = Importance.Default,
                });

            AndroidNotificationCenter.CancelNotification(AndroidId);

            var androidDelay = DebugDelaySeconds();
            DateTime fireTime;
            TimeSpan? repeat;
            if (androidDelay > 0)
            {
                fireTime = DateTime.Now.AddSeconds(androidDelay);
                repeat = null;
                Debug.Log($"[EveningReminder] debug delay {androidDelay}s instead of {EveningHour}:00");
            }
            else
            {
                // The next occurrence of EveningHour: today if it is still to
                // come, otherwise tomorrow. iOS gets this from the calendar
                // trigger with the date left unset; Android has no equivalent,
                // so the same rule is written out here.
                var today = DateTime.Now.Date.AddHours(EveningHour);
                fireTime = today > DateTime.Now ? today : today.AddDays(1);
                repeat = TimeSpan.FromDays(1);
            }

            var notification = new AndroidNotification
            {
                Title = Title,
                Text = Body,
                FireTime = fireTime,
                RepeatInterval = repeat,
                // Exact alarms are deliberately not requested: an evening
                // reminder does not need to be exact, and SCHEDULE_EXACT_ALARM
                // is a review question nobody wants to answer
                // (90-android/09-notifications, SCOPE).
                ShowTimestamp = false,
            };

            AndroidNotificationCenter.SendNotificationWithExplicitID(
                notification, AndroidChannel, AndroidId);

            Report($"scheduled id={AndroidId} on '{AndroidChannel}' for {fireTime:yyyy-MM-dd HH:mm:ss}, " +
                   $"repeat={(repeat.HasValue ? "daily" : "once")}, " +
                   $"permission={AndroidNotificationCenter.UserPermissionToPost}, " +
                   $"debugDelay={androidDelay}");
#endif
        }

        private static void Report(string line)
        {
            Debug.Log($"[EveningReminder] {line}");
            try
            {
                System.IO.File.AppendAllText(
                    System.IO.Path.Combine(Application.persistentDataPath, "reminder-state.txt"),
                    $"{DateTime.Now:HH:mm:ss} {line}\n");
            }
            catch (Exception)
            {
                // diagnostics must never take the app down
            }
        }
    }
}
