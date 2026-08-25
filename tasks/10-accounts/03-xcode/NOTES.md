Source: `cat-shelter-tasks.md` lines 407-447, and `knowledge/ios/01-appstore-requirements-2026.md`.

This requirement reads stricter than earlier project notes. Apple's own
wording: "Starting April 28, 2026, apps and games uploaded to App Store
Connect need to meet the following minimum requirements: iOS and iPadOS
apps must be built with the iOS 26 & iPadOS 26 SDK or later." That date has
passed. Xcode 16 no longer produces an uploadable build.

This constrains the *build tool*, not the deployment target: iOS 15+ as the
game's minimum runtime is a separate, still-valid choice and is unaffected.

Status is `done` per AGENT-BRIEF.md's environment table (verified
25 August 2026): Xcode 26.3 (build 17C529), `xcode-select` pointing at
`/Applications/Xcode.app/Contents/Developer`, iOS SDK and simulator 26.2.
`verify` is left `pending` rather than `passed` because AGENT-BRIEF records
the resulting version numbers but not a specific command run to obtain
them (unlike 04-unity, where the entitlements-check command is given
verbatim). A third party should re-run the commands in VERIFY above and
attach a VERIFY.md before flipping this to `passed`.
