<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-find-the-request-that-failed-in-csharp
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port 8.22.0 (Curl commit c84035f797), Surl.Api 1.0.0, Curl.Api 0.2.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0, dotnet 11.0.100: 4 of 4 cases pass on windows.
## Findings
1. Narrows `SurlExchangeRecord.Chunks` (one received chunk per request head, one sent chunk per answer head here) and takes the reason from the verbose Log line of the same request.
