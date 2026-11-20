<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-accept-basic-only-over-https-in-csharp
Verdict: accept with fixes
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 6 of 6 cases pass on windows.
## Findings
1. One program prints six lines (two listeners, three logins each); the cases match lines of it. The server's own certificate is trusted through `SelfSignedCertificatePem`, so verification stays on.
2. Not carried over from the console twin: the Digest refusal and the default-offers-Digest comparison (a second server and a second program run for little gain here).
3. `SurlServerOptions.AuthenticationMethods` is a null `IList<string>` that a collection initializer cannot fill (NullReferenceException); it is assigned with a collection expression instead. Worth a default empty list in Surl.Api.
4. The start-up warning is cited to the README, not cased.
