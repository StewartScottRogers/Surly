<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-refuse-an-oversized-request-head-in-csharp
Verdict: drafted, ready for review
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 4 of 4 cases pass on windows.
## Findings
1. SurlServerOptions has no property for --max-request-head; the program uses AdditionalArguments. Program note for the author: a MaxRequestHead property would match MaxFileSize.
2. Same RFC 6585 gap as the console recipe.
