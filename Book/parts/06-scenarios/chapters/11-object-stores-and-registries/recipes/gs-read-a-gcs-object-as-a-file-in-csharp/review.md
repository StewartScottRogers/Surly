<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: gs-read-a-gcs-object-as-a-file-in-csharp
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): Surl 5da5b3eaf, Curl c84035f797: 12 of 12 cases pass on windows.
## Findings
1. Program finding resolved: with Curl c84035f797 a `HttpMethod.Put` with a `Body` to `gs://` works, with HMAC (`UserName`/`Password`) and with `XOAuth2Bearer`, answered 200. gcs.md updated.
2. Over `https://` the login method must be named: `HttpAuthenticationMethods.Basic` or `Bearer`.
