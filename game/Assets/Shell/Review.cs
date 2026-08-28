using UnityEngine;

namespace CatShelter.Shell
{
    /// <summary>
    /// Task 60-shell-build/11: take a player who pressed a heart to the page
    /// where she can leave a review, and do nothing else.
    ///
    /// WHY NOT THE IN-APP RATING PROMPT. Both stores have one, and on both
    /// stores their own documentation rules it out for a button the player
    /// pressed:
    ///
    ///   Apple, "Requesting App Store reviews" —
    ///   https://developer.apple.com/documentation/storekit/requesting-app-store-reviews
    ///     "the system displays the review prompt to a user a maximum of three
    ///      times within a 365-day period"
    ///     "Avoid requesting a review as the result of a user action."
    ///   and, under "Manually request a review", the API for the case we have:
    ///     "To enable a person to initiate a review as a result of an action in
    ///      the UI, the sample code uses a deep link to the App Store page for
    ///      the app with the query parameter action=write-review appended"
    ///
    ///   Google, "Google Play In-App Reviews API" —
    ///   https://developer.android.com/guide/playcore/in-app-review
    ///     "you should not have a call-to-action option (such as a button) to
    ///      trigger the API, as a user might have already hit their quota and
    ///      the flow won't be shown, presenting a broken experience to the
    ///      user. For this use case, redirect the user to the Play Store
    ///      instead."
    ///
    /// So: a URL on both platforms. `SKStoreReviewController.requestReview` and
    /// `ReviewManager.launchReviewFlow` are both the wrong end of the stick
    /// here — they are for a moment the *app* chose, and this is a moment the
    /// *player* chose. Neither is used, and neither needs a native plugin: the
    /// whole feature is one documented URL per store handed to
    /// `Application.OpenURL`, which is why there is no CatReview.swift and no
    /// CatReview.java next to CatShare's.
    ///
    /// Nothing here reports back. Whether she wrote anything is between her
    /// and the store; there is no callback on either platform, and a tap on
    /// the heart is the only thing this game can honestly count.
    /// </summary>
    public static class Review
    {
        /// <summary>
        /// THE ONE CONSTANT THAT IS NOT REAL YET.
        ///
        /// The App Store assigns this number when the app record is created in
        /// App Store Connect; it cannot be derived from the bundle id and it
        /// has not been created. Empty on purpose rather than filled with a
        /// plausible-looking number — a wrong id is a heart that opens a
        /// stranger's app. Paste the digits from the app's product URL
        /// (apps.apple.com/app/id1234567890) here and the button appears.
        ///
        /// While it is empty, <see cref="Available"/> is false on iOS and the
        /// heart is not drawn at all.
        /// </summary>
        public const string AppStoreId = "";

        /// <summary>
        /// Unity's stand-in when ProjectSettings has no applicationIdentifier,
        /// which is where this project still is (`applicationIdentifier: {}`).
        /// A Play listing under this package does not exist, so the heart stays
        /// hidden on Android too until the game has a real package name.
        /// </summary>
        private const string UnityDefaultPackagePrefix = "com.DefaultCompany";

        /// <summary>
        /// Whether there is a review page to open. False in the editor, and
        /// false on a phone whose store identity has not been set yet.
        ///
        /// The caller is expected to hide the button rather than draw one that
        /// goes nowhere — the same rule that removed the "one more shelf" fake
        /// door on the lose card (DECISIONS D4). A heart that opens a 404 is
        /// worse than no heart.
        /// </summary>
        public static bool Available
        {
            get
            {
#if UNITY_EDITOR
                return false;
#elif UNITY_IOS
                return !string.IsNullOrEmpty(AppStoreId);
#elif UNITY_ANDROID
                return !string.IsNullOrEmpty(Application.identifier)
                       && !Application.identifier.StartsWith(UnityDefaultPackagePrefix);
#else
                return false;
#endif
            }
        }

        /// <summary>
        /// Open the store's review page for this app. Fire-and-forget, like
        /// <see cref="Share.Image"/>: the player leaves the game, and nothing
        /// waits for her to come back.
        /// </summary>
        public static void Open()
        {
            if (!Available)
            {
                // Not an error the player can see. On a device this means the
                // store identity is missing, which the caller should already
                // have caught by not drawing the button.
                Debug.Log("[Review] no store page to open here");
                return;
            }

#if UNITY_IOS && !UNITY_EDITOR
            // Apple's own sample URL, with the id substituted:
            // "https://apps.apple.com/app/idYOURAPPSTOREID?action=write-review"
            Application.OpenURL($"https://apps.apple.com/app/id{AppStoreId}?action=write-review");

#elif UNITY_ANDROID && !UNITY_EDITOR
            // Google's documented store-listing deep link, "Linking to Google
            // Play" — https://developer.android.com/distribute/marketing-tools/linking-to-google-play
            //   "https://play.google.com/store/apps/details?id=<package_name>"
            // Play has no documented write-review query parameter of its own;
            // the listing page is what its in-app-review page tells you to
            // redirect to, and the rating control is on it.
            //
            // Google's in-app example builds this as an ACTION_VIEW intent with
            // setPackage("com.android.vending") so the Play app opens instead
            // of a chooser. Application.OpenURL cannot set a package, so a
            // device with more than one handler for play.google.com may show a
            // chooser or a browser. See this task's NOTES.md — that is the one
            // known rough edge, and it costs a small Java plugin to remove if
            // it turns out to be real on a phone.
            Application.OpenURL($"https://play.google.com/store/apps/details?id={Application.identifier}");

#else
            Debug.Log("[Review] no store here");
#endif
        }
    }
}
