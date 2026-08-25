# App Store Requirements for an iOS Game — August 2026

Material collected on: 2026-08-24.
Project stack version: Unity 6.3 LTS (6000.3.x), target platform iOS, distribution via TestFlight and the App Store.

## Summary

- Starting April 28, 2026, builds uploaded to App Store Connect for iOS and iPadOS must be built with the iOS 26 / iPadOS 26 SDK or later — that means Xcode 26 or later. Apple's wording: "Starting April 28, 2026, apps and games uploaded to App Store Connect need to meet the following minimum requirements: iOS and iPadOS apps must be built with the iOS 26 & iPadOS 26 SDK or later". The requirement stated in the project document is confirmed, but the date is not "April 2026" in general — it is specifically April 28, 2026. ([Apple Developer — Upcoming SDK Minimum Requirements](https://developer.apple.com/news/?id=ueeok6yw))
- The requirement concerns the build toolchain, not the minimum iOS version the game must run on — the developer sets the deployment target themselves.
- Apple Developer Program membership costs 99 USD per year (299 USD per year for the Enterprise program); per Apple's statement, processing banking details after signing the Paid Apps Agreement and submitting tax forms takes 24 hours, but developer forums describe real delays of up to several weeks.
- The PrivacyInfo.xcprivacy file is mandatory: since May 1, 2024, App Store Connect does not accept new or updated builds if the use of "required reason API" is not declared in the privacy manifest. For a Unity project, the manifest is assembled in the UnityFramework target and merges data from the runtime, plugins, and third-party code.
- A game that takes a photo with the camera, sends it to its own server, and does not store it is still required to declare the collection of photos/video in App Privacy (the "nutrition label") in App Store Connect and state the purpose of use; "not stored on the server" does not exempt the app from declaring the collection.
- As of 2026, the age rating in App Store Connect has been expanded to the values 4+, 9+, 13+, 16+, 18+ (previously 4+/9+/12+/17+); the questionnaire on the new questions had to be filled out by January 31, 2026, otherwise update submissions are blocked.
- An app with user-generated content (including uploaded photos) is required to have filtering of objectionable material, a reporting mechanism, blocking of offenders, and published contact information (guideline 1.2); for content aimed at children, there are additional requirements for the privacy of minors' data (section 1.3, Kids Category) and for the privacy policy (guideline 5.1.1).
- ATT (App Tracking Transparency) is mandatory only if the app tracks the user across other companies' apps/websites or accesses the IDFA; for the app's own server-side photo processing without such tracking, an ATT request is not required.
- TestFlight: up to 100 internal testers (no device limit stated in the official text), up to 10,000 external testers per app; the first build for external testers goes through Beta App Review; builds are available to testers for 90 days.
- The loot box rule is set out in guideline 3.1.1: apps with loot boxes are required to disclose the odds of receiving each type of item prior to purchase.

## SDK and Xcode Requirement: Effective Date

The official Apple Developer page "Upcoming SDK Minimum Requirements" (opened via WebFetch) contains the exact wording:

> "Starting April 28, 2026, apps and games uploaded to App Store Connect need to meet the following minimum requirements:
> - iOS and iPadOS apps must be built with the iOS 26 & iPadOS 26 SDK or later
> - tvOS apps must be built with the tvOS 26 SDK or later
> - visionOS apps must be built with the visionOS 26 SDK or later
> - watchOS apps must be built with the watchOS 26 SDK or later"

([Apple Developer — Upcoming SDK Minimum Requirements](https://developer.apple.com/news/?id=ueeok6yw))

The same wording (with the same date, April 28, 2026) is repeated on the page "App Store submissions now open for the latest OS releases":

> "Starting April 2026, apps and games uploaded to App Store Connect must meet these minimum requirements: iOS and iPadOS apps must be built with the iOS 26 & iPadOS 26 SDK or later..."

The same page clarifies the build path: "Build your apps and games using the Xcode 26 Release Candidate and latest SDKs. Test with TestFlight. Submit for review to the App Store." ([Apple Developer — App Store submissions now open for the latest OS releases](https://developer.apple.com/news/?id=6lxhtioi))

Conclusion regarding the wording from the project document, "starting April 2026 only builds made with the iOS 26 SDK are accepted": **confirmed**, with the clarification of the exact date — April 28, 2026, not "early April." The requirement applies to the SDK/Xcode used to build the app (i.e., Xcode 26 or later is effectively required), not to the minimum iOS version the app must run on — that is set separately by the developer via the deployment target. Developers note (secondary sources, without official Apple confirmation for previous cycles) a historical pattern of annual tightening: "Starting April 2021, all iOS and iPadOS apps submitted to the App Store must be built with Xcode 12 and the iOS 14 SDK" and similarly for the iOS 18 SDK/Xcode 16 starting April 2025 — the exact date and quote for these past cycles were not verified against official Apple pages within this research; given only as context from secondary sources.

The exact Xcode 26 version number (e.g., 26.0 versus later point releases) and the minimum macOS version for a specific Xcode 26 point release were not verified against official Apple pages within this research — on this point: not verified.

## Minimum Target iOS Version

The official Apple Support page "iPhone models compatible with iOS 26" (opened via WebFetch) lists the models on which iOS 26 can be installed: iPhone 11, iPhone 11 Pro, iPhone 11 Pro Max, iPhone SE (2nd generation), iPhone 12 mini/12/12 Pro/12 Pro Max, iPhone 13 mini/13/13 Pro/13 Pro Max, iPhone SE (3rd generation), iPhone 14/14 Plus/14 Pro/14 Pro Max, iPhone 15/15 Plus/15 Pro/15 Pro Max, iPhone 16/16 Plus/16 Pro/16 Pro Max/16e, iPhone 17/17 Pro/17 Pro Max, iPhone Air, iPhone 17e. ([Apple Support — iPhone models compatible with iOS 26](https://support.apple.com/en-us/guide/iphone/iphe3fa5df43/ios))

Compared to the iOS 18 compatibility list, iPhone XR, iPhone XS, and iPhone XS Max have dropped out of this set — this cutoff is confirmed by a secondary source (TechRadar), which compared the iOS 18 and iOS 26 compatibility lists; this comparison does not follow directly from the official Apple page itself, since it only lists the current lineup for iOS 26. ([TechRadar — iOS 26 and iPadOS 26 compatibility explained](https://www.techradar.com/phones/ios/ios-26-compatibility-does-your-iphone-support-it-heres-the-full-list-of-supported-devices))

Practical conclusion for the project: if the deployment target is set to iOS 26, the minimum supported device is the iPhone 11 / iPhone SE (2nd generation) and newer; older models (iPhone XR/XS and earlier) will not be able to run the game at all, regardless of the app's deployment target, because they will not receive the system update to iOS 26. The app's actual deployment target itself (i.e., the minimum installed iOS version the game will launch on, as distinct from the system's update ceiling) is not dictated by Apple — this is the developer's choice in Xcode/Unity Player Settings; no official requirement for a specific minimum deployment target for games was found in the sources researched — not verified.

## Apple Developer Program: Cost, Timelines, Agreements/Tax/Banking

Membership cost: "The Apple Developer Program annual fee is 99 USD and the Apple Developer Enterprise Program annual fee is 299 USD, in local currency where available." Enrollment requires an Apple Account with two-factor authentication, and being of legal age in the user's region; for an individual/sole proprietor, a real legal name must be used — a pseudonym or company name in the name fields delays approval. Fee waivers are available for non-profit organizations, accredited educational institutions, and government entities. ([Apple Developer — Enrollment](https://developer.apple.com/help/account/membership/program-enrollment/))

Banking details in App Store Connect: the page "Enter banking information" (opened via WebFetch) directly states — "After the Account Holder approves the change, it will be processed within 24 hours." Before that, it is mandatory to sign the Paid Apps Agreement and submit all required tax forms: "Note that in order to add banking information, you'll first need to sign a Paid Apps Agreement" and "You must submit all required tax forms needed for your paid contract in order for us to process banking information." If the details are added by an Admin or Finance role, separate Account Holder approval is required: "If you hold the Admin or Finance role and are trying to add banking information, the Account Holder will need to approve the information in App Store Connect before it's processed." There is also a deadline for confirmation: "If they reject the change or if it isn't approved within 30 days, your bank account details will not be updated." ([Apple Developer — Enter banking information](https://developer.apple.com/help/app-store-connect/manage-banking-information/enter-banking-information/))

The official 24 hours is the stated processing time for an already-approved change, not the time for the initial review of the account as a whole (opening the Paid Apps Agreement, tax forms, organization verification). Real timelines for the initial review, as described on Apple developer forums, are noticeably longer than the official estimate (from several days to a month), but these are user forum reports, not Apple's official position, so a specific figure cannot be given — not verified.

Regarding the timeline for the actual payout of money already earned (separate from approval of the banking details themselves), the App Store Connect payments page was not directly opened via WebFetch within this research — not verified.

## Privacy Manifest (PrivacyInfo.xcprivacy) and the Required Reason API

The official Apple pages on this topic (`developer.apple.com/documentation/bundleresources/privacy-manifest-files` and technote TN3183, `describing-use-of-required-reason-api`) are built as a JS application (Swift-DocC) and did not open via WebFetch — only the page title is returned, with no text. Therefore, the facts in this section rely on pages that could be opened: a reference write-up from the Bitrise blog and the official Unity page on Apple's privacy policy, which itself retells Apple's requirement for the purposes of integration into a Unity project.

Enforcement date: per the Bitrise write-up, which quotes Apple's official announcement verbatim — "Starting this date, new apps that don't describe their use of required reasons API in their privacy manifest file aren't accepted by App Store Connect" — the date is May 1, 2024. ([Bitrise — Enforcement of Apple Privacy Manifest starting from May 1, 2024](https://bitrise.io/blog/post/enforcement-of-apple-privacy-manifest-starting-from-may-1-2024))

Categories of the required reason API that require a stated reason for use in the manifest (per the same write-up): file timestamp APIs (File Timestamp — `creationDate`, `modificationDate`, `fileModificationDate`, `contentModificationDateKey`, `stat`, etc.), system boot time APIs (System Boot Time — `systemUptime`, `mach_absolute_time()`), disk space APIs (Disk Space — `volumeAvailableCapacityKey`, `volumeAvailableCapacityForImportantUsageKey`, `volumeAvailableCapacityForOpportunisticUsageKey`, `volumeTotalCapacityKey`), active keyboard API (Active Keyboard — `ActiveInputNodes`), user defaults API (`UserDefaults`). The full official list and the exact approved reason codes were not directly verified against Apple's documentation, since the page did not open via WebFetch — not verified; to check before release, consult the contents directly in Xcode 26 or try again to open `developer.apple.com/documentation/bundleresources/describing-use-of-required-reason-api` in a browser.

What this means for the Unity project: the official Unity Manual page "Apple's privacy manifest policy requirements" (opened via WebFetch) requires creating a manifest file and to "save it in the Assets/Plugins folder of your project" so that it ends up in the generated Xcode project. The same page directly states the division of responsibility: "if your application includes multiple third-party SDKs, packages, and plug-ins, then these third-party components (if applicable) must provision their own privacy manifest files separately" and "It's your responsibility however, to make sure that the owners of these third-party components include privacy manifest files. Unity isn't responsible for any third-party privacy manifest, and their data collection and tracking practices." A direct warning about the consequences: "If the use of the required reason APIs by you or third-party SDKs isn't declared in the privacy manifest, your application might be rejected by the App Store." ([Unity Manual — Apple's privacy manifest policy requirements](https://docs.unity3d.com/6000.0/Documentation/Manual/apple-privacy-manifest-policy.html))

In the Xcode project generated by Unity 6, the final (merged) `PrivacyInfo.xcprivacy` file for the Unity runtime, plugins, packages, and project code resides in the `UnityFramework` target/folder — per the official Unity page on Xcode project structure. ([Unity Manual — Structure of a Unity Xcode project](https://docs.unity3d.com/6000.2/Documentation/Manual/StructureOfXcodeProject.html))

Practical risk for the project: if third-party SDKs are integrated into the game (analytics, ads, payments, any native plugins), each of them is required to carry its own `PrivacyInfo.xcprivacy`; missing declarations from a third-party SDK, or a mismatch between an SDK's declaration and the actual merged manifest in `UnityFramework`, is a common cause of rejection when uploading to App Store Connect.

## App Privacy ("Nutrition Labels") for a Game with a Camera Photo

The official "App Privacy Details" page (opened via WebFetch) places a user's photos/videos under the User Content category and explicitly lists the data type "Photos or Videos — The user's photos or videos," which must be declared if the app collects users' photos or videos. ([Apple Developer — App Privacy Details](https://developer.apple.com/app-store/app-privacy-details/))

For a game that takes a photo with the camera, sends it to its own server, and does not store it:

- The "Photos or Videos" data type must be declared as collected, because the data leaves the device (it is transmitted to the server), regardless of whether the server stores it: no persistent storage does not mean no collection.
- "Whether the data is linked to the user's identity" — per the page's definition, "Data collected from an app is often linked to the user's identity, unless specific privacy protections are put in place before collection to de-identify or anonymize it, such as: stripping data of any direct identifiers... manipulating data to break the linkage." If the server receives the photo together with a user/device/session identifier that allows the photo to be linked to a specific person, the declaration must reflect the link to identity; if the photo is technically de-identified and cannot be linked back, it can be declared as not linked to identity, but only if both of the page's requirements are met: not attempting to link the data back to identity, and not cross-referencing it with other data sets that would allow that.
- "Whether the data is used for tracking" — per the definition on the same page, tracking means "linking data collected from your app about a particular end-user or device... with Third-Party Data for targeted advertising or advertising measurement purposes, or sharing data collected from your app about a particular end-user or device with a data broker." If the photo is used only for the game's own functionality (for example, recognizing an object as part of gameplay) and is not shared with third parties for ad targeting, this is not tracking under Apple's definition, and ATT is not required on this basis (see the ATT section below).
- Procedurally: answers are entered in App Store Connect and must stay current: "You're responsible for keeping your responses accurate and up to date. If your practices change, update your responses in App Store Connect. You may update your answers at any time, and you do not need to submit an app update in order to change your answers."

The resulting recommendation for this game's privacy card: declare "Photos or Videos" as collected data, separately answer the question about linkage to identity (depending on whether an identifier is sent along with the photo) and about tracking (most likely no, provided the photo is not sent to advertising/analytics third parties). Not storing the photo on the server does not exempt the app from declaring the fact of collecting and transmitting the data.

Additionally (per the same page) — if the project uses third-party SDKs (advertising, analytics), their own data collection must be declared separately: "If you use third-party code — such as advertising or analytics SDKs — you need to describe what data the third-party code collects, how the data may be used, and whether the data is used to track users." ([Apple Developer — App Store user privacy and data use](https://developer.apple.com/app-store/user-privacy-and-data-use/))

## Age Rating (New System) and Moderation of User-Generated Content

The new App Store Connect rating system: previously the values 4+/9+/12+/17+ were used; the updated system introduces a more granular scale — 4+, 9+, 13+, 16+, 18+. Developers had to answer the updated questionnaire with new mandatory questions (In-app controls, Capabilities, Medical or wellness topics, Violent themes) by January 31, 2026, otherwise submission of app updates to App Store Connect is blocked. This is based on the official Apple Developer news item and the reference page for age rating values found via search; the news page itself, with the exact text about the January 31 deadline, was not opened directly via WebFetch in this research — the figure and month are confirmed by several independent search results referencing developer.apple.com/news, but an exact verbatim quote from that specific page could not be obtained — flagged as partially verified. The broader text on the updated ratings overall can be seen on the official page of age rating values and definitions. ([Apple Developer — Age ratings values and definitions](https://developer.apple.com/help/app-store-connect/reference/app-information/age-ratings-values-and-definitions/))

A separate detail important specifically for a game with a child-oriented look that accepts user-submitted images: the Kids Category in the App Review Guidelines (section 1.3, text obtained via WebFetch of the guidelines page) requires that apps "must not include links out of the app, purchasing opportunities, or other distractions to kids unless reserved for a designated area behind a parental gate," and also "You must comply with applicable privacy laws around the world relating to the collection of data from children online... Kids Category apps may not send personally identifiable information or device information to third parties. Apps in the Kids Category should not include third-party analytics or third-party advertising." ([App Store Review Guidelines, section 1.3](https://developer.apple.com/app-store/review/guidelines/))

Even if the app is not formally submitted in the Kids Category, but looks child-oriented in its design and works with photos/user-generated content, the general requirement on collecting data about minors from guideline 5.1.1 applies: apps that "collect, transmit, or have the capability to share personal information (e.g. name, address, email, location, photos, videos, drawings...) from a minor must include a privacy policy and must comply with all applicable children's privacy statutes" — up to and including COPPA (US) and GDPR (EU) where applicable. This wording comes from a secondary source (iubenda / Privacy World) retelling the text of guideline 5.1.1 — a direct WebFetch of this specific wording from the official Apple page was not confirmed, so the verbatim quote is given with the note "per secondary source, needs verification."

Requirements for moderating user-generated content — guideline 1.2 (text confirmed via direct WebFetch of the guidelines page): "Apps with user-generated content or social networking services must include: a method for filtering objectionable material from being posted to the app; a mechanism to report offensive content and timely responses to concerns; the ability to block abusive users from the service; published contact information so users can easily reach you." Also: "Apps with user-generated content or services that end up being used primarily for pornographic content, Chatroulette-style experiences, random or anonymous chat, objectification of real people (e.g. "hot-or-not" voting), making physical threats, or bullying do not belong on the App Store and may be removed without notice." ([App Store Review Guidelines, section 1.2](https://developer.apple.com/app-store/review/guidelines/))

Practical conclusion: a game where players upload their own photos (even ones processed into game content) is required to implement a filter for objectionable content before or immediately after publication, a "report" button, user blocking, visible developer/support contact information, and a privacy policy linked both from App Store Connect and from within the app itself (guideline 5.1.1(i)).

## ATT (App Tracking Transparency)

The official "App Store user privacy and data use" page (opened via WebFetch again, focusing on the ATT section) gives the exact definition of when the request is mandatory:

> "iOS 14.5, iPadOS 14.5, and tvOS 14.5 or later: You must receive user permission through the AppTrackingTransparency framework to: track users across apps and websites owned by other companies; access the device's advertising identifier (IDFA)."

Definition of tracking on the same page:

> "Tracking is defined as: linking user or device data collected from your app with user or device data collected from other companies' apps, websites, or offline properties for targeted advertising or advertising measurement purposes; sharing user or device data with data brokers."

Examples of actions that require an ATT request: showing targeted advertising based on user data from other companies' apps/sites; passing geolocation or an email list to a data broker; passing email, advertising, or other identifiers to third-party ad networks for retargeting; using third-party SDKs that combine the app's user data with data from other apps for ad targeting or ad-effectiveness measurement.

Explicitly listed as NOT requiring an ATT request: data linked only on the device and not leaving it in identifiable form; use of data by a data broker solely for fraud detection/prevention or security purposes; use of data by credit reporting agencies for creditworthiness assessment.

On the wording of the usage description, the page does not name the `NSUserTrackingUsageDescription` key by name, but it does directly require explanatory text in the system prompt: "You must also include a purpose string in the system prompt that explains why you'd like to track the user," which should "explain what this data will be used for to help the user understand what they're opting in to share." ([Apple Developer — App Store user privacy and data use](https://developer.apple.com/app-store/user-privacy-and-data-use/))

Conclusion for a game with a camera photo sent to its own server without storage and without transmission to third parties for advertising: an ATT request is not required, since there is no linking of the user's data with data from third-party companies for advertising purposes, and there is no access to the IDFA for tracking purposes. If an ad SDK or analytics with retargeting is added to the project, ATT becomes mandatory regardless of how the photos are processed.

## TestFlight: Limits, Timelines, Build Lifespan

Internal testers — the official "Add internal testers" page (opened via WebFetch): "Create a group and add up to 100 internal testers (App Store Connect users with access to your content) to test your app using TestFlight." Role required to add testers: "Account Holder, Admin, App Manager, Developer, or Marketing." Also: "Internal testers can download and test all builds for 90 days." The official text of this page does not state a limit on the number of devices per tester — not confirmed in the review. ([Apple Developer — Add internal testers](https://developer.apple.com/help/app-store-connect/test-a-beta-version/add-internal-testers))

External testers — the official "Invite external testers" page (opened via WebFetch): "After uploading your build, you can invite up to 10,000 external testers per app." Role required: "Account Holder, Admin, or App Manager." Mandatory condition: "To create an external group for external testing, you must first create an internal group for internal testing." On Beta App Review, the same page confirms the review is mandatory: "After you submit your build to TestFlight App Review, Apple reviews the build and its accompanying metadata... If Apple rejects your build or metadata, the status of the build will be Rejected." Limit on the number of submissions: "You can submit up to six builds for TestFlight App Review within a 24-hour period." ([Apple Developer — Invite external testers](https://developer.apple.com/help/app-store-connect/test-a-beta-version/invite-external-testers))

The exact official review time (in hours) for the first build for external testers is not explicitly stated on the Apple pages opened via WebFetch — secondary sources (developer forums, blogs) typically cite around 24 hours for the first build of a new version, and changes to encryption export compliance, entitlements, or privacy nutrition labels trigger a full review again — these figures were not verified against an official Apple page, flagged as "per secondary sources, not officially verified."

Build lifespan in TestFlight: per secondary sources, a build becomes unavailable to testers 90 days after upload, and each new build gets its own countdown restarted; this is consistent with the officially quoted text above about internal testers ("test all builds for 90 days"), but a separate official page specifically about build expiration was not opened via WebFetch in this research — partially verified.

## Loot Boxes and Odds Disclosure

Guideline 3.1.1 (In-App Purchase), text confirmed via direct WebFetch of the App Review Guidelines page:

> "Apps offering "loot boxes" or other mechanisms that provide randomized virtual items for purchase must disclose the odds of receiving each type of item to customers prior to purchase."

([App Store Review Guidelines, section 3.1.1](https://developer.apple.com/app-store/review/guidelines/))

The requirement appeared in the rules in December 2017 (per secondary sources — TouchArcade, Fenwick, MacStories; the date was not verified against an official Apple changelog page in this research) and has remained part of the current Guidelines under the same number, 3.1.1, ever since. Practical requirement for the project: if the game has a mechanic for randomly awarding virtual items for paid currency or money (gacha, chests, random rewards for a purchase), the odds of receiving each type/rarity of item must be shown in the interface before the purchase.

## Related Guideline Points Concerning a Game with a Camera and User-Generated Content

For completeness (text confirmed via WebFetch of the guidelines page):

- Guideline 5.1.1(i) — a mandatory link to the privacy policy both in App Store Connect and within the app, describing what data is collected, how and with whom it is shared, and how to delete it.
- Guideline 5.1.1(iii) (Data Minimization) — "Apps should only request access to data relevant to the core functionality of the app and should only collect and use data that is required to accomplish the relevant task. Where possible, use the out-of-process picker or a share sheet rather than requesting full access to protected resources like Photos or Contacts." For a game with a camera photo, this means: use the system UIImagePicker/camera sheet where possible, rather than requesting full access to the photo library, unless you need to store and re-read photos from the gallery.
- Guideline 5.1.1(iv) (Access) — "Apps must respect the user's permission settings and not attempt to manipulate, trick, or force people to consent to unnecessary data access."
- Guideline 5.1.2 (Data Use and Sharing) — "Unless otherwise permitted by law, you may not use, transmit, or share someone's personal data without first obtaining their permission. You must provide access to information about how and where the data will be used. You must clearly disclose where personal data will be shared with third parties, including with third-party AI, and obtain explicit permission before doing so." This directly concerns the "photo is sent to the server" scenario: the developer must explicitly warn the user and obtain consent before sending the photo to the server.

## Sources

- [Apple Developer — Upcoming SDK Minimum Requirements](https://developer.apple.com/news/?id=ueeok6yw)
- [Apple Developer — App Store submissions now open for the latest OS releases](https://developer.apple.com/news/?id=6lxhtioi)
- [Apple Support — iPhone models compatible with iOS 26](https://support.apple.com/en-us/guide/iphone/iphe3fa5df43/ios)
- [TechRadar — iOS 26 and iPadOS 26 compatibility explained](https://www.techradar.com/phones/ios/ios-26-compatibility-does-your-iphone-support-it-heres-the-full-list-of-supported-devices)
- [Apple Developer — Enrollment (Apple Developer Program)](https://developer.apple.com/help/account/membership/program-enrollment/)
- [Apple Developer — Enter banking information](https://developer.apple.com/help/app-store-connect/manage-banking-information/enter-banking-information/)
- [Bitrise — Enforcement of Apple Privacy Manifest starting from May 1, 2024](https://bitrise.io/blog/post/enforcement-of-apple-privacy-manifest-starting-from-may-1-2024)
- [Capgo — Privacy Manifest for iOS Apps](https://capgo.app/blog/privacy-manifest-for-ios-apps/)
- [Unity Manual — Apple's privacy manifest policy requirements (6000.0)](https://docs.unity3d.com/6000.0/Documentation/Manual/apple-privacy-manifest-policy.html)
- [Unity Manual — Structure of a Unity Xcode project (6000.2)](https://docs.unity3d.com/6000.2/Documentation/Manual/StructureOfXcodeProject.html)
- [Apple Developer — App Privacy Details](https://developer.apple.com/app-store/app-privacy-details/)
- [Apple Developer — App Store user privacy and data use](https://developer.apple.com/app-store/user-privacy-and-data-use/)
- [Apple Developer — Age ratings values and definitions](https://developer.apple.com/help/app-store-connect/reference/app-information/age-ratings-values-and-definitions/)
- [Apple Developer — App Review Guidelines](https://developer.apple.com/app-store/review/guidelines/)
- [Apple Developer — Add internal testers](https://developer.apple.com/help/app-store-connect/test-a-beta-version/add-internal-testers)
- [Apple Developer — Invite external testers](https://developer.apple.com/help/app-store-connect/test-a-beta-version/invite-external-testers)

Pages that could not be opened with substantive content via WebFetch (returned only the page title due to Swift-DocC JS rendering, or a 404/access error) and were therefore not used as a direct source of quotes, only as context from secondary sources: `developer.apple.com/documentation/bundleresources/privacy-manifest-files`, `developer.apple.com/documentation/technotes/tn3183-adding-required-reason-api-entries-to-your-privacy-manifest`, `developer.apple.com/documentation/bundleresources/describing-use-of-required-reason-api`, `developer.apple.com/documentation/apptrackingtransparency`, `help.apple.com/app-store-connect/en.lproj/dev388fa3577.html` (404).
