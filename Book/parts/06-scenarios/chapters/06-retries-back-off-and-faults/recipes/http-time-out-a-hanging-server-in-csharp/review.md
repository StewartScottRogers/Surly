<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-time-out-a-hanging-server-in-csharp
Verdict: drafted, ready for review
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 5 of 5 cases pass on windows.
## Findings
1. A cancelled CancellationToken surfaces as OperationCanceledException, not CurlException 28; claimed as such.
2. Times=1 so the retry scenario is served; the timeout scenarios hold only the first request.
Re-pinned to Surl 9ad60d80c / Curl 7d167fa10a: under load the timed lines read "after 5 s" because the stopwatch held the first Curl.Api request's cold start (4 s even on an idle machine), not a Curl regression; the program now makes one untimed fetch first and the hold is ten seconds, five times the two-second limit.
