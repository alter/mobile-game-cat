Verifier: environment check recorded in AGENT-BRIEF.md, dated 2026-08-25.
Did not install Unity, did not write the licence, did not run any build in
this session - this records how the existing installation and licence were
confirmed, not the act of installing them.

## How to reproduce

From a clean shell, no exported variables:

```bash
"/Applications/Unity/Hub/Editor/6000.3.22f1/Unity.app/Contents/Helpers/\
UnityLicensingClient.app/Contents/MacOS/Unity.Licensing.Client" --showEntitlements
```

Expect the output to list `com.unity.editor.headless` and
`com.unity.editor.platforms.ios` among the granted entitlements.

AGENT-BRIEF.md explicitly warns against two false positives: checking
`~/Library/...` for the licence file (it lives at
`/Library/Application Support/Unity/Unity_lic.ulf`, machine-wide, not
per-user) and treating "Unity started in batch mode" as proof of
entitlement (it only proves rights exist for that one action, not which
ones or until when).

## What was not checked

- The licence's expiry date was not read off in this check.
- No actual iOS build was run in this session to confirm the
  `com.unity.editor.platforms.ios` entitlement produces a working IPA end
  to end - only the entitlement string was confirmed present.
- AndroidPlayer and WebGLSupport modules are listed as installed in
  AGENT-BRIEF.md but are out of scope for this task and were not
  re-verified here.
- Iteration cost (project open/build time) was measured only on an empty
  project per AGENT-BRIEF.md; not re-measured with the actual game project.
