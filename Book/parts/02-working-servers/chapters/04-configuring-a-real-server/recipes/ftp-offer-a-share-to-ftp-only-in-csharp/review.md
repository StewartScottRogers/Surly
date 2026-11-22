<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ftp-offer-a-share-to-ftp-only-in-csharp
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port (Curl commit c84035f797): 8 cases pass on windows.
## Findings
1. `SurlServerOptions.Shares` takes the flag text after `;`; `SurlServerStartException.Lines[0]` is `option --share: unknown scheme nope`.
2. Curl.Api returns the HTTP 404 as a response.
